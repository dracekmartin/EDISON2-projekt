# Template Designer — Uživatelská příručka

**Template Designer** je vizuální nástroj pro tvorbu šablon tiskových sestav. Šablona popisuje rozvržení stránky — sekce, elementy, formátování. Výstupem je JSON soubor, který Print Engine (samostatný projekt) spojí s daty a vygeneruje PDF.

Pro vývojářskou dokumentaci viz [architektura.md](architektura.md).

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
Stránka je tvořena vertikálním sledem sekcí — každá sekce je vodorovný pruh stránky. Sekce nelze umístit volně; přidávají se vždy za ostatní. Typy sekcí:

| Typ | Popis |
|---|---|
| Záhlaví | Opakuje se na každé stránce |
| Pevná výška | Statický obsah, fixní výška |
| Tabulka | Opakující se řádky z dat |
| Tabulka (vlastní design) | Tabulka s uživatelem navrženým řádkem |
| Nekonečný text | Roztahuje se podle obsahu |
| Oddělovač | Mezera nebo čára |
| Zápatí | Opakuje se na každé stránce |

Sekce s dynamickou výškou (Tabulka, Nekonečný text) jsou v designeru zobrazeny s fixní placeholder výškou a označeny symbolem **∞** v pravém rohu záhlaví sekce.

### Element
Uvnitř sekce lze volně umísťovat elementy — text, obdélník, čáru, obrázek nebo tabulku. Pozice elementů jsou absolutní v rámci sekce (v mm).

| Typ | Editovatelné vlastnosti |
|---|---|
| Text | Text, font, velikost, styl, zarovnání, barva, zalamování |
| Obdélník | Barva výplně, barva okraje, tloušťka okraje, zaoblení rohů |
| Čára | Orientace, barva, tloušťka, styl (plná/čárkovaná/tečkovaná) |
| Obrázek | Datové pole, škálování |
| Tabulka | Datový zdroj, výška řádku, záhlaví, výška záhlaví |

Všechny elementy sdílejí skupinu vlastností **Pozice**: X, Y, šířka, výška, ZIndex, zamčeno.

---

## Co aplikace aktuálně umí

### Sekce
- Přidat sekci libovolného typu (tlačítko **+ Sekce** v toolbaru)
- Aktivovat sekci kliknutím v levém panelu nebo na canvasu
- Sekce se zobrazí se záhlavím (modrý pruh s názvem)

### Elementy
- Přidat element do aktivní sekce (**+ Element** v toolbaru)
- Nový element se umístí na pozici 5×5 mm v sekci

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

### Přesun elementů
- **Tažení myší** — přesune vybraný element
- Při multi-selectu se pohybují **všechny vybrané elementy** najednou
- Elementy nelze přetáhnout mimo hranici sekce — skupina se zastaví, jakmile první element narazí na okraj
- Se zapnutým Snap se elementy zarovnávají na mřížku

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
- **Ctrl+D** — duplikuje vybrané elementy (Copy + Paste v jednom kroku)
- Vložené elementy jsou ihned vybrány; operace je undo-able

### Editace vlastností sekce
- **Dvojklik na sekci** v levém panelu — otevře dialog vlastností sekce
- Editovatelné: Název, Výška (jen pro sekce s pevnou výškou), PrintBehavior, OverflowBehavior
- Stejný dialog jako pro elementy — OK zachová, Zrušit vrátí změny

### Správa sekcí
- **Přesunout sekci** — tlačítka ▲▼ v levém panelu, pravé tlačítko myši → Přesunout, nebo **Ctrl+Shift+↑↓**
- **Smazat sekci** — pravé tlačítko myši na sekci → Smazat sekci (šablona musí mít alespoň jednu sekci)
- Přesun a smazání jsou undo-able

### Vlastnosti stránky
- **Soubor → Vlastnosti stránky…** nebo **Ctrl+Shift+P** — otevře dialog pro editaci rozměrů a okrajů
- Editovatelné: Šířka, Výška (mm), okraje Nahoře / Dole / Vlevo / Vpravo (mm)
- Canvas se okamžitě překreslí po každé změně, Zrušit vrátí vše zpět
- Výchozí formát: A4 na výšku (210 × 297 mm, okraje 10 mm)

### Zoom
- Výběr z hodnot 50% / 75% / 100% / 150% / 200% v toolbaru
- **Ctrl+scroll** na canvasu — přiblížení / oddálení

### Mřížka
- **Grid** (tlačítko nebo Zobrazit → Mřížka) — zobrazí přímky mřížky na canvasu
- **Snap** — přichytávání elementů na mřížku při pohybu a resize
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
- Soubory mají příponu `.json` a jsou čitelné v libovolném textovém editoru

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
| **Ctrl+D** | Duplikovat vybrané elementy |
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

### Pole agendy — dostupná databázová pole

Každá šablona patří k **agendě** (např. Jízdní řád, Faktury). Agenda definuje, která databázová pole jsou k dispozici pro použití v `{POLE}` placeholderech. Tato pole jsou uložena v souboru `schemas/JR.schema.json` a zobrazují se v panelu vlevo záložka **Pole agendy**:

```
┌─────────────────────────────┐
│ ▣ Sekce  │ ⊞ Pole agendy   │
├─────────────────────────────┤
│ Agenda: Jízdní řád          │
├─────────────────────────────┤
│ CISLO_LINKY     Číslo linky │
│ TITULEK         Titulek      │
│ POPIS_PLATNOSTI Platnost     │
│ CISLO_ZASTAVKA  Č. zastávky │
│ POPIS_ZASTAVKA  Zastávka    │
│ CAS_ODJEZDU     Čas odjezdu │
├─────────────────────────────┤
│ 💡 Dvojklik = vložit {POLE} │
└─────────────────────────────┘
```

- Dvojklik na pole → vloží `{NAZEV_POLE}` do textu vybraného TextElementu
- Pokud není vybrán TextElement → zkopíruje `{NAZEV_POLE}` do schránky
- Pole lze použít v **libovolném** TextElementu — designer neomezuje kde

---

## Co bude přidáno (plánované funkce)

- **Pole agendy — browser databázových polí** — záložka v levém panelu; flat list dostupných `{POLE}` pro danou agendu; dvojklik vloží placeholder do textu; schema načítána z `schemas/*.schema.json`
- **Editor sloupců tabulky** — přidat/odebrat/přesunout sloupec, nastavit šířku a záhlaví
- ~~ZIndex ovládání~~ — hotovo
- ~~Zarovnání elementů~~ — hotovo
- **Zamčení elementu** — toggle zámku přes klávesovou zkratku nebo toolbar (IsLocked je v modelu, UI chybí)
- **Live preview s mock daty** — pravý panel s náhledem výstupu
- **Přehled elementů v dialogu vlastností sekce** — seznam elementů sekce, hromadná editace
- **Podmíněná viditelnost** — VisibilityExpression pro elementy
