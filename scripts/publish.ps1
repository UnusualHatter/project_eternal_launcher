# publish.ps1 — Build a release-ready output for Inno Setup
#
# Usage:
#   scripts\publish.ps1               # builds current version
#   scripts\publish.ps1 -Version 1.2.0
#
# Output lands in installer\publish\

param(
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"

$repoRoot   = Resolve-Path (Join-Path $PSScriptRoot "..")
$projectPath = Join-Path $repoRoot "src\LauncherTF2\LauncherTF2.csproj"
$outDir      = Join-Path $repoRoot "installer\publish"

# Wipe previous publish output so stale files don't creep in.
if (Test-Path $outDir) {
    Remove-Item $outDir -Recurse -Force
}

$publishArgs = @(
    "publish", $projectPath,
    "-c", "Release",
    "-r", "win-x64",
    "--self-contained", "false",
    "-p:PublishSingleFile=true",
    # IncludeAllContentForSelfExtract is intentionally NOT set — it would
    # bundle the native patchers inside the exe and extract them to a temp
    # directory at runtime, breaking their on-disk invocation paths.
    "-p:DebugType=none",          # strip PDB from publish output
    "-p:DebugSymbols=false",
    "-o", $outDir
)

if ($Version -ne "") {
    $publishArgs += "-p:Version=$Version"
    $publishArgs += "-p:AssemblyVersion=$Version"
    $publishArgs += "-p:FileVersion=$Version"
}

Write-Host "Publishing to $outDir ..." -ForegroundColor Cyan
& dotnet @publishArgs

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed (exit $LASTEXITCODE)"
}

# Sanity check — these must be present.
$required = @("LauncherTF2.exe", "steam_patcher.exe", "pure_patcher.exe")
foreach ($f in $required) {
    $full = Join-Path $outDir $f
    if (-not (Test-Path $full)) {
        throw "Expected file missing from publish output: $f"
    }
}

Write-Host ""
Write-Host "Publish succeeded." -ForegroundColor Green
Write-Host "Output: $outDir" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. (Optional) Sign the binaries in installer\publish\" -ForegroundColor Yellow
Write-Host "  2. Open installer\installer.iss in Inno Setup Compiler" -ForegroundColor Yellow
Write-Host "     or run:  iscc installer\installer.iss" -ForegroundColor Yellow
Write-Host "  3. (Optional) Sign the output .exe in installer\Output\" -ForegroundColor Yellow
