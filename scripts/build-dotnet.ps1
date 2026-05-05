$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$buildingBlockSolutions = Get-ChildItem -Path (Join-Path $repoRoot "libs") -Recurse -Filter "*.sln" |
  Where-Object { $_.FullName -notmatch "\\(bin|obj)\\" } |
  Sort-Object FullName

$serviceApiProjects = Get-ChildItem -Path (Join-Path $repoRoot "apps") -Recurse -Filter "*.API.csproj" |
  Where-Object { $_.FullName -notmatch "\\(bin|obj)\\" } |
  Sort-Object FullName

$buildTargets = @()
$buildTargets += $buildingBlockSolutions
$buildTargets += $serviceApiProjects

if ($buildTargets.Count -eq 0) {
  Write-Error "No .NET build targets were found under apps/ or libs/."
}

foreach ($target in $buildTargets) {
  Write-Host "Building $($target.FullName)"
  $output = & dotnet build $target.FullName --configuration Release --nologo --disable-build-servers -maxcpucount:1 /p:UseSharedCompilation=false 2>&1
  $exitCode = $LASTEXITCODE

  $output | ForEach-Object { Write-Host $_ }

  if ($exitCode -ne 0 -or ($output -match "Build FAILED")) {
    throw "dotnet build failed for $($target.FullName)"
  }
}
