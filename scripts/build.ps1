$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$solutionPath = Join-Path $repoRoot "project_eternal_launcher-main.sln"

if (-not (Test-Path $solutionPath)) {
	throw "Solution file not found: $solutionPath"
}

Write-Host "Building solution in Debug mode..." -ForegroundColor Cyan
dotnet build $solutionPath -c Debug

if ($LASTEXITCODE -ne 0) {
	throw "dotnet build failed with exit code $LASTEXITCODE"
}

Write-Host "Build finished successfully." -ForegroundColor Green