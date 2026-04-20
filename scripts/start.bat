@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
set "REPO_ROOT=%SCRIPT_DIR%.."
set "AGG_PROJECT=%REPO_ROOT%\PricingAggregator\PricingAggregator.csproj"
set "AGG_URL=http://localhost:5204/api/prices"

cd /d "%REPO_ROOT%"

echo [start] Configurando ambiente de teste...
set "TF2_PRICING_AGGREGATOR_URL=%AGG_URL%"

if exist "%AGG_PROJECT%" (
	echo [start] Iniciando PricingAggregator em nova janela...
	start "PricingAggregator" /D "%REPO_ROOT%" cmd /k "dotnet run --project PricingAggregator\PricingAggregator.csproj --no-build"
) else (
	echo [start] PricingAggregator nao encontrado. Prosseguindo sem backend de precos.
)

call "%SCRIPT_DIR%run.bat"
