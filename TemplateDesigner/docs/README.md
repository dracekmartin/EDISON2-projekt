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
│  šablony         │                                                  │
│                  │                                                  │
│  [+ Přidat]      │                                                  │
├──────────────────┴──────────────────────────────────────────────────┤
│  Panel vlastností (dole) — zobrazuje info o vybraném elementu       │
└─────────────────────────────────────────────────────────────────────┘
```

- **Panel sekcí vlevo** — seznam všech sekcí šablony; kliknutím se sekce aktivuje (do aktivní sekce se přidávají nové elementy)
- **Editační canvas uprostřed** — hlavní pracovní plocha; sekce a elementy jsou vidět přímo zde; sem se elementy přidávají, přesouvají, mění velikost
- **Panel vlastností dole** — zobrazuje informace o aktuálně vybraném elementu nebo sekci

Panely lze skrýt přes menu **Zobrazit**.

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

| Typ | Popis |
|---|---|
| Text | Statický nebo datově vázaný text (`{POLE}`) |
| Obdélník | Barevný obdélník nebo pozadí |
| Čára | Horizontální nebo vertikální čára |
| Obrázek | Statický nebo z datového zdroje |
| Tabulka | Speciální tabulka s dynamickými řádky |

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
- **Escape** — zruší výběr
- Klik na prázdné místo — zruší výběr

### Přesun elementů
- **Tažení myší** — přesune vybraný element
- Při multi-selectu se pohybují **všechny vybrané elementy** najednou
- Se zapnutým Snap se elementy zarovnávají na mřížku

### Změna velikosti
- Dostupná pro **jeden vybraný element** (8 bílých čtverečků na okrajích)
- Tažení za rohový handle → změna šířky i výšky
- Tažení za boční handle → změna jen jednoho rozměru
- Kurzor se mění podle směru resize (↔ ↕ ↗ ↘)
- Minimální rozměry: 3 mm šířka, 2 mm výška

### Undo / Redo
- **Ctrl+Z** — vrátit poslední akci
- **Ctrl+Y** — zopakovat vrácenou akci
- Undoable akce: přesun, změna velikosti (multi-přesun je jeden undo krok)

### Uložení a otevření
- **Ctrl+S** / menu Soubor → Uložit
- **Soubor → Uložit jako** — zvolí umístění
- **Soubor → Otevřít** — načte existující šablonu
- Soubory mají příponu `.json` a jsou čitelné v libovolném textovém editoru

### Zoom
- Výběr z hodnot 50% / 75% / 100% / 150% / 200% v toolbaru

### Mřížka
- **Grid** (tlačítko nebo Zobrazit → Mřížka) — zobrazí přímky mřížky na canvasu
- **Snap** — přichytávání elementů na mřížku při pohybu a resize
- Velikost mřížky: 5 mm (výchozí)

---

## Klávesové zkratky

| Zkratka | Akce |
|---|---|
| Ctrl+Z | Undo |
| Ctrl+Y | Redo |
| Ctrl+A | Vybrat vše v aktivní sekci |
| Ctrl+S | Uložit |
| Escape | Zrušit výběr |
| Ctrl+klik | Přidat/odebrat element z výběru |

---

## Datové vazby (placeholders)

Text elementů může obsahovat datové zástupné symboly ve formátu `{NAZEV_POLE}`, například:
- `Jízdní řád — {NAZEV_LINKY}`
- `Strana {STRANA} / {CELKEM_STRAN}`
- `Datum: {DATUM}`

V designeru se zobrazuje text doslova (včetně závorek). Print Engine za běhu nahradí `{POLE}` skutečnými daty.

---

## Co bude přidáno (plánované funkce)

- Editovatelný panel vlastností (změna textu, barvy, fontu, velikosti přímo z UI)
- Přejmenování sekce, smazání sekce / elementu (s undo)
- Drag & drop přeřazení sekcí v levém panelu
- Rubber band selection (výběr obdélníkem tažením na prázdném místě)
- Pohyb elementů klávesovými šipkami
- Live preview s mock daty (pravý panel)
- Uzamčení elementu (IsLocked)
- Podmíněná viditelnost (VisibilityExpression)
- Přiblížení kolečkem myši (Ctrl+scroll)
