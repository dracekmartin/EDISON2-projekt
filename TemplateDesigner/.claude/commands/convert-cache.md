---
description: Přepiš legacy Caché kód do nové třídy v moderní syntaxi
argument-hint: [cesta k legacy souboru]
allowed-tools: Read, Edit, Write, Bash(grep *), Bash(ls *), Glob, Grep
---

Přepiš legacy Caché kód "$ARGUMENTS" do nové třídy v CACHE/ v moderní syntaxi.
Zachovej business logiku, kompletně přepiš strukturu a syntaxi.

## Kontext — přečti před zahájením

Konvence a pravidla: !`cat .claude/rules/cache-api.md`
Business logika (JŘ kontext): !`cat .claude/rules/cache-business-logic.md`
Referenční implementace (vzor): !`cat CACHE/ALVA/TemplateDesigner/API/ZastavkoveJrMhdAPI.cls`
Abstraktní základ: !`cat CACHE/ALVA/TemplateDesigner/API/AgendaAPI.cls`
Existující třídy: !`ls CACHE/ALVA/TemplateDesigner/API/`

**PRVNÍ KROK:** Najdi a přečti legacy soubor "$ARGUMENTS" pomocí Glob a Read nástrojů.
Uživatel může zadat: přesnou cestu, jen název souboru (`Foo.cls`), nebo tečkovou notaci (`ALVA.TemplateDesigner.API.Foo.cls`).
Použij `Glob("CACHE/**/$ARGUMENTS")` nebo `Glob("CACHE/**/*.cls")` pro nalezení. Pokud argument není .cls soubor, hledej i mimo CACHE/. Pokud nenajdeš, vypiš dostupné soubory a zeptej se uživatele.

## Co udělat

1. Přečti a pochop business logiku legacy kódu
2. Vytvoř novou třídu v `CACHE/ALVA/TemplateDesigner/API/` (nebo jiné vhodné umístění)
3. Přepiš logiku do moderní syntaxe podle checklistu níže
4. Zeptej se uživatele na název třídy a umístění, pokud není jasné

## Konverzní checklist

### Co zachovat
- [ ] Veškerá business logika (výpočty, dotazy, podmínky)
- [ ] Funkční chování — výstup musí být identický

### Co změnit

#### Operátory
- [ ] `&` (AND) → `&&`
- [ ] `!` (OR) → `||` (pozor: `'` je negace v ObjectScript, ne `!`; `!` v legacy kódu = OR)

#### Pojmenování
- [ ] Zkratky proměnných → čitelné názvy (čím globálnější, tím důležitější)
- [ ] `_` v názvech proměnných → camelCase (ObjectScript `_` = concat)
- [ ] Obecné iterátory (`mycnt`, `cnt`, `ii`, `jj`) → pojmenované (`bodIdx`, `spojIdx`, `zastavkaIdx`)
- [ ] **Jazyk:** business logika česky (`pocetZastavek`, `aktualniSmer`), obecné programátorské vzory anglicky — "czechglish" (`GetData`, `SetTrasa`, `BuildOdjezdy`, ne `ZiskatData`, `NastavitTrasu`)

#### Globály a reference
- [ ] `@indirection@(...)` → přímé reference `^||NazevGlobalu(...)`
- [ ] Tajemné číselné indexy → pojmenované klíče
- [ ] Debug globals (`^jm`, `^zz`) → smazat (v produkci čisté; debug přes `^jm` jen při ladění)
- [ ] **SMOID** — zastaralý koncept. Pokud legacy kód používá `SMOID` nebo `SMO`, zkonzultovat s uživatelem a nahradit přímým `TypProvozuID`

#### Dot-notation
- [ ] `obj.KEY_NAME = v` → `obj.%Set("KEY_NAME", v)` (podtržítko v dot-notation nefunguje)

#### Struktura
- [ ] Routiny, makra, legacy konstrukty → třídy + ClassMethody
- [ ] API metody (s `ByRef sys`): `$$$FillParams` + `$$$CatchErrRest` + `ret $$$OK` (ne `ret 1`, ne `$$$Init`)
- [ ] Vstup přes `$g(^||ML("IN","key"))`, výstup přes `^||ML("OUT",...)`
- [ ] `$$$FillParams` místo předávání IdFa/dataDB mezi metodami
- [ ] Logicky členěný do metod — ne jedna mega-metoda

#### Výstupní formát
- [ ] JSON výstup přes `%DynamicObject` / `%DynamicArray` (ne delimited stringy)
- [ ] `$lb()` pro strukturované tuply
- [ ] Žádné `#`/`^`/`&`-delimited stringy

#### Komentáře
- [ ] Komentovat **neočividná business rozhodnutí** — proč se něco dělá takhle, ne jinak
- [ ] **KRITICKÉ: Komentáře VÝHRADNĚ uvnitř třídy** — NIC mimo blok `Class ... { }`. Žádné `///` komentáře před `Class`, žádné `//` komentáře za uzavírací `}`. Komentáře k třídě patří dovnitř bloku.
- [ ] **Víceřádkové komentáře mimo metody** (class-level dokumentace) psát jako `///` blok bez prázdných řádků — Caché/IRIS u `//` komentářů vkládá prázdné řádky mezi každý řádek při formátování. `///` blok zůstane souvislý.

#### Kvalita kódu (priorita = udržitelnost, ne performance)
- [ ] **Duplikace** — opakující se bloky extrahovat do helper metod
- [ ] **Mrtvý kód** — nepoužívané metody, proměnné, větve podmínek → nepřebírat
- [ ] **Jasnost** — složité podmínky rozdělit do pojmenovaných proměnných; magické konstanty pojmenovat
- [ ] **Dlouhé metody** — rozdělit na menší s jasnými názvy (každá dělá jednu věc)
- [ ] **Zbytečná složitost** — zjednodušit kde to jde, i za cenu mírně delšího kódu

#### Čištění
- [ ] Zakomentovaný kód → smazat
- [ ] `isZdendaPC` / `isDebug` bloky → smazat
- [ ] Zkratky `s`, `d`, `i`, `q`, `f` jsou OK (firemní standard)

#### For cykly — povinný formát
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

#### **HLAVNÍ PRIORITA: Klarita a udržitelnost kódu**

Cílem konverze je **maximální čitelnost a udržitelnost**. Vše ostatní (performance, stručnost) je sekundární. Pojmenování proměnných, metod a klíčů je nejdůležitější aspekt — kód se čte 10× víc než se píše. Pokud je kód čitelný a pochopitelný i pro někoho, kdo ho vidí poprvé, konverze splnila svůj účel.

## Postup

1. **Přečti celý legacy soubor** — porozuměj business logice, identifikuj vstupy/výstupy
2. **Navrhni strukturu nové třídy** — název, metody, rozdělení logiky; zeptej se uživatele
3. **Vypiš rozdíly** — co se zachová, co se přepíše, co se smaže
4. **Vytvoř novou třídu** — kompletní .cls soubor
5. **Finální validace** — po vytvoření přečti výsledný soubor a projdi KAŽDÝ bod checklistu. Ověř zejména:
   - Žádné komentáře mimo `Class ... { }` (ani `///` před Class, ani `//` za `}`)
   - Žádný `$$$Init` — vracet `$$$OK`
   - Žádné `IdFa`/`dataDB` v parametrech helper metod
   - Žádné mrtvé proměnné
   Pokud najdeš problém, oprav ho PŘED shrnutím.
6. **Shrnutí** — co je nové, co odpovídá legacy, na co dát pozor při testování

## Po dokončení

Připomeň uživateli:
- Otestovat v aplikaci, že po konverzi vše funguje jako předtím
