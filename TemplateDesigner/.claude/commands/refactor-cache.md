---
description: Refaktoruj existující Caché třídu v CACHE/ podle moderních konvencí
argument-hint: [cesta k .cls souboru v CACHE/]
allowed-tools: Read, Edit, Write, Bash(grep *), Bash(ls *), Glob, Grep
---

Refaktoruj existující Caché třídu "$ARGUMENTS" na místě — zachovej business logiku, uprav syntaxi a konvence.

## Kontext — přečti před zahájením

Konvence a pravidla: !`cat .claude/rules/cache-api.md`
Business logika (JŘ kontext): !`cat .claude/rules/cache-business-logic.md`
Referenční implementace (vzor): !`cat CACHE/ALVA/TemplateDesigner/API/ZastavkoveJrMhdAPI.cls`
Abstraktní základ: !`cat CACHE/ALVA/TemplateDesigner/API/AgendaAPI.cls`

Zdrojový soubor k refaktoru: !`cat $ARGUMENTS`

## Refaktorovací checklist

Projdi kód systematicky a aplikuj každý bod:

### Operátory
- [ ] `&` (AND) → `&&`
- [ ] `!` (OR) → `||` (pozor: `'` je negace v ObjectScript, ne `!`; `!` v legacy kódu = OR)

### Pojmenování
- [ ] Zkratky proměnných → čitelné názvy (čím globálnější, tím důležitější)
  - Parametry metod, klíče globálů: vždy celá slova
  - Lokální temp na jednom místě: rozumná zkratka OK
- [ ] `_` v názvech proměnných → camelCase (ObjectScript `_` = concat)
- [ ] Obecné iterátory (`mycnt`, `cnt`, `ii`, `jj`) → pojmenované podle toho, přes co se iteruje (`bodIdx`, `spojIdx`, `zastavkaIdx`, `hodinaIdx`)
- [ ] **Jazyk:** business logika česky (`pocetZastavek`, `aktualniSmer`, `casOdjezdu`), obecné programátorské vzory anglicky — "czechglish" (`GetData`, `SetTrasa`, `BuildOdjezdy`)

### Globály a reference
- [ ] `@indirection@(...)` → přímé reference `^||NazevGlobalu(...)`
- [ ] Tajemné číselné indexy `^||wg(1,2,3)` → pojmenované klíče `^||TiskMHD("trasa",SmerID,BodID)`

### Dot-notation
- [ ] `obj.KEY_NAME = v` → `obj.%Set("KEY_NAME", v)` (podtržítko v dot-notation nefunguje)
- [ ] Ověřit že jednoduché `obj.name = v` bez podtržítka zůstávají (ty fungují)

### Struktura metod
- [ ] Každá metoda s `ByRef sys`: `$$$Init` + `$$$FillParams` + `$$$CatchErrRest`
- [ ] Vstup přes `$g(^||ML("IN","key"))` — ne přes parametry metody
- [ ] Výstup přes `^||ML("OUT",...)` — pojmenované klíče nebo indexované řádky
- [ ] `$$$FillParams` místo předávání IdFa/dataDB mezi metodami

### Debugging
- [ ] `isZdendaPC` / `isDebug` bloky → smazat z produkčního kódu
- [ ] Pokud je potřeba debug výstup, použít `^jm` global — ale v produkci musí být čisté
- [ ] Žádné debug globals v commitnutém kódu

### Čištění
- [ ] Zakomentovaný kód → smazat (historie je v gitu)

### Výstupní formát
- [ ] JSON výstup přes `%DynamicObject` / `%DynamicArray` (ne delimited stringy)
- [ ] `$lb()` pro strukturované tuply v `^||ML("OUT",...)`
- [ ] Žádné `#`-delimited stringy, žádné `^`-delimited buňky

### Struktura kódu
- [ ] Preferovat třídy + ClassMethody — žádné routiny, makra ani jiné legacy konstrukty
- [ ] Čistý kód v třídách, logicky členěný do metod

### Klíčová slova
- [ ] Zkratky `s`, `d`, `i`, `q`, `f` jsou OK (firemní standard) — nepřepisovat na plné tvary

## Postup

1. **Přečti celý zdrojový soubor** — porozuměj business logice
2. **Identifikuj problémy** — vypiš seznam konkrétních nálezů podle checklistu
3. **Navrhni změny** — vypiš co a proč měníš, zeptej se uživatele jestli souhlasí
4. **Proveď refaktor** — aplikuj změny
5. **Shrnutí** — co se změnilo, co zůstalo, na co dát pozor

## Po dokončení

Připomeň uživateli:
- Nahrát třídu do systému (`do $system.OBJ.Load(...)` nebo přes Studio)
- Ověřit kompilaci v správném namespace
- Otestovat volání z C# strany (pokud existuje)
