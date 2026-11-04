# Run Cache %UnitTest tests via MLRest -> ALVA.EDTesting.TestRunner.RunPackage.
# Exit 0 = all passed; exit 1 = at least one test failed or runner crashed.
# Called from pre-push hook or interactively.
#
# Packages run IN PARALLEL (one runspace + own MLRest instance each; server side
# every request is its own Cache process with private ^||ML). MHD2 packages share one
# lane and run one after another: their tests write the same CB_DP data inside
# transactions, and Cache does not isolate uncommitted data between processes.
# Wall time = slowest package instead of the sum - TemplateDesigner is the expensive
# one (~45 s, gold digests). -Sequential is a troubleshooting fallback.
#
# Usage:
#   pwsh -File tools/run-cache-tests.ps1
#   pwsh -File tools/run-cache-tests.ps1 -Package "ALVA.EDISONMAIN2.Tests"
#   pwsh -File tools/run-cache-tests.ps1 -Sequential
#
# ASCII only -- Windows PowerShell 5.1 reads UTF-8-no-BOM as CP-1252.

[CmdletBinding()]
param(
    # Comma-separated list of Cache package prefixes where %UnitTest.TestCase subclasses live.
    # Default = all current test packages. Add new ones as more test projects appear.
    [string]$Package = "ALVA.EDISONMAIN2.Tests,ALVA.EDControls.Tests,ALVA.TemplateDesigner.Tests,ALVA.EDTesting.Tests",
    [string]$Server = "192.168.16.1",
    [int]$Port = 57770,
    [string]$Idfa = "CB_DP",
    [string]$DataNamespace = "MLINE",
    [string]$MLRestDll = "C:\Edison\MLRest.dll",
    [switch]$Sequential
)

$ErrorActionPreference = "Stop"

# LoginName/UserID: $env:CACHE_USER override, otherwise Windows username.
$user = if ($env:CACHE_USER) { $env:CACHE_USER } else { $env:USERNAME }

if (-not (Test-Path $MLRestDll)) {
    Write-Error "MLRest.dll not found at $MLRestDll. Build EDISON.sln or check path."
    exit 1
}

$pkgs = @($Package -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })

# One package run = one RunPackage request on a private MLRest instance.
# Returns a plain result object (no CVar leaves the runspace).
$runOne = {
    param($pkg, $Server, $Port, $Idfa, $DataNamespace, $user, $MLRestDll)

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        Add-Type -Path $MLRestDll

        $ml = New-Object ALWA.SYS.MLRest($Server, $Port, $false)
        $ml.Idfa = $Idfa
        $ml.DataNamespace = $DataNamespace
        $ml.LoginName = $user
        $ml.Mlp = $user        # Cache-side userID is sourced from Mlp, not UserID
        $ml.UserID = $user
        $ml.FullName = $user

        $cvarIn = New-Object ALWA.SYS.CVar
        $cvarIn["package"] = New-Object ALWA.SYS.CVar -ArgumentList @([object]$pkg)

        $result = $ml.Query("ALVA.EDTesting.TestRunner", "RunPackage", $cvarIn)

        if ($ml.RestStatus.State -ne [ALWA.SYS.MLRStatus+EState]::Ok) {
            return [PSCustomObject]@{
                Package = $pkg; Ok = $false; Seconds = $sw.Elapsed.TotalSeconds
                Error = "RestStatus: $($ml.RestStatus.Message)"
            }
        }

        # failures / skips: OUT(node, i, "class"/"method"/"message"), i = 1..count
        $readEntries = {
            param($node, $count)
            $entries = @()
            if ($count -gt 0 -and $result[$node]) {
                for ($i = 1; $i -le $count; $i++) {
                    $e = $result[$node][[string]$i]
                    if ($null -eq $e) { continue }
                    $entries += [PSCustomObject]@{
                        Class   = $e["class"].Value
                        Method  = $e["method"].Value
                        Message = $e["message"].Value
                    }
                }
            }
            , $entries
        }

        $failed = [int]$result["failed"].Value
        # Skipped ($$$AssertSkipped) does not fail the run; an older runner without the key reports 0.
        $skipped = [int]$result["skipped"].Value

        return [PSCustomObject]@{
            Package = $pkg; Ok = $true; Error = $null; Seconds = $sw.Elapsed.TotalSeconds
            Passed = [int]$result["passed"].Value
            Failed = $failed
            Skipped = $skipped
            Total  = [int]$result["total"].Value
            Failures = & $readEntries "failures" $failed
            Skips = & $readEntries "skips" $skipped
        }
    } catch {
        return [PSCustomObject]@{
            Package = $pkg; Ok = $false; Seconds = $sw.Elapsed.TotalSeconds
            Error = "Runner threw: $($_.Exception.Message)"
        }
    }
}

$results = @()
if ($Sequential -or $pkgs.Count -le 1) {
    foreach ($pkg in $pkgs) {
        Write-Host "==> Running Cache tests in package: $pkg" -ForegroundColor Cyan
        $results += & $runOne $pkg $Server $Port $Idfa $DataNamespace $user $MLRestDll
    }
} else {
    Write-Host ("==> Running {0} Cache test packages in parallel: {1}" -f $pkgs.Count, ($pkgs -join ', ')) -ForegroundColor Cyan
    $lanes = [ordered]@{}
    foreach ($pkg in $pkgs) {
        $lane = if ($pkg -match '^ALVA\.MHD2\.') { 'MHD2' } else { $pkg }
        if (-not $lanes.Contains($lane)) { $lanes[$lane] = @() }
        $lanes[$lane] += $pkg
    }
    $runLane = {
        param($runOneSrc, $lanePkgs, $Server, $Port, $Idfa, $DataNamespace, $user, $MLRestDll)
        $one = [scriptblock]::Create($runOneSrc)
        foreach ($p in $lanePkgs) { & $one $p $Server $Port $Idfa $DataNamespace $user $MLRestDll }
    }
    $pool = [runspacefactory]::CreateRunspacePool(1, $lanes.Count)
    $pool.Open()
    try {
        $jobs = foreach ($lane in $lanes.Keys) {
            $ps = [powershell]::Create()
            $ps.RunspacePool = $pool
            [void]$ps.AddScript($runLane.ToString()).
                AddArgument($runOne.ToString()).AddArgument([string[]]$lanes[$lane]).AddArgument($Server).AddArgument($Port).
                AddArgument($Idfa).AddArgument($DataNamespace).AddArgument($user).AddArgument($MLRestDll)
            [PSCustomObject]@{ Pkg = ($lanes[$lane] -join ', '); PS = $ps; Handle = $ps.BeginInvoke() }
        }
        foreach ($j in $jobs) {
            $out = $j.PS.EndInvoke($j.Handle)
            if ($out.Count -gt 0) {
                $results += @($out)
            } else {
                $err = ($j.PS.Streams.Error | Select-Object -First 1 | ForEach-Object { $_.ToString() })
                $results += [PSCustomObject]@{ Package = $j.Pkg; Ok = $false; Seconds = 0; Error = "no result ($err)" }
            }
            $j.PS.Dispose()
        }
    } finally {
        $pool.Close()
        $pool.Dispose()
    }
}

# Report in the original package order, regardless of finish order.
$totalPassed = 0
$totalFailed = 0
$totalSkipped = 0
$infraError = $false
$allFailures = New-Object System.Collections.Generic.List[object]
$allSkips = New-Object System.Collections.Generic.List[object]

foreach ($pkg in $pkgs) {
    $r = $results | Where-Object { $_.Package -eq $pkg } | Select-Object -First 1
    Write-Host "==> ${pkg}:" -ForegroundColor Cyan
    if ($null -eq $r) {
        Write-Host "  [ERROR] no result returned" -ForegroundColor Red
        $infraError = $true
        continue
    }
    if (-not $r.Ok) {
        Write-Host "  [ERROR] $($r.Error)" -ForegroundColor Red
        $infraError = $true
        continue
    }
    $totalPassed += $r.Passed
    $totalFailed += $r.Failed
    $totalSkipped += $r.Skipped
    $color = if ($r.Failed -eq 0) { 'Green' } else { 'Red' }
    $skipNote = if ($r.Skipped -gt 0) { ", {0} skipped" -f $r.Skipped } else { "" }
    Write-Host ("  {0}/{1} passed{2} ({3:N1} s)" -f $r.Passed, $r.Total, $skipNote, $r.Seconds) -ForegroundColor $color
    foreach ($f in $r.Failures) { $allFailures.Add($f) }
    foreach ($s in $r.Skips) { $allSkips.Add($s) }
}

Write-Host ""
Write-Host "-----------------------------------------"
$summaryColor = if (($totalFailed -eq 0) -and (-not $infraError)) { 'Green' } else { 'Red' }
Write-Host "  Total: $totalPassed passed, $totalFailed failed, $totalSkipped skipped" -ForegroundColor $summaryColor

# Before Failures: pre-push extracts the failure report from "Failures:" to the end of the log.
if ($totalSkipped -gt 0) {
    Write-Host ""
    Write-Host "Skipped:" -ForegroundColor DarkYellow
    foreach ($s in $allSkips) {
        Write-Host ("  [SKIP] {0}.{1}" -f $s.Class, $s.Method) -ForegroundColor DarkYellow
        if ($s.Message) { Write-Host ("    {0}" -f $s.Message) -ForegroundColor DarkGray }
    }
}

if ($totalFailed -gt 0) {
    Write-Host ""
    Write-Host "Failures:" -ForegroundColor Red
    foreach ($f in $allFailures) {
        Write-Host ("  [FAIL] {0}.{1}" -f $f.Class, $f.Method) -ForegroundColor Red
        if ($f.Message) { Write-Host ("    {0}" -f $f.Message) -ForegroundColor DarkGray }
    }
}

if (($totalFailed -gt 0) -or $infraError) { exit 1 }
exit 0
