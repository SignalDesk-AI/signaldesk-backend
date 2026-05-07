param(
    [switch]$SkipUp,
    [switch]$CheckServices
)

$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$LocalCompose = Join-Path $RepoRoot "infra\docker\docker-compose.local.yml"
$ObservabilityCompose = Join-Path $RepoRoot "infra\docker\docker-compose.observability.yml"
$Failures = New-Object System.Collections.Generic.List[string]
$ServiceReadinessUrls = @(
    @{ Name = "gateway-bff"; Uri = "http://localhost:3000/health/ready" },
    @{ Name = "notification-service"; Uri = "http://localhost:3001/health/ready" },
    @{ Name = "search-service"; Uri = "http://localhost:3002/health/ready" },
    @{ Name = "ai-service"; Uri = "http://localhost:3003/health/ready" },
    @{ Name = "identity-service"; Uri = "http://localhost:5001/health/ready" },
    @{ Name = "workspace-service"; Uri = "http://localhost:5002/health/ready" },
    @{ Name = "support-service"; Uri = "http://localhost:5003/health/ready" },
    @{ Name = "knowledge-service"; Uri = "http://localhost:5004/health/ready" },
    @{ Name = "campaign-service"; Uri = "http://localhost:5005/health/ready" }
)

function Invoke-Check {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][scriptblock]$Action
    )

    try {
        & $Action
        Write-Host "[PASS] $Name" -ForegroundColor Green
    }
    catch {
        $Failures.Add("$Name - $($_.Exception.Message)")
        Write-Host "[FAIL] $Name" -ForegroundColor Red
        Write-Host "       $($_.Exception.Message)" -ForegroundColor DarkRed
    }
}

function Assert-LastExitCode {
    param([string]$CommandName)

    if ($LASTEXITCODE -ne 0) {
        throw "$CommandName failed with exit code $LASTEXITCODE"
    }
}

function Assert-ContainsAll {
    param(
        [string[]]$Actual,
        [string[]]$Expected,
        [string]$Label
    )

    foreach ($item in $Expected) {
        if ($Actual -notcontains $item) {
            throw "$Label missing '$item'"
        }
    }
}

function Invoke-HttpOk {
    param(
        [Parameter(Mandatory = $true)][string]$Uri,
        [hashtable]$Headers = @{},
        [int]$Attempts = 5,
        [int]$DelaySeconds = 3
    )

    $lastError = $null

    for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $Uri -Headers $Headers -TimeoutSec 15
            if ($response.StatusCode -lt 200 -or $response.StatusCode -ge 300) {
                throw "HTTP $($response.StatusCode)"
            }

            return
        }
        catch {
            $lastError = $_
            if ($attempt -lt $Attempts) {
                Start-Sleep -Seconds $DelaySeconds
            }
        }
    }

    throw $lastError
}

function Assert-LokiHasLogs {
    param(
        [Parameter(Mandatory = $true)][string]$Query,
        [int]$Attempts = 8,
        [int]$DelaySeconds = 2
    )

    $encodedQuery = [uri]::EscapeDataString($Query)
    $lastError = $null

    for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
        try {
            $response = Invoke-RestMethod -UseBasicParsing -Uri "http://localhost:3100/loki/api/v1/query_range?query=$encodedQuery&limit=1" -TimeoutSec 15
            if (@($response.data.result).Count -gt 0) {
                return
            }

            throw "No matching Loki streams yet"
        }
        catch {
            $lastError = $_
            if ($attempt -lt $Attempts) {
                Start-Sleep -Seconds $DelaySeconds
            }
        }
    }

    throw $lastError
}

function Get-BasicAuthHeader {
    param(
        [Parameter(Mandatory = $true)][string]$Username,
        [Parameter(Mandatory = $true)][string]$Password
    )

    $pair = "$Username`:$Password"
    $encoded = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes($pair))
    return @{ Authorization = "Basic $encoded" }
}

Push-Location $RepoRoot
try {
    Invoke-Check "Docker Compose local config is valid" {
        & docker compose -f $LocalCompose config --quiet
        Assert-LastExitCode "docker compose local config"
    }

    Invoke-Check "Docker Compose observability config is valid" {
        & docker compose -f $ObservabilityCompose config --quiet
        Assert-LastExitCode "docker compose observability config"
    }

    if (-not $SkipUp) {
        Invoke-Check "Start local infrastructure" {
            & docker compose -f $LocalCompose up -d
            Assert-LastExitCode "docker compose local up"
        }

        Invoke-Check "Start observability infrastructure" {
            & docker compose -f $ObservabilityCompose up -d
            Assert-LastExitCode "docker compose observability up"
        }
    }

    Invoke-Check "PostgreSQL schemas exist" {
        $schemas = & docker exec signaldesk-postgres psql -U signaldesk -d signaldesk -Atc "select schema_name from information_schema.schemata where schema_name in ('identity','workspace','support','knowledge','campaign','ops') order by schema_name;"
        Assert-LastExitCode "postgres schema query"
        Assert-ContainsAll $schemas @("campaign", "identity", "knowledge", "ops", "support", "workspace") "PostgreSQL schema"
    }

    Invoke-Check "ops baseline tables exist" {
        $tables = & docker exec signaldesk-postgres psql -U signaldesk -d signaldesk -Atc "select table_name from information_schema.tables where table_schema='ops' order by table_name;"
        Assert-LastExitCode "postgres ops table query"
        Assert-ContainsAll $tables @("audit_logs", "dead_letters", "idempotency_keys", "inbox_messages", "outbox_events") "ops table"
    }

    Invoke-Check "ops inbox retention indexes exist" {
        $indexes = & docker exec signaldesk-postgres psql -U signaldesk -d signaldesk -Atc "select indexname from pg_indexes where schemaname='ops' and tablename='inbox_messages' order by indexname;"
        Assert-LastExitCode "postgres inbox index query"
        Assert-ContainsAll $indexes @("ix_inbox_processing_stale", "ix_inbox_retention", "ix_inbox_processed_lookup") "ops inbox index"
    }

    Invoke-Check "PgBouncer accepts SQL" {
        $result = & docker exec signaldesk-pgbouncer sh -c "PGPASSWORD=signaldesk psql -h 127.0.0.1 -p 5432 -U signaldesk -d signaldesk -Atc 'select 1'"
        Assert-LastExitCode "pgbouncer select"
        if ($result -ne "1") {
            throw "Expected 1, got '$result'"
        }
    }

    Invoke-Check "Redis responds" {
        $result = & docker exec signaldesk-redis redis-cli ping
        Assert-LastExitCode "redis ping"
        if ($result -ne "PONG") {
            throw "Expected PONG, got '$result'"
        }
    }

    Invoke-Check "MongoDB responds" {
        $result = & docker exec signaldesk-mongodb mongosh --quiet --eval "db.runCommand({ ping: 1 }).ok"
        Assert-LastExitCode "mongodb ping"
        if ($result -ne "1") {
            throw "Expected 1, got '$result'"
        }
    }

    Invoke-Check "RabbitMQ queues exist" {
        $queues = & docker exec signaldesk-rabbitmq rabbitmqctl -q list_queues name
        Assert-LastExitCode "rabbitmq list queues"
        Assert-ContainsAll $queues @(
            "notification.events",
            "notification.events.retry",
            "notification.events.dlq",
            "search.indexing",
            "search.indexing.retry",
            "search.indexing.dlq",
            "ai.tasks",
            "ai.tasks.retry",
            "ai.tasks.dlq",
            "campaign.events",
            "campaign.events.retry",
            "campaign.events.dlq"
        ) "RabbitMQ queue"
    }

    Invoke-Check "RabbitMQ exchanges exist" {
        $exchanges = & docker exec signaldesk-rabbitmq rabbitmqctl -q list_exchanges name
        Assert-LastExitCode "rabbitmq list exchanges"
        Assert-ContainsAll $exchanges @("signaldesk.domain-events", "signaldesk.domain-events.dlx") "RabbitMQ exchange"
    }

    Invoke-Check "RabbitMQ management API responds" {
        Invoke-HttpOk -Uri "http://localhost:15672/api/overview" -Headers (Get-BasicAuthHeader -Username "signaldesk" -Password "signaldesk")
    }

    Invoke-Check "Elasticsearch responds" {
        Invoke-HttpOk -Uri "http://localhost:9200"
    }

    Invoke-Check "Mailpit UI responds" {
        Invoke-HttpOk -Uri "http://localhost:8025"
    }

    Invoke-Check "Ollama responds" {
        Invoke-HttpOk -Uri "http://localhost:11434/api/tags"
    }

    Invoke-Check "Prometheus responds" {
        Invoke-HttpOk -Uri "http://localhost:9090/-/ready"
    }

    Invoke-Check "Grafana responds" {
        Invoke-HttpOk -Uri "http://localhost:3009/api/health"
    }

    Invoke-Check "Jaeger responds" {
        Invoke-HttpOk -Uri "http://localhost:16686"
    }

    Invoke-Check "Loki responds" {
        Invoke-HttpOk -Uri "http://localhost:3100/ready" -Attempts 8 -DelaySeconds 3
    }

    Invoke-Check "Promtail can read Docker container logs" {
        $logPath = & docker exec signaldesk-promtail sh -c "find /var/lib/docker/containers -name '*-json.log' -type f | head -n 1"
        Assert-LastExitCode "promtail docker log lookup"
        if ([string]::IsNullOrWhiteSpace($logPath)) {
            throw "No Docker json log files visible to Promtail"
        }
    }

    Invoke-Check "Loki receives Promtail Docker logs" {
        Assert-LokiHasLogs -Query '{container="signaldesk-promtail"}'
    }

    if ($CheckServices) {
        foreach ($service in $ServiceReadinessUrls) {
            Invoke-Check "$($service.Name) readiness responds" {
                Invoke-HttpOk -Uri $service.Uri -Attempts 3 -DelaySeconds 2
            }
        }
    }
    else {
        Write-Host "[SKIP] Backend service readiness checks. Run with -CheckServices after starting the apps." -ForegroundColor Yellow
    }
}
finally {
    Pop-Location
}

if ($Failures.Count -gt 0) {
    Write-Host ""
    Write-Host "Smoke test failed:" -ForegroundColor Red
    foreach ($failure in $Failures) {
        Write-Host "- $failure" -ForegroundColor Red
    }
    exit 1
}

Write-Host ""
Write-Host "Day 2 smoke test passed." -ForegroundColor Green
