# Testy skriptu v tools/nasazeni. Bez site, DEV a REL: repa, balik i DEV share jsou docasne fixture.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/nasazeni/tests/spust.ps1
#
# Exit 0 = vse proslo. ASCII-only zamerne: WinPS 5.1 cte UTF-8 bez BOM jako CP-1252.

$ErrorActionPreference = 'Stop'
$nasazeni = Split-Path -Parent $PSScriptRoot
$koren = Join-Path $env:TEMP "nasazeni-test-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
New-Item -ItemType Directory -Force -Path $koren | Out-Null
$env:EDISON_NASAD_HOME = Join-Path $koren 'stav'
Import-Module (Join-Path $nasazeni 'spolecne.psm1') -Force
$utf8 = New-Object Text.UTF8Encoding($false)
$script:proslo = 0; $script:selhalo = @()

function Test-Plati([bool]$Podminka, [string]$Popis) {
    if ($Podminka) { $script:proslo++ } else { $script:selhalo += $Popis; Write-Host "SELHALO: $Popis" }
}
function Test-Vyhodi([scriptblock]$Blok, [string]$Vzor, [string]$Popis) {
    try { & $Blok | Out-Null; $script:selhalo += "$Popis (nevyhodilo)"; Write-Host "SELHALO: $Popis (nevyhodilo)" }
    catch { Test-Plati ($_.Exception.Message -match $Vzor) "$Popis (vyjimka: $($_.Exception.Message))" }
}
function Set-Text([string]$Cesta, [string]$Text) {
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Cesta) | Out-Null
    [IO.File]::WriteAllText($Cesta, $Text, $utf8)
}
function Invoke-G([string]$Repo, [string[]]$Argumenty) {
    $r = Invoke-Prikaz 'git' (@('-C', $Repo, '-c', 'core.autocrlf=false') + $Argumenty)
    if ($r.Kod -ne 0) { throw "git $($Argumenty -join ' '): $($r.Vystup -join ' ')" }
    return (($r.Vystup | Where-Object { $_ }) -join "`n")
}
function New-Klon([string]$Origin, [string]$Cesta, [string]$Autor) {
    Invoke-Prikaz 'git' @('clone', '-q', '-c', 'core.autocrlf=false', $Origin, $Cesta) | Out-Null
    Invoke-G $Cesta @('config', 'user.name', $Autor) | Out-Null
    Invoke-G $Cesta @('config', 'user.email', "$($Autor -replace ' ', '.')@test") | Out-Null
}
function Add-Commit([string]$Repo, [string]$Zprava, [hashtable]$Soubory, [string]$Datum, [string]$Autor) {
    foreach ($kl in $Soubory.Keys) { Set-Text (Join-Path $Repo $kl) $Soubory[$kl] }
    Invoke-G $Repo @('add', '-A') | Out-Null
    if ($Datum) { $env:GIT_COMMITTER_DATE = $Datum; $env:GIT_AUTHOR_DATE = $Datum }
    $kdo = if ($Autor) { @('-c', "user.name=$Autor", '-c', "user.email=$Autor@test") } else { @() }
    try { Invoke-G $Repo ($kdo + @('commit', '-q', '-m', $Zprava)) | Out-Null }
    finally { Remove-Item Env:\GIT_COMMITTER_DATE, Env:\GIT_AUTHOR_DATE -ErrorAction SilentlyContinue }
    return Invoke-G $Repo @('rev-parse', 'HEAD')
}
function New-SetupDat([string]$Cesta, $Polozky) {
    $ms = New-Object IO.MemoryStream
    $enc = [Text.Encoding]::GetEncoding(1250)
    $zapis = { param($t) $b = $enc.GetBytes($t); $ms.Write($b, 0, $b.Length) }
    & $zapis ('{0:D3}' -f $Polozky.Count)
    foreach ($kl in $Polozky.Keys) {
        $data = $Polozky[$kl]; if ($data -is [string]) { $data = [Text.Encoding]::UTF8.GetBytes($data) }
        & $zapis ('{0:D9}{1:D3}{2}' -f $data.Length, $kl.Length, $kl); $ms.Write($data, 0, $data.Length)
    }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Cesta) | Out-Null
    [IO.File]::WriteAllBytes($Cesta, $ms.ToArray())
}
function New-Dll([string]$Slozka, [string]$Jmeno, [string]$Kod) {
    New-Item -ItemType Directory -Force -Path $Slozka | Out-Null
    $cesta = Join-Path $Slozka "$Jmeno.dll"
    Add-Type -TypeDefinition $Kod -OutputAssembly $cesta -OutputType Library
    return $cesta
}

try {
    # --- Otisk binarky ---------------------------------------------------------------------------
    $dll = New-Dll (Join-Path $koren 'pe') 'PeTest' 'public class PeTest { public int X; }'
    $puvodni = [IO.File]::ReadAllBytes($dll)
    $nepodepsany = [byte[]]($puvodni + [byte[]](1, 2, 3))      # delka, ktera neni nasobek 8
    Test-Plati (($nepodepsany.Length % 8) -ne 0) 'fixture: delka nepodepsaneho souboru neni nasobek 8'
    $vypln = (8 - ($nepodepsany.Length % 8)) % 8
    $blok = [byte[]](1..24 | ForEach-Object { 0xAB })
    $podepsany = [byte[]]($nepodepsany + (New-Object byte[] $vypln) + $blok)
    $cert = Get-PeCertPozice $podepsany
    [BitConverter]::GetBytes([uint32]($nepodepsany.Length + $vypln)).CopyTo($podepsany, $cert)
    [BitConverter]::GetBytes([uint32]$blok.Length).CopyTo($podepsany, $cert + 4)
    $pe = [BitConverter]::ToInt32($podepsany, 0x3C)
    [BitConverter]::GetBytes([uint32]0x12345678).CopyTo($podepsany, $pe + 24 + 64)
    Test-Plati ((Get-Md5Bajtu $nepodepsany) -ne (Get-Md5Bajtu $podepsany)) 'podpis zmeni MD5'
    Test-Plati ((Get-PeOtisk $nepodepsany) -eq (Get-PeOtisk $podepsany)) 'otisk podepsane a nepodepsane binarky je stejny i s vyplni'
    Test-Plati (Test-PodepsanyPe $podepsany) 'podepsana binarka se pozna'
    Test-Plati (-not (Test-PodepsanyPe $nepodepsany)) 'nepodepsana binarka se pozna'
    Test-Plati (Test-ShodaBinarek $nepodepsany $podepsany) 'shoda binarek pres otisk u podepsane'
    $jina = [byte[]]$nepodepsany.Clone(); $jina[$jina.Length - 1] = 9
    Test-Plati (-not (Test-ShodaBinarek $jina $podepsany)) 'jina binarka se podpisem neschova'
    $jenChecksum = [byte[]]$nepodepsany.Clone(); [BitConverter]::GetBytes([uint32]7).CopyTo($jenChecksum, $pe + 24 + 64)
    Test-Plati (-not (Test-ShodaBinarek $nepodepsany $jenChecksum)) 'bez podpisu se porovnava MD5, ne otisk'

    # --- Balik SETUP.DAT a VERZE.INF -----------------------------------------------------------------
    $polozky = [ordered]@{ 'ALVA_X.XML' = [Text.Encoding]::UTF8.GetBytes('<p ts="2026-10-20 10:00"><c>a</c></p>'); 'A.exe' = [byte[]](1..10) }
    $ms = New-Object IO.MemoryStream
    $zapis = { param($t) $b = [Text.Encoding]::GetEncoding(1250).GetBytes($t); $ms.Write($b, 0, $b.Length) }
    & $zapis ('{0:D3}' -f $polozky.Count)
    foreach ($kl in $polozky.Keys) { & $zapis ('{0:D9}{1:D3}{2}' -f $polozky[$kl].Length, $kl.Length, $kl); $ms.Write($polozky[$kl], 0, $polozky[$kl].Length) }
    $setup = Join-Path $koren 'SETUP.DAT'; [IO.File]::WriteAllBytes($setup, $ms.ToArray())
    $nactene = @(Read-SetupDat $setup '^A\.exe$')
    Test-Plati ($nactene.Count -eq 2 -and $nactene[0].Jmeno -eq 'ALVA_X.XML' -and $null -eq $nactene[0].Data) 'SETUP.DAT: jmena a obsah jen na vyzadani'
    Test-Plati ((Get-Md5Bajtu ([byte[]]$nactene[1].Data)) -eq (Get-Md5Bajtu $polozky['A.exe'])) 'SETUP.DAT: obsah binarky'
    $o1 = Get-XmlOtisk ([Text.Encoding]::UTF8.GetBytes('<p ts="1"><TimeChanged>1</TimeChanged><c>a</c></p>'))
    $o2 = Get-XmlOtisk ([Text.Encoding]::UTF8.GetBytes('<p ts="2"><TimeChanged>2</TimeChanged><c>a</c></p>'))
    $o3 = Get-XmlOtisk ([Text.Encoding]::UTF8.GetBytes('<p ts="2"><TimeChanged>2</TimeChanged><c>b</c></p>'))
    Test-Plati ($o1 -eq $o2 -and $o2 -ne $o3) 'XML se lisi jen obsahem, ne casovym razitkem'
    $v = Read-VerzeInf "LAST=261020.0.5`r`nTYPE=3`r`n"
    Test-Plati ($v['LAST'] -eq '261020.0.5' -and $v['TYPE'] -eq '3') 'VERZE.INF'
    $rel = Join-Path $koren 'rel'; Set-Text (Join-Path $rel 'TEST\VERZE.INF') "LAST=0.0.0.0`r`n"
    $nastaveniRel = [pscustomobject]@{ relUrl = ([Uri]$rel).AbsoluteUri }
    Test-Vyhodi { Wait-Verze $nastaveniRel 'TEST' '261020.1' 2 1 } 'neobjevila' 'cekani na verzi ma limit, 0.0.0.0 neni nova verze'
    Set-Text (Join-Path $rel 'TEST\VERZE.INF') "LAST=261021.0`r`n"
    Test-Plati ((Wait-Verze $nastaveniRel 'TEST' '261020.1' 5 1) -eq '261021.0') 'cekani vrati novou verzi'

    # --- API legacy DLL ---------------------------------------------------------------------------
    $v1 = New-Dll (Join-Path $koren 'api1') 'LegApi' 'namespace L { public class K { public int A() { return 1; } public int B() { return 2; } } }'
    $v2 = New-Dll (Join-Path $koren 'api2') 'LegApi' 'namespace L { public class K { public int A() { return 1; } public int C() { return 3; } } }'
    $chybi = @(Compare-LegacyApi $v1 $v2)
    Test-Plati ($chybi.Count -eq 1 -and $chybi[0] -match 'L\.K\.B\(') 'API: clen, ktery cil nema, se najde'
    Test-Plati (@(Compare-LegacyApi $v1 $v1).Count -eq 0) 'API: stejna verze nema rozdil'

    # --- Fixture rep EDISON a EDISON2 ---------------------------------------------------------------
    $edisonDir = Join-Path $koren 'Edison'; $share = Join-Path $koren 'share'
    New-Item -ItemType Directory -Force -Path $edisonDir, $share | Out-Null
    $origin = Join-Path $koren 'origin'; New-Item -ItemType Directory -Force -Path $origin | Out-Null
    foreach ($o in 'EDISON.git', 'EDISON2.git') { Invoke-Prikaz 'git' @('init', '-q', '--bare', '-b', 'main', (Join-Path $origin $o)) | Out-Null }
    $repa = Join-Path $koren 'repa'; $e1 = Join-Path $repa 'EDISON'; $e2 = Join-Path $repa 'EDISON2'
    $cizi = Join-Path $koren 'cizi\EDISON'
    $hintExt = Join-Path $edisonDir 'EXT.dll'; $hintLib2 = Join-Path $edisonDir 'LIB2.dll'
    $proj = {
        param($jmeno, $typ, $navic, $vystup)
        if (-not $vystup) { $vystup = "$edisonDir\" }
        "<Project><PropertyGroup><AssemblyName>$jmeno</AssemblyName><OutputType>$typ</OutputType></PropertyGroup><PropertyGroup Condition=`"'`$(Configuration)' == 'Release'`"><OutputPath>$vystup</OutputPath></PropertyGroup><ItemGroup>$navic</ItemGroup></Project>"
    }

    New-Klon (Join-Path $origin 'EDISON.git') $cizi 'Kolega'
    $kotva1 = Add-Commit $cizi 'zaklad' @{
        'MOD/CACHE/ALVA/MOD/A.cls' = "Class ALVA.MOD.A`n{`n/// popis`n}`n"
        'MOD/CACHE/ALVA/MOD/Tests/T.cls' = "/// NEPRIDAVAT do Studio projektu ALVA_MOD (nesmi se dostat do distribuce).`nClass ALVA.MOD.Tests.T`n{`n}`n"
        'MOD/CACHE/ALVA/MOD/B.cls' = "Class ALVA.MOD.B`n{`n}`n"
        'DIA/DIA.csproj' = (& $proj 'DIA' 'Library' '' 'bin\Release\'); 'DIA/d.cs' = 'class D {}'
        'APP/APP.csproj' = (& $proj 'APP' 'WinExe' "<Reference Include=`"EXT`"><HintPath>$hintExt</HintPath></Reference><Reference Include=`"LIB2`"><HintPath>$hintLib2</HintPath></Reference>")
        'APP/a.cs' = 'class A {}'
        'EXT/EXT.csproj' = (& $proj 'EXT' 'Library' ''); 'EXT/e.cs' = 'class E {}'
        'LIB2/LIB2.csproj' = (& $proj 'LIB2' 'Library' ''); 'LIB2/l.cs' = 'class L {}'
        'EDCORE/Resources/edisontree.xml' = '<tree/>'
    }
    Invoke-G $cizi @('push', '-q', 'origin', 'HEAD:main') | Out-Null
    New-Klon (Join-Path $origin 'EDISON.git') $e1 'Martin Test'
    Start-Sleep -Seconds 2
    [IO.File]::WriteAllBytes($hintExt, [byte[]](1, 2)); [IO.File]::WriteAllBytes($hintLib2, [byte[]](3, 4))
    Start-Sleep -Seconds 2
    # Kolegova zmena EXT ma datum pred buildem DLL, do repa ale prijde az pullem po nem.
    Add-Commit $cizi 'EXT zmena' @{ 'EXT/e.cs' = 'class E { int x; }' } (Get-Date).AddHours(-2).ToString('s') | Out-Null
    Add-Commit $cizi 'kolega meni A' @{ 'MOD/CACHE/ALVA/MOD/A.cls' = "Class ALVA.MOD.A`n{`n/// kolega`n}`n" } | Out-Null
    Invoke-G $cizi @('push', '-q', 'origin', 'HEAD:main') | Out-Null
    Invoke-G $e1 @('pull', '-q', '--ff-only') | Out-Null
    Add-Commit $cizi 'kolega meni A znovu' @{ 'MOD/CACHE/ALVA/MOD/A.cls' = "Class ALVA.MOD.A`n{`n/// kolega 2`n}`n" } | Out-Null
    Invoke-G $cizi @('push', '-q', 'origin', 'HEAD:main') | Out-Null
    Remove-Item -LiteralPath (Join-Path $e1 'MOD\CACHE\ALVA\MOD\B.cls')
    Add-Commit $e1 "moje zm$([char]0x11B)na" @{ 'MOD/CACHE/ALVA/MOD/A.cls' = "Class ALVA.MOD.A`n{`n/// moje`n}`n"; 'APP/a.cs' = 'class A { int y; }'; 'MOD/CACHE/OBapi.int' = 'ROUTINE OBapi'
        "DIA/p$([char]0x159)iklad.cs" = 'class P {}' } | Out-Null
    Invoke-G $e1 @('fetch', '-q') | Out-Null

    New-Klon (Join-Path $origin 'EDISON2.git') $e2 'Martin Test'
    $leg1 = New-Dll (Join-Path $koren 'leg-lokalni') 'LEG' 'public class G { public void Nova() {} public void Stara() {} }'
    $leg2 = New-Dll (Join-Path $koren 'leg-cil') 'LEG' 'public class G { public void Stara() {} }'
    Copy-Item $leg1 (Join-Path $edisonDir 'LEG.dll'); Copy-Item $leg2 (Join-Path $share 'LEG.dll')
    $kotva2 = Add-Commit $e2 'zaklad' @{
        'SHELL/SHELL.csproj' = (& $proj 'SHELL' 'WinExe' "<Reference Include=`"LEG`"><HintPath>$(Join-Path $edisonDir 'LEG.dll')</HintPath></Reference>")
        'EDControls/PublicAPI.Unshipped.txt' = ''
        'SHELL/CACHE/ALVA/SHELL/Tree.cls' = 'd ..AddExe("A", "A.exe", "F", "1", 1, "", 0, "", 0)'
        'TD/okno.cs' = 'class Okno { Stary s; }'
    }
    Invoke-G $e2 @('push', '-q', 'origin', 'HEAD:main') | Out-Null
    $poShellu = Add-Commit $e2 'shell zmena' @{ 'SHELL/s.cs' = 'class S {}' }
    Add-Commit $e2 'strom' @{ 'SHELL/CACHE/ALVA/SHELL/Tree.cls' = 'd ..AddExe("B", "B.exe", "F", "2", 1, "", 0, "", 0)' } $null 'tree-sync' | Out-Null
    Add-Commit $e2 'TD kolegy' @{ 'TD/CACHE/ALVA/TD/X.cls' = 'Class ALVA.TD.X {}' } $null 'Jan' | Out-Null
    Add-Commit $e2 'dokumentace' @{ 'SHELL/CLAUDE.md' = 'poznamka'; 'UPD/CLAUDE.md' = 'poznamka' } | Out-Null
    $td = New-Dll (Join-Path $koren 'td') 'TD' "[assembly: System.Reflection.AssemblyInformationalVersion(`"1.0.0+$kotva2`")] public class Td {}"
    Copy-Item $td (Join-Path $share 'TD.exe')

    $mcp = Join-Path $koren 'mcp.json'
    Set-Text $mcp '{ "release": { "modules": { "MOD": { "cacheProjects": [ "ALVA_MOD" ] } } } }'
    $nast = Join-Path $koren 'nastaveni.json'
    Set-Text $nast (([ordered]@{
        edisonRepo = '..\EDISON'; edisonDir = $edisonDir; devShare = $share; relUrl = ([Uri]$rel).AbsoluteUri
        kanalRel = 'TEST'; kanalTest = 'TEST'; sevenZip = 'neni'; mcpKonfigurace = $mcp
        stavDir = (Join-Path $koren 'stav'); zalohyDir = (Join-Path $koren 'zalohy'); treeSyncDir = (Join-Path $koren 'treesync')
        shell = [ordered]@{ soubory = @('SHELL.exe'); souboryPriZmene = [ordered]@{ 'UPD.exe' = @('UPD') }; slozky = @('SHELL'); projekty = @('SHELL/SHELL.csproj'); publikovat = 'SHELL/SHELL.csproj'; publishProfil = 'X' }
        td = [ordered]@{ soubor = 'TD.exe'; zdroj = 'TD'; publicApi = 'EDControls/PublicAPI.Unshipped.txt' }
        bezeZmeny = @('B.exe')
        strom = [ordered]@{ edcore = 'EDCore.dll'; legacyXml = 'EDCORE/Resources/edisontree.xml'; treeCls = 'SHELL/CACHE/ALVA/SHELL/Tree.cls'; projekt = 'ALVA_SHELL' }
    }) | ConvertTo-Json -Depth 5)
    $env:EDISON_NASAD_REPO2 = $e2

    # --- co-nasadit.ps1 ------------------------------------------------------------------------------
    $skript = Join-Path $nasazeni 'co-nasadit.ps1'
    $r = Invoke-Prikaz 'powershell' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $skript, '-Faze', 'dev', '-KotvaEdison', $kotva1, '-KotvaEdison2', $kotva2, '-BezFetch', '-Nastaveni', $nast)
    Test-Plati ($r.Kod -eq 0) "co-nasadit probehl ($($r.Vystup | Select-Object -Last 3))"
    $seznamCesta = @($r.Vystup | Where-Object { $_ -match 'seznam-dev-.*\.json$' })[-1]
    $s = [IO.File]::ReadAllText($seznamCesta, $utf8) | ConvertFrom-Json
    Test-Plati (@($s.repa.EDISON.konflikt) -contains 'MOD/CACHE/ALVA/MOD/A.cls') 'predpoved konfliktu pullu'
    Test-Plati (@($s.repa.EDISON.autoriKonfliktu) -contains 'Kolega') 'autor protistrany konfliktu'
    Test-Plati (@($s.buildLegacy | ForEach-Object { $_.vystup }) -contains 'APP.exe') 'zmeneny projekt jde do buildu'
    Test-Plati (@($s.predem | ForEach-Object { $_.dll }) -contains 'EXT.dll') 'DLL zastarala podle reflogu, i kdyz je commit starsi nez soubor'
    Test-Plati (-not (@($s.predem | ForEach-Object { $_.dll }) -contains 'LIB2.dll')) 'aktualni DLL se nestavi'
    Test-Plati (@($s.nepridavat) -contains 'ALVA.MOD.Tests.T') 'test s hlavickou NEPRIDAVAT'
    Test-Plati (@($s.cacheTesty) -contains 'ALVA.MOD.Tests') 'balik testu dotceneho modulu'
    Test-Plati (@($s.tridy | Where-Object { $_.trida -eq 'ALVA.MOD.A' -and @($_.cacheProjekty) -contains 'ALVA_MOD' }).Count -eq 1) 'trida a jeji Studio projekt'
    Test-Plati (@($s.shell.legacyApi | Where-Object { $_.dll -eq 'LEG.dll' -and $_.stav -eq 'cil nema nektere verejne cleny lokalni verze' }).Count -eq 1) 'legacy DLL na cili bez API shellu'
    Test-Plati ($s.shell.zmena) 'zmena shellu od kotvy'
    Test-Plati (@($s.tridy | ForEach-Object { $_.trida }) -contains 'OBapi.int') 'rutina .int je dokument Cache k nasazeni'
    Test-Plati (@($s.tridy | ForEach-Object { $_.trida }) -contains 'ALVA.SHELL.Tree') 'EDISON2: strom od tree-sync jde k nasazeni i bez meho commitu'
    Test-Plati (-not (@($s.tridy | ForEach-Object { $_.trida }) -contains 'ALVA.TD.X')) 'EDISON2: TD kolegy se nenasazuje'
    Test-Plati ($s.commity.EDISON.Count -eq 1) 'jeden commit se pocita jako jeden'
    Test-Plati ($s.commity.EDISON[0].zprava -eq "moje zm$([char]0x11B)na") "zprava commitu s diakritikou ($($s.commity.EDISON[0].zprava))"
    Test-Plati (@($s.buildLegacy | ForEach-Object { $_.vystup }) -contains 'DIA.dll') 'soubor s diakritikou v ceste najde svuj projekt'
    Test-Plati (@($s.buildLegacy | Where-Object { $_.vystup -eq 'APP.exe' -and $_.nasaditelny }).Count -eq 1) 'projekt s vystupem do C:\Edison je nasaditelny'
    Test-Plati (@($s.buildLegacy | Where-Object { $_.vystup -eq 'DIA.dll' -and -not $_.nasaditelny }).Count -eq 1) 'projekt s vystupem jinam je nenasaditelny'
    Test-Plati (-not (@($s.tridy | ForEach-Object { $_.trida }) -contains 'ALVA.MOD.B')) 'smazana trida se nenasazuje'
    Test-Plati ($s.shell.td.stav -eq 'API EDControls nic neodebira') "TD: bez odebraneho API neni co resit ($($s.shell.td.stav))"
    Test-Plati (-not (@($s.shell.souboryPriZmene) -contains 'UPD.exe')) 'zmena jen dokumentace nevynuti EDUPDATE2.exe'
    Test-Plati (@($s.cizi | Where-Object { $_.modul -eq 'MOD' -and $_.autor -eq 'Kolega' }).Count -ge 1) 'cizi commit nese modul'

    # Clenove projektu na DEV: clen mimo git, rozdelany v hlavni kopii, test NEPRIDAVAT, trida ze seznamu mimo projekt.
    $clenove = Join-Path $koren 'clenove'; Set-Text (Join-Path $clenove 'ALVA_MOD.txt') "ALVA.MOD.A|CLS`nALVA.MOD.Tests.T|CLS`nALVA.MOD.Ghost|CLS`n"
    Set-Text (Join-Path $e1 'MOD\CACHE\ALVA\MOD\A.cls') 'rozdelana prace'
    $r = Invoke-Prikaz 'powershell' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $skript, '-ClenoveProjektu', $clenove, '-Seznam', $seznamCesta, '-Nastaveni', $nast)
    Invoke-G $e1 @('checkout', '--', 'MOD/CACHE/ALVA/MOD/A.cls') | Out-Null
    $v = @([IO.File]::ReadAllText(@($r.Vystup | Where-Object { $_ -match 'clenove-.*\.json$' })[-1], $utf8) | ConvertFrom-Json)[0]
    Test-Plati ($r.Kod -eq 1 -and @($v.mimoGit) -contains 'ALVA.MOD.Ghost' -and @($v.necommitovane) -contains 'ALVA.MOD.A' -and @($v.testy) -contains 'ALVA.MOD.Tests.T' -and @($v.chybiVProjektu) -contains 'OBapi.int') "clenove projektu proti gitu a seznamu ($($r.Vystup -join ' '))"
    Set-Text (Join-Path $clenove 'ALVA_MOD.txt') "ALVA.MOD.A|CLS`nOBapi.INT|INT`n"
    $r = Invoke-Prikaz 'powershell' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $skript, '-ClenoveProjektu', $clenove, '-Seznam', $seznamCesta, '-Nastaveni', $nast)
    Test-Plati ($r.Kod -eq 0) "cisty projekt projde ($($r.Vystup -join ' '))"
    Set-Text (Join-Path $clenove 'ALVA_MOD.txt') "ALVA.MOD.A|CLS`nALVA.MOD.Tests.T|CLS`n"
    $r = Invoke-Prikaz 'powershell' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $skript, '-ClenoveProjektu', $clenove, '-Nastaveni', $nast)
    Test-Plati ($r.Kod -eq 1 -and ($r.Vystup -join ' ') -match 'testy ALVA\.MOD\.Tests\.T') 'test NEPRIDAVAT v projektu se najde i bez seznamu'

    # --- kotva ---------------------------------------------------------------------------------------
    $nastData = Get-Nastaveni $nast
    $seznamKotva = [pscustomobject]@{ kotva = [pscustomobject]@{ EDISON = 'stara1'; EDISON2 = 'stara2' }; repa = [pscustomobject]@{ EDISON = [pscustomobject]@{ head = 'nova1' }; EDISON2 = [pscustomobject]@{ head = 'nova2' } }
        buildLegacy = @([pscustomobject]@{ vystup = 'APP.exe' }); shell = [pscustomobject]@{ zmena = $true; souboryPriZmene = @() } }
    $sestaveniKotva = [pscustomobject]@{ soubory = @() }
    $kv = Get-KotvaPoNasazeni $nastData $seznamKotva $sestaveniKotva @('APP.exe')
    Test-Plati ($kv.commity.EDISON -eq 'stara1' -and @($kv.chybi) -contains 'SHELL.exe') 'castecne nasazeni kotvu neposune'
    $kv = Get-KotvaPoNasazeni $nastData $seznamKotva $sestaveniKotva @('APP.exe', 'SHELL.exe')
    Test-Plati ($kv.commity.EDISON -eq 'nova1') 'uplne nasazeni kotvu posune'
    $seznamKotva.buildLegacy += [pscustomobject]@{ vystup = 'MCP.exe'; nasaditelny = $false }
    $kv = Get-KotvaPoNasazeni $nastData $seznamKotva $sestaveniKotva @('APP.exe', 'SHELL.exe')
    Test-Plati ($kv.commity.EDISON -eq 'nova1') 'nenasaditelny projekt kotvu nedrzi'
    $seznamKotva | Add-Member -NotePropertyName tridy -NotePropertyValue @([pscustomobject]@{ trida = 'ALVA.MOD.A'; cacheProjekty = @('ALVA_MOD') })
    $kv = Get-KotvaPoNasazeni $nastData $seznamKotva $sestaveniKotva @('APP.exe', 'SHELL.exe') -VcetneProjektu
    Test-Plati ($kv.commity.EDISON -eq 'stara1' -and @($kv.chybi) -contains 'ALVA_MOD.XML') 'REL: projekt Cache bez zmeneho XML kotvu drzi'
    $kv = Get-KotvaPoNasazeni $nastData $seznamKotva $sestaveniKotva @('APP.exe', 'SHELL.exe', 'ALVA_MOD.XML') -VcetneProjektu
    Test-Plati ($kv.commity.EDISON -eq 'nova1') 'REL: se zmenenym XML projektu se kotva posune'
    $seznamKotva.tridy += [pscustomobject]@{ trida = 'ALVA.MOD.Tests.T'; cacheProjekty = @('ALVA_TESTY') }
    $seznamKotva | Add-Member -NotePropertyName nepridavat -NotePropertyValue @('ALVA.MOD.Tests.T')
    $kv = Get-KotvaPoNasazeni $nastData $seznamKotva $sestaveniKotva @('APP.exe', 'SHELL.exe', 'ALVA_MOD.XML') -VcetneProjektu
    Test-Plati ($kv.commity.EDISON -eq 'nova1') 'REL: zmena testovaci tridy NEPRIDAVAT kotvu nedrzi'
    $seznamKotva.tridy += [pscustomobject]@{ trida = 'ALVA.LIC.X'; modul = 'Licence'; cacheProjekty = $null }
    $kv = Get-KotvaPoNasazeni $nastData $seznamKotva $sestaveniKotva @('APP.exe', 'SHELL.exe', 'ALVA_MOD.XML') -VcetneProjektu
    Test-Plati ($kv.commity.EDISON -eq 'stara1' -and ($kv.chybi -join ' ') -match 'Licence \(modul mimo') 'REL: trida z modulu mimo konfiguraci MCP kotvu drzi'
    # Soubor postaveny pred commitem, ktery inventura videla a ktery jeho projekt meni, kotvu drzi.
    $headE1 = Invoke-G $e1 @('rev-parse', 'HEAD'); $headE2 = Invoke-G $e2 @('rev-parse', 'HEAD')
    $seznamGit = [pscustomobject]@{ kotva = [pscustomobject]@{ EDISON = 'stara1'; EDISON2 = 'stara2' }; autor = 'Martin Test'
        repa = [pscustomobject]@{ EDISON = [pscustomobject]@{ head = $headE1 }; EDISON2 = [pscustomobject]@{ head = $headE2 } }
        buildLegacy = @([pscustomobject]@{ vystup = 'APP.exe'; projekt = 'APP\APP.csproj'; nasaditelny = $true }); shell = [pscustomobject]@{ zmena = $true; souboryPriZmene = @() } }
    $sestaveniGit = { param($app, $shell) [pscustomobject]@{ soubory = @([pscustomobject]@{ jmeno = 'APP.exe'; commit = $app }, [pscustomobject]@{ jmeno = 'SHELL.exe'; commit = $shell }) } }
    $kv = Get-KotvaPoNasazeni $nastData $seznamGit (& $sestaveniGit $kotva1 $headE2) @('APP.exe', 'SHELL.exe')
    Test-Plati ($kv.commity.EDISON -eq 'stara1' -and ($kv.chybi -join ' ') -match 'APP\.exe \(postaveny pred') 'soubor postaveny pred zmenou ze seznamu kotvu drzi'
    $kv = Get-KotvaPoNasazeni $nastData $seznamGit (& $sestaveniGit $headE1 $poShellu) @('APP.exe', 'SHELL.exe')
    Test-Plati ($kv.commity.EDISON -eq $headE1 -and $kv.commity.EDISON2 -eq $headE2) "strom, TD ani dokumentace po buildu shellu kotvu nedrzi, kotva = commity inventury ($($kv.chybi -join ', '))"
    Test-Plati (@(Get-TestyVProjektu ([Text.Encoding]::UTF8.GetBytes('<Project><Items><ProjectItem name="PKG:ALVA.MOD" type="PKG"/></Items></Project><Class name="ALVA.MOD.Tests.T">'))) -contains 'ALVA.MOD.Tests.T') 'test v projektu s celym balikem (PKG)'

    $tridy = Join-Path $koren 'tridy'; Set-Text (Join-Path $tridy 'ALVA.MOD.A.cls') "Class ALVA.MOD.A`r`n{`r`n///    moje  `r`n}`r`n"
    $r = Invoke-Prikaz 'powershell' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $skript, '-PorovnejTridy', $tridy, '-Nastaveni', $nast)
    Test-Plati ($r.Kod -eq 0) 'trida z DEV se lisi od HEAD jen mezerami za ///'
    Set-Text (Join-Path $tridy 'ALVA.MOD.A.cls') "Class ALVA.MOD.A`n{`n/// cizi`n}`n"
    $r = Invoke-Prikaz 'powershell' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $skript, '-PorovnejTridy', $tridy, '-Nastaveni', $nast)
    Test-Plati ($r.Kod -eq 1) 'jina trida na DEV se ohlasi'

    Add-Commit $e2 'EDControls odebira typ' @{ 'EDControls/PublicAPI.Unshipped.txt' = "*REMOVED*Ns.Stary`n" } | Out-Null
    $r = Invoke-Prikaz 'powershell' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $skript, '-Faze', 'dev', '-KotvaEdison', $kotva1, '-KotvaEdison2', $poShellu, '-BezFetch', '-Nastaveni', $nast)
    $seznamAktualni = @($r.Vystup | Where-Object { $_ -match 'seznam-dev-.*\.json$' })[-1]
    $s = [IO.File]::ReadAllText($seznamAktualni, $utf8) | ConvertFrom-Json
    Test-Plati (-not $s.shell.zmena) 'strom, dokumentace ani zmena mimo slozky shellu nejsou zmena shellu'
    Test-Plati ($s.shell.td.stav -match 'pred zmenou API' -and @($s.shell.td.typyVZdroji) -contains 'Stary') "TD sestaveny pred odebranim typu, ktery pouziva ($($s.shell.td.stav))"

    # --- sestav-a-otestuj.ps1 ------------------------------------------------------------------------
    $sestav = Join-Path $nasazeni 'sestav-a-otestuj.ps1'
    $souhrn = 'Passed!  - Failed:     0, Passed:     5, Skipped:     0, Total:     5'
    # Skriptblok volany z jineho skriptu by $script: vazal na jeho rozsah, proto sdilena hashtabulka v uzaveru.
    $test = @{ volani = (New-Object System.Collections.ArrayList); odpovedi = @{}; souhrn = $souhrn }
    $spoustec = {
        param($exe, $argumenty, $slozka)
        $text = "$exe $($argumenty -join ' ')"
        [void]$test.volani.Add($text)
        if ($test.akce -and $text -match '^msbuild') { $a = $test.akce; $test.akce = $null; & $a }
        foreach ($vzor in @($test.odpovedi.Keys)) {
            if ($text -match $vzor) {
                $fronta = @($test.odpovedi[$vzor])
                $odp = $fronta[0]
                if ($fronta.Count -gt 1) { $test.odpovedi[$vzor] = @($fronta | Select-Object -Skip 1) }
                return $odp
            }
        }
        if ($text -match '^dotnet test') { return [pscustomobject]@{ Kod = 0; Vystup = @($test.souhrn) } }
        if ($text -match 'run-cache-tests') { return [pscustomobject]@{ Kod = 0; Vystup = @('  Total: 3 passed, 0 failed, 0 skipped') } }
        return [pscustomobject]@{ Kod = 0; Vystup = @('ok') }
    }.GetNewClosure()
    $ok = [pscustomobject]@{ Kod = 0; Vystup = @('ok') }; $chyba = [pscustomobject]@{ Kod = 1; Vystup = @('error MSB1') }

    $test.odpovedi = @{ '^msbuild' = @($chyba, $chyba, $ok) }
    $test.volani.Clear()
    & $sestav -Legacy 'APP\APP.csproj' -Shell -CacheTesty 'ALVA.MOD.Tests' -Spoustec $spoustec -BezKontrolSouboru -Nastaveni $nast | Out-Null
    $msb = @($test.volani | Where-Object { $_ -match '^msbuild' })
    Test-Plati ($msb.Count -eq 3) 'legacy build ma az tri pruchody'
    Test-Plati (@($msb | Where-Object { $_ -match '-t:Build' -and $_ -notmatch 'Rebuild' }).Count -eq 3) 'legacy jen Build, nikdy Rebuild'
    Test-Plati (@($test.volani | Where-Object { $_ -match 'run-cache-tests.*-Package ALVA\.MOD\.Tests' }).Count -eq 1) 'Cache testy dotcenych modulu'
    Test-Plati (@($test.volani | Where-Object { $_ -match 'EDISON2\.E2ETests' }).Count -eq 1) 'E2E testy bezi'
    Test-Plati ($test.volani[-1] -match '^dotnet publish') 'publish az po testech'
    $test.odpovedi = @{}
    $test.volani.Clear()
    $vystupSestaveni = & $sestav -Legacy 'APP\APP.csproj' -CacheTesty 'ALVA.MOD.Tests' -Spoustec $spoustec -BezKontrolSouboru -Nastaveni $nast
    $cacheVolani = @($test.volani | Where-Object { $_ -match 'run-cache-tests' })
    Test-Plati ($cacheVolani.Count -eq 1 -and $cacheVolani[0] -match '-Package ALVA\.MOD\.Tests' -and @($test.volani | Where-Object { $_ -match '^dotnet' }).Count -eq 0) 'bez shellu bezi jen Cache testy dotcenych modulu'
    $sd = [IO.File]::ReadAllText(@($vystupSestaveni)[-1], $utf8) | ConvertFrom-Json
    Test-Plati (@($sd.testy).Count -eq 1) 'souhrn testu bez shellu neni prazdny'

    $test.odpovedi = @{ 'B\\B\.csproj' = @($chyba, $ok) }
    $test.volani.Clear()
    & $sestav -Legacy 'A\A.csproj,B\B.csproj' -Spoustec $spoustec -BezKontrolSouboru -Nastaveni $nast | Out-Null
    Test-Plati (@($test.volani | Where-Object { $_ -match 'A\\A\.csproj' }).Count -eq 1 -and @($test.volani | Where-Object { $_ -match 'B\\B\.csproj' }).Count -eq 2) 'dalsi pruchod stavi jen projekty, ktere selhaly'

    $test.odpovedi = @{ '^msbuild' = @($chyba) }
    Test-Vyhodi { & $sestav -Legacy 'APP\APP.csproj' -Spoustec $spoustec -BezKontrolSouboru -Nastaveni $nast } 'pruchodech' 'legacy build po trech neuspesnych pruchodech skonci'

    $test.odpovedi = @{ '^dotnet build' = @([pscustomobject]@{ Kod = 1; Vystup = @('error CS0001') }) }
    $test.volani.Clear()
    Test-Vyhodi { & $sestav -Shell -Spoustec $spoustec -BezKontrolSouboru -Nastaveni $nast } 'dotnet build selhal' 'neuspesny dotnet build zastavi'
    Test-Plati (@($test.volani | Where-Object { $_ -match '^dotnet test' }).Count -eq 0) 'po neuspesnem buildu testy nebezi'

    $test.odpovedi = @{ '^dotnet build' = @([pscustomobject]@{ Kod = 1; Vystup = @('error BG1002: File not found') }, $ok) }
    $test.volani.Clear()
    & $sestav -Shell -Spoustec $spoustec -BezKontrolSouboru -Nastaveni $nast | Out-Null
    Test-Plati (@($test.volani | Where-Object { $_ -match '^dotnet build' }).Count -eq 2) 'BG1002 vede k jednomu opakovani buildu'

    $test.odpovedi = @{ 'E2ETests' = @([pscustomobject]@{ Kod = 0; Vystup = @('Passed!  - Failed:     0, Passed:     0, Skipped:     4, Total:     4') }) }
    Test-Vyhodi { & $sestav -Shell -Spoustec $spoustec -BezKontrolSouboru -Nastaveni $nast } 'preskocily' 'preskoceny test zastavi'
    $test.odpovedi = @{}

    try { & $sestav -Legacy 'APP\APP.csproj' -Shell -Seznam $seznamAktualni -Spoustec $spoustec -BezKontrolSouboru -Nastaveni $nast | Out-Null; Test-Plati $true 'build na commitech inventury projde' }
    catch { Test-Plati $false "build na commitech inventury projde ($($_.Exception.Message))" }
    $posunuty = Join-Path $koren 'seznam-posunuty.json'
    $sp = [IO.File]::ReadAllText($seznamAktualni, $utf8) | ConvertFrom-Json; $sp.repa.EDISON2.head = 'jiny'
    Write-Json $posunuty $sp
    Test-Vyhodi { & $sestav -Shell -Seznam $posunuty -Spoustec $spoustec -BezKontrolSouboru -Nastaveni $nast } 'EDISON2 se od inventury posunul' 'build odmitne HEAD posunuty od inventury'
    Test-Vyhodi { & $sestav -Legacy 'APP\APP.csproj' -Spoustec $spoustec -Nastaveni $nast } 'Chybi -Seznam' 'build bez inventury odmitne'
    $test.akce = { Add-Commit $e1 'pull behem buildu' @{ 'APP/b.cs' = 'class B {}' } | Out-Null }
    Test-Vyhodi { & $sestav -Legacy 'APP\APP.csproj' -Spoustec $spoustec -BezKontrolSouboru -Nastaveni $nast } 'behem buildu posunul' 'posun HEAD EDISON behem legacy buildu zastavi'

    # --- nahraj-na-dev.ps1 ---------------------------------------------------------------------------
    $nahraj = Join-Path $nasazeni 'nahraj-na-dev.ps1'
    $zdroj = Join-Path $koren 'zdroj\X.exe'; Set-Text $zdroj 'nova'
    Set-Text (Join-Path $share 'X.exe') 'stara'
    $sestaveni = Join-Path $koren 'sestaveni.json'
    Write-Json $sestaveni ([ordered]@{ commity = [ordered]@{ EDISON = 'a'; EDISON2 = 'b' }; soubory = @([ordered]@{ jmeno = 'X.exe'; cesta = $zdroj; md5 = (Get-Md5 $zdroj); nasadit = $true }) })
    $seznam = Join-Path $koren 'seznam.json'
    Write-Json $seznam ([ordered]@{ faze = 'rel'; cil = [ordered]@{ soubory = [ordered]@{ 'X.exe' = [ordered]@{ md5 = (Get-Md5 (Join-Path $share 'X.exe')) } } } })
    $r = Invoke-Prikaz 'powershell' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $nahraj, '-Sestaveni', $sestaveni, '-Seznam', $seznam, '-Soubory', 'X.exe', '-Nastaveni', $nast)
    Test-Plati ($r.Kod -ne 0 -and ($r.Vystup -join ' ') -match 'neni inventura faze dev') 'nahrani na DEV odmitne inventuru REL'
    Write-Json $seznam ([ordered]@{ faze = 'dev'; cil = [ordered]@{ soubory = [ordered]@{ 'X.exe' = [ordered]@{ md5 = (Get-Md5 (Join-Path $share 'X.exe')) } } } })
    Set-Text $zdroj 'podvrzena'
    $r = Invoke-Prikaz 'powershell' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $nahraj, '-Sestaveni', $sestaveni, '-Seznam', $seznam, '-Soubory', 'X.exe', '-Nastaveni', $nast)
    Test-Plati ($r.Kod -ne 0 -and ($r.Vystup -join ' ') -match 'jine MD5') 'zdroj jiny nez po sestaveni zastavi'
    Set-Text $zdroj 'nova'
    Set-Text (Join-Path $share 'X.exe') 'kolegova'
    $r = Invoke-Prikaz 'powershell' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $nahraj, '-Sestaveni', $sestaveni, '-Seznam', $seznam, '-Soubory', 'X.exe', '-Nastaveni', $nast)
    Test-Plati ($r.Kod -ne 0 -and ($r.Vystup -join ' ') -match 'zmenil') 'soubor na DEV share zmeneny od tabulky zastavi'
    Test-Plati ([IO.File]::ReadAllText((Join-Path $share 'X.exe')) -eq 'kolegova') 'po zastaveni zustal soubor na DEV share netknuty'
    Set-Text (Join-Path $share 'X.exe') 'stara'
    $r = Invoke-Prikaz 'powershell' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $nahraj, '-Sestaveni', $sestaveni, '-Seznam', $seznam, '-Soubory', 'X.exe', '-Nastaveni', $nast)
    Test-Plati ($r.Kod -eq 0 -and [IO.File]::ReadAllText((Join-Path $share 'X.exe')) -eq 'nova') "kopie na DEV share ($($r.Vystup -join ' '))"
    $zaloha = @($r.Vystup | Where-Object { $_ -match '^zaloha: ' } | ForEach-Object { $_ -replace '^zaloha: ', '' })[0]
    $stav = Read-NasazeniStav (Get-Nastaveni $nast)
    Test-Plati ($stav.dev.soubory.'X.exe' -eq (Get-Md5 $zdroj)) 'stav.json drzi MD5 nasazeneho souboru'
    Write-NasazeniStav (Get-Nastaveni $nast) 'rel' ([ordered]@{ cas = 'po zaloze' })
    $r = Invoke-Prikaz 'powershell' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $nahraj, '-Obnov', $zaloha, '-Nastaveni', $nast)
    Test-Plati ($r.Kod -eq 0 -and [IO.File]::ReadAllText((Join-Path $share 'X.exe')) -eq 'stara') 'obnova ze zalohy'
    $stav = Read-NasazeniStav (Get-Nastaveni $nast)
    Test-Plati ($null -eq $stav.dev) 'obnova vrati i zaznam DEV ve stav.json'
    Test-Plati ($stav.rel.cas -eq 'po zaloze') 'obnova DEV necha zaznam REL zapsany po zaloze'

    $r = Invoke-Prikaz 'powershell' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $nahraj, '-Sestaveni', $sestaveni, '-Seznam', $seznam, '-Soubory', 'X.exe', '-Nastaveni', $nast)
    $zaloha = @($r.Vystup | Where-Object { $_ -match '^zaloha: ' } | ForEach-Object { $_ -replace '^zaloha: ', '' })[0]
    Set-Text (Join-Path $share 'X.exe') 'kolega po nas'
    $r = Invoke-Prikaz 'powershell' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $nahraj, '-Obnov', $zaloha, '-Nastaveni', $nast)
    Test-Plati ($r.Kod -ne 0 -and [IO.File]::ReadAllText((Join-Path $share 'X.exe')) -eq 'kolega po nas') 'obnova neprepise soubor, ktery mezitim nahral nekdo jiny'

    # Kopie selze u druheho souboru (zamceny): prvni, na DEV share novy, se zase smaze.
    $zdrojN = Join-Path $koren 'zdroj\N.exe'; Set-Text $zdrojN 'novy soubor'
    $zdrojL = Join-Path $koren 'zdroj\L.exe'; Set-Text $zdrojL 'nova L'
    Set-Text (Join-Path $share 'L.exe') 'stara L'
    Write-Json $sestaveni ([ordered]@{ commity = [ordered]@{ EDISON = 'a'; EDISON2 = 'b' }; soubory = @(
        [ordered]@{ jmeno = 'N.exe'; cesta = $zdrojN; md5 = (Get-Md5 $zdrojN); nasadit = $true },
        [ordered]@{ jmeno = 'L.exe'; cesta = $zdrojL; md5 = (Get-Md5 $zdrojL); nasadit = $true }) })
    Write-Json $seznam ([ordered]@{ faze = 'dev'; cil = [ordered]@{ soubory = [ordered]@{ 'N.exe' = $null; 'L.exe' = [ordered]@{ md5 = (Get-Md5 (Join-Path $share 'L.exe')) } } } })
    $zamek = [IO.File]::Open((Join-Path $share 'L.exe'), 'Open', 'Read', 'Read')
    try { $r = Invoke-Prikaz 'powershell' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $nahraj, '-Sestaveni', $sestaveni, '-Seznam', $seznam, '-Soubory', 'N.exe,L.exe', '-Nastaveni', $nast) }
    finally { $zamek.Dispose() }
    Test-Plati ($r.Kod -ne 0 -and ($r.Vystup -join ' ') -match 'vraceny') "selhana kopie vrati DEV share ($($r.Vystup -join ' '))"
    Test-Plati (-not (Test-Path (Join-Path $share 'N.exe'))) 'po selhane kopii novy soubor na DEV share neni'
    Test-Plati ([IO.File]::ReadAllText((Join-Path $share 'L.exe')) -eq 'stara L') 'po selhane kopii zustal puvodni soubor'

    # Soubor, ktery uz na DEV share lezi se stejnym MD5, se pocita jako nasazeny, i kdyz se nic nekopiruje.
    $hE1 = Invoke-G $e1 @('rev-parse', 'HEAD')
    $zdrojY = Join-Path $koren 'zdroj\Y.exe'; Set-Text $zdrojY 'stejny'; Set-Text (Join-Path $share 'Y.exe') 'stejny'
    Write-Json $sestaveni ([ordered]@{ soubory = @([ordered]@{ jmeno = 'Y.exe'; cesta = $zdrojY; md5 = (Get-Md5 $zdrojY); commit = $hE1; nasadit = $true }) })
    Write-Json $seznam ([ordered]@{ faze = 'dev'; kotva = [ordered]@{ EDISON = 'k1'; EDISON2 = 'k2' }; repa = [ordered]@{ EDISON = [ordered]@{ head = $hE1 }; EDISON2 = [ordered]@{ head = 'h2' } }
        buildLegacy = @([ordered]@{ vystup = 'Y.exe'; projekt = 'APP\APP.csproj'; nasaditelny = $true }); shell = [ordered]@{ zmena = $false; souboryPriZmene = @() }; cil = [ordered]@{ soubory = [ordered]@{} } })
    $r = Invoke-Prikaz 'powershell' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $nahraj, '-Sestaveni', $sestaveni, '-Seznam', $seznam, '-Nastaveni', $nast)
    Test-Plati ($r.Kod -eq 0 -and (Read-NasazeniStav (Get-Nastaveni $nast)).dev.commity.EDISON -eq $hE1) "bez kopie se souborem uz na DEV share se kotva DEV posune ($($r.Vystup -join ' '))"

    # --- over-test-verzi.ps1 -------------------------------------------------------------------------
    $over = Join-Path $nasazeni 'over-test-verzi.ps1'
    $balik = Join-Path $koren 'stav\balik'
    $exeA = [byte[]](1..40); $exeB = [byte[]](41..60)
    $strom = 'd ..AddExe("B", "B.exe", "F", "2", 1, "", 0, "", 0)'   # Tree.cls na HEAD fixture (commit tree-sync)
    New-SetupDat (Join-Path $balik 'TEST-1.1\SETUP.DAT') ([ordered]@{ 'A.exe' = [byte[]](9, 9); 'B.exe' = $exeB; 'ALVA_SHELL.XML' = "<x ts=`"1`">$strom</x>"; 'ALVA_MOD.XML' = '<p ts="1"><ProjectItem name="ALVA.MOD.A.CLS" type="CLS"/></p>' })
    New-SetupDat (Join-Path $balik 'TEST-1.2\SETUP.DAT') ([ordered]@{ 'A.exe' = $exeA; 'B.exe' = $exeB; 'ALVA_SHELL.XML' = "<x ts=`"2`">$strom</x>"; 'ALVA_MOD.XML' = '<p ts="2"><ProjectItem name="ALVA.MOD.A.CLS" type="CLS"/><c>zmena</c></p>' })
    New-SetupDat (Join-Path $balik 'TEST-1.3\SETUP.DAT') ([ordered]@{ 'A.exe' = $exeA; 'B.exe' = $exeB; 'ALVA_SHELL.XML' = "<x>$strom</x>"; 'ALVA_MOD.XML' = '<p><ProjectItem name="PKG:ALVA.MOD" type="PKG"/><Class name="ALVA.MOD.Tests.T"></Class></p>' })
    $lokalni = Join-Path $koren 'lokalni'; New-Item -ItemType Directory -Force -Path $lokalni | Out-Null
    [IO.File]::WriteAllBytes((Join-Path $lokalni 'A.exe'), $exeA)
    $r = Invoke-Prikaz 'powershell' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $over, '-Verze', '1.2', '-Predchozi', '1.1', '-Lokalni', $lokalni, '-Projekty', 'ALVA_MOD', '-Nastaveni', $nast)
    Test-Plati ($r.Kod -eq 0) "over-test-verzi: nase binarka, zmeneny jen schvaleny projekt, strom sedi ($($r.Vystup | Select-Object -Last 4))"
    $vys = [IO.File]::ReadAllText(@($r.Vystup | Where-Object { $_ -match 'balik-TEST-1\.2\.json$' })[-1], $utf8) | ConvertFrom-Json
    Test-Plati (@($vys.zmeneneXml) -contains 'ALVA_MOD' -and @($vys.ciziXml).Count -eq 0) 'over-test-verzi: casove razitko neni zmena, obsah ano'
    Test-Plati (@($vys.bezeZmeny | Where-Object { $_.soubor -eq 'B.exe' -and $_.bezeZmeny }).Count -eq 1) 'over-test-verzi: soubor beze zmeny'
    $r = Invoke-Prikaz 'powershell' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $over, '-Verze', '1.2', '-Predchozi', '1.1', '-Lokalni', $lokalni, '-Nastaveni', $nast)
    $vys = [IO.File]::ReadAllText(@($r.Vystup | Where-Object { $_ -match 'balik-TEST-1\.2\.json$' })[-1], $utf8) | ConvertFrom-Json
    Test-Plati (@($vys.zmeneneXml).Count -eq 0 -and @($vys.ciziXml) -contains 'ALVA_MOD') 'over-test-verzi: bez -Projekty neni zadna zmena nase'
    $r = Invoke-Prikaz 'powershell' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $over, '-Verze', '1.3', '-Predchozi', '1.2', '-Lokalni', $lokalni, '-Projekty', 'ALVA_MOD', '-Nastaveni', $nast)
    Test-Plati ($r.Kod -ne 0 -and ($r.Vystup -join ' ') -match 'testovaci tridy v projektech: ALVA_MOD') 'over-test-verzi: test NEPRIDAVAT v projektu s celym balikem'
    $sestaveniVerze = Join-Path $koren 'sestaveni-verze.json'
    Set-Text (Join-Path $lokalni 'A.exe') 'prepsano jinym buildem'
    Write-Json $sestaveniVerze ([ordered]@{ cas = '2026-02-01T00:00:00'; soubory = @([ordered]@{ jmeno = 'A.exe'; cesta = (Join-Path $lokalni 'A.exe'); md5 = (Get-Md5Bajtu $exeA); nasadit = $true }) })
    $r = Invoke-Prikaz 'powershell' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $over, '-Verze', '1.2', '-Predchozi', '1.1', '-Sestaveni', $sestaveniVerze, '-Projekty', 'ALVA_MOD', '-Nastaveni', $nast)
    Test-Plati ($r.Kod -eq 0) 'over-test-verzi: porovnava s MD5 ze sestaveni, ne s dnesnim souborem'
    $seznamRel = Join-Path $koren 'seznam-rel.json'
    $tridyRel = @([ordered]@{ trida = 'ALVA.MOD.A'; cacheProjekty = @('ALVA_MOD') }, [ordered]@{ trida = 'ALVA.Y.B'; cacheProjekty = @('ALVA_Y') })
    $novySeznamRel = { param($tridy, $faze = 'rel') Write-Json $seznamRel ([ordered]@{ faze = $faze; kotva = [ordered]@{ EDISON = 'stara1'; EDISON2 = 'stara2' }
        repa = [ordered]@{ EDISON = [ordered]@{ head = 'h1' }; EDISON2 = [ordered]@{ head = 'h2' } }; buildLegacy = @(); shell = [ordered]@{ zmena = $false; souboryPriZmene = @() }; tridy = $tridy }) }
    & $novySeznamRel $tridyRel 'dev'
    $r = Invoke-Prikaz 'powershell' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $over, '-Verze', '1.2', '-Predchozi', '1.1', '-Sestaveni', $sestaveniVerze, '-Seznam', $seznamRel, '-Zapis', '-Nastaveni', $nast)
    Test-Plati ($r.Kod -ne 0 -and ($r.Vystup -join ' ') -match 'neni inventura faze rel') 'zapis REL odmitne inventuru DEV'
    & $novySeznamRel $tridyRel
    $r = Invoke-Prikaz 'powershell' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $over, '-Verze', '1.2', '-Predchozi', '1.1', '-Sestaveni', $sestaveniVerze, '-Seznam', $seznamRel, '-Zapis', '-Nastaveni', $nast)
    $stavRel = (Read-NasazeniStav (Get-Nastaveni $nast)).rel
    Test-Plati ($r.Kod -eq 0 -and $stavRel.commity.EDISON -eq 'stara1' -and ($r.Vystup -join ' ') -match 'beze zmeny proti 1\.1: ALVA_Y') "REL: projekt ze seznamu bez zmeny v baliku drzi kotvu ($($r.Vystup | Select-Object -Last 3))"
    & $novySeznamRel @($tridyRel[0])
    $r = Invoke-Prikaz 'powershell' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $over, '-Verze', '1.2', '-Predchozi', '1.1', '-Sestaveni', $sestaveniVerze, '-Seznam', $seznamRel, '-Zapis', '-Nastaveni', $nast)
    Test-Plati ($r.Kod -eq 0 -and (Read-NasazeniStav (Get-Nastaveni $nast)).rel.commity.EDISON -eq 'h1') 'REL: balik nese vse ze seznamu, kotva se posune na commity inventury'
    $sestaveniB = Join-Path $koren 'sestaveni-b.json'
    Write-Json $sestaveniB ([ordered]@{ soubory = @([ordered]@{ jmeno = 'B.exe'; cesta = 'neni'; md5 = (Get-Md5Bajtu $exeB); nasadit = $true }) })
    $r = Invoke-Prikaz 'powershell' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $over, '-Verze', '1.2', '-Predchozi', '1.1', '-Sestaveni', "$sestaveniVerze,$sestaveniB", '-Nastaveni', $nast)
    Test-Plati ($r.Kod -eq 0 -and @($r.Vystup | Where-Object { $_ -match '^\s+(A|B)\.exe\s+nase' }).Count -eq 2) "REL nese soubory ze dvou sestaveni ($($r.Vystup | Select-Object -Last 4))"
    $sestaveniStare = Join-Path $koren 'sestaveni-stare.json'
    Write-Json $sestaveniStare ([ordered]@{ cas = '2026-01-01T00:00:00'; soubory = @([ordered]@{ jmeno = 'A.exe'; cesta = 'neni'; md5 = 'STARE'; nasadit = $true }, [ordered]@{ jmeno = 'C.exe'; cesta = 'neni'; md5 = 'C'; nasadit = $true }) })
    $r = Invoke-Prikaz 'powershell' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $over, '-Verze', '1.2', '-Predchozi', '1.1', '-Sestaveni', "$sestaveniVerze,$sestaveniStare", '-Soubory', 'A.exe', '-Nastaveni', $nast)
    Test-Plati ($r.Kod -eq 0) "novejsi sestaveni prebije starsi i predane drive, -Soubory vynecha neschvalene ($($r.Vystup | Select-Object -Last 3))"
    $r = Invoke-Prikaz 'powershell' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $over, '-Verze', '1.2', '-Predchozi', '1.1', '-Sestaveni', "$sestaveniVerze,$sestaveniStare", '-Nastaveni', $nast)
    Test-Plati ($r.Kod -ne 0 -and ($r.Vystup -join ' ') -match 'C\.exe v baliku chybi') 'bez -Soubory se overi vse ze sestaveni'

    # --- nastaveni.json ------------------------------------------------------------------------------
    Remove-Item Env:\EDISON_NASAD_REPO2
    $skutecne = Get-Nastaveni
    foreach ($p in $skutecne.shell.projekty) { Test-Plati (Test-Path (Join-Path $skutecne.repoEdison2 $p)) "nastaveni.json: projekt shellu $p existuje" }
    Test-Plati (Test-Path (Join-Path $skutecne.repoEdison2 $skutecne.td.publicApi)) 'nastaveni.json: PublicAPI.Unshipped.txt existuje'
    Test-Plati (Test-Path (Join-Path $skutecne.repoEdison2 $skutecne.strom.treeCls)) 'nastaveni.json: Tree.cls existuje'
} catch {
    $script:selhalo += "neocekavana vyjimka: $($_.Exception.Message) @ $($_.InvocationInfo.ScriptLineNumber)"
    Write-Host "SELHALO: $($_.Exception.Message) @ $($_.InvocationInfo.ScriptLineNumber)"
} finally {
    Remove-Item Env:\EDISON_NASAD_HOME, Env:\EDISON_NASAD_REPO2 -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $koren -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ("tools/nasazeni testy: {0} proslo, {1} selhalo" -f $script:proslo, $script:selhalo.Count)
if ($script:selhalo.Count -gt 0) { exit 1 }
exit 0
