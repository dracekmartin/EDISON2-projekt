Include GENERAL

/// Agenda: **Linkový JŘ z dat autobusové dopravy (AD)** - celá linka v jednom tisku:
/// svislý seznam zastávek (tarifní čísla) × vodorovné sloupce jednotlivých SPOJŮ s časy v buňkách.
/// Legacy ekvivalent `ALVA.TISKJR` (klient) + `mkPLinWin^JRapiLinPrtWin` (JRMAIN, skutečný engine).
/// 
/// Datový zdroj (stejný jako `ZastavkoveJrAd`):
/// - `^[dataDB]JRL(sku,lin,...)` - linka ("I"), trasa ("J"), spoje ("T"/"Z"), trasy spojů ("W"),
///   km sloupce (PATCH,"KmsSmer"), návaznosti (spoj,31/32), omezení (spoj,11-15/22), PATCH metadata
/// - `^[dataDB]JRZ(czst,...)`    - zastávky (názvy)
/// - `^[dataDB]JRSET(...)`       - dopravci ("DOP"), texty legend ("NCOME"/"STALCOME"),
///   nadpisy skupin dnů ("Definice","WinMHD",rec,"DSC"), formát datumů ("Definice","TiskDtm")
/// 
/// ── Wire formát (čistě JSON, R3) - 1 stránka per (linka × směr) ──────────────────
/// **Sémantický tvar** (data = CO, element = JAK): element `LineTableElement` si matici
/// [zastávka × spoj] skládá sám; Caché neposílá layout (žádný legacy `idobj` model).
/// 
/// Názvy polí sdílí se zastávkovým JŘ (tam, kde jde o tutéž věc) - hlídá AgendaSchemaDriftTests.
///
///   values: CISLO_LINKY, LOKALNI_OZNACENI_LINKY, POPIS_LINKY, BARVA_LINKY(_2), POZNAMKA_NAD_LINKU, POZNAMKA_POD_DOPRAVCE, POPIS_DOPRAVCE,
///           DATUM_PLATNOSTI_OD/DO, POPIS_PLATNOSTI (jen pro starší šablony), TYP_JR, QR_KOD, SMER ("T"/"Z"), KONECNA_ZASTAVKA,
///           VYCHOZI_ZASTAVKA, AKTUALNI_ZASTAVKA_CZST/TC (zvýrazněná zastávka, je-li jednoznačná)
///   rows.ZASTAVKY:    [{ PORADI, TYP ("zastavka" hlavní/odjezdový řádek | "prijezd" vložený
///                        příjezdový u zastávek s atributem P/J), CZST, NAZEV_ZASTAVKY, TC, ZONY,
///                        MHD, KM:[per km-sloupec směru], ATRIBUTY:[znaky], NASTUPISTE }]
///                     - v pořadí směru jízdy (T vzestupně, Z sestupně)
///   rows.SPOJE:       [{ CISLO_SPOJE, SKUPINA, NZ, PLATNOST:[znaky], CASY:[zarovnané na ZASTAVKY:
///                        "HH:MM" | "" (mimo trasu) | "|" (projíždí) | "<" (jede jinudy)],
///                        PRIPOJE:[{ TYP ("vycka"/"navazuje"), PORADI, ZNAK, TEXT (směr), CAS, LINKA,
///                        SPOJ, AVL (A autobus / V vlak / L loď - prvek podle něj seskupuje řádky),
///                        RADEK (0/1 - legacy „typ výstupu": tisknout jako vložený řádek) }] }]
///                     - řazené dle legacy mkPoradiSp (odjezd z výchozí + bublinové doseřazení
///                       per tarifní číslo); spoj patřící do víc skupin dnů emitován vícekrát
///   rows.SKUPINY_DNU: [{ ID, NADPIS }] dle parametru DELENI_SLOUPCU (texty WinMHD DSC číselník)
///   rows.POZNAMKY:    [{ DRUH (1-10 legacy číselník druhů), TYP ("text"/"znak"/"nz"), ZNAK, TEXT }]
///                     - pořadí druhů pevné (1-10 jako legacy MT3)
/// 
/// ── Vědomé mezery vůči mkPLinWin (viz analýza linkovy-knizni-jr-implementace §11) ──
/// - bez 2D bin-packingu (`Strih`), měřicích řetězců a padding sloupců - layout řeší TD
/// - bez turnusů, obousměrného tisku, souhrnných JŘ (řádek zdrojové linky)
/// - pořadí druhů poznámek pevné (legacy konfigurovatelné), skupiny a sloupce řídí prvek Poznámky
/// - dělení do skupin dnů jen dle Plk znaků (legacy u spojů „jede vždy" ověřuje kalendář
///   mkStaLinSp - spoj bez týdenní platnosti jde do všech skupin, jako legacy fallback HD 10183)
/// - symbolový font (roadmap 42b): ZNAK hodnoty jsou legacy klíče (X, |, <, m, u…), ne glyfy
/// 
/// Gold oracle: `ALVA.TISKJR.Data/LoadData` s `VOLANO_Z~JRMAIN`; multi-linkový diff harness
/// (scratchpad LineTableHarness, režim diff) porovnává spoje/časy/km/zóny/poznámky napříč firmami.
Class ALVA.TemplateDesigner.Agendy.LinkovyJrAd Extends ALVA.TemplateDesigner.Agendy.BaseAgenda
{

Parameter AgendaVariantCode As %String = "LINKOVY_JR_AD";

Parameter Agenda As %String = "LINKOVY_JR";

Parameter AgendaLabel As %String = "Linkový JŘ";

Parameter Variant As %String = "AD";

/// Jen „AD": „(linková doprava)" se pletlo s agendou Linkový JŘ (Jan 27. 10.).
Parameter VariantLabel As %String = "AD";

/// Vydaná zákazníkům 22. 9. (před tím 24. 8. zveřejněná, 1. 9. skrytá, 3. 9. vrácená kvůli dovývoji).
///
/// ⚠️ **Skrývá se na dvou místech**: tady a v konstantě ve `CreateSampleTemplate` (jinak skrytí
/// obejde samotný start aplikace); vazbu hlídá `SkrytaAgendaTests`. **`Released` je serverová
/// hodnota, ne v exe** - nasazení třídy změní nabídku agend i těm, kdo mají starší exe.
///
/// ⚠️ **Skrytí neznepřístupní hotové šablony** (platí pro obě strany přepínače). Výběr agendy
/// nezveřejněnou agendu nenabízí, ale **aktuální agendu šablony ukáže vždy**, a v otevírání
/// z databáze jsou její šablony dosažitelné přes „Všechny agendy". Prvek linkové tabulky se váže
/// na druh agendy `LINE`, takže z palety zmizí a objeví se sám.
///
/// ⚠️ **Seznam agend se čte jednou při startu** - běžící aplikace změnu nepřebere, musí se zavřít
/// a spustit znovu.
///
/// Městská varianta (`LinkovyJrMhd`) je zveřejněná od verze 27. 10. (`B191f`).
Parameter Released = 1;

Parameter InstanceParamName As %String = "LINKA_ID";

Parameter CustomColumns As %Boolean = 1;

Parameter ElementKind As %String = "LINE";

Parameter InstanceLabel As %String = "linka";

Parameter InstanceLabelPlural As %String = "linky";

/// Vrátí DataSchema - skaláry hlavičky linky + kolekce ZASTAVKY/SPOJE/SKUPINY_DNU/POZNAMKY.
ClassMethod GetSchema(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        s def = ##class(%DynamicObject).%New()

        s fields = ##class(%DynamicArray).%New()
        // Popisky polí aspoň dvou agend drží slovník `Jr.NabidkaDat` - táž věc se musí ve všech agendách
        // jmenovat stejně. Pořadí bloků jako ve všech agendách JŘ (Jan 27. 10.): linka → dopravce →
        // platnost → zastávka a směr → poznámky a doplňky. Nabídka dat pole nepřeřazuje.
        d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, "CISLO_LINKY")
        d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, "LOKALNI_OZNACENI_LINKY")
        d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, "POPIS_LINKY")
        d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, "TYP_JR")
        d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, "BARVA_LINKY")
        d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, "BARVA_LINKY_2")
        d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, "POPIS_DOPRAVCE")
        d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, "NAZEV_DOPRAVCE")
        d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, "ADRESA_DOPRAVCE")
        d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, "TELEFON_DOPRAVCE")
        d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, "EMAIL_DOPRAVCE")
        d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, "ICO_DOPRAVCE")
        // GetData posílá dál i „Popis platnosti" kvůli starším šablonám; v nabídce není.
        d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, "DATUM_PLATNOSTI_OD")
        d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, "DATUM_PLATNOSTI_DO")
        // Zastávka výlepu (parametr „Zvýraznit zastávku") jako pole stránky, aby ji šlo dosadit do QR.
        // Klíč i popisek jako u vývěsek; do hlavičky výlepu („Zastávka: …") jiný název stránky není.
        d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, "POPIS_ZASTAVKA")
        d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, "AKTUALNI_ZASTAVKA_CZST")
        d ..AddField(fields, "AKTUALNI_ZASTAVKA_TC",   "Tarifní číslo aktuální zastávky", "number")
        // Jen u tisku po zastávkách (parametr „Stránka pro každou zastávku"); prvek podle něj zvýrazní inverzně
        // aktuální zastávku. Bez parametru pole na stránce není.
        d ..AddField(fields, "VYLEP_ZASTAVKY",         "Výlep pro zastávku",       "boolean")
        d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, "SMER")
        d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, "VYCHOZI_ZASTAVKA")
        d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, "KONECNA_ZASTAVKA")
        d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, "POZNAMKA_NAD_LINKU")
        d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, "POZNAMKA_POD_DOPRAVCE")
        d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, "QR_KOD")
        s def.fields = fields

        s collections = ##class(%DynamicArray).%New()

        // Bez „Pořadí" (Jan 27. 10.): nese tarifní číslo, ne pořadí 1..N jako jinde - v nabídce je jako
        // „Tarifní číslo". GetData PORADI posílá dál, prvek podle něj páruje řádky, značky a přípoje zastávky.
        s pole = ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddKolekce(collections, "ZASTAVKY")
        d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPoleKolekce(pole, "ZASTAVKY", "CZST")
        d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPoleKolekce(pole, "ZASTAVKY", "NAZEV_ZASTAVKY")
        d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPoleKolekce(pole, "ZASTAVKY", "AKTUALNI_ZASTAVKA")
        d ..AddField(pole, "TC",             "Tarifní číslo",      "number")
        d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPoleKolekce(pole, "ZASTAVKY", "ZONY")
        d ..AddField(pole, "MHD",            "MHD/IDS",            "text")
        d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPoleKolekce(pole, "ZASTAVKY", "NASTUPISTE")

        s pole = ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddKolekce(collections, "SPOJE")
        d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPoleKolekce(pole, "SPOJE", "CISLO_SPOJE")
        d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPoleKolekce(pole, "SPOJE", "NZ")

        s pole = ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddKolekce(collections, "SKUPINY_DNU")
        d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPoleKolekce(pole, "SKUPINY_DNU", "NADPIS")

        s pole = ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddKolekce(collections, "POZNAMKY")
        d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPoleKolekce(pole, "POZNAMKY", "DRUH")
        d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPoleKolekce(pole, "POZNAMKY", "ZNAK")
        d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPoleKolekce(pole, "POZNAMKY", "TEXT")

        d def.%Set("fieldCollections", collections)

        s ^||ML("OUT", $i(^||ML("OUT"))) = def.%ToJSON()
    } catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

/// Parametry sestavy. Linka se vybírá v Instance Pickeru (GetAvailableInstances).
ClassMethod GetParameters(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        // FillParams zde netřeba - parametry IdFa/dataDB nečtou (viz konvence GetParameters).
        s arr = ##class(%DynamicArray).%New()

        // Dělení sloupců spojů podle platnosti (legacy in("A",2,"arr",3) = 0/1/2)
        s p = ##class(%DynamicObject).%New()
        s p.name = "DELENI_SLOUPCU"
        s p.label = "Rozdělení sloupců podle platnosti"
        s p.type = "enum"
        // Skupina = název prvku jako u vývěsek; do 23. 10. „Tabulka linky", což se pletlo s agendou (`ux-conventions §8`).
        s p.group = "Tabulka spojů"
        s p.serverSide = 1
        s p.description = "Podle čeho se spoje rozdělí do skupin sloupců. Vlastní rozložení se nastavuje u prvku Tabulka spojů - tady se jen ukáže, že ho šablona má."
        s opts = ##class(%DynamicArray).%New(), klice = ##class(%DynamicArray).%New()
        s vzory = ..VzorySloupcuLinky()
        f i=1:1:$ll(vzory) s vzor = $li(vzory,i) d opts.%Push($li(vzor,2)), klice.%Push($li(vzor,1))
        // „Vlastní rozložení" je **ukazatel stavu šablony**, ne volba: vybrat ho ručně nejde,
        // objeví se, když si uživatel sloupce upraví u prvku. Bez téhle položky by parametr neměl
        // co hlásit a musel by lhát některým ze vzorů.
        d opts.%Push("Vlastní rozložení"), klice.%Push("")
        d p.%Set("options", opts)
        // Klíč vzoru u každé volby (aditivně, starší klient ho nečte): podle něj klient najde vzor v PRESETY_SLOUPCU.
        d p.%Set("optionKeys", klice)
        // Výchozí je trojice pracovní dny / soboty / neděle - poslední vzor.
        s p.defaultValue = $li($li(vzory, $ll(vzory)), 2)

        // Sloupec tarifních čísel: nastavení je u linky (`PATCH,netisknoutSloupecTC`, zapnuto na 377
        // linkách), ale uživatel ho má jít přebít. Výchozí volba je proto **konstanta** „Automaticky
        // (dle linky)" a linku rozhodne server při tisku - parametry se ze serveru berou bez znalosti
        // instance, takže default načtený z linky by znamenal přenačítání při každé změně linky
        // a souboj s uloženou volbou uživatele. Zavedený tvar, stejný jako `ROZDELENI_SLOUPCU`
        // u vývěsek; konvence v `ux-conventions.md`.
        s pTc = ##class(%DynamicObject).%New()
        s pTc.name         = "SLOUPEC_TC"
        s pTc.label        = "Sloupec tarifních čísel"
        s pTc.type         = "enum"
        s pTc.group        = "Tabulka spojů"
        s pTc.serverSide   = 1
        s pTc.defaultValue = "Automaticky (dle linky)"
        s pTc.description  = "Zda se tiskne sloupec tarifních čísel. Automaticky = podle nastavení u linky v jízdních řádech; volbou Tisknout nebo Netisknout se to dá přebít pro tuhle sestavu."
        s optsTc = ##class(%DynamicArray).%New()
        d optsTc.%Push("Automaticky (dle linky)")
        d optsTc.%Push("Tisknout")
        d optsTc.%Push("Netisknout")
        s pTc.options = optsTc


        // Písmeno systému v označení zóny: data ho nesou (změřeno 10. 9. na `ZL_KOVED` 823955 -
        // syrová hodnota `Z420,I201`, tedy dvě zóny, každá se značkou svého IDS), ale nastavení
        // `ZonaIdsDelZnk` ho umí odstranit a KOVED to má zapnuté. Linka přes dvě IDS se stejně
        // číslovanými zónami je pak v tisku nerozeznatelná (`B137`). Týž třístav.
        s pZon = ##class(%DynamicObject).%New()
        s pZon.name         = "PISMENO_V_ZONE"
        s pZon.label        = "Písmeno systému u zóny"
        s pZon.type         = "enum"
        s pZon.group        = "Tabulka spojů"
        s pZon.serverSide   = 1
        s pZon.defaultValue = "Automaticky (dle linky)"
        s pZon.description  = "Zda označení zóny nese písmeno svého dopravního systému (Z420 místo 420). Bez něj nejdou u linky přes dvě IDS rozlišit stejně číslované zóny. Automaticky = podle nastavení u linky v jízdních řádech."
        s optsZon = ##class(%DynamicArray).%New()
        d optsZon.%Push("Automaticky (dle linky)")
        d optsZon.%Push("Zobrazit")
        d optsZon.%Push("Skrýt")
        s pZon.options = optsZon


        // Obousměrný tisk: oba směry na jedné sestavě (legacy in("A",2,"arr",7), JRapiLinPrtWin.mac:598).
        // ⚠️ Starý modul zrcadlo tiše VYPNE, když se společný blok nevejde na stránku bez řezu (:627).
        // My ten fallback vědomě nemáme (rozhodnutí Jan 28. 8.): legacy ho mít musel, protože tiskl
        // naslepo do pevné stránky, kdežto u nás autor šablony vidí náhled a rozhodne se sám.
        // Server na tenhle parametr posílá jen DATA - všechny spoje obou směrů nad společným seznamem
        // zastávek, každý spoj označený svým směrem. O tom, který směr je vlevo, rozhoduje prvek.
        s pObou = ##class(%DynamicObject).%New()
        s pObou.name         = "OBOUSMERNY_TISK"
        s pObou.label        = "Obousměrný tisk"
        s pObou.type         = "bool"
        // „Obecné", ne „Tabulka spojů": rozhoduje o složení celé sestavy (jestli vyjde jedna stránka
        // nebo dvě), ne o vzhledu jednoho prvku. Že se promítne i do tabulky, platí skoro o každém
        // parametru téhle agendy (rozhodnutí Jan 31. 8.).
        s pObou.group        = "Obecné"
        s pObou.serverSide   = 1
        s pObou.defaultValue = "false"
        // S jedním směrem nemá co skládat a GetData ji ignoruje - nabízí se jen při obou směrech.
        s pObou.visibleWhen  = "SMER_TISKU=Oba"
        s pObou.description  = "Oba směry v jedné tabulce - spoje opačného směru se tisknou na druhé straně od zastávek. Vypnuto = každý směr na vlastní stránce."

        // Výlepy na zastávky (Jan 1. 10.): místo stránky za směr stránka za každou zastávku a směr,
        // na které je ta zastávka zvýrazněná - na výlep tak jde i QR s adresou své zastávky.
        // „Obecné" ze stejného důvodu jako obousměrný tisk: mění složení celé sestavy.
        s pVyl = ##class(%DynamicObject).%New()
        s pVyl.name         = "STRANKA_PRO_ZASTAVKU"
        s pVyl.label        = "Stránka pro každou zastávku"
        s pVyl.type         = "bool"
        s pVyl.group        = "Obecné"
        s pVyl.serverSide   = 1
        s pVyl.defaultValue = "false"
        s pVyl.description  = "Každá zastávka, kde spoje odjíždějí, dostane vlastní stránku za každý směr a je na ní zvýrazněná inverzně (bílé na černém). Zvýraznit zastávku a Zvýraznit tarifní číslo pak vyberou jen některé zastávky."

        // ⚠️ **Turnusy ke spojům tu parametr NEJSOU** - je to volba prvku (šablony), stejně jako
        // v legacy, kde to je nastavení sekce v návrháři sestav. Klient posílá `TURNUSY_KE_SPOJUM`
        // podle prvku (jako `DEFINICE_SLOUPCU`), takže dotaz do modulu turnusů (40-550 ms na linku)
        // se pořád nespustí nikomu, kdo turnusy netiskne. Rozhodnutí Jan 28. 8.: turnus je provozní
        // věc dopravce, ne informace pro cestujícího - patří k sestavě, ne k volbám tisku.

        // Název pro MHD. Legacy ho bere jen u městských linek, které to mají zapnuté - tahle volba
        // umí navíc i to vypnout (stejně jako u vývěsky). Výchozí zapnuto = chování starého modulu.
        s pMhd = ##class(%DynamicObject).%New()
        s pMhd.name         = "NAZEV_PRO_MHD"
        s pMhd.label        = "Použít název pro MHD"
        s pMhd.type         = "bool"
        s pMhd.group        = "Obecné"
        s pMhd.serverSide   = 1
        s pMhd.defaultValue = "true"
        s pMhd.description  = "Zastávka může mít v číselníku vedle názvu podle CIS ještě upravený název pro MHD. Starý modul ho tiskne u městských linek, které to mají zapnuté. Vypnuto = všude název podle CIS."

        s pZvT = ##class(%DynamicObject).%New()
        s pZvT.name         = "ZVYRAZNIT_TC"
        s pZvT.label        = "Zvýraznit tarifní číslo"
        s pZvT.type         = "string"
        s pZvT.group        = "Tabulka spojů"
        s pZvT.serverSide   = 1
        s pZvT.description  = "Tarifní číslo řádku, který se má zvýraznit. Spolu se zastávkou zúží zvýraznění na ten jediný řádek."


        // ── pořadí, v jakém je uvidí uživatel ────────────────────────────────────────
        // Skupiny se v tabu Parametry řadí podle **prvního výskytu** v tomhle poli, ne abecedně, takže
        // se sem pushuje najednou a v pořadí, které má platit. „Obecné" je první schválně - je to
        // nastavení celé sestavy, ne jednoho prvku (týž důvod i tvar jako u zastávkového JŘ).
        // ⚠️ Každý parametr musí mít `group`. Bez ní ho `AddParamGroups` shodí do vlastní skupiny
        // „Parametry sestavy" a uživatel uvidí osamocenou položku - přesně to potkalo `DELENI_SLOUPCU`
        // do 31. 8. (`L12`).
        // Obecné. Tvar názvu zastávky: ⚠️ **výchozí „Plný CIS", jinak než u vývěsky** („Zkrácený") -
        // starý linkový tisk nechává prázdnou část obce jako dvojčárku („Český Krumlov,,aut. nádraží"),
        // starý vývěskový modul ji srážel; parita je přednější (potvrzeno Janem 24. 8.).
        d arr.%Push(##class(ALVA.TemplateDesigner.Agendy.Jr.Parametry).ParametrTvaruNazvuZastavky("Plný CIS"))
        d arr.%Push(pMhd)
        d arr.%Push(##class(ALVA.TemplateDesigner.Agendy.Jr.Parametry).ParametrSmeruTisku())
        d arr.%Push(pObou)
        d arr.%Push(pVyl)
        d arr.%Push(##class(ALVA.TemplateDesigner.Agendy.Jr.Parametry).ParametrPoctuStran())
        d arr.%Push(p)         // Tabulka spojů - dělení spojů je nejdůležitější volba agendy
        d arr.%Push(pTc)
        d arr.%Push(##class(ALVA.TemplateDesigner.Agendy.Jr.Parametry).ParametrZastavkyBezZastaveni())
        d arr.%Push(pZon)
        // Barvy spojů - checkbox jako u vývěsky, viz Jr.Parametry.VolbaBarevSpoju
        d ..AddBoolParam(arr, "TISKNOUT_BARVY_SPOJU", "Barvy spojů", 1, "Tabulka spojů", "Časy spoje se tisknou barvou, kterou má spoj nastavenou v jízdních řádech. Vypnuto = celá tabulka černě.", 1)
        d arr.%Push(##class(ALVA.TemplateDesigner.Agendy.Jr.Parametry).ParametrZvyrazneniZastavky())
        d arr.%Push(pZvT)

        s ^||ML("OUT", $i(^||ML("OUT"))) = arr.%ToJSON()
    } catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

/// Třístavová volba „Automaticky (dle linky) / Tisknout / Netisknout" → 1 tiskne, 0 netiskne.
/// U neznámé hodnoty se spadne do „dle linky" (tolerantně, stejně jako to dělá starý modul
/// u `ROZDELENI_SLOUPCU`) - starší klient, který parametr neposílá, se tak chová jako dřív.
ClassMethod TristavDleLinky(volba As %String, dleLinky As %Boolean) As %Boolean
{
    i volba = "Tisknout" q 1
    i volba = "Netisknout" q 0
    q dleLinky
}

/// Skupiny linek pro výběr linek - společné agendám nad AD (`Jr.DataAd`).
ClassMethod GetAvailableSkupiny(ByRef sys As %String) As %Status
{
    q ##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).GetAvailableSkupiny(.sys)
}

/// Seznam linek pro výběr linek - společný agendám nad AD (`Jr.DataAd`).
ClassMethod GetAvailableInstances(ByRef sys As %String) As %Status
{
    q ##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).GetAvailableInstances(.sys)
}

/// Vrátí data pro tisk: 1 stránka per (linka × směr) - Tam, pak Zpět.
ClassMethod GetData(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        k ^||TiskKnizni                    // per-request cache (W-mapy)
        d ..ZacitDuvodyPrazdne()

        s sku = $g(^||ML("IN","SKUPINA_LINEK_ID"))
        i sku = "" s sku = $o(^[dataDB]JRL(""))
        s lin = ##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).FormatCisloLinky($g(^||ML("IN","LINKA_ID")))
        i lin = "" s lin = ##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).FormatCisloLinky($g(^||ML("IN","INSTANCE_ID")))
        i lin = "" d ..ChybaDat("Není vybraná linka. Vyberte linky k tisku.")
        i '$d(^[dataDB]JRL(sku,lin,"I")) d ..ChybaDat(##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).TextLinkaNeniVJr(sku, lin))

        d ..NactiNastaveniNazvu(dataDB, sku, lin)
        // Vlastní definice sloupců ze šablony (`DEFINICE_SLOUPCU`) přebíjí hrubý přepínač dělení.
        // Když nepřijde, chová se všechno jako dosud - proto je to aditivní.
        k defSloupcu
        s maDefinici = ##class(ALVA.TemplateDesigner.Agendy.Jr.VlastniSloupce).ParseDefiniceSloupcu(.defSloupcu)
        s deleni = ..DeleniZParametru($g(^||ML("IN","DELENI_SLOUPCU")))
        s volbaTc = $g(^||ML("IN","SLOUPEC_TC"))
        s volbaBez = $g(^||ML("IN","ZASTAVKY_BEZ_ZASTAVENI"))
        s volbaBarvy = ##class(ALVA.TemplateDesigner.Agendy.Jr.Parametry).VolbaBarevSpoju()
        s volbaZony = $g(^||ML("IN","PISMENO_V_ZONE"))

        // Obousměrný tisk: jedna stránka se spoji obou směrů nad společným seznamem zastávek.
        // Bool parametry chodí jako "true"/"false", starší klienti posílali "True" - proto tolerantně.
        s obousmerne = ($zcvt($g(^||ML("IN","OBOUSMERNY_TISK")),"L") = "true")
        s sTurnusy = ($zcvt($g(^||ML("IN","TURNUSY_KE_SPOJUM")),"L") = "true")
        s vylepy = ($zcvt($g(^||ML("IN","STRANKA_PRO_ZASTAVKU")),"L") = "true")
        s smery = ##class(ALVA.TemplateDesigner.Agendy.Jr.Parametry).SmeryKTisku($g(^||ML("IN","SMER_TISKU")))
        i obousmerne && (smery = "T,Z") {
            // Pořadí zastávek se bere ze směru „tam"; opačný směr se na tytéž řádky zarovná podle
            // pořadí zastávky. Linka obsluhovaná jen jedním směrem tím vyjde jako dnes - druhá
            // strana prostě nemá sloupce a prvek ji nenakreslí.
            s page = ..BuildStrankaLinky(dataDB, sku, lin, "T", deleni, volbaTc, volbaBez, volbaBarvy, .defSloupcu, 1, sTurnusy, volbaZony)
            i page '= "" {
                d ..DoplnBarvyLinky(page, IdFa, dataDB, sku, lin)
                i vylepy { d ..VypisVylepy(page, "T,Z", 1) }
                else { s ^||ML("OUT", $i(^||ML("OUT"))) = page.%ToJSON() }
            }
        }
        else {
            f si = 1:1:$l(smery, ",") {
                s smer = $p(smery, ",", si)
                s page = ..BuildStrankaLinky(dataDB, sku, lin, smer, deleni, volbaTc, volbaBez, volbaBarvy, .defSloupcu, 0, sTurnusy, volbaZony)
                continue:page=""
                d ..DoplnBarvyLinky(page, IdFa, dataDB, sku, lin)
                i vylepy { d ..VypisVylepy(page, smer, 0) }
                else { s ^||ML("OUT", $i(^||ML("OUT"))) = page.%ToJSON() }
            }
        }
        // Stránky linky vznikly, ale výlep žádná zastávka nedostala - jiný důvod než chybějící data.
        d ..VydatDuvodPrazdne($s(vylepy:"Žádná zastávka linky nedostala výlep: z žádné neodjíždí spoj, který by dál ještě stavěl, nebo žádná neodpovídá nastavení Zvýraznit zastávku.", 1:""))
    }
    catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

/// Barvy linky z jízdních řádů (#13606) týmž voláním jako vývěska AD (`Jr.DataAd.BarvaLinky`),
/// aby měla linka v obou sestavách stejnou barvu. Druhou barvu modul významově nepojmenovává.
ClassMethod DoplnBarvyLinky(page As %DynamicObject, IdFa As %String, dataDB As %String, sku As %String, lin As %String)
{
    d page.values.%Set("BARVA_LINKY", ##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).BarvaLinky(IdFa, dataDB, sku, lin))
    d page.values.%Set("BARVA_LINKY_2", ##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).BarvaLinky(IdFa, dataDB, sku, lin, 2))
}

/// Výlepy na zastávky: ze stránky směru (nebo obousměrné) vypíše jednu stránku za každou zastávku
/// výlepu. Stránka se nestaví znovu, jen se zkopíruje a přenastaví zvýraznění - na zvýraznění
/// nezávisí nic jiného než příznak řádku a pole zastávky v hlavičce, a stavba tabulky je drahá.
///
/// Zastávka výlepu = hlavní řádek, ze kterého v daném směru aspoň jeden spoj odjíždí: má tu čas,
/// není tu jen výstup (značka „V") a dál po směru ještě někde staví (konečná tedy výlep nemá).
/// Zadané „Zvýraznit zastávku/tarifní číslo" výlepy zúží na ty zastávky, které už stránka označila.
/// Stránka nese `SMER` svého výlepu (i obousměrná - QR potřebuje směr) a `VYLEP_ZASTAVKY` = "true",
/// podle kterého prvek aktuální zastávku vždy vytiskne inverzně (jako starý modul).
ClassMethod VypisVylepy(page As %DynamicObject, smery As %String, obousmerne As %Boolean)
{
    s zastavky = page.rows.%Get("ZASTAVKY"), spoje = page.rows.%Get("SPOJE")
    q:'$isobject(zastavky)||'$isobject(spoje)
    s filtr = ($g(^||TiskKnizni("zvyraznit","zastavka")) '= "") || ($g(^||TiskKnizni("zvyraznit","tc")) '= "")
    s pocet = zastavky.%Size()
    f si=1:1:$l(smery,",") {
        s smer = $p(smery,",",si)
        // Obousměrná stránka má řádky v pořadí směru „tam", opačný směr se čte odzadu.
        s krok = $s(obousmerne && (smer = "Z"):-1, 1:1)
        s i = $s(krok = 1:0, 1:pocet - 1)
        while (i >= 0) && (i < pocet) {
            s r = zastavky.%Get(i)
            i (r.%Get("TYP") = "zastavka") && ('filtr || (r.%Get("AKTUALNI_ZASTAVKA") = "true")) && ..Odjizdi(zastavky, spoje, smer, i, krok, pocet) {
                d ..VypisVylep(page, r.%Get("PORADI"), r.%Get("CZST"), smer, r.%Get("NAZEV_ZASTAVKY"))
            }
            s i = i + krok
        }
    }
}

/// Odjíždí z řádku `i` ve směru `smer` aspoň jeden spoj, který dál ještě někde staví? Vložený
/// příjezdový řádek téže zastávky se za „dál" nepočítá (obousměrná stránka ho má u opačného směru
/// po směru čtení).
ClassMethod Odjizdi(zastavky As %DynamicArray, spoje As %DynamicArray, smer As %String, i As %Integer, krok As %Integer, pocet As %Integer) As %Boolean
{
    s poradi = zastavky.%Get(i).%Get("PORADI"), odjizdi = 0
    s it = spoje.%GetIterator()
    while 'odjizdi && it.%GetNext(.k, .spoj) {
        continue:spoj.%Get("SMER")'=smer
        s casy = spoj.%Get("CASY")
        continue:'$isobject(casy)
        continue:'..JeCas(casy.%Get(i))
        s znacky = spoj.%Get("ZNACKY")
        continue:$isobject(znacky)&&(znacky.%Get(i)["V")
        s j = i + krok
        while 'odjizdi && (j >= 0) && (j < pocet) {
            i (zastavky.%Get(j).%Get("PORADI") '= poradi) && ..JeCas(casy.%Get(j)) s odjizdi = 1
            s j = j + krok
        }
    }
    q odjizdi
}

/// Je hodnota buňky času čas (`H:MM` / `HH:MM`), ne značka „|" / „<" ani prázdno?
ClassMethod JeCas(hodnota As %String) As %Boolean
{
    q hodnota?1.2N1":"2N
}

/// Jedna stránka výlepu: kopie stránky se zvýrazněnou jen zastávkou `poradi` (hlavní i vložený
/// příjezdový řádek mají totéž pořadí).
ClassMethod VypisVylep(page As %DynamicObject, poradi As %String, czst As %String, smer As %String, nazev As %String = "")
{
    s vylep = ##class(%DynamicObject).%FromJSON(page.%ToJSON())
    s it = vylep.rows.ZASTAVKY.%GetIterator()
    while it.%GetNext(.k, .r) {
        d r.%Set("AKTUALNI_ZASTAVKA", $s(r.%Get("PORADI") = poradi:"true", 1:"false"))
    }
    d vylep.values.%Set("SMER", smer)
    d vylep.values.%Set("POPIS_ZASTAVKA", nazev)
    d vylep.values.%Set("AKTUALNI_ZASTAVKA_CZST", czst)
    d vylep.values.%Set("AKTUALNI_ZASTAVKA_TC", +poradi)
    d vylep.values.%Set("VYLEP_ZASTAVKY", "true")
    s ^||ML("OUT", $i(^||ML("OUT"))) = vylep.%ToJSON()
}

/// Ukázková data pro live preview - živě z první linky aktuální firmy (DEV vzor).
ClassMethod GetMockData(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        k ^||TiskKnizni

        d ##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).PrvniLinka(dataDB, .sku, .lin)
        // Vlastní definice sloupců ze šablony jde i do ukázkových dat - bez ní ukazuje náhled
        // **bez vybrané linky** rozdělení podle linky a uživatelovy sloupce se v něm neprojeví,
        // takže to vypadá jako nefunkční featura (táž třída chyby jako `#13158` u vývěsek).
        // ⚠️ Parametry tisku (obousměrný tisk, turnusy) sem vědomě nejdou: podle kontraktu
        // `BaseAgenda` je mock ukázka bez parametrů a klient je posílá až do `GetData`.
        k defSloupcu
        d ##class(ALVA.TemplateDesigner.Agendy.Jr.VlastniSloupce).ParseDefiniceSloupcu(.defSloupcu)
        s page = ""
        i lin '= "" s page = ..BuildStrankaLinky(dataDB, sku, lin, "T", 2, "", "", "", .defSloupcu)
        i page '= "" d ..DoplnBarvyLinky(page, IdFa, dataDB, sku, lin)

        i page = "" {
            s page = ##class(%DynamicObject).%New()
            s v = ##class(%DynamicObject).%New()
            d v.%Set("POPIS_LINKY", ..TextUkazkyBezDat())
            d page.%Set("values", v)
            s r = ##class(%DynamicObject).%New()
            d r.%Set("ZASTAVKY", ##class(%DynamicArray).%New())
            d r.%Set("SPOJE", ##class(%DynamicArray).%New())
            d r.%Set("SKUPINY_DNU", ##class(%DynamicArray).%New())
            d r.%Set("POZNAMKY", ##class(%DynamicArray).%New())
            d page.%Set("rows", r)
        }

        s ^||ML("OUT", $i(^||ML("OUT"))) = page.%ToJSON()
    }
    catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

/// Vzory rozložení sloupců linky: `$lb(klíč, popisek, legacy mód)`. **Jediné místo**, odkud jdou volby
/// parametru „Rozdělení sloupců podle platnosti" (popisky jsou zamčené - ukládá se zobrazený text), klíče
/// k nim pro klienta (`optionKeys`) i převod volby na legacy mód. Do 30. 10. opsané i v klientovi.
ClassMethod VzorySloupcuLinky() As %List
{
    s vzory = $lb($lb("BEZ_ROZLISENI", "Bez rozlišení", 0))
    s vzory = vzory_$lb($lb("PRAC_VIKEND", "Pracovní dny / víkend+svátky", 1))
    s vzory = vzory_$lb($lb("PRAC_SO_NE", "Pracovní dny / Soboty / Neděle+svátky", 2))
    q vzory
}

/// Mapování volby parametru „Rozdělení sloupců podle platnosti" na legacy mód 0/1/2.
ClassMethod DeleniZParametru(label As %String) As %Integer
{
    // „Vlastní rozložení" sem nemá jak dojít jako skutečná volba - je to jen ukazatel a objeví se,
    // až když šablona definici má; grupování pak řídí ta definice, ne tenhle přepínač.
    s vzory = ..VzorySloupcuLinky()
    f i=1:1:$ll(vzory) s vzor = $li(vzory,i) ret:($li(vzor,2)=label) $li(vzor,3)
    q 2
}

/// Jazyková varianta textových číselníků (NCOME/STALCOME/WinMHD): "CZ" nebo "*SK".
///
/// ⚠️ **Rozhoduje jazyk šablony** (`JAZYK` z klienta), stejně jako u zastávkového JŘ (#12843):
/// firma může mít vyplněné obě varianty a dřív tu vyhrála čeština vždy, když existovala - slovenská
/// šablona na Nitře tiskla české poznámky (#9088, 23. 9.). Bez jazyka šablony zůstává dřívější volba.
/// Do 30. 10. `Jazyk(dataDB)` - stejně se jmenovala metoda vývěsky AD, která vrací jazyk šablony „cs" / „sk"
/// (`B56` S13); vývěska má teď `KodJazykaCiselniku()` se stejným významem.
ClassMethod KodJazykaCiselniku(dataDB As %String) As %String
{
    s kod = $zcvt($p($g(^||ML("IN","JAZYK")),"-",1),"L")
    i (kod = "sk") && $d(^[dataDB]JRSET("NCOME","*SK")) q "*SK"
    i (kod = "cs") && $d(^[dataDB]JRSET("NCOME","CZ")) q "CZ"
    i $g(^[dataDB]JRSET("NCOME","CZ","X")) '= "" q "CZ"
    i $d(^[dataDB]JRSET("NCOME","*SK")) q "*SK"
    q "CZ"
}

/// Sestaví JSON stránku knižního JŘ pro jednu linku a směr. "" když směr nemá spoje.
/// `obousmerne` = spoje **obou** směrů nad jedním seznamem zastávek (parametr „Obousměrný tisk").
/// Zastávkové řádky se staví pro `smer`, spoje se přiberou i z opačného směru a každý nese `SMER`.
/// Server tím posílá jen fakta - o tom, který směr se kreslí vlevo, rozhoduje prvek.
ClassMethod BuildStrankaLinky(dataDB As %String, sku As %String, lin As %String, smer As %String, deleni As %Integer, volbaTc As %String = "", volbaBezZastaveni As %String = "", volbaBarvy As %String = "", ByRef defSloupcu, obousmerne As %Boolean = 0, sTurnusy As %Boolean = 0, volbaZony As %String = "") As %DynamicObject
{
    i '$d(^[dataDB]JRL(sku,lin,"I")) q ""
    // B188: údaje zastávek z registru k „platí od" linky (časové varianty), jako starý tisk
    s ^||TiskKnizni("zastavky","hrl") = $lg(##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).HorologPlatnostiLinky(dataDB, sku, lin), 1)
    // U obousměrné stránky stačí spoje v jednom ze směrů - kdyby se ptalo jen na `smer`, linka
    // obsluhovaná pouze zpět by nevrátila nic.
    // Důvod prázdné stránky stejnými slovy jako linkový JŘ MHD (`BaseAgenda.PrazdnaStranka`, `B193g`).
    i obousmerne {
        i '##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).MaSpoje(dataDB, sku, lin, "T") && '##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).MaSpoje(dataDB, sku, lin, "Z") q ..PrazdnaStranka("Linka nemá ve vybraném směru žádné spoje.")
    }
    else {
        i '##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).MaSpoje(dataDB, sku, lin, smer) q ..PrazdnaStranka("Linka nemá ve vybraném směru žádné spoje.")
    }

    s rec = ..KodJazykaCiselniku(dataDB)
    s tiskDtm = ##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).TiskDtm(dataDB, sku, lin)

    // ── hlavička linky ──────────────────────────────────────────────
    // Obě čísla linky jako u vývěsky, šablona si vybere (rozhodnutí Jan 23. 10.); hlavičku, kterou
    // posílají všechny agendy AD stejně, skládá Jr.DataAd.
    s vals = ##class(%DynamicObject).%New()
    d ##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).AddHlavickaLinky(dataDB, sku, lin, vals)
    d vals.%Set("POPIS_DOPRAVCE",         ..PopisDopravce(dataDB, sku, lin))
    // Vzory rozložení sloupců - obsah drží **server**, klient zná jen klíče a popisky. Bez toho
    // by parametr uměl jen hlásit stav, ale ne přepnout (a klientský opis pravidel by se rozešel).
    d vals.%Set("PRESETY_SLOUPCU",        ..PresetySloupcuLinky(dataDB, rec, sku, lin))
    // Jen pro starší šablony - v nabídce dat už není (Jan 23. 10.), tvar se proto nemění.
    s platiOd = vals.%Get("DATUM_PLATNOSTI_OD"), platiDo = vals.%Get("DATUM_PLATNOSTI_DO")
    s popisPlatnosti = ""
    i platiOd '= "" {
        s popisPlatnosti = "Platí od "_platiOd
        i platiDo '= "" s popisPlatnosti = popisPlatnosti_" do "_platiDo
    }
    d vals.%Set("POPIS_PLATNOSTI",        popisPlatnosti)
    // ⚠️ U obousměrné stránky je směr **prázdný** - stránka nese oba a `SMER` u stránky by šablonu
    // navedl vytisknout jeden z nich jako by byl jediný. Směr každého spoje je na spoji;
    // konce linky drží `VYCHOZI_ZASTAVKA` a `KONECNA_ZASTAVKA`.
    d vals.%Set("SMER",                   $s(obousmerne:"", 1:smer))

    // ── zastávkové řádky v pořadí směru (vč. zdvojení příjezd/odjezd u atributů P/J) ──
    // rowMeta(i) = $lb(poradi, typ) - typ "O" hlavní/odjezdový, "P" vložený příjezdový.
    s jeTam = (smer = "T")
    s zastavky = ##class(%DynamicArray).%New()
    k rowMeta
    s prvniNazev = "", posledniNazev = ""
    // `linIDS` je ČTYŘSTAVOVÝ přepínač zóny × km, ne příznak „je to IDS linka":
    //   0 (i nenastaveno) = jen km · 1 = jen zóna · 2 = obojí · 3 = ani jedno
    // 1:1 legacy gPLSetWin (JRapiLinPrtWin.mac:1861-1868). Dřív se tu čtlo jen „je neprázdné →
    // tiskni zónu", takže u hodnoty 3 (1 126 linek v datech) jsme zónu tiskli, legacy ne, a km
    // sloupce jsme neřídili vůbec.
    s linIds = +$g(^[dataDB]JRL(sku,lin,"PATCH","linIDS"))
    s tiskZony = (linIds = 1) || (linIds = 2)
    s tiskKm = (linIds = 0) || (linIds = 2)
    // Sloupec tarifních čísel: u linky 1 = schovat; parametr sestavy to smí přebít.
    s tiskTc = ..TristavDleLinky(volbaTc, '+$g(^[dataDB]JRL(sku,lin,"PATCH","netisknoutSloupecTC")))
    // Zastávky, kde žádný spoj nestaví: u linky 1 = vyhodit je. Mapu obsluhovaných tarifních čísel
    // stavíme jen když je to potřeba - je to průchod všemi spoji směru.
    s tiskBezZastaveni = ..TristavDleLinky(volbaBezZastaveni, '+$g(^[dataDB]JRL(sku,lin,"PATCH","tisknoutJenTcKdeSpojeZastavuji")))
    k zastavuje
    i 'tiskBezZastaveni d ..ZastavujiciTc(dataDB, sku, lin, smer, .zastavuje)
    s tiskBarvy = ##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).TiskBarevSpoju(volbaBarvy, dataDB, sku, lin)
    // Odstranit z označení zóny písmena. Nastavení je globální, ale linka ho **přebíjí** - a ukládá
    // se u ní jako hodnota+1, takže 1 = neodstraňovat, 2 = odstraňovat, 0 nebo prázdno = platí
    // globální (1:1 legacy gPLSetWin `:1847`, kde je `hodnota-1`).
    s zonaDelZnkLinka = +$g(^[dataDB]JRL(sku,lin,"PATCH","ZonaIdsDelZnk"))
    s bezPismenVZone = $s(zonaDelZnkLinka:zonaDelZnkLinka - 1, 1:+$g(^[dataDB]JRSET("Definice","ZonaIdsDelZnk")))
    // Parametr sestavy nastavení přebíjí (`B137`): bez písmene nejdou u linky přes dvě IDS
    // rozlišit stejně číslované zóny, a na to nastavení v databázi uživatel návrháře nedosáhne.
    // Neznámá hodnota → zůstane „dle linky", stejně tolerantně jako ostatní třístavy.
    i volbaZony = "Zobrazit" { s bezPismenVZone = 0 }
    i volbaZony = "Skrýt" { s bezPismenVZone = 1 }
    // Km sloupce jsou u linky uložené **třikrát**: společný seznam `^JRL(sku,lin,"K")` a dva
    // směrové `PATCH,KmsSmer,T/Z`. Starý tisk bere u **obousměrné** sestavy ten společný a u
    // jednosměrné směrový (`JRapiLinPrtWin.mac:2523`, větev `sm="A"`), protože společný je
    // **odduplikovaná sjednocenina** obou směrů - profil obsažený v jiném do něj nejde.
    // Do 7. 9. jsme slepovali oba směrové za sebe a vyráběli tím sloupce nesoucí totéž:
    // na `330511` jedenáct místo osmi (T 5 + Z 6, tři profily jsou obsažené v jiných).
    // ⚠️ Prahy `Kms,Filtr` / `Kms,MaxFiltr` **neřešíme** - jsou to vstupy do generování těch
    // seznamů v jízdních řádech (`JRapiLinUtlKms1.mac:36-200`), takže je dostáváme profiltrované.
    s spolecneKm = (tiskKm && obousmerne)
    s pocetKmSloupcu = $s('tiskKm:0, spolecneKm:+$g(^[dataDB]JRL(sku,lin,"K")),
        1:+$g(^[dataDB]JRL(sku,lin,"PATCH","KmsSmer",smer)))
    s maxKmVeta = ..MaxKmVeta(dataDB, sku, lin, smer, pocetKmSloupcu, spolecneKm)
    k atributyZobrazene                       // znaky atributů trasy viditelné na stránce (legenda druhu 7)
    k atributyZastavek                        // atributy z číselníku zastávek (^JRZ $8, CSV) - legenda druhu 7
    k zvyraznene, zvyrazneneNazev             // tarifní číslo zvýrazněného řádku → CZST / název
    s maMhd = 0
    s idx = 0, poradi = ""
    f {
        i jeTam { s poradi = $o(^[dataDB]JRL(sku,lin,"J",poradi)) }
        else    { s poradi = $o(^[dataDB]JRL(sku,lin,"J",poradi), -1) }
        q:poradi=""
        i '$isvalidnum(poradi) continue
        s veta = $g(^[dataDB]JRL(sku,lin,"J",poradi))
        s czst = $p(veta,":",1)
        i czst = "" continue
        // Řádek, na kterém v tomhle směru nikdo nestaví (všichni projíždějí nebo jedou jinudy).
        // Vypuštěním z téhle smyčky se řádek neobjeví ani v `rowMeta`, takže se s ním samo zarovná
        // i pole časů u spojů - jinak by se časy rozjely o vypuštěné řádky.
        i 'tiskBezZastaveni, '$g(zastavuje(poradi)) continue

        s atrZnaky = $p($p(veta,":",2),"#",1)
        s zdvojit = (atrZnaky [ "P") || (atrZnaky [ "J")

        // vložený příjezdový řádek (nad hlavním) - legacy insertrow = row-0.01
        i zdvojit {
            s idx = idx + 1
            s rowMeta(idx) = $lb(poradi, "P")
            d zastavky.%Push(..BuildZastavkaRadek(dataDB, sku, lin, smer, poradi, veta, "prijezd", pocetKmSloupcu, maxKmVeta, jeTam, tiskZony, tiskTc, bezPismenVZone, spolecneKm, obousmerne))
        }

        s idx = idx + 1
        s rowMeta(idx) = $lb(poradi, "O")
        s z = ..BuildZastavkaRadek(dataDB, sku, lin, smer, poradi, veta, "zastavka", pocetKmSloupcu, maxKmVeta, jeTam, tiskZony, tiskTc, bezPismenVZone, spolecneKm, obousmerne)
        d zastavky.%Push(z)
        i z.%Get("AKTUALNI_ZASTAVKA") = "true" s zvyraznene(poradi) = czst, zvyrazneneNazev(poradi) = z.%Get("NAZEV_ZASTAVKY")

        f ai=1:1:$l(atrZnaky) s atributyZobrazene($e(atrZnaky,ai)) = ""
        // atributy z číselníku zastávek (^JRZ $8, CSV - např. "A,L" = přestup bus/vlak)
        s atrZst = $p(..ZaznamZastavky(dataDB, czst),"$",8)
        f ai=1:1:$l(atrZst,",") {
            s znakZst = $p(atrZst,",",ai)
            i znakZst '= "" s atributyZastavek(znakZst) = ""
        }
        i $p(veta,"#",4) '= "" s maMhd = 1
        i prvniNazev = "" s prvniNazev = ..NazevZastavky(dataDB, czst)
        s posledniNazev = ..NazevZastavky(dataDB, czst)
    }
    i idx = 0 q ..PrazdnaStranka($s('tiskBezZastaveni:"Žádný spoj linky ve vybraném směru nestaví na žádné zastávce, a ty se podle nastavení Zastávky, kde žádný spoj nestaví, netisknou. Zkontrolujte časy spojů v jízdních řádech.", 1:"Na trase linky není žádná zastávka. Zkontrolujte trasu linky v jízdních řádech."))

    d vals.%Set("VYCHOZI_ZASTAVKA", prvniNazev)
    d vals.%Set("KONECNA_ZASTAVKA",       posledniNazev)
    // Zastávka výlepu pro adresu v QR: jen když je zvýrazněný právě jeden řádek, jinak by adresa
    // ukázala na jedno z víc míst (kus názvu „Velešín" trefí čtyři zastávky). Pole jsou vždy,
    // aby v QR nezůstal nedosazený `{…}`.
    s zvTc = $o(zvyraznene(""))
    s jednoznacna = (zvTc '= "") && ($o(zvyraznene(zvTc)) = "")
    d vals.%Set("POPIS_ZASTAVKA",         $s(jednoznacna:zvyrazneneNazev(zvTc), 1:""))
    d vals.%Set("AKTUALNI_ZASTAVKA_CZST", $s(jednoznacna:zvyraznene(zvTc), 1:""))
    d vals.%Set("AKTUALNI_ZASTAVKA_TC",   $s(jednoznacna:+zvTc, 1:""))

    // ── spoje směru: legacy mkPoradiSp řazení + zařazení do skupin dnů ──
    s spoje = ##class(%DynamicArray).%New()
    k platnostZnakySeen, nzSeen, pripojePoznamky, markerSeen, atributSeen, mistenkaSeen, trasaZnakySeen, poznamkaSpojeSeen
    s pocetSkupin = $s(+$g(defSloupcu):+$g(defSloupcu), deleni=0:1, deleni=1:2, 1:3)

    // Směry k projití: u obousměrné stránky oba, jinak jen ten svůj. Časy se zarovnávají podle
    // **pořadí zastávky** (`wMap(pp)` v `BuildSpoj`), ne podle pozice ve směru, takže spoje
    // opačného směru sednou na tentýž `rowMeta` bez jakéhokoli přepočtu.
    s smeryVeta = $s(obousmerne:"T,Z", 1:smer)
    // Turnusy se tahají **jednou na linku**, ne per spoj - je to volání do jiného modulu (viz
    // NactiTurnusy) a per spoj by z jednoho dotazu bylo padesát.
    k turnusySpoju
    i sTurnusy d ..NactiTurnusy(dataDB, sku, lin, .turnusySpoju)
    f smerIdx = 1:1:$l(smeryVeta, ",") {
    s smerSpoju = $p(smeryVeta, ",", smerIdx)
    k serazeneSpoje
    d ..SeradSpoje(dataDB, sku, lin, smerSpoju, .serazeneSpoje, .pocetSpoju)
    f skupina = 1:1:pocetSkupin {
        f si=1:1:pocetSpoju {
            s sp = serazeneSpoje(si)
            s plk = $p($p($g(^[dataDB]JRL(sku,lin,smerSpoju,sp)),"#",1),":",7)
            // ⚠️ **Spoj bez týdenní platnosti patří do všech skupin dnů** - prázdné `plk` si
            // `ZarazeniSpojeKnizni` přeloží samo. Legacy zkusí dopočítat skutečnou platnost
            // kalendářem (`mkStaLinSp`) a **když mu to nevyjde, dá spoj do všech sekcí**
            // (`JRapiLinPrtWin:476-481`, komentář HD 10183).
            //
            // Do 24. 8. jsme místo kalendáře odhadovali dny ze zadaných datumů omezení. Vypadalo
            // to chytřeji, ale bylo to horší: odhad spoj zařadil do dvou skupin místo tří, takže
            // se v tabulce opakoval jednou míň a **zákazníkovi chyběl sloupec** (VS_CSAD 0883637).
            // Odhad byl navíc naše vlastní logika, kterou nic nedokládalo - tohle je chování
            // starého modulu a porovnání ho potvrdilo.
            i +$g(defSloupcu) {
                // Vlastní definice: den + značky, které spoj do sloupce pustí nebo z něj vyřadí.
                // Období (osa ISY) tu vědomě není - linkový JŘ pro ně nemá podklad, viz roadmapa.
                s cnz = $p($p($g(^[dataDB]JRL(sku,lin,smerSpoju,sp)),"#",1),":",8)
                s skupiny = ..ZarazeniSpojeDleDefiniceLinky(.defSloupcu, plk, cnz)
            }
            else { s skupiny = ..ZarazeniSpojeKnizni(deleni, plk) }
            i (","_skupiny_",") '[ (","_skupina_",") continue
            s spojObj = ..BuildSpoj(dataDB, sku, lin, smerSpoju, sp, skupina, idx, .rowMeta, rec, .platnostZnakySeen, .nzSeen, .pripojePoznamky, .markerSeen, .atributSeen, .mistenkaSeen, .trasaZnakySeen, .spojeSeen, tiskBarvy, .poznamkaSpojeSeen)
            i spojObj '= "" d ..PridejTurnusySpoje(spojObj, .turnusySpoju, sp)
            i spojObj '= "" d spoje.%Push(spojObj)
        }
    }
    }
    i spoje.%Size() = 0 q ..PrazdnaStranka("Žádný spoj linky nepatří do sloupců šablony. Zkontrolujte Rozdělení sloupců podle platnosti, případně vlastní rozložení sloupců u prvku Tabulka spojů.")

    // ── poznámky (pevné pořadí druhů 1:1 MT3: platnosti, NZ, návaznosti - pokračuje/vyčká/
    //    navazuje, atributy, místenkování, stálé značky, poznámky linky) ──
    s poznamky = ##class(%DynamicArray).%New()
    d ..PridatPoznamkyPlatnosti(dataDB, rec, poznamky, .platnostZnakySeen)
    d ..PridatPoznamkyNZ(dataDB, sku, lin, smeryVeta, rec, tiskDtm, poznamky, .nzSeen)
    f poradiDruhu3 = 1:1:3 {
        s seq = ""
        f {
            s seq = $o(pripojePoznamky(poradiDruhu3, seq))
            q:seq=""
            d poznamky.%Push(pripojePoznamky(poradiDruhu3, seq))
        }
    }
    d ..PridatPoznamkySpoju(dataDB, sku, lin, rec, poznamky, .poznamkaSpojeSeen)
    d ..PridatPoznamkyAtributu(dataDB, sku, lin, poznamky, .atributSeen, .spojeSeen, rec, tiskDtm)
    d ..PridatPoznamkyMistenkovani(dataDB, sku, lin, rec, poznamky, .mistenkaSeen)
    d ..PridatStaleZnacky(dataDB, rec, poznamky, .markerSeen, .atributyZobrazene, .atributyZastavek, .trasaZnakySeen, maMhd)
    d ..PridatPoznamkyLinky(dataDB, sku, lin, poznamky)

    // ── sestavení stránky ───────────────────────────────────────────
    s out = ##class(%DynamicObject).%New()
    d out.%Set("values", vals)
    s rows = ##class(%DynamicObject).%New()
    d rows.%Set("ZASTAVKY", zastavky)
    d rows.%Set("SPOJE", spoje)
    d rows.%Set("SKUPINY_DNU", $s(+$g(defSloupcu):..BuildSkupinyZDefinice(.defSloupcu), 1:..BuildSkupinyDnu(dataDB, rec, deleni, sku, lin)))
    d rows.%Set("POZNAMKY", poznamky)
    d out.%Set("rows", rows)
    q out
}

/// Tarifní čísla, na kterých v daném směru **aspoň jeden spoj staví** - `zastavuje(poradi) = 1`.
/// Staví = má v trase reálný čas; „projíždí" (`|`) ani „jede jinudy" (`&lt;`) se nepočítá.
/// 1:1 legacy `linkaZastavujeTc` (`JRapiLinPrtWin.mac:1637-1659`), včetně toho, že se to počítá
/// **per směr** - zastávka obsluhovaná jen v jednom směru zmizí jen z toho druhého.
ClassMethod ZastavujiciTc(dataDB As %String, sku As %String, lin As %String, smer As %String, ByRef zastavuje)
{
    k zastavuje
    s sp = ""
    f {
        s sp = $o(^[dataDB]JRL(sku,lin,smer,sp))
        q:sp=""
        i '$isvalidnum(sp) continue
        s cisloTrasy = $p($p($g(^[dataDB]JRL(sku,lin,smer,sp)),"#",1),":",3)
        i cisloTrasy = "" continue
        k wMap
        d ..WMapa(dataDB, sku, lin, cisloTrasy, .wMap)
        s pp = ""
        f {
            s pp = $o(wMap(pp))
            q:pp=""
            i ##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).CasHodnota($g(wMap(pp))) = "" continue
            s zastavuje(pp) = 1
        }
    }
}

/// JSON objekt jednoho zastávkového řádku (hlavní "zastavka" nebo vložený "prijezd").
ClassMethod BuildZastavkaRadek(dataDB As %String, sku As %String, lin As %String, smer As %String, poradi As %String, veta As %String, typ As %String, pocetKmSloupcu As %Integer, maxKmVeta As %String, jeTam As %Boolean, tiskZony As %Boolean = 0, tiskTc As %Boolean = 1, bezPismenVZone As %Boolean = 0, spolecneKm As %Boolean = 0, obousmerne As %Boolean = 0) As %DynamicObject
{
    s czst = $p(veta,":",1)
    s z = ##class(%DynamicObject).%New()
    d z.%Set("PORADI",     +poradi)
    d z.%Set("TYP",        typ)
    d z.%Set("CZST",       czst)
    s atrZnaky = $p($p(veta,":",2),"#",1)
    s nastupiste = $p($p(veta,"#",3),":",$s(jeTam:1,1:2))
    s nazevZast = ..NazevZastavky(dataDB, czst)
    // Atribut trasy „S" = tisk stanoviště: k názvu se připojí označník
    // (`JRapiLinPrtWin.mac:3124`). Text „stan." je doslovná parita starého modulu - ten má
    // v MHD režimu „nást.", ale AD agenda jede vždy tou první větví (`txStan`, :2971).
    // ⚠️ V obousměrné sestavě se spojí **obě** stanoviště čárkou („stan.16,19") - řádek je
    // společný pro oba směry, takže vypsat jen to svoje by druhé zamlčelo (legacy :3371).
    i atrZnaky [ "S" {
        s stanoviste = nastupiste
        i obousmerne {
            s stanTam = $p($p(veta,"#",3),":",1), stanZpet = $p($p(veta,"#",3),":",2)
            s stanoviste = stanTam
            i stanZpet '= "" s stanoviste = $s(stanTam '= "":stanTam_","_stanZpet, 1:stanZpet)
        }
        i stanoviste '= "" s nazevZast = nazevZast_", stan."_stanoviste
    }
    d z.%Set("NAZEV_ZASTAVKY", nazevZast)
    // Zvýrazněná zastávka: příznak nese řádek, **vzhled si určí šablona** pravidlem zvýraznění
    // nad tímhle polem (prvek to už umí). Klíč je týž jako u vývěsky, ať binding funguje v obou.
    d z.%Set("AKTUALNI_ZASTAVKA", $s(..JeZvyraznena(nazevZast, poradi):"true", 1:"false"))
    // Prázdné TC = sloupec se netiskne; prvek sloupec bez hodnot sám schová.
    d z.%Set("TC",         $s(tiskTc:+poradi, 1:""))
    // zóna: tiskne se, jen když to říká `linIDS` (viz BuildStrankaLinky); prázdná → "-"
    // (1:1 legacy gInfoTc). Klíč je množné ZONY jako u zastávkového JŘ; tam je to pole zón,
    // tady jedna hodnota - klient obojí čte přes GetString (pole spojí "/"), takže se binding
    // chová stejně.
    s zona = $p($p(veta,":",5),"#",2)
    i bezPismenVZone s zona = ..ZonaBezPismen(zona)
    s zona = $s('tiskZony:"", zona="":"-", 1:zona)
    d z.%Set("ZONY",       zona)
    // MHD/IDS text z kódu v "#"-poli 4 J-věty (legacy gTrsLin dta,6 → gTrsLinDta2ToText)
    d z.%Set("MHD",        ..MhdText($p(veta,"#",4)))
    d z.%Set("NASTUPISTE", nastupiste)

    s atributy = ##class(%DynamicArray).%New()
    f ai=1:1:$l(atrZnaky) d atributy.%Push($e(atrZnaky,ai))
    d z.%Set("ATRIBUTY", atributy)

    // km sloupce; -1 → "<" (jede jinudy); Zpět: km = maxKm - km (legacy KmSloupce/MaxKm).
    // Obousměrná sestava bere společný seznam, viz komentář u `spolecneKm` výše.
    s km = ##class(%DynamicArray).%New()
    f sl=1:1:pocetKmSloupcu {
        d ..PushKm(km, dataDB, sku, lin, smer, sl, poradi, maxKmVeta, jeTam, spolecneKm)
    }
    d z.%Set("KM", km)
    q z
}

/// Jedna hodnota km sloupce do pole: -1 = "<" (jede jinudy), prázdné = prázdno, směr "zpět"
/// se odečítá od maxima sloupce (1:1 legacy `KmSloupce`/`MaxKm`).
ClassMethod PushKm(km As %DynamicArray, dataDB As %String, sku As %String, lin As %String, smer As %String, sl As %Integer, poradi As %String, maxKmVeta As %String, jeTam As %Boolean, spolecne As %Boolean = 0)
{
    s kmVal = $p(..KmVeta(dataDB, sku, lin, smer, sl, spolecne),":",poradi)
    i kmVal = -1 { d km.%Push("<") }
    elseif kmVal = "" { d km.%Push("") }
    elseif 'jeTam { d km.%Push($p(maxKmVeta,":",sl) - kmVal) }
    else { d km.%Push(+kmVal) }
}

/// Označení zóny bez písmen (`51A` → `51`). Seznam znaků je 1:1 legacy `delZnk^JRapiLinPrtPS2`
/// (`:105`) - jen ASCII písmena, diakritika se v označení zón nevyskytuje.
ClassMethod ZonaBezPismen(zona As %String) As %String
{
    q $tr(zona, "QWERTYUIOPASDFGHJKLZXCVBNMqwertyuiopasdfghjklzxcvbnm", "")
}

/// MHD kód → zobrazovaný text (1:1 legacy gTrsLinDta2ToText; bez IDP-P/Z overridů z nastavení).
ClassMethod MhdText(val As %String) As %String
{
    i val = "" q ""
    i ",1,2,3,4,5,6,7,8,9," [ (","_val_",") q "MHD"
    i val [ "MHD" q "MHD"
    i val [ "I" q "IDS"
    i val [ "P" q "IDP-P"
    i val [ "Z" q "IDP-Z"
    i val [ "B" q "BID"
    q ""
}

/// Obsah jednoho km sloupce (":"-věta) - ze společného seznamu, nebo ze směrového.
/// **Jediné místo, které o tom rozhoduje**, ať se počet sloupců a jejich obsah nerozejdou.
ClassMethod KmVeta(dataDB As %String, sku As %String, lin As %String, smer As %String, sl As %Integer, spolecne As %Boolean = 0) As %String
{
    i spolecne q $g(^[dataDB]JRL(sku,lin,"K",sl))
    q $g(^[dataDB]JRL(sku,lin,"PATCH","KmsSmer",smer,sl))
}

/// Max km per km-sloupec (":"-věta) pro přepočet směru Zpět (legacy MaxKm; -1 a prázdné ignoruje).
ClassMethod MaxKmVeta(dataDB As %String, sku As %String, lin As %String, smer As %String, pocetKmSloupcu As %Integer, spolecne As %Boolean = 0) As %String
{
    s veta = ""
    f sl=1:1:pocetKmSloupcu {
        s kmVeta = ..KmVeta(dataDB, sku, lin, smer, sl, spolecne)
        s mymax = 0
        f i=1:1:$l(kmVeta,":") {
            s v = $p(kmVeta,":",i)
            i (v '= "") && (v '= -1) && (v > mymax) s mymax = v
        }
        s $p(veta,":",sl) = mymax
    }
    q veta
}

/// Seřadí spoje směru 1:1 s legacy mkPoradiSp^JRapiSpUtl1: primárně dle času odjezdu z výchozí
/// zastávky spoje, pak bublinové doseřazení porovnáním časů v jednotlivých tarifních číslech
/// (v pořadí směru jízdy). Vrací serazeneSpoje(1..n)=sp.
ClassMethod SeradSpoje(dataDB As %String, sku As %String, lin As %String, smer As %String, ByRef serazeneSpoje, ByRef pocetSpoju)
{
    k serazeneSpoje
    s pocetSpoju = 0

    // ruční pořadí nastavené u linky má přednost (legacy volá mkPoradiSp s par=1)
    i ..RucniPoradiSpoju(dataDB, sku, lin, smer, .serazeneSpoje, .pocetSpoju) q

    // primární pořadí: odjezd (hlava:4), při shodě číslo spoje
    k razeni
    s sp = ""
    f {
        s sp = $o(^[dataDB]JRL(sku,lin,smer,sp))
        q:sp=""
        i '$isvalidnum(sp) continue
        s odjezd = +$p($p($g(^[dataDB]JRL(sku,lin,smer,sp)),"#",1),":",4)
        s razeni(odjezd, sp) = ""
    }
    s odjezd = ""
    f {
        s odjezd = $o(razeni(odjezd))
        q:odjezd=""
        s sp = ""
        f {
            s sp = $o(razeni(odjezd, sp))
            q:sp=""
            s pocetSpoju = pocetSpoju + 1
            s serazeneSpoje(pocetSpoju) = sp
        }
    }
    i pocetSpoju < 2 q

    // časová mapa spoje: cas(sp, poradi) = odjezd + W offset (jen numerické)
    f i=1:1:pocetSpoju {
        s sp = serazeneSpoje(i)
        s hlava = $p($g(^[dataDB]JRL(sku,lin,smer,sp)),"#",1)
        s odj = +$p(hlava,":",4)
        d ..WMapaProRazeni(dataDB, sku, lin, $p(hlava,":",3), .wMap)
        s pp = ""
        f {
            s pp = $o(wMap(pp))
            q:pp=""
            s c = ..CasProRazeni($g(wMap(pp)))
            i c '= "" s cas(sp, pp) = odj + c
        }
    }

    // bublinové doseřazení 1:1 legacy mkPoradiSp: fáze 1 přes odjezdová tč spojů (TcOd = hlava:1),
    // fáze 2 přes příjezdová (TcDo = hlava:2), tč v pořadí směru jízdy; v každém tč se sousedé
    // s definovaným časem prohodí, je-li dřívější spoj pozdější.
    // ⚠️ Směr průchodu je proti intuici: u „tam" se jde tarifními čísly **od konce**
    // (legacy `Par=-1` pro T, `Par=1` pro Z). Měli jsme to obráceně a projevilo se to
    // prohozenými sousedními spoji (43/45 na KV_DP 0410107).
    s odKonce = (smer = "T")
    k tcOdSet, tcDoSet
    f i=1:1:pocetSpoju {
        s hlava = $p($g(^[dataDB]JRL(sku,lin,smer,serazeneSpoje(i))),"#",1)
        i $p(hlava,":",1) '= "" s tcOdSet(+$p(hlava,":",1)) = ""
        i $p(hlava,":",2) '= "" s tcDoSet(+$p(hlava,":",2)) = ""
    }
    f faze=1,2 {
        s tc = ""
        f {
            i faze=1 { s tc = $s(odKonce:$o(tcOdSet(tc),-1), 1:$o(tcOdSet(tc))) }
            else     { s tc = $s(odKonce:$o(tcDoSet(tc),-1), 1:$o(tcDoSet(tc))) }
            q:tc=""
            f pass=1:1:pocetSpoju {
                s zmena = 0
                f i=1:1:pocetSpoju-1 {
                    s spA = serazeneSpoje(i), spB = serazeneSpoje(i+1)
                    i '$d(cas(spA, tc)) continue
                    i '$d(cas(spB, tc)) continue
                    i cas(spA, tc) > cas(spB, tc) {
                        s serazeneSpoje(i) = spB, serazeneSpoje(i+1) = spA
                        s zmena = 1
                    }
                }
                q:'zmena
            }
        }
    }
}

/// **Ruční pořadí spojů nastavené u linky** (`PATCH,SORT`) - vrátí 1, když se použilo.
///
/// Uživatel si smí pořadí spojů přeskládat ručně a starý modul to respektuje. Zahodí ho ale
/// **celé a pro oba směry**, jakmile nesedí se skutečnými spoji: buď je v pořadí spoj, který
/// v datech není, nebo v datech je spoj, který v pořadí chybí (typicky po přidání spoje).
/// Pak se pořadí spočítá. 1:1 legacy `PoradiSpRucne^JRapiSpUtl1`.
///
/// ⚠️ **Kontrola musí jít přes oba směry, i když plníme jeden** - jinak by u linky s rozbitým
/// pořadím jednoho směru druhý směr ruční pořadí použil a starý modul ne.
///
/// ⚠️ **Reálně nastavené na 4 409 linkách** ze šesti měřených firem (21. 8.), takže to není
/// okrajová věc - do 21. 8. jsme ruční pořadí ignorovali úplně.
ClassMethod RucniPoradiSpoju(dataDB As %String, sku As %String, lin As %String, smer As %String, ByRef serazeneSpoje, ByRef pocetSpoju) As %Boolean
{
    // výstup si drží metoda sama - volá se i mimo `SeradSpoje` (z testu)
    k serazeneSpoje
    s pocetSpoju = 0
    q:'$d(^[dataDB]JRL(sku,lin,"PATCH","SORT")) 0

    k jeVRucnim
    s chyba = 0, sm = ""
    f {
        s sm = $o(^[dataDB]JRL(sku,lin,"PATCH","SORT",sm))
        q:sm=""
        q:chyba
        s poradi = ""
        f {
            s poradi = $o(^[dataDB]JRL(sku,lin,"PATCH","SORT",sm,poradi))
            q:poradi=""
            s sp = +$g(^[dataDB]JRL(sku,lin,"PATCH","SORT",sm,poradi))
            i '$d(^[dataDB]JRL(sku,lin,sm,sp)) s chyba = 1  q
            s jeVRucnim(sm,sp) = ""
        }
    }
    q:chyba 0

    // druhá polovina kontroly: každý spoj v datech musí být v ručním pořadí
    f sm = "T","Z" {
        s sp = ""
        f {
            s sp = $o(^[dataDB]JRL(sku,lin,sm,sp))
            q:sp=""
            i '$d(jeVRucnim(sm,sp)) s chyba = 1  q
        }
        q:chyba
    }
    q:chyba 0

    s poradi = ""
    f {
        s poradi = $o(^[dataDB]JRL(sku,lin,"PATCH","SORT",smer,poradi))
        q:poradi=""
        s pocetSpoju = pocetSpoju + 1
        s serazeneSpoje(pocetSpoju) = +$g(^[dataDB]JRL(sku,lin,"PATCH","SORT",smer,poradi))
    }
    q pocetSpoju > 0
}

/// Trasa pro **řazení spojů**, složená přes všechny navazující záznamy (`W(c)`, `W(c+.01)`, …).
/// Legacy skládá trasu takhle **jen tady** (`mkOdjCas^JRapiSpUtl1`), v tiskové cestě ne - proto
/// se to nesmí slévat s `WMapa`, kterou se kreslí časy. Dotčené jsou 3 linky ze 93 tisíc měřených.
ClassMethod WMapaProRazeni(dataDB As %String, sku As %String, lin As %String, cisloTrasy As %String, ByRef wMap)
{
    k wMap
    i cisloTrasy = "" q
    s trasa = "", c = +cisloTrasy
    f {
        q:'$d(^[dataDB]JRL(sku,lin,"W",c))
        s trasa = trasa_$g(^[dataDB]JRL(sku,lin,"W",c))
        s c = c + .01
    }
    f i=1:1:$l(trasa,"#") {
        s seg = $p(trasa,"#",i)
        s pom = $p(seg,":",1)
        i pom '= "" s wMap(+pom) = $p(seg,":",2)
    }
}

/// Čas zastávky **pro řazení spojů**. ⚠️ Legacy tu bere u pobytové zastávky **příjezd**, ne odjezd
/// (`+$p(kus,":",2)` nad „12/2" dá 12), takže se nesmí použít `CasHodnota`, která přičítá čekání.
/// Rozdíl se projeví prohozením sousedních spojů; pobyt má v trase 38 655 linek z měřených firem.
ClassMethod CasProRazeni(cas As %String) As %String
{
    i (cas = "") || (cas = "<") || (cas = "|") q ""
    q +$p(cas,"/",1)
}

/// Turnusy všech spojů linky: `turnusy(<číslo spoje>, <označení turnusu>) = ""`.
///
/// Turnusy **nejsou v `^JRL`** - drží je modul turnusů a vytahují se voláním
/// `ALVA.ADMAIN.MLAPI.gSplTur` (týmž, jaké volá starý tisk, `JRapiLinPrtWin.mac:2697`).
/// Odpověď má tvar `out("arr",1,linka,spoj,zpracování,turnus,datum)`.
///
/// ⚠️ **Sjednocuje se podle čísla turnusu, ne podle celého označení** (rozhodnutí Jan 28. 8.).
/// Označení je `<turnus>.<varianta>` a modul vrací záznam za každou kombinaci zpracování × datum,
/// takže jeden turnus vyjde v mnoha variantách: na lince 0320625 měl spoj 1 osm označení
/// `51164.01`-`51164.09`, ale **jediný** turnus 51164 (2 zpracování × 18 dnů). Tisknout osm řádků
/// o tomtéž turnusu nemá pro čtenáře cenu; varianty jsou vnitřní členění oběhu po dnech.
/// Legacy tiskne celé označení jen proto, že nic nesjednocuje.
///
/// ⚠️ **Datum platnosti bere z linky** (`PATCH,"Plati"`), ne dnešní - turnus se hledá ke dni,
/// ke kterému platí jízdní řád. Linka bez datumu platnosti se dotázat nedá, tam turnusy nebudou.
ClassMethod NactiTurnusy(dataDB As %String, sku As %String, lin As %String, ByRef turnusy)
{
    k turnusy
    s dh = $lg(##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).HorologPlatnostiLinky(dataDB, sku, lin), 1)
    q:dh=""
    k tin, tout
    f smerSp = "T", "Z" {
        s sp = ""
        f {
            s sp = $o(^[dataDB]JRL(sku,lin,smerSp,sp))
            q:sp=""
            continue:'$isvalidnum(sp)
            s tin("arr", 1, +lin, sp) = ""
        }
    }
    q:'$d(tin)
    s chyba = ##class(ALVA.ADMAIN.MLAPI).gSplTur(dataDB, dataDB, dh, .tin, .tout)
    // Chyba se **nepolyká**: uživatel si turnusy vyžádal parametrem, takže tisk bez nich by byl
    // tichý fallback - pozná se až na papíře, že tam nejsou.
    i +chyba d ..ChybaDat("Turnusy ke spojům nejde načíst"_$s($p(chyba,"~",2)'="":" ("_$p(chyba,"~",2)_")", 1:"")_". Zkontrolujte turnusy v modulu AD, nebo u Tabulky spojů vypněte Řádky turnusů.")
    s sp = ""
    f {
        s sp = $o(tout("arr",1,+lin,sp))
        q:sp=""
        s zp = ""
        f {
            s zp = $o(tout("arr",1,+lin,sp,zp))
            q:zp=""
            s tu = ""
            f {
                s tu = $o(tout("arr",1,+lin,sp,zp,tu))
                q:tu=""
                s turnusy(+sp, $p(tu,".",1)) = ""
            }
        }
    }
}

/// Doplní spoji pole `TURNUSY` (označení turnusů v pořadí číselníku). Spoj bez turnusu klíč
/// vůbec nedostane - aditivní wire změna (R3), starší klient ho ignoruje.
ClassMethod PridejTurnusySpoje(spojObj As %DynamicObject, ByRef turnusy, sp As %String)
{
    q:'$d(turnusy(+sp))
    s arr = ##class(%DynamicArray).%New()
    s tu = ""
    f {
        s tu = $o(turnusy(+sp, tu))
        q:tu=""
        d arr.%Push(tu)
    }
    d spojObj.%Set("TURNUSY", arr)
}

/// Sestaví JSON objekt jednoho spoje (sloupec matice): řádek platnosti (plný klíčový systém
/// legacy Platnost/Spoj2Tb), NZ, časy per řádek, návaznosti.
ClassMethod BuildSpoj(dataDB As %String, sku As %String, lin As %String, smer As %String, sp As %String, skupina As %Integer, pocetRadku As %Integer, ByRef rowMeta, rec As %String, ByRef platnostZnakySeen, ByRef nzSeen, ByRef pripojePoznamky, ByRef markerSeen, ByRef atributSeen, ByRef mistenkaSeen, ByRef trasaZnakySeen, ByRef spojeSeen, tiskBarvy As %Boolean = 1, ByRef poznamkaSpojeSeen) As %DynamicObject
{
    s spoj = $g(^[dataDB]JRL(sku,lin,smer,sp))
    s hlava = $p(spoj,"#",1)
    s cisloTrasy = $p(hlava,":",3)
    // každý spoj stránky - tvar věty atributu „všechny spoje" (legacy pSp v gAtrNZLi počítá všechny
    // spoje, ne jen ty s atributem; do 26. 10. se tu počítaly jen spoje s atributem, `B194` T14d)
    s spojeSeen(+sp) = ""

    // per-tč značky spoje z trasy (W segment ":"-piece 5, legacy gSpLin trs,6): N/V/x/G/a/b/c
    // → legenda druhu 7 i do buňky vedle času (`znackyTc`), kam je starý tisk kreslí
    // (JRapiLinPrtWin.mac:3838 a :1409). Zastávka na objednání „r" / podmínečně „i" jde do buňky
    // taky (telefon / vykřičník, GSId :1629), ale legendu nemá druhu 7 - vysvětluje ji věta OZO / OZP
    // druhu 5 (`JRapiNZUtl3.mac:68-72, 135-158`). Do 26. 10. se r/i vynechávaly úplně (`B194` T12).
    k znackyTc
    s wTrasaVeta = $g(^[dataDB]JRL(sku,lin,"W",cisloTrasy))
    f wi=1:1:$l(wTrasaVeta,"#") {
        s wSegment = $p(wTrasaVeta,"#",wi)
        s wPoradi = $p(wSegment,":",1)
        s wZnaky = $p(wSegment,":",5)
        f zi=1:1:$l(wZnaky) {
            s wZnak = $e(wZnaky,zi)
            i wZnak = "" continue
            i (wZnak = "r") || (wZnak = "i") {
                s klicZast = $s(wZnak = "r":"OZO", 1:"OZP")
                s atributSeen(klicZast, +sp) = ##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).UpresneniAtributuSpoje(dataDB, sku, lin, smer, sp, $s(wZnak = "r":"RBU", 1:"RBP"))
            }
            else { s trasaZnakySeen(wZnak) = "" }
            i wPoradi '= "" s znackyTc(+wPoradi) = $g(znackyTc(+wPoradi))_wZnak
        }
    }
    s odjezd = +$p(hlava,":",4)
    s plk = $p(hlava,":",7)
    s cnz = $p(spoj,"#",7)

    s o = ##class(%DynamicObject).%New()
    d o.%Set("CISLO_SPOJE", +sp)
    d o.%Set("SKUPINA", skupina)
    // Směr spoje posílá **každá** stránka, i jednosměrná - u obousměrné je to jediné, čím prvek
    // pozná, kam sloupec patří, a jednotný tvar je pro klienta jednodušší než pole navíc jen někdy.
    d o.%Set("SMER", smer)
    d o.%Set("NZ", cnz)
    // Dopravci spoje (#9417) - týmiž jmény, jaká skládá `PopisDopravce`, aby si klient mohl
    // `POPIS_DOPRAVCE` sestavit jen za spoje na stránce, i s jejich čísly jako starý tisk.
    s dopravciSpoje = ##class(%DynamicArray).%New()
    k videnDopravce
    s carriers = $p(spoj, "#", 2)
    f ci=1:1:$l(carriers,"~") {
        s slot = $p(carriers,"~",ci)
        continue:$p(slot,":",1)=""
        s nazevDop = ..NazevDopravce(dataDB, $p(slot,":",1), $p(slot,":",2))
        continue:nazevDop=""
        continue:$d(videnDopravce(nazevDop))
        s videnDopravce(nazevDop) = ""
        d dopravciSpoje.%Push(nazevDop)
    }
    d o.%Set("DOPRAVCI", dopravciSpoje)
    // Barva spoje jako prezentační nápověda (R3): 12. „#"-pole záznamu ve tvaru „r;g;b".
    // Prázdno = šablona rozhodne; `0;0;0` je v legacy „nenastaveno", ne černá, takže se neposílá.
    // Nastavení u linky (a parametr sestavy) barvy potlačí - pak se neposílá nic.
    i tiskBarvy {
        s barva = ##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).BarvaSpoje($p(spoj,"#",12))
        i barva '= "" d o.%Set("FORE_COLOR", barva)
    }

    // ── klíče řádku platnosti (1:1 legacy Platnost → Spoj2Tb pevné pořadí) ──
    k klice
    f i=1:1:$l(plk) {
        s znak = $e(plk,i)
        i "1234567X+" [ znak s klice(znak) = "", platnostZnakySeen(znak) = ""
    }
    // místenkování: hlava:6 ("R") + hlava:11 (typ) → R / þ(R1) / &(R2) / Ü(R3)
    // ⚠️ **Čtyři druhy zná jen slovenská verze.** Česká sloučí obě varianty s kancelářemi AMS
    // zpátky na R a þ (1:1 legacy JRapiLinPrtWin.mac:2448, texty gMstNZLi^JRapiNZUtl3), takže
    // ampersand ani Ü se u české firmy vytisknout nesmí - kolega to našel na lince 320913.
    s mistenka = $tr($p(hlava,":",6)_$p(hlava,":",11), " ", "")
    i rec '= "*SK" s mistenka = $case(mistenka, "R2":"R", "R3":"R1", :mistenka)
    s mistZnak = $case(mistenka, "R":"R", "R1":$c(254), "R2":"&", "R3":$c(220), :"")
    // spoj u značky: věty ke spojům v legendě vypíšou jen spoje se značkou na stránce (trtSp)
    i mistZnak '= "" s klice(mistZnak) = "", mistenkaSeen(mistZnak, +sp) = ""
    // pokračuje (omz34, typ výstupu ≠ 2) → klíč u (nebo p dle ^JRSET("Cis","comU34znk"))
    s omz34 = $g(^[dataDB]JRL(sku,lin,smer,sp,34))
    i (omz34 '= "") && ($p($p(omz34,"@",2),":",5) '= 2) {
        s klice($s(+$g(^[dataDB]JRSET("Cis","comU34znk")):"p", 1:"u")) = ""
    }
    // poznámka pro spoj/tč (omz41/42) → klíč p
    i ($d(^[dataDB]JRL(sku,lin,smer,sp,41))) || ($d(^[dataDB]JRL(sku,lin,smer,sp,42))) s klice("p") = "", poznamkaSpojeSeen(smer, +sp) = ""
    // atributy spoje (gSpLin dta ↔ hlava pieces: POS=12, IDS=14, BEV=15, SAO=16, PCZ=17, OBC=18,
    // PJI=19, NKA=20, RBU=21, CBE=22, RBP=23, wifi=25, zasu=26)
    f ai=1:1:13 {
        s klic = $p("POS,IDS,BEV,SAO,PCZ,OBC,PJI,NKA,RBU,CBE,RBP,wifi,zasu", ",", ai)
        s piece = $p("12,14,15,16,17,18,19,20,21,22,23,25,26", ",", ai)
        i +$p(hlava,":",piece) {
            s klice(klic) = ""
            // u „na objednání" a „podmínečně" nese spoj i upřesnění pro větu legendy
            s atributSeen(klic, +sp) = $s((klic = "RBU") || (klic = "RBP"):##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).UpresneniAtributuSpoje(dataDB, sku, lin, smer, sp, klic), 1:"")
        }
    }
    // PLATNOST = klíče v pevném legacy pořadí (Spoj2Tb ř.3754)
    s platnost = ##class(%DynamicArray).%New()
    f ki=1:1:26 {
        s klic = $p("X,1,2,3,4,5,6,7,+,R,þ,&,Ü,u,p,BEV,PJI,SAO,NKA,RBU,CBE,RBP,PCZ,OBC,wifi,zasu", ",", ki)
        i $d(klice(klic)) d platnost.%Push(klic)
    }
    d o.%Set("PLATNOST", platnost)
    i cnz '= "" s nzSeen(cnz) = ""

    // hranice spoje pro zdvojené řádky: první/poslední obsluhované poradi (v pořadí směru)
    d ..WMapa(dataDB, sku, lin, cisloTrasy, .wMap)
    s jeTam = (smer = "T")
    s prvniPoradi = "", posledniPoradi = ""
    f i=1:1:pocetRadku {
        i $lg(rowMeta(i),2) '= "O" continue
        s pp = $lg(rowMeta(i),1)
        i ##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).CasHodnota($g(wMap(pp))) '= "" {
            i prvniPoradi = "" s prvniPoradi = pp
            s posledniPoradi = pp
        }
    }

    // časy per řádek: "" mimo trasu, "|" projíždí, "<" jede jinudy, jinak HH:MM.
    // Hlavní řádek = odjezd (příjezd+čekání); vložený "P" řádek = příjezd (bez čekání).
    // Kraje spoje u zdvojených zastávek: na první obsluhované chybí příjezd, na poslední odjezd
    // (1:1 legacy Spoj2Tb ořez casobefore/casonext).
    s casy = ##class(%DynamicArray).%New()
    f i=1:1:pocetRadku {
        s pp = $lg(rowMeta(i),1), typRadku = $lg(rowMeta(i),2)
        s cas = $g(wMap(pp))
        i cas = "<" { d casy.%Push("<")  s markerSeen("<") = "" }
        elseif cas = "|" { d casy.%Push("|")  s markerSeen("|") = "" }
        elseif cas = "" { d casy.%Push("") }
        elseif typRadku = "P" {
            i pp = prvniPoradi { d casy.%Push("") }
            else { d casy.%Push(##class(ALVA.TemplateDesigner.Agendy.Jr.VystupLinkovy).FormatCas(odjezd + +$p(cas,"/",1))) }
        }
        else {
            i (pp = posledniPoradi) && ($d(rowMeta(i-1)) && ($lg($g(rowMeta(i-1)),2) = "P") && ($lg($g(rowMeta(i-1)),1) = pp)) {
                d casy.%Push("")          // zdvojená konečná: odjezd se maže, zůstává příjezd
            }
            else { d casy.%Push(##class(ALVA.TemplateDesigner.Agendy.Jr.VystupLinkovy).FormatCas(odjezd + ##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).CasHodnota(cas))) }
        }
    }
    d o.%Set("CASY", casy)

    // Značky u zastávky per řádek, stejně indexované jako CASY. Pole se posílá jen když má linka
    // aspoň jednu značku, ať se stránka zbytečně nenafukuje. U zdvojené zastávky je starý tisk
    // kreslí k odjezdu i k příjezdu (oracle CB_GWBUS 216/0340941: paragraf u spoje 3, telefon
    // u spoje 8). Do 26. 10. tu byl jen odjezdový řádek s odkazem na tc2row (`B194` T12);
    // příjezdový dostane značky, jen když má čas.
    i $d(znackyTc) {
        s maZnacku = 0
        s znacky = ##class(%DynamicArray).%New()
        f i=1:1:pocetRadku {
            s pp = $lg(rowMeta(i),1), typRadku = $lg(rowMeta(i),2)
            s znakyRadku = $g(znackyTc(pp))
            i typRadku = "P" {
                s casRadku = casy.%Get(i-1)
                i (casRadku = "") || (casRadku = "|") || (casRadku = "<") s znakyRadku = ""
            }
            i znakyRadku '= "" s maZnacku = 1
            d znacky.%Push(znakyRadku)
        }
        i maZnacku d o.%Set("ZNACKY", znacky)
    }

    // návaznosti: omzId 31 = vyčká, 32 = navazuje. Věta = znak@sekce@sekce…, sekce = tc#…:
    //   omz31: tc#maxCekaniMin#conn…, conn = AVL:linka:spoj:typVystupu:smerOdkud:minPrijezdu:
    //          typZobrazeni:dopravce:min:smerKam:linkaProTisk
    //   omz32: tc#conn…, conn = linka:spoj:smerKam:typVystupu:prefCsad:AVL:tablo:minOdjezdu:
    //          typZobrazeni:scitatCenu:dopravce:linkaProTisk
    // typVystupu=2 (jen exp. strojek) → vůbec netisknout; typZobrazeni: 0=značkou u spoje,
    // 1=v přípojném řádku, 2=obojí (1:1 g3132/cnvPrtn31/32).
    s pripoje = ##class(%DynamicArray).%New()
    f omzId = 31, 32 {
        s omz = $g(^[dataDB]JRL(sku,lin,smer,sp,omzId))
        i omz = "" continue
        s typ = $s(omzId=31:"vycka", 1:"navazuje")
        // Značku určuje VÝZNAM, ne první kus věty - ta nese u obou druhů "m", takže se navazující
        // spoj tiskl značkou pro vyčkání (nalezeno porovnáním se starým modulem, L1).
        // 1:1 legacy arr3132: omz31 → "m" (vyčká), omz32 → "^" (navazuje), JRapiLinPrtWin.mac:2210,2226.
        s znak = $s(omzId=31:"m", 1:"^")
        f sekceIdx=2:1:$l(omz,"@") {
            s sekce = $p(omz,"@",sekceIdx)
            i sekce = "" continue
            s tc = $p(sekce,"#",1)
            s prvniConn = $s(omzId=31:3, 1:2)
            s fraze = ""                       // agregát vět „spoje X linky Y…" pro poznámku sekce
            f connIdx=prvniConn:1:$l(sekce,"#") {
                s conn = $p(sekce,"#",connIdx)
                i conn = "" continue
                i omzId = 31 {
                    s linka2 = $p(conn,":",2), spoj2 = $p(conn,":",3)
                    s kam = $p(conn,":",5), casMin = +$p(conn,":",6)
                    s typVystupu = +$p(conn,":",4), typZobrazeni = +$p(conn,":",7)
                    s avl = $p(conn,":",1), linkaProTisk = $p(conn,":",11)
                }
                else {
                    s linka2 = $p(conn,":",1), spoj2 = $p(conn,":",2)
                    s kam = $p(conn,":",3), casMin = +$p(conn,":",8)
                    s typVystupu = +$p(conn,":",4), typZobrazeni = +$p(conn,":",9)
                    s avl = $p(conn,":",6), linkaProTisk = $p(conn,":",12)
                }
                // Vlastní „linka pro tisk" přebíjí číslo linky - starý modul to dělá vždy, když je
                // pole vyplněné (`JRapiLinPrtWin01.mac:76,135`), bez ohledu na cokoli dalšího.
                // Naměřeno 4. 9.: 3 515 z 71 889 přípojů ho má, u 3 512 se číslo liší - u vlaku
                // stojí v surovém poli číslo vlaku („Os 8009"), ale tisknout se má označení linky
                // („S1"). Nejvíc CB_GWBUS (29 %).
                i linkaProTisk '= "" s linka2 = linkaProTisk
                i typVystupu = 2 continue                 // jen exportní strojek - na JŘ vůbec

                s pr = ##class(%DynamicObject).%New()
                d pr.%Set("TYP", typ)
                d pr.%Set("PORADI", +tc)
                d pr.%Set("ZNAK", znak)
                d pr.%Set("TEXT", kam)
                d pr.%Set("CAS", ##class(ALVA.TemplateDesigner.Agendy.Jr.VystupLinkovy).FormatCas(casMin))
                d pr.%Set("LINKA", linka2)
                d pr.%Set("SPOJ", spoj2)
                // Druh dopravy (A autobus / V vlak / L loď) - prvek podle něj **seskupuje** přípojné
                // řádky, takže se autobus nesloučí s vlakem téhož směru. Starý modul ho má v klíči
                // vždy (`JRapiLinPrtWin01.mac:89`). Prázdné legacy čte jako autobus.
                d pr.%Set("AVL", $s(avl="":"A", 1:$zcvt(avl,"U")))
                // Přípoj bez minuty příjezdu nebo bez směru řádek nedostane - jinak v tabulce vznikne
                // řádek s časem „00:00" a prázdným popiskem. Starý modul ho zahodí týmiž dvěma
                // podmínkami (`JRapiLinPrtWin01.mac:84-87`); naměřeno 4. 9.: 965 z 71 889 přípojů,
                // které řádek dělají (nejvíc CB_GWBUS 4,2 %). Poznámky pod tabulkou se to netýká -
                // ty mají vlastní cestu podle typZobrazeni.
                s maRadek = ((typZobrazeni=1) || (typZobrazeni=2)) && (casMin > 0) && (kam '= "")
                d pr.%Set("RADEK", $s(maRadek:1, 1:0))
                d pripoje.%Push(pr)

                // fráze do agregované poznámky legendy (typZobrazeni 0 nebo 2). Návaznost bez druhu
                // dopravy (staré záznamy bez pole AVL) starý tisk do věty nedá vůbec - zná jen A, V, L
                // (n31 / n32 `JRapiNZUtl2.mac:266-281, 425-439`); do 26. 10. ji tu věta brala jako
                // autobus (`B194` T14c). Přípojný řádek ji dál bere jako autobus, jako starý tisk.
                i ((typZobrazeni=0) || (typZobrazeni=2)) && $case(avl, "A":1, "V":1, "L":1, :0) {
                    // AVL abeceda: A=autobus, V=vlak, L=loď (legacy $tr("AVL","ALD") před GSId)
                    i omzId = 31 {
                        // „spoje 25 linky 330510" / vlak: „vlaku 20880" (bez čísla spoje); [ z SMĚR].
                        // NCOME 31 kus 4 = „vlaku:linky:lodě z" - slova V:A:L a předložka za mezerou.
                        s kus4 = ..TextNavaznosti(dataDB, 31, 4)
                        s slovoAvl = $p($p(kus4," ",1), ":", $case(avl, "V":1, "L":3, :2))
                        s predlozka = $p(kus4, " ", 2, 99)
                        // „spoje" (2. pád) v číselníku není - slovensky „spoja"
                        s slovoSpoje = $s(..KodJazykaCiselniku(dataDB) = "*SK":"spoja", 1:"spoje")
                        s f31 = $s(+spoj2:slovoSpoje_" "_(+spoj2)_" ", 1:"")_slovoAvl_" "_..OznaceniLinky(linka2)
                        i kam '= "" s f31 = f31_" "_predlozka_" "_kam
                        s fraze = fraze_$s(fraze="":"", 1:",")_f31
                    }
                    else {
                        // „spoj 24 linky 330530 do X" / vlak: „vlak 20875 do X" (NCOME 32 kusy 3-7)
                        i avl = "V" { s f32 = ..TextNavaznosti(dataDB, 32, 6)_" "_..OznaceniLinky(linka2) }
                        elseif avl = "L" { s f32 = ..TextNavaznosti(dataDB, 32, 7)_" "_..OznaceniLinky(linka2) }
                        else { s f32 = ..TextNavaznosti(dataDB, 32, 3)_" "_(+spoj2)_" "_..TextNavaznosti(dataDB, 32, 4)_" "_..OznaceniLinky(linka2) }
                        i kam '= "" s f32 = f32_" "_..TextNavaznosti(dataDB, 32, 5)_" "_kam
                        s fraze = fraze_$s(fraze="":"", 1:", ")_f32
                    }
                }
            }
            // jedna poznámka druhu 3 per (spoj × tc) - 1:1 legacy agregace vět; dedup přes text
            // (spoj ve víc skupinách dnů se zpracovává vícekrát)
            i fraze '= "" {
                s nazevTc = ..NazevZastavkyPoradi(dataDB, sku, lin, tc)
                // Slova vět jsou z číselníku NCOME 31/32 v jazyce šablony, jako ve starém tisku -
                // do 23. 9. tu byla česky natvrdo a slovenská šablona je tiskla česky (#9088).
                i omzId = 31 {
                    s txt = ..TextNavaznosti(dataDB, 31, 1)_" "_(+sp)_" "_..TextNavaznosti(dataDB, 31, 2)_" "_nazevTc
                        _" "_..TextNavaznosti(dataDB, 31, 3)_" "_fraze
                    i +$p(sekce,"#",2) s txt = txt_" "_..TextNavaznosti(dataDB, 31, 5)_" "_(+$p(sekce,"#",2))_" "_..TextNavaznosti(dataDB, 31, 6)
                    s bucket = 2
                }
                else {
                    s txt = ..TextNavaznosti(dataDB, 32, 1)_" "_(+sp)_" "_..TextNavaznosti(dataDB, 32, 2)_" "_nazevTc_" "_fraze
                    s bucket = 3
                }
                i '$d(pripojePoznamky("seen", txt)) {
                    s pripojePoznamky("seen", txt) = ""
                    s seq = $o(pripojePoznamky(bucket, ""), -1) + 1
                    s pripojePoznamky(bucket, seq) = ##class(ALVA.TemplateDesigner.Agendy.Jr.VystupLinkovy).BuildPoznamka(3, "znak", znak, txt)
                }
            }
        }
    }
    d o.%Set("PRIPOJE", pripoje)

    // pokračuje (omz34): „spoj N pokračuje na lince číslo L spojem číslo S do zastávky Z"
    // (typ výstupu :5 = 2 → netisknout). Jen poznámka, žádný řádek matice.
    s omz34 = $g(^[dataDB]JRL(sku,lin,smer,sp,34))
    i (omz34 '= "") && ($p($p(omz34,"@",2),":",5) '= 2) {
        s data34 = $p(omz34,"@",2)
        s znak34 = $s(+$g(^[dataDB]JRSET("Cis","comU34znk")):"p", 1:"u")
        s txt = ..TextPokracovaniSpoje(dataDB, rec, +sp, +$p(data34,":",2), +$p(data34,":",3), $p(data34,":",4))
        i '$d(pripojePoznamky("seen", txt)) {
            s pripojePoznamky("seen", txt) = ""
            s seq = $o(pripojePoznamky(1, ""), -1) + 1
            s pripojePoznamky(1, seq) = ##class(ALVA.TemplateDesigner.Agendy.Jr.VystupLinkovy).BuildPoznamka(3, "znak", znak34, txt)
        }
    }

    q o
}


/// Datum "d.m.rrrr" → $H počet dnů. "" při nevalidním vstupu.
ClassMethod DatumNaDh(dat As %String) As %String
{
    s dd = +$p(dat,".",1), mm = +$p(dat,".",2), rr = +$p(dat,".",3)
    i (dd < 1) || (mm < 1) || (rr < 1900) q ""
    s iso = rr_"-"_$tr($j(mm,2)," ",0)_"-"_$tr($j(dd,2)," ",0)
    q $zdateh(iso, 3, , , , , , , "")
}

/// $H den → platnostní klíče: den v týdnu 1(Po)-7(Ne); státní svátek přidá "+" (den nese oba -
/// 24.12. v sobotu patří do skupiny So i Ne+svátky, jako legacy kalendář).
ClassMethod DhNaKlic(dh As %Integer) As %String
{
    s datum = $zdate(dh, 3)                       // yyyy-mm-dd
    s rr = +$p(datum,"-",1), mm = +$p(datum,"-",2), dd = +$p(datum,"-",3)
    s dow = $zdate(dh, 10)                        // 0=Ne .. 6=So
    s klice = $s(dow=0:7, 1:dow)
    i ..JeSvatek(dd, mm, rr, dh) s klice = klice_"+"
    q klice
}

/// Český státní svátek (pevné + Velký pátek a Velikonoční pondělí dle computu).
ClassMethod JeSvatek(dd As %Integer, mm As %Integer, rr As %Integer, dh As %Integer) As %Boolean
{
    i ",1.1,1.5,8.5,5.7,6.7,28.9,28.10,17.11,24.12,25.12,26.12," [ (","_dd_"."_mm_",") q 1
    // Velikonoční neděle (Meeus/Jones/Butcher pro gregoriánský kalendář)
    s a = rr#19, b = rr\100, c = rr#100
    s d = b\4, e = b#4, f = (b+8)\25, g = (b-f+1)\3
    s h = ((19*a)+b-d-g+15)#30
    s i = c\4, k = c#4
    s l = (32+(2*e)+(2*i)-h-k)#7
    s m = (a+(11*h)+(22*l))\451
    s mesic = (h+l-(7*m)+114)\31
    s den = ((h+l-(7*m)+114)#31)+1
    s dhNedele = $zdateh(rr_"-"_$tr($j(mesic,2)," ",0)_"-"_$tr($j(den,2)," ",0), 3, , , , , , , "")
    i dhNedele = "" q 0
    q (dh = (dhNedele - 2)) || (dh = (dhNedele + 1))    // Velký pátek, Velikonoční pondělí
}

/// Zařazení spoje do skupin dnů dle módu dělení a Plk (legacy in("A",2,"arr",3)).
/// Spoj bez rozpoznané platnosti → všechny skupiny (legacy fallback HD 10183).
ClassMethod ZarazeniSpojeKnizni(deleni As %Integer, plk As %String) As %String
{
    i deleni = 0 q "1"

    s maPD = 0, maSo = 0, maNe = 0
    f i=1:1:$l(plk) {
        s znak = $e(plk,i)
        i "X12345" [ znak s maPD = 1
        i znak = 6 s maSo = 1
        i "7+" [ znak s maNe = 1
    }
    i ('maPD) && ('maSo) && ('maNe) q $s(deleni=1:"1,2", 1:"1,2,3")

    s skupiny = ""
    i deleni = 1 {
        i maPD s skupiny = "1"
        i maSo || maNe s skupiny = skupiny_$s(skupiny'="":",",1:"")_"2"
    }
    else {
        i maPD s skupiny = "1"
        i maSo s skupiny = skupiny_$s(skupiny'="":",",1:"")_"2"
        i maNe s skupiny = skupiny_$s(skupiny'="":",",1:"")_"3"
    }
    q skupiny
}

/// Vzory rozložení sloupců spojů - **jediný zdroj pravdy je server**, klient je nestaví sám.
/// Klíče jsou stabilní (visí na nich volba parametru), nadpisy jdou z téhož číselníku jako pásy
/// skupin, aby vzor vypadal stejně jako dnešní hrubé dělení.
ClassMethod PresetySloupcuLinky(dataDB As %String, rec As %String, sku As %String = "", lin As %String = "") As %String
{
    d ..NadpisySkupinDnu(dataDB, rec, .t, sku, lin)

    s out = ##class(%DynamicObject).%New()
    d out.%Set("BEZ_ROZLISENI", ..PresetSloupceLinky($lb($lb("1","",""))))
    d out.%Set("PRAC_VIKEND",   ..PresetSloupceLinky($lb($lb("1",t("dvojicePD"),"X"), $lb("2",t("dvojiceVikend"),"6+"))))
    d out.%Set("PRAC_SO_NE",    ..PresetSloupceLinky($lb($lb("1",t("PD"),"X"), $lb("2",t("SO"),"6"), $lb("3",t("NE"),"+"))))
    q out.%ToJSON()
}

/// Nadpisy skupin dnů z číselníku WinMHD DSC (`^JRSET("Definice","WinMHD",rec,"DSC")`) podle
/// starého tisku ve tvaru MHD (`JRapiLinPrtMhd1.mac:544-562`): trojice pracovní dny / soboty /
/// neděle a svátky = kusy 2-4, dvojice pracovní dny / víkend = kusy **8 a 9**.
/// Do 24. 10. brala dvojice kus 2 a kus 7 (třetí nadpis prázdninové trojice), takže firma
/// s vyplněnou sadou tiskla „Pracovní dny" vedle „soboty, neděle a svátky" (`B194a` C8).
/// Prázdný kus 8 nebo 9 → dřívější kus 2 / 7 (rozhodnutí Jan 24. 10.; starý tisk by nechal
/// nadpis prázdný), prázdné všechno → pevný text.
/// Čte se společně se Zastávkovým JŘ AD (`Jr.DataAd.NadpisySkupinDnuZNastaveni`, `B194` T11): i sada nadpisů
/// u linky, a zalomení `<br>` (CB_GWBUS „Sobota,<br> Neděle…") se v jednořádkovém nadpisu tiskne mezerou -
/// do 26. 10. vyšlo doslova.
ClassMethod NadpisySkupinDnu(dataDB As %String, rec As %String, ByRef t, sku As %String = "", lin As %String = "")
{
    k t
    d ##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).NadpisySkupinDnuZNastaveni(dataDB, sku, lin, rec, 0, "pracovní dny~soboty~neděle a svátky", .trojice)
    d ##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).NadpisySkupinDnuZNastaveni(dataDB, sku, lin, rec, 1, "~~soboty, neděle a svátky", .prazdniny)
    s t("PD") = ..JednimRadkem(.trojice, 1), t("SO") = ..JednimRadkem(.trojice, 2), t("NE") = ..JednimRadkem(.trojice, 3)
    d ##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).NadpisySkupinDnuZNastaveni(dataDB, sku, lin, rec, 2, t("PD")_"~"_..JednimRadkem(.prazdniny, 3), .dvojice)
    s t("dvojicePD") = ..JednimRadkem(.dvojice, 1), t("dvojiceVikend") = ..JednimRadkem(.dvojice, 2)
}

/// Nadpis `t(i)` s případným druhým řádkem `t(i,2)` do jednoho řádku (linkový JŘ má nadpis skupiny jednořádkový).
ClassMethod JednimRadkem(ByRef t, i As %Integer) As %String
{
    q $g(t(i))_$s($g(t(i,2)) '= "":" "_t(i,2), 1:"")
}

/// Jeden vzor: seznam `$lb(id, nadpis, dny)` → pole sloupců ve wire tvaru `DEFINICE_SLOUPCU`.
ClassMethod PresetSloupceLinky(sloupce As %List) As %DynamicArray
{
    s arr = ##class(%DynamicArray).%New()
    f i=1:1:$ll(sloupce) {
        s def = $li(sloupce, i)
        s sl = ##class(%DynamicObject).%New()
        d sl.%Set("id", $li(def,1))
        d sl.%Set("nadpis", $li(def,2))
        d sl.%Set("dny", $li(def,3))
        d arr.%Push(sl)
    }
    q arr
}

/// Zařazení spoje podle **vlastní definice sloupců** ze šablony. Význam definice (dny, značky
/// „jen s těmito", značky „bez těchto") drží `BaseAgenda`, aby táž definice znamenala v obou
/// rodinách agend totéž.
///
/// ⚠️ **Osa Období se tu vědomě nevyhodnocuje** - vývěska k ní má podklad z ISY, linkový JŘ ne.
/// Sloupec s obdobím proto v lince omezí jen dny a značky; je to zapsané v roadmapě.
///
/// Vrací CSV pořadí sloupců (1..N), ne jejich `id` - prvek pracuje s pořadím pásu.
ClassMethod ZarazeniSpojeDleDefiniceLinky(ByRef defArr, plk As %String, cnz As %String) As %String
{
    s dnySpoje = ##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).DnySpoje(plk)
    s out = ""
    f i=1:1:+$g(defArr) {
        i '##class(ALVA.TemplateDesigner.Agendy.Jr.VlastniSloupce).JeSpojVeSloupci(.defArr, i, dnySpoje, cnz) continue
        s out = out_$s(out="":"", 1:",")_i
    }
    q out
}

/// Nadpisy pásů podle vlastní definice - v pořadí, v jakém je uživatel nadefinoval.
ClassMethod BuildSkupinyZDefinice(ByRef defArr) As %DynamicArray
{
    s arr = ##class(%DynamicArray).%New()
    f i=1:1:+$g(defArr) {
        s nadpis = defArr(i,"nadpis")
        i $g(defArr(i,"nadpis2")) '= "" s nadpis = nadpis_" "_defArr(i,"nadpis2")
        d arr.%Push(##class(ALVA.TemplateDesigner.Agendy.Jr.VystupLinkovy).BuildSkupina(i, nadpis))
    }
    q arr
}

/// Definice skupin dnů - nadpisy viz `NadpisySkupinDnu`.
ClassMethod BuildSkupinyDnu(dataDB As %String, rec As %String, deleni As %Integer, sku As %String = "", lin As %String = "") As %DynamicArray
{
    d ..NadpisySkupinDnu(dataDB, rec, .t, sku, lin)

    s arr = ##class(%DynamicArray).%New()
    i deleni = 0 {
        d arr.%Push(##class(ALVA.TemplateDesigner.Agendy.Jr.VystupLinkovy).BuildSkupina(1, ""))
    }
    elseif deleni = 1 {
        d arr.%Push(##class(ALVA.TemplateDesigner.Agendy.Jr.VystupLinkovy).BuildSkupina(1, t("dvojicePD")))
        d arr.%Push(##class(ALVA.TemplateDesigner.Agendy.Jr.VystupLinkovy).BuildSkupina(2, t("dvojiceVikend")))
    }
    else {
        d arr.%Push(##class(ALVA.TemplateDesigner.Agendy.Jr.VystupLinkovy).BuildSkupina(1, t("PD")))
        d arr.%Push(##class(ALVA.TemplateDesigner.Agendy.Jr.VystupLinkovy).BuildSkupina(2, t("SO")))
        d arr.%Push(##class(ALVA.TemplateDesigner.Agendy.Jr.VystupLinkovy).BuildSkupina(3, t("NE")))
    }
    q arr
}

/// Poznámky druhu 1 - týdenní platnosti (jen znaky použité na stránce, pořadí X,1-7,+).
/// Texty z ^JRSET("NCOME",rec,znak), fallback hardcoded.
ClassMethod PridatPoznamkyPlatnosti(dataDB As %String, rec As %String, poznamky As %DynamicArray, ByRef platnostZnakySeen)
{
    f i=1:1:10 {
        s znak = $p("X,1,2,3,4,5,6,7,+", ",", i)
        i znak = "" continue
        i '$d(platnostZnakySeen(znak)) continue
        s txt = $g(^[dataDB]JRSET("NCOME",rec,znak))
        i txt = "" s txt = ..TextPlatnosti(znak)
        d poznamky.%Push(##class(ALVA.TemplateDesigner.Agendy.Jr.VystupLinkovy).BuildPoznamka(1, "znak", znak, txt))
    }
}

/// Fallback text týdenní platnosti (když číselník NCOME chybí).
ClassMethod TextPlatnosti(znak As %String) As %String
{
    q $case(znak,
        "X":"jede v pracovních dnech",
        1:"jede v pondělí", 2:"jede v úterý", 3:"jede ve středu", 4:"jede ve čtvrtek",
        5:"jede v pátek", 6:"jede v sobotu", 7:"jede v neděli",
        "+":"jede v neděli a ve státem uznané svátky", :"")
}

/// Poznámky druhu 2 - negativní značky: text z datumových omezení spojů nesoucích danou NZ.
/// `smery` = seznam směrů oddělený čárkou („T" nebo „T,Z" u obousměrné stránky).
ClassMethod PridatPoznamkyNZ(dataDB As %String, sku As %String, lin As %String, smery As %String, rec As %String, tiskDtm As %Integer, poznamky As %DynamicArray, ByRef nzSeen)
{
    s znak = ""
    f {
        s znak = $o(nzSeen(znak))
        q:znak=""
        s txt = ""
        // ⚠️ U obousměrné stránky nese značku i spoj opačného směru, takže se text hledá v obou.
        // Kdyby se hledal jen v tom „svém", u takové značky by legenda zůstala bez textu.
        f smerIdx = 1:1:$l(smery, ",") {
            s smerNz = $p(smery, ",", smerIdx)
            s sp = ""
            f {
                s sp = $o(^[dataDB]JRL(sku,lin,smerNz,sp))
                q:sp=""
                i '$isvalidnum(sp) continue
                i $p($g(^[dataDB]JRL(sku,lin,smerNz,sp)),"#",7) '= znak continue
                s txt = ##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).TextOmezeni(dataDB, sku, lin, smerNz, sp, rec, tiskDtm)
                q:txt'=""
            }
            q:txt'=""
        }
        i txt '= "" d poznamky.%Push(##class(ALVA.TemplateDesigner.Agendy.Jr.VystupLinkovy).BuildPoznamka(2, "nz", znak, txt))
    }
}

/// Poznámky druhu 5 - atributy spojů (BEV, PJI, SAO…). Legacy v režimu „značka nad spojem"
/// (pozorovaný gold) tiskne jednu generickou větu bez výčtu spojů - značky u konkrétních spojů
/// nese řádek platnosti.
///
/// **Pořadí je legacy pořadí**, ne naše abecední: řádky legendy jdou v tom pořadí, v jakém je
/// starý modul prochází (POS, IDS, BEV, SAO, PCZ, OBC, PJI, NKA, RBU, CBE, RBP, wifi, zasu,
/// a nakonec OZO / OZP - zastávka na objednání / podmínečně, `JRapiNZUtl3.mac` cnt 530 / 534).
///
/// Řádek dostane **každý** atribut, který se na stránce vyskytl - nastavení „značka atributu nad
/// spoj" neřídí, **jestli** se vytiskne, ale **jakým tvarem** (viz `VetaAtributu`).
ClassMethod PridatPoznamkyAtributu(dataDB As %String, sku As %String, lin As %String, poznamky As %DynamicArray, ByRef atributSeen, ByRef spojeSeen, rec As %String = "CZ", tiskDtm As %Integer = 0)
{
    s patrSp = $g(^[dataDB]JRSET("Definice","PAtrSp"))
    s bbpLinka = +$g(^[dataDB]JRL(sku,lin,"PATCH","TZPA","BBP"))
    // kolik spojů je na stránce celkem - podle toho se pozná tvar „všechny spoje"
    s spojuCelkem = 0, sp = ""
    f { s sp = $o(spojeSeen(sp))  q:sp=""  s spojuCelkem = spojuCelkem + 1 }
    f ki=1:1:15 {
        s klic = $p("POS,IDS,BEV,SAO,PCZ,OBC,PJI,NKA,RBU,CBE,RBP,wifi,zasu,OZO,OZP", ",", ki)
        i '$d(atributSeen(klic)) continue
        // spoje, které atribut mají - v pořadí čísla spoje
        s vycet = "", pocet = 0, sp = ""
        f {
            s sp = $o(atributSeen(klic, sp))
            q:sp=""
            s pocet = pocet + 1
            s vycet = vycet_$s(vycet="":"", 1:", ")_sp
        }
        s veta = ..VetaAtributu(klic, rec, ..GenerickaVeta(klic, patrSp, bbpLinka), vycet, pocet, spojuCelkem)
        i veta = "" continue
        i (klic = "RBU") || (klic = "RBP") || (klic = "OZO") || (klic = "OZP") {
            d ..PridatVetyUpresneni(poznamky, klic, rec, veta, .atributSeen)
            continue
        }
        i (klic = "PJI") || (klic = "BEV") s veta = ..DoplnOmezeniAtributu(dataDB, sku, lin, klic, rec, veta, .atributSeen, tiskDtm)
        d poznamky.%Push(##class(ALVA.TemplateDesigner.Agendy.Jr.VystupLinkovy).BuildPoznamka(5, "znak", klic, veta))
    }
}

/// Omezení platnosti atributu u spoje za větou atributu, 1:1 gAtrNZLi (`JRapiNZUtl3.mac:80-117,
/// 257-312`). Linka ho drží po spojích v `PATCH,"attrOm"` (19 = jízdní kola, 15 = bezbariérové
/// vozidlo) jako „od~do~počet~text", za „#" druhé období; počet a text jen u kol. Spoje se stejným
/// omezením tvoří skupinu: mají-li ho všechny spoje s atributem, připíše se rovnou za větu, jinak
/// za každou skupinu „ / spoj(e) výčet omezení". Do 26. 10. se omezení netisklo (`B194` T14a).
ClassMethod DoplnOmezeniAtributu(dataDB As %String, sku As %String, lin As %String, klic As %String, rec As %String, veta As %String, ByRef atributSeen, tiskDtm As %Integer) As %String
{
    s idOm = $case(klic, "PJI":19, "BEV":15, :"")
    q:idOm="" veta
    s sablona = ..AtributSablona(klic, rec)
    k skupiny
    s pocet = 0, sp = ""
    f {
        s sp = $o(atributSeen(klic, sp))
        q:sp=""
        s pocet = pocet + 1
        s om = $g(^[dataDB]JRL(sku,lin,"PATCH","attrOm",idOm,sp))
        s o1 = $p(om,"#",1), o2 = $p(om,"#",2)
        s od1 = +..DatumNaDh($p(o1,"~",1)), do1 = +..DatumNaDh($p(o1,"~",2))
        s od2 = +..DatumNaDh($p(o2,"~",1)), do2 = +..DatumNaDh($p(o2,"~",2))
        s poc1 = 0, poc2 = 0, info1 = "", info2 = ""
        i idOm = 19 {
            s poc1 = +$p(o1,"~",3), poc2 = +$p(o2,"~",3), info1 = $p(o1,"~",4), info2 = $p(o2,"~",4)
            continue:'(od1 || od2 || poc1 || (info1 '= "") || (info2 '= ""))
            s:'do1 do1 = od1
            s:'od1 od1 = do1
        }
        else {
            // bezbariérové vozidlo: chybějící „od" se z „do" nedoplní (legacy :106-112)
            continue:'(od1 || od2)
            s:'do1 do1 = od1
        }
        // pořadí skupin jako legacy (od1, do1, počet1, od2, do2, počet2 vzestupně, pak text):
        // čísla pevné šířky se řadí jako text stejně jako čísla
        s razeni = ""
        f cislo = od1, do1, poc1, od2, do2, poc2 s razeni = razeni_$j(cislo, 6)
        s razeni = razeni_"|"_info1_"|"_info2
        i '$d(skupiny(razeni)) s skupiny(razeni) = $lb(od1, do1, poc1, info1, od2, do2, poc2, info2), skupiny(razeni, "spoje") = "", skupiny(razeni, "pocet") = 0
        s skupiny(razeni, "spoje") = skupiny(razeni, "spoje")_$s(skupiny(razeni, "spoje")="":"", 1:", ")_sp
        s skupiny(razeni, "pocet") = skupiny(razeni, "pocet") + 1
    }
    s razeni = ""
    f {
        s razeni = $o(skupiny(razeni))
        q:razeni=""
        s d = skupiny(razeni)
        s txt1 = ..TextOmezeniAtributu($li(d,1), $li(d,2), $li(d,3), $li(d,4), sablona, tiskDtm)
        s txt2 = ..TextOmezeniAtributu($li(d,5), $li(d,6), $li(d,7), $li(d,8), sablona, tiskDtm)
        s txt = txt1_$s(txt2'="":", "_txt2, 1:"")
        i skupiny(razeni, "pocet") = pocet {
            s veta = veta_" "_txt
            continue
        }
        s veta = veta_" / "_$s(skupiny(razeni, "pocet")=1:$p(sablona,"|",3), 1:$p(sablona,"|",4))_" "_skupiny(razeni, "spoje")_" "_txt
    }
    q $zstrip(veta, "<=>W")
}

/// Jedno období omezení atributu: „od D1 do D2" podle režimu data, u kol „nejvýše N kol(a)"
/// a text spoje (legacy oddotxt). `od` / `do` jsou $H, 0 = nezadáno.
ClassMethod TextOmezeniAtributu(od As %Integer, do As %Integer, pocet As %Integer, info As %String, sablona As %String, tiskDtm As %Integer) As %String
{
    s txt = ""
    i od s txt = ##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).FormatDatumyOmezeni(..DhNaDatum(od)_":"_$s(do:..DhNaDatum(do), 1:""), tiskDtm)
    i pocet s txt = txt_$s(txt="":"", 1:", ")_$p(sablona,"|",8)_" "_pocet_" "_$p(sablona,"|",9)
    i info '= "" s txt = txt_$s(txt="":"", 1:", ")_info
    q txt
}

/// $H den → „d.m.rrrr" (vstup pro `FormatDatumyOmezeni`).
ClassMethod DhNaDatum(dh As %Integer) As %String
{
    s iso = $zdate(dh, 3)
    q (+$p(iso,"-",3))_"."_(+$p(iso,"-",2))_"."_$p(iso,"-",1)
}

/// Věty „na objednání" a „podmínečně" s upřesněním (`UpresneniAtributuSpoje`). Mají-li spoje
/// stránky totéž upřesnění, přilepí se za obecnou větu; liší-li se, dostane každé vlastní větu
/// s výčtem spojů. Prázdné upřesnění je taky jedno z nich, takže spoj bez něj vedle spoje s ním
/// dostane větu bez dovětku. 1:1 legacy `gAtrNZLi^JRapiNZUtl3` (`:227-256`).
///
/// Vědomě jinak: věta s výčtem bere i 7. díl šablony, legacy ho tam vynechává. V češtině to nic
/// nemění (u RBU i RBP je prázdný), slovenská věta by ale skončila „spoj 5 je obsluhovaný …"
/// bez „radiobusom".
ClassMethod PridatVetyUpresneni(poznamky As %DynamicArray, klic As %String, rec As %String, obecnaVeta As %String, ByRef atributSeen)
{
    // různá upřesnění v pořadí čísel spojů: texty(i) = upřesnění, spojeTextu(i) = výčet spojů
    k texty, spojeTextu, pocetSpoju
    s pocetTextu = 0, sp = ""
    f {
        s sp = $o(atributSeen(klic, sp))
        q:sp=""
        s txt = atributSeen(klic, sp), idx = 0
        f i=1:1:pocetTextu { i texty(i) = txt { s idx = i  q } }
        i 'idx s pocetTextu = pocetTextu + 1, idx = pocetTextu, texty(idx) = txt, spojeTextu(idx) = "", pocetSpoju(idx) = 0
        s spojeTextu(idx) = spojeTextu(idx)_$s(spojeTextu(idx)="":"", 1:", ")_sp
        s pocetSpoju(idx) = pocetSpoju(idx) + 1
    }
    i pocetTextu = 1 {
        d poznamky.%Push(##class(ALVA.TemplateDesigner.Agendy.Jr.VystupLinkovy).BuildPoznamka(5, "znak", ..ZnakAtributuLegendy(klic), obecnaVeta_$s(texty(1)="":"", 1:" "_texty(1))))
        q
    }
    s sablona = ..AtributSablona(klic, rec)
    f i=1:1:pocetTextu {
        s veta = $s(pocetSpoju(i) > 1:..SlozVetu(sablona, 4, spojeTextu(i), 6), 1:..SlozVetu(sablona, 3, spojeTextu(i), 5))
        i texty(i) '= "" s veta = veta_" "_texty(i)
        d poznamky.%Push(##class(ALVA.TemplateDesigner.Agendy.Jr.VystupLinkovy).BuildPoznamka(5, "znak", ..ZnakAtributuLegendy(klic), veta))
    }
}

/// Značka řádku legendy atributu: zastávka na objednání / podmínečně (OZO / OZP) má tentýž telefon
/// a vykřičník jako spoj na objednání / podmínečně (legacy GSId `JRapiLinPrtWin.mac:1629`).
ClassMethod ZnakAtributuLegendy(klic As %String) As %String
{
    q $case(klic, "OZO":"RBU", "OZP":"RBP", :klic)
}

/// Má se u atributu použít **generická věta** (bez výčtu spojů), nebo věta s výčtem?
/// 1:1 legacy `gAtrNZLi^JRapiNZUtl3` (`:196-215`), kde se generický tvar přepisuje přes vypočtený:
///
/// - **BEV, PJI, SAO, NKA** - generická jen když je zapnutá „značka atributu nad spoj" (`PAtrSp`,
///   `~`-věta bezbariérové~kola~samoobslužný~nízkokapacitní). U BEV navíc **linka nastavení
///   přebíjí** (`PATCH,TZPA,BBP`: 1 = ano, 2 = ne, jinak platí globální).
/// - **POS a IDS** - generickou větu nemají **nikdy**, vždy se vypisují čísla spojů. Legacy pro ně
///   žádný přepis nemá, a je to smysluplné: „za zvláštních podmínek zásilky" a „v systému
///   integrované dopravy" se týkají konkrétních spojů, ne linky.
/// - **zbytek** - generická vždy.
///
/// ⚠️ **Našlo to porovnání se starým modulem** (21. 8.): u bezbariérového vozidla jsme posílali
/// generickou větu i tam, kde starý modul vypisuje spoje. **POS a IDS doloženy porovnáním 31. 8.**
/// na jediných linkách v datech, kde se ty atributy vyskytují (`VS_CSAD 13/0883637` a `9/0240801`)
/// - obě věty se se starým modulem shodují, viz `LinkovyJrAdLegacyTest.TestVetyAtributuProtiLegacy`.
ClassMethod GenerickaVeta(klic As %String, patrSp As %String, bbpLinka As %Integer) As %Boolean
{
    i (klic = "POS") || (klic = "IDS") q 0
    // BEV: linka přebíjí globální nastavení (1 = tisknout, 2 = netisknout, jinak globální).
    i klic = "BEV" {
        i bbpLinka = 1 q 1
        i bbpLinka = 2 q 0
        q ''+$p(patrSp,"~",1)
    }
    i klic = "PJI" q ''+$p(patrSp,"~",2)
    i klic = "SAO" q ''+$p(patrSp,"~",3)
    i klic = "NKA" q ''+$p(patrSp,"~",4)
    q 1
}

/// Věta atributu spoje, složená ze **šablony starého modulu**. Čtyři tvary, 1:1 legacy
/// `gAtrNZLi^JRapiNZUtl3` (`:193-215`); šablona je `|`-věta
/// `znak|všechny|spoj|spoje|j.č.|mn.č.|doplněk`:
///
/// - **značka nad spojem zapnutá** → generická věta bez výčtu: `3. + 5. + 7. díl`
/// - **všechny spoje stránky** ho mají → `2. + 4. + 6. + 7. díl` („všechny spoje s…")
/// - **víc spojů, ne všechny** → `4. díl + výčet + 6. + 7. díl` („spoje 1, 2 s…")
/// - **jediný spoj** → `3. díl + číslo + 5. + 7. díl` („spoj 19 přepravuje…")
///
/// U některých atributů je 5. nebo 6. díl prázdný (BEV, CBE), takže věta vyjde **bez slovesa** -
/// „spoj s bezbariérově přístupným vozidlem". Prázdný díl proto nesmí nechat dvojitou mezeru.
///
/// ⚠️ **Dvě chyby, které tady našlo porovnání se starým modulem** (`L1` a 21. 8.): měli jsme věty
/// opsané po svém a u BEV nám přebývalo „jede"; a tiskli jsme generickou větu i tam, kde starý
/// modul vypisuje čísla spojů. Šablony proto neopisovat volně - jen doslova.
///
/// ⚠️ **Texty NEJSOU v číselníku.** Rozhodnutí „brát je z `^JRSET("NCOME")`" (analýza §13.4 bod 6)
/// bylo postavené na omylu: v číselníku jsou předložky omezení a stálé značky, kdežto tyhle věty
/// má legacy **zadrátované ve zdroji**, zvlášť pro češtinu a slovenštinu. Zákazník je tedy upravit
/// nemůže ani ve starém modulu - a slovenština funguje tím, že se vezme druhá sada.
///
/// ⚠️ **Rozsah počítání spojů je stránka, a je to správně** (dořešeno 31. 8., dřív tu stálo, že
/// legacy počítá přes celou linku a my jinak). Starý modul počítá **v rozsahu, ve kterém tiskne
/// legendu**: má-li blok poznámek na směr, počítá spoje směru (`JRapiLinPrtWin` předá `in("sm")`),
/// má-li jeden blok za linku, počítá obě strany. Náš model = stránka na směr s vlastní legendou,
/// tedy první případ; u obousměrné stránky se spoje sčítají přes oba směry, což odpovídá druhému.
/// Doloženo porovnáním na `VS_CSAD 24/0941210`, kde atribut mají všechny spoje směru Z, ale ne celé
/// linky - obě strany tam říkají „všechny spoje".
ClassMethod VetaAtributu(klic As %String, rec As %String = "CZ", znackaNadSpojem As %Boolean = 0, vycetSpoju As %String = "", pocetSpoju As %Integer = 0, spojuCelkem As %Integer = 0) As %String
{
    s sablona = ..AtributSablona(klic, rec)
    q:sablona="" ""
    i znackaNadSpojem q ..SlozVetu(sablona, 3, "", 5)
    i (pocetSpoju > 1) && (pocetSpoju = spojuCelkem) q ..SlozVetu(sablona, 2, $p(sablona,"|",4), 6)
    i pocetSpoju > 1 q ..SlozVetu(sablona, 4, vycetSpoju, 6)
    q ..SlozVetu(sablona, 3, vycetSpoju, 5)
}

/// Poskládá větu z dílů šablony: `<uvodniDil> <vlozka> <slovesnyDil> <7. díl>`.
/// Prázdný díl se přeskočí, ať nezůstane dvojitá mezera.
ClassMethod SlozVetu(sablona As %String, uvodniDil As %Integer, vlozka As %String, slovesnyDil As %Integer) As %String
{
    s veta = ""
    f kus = $p(sablona,"|",uvodniDil), vlozka, $p(sablona,"|",slovesnyDil), $p(sablona,"|",7) {
        s cast = $zstrip(kus, "<>W")
        i cast '= "" s veta = veta_$s(veta="":"", 1:" ")_cast
    }
    q veta
}

/// Šablona věty atributu, opsaná **doslova** z legacy `gAtrNZLi^JRapiNZUtl3` (`:161-192`).
/// Dvě jazykové sady, protože je tak má i starý modul; `rec` = „CZ" nebo „*SK" (viz `KodJazykaCiselniku`).
/// Jediná vědomá odchylka: u `CBE` opravený překlep „částečne" → „částečně" (`B194` T4b).
ClassMethod AtributSablona(klic As %String, rec As %String = "CZ") As %String
{
    i rec = "*SK" {
        q $case(klic,
            "POS":"POS|všetky|spoj|spoje|prepravuje|prepravujú|za osobitných podmienok zásielky, bez sprievodu cestujúceho",
            "IDS":"IDS|všetky|spoj|spoje|je|sú|v systéme integrovanej dopravy",
            "BEV":"BEV|všetky|spoj|spoje|||s bezbariérovým prístupom do vozidla",
            "SAO":"SAO|všetky|spoj|spoje|||so samoobslužným spôsobom odbavovania cestujúcich",
            "PCZ":"PCZ|všetky|spoj|spoje|prepravuje|prepravujú|cestovnú batožinu",
            "OBC":"OBC|všetky|spoj|spoje|s možnosťou|s možnosťou|občerstvenia",
            "PJI":"PJI|všetkými|spojom|spoji|možno prepravovať|možno přepravovať|bicykle|najviac|bicykel(ov)",
            "NKA":"NKA|všetky|spoj|spoje|je obsluhovaný|sú obsluhované|nízkokapacitnej autobusom",
            "RBU":"RBU|všetky|spoj|spoje|je obsluhovaný|sú obsluhované|radiobusom",
            "CBE":"CBE|všetky|spoj|spoje|||s čiastočne bezbariérovým prístupným vozidlom, nutná dopomoc sprievodcu",
            "RBP":"RBP|všetky|spoj|spoje|jede len podmienečne|idú len podmienečne|",
            "wifi":"wifi|všetky|spoj|spoje|s bezdrôtovým pripojením k internetu",
            "zasu":"zasu|všetky|spoj|spoje|s prípojkou 230 V",
            "OZO":"OZO|všetky|spoj|spoje|zastávku obsluhuje len na objednanie|zastávku obsluhujú len na objednanie|",
            "OZP":"OZP|všetky|spoj|spoje|zastávku obsluhuje len podmienečne|zastávku obsluhujú len podmienečne|",
            :"")
    }
    q $case(klic,
        "POS":"POS|všechny|spoj|spoje|přepravuje|přepravují|za zvláštních podmínek zásilky, bez doprovodu cestujícího",
        "IDS":"IDS|všechny|spoj|spoje|je|jsou|v systému integrované dopravy",
        "BEV":"BEV|všechny|spoj|spoje|||s bezbariérově přístupným vozidlem",
        "SAO":"SAO|všechny|spoj|spoje|má|mají|samoobslužný způsob odbavování cestujících",
        "PCZ":"PCZ|všechny|spoj|spoje|přepravuje|přepravují|cestovní zavazadla",
        "OBC":"OBC|všechny|spoj|spoje|s možností|s možností|občerstvení",
        "PJI":"PJI|všechny|spoj|spoje|přepravuje|přepravují|jízdní kola|nejvýše|kol(a)",
        "NKA":"NKA|všechny|spoj|spoje|je obsluhován|jsou obsluhovány|nízkokapacitním autobusem",
        "RBU":"RBU|všechny|spoj|spoje|jede jen na objednání|jedou jen na objednání|",
        "CBE":"CBE|všechny|spoj|spoje|||s částečně bezbariérově přístupným vozidlem, nutná dopomoc průvodce",
        "RBP":"RBP|všechny|spoj|spoje|jede jen podmínečně|jedou jen podmínečně|",
        "wifi":"wifi|všechny|spoj|spoje|s bezdrátovým připojením k internetu",
        "zasu":"zasu|všechny|spoj|spoje|s přípojkou 230 V",
        "OZO":"OZO|všechny|spoj|spoje|zastávku obsluhuje jen na objednání|zastávku obsluhují jen na objednání|",
        "OZP":"OZP|všechny|spoj|spoje|zastávku obsluhuje jen podmínečně|zastávku obsluhují jen podmínečně|",
        :"")
}

/// Poznámky druhu 4 - poznámka pro spoj a pro tarifní body spoje (`omz` 41 / 42, značka „p"),
/// 1:1 gComNZLi → p41 / p42 / sComToArr (`JRapiNZUtl2.mac:73-91, 559-588`) a výstup
/// `JRapiLinPrtWin.mac:3582-3594`: každý text zvlášť, spoje se stejným textem v jednom řádku
/// „spoj 1,3,5 text" (čísla bez mezer, „spoj" i u víc spojů). Poznámka k tarifnímu bodu dostane
/// úvod z číselníku firmy (`NCOME` 42) a číslo bodu. Do 26. 10. se tiskla jen značka u spoje
/// a legenda ji nevysvětlila (`B194` T13b).
/// ⚠️ Firma s `comU34znk` dává pod „p" i věty „pokračuje" (n34); na serveru ji nemá žádná firma,
/// ty věty zůstávají v návaznostech.
ClassMethod PridatPoznamkySpoju(dataDB As %String, sku As %String, lin As %String, rec As %String, poznamky As %DynamicArray, ByRef poznamkaSpojeSeen)
{
    k texty, poradi
    s uvod42 = $g(^[dataDB]JRSET("NCOME",rec,42))
    // pořadí jako legacy: směr T před Z, spoje vzestupně, u spoje 41 před 42
    s smer = ""
    f {
        s smer = $o(poznamkaSpojeSeen(smer))
        q:smer=""
        s sp = ""
        f {
            s sp = $o(poznamkaSpojeSeen(smer, sp))
            q:sp=""
            // sdílený spoj se označuje „číslo/sdílené číslo" (gComNZLi oznsp)
            s sdil = $p($p($p($g(^[dataDB]JRL(sku,lin,smer,sp)),"#",1),":",10),"$",1)
            s oznSp = sp_$s(sdil:"/"_sdil, 1:"")
            i $d(^[dataDB]JRL(sku,lin,smer,sp,41)) {
                s omz = $g(^[dataDB]JRL(sku,lin,smer,sp,41))
                i $p(omz,"@",1) = "p" d ..PridatTextSpoje(.texty, .poradi, $tr($p(omz,"@",2,$l(omz,"@")),"\",""), oznSp)
            }
            i $d(^[dataDB]JRL(sku,lin,smer,sp,42)) && ($p($g(^[dataDB]JRL(sku,lin,smer,sp,42)),"@",1) = "p") {
                s tb = ""
                f {
                    s tb = $o(^[dataDB]JRL(sku,lin,smer,sp,42,tb), 1, pozn)
                    q:tb=""
                    d ..PridatTextSpoje(.texty, .poradi, uvod42_" "_tb_" : "_$tr(pozn,"\",""), oznSp)
                }
            }
        }
    }
    f n=1:1:$g(texty) d poznamky.%Push(##class(ALVA.TemplateDesigner.Agendy.Jr.VystupLinkovy).BuildPoznamka(4, "znak", "p", "spoj "_texty(n,"sp")_" "_texty(n)))
}

/// Přidá spoj k textu poznámky pro spoj; stejný text = jeden řádek (sComToArr). Prázdný text nic.
ClassMethod PridatTextSpoje(ByRef texty, ByRef poradi, text As %String, oznSp As %String)
{
    q:text=""
    i '$d(poradi(text)) s n = $i(texty), poradi(text) = n, texty(n) = text, texty(n,"sp") = oznSp q
    s n = poradi(text)
    s texty(n,"sp") = texty(n,"sp")_","_oznSp
}

/// Poznámky druhu 6 - místenkování, 1:1 gMstNZLi (`JRapiNZUtl3.mac:325-399`): věta podle druhu
/// místenky, za ní obecný doplněk linky (`^JRL(…,"C",2)`) a poznámky k vybraným spojům
/// (`"C",2,n` = „spoje~text"), ze kterých zůstanou jen spoje s touto značkou na stránce.
/// Nastavení firmy (`JRapiSet.mac:316-317`): `MistOR` = každá poznámka ke spojům na nový řádek
/// („#" legenda láme jako odstavec), `MistNOD` = bez dvojtečky před nimi. Do 26. 10. se tiskla
/// jen věta bez doplňku linky (`B194` T13a).
ClassMethod PridatPoznamkyMistenkovani(dataDB As %String, sku As %String, lin As %String, rec As %String, poznamky As %DynamicArray, ByRef mistenkaSeen)
{
    s oddelovac = $s($d(^[dataDB]JRSET("Definice","MistOR")):"#", 1:" ")
    s dvojtecka = (+$g(^[dataDB]JRSET("Definice","MistNOD")) = 0)
    // legacy podmínka `'$e(veta,1)'=" "` platí vždy, mezera se tedy přidává bez výjimky
    s doplnek = $g(^[dataDB]JRL(sku,lin,"C",2))
    s:doplnek'="" doplnek = " "_doplnek
    s maPoznamkySpoju = ($o(^[dataDB]JRL(sku,lin,"C",2,"")) '= "")
    k vety
    // klíče značek v pořadí $o jako legacy (& , AMS, R, Ü, þ)
    s znak = ""
    f {
        s znak = $o(mistenkaSeen(znak))
        q:znak=""
        s txt = ..TextMistenky(znak, rec)
        continue:txt=""
        i (rec = "*SK") && ((znak = "&") || (znak = $c(220))) s vety("AMS") = "automatizovaný miestenkový systém vo vybraných mestách"
        s txt = txt_doplnek
        i maPoznamkySpoju {
            s:dvojtecka txt = txt_":"
            s pocet = 0, n = ""
            f {
                s n = $o(^[dataDB]JRL(sku,lin,"C",2,n), 1, veta)
                q:n=""
                s vstup = $tr($p(veta,"~",1),";",","), spoje = ""
                f i=1:1:$l(vstup,",") {
                    s spoj = $p(vstup,",",i)
                    continue:spoj=""
                    continue:'$d(mistenkaSeen(znak, spoj))
                    s spoje = spoje_$s(spoje="":"", 1:",")_spoj
                }
                continue:spoje=""
                s pocet = pocet + 1
                s:pocet>1 txt = txt_";"
                s txt = txt_oddelovac_$s($l(spoje,",")>1:"na spoje", 1:"na spoj")_" "_spoje_" "_$p(veta,"~",2)
            }
            s:$e(txt,$l(txt))=":" txt = $e(txt,1,$l(txt)-1)
        }
        s vety(znak) = txt
    }
    s znak = ""
    f {
        s znak = $o(vety(znak))
        q:znak=""
        d poznamky.%Push(##class(ALVA.TemplateDesigner.Agendy.Jr.VystupLinkovy).BuildPoznamka(6, "znak", znak, vety(znak)))
    }
}

/// Věta místenkování podle značky a jazyka (gMstNZLi). Česká verze zná jen R a þ.
ClassMethod TextMistenky(znak As %String, rec As %String) As %String
{
    i rec '= "*SK" q $case(znak, "R":"k jízdence je možné zakoupit místenku", $c(254):"spoj je možné použít jen s místenkou", :"")
    q:znak="R" "miestenku s cestovným lístkom možno zakúpiť"
    q:znak=$c(254) "miestenku s cestovným lístkom treba zakúpiť"
    q:znak="&" "miestenku s cestovným lístkom možno zakúpiť v miestenkových kanceláriách AMS"
    q:znak=$c(220) "miestenku s cestovným lístkom treba zakúpiť v miestenkových kanceláriách AMS"
    q ""
}

/// Poznámky druhu 7 - stálé značky: markery matice (| projíždí, < jede jinudy), MHD sloupec,
/// atributy zastávek/trasy viditelné na stránce. Texty z ^JRSET("STALCOME",rec,klíč);
/// klíče dle legacy gStzNZLi konverze (A→auto, L→vlak, x→x…).
ClassMethod PridatStaleZnacky(dataDB As %String, rec As %String, poznamky As %DynamicArray, ByRef markerSeen, ByRef atributyZobrazene, ByRef atributyZastavek, ByRef trasaZnakySeen, maMhd As %Boolean)
{
    k klice
    // markery z časových buněk
    i $d(markerSeen("|")) s klice("|") = ""
    i $d(markerSeen("<")) s klice("<") = ""
    // per-tč značky spojů z tras (W piece 5, 1:1 gStzNZLi spojová sekce)
    s znak = ""
    f {
        s znak = $o(trasaZnakySeen(znak))
        q:znak=""
        s klic = $case(znak, "V":"(", "N":")", "x":"x", "G":"G", "a":"GA", "b":"GB", "c":"GC", :"")
        i klic '= "" s klice(klic) = ""
    }
    // MHD sloupec → text „zastávka s možností přestupu na MHD"
    i maMhd s klice("MHD") = ""
    // atributy trasy (jen významové pro cestujícího; řídicí P/T/S/E/J/o/p se netisknou), převod
    // 1:1 gStzNZLi (`JRapiNZUtl3.mac:467-484`). G, a, b, c, e, f, g, O chyběly do 26. 10. - zastávka
    // značku nesla, legenda ji nevysvětlila (`B194` T14b). Vědomě jen zobrazené zastávky: starý tisk
    // bere celou trasu, takže při „netisknout zastávky bez zastavení" vysvětlí i značku, která na
    // stránce není.
    s znak = ""
    f {
        s znak = $o(atributyZobrazene(znak))
        q:znak=""
        s klic = $case(znak, "A":"auto", "L":"vlak", "x":"x", "y":"y", "z":"z", "V":"(", "N":")", "D":"lod", "C":"CLO",
            "G":"G", "a":"GA", "b":"GB", "c":"GC", "e":"e", "f":"f", "g":"g", "O":"O", :"")
        i klic '= "" s klice(klic) = ""
    }
    // atributy z číselníku zastávek (^JRZ $8, 1:1 gStzNZLi zastávková sekce)
    s znak = ""
    f {
        s znak = $o(atributyZastavek(znak))
        q:znak=""
        s klic = $case(znak, "A":"auto", "L":"vlak", "B":"zbez", "BW":"BWC", "W":"WC", "R":"RZ",
            "ZP":"ZP", "TP":"TP", "PR":"PR", "O":"O", "D":"lod", "e":"e", "f":"f", "g":"g", "x":"x", :"")
        i klic '= "" s klice(klic) = ""
    }
    s klic = ""
    f {
        s klic = $o(klice(klic))
        q:klic=""
        d ..PridatStalouZnacku(dataDB, rec, poznamky, klic)
    }
}

/// Přidá jednu stálou značku s textem ze STALCOME. Klíč textu = legacy klíč (auto/vlak/x/…),
/// zobrazovaný ZNAK = konvertovaný znak (auto→A, vlak→L…, jako legacy Komentar $case).
ClassMethod PridatStalouZnacku(dataDB As %String, rec As %String, poznamky As %DynamicArray, klic As %String)
{
    s txt = $g(^[dataDB]JRSET("STALCOME",rec,klic))
    i txt = "" q
    s znak = $case(klic, "auto":"A", "vlak":"L", "CLO":"C", "(":"V", ")":"N", "lod":"D", "GA":"a", "GB":"b", "GC":"c", :klic)
    d poznamky.%Push(##class(ALVA.TemplateDesigner.Agendy.Jr.VystupLinkovy).BuildPoznamka(7, $s(klic="MHD":"text",1:"znak"), znak, txt))
}

/// Poznámky linky (^JRL(sku,lin,"C",key,idx), klíč 2 se přeskakuje): 1 → druh 8 (tarifní),
/// 3 → druh 9 (všeobecné), 4 → druh 10 (radiobus), jiné → druh 9. Věta se čistí od "$$" (TrNz)
/// a nechává "#" jako oddělovač odstavců (1:1 legacy tvar).
ClassMethod PridatPoznamkyLinky(dataDB As %String, sku As %String, lin As %String, poznamky As %DynamicArray)
{
    s key = ""
    f {
        s key = $o(^[dataDB]JRL(sku,lin,"C",key))
        q:key=""
        i key = 2 continue
        s druh = $case(key, 1:8, 3:9, 4:10, :9)
        s txt = "", idx = ""
        f {
            s idx = $o(^[dataDB]JRL(sku,lin,"C",key,idx))
            q:idx=""
            s txt = txt_$g(^[dataDB]JRL(sku,lin,"C",key,idx))
        }
        s txt = $replace(txt, "$$", "")      // legacy TrNz odstraňuje "$$"
        // prázdné odstavce ("##", koncové "#") legacy tiskne jako mezeru mezi mřížkami
        s upraveny = ""
        f pi=1:1:$l(txt,"#") {
            s cast = $p(txt,"#",pi)
            i (cast = "") && (pi > 1) s cast = " "
            i pi > 1 s upraveny = upraveny_"#"
            s upraveny = upraveny_cast
        }
        s txt = upraveny
        i txt '= "" d poznamky.%Push(##class(ALVA.TemplateDesigner.Agendy.Jr.VystupLinkovy).BuildPoznamka(druh, "text", "", txt))
    }
}

/// Název zastávky podle pořadí (tarifního čísla) v trase linky - do **textů návazností**
/// („spoj 21 vyčká v zastávce …").
///
/// ⚠️ **Bere týž zdroj jako seznam zastávek, ale vždy zkrácený tvar.** Starý modul to tak má:
/// ve sloupci zastávek tiskne plný tvar s dvojčárkou, ve větě poznámky jedinou čárku. Doloženo
/// porovnáním na třech firmách - do 24. 8. tu byl natvrdo CIS název, takže na městských linkách
/// (kde legacy bere upravený název pro MHD) vycházelo v poznámce jiné jméno než ve sloupci.
///
/// Volba „Tvar názvu zastávky" se sem **záměrně nepromítá**: řídí sloupec, kde je tvar vidět,
/// kdežto ve větě by dvojčárka vypadala jako překlep. Kdyby to někdo chtěl jinak, je to featura.
ClassMethod NazevZastavkyPoradi(dataDB As %String, sku As %String, lin As %String, poradi As %String) As %String
{
    s czst = $p($g(^[dataDB]JRL(sku,lin,"J",poradi)),":",1)
    i czst = "" q ""
    q ##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).FormatNazvuZastavky(..ZdrojNazvuZastavky(dataDB, czst), "", "Zkrácený", ..KodJazykaCiselniku(dataDB))
}

/// ═══════════════════════════════════════════════════════════════════════════════
/// Helpery sdílené se ZastavkoveJrAd vzorem (duplikované; extrakce až podle výsledků)
/// ═══════════════════════════════════════════════════════════════════════════════
/// W-mapa `poradi → cas`, cachovaná per `cisloTrasy`. `cas` = číslo | "<" jede jinudy |
/// "|" projíždí | "" mimo trasu (chybějící segment).
ClassMethod WMapa(dataDB As %String, sku As %String, lin As %String, cisloTrasy As %String, ByRef wMap)
{
    k wMap
    i cisloTrasy = "" q
    i $d(^||TiskKnizni("wmap",cisloTrasy)) { m wMap = ^||TiskKnizni("wmap",cisloTrasy)  q }
    s wTrasa = $g(^[dataDB]JRL(sku,lin,"W",cisloTrasy))
    f i=1:1:$l(wTrasa,"#") {
        s seg = $p(wTrasa,"#",i)
        s p = $p(seg,":",1)
        i p '= "" s wMap(p) = $p(seg,":",2)
    }
    i $d(wMap) m ^||TiskKnizni("wmap",cisloTrasy) = wMap
}

/// Věta o pokračování spoje na jinou linku z číselníku firmy (`NCOME` 34, kusy „spoj~pokračuje na
/// lince číslo ~spojem číslo~do zastávky" v jazyce šablony), skládaná jako starý tisk - kusy a čísla
/// odděluje mezera (`JRapiExp32.mac:353-357`). Bez číselníku české znění. Do 26. 10. byla věta napevno
/// česky i ve slovenské šabloně (`B194` T10a).
ClassMethod TextPokracovaniSpoje(dataDB As %String, rec As %String, sp As %String, linka As %String, spoj As %String, zastavka As %String) As %String
{
    s vzor = $g(^[dataDB]JRSET("NCOME",rec,34))
    i $l(vzor,"~") < 4 s vzor = "spoj~pokračuje na lince číslo~spojem číslo~do zastávky"
    q $zstrip($p(vzor,"~",1)_" "_sp_" "_$p(vzor,"~",2)_" "_linka_" "_$p(vzor,"~",3)_" "_spoj_" "_$p(vzor,"~",4)_" "_zastavka, "<=>W")
}


/// Dopravci linky (distinct fa:pr přes oba směry) - plný tvar (knižní JŘ tiskne jako
/// samostatný řádek hlavičky, viz gold PREPRAVCE_NAZEV).
ClassMethod PopisDopravce(dataDB As %String, sku As %String, lin As %String) As %String
{
    k dop
    f smer="T","Z" {
        s sp = ""
        f {
            s sp = $o(^[dataDB]JRL(sku,lin,smer,sp))
            q:sp=""
            i '$isvalidnum(sp) continue
            s carriers = $p($g(^[dataDB]JRL(sku,lin,smer,sp)), "#", 2)
            f ci=1:1:$l(carriers,"~") {
                s slot = $p(carriers,"~",ci)
                i slot = "" continue
                s fa = $p(slot,":",1)
                i fa = "" continue
                s dop(fa, $p(slot,":",2)) = ""
            }
        }
    }
    k seenName
    s txt = "", fa = ""
    f {
        s fa = $o(dop(fa))
        q:fa=""
        s pr = ""
        f {
            s pr = $o(dop(fa,pr))
            q:pr=""
            s nazev = ..NazevDopravce(dataDB, fa, pr)
            i nazev = "" continue
            i $d(seenName(nazev)) continue
            s seenName(nazev) = ""
            i txt '= "" s txt = txt_", "
            s txt = txt_nazev
        }
    }
    q txt
}

/// Text dopravce (fa,pr) z `^JRSET("DOP",...)` - zkrácený MHD název, jinak plný tvar s adresou.
ClassMethod NazevDopravce(dataDB As %String, fa As %String, pr As %String) As %String
{
    i pr '= "" {
        s mhd = $p($g(^[dataDB]JRSET("DOP",fa,pr)),"~",6)
        i $tr(mhd," ","") '= "" q mhd
    }
    s firma = $g(^[dataDB]JRSET("DOP",fa))
    s mhd = $p(firma,"~",14)
    i $tr(mhd," ","") '= "" q mhd

    s jmeno = $p(firma,"~",7)
    i jmeno = "" s jmeno = $p(firma,"~",1)
    s sidlo = "", ulice = "", tel = "", fax = ""
    i pr '= "" {
        s provoz = $g(^[dataDB]JRSET("DOP",fa,pr))
        s sidlo = $p(provoz,"~",1)
        s ulice = $p(provoz,"~",2)
        s tel   = $p(provoz,"~",3)
        s fax   = $p(provoz,"~",5)
    }
    // Skládání 1:1 legacy `JRapiLinPrtWin:2803-2807`: každá část se lepí přes „, ", prázdná se
    // **přeskočí**, telefon má předponu „tel. " a fax „fax ".
    // ⚠️ Do 24. 8. tu bylo `text_","_ulice_",tel:"_tel`, takže u provozu bez ulice vycházelo
    // „…,,tel:386100111" - dvojčárka a jiná zkratka než ve starém tisku. Odhalilo to porovnání
    // hlavičky, které se do té doby nedělalo vůbec.
    s text = jmeno
    i sidlo '= "" s text = text_", "_sidlo
    i ulice '= "" s text = text_", "_ulice
    i tel '= ""   s text = text_", tel. "_tel
    i fax '= ""   s text = text_", fax "_fax
    q text
}

/// Název zastávky z registru (`^JRZ`), ve tvaru, v jakém ho tiskne starý modul.
///
/// **Upravený název pro MHD** (pole 7) použije legacy **jen u městských linek, a jen když to má
/// linka zapnuté** (`gPLSetWin` „nazMHD" × typ linky MEST/MESP) - jinak bere složený název podle
/// registru (pole 2). My jsme do 24. 8. brali název pro MHD **vždycky**, takže se zhruba u poloviny
/// zastávek tisklo jiné jméno než ve starém modulu; rozhodnutí přebírá `NactiNastaveniNazvu`.
/// ⚠️ **Vývěska ho bere vždy** (jako stará vývěska) a je to vědomě rozdílné (Jan 23. 10., `B193i`):
/// název pro MHD často nemá obec („Vsetín,,aut.nádr." → „aut.nádr."), sjednocení podle vývěsky by
/// změnilo názvy na 81-96 % linek CB_CSADAD a KV_DP (`LinkovyJrNastaveniProbe.NazvyProMhdDopad`).
///
/// Samotný tvar (dvojčárka × jedna čárka × okres) řeší sdílené `FormatNazvuZastavky` v `BaseAgenda`,
/// společné s vývěskou - aby se táž zastávka nejmenovala v každé sestavě jinak.
ClassMethod NazevZastavky(dataDB As %String, czst As %String) As %String
{
    i czst = "" q ""
    q ##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).FormatNazvuZastavky(..ZdrojNazvuZastavky(dataDB, czst), $p(..ZaznamZastavky(dataDB, czst), "$", 3), $g(^||TiskKnizni("nazev","tvar")), ..KodJazykaCiselniku(dataDB))
}

/// Záznam zastávky z registru k „platí od" linky, pro kterou se stránka staví (`B188`, časové
/// varianty) - datum nastaví `BuildStrankaLinky`. Mimo sestavu stránky (bez data) platí dnešek
/// jako v legacy `gZst`.
ClassMethod ZaznamZastavky(dataDB As %String, czst As %String) As %String
{
    ret ##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).ZaznamZastavkyKDatu(dataDB, czst, $g(^||TiskKnizni("zastavky","hrl")))
}

/// Kus věty návaznosti z číselníku `^JRSET("NCOME",jazyk,31|32)` (kusy oddělené `~`).
/// Bez záznamu platí česká slova, jak byla v kódu do 23. 9.
/// 31 = spoj~vyčká v zastávce~příjezdu~vlaku:linky:lodě z~nejvýše~minut,
/// 32 = na spoj~navazuje v zastávce~spoj~linky~do~vlak~lodní doprava.
ClassMethod TextNavaznosti(dataDB As %String, omz As %Integer, kus As %Integer) As %String
{
    s vychozi = $s(omz = 31:"spoj~vyčká v zastávce~příjezdu~vlaku:linky:lodě z~nejvýše~minut",
        1:"na spoj~navazuje v zastávce~spoj~linky~do~vlak~lodní doprava")
    s text = $p($g(^[dataDB]JRSET("NCOME", ..KodJazykaCiselniku(dataDB), omz)), "~", kus)
    q $s(text '= "":text, 1:$p(vychozi, "~", kus))
}

/// Označení navazující linky do textu poznámky. U autobusu je to číslo a vede se bez vodicích nul
/// („0330510" → „330510"), u vlaku ale bývá **nečíselné** („R 627") - a to se převodem na číslo
/// scvrklo na nulu, takže v tisku stálo „vyčká příjezdu vlaku 0".
/// ⚠️ Našlo to rozšíření porovnávací sady 24. 8.; na původních čtyřech linkách vlakový přípoj nebyl.
ClassMethod OznaceniLinky(hodnota As %String) As %String
{
    i $isvalidnum(hodnota) q +hodnota
    q $zstrip(hodnota, "<>W")
}

/// Má se řádek zastávky označit jako zvýrazněný?
///
/// Starý modul zvýrazňuje **řádky zvolené zastávky** (`in("A",2,"arr",5)`), a je-li navíc zadané
/// tarifní číslo (`arr,10`), zúží to na ten jediný řádek - 1:1 `JRapiLinPrtWin:1212-1218`.
/// Zastávka se u nás nezadává vnitřním číslem jako v legacy (uživatel ho nezná a v panelu voleb
/// není odkud vybírat), ale **kusem názvu**: linka projíždí jedno město víckrát, takže „Krumlov"
/// označí všechny jeho zastávky - přesně to legacy dělá volbou přes číslo zastávky.
///
/// Samotné tarifní číslo bez zastávky zvýrazní ten řádek taky; legacy to sice takhle nezná
/// (u něj `arr,10` jen zužuje), ale prázdná zastávka + zadané číslo by jinak neudělaly nic,
/// což by z panelu voleb vypadalo jako porucha.
ClassMethod JeZvyraznena(nazevZastavky As %String, poradi As %String) As %Boolean
{
    s hledanaZast = $g(^||TiskKnizni("zvyraznit","zastavka"))
    s hledaneTc = $g(^||TiskKnizni("zvyraznit","tc"))
    // ⚠️ Podmínka za `q:` končí první mezerou, proto se tu rozhoduje běžnými `if` bloky.
    i (hledanaZast = "") && (hledaneTc = "") q 0
    i hledanaZast '= "" {
        i '($zcvt(nazevZastavky,"L") [ hledanaZast) q 0
    }
    i hledaneTc '= "" q (+hledaneTc = +poradi)
    q 1
}

/// Ze kterého pole registru se vezme jméno zastávky - upravený název pro MHD, nebo složený název
/// podle registru. Rozhodnuto jednou za požadavek v `NactiNastaveniNazvu`; **tvar** se řeší až nad
/// tímhle, protože sloupec a text poznámky ho mají každý jiný.
ClassMethod ZdrojNazvuZastavky(dataDB As %String, czst As %String) As %String
{
    i czst = "" q ""
    s rec = ..ZaznamZastavky(dataDB, czst)
    s nazev = ""
    i $g(^||TiskKnizni("nazev","mhd")) s nazev = $p(rec, "$", 7)
    i nazev = "" s nazev = $p(rec, "$", 2)
    q nazev
}

/// Rozhodne **jednou za požadavek**, jak se skládají názvy zastávek, a uloží to do cache.
/// Volba u tisku i vlastnost linky se protahovaly všemi voláními jména, což je nepřehledné.
///
/// „Použít název pro MHD" je **volba nad rámec starého modulu** (má ji i vývěska): legacy tu
/// podmínku nemá jak vypnout. Výchozí zapnuto = chování starého modulu zůstává.
ClassMethod NactiNastaveniNazvu(dataDB As %String, sku As %String, lin As %String)
{
    // ⚠️ Výchozí hodnotu musí doplnit **server**, ne jen deklarace parametru: tisk z jiného
    // klienta (a testy) parametr nepošlou vůbec, a prázdná hodnota znamená ve sdíleném
    // formátování „Zkrácený" - tedy tichý rozchod se starým modulem.
    s tvarNazvu = $g(^||ML("IN","TVAR_NAZVU_ZASTAVKY"))
    i tvarNazvu = "" s tvarNazvu = "Plný CIS"
    s ^||TiskKnizni("nazev","tvar") = tvarNazvu
    s ^||TiskKnizni("zvyraznit","zastavka") = $zcvt($zstrip($g(^||ML("IN","ZVYRAZNIT_ZASTAVKU")), "<>W"), "L")
    s ^||TiskKnizni("zvyraznit","tc") = $zstrip($g(^||ML("IN","ZVYRAZNIT_TC")), "<>W")
    // Typ linky je zakódovaný: surová hodnota / 10 je index do převodní tabulky starého modulu
    // (`TYPLINKYREAD`). Městská linka = MEST (2) nebo MESP (9).
    s surovy = +$p($g(^[dataDB]JRL(sku,lin,"I")), "#", 1)
    s typLinky = $p("0~1~2~3~5~7~6~8~4~9~10~11", "~", (surovy \ 10) + 1)
    s jeMestska = (typLinky = 2) || (typLinky = 9)
    s maNazevMhd = +$g(^[dataDB]JRL(sku,lin,"PATCH","nazMHD"))
    // Tolerantně jako ostatní bool parametry (starší klient posílal „True"); prázdné = zapnuto.
    s dovoleno = ($zcvt($g(^||ML("IN","NAZEV_PRO_MHD")),"L") '= "false")
    s ^||TiskKnizni("nazev","mhd") = jeMestska && maNazevMhd && dovoleno
}

}
