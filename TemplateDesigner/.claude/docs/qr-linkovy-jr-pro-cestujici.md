# QR na linkovém JŘ - aplikace pro cestující (analýza 2026-10-01)

> Jednorázová analýza: až se demo postaví a zjištění se přenesou do trvalých docs, soubor se
> smaže. Záměr drží [`roadmap.md`](roadmap.md) (sekce „QR na vývěsce - kam se odsud dostanu").

## Co se po nás chce

Cestující na zastávce načte QR z vytištěného **linkového jízdního řádu AD** a otevře se mu
webová aplikace M-line. **Každý výlep je tištěný pro konkrétní zastávku (označník)**, takže
aplikace ví, kde cestující stojí, a **ví, že chce jet dnes** - nic z toho nevybírá. Vybere jen
cíl a aplikace mu řekne, za kolik minut jede nejbližší spoj, který tam opravdu dojede, a co
k němu říká tištěný jízdní řád (bezbariérový spoj, kola, místenka, omezení).

**Rozsah (rozhodnutí Jan 1. 10.):** demo jako proof of concept, ne hotový produkt.

- jen **linkový JŘ AD** (`LinkovyJrAd.cls`), zastávkový JŘ a MHD později,
- aplikace je **samostatná lokální aplikace v `C:\DEV\edwcore`** (localhost), ne veřejná služba,
- dnešní QR s pevnou adresou se upraví tak, aby **každá stránka** vedla na svoji adresu v té
  aplikaci,
- zkouší se na notebooku (telefon a adresa v místní síti později),
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
  Adresu jde složit v šabloně z polí stránky, např. `http://localhost:<port>/linka/{CISLO_LINKY}/...`,
  a každý výlep pak má svoje QR bez úpravy návrháře.
- **Pole `QR_KOD`** linkový JŘ už posílá (`LinkovyJrAd.cls:641`, z `PATCH,"QR_CODE"`),
  v dev datech je prázdné. Je to dnešní „obecné QR pro jednu adresu".
- **EDISONKA web už volá Caché** - klient `edwcore/apps/edisonkaweb/backend/src/infrastructure/cache/cache-client.ts`
  (HTTP na `/alwa/sys/rest/QueryJSON/<Třída>/<Metoda>`). Vzor pro to, jak se demo připojí.

## Mezera

1. **Zastávka výlepu se nedostane do QR.** Výlep pro konkrétní zastávku linkový JŘ umí už dnes:
   parametry sestavy „Zvýraznit zastávku" a „Zvýraznit tarifní číslo" (`LinkovyJrAd.cls:333-347`)
   označí řádek zastávky jako aktuální (`:871`). Je to ale příznak v řádku tabulky a QR dosazuje
   jen pole stránky. Chybí pole stránky s klíčem zvýrazněné zastávky (`CZST` + tarifní číslo) -
   přidané pole, tisk se nezmění. Směr v QR být nemusí: ze zastávky jede linka oběma směry
   a aplikace nabídne cíle tam i zpět.
2. **Firma v adrese chybí** - výstup IdFa nenese. Pro lokální demo stačí firma v konfiguraci
   aplikace.
3. **Wire tvar:** `GetData` zapisuje do `^||ML("OUT",n)` (CVar), kdežto klient v `edwcore` volá
   JSON metody (`QueryJSON`, signatura `(sys, in, out)`). Je potřeba tenká Caché metoda,
   která `GetData` zavolá a vrátí JSON, nebo klient na CVar cestu.
4. **„Jede to dnes?"** - aplikace vezme dnešní datum: pracovní den / den v týdnu / neděle
   a svátky jdou z kódů `PLATNOST` (`X`, `1`-`7`, `+`), státní svátky si spočítá sama a na svátek
   se chová jako v neděli. Omezení jsou jen text; demo je u spoje ukáže a jednoduché tvary
   („nejede 24.12.22") zkusí rozpoznat.
5. **Velikost:** linka je malá (2 stránky), načtení celé linky na jedno QR je pro demo v pořádku.

## Postup po krocích

Každý krok končí něčím, co jde vyzkoušet. Byznysová logika („co jede dnes") je v backendu
aplikace, Caché jen vydá data - stejné rozdělení jako u EDISONKA webu.

1. **QR výlepu nese zastávku.** Pole stránky se zvýrazněnou zastávkou v `LinkovyJrAd.cls`,
   v šabloně linkového JŘ AD prvek QR s adresou `<začátek adresy>/linka/{CISLO_LINKY}/<zastávka>`.
   *Vyzkoušíš:* v návrháři zadáš „Zvýraznit zastávku" (Velešín), náhled ukáže QR s adresou
   s linkou a zastávkou. Jiná zastávka = jiné QR.
2. **Aplikace ukáže, kde cestující stojí.** Tenká JSON metoda v Caché nad `GetData`, v `edwcore`
   nová lokální aplikace (backend s Caché klientem podle EDISONKA webu, frontend). Stránka podle
   adresy ukáže zastávku, linku, označník a seznam cílů v obou směrech.
   *Vyzkoušíš:* otevřeš adresu z QR z kroku 1 v prohlížeči a vidíš „Velešín,aut.nádr. - linka
   530" a kam se odsud dá dojet.
3. **Nejbližší spoj dnes.** Po výběru cíle nejbližší spoje, které dnes jedou a v cíli staví:
   za kolik minut odjezd, kdy příjezd.
   *Vyzkoušíš:* vybereš České Budějovice a porovnáš s tištěným JŘ pro zvolený den (pracovní
   den, sobota, svátek).
4. **Poznámky slovy.** U spoje bezbariérový vůz, kola, místenka, návaznost a text omezení;
   spoj s rozpoznaným omezením na dnešek se vyřadí.
   *Vyzkoušíš:* na 24. 12. zmizí spoj s „nejede 24.12." a u ostatních čteš poznámky.

Breaking change: žádná (přidané pole, nová metoda, nová aplikace, šablona jen jinak vyplněná).
Nadstavby na později: zastávkový JŘ a MHD, další linky ze stejné zastávky, **mapka a jednoduchá
navigace k jinému označníku** (přání Jan 1. 10.; GPS má číselník sloupků AD, volný text označníku
ne - viz `ad-data-mapa.md`, v `edwcore` je `EDMap` a mapové služby `apps/mapy`).

**Stav 1. 10.:** kroky 1-4 v základu hotové. Cestující zadá cíl, aplikace najde spoje a u cíle
opačným směrem pošle na protější označník (QR nese i směr výlepu). Omezení se nevyhodnocují
z textu (skládá se podle nastavení firmy, někdy bez roku), ale z dat spoje
`^JRL(sku,lin,smer,sp,11|12|13|22)`, která přidává `API.Cestujici`; spoj, který dnes nejede,
se cestujícímu neukáže a text omezení se nezobrazuje.
