# Návrhář šablon — plán a stav vývoje

> **Seed pro wiki dokument „Novinky a plány"** (zákaznická obdoba changelogu + roadmapy) —
> psáno uživatelským jazykem. Aktualizováno 2026-07-13.
>
> Přehled toho, na čem aktuálně pracujeme, a co je v Návrháři šablon zatím omezené. Než založíte
> tiket na helpdesku ohledně chybějící funkce, mrkněte se sem — možná už ji evidujeme a chystáme.
> Hotové položky se přesouvají dolů do „Co je nově hotové", ať je vidět, co se mezitím vyřešilo.

---

## Plánujeme / zatím nemáme

- **Pevná (Fixed) tabulka** — dnes je k dispozici jen tabulka, jejíž řádky se generují automaticky
  z dat. Tabulka s ručně zadanými řádky a pevným počtem (typicky záhlaví dokladu nebo faktura)
  se připravuje jako samostatný typ prvku.
- **Sloučené buňky v tabulce** — zatím nejde spojit víc buněk do jedné; čeká na pevnou tabulku výše.
- **Mezisoučty ve skupinách řádků** — souhrnný řádek za skupinu dat (např. mezisoučet za
  kategorii ve výpisu) zatím tabulka neumí.
- **Import z PDF — rozpoznání tabulky jako celku** (u obecných dokumentů) — tabulka z PDF se
  naimportuje jako sada jednotlivých čar a textů; automatické složení do jednoho prvku se chystá.
  *(Jízdní řády tento problém nemají — tam strukturu rozpozná AI import.)*
- **Rychlý tisk přímo z agendy** — plánujeme možnost vytisknout sestavu jedním tlačítkem přímo
  u linky/dokladu v EDISONu, bez nutnosti otevírat celý Návrhář.
- **Zastávkový jízdní řád (data z hlavního JŘ modulu) — vzhled legendy poznámek** — obsah
  a pořadí legendy už odpovídá dosavadnímu tisku, dolaďuje se ještě šířka/rozložení.
- **Knižní a linkový jízdní řád** — agenda je v testovacím provozu, data odpovídají realitě,
  doladit zbývá:
  - u linek s velkým počtem spojů se sloupce zatím jen zužují — rozdělení široké tabulky na
    víc stránek zatím nejde,
  - grafické symboly v linkové tabulce se zatím tisknou jako textová zkratka
    (zastávkový JŘ už piktogramy má),
  - pořadí poznámek v legendě je zatím pevně dané,
  - tisk turnusů a obousměrný tisk (oba směry na jedné stránce) zatím nejsou k dispozici.
- **Brožurové řazení stránek / oboustranný tisk** při skládání více vývěsek na list — v1
  imposice skládá jednostranně; duplex zatím řeší tiskárna.

*(Průběžně aktualizujeme. Pokud vám tu něco pro vaši práci citelně chybí, dejte vědět přes
helpdesk — pomůže nám to určit prioritu.)*

---

## Co je nově hotové

**2026-07-13** — **Import PDF s umělou inteligencí**: z existující PDF vývěsky vznikne editovatelná
šablona (AI rozpozná zastávky, odjezdy, poznámky a namapuje datová pole). **AI pomocník**: záložka
Pomocník odpoví, kde v aplikaci co najdete. Obojí vyžaduje aktivaci AI služby u M-line.
**Nápověda (F1)** nově s vyhledáváním (Ctrl+F).

**2026-07-12** — **Více vývěsek na jeden list (imposice)**: např. dvě A5 vývěsky na list A4,
volitelně tatáž vývěska vícekrát pro řezání, včetně ořezových značek — přímo v tiskovém dialogu.
**Rozdělení výstupu do souborů** po linkách nebo zastávkách, s názvem souboru podle dat.

**2026-07-11** — **Přehlednější členění sestav**: agenda (Zastávkový JŘ, Faktury…) × varianta dat
(AD / MHD) — vidíte ji ve stavovém řádku, titulku okna i přehledu šablon.

**2026-07-10** — Zastávkový JŘ: volitelný sloupec **nástupiště**, dopravní **piktogramy**
(bezbariérové spoje, denní značky ①–⑦) shodné s dosavadními jízdními řády, souhrn „všechny
zastávky na znamení" místo značky u každé.

**2026-07-08** — Tabulka: **více pravidel zvýraznění najednou** (seznam pravidel podmíněného
formátování řádků).

**2026-07-05** — Tabulka: podmíněná viditelnost řádku (řádek, který nesplňuje podmínku, se
v tisku vůbec neobjeví) a podmíněné zvýraznění řádku (např. záporná částka se automaticky
podbarví); dále počítané sloupce (hodnota dopočítaná výrazem nad ostatními poli řádku) a
minimální/maximální počet řádků (pro tabulky s pevným vzhledem, např. fakturu).

**2026-07-02** — Zastávkový jízdní řád (data z hlavního JŘ modulu): zobrazení víc variant délky
jízdy ve sloupci časů, pokud různé spoje jedou stejným úsekem různě dlouho.

**2026-06-30** — Šablony se nově ukládají do centrální databáze sdílené v rámci firmy, místo
souborů na jednotlivých počítačích. Export do souboru zůstává jako doplňková možnost (např. pro
poslání e-mailem).

**2026-06** — Zastávkový jízdní řád z hlavního modulu Jízdní řády (AD) — datový obsah ověřen
1:1 proti dosavadnímu tisku na řadě konkrétních linek.
