<#
  Vzorkuje strom procesů shellu (EDISONMAIN2 + jeho WebView2 potomci) a vypisuje CPU%
  a pracovní sadu. Pusť, klikej v shellu, sleduj špičku:

    pwsh -File tools/measure-shell-processes.ps1 -Seconds 60

  Proč strom a ne jen appka: WebView2 běží v samostatných procesech (browser, renderer,
  GPU, utility), takže Task Manager u EDISONMAIN2.exe ukáže jen zlomek skutečné spotřeby.
  Cizí msedgewebview2 (Teams, prohlížeč) se odfiltrují přes ParentProcessId.
#>
param([int]$Seconds = 60, [double]$IntervalSec = 1.0, [string]$Process = 'EDISONMAIN2')

$cpuCount = [Environment]::ProcessorCount
$prev = @{}
$sw = [Diagnostics.Stopwatch]::StartNew()

function Get-Tree {
    $root = Get-Process -Name $Process -ErrorAction SilentlyContinue
    if (-not $root) { return @() }
    $rootIds = $root.Id
    # WebView2 potomci běží jako msedgewebview2.exe; svážeme je přes ParentProcessId.
    $all = Get-CimInstance Win32_Process -Filter "Name='msedgewebview2.exe'" -ErrorAction SilentlyContinue
    $wanted = New-Object System.Collections.Generic.HashSet[int]
    $rootIds | ForEach-Object { [void]$wanted.Add($_) }
    # dvě úrovně stačí: browser proces je dítě appky, renderery jsou děti browseru
    for ($i = 0; $i -lt 2; $i++) {
        foreach ($p in $all) { if ($wanted.Contains([int]$p.ParentProcessId)) { [void]$wanted.Add([int]$p.ProcessId) } }
    }
    Get-Process -Id ([int[]]$wanted) -ErrorAction SilentlyContinue
}

Write-Host ("{0,-9} {1,6} {2,8} {3,10} {4,10}" -f 'čas', 'proc', 'CPU %', 'RAM MB', 'peak MB')
while ($sw.Elapsed.TotalSeconds -lt $Seconds) {
    $procs = Get-Tree
    if (-not $procs) { Start-Sleep -Seconds $IntervalSec; continue }
    $cpu = 0.0; $ram = 0.0; $peak = 0.0
    foreach ($p in $procs) {
        $t = $p.TotalProcessorTime.TotalSeconds
        if ($prev.ContainsKey($p.Id)) { $cpu += ($t - $prev[$p.Id]) }
        $prev[$p.Id] = $t
        $ram  += $p.WorkingSet64 / 1MB
        $peak += $p.PeakWorkingSet64 / 1MB
    }
    $cpuPct = [math]::Round(100 * $cpu / ($IntervalSec * $cpuCount), 1)
    Write-Host ("{0,-9} {1,6} {2,8} {3,10} {4,10}" -f `
        (Get-Date -Format 'HH:mm:ss'), $procs.Count, $cpuPct, [math]::Round($ram), [math]::Round($peak))
    Start-Sleep -Seconds $IntervalSec
}
