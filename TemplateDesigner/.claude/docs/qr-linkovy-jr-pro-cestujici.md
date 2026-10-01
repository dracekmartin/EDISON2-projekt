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
- **Aplikace „Vyhledávač odjezdů"** (`edwcore/apps/tisky-qr`): hlavička s linkou, zastávkou,
  označníkem a způsoby platby; pole „Kam chcete jet?" s našeptáváním přes celou linku; nejbližší
  spoj („za 12 min", odjezd, příjezd, doba jízdy) a další spoje; když dnes nic nejede, první spoj
  v dalších dnech (týden dopředu).
- **Co jede dnes** počítá aplikace sama: dny platnosti, státní svátky, sudé/liché týdny a datumová
  omezení. Spoj, který dnes nejede, se neukáže; časové poznámky se nezobrazují vůbec.
- **U spoje** jen vybavení a služby (bezbariérový vůz, kola, místenky…), u cíle na znamení
  připomínka.
- **Opačný směr:** cíl, který leží opačným směrem, pošle cestujícího na protější označník (číslo,
  směr, vzdálenost) a mapa ukáže pěší přechod. Funguje jen tam, kde má označník GPS.
- **Mapa** (`EDMap`, OpenStreetMap): „Jste zde" na označníku výlepu, trasa do cíle, cíl, při
  přechodu přiblížení na pěší úsek a odkaz na pěší navigaci.
- **Čeština a angličtina** podle jazyka telefonu, přepínač v záhlaví. Česky zůstávají jen místní
  názvy (zastávky, trasa linky); vlastnost spoje bez překladu se v angličtině neukáže.

Vyzkoušení: `/linka/330510/13906/14/T` (CB_CSADAD, s placením), přechod označníků
`/linka/822995/204/14/T?firma=ZL_KOVED&cas=07:00` a cíl opačným směrem (Napajedla).

## Odkud jsou data

Všechno přes jednorázovou třídu **`ALVA.TiskyQrDemo.Cestujici`** (`CACHE/ALVA/TiskyQrDemo/`) -
schválně mimo `ALVA.TemplateDesigner` a mimo Caché projekt, takže se nevydává s návrhářem.
Volá ji aplikace přes MLRest `QueryJSON`. Do tisku návrháře přibyla jen pole zastávky výlepu
v `LinkovyJrAd.cls` (s přebaselinovanými zlatými odtisky).

- **Stránky linkového JŘ** (`LinkovyJrAd.GetData`) beze změny: tabulka spoj × zastávka s časem.
  Skupinu linek do QR nedáváme (mění se s novým JŘ), třída bere nejvyšší, ve které linka je.
- **Omezení spojů jako data** z `^JRL(sku,lin,smer,sp,11|12|13|22)` (jen / jede / také / nejede)
  a sudé/liché týdny z hlavy spoje. Text omezení se z dat skládá podle nastavení firmy a může být
  bez roku, proto se z něj nečte.
- **Poloha:** zastávka z `^JRZ(czst)` `$`-pole 12/13; označník z `^JRZ(czst,"Sloupek",id)`
  (přednost má platná časová varianta), na který odkazuje trasa linky polem sloupku. Vzdálenost
  označníků počítá server. Detail a vyplněnost po firmách: [`ad-data-mapa.md`](ad-data-mapa.md).
- **Způsob platby** EDISON neeviduje; demo ho vyvodí z prodaných jízdenek modulu Tickets
  (`^LYOBD` denní index, `^LY` pole 11 linka, 22 způsob platby) za poslední tři dny s daty.
  Ven jdou jen počty. Bez dat o prodejích (ZL_KOVED) se blok neukáže - znamená to „nevíme".

## Co zůstává otevřené

- **QR z návrháře pro ZL_KOVED** (Babice) neověřené; adresa se zkoušela ručně.
- **Telefon:** QR musí nést adresu počítače v síti místo `localhost`.
- **Jedno QR za označník** místo za linku: přes označník jede víc linek, adresa by nesla označník
  a aplikace by spojila všechno, co tudy jede.
- **Placení podle spoje** místo celé linky - prodeje nesou i číslo spoje, terminál má vozidlo.
- **Poloha označníků** chybí u CB_CSADAD; bez ní přechod na mapě nejde ukázat.
