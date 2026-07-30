---
description: Refaktoruj existující Caché třídu v CACHE/ podle moderních konvencí
argument-hint: [cesta k .cls souboru v CACHE/]
allowed-tools: Read, Edit, Write, Bash(grep *), Bash(ls *), Glob, Grep
---

Refaktoruj existující Caché třídu "$ARGUMENTS" na místě - zachovej business logiku, uprav syntaxi a konvence.

## Kontext - přečti před zahájením

Konvence a pravidla: !`cat .claude/rules/cache-api.md`
Business logika (JŘ kontext): !`cat .claude/docs/cache-business-logic.md`
Referenční implementace (vzor): !`cat CACHE/ALVA/TemplateDesigner/API/ZastavkoveJrMhdAPI.cls`
Abstraktní základ: !`cat CACHE/ALVA/TemplateDesigner/API/AgendaAPI.cls`

Dostupné .cls soubory: !`ls CACHE/ALVA/TemplateDesigner/API/`

**PRVNÍ KROK:** Najdi a přečti soubor "$ARGUMENTS" pomocí Glob a Read nástrojů.
Uživatel může zadat: přesnou cestu (`CACHE/ALVA/.../Foo.cls`), jen název (`Foo.cls`), nebo tečkovou notaci (`ALVA.TemplateDesigner.API.Foo.cls`).
Použij `Glob("CACHE/**/$ARGUMENTS")` nebo `Glob("CACHE/**/*.cls")` pro nalezení souboru. Pokud nenajdeš, vypiš dostupné soubory a zeptej se uživatele.

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
- [ ] **Jazyk:** business logika česky (`pocetZastavek`, `aktualniSmer`, `casOdjezdu`), obecné programátorské vzory anglicky - "czechglish" (`GetData`, `SetTrasa`, `BuildOdjezdy`)

### Globály a reference
- [ ] `@indirection@(...)` → přímé reference `^||NazevGlobalu(...)`
- [ ] Tajemné číselné indexy `^||wg(1,2,3)` → pojmenované klíče `^||TiskMHD("trasa",SmerID,BodID)`
- [ ] **SMOID** - zastaralý koncept. Pokud kód používá `SMOID` nebo `SMO`, zkonzultovat s uživatelem a nahradit přímým `TypProvozuID`

### Dot-notation
- [ ] `obj.KEY_NAME = v` → `obj.%Set("KEY_NAME", v)` (podtržítko v dot-notation nefunguje)
- [ ] Ověřit že jednoduché `obj.name = v` bez podtržítka zůstávají (ty fungují)

### Struktura metod
- [ ] API metody (s `ByRef sys`): `s err = ""` + `$$$FillParams` + `$$$CatchErrRest` + `ret $$$OK` (bez `$$$Init`, ne `ret 1`; `s err = ""` na prvním řádku je povinné - `$$$CatchErrRest` čte `err`, `$$$FillParams` ho nenastavuje)
- [ ] Vstup přes `$g(^||ML("IN","key"))` - ne přes parametry metody
- [ ] Výstup přes `^||ML("OUT",...)` - pojmenované klíče nebo indexované řádky
- [ ] `$$$FillParams` místo předávání IdFa/dataDB mezi metodami

### Debugging
- [ ] `isZdendaPC` / `isDebug` bloky → smazat z produkčního kódu
- [ ] Pokud je potřeba debug výstup, použít `^jm` global - ale v produkci musí být čisté
- [ ] Žádné debug globals v commitnutém kódu

### Kvalita kódu (priorita = udržitelnost, ne performance)
- [ ] **Duplikace** - opakující se bloky extrahovat do helper metod
- [ ] **Mrtvý kód** - nepoužívané metody, proměnné, větve podmínek → smazat
- [ ] **Jasnost** - složité podmínky rozdělit do pojmenovaných proměnných; magické konstanty pojmenovat
- [ ] **Dlouhé metody** - rozdělit na menší s jasnými názvy (každá dělá jednu věc)
- [ ] **Zbytečná složitost** - zjednodušit kde to jde, i za cenu mírně delšího kódu

### Komentáře
- [ ] Komentovat **neočividná business rozhodnutí** - proč se něco dělá takhle, ne jinak (ne co kód dělá, ale proč)
- [ ] **KRITICKÉ: Komentáře VÝHRADNĚ uvnitř třídy** - NIC mimo blok `Class ... { }`. Žádné `///` komentáře před `Class`, žádné `//` komentáře za uzavírací `}`. Caché API pro vyhledávání tříd je ignoruje/ořízne. Komentáře k třídě patří dovnitř bloku jako `///` na metodách nebo `//` v kódu.
- [ ] **Víceřádkové komentáře mimo metody** (class-level dokumentace) psát jako `///` blok bez prázdných řádků - Caché/IRIS u `//` komentářů vkládá prázdné řádky mezi každý řádek při formátování. `///` blok zůstane souvislý.

### Čištění
- [ ] Zakomentovaný kód → smazat (historie je v gitu)

### Výstupní formát
- [ ] JSON výstup přes `%DynamicObject` / `%DynamicArray` (ne delimited stringy)
- [ ] `$lb()` pro strukturované tuply v `^||ML("OUT",...)`
- [ ] Žádné `#`-delimited stringy, žádné `^`-delimited buňky

### Struktura kódu
- [ ] Preferovat třídy + ClassMethody - žádné routiny, makra ani jiné legacy konstrukty
- [ ] Čistý kód v třídách, logicky členěný do metod

### Klíčová slova
- [ ] Zkratky `s`, `d`, `i`, `q`, `f` jsou OK (firemní standard) - nepřepisovat na plné tvary

### For cykly - povinný formát
- [ ] Iterující proměnná na **vlastním řádku**, pojmenovaná podle toho, přes co se iteruje (žádné `cnt`, `ii`, `jj`)
- [ ] `f {` na **vlastním řádku**
- [ ] Iterace + ukončovací podmínka na **jednom řádku** přes **postcondition** `q:var=""`

```objectscript
// ✅ Správně
s BodID = ""
f {
    s BodID = $o(^||TiskMHD("trasa",SmerID,BodID)) q:BodID=""
    i BodID = "slouceni" continue
    // ... logika ...
}

// ❌ Špatně
s BodID = "" f {
    s BodID = $o(^||TiskMHD("trasa",SmerID,BodID)) i BodID = "" q
```

### **HLAVNÍ PRIORITA: Klarita a udržitelnost kódu**

Cílem refaktoringu je **maximální čitelnost a udržitelnost**. Vše ostatní (performance, stručnost) je sekundární. Pojmenování proměnných, metod a klíčů je nejdůležitější aspekt - kód se čte 10× víc než se píše. Pokud je kód čitelný a pochopitelný i pro někoho, kdo ho vidí poprvé, refaktoring splnil svůj účel.

## Postup

1. **Přečti celý zdrojový soubor** - porozuměj business logice
2. **Identifikuj problémy** - vypiš seznam konkrétních nálezů podle checklistu
3. **Navrhni změny** - vypiš co a proč měníš, zeptej se uživatele jestli souhlasí
4. **Proveď refaktor** - aplikuj změny
5. **Finální validace** - po provedení změn přečti výsledný soubor a projdi KAŽDÝ bod checklistu znovu. Ověř zejména:
   - Žádné komentáře mimo `Class ... { }` (ani `///` před Class, ani `//` za uzavírací `}`)
   - Žádný `$$$Init` - místo něj `s err = ""` na prvním řádku + vracet `$$$OK`
   - Žádné `IdFa`/`dataDB` v parametrech helper metod (mají mít `ByRef sys` + `$$$FillParams`)
   - Žádné mrtvé proměnné (přiřazené ale nepřečtené)
   Pokud najdeš problém, oprav ho PŘED shrnutím.
6. **Shrnutí** - co se změnilo, co zůstalo, na co dát pozor

## Po dokončení

Připomeň uživateli:
- Otestovat v aplikaci, že po refaktoringu vše funguje jako předtím
