$ErrorActionPreference = "Stop"

$projectPath = Join-Path $PSScriptRoot "..\src\LauncherTF2\LauncherTF2.csproj"

dotnet build $projectPath -c Debug