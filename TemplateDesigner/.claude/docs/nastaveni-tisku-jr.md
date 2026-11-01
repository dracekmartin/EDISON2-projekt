# Nastavení tisku v jízdních řádech - co přebírá návrhář (stav 29. 10.)

> Přehled pro schůzku s kolegy z jízdních řádů: která nastavení tisku v EDISONu návrhář už přebírá,
> která ne, a co by šlo zrušit a definovat v šabloně. Co návrhář čte, je ověřené čtením kódu agend; kde
> se nastavení otevírá, je ze zdroje klientů (`C:\DEV\EDISON\JRMAIN`, `ISYJR2`, `JRSETTINGS`, `MHD2*`),
> v běžící aplikaci neproklikané.
> **Jednorázový podklad.** Schůzka 29. 10.: většinu tiskových nastavení kolegové z jízdních řádů vyhodí,
> zbytek dodělají. Soubor zůstává jako podklad pro srovnání agend po úklidu (co agendy čtou a odkud);
> smazat, až kolegové nastavení uklidí a agendy se srovnají.

Stavy: **bereme** (návrhář nastavení čte, šablona ho nepřebije) · **bereme, šablona přebije** (parametr
s volbou „Automaticky (dle linky)") · **nebereme** (nahrazuje šablona, nebo chybí).

**Okno linky** (výchozí bod částí 1, 2 a 4): v Jízdních řádech vybrat linku a **Upravit linku**. Otevře se
okno **„Editace Linky"** (starý editor; nový editor linky je v Jízdních řádech zatím vypnutý).

## 1. „Nastavení pro tisk linky" (u linky, 82 polí)

**Kde:** okno linky → tlačítko **Nastavení k tisku** (nebo Nástroje → Nastavení k tisku).

- **Rozvržení starého tisku, asi 60 polí - nebereme, nahrazuje šablona.** Počty spojů na stranu,
  prázdné sloupce, měřítko, okraje, písma, šířky sloupců, ruční zlom, výška JŘ, dělená linka na stránky,
  podtržení, řazení a zalamování poznámek, celý Win tisk A4/A5 (šablona, písma, druhy a způsob poznámek).
- **Co se na výtisku ukáže - bereme, šablona přebije:** sloupec tarifních čísel, jen zastávky, kde spoje
  zastavují, písmena v označení zóny (jen linkový JŘ; zastávkový je bere bez přebití).
- **Bereme:** rok v omezeních, tisk zón a km (jen linkový JŘ, zastávkový řídí šablona), ruční název zastávky
  pro MHD (jen linkový JŘ, zastávkový ho bere vždy), bezbariérové vozidlo nad spojem nebo v poznámkách
  (jen linkový JŘ). Barevný tisk spojů je jen záloha, rozhoduje šablona.
- **Nebereme, chybí:** tištěné číslo linky (trojmístné, vlastní, tablo), cizojazyčné poznámky,
  km sloupce pro jednosměrné směry, způsob výpisu návazností.
- **Vývěska MHD (5 polí) - bereme** poznámky o odchylkách v trase a jejich písmena, značky minutových
  tras a lokální název. Automatické generování časových průběhů bereme jen s volbou „Čas jízdy podle
  nastavení linky" (od 1. 11., `B144`), jinak čas jízdy i varianty počítáme vždy.

## 2. „Editace vlastností linky" a pořadí spojů (u linky)

**Kde:** okno linky → tlačítko **Vlastnosti** (nebo Nástroje → Vlastnosti linky). Záložky Obecné,
Poznámky 1 a 2, Místenkování, MHD, MHD - poznámky, **MHD - další** a QR kód. Ruční pořadí spojů:
okno linky → Nástroje → **Pořadí spojů pro tisk**.

- **Bereme, šablona přebije:** rozvržení sloupců vývěsky (pracovní / sobota / neděle a varianty),
  záložka MHD - další.
- **Bereme:** prázdninové dělení sloupců, vyřazení spojů ze sloupce, nevypisovat značku ve sloupci
  (vše MHD - další), výběr sady vlastních nadpisů sloupců. Vlastní sloupce v šabloně tohle všechno nahradí.
- **Bereme jako údaje** (šablona je jen umístí): poznámka nad linkou a pod dopravcem, QR, typ JŘ
  (výluka, návrh), lokální označení, informace, komentáře, „jede na objednání / podmínečně", spoje
  nepoznámkovat, ruční pořadí spojů.
- **Nebereme:** jednosměrná linka.

## 3. „Jízdní řády - nastavení" (za firmu, část tisk asi 50 polí)

**Kde:** okno **„Jízdní řády - nastavení"** starého programu nastavení jízdních řádů (`JRSETTINGS`). Nový
klient jízdních řádů má stránku Tisk připravenou, ale vypnutou. Cestu v menu EDISONu jsem neověřil.
Skupiny, kterých se to týká:

- **Bereme:** „Tisk PS - linky, omezení" (způsob tisku roku v omezeních, způsob tisku místenkování,
  upřesnění značek „jede jen na objednání / podmínečně"), „Tisk značky pro atribut nad spoj" (které
  atributy dostanou větu v legendě), „Zóny - tisk a export do JDF bez písmen" (stránka Systém - Linky).
  Rok v omezeních a písmena v zóně jsou výchozí hodnoty, linka je smí přepsat.
- **Bereme:** sady vlastních nadpisů sloupců vývěsky („Win MHD - zastávkové", „Nadpisy sekcí pro rozvržení …").
  Jestli se tiskne bezbariérovost (zastávkový JŘ), je uložené v tiskovém programu PostScript, ne ve formuláři.
- **Nebereme, nahrazuje šablona:** „Tisky PS - měřítko, okraje", fonty, okraje, rámečky, „Linky - zvýrazněná
  zastávka" (dnes parametr sestavy), pořadí tisku poznámek, tisk stálých značek.
- **Nebereme, chybí:** „Tisk PS - linky - souhrn" (pořadí a souhrn dopravců), „PS MHD - vývěsní, knižní
  jízdní řády" (vlastní texty IDP-P / IDP-Z).
- **Bereme jako údaje:** číselníky textů značek a dopravci.

## 4. Pod linkou (spoj, tarifní číslo)

**Kde:** okno linky → spoj (barva, pokračování, návaznosti, místenka); tarifní čísla v trase linky.

- **Bereme jako údaje:** barva spoje, pokračování a návaznosti, místenka.
- **Nebereme:** netisknout tarifní číslo v knižním JŘ, ruční přiřazení spojů do sekcí
  (v datech 0 linek, odloženo `B98`).

## 5. Modul MHD

**Kde:** MHD Linky → okno **„Vlastnosti linek"**, skupina **Tisk**; MHD Číselníky → **Nastavení**, skupina
**Negativní značky**; MHD Číselníky → typy provozu a poznámky ke spojům.

- **Linka - bereme:** „Automaticky poznámkovat odlišnosti v platnostech spojů", „Automaticky poznámkovat
  rozdíly v trase spojů" s algoritmem a písmeny, QR kód, poznámka nad linkou, výluka / návrh.
- **Firma - bereme:** „Rozsah pro přidělování NZ od / do".
- **Číselníky - bereme:** typy provozu (sloučení do sloupců, nadpisy, pořadí), poznámky ke spojům.

## 6. Volby ve starém tiskovém dialogu (neukládají se)

**Kde:** okno linky → **Tisky** → Tisk JŘ, Tisk JŘ MHD (zastávkové); okno linky → **Postscriptové výstupy**
(vývěsní, knižní, vývěsní pro MHD).

Dělení podle platnosti, způsob tisku poznámek, zvýraznění zastávky a tarifního čísla, turnusy, obousměrný
JŘ, návaznosti, výběr typů provozu, rámečky čísel spojů, prázdné sloupce. U nás parametry sestavy nebo
vlastnosti šablony; zaniknou se starým tiskem.

## Co z toho plyne

Jízdní řády už dnes hlásí, že **starý návrhář sestav skončí 31. 1. 2027** a k tisku linkových
a zastávkových JŘ se má používat nový návrhář.

- **Zrušit se starým tiskem:** rozvržení v „Nastavení pro tisk linky" (asi 60 polí) a tiskové skupiny
  rozvržení v „Jízdní řády - nastavení". Návrhář je nečte a šablona je umí sama.
- **Kandidáti na přesun do šablony** (mění vzhled, ne údaje): rok v omezeních, zóny a km, písmena v zóně,
  sloupec tarifních čísel, jen zastávky, kde spoje zastavují, bezbariérovost nad spojem nebo v poznámkách,
  tvar místenkování, rozvržení sloupců vývěsky, nevypisovat značku ve sloupci, vlastní nadpisy sloupců.
  Kde nastavení používá hodně linek, nejdřív volba „Automaticky (dle linky)" (`ux-conventions §8`).
- **Zůstává v jízdních řádech:** údaje z části 2, 4 a 5 (poznámky, QR, typ JŘ, barvy a pořadí spojů,
  číselníky, dopravci).
- **Rozhodnout s kolegy:** které kandidáty přesunout; jestli chybějící nastavení (číslo linky, cizojazyčné
  poznámky, km pro jednosměrné směry, návaznosti, pořadí dopravců, texty IDP) někdo používá.
  Volby rozvržení sloupců mají vývěska a linkový JŘ od 1. 11. pojmenované stejně (`P2`, [`legacy-nahrada.md §4`](legacy-nahrada.md));
  když úklid sadu voleb změní, přejmenovat popisek jde bez ztráty uložených voleb.
