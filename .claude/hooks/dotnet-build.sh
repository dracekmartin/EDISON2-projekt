#!/bin/bash
# PostToolUse hook: po editaci .cs/.xaml v EDISON2 zbuilduje projekt, do kterého soubor patří.
# Chyby jdou na stdout, aby je Claude viděl a opravil. Vždy exit 0, editaci neblokuje.
# Bez externích závislostí (žádný jq). Testy: bash .claude/hooks/tests/dotnet-build.sh

INPUT=$(cat)

FILE_PATH=$(echo "$INPUT" | grep -o '"file_path":"[^"]*"' | head -1 | sed 's/"file_path":"//;s/"$//')
[ -z "$FILE_PATH" ] && exit 0

case "$FILE_PATH" in
  *.cs|*.xaml) ;;
  *) exit 0 ;;
esac

FP=$(echo "$FILE_PATH" | sed 's|\\\\|/|g; s|\\|/|g')
DIR=$(dirname "$FP")

# Jen strom EDISON2 a jeho worktree: legacy repo EDISON dotnet build nepřeloží nikdy.
ROOT=$(git -C "$DIR" rev-parse --show-toplevel 2>/dev/null) || exit 0
[ -f "$ROOT/EDISON2.sln" ] || exit 0

# Nejbližší složka s .csproj nad souborem. Cesta relativní ke kořeni obejde rozdílnou
# velikost písmene disku ve file_path a ve výstupu gitu.
REL=$(git -C "$DIR" rev-parse --show-prefix 2>/dev/null)
REL=${REL%/}
PROJ=""
while [ -z "$PROJ" ]; do
  for P in "$ROOT${REL:+/$REL}"/*.csproj; do
    [ -f "$P" ] && { PROJ=$P; break; }
  done
  [ -z "$REL" ] && break
  case "$REL" in */*) REL=${REL%/*} ;; *) REL="" ;; esac
done
[ -n "$PROJ" ] || exit 0

# Souběžné buildy chatů v jednom stromu si přepisují obj/ (CS2001, *_wpftmp.csproj).
# Zámek je v git dir, takže worktree má vlastní a buildí nezávisle.
LOCK="$(git -C "$ROOT" rev-parse --absolute-git-dir)/dotnet-build.lock"
# Zámek starší než 10 min zbyl po zabitém hooku, timeout hooku v settings.json je kratší.
[ -n "$(find "$LOCK" -maxdepth 0 -mmin +10 2>/dev/null)" ] && rmdir "$LOCK" 2>/dev/null
if ! mkdir "$LOCK" 2>/dev/null; then
  echo "dotnet build přeskočen: v tomhle stromu právě builduje jiný chat a souběžný build by kolidoval v obj/."
  exit 0
fi
trap 'rmdir "$LOCK" 2>/dev/null' EXIT
trap 'exit 0' INT TERM HUP

# global.json (verze SDK) hledá dotnet od pracovního adresáře, ne od projektu.
cd "$ROOT" || exit 0
# Bez opakování kopie: zamčený soubor v bin/ by jinak build zdržel o 10 s.
OUTPUT=$(dotnet build "$PROJ" --nologo -v q -p:CopyRetryCount=0 2>&1)
[ $? -eq 0 ] && exit 0

# Exe spuštěné z bin/ drží DLL zamčené. Selhaná kopie bez chyby překladu není chyba kódu.
ERRS=$(echo "$OUTPUT" | grep -E ": error ")
if echo "$ERRS" | grep -qE ": error MSB30(21|27):" && ! echo "$ERRS" | grep -qvE ": error MSB30(21|27):"; then
  echo "dotnet build neověřen ($(basename "$PROJ")): kopii do bin/ blokuje zamčený soubor, nejspíš běží exe z bin/ (MSB3021)."
  exit 0
fi
echo "dotnet build FAILED ($(basename "$PROJ")) after editing $FP:"
echo "$OUTPUT" | grep -E "(error |warning )" | head -20

exit 0
