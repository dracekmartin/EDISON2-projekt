#!/bin/bash
# Testy .claude/hooks/dotnet-build.sh. Spuštění: bash .claude/hooks/tests/dotnet-build.sh
#
# Hook se pouští nad dočasnými repy: kopie stromu EDISON2 (EDISON2.sln, projekty, worktree)
# a repo bez EDISON2.sln jako legacy EDISON. dotnet je podvržený přes PATH: zapíše, co by
# buildil a odkud, a vrátí výstup a exit kód, které test zrovna potřebuje.

H="$(cd "$(dirname "$0")/.." && pwd)/dotnet-build.sh"
FAIL=0
T=$(mktemp -d)
trap 'rm -rf "$T"' EXIT

BIN="$T/bin"
mkdir -p "$BIN"
cat > "$BIN/dotnet" <<'EOF'
#!/bin/bash
echo "$(pwd -W)|$*" >> "$FAKE_LOG"
[ -d "$FAKE_LOCK" ] && echo "zamceno" >> "$FAKE_LOG"
printf '%s\n' "$FAKE_OUT"
exit "${FAKE_EXIT:-0}"
EOF
chmod +x "$BIN/dotnet"
export PATH="$BIN:$PATH" FAKE_LOG="$T/dotnet.log"

G="-c user.name=t -c user.email=t@t -c core.hooksPath=/dev/null -c commit.gpgsign=false -c core.autocrlf=false"
E2="$T/E2"
mkdir -p "$E2/App/Views/Deep" "$E2/Tests" "$E2/Other" "$E2/Loose"
touch "$E2/EDISON2.sln" "$E2/App/App.csproj" "$E2/Tests/Tests.csproj" "$E2/Other/Other.csproj" \
      "$E2/App/Views/Main.xaml" "$E2/App/Views/Deep/Vm.cs" "$E2/Tests/T.cs" "$E2/Other/O.cs" \
      "$E2/Loose/x.cs" "$E2/readme.md"
git -C "$E2" init --quiet
git -C "$E2" $G add -A
git -C "$E2" $G commit --quiet -m init
git -C "$E2" $G worktree add --quiet "$E2/.claude/worktrees/wt" 2>/dev/null
WT="$E2/.claude/worktrees/wt"

LEG="$T/EDISON"
mkdir -p "$LEG/Proj"
touch "$LEG/Proj/Proj.csproj" "$LEG/Proj/a.cs"
git -C "$LEG" init --quiet
mkdir -p "$T/nogit"
touch "$T/nogit/a.cs"

win() { (cd "$(dirname "$1")" && echo "$(pwd -W)/$(basename "$1")"); }
LOCK="$E2/.git/dotnet-build.lock"

# Hook dostane cestu jako Claude Code: Windows tvar se zpětnými lomítky escapovanými v JSONu.
run() {
  : > "$FAKE_LOG"
  local p rc
  p=$(win "$1" | sed 's|/|\\\\|g')
  [ -n "$LOWER" ] && p="$(printf %s "${p:0:1}" | tr 'A-Z' 'a-z')${p:1}"
  OUT=$(printf '{"tool_name":"Edit","tool_input":{"file_path":"%s","old_string":"a"}}' "$p" \
        | FAKE_LOCK="${FAKE_LOCK:-$LOCK}" FAKE_EXIT="$FAKE_EXIT" FAKE_OUT="$FAKE_OUT" bash "$H" 2>&1)
  rc=$?
  [ $rc -eq 0 ] || bad "hook skončil s $rc a blokoval by editaci ($1)"
}
ok()   { echo "  ok    $1"; }
bad()  { echo "  FAIL  $1"; FAIL=1; }
built() { grep -q "build $(win "$2") " "$FAKE_LOG" && ok "$1" || bad "$1 (dotnet: $(cat "$FAKE_LOG"))"; }
silent() { [ ! -s "$FAKE_LOG" ] && [ -z "$OUT" ] && ok "$1" || bad "$1 (dotnet: $(cat "$FAKE_LOG"), výstup: $OUT)"; }
says() { case "$OUT" in *"$2"*) ok "$1";; *) bad "$1 (výstup: $OUT)";; esac; }
lacks() { case "$OUT" in *"$2"*) bad "$1 (výstup: $OUT)";; *) ok "$1";; esac; }

COPY='C:\sdk\Microsoft.Common.CurrentVersion.targets(5397,5): error MSB3021: Unable to copy file "obj\Debug\apphost.exe" to "bin\Debug\App.exe". The process cannot access the file because it is being used by another process. [C:\E2\App\App.csproj]'
CS='C:\E2\App\Views\Deep\Vm.cs(3,1): error CS1002: ; expected [C:\E2\App\App.csproj]'

echo "builduje projekt souboru, ne řešení:"
run "$E2/App/Views/Deep/Vm.cs";  built "zanořený .cs → App.csproj" "$E2/App/App.csproj"
grep -qF "$(cd "$E2" && pwd -W)|" "$FAKE_LOG" && ok "build běží z kořene stromu (global.json)" || bad "build běží odjinud: $(cat "$FAKE_LOG")"
grep -q -- "-p:CopyRetryCount=0" "$FAKE_LOG" && ok "kopie do bin/ se neopakuje" || bad "build bez CopyRetryCount=0: $(cat "$FAKE_LOG")"
run "$E2/App/Views/Main.xaml";   built ".xaml → App.csproj" "$E2/App/App.csproj"
run "$E2/Tests/T.cs";            built "testovací projekt → Tests.csproj" "$E2/Tests/Tests.csproj"
LOWER=1 run "$E2/Other/O.cs";    built "malé písmeno disku ve file_path" "$E2/Other/Other.csproj"
[ -z "$OUT" ] && ok "úspěšný build mlčí" || bad "úspěšný build hlásí: $OUT"

echo "chyba překladu se ohlásí:"
FAKE_EXIT=1 FAKE_OUT="$CS" run "$E2/App/Views/Deep/Vm.cs"
says "hláška FAILED s projektem" "dotnet build FAILED (App.csproj)"
says "cesta souboru bez escapovaných lomítek" "after editing $(win "$E2/App/Views/Deep/Vm.cs")"
says "řádek s chybou překladače" "error CS1002"
[ -d "$LOCK" ] && bad "zámek zůstal po neúspěšném buildu" || ok "zámek uvolněn i po neúspěšném buildu"
FAKE_EXIT=1 FAKE_OUT="$CS
$COPY" run "$E2/App/Views/Deep/Vm.cs"
says "chyba překladu se ohlásí i vedle zamčené kopie" "error CS1002"
FAKE_EXIT=1 FAKE_OUT="Build FAILED." run "$E2/App/Views/Deep/Vm.cs"
says "selhání bez řádku error je FAILED, ne zamčená kopie" "dotnet build FAILED"

echo "zamčená kopie do bin/ (běžící exe) je jedna věta:"
FAKE_EXIT=1 FAKE_OUT="$COPY
$COPY" run "$E2/App/Views/Deep/Vm.cs"
says "jedna věta proč" "kopii do bin/ blokuje zamčený soubor"
lacks "bez hlášky FAILED" "FAILED"
[ "$(printf '%s\n' "$OUT" | wc -l)" -eq 1 ] && ok "právě jeden řádek" || bad "víc řádků: $OUT"
FAKE_EXIT=1 FAKE_OUT="${COPY/MSB3021/MSB3027}" run "$E2/App/Views/Deep/Vm.cs"
says "MSB3027 (vyčerpané opakování) taky" "kopii do bin/ blokuje zamčený soubor"

echo "nebuildí:"
run "$LEG/Proj/a.cs";   silent "soubor v repu bez EDISON2.sln (legacy EDISON)"
run "$T/nogit/a.cs";    silent "soubor mimo git"
run "$E2/readme.md";    silent "soubor, který není .cs ani .xaml"
run "$E2/Loose/x.cs";   silent ".cs bez .csproj nad sebou"

echo "zámek proti souběžnému buildu:"
run "$E2/Other/O.cs"
grep -q zamceno "$FAKE_LOG" && ok "build drží zámek" || bad "build běží bez zámku"
[ -d "$LOCK" ] && bad "zámek zůstal po buildu" || ok "zámek uvolněn po buildu"
mkdir "$LOCK"
run "$E2/Other/O.cs"
[ ! -s "$FAKE_LOG" ] && ok "druhý build se přeskočí" || bad "druhý build běžel: $(cat "$FAKE_LOG")"
says "jedna věta proč" "builduje jiný chat"
[ -d "$LOCK" ] && ok "cizí zámek zůstane" || bad "hook smazal cizí zámek"
FAKE_LOCK="$E2/.git/worktrees/wt/dotnet-build.lock" run "$WT/Other/O.cs"
built "worktree buildí svůj strom i se zámkem hlavního" "$WT/Other/Other.csproj"
touch -d '20 minutes ago' "$LOCK"
run "$E2/Other/O.cs";   built "zámek starší než 10 min se převezme" "$E2/Other/Other.csproj"
[ -d "$LOCK" ] && { bad "převzatý zámek zůstal"; rmdir "$LOCK"; } || ok "převzatý zámek uvolněn"

exit $FAIL
