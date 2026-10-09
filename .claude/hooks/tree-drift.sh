#!/bin/bash
# SessionStart hook: upozorni, ze se legacy zdroj stromu modulu pohnul a Tree.cls je stale,
# a ukaze stav ulohy tree-sync (tools/tree-sync-task.ps1), ktera strom srovnava sama.
#
# Zamerne NEBLOKUJE. Drift zpusobi commit v CIZIM repu, ne tvuj push; fingerprint se navic
# porovnava proti lokalni kopii legacy repa (s ulohou proti jejimu klonu), takze tvrdy gate
# by se mylil obema smery (kolega nepushnul / ja nepullnul). Proto advisory, ne pre-push brana.
#
# Samostatny hook, ne rozsireni session-brief.sh - ten ma jineho autora a vlastni rozpocet radku.
# Pri srovnanem stavu nevypise NIC. Publikum je AGENT (viz komentar u echo bloku).

cd "${CLAUDE_PROJECT_DIR:-.}" 2>/dev/null || exit 0

FP="tools/tree-sources.sha256"
LEGACY="${EDISON_LEGACY_ROOT:-/c/MLineRepos/EDISON}"
# Stejne umisteni jako v tree-sync-task.ps1; stroj bez ulohy tenhle adresar nema.
TS="${TREE_SYNC_HOME:-${LOCALAPPDATA:+$(cygpath -u "$LOCALAPPDATA" 2>/dev/null)/m-line/tree-sync}}"
TASK=""
[ -n "$TS" ] && [ -f "$TS/status.txt" ] && TASK=1
# S ulohou se porovnava s jejim legacy klonem (aktualni origin): lokalni EDISON muze byt pozadu
# a sync-tree.ps1 by z nej na DEV nasadil starsi strom, nez pushnula uloha.
[ -n "$TASK" ] && [ -d "$TS/EDISON" ] && LEGACY="$TS/EDISON"

[ -f "$FP" ] || exit 0      # stav pred prvnim zapisem fingerprintu

DRIFT=""
if [ -d "$LEGACY" ]; then   # stroj bez legacy repa - zadny sum
    while IFS= read -r line; do
        line="${line%$'\r'}"
        case "$line" in ''|'#'*) continue ;; esac
        want="${line%% *}"
        rel="${line##*  }"
        src="$LEGACY/$rel"
        # Presunuty/prejmenovany zdroj neni drift stromu - to je vec generatoru, ne teto hlasky.
        [ -f "$src" ] || continue
        # CR se zahazuje: git checkoutuje legacy soubory podle autocrlf jako CRLF i LF,
        # takze hash surovych bajtu by se mezi checkouty neshodl.
        have=$(tr -d '\r' < "$src" 2>/dev/null | sha256sum | cut -d' ' -f1)
        [ -n "$have" ] || continue
        [ "$have" = "$want" ] || DRIFT="$DRIFT ${rel##*/}"
    done < "$FP"
fi

# Vystup hooku cte AGENT, ne clovek - VSCode extension ho uzivateli nevykresluje
# (stejne jako u session-brief.sh a drift-report.sh). Proto je formulovany jako pokyn:
# jinak by varovani skoncilo v kontextu a k cloveku nedoslo.
if [ -n "$DRIFT" ] && [ -n "$TASK" ]; then
    echo "! Tree.cls v tomhle checkoutu je pozadu za legacy XML:${DRIFT}"
    echo "  Strom srovnava uloha tree-sync (do 10 min po zmene). Rekni uzivateli v prvni odpovedi,"
    echo "  at pullne EDISON2. sync-tree.ps1 nenabizej: z lokalniho legacy repa muze nasadit starsi strom."
elif [ -n "$DRIFT" ]; then
    echo "! Legacy strom modulu se pohnul:${DRIFT}"
    echo "  Rekni to uzivateli hned v prvni odpovedi a nabidni mu regeneraci:"
    echo "      powershell -File tools/sync-tree.ps1"
    echo "  Dokud se nezregeneruje, je Tree.cls stale snapshot - ve stromu modulu"
    echo "  chybi nebo prebyvaji polozky proti tomu, co je v legacy XML."
fi

if [ -n "$TASK" ]; then
    get() { sed -n "s/^$1=//p" "$TS/status.txt" | head -1 | tr -d '\r'; }
    result=$(get result)
    last_ok=$(get last_ok)
    if [ "$result" = "error" ]; then
        echo "! Uloha tree-sync naposledy selhala ($(get last_run)): $(get message)"
        echo "  Log: $TS/last-run.log. Rekni to uzivateli v prvni odpovedi; dokud selhava,"
        echo "  strom na DEV ani v gitu nesleduje legacy XML."
    fi
    # Den bez uspesneho behu: pocitac byl vypnuty, spadla VPN nebo uloha v Planovaci nebezi.
    # Prazdne last_ok (uloha jeste neuspela) by date -d vzal jako dnesni pulnoc; chybu ukaze blok vyse.
    ok_epoch=""
    [ -n "$last_ok" ] && ok_epoch=$(date -d "$last_ok" +%s 2>/dev/null)
    if [ -n "$ok_epoch" ] && [ $(( $(date +%s) - ok_epoch )) -gt 86400 ]; then
        echo "! Uloha tree-sync nema uspesny beh od $last_ok. Rekni to uzivateli v prvni odpovedi."
    fi
fi

if [ -n "$TS" ] && [ -s "$TS/rel-pending.txt" ]; then
    n=$(grep -c . "$TS/rel-pending.txt")
    since=$(head -1 "$TS/rel-pending.txt" | cut -d' ' -f1)
    echo "! Strom na DEV je novejsi nez EDISONMAIN2 na REL ($n zmen od $since, $TS/rel-pending.txt)."
    echo "  Zakaznici ho dostanou az s vydanim EDISONMAIN2. Pripomen to uzivateli v prvni odpovedi;"
    echo "  po vydani pripominku smaze: powershell -File tools/tree-sync-task.ps1 -AckRel"
fi

exit 0
