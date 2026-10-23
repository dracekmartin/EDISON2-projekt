# QR vyhledávač pro cestující na zastávce (zadání 13. 10., předchozí demo 1. 10.)

> Jednorázový podklad: až aplikace vznikne v ISYCloud, zadání se přenese k ní do edwcore a soubor
> se smaže. Položka v [`roadmap.md`](roadmap.md) („Vyhledávač odjezdů pro cestující").

## Zadání (rozhodnutí Jan 13. 10.)

Náhrada papírového JŘ na zastávce v jeho hlavní otázce: **kam se odsud teď dostanu**. Cestující
nemyslí v linkách, spojích ani značkách, zadává cíl. Přínos je i pro cizince, který IDOS nezná:
naskenuje QR u sloupku a hned má informace o zastávce, kde stojí.

- **QR je lokalizované:** nese zastávku (CIS číslo) a stanoviště nebo označník, pokud je zadané.
  Jeden tvar QR pro všechny vývěsky; poloha z GPS se nepoužívá.
- **Cestující zadá jen cíl.** Pro demo jednoduchý výběr z obcí a zastávek, kam odsud dnes přímo
  něco jede. Místa typu „nemocnice" až později.
- **Odpověď:** nejbližší přímé spoje do cíle - za kolik minut, odjezd a příjezd, doba jízdy;
  linka jen drobně ke kontrole u autobusu. Platnost (dny, svátky, poznámky) vyhodnotí aplikace,
  značky cestující nevidí. Čas je „teď"; když už dnes nic nejede, první spoj dalšího dne.
- **Jede z jiného stanoviště téže zastávky** - aplikace řekne odkud. Okolní zastávky až nadstavba.
- **Bez přestupů** - papírový JŘ je taky nezná.
- **Data z CIS** (import JDF v ISYCloud): platná, všichni dopravci. QR není součást výlepu
  konkrétního dopravce, proto ne EDISON.
- **Doplněk z EDISONu** tam, kde je dopravce u nás: **označníky** (ze kterého jede linka v daném
  směru, poloha) a **způsoby platby**. QR z návrháře nese i označník, na kterém visí, a aplikace
  řekne „jede z protějšího označníku 16, 40 m". Spoj z CIS se páruje přes číslo linky a směr
  (JDF: liché číslo spoje tam, sudé zpět). Do ISYCloud jde **exportem**, ne živým dotazem
  (veřejná aplikace nesmí záviset na serverech dopravců za VPN); pro demo jednorázově.
- **Platby** nejsou v CIS a EDISON je jako nastavení nemá. Pro demo **zástupná data**; zadávat
  je má dopravce nebo organizátor (kdo a kde, se rozhodne).
- **Demo na ZL_KOVED** (Uherské Hradiště, polohy označníků v EDISONu jsou), platby zástupné.
- **Umístění:** aplikace v ISYCloud vedle tabule odjezdů (např. `/qr-vyhledavac/<CIS číslo>`),
  nasazení se doladí. Čeština a angličtina podle jazyka telefonu.
- **Do budoucna:** zpoždění z dispečinku, okolní zastávky, MHD (v CIS není).
- **Proti demu z 1. 10. odpadá:** platby (z prodejů v EDISONu) a mapa označníků (CIS je nemá).

**Otevřené rozhodnutí:** nabídka cílů je ze **všech sloupků zastávky** (jinak by „jede z protějšího
sloupku" nešlo říct) a z jízd **do konce zítřka** - cíl obsluhovaný jen o víkendu se ve středu
nenabídne. Natáhnout na 7 dní jako hledání nejbližšího spoje? Pro demo zůstává (Jan 13. 10.).

**Číslo zastávky pro adresu v QR je v každé agendě v jiném poli** (review `B193h`, odloženo sem
Janem 23. 10.): ukázkové vývěsky AD skládají adresu z `{CISLO_ZASTAVKA_CIS}` (CIS číslo, + `{SLOUPEK}`),
linkové JŘ z `{AKTUALNI_ZASTAVKA_CZST}` + `{AKTUALNI_ZASTAVKA_TC}` (interní číslo, starší lokální demo).
Vývěska MHD nese `{CISLO_ZASTAVKA}` jako „číslo-označník" (`ZastavkoveJrMhd.cls:1122`), CIS číslo nemá.
Až se tu rozhodne tvar adresy, vybrat jedno pole pro všechny agendy a doplnit ho tam, kde chybí.

## Stav 13. 10. - nasazeno v ISYCloud 0.37.1

- **Aplikace:** `https://isycloud.cz/qr-vyhledavac/<CIS číslo>?sloupek=<id sloupku>` (edwcore
  `apps/isycloud`, stránka `pages/QrVyhledavac`, služba `qrVyhledavacService.ts`), CZ/EN/DE/PL.
  Ladicí parametry adresy: `&datum=&cas=` (jiné „teď"), `&cil=o:Brno` (hotový výsledek), `&lang=`.
- **Sloupky CIS nezná** (Zlín i UH nádraží: 0 stanovišť u tisíců zastavení), proto doplněk
  z EDISONu: tabulka `bus_qr_doplnek`, nahrává admin (`POST /api/portal/autobusy/qr-doplnky/:zdroj`).
  Na produkci nahrané ZL_KOVED (sloupky, **bez** zástupných plateb) a IDZK (převod na místa e-shopu).
  Export z EDISONu: `ALVA.TiskyQrDemo.Cestujici.ExportSloupku`.
- **Místenka:** odkaz z textu dopravce v CIS (`bus_mistenky`), nebo e-shop z doplňku pro linky, které
  e-shop prodává, i když to dopravce v CIS neuvedl. E-shop IDZK (Bileto) jde předvyplnit
  `#/search-results?from=&to=&datetime=`, ale místa čísluje po svém - převodní tabulka je pro demo
  ruční (Zlín, Brno, Vsetín, Luhačovice, Uherský Brod); do provozu ji má dodat KOVED/Bileto.
- **Opravena chyba ISYCloud:** časový kód „jede od-do" (typ 1) se četl jako dny navíc, sezónní
  a prázdninové spoje jezdily celý rok (tabule, vyhledávač spojení, GTFS). Kolegovi oznámit.
- **Šablony v ZL_KOVED:** „QR na zastávku - vyhledávač pro cestující (A5 na výšku)"
  (`templates/qr-zastavka-vyhledavac-a5.json`, list pro každý sloupek) a „Demo QR - vyhledávač pro
  cestující (zastávkový JŘ AD)". Ukázka místenky: linka 823955, Zlín aut. nádr. sloupek 1, cíl Brno.

**Otevřené:** samostatná agenda „QR zastávek" (dnes tisk po linkách, sloupek na více linkách vyjde
vícekrát); doplněk do provozu pravidelným exportem z EDISONu místo jednorázového nahrání; kdo
zadává platby; limit veřejné části 120 požadavků/min z jedné IP (sál na jedné wifi ho vyčerpá).

## Tabule odjezdů ISYCloud (12. 10.)

Veřejná stránka ISYCloud `https://isycloud.cz/odjezdy/<CIS číslo zastávky>` bez přihlášení:
nejbližší odjezdy všech linek ze zastávky s odpočtem minut, klik na odjezd rozbalí trasu spoje.
Data jsou z celostátního registru (import JDF z CIS), ne z EDISONu, takže MHD a linky mimo CIS
v ní nejsou. Na zastávkový JŘ AD vede QR bez změny kódu: pole `{CISLO_ZASTAVKA_CIS}` agenda už
má. Ukázka `templates/demo-qr-zastavkovy-jr-ad.json` (v DB CB_CSADAD), QR se schová u zastávky
bez CIS čísla. U CB_CSADAD se číslo zastávky shoduje s CIS číslem (linka 330510).

## Předchozí demo (1. 10.) - lokální aplikace nad EDISONem

### Záměr

Cestující na zastávce načte QR z výlepu **linkového JŘ AD**. Výlep je tištěný pro konkrétní
zastávku a směr, takže aplikace ví, kde cestující stojí, a předpokládá, že chce jet **dnes**.
Cestující zadá jen cíl a dozví se, za kolik minut jede nejbližší spoj, který tam opravdu dojede.
Hranice: jen „co odsud jede přímo", žádný plánovač s přestupy (tam vyhrává IDOS).

Rozsah (rozhodnutí Jan 1. 10.): demo jako proof of concept, lokální aplikace v `C:\DEV\edwcore`,
zkouší se na notebooku. Zastávkový JŘ, MHD a další linky ze zastávky až později.

### Co demo umí

- **QR výlepu** nese linku, zastávku, tarifní číslo a směr:
  `http://localhost:5185/linka/{CISLO_LINKY}/{AKTUALNI_ZASTAVKA_CZST}/{AKTUALNI_ZASTAVKA_TC}/{SMER}`.
  Zastávku výlepu určí parametr sestavy „Zvýraznit zastávku" / „Zvýraznit tarifní číslo"; pole
  stránky se vyplní jen při jednoznačné shodě. Parametr „Stránka pro každou zastávku" vytiskne
  výlep pro každou zastávku a směr, každý se svým QR. Ukázka: `templates/demo-qr-linkovy-jr-ad.json`
  (výlepy zapnuté).
- **Aplikace „Vyhledávač odjezdů"** (`edwcore/apps/tisky-qr`), záměrně minimalistická: nahoře
  panel se zastávkou, označníkem a polem „Kam chcete jet?" (linka se nezobrazuje - cestujícího
  zajímá kam, ne čím). Výsledek je jedna karta: velké „za 12 min" (nad hodinu „v 14:20"), časy
  a doba jízdy, štítky platby (obrys) a vybavení (výplň) s ikonami, další odjezdy na odkaz.
  Minuty se přepočítávají každých 30 s; když dnes nic nejede, první spoj v dalších dnech.
- **Co jede dnes** počítá aplikace sama: dny platnosti, státní svátky, sudé/liché týdny a datumová
  omezení. Spoj, který dnes nejede, se neukáže; časové poznámky se nezobrazují vůbec.
- **Opačný směr:** na začátku karty „Jede z protějšího označníku 15 (směr…), 44 m" s odkazem
  na mapu a pěší navigaci. Mapa je jen k tomuhle přechodu: odkud a kam s čísly označníků,
  ostatní označníky zastávky jako tečky, celá obrazovka, poloha cestujícího z GPS (pro demo
  daleko od zastávky posunutá na označník výlepu) a výseč směru z kompasu, bez něj ze směru chůze.
  Poloha na telefonu jde jen přes zabezpečenou adresu nebo `localhost` (kabel a `adb reverse`).
- **Čeština a angličtina** podle jazyka telefonu, přepínač v záhlaví. Česky zůstávají jen místní
  názvy; vlastnost spoje bez překladu se v angličtině neukáže.

Vyzkoušení: `/linka/330510/13906/14/T` (CB_CSADAD, s placením), přechod označníků
`/linka/822995/37411/18/T?firma=ZL_KOVED&cas=07:00` (Uherské Hradiště, 33 označníků) a cíl
opačným směrem (Babice).

### Odkud jsou data

Všechno přes jednorázovou třídu **`ALVA.TiskyQrDemo.Cestujici`** (`CACHE/ALVA/TiskyQrDemo/`) -
schválně mimo `ALVA.TemplateDesigner` a mimo Caché projekt, takže se nevydává s návrhářem.
Volá ji aplikace přes MLRest `QueryJSON`. Do tisku návrháře přibyla jen pole zastávky výlepu
v `LinkovyJrAd.cls` (s přebaselinovanými zlatými odtisky).

- **Stránky linkového JŘ** (`LinkovyJrAd.GetData`) beze změny: tabulka spoj × zastávka s časem.
  Skupinu linek do QR nedáváme (mění se s novým JŘ): třída bere tu, jejíž platnost linky
  zahrnuje dnešek (nejvyšší číslo skupiny nestačí - firmy v nich drží i archivní kopie).
- **Omezení spojů jako data** z `^JRL(sku,lin,smer,sp,11|12|13|22)` (jen / jede / také / nejede)
  a sudé/liché týdny z hlavy spoje. Text omezení se z dat skládá podle nastavení firmy a může být
  bez roku, proto se z něj nečte.
- **Poloha:** zastávka z `^JRZ(czst)` `$`-pole 12/13; označník z `^JRZ(czst,"Sloupek",id)`
  (přednost má platná časová varianta), na který odkazuje trasa linky polem sloupku. Vzdálenost
  označníků počítá server. Detail a vyplněnost po firmách: [`ad-data-mapa.md`](ad-data-mapa.md).
- **Způsob platby** EDISON neeviduje; demo ho vyvodí z prodaných jízdenek modulu Tickets
  (`^LYOBD` denní index, `^LY` pole 11 linka, 22 způsob platby) za poslední tři dny s daty.
  Ven jdou jen počty. Bez dat o prodejích (ZL_KOVED) se blok neukáže - znamená to „nevíme".

### Co zůstává otevřené

- **QR z návrháře pro ZL_KOVED** (Babice) neověřené; adresa se zkoušela ručně.
- **Telefon:** ve firemní wifi se zařízení nevidí; funguje kabel s `adb reverse` nebo hotspot
  (poloha přes hotspot jen s výjimkou „insecure origins" v prohlížeči).
- **Zpoždění polohy** několik sekund (hlavně v Brave, který nedává ani kompas) - neřešeno.
- **Placení podle spoje** místo celé linky - prodeje nesou i číslo spoje, terminál má vozidlo.
- **Poloha označníků** chybí u CB_CSADAD; bez ní přechod na mapě nejde ukázat.
