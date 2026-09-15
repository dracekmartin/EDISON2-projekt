#!/bin/bash
# PostToolUse hook: uploads & compiles ObjectScript files on InterSystems server
# after Claude edits them. No external dependencies (no Python, no jq).
#
# Config env vars (set in ~/.claude/settings.json):
#   CACHE_SERVER, CACHE_NAMESPACE, CACHE_USER, CACHE_PASSWORD

INPUT=$(cat)

# Extract file_path from hook JSON using pure bash/sed
FILE_PATH=$(echo "$INPUT" | grep -o '"file_path":"[^"]*"' | head -1 | sed 's/"file_path":"//;s/"$//' | sed 's|\\\\|/|g')

[ -z "$FILE_PATH" ] && exit 0

# Ve worktree nenasazovat - sdílený server patří hlavní kopii. Kotveno na cestu
# souboru, ne na CLAUDE_PROJECT_DIR: ta při vstupu do worktree během session
# zůstává na hlavním checkoutu, takže původní podmínka nikdy nesedla.
case "$FILE_PATH" in
  */.claude/worktrees/*) exit 0 ;;
esac

case "$FILE_PATH" in
  *.cls|*.mac|*.int|*.inc) ;;
  *) exit 0 ;;
esac

SERVER="${CACHE_SERVER:-http://192.168.16.1:57770}"
NAMESPACE="${CACHE_NAMESPACE:-MLINE}"
USER="${CACHE_USER:-_SYSTEM}"
PASS="${CACHE_PASSWORD:-SYS}"

# .../CACHE/ALVA/Foo/Bar.cls → ALVA.Foo.Bar.cls
AFTER_CACHE=$(echo "$FILE_PATH" | sed -n 's|.*/CACHE/||p')
[ -z "$AFTER_CACHE" ] && exit 0

DIR_PART=$(dirname "$AFTER_CACHE")
FILENAME=$(basename "$AFTER_CACHE")
EXT="${FILENAME##*.}"
BASE="${FILENAME%.*}"

if [ "$DIR_PART" = "." ]; then
  DOC_NAME="${BASE}.${EXT}"
else
  DOC_NAME="$(echo "$DIR_PART" | sed 's|/|.|g').${BASE}.${EXT}"
fi

API="${SERVER}/api/atelier/v1/${NAMESPACE}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Build JSON payload to temp file (avoids shell arg length limit)
TMPFILE=$(mktemp)
awk -f "$SCRIPT_DIR/build-payload.awk" "$FILE_PATH" > "$TMPFILE"

# Upload
HTTP=$(curl -s -o /dev/null -w "%{http_code}" \
  -X PUT -H "Content-Type: application/json" \
  -u "${USER}:${PASS}" -d @"$TMPFILE" \
  "${API}/doc/${DOC_NAME}?ignoreConflict=1")

rm -f "$TMPFILE"

[ "$HTTP" -lt 200 ] || [ "$HTTP" -ge 300 ] && { echo "Upload failed ${DOC_NAME}: HTTP ${HTTP}" >&2; exit 0; }

# Compile — zachytit odpověď a vypsat compile chyby (Atelier status.errors[]).
# Bez tohohle se syntax chyby tiše ztrácely (>/dev/null) a projevily se až za běhu.
COMPILE_RESP=$(curl -s -X POST -H "Content-Type: application/json" \
  -u "${USER}:${PASS}" -d "[\"${DOC_NAME}\"]" \
  "${API}/action/compile?flags=cuk")

case "$COMPILE_RESP" in
  *'"errors":[]'*) ;;                                            # čistý compile
  *'"status":{"errors":null'*) ;;                               # čistý compile (alt. tvar)
  *) echo "Compile error ${DOC_NAME}: ${COMPILE_RESP}" >&2 ;;   # chyby → na stderr
esac

exit 0
