#!/bin/bash
# SessionStart hook: upozorni, ze se legacy zdroj stromu modulu pohnul a Tree.cls je stale.
#
# Zamerne NEBLOKUJE. Drift zpusobi commit v CIZIM repu, ne tvuj push; fingerprint se navic
# porovnava proti lokalni kopii legacy repa, takze tvrdy gate by se mylil obema smery
# (kolega nepushnul / ja nepullnul). Proto advisory, ne pre-push brana.
#
# Samostatny hook, ne rozsireni session-brief.sh - ten ma jineho autora a vlastni rozpocet radku.
# Pri srovnanem stavu nevypise NIC. Publikum je AGENT (viz komentar u echo bloku).

cd "${CLAUDE_PROJECT_DIR:-.}" 2>/dev/null || exit 0

FP="tools/tree-sources.sha256"
LEGACY="${EDISON_LEGACY_ROOT:-/c/MLineRepos/EDISON}"

[ -f "$FP" ] || exit 0      # stav pred prvnim zapisem fingerprintu
[ -d "$LEGACY" ] || exit 0  # stroj bez legacy repa - zadny sum

DRIFT=""
while IFS= read -r line; do
    line="${line%$'\r'}"
    case "$line" in ''|'#'*) continue ;; esac
    want="${line%% *}"
    rel="${line##*  }"
    src="$LEGACY/$rel"
    # Presunuty/prejmenovany zdroj neni drift stromu - to je vec generatoru, ne teto hlasky.
    [ -f "$src" ] || continue
    # CR se zahazuje: git checkoutuje legacy soubory na Windows jako CRLF a na CI jako LF,
    # takze hash surovych bajtu by se nikdy neshodl s tim, co spocital workflow.
    have=$(tr -d '\r' < "$src" 2>/dev/null | sha256sum | cut -d' ' -f1)
    [ -n "$have" ] || continue
    [ "$have" = "$want" ] || DRIFT="$DRIFT ${rel##*/}"
done < "$FP"

[ -n "$DRIFT" ] || exit 0

# Vystup hooku cte AGENT, ne clovek - VSCode extension ho uzivateli nevykresluje
# (stejne jako u session-brief.sh a drift-report.sh). Proto je formulovany jako pokyn:
# jinak by varovani skoncilo v kontextu a k cloveku nedoslo.
echo "! Legacy strom modulu se pohnul:${DRIFT}"
echo "  Rekni to uzivateli hned v prvni odpovedi a nabidni mu regeneraci:"
echo "      powershell -File tools/sync-tree.ps1"
echo "  Dokud se nezregeneruje, je Tree.cls stale snapshot - ve stromu modulu"
echo "  chybi nebo prebyvaji polozky proti tomu, co je v legacy XML."

exit 0
