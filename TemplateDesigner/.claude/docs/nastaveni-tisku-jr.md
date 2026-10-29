# Nastavení tisku v jízdních řádech - co přebírá návrhář (stav 29. 10.)

> Přehled pro schůzku s kolegy z jízdních řádů: která nastavení tisku v EDISONu návrhář už přebírá,
> která ne, a co by šlo zrušit a definovat v šabloně. Ověřeno čtením kódu agend a zdroje starého
> modulu (`JRMAIN/CACHE/JRapiLin.mac`, `JRapiLin1.mac`, `JRapiSet.mac`).
> **Jednorázový podklad:** po schůzce rozhodnutí zapsat do roadmapy a trvalých dokumentů
> (`legacy-nahrada.md`, `ad-data-mapa.md`) a tento soubor smazat.

Stavy: **bereme** (návrhář nastavení čte, šablona ho nepřebije) · **bereme, šablona přebije** (parametr
s volbou „Automaticky (dle linky)") · **nebereme** (nahrazuje šablona, nebo chybí).

## 1. Jízdní řády AD - linka - dialog „Nastavení pro tisk linky" (82 polí)

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
  tras a lokální název. Automatické generování časových průběhů nebereme, varianty počítáme vždy.

## 2. Jízdní řády AD - linka - vlastnosti linky, záložky „MHD - další" a „MHD"

- **Bereme, šablona přebije:** rozvržení sloupců vývěsky (pracovní / sobota / neděle a varianty).
- **Bereme:** prázdninové dělení sloupců, vyřazení spojů ze sloupce, nevypisovat značku ve sloupci,
  vlastní nadpisy sloupců. Vlastní sloupce v šabloně tohle všechno nahradí.
- **Bereme jako údaje** (šablona je jen umístí): poznámka nad linkou a pod dopravcem, QR, typ JŘ
  (výluka, návrh), lokální označení, informace, komentáře, „jede na objednání / podmínečně", spoje
  nepoznámkovat.
- **Nebereme:** jednosměrná linka.

## 3. Jízdní řády AD - nastavení programu za firmu, část tisk linek a MHD (asi 50 polí)

- **Bereme:** výchozí rok v omezeních a písmena v zóně (linka je smí přepsat), které atributy spoje
  dostanou větu v legendě, tvar místenkování, texty „na objednání / podmínečně". Z tiskových programů
  firmy jen to, jestli se tiskne bezbariérovost (zastávkový JŘ).
- **Nebereme, nahrazuje šablona:** měřítka, okraje, písma, rámečky, zvýraznění zastávky na vývěsce
  (dnes parametr sestavy), pořadí poznámek.
- **Nebereme, chybí:** pořadí a souhrn dopravců, vlastní texty IDP-P / IDP-Z.
- **Bereme jako údaje:** číselníky textů značek a dopravci.

## 4. Jízdní řády AD - pod linkou (tarifní číslo, spoj)

- **Bereme jako údaje:** barva spoje, ruční pořadí spojů, pokračování a návaznosti, místenka.
- **Nebereme:** netisknout tarifní číslo v knižním JŘ, ruční přiřazení spojů do sekcí
  (v datech 0 linek, odloženo `B98`).

## 5. MHD (modul MHD)

- **Linka - bereme:** automatické poznámkování odlišností v platnosti a rozdílů v trase, QR, výluka / návrh.
- **Firma - bereme:** rozsah čísel negativních značek.
- **Číselníky - bereme:** typy provozu (sloučení do sloupců, nadpisy, pořadí), poznámky ke spojům.

## 6. Volby ve starém tiskovém dialogu (neukládají se)

Dělení podle platnosti, způsob tisku poznámek, zvýraznění zastávky a tarifního čísla, turnusy, obousměrný
JŘ, návaznosti, výběr typů provozu, rámečky čísel spojů, prázdné sloupce. U nás parametry sestavy nebo
vlastnosti šablony; zaniknou se starým tiskem.

## Co z toho plyne

- **Zrušit se starým tiskem:** rozvržení v „Nastavení pro tisk linky" (asi 60 polí) a v nastavení
  programu. Návrhář je nečte a šablona je umí sama.
- **Kandidáti na přesun do šablony** (mění vzhled, ne údaje): rok v omezeních, zóny a km, písmena v zóně,
  sloupec tarifních čísel, jen zastávky, kde spoje zastavují, bezbariérovost nad spojem nebo v poznámkách,
  tvar místenkování, rozvržení sloupců vývěsky, nevypisovat značku ve sloupci, vlastní nadpisy sloupců.
  Kde nastavení používá hodně linek, nejdřív volba „Automaticky (dle linky)" (`ux-conventions §8`).
- **Zůstává v jízdních řádech:** údaje z části 2, 4 a 5 (poznámky, QR, typ JŘ, barvy a pořadí spojů,
  číselníky, dopravci).
- **Rozhodnout s kolegy:** které kandidáty přesunout; jestli chybějící nastavení (číslo linky, cizojazyčné
  poznámky, km pro jednosměrné směry, návaznosti, pořadí dopravců, texty IDP) někdo používá; jak se
  jmenují volby rozvržení sloupců, které mají vývěska a linkový JŘ různě (`P2`, [`legacy-nahrada.md §4`](legacy-nahrada.md)).
