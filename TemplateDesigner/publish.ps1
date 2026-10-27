# TemplateDesigner publish skript
# Vygeneruje single-file .exe + volnou EDControls.dll do C:\Edison\ (cesta z publish profilu).
#
# Použití (z rootu TemplateDesigner/):
#   .\publish.ps1
#
# Skript volá `dotnet publish` s profilem "SingleFileCEdison".
# Po dokončení vypíše cestu, velikost a čas vytvořených souborů.

#requires -Version 5.1
$ErrorActionPreference = 'Stop'

$projectDir = Join-Path $PSScriptRoot 'dotnet'
$csproj     = Join-Path $projectDir 'TemplateDesigner.csproj'
$targetExe  = 'C:\Edison\TemplateDesigner.exe'
# EDControls.dll je samostatný výstupní soubor (sdílená s EDISONMAIN2, viz root CLAUDE.md).
$targetDll  = 'C:\Edison\EDControls.dll'
$targets    = @($targetExe, $targetDll)

if (-not (Test-Path $csproj)) {
    Write-Error "Nenalezen TemplateDesigner.csproj v $projectDir"
    exit 1
}

# Ověř, že žádný cílový soubor nedrží běžící proces. EDControls.dll zamyká KAŽDÁ m-line
# aplikace, která ji načetla - tedy i EDISONMAIN2, ne jen TemplateDesigner.
foreach ($t in $targets) {
    if (-not (Test-Path $t)) { continue }
    try {
        [System.IO.File]::Open($t, 'Open', 'ReadWrite', 'None').Close()
    } catch {
        Write-Error "Soubor '$t' je uzamčený běžícím procesem. Zavři TemplateDesigner i EDISONMAIN2 a spusť znovu."
        exit 1
    }
}

Write-Host "Publishing TemplateDesigner -> C:\Edison\ ..." -ForegroundColor Cyan
Push-Location $projectDir
try {
    & dotnet publish $csproj /p:PublishProfile=SingleFileCEdison -v:minimal
    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet publish selhal (exit code $LASTEXITCODE)."
        exit $LASTEXITCODE
    }
} finally {
    Pop-Location
}

$missing = $targets | Where-Object { -not (Test-Path $_) }
if ($missing) {
    Write-Warning "Publish nahlásil úspěch, ale chybí: $($missing -join ', '). Zkontroluj výstup výše."
    exit 1
}

Write-Host ""
Write-Host "Hotovo." -ForegroundColor Green
foreach ($t in $targets) {
    $info = Get-Item $t
    Write-Host ("  {0,-28} {1,6} MB   {2}" -f $info.Name, [math]::Round($info.Length / 1MB, 1), $info.LastWriteTime)
}
Write-Host "  Nasazovat se musi OBOJI (EDControls.dll je sdilena s EDISONMAIN2)." -ForegroundColor Yellow
