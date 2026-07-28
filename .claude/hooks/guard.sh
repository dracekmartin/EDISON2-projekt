#!/bin/bash
# PreToolUse hook: tvrde zastavky. Blokuje editace, ktere maji byt vzdy vedomym
# rozhodnutim cloveka, ne vedlejsim efektem behu agenta.
#
# Exit 0 = propustit, exit 2 = zablokovat (stderr se vraci agentovi jako duvod).
# Zamerne blokuje, nikoli "opravuje" - agent se ma zeptat, ne to obejit.
#
# Hranice: hlida jen Edit/Write/NotebookEdit podle file_path. Zapis pres Bash
# (sed -i, presmerovani) tim neprojde - na to jsou permissions.deny.
# Bez externich zavislosti (zadny jq), stejny styl jako ostatni hooky v repu.

# Vypnuto, dokud si ho nekdo vedome nezapne (EDISON_GUARD=1 v .claude/settings.local.json).
# Registrace je verzovana, aby neprebila projektove hooky, ale blokovani je opt-in -
# tvrda zastavka je trenice a nema prijit nekomu, kdo o ni nepozadal.
[ "$EDISON_GUARD" = "1" ] || exit 0

INPUT=$(cat)

TOOL=$(echo "$INPUT" | grep -o '"tool_name":"[^"]*"' | head -1 | sed 's/"tool_name":"//;s/"$//')
case "$TOOL" in
  Edit|Write|NotebookEdit|MultiEdit) ;;
  *) exit 0 ;;
esac

FILE_PATH=$(echo "$INPUT" | grep -o '"file_path":"[^"]*"' | head -1 | sed 's/"file_path":"//;s/"$//')
[ -z "$FILE_PATH" ] && exit 0

# Normalizace oddelovacu - na Windows prichazi cesta s zpetnymi lomitky
FP=$(echo "$FILE_PATH" | sed 's|\\\\|/|g; s|\\|/|g')

blokuj() {
  echo "ZABLOKOVANO hookem guard.sh: $1" >&2
  echo "" >&2
  echo "Soubor: $FP" >&2
  echo "" >&2
  echo "$2" >&2
  exit 2
}

case "$FP" in
  # Gold baseline = zmrazeny digest v konstantach Expected* uvnitr test tridy.
  # Prepsat ho znamena rict "novy vystup je spravny" - to je tvrzeni o datech,
  # ne uklid. Osm commitu to uz delalo a kazdy k tomu mel duvod v message.
  *GoldTest.cls|*LegacyParityTest.cls|*LegacyTest.cls)
    blokuj "re-baseline gold / parity testu" \
"Tenhle soubor drzi zmrazeny ocekavany vystup proti legacy systemu. Prepsat ho smi
jen clovek, ktery vi, ze zmena vystupu je zamerna.

Co udelat misto toho:
  1. spust test a zjisti, CO presne se v digestu zmenilo
  2. vysvetli, proc je novy vystup spravny (ktere pole pribylo, ktere poradi se zmenilo)
  3. nech rozhodnuti na uzivateli - teprve po jeho souhlasu baseline prepis"
    ;;

  # Legacy = jina pravidla (minimalni diff, napodobit okoli, zadne nove patterny).
  # R1-R5 a MVVM standard tam neplati a agent by je zacal "opravovat".
  */EDISONMAIN2.LegacyMiniApps/*|*.mac|*.int)
    blokuj "zasah do legacy kodu" \
"Legacy se udrzuje jinym rezimem nez novy vyvoj: minimalni diff, napodobit okolni
kod, zadne zavadeni novych patternu, overeni reprodukci pred a po.

Rekni uzivateli, co presne chces zmenit a proc, a nech ho rozhodnout."
    ;;

  # Brana sama. Agent, ktery uprav pre-push, aby testy prosly, je nejhorsi mozny
  # vysledek - tise zrusi jedinou tvrdou kontrolu, kterou repo ma.
  */.husky/pre-push|*/tools/run-cache-tests.ps1)
    blokuj "zmena testovaci brany" \
"Tohle je pre-push brana. Kdyz pada, je to signal o kodu, ne o brane.
Oprav pricinu; zmenu brany navrhni uzivateli slovy a nech rozhodnuti na nem."
    ;;
esac

exit 0
