$ErrorActionPreference = "Stop"

Write-Host "🔨 Building Project Eternal Launcher..." -ForegroundColor Cyan

$projectPath = Join-Path $PSScriptRoot "..\src\LauncherTF2\LauncherTF2.csproj"

if (-not (Test-Path $projectPath)) {
    Write-Error "Project file not found at $projectPath"
}

dotnet build $projectPath -c Debug

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Build Success!" -ForegroundColor Green
} else {
    Write-Host "❌ Build Failed!" -ForegroundColor Red
    exit 1
}
