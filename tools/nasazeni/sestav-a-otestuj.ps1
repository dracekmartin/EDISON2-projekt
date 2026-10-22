# Postavi a otestuje, co se bude nasazovat. Na DEV ani REL nic nenahraje.
#
# Stavi jen commity, ktere videla inventura (-Seznam): kdyz se HEAD od ni posunul, odmitne.
# Legacy projekty z EDISON postavi do C:\Edison: nejdriv zastarale DLL (-Predem, z co-nasadit.ps1), pak
# zmenene projekty (-Legacy). Shell z EDISON2 (-Shell) postavi v docasnem worktree na commitu, ktery
# zaznamena na zacatku behu, pusti vsechny testy (unit s okny, integracni, Cache vcetne -CacheTesty, E2E)
# a publikuje do C:\Edison, s -Navic i EDUPDATE2.exe nebo EDISONRUNTIME.zip. Neprosly nebo preskoceny test
# zastavi.
# Zapise seznam postavenych souboru s MD5 a commitem, ze ktereho nahraj-na-dev.ps1 a over-test-verzi.ps1
# poznaji, ze nasazuji a overuji prave tyhle soubory.
#
#   powershell -NoProfile -File tools/nasazeni/sestav-a-otestuj.ps1 -Seznam <seznam-dev.json> -Predem MZDY2APICIS\MZDY2APICIS.csproj -Legacy MHD2Ridici\MHD2Ridici.csproj -Shell -CacheTesty ALVA.MHD2.Ridici.Tests
#
# Vystup: %LOCALAPPDATA%\edison-nasad\sestaveni-<cas>.json (cesta na poslednim radku), logy krok po kroku vedle.
# Legacy vzdy Build, nikdy Rebuild: Rebuild nejdriv smaze DLL v C:\Edison, na ktere ostatni projekty odkazuji
# pres HintPath, a build pak spadne. Lokalni C:\Edison se nehlida, kriticke je jen to, co jde na DEV a REL.
# ASCII-only zamerne: WinPS 5.1 cte UTF-8 bez BOM jako CP-1252.

[CmdletBinding()]
param(
    [string[]]$Predem = @(),
    [string[]]$Legacy = @(),
    [switch]$Shell,
    [string[]]$CacheTesty = @(),
    [string[]]$Navic = @(),
    [string]$Seznam,
    [string]$Nastaveni,
    [scriptblock]$Spoustec,
    [switch]$BezKontrolSouboru
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'spolecne.psm1') -Force
$k = Get-Nastaveni $Nastaveni
$rozdel = { param($s) @($s | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ }) }
$Predem = @(& $rozdel $Predem); $Legacy = @(& $rozdel $Legacy); $CacheTesty = @(& $rozdel $CacheTesty); $Navic = @(& $rozdel $Navic)
if ($Navic.Count -gt 0 -and -not $Shell) { throw '-Navic se stavi spolu se shellem, chybi -Shell.' }
$logy = Join-Path $k.stavDir "build-$(New-CasovaZnacka)"
New-Item -ItemType Directory -Force -Path $logy | Out-Null

function Invoke-Krok([string]$Nazev, [string]$Exe, [string[]]$Argumenty, [string]$Slozka, [hashtable]$Prostredi = @{}) {
    $puvodni = @{}
    foreach ($kl in $Prostredi.Keys) { $puvodni[$kl] = [Environment]::GetEnvironmentVariable($kl); [Environment]::SetEnvironmentVariable($kl, $Prostredi[$kl]) }
    try {
        $r = if ($script:Spoustec) { & $script:Spoustec $Exe $Argumenty $Slozka } else { Invoke-Prikaz $Exe $Argumenty $Slozka }
    } finally {
        foreach ($kl in $puvodni.Keys) { [Environment]::SetEnvironmentVariable($kl, $puvodni[$kl]) }
    }
    $log = Join-Path $script:logy "$Nazev.log"
    [IO.File]::WriteAllLines($log, [string[]]@($r.Vystup))
    Write-Host ("==> {0}: kod {1} (log {2})" -f $Nazev, $r.Kod, $log)
    return $r
}

# Souhrn dotnet test: "Passed!  - Failed: 0, Passed: 12, Skipped: 0, Total: 12". Preskoceny test zastavi:
# znamena zamcenou plochu, chybejici VPN nebo promennou, takze sada neoverila, co mela.
function Assert-Testy([string]$Nazev, $Vysledek) {
    $souhrny = @($Vysledek.Vystup | ForEach-Object {
        if ($_ -match '(Passed|Failed)!\s+-\s+Failed:\s+(\d+),\s+Passed:\s+(\d+),\s+Skipped:\s+(\d+)') {
            [pscustomobject]@{ Selhalo = [int]$Matches[2]; Proslo = [int]$Matches[3]; Preskoceno = [int]$Matches[4] }
        }
    })
    $selhalo = ($souhrny | Measure-Object Selhalo -Sum).Sum
    $proslo = ($souhrny | Measure-Object Proslo -Sum).Sum
    $preskoceno = ($souhrny | Measure-Object Preskoceno -Sum).Sum
    if ($Vysledek.Kod -ne 0 -or $selhalo -gt 0) { throw "$Nazev neprosly (kod $($Vysledek.Kod), selhalo $selhalo), log v $script:logy" }
    if ($souhrny.Count -eq 0) { throw "$Nazev nemaji souhrn, log v $script:logy" }
    if ($preskoceno -gt 0) { throw "$Nazev preskocily $preskoceno testu (plocha, VPN?), log v $script:logy" }
    return "$Nazev $proslo"
}

# Build server dotnet drzi soubory ve worktree i po buildu, git worktree remove pak adresar nesmaze cely.
function Remove-DocasnyWorktree([string]$Repo, [string]$Cesta) {
    Invoke-Git $Repo @('worktree', 'remove', '--force', $Cesta) -PovolChybu | Out-Null
    Invoke-Git $Repo @('worktree', 'prune') -PovolChybu | Out-Null
    if (-not (Test-Path -LiteralPath $Cesta)) { return }
    Invoke-Prikaz 'dotnet' @('build-server', 'shutdown') | Out-Null
    for ($i = 0; $i -lt 5 -and (Test-Path -LiteralPath $Cesta); $i++) {
        try { Remove-Item -LiteralPath $Cesta -Recurse -Force } catch { Start-Sleep -Seconds 2 }
    }
    if (Test-Path -LiteralPath $Cesta) { throw "Docasny worktree $Cesta nejde smazat, neco v nem drzi soubory." }
}

$souhrnTestu = @()
$soubory = @()
$commity = [ordered]@{
    EDISON = ((Invoke-Git $k.repoEdison @('rev-parse', 'HEAD')).Vystup -join '').Trim()
    EDISON2 = ((Invoke-Git $k.repoEdison2 @('rev-parse', 'HEAD')).Vystup -join '').Trim()
}
# Stavi se jen commity, ktere videla inventura: novejsi by na DEV dostal build, ktery tabulka neukazala.
if ($Seznam) {
    $seznamData = [IO.File]::ReadAllText($Seznam) | ConvertFrom-Json
    foreach ($r in @($commity.Keys)) {
        $videla = $seznamData.repa.$r.head
        if ($videla -ne $commity[$r]) { throw "HEAD $r se od inventury posunul ($videla -> $($commity[$r])), pust co-nasadit.ps1 znovu." }
    }
} elseif (-not $BezKontrolSouboru) { throw 'Chybi -Seznam: build musi stavet commity, ktere videla inventura.' }

# --- Legacy -----------------------------------------------------------------------------------------
if ($Predem.Count + $Legacy.Count -gt 0) {
    if (-not $BezKontrolSouboru) {
        # Legacy se stavi z hlavni kopie: rozdelana prace by se do buildu dostala bez commitu.
        $necisty = @((Invoke-Git $k.repoEdison @('status', '--porcelain')).Vystup | Where-Object { $_ })
        if ($necisty.Count -gt 0) { throw "EDISON ma necommitovane zmeny, build by neodpovidal commitu: $($necisty -join ', ')" }
        # Shell spusteny z C:\Edison drzi legacy DLL odtud (nebali je), build by je neprepsal.
        $koren = $k.edisonDir.TrimEnd('\') + '\'
        $bezi = @(Get-Process | Where-Object { $_.Path -and $_.Path.StartsWith($koren, [StringComparison]::OrdinalIgnoreCase) -and -not $_.Path.Substring($koren.Length).Contains('\') })
        if ($bezi.Count -gt 0) { throw "Z $koren bezi $(($bezi | ForEach-Object { "$($_.Name) (pid $($_.Id))" }) -join ', '): zamyka vystupy buildu, zavri ho." }
    }
    $msbuild = 'msbuild'
    if (-not $Spoustec) {
        $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
        $msbuild = @(& $vswhere -latest -prerelease -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe')[0]
        if (-not $msbuild) { throw 'MSBuild nenalezen (vswhere).' }
    }
    $poradi = @($Predem | ForEach-Object { [pscustomobject]@{ Projekt = $_; Nasadit = $false } }) + @($Legacy | ForEach-Object { [pscustomobject]@{ Projekt = $_; Nasadit = $true } })
    # Projekty na sebe odkazuji pres HintPath do C:\Edison, ne ProjectReference: projekt, ktery spadne na DLL
    # jineho projektu ze seznamu, projde v dalsim pruchodu, az ji ten postavi.
    $zbyva = $poradi
    for ($pruchod = 1; $pruchod -le 3 -and $zbyva.Count -gt 0; $pruchod++) {
        $zbyva = @($zbyva | Where-Object {
            $nazev = [IO.Path]::GetFileNameWithoutExtension($_.Projekt)
            $r = Invoke-Krok "msbuild-$nazev-$pruchod" $msbuild @((Join-Path $k.repoEdison $_.Projekt), '-t:Build', '-p:Configuration=Release', '-p:ResolveNuGetPackages=false', '-m:1', '-nologo', '-v:q') $k.repoEdison
            $r.Kod -ne 0
        })
    }
    if ($zbyva.Count -gt 0) { throw "Build selhal i po trech pruchodech: $(($zbyva | ForEach-Object { $_.Projekt }) -join ', '), log v $logy" }
    # Legacy se stavi z hlavni kopie: pull behem buildu by do vystupu dostal jiny commit, nez zapise manifest.
    $headPo = ((Invoke-Git $k.repoEdison @('rev-parse', 'HEAD')).Vystup -join '').Trim()
    if ($headPo -ne $commity.EDISON) { throw "HEAD EDISON se behem buildu posunul ($($commity.EDISON) -> $headPo), postav znovu." }
    if (-not $BezKontrolSouboru) {
        $necisty = @((Invoke-Git $k.repoEdison @('status', '--porcelain')).Vystup | Where-Object { $_ })
        if ($necisty.Count -gt 0) { throw "EDISON se behem buildu zmenil, vystupy nemusi odpovidat commitu: $($necisty -join ', ')" }
    }
    if (-not $BezKontrolSouboru) {
        foreach ($p in $poradi) {
            $info = Get-ProjektInfo (Join-Path $k.repoEdison $p.Projekt)
            $vystup = Join-Path $k.edisonDir $info.Vystup
            # Soubor v C:\Edison musi byt kopie mezivystupu tohoto buildu, jinak tam lezi stara nebo cizi verze.
            $mezi = @(Get-ChildItem -LiteralPath (Join-Path $info.Slozka 'obj') -Recurse -File -Filter $info.Vystup -ErrorAction SilentlyContinue |
                Where-Object { $_.FullName -match '\\Release\\' })
            if (-not (Test-Path -LiteralPath $vystup) -or $mezi.Count -eq 0 -or (Get-Md5 $vystup) -ne (Get-Md5 $mezi[0].FullName)) {
                throw "$($p.Projekt) nestavi do $vystup, nebo se vystup neprepsal: tyhle skripty ho nasadit neumi."
            }
            if ((Test-ReleaseBuild $vystup) -eq $false) { throw "$($info.Vystup) je Debug build" }
            $soubory += [ordered]@{ jmeno = $info.Vystup; cesta = $vystup; md5 = Get-Md5 $vystup; verze = Get-ProduktVerze $vystup; commit = $commity.EDISON; nasadit = $p.Nasadit; druh = $(if ($p.Nasadit) { 'legacy' } else { 'predem' }) }
        }
    }
}

# --- Shell ------------------------------------------------------------------------------------------
if ($Shell) {
    $repo = $k.repoEdison2
    $worktree = $null
    if (-not $BezKontrolSouboru) {
        # Vzdy worktree na commitu zaznamenanem na zacatku: v hlavni kopii by se do publish po desitkach minut
        # testu dostala rozdelana prace jineho chatu a HEAD se mezitim muze posunout. Mimo repo, protoze editor
        # otevreneho repa by do worktree pod .claude\ zapisoval i po jeho smazani.
        $worktree = Join-Path $k.stavDir 'worktree-shell'
        Remove-DocasnyWorktree $repo $worktree
        Invoke-Git $repo @('worktree', 'add', '--detach', $worktree, $commity.EDISON2) | Out-Null
        $repo = $worktree
    }
    # Restore testoveho projektu jinak pousti "dotnet husky install", ktery z worktree prepise core.hooksPath
    # hlavni kopie na docasnou slozku.
    $puvodniHusky = $env:HUSKY
    $env:HUSKY = '0'
    try {
        $legacyRoot = $k.repoEdison -replace '\\', '/'
        $r = Invoke-Krok 'dotnet-build' 'dotnet' @('build', 'EDISON2.sln', '-nologo', '-v', 'q') $repo
        if ($r.Kod -ne 0 -and ($r.Vystup -join "`n") -match 'BG1002') {
            # Prvni WPF build v cerstvem stromu obcas nenajde vygenerovany .g.cs, druhy projde.
            $r = Invoke-Krok 'dotnet-build-2' 'dotnet' @('build', 'EDISON2.sln', '-nologo', '-v', 'q') $repo
        }
        if ($r.Kod -ne 0) { throw "dotnet build selhal, testy se nepousti. Log v $logy" }

        $souhrnTestu += Assert-Testy 'unit+okna' (Invoke-Krok 'testy-unit' 'dotnet' @('test', 'EDISON2.sln', '--no-build', '-nologo', '-v', 'q', '--filter', 'Category!=Integration&Category!=E2E') $repo @{ EDISON_WINDOW_TESTS = '1'; EDISON_LEGACY_ROOT = $legacyRoot })
        $souhrnTestu += Assert-Testy 'integracni' (Invoke-Krok 'testy-integracni' 'dotnet' @('test', 'EDISON2.IntegrationTests', '--no-build', '-nologo', '-v', 'q') $repo @{ EDISON_IT_REQUIRED = '1' })
        $cacheArgumenty = @(, @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', 'tools/run-cache-tests.ps1'))
        if ($CacheTesty.Count -gt 0) { $cacheArgumenty += , @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', 'tools/run-cache-tests.ps1', '-Package', ($CacheTesty -join ',')) }
        $i = 0
        foreach ($argumenty in $cacheArgumenty) {
            $i++
            $r = Invoke-Krok "testy-cache-$i" 'powershell' $argumenty $repo
            $celkem = @($r.Vystup | ForEach-Object {
                if ($_ -match 'Total:\s+(\d+) passed,\s+(\d+) failed,\s+(\d+) skipped') { , [int[]]@($Matches[1], $Matches[2], $Matches[3]) }
            })
            if ($r.Kod -ne 0 -or $celkem.Count -eq 0 -or $celkem[-1][1] -gt 0) { throw "Cache testy ($i) neprosly, log v $logy" }
            if ($celkem[-1][2] -gt 0) { throw "Cache testy ($i) preskocily $($celkem[-1][2]) testu, log v $logy" }
            $souhrnTestu += "cache$i $($celkem[-1][0])"
        }
        $souhrnTestu += Assert-Testy 'e2e' (Invoke-Krok 'testy-e2e' 'dotnet' @('test', 'EDISON2.E2ETests', '--no-build', '-nologo', '-v', 'q') $repo @{ EDISON_E2E = '1' })

        $r = Invoke-Krok 'dotnet-publish' 'dotnet' @('publish', $k.shell.publikovat, "-p:PublishProfile=$($k.shell.publishProfil)", '-nologo', '-v', 'q') $repo
        if ($r.Kod -ne 0) { throw "dotnet publish selhal, log v $logy" }
        if (-not $BezKontrolSouboru) {
            foreach ($n in $k.shell.soubory) {
                $cesta = Join-Path $k.edisonDir $n
                $verze = Get-ProduktVerze $cesta
                if ($verze -notmatch [regex]::Escape($commity.EDISON2)) { throw "$n ma verzi $verze, ne commit $($commity.EDISON2)" }
                $soubory += [ordered]@{ jmeno = $n; cesta = $cesta; md5 = Get-Md5 $cesta; verze = $verze; commit = $commity.EDISON2; nasadit = $true; druh = 'shell' }
            }
        }
        foreach ($n in $Navic) {
            $jak = $k.shell.navic.$n
            if (-not $jak) { throw "$n neni v nastaveni.json shell.navic" }
            $r = if ($jak.publikovat) {
                Invoke-Krok "publish-$n" 'dotnet' @('publish', $jak.publikovat, "-p:PublishProfile=$($k.shell.publishProfil)", '-nologo', '-v', 'q') $repo
            } else {
                Invoke-Krok "skript-$n" 'powershell' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $jak.skript) $repo
            }
            if ($r.Kod -ne 0) { throw "$n se nepostavil, log v $logy" }
            if (-not $BezKontrolSouboru) {
                $cesta = Join-Path $k.edisonDir $n
                $verze = Get-ProduktVerze $cesta
                if ($jak.publikovat -and $verze -notmatch [regex]::Escape($commity.EDISON2)) { throw "$n ma verzi $verze, ne commit $($commity.EDISON2)" }
                $soubory += [ordered]@{ jmeno = $n; cesta = $cesta; md5 = Get-Md5 $cesta; verze = $verze; commit = $commity.EDISON2; nasadit = $true; druh = 'shell' }
            }
        }
    } finally {
        $env:HUSKY = $puvodniHusky
        # Chyba uklidu nesmi prebit chybu buildu nebo testu. Zbyly worktree smaze pristi beh pred zalozenim.
        if ($worktree) { try { Remove-DocasnyWorktree $k.repoEdison2 $worktree } catch { Write-Warning $_.Exception.Message } }
    }
}

$sestaveni = [ordered]@{ cas = (Get-Date).ToString('s'); commity = $commity; testy = $souhrnTestu; logy = $logy; soubory = $soubory }
$vystup = Join-Path $k.stavDir "sestaveni-$(New-CasovaZnacka).json"
Write-Json $vystup $sestaveni
Write-Host ("Testy: {0}" -f ($souhrnTestu -join ', '))
foreach ($s in $soubory) { Write-Host ("  {0,-22} {1} {2}{3}" -f $s.jmeno, $s.md5.Substring(0, 8), $s.verze, $(if ($s.nasadit) { '' } else { ' (jen predem)' })) }
$vystup
