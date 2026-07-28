#!/bin/bash
# SessionStart hook: nabidne frontu prace, aby ji uzivatel nemusel diktovat.
# Vypisuje jen to, co se meni mezi sessions (stav) - znalost je v CLAUDE.md a rules/.
#
# Zamerne KRATKE. Tohle jde do kazde session, takze kazdy radek se plati porad.
# Tikety se sem netahaji - hook neumi MCP; agenda si je dotahne, az o ne pujde.

cd "${CLAUDE_PROJECT_DIR:-.}" 2>/dev/null || exit 0
[ -d .git ] || exit 0

ROADMAP="TemplateDesigner/.claude/docs/roadmap.md"

echo "=== Stav repa ==="
echo ""

VETEV=$(git rev-parse --abbrev-ref HEAD 2>/dev/null)
ROZDELANO=$(git status --porcelain 2>/dev/null | wc -l | tr -d ' ')
echo "Vetev: ${VETEV}   rozdelanych souboru: ${ROZDELANO}"
if [ "$ROZDELANO" != "0" ]; then
  git status --short 2>/dev/null | head -8 | sed 's/^/    /'
fi

echo ""
echo "Poslednich 5 commitu:"
git log --oneline -5 2>/dev/null | sed 's/^/    /'

if [ -f "$ROADMAP" ]; then
  echo ""
  echo "=== Roadmapa: co je nahore ==="
  echo ""
  # Ukazatel na dalsi session (blockquote "▶") + prvni dve vlny s bloky.
  grep -m1 -A2 "▶ Další session" "$ROADMAP" 2>/dev/null | sed 's/^> //; s/^/  /'
  echo ""
  # Radky tabulek vln = "| **Bxx** | ... | vel | zona | zavisi |"
  grep -oE '^\| \*\*[A-Z]+-?[0-9]+\*\* \| \*\*[^|]{0,70}' "$ROADMAP" 2>/dev/null \
    | head -6 \
    | sed 's/^| //; s/ | \*\*/  -  /; s/\*\*//g; s/^/  /'
fi

echo ""
echo "=== Jak zadat praci ==="
echo ""
echo "  analyza <tiket|ID|otazka>   pochopit pozadavek, nic nemenit (read-only)"
echo "  implementuj <ID|tiket>      jedna polozka od zadani po report"
echo "  novy-element <Nazev>        novy typ elementu (TD)"
echo ""
echo "  Nebo rekni prioritu vlastnimi slovy - treba \"dneska ukliď male bloky\"."

exit 0
