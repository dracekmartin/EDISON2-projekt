# Spolecne funkce pro skripty v tools/nasazeni (co-nasadit, sestav-a-otestuj, nahraj-na-dev, over-test-verzi):
# nastaveni a stav, git, MD5 a otisk binarky, cteni baliku verze (SETUP.DAT), strom z EDCore.dll, porovnani
# verejneho API legacy DLL. Samo nic nedela.
# ASCII-only zamerne: WinPS 5.1 cte UTF-8 bez BOM jako CP-1252.

$ErrorActionPreference = 'Stop'
$script:Utf8 = New-Object System.Text.UTF8Encoding($false)
$script:Enc1250 = [Text.Encoding]::GetEncoding(1250)

# --- Konfigurace, repa, stav ------------------------------------------------------------------

function Get-Nastaveni([string]$Cesta) {
    if (-not $Cesta) { $Cesta = Join-Path $PSScriptRoot 'nastaveni.json' }
    $k = [IO.File]::ReadAllText($Cesta, $script:Utf8) | ConvertFrom-Json
    $repo2 = if ($env:EDISON_NASAD_REPO2) { $env:EDISON_NASAD_REPO2 } else { (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path }
    $k | Add-Member -NotePropertyName repoEdison2 -NotePropertyValue $repo2 -Force
    $k | Add-Member -NotePropertyName repoEdison -NotePropertyValue ([IO.Path]::GetFullPath((Join-Path $repo2 $k.edisonRepo))) -Force
    foreach ($p in 'stavDir', 'zalohyDir', 'treeSyncDir', 'mcpKonfigurace') {
        $k.$p = [Environment]::ExpandEnvironmentVariables($k.$p)
    }
    # Testy presmeruji stav a zalohy, aby nesahaly na skutecny stav nasazeni.
    if ($env:EDISON_NASAD_HOME) {
        $k.stavDir = $env:EDISON_NASAD_HOME
        $k.zalohyDir = Join-Path $env:EDISON_NASAD_HOME 'zalohy'
    }
    New-Item -ItemType Directory -Force -Path $k.stavDir | Out-Null
    return $k
}

function Read-NasazeniStav($Nastaveni) {
    $cesta = Join-Path $Nastaveni.stavDir 'stav.json'
    if (-not (Test-Path -LiteralPath $cesta)) { return [pscustomobject]@{ dev = $null; rel = $null } }
    return [IO.File]::ReadAllText($cesta, $script:Utf8) | ConvertFrom-Json
}

function Write-NasazeniStav($Nastaveni, [string]$Cil, $Hodnota) {
    $stav = Read-NasazeniStav $Nastaveni
    $stav | Add-Member -NotePropertyName $Cil -NotePropertyValue $Hodnota -Force
    Write-Json (Join-Path $Nastaveni.stavDir 'stav.json') $stav
}

# Kotva (commity posledniho nasazeni) se posune na commity, ktere videla inventura, jen kdyz se nasadilo vse, co
# seznam ukazal jako potrebne, a zadny nasazeny soubor neni postaveny pred commitem, ktery ho meni. Jinak by
# nenasazena zmena z pristi inventury tise zmizela. Projekt, ktery nestavi do C:\Edison, skripty nasadit neumi,
# kotvu proto nedrzi. -VcetneProjektu (REL) pocita i projekty Cache jako "<projekt>.XML".
function Get-KotvaPoNasazeni($Nastaveni, $SeznamData, $SestaveniData, [string[]]$Nasazeno, [switch]$VcetneProjektu) {
    $potreba = @($SeznamData.buildLegacy | Where-Object { $_.nasaditelny -ne $false } | ForEach-Object { $_.vystup })
    if ($SeznamData.shell.zmena) { $potreba += @($Nastaveni.shell.soubory) }
    $potreba += @($SeznamData.shell.souboryPriZmene)
    if ($VcetneProjektu) {
        $potreba += @(Get-ProjektyTrid $SeznamData | ForEach-Object { "$_.XML" })
        # Trida z modulu, ktery konfigurace MCP nezna, nema projekt k importu: kotva pocka na doplneni modulu.
        $potreba += @(Get-TridyKNasazeni $SeznamData | Where-Object { -not $_.cacheProjekty } | ForEach-Object { "$($_.modul) (modul mimo konfiguraci MCP)" })
    }
    $chybi = @($potreba | Where-Object { $_ -and $Nasazeno -notcontains $_ } | Sort-Object -Unique)
    foreach ($s in @($SestaveniData.soubory | Where-Object { $Nasazeno -contains $_.jmeno })) {
        $zdroj = Get-ZdrojSouboru $Nastaveni $SeznamData $s.jmeno
        if (-not $zdroj) { continue }
        $head = $SeznamData.repa.($zdroj.repo).head
        $zmeny = $null
        if ($s.commit -and $head) {
            $a = @('log', '--no-merges', '--format=%h', "$($s.commit)..$head")
            if ($zdroj.repo -eq 'EDISON' -and $SeznamData.autor) { $a += "--author=$($SeznamData.autor)" }
            $repo = if ($zdroj.repo -eq 'EDISON') { $Nastaveni.repoEdison } else { $Nastaveni.repoEdison2 }
            $r = Invoke-Git $repo ($a + @('--') + $zdroj.cesty) -PovolChybu
            if ($r.Kod -eq 0) { $zmeny = @($r.Vystup | Where-Object { $_ }) }
        }
        if ($null -eq $zmeny -or $zmeny.Count -gt 0) { $chybi += "$($s.jmeno) (postaveny pred zmenou ze seznamu)" }
    }
    if ($chybi.Count -eq 0) { return [pscustomobject]@{ commity = [ordered]@{ EDISON = $SeznamData.repa.EDISON.head; EDISON2 = $SeznamData.repa.EDISON2.head }; chybi = @() } }
    return [pscustomobject]@{ commity = $SeznamData.kotva; chybi = $chybi }
}

# Testovaci tridy s hlavickou NEPRIDAVAT v projektech nejsou, na REL se nedostanou.
function Get-TridyKNasazeni($SeznamData) {
    $testy = @($SeznamData.nepridavat)
    return @($SeznamData.tridy | Where-Object { $testy -notcontains $_.trida })
}

function Get-ProjektyTrid($SeznamData) {
    return @(Get-TridyKNasazeni $SeznamData | ForEach-Object { $_.cacheProjekty } | Where-Object { $_ } | Sort-Object -Unique)
}

# Strom (tree-sync commituje Tree.cls pod CACHE), tridy Cache a dokumentace binarky nemeni.
function Get-MimoBinarky { return @(':(exclude)*/CACHE/*', ':(exclude)*/DOCS/*', ':(exclude)*.md') }

# Odkud se soubor stavi: repo a pathspec pro git log.
function Get-ZdrojSouboru($Nastaveni, $SeznamData, [string]$Jmeno) {
    if (@($Nastaveni.shell.soubory) -contains $Jmeno) { return [pscustomobject]@{ repo = 'EDISON2'; cesty = @($Nastaveni.shell.slozky) + (Get-MimoBinarky) } }
    $p = $Nastaveni.shell.souboryPriZmene.PSObject.Properties[$Jmeno]
    if ($p) { return [pscustomobject]@{ repo = 'EDISON2'; cesty = @($p.Value) + (Get-MimoBinarky) } }
    $l = @($SeznamData.buildLegacy | Where-Object { $_.vystup -eq $Jmeno })[0]
    if ($l) { return [pscustomobject]@{ repo = 'EDISON'; cesty = @(((Split-Path -Parent $l.projekt) -replace '\\', '/')) + (Get-MimoBinarky) } }
    return $null
}

function Write-Json([string]$Cesta, $Objekt) {
    [IO.File]::WriteAllText($Cesta, ($Objekt | ConvertTo-Json -Depth 12), $script:Utf8)
}

function New-CasovaZnacka { return (Get-Date -Format 'yyyyMMdd-HHmmss') }

# --- Externi programy ---------------------------------------------------------------------------

# Git pise prubeh na stderr; pri Stop by to skript shodilo, o vysledku rozhoduje exit kod.
function Invoke-Prikaz([string]$Exe, [string[]]$Argumenty, [string]$Slozka) {
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $puvodni = $null
    if ($Slozka) { $puvodni = Get-Location; Set-Location -LiteralPath $Slozka }
    try { $vystup = & $Exe @Argumenty 2>&1 | ForEach-Object { "$_" } }
    finally {
        $ErrorActionPreference = $prev
        if ($puvodni) { Set-Location -LiteralPath $puvodni }
    }
    return [pscustomobject]@{ Kod = $LASTEXITCODE; Vystup = @($vystup) }
}

# core.quotePath=false: jinak git cesty s diakritikou vypise v uvozovkach s escapy a na soubor nesedi. Cesty
# i zpravy commitu pise v UTF-8, PowerShell by je cetl v kodove strance konzole.
function Invoke-Git([string]$Repo, [string[]]$Argumenty, [switch]$PovolChybu) {
    $kodovani = [Console]::OutputEncoding
    [Console]::OutputEncoding = $script:Utf8
    try { $r = Invoke-Prikaz 'git' (@('-C', $Repo, '-c', 'core.quotePath=false') + $Argumenty) }
    finally { [Console]::OutputEncoding = $kodovani }
    if (-not $PovolChybu -and $r.Kod -ne 0) { throw "git $($Argumenty -join ' ') v $Repo skoncil s kodem $($r.Kod): $($r.Vystup -join ' ')" }
    # Varovani gitu (napr. poskozeny commit-graph) jdou na stderr a vystup by rozbila pro parsovani.
    $r.Vystup = @($r.Vystup | Where-Object { $_ -notmatch '^warning: ' })
    return $r
}

# Obsah souboru z gitu jako bajty: vystup prikazu by PowerShell prekodoval podle konzole.
function Get-GitBlob([string]$Repo, [string]$Revize, [string]$Cesta) {
    $psi = New-Object Diagnostics.ProcessStartInfo 'git'
    $psi.Arguments = "-C `"$Repo`" cat-file blob `"${Revize}:$Cesta`""
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $p = [Diagnostics.Process]::Start($psi)
    $ms = New-Object IO.MemoryStream
    $p.StandardOutput.BaseStream.CopyTo($ms)
    $chyba = $p.StandardError.ReadToEnd()
    $p.WaitForExit()
    if ($p.ExitCode -ne 0) { throw "git cat-file ${Revize}:$Cesta v $Repo selhal: $chyba" }
    return , $ms.ToArray()
}

function Get-GitText([string]$Repo, [string]$Revize, [string]$Cesta) {
    return $script:Utf8.GetString((Get-GitBlob $Repo $Revize $Cesta))
}

# 'bash' v PATH je na Windows typicky WSL, ktery nerozumi C:/ cestam. Git Bash odvodime od git.exe.
function Get-GitBash {
    $git = (Get-Command git -ErrorAction SilentlyContinue).Source
    if ($git) {
        $kandidat = Join-Path (Split-Path -Parent (Split-Path -Parent $git)) 'bin\bash.exe'
        if (Test-Path $kandidat) { return $kandidat }
    }
    $kandidat = Join-Path $env:ProgramFiles 'Git\bin\bash.exe'
    if (Test-Path $kandidat) { return $kandidat }
    throw 'Git Bash nenalezen'
}

# --- Soubory a sestaveni -----------------------------------------------------------------------

function Get-Md5([string]$Cesta) { return (Get-FileHash -Algorithm MD5 -LiteralPath $Cesta).Hash }

function Get-Md5Bajtu([byte[]]$Bajty) {
    return ([BitConverter]::ToString([Security.Cryptography.MD5]::Create().ComputeHash($Bajty)) -replace '-', '')
}

function Get-ProduktVerze([string]$Cesta) {
    if (-not (Test-Path -LiteralPath $Cesta)) { return $null }
    return (Get-Item -LiteralPath $Cesta).VersionInfo.ProductVersion
}

function Test-PodepsanyPe([byte[]]$Bajty) {
    $cert = Get-PeCertPozice $Bajty
    if ($cert -lt 0) { return $false }
    return ([BitConverter]::ToUInt32($Bajty, $cert) -ne 0)
}

function Get-PeCertPozice([byte[]]$Bajty) {
    if ($Bajty.Length -lt 0x40 -or $Bajty[0] -ne 0x4D -or $Bajty[1] -ne 0x5A) { return -1 }
    $pe = [BitConverter]::ToInt32($Bajty, 0x3C)
    if ($pe -lt 0 -or $pe + 24 + 112 + 40 -gt $Bajty.Length) { return -1 }
    $magic = [BitConverter]::ToUInt16($Bajty, $pe + 24)
    $adresare = $pe + 24 + $(if ($magic -eq 0x10b) { 96 } else { 112 })
    return $adresare + 4 * 8
}

# Otisk jako Authenticode: bez checksumu, polozky certifikatove tabulky a bloku podpisu. Podpis
# zarovna soubor nulami na nasobek 8, proto se nepodepsany soubor na nasobek 8 doplni taky.
function Get-PeOtisk([byte[]]$Bajty) {
    $cert = Get-PeCertPozice $Bajty
    if ($cert -lt 0) { return $null }
    $pe = [BitConverter]::ToInt32($Bajty, 0x3C)
    $checksum = $pe + 24 + 64
    $coff = [BitConverter]::ToUInt32($Bajty, $cert)
    $konec = if ($coff -gt 0) { [int]$coff } else { $Bajty.Length }
    $vypln = if ($coff -gt 0) { 0 } else { (8 - ($Bajty.Length % 8)) % 8 }
    $md5 = [Security.Cryptography.MD5]::Create()
    $md5.TransformBlock($Bajty, 0, $checksum, $null, 0) | Out-Null
    $md5.TransformBlock($Bajty, $checksum + 4, $cert - ($checksum + 4), $null, 0) | Out-Null
    $md5.TransformBlock($Bajty, $cert + 8, $konec - ($cert + 8), $null, 0) | Out-Null
    $md5.TransformFinalBlock((New-Object byte[] $vypln), 0, $vypln) | Out-Null
    return ([BitConverter]::ToString($md5.Hash) -replace '-', '')
}

# Shoda dvou binarek: MD5, a kdyz je aspon jedna podepsana, otisk bez podpisu.
function Test-ShodaBinarek([byte[]]$A, [byte[]]$B) {
    if ((Get-Md5Bajtu $A) -eq (Get-Md5Bajtu $B)) { return $true }
    if (-not ((Test-PodepsanyPe $A) -or (Test-PodepsanyPe $B))) { return $false }
    $oa = Get-PeOtisk $A
    return ($oa -and $oa -eq (Get-PeOtisk $B))
}

# $null = nejde urcit (.NET Core assembly ve WinPS, poskozeny soubor).
function Test-ReleaseBuild([string]$Cesta) {
    try {
        $a = [Reflection.Assembly]::ReflectionOnlyLoadFrom($Cesta)
        $atribut = @($a.GetCustomAttributesData() | Where-Object { $_.AttributeType.Name -eq 'DebuggableAttribute' })
    } catch { return $null }
    if ($atribut.Count -eq 0) { return $true }
    $argumenty = $atribut[0].ConstructorArguments
    if ($argumenty.Count -eq 2) { return (-not [bool]$argumenty[1].Value) }
    return (([int]$argumenty[0].Value -band 256) -eq 0)   # DebuggingModes.DisableOptimizations
}

# Vypis verejneho API v samostatnem procesu: dve verze tehoz assembly v jednom procesu nejdou nacist.
$script:ApiSkript = @'
$cesta = $env:NASAZENI_API_DLL
$slozka = Split-Path -Parent $cesta
[AppDomain]::CurrentDomain.add_ReflectionOnlyAssemblyResolve({
    param($s, $e)
    $jmeno = (New-Object Reflection.AssemblyName($e.Name)).Name
    $p = Join-Path $env:NASAZENI_API_SLOZKA "$jmeno.dll"
    if (Test-Path $p) { return [Reflection.Assembly]::ReflectionOnlyLoadFrom($p) }
    return [Reflection.Assembly]::ReflectionOnlyLoad($e.Name)
})
$a = [Reflection.Assembly]::ReflectionOnlyLoadFrom($cesta)
try { $typy = $a.GetTypes() } catch [Reflection.ReflectionTypeLoadException] { $typy = $_.Exception.Types | Where-Object { $_ } }
$f = [Reflection.BindingFlags]'Public,Instance,Static,DeclaredOnly'
$radky = foreach ($t in $typy) {
    if (-not ($t.IsPublic -or $t.IsNestedPublic)) { continue }
    "T:$($t.FullName)"
    try { $clenove = $t.GetMembers($f) } catch { continue }
    foreach ($m in $clenove) {
        try {
            $par = ''
            if ($m -is [Reflection.MethodBase]) { $par = '(' + (($m.GetParameters() | ForEach-Object { $_.ParameterType.FullName }) -join ',') + ')' }
            $navrat = ''
            if ($m -is [Reflection.MethodInfo]) { $navrat = ':' + $m.ReturnType.FullName }
            elseif ($m -is [Reflection.PropertyInfo]) { $navrat = ':' + $m.PropertyType.FullName }
            elseif ($m -is [Reflection.FieldInfo]) { $navrat = ':' + $m.FieldType.FullName }
            "$($m.MemberType):$($t.FullName).$($m.Name)$par$navrat"
        } catch { "$($m.MemberType):$($t.FullName).$($m.Name)?" }
    }
}
$radky | Sort-Object -Unique
'@

function Get-VerejneApi([string]$Cesta, [string]$SlozkaZavislosti) {
    $env:NASAZENI_API_DLL = $Cesta
    $env:NASAZENI_API_SLOZKA = $(if ($SlozkaZavislosti) { $SlozkaZavislosti } else { Split-Path -Parent $Cesta })
    # Pres -Command by WinPS 5.1 z argumentu nativniho programu vypustil uvozovky, proto soubor.
    $skript = Join-Path $env:TEMP "nasazeni-verejne-api-$([guid]::NewGuid().ToString('N')).ps1"
    [IO.File]::WriteAllText($skript, $script:ApiSkript, $script:Utf8)
    try {
        $r = Invoke-Prikaz 'powershell' @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', $skript)
    } finally {
        Remove-Item Env:\NASAZENI_API_DLL, Env:\NASAZENI_API_SLOZKA -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $skript -ErrorAction SilentlyContinue
    }
    if ($r.Kod -ne 0) { throw "API $Cesta nejde vypsat: $($r.Vystup -join ' ')" }
    return @($r.Vystup | Where-Object { $_ -match '^[A-Za-z]+:' })
}

# Cleny, ktere ma lokalni verze (proti ni se shell sestavil) a cilova nema: shell by na nich za behu spadl.
function Compare-LegacyApi([string]$Lokalni, [string]$Cil, [string]$SlozkaZavislostiCile) {
    $mistni = Get-VerejneApi $Lokalni
    $cilove = @{}
    foreach ($r in (Get-VerejneApi $Cil $SlozkaZavislostiCile)) { $cilove[$r] = $true }
    return @($mistni | Where-Object { -not $cilove.ContainsKey($_) })
}

# Strom legacy shellu je resource v EDCore.dll (Resources.resx), stejny text jako edisontree.xml v gitu.
function Get-StromEdCore([string]$Cesta) {
    $a = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes($Cesta))
    $jmeno = @($a.GetManifestResourceNames() | Where-Object { $_ -like '*.Resources.resources' })[0]
    if (-not $jmeno) { return $null }
    $ctecka = New-Object System.Resources.ResourceReader($a.GetManifestResourceStream($jmeno))
    try {
        foreach ($polozka in $ctecka) { if ($polozka.Key -eq 'edisontree') { return [string]$polozka.Value } }
    } finally { $ctecka.Close() }
    return $null
}

function ConvertTo-PorovnatelnyText([string]$Text) {
    if ($null -eq $Text) { return $null }
    return ($Text.TrimStart([char]0xFEFF) -replace "`r`n", "`n").TrimEnd()
}

# --- Projekty legacy ---------------------------------------------------------------------------

function Get-ProjektInfo([string]$Cesta) {
    $xml = New-Object Xml.XmlDocument
    $xml.Load($Cesta)
    $hodnota = { param($jmeno) $u = $xml.SelectSingleNode("//*[local-name()='$jmeno']"); if ($u) { $u.InnerText.Trim() } else { '' } }
    $assembly = & $hodnota 'AssemblyName'
    if (-not $assembly) { $assembly = [IO.Path]::GetFileNameWithoutExtension($Cesta) }
    $typ = & $hodnota 'OutputType'
    $pripona = if ($typ -match '^(WinExe|Exe)$') { '.exe' } else { '.dll' }
    $slozka = Split-Path -Parent $Cesta
    $odkazy = @($xml.SelectNodes("//*[local-name()='ProjectReference']") | ForEach-Object {
        [IO.Path]::GetFullPath((Join-Path $slozka $_.GetAttribute('Include')))
    })
    $hint = @($xml.SelectNodes("//*[local-name()='HintPath']") | ForEach-Object { $_.InnerText.Trim() })
    # OutputPath pro Release nebo bez podminky. Bez OutputPath stavi SDK projekt do bin\.
    $cesty = @($xml.SelectNodes("//*[local-name()='OutputPath']") |
        Where-Object { $c = $_.ParentNode.GetAttribute('Condition'); -not $c -or $c -match 'Release' } | ForEach-Object { $_.InnerText.Trim() })
    $slozkaVystupu = if ($cesty.Count -gt 0) { [IO.Path]::GetFullPath([IO.Path]::Combine($slozka, $cesty[-1])) } else { $null }
    return [pscustomobject]@{
        Cesta = $Cesta; Slozka = $slozka; Assembly = $assembly; Vystup = "$assembly$pripona"
        ProjectReference = $odkazy; HintPath = $hint; SlozkaVystupu = $slozkaVystupu
    }
}

# Sablonove kopie WPF (_wpftmp) nejsou projekty.
function Get-VsechnyProjekty([string]$Repo) {
    $r = Invoke-Git $Repo @('ls-files', '*.csproj', '*.vbproj')
    return @($r.Vystup | Where-Object { $_ -and $_ -notmatch '_wpftmp' } | ForEach-Object { Join-Path $Repo ($_ -replace '/', '\') })
}

function Get-ProjektSouboru([string]$Repo, [string]$RelativniCesta) {
    $slozka = Split-Path -Parent (Join-Path $Repo ($RelativniCesta -replace '/', '\'))
    while ($slozka -and $slozka.Length -ge $Repo.Length) {
        $p = @(Get-ChildItem -LiteralPath $slozka -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Extension -in '.csproj', '.vbproj' -and $_.Name -notmatch '_wpftmp' })
        if ($p.Count -gt 0) { return $p[0].FullName }
        $slozka = Split-Path -Parent $slozka
    }
    return $null
}

# HEAD v case zapisu DLL z reflogu: datum commitu nerika, kdy commit do repa dorazil.
function Test-ZastaralaDll([string]$Repo, [string]$Dll, [string]$SlozkaProjektu) {
    if (-not (Test-Path -LiteralPath $Dll)) { return 'chybi' }
    $cas = (Get-Item -LiteralPath $Dll).LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss')
    $r = Invoke-Git $Repo @('rev-parse', '--verify', "HEAD@{$cas}") -PovolChybu
    if ($r.Kod -ne 0 -or ($r.Vystup -join ' ') -match 'only goes back') { return 'bez-reflogu' }
    $sha = @($r.Vystup | Where-Object { $_ -match '^[0-9a-f]{40}$' })[0]
    if (-not $sha) { return 'bez-reflogu' }
    $rel = $SlozkaProjektu.Substring($Repo.Length).TrimStart('\') -replace '\\', '/'
    $d = Invoke-Git $Repo @('diff', '--quiet', "$sha..HEAD", '--', $rel) -PovolChybu
    if ($d.Kod -eq 1) { return 'zastarala' }
    return 'aktualni'
}

# --- REL: VERZE.INF a balik ---------------------------------------------------------------------

function Read-VerzeInf([string]$Text) {
    $v = @{}
    foreach ($radek in ($Text -split "`r?`n")) {
        if ($radek -match '^\s*([^=]+?)\s*=\s*(.*?)\s*$') { $v[$Matches[1]] = $Matches[2] }
    }
    return $v
}

function Get-Url([string]$Url) {
    $wc = New-Object Net.WebClient
    try { return $wc.DownloadString($Url) } finally { $wc.Dispose() }
}

function Get-VerzeInf($Nastaveni, [string]$Kanal) {
    return Read-VerzeInf (Get-Url "$($Nastaveni.relUrl)/$Kanal/VERZE.INF")
}

# Ceka na novou verzi v kanalu: LAST jine nez -Od a ne 0.0.0.0 (stavba bezi).
function Wait-Verze($Nastaveni, [string]$Kanal, [string]$Od, [int]$LimitSekund = 3600, [int]$Interval = 15) {
    $konec = (Get-Date).AddSeconds($LimitSekund)
    while ($true) {
        $last = (Get-VerzeInf $Nastaveni $Kanal)['LAST']
        if ($last -and $last -ne $Od -and $last -ne '0.0.0.0') { return $last }
        if ((Get-Date) -gt $konec) { throw "Verze v kanalu $Kanal se do $LimitSekund s neobjevila (posledni LAST=$last)" }
        Start-Sleep -Seconds $Interval
    }
}

function Get-Balik($Nastaveni, [string]$Kanal, [string]$Verze) {
    $slozka = Join-Path $Nastaveni.stavDir "balik\$Kanal-$Verze"
    $dat = Join-Path $slozka 'SETUP.DAT'
    if (Test-Path -LiteralPath $dat) { return $dat }
    New-Item -ItemType Directory -Force -Path $slozka | Out-Null
    $zip = Join-Path $slozka 'SETUP.zip'
    $wc = New-Object Net.WebClient
    try { $wc.DownloadFile("$($Nastaveni.relUrl)/$Kanal/$Verze/SETUP.zip", $zip) } finally { $wc.Dispose() }
    $r = Invoke-Prikaz $Nastaveni.sevenZip @('x', '-y', "-o$slozka", $zip)
    if ($r.Kod -ne 0 -or -not (Test-Path -LiteralPath $dat)) { throw "Rozbaleni $zip selhalo: $($r.Vystup -join ' ')" }
    Remove-Item -LiteralPath $zip
    return $dat
}

# SETUP.DAT: 3 cislice pocet, pak za kazdy soubor 9 cislic velikost, 3 cislice delka jmena, jmeno, obsah
# (EDISON/SETUPWIZARD/frmMain.vb). Cte se proudem, obsah jen u jmen podle -VzorObsahu, -Zpracuj ho muze
# hned zpracovat (XML maji stovky MB, v pameti se nedrzi).
function Read-SetupDat([string]$Cesta, [string]$VzorObsahu = '^$', [scriptblock]$Zpracuj) {
    $fs = [IO.File]::OpenRead($Cesta)
    $cti = {
        param($n)
        $b = New-Object byte[] $n; $hotovo = 0
        while ($hotovo -lt $n) { $r = $fs.Read($b, $hotovo, $n - $hotovo); if ($r -le 0) { throw "SETUP.DAT konci predcasne" }; $hotovo += $r }
        , $b
    }
    try {
        $pocet = [int]$script:Enc1250.GetString((& $cti 3))
        for ($i = 0; $i -lt $pocet; $i++) {
            [long]$velikost = [long]$script:Enc1250.GetString((& $cti 9))
            $delka = [int]$script:Enc1250.GetString((& $cti 3))
            $jmeno = $script:Enc1250.GetString((& $cti $delka)).Trim()
            $data = $null
            if ($jmeno -match $VzorObsahu) {
                $obsah = & $cti ([int]$velikost)
                $data = if ($Zpracuj) { & $Zpracuj $jmeno $obsah } else { , $obsah }
            } else {
                $fs.Seek($velikost, [IO.SeekOrigin]::Current) | Out-Null
            }
            [pscustomobject]@{ Jmeno = $jmeno; Velikost = $velikost; Data = $data }
        }
    } finally { $fs.Dispose() }
}

# XML exportu se s kazdou verzi exportuji znovu: casova razitka nejsou zmena.
function Get-XmlOtisk([byte[]]$Bajty) {
    $t = $script:Utf8.GetString($Bajty)
    $t = [regex]::Replace($t, '<TimeChanged>[^<]*</TimeChanged>|<TimeCreated>[^<]*</TimeCreated>', '')
    $t = [regex]::Replace($t, '\s(ts|LastModified|TimeChanged|TimeCreated)="[^"]*"', '')
    return Get-Md5Bajtu ($script:Utf8.GetBytes($t))
}

# Tridy testu v exportu projektu: jako polozka projektu i jako trida (projekt s celym balikem PKG:
# polozky trid nema, tridy jsou jen v <Class>). Jmena bez pripony .cls.
function Get-TestyVProjektu([byte[]]$Bajty) {
    $t = $script:Utf8.GetString($Bajty)
    $nalez = [regex]::Matches($t, '<(ProjectItem|Class) name="([^"]*\.Tests\.[^"]*)"')
    return @($nalez | ForEach-Object { $_.Groups[2].Value -replace '(?i)\.cls$', '' } | Sort-Object -Unique)
}

# Testy, ktere do distribuce nepatri, nesou v hlavicce "NEPRIDAVAT do Studio projektu". Jine testy
# (EDISON2, moduly kolegu) jdou do distribuce zamerne.
function Get-NepridavatTridy([string]$Repo) {
    $g = Invoke-Git $Repo @('grep', '-l', 'NEPRIDAVAT do Studio projektu', 'HEAD', '--', '*.cls') -PovolChybu
    return @($g.Vystup | Where-Object { $_ -match '/CACHE/(.+)\.cls$' } | ForEach-Object { $Matches[1] -replace '/', '.' })
}

# Uzly stromu noveho shellu: radky AddExe/AddFamily z Tree.cls (v gitu i v exportu XML).
function Get-UzlyStromu([string]$Text) {
    return @(([regex]::Matches($Text, 'd \.\.Add(Exe|Family)\([^\r\n]*\)')) | ForEach-Object { $_.Value.Trim() } | Sort-Object -Unique)
}

# --- Tridy Cache -------------------------------------------------------------------------------

# Cache pri ulozeni tridy vklada prazdne radky mezi // komentare mimo metody a mezeru za ///. Prazdne radky
# vynechava jen u tridy (-BezPrazdnych): v rutine je cte $t(label+i) a prazdny radek meni chovani.
# U tridy server take pise jeden include bez zavorek a ciselny parametr bez uvozovek.
function ConvertTo-PorovnatelnaTrida([string]$Text, [switch]$BezPrazdnych) {
    $radky = ($Text.TrimStart([char]0xFEFF) -split "`r?`n") | ForEach-Object {
        $r = ($_ -replace '^(\s*///)\s+', '$1 ').TrimEnd()
        if ($BezPrazdnych) { $r = $r -replace '^Include \(\s*([\w.%]+)\s*\)$', 'Include $1' -replace '^(Parameter \w+ = )"(-?\d+(\.\d+)?)";$', '$1$2;' }
        $r
    } | Where-Object { $_ -or -not $BezPrazdnych }
    return (($radky -join "`n").TrimEnd())
}

# --- Atelier REST na DEV Cache -------------------------------------------------------------------

# Prihlaseni bere z promennych, ktere nastavuje ~/.claude/settings.json pro hook kompilace (CACHE_*).
# WebClient s UTF-8: Invoke-RestMethod ve WinPS 5.1 cte odpoved bez charsetu jako Latin-1 a rozbije diakritiku.
function Invoke-Atelier([string]$Cesta, [string]$Telo) {
    $server = if ($env:CACHE_SERVER) { $env:CACHE_SERVER.TrimEnd('/') } else { 'http://192.168.16.1:57770' }
    $ns = if ($env:CACHE_NAMESPACE) { $env:CACHE_NAMESPACE } else { 'MLINE' }
    if (-not $env:CACHE_USER -or -not $env:CACHE_PASSWORD) { throw 'Chybi CACHE_USER nebo CACHE_PASSWORD (nastavuje je ~/.claude/settings.json).' }
    $wc = New-Object Net.WebClient
    $wc.Encoding = $script:Utf8
    $wc.Headers['Authorization'] = 'Basic ' + [Convert]::ToBase64String($script:Utf8.GetBytes("$($env:CACHE_USER):$($env:CACHE_PASSWORD)"))
    try {
        $url = "$server/api/atelier/v1/$ns/$Cesta"
        if ($Telo) { $wc.Headers['Content-Type'] = 'application/json'; $json = $wc.UploadString($url, 'POST', $Telo) } else { $json = $wc.DownloadString($url) }
    } finally { $wc.Dispose() }
    return $json | ConvertFrom-Json
}

# Zdrojak dokumentu z DEV jako text; $null, kdyz na serveru neni.
function Get-AtelierDokument([string]$Dokument) {
    try { $o = Invoke-Atelier "doc/$Dokument" }
    catch {
        $we = $_.Exception
        while ($we -and $we -isnot [Net.WebException]) { $we = $we.InnerException }
        if ($we -and $we.Response -and [int]$we.Response.StatusCode -eq 404) { return $null }
        throw
    }
    return (@($o.result.content) -join "`n")
}

function Invoke-AtelierSql([string]$Sql, [object[]]$Parametry = @()) {
    $o = Invoke-Atelier 'action/query' (@{ query = $Sql; parameters = $Parametry } | ConvertTo-Json -Compress)
    return @($o.result.content)
}

Export-ModuleMember -Function *
