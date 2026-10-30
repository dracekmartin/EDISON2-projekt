# Glob: CACHE/**

## 🚨 Nasazení `.cls`: editace nástrojem, ověření voláním serveru

`.cls` nasazuje na dev **PostToolUse hook** a ten reaguje **jen na Edit/Write**. Cokoli jiného
(skript z Bashe, `sed`, python) změní soubor v repu a **server nechá starý**.

- **Nikdy neupravuj `.cls` skriptem.** Ani „mechanicky" (mazání řádků, přidání dvou polí) - právě tam
  se to stalo dvakrát 4. 8. **Když se to stane, vrať soubor (`git checkout --`) a zapiš změnu znovu
  editačním nástrojem** - jen tak hook spustí harness a nasazení proběhne.
- ⚠️ **Ruční poslání cesty hooku NENASADÍ** (ověřeno 8. 9.). Volání
  `echo '{"tool_input":{"file_path":"<abs cesta>.cls"}}' | bash .claude/hooks/compile-objectscript.sh`
  skončí s kódem 0, bez chyby na stderr - a server dál vrací starou třídu. Do 8. 9. tady stálo jako
  doporučená záchrana; je to tedy **stejná past jako `cache_compile`** níž, jen tiší.
- ⚠️ **Skript může soubor rozbít tak, že se od té chvíle nenasadí NIC** (10. 9.). Python zápis
  s `encoding='utf-8-sig'` přidá souboru **BOM**, který v žádném `.cls` není; upload pak posílá
  soubor, jehož první řádek začíná neviditelným znakem, **překlad spadne a server drží poslední
  funkční verzi**. Navenek to vypadá jako „hook se nespustil": další editace nástrojem soubor
  poslušně nahrávají a taky se nepřeloží. Poznávací znak - `head -c 3 soubor.cls | xxd` vrátí
  `efbb bf` místo prvních písmen; ostatní `.cls` v repu BOM nemají.
- **`cache_compile` (MCP) to nespraví** a **lže uklidňujícím způsobem**: hledá zdroj pod
  `C:\DEV\EDISON`, takže u tohohle repa nahlásí `not-found-on-disk`, zkompiluje **starou serverovou
  kopii** a napíše „up-to-date".
- **Jak se to pozná:** testy jsou zelené a aplikace se chová podle starého kódu. Testy to nechytí -
  `%UnitTest` jede proti serveru, ale drift v parametrech/formulářích se v odtiscích neprojeví.
- **Ověřuj voláním, ne čtením.** „Zdroj v repu to má" ani „grep v nasazené metodě" nestačí:
  **zavolej agendu / dispatch a podívej se, co reálně vrátí** (`cache_query_cvar` na `GetParameters`,
  `Init`, …). To je jediná kontrola, která odpovídá na otázku „co uvidí klient".
  ⚠️ **Voláním ověř KAŽDOU třídu, kterou jsi změnil**, ne jednu za všechny - 5. 8. jsem ověřil
  legendu poznámek, ale ne seznam zastávek, a ten byl rozbitý (uživateli spadlo přidání prvku).

## 🚨 Uvozovka v textu se ZDVOJUJE

`"` uvnitř textového literálu ho **ukončí** - musí být `""`. Typicky to potká vysvětlivky:
`"… (výchozí „×")"` vypadá nevinně, ale ObjectScript to přečte jako konec textu, pak `)` mimo
literál a poslední `"` otevře nový → `<SYNTAX>` a **spadne to teprve uživateli při otevření
formuláře**. Správně: `"… (výchozí ""×"")"`.

Hlídá to `ObjectScriptSyntaxTests` (projede všechny `.cls` v repu). Nic jiného to nezachytí: rozbitá
třída se nasadí, .NET testy o Caché zdrojácích nevědí a `%UnitTest` kompiluje jen testovací package.
**Chyba se pak projeví jako Caché test padající bez hlášky** - na serveru zůstane stará verze třídy.

Kontrola má dvě části, protože chyba má dvě podoby:

1. **řádek končí uprostřed textu** (lichý počet uvozovek) - `s x = "neuzavřeno`
2. **uvozovek je sudě, ale text skončil uprostřed** - `d Neco(x, "„<" nevzniklo (" _ y _ ")")`.
   Tuhle půlku kontrola do 13. 8. neviděla. ⚠️ **Platí jen uvnitř závorek**: mimo ně je „text, mezera,
   slovo" legitimní podmíněný příkaz (`s:group'="" param.group = group`), takže tam se pravidlo
   neuplatní a chyba tohoto tvaru projde. V praxi vzniká v argumentu (hlášky testů, helpTexty).

## Caché namespace struktura

`ALVA.TemplateDesigner.*` se dělí **podle role třídy** do folder/namespace:

- **`ALVA.TemplateDesigner.API.*`** - obecná business logika TD projektu (cross-cutting). Příklady: `Sablony` (CRUD šablon), v budoucnu `UserPreferences`, `SectionTemplates`.
- **`ALVA.TemplateDesigner.Agendy.*`** - agenda implementations (data sources). Příklady: `BaseAgenda` (abstract base), `ZastavkoveJrMhd`, `Faktury`.
- **`ALVA.TemplateDesigner.Agendy.Jr.*`** - pomocné třídy sdílené agendami JŘ podle zdroje dat a druhu výstupu (`B56`, R8),
  jméno podle role (`DataAd`). Nejsou potomky `BaseAgenda`, registr je nenabízí; testy je čtou jako kód agend (`ZdrojeAgend`).
- **`ALVA.TemplateDesigner.EDList.*`** - dispatch třídy pro EDList komponentu (UI contract). Příklady: `Sablony` (backing pro CacheTemplatePickerDialog), `Instance` (backing pro InstancePickerDialog).
- **`ALVA.TemplateDesigner.EDInputForm.*`** - dispatch třídy pro EDInputForm komponentu (hlavní editační cesta elementů).
- **`ALVA.TemplateDesigner.EDInputGrid.*`** - dispatch třídy pro EDInputGrid (až bude potřeba).

**Klíčový princip:** folder/namespace 1:1 mapuje na **roli třídy**. Doménový název může opakovat napříč rolemi - `API.Sablony` (CRUD) vs `EDList.Sablony` (EDList dispatch nad šablonami) jsou jasně rozlišené namespace prefixem.

Folder = namespace prefix; class name musí odpovídat file path (InterSystems convention).

---

## Caché agendy - kontrakt `BaseAgenda`

Každá agenda je jedna Caché třída, **přímý potomek** `ALVA.TemplateDesigner.Agendy.BaseAgenda` - registr
hledá jen přímé potomky, sdílený kód patří do pomocných tříd mimo hierarchii (`architektonicka-rozhodnuti-agendy.md` R8).
Kontrakt, výchozí metody a parametry třídy s popisem drží `BaseAgenda.cls`; čti je tam, sem se kód neopisuje
(opsaný vzor tu do 30. 10. učil klíče, které návrhář nečte).

- **Povinné:** `GetSchema` (nabídka dat), `GetData` (stránky pro tisk), `GetMockData` (ukázka pro náhled bez instance;
  agendy JŘ posílají prázdnou stránku s výzvou vybrat linku).
- **Volitelné**, výchozí prázdný výsledek: `GetParameters`, `GetAvailableSkupiny`, `GetAvailableInstances`,
  `GetTableTemplates` / `GetTableTemplate` (předpřipravené tabulky - návrhář je nevolá, zrušit nebo napojit rozhodne `B56`).
- **Parametry třídy** - `AgendaVariantCode` (stabilní persistovaný klíč), `Agenda` / `Variant` s labely, `ElementKind`,
  `InstanceParamName`, `CustomColumns`, `VariantyTras`, `RozdilyVerzi`, `Released` (bez něj se agenda uživatelům nenabízí).
- **Každá metoda s `ByRef sys`** dodržuje pravidla 14-15 níž, nebo jen přesměruje na metodu jiné agendy
  (`q ##class(…).X(.sys)`); hlídá `OsetreniChybAgendTests`.
- **Vzorem jsou agendy JŘ** (`ZastavkoveJrAd`, `LinkovyJrAd`), ne Faktury - ukázka z počátku, která se možná smaže.

### Wire formáty

#### GetSchema - OUT[1]
```json
{
  "fields": [
    { "name": "CISLO_LINKY", "label": "Číslo linky",  "type": "number" },
    { "name": "NAZEV",       "label": "Název linky",   "type": "text"   }
  ],
  "fieldCollections": [
    {
      "name": "Zastávky", "collectionKey": "ZASTAVKY",
      "fields": [
        { "name": "POPIS",   "label": "Název zastávky", "type": "text" },
        { "name": "CAS_ODJ", "label": "Čas odjezdu",   "type": "time" }
      ]
    }
  ]
}
```

#### GetMockData - OUT[1] (náhled bez vybrané instance)
```json
{
  "values": {
    "CISLO_LINKY": "42",
    "NAZEV": "Linka Praha–Brno"
  },
  "rows": {
    "ZASTAVKY": [
      { "POPIS": "Praha hl.n.", "CAS_ODJ": "8:00" },
      { "POPIS": "Pardubice",   "CAS_ODJ": "9:15" }
    ],
    "SPOJE": [
      { "CISLO_SPOJE": "1001", "CAS": "8:00" }
    ]
  }
}
```

#### GetData - IN + OUT (print-time)

Na rozdíl od ostatních metod může `GetData` vrátit **více stránek** (např. zastávkový JŘ MHD = 1 stránka per zastávka × směr). Každá `OUT[n]` je **jedna kompletní samostatná stránka** ve stejném formátu jako `GetMockData`:

```
IN:      parametry závislé na agendě (číslo linky, datum, ...)
OUT[1] = { "values": {...}, "rows": { "ZASTAVKY": [...], "POZNAMKY": [...] } }   ← stránka 1
OUT[2] = { "values": {...}, "rows": {...} }                                       ← stránka 2
...
OUT[N] = { "values": {...}, "rows": {...} }                                       ← stránka N
```

Každý OUT node je vlastní `%DynamicObject.%ToJSON()` → na .NET straně `CacheAgendaProvider.GetData` iteruje všechny OUT klíče (kromě `"ERR"`) a parsuje každý jako samostatný `DataContext`. Výsledek je `List<DataContext>` předaný print enginu.

**Pro agendu s jediným výstupem** (např. jedna faktura) stačí zapsat pouze `OUT[1]` - C# strana přijímá 1..N stránek.

**Instance bez jediné stránky** (nestandardní data) vrací místo prázdna `OUT("DUVOD_PRAZDNE")` = text
pro uživatele: co v datech chybí a co s tím. Všechny agendy JŘ přes `BaseAgenda`: `ZacitDuvodyPrazdne`
na začátku GetData, `q ..PrazdnaStranka("…")` místo prázdné stránky, `VydatDuvodPrazdne` na konci.
Návrhář z něj udělá hlášku-problém „nemá data, nevytiskne se" (náhled, kontrola, tisk víc linek).
⚠️ **Ne chyba přes `$$$ThrowStatus`**: výjimka by při tisku víc linek zastavila i ty s daty.

**Data, která linku vytisknout nedovolí** (linka v JŘ není, spoj MHD bez kalendáře, turnusy nejdou
načíst), hlásí agenda přes `..ChybaDat("co je špatně. Co s tím.")` z `BaseAgenda`: tisk se zastaví
(rozhodnutí Jan 26. 10.), návrhář text ukáže jako informaci s číslem linky, ne jako chybu programu.
Pozná ji podle obecné chyby 5001 (`CacheAgendaProvider.ChybaDat`), takže **technický text do
`$$$GeneralError` nepiš** - uživatel ho uvidí doslova.

---

### Wire formát pravidlo (R3)

**Veškerá komunikace mezi Caché API a .NET stranou je čistě JSON.** Caché vždy zapisuje `obj.%ToJSON()` do `^||ML("OUT", n)`, .NET čte `result[key]?.Value?.ToString()` a parsuje přes `JsonNode.Parse` / `JsonSerializer.Deserialize`.

**Co se NEpoužívá:**
- `$lb()` / `$listbuild` na wire formátu - pouze pro interní working globals (`^||TiskMHD("trasa",...)`), nikdy v `^||ML("OUT",...)`
- Numerické subscripty na `^||ML("OUT",...)` jako strukturovaná data (jen jako sekvenční indexy pro multi-page u `GetData`)
- Pipe-delimited nebo `del`-delimited stringy
- Smíšený CVar+JSON hybrid

**Proč:** JSON je autoreferenční (struktura popsaná schématem), verzovatelný, debugovatelný v síťovém monitoru, a mapuje se 1:1 na `%DynamicObject` v Caché a `JsonNode` v .NET. Nové API metody musí tento vzor následovat.

**⚠️ Past: nový server-side parametr sestavy** (hodnotu zpracovává `GetData`, ne klientský filtr)
musí mít v `GetParameters` příznak `serverSide` (`s pX.serverSide = 1`, u `AddBoolParam` poslední
argument) - jinak jeho změna v UI nespustí načtení dat a klik je no-op. Klient žádný seznam
takových parametrů nedrží (od 21. 9.), ptá se definice parametru. Bool parametry se na wire
posílají jako `"true"/"false"` a server je čte tolerantně přes `$zcvt(...,"L")` (starší klienti
posílali `"True"`). Labely enum parametrů jsou ZAMČENÉ - persistuje se zobrazovaný text
(viz `ux-conventions §8`).

**Volba, která patří k prvku, ale server ji musí znát** (dnes `DEFINICE_SLOUPCU`, `JAZYK`,
`TURNUSY_KE_SPOJUM`) **není parametr sestavy**: skládá se do IN vždy **čerstvě ze šablony**
v `ComposeGetDataParams` (nikdy z persistovaných hodnot - třída chyb `B24`), příznak `serverSide`
nedostane (patří do `InfrastructureKeys` v `ServerSideParamsDriftTests`) a **re-fetch při její změně drží
`CurrentWireSnapshot`** - do něj se nová volba musí přidat, jinak zaškrtnutí nic neudělá, dokud
uživatel nezmáčkne obnovení dat. Poznávací znak: „odpověď na tuhle otázku dává šablona, ne uživatel
před tiskem."

**Parametr, který má účinek jen s určitým sloupcem prvku**, nese `visibleWhenColumn` = klíč sloupce
(dnes parametry variant tras → `CAS_JIZDY_VARIANTY`) a návrhář ho schová, když sloupec žádný prvek
šablony neukazuje - uživatel má vidět jen nastavení pro to, s čím v šabloně pracuje (rozhodnutí Jan
5. 10.). ⚠️ **Samostatný klíč, ne nový tvar `visibleWhen`**: starší klient by neznámou podmínku
vyhodnotil jako nesplněnou a parametr schoval natrvalo, `visibleWhenColumn` jen ignoruje. Klíč sloupce
musí prvky znát (`ParametrySloupcemVariantTests`), jinak by se parametr schoval navždy.

**Parametr, který se týká jen některých instancí**, nese `visibleWhenListedIn` = název hodnoty v datech,
kterou `GetData` posílá jako pole názvů parametrů týkajících se načtené instance; panel ukáže jen ty
(dnes typy provozu linkového JŘ MHD → `TYPY_PROVOZU_LINKY`: firma jich má desítky, linka pár).
⚠️ Seznam **nesmí záviset na hodnotách parametrů** - vypnutý typ by z panelu zmizel a nešel zapnout.
Bez dat, bez té hodnoty a v tiskovém dialogu (tiskne víc instancí) se nic neschovává.

---

### Prezentační nápovědy (R3) - kolik vzhledu smějí nést data

**Princip:** data říkají CO (odjezd 14:35), šablona říká JAK (font, barva). **Prezentační nápověda** je
úzká výjimka: klíč, kterým data legitimně nesou kus VZHLEDU, protože z nich vyplývá (nízkopodlažní spoj
zelený, mezisoučet tučný). Pravidla:

- **Vždy volitelná, nikdy povinná.** Element ji *smí* respektovat; element, který ji nezná, ji ignoruje
  (nesmí kvůli ní spadnout ani vyžadovat její přítomnost).
- **Vizuální význam sémantické úrovně vlastní ŠABLONA, ne data.** Data řeknou „zvýraznění úrovně 3",
  šablona určí, jak úroveň 3 vypadá. (Výjimka: přímé barvy níže - legacy parita.)

**Povolený seznam v1** (schválené, produkované/konzumované dnes):

| Klíč | Kde | Hodnota | Sémantika |
|---|---|---|---|
| `FORE_COLOR` | řádek kolekce / položka (odjezd, anotace, poznámka) | `#AARRGGBB`, prázdné = dědit | barva textu buňky/značky; přímá barva (legacy parita - JŘ ji už používá pro barevné spoje/poznámky) |
| `BACK_COLOR` | dtto | `#AARRGGBB`, prázdné = dědit | barva pozadí |

Prázdná hodnota = „element/šablona rozhodne" (nepřebíjí kaskádu stylů). Dnes je čte `ScheduleElementBase`
(→ DepartureGrid minuty/anotace, NotesLegend značky).

**Vědomě odložené na F1** (rozšíří se dle reálného RG usage - REPORTGENERATOR je hlavní konzument,
zatím nemigrovaný; navrhovat teď naslepo = špatný tvar): sémantická úroveň zvýraznění (`ZVYRAZNENI` 0–5,
šablona určí vzhled úrovní), typ řádku (data / mezisoučet / nadpis skupiny), „opakuj tento řádek na další
straně" (mezisoučty), „text přes celou šířku". Rezervujeme **princip + konvenci** (viz níže), ne konkrétní klíče.

**Konvence pro budoucí nápovědy:** klíč VELKÝMI písmeny (jako pole); **sémantické před syrovým vzhledem**
(radši „typ řádku = mezisoučet" než napevno barva); přímé barvy (`*_COLOR`) jen tam, kde to legacy vyžaduje.

### Evoluce kontraktu (R3) - jen aditivně

Datový kontrakt agend (schema, řádkové kolekce, prezentační nápovědy) se smí měnit **výhradně přidáváním**:

- **Přidat nový klíč / pole / nápovědu = OK.** Starší klient/element ho ignoruje (dopředná kompatibilita).
- **Změnit význam existujícího klíče nebo ho odebrat = ZAKÁZÁNO** bez migrace. (Stejná filozofie jako
  JSON šablony + `TemplateMigrator`.)

Důsledek: agenda může kdykoli obohatit výstup, aniž rozbije existující šablony a elementy. Migrovaný RG
(F1/F2) přidává prezentační nápovědy podle tohoto pravidla, ne ad hoc vlastními dialekty.

---

### Názvosloví - přehled pojmů API agendy

| Anglický název v kódu | Český ekvivalent | Business význam | Příklad |
|---|---|---|---|
| `GetSchema` | GetSchema | Vrátí **definici dostupných dat** agendy - co existuje jako pole, ne konkrétní hodnoty | `{ fields: [{name:"CISLO_LINKY",...}], fieldCollections: [...] }` |
| `GetTableTemplates` | GetTableTemplates | Seznam **předpřipravených tabulek**; volitelné, návrhář je dnes nevolá | `[{key:"ZAHLAVI_LINKY", label:"Záhlaví linky"}]` |
| `GetTableTemplate` | GetTableTemplate | Jedna **předpřipravená tabulka** - sloupce + předvyplněné řádky; volitelné, nevolá se | `{ columns:[...], rows:[{cells:[{content:"{CISLO_LINKY}"}]}] }` |
| `GetData` | GetData | Vrátí **reálná data z DB** pro tisk; volaná print enginem za běhu | `{ CISLO_LINKY:"42", NAZEV:"Praha–Brno" }` + kolekce řádků |
| `GetMockData` | GetMockData | Vrátí **ukázku pro náhled** bez vybrané instance | stejná struktura jako GetData; agendy JŘ posílají prázdnou stránku s výzvou vybrat linku |
| `fields` | skalární pole | Pole agendy kde každá hodnota existuje **právě jednou** na celou sestavu | `CISLO_LINKY`, `POPIS_DOPRAVCE`, `DATUM_PLATNOSTI_OD` |
| `fieldCollections` | tabulkové kolekce | Kolekce sad polí kde každá kolekce tvoří **seznam řádků** - zdroj dat pro Dynamic tabulku | kolekce `Zastávky` obsahuje pole `POPIS_ZASTAVKA`, `CAS_ODJEZDU`… |
| `collectionKey` | klíč kolekce | Identifikátor kolekce řádků v `fieldCollections`; designer ho zobrazuje jako volbu DataSource Dynamic tabulky | `"ZASTAVKY"`, `"SPOJE"` |
| `key` (v GetTableTemplates) | klíč šablony | Interní identifikátor Fixed šablony; předává se jako parametr do `GetTableTemplate` | `"ZAHLAVI_LINKY"`, `"PLATNOST_DOPRAVCE"` |

**Konvence klíčů šablon tabulek:** česky, velkými písmeny, podtržítko jako oddělovač, bez prefixu třídy/agendy. Příklady: `ZAHLAVI_LINKY`, `PLATNOST_DOPRAVCE`.

**Konvence collectionKey:** česky, velkými písmeny, množné číslo, podtržítko jako oddělovač. Příklady: `ZASTAVKY`, `SPOJE`, `POLOZKY_FAKTURY`.

---

### C# strana - registr a provider

- Agendy najde **registr na serveru** (`AgendyRegistr.ListAgendy` → `CacheAgendaRegistry`); konfigurace aplikace
  (`templatedesigner.appsettings.json`) je nedefinuje. `CacheSchemaWiring.TryCreate` nad nimi vytvoří `CacheAgendaProvider`,
  který volá metody agend přes MLRest a čte jejich JSON.
- Fallback nabídky dat: `CompositeDataSchemaProvider` (Caché primary + JSON ze `schemas/*.schema.json`).
- **Návrhář se o agendě rozhoduje podle registru, ne podle kódu agendy** (`B194g` R4, 30. 10.). Co návrhář
  potřebuje vědět, ohlásí agenda parametrem třídy, `ListAgendy` ho pošle (aditivně, s výchozí hodnotou pro starší
  server) a C# ho čte z `AgendaRegistryEntry`. Kód agendy v kódu návrháře zastaví `KodyAgendVKoduTests`;
  výjimky (převody starých šablon, výchozí agenda nové šablony) jsou v testu s důvodem.

---

### Přidání nové agendy - checklist

1. Vytvořit Caché třídu `ALVA.TemplateDesigner.Agendy.<Agenda> Extends ALVA.TemplateDesigner.Agendy.BaseAgenda` (přímý potomek)
   a nastavit parametry třídy (viz kontrakt výš); `AgendaVariantCode` se už nikdy nemění.
2. Implementovat `GetSchema`, `GetData`, `GetMockData`, volitelné podle potřeby; každou metodu podle pravidel 14-15
3. Nic neregistrovat ručně - agendu najde auto-discovery (`AgendyRegistr.ListAgendy`); v UI se nabízí až s `Released = 1`
4. Volitelně: přidat `schemas/<Agenda>.schema.json` jako offline fallback
   - `GetMockData` vrací JSON: `{ "values": { "POLE": "hodnota", ... }, "rows": { "KLIC": [ {...}, ... ] } }`
5. Linku s daty doplnit do `KontraktAgendTest.LinkyAgend` - test kontraktu na ní ověří, že data posílají
   každé nabízené pole. Položku dat mimo nabídku (čte ji prvek, ne uživatel) zapsat do `PolozkyMimoNabidku`
   s druhem; jinak test spadne, protože nevíme, kdo ji čte.
6. Agenda s prvky JŘ (`ElementKind` STOP / LINE) posílá položky, které prvky čtou, ve tvaru z `KontraktDat.cs`
   (integrační `KontraktDatAgendTests`, pre-push). Co agenda vědomě nemá, patří do výjimek testu s důvodem.

---

## Kódové konvence pro ObjectScript

### Operátory
1. **Vždy `&&` pro AND** - nikdy `&` (legacy syntaxe)
2. **Vždy `||` pro OR** - nikdy `!` (legacy OR syntaxe); negace v ObjectScript = `'` (apostrof), ne `!`

### Pojmenování
3. **Čitelné názvy - čím globálnější, tím důležitější.** Parametry metod, properties tříd, klíče globálů: vždy celá slova (`AutoPoznamkyRozdilTrasyAlgoritmus` místo `AutoPoznRozdTrsSpAlg`). Lokální temp proměnná použitá na jednom místě může být rozumná zkratka.
4. **Žádné `_` v názvech proměnných** - `_` je v ObjectScript operátor spojení stringů. Použít camelCase nebo celá slova.
5. **Jazyk: "czechglish"** - business logika česky (`pocetZastavek`, `aktualniSmer`, `casOdjezdu`), obecné programátorské vzory anglicky (`GetData`, `SetTrasa`, `BuildOdjezdy`). Ne `ZiskatData` ani `NastavitTrasu`. Upřesněno 30. 10. (`B194g` R7):
   - **Metoda, která něco dělá:** anglické sloveso + česká věc. Zavedená slovesa: `Get` (vrátí), `Build` (sestaví objekt
     pro výstup), `Add` (přidá do pole nebo objektu), `Load` (načte z globálu), `Write` (zapíše do OUT nebo globálu),
     `Format` (hodnota na text), `Parse`, `Convert`. Ne `PridatX`, `NactiX`, `SestavitX`, ani rozkazovací `SestavX`.
   - **Funkce pojmenovaná tím, co vrací:** česky bez slovesa (`NazevLinky`, `TextAtributu`, `PocetZastaveni`).
   - **Otázka ano/ne:** `Je…` / `Ma…` (zavedené, `JeAktualni`), ne `Is…`.
   - Platí pro **nové názvy**. Asi 76 stávajících metod s českým slovesem se přejmenuje, až se jejich kód v `B56`
     stěhuje do pomocných tříd, ne samostatnými commity jen kvůli názvu. Hlídá `NazvyMetodAgendTests`: nový název
     s českým slovesem neprojde a seznam stávajících se smí jen zmenšovat.
   - **Nikdy se nepřejmenovávají** třídy agend, `AgendaVariantCode`, klíče v datech a názvy parametrů sestavy - visí na nich
     uložené šablony a volby uživatelů.
6. **Žádné tajemné číselné indexy** v globálech - pojmenované klíče (`^||TiskMHD("trasa",SmerID,BodID)` místo `^||wg(1,2,3)`)
   - **Pracovní globál** má každá agenda svůj: `^||TiskAd` (zastávkový AD), `^||TiskMHD` (zastávkový MHD), `^||TiskKnizni`
     (linkový AD), `^||TiskKnizniMhd` (linkový MHD); stávající se nepřejmenovávají. Nová agenda: `^||Tisk` + jméno třídy
     agendy (`^||TiskFaktury`).
   - **Pomocná třída sdílená agendami** (`B56`, R8) do pracovního globálu agendy napřímo nesahá - každá agenda ho má jinak
     klíčovaný (`WMapa`, `legacy-nahrada.md §4`); data nebo jméno globálu dostane parametrem.

### Struktura kódu
7. **Žádná `@` indirection** - přímé reference (`^||TiskMHD(...)` místo `@wg@(...)`)
8. **Klíčová slova ve zkratkách** - `s`, `d`, `i`, `q`, `f` jsou OK (firemní standard)
9. **Konzistentní error handling** - `$$$CatchErrRest`; debug přes `^jm` jen při ladění, v produkci čisté
10. **`_` v dot-notation** - zakázáno (`obj.KEY_NAME` nefunguje); používat `obj.%Set("KEY_NAME", v)`
11. **Preferovat třídy + ClassMethody** - žádné routiny, makra ani jiné legacy konstrukty
12. **Komentáře jen uvnitř třídy** - žádné komentáře mimo blok `Class ... { }` (Caché API pro vyhledávání tříd je ignoruje/ořízne)

### Parametry a makra
13. **`$$$FillParams` místo předávání IdFa/dataDB** - pokud třída Include GENERAL a metoda má `ByRef sys`, makro `$$$FillParams` zpřístupní `IdFa`, `dataDB`, `del`, `userID`, `GUID` atd. jako lokální proměnné. Nepředávat je jako parametry mezi metodami - každá metoda si zavolá `$$$FillParams` sama.

### Návratové hodnoty a error handling
14. **Nepoužívat `$$$Init`** - TD konvence. ⚠️ **Pozor na odchylku:** EDISON-wide skeleton (`docs/mlrest.md §2.2.4` + root `CLAUDE.md`) `$$$Init` **standardně má** (inicializuje `ret`+`err`, konec `ret ret`); TD ho vědomě NEpoužívá a končí `ret $$$OK`. Pokud kopíruješ skeleton z `docs/mlrest.md`, `$$$Init` vynech. **Důsledek:** protože `$$$Init` odpadá, `err` (které čte `$$$CatchErrRest`) si musí inicializovat metoda sama - viz rule 15.
15. **Návratový vzor pro API metody** (s `ByRef sys`): `s err = ""` na prvním řádku, pak `try { $$$FillParams ... } catch (ex) { $$$CatchErrRest } ret $$$OK`. **`s err = ""` je POVINNÉ** - `$$$FillParams` `err` nenastavuje, `$$$CatchErrRest` ho čte; bez initu spadne catch při reálné chybě na sekundární `<UNDEFINED>err` místo čisté hlášky. (Nahrazuje to, co dřív dělal `$$$Init`.)
16. **Helper metody bez `ByRef sys`** - nemusí mít try/catch ani vracet %Status, pokud jsou volány jen z API metod které catch mají.
