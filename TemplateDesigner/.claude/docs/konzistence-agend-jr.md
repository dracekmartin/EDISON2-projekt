# Konzistence agend jízdních řádů - druhé kolo (`B194`)

> Jednorázová analýza 23. 10. 2026: nezávislá revize zastávkového a linkového JŘ (AD i MHD) po uzavření
> `B193`, s důrazem na jednotné názvosloví a ovládání. Šest oblastí (parametry, nabídka dat, prvky,
> aplikace, tisk, architektura), každý nález **přeověřený proti kódu po commitu `3139b27d`**.
> **Smazat po dokončení `B194`** - trvalá pravidla do `cache-api.md` / `ux-conventions.md`,
> kandidáti sloučení kódu do [`legacy-nahrada.md §4`](legacy-nahrada.md).
> Rozdílový JŘ je v rozsahu jen tam, kde ho rozbila změna společného kódu (`C1`).

**Roadmapa** má každou položku jako jeden řádek pod týmž ID: §1-§7 tady = `B194a`-`B194g` tam, §9 = `B194h`.
Hotovou položku smaž tady i v roadmapě (do `roadmap-archive.md`).

**Jak číst položky.** `[S/M/L]` velikost · **tisk** = oprava změní vytištěný výstup vydané agendy ·
**Jan** = potřebuje rozhodnutí (jinak zjevná oprava) · *ověřeno:* kód / server (čtecí volání na dev
serveru) / neověřeno. Odkazy jsou jméno metody + řádek k 23. 10.; řádky se posouvají, metoda platí.

**Co je zamčené:** popisky voleb enum parametrů (ukládá se zobrazený text, `ux-conventions §8`), klíče
polí na wire a klíče vlastností v šablonách (jen přidávat), kódy agend. Popisky polí, parametrů,
skupin a formulářů se mění volně - bere je návrhář ze serveru.

## 1. Chyby k opravě

Všechny hotové (C1-C12, 23.-26. 10.) - viz `roadmap-archive.md` a changelog.

## 2. Tisk - rozdíly mezi agendami

Všechno hotové nebo rozhodnuté (27. 10.) - viz `roadmap-archive.md`, `B194` T1-T14 (T7 vědomý rozdíl).

## 3. Nabídka dat a parametry sestavy

Zbývající `P2` (popisky voleb rozdělení sloupců) čeká v backlogu; detail přesunut do
[`legacy-nahrada.md §4`](legacy-nahrada.md) (28. 10.).

## 4. Prvky a formuláře

Všechno hotové (28. 10.) - viz `roadmap-archive.md`, `B194` E3, E3b, E3c a E13a-E13i. Trvalá pravidla jsou
v `ux-conventions §4` (kostra formuláře) a `§8` (popisky), záložka Sloupce v `mvvm-patterns.md`.
Co po tom našla revize, je v §9.

## 5. Aplikace a dokumentace

Vše hotové nebo rozhodnuté (27. 10.) - viz `roadmap-archive.md`, `B194` A1-A5.

## 6. Pojistky - testy, které by tyto nálezy chytily

Všechny hotové 30. 10. (G1-G8, hlídání linek z testů, zbytky dat vývěsky AD) - co hlídají a proč,
je v `roadmap-archive.md` u `B194` a v changelogu 2026-10-29 a 2026-10-30.

## 7. Architektura před sloučením kódu (`B56`)

R1 (pomocné třídy mimo hierarchii agend, `architektonicka-rozhodnuti-agendy.md` R8) a R2 (opsané pod jiným
jménem) jsou hotové; R2, připravenost na `B56` a postup jsou v [`legacy-nahrada.md §4`](legacy-nahrada.md).

- **R4 Schopnosti agend natvrdo v C#** [S-M] **Jan**. `JeRozdilovyJr`, `CreateSampleTemplate` (`"LINKOVY_JR_AD"`),
  `ScheduleImportTypes.ForAgenda` (podle prefixu kódu; enum `ZastavkovyJrMhd` platí i pro AD).
  `ShouldPrefillDepartureColumns` už jde přes příznak `CustomColumns` (28. 10.). Návrh:
  příznaky jako parametry třídy v `BaseAgenda`, přes `ListAgendy` do `AgendaRegistryEntry` (aditivně).
- **R5 Parametry vývěsek opsané dvakrát** [S-M]. `ZastavkoveJrAd.AddStandardJRParameters` ×
  `ZastavkoveJrMhd.AddStandardJRParameters`; dnes shodné (ověřeno voláním). První krok `B56` podle vzoru
  `LinkovyJrAd.Parametr*`, před doslovnými buildery. Shodu hlídá `KontraktAgendTest.TestSdileneParametryShodne`
  (`G2`); přehled sdílených parametrů vypíše `KontraktAgendTest.Prehled` - 29. 10.: 20 sdílených, 16 shodných,
  4 vědomé rozdíly (tvar názvu zastávky, počet stran, zastávky bez zastavení, skupina barev spojů).
- **R7 Názvosloví v kódu** [S]. `^||TiskKnizni*`, infinitivy v `ZastavkoveJrMhd` (`PripravitTrasu`,
  `SestavitStranky`) proti `cache-api.md` pravidlu 5. Kanon jen pro nové názvy, interní sjednotit při `B56`,
  třídy nepřejmenovávat.

## 8. Co je v pořádku

- **Vědomě jinak (rozhodnutí Jan 26. 10.):** `T5`, `T6`, `T4c`, `T8`, `T10b` - přesunuto do
  [`legacy-nahrada.md §4`](legacy-nahrada.md) (28. 10.), odstavec „Vědomé rozdíly mezi agendami JŘ".

- Parametry: „Obecné" první ve všech čtyřech agendách, ostatní skupiny nesou název prvku (od 26. 10. i typy provozu, `SkupinyParametruTest`);
  „Automaticky (dle linky)" jednotně jako první volba; společné definice (tvar názvu, směr, počet stran,
  zvýraznění zastávky) mají stejný klíč i popisek; `serverSide` sedí oběma směry.
- Data: obě čísla linky ve všech agendách pod stejným klíčem i popiskem; kolekce `ZASTAVKY`, `SKUPINY_DNU`,
  `POZNAMKY` shodné; datum dd.mm.rrrr a časy HH:MM jednotně; každé pole schématu agenda opravdu posílá
  (od 30. 10. hlídá `KontraktAgendTest.TestDataOdpovidajiNabidce`).
- Tisk: texty dnů v týdnu a kroužkové číslice, piktogram bezbariéru, kola a místenka v AD, kategorie
  poznámek linky jsou všude stejné.
- Aplikace: paleta a nabídka prvků se řídí druhem agendy z registru; tiskový dialog je jeden obecný;
  Storno/OK ze sdílených textů; dlouhá pomlčka v UI textech není (kromě `T9`).
- Architektura: C# je až na `R4` agendově nezávislý; drift testy čtou `.cls` offline a novou agendu
  zahrnou samy; vzory k následování existují (`LinkovyJrAd.Parametr*`, „jen přidávat" s testem
  vyřazených polí, oracle se starým modulem u linkového AD, linkového MHD a vývěsek).

## 9. Dodělávky po sjednocení formulářů (revize 28. 10.)

Nezávislá revize `B194d`: formuláře šesti tabulek a Textu volané na serveru (server = repo do posledního pole),
kód a testy (build bez chyb, 2492 testů zelených v čisté kopii `7dfb5b1a`) a dokumentace.
Směr potvrzený: tabulky mají jednu kostru, stejné sady stylu a stejné popisky; „barva" najde v každém
formuláři všechna barevná pole.
H1, H2, H4, H5, H7, H8, H14, H15 a dokumentační část H13 jsou hotové (changelog 2026-10-28 (18)-(21)),
H3 rozhodnutý (nechat, archiv), H6, H9, H10 a H11 hotové (changelog 2026-10-28 (22)-(26)), H12 v backlogu (roadmapa),
H13 rozhodnutý (Novinky beze změny), H16 smazaný, H17 hotový (changelog 2026-10-28 (27)). `B194h` je uzavřený.

Popisky jednoduchých prvků z téže revize jsou hotové (changelog 2026-10-28 (28)).

**Soubor smazat po R4-R7** (zbytek §7) - připravenost na `B56`, R2, P2 a vědomé rozdíly už jsou
v `legacy-nahrada.md §4` (30. 10.).
