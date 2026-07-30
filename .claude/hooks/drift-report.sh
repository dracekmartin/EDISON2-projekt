#!/bin/bash
# PostToolUse hook: pusti kontroly, ktere si repo sam predepsal, a jen je nahlasi.
# Nic neblokuje ani neopravuje - exit 0 vzdy.
#
# Kontroluje POUZE prave editovany soubor, ne cely repo. Duvod: R1/R2/R4 jsou
# pravidla TemplateDesigneru, ale EDControls/EDISONMAIN2 maji legitimni duvody
# je nesplnovat (EDViewModel base potrebuje System.Windows). Repo-wide grep by
# vyrobil 10 falesnych nalezu a hook by se stal tapetou.
#
# Zdroj kontrol: TemplateDesigner/.claude/rules/architecture-rules.md
# sekce "Kde jsou pravidla overitelna".

INPUT=$(cat)

FILE_PATH=$(echo "$INPUT" | grep -o '"file_path":"[^"]*"' | head -1 | sed 's/"file_path":"//;s/"$//')
[ -z "$FILE_PATH" ] && exit 0

FP=$(echo "$FILE_PATH" | sed 's|\\\\|/|g; s|\\|/|g')
[ -f "$FP" ] || exit 0

# Pravidla R1/R2/R4 plati pro TemplateDesigner
case "$FP" in
  */TemplateDesigner/*) ;;
  *) exit 0 ;;
esac

NALEZY=""

case "$FP" in
  *ViewModel.cs)
    # R2: chyby pres IPopupDisplayer, nikdy MessageBox
    if grep -q "MessageBox\." "$FP" 2>/dev/null; then
      NALEZY="${NALEZY}  R2: MessageBox v ViewModelu - chyby maji jit pres IPopupDisplayer.Error(ex)
$(grep -n "MessageBox\." "$FP" | head -3 | sed 's/^/      /')
"
    fi
    # R4: ViewModel bez WPF referenci
    if grep -q "^using System\.Windows" "$FP" 2>/dev/null; then
      NALEZY="${NALEZY}  R4: using System.Windows ve ViewModelu - View operace patri do callbacku (Action?)
$(grep -n "^using System\.Windows" "$FP" | head -3 | sed 's/^/      /')
"
    fi
    # R1: synchronni Cache volani. Fast operace jsou OK, proto jen upozorneni.
    if grep -q "MLRest\.\(Query\|QueryOrThrow\)" "$FP" 2>/dev/null; then
      if ! grep -q "Task\.Run" "$FP" 2>/dev/null; then
        NALEZY="${NALEZY}  R1?: MLRest volani bez Task.Run - u operaci nad ~500ms ma byt async
"
      fi
    fi
    ;;
esac

# Brany, ktere jsou na tenhle soubor navazane. Jeden radek, ne kazani.
BRANA=""
case "$FP" in
  */CACHE/*/EDInputForm/*.cls)
    BRANA="dotnet test TemplateDesigner.Tests --filter EDInputFormSchemaDrift   (labely: kanon v ux-conventions.md §8)" ;;
  */dotnet/Models/Elements/*.cs|*/dotnet/Models/ElementTypes.cs|*/dotnet/Models/TemplateElement.cs)
    BRANA="dotnet test TemplateDesigner.Tests --filter \"ElementRegistrationDrift|CloneCompleteness\"" ;;
esac

if [ -n "$NALEZY" ]; then
  echo "drift-report - nalezy v $(basename "$FP"):"
  printf "%s" "$NALEZY"
fi
[ -n "$BRANA" ] && echo "drift-report - brana pro tenhle soubor: $BRANA"

exit 0
