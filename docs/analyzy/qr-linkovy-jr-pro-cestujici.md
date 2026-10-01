# QR na linkovém JŘ - aplikace pro cestující (analýza 2026-10-01)

> Snímek ke dni vzniku, neudržuje se. Záměr drží `TemplateDesigner/.claude/docs/roadmap.md`
> (sekce „QR na vývěsce - kam se odsud dostanu").

## Co se po nás chce

Cestující na zastávce načte QR z vytištěného **linkového jízdního řádu AD** a otevře se mu
webová aplikace M-line. Vybere, odkud a kam jede, a aplikace mu řekne, za kolik minut jede
nejbližší spoj, který tam opravdu dojede, a co k němu říká tištěný jízdní řád (bezbariérový
spoj, kola, místenka, omezení).

**Rozsah (rozhodnutí Jan 1. 10.):** demo jako proof of concept, ne hotový produkt.

- jen **linkový JŘ AD** (`LinkovyJrAd.cls`), zastávkový JŘ a MHD později,
- aplikace je **samostatná lokální aplikace v `C:\DEV\edwcore`** (localhost), ne veřejná služba,
- dnešní QR s pevnou adresou se upraví tak, aby **každá stránka** vedla na svoji adresu v té
  aplikaci,
- drobné nejistoty dat (staré dev data, parsování textů omezení) se v demu neřeší.

## Co data linkového JŘ umí

Ověřeno na dev serveru: firma `CB_CSADAD`, skupina linek 1, linka `0330530`
(Kaplice-Velešín-České Budějovice). Jedno načtení vrátí 2 stránky, jednu za směr (T, Z).

**Pro demo je to lepší zdroj než zastávkový JŘ.** Nese přímo tabulku spoj × zastávka s časem
u každé zastávky, takže odpověď „kdy jede spoj z A do B" se jen přečte, nic se nedopočítává.

- **Časy po spojích:** každý spoj má `CASY[i]` zarovnané se `ZASTAVKY[i]` (`LinkovyJrAd.cls:1323-1340`).
  Hodnota je `HH:MM`, `|` = projíždí, `<` = jede jinudy, prázdná = mimo trasu spoje.
- **Příjezd × odjezd:** zastávka s delším pobytem má dva řádky, příjezd a odjezd (`:723-728`).
- **Platnost spoje** jako kódy v `PLATNOST`: `X` (pracovní dny), `1`-`7`, `+`, místenky, kola
  (`PJI`), bezbariérový (`BEV`) a další atributy (`:1288-1293`).
- **Omezení** (`NZ`) mají kód u spoje, ale výklad jen jako text („nejede 24.12.22,31.12.22",
  `SestavTextOmezeni` `:2024`). Data jako data se neposílají.
- **Značky u zastávky** v `ZNACKY[i]`: `x` na znamení, `V` jen výstup (`:1211` a dál).
- **Návaznosti** v `PRIPOJE` (čeká na / navazuje na linku a spoj, s časem).
- **Zastávky:** `CZST` (stálý klíč číselníku zastávek), název, `NASTUPISTE` (označník), zóny, km
  (`:844-899`).
- **Legenda** `POZNAMKY {DRUH, TYP, ZNAK, TEXT}` - druh 1 týdenní platnost, 2 omezení,
  3 návaznosti, 5 atributy, 6 místenky, 7 pevné značky, 8-9 poznámky linky.
- Spoj jedoucí ve víc skupinách dnů je ve výstupu víckrát - aplikace ho sloučí podle `CISLO_SPOJE`.

**Ukázka z reálných dat** - Velešín,aut.nádr. → České Budějovice,aut.nádr., pracovní den:
25 spojů, např. spoj 1 odjezd 4:35 / příjezd 5:10, spoj 205 5:48 / 6:25, spoj 13 jen v pátek
13:20 / 13:50, spoj 237 s omezením „nejede 24.12., 31.12." 22:30 / 22:55.

## Co už existuje a jen se nenapojilo

- **QR prvek dosazuje datová pole** (`TemplateDesigner/dotnet/Models/Elements/BarcodeElement.cs:105`).
  Adresu jde složit v šabloně, např. `http://localhost:<port>/linka/{CISLO_LINKY}/{SMER}`,
  a každá stránka (směr) pak má svoje QR **bez úpravy kódu**.
- **Pole `QR_KOD`** linkový JŘ už posílá (`LinkovyJrAd.cls:641`, z `PATCH,"QR_CODE"`),
  v dev datech je prázdné. Je to dnešní „obecné QR pro jednu adresu".
- **EDISONKA web už volá Caché** - klient `edwcore/apps/edisonkaweb/backend/src/infrastructure/cache/cache-client.ts`
  (HTTP na `/alwa/sys/rest/QueryJSON/<Třída>/<Metoda>`). Vzor pro to, jak se demo připojí.

## Mezera

1. **Linkový JŘ nezná zastávku, kde cestující stojí.** Stránka linkového JŘ je stejná pro všechny
   zastávky linky, takže QR nese jen linku a směr. Cestující si v demu vybere i „odkud".
   Označník jako výchozí bod by šel až s QR tištěným pro jednotlivou zastávku.
2. **Firma v adrese chybí** - výstup IdFa nenese. Pro lokální demo stačí firma v konfiguraci
   aplikace.
3. **Wire tvar:** `GetData` zapisuje do `^||ML("OUT",n)` (CVar), kdežto klient v `edwcore` volá
   JSON metody (`QueryJSON`, signatura `(sys, in, out)`). Je potřeba tenká Caché metoda,
   která `GetData` zavolá a vrátí JSON, nebo klient na CVar cestu.
4. **„Jede to dnes?"** - pracovní den / sobota / neděle jde z kódů `PLATNOST`, svátky si aplikace
   spočítá sama. Omezení jsou jen text; demo je ukáže, ale nevyhodnotí (nebo je zkusí rozparsovat).
5. **Velikost:** linka je malá (2 stránky), načtení celé linky na jedno QR je pro demo v pořádku.

## Doporučená cesta pro demo

1. **Šablona:** v linkovém JŘ AD přidat (nebo upravit) prvek QR s obsahem
   `http://localhost:<port>/linka/{CISLO_LINKY}/{SMER}`. V TD se nic neprogramuje.
2. **Caché:** tenká JSON metoda nad `LinkovyJrAd.GetData` (linka → stránky obou směrů).
   Výstup agendy se nemění, takže tisk ani zlaté odtisky se nedotknou.
3. **edwcore:** nová lokální aplikace (frontend + tenký backend s Caché klientem podle vzoru
   EDISONKA webu). Obrazovky: linka a směr z adresy → výběr „odkud" a „kam" → nejbližší spoje
   s minutami do odjezdu, časem příjezdu a poznámkami slovy.

Breaking change: žádná (nová metoda, nová aplikace, šablona jen jinak vyplněná).
Nadstavby na později: označník z QR pro zastávku, omezení jako data, zastávkový JŘ a MHD,
další linky ze stejné zastávky.
