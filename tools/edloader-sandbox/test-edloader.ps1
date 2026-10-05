# Scenario runner for EDLOADER (EDISON.exe) inside Windows Sandbox: a clean Windows without the .NET 10
# Desktop Runtime, prepared by prepare-host.ps1. Every action appends to protokol-<date>.txt in the
# mapped work folder, so the log survives closing the sandbox.
#
# What EDLOADER does (EDISON/EDLOADER/clsStartup.vb, Startup.Run): it starts the new shell only when the
# new-shell files lie next to it, ^|"MLINE"|EDISON("System","NewShell")=1 on the server (read through
# ALVA.SETUP.Main.LoadSettings) and a runtime is available. Without a global .NET 10 Desktop Runtime it
# unpacks EDISONRUNTIME.zip to dotnet.new, renames it to dotnet\ and writes dotnet\EDISONRUNTIME.version,
# under the lock dotnet.lock\lock (120 s). Every failure ends in the old shell (EDISONMAIN.EXE).
# The .NET exes look for dotnet\ next to them first, then for a global runtime (SingleFilePublish.targets).
#
# The node is switched in the Cache terminal of the test server (applies from the next start):
#   on:  s ^|"MLINE"|EDISON("System","NewShell")=1      off:  k ^|"MLINE"|EDISON("System","NewShell")
#
# Scenarios, in a workable order (observed 2026-10-02 on 260929.4, runtime 10.0.12):
#   S2  node on, first start           Start   dotnet.new, then dotnet\ with the marker (~1 s), new shell
#                                              (3.3 s from EDISON.exe); login and TemplateDesigner work
#   S3  node on, next start            Start   no unpacking, new shell in 0.8 s
#   S5  node on, two starts at once    Reset, Twice   one unpack, both new shells start
#   S6  node on, interrupted unpack    Reset, Kill, Start   only dotnet.new is left, no dotnet\; the next
#                                              start removes it, unpacks again and starts the new shell
#   S7  node on, no write right        ReadOnly   old shell without any message, no wait for the lock
#   S4  node off, dotnet\ present      Start   old shell; TemplateDesigner from its tree runs on dotnet\
#   S1  node off, fresh sandbox        Start   old shell, no dotnet\; TemplateDesigner from the old shell
#                                              shows the .NET "You must install .NET Desktop Runtime" dialog
#   S1b after installing the runtime   (the dialog's download, needs admin) TemplateDesigner from the old
#                                              shell starts on the global runtime
#   S8  node on, global runtime, no dotnet\   Start   no dotnet\, no lock, new shell on the global runtime
#                                              in 0.9 s
# S1, S1b and S8 need a fresh sandbox (closing it discards everything). TemplateDesigner is started by
# hand from the tree ("Navrhar sestav (novy)") to go the way users do; action Td starts it directly.
#
# Update scenarios: prepare-host.ps1 -ServerFolder -StationRuntimeZip <older zip> (no -StationVersion,
# the station must still log in to change the settings). The client update needs the station in the
# server's localPath list and a readable source: the shell reads localSourcePath, else edisonPath, which
# is a path on the server (Updater.cs, UpdateSettings.SourceDir).
#   U0  node on   Ids, Start, log in; Nastaveni > Aktualizace: add the Ids IPv4 to the list of computers
#                 (localPath) and write <work folder>\server into "Cesta pro stazeni dat:" (localSourcePath,
#                 a text box in the toolbar above the list); save, close the shell. Keep the sandbox running,
#                 a restart changes its name and address. Set BOTH before the server gets a newer version:
#                 a listed station with an empty localSourcePath reads edisonPath, a path on the server, and
#                 ends with "Z cesty ... nelze zjistit serverovou verzi" after the login, before the shell
#                 opens. Fallback in the Cache terminal:
#                 s ^|"MLINE"|EDISON("System","Update",11)=$lb("<work folder>\server")
#   Then a real version: TEST FULL, installed on the server, its installation directory copied to the host
#   (RDP with redirected drive C:, robocopy E:\<EDISON> \\tsclient\C\<copy> /E /XD dotnet dotnet.new
#   dotnet.lock _install /R:1 /W:1) and taken over with prepare-host.ps1 -ServerSource <copy>.
#   U1  node on   Update: the new shell starts on the older runtime, after the login finds the newer version,
#                 starts EDUPDATE and exits; EDUPDATE copies the changed files (EDISONCRC.bin), starts
#                 EDISON.exe, which patches dotnet\ with the newer runtime next to the old one and starts the
#                 new shell again. Observed 2026-10-02, 260929.4.1 -> 261002.0: 8 s from the login to the
#                 new shell, 44 files, dotnet\ fxr 10.0.11 -> 10.0.11,10.0.12 in 1.5 s.
#   U2  node off  SetVersion -Value <the old version>, Update: the same through the old shell; observed: EDUPDATE
#                 runs ~18 s (waits for its completion message), then the old shell starts again.
# Cleanup in the Cache terminal: k ^|"MLINE"|EDISON("System","Update",11) and ...("Update",8) (the list).
#
# Actions (run from the PowerShell window that opens with the sandbox):
#   Setup     copy install\ to C:\EDISON and Check (runs by itself after the sandbox starts)
#   Check     global .NET 10, files, VERZE.INF, server reachability, state of dotnet\
#   Start     start EDISON.exe and log the unpacking and which shell starts
#   Twice     two EDISON.exe 300 ms apart
#   Kill      start EDISON.exe and kill it as soon as dotnet.new appears
#   ReadOnly  copy to C:\EDISONRO without the right to create files and start from there
#   Td        start TemplateDesigner.exe directly
#   Shell     start EDISONMAIN2.exe directly (bypasses EDLOADER, the shell redirects back through it)
#   Reset     delete dotnet\, dotnet.new and dotnet.lock in C:\EDISON
#   State     log the state only
#   Ids       log the computer name, DNS host name and IPv4 to put into localPath on the server
#   SetVersion -Value <v>   write LAST=<v> into C:\EDISON\VERZE.INF (makes the station a version behind)
#   Update    start EDISON.exe and follow everything for -Seconds (default 600; Ctrl+C ends it, the log
#             stays): processes starting and ending, VERZE.INF, the runtime zip and dotnet\ versions
#   -Note S2  tags the log entry with the scenario
#
# Pitfalls:
#   - The sandbox start allows scripts for the whole (disposable) sandbox. With an older edison-sandbox.wsb a
#     PowerShell window opened by hand refuses scripts; run Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
#   - Setup and ReadOnly copy ~550 MB silently, about 30 s.
#   - icacls W includes SYNCHRONIZE and denying it blocks starting any exe, so ReadOnly denies WD and AD only.
#   - VERZE.INF must equal the server's Cache version, otherwise both shells stop with a version mismatch.
#   - Without <IRIS>1</IRIS> in EDISONini.xml the old shell connects through the superserver port as well and
#     fails with "Communication link failure" from outside the server's network; the update copies the
#     server's EDISONini.xml, so prepare-host.ps1 sets IRIS in every copy it writes.
#   - Clean Windows lacks the Visual C++ redistributable: the old shell's dashboard panels report a missing
#     CefSharp.Core.Runtime.dll. Unrelated to EDLOADER.
#
# ASCII only -- Windows PowerShell 5.1 reads UTF-8-no-BOM as CP-1252.

param(
    [ValidateSet("Setup", "Check", "Start", "Twice", "Kill", "ReadOnly", "Td", "Shell", "Reset", "State",
                 "Ids", "SetVersion", "Update")]
    [string]$Action = "Check",
    [int]$Seconds = 0,
    [string]$Value = "",
    [string]$Note = ""
)
if ($Seconds -le 0) { $Seconds = $(if ($Action -eq "Update") { 600 } else { 240 }) }
$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot
$Source = Join-Path $Root "install"
$Edison = "C:\EDISON"
$Log = Join-Path $Root ("protokol-{0:yyyyMMdd}.txt" -f (Get-Date))
$Watched = "EDISON", "EDISONMAIN", "EDISONMAIN2", "EDISONMAIN2API", "TemplateDesigner", "EDUPDATE"

function Write-Log([string]$text) {
    $line = "{0:HH:mm:ss.fff}  {1}" -f (Get-Date), $text
    Write-Host $line
    Add-Content -Path $Log -Value $line
}

function Get-GlobalRuntime {
    $dir = Join-Path $env:ProgramW6432 "dotnet\shared\Microsoft.WindowsDesktop.App"
    if (-not (Test-Path $dir)) { return @() }
    @(Get-ChildItem $dir -Directory -Filter "10.*" | ForEach-Object Name)
}

function Get-Running {
    @(Get-Process -Name $Watched -ErrorAction SilentlyContinue | ForEach-Object { "{0}({1})" -f $_.ProcessName, $_.Id })
}

function Write-State([string]$dir) {
    $dotnet = Join-Path $dir "dotnet"
    $marker = Join-Path $dotnet "EDISONRUNTIME.version"
    $parts = @()
    if (Test-Path $dotnet) {
        $ver = if (Test-Path $marker) { (Get-Content $marker -Raw).Trim() } else { "no marker" }
        $fxr = @(Get-ChildItem (Join-Path $dotnet "host\fxr") -Directory -ErrorAction SilentlyContinue | ForEach-Object Name) -join ","
        $files = @(Get-ChildItem $dotnet -Recurse -File -ErrorAction SilentlyContinue).Count
        $parts += "dotnet\ yes (version $ver, fxr $fxr, $files files)"
    } else { $parts += "dotnet\ no" }
    $parts += "dotnet.new " + $(if (Test-Path (Join-Path $dir "dotnet.new")) { "YES" } else { "no" })
    $parts += "dotnet.lock " + $(if (Test-Path (Join-Path $dir "dotnet.lock\lock")) { "yes" } else { "no" })
    $running = Get-Running
    $parts += "running: " + $(if ($running.Count) { $running -join " " } else { "nothing" })
    Write-Log ("STATE {0}: {1}" -f $dir, ($parts -join " | "))
}

function Invoke-Check {
    Write-Log ("CHECK computer {0}, Windows {1}" -f $env:COMPUTERNAME, [Environment]::OSVersion.Version)
    $rt = Get-GlobalRuntime
    Write-Log ("global .NET 10 Desktop: " + $(if ($rt.Count) { $rt -join ", " } else { "NONE" }))
    foreach ($f in "EDISON.exe", "EDISONMAIN.exe", "EDISONMAIN2.exe", "EDControls.dll", "EDISONMAIN2API.exe",
                   "EDISONRUNTIME.zip", "TemplateDesigner.exe", "VERZE.INF", "EDISONini.xml") {
        if (-not (Test-Path (Join-Path $Edison $f))) { Write-Log "MISSING $f" }
    }
    $verze = Join-Path $Edison "VERZE.INF"
    if (Test-Path $verze) { Write-Log ("VERZE.INF: " + ((Get-Content $verze) -join " ; ")) }
    try {
        $c = ([xml](Get-Content (Join-Path $Edison "EDISONini.xml"))).edison.connection
        $ok = Test-NetConnection -ComputerName $c.server -Port ([int]$c.restport) -InformationLevel Quiet -WarningAction SilentlyContinue
        Write-Log ("server {0}:{1} reachable: {2}" -f $c.server, $c.restport, $ok)
    } catch { Write-Log ("connection not checked: " + $_.Exception.Message) }
    Write-State $Edison
}

# Logs only changes, so a dotnet\ left from an earlier run does not look like a fresh unpack.
function Watch-Start([string]$dir, [int]$starts = 1, [switch]$KillOnStaging) {
    Write-State $dir
    $paths = [ordered]@{ "dotnet.new" = (Join-Path $dir "dotnet.new"); "dotnet\" = (Join-Path $dir "dotnet")
                         "marker" = (Join-Path $dir "dotnet\EDISONRUNTIME.version") }
    $present = @{}
    foreach ($k in $paths.Keys) { $present[$k] = Test-Path $paths[$k] }
    $t0 = Get-Date
    for ($i = 0; $i -lt $starts; $i++) {
        Start-Process -FilePath (Join-Path $dir "EDISON.exe") -WorkingDirectory $dir
        Write-Log "START EDISON.exe no. $($i + 1) from $dir"
        if ($i + 1 -lt $starts) { Start-Sleep -Milliseconds 300 }
    }
    $seen = @{}
    while (((Get-Date) - $t0).TotalSeconds -lt $Seconds) {
        $s = ((Get-Date) - $t0).TotalSeconds
        foreach ($k in $paths.Keys) {
            $now = Test-Path $paths[$k]
            if ($now -ne $present[$k]) {
                $present[$k] = $now
                Write-Log ("+{0:N1} s  {1} {2}" -f $s, $k, $(if ($now) { "appeared" } else { "removed" }))
            }
        }
        if ($KillOnStaging -and $present["dotnet.new"]) {
            Get-Process -Name "EDISON" -ErrorAction SilentlyContinue | Stop-Process -Force   # unpacking takes ~0.8 s
            Write-Log ("+{0:N1} s  EDISON.exe killed while unpacking" -f $s)
            break
        }
        foreach ($p in @(Get-Process -Name $Watched -ErrorAction SilentlyContinue)) {
            $key = "{0}/{1}" -f $p.ProcessName, $p.Id
            if (-not $seen.ContainsKey($key)) { $seen[$key] = $true; Write-Log ("+{0:N1} s  started {1} (pid {2})" -f $s, $p.ProcessName, $p.Id) }
        }
        $loaderGone = -not (Get-Process -Name "EDISON" -ErrorAction SilentlyContinue)
        $shellUp = [bool](Get-Process -Name "EDISONMAIN", "EDISONMAIN2" -ErrorAction SilentlyContinue)
        if ($loaderGone -and ($shellUp -or $s -gt 20)) { break }
        Start-Sleep -Milliseconds $(if ($KillOnStaging) { 20 } else { 250 })
    }
    Write-Log ("watch ended after {0:N1} s" -f ((Get-Date) - $t0).TotalSeconds)
    Write-State $dir
}

# Station facts that change during an update; compared every 500 ms by Follow-Update.
function Get-UpdateSnapshot([string]$dir) {
    $dotnet = Join-Path $dir "dotnet"
    $marker = Join-Path $dotnet "EDISONRUNTIME.version"
    $zip = Get-Item (Join-Path $dir "EDISONRUNTIME.zip") -ErrorAction SilentlyContinue
    [ordered]@{
        "VERZE.INF" = ((Get-Content (Join-Path $dir "VERZE.INF") -ErrorAction SilentlyContinue) -join " ")
        "runtime zip" = $(if ($zip) { "{0} B {1:HH:mm:ss}" -f $zip.Length, $zip.LastWriteTime } else { "none" })
        "dotnet.new" = [string](Test-Path (Join-Path $dir "dotnet.new"))
        "dotnet\ fxr" = (@(Get-ChildItem (Join-Path $dotnet "host\fxr") -Directory -ErrorAction SilentlyContinue | ForEach-Object Name) -join ",")
        "dotnet\ marker" = $(if (Test-Path $marker) { (Get-Content $marker -Raw).Trim() } else { "" })
    }
}

function Follow-Update([string]$dir) {
    $last = Get-UpdateSnapshot $dir
    foreach ($k in $last.Keys) { Write-Log ("  {0}: {1}" -f $k, $last[$k]) }
    $running = @{}
    foreach ($p in @(Get-Process -Name $Watched -ErrorAction SilentlyContinue)) { $running["$($p.ProcessName)/$($p.Id)"] = $true }
    $t0 = Get-Date
    Start-Process -FilePath (Join-Path $dir "EDISON.exe") -WorkingDirectory $dir
    Write-Log "START EDISON.exe from $dir, following for $Seconds s (Ctrl+C ends it)"
    while (((Get-Date) - $t0).TotalSeconds -lt $Seconds) {
        $s = ((Get-Date) - $t0).TotalSeconds
        $now = @{}
        foreach ($p in @(Get-Process -Name $Watched -ErrorAction SilentlyContinue)) {
            $key = "$($p.ProcessName)/$($p.Id)"; $now[$key] = $true
            if (-not $running.ContainsKey($key)) { Write-Log ("+{0:N1} s  started {1}" -f $s, $key) }
        }
        foreach ($key in @($running.Keys)) { if (-not $now.ContainsKey($key)) { Write-Log ("+{0:N1} s  ended {1}" -f $s, $key) } }
        $running = $now
        $snap = Get-UpdateSnapshot $dir
        foreach ($k in $snap.Keys) {
            if ($snap[$k] -ne $last[$k]) { Write-Log ("+{0:N1} s  {1}: {2} -> {3}" -f $s, $k, $last[$k], $snap[$k]) }
        }
        $last = $snap
        Start-Sleep -Milliseconds 500
    }
    Write-Log ("follow ended after {0:N1} s" -f ((Get-Date) - $t0).TotalSeconds)
    Write-State $dir
}

if ($Note) { Write-Log "NOTE: $Note" }
switch ($Action) {
    "Setup" {
        if (-not (Test-Path (Join-Path $Source "EDISON.exe"))) { Write-Log "No installation in $Source, run prepare-host.ps1 on the host first"; break }
        # /E, not /MIR: dotnet\ from earlier starts stays (scenarios S3, S4)
        robocopy $Source $Edison /E /R:1 /W:1 /NFL /NDL /NP /NJH /NJS | Out-Null
        if ($LASTEXITCODE -ge 8) { Write-Log "SETUP copy to $Edison FAILED (robocopy $LASTEXITCODE), close the EDISON programs and repeat"; break }
        Write-Log "SETUP installation copied to $Edison"
        Invoke-Check
    }
    "Check" { Invoke-Check }
    "Start" { Watch-Start $Edison }
    "Twice" { Watch-Start $Edison -starts 2 }
    "Kill" { Watch-Start $Edison -KillOnStaging }
    "ReadOnly" {
        $ro = "C:\EDISONRO"
        if (-not (Test-Path (Join-Path $ro "EDISON.exe"))) {
            robocopy $Source $ro /E /R:1 /W:1 /NFL /NDL /NP /NJH /NJS | Out-Null
            if ($LASTEXITCODE -ge 8) { Write-Log "READONLY copy to $ro FAILED (robocopy $LASTEXITCODE)"; break }
            Write-Log "READONLY copy $ro done"
        }
        icacls $ro /remove:d $env:USERNAME /T /C /Q | Out-Null
        # only creating files and folders (WD, AD): exactly what EDLOADER needs to unpack
        icacls $ro /deny ("{0}:(OI)(CI)(WD,AD)" -f $env:USERNAME) /C /Q | Out-Null
        Write-Log "READONLY $ro without the right to create files for $env:USERNAME"
        Watch-Start $ro
    }
    "Td" {
        Start-Process -FilePath (Join-Path $Edison "TemplateDesigner.exe") -WorkingDirectory $Edison
        Write-Log "START TemplateDesigner.exe directly"
        Start-Sleep -Seconds 5
        Write-State $Edison
    }
    "Shell" {
        Start-Process -FilePath (Join-Path $Edison "EDISONMAIN2.exe") -WorkingDirectory $Edison
        Write-Log "START EDISONMAIN2.exe directly"
        Start-Sleep -Seconds 8
        Write-State $Edison
    }
    "Reset" {
        foreach ($d in "dotnet", "dotnet.new", "dotnet.lock") {
            $p = Join-Path $Edison $d
            if (Test-Path $p) { Remove-Item $p -Recurse -Force; Write-Log "RESET removed $p" }
        }
        Write-State $Edison
    }
    "State" { Write-State $Edison }
    "Ids" {
        $ipv4 = @([Net.Dns]::GetHostAddresses([Net.Dns]::GetHostName()) |
                  Where-Object { $_.AddressFamily -eq [Net.Sockets.AddressFamily]::InterNetwork } | ForEach-Object { "$_" })
        Write-Log ("IDS computer name {0} | DNS host name {1} | IPv4 {2}" -f $env:COMPUTERNAME, [Net.Dns]::GetHostName(), ($ipv4 -join ", "))
        Write-Log "    localPath entry: the DNS host name (the new shell compares Dns.GetHostName) or the first IPv4"
    }
    "SetVersion" {
        if ($Value -eq "") { throw "SetVersion needs -Value, e.g. -Value 260929.4" }
        [IO.File]::WriteAllText((Join-Path $Edison "VERZE.INF"), "LAST=$Value`r`n", [Text.Encoding]::ASCII)
        Write-Log "SETVERSION C:\EDISON\VERZE.INF = LAST=$Value"
    }
    "Update" { Follow-Update $Edison }
}
