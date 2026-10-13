# QR na linkovém JŘ - vyhledávač odjezdů pro cestující (demo, stav 2026-10-01)

> Jednorázový podklad k demu: až se rozhodne, co s ním dál, zjištění se přenesou do trvalých docs
> a soubor se smaže. Záměr drží [`roadmap.md`](roadmap.md) (sekce „QR na vývěsce").

## Záměr

Cestující na zastávce načte QR z výlepu **linkového JŘ AD**. Výlep je tištěný pro konkrétní
zastávku a směr, takže aplikace ví, kde cestující stojí, a předpokládá, že chce jet **dnes**.
Cestující zadá jen cíl a dozví se, za kolik minut jede nejbližší spoj, který tam opravdu dojede.
Hranice: jen „co odsud jede přímo", žádný plánovač s přestupy (tam vyhrává IDOS).

Rozsah (rozhodnutí Jan 1. 10.): demo jako proof of concept, lokální aplikace v `C:\DEV\edwcore`,
zkouší se na notebooku. Zastávkový JŘ, MHD a další linky ze zastávky až později.

## Co demo umí

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

## Odkud jsou data

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

## Tabule odjezdů ISYCloud (12. 10.)

Veřejná stránka ISYCloud `https://isycloud.cz/odjezdy/<CIS číslo zastávky>` bez přihlášení:
nejbližší odjezdy všech linek ze zastávky s odpočtem minut, klik na odjezd rozbalí trasu spoje.
Data jsou z celostátního registru (import JDF z CIS), ne z EDISONu, takže MHD a linky mimo CIS
v ní nejsou. Na zastávkový JŘ AD vede QR bez změny kódu: pole `{CISLO_ZASTAVKA_CIS}` agenda už
má. Ukázka `templates/demo-qr-zastavkovy-jr-ad.json` (v DB CB_CSADAD), QR se schová u zastávky
bez CIS čísla. U CB_CSADAD se číslo zastávky shoduje s CIS číslem (linka 330510).

## Co zůstává otevřené

- **QR z návrháře pro ZL_KOVED** (Babice) neověřené; adresa se zkoušela ručně.
- **Telefon:** ve firemní wifi se zařízení nevidí; funguje kabel s `adb reverse` nebo hotspot
  (poloha přes hotspot jen s výjimkou „insecure origins" v prohlížeči).
- **Zpoždění polohy** několik sekund (hlavně v Brave, který nedává ani kompas) - neřešeno.
- **Jedno QR za označník** místo za linku: přes označník jede víc linek, adresa by nesla označník
  a aplikace by spojila všechno, co tudy jede. Za zastávku (ne za označník) to dnes dělá tabule
  ISYCloud výše.
- **Placení podle spoje** místo celé linky - prodeje nesou i číslo spoje, terminál má vozidlo.
- **Poloha označníků** chybí u CB_CSADAD; bez ní přechod na mapě nejde ukázat.
