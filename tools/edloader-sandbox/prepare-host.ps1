# Prepares a Windows Sandbox work folder for testing EDLOADER (EDISON.exe) on a clean station
# without the .NET 10 Desktop Runtime: unpacking EDISONRUNTIME.zip into dotnet\, the runtime lock,
# the fallback to the old shell, and TemplateDesigner started from the old shell. The scenarios
# and their expected results are described in test-edloader.ps1 next to this script.
#
# The work folder (default C:\EdisonSandbox) gets:
#   install\              station files: a copy of a server installation, or built from a release package
#   test-edloader.ps1     scenario runner used inside the sandbox (copied from this folder)
#   edison-sandbox.wsb    sandbox definition that maps the work folder; double-click it to start
#   protokol-<date>.txt   written by test-edloader.ps1 inside the sandbox, survives closing it
#   package\              downloaded release packages (cache, reused by the next run)
#
# Two sources of the station files:
#   -Source <dir>         copy of the server's EDISON installation directory (UNC or local);
#                         dotnet\, the lock folders and _install are skipped so the station starts clean
#   -Version -Channel     built from <ReleaseUrl>/<Channel>/<Version>/SETUP.zip and DEFINITION.zip,
#                         the way EDISONINS lays them out; Cache XML exports and globals are skipped,
#                         VERZE.INF and EDISONini.xml are written by this script
#
# The connection is never stored in the repository: test servers and channels change. -Server and
# -RestPort are required with -Version and rewrite EDISONini.xml with -Source. VERZE.INF has to equal
# the Cache version of that server (with a patch, e.g. 260929.4.1), otherwise both shells stop with
# a version mismatch; the script reads it from ALVA.SETUP.Main.LoadSettings, -InstalledVersion
# overrides it. The same read-only call reports the newShell node and the localPath list.
#
# Update test (scenarios U0-U2 in test-edloader.ps1): -ServerFolder also builds <WorkDir>\server, a copy of
# the station files with the server's VERZE.INF, as the server installation the station updates from.
# The sandbox maps the work folder at the same path, so on the server set localSourcePath to
# <WorkDir>\server and add the sandbox to localPath (test-edloader.ps1 -Action Ids prints the entries;
# the sandbox gets a new computer name on every start). For a real server update install the new version
# on the server, copy its installation directory to the host and refresh the folder with
# -ServerSource <copy>: only <WorkDir>\server changes, the running sandbox sees it at once, and
# EDISONini.xml in it is rewritten to the address the sandbox reaches (EDUPDATE always copies that file,
# the server's own copy points to an address the station may not reach). Every EDISONini.xml the script
# writes or rewrites gets <IRIS> = -Iris (default 1): without it the old shell connects through the
# superserver port, which is usually reachable only inside the server's network (seen on 2026-10-02:
# "Communication link failure ... :1972" after the update copied the server's file).
# -StationVersion makes the station a version behind (e.g. 260929.4 against 260929.4.1) and
# -StationRuntimeZip gives it an older runtime, so the update also patches dotnet\ (build one with tools\build-runtime-zip.ps1 -Version <older> -OutFile <path>,
# never into C:\Edison). Put both server settings back after the test.
#
# Prerequisites on the host:
#   - Windows Sandbox, once as admin: Enable-WindowsOptionalFeature -Online -FeatureName
#     Containers-DisposableClientVM -All (then restart)
#   - network from the host to the server (the sandbox goes out through the host)
#   - 7za.exe for the release package (default C:\Edison\7za.exe)
#   - MLRest.dll for the server check (default C:\Edison\MLRest.dll, use a Release build)
#
# Usage (Windows PowerShell 5.1, on the host, not in the sandbox):
#   powershell -ExecutionPolicy Bypass -File tools\edloader-sandbox\prepare-host.ps1 `
#       -Version 260929.4 -Channel TEST -Server <host> -RestPort <port>
#   powershell -ExecutionPolicy Bypass -File tools\edloader-sandbox\prepare-host.ps1 -Source \\<server>\<EDISON>
#   powershell -ExecutionPolicy Bypass -File tools\edloader-sandbox\prepare-host.ps1 -ServerSource <copy> -WorkDir <dir>
# Then double-click <WorkDir>\edison-sandbox.wsb. Close a running sandbox first: install\ is rebuilt.
#
# ASCII only -- Windows PowerShell 5.1 reads UTF-8-no-BOM as CP-1252.

[CmdletBinding(DefaultParameterSetName = "Package")]
param(
    [Parameter(ParameterSetName = "Copy", Mandatory = $true)][string]$Source,
    [Parameter(ParameterSetName = "Package", Mandatory = $true)][string]$Version,
    [Parameter(ParameterSetName = "Package")][ValidateSet("TEST", "release")][string]$Channel = "TEST",
    [Parameter(ParameterSetName = "Refresh", Mandatory = $true)][string]$ServerSource,
    [string]$Server = "",
    [int]$RestPort = 0,
    [int]$SuperserverPort = 1972,
    [ValidateSet(0, 1)][int]$Iris = 1,
    [string]$InstalledVersion = "",
    [switch]$ServerFolder,
    [string]$StationVersion = "",
    [string]$StationRuntimeZip = "",
    [string]$WorkDir = "C:\EdisonSandbox",
    [string]$ReleaseUrl = "http://release.m-line.cz:59993/edison",
    [string]$SevenZip = "C:\Edison\7za.exe",
    [string]$MLRestDll = "C:\Edison\MLRest.dll"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"   # Invoke-WebRequest is many times slower with the progress bar
# Absolute, because the .wsb needs absolute paths and [IO.File] resolves against the process directory
$WorkDir = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($WorkDir)
$install = Join-Path $WorkDir "install"
$serverDir = Join-Path $WorkDir "server"
$enc1250 = [Text.Encoding]::GetEncoding(1250)

if ($PSCmdlet.ParameterSetName -eq "Package" -and ($Server -eq "" -or $RestPort -le 0)) {
    throw "-Version needs -Server and -RestPort (the station's EDISONini.xml is written from them)."
}

# XmlDocument.Load honours the encoding; Get-Content in PS 5.1 reads UTF-8 without BOM as ANSI.
function Read-Xml([string]$path) {
    $xml = New-Object Xml.XmlDocument
    $xml.Load($path)
    return ,$xml
}

# Fills -Server / -RestPort that were not passed from the first connection of an existing EDISONini.xml.
function Complete-Connection([string]$iniPath) {
    if (-not (Test-Path $iniPath)) { return }
    $connection = (Read-Xml $iniPath).SelectSingleNode("/edison/connection")
    if (-not $connection) { return }
    if ($script:Server -eq "") { $script:Server = $connection.GetAttribute("server") }
    if ($script:RestPort -le 0) { $script:RestPort = [int]("0" + $connection.GetAttribute("restport")) }
}

# Refuses a source inside install\ or server\, which this run deletes or mirrors over.
function Assert-OutsideWorkFolders([string]$path) {
    $full = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($path).TrimEnd("\") + "\"
    foreach ($target in $install, $serverDir) {
        if ($full.StartsWith($target + "\", [StringComparison]::OrdinalIgnoreCase)) { throw "$path lies in $target, which this run replaces." }
    }
}

# Read-only server check, the same call EDLOADER makes before the login. Returns key -> values.
function Get-ServerSettings([string]$server, [int]$port) {
    $result = @{}
    if (-not (Test-NetConnection -ComputerName $server -Port $port -InformationLevel Quiet -WarningAction SilentlyContinue)) {
        Write-Host "Server ${server}:$port is not reachable from this host."
        return $result
    }
    if (-not (Test-Path $MLRestDll)) { Write-Host "No $MLRestDll, server settings not read."; return $result }
    Add-Type -Path $MLRestDll
    $rest = New-Object ALWA.SYS.MLRest($server, $port)
    $rows = $rest.Query("ALVA.SETUP.Main.LoadSettings", [object[]]@())
    if ("$($rest.RestStatus.State)" -eq "Error") { Write-Host "LoadSettings failed: $($rest.RestStatus.Message)"; return $result }
    for ($i = 1; $i -lt $rows.Length; $i++) {
        $parts = ([string]$rows[$i]).Split($rest.Delimiter)
        if (-not $result.ContainsKey($parts[0])) { $result[$parts[0]] = @() }
        $result[$parts[0]] += ,(@($parts | Select-Object -Skip 1))
    }
    return $result
}

# DEFINITION.DAT: 3-digit count, two length-prefixed header strings, then per file a 3-digit length
# and an entry "<TYPE>~[<subdir>~]<name>" with every character shifted by +1. Lengths are not shifted.
function Read-Definition([string]$path) {
    $raw = $enc1250.GetString([IO.File]::ReadAllBytes($path))
    $script:defPos = 0
    $next = { param($n) $s = $raw.Substring($script:defPos, $n); $script:defPos += $n; $s }
    $count = [int](& $next 3)
    & $next ([int](& $next 3)) | Out-Null
    & $next ([int](& $next 3)) | Out-Null
    $entries = New-Object System.Collections.Generic.List[string]
    for ($i = 0; $i -lt $count; $i++) {
        $shifted = & $next ([int](& $next 3))
        $entries.Add(-join ($shifted.ToCharArray() | ForEach-Object { [char]([int]$_ - 1) }))
    }
    return $entries
}

# SETUP.DAT: 3-digit count, then per file 9-digit size, 3-digit name length, name, content
# (EDISON/SETUPWIZARD/frmMain.vb, createFile_SetupDat). The order matches DEFINITION.DAT.
function Expand-SetupDat([string]$setupPath, $definition, [string]$target) {
    $fs = [IO.File]::OpenRead($setupPath)
    $buffer = New-Object byte[] (1MB)
    $readText = {
        param($n)
        $bytes = New-Object byte[] $n; $done = 0
        while ($done -lt $n) { $r = $fs.Read($bytes, $done, $n - $done); if ($r -le 0) { throw "SETUP.DAT ends early" }; $done += $r }
        $enc1250.GetString($bytes)
    }
    $written = 0; $skipped = 0; [long]$total = 0
    try {
        $count = [int](& $readText 3)
        if ($count -ne $definition.Count) { throw "SETUP.DAT has $count files, DEFINITION.DAT $($definition.Count)" }
        for ($i = 0; $i -lt $count; $i++) {
            [long]$size = [long](& $readText 9)
            $name = (& $readText ([int](& $readText 3))).TrimEnd()   # 19 C1 names end with a space in the manifest
            $kind = $definition[$i].Substring(0, 3)
            $parts = $definition[$i].Substring(3).Split("~")
            if ($parts[-1].TrimEnd() -ne $name) { throw "File $i is '$name' but DEFINITION.DAT says '$($definition[$i])'" }
            if ($kind -eq "XML" -or $kind -eq "GLB") {          # Cache project exports and globals, not station files
                $fs.Seek($size, [IO.SeekOrigin]::Current) | Out-Null; $skipped++; continue
            }
            $dir = $target
            if ($parts.Length -gt 2) { $dir = Join-Path $target (($parts[1..($parts.Length - 2)]) -join "\") }   # EDFiles, PS, locales
            New-Item -ItemType Directory -Force -Path $dir | Out-Null
            $out = [IO.File]::Create((Join-Path $dir $name))
            try {
                $left = $size
                while ($left -gt 0) {
                    $n = $fs.Read($buffer, 0, [int][Math]::Min([long]$buffer.Length, $left))
                    if ($n -le 0) { throw "SETUP.DAT ends inside $name" }
                    $out.Write($buffer, 0, $n); $left -= $n
                }
            } finally { $out.Dispose() }
            $written++; $total += $size
        }
    } finally { $fs.Dispose() }
    Write-Host ("Station files: {0} written ({1:N0} MB), {2} Cache XML/globals skipped" -f $written, ($total / 1MB), $skipped)
}

function Write-EdisonIni([string]$path, [string]$server, [int]$restPort) {
    $xml = "<edison>`r`n  <initialized>1</initialized>`r`n  <forceChange>1</forceChange>`r`n  <IRIS>$Iris</IRIS>`r`n" +
           "  <connection name=`"SANDBOX`" server=`"$server`" port=`"$SuperserverPort`" restport=`"$restPort`" />`r`n</edison>`r`n"
    [IO.File]::WriteAllText($path, $xml, (New-Object Text.UTF8Encoding($false)))
}

# Points every connection of an existing EDISONini.xml to the given server and sets <IRIS>, the rest stays.
# Without IRIS=1 the old shell opens the superserver (port) besides REST (frmNoMDI.vb, OpenConnection);
# from outside the server's network only the REST port is usually reachable.
function Set-IniConnection([string]$path, [string]$server, [int]$restPort) {
    $ini = Read-Xml $path
    foreach ($node in $ini.SelectNodes("/edison/connection")) {
        $node.SetAttribute("server", $server)
        $node.SetAttribute("restport", "$restPort")
    }
    $irisNode = $ini.SelectSingleNode("/edison/IRIS")
    if (-not $irisNode) {
        $irisNode = $ini.CreateElement("IRIS")
        [void]$ini.DocumentElement.InsertBefore($irisNode, $ini.SelectSingleNode("/edison/connection"))
    }
    $irisNode.InnerText = "$Iris"
    $ini.Save($path)
}

function Get-ZipRuntimeVersion([string]$zipPath) {
    if (-not (Test-Path $zipPath)) { return "-" }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $entry = $zip.GetEntry("EDISONRUNTIME.version")
        if (-not $entry) { return "no version" }
        $reader = New-Object IO.StreamReader($entry.Open())
        try { return $reader.ReadToEnd().Trim() } finally { $reader.Dispose() }
    } finally { $zip.Dispose() }
}

# Refresh: only <WorkDir>\server from a copy of the real server installation; the station stays as it is.
if ($PSCmdlet.ParameterSetName -eq "Refresh") {
    if (-not (Test-Path (Join-Path $ServerSource "VERZE.INF"))) { throw "No VERZE.INF in $ServerSource" }
    Assert-OutsideWorkFolders $ServerSource
    Complete-Connection (Join-Path $install "EDISONini.xml")
    if ($Server -eq "" -or $RestPort -le 0) { throw "Pass -Server and -RestPort, the station's EDISONini.xml does not give them." }
    # _install is the installer's staging (targetPath in LoadSettings), stations never copy it
    robocopy $ServerSource $serverDir /MIR /XD dotnet dotnet.new dotnet.lock _install /R:1 /W:1 /NFL /NDL /NP /NJH /NJS | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy to $serverDir failed with $LASTEXITCODE" }
    $iniPath = Join-Path $serverDir "EDISONini.xml"
    if (Test-Path $iniPath) { Set-IniConnection $iniPath $Server $RestPort } else { Write-EdisonIni $iniPath $Server $RestPort }
    "--- server folder $serverDir refreshed from $ServerSource"
    "VERZE.INF: " + (Get-Content (Join-Path $serverDir "VERZE.INF")) + ", runtime zip " + (Get-ZipRuntimeVersion (Join-Path $serverDir "EDISONRUNTIME.zip"))
    "EDISONCRC.bin: " + $(if (Test-Path (Join-Path $serverDir "EDISONCRC.bin")) { "present, EDUPDATE copies only the files that changed" } else { "missing, EDUPDATE copies everything" })
    "EDISONini.xml connection: ${Server}:$RestPort"
    "Station install\VERZE.INF: " + (Get-Content (Join-Path $install "VERZE.INF") -ErrorAction SilentlyContinue)
    $settings = Get-ServerSettings $Server $RestPort
    if ($settings.ContainsKey("version")) { "Server Cache version: " + $settings["version"][0][0] }
    return
}

New-Item -ItemType Directory -Force -Path $WorkDir | Out-Null

# Everything that can fail is checked before install\ is deleted.
if ($PSCmdlet.ParameterSetName -eq "Copy") {
    if (-not (Test-Path (Join-Path $Source "EDISON.exe"))) { throw "No EDISON.exe in $Source" }
    Assert-OutsideWorkFolders $Source
    Complete-Connection (Join-Path $Source "EDISONini.xml")
    if ($Server -eq "" -or $RestPort -le 0) { throw "Pass -Server and -RestPort, EDISONini.xml in $Source does not give them." }
    $settings = Get-ServerSettings $Server $RestPort
    if (Test-Path $install) { Remove-Item $install -Recurse -Force }
    robocopy $Source $install /MIR /XD dotnet dotnet.new dotnet.lock _install /R:1 /W:1 /NFL /NDL /NP /NJH /NJS | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed with $LASTEXITCODE" }
    $iniPath = Join-Path $install "EDISONini.xml"
    if (Test-Path $iniPath) { Set-IniConnection $iniPath $Server $RestPort } else { Write-EdisonIni $iniPath $Server $RestPort }
} else {
    $packageDir = Join-Path $WorkDir "package\$Channel-$Version"
    New-Item -ItemType Directory -Force -Path $packageDir | Out-Null
    foreach ($file in "SETUP", "DEFINITION") {
        $zip = Join-Path $packageDir "$file.zip"
        if (-not (Test-Path (Join-Path $packageDir "$file.DAT"))) {
            if (-not (Test-Path $zip)) {
                Write-Host "Downloading $ReleaseUrl/$Channel/$Version/$file.zip"
                Invoke-WebRequest -UseBasicParsing -Uri "$ReleaseUrl/$Channel/$Version/$file.zip" -OutFile $zip
            }
            & $SevenZip x -y "-o$packageDir" $zip | Out-Null   # 7z archives despite the .zip name
            if ($LASTEXITCODE -ne 0) { throw "7za failed on $zip ($LASTEXITCODE)" }
        }
    }
    $definition = Read-Definition (Join-Path $packageDir "DEFINITION.DAT")
    $settings = Get-ServerSettings $Server $RestPort
    if ($InstalledVersion -eq "" -and $settings.ContainsKey("version")) { $InstalledVersion = "$($settings["version"][0][0])" }
    # LoadSettings returns the row empty on a server without an installed release
    if ($InstalledVersion -eq "") { throw "Server version unknown, pass -InstalledVersion (VERZE.INF must equal it)." }
    if (Test-Path $install) { Remove-Item $install -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $install | Out-Null
    Expand-SetupDat (Join-Path $packageDir "SETUP.DAT") $definition $install
    Write-EdisonIni (Join-Path $install "EDISONini.xml") $Server $RestPort
    [IO.File]::WriteAllText((Join-Path $install "VERZE.INF"), "LAST=$InstalledVersion`r`n", [Text.Encoding]::ASCII)
}

# The server folder is copied before the station changes below, so it keeps the server's version and runtime.
# Without -ServerFolder it stays, it may hold a real server copy taken over with -ServerSource.
if ($ServerFolder) {
    if (Test-Path $serverDir) { Remove-Item $serverDir -Recurse -Force }
    robocopy $install $serverDir /E /R:1 /W:1 /NFL /NDL /NP /NJH /NJS | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy to $serverDir failed with $LASTEXITCODE" }
}
if ($StationVersion -ne "") {
    [IO.File]::WriteAllText((Join-Path $install "VERZE.INF"), "LAST=$StationVersion`r`n", [Text.Encoding]::ASCII)
}
if ($StationRuntimeZip -ne "") { Copy-Item $StationRuntimeZip (Join-Path $install "EDISONRUNTIME.zip") -Force }

Copy-Item (Join-Path $PSScriptRoot "test-edloader.ps1") $WorkDir -Force
$wsb = @"
<Configuration>
  <Networking>Default</Networking>
  <MemoryInMB>6144</MemoryInMB>
  <MappedFolders>
    <MappedFolder>
      <HostFolder>$WorkDir</HostFolder>
      <SandboxFolder>$WorkDir</SandboxFolder>
      <ReadOnly>false</ReadOnly>
    </MappedFolder>
  </MappedFolders>
  <LogonCommand>
    <Command>powershell.exe -ExecutionPolicy Bypass -NoExit -Command "Set-ExecutionPolicy -Scope LocalMachine -ExecutionPolicy Bypass -Force; &amp; '$WorkDir\test-edloader.ps1' -Action Setup"</Command>
  </LogonCommand>
</Configuration>
"@
[IO.File]::WriteAllText((Join-Path $WorkDir "edison-sandbox.wsb"), $wsb, [Text.Encoding]::ASCII)

Write-Host "--- station in $install"
foreach ($f in "EDISON.exe", "EDISONMAIN.exe", "EDISONMAIN2.exe", "EDControls.dll", "EDISONMAIN2API.exe",
               "EDISONRUNTIME.zip", "TemplateDesigner.exe", "VERZE.INF", "EDISONini.xml") {
    $p = Join-Path $install $f
    if (Test-Path $p) { "{0,-22} {1,12} B" -f $f, (Get-Item $p).Length } else { "{0,-22} MISSING" -f $f }
}
"VERZE.INF: " + (Get-Content (Join-Path $install "VERZE.INF") -ErrorAction SilentlyContinue) +
    ", runtime zip " + (Get-ZipRuntimeVersion (Join-Path $install "EDISONRUNTIME.zip"))
if ($ServerFolder) {
    "--- server folder $serverDir (update source)"
    "VERZE.INF: " + (Get-Content (Join-Path $serverDir "VERZE.INF")) + ", runtime zip " + (Get-ZipRuntimeVersion (Join-Path $serverDir "EDISONRUNTIME.zip"))
    "On the server (Nastaveni > Aktualizace in the old shell): localSourcePath = $serverDir,"
    "localPath = the entry from test-edloader.ps1 -Action Ids inside the sandbox. Put both back afterwards."
}
"Server ${Server}:$RestPort"
if ($settings.Count) {
    "  version   " + ($settings["version"][0] -join " | ")
    # LoadSettings leaves the row out when the node is not set
    "  newShell  " + $(if ($settings.ContainsKey("newShell") -and "$($settings["newShell"][0])" -eq "1") { "1 (EDLOADER starts the new shell)" } else { "not set (old shell)" })
    "  pending   " + ($settings["pending"][0] -join " | ")
    $clients = @($settings["localPath"] | Where-Object { $_ -and $_[0] }).Count
    "  localPath {0} station(s) listed; the sandbox is not one of them, so it counts as a server installation" -f $clients
}
"Next: double-click $WorkDir\edison-sandbox.wsb"
