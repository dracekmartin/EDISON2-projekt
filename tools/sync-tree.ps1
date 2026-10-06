# Sync stromu modulu z legacy XML do Cache jednim prikazem.
#
# ASCII-only zamerne: Windows PowerShell 5.1 cte UTF-8 bez BOM jako CP-1252, takze
# diakritika ve stringech by se rozsypala (stejny duvod jako u run-cache-tests.ps1).
#
# Proc skript a ne tri prikazy: generator zapisuje Tree.cls pres File.WriteAllText, takze
# PostToolUse hook compile-objectscript.sh NEFIRUJE a na serveru zustane stara trida.
# Deploy se proto musi vynutit - a to je krok, ktery se zapomina.

[CmdletBinding()]
param(
    # Nabidne git pull v legacy repu. Default vypnuty - do ciziho repa se nezasahuje bez rceni.
    [switch]$Pull,
    # Po nasazeni pusti Cache testy dotcene package.
    [switch]$Test,
    # Jen regeneruj a ukaz diff, nenasazuj.
    [switch]$NoDeploy
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$legacy = if ($env:EDISON_LEGACY_ROOT) { $env:EDISON_LEGACY_ROOT } else { 'C:\MLineRepos\EDISON' }
$treeCls = Join-Path $repo 'EDISONMAIN2\CACHE\ALVA\EDISONMAIN2\API\Tree.cls'

function Step($msg) { Write-Host "==> $msg" -ForegroundColor Cyan }
function Fail($msg) { Write-Host "FAILED: $msg" -ForegroundColor Red; exit 1 }

# 'bash' v PATH je na Windows typicky WSL (C:\Windows\System32\bash.exe), ktery
# nerozumi C:/... cestam a hook by nenasel. Git Bash odvodime od umisteni git.exe.
function Resolve-GitBash {
    $git = (Get-Command git -ErrorAction SilentlyContinue).Source
    if ($git) {
        $root = Split-Path -Parent (Split-Path -Parent $git)
        $cand = Join-Path $root 'bin\bash.exe'
        if (Test-Path $cand) { return $cand }
    }
    $cand2 = Join-Path $env:ProgramFiles 'Git\bin\bash.exe'
    if (Test-Path $cand2) { return $cand2 }
    Fail 'Git Bash nenalezen - bash v PATH je WSL a deploy hook by nespustil'
}

if (-not (Test-Path $legacy)) { Fail "legacy repo nenalezeno: $legacy" }

if ($Pull) {
    Step "git pull v legacy repu ($legacy)"
    git -C $legacy pull --ff-only
    if ($LASTEXITCODE -ne 0) { Fail 'git pull v legacy repu selhal' }
}

Step 'Regenerace Tree.cls z legacy XML'
Push-Location $repo
# Generator jinak zapise do c:\MLineRepos\EDISON2 - z worktree by prepsal hlavni checkout a nasadil starou tridu.
$env:EDISON2_ROOT = $repo
try { dotnet run --project tools/GenerateTree } finally { Pop-Location }
# Generator vraci nenulovy kod, kdyz nezapsal (napr. smazany splice marker) - nesmi se
# pokracovat na deploy, jinak bychom nasadili starou tridu a hlasili uspech.
if ($LASTEXITCODE -ne 0) { Fail 'generator selhal, Tree.cls NEZAPSAN - deploy preskocen' }

Step 'Zmeny v repu'
git -C $repo diff --stat -- EDISONMAIN2/CACHE/ALVA/EDISONMAIN2/API/Tree.cls tools/tree-sources.sha256
$dirty = git -C $repo status --porcelain -- EDISONMAIN2/CACHE/ALVA/EDISONMAIN2/API/Tree.cls
if (-not $dirty) { Write-Host '    Tree.cls bez zmeny - snapshot uz byl srovnany.' }

if ($NoDeploy) { Step 'NoDeploy - konec'; exit 0 }

# Deploy hook ve worktree nic nenasadi (sdileny server patri hlavni kopii) a skonci s kodem 0,
# hlaseni "nasazen" by lhalo a testy by bezely proti stare tride.
if ($repo -like '*\.claude\worktrees\*') {
    Fail 'worktree: Tree.cls je zregenerovany, ale nasazuje se jen z hlavniho checkoutu - po slit tam spust sync-tree.ps1'
}

Step 'Deploy na Cache (vynuceny, hook na File.WriteAllText nefiruje)'
$fwd = $treeCls.Replace('\', '/')
$payload = '{"tool_input":{"file_path":"' + $fwd + '"}}'
# bash dostane forward-slash cestu; backslashe by sezral jako escape sekvence.
$hook = (Join-Path $repo '.claude\hooks\compile-objectscript.sh').Replace('\', '/')
# Presmerovani dela BASH, ne PowerShell: 2>&1 na native prikazu ve WinPS 5.1 zabali
# stderr do NativeCommandError a nastavi $? na false i pri exit 0.
$bash = Resolve-GitBash
$out = $payload | & $bash -c ("bash " + [char]39 + $hook + [char]39 + " 2>&1")
if ($LASTEXITCODE -ne 0) { Fail "deploy hook skoncil s kodem $LASTEXITCODE : $out" }
# Hook je advisory (vzdy exit 0) a chyby pise na stderr - proto se kontroluje i vystup.
if ($out) { Fail "deploy hook ohlasil problem: $out" }
Write-Host '    Tree.cls nasazen a zkompilovan.'

if ($Test) {
    Step 'Cache testy (ALVA.EDISONMAIN2.Tests)'
    $runner = Join-Path $PSScriptRoot 'run-cache-tests.ps1'
    & $runner -Package 'ALVA.EDISONMAIN2.Tests'
    if ($LASTEXITCODE -ne 0) { Fail 'Cache testy selhaly' }
}

Write-Host ''
Write-Host 'Hotovo. Zkontroluj diff a commitni (Tree.cls + tools/tree-sources.sha256).' -ForegroundColor Green
