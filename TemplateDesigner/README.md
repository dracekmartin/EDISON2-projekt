# Template Designer — Uživatelská příručka

**Template Designer** je vizuální nástroj pro tvorbu šablon tiskových sestav. Šablona popisuje rozvržení stránky — sekce, elementy, formátování. Výstupem je JSON soubor, který Print Engine (samostatný projekt) spojí s daty a vygeneruje PDF.

Pro vývojářskou dokumentaci viz [CLAUDE.md](CLAUDE.md).

---

## Rozložení okna

```
┌─────────────────────────────────────────────────────────────────────┐
│  Menu: Soubor | Upravit | Zobrazit                                  │
├─────────────────────────────────────────────────────────────────────┤
│  Toolbar: [+ Sekce ▾] [+ Element ▾] | [Zpět] [Vpřed] | [Zoom ▾]  │
│           [Grid ☐] [Snap ☐] | [Uložit] [Otevřít]                  │
├──────────────────┬──────────────────────────────────────────────────┤
│  Panel sekcí     │                                                  │
│  (vlevo, 200px)  │   Editační canvas                               │
│                  │   — sem se kliká a táhne                        │
│  Seznam sekcí    │                                                  │
│  šablony         │                       ┌─────────────────────┐   │
│                  │                       │ Vlastnosti: Text    │   │
│  [+ Přidat]      │                       │ Text: [Ahoj world]  │   │
│                  │                       │ Barva: [■ #FF0000]  │   │
│                  │                       │ [OK]  [Zrušit]      │   │
│                  │                       └─────────────────────┘   │
├──────────────────┴──────────────────────────────────────────────────┤
│  Status bar — info o vybrané sekci / elementu                       │
└─────────────────────────────────────────────────────────────────────┘
```

- **Panel sekcí vlevo** — seznam všech sekcí šablony; kliknutím se sekce aktivuje (do aktivní sekce se přidávají nové elementy)
- **Editační canvas uprostřed** — hlavní pracovní plocha; sekce a elementy jsou vidět přímo zde; sem se elementy přidávají, přesouvají, mění velikost
- **Plovoucí dialog vlastností** — otevře se po dvojkliku nebo Enteru na vybraný element; zobrazuje editovatelné vlastnosti; zavírá se tlačítkem OK / Zrušit
- **Status bar dole** — jednořádkový přehled (aktivní sekce, vybraný element, počet vybraných)

Panel sekcí lze skrýt přes menu **Zobrazit**.

---

## Základní pojmy

### Šablona
Šablona = definice stránky. Obsahuje rozměry stránky, okraje a seznam sekcí.

### Sekce
Stránka je tvořena vertikálním sledem sekcí — každá sekce je vodorovný pruh stránky. Sekce nelze umístit volně; přidávají se vždy za ostatní.

Přidáváte je přes **[+ Přidat sekci ▾]** s presety:

| Preset | Chování tisku |
|---|---|
| Záhlaví stránky | Opakuje se na každé stránce |
| Zápatí stránky | Opakuje se na každé stránce |
| Záhlaví sestavy | Jednou na začátku |
| Patička sestavy | Jednou na konci |
| Záhlaví kromě 1. str. | Na každé stránce kromě první |
| Oddělovač | Statický obsah, malá výška |
| Prázdná sekce | Statický obsah, vlastní výška |

Uložené šablony sekcí se zobrazí v tomtéž menu (viz sekce o Custom šablonách sekcí).

### Element
Uvnitř sekce lze volně umísťovat elementy — text, obdélník, čáru, obrázek nebo tabulku. Pozice elementů jsou absolutní v rámci sekce (v mm).

| Typ | Editovatelné vlastnosti |
|---|---|
| Text | Text, font, velikost, styl, zarovnání, barva textu/pozadí, zalamování |
| Obdélník | Barva výplně, barva okraje, tloušťka okraje, zaoblení rohů |
| Čára | Orientace, barva, tloušťka, styl (plná/čárkovaná/tečkovaná) |
| Obrázek | Datové pole, škálování |
| QR kód / Čárový kód | Typ (QR / Code128), obsah (text nebo `{POLE}`), barvy |
| Tabulka | Mód (pevná/dynamická), sloupce, řádky, orámování, záhlaví, barvy |

Všechny elementy sdílejí skupinu vlastností **Pozice**: X, Y, šířka, výška, ZIndex, zamčeno.

---

## Co aplikace aktuálně umí

### Sekce
- Přidat sekci libovolného typu (tlačítko **+ Sekce** v toolbaru)
- Aktivovat sekci kliknutím v levém panelu nebo na canvasu
- Sekce se zobrazí se záhlavím (modrý pruh s názvem)

### Elementy
- **Přidat element tažením z palety** — v druhém řádku toolbaru jsou ikony typů elementů (Text, Obdélník, Čára, Obrázek, Tabulka, QR, Code128); stiskněte ikonu a táhněte na canvas → element se vytvoří na pozici puštění myši v cílové sekci; při tažení se zobrazí ghost preview (čárkovaný obrys) a zvýrazní se cílová sekce
- **Přidat element přes menu** — tlačítko **+ Element** v toolbaru → klik na typ → element na pozici 5×5 mm (fallback)
- **Přetáhnout pole agendy na canvas** — z panelu "Pole agendy" táhněte pole přímo na canvas → vytvoří TextElement s `{POLE}` na pozici kurzoru; pokud pole pustíte na existující TextElement, `{POLE}` se připojí k jeho textu

### Výběr elementů
- **Klik** — vybere element (modré dashed ohraničení + 8 resize handles)
- **Ctrl+klik** — přidá / odebere element z výběru (multi-select)
- **Ctrl+A** — vybere všechny elementy aktivní sekce
- **Escape** — zavře dialog vlastností (pokud je otevřený), jinak zruší výběr
- Klik na prázdné místo — zruší výběr

### Editace vlastností elementu
- **Dvojklik** na element — otevře plovoucí dialog vlastností
- **Enter** — otevře dialog pro aktuálně vybraný element
- Dialog zobrazuje skupiny vlastností (Obsah, Font, Zarovnání, Vzhled, Pozice)
- Změny se aplikují živě na canvas s plnou podporou undo/redo
- **OK** — zavře dialog, změny zůstanou
- **Zrušit** — vrátí veškeré změny provedené od otevření dialogu
- Tlačítko **X** — schová dialog, změny zůstanou (jako OK)
- Pokud je dialog otevřený a klikneš na jiný element, dialog se aktualizuje

### Editor tabulky (TableElement)

Dvojklikem na tabulkový element se otevře **TableEditorDialog** se dvěma záložkami:

**Záložka Sloupce:**
- Přidat / odebrat / přesunout sloupec nahoru nebo dolů
- Pro každý sloupec: záhlaví, šířka (mm nebo %), zarovnání, datové pole, formát, viditelnost
- Pole "Datové pole" se validuje proti schématu agendy — neplatné pole je zvýrazněno červeně

**Záložka Řádky** (jen pro pevný mód):
- Přidat / odebrat / přesunout řádek
- Editace obsahu buněk přímo v tabulce — statický text nebo `{POLE}`
- **Viditelnost řádku** — pole pro výraz podmíněné viditelnosti (např. `{TISK_SIPKY}`, `{POCET} > 0`); řádky s nesplněnou podmínkou se při tisku přeskočí; prázdné = vždy viditelný; ⚑ příznak v seznamu signalizuje, že má řádek nastavenou podmínku

**Záložka Souhrn** (zápatí tabulky s agregacemi):
- Přidání souhrnných řádků zobrazených pod datovými řádky
- Buňky mohou obsahovat **agregační funkce**: `SUM({POLE})`, `COUNT()`, `AVG({POLE})`, `MIN({POLE})`, `MAX({POLE})`
- Agregace se vyhodnocují nad řádky dynamické tabulky (v preview i při tisku)
- Příklad: buňka s textem `Celkem: SUM({CENA})` zobrazí `Celkem: 60000`
- Souhrnné řádky mají šedé pozadí a tučné písmo

**Dva módy tabulky:**
- **Pevná (Fixed)** — počet řádků je pevný, definovaný v šabloně; buňky obsahují statický text nebo placeholdery
  - Tlačítko **Ze šablony...** — načte předdefinované rozvržení tabulky z Caché (sloupce + řádky předvyplněny) jedním klikem
- **Dynamická (Dynamic)** — sloupce jsou z designeru, řádky se generují za běhu z dat
  - **Datový zdroj** — filtrovatelný ComboBox nabízí datové kolekce ze schématu agendy (např. "ZASTAVKY", "SPOJE")
  - **Datové pole sloupce** — ComboBox nabízí pole dostupná v dané kolekci; lze i ručně zadat
  - Při prvním otevření editoru bez sloupců se sloupce automaticky předvyplní z vybrané datové kolekce

**Ze šablony... (Fixed mód):**

Tlačítko "Ze šablony..." v záložce Sloupce umožňuje načíst celé rozvržení tabulky z předdefinovaných šablon uložených v Caché:

1. Nastavte mód tabulky na **Pevná**
2. Otevřete editor tabulky (dvojklik → tlačítko **Upravit sloupce / řádky...**)
3. Klikněte na **Ze šablony...**
4. Vyberte šablonu ze seznamu (např. "Záhlaví linky", "Platnost a dopravce") → **Použít**

Šablona nahradí aktuální sloupce i řádky. Obsah buněk může obsahovat placeholdery (`{CISLO_LINKY}`, `{DATUM_OD}` apod.) — Print Engine je za běhu nahradí skutečnými daty.

> Dostupné šablony jsou definovány v Caché metodou `GetTableTemplates` příslušné agendy. Tlačítko se zobrazí jen pokud je Caché připojeno.

### QR kódy a čárové kódy (BarcodeElement)

Přidat přes **[+ Element] → QR kód** nebo **Čárový kód (Code128)**:
- **Obsah**: statický text nebo `{POLE}` placeholder
- **Typ**: QR kód (čtvercový) nebo Code128 (lineární čárový kód)
- Barvy popředí a pozadí
- V designeru se zobrazuje reálný kód — ne jen placeholder

### Přesun elementů
- **Tažení myší** — přesune vybraný element
- Při multi-selectu se pohybují **všechny vybrané elementy** najednou
- Elementy nelze přetáhnout mimo hranici sekce — skupina se zastaví, jakmile první element narazí na okraj
- Se zapnutým Snap se elementy zarovnávají na mřížku

### Přichytávání k elementům (Smart Snap)
- **Snap elem.** (tlačítko v toolbaru nebo Zobrazit → Přichytit k elementům) — zapne přichytávání k ostatním elementům v sekci
- Při tažení element automaticky přiskočí k hranám, středům nebo mezerám jiných elementů
- **Edge↔Edge** — levá/pravá/horní/dolní hrana se zarovná k odpovídající hraně souseda
- **Center↔Center** — střed elementu se zarovná na střed jiného elementu
- **Gap snap** — element se zastaví ve vzdálenosti 4 mm od souseda; v mezeře se zobrazí krátká černá čárka
- Cílový element je zvýrazněn jemným šedým dashed rámečkem
- Přichytávání k elementům má přednost před mřížkou; na každé ose (X, Y) se rozhoduje nezávisle
- Výchozí: zapnuto

### Změna velikosti
- Dostupná pro **jeden vybraný element** (8 bílých čtverečků na okrajích)
- Tažení za rohový handle → změna šířky i výšky
- Tažení za boční handle → změna jen jednoho rozměru
- Kurzor se mění podle směru resize (↔ ↕ ↗ ↘)
- Minimální rozměry: 3 mm šířka, 2 mm výška

### Výběr obdélníkem (rubber band)
- **Tažení myší na prázdném místě** — kreslí modrý výběrový obdélník
- Po uvolnění myši se vyberou všechny elementy, jejichž plocha se obdélníkem překrývá
- Výběr je omezen na aktivní sekci
- Ctrl+tažení přidá výběr k již vybraným elementům

### Kopírování / vkládání
- **Ctrl+C** — zkopíruje vybrané elementy do schránky
- **Ctrl+V** — vloží zkopírované elementy do aktivní sekce (s offsetem +5 mm)
- **Ctrl+D** — duplikuje vybrané elementy (+5 mm offset); neovlivňuje clipboard
- **Ctrl+D, šipka** — směrové duplikování: kopie se umístí přesně vedle originálu s 4 mm mezerou (↓ pod, ↑ nad, → vpravo, ← vlevo)
- Vložené elementy jsou ihned vybrány; operace je undo-able

### Editace vlastností sekce
- **Dvojklik na sekci** v levém panelu — otevře dialog vlastností sekce
- Editovatelné: Název, Výška (jen pro sekce s pevnou výškou), PrintBehavior, OverflowBehavior
- Stejný dialog jako pro elementy — OK zachová, Zrušit vrátí změny

### Správa sekcí
- **Přeřadit sekci tažením** — v levém panelu stiskněte sekci a táhněte na novou pozici; vizuální indikátor ukazuje kam se sekce vloží; plný undo/redo
- **Přesunout sekci** — tlačítka ▲▼ v levém panelu, pravé tlačítko myši → Přesunout, nebo **Ctrl+Shift+↑↓**
- **Smazat sekci** — pravé tlačítko myši na sekci → Smazat sekci (šablona musí mít alespoň jednu sekci)
- Přesun a smazání jsou undo-able

### AI Assistant — generování šablon z popisu

AI Assistant automaticky vygeneruje kompletní šablonu podle textového popisu. Stačí popsat, co chcete vytisknout, a AI vytvoří sekce, elementy, formátování a datové vazby.

**Spuštění:**
- Menu **Nástroje → AI Assistant pro generování šablon**
- Otevře se dialog s textovým polem pro popis

**Jak to funguje:**
1. Zadáte popis šablony v češtině, např.:
   ```
   Vytvoř fakturu s hlavičkou obsahující logo a název firmy,
   tabulkou položek (popis, množství, cena) a zápatím s celkovou částkou.
   ```
2. Vyberte agendu (volitelné) — pokud vyberete "Jízdní řád" nebo "Faktury", AI použije skutečná pole z Caché
3. Klikněte **Vygenerovat**
4. AI vrátí hotovou šablonu — ta se okamžitě otevře v designeru

**Schema injection — AI zná vaše databázová pole:**

Pokud vyberete agendu, AI dostane seznam dostupných polí ze schématu:
- **Skalární pole** — např. `CISLO_FAKTURY`, `DATUM_VYSTAVENI`, `NAZEV_DODAVATELE`
- **Kolekce** — např. `POLOZKY` (pro Dynamic tabulky s poli `POPIS`, `MNOZSTVI`, `CENA`)
- **Parametry** — např. `TISK_POZNAMKY`, `FORMAT_CASTKY`

AI používá **pouze skutečná pole** z vašeho systému, nevymýšlí si názvy. Tabulky mají správné `dataSource`, placeholdery odpovídají skutečným DB sloupcům.

**Konfigurace API klíče:**

AI Assistant vyžaduje **Anthropic API klíč** (Claude). Nastavit lze dvěma způsoby:

1. **Lokální config soubor** (doporučeno) — neverzuje se do gitu:
   ```json
   // dotnet/appsettings.local.json
   {
     "aiAssistant": {
       "anthropicApiKey": "sk-ant-api03-..."
     }
   }
   ```

2. **Dialog při spuštění** — pokud není klíč nakonfigurovaný, můžete ho zadat ručně do pole "Anthropic API klíč"

**Získání API klíče:**
- Registrace na https://console.anthropic.com/
- Vytvoření API klíče v sekci API Keys
- Zkopírování klíče do `appsettings.local.json` nebo do dialogu

**Zapnutí/vypnutí funkce:**

V `appsettings.json`:
```json
"aiAssistant": {
  "enabled": true   // false = skryje menu položku
}
```

**Model a náklady:**

- Výchozí model: **Claude Haiku 4.5** (rychlý a levný, ~0.8¢ za 1000 tokenů)
- Typická šablona: 2000-5000 tokenů (~$0.02-0.05 za generování)
- Pro změnu modelu viz konfigurace v `AnthropicApiClient.cs`

**Tipy pro použití:**

- Buďte specifičtí: "tabulka s 3 sloupci (název, cena, množství)" → AI vytvoří přesně to
- Zmíňte agendu v popisu: "Vytvoř šablonu pro jízdní řád s tabulkou zastávek" → AI použije pole z JR schématu
- AI respektuje typografii: "velký tučný titulek" → FontSize 18, FontWeight Bold
- Kontrolujte výsledek: AI je chytré, ale může udělat chybu — vždy zkontrolujte generovanou šablonu

**Příklad — faktura s FAKTURY agendou:**

```
Popis:
Vytvoř šablonu faktury s hlavičkou (číslo, datum), tabulkou položek
(popis, množství, cena) a zápatím se souhrnem (základ, DPH, celkem).

Agenda: FAKTURY

→ AI vygeneruje:
- Hlavička s {CISLO_FAKTURY}, {DATUM_VYSTAVENI}
- Dynamic tabulka s dataSource="POLOZKY", sloupce POPIS, MNOZSTVI, CENA
- Zápatí s Fixed tabulkou: {ZAKLAD_DANE}, {DPH}, {CELKOVA_CASTKA}
```

**Debug a řešení problémů:**

Pokud AI vrátí chybu nebo prázdný výsledek:
- Zkontrolujte API klíč (platnost, kredit na účtu)
- Zkrátete popis (max ~500 slov)
- Kompletní JSON odpověď se ukládá do `ai_generated_template_debug.json` vedle aplikace

---

### Custom šablony sekcí

Custom šablona sekce je **uložená konfigurace sekce včetně všech elementů**, kterou si vytvoříte a pojmenujete pro opakované použití.

**Příklad:** Vytvoříte sekci "Záhlaví DPO" s logem, názvem firmy a čárou → uložíte jako šablonu → příště jedno kliknutí a máte hotovo.

**Uložení sekce jako šablony:**
1. Pravý klik na sekci v levém panelu
2. Vyberte **💾 Uložit jako šablonu...**
3. Zadejte název (např. "Záhlaví DPO") → **Uložit**

→ Šablona se uloží do `section-templates/<název>.json` vedle aplikace.

**Načtení uložené šablony:**
1. Klikněte **[+ Přidat sekci ▾]**
2. V menu se zobrazí všechny uložené šablony (položky s ikonou 💾)
3. Klikněte na šablonu → vytvoří se kopie sekce se všemi elementy

**Kde se šablony ukládají:**
```
<složka-aplikace>/section-templates/
```
Soubory jsou čitelné v textovém editoru a lze je **ručně upravit**.

**Sdílení šablon s kolegy:**
- Kopírování složky `section-templates/` z jednoho PC na druhý
- Nebo uložit šablony na sdílený síťový disk — pak mají přístup všichni uživatelé

**Tipy:**
- Používejte popisné názvy: "Záhlaví DPO officiální" místo "Template1"
- Smazat šablonu: smažte příslušný `*.json` soubor ze složky `section-templates/`
- Složku `section-templates/` zálohujte spolu s ostatními daty aplikace

### Vlastnosti stránky
- **Soubor → Vlastnosti stránky…** nebo **Ctrl+Shift+P** — otevře dialog pro editaci rozměrů a okrajů
- Editovatelné: Šířka, Výška (mm), okraje Nahoře / Dole / Vlevo / Vpravo (mm), kód agendy, formát čísel a datumů
- Canvas se okamžitě překreslí po každé změně, Zrušit vrátí vše zpět
- Výchozí formát: A4 na výšku (210 × 297 mm, okraje 10 mm)

### Live preview

Pravý panel zobrazuje **náhled šablony s mock daty** — takto bude šablona vypadat při tisku.

- Otevřít / zavřít přes menu **Zobrazit → Live preview** nebo tlačítko v toolbaru
- Automaticky se aktualizuje při každé změně šablony
- **Mock data ze systému** — pokud agenda implementuje `GetMockData` v Caché, preview zobrazuje reálná ukázková data (skutečné hodnoty polí, reálné řádky tabulek)
- Fallback: hodnoty ze schématu agendy (pole `example`) nebo generické zástupce
- Dynamic tabulky zobrazují všechny mock řádky vrácené z `GetMockData`

### Parametry sestavy v preview

Pokud agenda definuje parametry (přes Caché `GetParameters`), zobrazí se v záhlaví preview panelu interaktivní pruh s kontrolkami:

```
┌────────────────────────────────────────────────────────────────────┐
│ Náhled • vzorová data z Caché          [PDF]                       │
├────────────────────────────────────────────────────────────────────┤
│ ☑ Tisknout šipky směru   ☐ Tarifní zóna   Formát času: H:mm ▾    │
├────────────────────────────────────────────────────────────────────┤
│  [stránka náhledu]                                                 │
```

- **CheckBox** pro parametry typu `bool` — zaškrtnutí/odškrtnutí okamžitě aktualizuje preview
- **ComboBox** pro parametry typu `enum` — výběr z předdefinovaných možností
- **TextBox** pro ostatní typy — ručně zadaná hodnota

Hodnoty parametrů jsou dostupné jako `{NAZEV_PARAMETRU}` v libovolném TextElementu nebo buňce tabulky (stejná syntaxe jako datová pole). Parametry jsou rovněž zobrazeny v **Pickeru polí** s příznakem "(parametr)" pro snadné vložení do textu.

> Parametry definuje programátor agendy v Caché metodou `GetParameters`. Uživatel nastavuje jejich hodnoty v preview panelu; při ostrém tisku je Print Engine dostane standardní cestou (print dialog — budoucí funkce).

### Podmíněná viditelnost elementů a sekcí

Každý element a každá sekce má vlastnost **Viditelnost** (v dialogu vlastností, skupina "Podmínky"). Výraz určuje, zda se element/sekce v preview a při tisku zobrazí.

**Syntaxe výrazů:**

```
{TISK_SIPKY}                   → parametr nebo pole (truthy/falsy hodnota)
{POCET} > 0                    → porovnání s číslem
{TYP} == "JR"                  → porovnání se stringem
!{TISK_TARIFU}                 → negace
{TISK_SIPKY} && {POCET} > 0    → logické AND
{A} || {B}                     → logické OR
({A} || {B}) && {C}            → závorky pro prioritu
```

- Prázdné pole = vždy viditelný (výchozí stav)
- Výrazy se vyhodnocují v preview i při tisku (Print Engine)
- Chyba syntaxe → element zůstane viditelný (fail-open, překlep nic nerozbije)
- Hodnoty parametrů i datových polí jsou dostupné jako `{NAZEV}` v libovolném výrazu

**Typová koerce proměnných:**

| Hodnota | Vyhodnocení |
|---|---|
| `""` (prázdný string) | false |
| `"false"` / `"0"` | false |
| `"true"` / `"1"` nebo jiný neprázdný string | true |
| číslo != 0 | true |

### Export náhledu do PDF

Aktuální live preview lze exportovat přímo do PDF souboru — se stejnými mock daty, která jsou vidět v náhledu.

- **Soubor → Exportovat náhled jako PDF...** — otevře dialog pro uložení souboru
- **Tlačítko PDF** v záhlaví live preview panelu — stejná funkce, rychlejší přístup
- Výstup je jednostránkový PDF odpovídající rozměrům šablony (šířka × výška v mm)
- Okraje šablony jsou zachovány; stránka má bílé pozadí
- Data v PDF jsou identická s preview (Caché mock nebo schema fallback)

> PDF export slouží jako rychlá kontrola rozvržení před ostrým tiskem. Nenahrazuje Print Engine — ten zpracuje reálná data a zvládne vícestr. výstup.

### Zoom
- Výběr z hodnot 50% / 75% / 100% / 150% / 200% v toolbaru
- **Ctrl+scroll** na canvasu — přiblížení / oddálení

### Mřížka a přichytávání
- **Grid** (tlačítko nebo Zobrazit → Mřížka) — zobrazí přímky mřížky na canvasu
- **Snap** — přichytávání elementů na mřížku při pohybu a resize
- **Snap elem.** — přichytávání k ostatním elementům (viz sekce Přichytávání k elementům)
- Velikost mřížky: 5 mm (výchozí)

### Zarovnání elementů
Funguje pro **multi-select** (min 2 vybrané elementy; Rozložit min 3). Vše undo-able.

| Tlačítko | Akce |
|---|---|
| ⬅ | Zarovnat vlevo — všechny na nejlevější X |
| ➡ | Zarovnat vpravo — všechny na nejpravější pravý okraj |
| ↔ | Zarovnat na střed vodorovně |
| ⬆ | Zarovnat nahoru — všechny na nejhornější Y |
| ⬇ | Zarovnat dolů — všechny na nejspodnější dolní okraj |
| ↕ | Zarovnat na střed svisle |
| ⬌ | Rozložit rovnoměrně vodorovně (min 3) |
| ⬍ | Rozložit rovnoměrně svisle (min 3) |

Tlačítka jsou v druhém řádku toolbaru. Stejné operace v menu **Upravit**.

### ZIndex — pořadí překrývání
- **Ctrl+PgUp** — přenést dopředu (ZIndex +1)
- **Ctrl+PgDn** — odeslat dozadu (ZIndex −1, min 0)
- **Ctrl+Shift+PgUp** — přenést zcela dopředu (nad všechny ostatní v sekci)
- **Ctrl+Shift+PgDn** — odeslat zcela dozadu (pod všechny ostatní)
- Stejné operace v menu **Upravit** a tlačítka ↑Z ↓Z ⤒Z ⤓Z v toolbaru
- Funguje i pro multi-select (všechny vybrané elementy se posunou najednou)
- Efekt je viditelný jen když se elementy **překrývají** (např. obdélník jako pozadí za textem)
- Vše undo-able

### Undo / Redo
- **Ctrl+Z** — vrátit poslední akci
- **Ctrl+Y** — zopakovat vrácenou akci
- Undoable akce: přesun, resize, změna vlastností, přidání/smazání elementu, přidání/smazání/přesun sekce, změna vlastností stránky

### Uložení a otevření
- **Ctrl+S** / menu Soubor → Uložit
- **Soubor → Uložit jako** — zvolí umístění
- **Soubor → Otevřít** — načte existující šablonu
- **Soubor → Exportovat náhled jako PDF...** — uloží aktuální náhled jako PDF
- Soubory šablon mají příponu `.json` a jsou čitelné v libovolném textovém editoru

---

## Klávesové zkratky

| Zkratka | Akce |
|---|---|
| Ctrl+Z | Undo |
| Ctrl+Y | Redo |
| Ctrl+A | Vybrat vše v aktivní sekci |
| Ctrl+S | Uložit |
| **Enter** | Otevřít dialog vlastností pro vybraný element |
| **Dvojklik** na element | Otevřít dialog vlastností elementu |
| **Dvojklik** na sekci | Otevřít dialog vlastností sekce |
| Escape | Zavřít dialog vlastností / zrušit výběr |
| Ctrl+klik | Přidat/odebrat element z výběru |
| **Del** | Smazat vybrané elementy |
| **↑ ↓ ← →** | Posunout vybrané elementy o 1 mm |
| **Shift+šipky** | Posunout vybrané elementy o velikost mřížky (5 mm) |
| **Ctrl+C** | Kopírovat vybrané elementy |
| **Ctrl+V** | Vložit ze schránky do aktivní sekce |
| **Ctrl+D** | Duplikovat vybrané elementy (+5 mm offset) |
| **Ctrl+D, šipka** | Směrové duplikování (kopie vedle originálu s 4 mm mezerou) |
| **Ctrl+Shift+↑** | Přesunout aktivní sekci nahoru |
| **Ctrl+Shift+↓** | Přesunout aktivní sekci dolů |
| **Ctrl+scroll** | Zoom in / out |
| **Ctrl+Shift+P** | Vlastnosti stránky (rozměry, okraje) |
| **Ctrl+PgUp** | Přenést dopředu (ZIndex +1) |
| **Ctrl+PgDn** | Odeslat dozadu (ZIndex −1) |
| **Ctrl+Shift+PgUp** | Přenést zcela dopředu |
| **Ctrl+Shift+PgDn** | Odeslat zcela dozadu |

---

## Datové vazby (placeholders)

Text elementů může obsahovat datové zástupné symboly ve formátu `{NAZEV_POLE}`, například:
- `Jízdní řád — {NAZEV_LINKY}`
- `Strana {STRANA} / {CELKEM_STRAN}`
- `Datum: {DATUM}`

V designeru se zobrazuje text doslova (včetně závorek). Print Engine za běhu nahradí `{POLE}` skutečnými daty.

### Formátování placeholderů

Placeholdery podporují formátovací instrukce za dvojtečkou: `{POLE:formát}`.

**Číselné a datumové formáty (.NET):**
- `{CENA:N2}` → `12 000,00` (číslo se 2 desetinnými místy)
- `{DATUM:dd.MM.yyyy}` → `01.01.2024`
- `{CAS:HH:mm}` → `08:00`

Formát čísel a datumů závisí na **kultuře šablony** — nastavitelné ve vlastnostech stránky (výchozí cs-CZ). Například `{CENA:N2}` zobrazí `12 000,00` v češtině, ale `12,000.00` v angličtině.

**Řetězcové funkce:**
- `{POLE:UPPER}` / `{POLE:LOWER}` — velikost písmen
- `{POLE:LEFT(4)}` / `{POLE:RIGHT(4)}` — první/poslední N znaků
- `{POLE:PADLEFT(3,'0')}` — doplnění na délku (např. `42` → `042`)
- `{POLE:TRIM}` — oříznutí mezer

Formátování funguje v TextElementech, buňkách tabulky i v souhrnných řádcích.

### Pole agendy — dostupná databázová pole

Každá šablona patří k **agendě** (např. Jízdní řád, Faktury). Agenda definuje, která databázová pole jsou k dispozici pro použití v `{POLE}` placeholderech. Pole, kolekce a parametry se zobrazují v panelu vlevo záložka **Pole agendy** jako strom se třemi skupinami:

```
┌──────────────────────────────────┐
│ ▣ Sekce  │ ⊞ Pole agendy        │
├──────────────────────────────────┤
│ Agenda: Jízdní řád               │
├──────────────────────────────────┤
│ ▼ 📋 Skalární pole (4)           │
│     CISLO_LINKY  Číslo linky     │
│     TITULEK      Titulek         │
│     NAZEV        Název linky     │
│     DATUM_OD     Platnost od     │
│ ▼ 📊 Kolekce (2)                 │
│   ▼ Zastávky  [ZASTAVKY]         │
│       POPIS    Název zastávky    │
│       CAS_ODJ  Čas odjezdu       │
│   ▼ Spoje  [SPOJE]               │
│       CISLO_SPOJE  Číslo spoje   │
│ ▼ ⚙ Parametry (3)               │
│     TISK_SIPKY  Tisknout šipky…  │
│     TISK_TARIFU Tisknout tarif…  │
│     FORMAT_CASU Formát času…     │
├──────────────────────────────────┤
│ 💡 Dvojklik = vložit {POLE}      │
└──────────────────────────────────┘
```

- **Skalární pole** — jednorázové hodnoty pro celou sestavu (číslo linky, datum, …)
- **Kolekce** — tabulkové zdroje dat pro Dynamic TableElement; pole uvnitř kolekce jsou dostupná v řádcích tabulky
- **Parametry** — interaktivní přepínače viditelné v preview panelu; jsou označeny příznakem "(parametr)"
- Dvojklik na **pole nebo parametr** → vloží `{NAZEV_POLE}` do textu vybraného TextElementu
- Pokud není vybrán TextElement → zkopíruje `{NAZEV_POLE}` do schránky
- Skupinové uzly (📋 / 📊 / ⚙) a záhlaví kolekcí dvojklikem nic nedělají
- Pole lze použít v **libovolném** TextElementu — designer neomezuje kde

---

## Co bude přidáno (plánované funkce)

### Tabulky a JR

- **JR Tabulka (JRTableElement)** — specializovaný element pro jízdní řády
  - Matice zastávky × spoje (dynamické sloupce i řádky — aktuální TableElement toto nezvládne)
  - Uživatel konfiguruje zobrazení: které levé sloupce (Zóna, Km, Zastávka, MHD), šířky, barvy, záhlaví
  - Šipky směru jízdy, víceúrovňové záhlaví (Pracovní dny / Víkendy), střídání barev
  - Data (spoje, časy) přijdou z Caché — šablona funguje pro libovolnou linku
- **Grouping v Dynamic tabulkách** — Group Header/Footer řádky mezi daty

### Podmíněnost a parametry

- **Print dialog parametrů** — dynamicky generovaný ze šablony před tiskem (Part 2 parametrů)
  - Part 1 je hotový: parametry definované v Caché jsou viditelné a interaktivní v live preview
  - Chybí: ostrý print dialog, ukládání hodnot do JSON šablony, offline fallback bez Caché

### Formátování

- ✅ **Základní formátování placeholderů** — `{POLE:N2}`, `{POLE:dd.MM.yyyy}`, `{POLE:UPPER}`, `{POLE:LEFT(4)}` (implementováno)
- **Rozšíření:** další řetězcové funkce, podmíněné formátování dle hodnoty

### Ostatní

- **Zamčení elementu** — toggle zámku přes klávesovou zkratku nebo toolbar (IsLocked je v modelu, UI chybí)
