# TemplateDesigner publish skript
# Vygeneruje single-file .exe do C:\Edison\ (cesta z publish profilu).
#
# Použití (z rootu TemplateDesigner/):
#   .\publish.ps1
#
# Skript volá `dotnet publish` s profilem "SingleFile C-Edison".
# Po dokončení vypíše cestu, velikost a čas vytvořeného .exe.

#requires -Version 5.1
$ErrorActionPreference = 'Stop'

$projectDir = Join-Path $PSScriptRoot 'dotnet'
$csproj     = Join-Path $projectDir 'TemplateDesigner.csproj'
$targetExe  = 'C:\Edison\TemplateDesigner.exe'

if (-not (Test-Path $csproj)) {
    Write-Error "Nenalezen TemplateDesigner.csproj v $projectDir"
    exit 1
}

# Pokud je v C:\Edison\ stará verze, ověř, že není uzamčená běžícím procesem.
if (Test-Path $targetExe) {
    try {
        [System.IO.File]::Open($targetExe, 'Open', 'ReadWrite', 'None').Close()
    } catch {
        Write-Error "Soubor '$targetExe' je uzamčený (pravděpodobně běží TemplateDesigner.exe). Zavři aplikaci a spusť znovu."
        exit 1
    }
}

Write-Host "Publishing TemplateDesigner -> C:\Edison\ ..." -ForegroundColor Cyan
Push-Location $projectDir
try {
    & dotnet publish $csproj /p:PublishProfile="SingleFile C-Edison" -v:minimal
    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet publish selhal (exit code $LASTEXITCODE)."
        exit $LASTEXITCODE
    }
} finally {
    Pop-Location
}

if (-not (Test-Path $targetExe)) {
    Write-Warning "Publish nahlásil úspěch, ale $targetExe neexistuje. Zkontroluj výstup výše."
    exit 1
}

$info   = Get-Item $targetExe
$sizeMb = [math]::Round($info.Length / 1MB, 1)

Write-Host ""
Write-Host "Hotovo." -ForegroundColor Green
Write-Host "  Soubor:    $targetExe"
Write-Host "  Velikost:  $sizeMb MB"
Write-Host "  Vytvoreno: $($info.LastWriteTime)"
