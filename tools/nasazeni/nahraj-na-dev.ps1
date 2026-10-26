# Nahraje schvalene soubory na DEV share.
#
# Pred kopii overi, ze kazdy soubor je ten z buildu (MD5 ze sestaveni-*.json) a ze soubor na DEV share je
# porad ten, ktery ukazala schvalena tabulka (seznam-dev-*.json). Kdyz ho mezitim nekdo prepsal (kolega
# nahral vlastni build), zastavi se driv, nez cokoli zkopiruje. Puvodni soubory i stav.json zazalohuje. Kdyz
# kopie selze uprostred, vrati uz nahrane soubory. -Obnov vrati DEV share i zaznam DEV ve stav.json do stavu
# pred nasazenim. Nahrane MD5 zapise do stav.json. Commity posledniho nasazeni posune jen tehdy, kdyz na DEV
# share je vse, co seznam ukazal jako potrebne (i soubor, ktery tam uz lezel se stejnym MD5), jinak by
# nenasazena zmena z pristi inventury zmizela. Bez -Soubory nic nekopiruje a jen posune commity (nasazeni
# jen trid Cache, ty na DEV nasazuje hook).
#
#   powershell -NoProfile -File tools/nasazeni/nahraj-na-dev.ps1 -Sestaveni <sestaveni.json> -Seznam <seznam-dev.json> -Soubory a.exe,b.dll
#   powershell -NoProfile -File tools/nasazeni/nahraj-na-dev.ps1 -Obnov <slozka zalohy>
#
# ASCII-only zamerne: WinPS 5.1 cte UTF-8 bez BOM jako CP-1252.

[CmdletBinding(DefaultParameterSetName = 'Kopie')]
param(
    [Parameter(ParameterSetName = 'Kopie', Mandatory)] [string]$Sestaveni,
    [Parameter(ParameterSetName = 'Kopie', Mandatory)] [string]$Seznam,
    [Parameter(ParameterSetName = 'Kopie')] [string[]]$Soubory = @(),
    [Parameter(ParameterSetName = 'Obnov', Mandatory)] [string]$Obnov,
    [string]$Cil,
    [string]$Nastaveni
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'spolecne.psm1') -Force
$k = Get-Nastaveni $Nastaveni
$cilSlozka = if ($Cil) { $Cil } else { $k.devShare }
$utf8 = New-Object Text.UTF8Encoding($false)

# Zaloha: puvodni soubory ve slozce "soubory", popis.json (co se nahralo s jakym MD5 a ktere soubory predtim
# nebyly) a kopie stav.json. -Obnov podle ni vrati DEV share i stav.json do stavu pred nasazenim.
function Restore-Zaloha([string]$Zaloha, [string[]]$Jen) {
    $popis = [IO.File]::ReadAllText((Join-Path $Zaloha 'popis.json'), $utf8) | ConvertFrom-Json
    foreach ($p in $popis.soubory) {
        if ($PSBoundParameters.ContainsKey('Jen') -and $Jen -notcontains $p.jmeno) { continue }
        $cil = Join-Path $cilSlozka $p.jmeno
        if ($p.puvodni) { Copy-Item -LiteralPath (Join-Path $Zaloha "soubory\$($p.jmeno)") -Destination $cil -Force }
        elseif (Test-Path -LiteralPath $cil) { Remove-Item -LiteralPath $cil -Force }
        Write-Host ("obnoven {0,-22} {1}" -f $p.jmeno, $(if ($p.puvodni) { $p.puvodni.Substring(0, 8) } else { '(smazan, predtim nebyl)' }))
    }
    # Vraci se jen zaznam DEV: zaznam REL a ostatni klice mohly mezitim vzniknout jinym nasazenim.
    $stavZalohy = Join-Path $Zaloha 'stav.json'
    $devPred = $null
    if (Test-Path -LiteralPath $stavZalohy) { $devPred = ([IO.File]::ReadAllText($stavZalohy, $utf8) | ConvertFrom-Json).dev }
    Write-NasazeniStav $k 'dev' $devPred
}

if ($Obnov) {
    $popis = [IO.File]::ReadAllText((Join-Path $Obnov 'popis.json'), $utf8) | ConvertFrom-Json
    # Na DEV share musi lezet to, co tohle nasazeni nahralo: novejsi nahrani kolegy by obnova tise prepsala.
    foreach ($p in $popis.soubory) {
        $cil = Join-Path $cilSlozka $p.jmeno
        $ted = if (Test-Path -LiteralPath $cil) { Get-Md5 $cil } else { $null }
        if ($ted -ne $p.nahrany) { throw "$($p.jmeno) na DEV share uz neni z tohoto nasazeni ($ted), obnova by prepsala cizi soubor." }
    }
    Restore-Zaloha $Obnov
    exit 0
}

$sestaveniData = [IO.File]::ReadAllText($Sestaveni, $utf8) | ConvertFrom-Json
$seznamData = [IO.File]::ReadAllText($Seznam, $utf8) | ConvertFrom-Json
if ($seznamData.faze -ne 'dev') { throw "$Seznam neni inventura faze dev." }
$soubory = @($Soubory | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })

# Vsechny kontroly pred prvni kopii: radsi nic nez pul nasazeni.
$plan = foreach ($n in $soubory) {
    $polozka = @($sestaveniData.soubory | Where-Object { $_.jmeno -eq $n })[0]
    if (-not $polozka) { throw "$n neni v sestaveni $Sestaveni" }
    if ((Get-Md5 $polozka.cesta) -ne $polozka.md5) { throw "$n v $($polozka.cesta) ma jine MD5 nez po sestaveni, nekdo ho prepsal" }
    $cil = Join-Path $cilSlozka $n
    $ocekavany = $null
    if ($seznamData.cil.soubory.PSObject.Properties[$n] -and $seznamData.cil.soubory.$n) { $ocekavany = $seznamData.cil.soubory.$n.md5 }
    $ted = if (Test-Path -LiteralPath $cil) { Get-Md5 $cil } else { $null }
    if ($ted -ne $ocekavany) { throw "$n na DEV share se od schvalene tabulky zmenil ($ocekavany -> $ted), nekdo ho mezitim nahral" }
    [pscustomobject]@{ Jmeno = $n; Zdroj = $polozka.cesta; Cil = $cil; Md5 = $polozka.md5; Puvodni = $ted }
}

$plan = @($plan)
$zaloha = Join-Path $k.zalohyDir (New-CasovaZnacka)
New-Item -ItemType Directory -Force -Path (Join-Path $zaloha 'soubory') | Out-Null
$stavCesta = Join-Path $k.stavDir 'stav.json'
if (Test-Path -LiteralPath $stavCesta) { Copy-Item -LiteralPath $stavCesta -Destination (Join-Path $zaloha 'stav.json') }
foreach ($p in $plan) { if ($p.Puvodni) { Copy-Item -LiteralPath $p.Cil -Destination (Join-Path $zaloha "soubory\$($p.Jmeno)") -Force } }
Write-Json (Join-Path $zaloha 'popis.json') ([ordered]@{ soubory = @($plan | ForEach-Object { [ordered]@{ jmeno = $_.Jmeno; puvodni = $_.Puvodni; nahrany = $_.Md5 } }) })

$zmenene = @()
if ($plan.Count -gt 0) { try {
    foreach ($p in $plan) {
        $zmenene += $p.Jmeno
        Copy-Item -LiteralPath $p.Zdroj -Destination $p.Cil -Force
        $po = Get-Md5 $p.Cil
        if ($po -ne $p.Md5) { throw "$($p.Jmeno) po kopii nesedi ($po)" }
        Write-Host ("{0,-22} {1} -> {2}" -f $p.Jmeno, $(if ($p.Puvodni) { $p.Puvodni.Substring(0, 8) } else { '(novy)' }), $p.Md5.Substring(0, 8))
    }
} catch {
    # Pul nasazeni je horsi nez zadne: uz nahrane soubory se vrati ze zalohy. Soubor, jehoz kopie selhala, se
    # vraci jen kdyz se zmenil (zamceny soubor se nezmenil a vratit by nesel).
    $chyba = $_.Exception.Message
    $posledni = $plan | Where-Object { $_.Jmeno -eq $zmenene[-1] }
    $ted = if (Test-Path -LiteralPath $posledni.Cil) { Get-Md5 $posledni.Cil } else { $null }
    if ($ted -eq $posledni.Puvodni) { $zmenene = @($zmenene | Select-Object -SkipLast 1) }
    try { Restore-Zaloha $zaloha $zmenene } catch { throw "Kopie selhala ($chyba) a vraceni ze zalohy taky: $($_.Exception.Message). Zaloha: $zaloha" }
    throw "Kopie selhala ($chyba), DEV share je vraceny do puvodniho stavu. Zaloha: $zaloha"
} }

# Soubor ze sestaveni, ktery uz na DEV share lezi se stejnym MD5, je nasazeny i bez kopie (tabulka ho neukaze).
$naShare = @($sestaveniData.soubory | Where-Object { $_.nasadit -and $soubory -notcontains $_.jmeno } | Where-Object {
    $c = Join-Path $cilSlozka $_.jmeno; (Test-Path -LiteralPath $c) -and (Get-Md5 $c) -eq $_.md5 } | ForEach-Object { $_.jmeno })
$stav = Read-NasazeniStav $k
$souboryStavu = [ordered]@{}
if ($stav.dev -and $stav.dev.soubory) { foreach ($pr in $stav.dev.soubory.PSObject.Properties) { $souboryStavu[$pr.Name] = $pr.Value } }
foreach ($p in $plan) { $souboryStavu[$p.Jmeno] = $p.Md5 }
$kotva = Get-KotvaPoNasazeni $k $seznamData $sestaveniData (@($plan | ForEach-Object { $_.Jmeno }) + $naShare)
if ($kotva.chybi.Count -gt 0) { Write-Host "Kotva DEV zustava, nenasazeno: $($kotva.chybi -join ', ')" }
Write-NasazeniStav $k 'dev' ([ordered]@{ cas = (Get-Date).ToString('s'); commity = $kotva.commity; soubory = $souboryStavu })
Write-Host "zaloha: $zaloha"
