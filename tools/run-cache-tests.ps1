# Run Cache %UnitTest tests via MLRest -> ALVA.EDTesting.TestRunner.RunPackage.
# Exit 0 = all passed; exit 1 = at least one test failed or runner crashed.
# Called from pre-push hook or interactively.
#
# Usage:
#   pwsh -File tools/run-cache-tests.ps1
#   pwsh -File tools/run-cache-tests.ps1 -Package "ALVA.EDISONMAIN2.Tests"
#
# ASCII only -- Windows PowerShell 5.1 reads UTF-8-no-BOM as CP-1252.

[CmdletBinding()]
param(
    # Comma-separated list of Cache package prefixes where %UnitTest.TestCase subclasses live.
    # Default = all current test packages. Add new ones as more test projects appear.
    [string]$Package = "ALVA.EDISONMAIN2.Tests",
    [string]$Server = "192.168.16.1",
    [int]$Port = 57770,
    [string]$Idfa = "CB_DP",
    [string]$DataNamespace = "MLINE",
    [string]$MLRestDll = "C:\Edison\MLRest.dll"
)

$ErrorActionPreference = "Stop"

# LoginName/UserID: $env:CACHE_USER override, otherwise Windows username.
$user = if ($env:CACHE_USER) { $env:CACHE_USER } else { $env:USERNAME }

if (-not (Test-Path $MLRestDll)) {
    Write-Error "MLRest.dll not found at $MLRestDll. Build EDISON.sln or check path."
    exit 1
}

Add-Type -Path $MLRestDll

$ml = New-Object ALWA.SYS.MLRest($Server, $Port, $false)
$ml.Idfa = $Idfa
$ml.DataNamespace = $DataNamespace
$ml.LoginName = $user
$ml.Mlp = $user        # Cache-side userID is sourced from Mlp, not UserID
$ml.UserID = $user
$ml.FullName = $user

$totalPassed = 0
$totalFailed = 0
$allFailures = New-Object System.Collections.Generic.List[object]

foreach ($pkg in ($Package -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })) {
    Write-Host "==> Running Cache tests in package: $pkg" -ForegroundColor Cyan

    $cvarIn = New-Object ALWA.SYS.CVar
    $cvarIn["package"] = New-Object ALWA.SYS.CVar -ArgumentList @([object]$pkg)

    try {
        $result = $ml.Query("ALVA.EDTesting.TestRunner", "RunPackage", $cvarIn)
    } catch {
        Write-Host "  [ERROR] Runner threw: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }

    if ($ml.RestStatus.State -ne [ALWA.SYS.MLRStatus+EState]::Ok) {
        Write-Host "  [ERROR] RestStatus: $($ml.RestStatus.Message)" -ForegroundColor Red
        exit 1
    }

    $passed = [int]$result["passed"].Value
    $failed = [int]$result["failed"].Value
    $total  = [int]$result["total"].Value
    $totalPassed += $passed
    $totalFailed += $failed

    $color = if ($failed -eq 0) { 'Green' } else { 'Red' }
    Write-Host "  $passed/$total passed" -ForegroundColor $color

    if ($failed -gt 0) {
        $failures = $result["failures"]
        if ($failures) {
            for ($i = 1; $i -le $failed; $i++) {
                $f = $failures[[string]$i]
                if ($null -eq $f) { continue }
                $cls = $f["class"].Value
                $mth = $f["method"].Value
                $msg = $f["message"].Value
                $allFailures.Add([PSCustomObject]@{ Class = $cls; Method = $mth; Message = $msg })
            }
        }
    }
}

Write-Host ""
Write-Host "-----------------------------------------"
$summaryColor = if ($totalFailed -eq 0) { 'Green' } else { 'Red' }
Write-Host "  Total: $totalPassed passed, $totalFailed failed" -ForegroundColor $summaryColor

if ($totalFailed -gt 0) {
    Write-Host ""
    Write-Host "Failures:" -ForegroundColor Red
    foreach ($f in $allFailures) {
        Write-Host ("  [FAIL] {0}.{1}" -f $f.Class, $f.Method) -ForegroundColor Red
        if ($f.Message) { Write-Host ("    {0}" -f $f.Message) -ForegroundColor DarkGray }
    }
    exit 1
}

exit 0
