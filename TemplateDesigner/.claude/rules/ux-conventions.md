# Glob: dotnet/Windows/**

## UX konvence - co už dodržujeme

Tento soubor formalizuje patterny, které se v TD už používají. Cíl: zachovat **konzistenci napříč okny** při přidávání nových dialogů a featur.

> **Související:** vzor okna a MVVM hranice viz `../docs/architecture.md` §Vzor pro každé okno.

---

## 1. Dialogová tlačítka a struktura okna

**Vzor okna / dialogu (struktura souborů, ViewModel, code-behind, XAML binding, IsDefault/IsCancel, Owner, RequestClose):** viz `../docs/architecture.md` §Vzor pro každé okno. Je závazný standard, nepřepisuj sem.

**Postupná unifikace dialogů - průběžné pravidlo, ne úkol** (přesunuto z roadmapy 29. 7.): dialogy se
sjednocují **při dotyku**, ne velkým refaktorem. Když na dialogu z jiného důvodu pracuješ, posuň ho
o krok blíž ke standardu (formulářová část na EDInputForm, tlačítka a chrome dle pravidel níže).
Jako plánovaná položka to v roadmapě jen viselo - je to způsob práce, který platí pořád.

**Co doplňuje tento doc** (UX-specifické konvence, ne kódový template):

- **Labely standardních tlačítek přes `{StaticResource EDStrings.*}`** - jediný zdroj pravdy (EDStrings.xaml je mergnutý v App.xaml). Nepoužívej literály:
  - `EDStrings.Cancel` = **„Storno"** (firemní standard - EDFooter i EDStrings; NE „Zrušit")
  - `EDStrings.Ok` = „OK", `EDStrings.Save` = „Uložit", `EDStrings.Close` = „Zavřít"
  - IsCancel tlačítko = vždy `EDStrings.Cancel`. IsDefault (primární) = buď `EDStrings.Ok`, nebo **akční sloveso** k obsahu dialogu („Otevřít", „Použít", „Přidat") - akční sloveso je vhodnější, když je jasnější než generické OK.
- Tlačítka **akčně pojmenovaná**, ne „Ano/Ne" - např. „Smazat" / „Storno", „Uložit" / „Zahodit", ne „Yes" / „No".
- Destruktivní akce a confirmation → vždy přes `IPopupDisplayer.Confirm` z EDControls (R2), nikdy `MessageBox.Show`.
- Multi-tab dialog: **max 3 taby**. Pokud potřebuješ víc, rozděl do samostatných dialogů nebo restrukturuj.

---

## 2. Tooltipy

- **Každý ikonový button** v toolbaru / paletě **musí mít** `ToolTip` - bez textu je tlačítko slepé.
- Tooltip format: `"Akce - klávesa"` (např. `"Duplikovat - Ctrl+D"`).
- Pokud akce nemá keyboard shortcut, jen `"Akce"`.

---

## 3. Keyboardové zkratky - autoritativní tabulka

> Autoritativní = odpovídá `MainWindow.xaml.cs` KeyDown handleru (ověřeno 2026-07-03). Při změně
> zkratky v kódu aktualizuj i tuto tabulku. Save As **nemá** zkratku (jen menu/příkaz) - dřívější
> „Ctrl+Shift+S" byl drift, neexistuje.

### Edit operace

| Zkratka | Akce |
|---|---|
| Ctrl+Z / Ctrl+Y | Undo / Redo |
| Ctrl+X / Ctrl+C / Ctrl+V | Cut / Copy / Paste |
| Ctrl+D | Duplikovat (plain duplikát +5mm offset) |
| Ctrl+D + šipka | Směrové duplikování (kopie vedle s 4mm mezerou) |
| Ctrl+Shift+C / Ctrl+Shift+V | Copy / Paste **stylu** (TextStyle + Border + Background) |
| Delete | Smazat výběr |
| Ctrl+A | Vybrat vše (v aktivní sekci) |
| Enter | Vlastnosti výběru (1 element = dialog; víc = dialog společných vlastností) |
| F2 | Inline editace textu (jen TextElement, 1 vybraný) |
| Dvojklik | Vlastnosti elementu (dvojklik do prázdna = zrušit výběr) |

### Uvnitř pevné tabulky (editace buněk na plátně)

⚠️ **Dvojklik zůstává na nastavení i tady** (rozhodnutí Jan 12. 8.). První návrh na něj věsil vstup
do buňky, jenže uživatel má z ostatních tabulek (zastávky, odjezdy, tabulka z dat) naučené, že
dvojklik otevře nastavení - tabulka by byla jediná výjimka **uvnitř naší aplikace**, a ta shoda váží
víc než zvyk z Wordu. Do obsahu se proto vchází **F2** (u textového prvku znamená totéž: „uprav
obsah na místě"), **Alt+klikem** rovnou do zvolené buňky, nebo položkou **„Vyplnit tabulku"**
v místní nabídce. Alt je zvolený záměrně: v aplikaci už znamená „o úroveň hloub" (vytáhnout prvek
ze skupiny) a je to **jeden klik bez F-klávesy** - na noteboocích s Fn řadou to rozhoduje
(rozhodnutí Jan 12. 8. po zkoušení).

⚠️ **Myší cesta dovnitř „druhý klik na už vybranou tabulku" se zkusila a zahodila** (12. 8.):
první klik dvojkliku ji spustil dřív, než dorazil druhý, takže na vybrané tabulce vlezl do buňky
a na nevybrané otevřel nastavení - nepředvídatelné. Odlišit obojí jde jen čekáním na případný
druhý klik, což by vstup do buňky pokaždé zdrželo. **Nezavádět znovu.**

| Zkratka | Akce |
|---|---|
| Alt+klik | dovnitř rovnou do té buňky (hlavní cesta) |
| F2 (nebo místní nabídka) | dovnitř do naposledy kliknuté buňky |
| Dvojklik | vlastnosti prvku (jako u všech ostatních prvků) |
| Alt+Enter (nebo místní nabídka) | **vlastnosti buňky** - týž formulář jako v panelu, jen v okně; samotný Enter musí zůstat posunu o buňku níž |
| Ctrl+B / Ctrl+I / Ctrl+U | tučné / kurzíva / podtržení buňky, i během psaní. **Uvnitř tabulky přebíjejí globální význam** (Ctrl+B jinak sbaluje levý panel) - v tabulce čeká uživatel tučné a globální zkratka mimo tabulku nikam nezmizí |
| Klik (uvnitř) | přepnout buňku |
| psaní | přepíše obsah buňky (jako Excel) |
| F2 | upravit stávající obsah buňky |
| Tab / Shift+Tab | buňka vedle / předchozí (přes konec řádku na další) |
| Enter / Shift+Enter | potvrdit a o buňku níž / výš |
| Šipky | pohyb po buňkách |
| Delete / Backspace | vymazat **obsah buňky** (ne prvek - to je ta past) |
| Ctrl+C / Ctrl+X / Ctrl+V | schránka pracuje s **buňkami**, ne s prvkem; Ctrl+V vloží blok z Excelu (řádky po koncích řádku, buňky po tabulátorech) a tabulku dorovná |
| `{` při psaní | nabídka datových polí; šipky vybírají, Enter nebo Tab doplní i s koncovou závorkou, Esc zavře nabídku (teprve druhý Esc zahodí úpravu) |
| tažení vnitřní hranice | šířka sloupce / výška řádku; funguje i na **vybrané** tabulce, nemusí se dovnitř |
| Esc | ven z buňky na prvek, podruhé zrušit výběr |

**Tažení hranic - dvě pravidla, obě vydřená zkoušením 12. 8.:**

1. **Mění se jen dvojice u hrany** - co jeden získá, druhý ztratí, zbytek tabulky se nehne.
   První návrh dal tažené položce pevný rozměr a zbytek nechal rozdělit mezi všechny ostatní,
   takže se při tažení dolů hýbalo i to nad hranou. U sloupců to nebylo vidět, dokud se táhla
   první hrana.
2. **Vnější hrana patří prvku, ne krajní buňce** - jinak by uživatel nevěděl, jestli mění tabulku,
   nebo sloupec (stejně to má Excel i Word). Krajní sloupec se rozšiřuje hranou vedle něj.

⚠️ **O tom, komu patří klávesy, nesmí rozhodovat fokus plátna.** Kliknutí na tlačítko lišty
(nebo na zaškrtávátko v panelu) fokus plátnu vezme, takže buňka zůstane zvýrazněná, ale psaní
i Delete padají do prázdna - přesně tak to 17. 8. spadlo. Platí proto dvě pravidla: klávesy patří
buňce, **dokud si je nenárokuje pole, kam se opravdu píše** (panel vlastností, formulář prvku, jiný
textbox), a každá akce, která si vzala fokus (tlačítko lišty, zavřený dialog), ho **vrátí buňce** -
do rozepsané buňky, jinak na plátno.

**Uvnitř tabulky patří lišta i panel vlastností VYBRANÉ BUŇCE, ne prvku.** Rozsah editace musí jít
poznat, jinak stojí proti sobě dvě sady týchž vlastností: do 17. 8. panel měnil celou tabulku,
zatímco lišta nad ním jednu buňku, a nešlo je od sebe rozlišit (nález Jan). Platí proto:

- panel u buňky ukazuje **týž formulář jako okno Alt+Enter** (jedno schéma, dva hostitelé - jako
  u prvků; dvě různá by se rozešla) a začíná skupinou **„Vybraná buňka"** s tlačítkem
  **„Nastavení celé tabulky"** (= odchod z buňky, táž cesta jako Esc, jen pojmenovaná),
- formulář prvku má skupinu **„Styl všech buněk"** - z popisku je vidět, že je to výchozí pro celou
  tabulku, ne pro tu jednu,
- pole ukazují **vyřešenou** hodnotu (i tu zděděnou - formulář ji dostane z kaskády, ne ze schématu),
  ale zapisuje se **jen změněná vlastnost**; vymazané pole vrací dědění a zpětný krok nesmí
  zobrazenou hodnotu zapsat natvrdo.

### Grouping / layout

| Zkratka | Akce |
|---|---|
| Ctrl+G / Ctrl+Shift+G | Seskupit / Rozeskupit elementy |
| Alt+klik | Vybrat jen jeden element ze skupiny |
| Ctrl+Shift+H / Ctrl+Shift+J | Split element horizontálně / vertikálně |
| Ctrl+Shift+N | Normalizovat pozice (snap na grid) |
| Šipky | Posun vybraného elementu o krok (Shift = velký krok, Ctrl = jemný krok) |
| Ctrl+PageUp / Ctrl+PageDown | Z-order dopředu / dozadu (+Shift = do krajní pozice) |
| Ctrl+Shift+↑/↓ (aktivní sekce) | Přesun sekce nahoru / dolů |

### Soubory a view

| Zkratka | Akce |
|---|---|
| Ctrl+N | Nová šablona |
| Ctrl+O | Otevřít z databáze |
| Ctrl+S | Uložit (do databáze) |
| Ctrl+P | Tisk |
| Ctrl+Shift+P | Vlastnosti stránky |
| Ctrl+Shift+A | Změnit agendu |
| Ctrl+0 | Přizpůsobit stránce (Fit) - == tlačítko „Fit" |
| Ctrl+1 | Skutečná velikost 100 % (1:1) - == tlačítko „1:1" |
| Ctrl+B | Sbalit / rozbalit levý panel |
| F5 | Obnovit data - refetch GetData aktuální instance + invalidace cache (== tlačítko ↻ u instance chipu) |
| F1 | Nápověda |
| Escape | Zrušit přidávání elementu / probíhající operaci |

### Pravidla pro nové zkratky

- **Nepřebíjet** standardní Windows zkratky (Ctrl+S, Ctrl+Z, Ctrl+C, Alt+F4, F1, Esc).
- **Ctrl+Shift+X** prostor je pro „advanced" varianty (Copy Style, Normalize).
- **Alt+X** prostor je pro alternativní výběr (Alt+klik).
- **Tlačítko a jeho zkratka musí dělat totéž** (Fit ↔ Ctrl+0, 1:1 ↔ Ctrl+1). Při přidání nové zkratky - **doplnit ji do této tabulky**.

---

## 4. Properties panel a dialog

> Detail viz `feature-design-principles.md` §2 UX rozhodovací mřížka.

- **Panel** = nejčastěji měněné properties (rychlý přístup).
- **Dialog** = kompletní cascade, per-column granularita.
- **Šablona / preset** = per-customer kombinace, ne element properties.
- Tri-state placeholder: `null = dědit` → XAML zobrazí *italic šedý* placeholder zděděné hodnoty; „×" reset jen při explicit override.
- Multi-element edit: Enter na multi-select → dialog společných vlastností.

---

## 5. Context menu

- Pravý klik na canvasu = sloučené menu (element items + section items).
- Pořadí položek pro elementy: **Cut / Copy / Paste / Delete / --- / Z-order / --- / Vlastnosti**.
- Separátory oddělují skupiny akcí (editační / pořadí / detail).

---

## 6. Toolbar / paleta

- Ikony z **Fluent UI System Icons** (Microsoft, MIT licence) - viz `EDIcons.xaml`.
- Skupiny tlačítek oddělené `Separator` (Soubor / Upravit / Přidat / Elementy / Vrstva / Zarovnat / Zobrazení).
- Drag-from-palette: `PreviewMouseLeftButtonDown` → ghost preview na canvasu → drop = `AddElementCommand`.

---

## 7. Confirmation a chybové stavy

- **Mazání sekce / šablony** → confirmation dialog povinný (přes `IPopupDisplayer.Confirm`).
- **Mazání elementu** → bez confirmation (Ctrl+Z to vrátí).
- **Neuložené změny při zavření** → `IPopupDisplayer.ConfirmClose` se 3 možnostmi (Save / Discard / Cancel).
- **Validace v UI** - červený rámeček + tooltip s důvodem (B0-11 pattern).

---

## 8. Terminologie UI popisků (jednotná napříč kódem)

Stejná vlastnost = **stejný label** ve všech elementech, dialozích i EDInputForm `.cls`. Label
je zdvojený (PropertyDescriptor v `.cs` **i** EDInputForm `.cls`) → při změně/přidání uprav **obě**
místa. **Před vymýšlením nového popisku grepni, jak se to jmenuje jinde**, a použij zavedený termín.

Kanonické termíny (font/styl textu):

| Vlastnost | Kanonický label | NE |
|---|---|---|
| Rodina písma (FontFamily) | **Font** | ~~Rodina písma~~, ~~Písmo~~ |
| Velikost písma (FontSize) | **Velikost** (`Unit = "pt"`) | ~~Velikost písma~~ |
| Tučné / Kurzíva / Podtržení / Přeškrtnutí | **Tučné** / **Kurzíva** / **Podtržení** / **Přeškrtnutí** | anglicismy |
| Řádkování (LineSpacing) | **Řádkování** | - |
| Barva textu / pozadí buňky | **Barva textu buněk** / **Pozadí buněk** | - |
| Barva textu / pozadí záhlaví | **Barva textu záhlaví** / **Pozadí záhlaví** | - |

Kanonické termíny (obecné vlastnosti - rozhodnutí Jan 2026-07-28):

| Vlastnost | Kanonický label | NE |
|---|---|---|
| Uzamčení elementu (IsLocked) | **Uzamčeno** | ~~Zamčeno~~ |
| Výraz podmíněné viditelnosti (VisibilityExpression) | **Podmínka viditelnosti** | ~~Viditelnost~~ (samostatné; „Viditelnost řádku" je jiný pojem a je OK) |
| Síla čáry (BorderWidth aj.) | **Tloušťka …** („Tloušťka rámečku", „Tloušťka okraje") | ~~Šířka rámečku~~, ~~Tl. …~~ |
| Střídavé podbarvení řádků | **Střídání řádků** | ~~Zebra barva~~ |
| Směry zarovnání | **Vodorovně / Svisle**, stavy **Nahoře / Dole** | ~~Horizontálně/Vertikálně~~, ~~Nahoru/Dolů~~ |
| Přestupy u zastávky | **Přestupy (vlak/autobus)** | ~~(vlak/bus)~~ |

**Strojová pojistka:** `EDInputFormSchemaDriftTests` (ban list zakázaných tvarů + „stejný field key
= stejný label napříč .cls") - nový termín přidej do kanonu ZDE a případně do ban listu testu;
vědomé kontextové rozdíly labelu téhož klíče patří do `exempt` v testu s komentářem.

Kanonické termíny (doména JŘ):

| Pojem | Kanonický label | NE |
|---|---|---|
| Pole „nástupiště" z trasy linky (volný text) | **Označník** (pole `NASTUPISTE` na wire zůstává) | ~~Nástupiště~~, ~~stanoviště~~ |
| Číslo z číselníku sloupků zastávky (AD) | **Sloupek** (pole `SLOUPEK`) | ~~označník~~ |
| Zastřešující pojem pro „ten z těch dvou, který je zvolený" | **Stanoviště** (volba „Číslo stanoviště podle") | konkrétní z těch dvou, když se míní obojí |

⚠️ **Označník a sloupek NEJSOU synonyma** (zjištěno 2026-08-03 u #13088): v AD jsou to dvě různá pole
vedle sebe (volný text vs. číselník s GPS a popisem směru) a zákazník je rozlišuje - do 3. 8. tu bylo
napsané, že „sloupek" je zakázaný tvar téhož, což bylo nepravda. **V MHD se témuž fyzickému stanovišti
doménově říká označník**, takže se názvosloví mezi AD a MHD záměrně liší a nesjednocuje (rozhodnutí
Jan 2026-08-03); MHD pole `SLOUPEK` nemá. Datová mapa obou → `docs/ad-data-mapa.md`.
„Stanoviště" tím přestalo být zakázaný tvar (byl zakázaný jako synonymum označníku) a je to teď
**zastřešující pojem** - používej ho jen tam, kde se opravdu míní „ten zvolený z těch dvou"
(popisky „Více stanovišť", „Řádek pro každé stanoviště", „Číslo stanoviště podle").

Výjimka: text „tisk stanoviště" v legendě CIS atributů (`ZastavkoveJrAd.TextAtributu`, znak „S") je
doslovná parita textů starého modulu - nechává se.

**UI texty jen česky** - žádné cizí výrazy typu „per", „split" v labelech, tooltipech a helpTextech
(„Samostatná stránka pro každé stanoviště", ne „per označník"; „Řádek pro každé stanoviště",
ne „per stanoviště").

**⚠️ Labely enum parametrů sestavy jsou ZAMČENÉ** - hodnota volby se persistuje i porovnává na
serveru jako zobrazovaný text (`RAZENI_VARIANT`, `ROZDELENI_SLOUPCU`, `SCHOVAT_V_ROZSAHU`…);
přejmenování labelu by tiše zahodilo uloženou volbu uživatelů na default. Budoucí přejmenování jen
se stabilním kódem vedle labelu (aditivní wire změna).

Výjimka: složené popisky s jasnou sémantikou (`Výchozí font` = font dokumentu) jsou OK, dokud
obsahují kanonický kořen. Nový termín, který v tabulce není, přidej sem, až ho poprvé zavedeš.

---

## 9. Co `ux-conventions.md` **neřeší**

- Konkrétní rozměry oken (default sizes) - záměrně, je to per-dialog.
- Konkrétní wording („Opravdu chcete..." vs „Smazat?") - záměrně, vyžaduje review s týmem.
- Barvy / theming - záměrně, doménová decision.
- Loading state UX - řešeno per-case (`EDActionInProgress` overlay pro long ops).

Pokud některá z těchto otázek vyplyne v konkrétním requestu, řeší se ad hoc + případně se sem doplní policy.
