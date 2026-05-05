$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$searchRoots = @(
  Join-Path $repoRoot "apps"
  Join-Path $repoRoot "libs"
)

$solutions = Get-ChildItem -Path $searchRoots -Recurse -Filter "*.sln" |
  Where-Object { $_.FullName -notmatch "\\(bin|obj)\\" } |
  Sort-Object FullName

if ($solutions.Count -eq 0) {
  Write-Error "No .NET solution files were found under apps/ or libs/."
}

foreach ($solution in $solutions) {
  Write-Host "Building $($solution.FullName)"
  $output = & dotnet build $solution.FullName --configuration Release --nologo --disable-build-servers -maxcpucount:1 /p:UseSharedCompilation=false 2>&1
  $exitCode = $LASTEXITCODE

  $output | ForEach-Object { Write-Host $_ }

  if ($exitCode -ne 0 -or ($output -match "Build FAILED")) {
    throw "dotnet build failed for $($solution.FullName)"
  }
}
