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

Legacy zdrojový kód: !`cat $ARGUMENTS`

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

#### Dot-notation
- [ ] `obj.KEY_NAME = v` → `obj.%Set("KEY_NAME", v)` (podtržítko v dot-notation nefunguje)

#### Struktura
- [ ] Routiny, makra, legacy konstrukty → třídy + ClassMethody
- [ ] Každá metoda s `ByRef sys`: `$$$Init` + `$$$FillParams` + `$$$CatchErrRest`
- [ ] Vstup přes `$g(^||ML("IN","key"))`, výstup přes `^||ML("OUT",...)`
- [ ] `$$$FillParams` místo předávání IdFa/dataDB mezi metodami
- [ ] Logicky členěný do metod — ne jedna mega-metoda

#### Výstupní formát
- [ ] JSON výstup přes `%DynamicObject` / `%DynamicArray` (ne delimited stringy)
- [ ] `$lb()` pro strukturované tuply
- [ ] Žádné `#`/`^`/`&`-delimited stringy

#### Čištění
- [ ] Zakomentovaný kód → smazat
- [ ] `isZdendaPC` / `isDebug` bloky → smazat
- [ ] Zkratky `s`, `d`, `i`, `q`, `f` jsou OK (firemní standard)

## Postup

1. **Přečti celý legacy soubor** — porozuměj business logice, identifikuj vstupy/výstupy
2. **Navrhni strukturu nové třídy** — název, metody, rozdělení logiky; zeptej se uživatele
3. **Vypiš rozdíly** — co se zachová, co se přepíše, co se smaže
4. **Vytvoř novou třídu** — kompletní .cls soubor
5. **Shrnutí** — co je nové, co odpovídá legacy, na co dát pozor při testování

## Po dokončení

Připomeň uživateli:
- Nahrát třídu do systému (`do $system.OBJ.Load(...)` nebo přes Studio)
- Ověřit kompilaci v správném namespace
- Porovnat výstup nové třídy s legacy — musí být funkčně identický
- Pokud třída má C# protějšek, aktualizovat appsettings.json / CacheAgendaProvider
