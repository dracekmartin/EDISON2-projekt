# Zjisti, co je potreba nasadit na DEV share (-Faze dev) nebo na REL (-Faze rel). Nic nemeni.
#
# Projde commity od posledniho nasazeni a urci z nich binarky k postaveni a tridy a projekty Cache:
# v EDISON jen tvoje, v EDISON2 vsechny (main se nasazuje cely, vcetne stromu od tree-sync) krome TD,
# ktery vydava jeho autor sam. Podiva se, co je ted na DEV share nebo v poslednim TEST baliku na REL.
# Hlasi, co by nasazeni ohrozilo: konflikt pri pullu, cizi zmeny ve stejnych modulech, zastarale DLL
# v C:\Edison (build by spadl), projekt, ktery nestavi do C:\Edison (skripty ho nasadit neumi), testy
# v projektech, legacy DLL na cili bez API, ktere shell vola, TD na cili starsi nez zmena API EDControls,
# strom v EDCore.dll jiny nez v gitu.
# Z vystupu skill /edison-nasad sestavi tabulku ke schvaleni.
#
#   powershell -NoProfile -File tools/nasazeni/co-nasadit.ps1 -Faze dev
#   powershell -NoProfile -File tools/nasazeni/co-nasadit.ps1 -Faze rel [-BezBaliku]
#     -BezBaliku: TEST balik (stovky MB) se nestahuje, stav REL je pak neznamy a nasazuje se vse ze sestaveni
#   powershell -NoProfile -File tools/nasazeni/co-nasadit.ps1 -PorovnejTridy <slozka>   tridy z DEV proti gitu
#   powershell -NoProfile -File tools/nasazeni/co-nasadit.ps1 -ClenoveProjektu <slozka> [-Seznam <seznam.json>]
#     clenove projektu z DEV (<projekt>.txt, radky "Jmeno|Typ" z cache_project_info) proti gitu
#   powershell -NoProfile -File tools/nasazeni/co-nasadit.ps1 -OverDev -Seznam <seznam.json>
#     oboji naraz: tridy ze seznamu a cleny jejich projektu stahne z DEV pres Atelier (prihlaseni CACHE_*)
#
# Vystup: %LOCALAPPDATA%\edison-nasad\seznam-<faze>-<cas>.json (cesta na poslednim radku) a prehled.
# "Posledni nasazeni" je ve stav.json, ktery zapisuji nahraj-na-dev.ps1 a over-test-verzi.ps1. Prvni beh
# ho nema, zadava se -KotvaEdison a -KotvaEdison2.
# ASCII-only zamerne: WinPS 5.1 cte UTF-8 bez BOM jako CP-1252.

[CmdletBinding()]
param(
    [ValidateSet('dev', 'rel')] [string]$Faze = 'dev',
    [string]$KotvaEdison,
    [string]$KotvaEdison2,
    [string]$PorovnejTridy,
    [string]$ClenoveProjektu,
    [switch]$OverDev,
    [string]$Seznam,
    [switch]$BezFetch,
    [switch]$BezBaliku,
    [string]$Nastaveni,
    [string]$Cil
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'spolecne.psm1') -Force
$k = Get-Nastaveni $Nastaveni
$repa = [ordered]@{ EDISON = $k.repoEdison; EDISON2 = $k.repoEdison2 }

# Jmeno dokumentu Cache: trida tecky (ALVA.MOD.A), include a rutina jmeno souboru (ALVAMHD.inc, OBapiED.int).
function Get-JmenoTridy([string]$Soubor) {
    if ($Soubor -notmatch '/CACHE/(.+)\.(cls|inc|mac|int)$') { return $null }
    if ($Matches[2] -ne 'cls') { return ($Soubor -split '/')[-1] }
    return ($Matches[1] -replace '/', '.')
}

# --- Porovnani trid z DEV (cache_get_source) s HEAD ---------------------------------------------
function Compare-TridySGitem([string]$Slozka) {
    return @(foreach ($f in Get-ChildItem -LiteralPath $Slozka -File) {
        $jeTrida = $f.Extension -ieq '.cls'
        $trida = if ($jeTrida) { [IO.Path]::GetFileNameWithoutExtension($f.Name) } else { $f.Name }
        $cesta = $null; $repoTridy = $null
        foreach ($r in $repa.Keys) {
            $relativni = if ($jeTrida) { '*/CACHE/' + ($trida -replace '\.', '/') + '.cls' } else { "*/CACHE/*$($f.Name)" }
            $nalez = @((Invoke-Git $repa[$r] @('ls-files', $relativni)).Vystup | Where-Object { $_ -and ($_ -split '/')[-1] -ieq $(if ($jeTrida) { ($trida -split '\.')[-1] + '.cls' } else { $f.Name }) })
            if ($nalez.Count -gt 0) { $cesta = $nalez[0]; $repoTridy = $r; break }
        }
        if (-not $cesta) { [pscustomobject]@{ trida = $trida; shoda = $false; duvod = 'neni v gitu' }; continue }
        $git = ConvertTo-PorovnatelnaTrida (Get-GitText $repa[$repoTridy] 'HEAD' $cesta) -BezPrazdnych:$jeTrida
        $dev = ConvertTo-PorovnatelnaTrida ([IO.File]::ReadAllText($f.FullName, (New-Object Text.UTF8Encoding($false)))) -BezPrazdnych:$jeTrida
        $duvod = ''
        if ($git -ne $dev) {
            $a = $git -split "`n"; $b = $dev -split "`n"; $i = 0
            while ($i -lt [Math]::Min($a.Count, $b.Count) -and $a[$i] -eq $b[$i]) { $i++ }
            # Cislo radku po vynechani prazdnych v souboru nesedi, text radku jde najit.
            $radek = if ($i -lt $a.Count) { $a[$i].Trim() } else { '(konec)' }
            $duvod = "prvni rozdil v gitu u radku: $($radek.Substring(0, [Math]::Min(80, $radek.Length)))"
        }
        [pscustomobject]@{ trida = $trida; repo = $repoTridy; cesta = $cesta; shoda = ($git -eq $dev); duvod = $duvod }
    })
}
if ($PorovnejTridy) {
    $vysledek = Compare-TridySGitem $PorovnejTridy
    $vysledek | Format-Table -AutoSize | Out-String -Width 200 | Write-Host
    $vystup = Join-Path $k.stavDir "porovnani-trid-$(New-CasovaZnacka).json"
    Write-Json $vystup @($vysledek)
    $vystup
    if (@($vysledek | Where-Object { -not $_.shoda }).Count -gt 0) { exit 1 }
    exit 0
}

# --- Clenove Studio projektu na DEV (cache_project_info) proti gitu ------------------------------
# Import z DEV na REL posle cely projekt. Clen, ktery v gitu neni, nebo je v hlavni kopii rozdelany (hook ho
# nasadil bez commitu), by na REL dostal necommitovanou praci. Hlasi i testy NEPRIDAVAT v projektu a s -Seznam
# tridy ze seznamu mimo projekt (import by je vynechal).
function Get-ClenoveProjektu([string]$Slozka, $SeznamData) {
    $vGitu = @{}; $rozdelane = @{}
    foreach ($r in $repa.Keys) {
        foreach ($f in (Invoke-Git $repa[$r] @('ls-files', '--', '*/CACHE/*')).Vystup) { $t = Get-JmenoTridy $f; if ($t) { $vGitu[$t] = $r } }
        foreach ($l in (Invoke-Git $repa[$r] @('status', '--porcelain', '--', '*/CACHE/*')).Vystup) {
            if (-not $l -or $l.Length -lt 4) { continue }
            $t = Get-JmenoTridy (($l.Substring(3) -split ' -> ')[-1].Trim('"'))
            if ($t) { $rozdelane[$t] = $r }
        }
    }
    # Testy hleda v celem gitu: seznam.nepridavat zna jen moduly, ve kterych se od kotvy commitovalo.
    $nepridavat = @(foreach ($r in $repa.Keys) { Get-NepridavatTridy $repa[$r] })
    return @(foreach ($f in Get-ChildItem -LiteralPath $Slozka -File -Filter '*.txt') {
        $projekt = $f.BaseName
        $clenove = @{}; $baliky = @(); $mimoGit = @(); $necommit = @(); $testy = @(); $neovereno = @()
        foreach ($radek in [IO.File]::ReadAllLines($f.FullName)) {
            $jmeno, $typ = $radek.Trim() -split '\|', 2
            if (-not $jmeno -or -not $typ) { continue }
            if ($typ -ieq 'PKG') { $baliky += $jmeno; $neovereno += "$jmeno (PKG)"; continue }
            $doc = if ($typ -ieq 'CLS') { $jmeno -replace '\.cls$', '' } elseif ($jmeno -match '\.(inc|mac|int)$') { $jmeno } elseif ($typ -match '^(INC|MAC|INT)$') { "$jmeno.$($typ.ToLowerInvariant())" } else { $null }
            if (-not $doc) { $neovereno += "$jmeno ($typ)"; continue }
            $clenove[$doc] = $true
            if (-not $vGitu.ContainsKey($doc)) { $mimoGit += $doc }
            if ($rozdelane.ContainsKey($doc)) { $necommit += $doc }
            if ($nepridavat -contains $doc) { $testy += $doc }
        }
        $jeClen = { param($t) $clenove.ContainsKey($t) -or @($baliky | Where-Object { $t -like "$_.*" }).Count -gt 0 }
        $chybi = @()
        if ($SeznamData) { $chybi = @(Get-TridyKNasazeni $SeznamData | Where-Object { @($_.cacheProjekty) -contains $projekt -and -not (& $jeClen $_.trida) } | ForEach-Object { $_.trida }) }
        [pscustomobject]@{ projekt = $projekt; clenu = $clenove.Count; mimoGit = $mimoGit; necommitovane = $necommit; testy = $testy; chybiVProjektu = $chybi; neovereno = $neovereno }
    })
}
if ($ClenoveProjektu) {
    $seznamData = if ($Seznam) { [IO.File]::ReadAllText($Seznam, (New-Object Text.UTF8Encoding($false))) | ConvertFrom-Json } else { $null }
    $vysledek = Get-ClenoveProjektu $ClenoveProjektu $seznamData
    foreach ($v in $vysledek) {
        Write-Host ("{0}: clenu {1}, mimo git {2}, necommitovane {3}, testy {4}, chybi v projektu {5}{6}" -f $v.projekt, $v.clenu,
            ($v.mimoGit -join ', '), ($v.necommitovane -join ', '), ($v.testy -join ', '), ($v.chybiVProjektu -join ', '),
            $(if ($v.neovereno.Count) { ", neovereno $($v.neovereno -join ', ')" } else { '' }))
    }
    $vystup = Join-Path $k.stavDir "clenove-$(New-CasovaZnacka).json"
    Write-Json $vystup @($vysledek)
    $vystup
    if (@($vysledek | Where-Object { $_.mimoGit.Count + $_.necommitovane.Count + $_.testy.Count + $_.chybiVProjektu.Count -gt 0 }).Count -gt 0) { exit 1 }
    exit 0
}

# --- Oboji z DEV pres Atelier -----------------------------------------------------------------------
# Studio projekt tridy hleda primo na DEV (%Studio.ProjectItem), takze nezavisi na konfiguraci MCP.
# Exit 1 jen pro to, co ohrozi DEV: trida chybi, nesedi s HEAD, neni v zadnem projektu, rozdelana nebo test
# v projektu. Clen mimo git hlasi, ale nezastavuje: vadi az pri importu na REL.
if ($OverDev) {
    if (-not $Seznam) { throw '-OverDev potrebuje -Seznam.' }
    $seznamData = [IO.File]::ReadAllText($Seznam, (New-Object Text.UTF8Encoding($false))) | ConvertFrom-Json
    $koren = Join-Path $k.stavDir "dev-$(New-CasovaZnacka)"
    $slozkaTrid = Join-Path $koren 'tridy'; $slozkaProjektu = Join-Path $koren 'projekty'
    New-Item -ItemType Directory -Force -Path $slozkaTrid, $slozkaProjektu | Out-Null
    $polozky = @(Invoke-AtelierSql 'SELECT Project, Name, Type FROM %Studio.ProjectItem')
    $projektyClenu = @{}
    foreach ($p in $polozky) { $n = "$($p.Name)".ToUpperInvariant(); if (-not $projektyClenu.ContainsKey($n)) { $projektyClenu[$n] = @() }; $projektyClenu[$n] += "$($p.Project)" }
    $balikyDev = @($polozky | Where-Object { "$($_.Type)" -ieq 'PKG' })
    $chybiNaDev = @(); $bezProjektu = @(); $projekty = @{}
    foreach ($t in Get-TridyKNasazeni $seznamData) {
        $jeTrida = $t.cesta -match '\.cls$'
        $soubor = if ($jeTrida) { "$($t.trida).cls" } else { $t.trida }
        $text = Get-AtelierDokument $soubor
        # Include ve vnorene slozce ma na serveru jmeno s cestou (ALVA.ISY.ISYZastavky.ALVAISYZastavky.inc).
        if ($null -eq $text -and -not $jeTrida -and $t.cesta -match '/CACHE/(.+)$') { $text = Get-AtelierDokument ($Matches[1] -replace '/', '.') }
        if ($null -eq $text) { $chybiNaDev += $t.trida; continue }
        [IO.File]::WriteAllText((Join-Path $slozkaTrid $soubor), $text, (New-Object Text.UTF8Encoding($false)))
        $kde = @($projektyClenu[$t.trida.ToUpperInvariant()])
        # Clen muze byt i cely balik (PKG ALVA.EDTesting): trida patri projektu, jehoz balik je jejim prefixem.
        $kde += @($balikyDev | Where-Object { $t.trida -like "$($_.Name).*" } | ForEach-Object { "$($_.Project)" })
        $kde += @($t.cacheProjekty | Where-Object { $_ })
        $kde = @($kde | Where-Object { $_ } | Sort-Object -Unique)
        if ($kde.Count -eq 0) { $bezProjektu += $t.trida }
        foreach ($p in $kde) { $projekty[$p] = $true }
    }
    foreach ($p in $projekty.Keys) {
        $radky = @($polozky | Where-Object { "$($_.Project)" -eq $p } | ForEach-Object { "$($_.Name)|$($_.Type)" })
        [IO.File]::WriteAllLines((Join-Path $slozkaProjektu "$p.txt"), [string[]]$radky)
    }
    $tridy = @(Compare-TridySGitem $slozkaTrid)
    $clenove = @(Get-ClenoveProjektu $slozkaProjektu $seznamData)
    $nesedi = @($tridy | Where-Object { -not $_.shoda })
    $vysledek = [ordered]@{
        slozka = $koren; tridu = $tridy.Count; nesedi = $nesedi; chybiNaDev = $chybiNaDev; bezProjektu = $bezProjektu
        projekty = $clenove
    }
    Write-Host ("Tridy: {0} na DEV, nesedi {1}, chybi {2}, bez projektu {3}" -f $tridy.Count, $nesedi.Count, $chybiNaDev.Count, $bezProjektu.Count)
    foreach ($t in $nesedi) { Write-Host ("  nesedi {0}: {1}" -f $t.trida, $t.duvod) }
    foreach ($v in $clenove) {
        $text = @()
        foreach ($pole in 'mimoGit', 'necommitovane', 'testy', 'chybiVProjektu') { if (@($v.$pole).Count) { $text += "$pole $(@($v.$pole) -join ', ')" } }
        if ($text.Count) { Write-Host ("  {0}: {1}" -f $v.projekt, ($text -join '; ')) }
    }
    $vystup = Join-Path $k.stavDir "over-dev-$(New-CasovaZnacka).json"
    Write-Json $vystup $vysledek
    $vystup
    $vadi = $nesedi.Count + $chybiNaDev.Count + $bezProjektu.Count + @($clenove | Where-Object { $_.necommitovane.Count + $_.testy.Count + $_.chybiVProjektu.Count -gt 0 }).Count
    if ($vadi -gt 0) { exit 1 }
    exit 0
}

$autor = ((Invoke-Git $k.repoEdison @('config', 'user.name')).Vystup -join '').Trim()

# --- 1. Repa ----------------------------------------------------------------------------------------
$infoRep = [ordered]@{}
foreach ($jmeno in $repa.Keys) {
    $repo = $repa[$jmeno]
    if (-not $BezFetch) { Invoke-Git $repo @('fetch', '--quiet', 'origin') | Out-Null }
    $head = ((Invoke-Git $repo @('rev-parse', 'HEAD')).Vystup -join '').Trim()
    $pocty = Invoke-Git $repo @('rev-list', '--left-right', '--count', 'HEAD...@{u}') -PovolChybu
    $pred = 0; $za = 0
    if ($pocty.Kod -eq 0) { $c = (($pocty.Vystup -join ' ').Trim() -split '\s+'); $pred = [int]$c[0]; $za = [int]$c[1] }
    $necommit = @((Invoke-Git $repo @('status', '--porcelain')).Vystup | Where-Object { $_ })
    $konflikt = @(); $autoriKonfliktu = @()
    if ($pred -gt 0 -and $za -gt 0) {
        $m = Invoke-Git $repo @('merge-tree', '--write-tree', '--name-only', '--no-messages', 'HEAD', '@{u}') -PovolChybu
        if ($m.Kod -eq 1) {
            $konflikt = @($m.Vystup | Select-Object -Skip 1 | Where-Object { $_ })
            foreach ($soubor in $konflikt) {
                $autoriKonfliktu += (Invoke-Git $repo @('log', '--format=%an', 'HEAD..@{u}', '--', $soubor)).Vystup
            }
            $autoriKonfliktu = @($autoriKonfliktu | Where-Object { $_ } | Sort-Object -Unique)
        }
    }
    $infoRep[$jmeno] = [ordered]@{
        cesta = $repo; head = $head; predOriginem = $pred; zaOriginem = $za
        necommitovane = $necommit; konflikt = $konflikt; autoriKonfliktu = $autoriKonfliktu
    }
}

# --- 2. Kanaly REL ----------------------------------------------------------------------------------
$kanaly = [ordered]@{}
foreach ($kanal in $k.kanalRel, $k.kanalTest) {
    try { $v = Get-VerzeInf $k $kanal; $kanaly[$kanal] = [ordered]@{ LAST = $v['LAST']; TYPE = $v['TYPE'] } }
    catch { $kanaly[$kanal] = [ordered]@{ chyba = $_.Exception.Message } }
}
$staviSe = @($kanaly.Values | Where-Object { $_['LAST'] -eq '0.0.0.0' }).Count -gt 0

# --- 3. Kotva ---------------------------------------------------------------------------------------
$stav = Read-NasazeniStav $k
$ulozena = $null
if ($stav.PSObject.Properties[$Faze] -and $stav.$Faze) { $ulozena = $stav.$Faze.commity }
$kotva = [ordered]@{
    EDISON = $(if ($KotvaEdison) { $KotvaEdison } elseif ($ulozena) { $ulozena.EDISON } else { $null })
    EDISON2 = $(if ($KotvaEdison2) { $KotvaEdison2 } elseif ($ulozena) { $ulozena.EDISON2 } else { $null })
}
if (-not $kotva.EDISON -or -not $kotva.EDISON2) { throw "Pro fazi $Faze neni ve stav.json kotva: zadej -KotvaEdison a -KotvaEdison2." }

# --- 4. Commity od kotvy ------------------------------------------------------------------------------
function Get-Commity([string]$Repo, [string]$Od, [string]$Autor) {
    $a = @('log', '--no-merges', '--format=%h|%an|%s', "$Od..HEAD")
    if ($Autor) { $a = @('log', '--no-merges', "--author=$Autor", '--format=%h|%an|%s', "$Od..HEAD") }
    return @((Invoke-Git $Repo $a).Vystup | Where-Object { $_ } | ForEach-Object {
        $p = $_ -split '\|', 3; [ordered]@{ sha = $p[0]; autor = $p[1]; zprava = $p[2] }
    })
}
function Get-SouboryCommitu([string]$Repo, [string]$Od, [string]$Autor) {
    $a = @('log', '--no-merges', '--name-only', '--format=', "$Od..HEAD")
    if ($Autor) { $a += "--author=$Autor" }
    $zmenene = @((Invoke-Git $Repo $a).Vystup | Where-Object { $_ } | Sort-Object -Unique)
    # Log vypisuje i smazane soubory, ty se nenasazuji.
    $existuji = @{}
    foreach ($f in (Invoke-Git $Repo @('ls-tree', '-r', '--name-only', 'HEAD')).Vystup) { $existuji[$f] = $true }
    return @($zmenene | Where-Object { $existuji.ContainsKey($_) })
}

$commity = [ordered]@{
    EDISON = @(Get-Commity $k.repoEdison $kotva.EDISON $autor)
    EDISON2 = @(Get-Commity $k.repoEdison2 $kotva.EDISON2 $null)
}
$soubory = [ordered]@{
    EDISON = @(Get-SouboryCommitu $k.repoEdison $kotva.EDISON $autor)
    # EDISON2 se nasazuje cely z main, vcetne stromu od tree-sync. TD vydava jeho autor sam.
    EDISON2 = @(Get-SouboryCommitu $k.repoEdison2 $kotva.EDISON2 $null | Where-Object { $_ -notlike "$($k.td.zdroj)/*" })
}

# Cizi commity v tychz slozkach: build i export z DEV je pribali.
$cizi = @()
foreach ($jmeno in $repa.Keys) {
    foreach ($slozka in @($soubory[$jmeno] | ForEach-Object { ($_ -split '/')[0] } | Sort-Object -Unique)) {
        $r = Invoke-Git $repa[$jmeno] @('log', '--no-merges', '--format=%h|%an|%s', "$($kotva[$jmeno])..HEAD", '--', $slozka)
        $cizi += @($r.Vystup | Where-Object { $_ } | ForEach-Object {
            $p = $_ -split '\|', 3
            if ($p[1] -ne $autor) { [ordered]@{ repo = $jmeno; modul = $slozka; sha = $p[0]; autor = $p[1]; zprava = $p[2] } }
        })
    }
}

# --- 5. Legacy projekty k buildu, zastarale DLL ----------------------------------------------------
$projekty = @{}
foreach ($f in $soubory.EDISON) {
    if ($f -match '/CACHE/|/DOCS/|\.md$' -or $f -notmatch '/') { continue }
    $p = Get-ProjektSouboru $k.repoEdison $f
    if ($p) { $projekty[$p] = $true }
}
$infoProjektu = @{}
function Get-Info([string]$Cesta) {
    if (-not $script:infoProjektu.ContainsKey($Cesta)) { $script:infoProjektu[$Cesta] = Get-ProjektInfo $Cesta }
    return $script:infoProjektu[$Cesta]
}
$uzaver = @{}
$fronta = New-Object System.Collections.Queue
foreach ($p in $projekty.Keys) { $fronta.Enqueue($p) }
while ($fronta.Count -gt 0) {
    $p = $fronta.Dequeue()
    if ($uzaver.ContainsKey($p) -or -not (Test-Path -LiteralPath $p)) { continue }
    $uzaver[$p] = $true
    foreach ($o in (Get-Info $p).ProjectReference) { $fronta.Enqueue($o) }
}
$predem = @()
if ($uzaver.Count -gt 0) {
    $podleAssembly = @{}
    foreach ($p in Get-VsechnyProjekty $k.repoEdison) {
        try { $i = Get-Info $p; if (-not $podleAssembly.ContainsKey($i.Assembly)) { $podleAssembly[$i.Assembly] = $i } } catch { }
    }
    $edisonDir = $k.edisonDir.TrimEnd('\') + '\'
    $hinty = @{}
    foreach ($p in $uzaver.Keys) {
        foreach ($h in (Get-Info $p).HintPath) {
            if ($h.StartsWith($edisonDir, [StringComparison]::OrdinalIgnoreCase) -and $h -match '\.dll$') { $hinty[[IO.Path]::GetFileNameWithoutExtension($h)] = $h }
        }
    }
    foreach ($assembly in $hinty.Keys) {
        $i = $podleAssembly[$assembly]
        if (-not $i -or $uzaver.ContainsKey($i.Cesta)) { continue }
        $s = Test-ZastaralaDll $k.repoEdison $hinty[$assembly] $i.Slozka
        if ($s -ne 'aktualni') { $predem += [ordered]@{ projekt = $i.Cesta.Substring($k.repoEdison.Length + 1); dll = "$assembly.dll"; stav = $s } }
    }
}
$edisonKoren = [IO.Path]::GetFullPath($k.edisonDir).TrimEnd('\')
$buildLegacy = @($projekty.Keys | Sort-Object | ForEach-Object {
    $i = Get-Info $_
    $nasaditelny = [bool]($i.SlozkaVystupu -and $i.SlozkaVystupu.TrimEnd('\') -ieq $edisonKoren)
    [ordered]@{ projekt = $_.Substring($k.repoEdison.Length + 1); vystup = $i.Vystup; nasaditelny = $nasaditelny }
})

# --- 6. Tridy Cache, projekty MCP, testy ------------------------------------------------------------
$mcp = [ordered]@{ platna = $false; chyba = ''; moduly = @() }
$mapaModulu = @{}
try {
    $konf = [IO.File]::ReadAllText($k.mcpKonfigurace) | ConvertFrom-Json
    $mcp.platna = $true
    foreach ($m in $konf.release.modules.PSObject.Properties) { $mapaModulu[$m.Name.ToUpperInvariant()] = @($m.Value.cacheProjects) }
    $mcp.moduly = @($mapaModulu.Keys | Sort-Object)
} catch { $mcp.chyba = $_.Exception.Message }

$tridy = @(); $dotceneModuly = @{}
foreach ($jmeno in $repa.Keys) {
    foreach ($f in $soubory[$jmeno]) {
        $t = Get-JmenoTridy $f
        if (-not $t) { continue }
        $modul = ($f -split '/')[0]
        $dotceneModuly["$jmeno|$modul"] = $true
        $tridy += [ordered]@{ trida = $t; repo = $jmeno; cesta = $f; modul = $modul; cacheProjekty = $mapaModulu[$modul.ToUpperInvariant()] }
    }
}
$chybiVMcp = @($dotceneModuly.Keys | ForEach-Object { ($_ -split '\|')[1] } | Where-Object { -not $mapaModulu.ContainsKey($_.ToUpperInvariant()) } | Sort-Object -Unique)

$cacheTesty = @(); $nepridavat = @()
foreach ($klic in $dotceneModuly.Keys) {
    $jmeno, $modul = $klic -split '\|'
    $repo = $repa[$jmeno]
    $testy = @((Invoke-Git $repo @('ls-files', "$modul/CACHE/*/Tests/*.cls")).Vystup | Where-Object { $_ })
    $cacheTesty += @($testy | ForEach-Object { $t = Get-JmenoTridy $_; $t.Substring(0, $t.IndexOf('.Tests.') + 6) })
    $g = Invoke-Git $repo @('grep', '-l', 'NEPRIDAVAT do Studio projektu', 'HEAD', '--', "$modul/CACHE/") -PovolChybu
    $nepridavat += @($g.Vystup | Where-Object { $_ } | ForEach-Object { Get-JmenoTridy ($_ -replace '^HEAD:', '') })
}
$cacheTesty = @($cacheTesty | Sort-Object -Unique)
$nepridavat = @($nepridavat | Where-Object { $_ } | Sort-Object -Unique)

# --- 7. Cil: soubory na DEV share nebo v poslednim TEST baliku --------------------------------------
$cilSoubory = [ordered]@{}
$cilSlozka = $null
$verzeCile = $null
$cilChyba = $null
$jmenaNaCili = @($k.shell.soubory) + @($k.td.soubor) + @($k.bezeZmeny) + @($buildLegacy | Where-Object { $_.nasaditelny } | ForEach-Object { $_.vystup })
$legacyShellu = @{}
foreach ($pr in $k.shell.projekty) {
    foreach ($h in (Get-ProjektInfo (Join-Path $k.repoEdison2 $pr)).HintPath) {
        if ($h.StartsWith($k.edisonDir, [StringComparison]::OrdinalIgnoreCase) -and $h -match '\.dll$') { $legacyShellu[[IO.Path]::GetFileName($h)] = $true }
    }
}
$jmenaNaCili = @($jmenaNaCili + $legacyShellu.Keys | Sort-Object -Unique)
if ($Faze -eq 'dev') {
    $cilSlozka = if ($Cil) { $Cil } else { $k.devShare }
    foreach ($n in $jmenaNaCili) {
        $p = Join-Path $cilSlozka $n
        if (Test-Path -LiteralPath $p) {
            $cilSoubory[$n] = [ordered]@{ md5 = Get-Md5 $p; verze = Get-ProduktVerze $p; cas = (Get-Item -LiteralPath $p).LastWriteTime.ToString('s') }
        } else { $cilSoubory[$n] = $null }
    }
} elseif ($BezBaliku) {
    $cilChyba = 'stav REL neovereny (-BezBaliku)'
} else {
    $verzeCile = $kanaly[$k.kanalTest]['LAST']
    if (-not $verzeCile -or $verzeCile -eq '0.0.0.0') {
        $cilChyba = "TEST verze se prave stavi nebo VERZE.INF nejde precist (LAST=$verzeCile), stav REL neznamy"
    } else {
        $dat = Get-Balik $k $k.kanalTest $verzeCile
        $cilSlozka = Join-Path (Split-Path -Parent $dat) 'soubory'
        New-Item -ItemType Directory -Force -Path $cilSlozka | Out-Null
        $vzor = '^(' + (($jmenaNaCili | ForEach-Object { [regex]::Escape($_) }) -join '|') + ')$'
        foreach ($e in Read-SetupDat $dat $vzor) {
            if ($null -eq $e.Data) { continue }
            $p = Join-Path $cilSlozka $e.Jmeno
            [IO.File]::WriteAllBytes($p, [byte[]]$e.Data)
            $cilSoubory[$e.Jmeno] = [ordered]@{ md5 = Get-Md5Bajtu $e.Data; verze = Get-ProduktVerze $p; podepsany = Test-PodepsanyPe $e.Data }
        }
    }
}

# Cas, kdy commit dorazil do originu: prvni zaznam reflogu upstreamu (push, fetch), ktery ho obsahuje. Kolega
# muze stavet s nasi zmenou az od pushe, cas commitu je drivejsi. Nepushnuty commit: $null.
$reflogUpstreamu = $null
function Get-CasVOriginu([string]$Commit) {
    if ($null -eq $script:reflogUpstreamu) {
        $radky = @((Invoke-Git $k.repoEdison @('log', '-g', '--date=iso-strict', '--format=%H|%gd', '@{u}') -PovolChybu).Vystup | Where-Object { $_ -match '\|' })
        [array]::Reverse($radky)
        $script:reflogUpstreamu = $radky
    }
    $r = $script:reflogUpstreamu
    $obsahuje = { param($i) (Invoke-Git $k.repoEdison @('merge-base', '--is-ancestor', $Commit, ($r[$i] -split '\|')[0]) -PovolChybu).Kod -eq 0 }
    if ($r.Count -eq 0 -or -not (& $obsahuje ($r.Count - 1))) { return $null }
    $od = 0; $do = $r.Count - 1
    while ($od -lt $do) { $s = [int][Math]::Floor(($od + $do) / 2); if (& $obsahuje $s) { $do = $s } else { $od = $s + 1 } }
    if ($r[$od] -match '@\{([^}]+)\}$') { return $Matches[1] }
    return $null
}

# Soubor na DEV share mladsi nez push posledni vlastni zmeny projektu postavil nekdo jiny (kolega, jiny chat) az
# po nem. Nas build by mel stejny kod a jine MD5 (legacy build neni deterministicky), tabulka ho proto nabidne
# jen jako volitelny a nahraj-na-dev.ps1 -UzNaCili ho zapocte bez kopie.
$nahraneDev = @{}
if ($stav.dev -and $stav.dev.soubory) { foreach ($pr in $stav.dev.soubory.PSObject.Properties) { $nahraneDev[$pr.Name] = $pr.Value } }
foreach ($b in $buildLegacy) {
    $b['vlastniZmena'] = $null; $b['cilPoZmene'] = $false
    $c = $cilSoubory[$b.vystup]
    if ($Faze -ne 'dev' -or -not $c -or -not $c.cas -or $nahraneDev[$b.vystup] -eq $c.md5) { continue }
    $slozka = (Split-Path -Parent $b.projekt) -replace '\\', '/'
    $posledni = ((Invoke-Git $k.repoEdison (@('log', '-1', '--no-merges', "--author=$autor", '--format=%H', "$($kotva.EDISON)..HEAD", '--', $slozka) + (Get-MimoBinarky))).Vystup -join '').Trim()
    if (-not $posledni) { continue }
    $b['vlastniZmena'] = Get-CasVOriginu $posledni
    $b['cilPoZmene'] = [bool]($b['vlastniZmena'] -and ([datetime]$c.cas) -gt ([datetimeoffset]$b['vlastniZmena']).LocalDateTime)
}

# --- 8. Shell ---------------------------------------------------------------------------------------
$zmenyShellu = @((Invoke-Git $k.repoEdison2 (@('log', '--format=%h|%an|%s', "$($kotva.EDISON2)..HEAD", '--') + @($k.shell.slozky) + @(Get-MimoBinarky))).Vystup | Where-Object { $_ })
$priZmene = @()
foreach ($p in $k.shell.souboryPriZmene.PSObject.Properties) {
    $z = (Invoke-Git $k.repoEdison2 (@('log', '--oneline', "$($kotva.EDISON2)..HEAD", '--') + @($p.Value) + @(Get-MimoBinarky))).Vystup | Where-Object { $_ }
    if (@($z).Count -gt 0) { $priZmene += $p.Name }
}
$unshipped = Get-GitText $k.repoEdison2 'HEAD' $k.td.publicApi
# Radek PublicAPI: typ "Ns.Typ", clen "Ns.Typ.Clen(args) -> T" nebo "Ns.Typ.Vlastnost.get -> T".
$odebrane = @(($unshipped -split "`r?`n") | Where-Object { $_ -like '`*REMOVED`**' })
$odebraneTypy = @($odebrane | ForEach-Object {
    $j = ($_ -replace '^\*REMOVED\*', '') -replace '^((static|virtual|override|abstract|const|readonly|sealed)\s+)+', ''
    if ($j -notmatch '\(| -> ') { ($j -split '\.')[-1] }
} | Where-Object { $_ -match '^[A-Z]' } | Sort-Object -Unique)
$td = [ordered]@{ verze = $null; commit = $null; stav = $null; typyVZdroji = @() }
if ($cilSoubory[$k.td.soubor]) {
    $td.verze = $cilSoubory[$k.td.soubor].verze
    if ($td.verze -match '\+([0-9a-f]{7,40})') {
        $td.commit = $Matches[1]
        # Starnuti TD meri jen odebrane API: pridany clen stary TD nerozbije.
        $zmenaApi = ((Invoke-Git $k.repoEdison2 @('log', '-1', '--format=%H', '-G', '^\*REMOVED\*', '--', $k.td.publicApi)).Vystup -join '').Trim()
        if ((Invoke-Git $k.repoEdison2 @('cat-file', '-e', "$($td.commit)^{commit}") -PovolChybu).Kod -ne 0) {
            $td.stav = 'commit TD neni v repu'
        } elseif (-not $zmenaApi) {
            $td.stav = 'API EDControls nic neodebira'
        } elseif ((Invoke-Git $k.repoEdison2 @('merge-base', '--is-ancestor', $zmenaApi, $td.commit) -PovolChybu).Kod -eq 0) {
            # TD sestaveny po zmene API odebrane cleny nevola, jinak by nesel prelozit.
            $td.stav = 'sestaveny po zmene API'
        } else {
            $td.stav = 'sestaveny pred zmenou API EDControls, vydat TD spolu s DLL'
            foreach ($jm in $odebraneTypy) {
                $g = Invoke-Git $k.repoEdison2 @('grep', '-l', '-w', $jm, $td.commit, '--', $k.td.zdroj) -PovolChybu
                if ($g.Kod -eq 0 -and @($g.Vystup | Where-Object { $_ -match '\.(cs|xaml)$' }).Count -gt 0) { $td.typyVZdroji += $jm }
            }
        }
    }
}
$legacyApi = @()
# Bez cile (-BezBaliku, stavba verze) nejde TD ani legacy DLL overit: prazdny vysledek by vypadal jako "sedi".
if (-not $cilSlozka) {
    $td.stav = 'cil neovereny'
    $legacyApi = @($legacyShellu.Keys | Where-Object { Test-Path -LiteralPath (Join-Path $k.edisonDir $_) } | ForEach-Object { [ordered]@{ dll = $_; stav = 'cil neovereny'; chybiClenu = $null } })
}
foreach ($dll in $(if ($cilSlozka) { @($legacyShellu.Keys) } else { @() })) {
    $mistni = Join-Path $k.edisonDir $dll
    $cilova = Join-Path $cilSlozka $dll
    if (-not (Test-Path -LiteralPath $mistni)) { continue }
    if (-not (Test-Path -LiteralPath $cilova)) { $legacyApi += [ordered]@{ dll = $dll; stav = 'na cili chybi'; chybiClenu = $null }; continue }
    if ((Get-Md5 $mistni) -eq (Get-Md5 $cilova)) { continue }
    $chybi = @(Compare-LegacyApi $mistni $cilova $k.edisonDir)
    $legacyApi += [ordered]@{ dll = $dll; stav = $(if ($chybi.Count -gt 0) { 'cil nema nektere verejne cleny lokalni verze' } else { 'jina, API sedi' }); chybiClenu = @($chybi | Select-Object -First 20) }
}

# --- 9. Strom --------------------------------------------------------------------------------------
$strom = [ordered]@{ edcoreShodaSGitem = $null; treeSync = $null; relPending = $null }
$edcore = Join-Path $k.edisonDir $k.strom.edcore
if (Test-Path -LiteralPath $edcore) {
    $strom.edcoreShodaSGitem = ((ConvertTo-PorovnatelnyText (Get-StromEdCore $edcore)) -eq (ConvertTo-PorovnatelnyText (Get-GitText $k.repoEdison 'HEAD' $k.strom.legacyXml)))
}
$st = Join-Path $k.treeSyncDir 'status.txt'
if (Test-Path -LiteralPath $st) { $strom.treeSync = (Get-Content -LiteralPath $st) -join '; ' }
$rp = Join-Path $k.treeSyncDir 'rel-pending.txt'
if (Test-Path -LiteralPath $rp) { $strom.relPending = (Get-Content -LiteralPath $rp | Measure-Object).Count }

# --- Vystup -----------------------------------------------------------------------------------------
$inventura = [ordered]@{
    faze = $Faze; cas = (Get-Date).ToString('s'); autor = $autor
    repa = $infoRep; kanaly = $kanaly; staviSe = $staviSe; kotva = $kotva
    commity = $commity; cizi = $cizi
    buildLegacy = $buildLegacy; predem = $predem
    tridy = $tridy; cacheTesty = $cacheTesty; nepridavat = $nepridavat
    mcp = [ordered]@{ platna = $mcp.platna; chyba = $mcp.chyba; chybiModuly = $chybiVMcp }
    shell = [ordered]@{ zmena = ($zmenyShellu.Count -gt 0); commity = $zmenyShellu.Count; souboryPriZmene = $priZmene; odebraneApi = $odebrane.Count; td = $td; legacyApi = $legacyApi }
    cil = [ordered]@{ slozka = $cilSlozka; verze = $verzeCile; chyba = $cilChyba; soubory = $cilSoubory }
    strom = $strom
}
$vystup = Join-Path $k.stavDir "seznam-$Faze-$(New-CasovaZnacka).json"
Write-Json $vystup $inventura

foreach ($jmeno in $repa.Keys) {
    $r = $infoRep[$jmeno]
    Write-Host ("{0}: pred originem {1}, za {2}, necommitovanych {3}{4}" -f $jmeno, $r.predOriginem, $r.zaOriginem, $r.necommitovane.Count,
        $(if ($r.konflikt.Count) { ", KONFLIKT v $($r.konflikt.Count) souborech ($($r.autoriKonfliktu -join ', '))" } else { '' }))
}
Write-Host ("Kanaly: " + (($kanaly.Keys | ForEach-Object { "$_ LAST=$($kanaly[$_]['LAST'])" }) -join ', ') + $(if ($staviSe) { ' (NEKDO STAVI)' } else { '' }))
Write-Host ("Commity od kotvy: EDISON {0} tvych, EDISON2 {1}, cizich ve stejnych slozkach {2}" -f $commity.EDISON.Count, $commity.EDISON2.Count, $cizi.Count)
Write-Host ("Legacy build: {0}; predem: {1}" -f (($buildLegacy | ForEach-Object { "$($_.vystup)$(if (-not $_.nasaditelny) { ' (nestavi do C:\Edison, nenasaditelny)' })" }) -join ', '), (($predem | ForEach-Object { "$($_.dll) ($($_.stav))" }) -join ', '))
Write-Host ("Tridy: {0}, Cache testy: {1}, NEPRIDAVAT: {2}, MCP: {3}" -f $tridy.Count, ($cacheTesty -join ', '), $nepridavat.Count, $(if ($mcp.platna) { if ($chybiVMcp.Count) { "chybi $($chybiVMcp -join ', ')" } else { 'ok' } } else { "NEPLATNA: $($mcp.chyba)" }))
Write-Host ("Shell: {0} commitu, odebrane API {1}, TD na cili {2} ({3}), legacy API: {4}" -f $zmenyShellu.Count, $odebrane.Count, $td.verze, $td.stav,
    (($legacyApi | ForEach-Object { "$($_.dll) $($_.stav)" }) -join ', '))
Write-Host ("Strom: EDCore = git {0}, tree-sync: {1}" -f $strom.edcoreShodaSGitem, $strom.treeSync)
$vystup
