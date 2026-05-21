# Plán migrace editorů TemplateDesigneru na EDInputForm

> Konsolidace architektonické diskuze z 2026-05-18. Slouží jako trvalá reference pro budoucí implementaci.
> Související dokumenty: [`../../DOCS/01-wire-formats.md`](../../DOCS/01-wire-formats.md), [`legacy-component-research.md`](legacy-component-research.md).

---

## 1. Cíl — proč o tom mluvíme

V TemplateDesigneru jsou dnes 4 dialogy s vlastním systémem editace vlastností (`ElementPropertiesWindow`, `TableEditorDialog`, `StopColumnsEditorDialog`, `DepartureGroupEditorDialog`). Definice formulářů žije v C# přes `PropertyDescriptor` v každé element třídě.

**Cíl migrace:** definice editovacích formulářů přesunout do Caché tak, aby:
- byla konzistentní se standardním EDISON vzorem (BIG / EDInputForm s definicí na serveru),
- umožňovala **per-customer patche** (změna labelu, skrytí pole, jiný validační rozsah) **bez rebuildu .exe**,
- využívala stejnou generickou komponentu pro vykreslení jako ostatní formuláře v aplikaci.

---

## 2. Dva různé koncepty, které je nutné neplést dohromady

V diskuzi se opakovaně mlhalo, co se vlastně synchronizuje. Důsledně oddělujeme:

### 2.1 Entita (doménový objekt)
**Co to je:** datová struktura objektu — co existuje za vlastnosti, jak se to ukládá, jak se to v paměti chová.

**Příklady:**
- `Linka` — má CisloLinky, NazevLinky, PlatiOd, PlatiDo, …
- `TextElement` — má Text, FontSize, TextColor, X, Y, Width, Height, Bold, Italic, …

### 2.2 Definice editoru (formuláře)
**Co to je:** UI metadata pro úpravu entity — která pole zobrazit, jakými widgety, jakými labely, v jakém pořadí, s jakou validací, ve kterých skupinách.

**Příklady:**
- „Pro úpravu TextElementu zobraz pole Text (TextBox), FontSize (NumericBox, rozsah 4–200, label Velikost), Bold (CheckBox), TextColor (color picker)"

**Tyto dvě věci se občas pletou**, protože každé pole formuláře obvykle odpovídá vlastnosti entity. Ale jsou to **odlišné koncepty**.

---

## 3. Jak to dělal starý systém (BIG / BaseInputGrid)

| Koncept | Kde žije v legacy |
|---|---|
| **Entita Linka** | Caché globál (`^ALVA.MHD.LSO(IdFa, 1, "DTA", LinkaID, ...)`) — záznam přímo v globálu |
| **Definice formuláře pro Linku** | Caché třída (Linka BIG editor class) — popis jakých polí |
| **C# strana** | Tenká vrstva — dostává obojí z Caché jako neztypovaný slovník hodnot, jen vykresluje |

V legacy **obě věci žijí v Caché**. C# strana o nich nedrží paralelní pravdu, takže drift problém neexistuje. Tradiční EDISON modul (faktura, zákazník, číselník) je primárně práce nad záznamy v globálu — C# je v podstatě jen tlumočník mezi UI a Caché.

### 3.1 BIG měl tři módy aktivace

1. **Package mode** — definice přijde z Caché (standardní, 99 % použití)
2. **Local Groups mode** — definice se sestaví v C# kódu a předá BIGu (okrajové)
3. **Adaptor mode** — definice odvozená reflexí nad C# objektem (okrajové)

V této migraci nás zajímá **Package mode** — to je vzor, který chceme držet.

### 3.2 Multi-tab v BIG

Legacy řešení: jeden BIG = jeden formulář (`Group → Field`). Pokud bylo potřeba více tabů, použila se WinForms `TabControl` komponenta s několika BIGy uvnitř. **Nesázelo se na vnořené taby uvnitř BIGu.**

To je správný vzor, který přebereme: **EDInputForm zůstane jednoduchá `Group → Field`. Pro multi-tab použijeme WPF `<TabControl>` v okně + na každém tabu vlastní EDInputForm s vlastní Caché backing třídou.**

---

## 4. Proč je TemplateDesigner odlišný od typického EDISON modulu

V tradičním modulu (Linka):
- Entita Linka je **záznam v Caché globálu**, nikdy v C# typu
- C# je tenká renderovací vrstva nad slovníkem hodnot z Caché
- Žádná paralelní pravda → žádný drift

V TemplateDesigneru:
- TextElement je **objekt v paměti aplikace** s typovými vlastnostmi
- Mezi „otevři šablonu" a „ulož šablonu" má C# **bohaté chování s elementem**:
  - `Render(canvas)` — 30× za sekundu při interakci kreslí podle hodnot
  - `Clone()` — Ctrl+D vytváří hlubokou kopii
  - `ApplyScale(0.5f)` — proporcionálně zmenšuje
  - `CalculateHeightFromData(data)` — dynamická výška
  - Serializace celého stromu do JSON šablony
- Šablona se ukládá jako **jeden JSON blob v `^ALVA.TemplateDesigner.Templates(...)`** — Caché ten blob nerozbaluje, neví o jednotlivých elementech uvnitř

**Důsledek:** C# třída `TextElement` s typovými vlastnostmi **musí existovat**, aby kód uměl renderovat / klonovat / škálovat. Nelze ji zbavit ve prospěch slovníku, aniž by se ztratila typová bezpečnost (renderer by všude přistupoval přes `(float)values["FontSize"]`).

**Toto je inverzní situace vůči legacy EDISONu**, kde C# je tenké a Caché je tlusté. V TD je C# bohaté na chování a Caché může být tenké (zná jen JSON blob šablony).

---

## 5. Co se v TD bude synchronizovat (a co ne)

**Entita TextElement do Caché nemigruje.** Zůstává čistě v C# (a v JSON blob šablony). Caché o jednotlivých elementech vůbec neví.

**Co se přesouvá do Caché:** pouze **definice editoru** pro každý typ elementu.

| Strana | Co drží |
|---|---|
| **C# třída `TextElement`** | Vlastnosti (Text, FontSize, TextColor, Bold, …) + chování (`Render`, `Clone`, `ApplyScale`, …) |
| **Caché třída pro editor TextElementu** | Definici formuláře — která pole, jaké typy, labely, validace, viditelnost, per-customer patche |
| **EDInputForm komponenta** | Generická vrstva — pro typ pole „bool" vyrenderuje CheckBox, pro „decimal" NumericBox s rozsahem |
| **Most (bridge) v TemplateDesigneru** | Lepidlo — když uživatel ve formuláři změní pole „Bold", reflexí najde `textElement.Bold` a zapíše hodnotu |

### 5.1 Mapa odpovědností v ASCII

```
┌─────────────────────────────────────────────────────────────────┐
│ CACHÉ (specifické per formulář, customizovatelné per zákazník)  │
├─────────────────────────────────────────────────────────────────┤
│ • Definice formuláře — která pole, v jakém pořadí, jakého typu  │
│ • Labely a popisky (i lokalizace)                               │
│ • Skupiny (Group)                                               │
│ • Validační rozsahy                                             │
│ • Závislosti mezi poli (skrýt pole když...)                     │
│ • Per-customer patche                                           │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│ EDInputForm (generická komponenta, hardcoded chování widgetů)   │
├─────────────────────────────────────────────────────────────────┤
│ • „Type=text → vyrenderuj TextBox"                              │
│ • „Type=bool → vyrenderuj CheckBox"                             │
│ • „Type=color → vyrenderuj color picker"                        │
│ • Klávesové navigace, focus management                          │
│ • Posílá změny zpátky (klíč, hodnota)                           │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│ C# entita (specifická pro TD, drží stav v paměti)               │
├─────────────────────────────────────────────────────────────────┤
│ • Třída TextElement s typovými vlastnostmi                      │
│ • Logika: Render(), Clone(), ApplyScale(), JSON serializace     │
│ • Žádné info o formuláři ani UI                                 │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│ Bridge (lepidlo mezi formulářem a entitou, malá vrstva v TD)    │
├─────────────────────────────────────────────────────────────────┤
│ • Mapuje (klíč pole, hodnota) → instance entity přes reflexi    │
│ • Sleduje shodu názvů (CI test)                                 │
│ • Volá překreslení designeru po změně hodnoty                   │
└─────────────────────────────────────────────────────────────────┘
```

### 5.2 Jediná smlouva mezi Caché a C#

```
Klíč pole v Caché definici    ↔    Název C# vlastnosti

AddField("Bold", ...)              ↔    public bool Bold
AddField("FontSize", ...)          ↔    public float FontSize
AddField("TextColor", ...)         ↔    public string TextColor
```

C# strana **nemá ponětí** o pořadí polí, layoutu, widgetech, labelech, skupinách. Veškerá UI rozhodnutí žijí v Caché. Most si jen najde C# vlastnost podle stejného jména jako klíč pole.

---

## 6. Co C# přesně potřebuje vědět o formuláři

**Skoro nic.** Jen:
- Že pro úpravu `TextElementu` existuje nějaký formulář v Caché (název backing třídy)
- Hodnoty pocházející z formuláře dorazí jako pár `(klíč, hodnota)` — most je propasíruje do C# vlastností přes reflexi

C# strana **nezná**:
- Kolik polí formulář má
- Jaký druhé pole
- Jaký widget se použije
- Jaký label
- Ve které skupině pole je

To všechno řeší Caché + EDInputForm spolu.

---

## 7. Tři příklady použití

### Příklad 1 — Klasický master-data formulář (Linka, faktura, …)

| Strana | Co má |
|---|---|
| Caché | Definici formuláře + záznam entity v globálu |
| EDInputForm | Standardně vyrenderuje pole |
| C# | **Nic vlastního** — žádná C# třída pro Linku. Hodnoty si EDInputForm s Caché vyhandluje sám. |
| Most | Není potřeba |

Tradiční EDISON modul. Jednoduchý.

### Příklad 2 — TD úprava TextElementu

| Strana | Co má |
|---|---|
| Caché | Definici editoru pro TextElement (`AddField("Text", text)`, `AddField("FontSize", decimal, min=4, max=200)`, …). **Žádná Caché entita TextElement neexistuje.** |
| EDInputForm | Standardně vyrenderuje TextBox, NumericBox, CheckBox, ColorPicker |
| C# | Třída `TextElement` s vlastnostmi `Text`, `FontSize`, `Bold`, `TextColor` + metody `Render()`, `Clone()`, `ApplyScale()` |
| Most | Při změně pole „Bold" → reflexí najde `textElement.Bold` → zapíše hodnotu → spustí překreslení plátna |

### Příklad 3 — Per-customer patch pro TextElement

| Strana | Co se mění |
|---|---|
| Caché | Patch v subclass editor třídě pro daného zákazníka (např. „pro DPKV schovej pole `Strikethrough`, omez `FontSize` na rozsah 6–24 pt") |
| EDInputForm | **Beze změny** — automaticky vyrenderuje editor podle nové definice |
| C# | **Beze změny** — `TextElement.Strikethrough` v entitě stále existuje, jen ho uživatel DPKV nevidí v editoru |
| Most | **Beze změny** — pokud Strikethrough nepřijde z formuláře, vlastnost zůstane na defaultní hodnotě |

To je ta hodnota patchování — žádný rebuild `.exe`, jen Caché úprava.

---

## 8. Co potřebuje EDInputForm doplnit (gap analysis)

Z auditu `legacy-component-research.md` sekce 7 + diskuze:

| Co chybí | K čemu to TD potřebuje | Precedent v legacy |
|---|---|---|
| **Pole pro barvu** (color picker + hex) | TextColor, BackgroundColor, BorderColor, FillColor | Starý BIG to měl (typ Color) |
| **Pole pro výběr fontu** (rodina + velikost + bold/italic) | Volba fontu textu | Starý BIG to měl (typ Font) |
| **Pole s našeptáváním** (filterovatelný ComboBox) | Výběr datového pole z agendy (placeholder `{NAZEV_POLE}`) | Dnes řeší TD vlastním deskriptorem |
| **Pole „text + tlačítko"** (TextBox + ikona, která otevře další dialog) | Placeholdery — uživatel napíše text + může otevřít picker datových polí | Dnes řeší TD vlastním deskriptorem |
| **Závislosti mezi poli** (jedno pole určuje viditelnost jiného) | TableElement: Mode=Dynamic mění zobrazení Sloupců | V BIG částečně přes server validaci |
| **Tlačítko s callback ID** (Button → otevři sub-dialog) | „Upravit sloupce..." otevírá nested editor | BIG měl button typ s callbackem |
| **Persist viditelnosti polí per uživatel** | „Tohle pole nepoužívám, schovat ho" | BIG měl `BIG.UserData` blob storage — máme analogii v `ALVA.EDControls.UserData` (Tier 1, hotové) |

**Wire formát** EDInputForm dnes vrací `$lb()` poziční seznam (legacy CVar wire). Při refaktoru by se měl přejít na JSON přes `%ToJSON()` — symetricky s ostatními novými endpointy. Viz [`../../DOCS/01-wire-formats.md`](../../DOCS/01-wire-formats.md).

---

## 9. Drift problém a tři reálné cesty řešení

### 9.1 Drift problém

Pokud C# má vlastnost `FontSize` a Caché má pole s klíčem `FontSize`, **musí se shodovat**. Drift = překlep nebo přejmenování jedné strany bez druhé.

Drift se týká **pouze názvů polí v editoru proti názvům C# vlastností** — nic víc. Ne celé entity, ne jejího chování, ne UI metadat.

### 9.2 Tři cesty řešení driftu

**Možnost A — Konvence + automatická kontrola v CI**

Definice fieldu v Caché používá **přesně stejný klíč** jako C# vlastnost. CI test prochází všechny Caché editor třídy a ověří, že každý field klíč má protějšek v C# třídě.

```
Přidat Strikethrough:
1. C#:    public bool Strikethrough { get; set; } = false;
2. Caché: d form.AddField("Strikethrough", "Přeškrtnuté", "bool")
3. CI testem se ověří shoda
```

- ✅ Drift se zachytí při buildu
- ❌ Pořád píšeme na dvou místech

**Možnost B — C# je hlavní zdroj, Caché obsahuje jen patche**

C# vlastnost si nese **vlastní popis editoru přes anotace přímo na vlastnosti**:

```csharp
[EditorField(Label="Velikost", Group="Font", Min=4, Max=200, Unit="pt")]
public float FontSize { get; set; } = 12f;
```

Caché třída pro TextElement už **neobsahuje plnou definici formuláře** — jen patche pro zákazníky:

```objectscript
// Pro DPKV: zúžit FontSize na 6-24 pt
d form.PatchField("FontSize", "min", 6)
d form.PatchField("FontSize", "max", 24)
d form.HideField("Strikethrough")
```

Tok: .NET pošle Caché info o typu elementu → Caché načte patche pro daného zákazníka → vrátí seznam patchů → .NET je aplikuje na default editor metadata z C# anotací → vykreslí.

- ✅ Přidání atributu = **jedno místo** (C# property s anotací)
- ✅ Drift skoro nemožný — defaultní metadata jsou „přilepena" k C# property
- ⚠️ Odlišnější od legacy vzoru, kde Caché definovala vše

**Možnost C — Codegen z jedné strany do druhé**

Generátor odvozuje druhou definici za běhu buildu.

- ✅ Eliminuje drift definitivně
- ❌ Build pipeline komplexita, generátor je třeba napsat a udržovat — pro náš objem overkill

### 9.3 Volba mezi A a B

**B preferujeme** pro logic-heavy TD případ, **ale rozhodneme po experimentu** s prvním reálným formulářem v Tier 3. Důvody pro B:
- Standardní vzor je legacy (Caché = vše), B se od něj liší — jediná „cena" je odklon od muscle memory týmu
- Pro TD je výhoda B jednoznačná — všechno o vlastnosti na jednom místě
- Pro klasický master-data formulář (Linka) je B-výhoda nulová, protože C# pro Linku stejně nemá typovou třídu

**Kompromisní možnost:** A pro master-data formuláře, B pro TD elementy. Obě fungují přes stejnou EDInputForm. Rozhodne se v Tier 4 podle reálné zkušenosti.

---

## 10. Pravidlo „tři místa" — co reálně musíš udělat při přidání atributu

Klíčový praktický bod: i s **Cestou B** (která drift eliminuje), přidání nového viditelného atributu vyžaduje koordinovanou změnu na **třech místech**, protože každé z nich dělá něco jiného:

| Místo | Co | Proč nutné |
|---|---|---|
| ✅ **C# vlastnost** (typ + storage + default) | `public bool Strikethrough { get; set; } = false;` | Aby element entitu uměl klonovat, serializovat do JSON, držet v paměti |
| ✅ **C# renderer** (vykreslení efektu) | Nakreslit přeškrtnutí v `Render()` metodě | Jinak změna nemá vizuální efekt |
| ✅ **Caché editor definice** (UI metadata) | `AddField("Strikethrough", "Přeškrtnuté", "bool")` | Aby se pole v editoru objevilo |

S Cestou B se to redukuje na:
- C# vlastnost s anotací: `[EditorBool(Label="Přeškrtnuté", Group="Font")] public bool Strikethrough { get; set; }`
- C# renderer: úprava `Render()` metody
- Caché: nic (pole se objeví automaticky)

Tj. **renderer změna je vždy nutná**, ať tak nebo tak — to je doménová logika. „Drift hlídání" je o synchronizaci jména pole, ne o eliminaci nezbytných kroků.

---

## 11. Migrace v rámci celkového plánu

Konkrétní pořadí a stav implementace **není v tomto dokumentu** — je v jediné autoritativní roadmapě [`TemplateDesigner/.claude/docs/roadmap.md`](../../../TemplateDesigner/.claude/docs/roadmap.md), konkrétně v sekci **„Caché Migrace — 5 Vln"** → **Wave 4 (Form-driven UI)**.

Tento dokument popisuje **architekturu a důvody** rozhodnutí (proč entitu v C# vs definici v Caché, jak řešit drift, jak rozdělit odpovědnosti). Wave 4 v TD roadmapě popisuje **konkrétní kroky a status** (W4-1 až W4-7).

Cyklus celé migrace: **měsíce, ne týdny**. Realistický předpoklad.

---

## 12. Co je out-of-scope této migrace

- **BDG / editovatelná tabulka** — záměrně, je to 4 500+ LOC + komerční C1FlexGrid. TD má `StopColumnsEditorDialog` s vlastní DataGrid logikou, funguje. Není to cíl pokrýt EDControls.
- **Codebook centrální katalog** — TD má 2–3 codebooky (agendy, linky, šablony), overhead se nevyplatí.
- **Adaptor mode (reflexe v BIG)** — okrajový legacy vzor, nepotřebujeme ho.
- **Plná lokalizace** — labely v Caché jsou hardcoded česky, multi-language nepřidáváme do tohoto plánu.

---

## 13. Wire formát pro EDInputForm (rozhodnuto)

Experiment proběhl na L6 InstancePickerDialog s paralelní implementací klasické vs envelope cesty. **Finální standard** je v [`../../DOCS/01-wire-formats.md`](../../DOCS/01-wire-formats.md):

- **Default pro nové endpointy**: CVar named IN + 4 OUT styly podle obsahu (CVar scalars / JSON v slotu / JSON multi-slot / JSON envelope)
- **JSON envelope je opt-in pro composite endpointy** — což zahrnuje právě EDInputForm Init/Validate/Save (Tab → Group → Field hierarchii)
- **Helper `MLRestJson.CallAsync<TReq, TResp>`** v EDControls je dostupný pro envelope volání

EDInputForm Tier 3 refactor použije envelope wire formát — composite struktura formuláře to vyžaduje.

---

## 14. Diskutované alternativy, které jsme **odmítli**

| Alternativa | Proč ne |
|---|---|
| Plně JSON-everywhere včetně IN bez CVar | Pro triviální parametry (1–2 hodnoty jako `scope`, `key`) je CVar named o řádek čistší. Nevýznamný zisk za univerzální převod. |
| Protobuf / MessagePack / CBOR | Caché bez nativní podpory. Vyžadovalo by komunitní knihovny + učení týmu. Pro KB-velikost payloadů marginální výkonový zisk za vysokou cenu. |
| Entita TextElement i v Caché | Není proč — TextElement je run-time grafický objekt s chováním, ne master-data record. Caché šablonu zná jen jako JSON blob. |
| TextElement jako `Dictionary<string, object>` v C# (bez typových vlastností) | Ztratíme typovou bezpečnost, IDE refactor, intellisense. Renderer by všude přistupoval přes `(float)values["FontSize"]`. Drift se eliminuje na úkor řádu kódu. |
| Vnořená struktura `Tab → Group → Field` v EDInputForm | Legacy BIG to neumělo, řešilo se WinForms TabControlem nahoře. Stejně to budeme držet v nové verzi. EDInputForm zůstává `Group → Field`, multi-tab přes WPF `<TabControl>`. |
| Codegen C# anotací z Caché definice (nebo opačně) | Build infrastruktura, generátor k udržování. Pro náš objem práce overkill. |

---

## 15. Co dál

1. **Tier 2 dokončit** — L5 (TemplatePicker) commit + L6 (InstancePicker)
2. **Experiment JSON envelope** na `TemplatesList` endpointu (paralelní implementace, porovnání)
3. **Rozhodnutí o JSON envelope** → propsáno do [`../../DOCS/01-wire-formats.md`](../../DOCS/01-wire-formats.md)
4. **Tier 3 začne** s rozhodnutým wire formátem a feature parity EDInputForm s BIG
5. **Tier 4 a 5** podle plánu

---

## 16. Historie diskuze

- 2026-05-18 — dokument vytvořen jako konsolidace diskuze o migraci editorů. Pokrývá rozhodnutí o (a) inverzní situaci TD vůči legacy, (b) tabové struktuře `Group → Field` + vnější `TabControl`, (c) tří míst pro přidání atributu, (d) Cestě A vs B pro drift handling, (e) JSON envelope experimentu.
