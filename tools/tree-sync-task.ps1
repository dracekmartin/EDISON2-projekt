# Uloha tree-sync: drzi strom modulu (Tree.cls) na DEV a v gitu srovnany s legacy XML v repu EDISON.
#
# Bezi z Planovace uloh na vyvojarskem stroji, ne na GitHubu: runner GitHubu na DEV (VPN)
# nedosahne a minuty dochazi. Beh bez zmeny jen porovna fingerprint zdroju a skonci. Pri zmene
# pusti sync-tree.ps1 (regenerace, vynuceny deploy na DEV, Cache testy), commitne Tree.cls
# s fingerprintem jako autor "tree-sync" a pushne do main. Pre-push pusti zbytek testu.
#
# Na REL se zmena sama nedostane, jde tam az s vydanim EDISONMAIN2 (import ze ziveho DEV).
# Beh, ktery strom zmenil, proto zapise pripominku. tree-drift.sh ji ukazuje, dokud ji -AckRel nesmaze.
#
#   powershell -File tools/tree-sync-task.ps1 -Install     klony + registrace v Planovaci uloh
#   powershell -File tools/tree-sync-task.ps1 -Uninstall   zrusi registraci a stav, klony a pripominka REL zustanou
#   powershell -File tools/tree-sync-task.ps1 -AckRel      EDISONMAIN2 je vydany na REL
#   powershell -File tools/tree-sync-task.ps1              jeden beh (spousti ho Planovac)
#
# Instalovat jen na jeden stroj: dve ulohy by si pri soubehu mohly nechat na DEV starsi strom, nez je v main.
# Klony, stav a log jsou v %LOCALAPPDATA%\m-line\tree-sync, $env:TREE_SYNC_HOME je presmeruje (test).
# ASCII-only zamerne: WinPS 5.1 cte UTF-8 bez BOM jako CP-1252.

[CmdletBinding(DefaultParameterSetName = 'Run')]
param(
    [Parameter(ParameterSetName = 'Install')] [switch]$Install,
    [Parameter(ParameterSetName = 'Uninstall')] [switch]$Uninstall,
    [Parameter(ParameterSetName = 'AckRel')] [switch]$AckRel
)

$ErrorActionPreference = 'Stop'

$work = if ($env:TREE_SYNC_HOME) { $env:TREE_SYNC_HOME } else { Join-Path $env:LOCALAPPDATA 'm-line\tree-sync' }
$legacy = Join-Path $work 'EDISON'
$repo = Join-Path $work 'EDISON2'
$statusFile = Join-Path $work 'status.txt'
$relFile = Join-Path $work 'rel-pending.txt'
$logFile = Join-Path $work 'last-run.log'
$taskName = 'EDISON2 tree-sync'
$treeRel = 'EDISONMAIN2/CACHE/ALVA/EDISONMAIN2/API/Tree.cls'
$fpRel = 'tools/tree-sources.sha256'
$legacyUrl = 'https://github.com/mlinecz/EDISON.git'
$repoUrl = 'https://github.com/mlinecz/EDISON2.git'
$utf8 = New-Object System.Text.UTF8Encoding($false)

# Zdroje generatoru a legacy soubory, ktere v pre-push cte TreeGeneratorTests a LegacyDerivedAccessTests.
$legacyFiles = @(
    'EDCORE/Resources/edisonnavbar.xml',
    'EDCORE/Resources/edisontree.xml',
    'EDCORE/EDFormInterfaceLic.vb',
    'EDLIBRARY/clsConversion.vb',
    'EDUsers/EDUsers/EDUsers.vb'
)

function Step($msg) { Write-Host "==> $msg" }

# Git pise prubeh na stderr; pri Stop by to skript shodilo, o vysledku rozhoduje exit kod.
function Invoke-Native {
    $exe = $args[0]
    $rest = @($args | Select-Object -Skip 1)
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { & $exe @rest } finally { $ErrorActionPreference = $prev }
    if ($LASTEXITCODE -ne 0) { throw "$exe $($rest -join ' ') skoncil s kodem $LASTEXITCODE" }
}

function Read-Status {
    $s = @{}
    if (Test-Path -LiteralPath $statusFile) {
        foreach ($l in [IO.File]::ReadAllLines($statusFile, $utf8)) { $k, $v = $l -split '=', 2; $s[$k] = $v }
    }
    return $s
}

function Write-Status([string]$result, [string]$message) {
    $now = Get-Date -Format 's'
    $lastOk = if ($result -eq 'error') { (Read-Status)['last_ok'] } else { $now }
    $lines = @("last_run=$now", "result=$result", "last_ok=$lastOk", "message=$($message -replace '\s+', ' ')")
    [IO.File]::WriteAllLines($statusFile, [string[]]$lines, $utf8)
}

# Deploy hook bere prihlaseni do Cache z prostredi. Claude Code ho plni z ~/.claude/settings.json, Planovac ne.
function Import-CacheEnv {
    $settings = Join-Path $env:USERPROFILE '.claude\settings.json'
    if (-not (Test-Path -LiteralPath $settings)) { return }
    $cfg = [IO.File]::ReadAllText($settings, $utf8) | ConvertFrom-Json
    foreach ($name in 'CACHE_SERVER', 'CACHE_NAMESPACE', 'CACHE_USER', 'CACHE_PASSWORD') {
        $value = $cfg.env.$name
        if ($value -and -not [Environment]::GetEnvironmentVariable($name)) { Set-Item -Path "env:$name" -Value $value }
    }
}

function Get-SourceFingerprint([string]$path) {
    # Bez CR jako generator a tree-drift.sh. Latin-1 prevadi bajty 1:1, hash je tedy nad puvodnimi bajty.
    $latin1 = [Text.Encoding]::GetEncoding(28591)
    $bytes = $latin1.GetBytes($latin1.GetString([IO.File]::ReadAllBytes($path)).Replace("`r", ''))
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return -join ($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString('x2') }) } finally { $sha.Dispose() }
}

function Test-SourcesInSync {
    foreach ($line in [IO.File]::ReadAllLines((Join-Path $repo $fpRel))) {
        if ($line -eq '' -or $line.StartsWith('#')) { continue }
        $hash, $rel = $line -split '  ', 2
        $src = Join-Path $legacy $rel
        if (-not (Test-Path -LiteralPath $src)) { throw "legacy zdroj $rel v klonu chybi: doplnit do `$legacyFiles a spustit -Install" }
        if ((Get-SourceFingerprint $src) -ne $hash) { return $false }
    }
    return $true
}

function Invoke-SyncTree([switch]$Test, [switch]$NoDeploy) {
    $a = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $repo 'tools\sync-tree.ps1'), '-RejectStubs')
    if ($Test) { $a += '-Test' }
    if ($NoDeploy) { $a += '-NoDeploy' }
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { $out = @(& powershell.exe @a); $code = $LASTEXITCODE } finally { $ErrorActionPreference = $prev }
    $out | ForEach-Object { Write-Host $_ }
    if ($code -ne 0) {
        # Do stavu jde duvod, ktery sync-tree vypsal, ne cely prikazovy radek.
        $reason = $out | Where-Object { $_ -like 'FAILED:*' } | Select-Object -Last 1
        if ($reason) { throw "sync-tree.ps1: $($reason.Substring(7).Trim())" }
        throw "sync-tree.ps1 skoncil s kodem $code, detail v logu behu"
    }
}

function Get-TreeSummary([string]$oldTree) {
    # python3 je na Windows casto jen alias do Storu, ktery nic nespusti.
    # [string[]]: jednoprvkove pole by se rozbalilo na retezec a splat by ho rozlozil na znaky.
    [string[]]$py = if (Get-Command py -ErrorAction SilentlyContinue) { 'py', '-3' } else { 'python' }
    $script = Join-Path $repo 'tools\tree-diff-summary.py'
    $prevEnc = [Console]::OutputEncoding
    # Skript pise UTF-8. Bez toho by ho PowerShell cetl v konzolove strance a diakritika by se rozsypala.
    try { [Console]::OutputEncoding = $utf8 } catch { Write-Host '    (kodovani konzole nejde zmenit, diakritika v souhrnu muze byt rozsypana)' }
    try { return ((Invoke-Native @py $script --oneline $oldTree (Join-Path $repo $treeRel)) -join ' ').Trim() }
    finally { try { [Console]::OutputEncoding = $prevEnc } catch { } }
}

function Push-Main {
    for ($attempt = 1; ; $attempt++) {
        $prev = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try { & git -C $repo push --quiet origin HEAD:main } finally { $ErrorActionPreference = $prev }
        if ($LASTEXITCODE -eq 0) { return }

        Invoke-Native git -C $repo fetch --quiet origin
        & git -C $repo merge-base --is-ancestor origin/main HEAD
        if ($LASTEXITCODE -eq 0) { throw "push neprosel pre-push testy, viz $repo\.husky\last-test-run.log" }
        if ($attempt -ge 2) { throw 'push odmitnut i po rebase na origin/main' }

        Step 'Mezitim pribyl commit v main - rebase'
        $ErrorActionPreference = 'Continue'
        try { & git -C $repo rebase --quiet origin/main } finally { $ErrorActionPreference = $prev }
        if ($LASTEXITCODE -ne 0) {
            & git -C $repo rebase --abort
            throw 'rebase na origin/main narazil na konflikt (rucni uprava Tree.cls?)'
        }
        # Na DEV je strom z puvodni baze. Kdyby novy commit menil Tree.cls, DEV by o nej prisel.
        # Nasazuje se az po kontrole, ze regenerace sedi s main; jinak by na DEV skoncilo neco mimo git.
        Invoke-SyncTree -NoDeploy
        if (Invoke-Native git -C $repo status --porcelain -- $treeRel $fpRel) {
            throw 'po rebase se Tree.cls regeneruje jinak, nez je v main (rucni zasah do generovaneho bloku?)'
        }
        Invoke-SyncTree
    }
}

function Invoke-Run {
    foreach ($dir in $legacy, $repo) {
        if (-not (Test-Path -LiteralPath (Join-Path $dir '.git'))) { throw "klon $dir chybi, spust tools/tree-sync-task.ps1 -Install" }
    }
    if ((& git -C $repo config core.hooksPath) -ne '.husky') { throw "v $repo neni pre-push hook, push by obesel testy: spust -Install" }
    Import-CacheEnv
    # Pod conhost --headless nikdo vyzvu k prihlaseni nevidi; git by na ni visel az do limitu ulohy.
    $env:GIT_TERMINAL_PROMPT = '0'
    $env:GCM_INTERACTIVE = 'never'

    Step 'Fetch legacy EDISON a EDISON2'
    Invoke-Native git -C $legacy fetch --quiet --depth 1 origin
    Invoke-Native git -C $legacy reset --quiet --hard '@{u}'
    foreach ($state in 'rebase-merge', 'rebase-apply') {
        if (Test-Path -LiteralPath (Join-Path $repo ".git\$state")) { & git -C $repo rebase --abort }
    }
    # Klon patri jen uloze: reset zahodi i commit z behu, kteremu neprosel push. Dalsi beh ho vytvori znovu.
    Invoke-Native git -C $repo fetch --quiet origin
    Invoke-Native git -C $repo reset --quiet --hard
    Invoke-Native git -C $repo checkout --quiet -B main origin/main

    if (Test-SourcesInSync) {
        Step 'Strom je srovnany s legacy XML.'
        Write-Status 'ok' 'zdroje beze zmeny'
        return
    }

    Step 'Legacy zdroje stromu se zmenily'
    $oldTree = Join-Path $work 'Tree.old.cls'
    Copy-Item -LiteralPath (Join-Path $repo $treeRel) -Destination $oldTree -Force
    $env:EDISON_LEGACY_ROOT = $legacy
    Invoke-SyncTree -Test

    $treeChanged = [bool](Invoke-Native git -C $repo status --porcelain -- $treeRel)
    $summary = Get-TreeSummary $oldTree
    $msgFile = Join-Path $work 'commit-msg.txt'
    # Zprava pres soubor: argument prikazove radky by diakritiku v nazvech uzlu nemusel prenest.
    [IO.File]::WriteAllText($msgFile, "EDISON2, EDISONMAIN2 - regenerate module tree from legacy XML: $summary`n", $utf8)
    Invoke-Native git -C $repo -c user.name=tree-sync commit --quiet -F $msgFile --only -- $treeRel $fpRel

    Step 'Push do main'
    Push-Main
    $sha = (Invoke-Native git -C $repo rev-parse --short HEAD) -join ''
    if ($treeChanged) {
        [IO.File]::AppendAllText($relFile, "$(Get-Date -Format 'yyyy-MM-dd HH:mm') $sha $summary`n", $utf8)
    }
    Write-Status 'changed' "$sha $summary"
    Step "Hotovo: $sha $summary"
}

function Invoke-Install {
    if (-not (Test-Path -LiteralPath (Join-Path $legacy '.git'))) {
        Step "Klon legacy EDISON (jen soubory stromu) do $legacy"
        # Cela historie legacy repa ma stovky MB, uloha potrebuje posledni commit a pet souboru.
        Invoke-Native git clone --quiet --depth 1 --filter=blob:none --no-checkout $legacyUrl $legacy
    }
    Invoke-Native git -C $legacy sparse-checkout set --no-cone @legacyFiles
    Invoke-Native git -C $legacy checkout --quiet

    if (-not (Test-Path -LiteralPath (Join-Path $repo '.git'))) {
        Step "Klon EDISON2 do $repo"
        Invoke-Native git clone --quiet $repoUrl $repo
    }
    # Planovac spousti skript z klonu a klon aktualizuje az beh skriptu: bez nej v origin/main by uloha nikdy nenabehla.
    Invoke-Native git -C $repo fetch --quiet origin
    Invoke-Native git -C $repo reset --quiet --hard
    Invoke-Native git -C $repo checkout --quiet -B main origin/main
    $script = Join-Path $repo 'tools\tree-sync-task.ps1'
    if (-not (Test-Path -LiteralPath $script)) { throw 'tools/tree-sync-task.ps1 jeste neni v origin/main: nejdriv ho pushni, pak -Install znovu' }

    Step 'Pre-push hook (husky) v klonu EDISON2'
    Push-Location $repo
    try { Invoke-Native dotnet tool restore; Invoke-Native dotnet husky install } finally { Pop-Location }

    Step "Registrace ulohy '$taskName' (kazdych 10 minut, jen pri prihlasenem uzivateli)"
    # conhost --headless: jinak by kazdych 10 minut problikla konzole.
    $cmd = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$script`" > `"$logFile`" 2>&1"
    $action = New-ScheduledTaskAction -Execute (Join-Path $env:SystemRoot 'System32\conhost.exe') -Argument "--headless cmd.exe /c $cmd"
    $trigger = New-ScheduledTaskTrigger -Once -At (Get-Date) -RepetitionInterval (New-TimeSpan -Minutes 10)
    # Interactive: git na GitHub a VPN na DEV jsou vazane na session prihlaseneho uzivatele.
    $principal = New-ScheduledTaskPrincipal -UserId "$env:USERDOMAIN\$env:USERNAME" -LogonType Interactive -RunLevel Limited
    $settings = New-ScheduledTaskSettingsSet -MultipleInstances IgnoreNew -StartWhenAvailable -AllowStartIfOnBatteries `
        -DontStopIfGoingOnBatteries -ExecutionTimeLimit (New-TimeSpan -Hours 1)
    Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings `
        -Description 'Drzi Tree.cls na DEV a v EDISON2 srovnany s legacy XML (tools/tree-sync-task.ps1).' -Force | Out-Null
    Start-ScheduledTask -TaskName $taskName
    Write-Host "    Hotovo, prvni beh spusten. Log behu: $logFile, stav: $statusFile"
}

New-Item -ItemType Directory -Force -Path $work | Out-Null

if ($AckRel) {
    if (Test-Path -LiteralPath $relFile) { Remove-Item -LiteralPath $relFile -Force }
    Write-Host 'Pripominka vydani EDISONMAIN2 na REL smazana.'
    exit 0
}

# Install resetuje klon a Uninstall maze stav: pod rozbehnutym behem by mu zahodily commit nebo stav zase zalozil.
$mutex = New-Object System.Threading.Mutex($false, 'Local\EDISON2-tree-sync')
try { $owned = $mutex.WaitOne(0) } catch [System.Threading.AbandonedMutexException] { $owned = $true }
if (-not $owned) {
    Write-Host 'Beh tree-sync prave probiha, zkus to za par minut.'
    exit $(if ($Install -or $Uninstall) { 1 } else { 0 })
}

if ($Install) { Invoke-Install; exit 0 }
if ($Uninstall) {
    if (Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue) {
        Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
    }
    # Podle status.txt pozna tree-drift.sh, ze uloha bezi; bez nej se vrati k rade sync-tree.ps1.
    if (Test-Path -LiteralPath $statusFile) { Remove-Item -LiteralPath $statusFile -Force }
    Write-Host "Uloha '$taskName' zrusena. Klony a pripominka REL v $work zustaly."
    exit 0
}

try {
    Invoke-Run
    exit 0
}
catch {
    Write-Host "FAILED: $($_.Exception.Message)"
    Write-Status 'error' $_.Exception.Message
    exit 1
}
