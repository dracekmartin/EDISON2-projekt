# Overi TEST verzi na REL: obsahuje to, co jsme nahrali, a nic navic?
#
# S -Cekej pocka, az se verze dostavi (VERZE.INF), a balik stahne. Overi, ze v nem jsou nase binarky
# (MD5 ze sestaveni-*.json, u podepsaneho souboru otisk bez podpisu) a ze se proti predchozi verzi zmenily
# jen projekty Cache ze seznamu (-Seznam a -Projekty), cizi zmeny vypise zvlast. Zkontroluje, ze v projektech
# nejsou testy s hlavickou "NEPRIDAVAT do Studio projektu" a ze strom noveho i stareho shellu odpovida gitu.
# S -Zapis ulozi overene soubory a commity do stav.json jako posledni nasazeni na REL. Commity posune, jen kdyz
# balik nese vse ze seznamu, vcetne zmeneneho XML kazdeho projektu. -Predchozi je proto verze pred importem.
#
#   powershell -NoProfile -File tools/nasazeni/over-test-verzi.ps1 -Predchozi 261020.1 -Cekej -Sestaveni <sestaveni1.json>,<sestaveni2.json> -Soubory a.exe,b.dll -Seznam <seznam-rel.json> -Zapis
#   powershell -NoProfile -File tools/nasazeni/over-test-verzi.ps1 -Verze 261020.2 -Predchozi 261020.1 -Lokalni <slozka se soubory>
#
# Exit 1 = neco nesedi. ASCII-only zamerne: WinPS 5.1 cte UTF-8 bez BOM jako CP-1252.

[CmdletBinding()]
param(
    [string]$Verze,
    [Parameter(Mandatory)] [string]$Predchozi,
    [string]$Kanal,
    [string[]]$Sestaveni = @(),
    [string[]]$Soubory = @(),
    [string]$Lokalni,
    [switch]$Cekej,
    [string[]]$Projekty = @(),
    [switch]$Zapis,
    [string]$Seznam,
    [string]$Nastaveni
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'spolecne.psm1') -Force
$k = Get-Nastaveni $Nastaveni
if (-not $Kanal) { $Kanal = $k.kanalTest }
$rozdel = { param($s) @($s | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ }) }
$Sestaveni = @(& $rozdel $Sestaveni); $Projekty = @(& $rozdel $Projekty); $Soubory = @(& $rozdel $Soubory)
if ($Sestaveni.Count -eq 0 -and -not $Lokalni) { throw 'Zadej -Sestaveni nebo -Lokalni.' }
if ($Zapis -and ($Sestaveni.Count -eq 0 -or -not $Seznam)) { throw '-Zapis potrebuje -Sestaveni a -Seznam (podle seznamu se posouva kotva).' }
$utf8 = New-Object Text.UTF8Encoding($false)
# Projekty trid ze seznamu jsou nase. Projekt, ktery se v baliku nezmenil (kolega ho importuje sam, nebo
# import chybi), neni chyba balicku, ale drzi kotvu REL: pristi inventura ho ukaze znovu.
$seznamData = $null
$projektySeznamu = @()
if ($Seznam) {
    $seznamData = [IO.File]::ReadAllText($Seznam, $utf8) | ConvertFrom-Json
    if ($Zapis -and $seznamData.faze -ne 'rel') { throw "$Seznam neni inventura faze rel, kotvu REL podle nej posouvat nejde." }
    $projektySeznamu = @(Get-ProjektyTrid $seznamData)
    $Projekty = @($Projekty + $projektySeznamu | Sort-Object -Unique)
}

if ($Cekej) { $Verze = Wait-Verze $k $Kanal $Predchozi; Write-Host "Verze $Verze je hotova." }
if (-not $Verze) { throw 'Zadej -Verze nebo -Cekej.' }

# Ocekavane nase soubory: jmeno -> MD5 ze sestaveni a lokalni kopie (ta jen pro otisk podepsaneho souboru,
# v C:\Edison ji mezitim muze prepsat jiny build). REL nese soubory z vic kol DEV, kazde ma sve sestaveni,
# novejsi (podle casu sestaveni) prebije starsi. -Soubory omezi overeni na soubory, ktere na REL opravdu sly.
$nase = [ordered]@{}
$souboryNase = [ordered]@{}
if ($Sestaveni.Count -gt 0) {
    $data = @($Sestaveni | ForEach-Object { [IO.File]::ReadAllText($_, $utf8) | ConvertFrom-Json } | Sort-Object { [string]$_.cas })
    foreach ($d in $data) {
        foreach ($s in $d.soubory) {
            if (-not $s.nasadit -or ($Soubory.Count -gt 0 -and $Soubory -notcontains $s.jmeno)) { continue }
            $nase[$s.jmeno] = [pscustomobject]@{ md5 = $s.md5; cesta = $s.cesta }; $souboryNase[$s.jmeno] = $s
        }
    }
    $chybiVSestaveni = @($Soubory | Where-Object { -not $nase.Contains($_) })
    if ($chybiVSestaveni.Count -gt 0) { throw "Zadne sestaveni nenese: $($chybiVSestaveni -join ', ')" }
} else {
    foreach ($f in Get-ChildItem -LiteralPath $Lokalni -File) { $nase[$f.Name] = [pscustomobject]@{ md5 = (Get-Md5 $f.FullName); cesta = $f.FullName } }
}

$binarky = @($nase.Keys) + @($k.bezeZmeny) + @($k.strom.edcore) | Sort-Object -Unique
$vzorBinarek = '^(' + (($binarky | ForEach-Object { [regex]::Escape($_) }) -join '|') + ')$'
$vzor = "$vzorBinarek|\.XML$"
$zpracuj = {
    param($jmeno, $bajty)
    if ($jmeno -notmatch '\.XML$') { return , $bajty }
    $xml = [ordered]@{ otisk = Get-XmlOtisk $bajty; testy = @(Get-TestyVProjektu $bajty); uzly = $null }
    if ($jmeno -ieq "$($k.strom.projekt).XML") { $xml.uzly = @(Get-UzlyStromu ([Text.Encoding]::UTF8.GetString($bajty))) }
    return $xml
}

$novy = @{}; foreach ($e in Read-SetupDat (Get-Balik $k $Kanal $Verze) $vzor $zpracuj) { $novy[$e.Jmeno] = $e.Data }
$stary = @{}; foreach ($e in Read-SetupDat (Get-Balik $k $Kanal $Predchozi) $vzor $zpracuj) { $stary[$e.Jmeno] = $e.Data }

function Find-Polozka($Mapa, [string]$Jmeno) {
    # Carka: pole bajtu by se jinak rozbalilo do pipeline po prvcich (desitky MB krat padesat).
    foreach ($kl in $Mapa.Keys) { if ($kl -ieq $Jmeno) { return , $Mapa[$kl] } }
    return $null
}

$chyby = @()
$vysledekNase = foreach ($n in $nase.Keys) {
    $vBaliku = Find-Polozka $novy $n
    if ($null -eq $vBaliku) { $chyby += "$n v baliku chybi"; [ordered]@{ soubor = $n; shoda = $false; podepsany = $null }; continue }
    $podepsany = Test-PodepsanyPe ([byte[]]$vBaliku)
    $shoda = ((Get-Md5Bajtu ([byte[]]$vBaliku)) -eq $nase[$n].md5)
    if (-not $shoda -and $podepsany) {
        if ((Test-Path -LiteralPath $nase[$n].cesta) -and (Get-Md5 $nase[$n].cesta) -eq $nase[$n].md5) {
            $shoda = Test-ShodaBinarek ([byte[]]$vBaliku) ([IO.File]::ReadAllBytes($nase[$n].cesta))
        } else { $chyby += "$n je v baliku podepsany a lokalni kopie z buildu uz neni, otisk nejde overit" }
    }
    if (-not $shoda) { $chyby += "$n v baliku neni nase verze" }
    [ordered]@{ soubor = $n; shoda = $shoda; podepsany = $podepsany }
}
$vysledekBezeZmeny = foreach ($n in $k.bezeZmeny) {
    if ($nase.Contains($n)) { continue }
    $a = Find-Polozka $novy $n; $b = Find-Polozka $stary $n
    if ($null -eq $a -and $null -eq $b) { continue }
    $shoda = ($null -ne $a -and $null -ne $b -and (Test-ShodaBinarek ([byte[]]$a) ([byte[]]$b)))
    [ordered]@{ soubor = $n; bezeZmeny = $shoda }
}

$nepridavat = @{}
foreach ($r in $k.repoEdison, $k.repoEdison2) { foreach ($t in Get-NepridavatTridy $r) { $nepridavat[$t] = $true } }
$zmeneneXml = @(); $ciziXml = @(); $testy = @()
foreach ($kl in ($novy.Keys | Where-Object { $_ -match '\.XML$' } | Sort-Object)) {
    $a = $novy[$kl]; $b = Find-Polozka $stary $kl
    $zakazane = @($a.testy | Where-Object { $nepridavat.ContainsKey($_) })
    if ($zakazane.Count -gt 0) { $testy += [ordered]@{ projekt = $kl; tridy = $zakazane } }
    if ($null -eq $b -or $a.otisk -ne $b.otisk) {
        $projekt = $kl -replace '\.XML$', ''
        # Cizi zmeny na sdilenem REL jsou bezne (kolegove), jen se vypisou. Bez -Projekty neni nic nase.
        if ($Projekty -icontains $projekt) { $zmeneneXml += $projekt } else { $ciziXml += $projekt }
    }
}
if ($testy.Count -gt 0) { $chyby += "testovaci tridy v projektech: $(($testy | ForEach-Object { $_.projekt }) -join ', ')" }
$projektyBezeZmeny = @($projektySeznamu | Where-Object { $zmeneneXml -notcontains $_ })

$strom = [ordered]@{ novyShell = $null; legacyShell = $null }
$xmlStromu = Find-Polozka $novy "$($k.strom.projekt).XML"
if ($xmlStromu) {
    $git = @(Get-UzlyStromu (Get-GitText $k.repoEdison2 'HEAD' $k.strom.treeCls))
    $rozdil = @(Compare-Object $xmlStromu.uzly $git)
    $strom.novyShell = ($rozdil.Count -eq 0)
    if (-not $strom.novyShell) { $chyby += "strom noveho shellu v baliku se lisi od gitu ($($rozdil.Count) uzlu)" }
}
$edcore = Find-Polozka $novy $k.strom.edcore
if ($edcore) {
    $tmp = Join-Path $k.stavDir "edcore-$Verze.dll"
    [IO.File]::WriteAllBytes($tmp, [byte[]]$edcore)
    $strom.legacyShell = ((ConvertTo-PorovnatelnyText (Get-StromEdCore $tmp)) -eq (ConvertTo-PorovnatelnyText (Get-GitText $k.repoEdison 'HEAD' $k.strom.legacyXml)))
    if (-not $strom.legacyShell) { $chyby += 'strom v EDCore.dll baliku se lisi od gitu' }
}

$vysledek = [ordered]@{
    kanal = $Kanal; verze = $Verze; predchozi = $Predchozi; ok = ($chyby.Count -eq 0); chyby = $chyby
    nase = @($vysledekNase); bezeZmeny = @($vysledekBezeZmeny); zmeneneXml = $zmeneneXml; ciziXml = $ciziXml
    projektyBezeZmeny = $projektyBezeZmeny; testyVProjektech = $testy; strom = $strom
}
$vystup = Join-Path $k.stavDir "balik-$Kanal-$Verze.json"
Write-Json $vystup $vysledek

if ($Zapis -and $vysledek.ok) {
    $md5Nase = [ordered]@{}; foreach ($n in $souboryNase.Keys) { $md5Nase[$n] = $souboryNase[$n].md5 }
    $nasazeno = @($md5Nase.Keys) + @($zmeneneXml | ForEach-Object { "$_.XML" })
    $kotva = Get-KotvaPoNasazeni $k $seznamData ([pscustomobject]@{ soubory = @($souboryNase.Values) }) $nasazeno -VcetneProjektu
    if ($kotva.chybi.Count -gt 0) { Write-Host "Kotva REL zustava, nenasazeno: $($kotva.chybi -join ', ')" }
    Write-NasazeniStav $k 'rel' ([ordered]@{ cas = (Get-Date).ToString('s'); commity = $kotva.commity; verze = $Verze; soubory = $md5Nase })
}

Write-Host ("Balik {0} {1} proti {2}: {3}" -f $Kanal, $Verze, $Predchozi, $(if ($vysledek.ok) { 'OK' } else { 'NESEDI' }))
foreach ($r in $vysledekNase) { Write-Host ("  {0,-22} {1}{2}" -f $r.soubor, $(if ($r.shoda) { 'nase' } else { 'NENI NASE' }), $(if ($r.podepsany) { ' (podepsany)' } else { '' })) }
Write-Host ("  beze zmeny: {0}" -f (($vysledekBezeZmeny | ForEach-Object { "$($_.soubor)=$($_.bezeZmeny)" }) -join ', '))
Write-Host ("  zmenena XML: {0}" -f ($zmeneneXml -join ', '))
if ($ciziXml.Count) { Write-Host ("  cizi zmeny XML: {0}" -f ($ciziXml -join ', ')) }
if ($projektyBezeZmeny.Count) { Write-Host ("  projekty ze seznamu beze zmeny proti {0}: {1}" -f $Predchozi, ($projektyBezeZmeny -join ', ')) }
Write-Host ("  testy v projektech: {0}, strom novy shell = git {1}, EDCore = git {2}" -f $testy.Count, $strom.novyShell, $strom.legacyShell)
foreach ($c in $chyby) { Write-Host "  CHYBA: $c" }
$vystup
if (-not $vysledek.ok) { exit 1 }
