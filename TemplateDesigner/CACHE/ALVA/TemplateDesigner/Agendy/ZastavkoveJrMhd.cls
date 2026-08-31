Include (ALVAMHD, GENERAL)

/// Agenda MHD - zastávkové jízdní řády městské hromadné dopravy.
///
/// Data:
/// - **Skalární pole linky**: číslo (licenční + veřejné), název, platnost od/do,
///   QR kód, typ JŘ (návrh/výlukový/normální), dopravce atd.
/// - **Tabulkové kolekce per stránka**: ZASTAVKY (zastávka + tarifní číslo + čas jízdy + atributy),
///   SKUPINY_DNU (typy provozu - pracovní den/sobota/neděle/...), ODJEZDY (čas + anotace),
///   POZNAMKY (legenda symbolů a omezení).
///
/// Multi-page výstup: jedna stránka = jedna zastávka × jeden směr. Linka má typicky desítky
/// až stovky stránek (např. 50 zastávek × 2 směry = 100 stránek).
///
/// Zdrojová data v Caché:
/// - `^[dataDB]ALVA.MHD.LSO` - linky, spoje, trasy
/// - `^[dataDB]ALVA.MHD.ZST` - zastávky
/// - `^[dataDB]ALVA.MHD.CHR` - chronometráže (doby jízdy)
/// - `^[dataDB]ALVA.MHD.ENUMS` - atributy, typy provozu, poznámky
/// - Helper třídy: `ALVA.MHD.LinkyFce`, `SpojeFce`, `ZastavkyFce`, `KalendarFce`, `ChronometrazeFce`
///
/// Implementace = 6-fázový tiskový engine v procesně-privátním globálu ^||TiskMHD
/// (detail struktury v komentáři u GetData).
///
/// Wire formát výstupu viz `.claude/docs/elementy-architektura.md`.
Class ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd Extends ALVA.TemplateDesigner.Agendy.BaseAgenda
{

Parameter AgendaVariantCode = "ZASTAVKOVY_JR_MHD";

Parameter Agenda = "ZASTAVKOVY_JR";

Parameter AgendaLabel = "Zastávkový JŘ";

Parameter Variant = "MHD";

Parameter VariantLabel = "MHD";

Parameter Released = 1;

Parameter InstanceParamName = "LINKA_ID";

Parameter ElementKind = "STOP";

Parameter InstanceLabel = "linka";

Parameter InstanceLabelPlural = "linky";

/// Vrátí schéma polí MHD agendy.
ClassMethod GetSchema(ByRef sys As %String) As %Status
{
    s def = ##class(%DynamicObject).%New()

    // Standardní JŘ skalární pole
    s fields = ##class(%DynamicArray).%New()
    d ..AddStandardJRFields(fields)
    s def.fields = fields

    // Standardní JŘ kolekce + MHD-specifické
    s collections = ##class(%DynamicArray).%New()
    d ..AddStandardJRCollections(collections)
    s def.fieldCollections = collections

    s ^||ML("OUT", $i(^||ML("OUT"))) = def.%ToJSON()
    ret $$$OK
}

/// Vrátí seznam Fixed šablon tabulek pro MHD.
ClassMethod GetTableTemplates(ByRef sys As %String) As %Status
{
    s arr = ##class(%DynamicArray).%New()

    s t1 = ##class(%DynamicObject).%New()
    s t1.key   = "ZAHLAVI_LINKY"
    s t1.label = "Záhlaví linky"
    d arr.%Push(t1)

    s t2 = ##class(%DynamicObject).%New()
    s t2.key   = "PLATNOST_DOPRAVCE"
    s t2.label = "Platnost a dopravce"
    d arr.%Push(t2)

    s ^||ML("OUT", $i(^||ML("OUT"))) = arr.%ToJSON()
    ret $$$OK
}

/// Vrátí definici Fixed šablony.
ClassMethod GetTableTemplate(ByRef sys As %String) As %Status
{
    s key = $g(^||ML("IN", "key"))

    if key = "ZAHLAVI_LINKY" {
        d ..WriteTemplate2Col([
            ["Linka",       "{CISLO_LINKY}"],
            ["Označení",    "{LOKALNI_OZNACENI_LINKY}"],
            ["Směr",        "{KONECNA_ZASTAVKA}"],
            ["Zastávka",    "{POPIS_ZASTAVKA}"],
            ["Platnost",    "{POPIS_PLATNOSTI}"]
        ])
    }
    elseif key = "PLATNOST_DOPRAVCE" {
        d ..WriteTemplate2Col([
            ["Platnost",  "{POPIS_PLATNOSTI}"],
            ["Dopravce",  "{POPIS_DOPRAVCE}"],
            ["Typ JR",    "{TYP_JR}"]
        ])
    }

    ret $$$OK
}

/// Vrátí parametry sestavy pro MHD.
ClassMethod GetParameters(ByRef sys As %String) As %Status
{
    s arr = ##class(%DynamicArray).%New()

    // Linka se vybírá v Instance Pickeru (ne jako parametr sestavy) - viz GetAvailableInstances.
    // GetData dostává LINKA_ID z pickeru, ne z těchto parametrů.
    d ..AddStandardJRParameters(arr)

    s ^||ML("OUT", $i(^||ML("OUT"))) = arr.%ToJSON()
    ret $$$OK
}

/// Vrátí seznam linek pro Instance Picker.
/// Skupiny linek pro filtr Instance Pickeru - ENUMS "SkupinyLinek" mimo Blocked.
/// OUT["1"] = JSON pole [{ "value": id, "label": název }].
ClassMethod GetAvailableSkupiny(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        s arr = ##class(%DynamicArray).%New()
        s id = ""
        f {
            s id = $o(^[dataDB]ALVA.MHD.ENUMS(IdFa,"SkupinyLinek","DTA",id))
            q:id=""
            i +$g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"SkupinyLinek","DTA",id,"Blocked")) continue

            s nazev = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"SkupinyLinek","DTA",id,"Nazev"))
            s obj = ##class(%DynamicObject).%New()
            d obj.%Set("value", id)
            d obj.%Set("label", $s(nazev'="":nazev, 1:"Skupina "_id))
            d arr.%Push(obj)
        }
        s ^||ML("OUT","1") = arr.%ToJSON()
    }
    catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

/// IN["search"] = volitelný substring filtr (case-insensitive na display = "Cislo - Nazev")
/// IN["limit"]  = max počet řádků (default 50)
/// IN["SKUPINA_LINEK_ID"] = ID skupiny linek (volitelné; default = první nezblokovaná);
///                          v budoucnu bude předáváno z Edisona jako kontext
/// OUT["1"] = JSON pole { "value": LinkaID, "display": "Cislo - Nazev", "subtitle": "Platnost ..." }
ClassMethod GetAvailableInstances(ByRef sys As %String) As %Status
{
    s err = ""   // $$$CatchErrRest čte err; bez init by catch (např. firma bez MHD dat → throw) padl na <UNDEFINED>err
    try {
        $$$FillParams

        s search = $zcvt($g(^||ML("IN","search")), "L")
        s limit  = +$g(^||ML("IN","limit"))
        if 'limit s limit = 50

        // Skupina linek = organizační kontext. V budoucnu předáváno z Edisona;
        // zatím default první nezblokovaná skupina v ENUMS.
        s SkupinaLinekID = +$g(^||ML("IN","SKUPINA_LINEK_ID"))
        if 'SkupinaLinekID s SkupinaLinekID = ..GetDefaultSkupinaLinekID(IdFa, dataDB)

        s arr = ##class(%DynamicArray).%New()
        s count = 0
        // Bez aktivní skupiny linek (typicky firma bez MHD dat, např. čistě AD firma) → prázdný
        // picker, NE chyba: přepnutí na agendu MHD u takové firmy nemá vyhazovat popup. Smyčka níže
        // se SkupinaLinekID=0 nenajde v indexu žádnou linku a vrátí prázdné pole.
        // Iterace přes index ^ALVA.MHD.LSO(IdFa,1,"IDX",1,1,1,SkupinaLinekID,LinkaID) - jen linky skupiny.
        s LinkaID = ""
        for {
            s LinkaID = $o(^[dataDB]ALVA.MHD.LSO(IdFa,1,"IDX",1,1,1,SkupinaLinekID,LinkaID))
            q:LinkaID=""

            // Přeskočit linky bez nastavené trasy - orphan / nedokončené záznamy,
            // GetData by na nich padl na <SUBSCRIPT> v PripravitTrasu.
            s TrasaID = ##class(ALVA.MHD.LinkyFce).GetTrasaId(IdFa, dataDB, LinkaID)
            if TrasaID = "" continue

            s cislo  = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"CisloVerejne"))
            if cislo = "" s cislo = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"CisloLinkyDleLicence"))
            s nazev  = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"NazevLinky"))
            s platOd = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"PlatiOd"))
            s platDo = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"PlatiDo"))

            s display = cislo
            if nazev '= "" {
                if display '= "" s display = display_" - "_nazev
                else  s display = nazev
            }
            if display = "" s display = "Linka "_LinkaID

            // Filtr - substring match case-insensitive na display
            if search '= "" {
                if '$f($zcvt(display, "L"), search) continue
            }

            s subtitle = ""
            if platOd '= "" {
                s subtitle = "Platnost od "_platOd
                if platDo '= "" s subtitle = subtitle_" do "_platDo
            }

            s obj = ##class(%DynamicObject).%New()
            // "string" type - .NET čte přes GetString(); bez toho číselné cislo/LinkaID → JSON number → chyba
            d obj.%Set("value",    LinkaID, "string")
            d obj.%Set("cislo",    cislo, "string")
            d obj.%Set("nazev",    nazev, "string")
            d obj.%Set("display",  display, "string")
            if subtitle '= "" d obj.%Set("subtitle", subtitle, "string")
            d arr.%Push(obj)

            s count = count + 1
            q:count>=limit
        }

        s ^||ML("OUT","1") = arr.%ToJSON()
    } catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

/// ═══════════════════════════════════════════════════════════════════════════════
/// GetData - hlavní metoda pro tisk
/// ═══════════════════════════════════════════════════════════════════════════════
/// Vrátí kompletní data pro tisk zastávkového MHD JŘ.
/// Vstup:  IN["LINKA_ID"] = ID linky
/// Výstup: OUT[1..N] = JSON stránky (jedna stránka = zastávka × směr)
///
/// Každá stránka má strukturu:
///   { "values": { skalární pole }, "rows": { ZASTAVKY: [...], SKUPINY_DNU: [...], ODJEZDY: [...], POZNAMKY: [...] } }
///
/// Pracovní procesně-privátní globál ^||TiskMHD (kilnuto na začátku i konci GetData):
///   ("config","LinkaID")                                       = ID linky
///   ("config","TrasaID")                                       = ID trasy
///   ("config","platnostOdHrl")                                 = platnost od (horolog)
///   ("config","platnostDoHrl")                                 = platnost do (horolog)
///   ("config","PlatnostChronometrazeID")                       = ID platnosti chronometráže
///   ("config","AutoPoznamkyRozdilTrasy")                       = flag: auto-poznámky rozdílů tras
///   ("config","AutoPoznamkyRozdilTrasyAlgoritmus")             = 1001 nebo 1002
///   ("config","AutoPoznamkyRozdilTrasyPismena")                = písmena pro alg. 1001/1002
///   ("config","AutoPoznamkyOdlisnostiPlatnosti")               = flag: auto-poznámky platností
///   ("config","rozsahNZOd")                                    = rozsah NZ značek od
///   ("config","rozsahNZDo")                                    = rozsah NZ značek do
///   ("config","CisloLinky")                                    = číslo linky dle licence
///   ("config","LokalniOznaceni")                               = veřejné číslo linky
///   ("config","NazevLinky")                                    = název linky
///   ("config","QRKod")                                         = QR kód URL
///   ("config","TypJR")                                         = typ JŘ (NÁVRH/VÝLUKOVÝ/"")
///   ("trasa",SmerID,BodID)                                     = $lb(ZastavkaID, CasJizdy, ZastavkaCislo)
///   ("trasa",SmerID,"slouceni",BodID,BodID2)                   = ""   konverzní tabulka sloučených bodů
///   ("trasa",SmerID,"slouceniInv",BodID2)                      = BodID hlavní bod po sloučení
///   ("trasa",SmerID,"netisknout",BodID)                        = ""   bod s příznakem „netisknout na zastávkovém JŘ"
///   ("poznamkyZast",SmerID,BodID,Znak)                         = $lb(TextPoznamky, TypPoznamky)
///   ("atributyZast",SmerID,BodID,AtributID)                    = $lb(Popis, Typ, SpecialniZnak)
///   ("odjezdy",SmerID,BodID,SmoSloupec,Hodina,Minuta,SpojID)   = ""  (SmoSloupec 1-4 = sekce minut odjezdu dle SMOID)
///   ("smoSloupce",SmoSloupec)                                  = ""  (registr tiskových sloupců)
///   ("smoNadpis",SmoSloupec)                                   = $lb(nadpis1, nadpis2)
///   ("smoTypy",SmoSloupec,TypProvozuID)                        = počet spojů typu ve sloupci (detekce sloučení)
///   ("smoZnacka",TypProvozuID)                                 = $lb(znak, text)  značka typu mimo dominantní režim sloupce
///   ("spojTyp",SpojID)                                         = TypProvozuID spoje
///   ("spojTrasa",SmerID,SpojID)                                = TrasaBitmap
///   ("spojTrasa",SmerID,SpojID,BodID)                          = TrasaBitmapZBodu
///   ("spojNZ",SpojID)                                          = NegativniZnacka
///   ("nzTexty",NZ)                                             = OmezeniTXT
///   ("spojTydenni",SpojID)                                     = tydenniPlatnost
///   ("spojPoznamky",SpojID,...)                                = poznámky spoje
///   ("spojAtributy",SpojID,...)                                = atributy spoje
///   ("obsluha",SmerID,BodID)                                   = TrasaBitmap obsluhovaných zastávek z bodu
///   ("obsluhaVse",SmerID,BodID)                                = AllStopTrasaBitmap (1=všechny, 9=jen některé)
///   ("obsluhaKonec",SmerID,BodID,SpojID)                       = konečný bod spoje
///   ("rozdilTrasy",SmerID,BodID,Typ,Bitmap,SpojID)             = ""
///   ("rozdilTrasyPism",SmerID,BodID,Typ,Bitmap)                = písmeno
ClassMethod GetData(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        s LinkaID = $g(^||ML("IN","LINKA_ID"))
        i 'LinkaID $$$ThrowStatus($$$ERROR($$$GeneralError, "LINKA_ID is required"))

        k ^||TiskMHD

        // ── Příprava konfigurace ────────────────────────────────────────
        s TrasaID = ##class(ALVA.MHD.LinkyFce).GetTrasaId(IdFa, dataDB, LinkaID)
        s platnostOd = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"PlatiOd"))
        s platnostDo = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"PlatiDo"))
        s VariantaChronometrazeID = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"VariantaChronometrazeID"))
        s platnostOdHrl = $zdh(platnostOd, 3)
        s platnostDoHrl = $zdh(platnostDo, 3)
        s PlatnostChronometrazeID = ##class(ALVA.MHD.ChronometrazeFce).GetPlatnostChronometrazeID(IdFa, dataDB, VariantaChronometrazeID, platnostOdHrl)

        // Linka bez nastavené trasy - fáze 1 by padla na <SUBSCRIPT>. Vyhodit srozumitelnou chybu.
        if TrasaID = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "Linka "_LinkaID_" nemá nastavenou trasu - nelze tisknout"))

        // Nastavení auto-poznámkování
        s AutoPoznamkyRozdilTrasy = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"AutomatickyPoznamkovatRozdilyVTraseSpoju"))
        s AutoPoznamkyRozdilTrasyAlgoritmus = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"AutomatickyPoznamkovatRozdilyVTraseSpojuAlgoritmus"))
        s AutoPoznamkyRozdilTrasyPismena = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"AutomatickyPoznamkovatRozdilyVTraseSpojuPismena"))
        i 'AutoPoznamkyRozdilTrasyAlgoritmus s AutoPoznamkyRozdilTrasyAlgoritmus = 1001
        i AutoPoznamkyRozdilTrasyPismena = "" s AutoPoznamkyRozdilTrasyPismena = "abcdefghijklmnopqrstuvwxyz"
        s AutoPoznamkyOdlisnostiPlatnosti = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"AutomatickyPoznamkovatOdlisnostiVPlatnostechSpoju"))

        // NZ rozsah
        s rozsahNZOd = $g(^[dataDB]ALVA.MHD.SETTINGS(IdFa,"Obecne","DTA",1001,"RozsahNegativnichZnacekOd"))
        s rozsahNZDo = $g(^[dataDB]ALVA.MHD.SETTINGS(IdFa,"Obecne","DTA",1001,"RozsahNegativnichZnacekDo"))

        // Skalární info o lince (pro výstup)
        s CisloLinky = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"CisloLinkyDleLicence"))
        s LokalniOznaceni = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"CisloVerejne"))
        s NazevLinky = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"NazevLinky"))
        s QRKod = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"QRCode"))
        s TypJR = ""
        i +$g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"Navrh")) s TypJR = "NÁVRH"
        i +$g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"Vyluka")) s TypJR = "VÝLUKOVÝ"

        // Uložit konfiguraci do working globálu - helper metody čtou odtud
        s ^||TiskMHD("config","LinkaID") = LinkaID
        s ^||TiskMHD("config","TrasaID") = TrasaID
        s ^||TiskMHD("config","platnostOdHrl") = platnostOdHrl
        s ^||TiskMHD("config","platnostDoHrl") = platnostDoHrl
        s ^||TiskMHD("config","PlatnostChronometrazeID") = PlatnostChronometrazeID
        s ^||TiskMHD("config","AutoPoznamkyRozdilTrasy") = AutoPoznamkyRozdilTrasy
        s ^||TiskMHD("config","AutoPoznamkyRozdilTrasyAlgoritmus") = AutoPoznamkyRozdilTrasyAlgoritmus
        s ^||TiskMHD("config","AutoPoznamkyRozdilTrasyPismena") = AutoPoznamkyRozdilTrasyPismena
        s ^||TiskMHD("config","AutoPoznamkyOdlisnostiPlatnosti") = AutoPoznamkyOdlisnostiPlatnosti
        s ^||TiskMHD("config","rozsahNZOd") = rozsahNZOd
        s ^||TiskMHD("config","rozsahNZDo") = rozsahNZDo
        s ^||TiskMHD("config","CisloLinky") = CisloLinky
        s ^||TiskMHD("config","LokalniOznaceni") = LokalniOznaceni
        s ^||TiskMHD("config","NazevLinky") = NazevLinky
        s ^||TiskMHD("config","QRKod") = QRKod
        s ^||TiskMHD("config","TypJR") = TypJR

        // ═══════════════════════════════════════════════════════════════════
        // FÁZE 1: Příprava trasy pro oba směry
        // ═══════════════════════════════════════════════════════════════════
        d ..PripravitTrasu(.sys)

        // ═══════════════════════════════════════════════════════════════════
        // FÁZE 2: Poznámky a atributy zastávek (z trasy + číselníku)
        // ═══════════════════════════════════════════════════════════════════
        d ..NacistPoznamkyAAtributyZastavek(.sys)

        // ═══════════════════════════════════════════════════════════════════
        // FÁZE 3: Auto-poznámkování rozdílů v platnostech (NZ)
        // ═══════════════════════════════════════════════════════════════════
        i AutoPoznamkyOdlisnostiPlatnosti {
            d ..AutoPoznamkovatPlatnosti(.sys)
        }

        // ═══════════════════════════════════════════════════════════════════
        // FÁZE 4: Promítnutí spojů do zastávek
        // ═══════════════════════════════════════════════════════════════════
        d ..PromitnutSpoje(.sys)

        // ═══════════════════════════════════════════════════════════════════
        // FÁZE 4b: Značky sloučených skupin dnů (víc typů provozu v 1 sloupci)
        // ═══════════════════════════════════════════════════════════════════
        d ..PriraditZnackySmoSkupin(.sys)

        // ═══════════════════════════════════════════════════════════════════
        // FÁZE 5: Bitmapy obsluhovaných zastávek + rozdíly tras
        // ═══════════════════════════════════════════════════════════════════
        d ..VypocitatBitmapyObsluhy()

        // ═══════════════════════════════════════════════════════════════════
        // FÁZE 6: Sestavení JSON výstupu - pole stránek
        // ═══════════════════════════════════════════════════════════════════
        d ..SestavitStranky(.sys)
    }
    catch (ex) { $$$CatchErrRest }
    k ^||TiskMHD                       // úklid working global i po chybě (self-healing kill je na začátku)
    ret $$$OK
}

/// ═══════════════════════════════════════════════════════════════════════════════
/// Fáze 1: Příprava trasy
/// ═══════════════════════════════════════════════════════════════════════════════
/// Načte body trasy pro oba směry, chronometráže, sloučí totožné zastávky.
/// Výstup: ^||TiskMHD("trasa",SmerID,BodID) = $lb(ZastavkaID, CasJizdy, ZastavkaCislo)
ClassMethod PripravitTrasu(ByRef sys As %String)
{
    $$$FillParams
    s TrasaID = $g(^||TiskMHD("config","TrasaID"))
    s PlatnostChronometrazeID = $g(^||TiskMHD("config","PlatnostChronometrazeID"))

    f SmerID = 0,1 {
        s smerKrok = 1 i SmerID s smerKrok = -1
        s predchoziZastavkaID = 0, kumulativniCas = 0

        s BodID = ""
        f {
            s BodID = $o(^[dataDB]ALVA.MHD.LSO(IdFa,4,"DTA",TrasaID,"DTA",1,"ModelTrasaBodDetail",BodID), smerKrok) q:BodID=""

            // ZastavkaID podle směru
            s klic = $s(SmerID=0:"ZastavkaTamID", 1:"ZastavkaZpetID")
            s ZastavkaID = $g(^[dataDB]ALVA.MHD.LSO(IdFa,4,"DTA",TrasaID,"DTA",1,"ModelTrasaBodDetail",BodID,"Val",klic))
            i 'ZastavkaID continue

            // Chronometráž (čas jízdy z předchozí zastávky)
            s casUseku = 0
            i predchoziZastavkaID && (PlatnostChronometrazeID '= "") {
                s ChronometrazID = $g(^[dataDB]ALVA.MHD.CHR(IdFa,2,"IDX",1,1,1,PlatnostChronometrazeID,predchoziZastavkaID,ZastavkaID))
                i ChronometrazID {
                    s casUseku = $g(^[dataDB]ALVA.MHD.CHR(IdFa,2,"DTA",ChronometrazID,"DTA",1,"ZakladniDobaJizdy"))
                }
            }
            s predchoziZastavkaID = ZastavkaID
            s kumulativniCas = kumulativniCas + casUseku

            // Ručně zadaný čas má přednost
            s klicCas = $s(SmerID=0:"JizdniDobaTam", 1:"JizdniDobaz5")
            s rucniCas = $g(^[dataDB]ALVA.MHD.LSO(IdFa,4,"DTA",TrasaID,"DTA",1,"ModelTrasaBodDetail",BodID,"Val",klicCas))
            i rucniCas '= "" s kumulativniCas = rucniCas

            // Filtr: jen skutečné zastávky (KategorieZastavekID=1001)
            s KategorieZastavekID = $g(^[dataDB]ALVA.MHD.ZST(IdFa,1,"DTA",ZastavkaID,"DTA",1,"KategorieZastavekID"))
            i KategorieZastavekID '= 1001 continue

            // Per-bod příznak „netisknout na zastávkovém JŘ" (default tisknout) - KV_DP jím skrývá
            // variantní větve trasy; legacy „MH pro KV_DP" (LinkyPrintFormatMHD:713-720)
            s klicTisk = $s(SmerID=0:"TisknoutZastavkovyTam", 1:"TisknoutZastavkovyZpet")
            i '$g(^[dataDB]ALVA.MHD.LSO(IdFa,4,"DTA",TrasaID,"DTA",1,"ModelTrasaBodDetail",BodID,"Val",klicTisk),1) {
                s ^||TiskMHD("trasa",SmerID,"netisknout",BodID) = ""
            }

            s ZastavkaCislo = $g(^[dataDB]ALVA.MHD.ZST(IdFa,1,"DTA",ZastavkaID,"DTA",1,"Cislo"))
            s ^||TiskMHD("trasa",SmerID,BodID) = $lb(ZastavkaID, kumulativniCas, ZastavkaCislo)
            s ^||TiskMHD("trasa",SmerID,"slouceni",BodID,BodID) = ""
        }

        // Sloučení totožných po sobě jdoucích zastávek
        d ..SloucitTotozneZastavky(SmerID)
    }
}

/// Sloučí zastávky se stejným ZastavkaCislo jdoucí za sebou.
/// Aktualizuje "slouceni" a "slouceniInv" v pracovním globálu.
ClassMethod SloucitTotozneZastavky(SmerID As %Integer)
{
    s smerKrok = 1 i SmerID s smerKrok = -1

    s BodID = ""
    f {
        s val = "" s BodID = $o(^||TiskMHD("trasa",SmerID,BodID), -smerKrok, val) q:BodID=""
        i 'BodID continue ; skip "slouceni"/"slouceniInv" a ostatní ne-numerické pomocné subscripty
        s dalsiBodID = BodID
        s pokracovat = 0

        f {
            s dalsiVal = "" s dalsiBodID = $o(^||TiskMHD("trasa",SmerID,dalsiBodID), -smerKrok, dalsiVal) i (dalsiBodID = "") || (pokracovat) q
            i 'dalsiBodID continue ; skip ne-numerické pomocné subscripty
            s pokracovat = 1

            // Stejné číslo zastávky → sloučit
            i $lg(val,3) = $lg(dalsiVal,3) {
                s ^||TiskMHD("trasa",SmerID,"slouceni",BodID,dalsiBodID) = ""
                s ^||TiskMHD("trasa",SmerID,"slouceniInv",dalsiBodID) = BodID
                k ^||TiskMHD("trasa",SmerID,"slouceni",dalsiBodID)
                k ^||TiskMHD("trasa",SmerID,dalsiBodID)
                s pokracovat = 0
            }
        }
    }
}

/// ═══════════════════════════════════════════════════════════════════════════════
/// Fáze 2: Poznámky a atributy zastávek
/// ═══════════════════════════════════════════════════════════════════════════════
/// Načte poznámky a atributy pro každou zastávku z číselníku a z trasy.
ClassMethod NacistPoznamkyAAtributyZastavek(ByRef sys As %String)
{
    $$$FillParams
    s TrasaID = $g(^||TiskMHD("config","TrasaID"))

    f SmerID = 0,1 {
        s BodID = ""
        f {
            s val = "" s BodID = $o(^||TiskMHD("trasa",SmerID,BodID), 1, val) q:BodID=""
            i 'BodID continue ; skip ne-numerické pomocné subscripty ("slouceni", "slouceniInv")
            s ZastavkaID = $lg(val, 1)
            i ZastavkaID = "" continue

            // Atributy zastávky z číselníku
            s atributCnt = ""
            f {
                s atributCnt = $o(^[dataDB]ALVA.MHD.ZST(IdFa,1,"DTA",ZastavkaID,"DTA",1,"Atributy",atributCnt)) q:atributCnt=""
                s AtributID = $g(^[dataDB]ALVA.MHD.ZST(IdFa,1,"DTA",ZastavkaID,"DTA",1,"Atributy",atributCnt,"Itm"))
                i AtributID = "" continue
                s Popis = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"Atribut","DTA",AtributID,"Popis"))
                s SpecialniZnak = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"Atribut","DTA",AtributID,"SpecZnak"))
                i SpecialniZnak = "" continue
                s ^||TiskMHD("atributyZast",SmerID,BodID,AtributID) = $lb(Popis, 2, SpecialniZnak)
            }

            // Poznámky a atributy z trasy linky (per sloučený bod)
            s smerKlic = $s(SmerID=0:"Tam", 1:"Zpet")
            s subBodID = ""
            f {
                s subBodID = $o(^||TiskMHD("trasa",SmerID,"slouceni",BodID,subBodID)) q:subBodID=""

                // Poznámky
                s poznamkaCnt = ""
                f {
                    s poznamkaCnt = $o(^[dataDB]ALVA.MHD.LSO(IdFa,4,"DTA",TrasaID,"DTA",1,"ModelTrasaBodDetail",subBodID,"Val","Poznamky"_smerKlic,poznamkaCnt)) q:poznamkaCnt=""
                    s PoznamkaSpojID = $g(^[dataDB]ALVA.MHD.LSO(IdFa,4,"DTA",TrasaID,"DTA",1,"ModelTrasaBodDetail",subBodID,"Val","Poznamky"_smerKlic,poznamkaCnt,"Itm"))
                    // Filtr: jen poznámky určené pro tisk zastávkového JŘ
                    i '$g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"PoznamkaSpoj","DTA",PoznamkaSpojID,"TiskJRMHDZastavkove")) continue
                    s pouzeZnacku = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"PoznamkaSpoj","DTA",PoznamkaSpojID,"TiskJRMHDZastavkovePouzeZnacku"))
                    s TypPoznamkyID = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"PoznamkaSpoj","DTA",PoznamkaSpojID,"TypPoznamkyID"))
                    s Text = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"PoznamkaSpoj","DTA",PoznamkaSpojID,"Text"))
                    s PopisPoznamky = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"PoznamkaSpoj","DTA",PoznamkaSpojID,"PopisPoznamky"))
                    i pouzeZnacku s PopisPoznamky = ""
                    i Text = "" continue
                    // Barvy (volitelné - sdíleno s vozovým JŘ; prázdné → default rendering)
                    s pForeColor = ..RgbToHex($g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"PoznamkaSpoj","DTA",PoznamkaSpojID,"TiskJRMHDVozoveBarvaZnakuRGB")))
                    s pBackColor = ..RgbToHex($g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"PoznamkaSpoj","DTA",PoznamkaSpojID,"TiskJRMHDVozoveBarvaPozadiRGB")))
                    // Textová poznámka (TypPoznamkyID=0)
                    i 'TypPoznamkyID {
                        s ^||TiskMHD("poznamkyZast",SmerID,BodID,Text) = $lb(PopisPoznamky, 0, pForeColor, pBackColor)
                    }
                }

                // Atributy z trasy
                s atributCnt = ""
                f {
                    s atributCnt = $o(^[dataDB]ALVA.MHD.LSO(IdFa,4,"DTA",TrasaID,"DTA",1,"ModelTrasaBodDetail",subBodID,"Val","Atributy"_smerKlic,atributCnt)) q:atributCnt=""
                    s AtributID = $g(^[dataDB]ALVA.MHD.LSO(IdFa,4,"DTA",TrasaID,"DTA",1,"ModelTrasaBodDetail",subBodID,"Val","Atributy"_smerKlic,atributCnt,"Itm"))
                    i AtributID = "" continue
                    s Popis = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"Atribut","DTA",AtributID,"Popis"))
                    s SpecialniZnak = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"Atribut","DTA",AtributID,"SpecZnak"))
                    i SpecialniZnak = "" continue
                    s ^||TiskMHD("atributyZast",SmerID,BodID,AtributID) = $lb(Popis, 2, SpecialniZnak)
                }
            }
        }
    }
}

/// ═══════════════════════════════════════════════════════════════════════════════
/// Fáze 3: Auto-poznámkování platností
/// ═══════════════════════════════════════════════════════════════════════════════
/// Porovná bitmapu platnosti spoje s bitmapou typu provozu.
/// Vrátí 1 pokud se liší (= spoj má omezenou platnost), 0 pokud jsou stejné nebo při chybě.
/// Output parametry: tydenniPlatnost (text dnů) a OmezeniTXT (popis omezení).
ClassMethod PorovnatBitmapyPlatnosti(ByRef sys As %String, SpojID As %String, TypProvozuID As %String, Output tydenniPlatnost As %String, Output OmezeniTXT As %String) As %Boolean
{
    $$$FillParams
    s tydenniPlatnost = "", OmezeniTXT = ""

    i '$g(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",SpojID,"DTA",1,"RucniZadani")) q 0

    s ProvozBitMapa = $g(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",SpojID,"DTA",1,"ProvozBitMapa"))
    s bitmapSpoje = $p(ProvozBitMapa,"~",3)
    k arr s isok = ##class(ALVA.MHD.KalendarFce).GetJede(IdFa,dataDB,$p(ProvozBitMapa,"~",1),$p(ProvozBitMapa,"~",2),TypProvozuID,"","",.arr)
    i 'isok q 0
    s bitmapTypuProvozu = $lg($g(arr("dta",1)),3)
    k arr

    i bitmapTypuProvozu = bitmapSpoje q 0

    // Bitmapy se liší → generovat rozšířený rozpis
    k arri, arr
    s arri("dta",1) = $p(ProvozBitMapa,"~",1)
    s arri("dta",2) = $p(ProvozBitMapa,"~",2)
    s arri("dta",3) = bitmapSpoje
    s arri("dta",4) = bitmapTypuProvozu
    s isok = ##class(ALVA.MHD.KalendarFce).MakeRozpisTplProTypProvozuID(IdFa,dataDB,TypProvozuID,.arri)
    i 'isok q 0

    s tydenniPlatnost = $g(arr("dta",1))

    k arri m arri = arr s arri("par",1) = 0 k arr
    s isok = ##class(ALVA.MHD.KalendarFce).MakeOmezeniTXT(IdFa,dataDB,.arri,.arr)
    i 'isok q 0

    s OmezeniTXT = $g(arr("dta",1))
    q (OmezeniTXT '= "")
}

/// Automatické přidělení NZ (negativních značek) spojům s omezenou platností.
/// Porovnává bitmapu platnosti spoje s bitmapou typu provozu.
ClassMethod AutoPoznamkovatPlatnosti(ByRef sys As %String)
{
    $$$FillParams
    s LinkaID = $g(^||TiskMHD("config","LinkaID"))

    s SpojID = ""
    f {
        s SpojID = $o(^[dataDB]ALVA.MHD.LSO(IdFa,2,"IDX",1,1,1,LinkaID,SpojID)) q:SpojID=""
        s NegativniZnacka = $g(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",SpojID,"DTA",1,"NegativniZnacka"))
        i NegativniZnacka = "" continue
        s TypProvozuID = $g(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",SpojID,"DTA",1,"TypProvozuID"))

        s tydenniPlatnost = "", OmezeniTXT = ""
        i '..PorovnatBitmapyPlatnosti(.sys, SpojID, TypProvozuID, .tydenniPlatnost, .OmezeniTXT) continue

        s ^||TiskMHD("nzTexty",NegativniZnacka) = OmezeniTXT
    }
}

/// Mapuje typ provozu → sekce minut odjezdu = tiskový sloupec (1-4) přes číselníkové pole SMOID
/// ("SMO1".."SMO4"). Legacy (LinkyPrintFormatMHD) takto slučuje mnoho jemných typů provozu do ≤4
/// sloupců mřížky; typ bez SMO1-4 se do vývěsky netiskne → "". Vývěska tak má pár sloupců, ne desítky.
ClassMethod SmoSloupec(dataDB As %String, IdFa As %String, TypProvozuID As %String) As %String
{
    q $case($g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"TypyProvozu","DTA",TypProvozuID,"SMOID")), "SMO1":1, "SMO2":2, "SMO3":3, "SMO4":4, :"")
}

/// Značky sloučených skupin dnů: víc typů provozu v jednom SMO sloupci (kolize v číselníku -
/// legacy ji tiskla ztrátovým přepisem) → nadpis sloupce dostane dominantní typ (nejvíc spojů)
/// a každý ostatní typ automatickou značku + legendu („jede jen 31.12."). Značka jde stejným NZ
/// kanálem jako ruční negativní značky; ruční NegativniZnacka typu z číselníku má přednost.
/// Sloupec s jediným typem = beze změny chování (gold parita CB_DP).
ClassMethod PriraditZnackySmoSkupin(ByRef sys As %String)
{
    $$$FillParams
    s rozsahNZOd = $g(^||TiskMHD("config","rozsahNZOd"))
    // Fallback mimo denní značky 1-7, kdyby firma neměla rozsah NZ nastavený
    i rozsahNZOd = "" s rozsahNZOd = 10

    s smoCol = ""
    f {
        s smoCol = $o(^||TiskMHD("smoTypy",smoCol)) q:smoCol=""

        s dominant = "", maxSpoju = -1, pocetTypu = 0
        s TypProvozuID = ""
        f {
            s TypProvozuID = $o(^||TiskMHD("smoTypy",smoCol,TypProvozuID)) q:TypProvozuID=""
            s pocetTypu = pocetTypu + 1
            i ^||TiskMHD("smoTypy",smoCol,TypProvozuID) > maxSpoju {
                s maxSpoju = ^||TiskMHD("smoTypy",smoCol,TypProvozuID)
                s dominant = TypProvozuID
            }
        }
        i pocetTypu < 2 continue

        s TypProvozuID = "", seznamOstatnich = ""
        f {
            s TypProvozuID = $o(^||TiskMHD("smoTypy",smoCol,TypProvozuID)) q:TypProvozuID=""
            i TypProvozuID = dominant continue

            s nadpis1 = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"TypyProvozu","DTA",TypProvozuID,"NadpisSMOPrvni"))
            s nadpis2 = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"TypyProvozu","DTA",TypProvozuID,"NadpisSMODruhy"))
            s popis = nadpis1
            i nadpis2 '= "" s popis = $s(popis="":nadpis2, 1:popis_" "_nadpis2)
            i popis = "" s popis = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"TypyProvozu","DTA",TypProvozuID,"Nazev"))
            s seznamOstatnich = $s(seznamOstatnich="":popis, 1:seznamOstatnich_", "_popis)

            i $d(^||TiskMHD("smoZnacka",TypProvozuID)) continue
            s text = "jede jen "_popis

            // Ruční značka typu z číselníku má přednost; jinak první volný znak z NZ řady
            s znak = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"TypyProvozu","DTA",TypProvozuID,"NegativniZnacka"))
            i znak = "" {
                f znak = rozsahNZOd:1 {
                    i $d(^||TiskMHD("nzTexty",znak)) continue
                    i $d(^||TiskMHD("nzPrirazen",znak)) continue
                    s obsazena = 0
                    s typIter = ""
                    f {
                        s typIter = $o(^[dataDB]ALVA.MHD.ENUMS(IdFa,"TypyProvozu","DTA",typIter)) q:typIter=""
                        i $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"TypyProvozu","DTA",typIter,"NegativniZnacka")) = znak s obsazena = 1
                    }
                    i 'obsazena q
                }
            }
            s ^||TiskMHD("nzPrirazen",znak) = text
            s ^||TiskMHD("smoZnacka",TypProvozuID) = $lb(znak, text)
        }

        // Nadpis sloučeného sloupce: 1. řádek = dominantní typ, 2. řádek = výčet ostatních
        // („24.12., 25.12. a 1.1., 31.12.") - čtenář vidí, co sloupec pokrývá; per šablona
        // přepsatelné přes override nadpisu v dialogu sloupců
        s domNadpis1 = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"TypyProvozu","DTA",dominant,"NadpisSMOPrvni"))
        s domNadpis2 = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"TypyProvozu","DTA",dominant,"NadpisSMODruhy"))
        s domPopis = domNadpis1
        i domNadpis2 '= "" s domPopis = $s(domPopis="":domNadpis2, 1:domPopis_" "_domNadpis2)
        s ^||TiskMHD("smoNadpis",smoCol) = $lb(domPopis, seznamOstatnich)
    }
}

/// ═══════════════════════════════════════════════════════════════════════════════
/// Fáze 4: Promítnutí spojů
/// ═══════════════════════════════════════════════════════════════════════════════
/// Iteruje všechny spoje linky, přiřadí odjezdové časy do zastávek,
/// načte poznámky a atributy spoje, zpracuje NZ.
ClassMethod PromitnutSpoje(ByRef sys As %String)
{
    $$$FillParams
    s LinkaID = $g(^||TiskMHD("config","LinkaID"))
    s AutoPoznamkyOdlisnostiPlatnosti = $g(^||TiskMHD("config","AutoPoznamkyOdlisnostiPlatnosti"))

    s SpojID = ""
    f {
        s SpojID = $o(^[dataDB]ALVA.MHD.LSO(IdFa,2,"IDX",1,1,1,LinkaID,SpojID)) q:SpojID=""
        s SmerID = $g(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",SpojID,"DTA",1,"SmerID"))
        s TypProvozuID = $g(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",SpojID,"DTA",1,"TypProvozuID"))
        // Spoj bez skupiny dnů / směru nelze zařadit do mřížky - přeskoč. Prázdný subscript
        // v ^|| working globálu = <SUBSCRIPT> a shodil by tisk celé linky.
        i (TypProvozuID="")||(SmerID="") continue
        // Sekce minut odjezdu = tiskový sloupec (1-4) dle SMOID číselníku typů provozu. Legacy takto
        // slučuje mnoho jemných typů do ≤4 sloupců (LinkyPrintFormatMHD); typ bez SMO1-4 se netiskne.
        s smoCol = ..SmoSloupec(dataDB, IdFa, TypProvozuID)
        i smoCol '= "" {
            s ^||TiskMHD("smoSloupce",smoCol) = ""
            s ^||TiskMHD("smoNadpis",smoCol) = $lb($g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"TypyProvozu","DTA",TypProvozuID,"NadpisSMOPrvni")),$g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"TypyProvozu","DTA",TypProvozuID,"NadpisSMODruhy")))
            s ^||TiskMHD("smoTypy",smoCol,TypProvozuID) = $g(^||TiskMHD("smoTypy",smoCol,TypProvozuID)) + 1
            s ^||TiskMHD("spojTyp",SpojID) = TypProvozuID
        }

        s smerKrok = 1 i SmerID s smerKrok = -1

        // Rozložení spoje do tabulky zastávek
        k tabulkaSpoje
        s isok = ##class(ALVA.MHD.SpojeFce).MakeTableSpoj(.sys,SpojID,"",.tabulkaSpoje)
        i 'isok continue

        // Konečný bod spoje
        s konecnyBod = $o(tabulkaSpoje("arr",1,""),-1)
        i SmerID s konecnyBod = $o(tabulkaSpoje("arr",1,""))

        // Promítnout časy do zastávek
        s TrasaBitmap = ""
        s BodID = ""
        f {
            s BodID = $o(^||TiskMHD("trasa",SmerID,BodID)) q:BodID=""
            i 'BodID continue ; skip ne-numerické pomocné subscripty
            s jeKonecny = (BodID = konecnyBod)

            // Najít čas spoje v tomto bodu (včetně sloučených)
            s casOdjezdu = ""
            s subBodID = ""
            f {
                s subBodID = $o(^||TiskMHD("trasa",SmerID,"slouceni",BodID,subBodID), smerKrok) q:subBodID=""
                i subBodID = konecnyBod s jeKonecny = 1
                i '$d(tabulkaSpoje("arr",1,subBodID)) continue
                s bodVal = $g(tabulkaSpoje("arr",1,subBodID))
                // Průjezd → přeskočit
                i $lg(bodVal,5) continue
                // Čas = max(příjezd, odjezd)
                s cas = $lg(bodVal,3) i $lg(bodVal,4) > cas s cas = $lg(bodVal,4)
                i cas > casOdjezdu s casOdjezdu = cas
            }

            i casOdjezdu '= "" {
                s $e(TrasaBitmap,BodID) = 1
                i 'jeKonecny {
                    s hh = casOdjezdu \ 60, mm = casOdjezdu # 60
                    i smoCol '= "" s ^||TiskMHD("odjezdy",SmerID,BodID,smoCol,hh,mm,SpojID) = ""
                    s ^||TiskMHD("obsluhaKonec",SmerID,BodID,SpojID) = ""
                }
            }
        }
        s ^||TiskMHD("spojTrasa",SmerID,SpojID) = TrasaBitmap

        // Poznámky spoje
        k myarr s isok = ##class(ALVA.MHD.SpojeFce).GetPoznamkySpoj(.sys,SpojID,"",.myarr)
        i 'isok continue
        // Pro auto-poznámkování platností odstranit spec.znaky a NZ
        i AutoPoznamkyOdlisnostiPlatnosti {
            s poznamkaID = ""
            f {
                s poznamkaID = $o(myarr("arr",1,poznamkaID)) q:poznamkaID=""
                s typPozn = $g(myarr("arr",1,poznamkaID,"TypPoznamkyID"))
                i (typPozn = 1) || (typPozn = 2) k myarr("arr",1,poznamkaID)
            }
        }
        k ^||TiskMHD("spojPoznamky",SpojID) m ^||TiskMHD("spojPoznamky",SpojID) = myarr

        // Atributy spoje
        k myarr s isok = ##class(ALVA.MHD.SpojeFce).GetAtributySpoj(.sys,SpojID,"",.myarr)
        i 'isok continue
        k ^||TiskMHD("spojAtributy",SpojID) m ^||TiskMHD("spojAtributy",SpojID) = myarr

        // NZ přiřazení
        d ..PriraditNZSpoji(.sys, SpojID, TypProvozuID)
    }
}

/// Rozsah platnosti ruční poznámky spoje „od bodu – do bodu" (TiskJRMHDZastavkoveBodOd/Do):
/// poznámka (spec. znak i text) se tiskne jen na stránkách zastávek v rozsahu; 0/prázdné = bez
/// omezení. Zpětný směr replikuje legacy podmínky 1:1 (LinkyPrintFormatMHD:1047-1051) včetně
/// jejich brány přes BodDo a obráceného porovnání.
ClassMethod PoznamkaSpojeVRozsahu(SmerID As %Integer, BodID As %String, SpojID As %String, poznamkaID As %String) As %Boolean
{
    s bodOd = $g(^||TiskMHD("spojPoznamky",SpojID,"arr",2,poznamkaID,"TiskJRMHDZastavkoveBodOd"))
    s bodDo = $g(^||TiskMHD("spojPoznamky",SpojID,"arr",2,poznamkaID,"TiskJRMHDZastavkoveBodDo"))
    i ('SmerID) && (+bodOd) && (BodID < bodOd) q 0
    i ('SmerID) && (+bodDo) && (BodID > bodDo) q 0
    i (+SmerID) && (+bodDo) && (BodID > bodOd) q 0
    i (+SmerID) && (+bodDo) && (BodID < bodDo) q 0
    q 1
}

/// Přiřadí negativní značku spoji (automaticky nebo z ručního zadání).
ClassMethod PriraditNZSpoji(ByRef sys As %String, SpojID As %String, TypProvozuID As %String)
{
    $$$FillParams
    s AutoPoznamkyOdlisnostiPlatnosti = $g(^||TiskMHD("config","AutoPoznamkyOdlisnostiPlatnosti"))
    s rozsahNZOd = $g(^||TiskMHD("config","rozsahNZOd"))
    s rozsahNZDo = $g(^||TiskMHD("config","rozsahNZDo"))

    i AutoPoznamkyOdlisnostiPlatnosti {
        // Automatické: z bitmapy platnosti
        s tydenniPlatnost = "", OmezeniTXT = ""
        i '..PorovnatBitmapyPlatnosti(.sys, SpojID, TypProvozuID, .tydenniPlatnost, .OmezeniTXT) q

        // Týdenní platnost
        i tydenniPlatnost '= "" s ^||TiskMHD("spojTydenni",SpojID) = tydenniPlatnost

        // Zatřídit značku: hledat existující se stejným textem
        s spojNZ = ""
        // Nejprve v číselníku NZ
        s nz = ""
        f {
            s nz = $o(^||TiskMHD("nzTexty",nz)) i (nz = "") || (spojNZ '= "") q
            i $g(^||TiskMHD("nzTexty",nz)) = OmezeniTXT s spojNZ = nz
        }
        // Pak v přiřazených NZ
        i spojNZ = "" {
            s nz = ""
            f {
                s nz = $o(^||TiskMHD("nzPrirazen",nz)) i (nz = "") || (spojNZ '= "") q
                i $g(^||TiskMHD("nzPrirazen",nz)) = OmezeniTXT s spojNZ = nz
            }
        }
        // Nová značka
        i spojNZ = "" {
            f spojNZ = rozsahNZOd:1 {
                // Kontrola: není obsazená u typu provozu
                s obsazena = 0
                s typProvozuIter = ""
                f {
                    s typProvozuIter = $o(^[dataDB]ALVA.MHD.ENUMS(IdFa,"TypyProvozu","DTA",typProvozuIter)) q:typProvozuIter=""
                    i $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"TypyProvozu","DTA",typProvozuIter,"NegativniZnacka")) = spojNZ s obsazena = 1
                }
                i obsazena continue
                i $d(^||TiskMHD("nzTexty",spojNZ)) continue
                i $d(^||TiskMHD("nzPrirazen",spojNZ)) continue
                q
            }
        }
        s ^||TiskMHD("nzPrirazen",spojNZ) = OmezeniTXT
        s ^||TiskMHD("spojNZ",SpojID) = spojNZ
    }
    else {
        // Ruční: z ručně zadané poznámky spoje
        s PoznamkaID = ""
        s poznamkaID = ""
        f {
            s poznamkaID = $o(^||TiskMHD("spojPoznamky",SpojID,"arr",1,poznamkaID)) i (poznamkaID = "") || (PoznamkaID '= "") q
            i $g(^||TiskMHD("spojPoznamky",SpojID,"arr",1,poznamkaID,"TypPoznamkyID")) = 2 {
                i $g(^||TiskMHD("spojPoznamky",SpojID,"arr",1,poznamkaID,"TiskJRMHDZastavkove")) s PoznamkaID = poznamkaID
            }
        }
        i PoznamkaID = "" q

        s spojNZ = $g(^||TiskMHD("spojPoznamky",SpojID,"arr",1,PoznamkaID,"NZ"))
        s OmezeniTXT = $g(^||TiskMHD("spojPoznamky",SpojID,"arr",1,PoznamkaID,"PopisPoznamky"))
        i $g(^||TiskMHD("spojPoznamky",SpojID,"arr",1,PoznamkaID,"TiskJRMHDZastavkovePouzeZnacku")) s OmezeniTXT = ""
        s ^||TiskMHD("spojNZ",SpojID) = spojNZ
        s ^||TiskMHD("nzPrirazen",spojNZ) = OmezeniTXT
    }
}

/// ═══════════════════════════════════════════════════════════════════════════════
/// Fáze 5: Bitmapy obsluhy
/// ═══════════════════════════════════════════════════════════════════════════════
/// Pro každou zastávku v obou směrech vypočítá bitmapu obsluhovaných zastávek
/// a AllStopTrasaBitmap (1=všechny spoje, 9=jen některé).
ClassMethod VypocitatBitmapyObsluhy()
{
    s AutoPoznamkyRozdilTrasy = $g(^||TiskMHD("config","AutoPoznamkyRozdilTrasy"))
    s AutoPoznamkyRozdilTrasyAlgoritmus = $g(^||TiskMHD("config","AutoPoznamkyRozdilTrasyAlgoritmus"))
    s AutoPoznamkyRozdilTrasyPismena = $g(^||TiskMHD("config","AutoPoznamkyRozdilTrasyPismena"))

    f SmerID = 0,1 {
        s smerKrok = 1 i SmerID s smerKrok = -1

        s BodID = ""
        f {
            s BodID = $o(^||TiskMHD("trasa",SmerID,BodID)) q:BodID=""
            i 'BodID continue ; skip ne-numerické pomocné subscripty

            // Bitmapa obsluhovaných zastávek z tohoto bodu
            s ObsluhaBitmap = ""
            s SpojID = ""
            f {
                s SpojID = $o(^||TiskMHD("obsluhaKonec",SmerID,BodID,SpojID)) q:SpojID=""
                s TrasaSpojBitmap = $g(^||TiskMHD("spojTrasa",SmerID,SpojID))

                // Spoj v zastávce nestaví → přeskočit
                i '$e(TrasaSpojBitmap, BodID) continue

                // Konečný bod spoje: najít poslední obsluhovanou zastávku
                s konecnyBod = BodID
                s dalsiBod = BodID f {
                    s dalsiBod = $o(^||TiskMHD("trasa",SmerID,dalsiBod), smerKrok) i dalsiBod = "" q
                    i 'dalsiBod continue ; skip ne-numerické subscripty
                    i $e(TrasaSpojBitmap, dalsiBod) {
                        s $e(ObsluhaBitmap, dalsiBod) = 1
                        s $e(ObsluhaBitmap, BodID) = 1
                        s konecnyBod = dalsiBod
                    }
                }
                s ^||TiskMHD("obsluhaKonec",SmerID,BodID,SpojID) = konecnyBod
                // Trasa spoje z bodu - včetně konečného bodu (jako legacy)
                s TrasaSpojZBodu = ""
                s dalsiBod = BodID f {
                    s dalsiBod = $o(^||TiskMHD("trasa",SmerID,dalsiBod), smerKrok) i dalsiBod = "" q
                    i 'dalsiBod continue ; skip ne-numerické subscripty
                    i $e(TrasaSpojBitmap, dalsiBod) {
                        s $e(TrasaSpojZBodu, dalsiBod) = 1
                        s $e(TrasaSpojZBodu, BodID) = 1
                    }
                }
                s ^||TiskMHD("spojTrasa",SmerID,SpojID,BodID) = TrasaSpojZBodu
            }
            s ^||TiskMHD("obsluha",SmerID,BodID) = ObsluhaBitmap

            // AllStopTrasaBitmap: 1=všechny staví, 9=jen některé
            s AllStopBitmap = ObsluhaBitmap
            s SpojID = ""
            f {
                s SpojID = $o(^||TiskMHD("obsluhaKonec",SmerID,BodID,SpojID)) q:SpojID=""
                s TrasaSpojZBodu = $g(^||TiskMHD("spojTrasa",SmerID,SpojID,BodID))
                s konecnyBod = $g(^||TiskMHD("obsluhaKonec",SmerID,BodID,SpojID))
                f bodIterator = BodID:smerKrok:konecnyBod {
                    i ('$e(TrasaSpojZBodu, bodIterator)) && (+$e(AllStopBitmap, bodIterator)) {
                        s $e(AllStopBitmap, bodIterator) = 9
                    }
                }
            }
            s ^||TiskMHD("obsluhaVse",SmerID,BodID) = AllStopBitmap

            // Rozdílové bitmapy tras pro auto-poznámkování (alg. 1002)
            i AutoPoznamkyRozdilTrasy && (AutoPoznamkyRozdilTrasyAlgoritmus = 1002) {
                s pocetPismen = 0
                s SpojID = ""
                f {
                    s SpojID = $o(^||TiskMHD("obsluhaKonec",SmerID,BodID,SpojID)) q:SpojID=""
                    s TrasaSpojZBodu = $g(^||TiskMHD("spojTrasa",SmerID,SpojID,BodID))
                    s konecnyBod = $g(^||TiskMHD("obsluhaKonec",SmerID,BodID,SpojID))

                    // Zastavuje navíc (kde AllStopBitmap=9)
                    s DiffBitmap = ""
                    f bodIterator = BodID:smerKrok:konecnyBod {
                        i (+$e(TrasaSpojZBodu, bodIterator)) && (+$e(AllStopBitmap, bodIterator) = 9) {
                            s $e(DiffBitmap, bodIterator) = 1
                        }
                    }
                    i DiffBitmap '= "" {
                        s ^||TiskMHD("rozdilTrasy",SmerID,BodID,0,DiffBitmap,SpojID) = ""
                    }

                    // Konečný interval (končí dříve)
                    // Najít poslední číselný BodID v trase (přeskočit "slouceni" a jiné stringy)
                    s lastBod = $o(^||TiskMHD("trasa",SmerID,""),-smerKrok)
                    i 'lastBod s lastBod = $o(^||TiskMHD("trasa",SmerID,lastBod),-smerKrok)
                    i ((smerKrok=1)&&(konecnyBod < lastBod)) || ((smerKrok=-1)&&(konecnyBod > lastBod)) {
                        s EndDiffBitmap = ""
                        s $e(EndDiffBitmap, konecnyBod) = 1
                        s ^||TiskMHD("rozdilTrasy",SmerID,BodID,1,EndDiffBitmap,SpojID) = ""
                    }
                }

                // Přiřadit písmena k rozdílovým bitmapám
                s pocetPismen = 0
                s typRozdilu = ""
                f {
                    s typRozdilu = $o(^||TiskMHD("rozdilTrasy",SmerID,BodID,typRozdilu)) q:typRozdilu=""
                    s DiffBitmap = ""
                    f {
                        s DiffBitmap = $o(^||TiskMHD("rozdilTrasy",SmerID,BodID,typRozdilu,DiffBitmap)) q:DiffBitmap=""
                        s pocetPismen = pocetPismen + 1
                        s pismeno = $e(AutoPoznamkyRozdilTrasyPismena, pocetPismen)
                        i pismeno = "" s pismeno = "#"
                        s ^||TiskMHD("rozdilTrasyPism",SmerID,BodID,typRozdilu,DiffBitmap) = pismeno
                    }
                }
            }
        }
    }
}

/// ═══════════════════════════════════════════════════════════════════════════════
/// Fáze 6: Sestavení JSON výstupu
/// ═══════════════════════════════════════════════════════════════════════════════
/// Hlavní výstupní smyčka: pro každou zastávku × směr sestaví jednu stránku JSON.
/// Každá stránka se zapíše jako samostatný OUT node (řeší limit délky stringu).
ClassMethod SestavitStranky(ByRef sys As %String)
{
    $$$FillParams
    s LinkaID = $g(^||TiskMHD("config","LinkaID"))
    s AutoPoznamkyRozdilTrasy = $g(^||TiskMHD("config","AutoPoznamkyRozdilTrasy"))
    s AutoPoznamkyRozdilTrasyAlgoritmus = $g(^||TiskMHD("config","AutoPoznamkyRozdilTrasyAlgoritmus"))

    // Explicitní pořadí: nejdřív Tam (0), pak Zpět (1)
    f SmerID = 0,1 {
        i '$d(^||TiskMHD("trasa",SmerID)) continue

        s smerPopis = $s(SmerID=0:"T", 1:"Z")

        // Sesbírat BodID v pořadí jízdy (Tam=vzestupně, Zpět=sestupně)
        // a uložit do pole pro konzistentní iteraci
        k orderedBods s orderedBodCnt = 0
        s BodID = ""
        f {
            s BodID = $o(^||TiskMHD("trasa",SmerID,BodID)) q:BodID=""
            i BodID = "slouceni" continue
            i 'BodID continue
            s orderedBodCnt = orderedBodCnt + 1
            s orderedBods(orderedBodCnt) = BodID
        }
        // Pro směr Zpět obrátit pořadí
        i SmerID = 1 {
            k reversedBods
            f i = 1:1:orderedBodCnt s reversedBods(i) = orderedBods(orderedBodCnt - i + 1)
            k orderedBods m orderedBods = reversedBods
        }

        // Zjistit počet obsluhovaných zastávek
        s pocetZastavek = 0
        f i = 1:1:orderedBodCnt {
            s BodID = orderedBods(i)
            i '$d(^||TiskMHD("odjezdy",SmerID,BodID)) continue
            s ObsluhaBitmap = $g(^||TiskMHD("obsluha",SmerID,BodID))
            i '$e(ObsluhaBitmap, BodID) continue
            s pocetZastavek = pocetZastavek + 1
        }

        // Iterace zastávek → stránky (v pořadí jízdy)
        f i = 1:1:orderedBodCnt {
            s aktualniZastBodID = orderedBods(i)
            // Zastávka nemá odjezdy → přeskočit
            i '$d(^||TiskMHD("odjezdy",SmerID,aktualniZastBodID)) continue
            s ObsluhaBitmap = $g(^||TiskMHD("obsluha",SmerID,aktualniZastBodID))
            i '$e(ObsluhaBitmap, aktualniZastBodID) continue

            s page = ..SestavitJednuStranku(.sys, SmerID, aktualniZastBodID, pocetZastavek)
            s ^||ML("OUT",$i(^||ML("OUT"))) = page.%ToJSON()
        }
    }
}

/// Sestaví jednu stránku (= jedna zastávka × jeden směr).
ClassMethod SestavitJednuStranku(ByRef sys As %String, SmerID As %Integer, aktualniZastBodID As %String, pocetZastavek As %Integer) As %DynamicObject
{
    $$$FillParams
    s page = ##class(%DynamicObject).%New()
    s smerKrok = 1 i SmerID s smerKrok = -1

    // Načíst konfiguraci
    s TrasaID = $g(^||TiskMHD("config","TrasaID"))
    s platnostOd = $g(^||TiskMHD("config","platnostOdHrl"))
    s platnostDo = $g(^||TiskMHD("config","platnostDoHrl"))
    s CisloLinky = $g(^||TiskMHD("config","CisloLinky"))
    s LinkaID = $g(^||TiskMHD("config","LinkaID"))
    s LokalniOznaceni = $g(^||TiskMHD("config","LokalniOznaceni"))
    s NazevLinky = $g(^||TiskMHD("config","NazevLinky"))
    s QRKod = $g(^||TiskMHD("config","QRKod"))
    s TypJR = $g(^||TiskMHD("config","TypJR"))
    s AutoPoznamkyRozdilTrasy = $g(^||TiskMHD("config","AutoPoznamkyRozdilTrasy"))
    s AutoPoznamkyRozdilTrasyAlgoritmus = $g(^||TiskMHD("config","AutoPoznamkyRozdilTrasyAlgoritmus"))
    s smerPopis = $s(SmerID=0:"T", 1:"Z")

    s aktualniZastVal = $g(^||TiskMHD("trasa",SmerID,aktualniZastBodID))
    s aktualniZastavkaID = $lg(aktualniZastVal, 1)
    s aktualniZastavkaCas = $lg(aktualniZastVal, 2)
    s NazevZastavky = $lg(##class(ALVA.MHD.ZastavkyFce).GetNazevHrl(IdFa, dataDB, aktualniZastavkaID, platnostOd), 1)
    s CisloZastavky = $g(^[dataDB]ALVA.MHD.ZST(IdFa,1,"DTA",aktualniZastavkaID,"DTA",1,"Cislo"))
    s Oznacnik = $g(^[dataDB]ALVA.MHD.ZST(IdFa,1,"DTA",aktualniZastavkaID,"DTA",1,"Oznacnik"))

    s ObsluhaBitmap = $g(^||TiskMHD("obsluha",SmerID,aktualniZastBodID))

    // Tisknutá bitmapa = obsluha bez bodů s příznakem „netisknout" (bit aktuální zastávky
    // zůstává - legacy čistí jen mycnt'=cnt). Vstup pro porovnání tras (alg. 1001).
    s TisknutaTrasaBitmap = ObsluhaBitmap
    s skrytyBod = ""
    f {
        s skrytyBod = $o(^||TiskMHD("trasa",SmerID,"netisknout",skrytyBod)) q:skrytyBod=""
        i skrytyBod = aktualniZastBodID continue
        i $e(TisknutaTrasaBitmap,skrytyBod) '= "" s $e(TisknutaTrasaBitmap,skrytyBod) = " "
    }

    // ── Skalární hodnoty ─────────────────────────────────────────────────
    s vals = ##class(%DynamicObject).%New()
    d vals.%Set("CISLO_LINKY",            CisloLinky)
    d vals.%Set("LOKALNI_OZNACENI_LINKY", LokalniOznaceni)
    d vals.%Set("POPIS_LINKY",            NazevLinky)
    d vals.%Set("POPIS_DOPRAVCE",         ..PopisDopravce(.sys, dataDB, IdFa, LinkaID))
    d vals.%Set("POPIS_PLATNOSTI",        ..FormatDatum(platnostOd)_" - "_..FormatDatum(platnostDo))
    d vals.%Set("DATUM_PLATNOSTI_OD",     ..FormatDatum(platnostOd))
    d vals.%Set("DATUM_PLATNOSTI_DO",     ..FormatDatum(platnostDo))
    d vals.%Set("SMER",                   smerPopis)   // T/Z (A1); KONECNA_ZASTAVKA = konečná se nastaví po sestavení trasy
    d vals.%Set("TYP_JR",                 TypJR)
    d vals.%Set("QR_KOD",                 QRKod)
    d vals.%Set("POPIS_ZASTAVKA",         NazevZastavky)
    d vals.%Set("CISLO_ZASTAVKA",         CisloZastavky_"-"_Oznacnik)
    // TITULEK / POZNAMKA_POD_DOPRAVCE / INFORMACE: MHD pro ně nemá zdroj → nejsou ve schématu (viz AD, které je má z JRMAIN)
    // PRVNI_POSLEDNI_SPOJ (text prvního/posledního spoje) odstraněno 2026-07-20 - nepoužívané a drahé:
    // legacy MHD2 CalculatePrvniAndPosledniSpojeText se počítalo per stránka (~6,8 s z 7 s na velké lince,
    // full sken spojů + den-po-dni platnost). Kdyby ho někdo chtěl, reimplementovat nad daty fáze 4 v ^||TiskMHD.
    d page.%Set("values", vals)

    // ── Kolekce ──────────────────────────────────────────────────────────
    s rows = ##class(%DynamicObject).%New()

    // ZASTAVKY - vždy všechny v daném směru, s příznakem JE_AKTUALNI
    s zastavky = ##class(%DynamicArray).%New()
    s poradiZast = 0
    s konecnaNazev = ""                       // poslední zastávka ve směru = konečná (→ KONECNA_ZASTAVKA, A1)
    s bodIterator = ""
    f {
        s bodIterator = $o(^||TiskMHD("trasa",SmerID,bodIterator), smerKrok) q:bodIterator=""
        i 'bodIterator continue ; skip ne-numerické pomocné subscripty ("slouceni"/"slouceniInv")
        // Bod s příznakem „netisknout" se skryje všude kromě aktuální zastávky (jako legacy)
        i (bodIterator '= aktualniZastBodID) && $d(^||TiskMHD("trasa",SmerID,"netisknout",bodIterator)) continue
        // Za aktuální: jen obsluhovatelné zastávky
        i (SmerID=0)&&(bodIterator>aktualniZastBodID)&&('$e(ObsluhaBitmap,bodIterator)) continue
        i (SmerID=1)&&(bodIterator<aktualniZastBodID)&&('$e(ObsluhaBitmap,bodIterator)) continue

        s zastVal = $g(^||TiskMHD("trasa",SmerID,bodIterator))
        s zastID = $lg(zastVal, 1)
        s zastCas = $lg(zastVal, 2)
        s zastCislo = $lg(zastVal, 3)

        s poradiZast = poradiZast + 1
        s jeAktualni = (bodIterator = aktualniZastBodID)
        s casJizdy = 0
        i jeAktualni || ((SmerID=0)&&(bodIterator>aktualniZastBodID)) || ((SmerID=1)&&(bodIterator<aktualniZastBodID)) {
            s casJizdy = zastCas - aktualniZastavkaCas
        }

        s zastNazev = $lg(##class(ALVA.MHD.ZastavkyFce).GetNazevHrl(IdFa, dataDB, zastID, platnostOd), 1)

        // Odsazení (legacy: dta 10)
        s odsazeni = 0

        // Na znamení
        s naZnameni = 0

        s z = ..BuildZastavka(bodIterator, zastNazev, zastCislo, casJizdy, poradiZast, jeAktualni, naZnameni, odsazeni)

        // Zóny
        s zonaidx = ""
        f {
            s zonaidx = $o(^[dataDB]ALVA.MHD.ZST(IdFa,1,"DTA",zastID,"DTA",1,"ZonyID",zonaidx)) q:zonaidx=""
            s ZonaID = $g(^[dataDB]ALVA.MHD.ZST(IdFa,1,"DTA",zastID,"DTA",1,"ZonyID",zonaidx,"Itm"))
            s ZonaCislo = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"Zony","DTA",ZonaID,"Cislo"))
            i ZonaCislo '= "" d ..AddZona(z, ZonaCislo)
        }

        // Atributy zastávky (jen neprojete + aktuální jen N,V,x)
        i ((SmerID=0)&&(bodIterator'<aktualniZastBodID)) || ((SmerID=1)&&(bodIterator'>aktualniZastBodID)) {
            s AtributID = ""
            f {
                s AtributID = $o(^||TiskMHD("atributyZast",SmerID,bodIterator,AtributID)) q:AtributID=""
                // Aktuální zastávka: jen základní atributy (ID<=1003)
                i (bodIterator=aktualniZastBodID)&&(AtributID>1003) continue
                s atrVal = $g(^||TiskMHD("atributyZast",SmerID,bodIterator,AtributID))
                s SpecZnak = ..ConvertSpecialniZnak($lg(atrVal,3))
                d ..AddAtribut(z, SpecZnak, $lg(atrVal,1))
            }
        }

        // Poznámky zastávky (jen neprojete)
        i ((SmerID=0)&&(bodIterator>aktualniZastBodID)) || ((SmerID=1)&&(bodIterator<aktualniZastBodID)) {
            s znakPoznamky = ""
            f {
                s znakPoznamky = $o(^||TiskMHD("poznamkyZast",SmerID,bodIterator,znakPoznamky)) q:znakPoznamky=""
                s pozVal = $g(^||TiskMHD("poznamkyZast",SmerID,bodIterator,znakPoznamky))
                d ..AddPoznamkaZastavky(z, znakPoznamky, "text", $lg(pozVal,1))
            }
        }

        s konecnaNazev = zastNazev
        s oznacnikRow = $g(^[dataDB]ALVA.MHD.ZST(IdFa,1,"DTA",zastID,"DTA",1,"Oznacnik"))
        d z.%Set("NASTUPISTE", oznacnikRow)   // TR3: nástupiště per zastávka (MHD = Oznacnik)
        d zastavky.%Push(z)
    }
    d rows.%Set("ZASTAVKY", zastavky)
    d vals.%Set("KONECNA_ZASTAVKA", konecnaNazev)   // A1: konečná směru (dřív T/Z, to je teď v SMER)

    // SKUPINY_DNU - z typů provozu. Struktura sloupců je per LINKA (všechny SMO sloupce linky),
    // ne per zastávka - sloupec bez odjezdů na této stránce se tiskne prázdný se záhlavím, aby
    // layout seděl napříč stránkami i se SMO nastavením (jako legacy, „doplním TypProvozuID").
    s skupiny = ##class(%DynamicArray).%New()
    s smoCol = ""
    f {
        s smoCol = $o(^||TiskMHD("smoSloupce",smoCol)) q:smoCol=""
        s nadpisLb = $g(^||TiskMHD("smoNadpis",smoCol))
        // Pořadí = číslo sloupce (SMO1..SMO4), aby sloupce šly zleva SMO1→SMO4 jako v legacy.
        d skupiny.%Push(..BuildSkupinaDnu(smoCol, $lg(nadpisLb,1), $lg(nadpisLb,2), smoCol))
    }
    d rows.%Set("SKUPINY_DNU", skupiny)

    // ODJEZDY
    s odjezdy = ##class(%DynamicArray).%New()
    // Lokální sběr poznámek pro tuto stránku
    k strankovePoznamky

    // Pořadí iterace = legacy (typ provozu vzestupně, pak hodina/minuta/spoj) - na pořadí
    // závisí přidělování písmen rozdílových poznámek (alg. 1001); po sloupcích by písmena
    // vyšla jinak než na legacy vývěsce. Výstupní pořadí řádků element neřeší (řadí si sám).
    k typyNaStrance
    s smoCol = ""
    f {
        s smoCol = $o(^||TiskMHD("odjezdy",SmerID,aktualniZastBodID,smoCol)) q:smoCol=""
        s hh = "" f {
            s hh = $o(^||TiskMHD("odjezdy",SmerID,aktualniZastBodID,smoCol,hh)) q:hh=""
            s mm = "" f {
                s mm = $o(^||TiskMHD("odjezdy",SmerID,aktualniZastBodID,smoCol,hh,mm)) q:mm=""
                s SpojID = "" f {
                    s SpojID = $o(^||TiskMHD("odjezdy",SmerID,aktualniZastBodID,smoCol,hh,mm,SpojID)) q:SpojID=""
                    s tpid = $g(^||TiskMHD("spojTyp",SpojID))
                    i tpid '= "" s typyNaStrance(tpid,smoCol) = ""
                }
            }
        }
    }

    s tpid = ""
    f {
        s tpid = $o(typyNaStrance(tpid)) q:tpid=""
        s smoCol = ""
        f {
        s smoCol = $o(typyNaStrance(tpid,smoCol)) q:smoCol=""
        s hh = ""
        f {
            s hh = $o(^||TiskMHD("odjezdy",SmerID,aktualniZastBodID,smoCol,hh)) q:hh=""
            s mm = ""
            f {
                s mm = $o(^||TiskMHD("odjezdy",SmerID,aktualniZastBodID,smoCol,hh,mm)) q:mm=""
                s SpojID = ""
                f {
                    s SpojID = $o(^||TiskMHD("odjezdy",SmerID,aktualniZastBodID,smoCol,hh,mm,SpojID)) q:SpojID=""
                    // Sloupec může sdílet víc typů provozu - v tomto průchodu jen aktuální typ
                    i $g(^||TiskMHD("spojTyp",SpojID)) '= tpid continue

                    s odj = ..BuildOdjezd(aktualniZastBodID, smoCol, hh, mm)

                    // ── Anotace odjezdu ──

                    // Týdenní platnost (automatická)
                    s tydPlatnost = $g(^||TiskMHD("spojTydenni",SpojID))
                    f denIdx = 1:1:$l(tydPlatnost) {
                        s denZnak = $e(tydPlatnost, denIdx)
                        i denZnak = "" continue
                        // Značka dne = číslice 1-7 / + / X (kroužkový glyf symbolového fontu, 1:1 legacy
                        // GSId id 8-16), NE textová zkratka „Po" - konzistentní s legendou (specialni + číslice).
                        d ..AddAnotace(odj, "specialni", denZnak)
                        // Poznámka pro den
                        s denText = ..GetDenVTydnuText(denZnak)
                        i denText '= "" s strankovePoznamky(denZnak) = $lb(denText, 2)
                    }

                    // Speciální znaky ze spoje (ručně zadané)
                    s poznamkaID = ""
                    f {
                        s poznamkaID = $o(^||TiskMHD("spojPoznamky",SpojID,"arr",1,poznamkaID)) q:poznamkaID=""
                        i $g(^||TiskMHD("spojPoznamky",SpojID,"arr",1,poznamkaID,"TypPoznamkyID")) '= 1 continue
                        i '$g(^||TiskMHD("spojPoznamky",SpojID,"arr",1,poznamkaID,"TiskJRMHDZastavkove")) continue
                        i '..PoznamkaSpojeVRozsahu(SmerID, aktualniZastBodID, SpojID, poznamkaID) continue
                        s specZnak = $g(^||TiskMHD("spojPoznamky",SpojID,"arr",1,poznamkaID,"SpecZnak"))
                        i specZnak = "" continue
                        // Barvy z PoznamkaSpoj (sdílené s vozovým JŘ; prázdné → default rendering).
                        // Aplikují se i na anotaci nad minutou (konzistence s legendou).
                        s pForeColor = ..RgbToHex($g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"PoznamkaSpoj","DTA",poznamkaID,"TiskJRMHDVozoveBarvaZnakuRGB")))
                        s pBackColor = ..RgbToHex($g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"PoznamkaSpoj","DTA",poznamkaID,"TiskJRMHDVozoveBarvaPozadiRGB")))
                        d ..AddAnotace(odj, "specialni", specZnak, pForeColor, pBackColor)
                        s PopisPozn = $g(^||TiskMHD("spojPoznamky",SpojID,"arr",1,poznamkaID,"PopisPoznamky"))
                        i '$g(^||TiskMHD("spojPoznamky",SpojID,"arr",1,poznamkaID,"TiskJRMHDZastavkovePouzeZnacku")) {
                            s strankovePoznamky(specZnak) = $lb(PopisPozn, 2, "", pForeColor, pBackColor)
                        }
                    }

                    // NZ (negativní značka)
                    s spojNZ = $g(^||TiskMHD("spojNZ",SpojID))
                    i spojNZ '= "" {
                        d ..AddAnotace(odj, "nz", spojNZ)
                        s nzTxt = $g(^||TiskMHD("nzPrirazen",spojNZ))
                        i nzTxt = "" s nzTxt = $g(^||TiskMHD("nzTexty",spojNZ))
                        i nzTxt '= "" s strankovePoznamky("nz_"_spojNZ) = $lb(nzTxt, 1, spojNZ)
                    }

                    // Značka sloučené skupiny dnů: typ mimo dominantní režim SMO sloupce
                    // → NZ značka + legenda „jede jen …" (viz PriraditZnackySmoSkupin)
                    s spojTypProvozu = $g(^||TiskMHD("spojTyp",SpojID))
                    i spojTypProvozu '= "" {
                        s smoZnackaVal = $g(^||TiskMHD("smoZnacka",spojTypProvozu))
                        i smoZnackaVal '= "" {
                            s smoZnak = $lg(smoZnackaVal,1)
                            d ..AddAnotace(odj, "nz", smoZnak)
                            s strankovePoznamky("nz_"_smoZnak) = $lb($lg(smoZnackaVal,2), 1, smoZnak)
                        }
                    }

                    // Atributy spoje
                    s atributID = ""
                    f {
                        s atributID = $o(^||TiskMHD("spojAtributy",SpojID,"arr",1,atributID)) q:atributID=""
                        // SpecZnak z GetAtributySpoj může obsahovat interní ID - přečíst displayový znak z ENUMS
                        s specZnak = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"Atribut","DTA",atributID,"SpecZnak"))
                        i specZnak = "" continue
                        s specZnak = ..ConvertSpecialniZnak(specZnak)
                        s Popis = $g(^||TiskMHD("spojAtributy",SpojID,"arr",1,atributID,"Popis"))
                        d ..AddAnotace(odj, "specialni", specZnak)
                        s strankovePoznamky(specZnak) = $lb(Popis, 2)
                    }

                    // Auto-poznámkování tras (alg. 1001) - porovnává se TISKNUTÁ bitmapa (bez
                    // skrytých bodů), jinak by každý spoj přes skrytou větev generoval falešnou poznámku
                    i AutoPoznamkyRozdilTrasy && (AutoPoznamkyRozdilTrasyAlgoritmus = 1001) {
                        d ..DoplnitPoznamkuRozdilTrasy1001(.sys, odj, SmerID, aktualniZastBodID, SpojID, .strankovePoznamky, TisknutaTrasaBitmap)
                    }

                    // Auto-poznámkování tras (alg. 1002)
                    i AutoPoznamkyRozdilTrasy && (AutoPoznamkyRozdilTrasyAlgoritmus = 1002) {
                        d ..DoplnitPoznamkuRozdilTrasy1002(.sys, odj, SmerID, aktualniZastBodID, SpojID, .strankovePoznamky)
                    }

                    // Textové poznámky spoje
                    s poznamkaID = ""
                    f {
                        s poznamkaID = $o(^||TiskMHD("spojPoznamky",SpojID,"arr",1,poznamkaID)) q:poznamkaID=""
                        i $g(^||TiskMHD("spojPoznamky",SpojID,"arr",1,poznamkaID,"TypPoznamkyID")) '= 0 continue
                        i '$g(^||TiskMHD("spojPoznamky",SpojID,"arr",1,poznamkaID,"TiskJRMHDZastavkove")) continue
                        i '..PoznamkaSpojeVRozsahu(SmerID, aktualniZastBodID, SpojID, poznamkaID) continue
                        s znakPozn = $g(^||TiskMHD("spojPoznamky",SpojID,"arr",1,poznamkaID,"Text"))
                        i znakPozn = "" continue
                        // Barvy z PoznamkaSpoj (sdílené s vozovým JŘ; prázdné → default rendering).
                        // Aplikují se i na anotaci nad minutou (konzistence s legendou).
                        s pForeColor = ..RgbToHex($g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"PoznamkaSpoj","DTA",poznamkaID,"TiskJRMHDVozoveBarvaZnakuRGB")))
                        s pBackColor = ..RgbToHex($g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"PoznamkaSpoj","DTA",poznamkaID,"TiskJRMHDVozoveBarvaPozadiRGB")))
                        d ..AddAnotace(odj, "text", znakPozn, pForeColor, pBackColor)
                        s PopisPozn = $g(^||TiskMHD("spojPoznamky",SpojID,"arr",1,poznamkaID,"PopisPoznamky"))
                        i '$g(^||TiskMHD("spojPoznamky",SpojID,"arr",1,poznamkaID,"TiskJRMHDZastavkovePouzeZnacku")) {
                            s strankovePoznamky(znakPozn) = $lb(PopisPozn, 0, "", pForeColor, pBackColor)
                        }
                    }

                    d odjezdy.%Push(odj)
                }
            }
        }
        }
    }
    d rows.%Set("ODJEZDY", odjezdy)

    // POZNAMKY - seřazené: znaky (text) → NZ → speciální
    s poznamky = ##class(%DynamicArray).%New()

    // Znaky z trasy (textové poznámky) - formát $lb(text, 0, foreColor, backColor)
    s znakPoznamky = ""
    f {
        s znakPoznamky = $o(^||TiskMHD("poznamkyZast",SmerID,aktualniZastBodID,znakPoznamky)) q:znakPoznamky=""
        s pozVal = $g(^||TiskMHD("poznamkyZast",SmerID,aktualniZastBodID,znakPoznamky))
        d poznamky.%Push(..BuildPoznamka("text", znakPoznamky, $lg(pozVal,1), $lg(pozVal,3), $lg(pozVal,4)))
    }

    // Poznámky ze stránky (odjezdy) - formát $lb(text, typeCode, znakOrEmpty, foreColor, backColor)
    // NZ má znak v poli 3 a žádnou barvu (4,5 prázdné); text/specialni má 3 prázdné a barvy v 4,5.
    s znak = ""
    f {
        s znak = $o(strankovePoznamky(znak)) q:znak=""
        s pVal = $g(strankovePoznamky(znak))
        s pTypCislo = $lg(pVal, 2)
        s pTyp = $s(pTypCislo=0:"text", pTypCislo=1:"nz", 1:"specialni")
        s pZnak = $s(pTypCislo=1:$lg(pVal,3), 1:znak)
        s pText = $lg(pVal, 1)
        d poznamky.%Push(..BuildPoznamka(pTyp, pZnak, pText, $lg(pVal,4), $lg(pVal,5)))
    }

    // Atributy zastávky jako poznámky - ze VŠECH zobrazených zastávek (ne jen aktuální)
    // Deduplikace přes poznamkyAtribut(SpecZnak) aby se stejný atribut nevypsal víckrát
    k poznamkyAtribut
    s bodIterator = ""
    f {
        s bodIterator = $o(^||TiskMHD("trasa",SmerID,bodIterator)) q:bodIterator=""
        i 'bodIterator continue ; skip ne-numerické pomocné subscripty
        // Stejný filtr jako u zastávek v StopList: jen neprojete + aktuální, bez skrytých bodů
        i (bodIterator '= aktualniZastBodID) && $d(^||TiskMHD("trasa",SmerID,"netisknout",bodIterator)) continue
        i (SmerID=0)&&(bodIterator<aktualniZastBodID) continue
        i (SmerID=1)&&(bodIterator>aktualniZastBodID) continue
        s AtributID = ""
        f {
            s AtributID = $o(^||TiskMHD("atributyZast",SmerID,bodIterator,AtributID)) q:AtributID=""
            i (bodIterator=aktualniZastBodID)&&(AtributID>1003) continue
            s atrVal = $g(^||TiskMHD("atributyZast",SmerID,bodIterator,AtributID))
            s SpecZnak = ..ConvertSpecialniZnak($lg(atrVal,3))
            i SpecZnak = "" continue
            i $d(poznamkyAtribut(SpecZnak)) continue
            s poznamkyAtribut(SpecZnak) = ""
            // „na znamení" řádek otagovat (rozpoznání dle popisu, konzistentně s TD IsNaZnameniAttr;
            // symbol se liší dle firmy) → přepínač „Na znamení" schová značku i tento řádek legendy (#7161).
            s popisAtr = $lg(atrVal,1)
            s katAtr = $s($zcvt($e(popisAtr,1,9),"L")="na znamen":"atribut-zastavky", 1:"")
            d poznamky.%Push(..BuildPoznamka("specialni", SpecZnak, popisAtr, "", "", katAtr))
        }
    }

    d rows.%Set("POZNAMKY", poznamky)
    d page.%Set("rows", rows)

    q page
}

/// ═══════════════════════════════════════════════════════════════════════════════
/// Pomocné metody pro auto-poznámkování tras
/// ═══════════════════════════════════════════════════════════════════════════════
/// Auto-poznámkování alg. 1001: textová poznámka o rozdílech v trase spoje.
ClassMethod DoplnitPoznamkuRozdilTrasy1001(ByRef sys As %String, odj As %DynamicObject, SmerID As %Integer, aktualniZastBodID As %String, SpojID As %String, ByRef strankovePoznamky, TisknutaTrasaBitmap As %String = "")
{
    $$$FillParams
    s platnostOd = $g(^||TiskMHD("config","platnostOdHrl"))
    s AutoPoznamkyRozdilTrasyPismena = $g(^||TiskMHD("config","AutoPoznamkyRozdilTrasyPismena"))

    s TrasaSpojZBodu = $g(^||TiskMHD("spojTrasa",SmerID,SpojID,aktualniZastBodID))
    i TisknutaTrasaBitmap = "" s TisknutaTrasaBitmap = $g(^||TiskMHD("obsluha",SmerID,aktualniZastBodID))

    i TisknutaTrasaBitmap = TrasaSpojZBodu q

    // Generovat text
    k arri, arro
    s arri("dta",1) = SmerID
    s arri("dta",2) = aktualniZastBodID
    s arri("dta",3) = TisknutaTrasaBitmap
    s arri("dta",4) = TisknutaTrasaBitmap
    s arri("dta",5) = TrasaSpojZBodu
    s arri("dta",6) = platnostOd
    s tmpcnt = ""
    f {
        s tmpcnt = $o(^||TiskMHD("trasa",SmerID,tmpcnt)) q:tmpcnt=""
        i 'tmpcnt continue ; skip ne-numerické pomocné subscripty
        s arri("arr",tmpcnt) = ^||TiskMHD("trasa",SmerID,tmpcnt)
    }
    try {
        s isok = ##class(ALVA.MHD.LinkyPrintFormatMHD).MakeOmezTrasaTxMH29052025(IdFa,dataDB,.arri,.arro)
    }
    catch ex {
        s arro("dta",1) = "Chyba při generování poznámky"
    }

    s poznamkaText = $g(arro("dta",1))
    i poznamkaText = "" q

    // Dohledat existující znak
    s idznk = ""
    s existujiciZnak = ""
    f {
        s existujiciZnak = $o(strankovePoznamky(existujiciZnak)) i (existujiciZnak = "") || (idznk '= "") q
        i $lg($g(strankovePoznamky(existujiciZnak)),1) = poznamkaText s idznk = existujiciZnak
    }

    // Přiřadit nový znak
    i idznk = "" {
        f pismenoIdx = 1:1:$l(AutoPoznamkyRozdilTrasyPismena) {
            i idznk '= "" continue
            s kandidat = $e(AutoPoznamkyRozdilTrasyPismena, pismenoIdx)
            i '$d(strankovePoznamky(kandidat)) {
                s idznk = kandidat
                s strankovePoznamky(idznk) = $lb(poznamkaText, 0)
            }
        }
    }

    i idznk '= "" {
        s typCislo = +$lg($g(strankovePoznamky(idznk)), 2)
        s typStr = $s(typCislo=0:"text", typCislo=1:"nz", 1:"specialni")
        d ..AddAnotace(odj, typStr, idznk)
    }
}

/// Auto-poznámkování alg. 1002: písmena z rozdílových bitmap.
/// Přidá anotaci k odjezdu + text legendy do strankovePoznamky.
ClassMethod DoplnitPoznamkuRozdilTrasy1002(ByRef sys As %String, odj As %DynamicObject, SmerID As %Integer, aktualniZastBodID As %String, SpojID As %String, ByRef strankovePoznamky)
{
    $$$FillParams
    s platnostOd = $g(^||TiskMHD("config","platnostOdHrl"))

    s typRozdilu = ""
    f {
        s typRozdilu = $o(^||TiskMHD("rozdilTrasy",SmerID,aktualniZastBodID,typRozdilu)) q:typRozdilu=""
        s DiffBitmap = ""
        f {
            s DiffBitmap = $o(^||TiskMHD("rozdilTrasy",SmerID,aktualniZastBodID,typRozdilu,DiffBitmap)) q:DiffBitmap=""
            // Kontrola: tento spoj je v této rozdílové skupině?
            i '$d(^||TiskMHD("rozdilTrasy",SmerID,aktualniZastBodID,typRozdilu,DiffBitmap,SpojID)) continue
            s pismeno = $g(^||TiskMHD("rozdilTrasyPism",SmerID,aktualniZastBodID,typRozdilu,DiffBitmap))
            i pismeno = "" continue
            d ..AddAnotace(odj, "text", pismeno)

            // Přidat text do legendy (jen jednou per písmeno)
            i '$d(strankovePoznamky(pismeno)) {
                i typRozdilu = 0 {
                    // Rozdíl trasy: "zastávky obsluhují pouze shodně označené spoje"
                    s strankovePoznamky(pismeno) = $lb("zastávky obsluhují pouze shodně označené spoje", 0)
                }
                elseif typRozdilu = 1 {
                    // Končí dříve: najít název konečné zastávky z DiffBitmap
                    s konBod = "" f pp = 1:1:$l(DiffBitmap) { i $e(DiffBitmap,pp) s konBod = pp q }
                    s konNazev = ""
                    i konBod '= "" {
                        s trasaVal = $g(^||TiskMHD("trasa",SmerID,konBod))
                        s konZastID = $lg(trasaVal, 1)
                        i konZastID '= "" s konNazev = $lg(##class(ALVA.MHD.ZastavkyFce).GetNazevHrl(IdFa, dataDB, konZastID, platnostOd), 1)
                    }
                    i konNazev '= "" {
                        s strankovePoznamky(pismeno) = $lb("končí v zast. "_konNazev, 0)
                    } else {
                        s strankovePoznamky(pismeno) = $lb("končí dříve", 0)
                    }
                }
            }
        }
    }
}

/// Vrátí textový popis dne v týdnu pro speciální znaky.
ClassMethod GetDenVTydnuText(denZnak As %String) As %String
{
    i denZnak = "1" q "jede v pondělí"
    i denZnak = "2" q "jede v úterý"
    i denZnak = "3" q "jede ve středu"
    i denZnak = "4" q "jede ve čtvrtek"
    i denZnak = "5" q "jede v pátek"
    i denZnak = "6" q "jede v sobotu"
    i denZnak = "7" q "jede v neděli"
    q ""
}

/// ═══════════════════════════════════════════════════════════════════════════════
/// JŘ helpery - stavba JSON objektů
/// ═══════════════════════════════════════════════════════════════════════════════
/// Vytvoří %DynamicObject zastávky s inicializovanými vnořenými poli.
ClassMethod BuildZastavka(id As %String, popis As %String, tc As %String = "", casJizdy As %Integer = 0, poradi As %Integer = 1, jeAktualni As %Boolean = 0, naZnameni As %Boolean = 0, odsazeni As %Integer = 0) As %DynamicObject
{
    s zastavka = ##class(%DynamicObject).%New()
    // Čitelná field ID (SLOVO_SLOVO) - sdílený kontrakt sloupců StopList↔DynamicTable.
    // TC (číslo zastávky z CIS) vědomě nevystaveno; poziční potřebu pokrývá PORADI.
    d zastavka.%Set("ID", id)                          // interní join klíč (ODJEZDY.ZASTAVKA_ID), ne ve schématu
    d zastavka.%Set("NAZEV_ZASTAVKY", popis)
    d zastavka.%Set("CAS_JIZDY", casJizdy)
    d zastavka.%Set("PORADI", poradi)
    d zastavka.%Set("AKTUALNI_ZASTAVKA", $s(jeAktualni:"true", 1:"false"))
    d zastavka.%Set("NA_ZNAMENI", $s(naZnameni:"true", 1:"false"))
    d zastavka.%Set("ODSAZENI", odsazeni)
    d zastavka.%Set("ZONY", ##class(%DynamicArray).%New())
    d zastavka.%Set("ATRIBUTY", ##class(%DynamicArray).%New())
    d zastavka.%Set("POZNAMKY_ZASTAVKY", ##class(%DynamicArray).%New())
    q zastavka
}

ClassMethod AddZona(zastavka As %DynamicObject, zona As %String)
{
    d zastavka.%Get("ZONY").%Push(zona)
}

ClassMethod AddAtribut(zastavka As %DynamicObject, znak As %String, popis As %String = "")
{
    s atribut = ##class(%DynamicObject).%New()
    d atribut.%Set("ZNAK", znak)
    d atribut.%Set("POPIS", popis)
    d zastavka.%Get("ATRIBUTY").%Push(atribut)
}

ClassMethod AddPoznamkaZastavky(zastavka As %DynamicObject, znak As %String, typ As %String = "text", text As %String = "")
{
    s poznamka = ##class(%DynamicObject).%New()
    d poznamka.%Set("ZNAK", znak)
    d poznamka.%Set("TYP", typ)
    d poznamka.%Set("TEXT", text)
    d zastavka.%Get("POZNAMKY_ZASTAVKY").%Push(poznamka)
}

ClassMethod BuildSkupinaDnu(id As %String, nadpis As %String, nadpis2 As %String = "", poradi As %Integer = 1) As %DynamicObject
{
    s skupina = ##class(%DynamicObject).%New()
    d skupina.%Set("ID", id)
    d skupina.%Set("NADPIS", nadpis)
    d skupina.%Set("NADPIS2", nadpis2)
    d skupina.%Set("PORADI", poradi)
    q skupina
}

ClassMethod BuildOdjezd(zastavkaId As %String, skupinaDnuId As %String, hodina As %Integer, minuta As %Integer) As %DynamicObject
{
    s odjezd = ##class(%DynamicObject).%New()
    d odjezd.%Set("ZASTAVKA_ID", zastavkaId)
    d odjezd.%Set("SKUPINA_DNU_ID", skupinaDnuId)
    d odjezd.%Set("HODINA", hodina)
    d odjezd.%Set("MINUTA", minuta)
    d odjezd.%Set("FORE_COLOR", "")
    d odjezd.%Set("BACK_COLOR", "")
    d odjezd.%Set("ANOTACE", ##class(%DynamicArray).%New())
    q odjezd
}

ClassMethod AddAnotace(odjezd As %DynamicObject, typ As %String, znak As %String, foreColor As %String = "", backColor As %String = "")
{
    s anotace = ##class(%DynamicObject).%New()
    d anotace.%Set("TYP", typ)
    d anotace.%Set("ZNAK", znak)
    d anotace.%Set("FORE_COLOR", foreColor)
    d anotace.%Set("BACK_COLOR", backColor)
    d odjezd.%Get("ANOTACE").%Push(anotace)
}

ClassMethod BuildPoznamka(typ As %String, znak As %String, text As %String, foreColor As %String = "", backColor As %String = "", kategorie As %String = "") As %DynamicObject
{
    s poznamka = ##class(%DynamicObject).%New()
    d poznamka.%Set("TYP", typ)
    d poznamka.%Set("ZNAK", znak)
    d poznamka.%Set("TEXT", text)
    d poznamka.%Set("FORE_COLOR", foreColor)
    d poznamka.%Set("BACK_COLOR", backColor)
    // kategorie = interní tag pro TD filtr viditelnosti legendy (#7161): „atribut-zastavky"
    // = řádek „na znamení" (skryje se s přepínačem „Na znamení"). Prázdné = needitelné filtrem.
    d poznamka.%Set("KATEGORIE", kategorie)
    q poznamka
}

/// Konvertuje 8-bit integer (0-255) na dvouznakovou velkou hex reprezentaci.
ClassMethod IntToHex2(n As %Integer) As %String [ Private ]
{
    s digits = "0123456789ABCDEF"
    i n < 0 s n = 0
    i n > 255 s n = 255
    q $e(digits, (n\16) + 1) _ $e(digits, (n#16) + 1)
}

/// Konvertuje RGB string "R;G;B" (např. "255;191;128") na SkiaSharp hex "#FFRRGGBB".
/// Prázdný vstup nebo nevalidní formát → prázdný string.
ClassMethod RgbToHex(rgb As %String) As %String
{
    i rgb = "" q ""
    s r = +$p(rgb,";",1)
    s g = +$p(rgb,";",2)
    s b = +$p(rgb,";",3)
    q "#FF"_..IntToHex2(r)_..IntToHex2(g)_..IntToHex2(b)
}

/// Konvertuje číselný kód speciálního znaku na Unicode string.
ClassMethod ConvertSpecialniZnak(cislo As %String) As %String
{
    i cislo = 1 q "Po"
    i cislo = 2 q "Út"
    i cislo = 3 q "St"
    i cislo = 4 q "Čt"
    i cislo = 5 q "Pá"
    i cislo = 6 q "So"
    i cislo = 7 q "Ne"
    i +cislo > 31 q $c(cislo)
    q cislo
}

/// ═══════════════════════════════════════════════════════════════════════════════
/// Standardní JŘ schema definice
/// ═══════════════════════════════════════════════════════════════════════════════
/// Přidá standardní skalární pole JŘ agendy do pole fields.
ClassMethod AddStandardJRFields(fields As %DynamicArray)
{
    // Pořadí seskupené podle toho, čeho se pole týká - shodně s AD vývěskou (nález Jan 31. 8.:
    // zastávková pole byla rozházená mezi ostatní a hledala se v seznamu po jednom).

    // ── linka ──
    d ..AddField(fields, "CISLO_LINKY",            "Číslo linky (dle licence)", "number")
    d ..AddField(fields, "LOKALNI_OZNACENI_LINKY", "Veřejné označení linky",    "text")
    d ..AddField(fields, "POPIS_LINKY",            "Popis linky",            "text")
    d ..AddField(fields, "TYP_JR",                 "Typ jízdního řádu",      "text")

    // ── dopravce ──
    d ..AddField(fields, "POPIS_DOPRAVCE",         "Popis dopravce",         "text")

    // ── platnost ──
    d ..AddField(fields, "POPIS_PLATNOSTI",        "Popis platnosti",        "text")
    d ..AddField(fields, "DATUM_PLATNOSTI_OD",     "Datum platnosti od",     "date")
    d ..AddField(fields, "DATUM_PLATNOSTI_DO",     "Datum platnosti do",     "date")

    // ── zastávka, na které vývěska visí, a směr ──
    d ..AddField(fields, "POPIS_ZASTAVKA",         "Aktuální zastávka",      "text")
    d ..AddField(fields, "CISLO_ZASTAVKA",         "Číslo zastávky",         "text")
    d ..AddField(fields, "SMER",                   "Směr (T/Z)",             "text")
    d ..AddField(fields, "KONECNA_ZASTAVKA",       "Konečná zastávka",       "text")

    // ── doplňky ──
    d ..AddField(fields, "QR_KOD",                 "QR kód URL",             "text")
}

/// Přidá standardní tabulkové kolekce JŘ agendy do pole collections.
ClassMethod AddStandardJRCollections(collections As %DynamicArray)
{
    // ZASTAVKY
    s colZastavky = ##class(%DynamicObject).%New()
    s colZastavky.name          = "Zastávky"
    s colZastavky.collectionKey = "ZASTAVKY"
    s colZastavkyFields = ##class(%DynamicArray).%New()
    d ..AddField(colZastavkyFields, "NAZEV_ZASTAVKY",    "Název zastávky",    "text")
    d ..AddField(colZastavkyFields, "CAS_JIZDY",         "Čas jízdy",         "number")
    d ..AddField(colZastavkyFields, "PORADI",            "Pořadí",            "number")
    d ..AddField(colZastavkyFields, "AKTUALNI_ZASTAVKA", "Aktuální zastávka", "boolean")
    d ..AddField(colZastavkyFields, "ZONY",              "Zóny",              "text")
    d ..AddField(colZastavkyFields, "NASTUPISTE",        "Označník",          "text")
    s colZastavky.fields = colZastavkyFields
    d collections.%Push(colZastavky)

    // SKUPINY_DNU
    s colSkupiny = ##class(%DynamicObject).%New()
    s colSkupiny.name          = "Skupiny dnů"
    s colSkupiny.collectionKey = "SKUPINY_DNU"
    s colSkupinyFields = ##class(%DynamicArray).%New()
    // ID (join/pivot klíč) zůstává ve wire, ale NEvystavuje se jako sloupec.
    d ..AddField(colSkupinyFields, "NADPIS",  "Nadpis skupiny",   "text")
    d ..AddField(colSkupinyFields, "NADPIS2", "Nadpis skupiny 2", "text")
    d ..AddField(colSkupinyFields, "PORADI",  "Pořadí",           "number")
    s colSkupiny.fields = colSkupinyFields
    d collections.%Push(colSkupiny)

    // ODJEZDY
    s colOdjezdy = ##class(%DynamicObject).%New()
    s colOdjezdy.name          = "Odjezdy"
    s colOdjezdy.collectionKey = "ODJEZDY"
    s colOdjezdyFields = ##class(%DynamicArray).%New()
    // Ploché user-facing pole. ZASTAVKA_ID/SKUPINA_DNU_ID (join/pivot klíče) + FORE_COLOR/BACK_COLOR
    // (prezentační nápovědy, čte je jen render) zůstávají ve wire, ale NEvystavují se jako sloupce.
    d ..AddField(colOdjezdyFields, "HODINA", "Hodina", "number")
    d ..AddField(colOdjezdyFields, "MINUTA", "Minuta", "number")
    s colOdjezdy.fields = colOdjezdyFields
    d collections.%Push(colOdjezdy)

    // POZNAMKY
    s colPoznamky = ##class(%DynamicObject).%New()
    s colPoznamky.name          = "Poznámky"
    s colPoznamky.collectionKey = "POZNAMKY"
    s colPoznamkyFields = ##class(%DynamicArray).%New()
    d ..AddField(colPoznamkyFields, "TYP",  "Typ poznámky", "text")
    d ..AddField(colPoznamkyFields, "ZNAK", "Znak",          "text")
    d ..AddField(colPoznamkyFields, "TEXT", "Text poznámky",  "text")
    s colPoznamky.fields = colPoznamkyFields
    d collections.%Push(colPoznamky)
}

/// Přidá standardní JŘ parametry do pole parametrů.
ClassMethod AddStandardJRParameters(arr As %DynamicArray)
{
    // `group` = vizuální seskupení v liště parametrů / dialogu (aditivní klíč - starší klient ignoruje).
    // Konvence: skupina = UI název elementu, který parametr ovlivňuje; celo-šablonové → „Obecné".
    d ..AddBoolParam(arr, "TISK_SIPKY",          "Tisknout šipky směru",      "true",  "Seznam zastávek")
    d ..AddBoolParam(arr, "TISK_PROJETYCH_ZAST", "Tisknout projeté zastávky", "false", "Seznam zastávek")

    // Časová osa odjezdů - popisné labely (Plný den vědomě vynechán, lze docílit přes Vlastní rozsah 0..23)
    s param = ##class(%DynamicObject).%New()
    s param.name         = "CASOVA_OSA"
    s param.label        = "Časová osa odjezdů"
    s param.type         = "enum"
    s param.group        = "Mřížka odjezdů"
    s param.defaultValue = "Jen hodiny s odjezdy"
    s opts = ##class(%DynamicArray).%New()
    d opts.%Push("Jen hodiny s odjezdy")
    d opts.%Push("Souvislá od první do poslední")
    d opts.%Push("Vlastní rozsah")
    s param.options = opts
    d arr.%Push(param)

    // Hodiny od/do - viditelné jen pro Vlastní rozsah; podporují wrap přes půlnoc (např. 22-5).
    s pOd = ##class(%DynamicObject).%New()
    s pOd.name         = "HODINY_OD"
    s pOd.label        = "Hodiny od"
    s pOd.type         = "number"
    s pOd.group        = "Mřížka odjezdů"
    s pOd.defaultValue = "4"
    s pOd.visibleWhen  = "CASOVA_OSA=Vlastní rozsah"
    d arr.%Push(pOd)

    s pDo = ##class(%DynamicObject).%New()
    s pDo.name         = "HODINY_DO"
    s pDo.label        = "Hodiny do"
    s pDo.type         = "number"
    s pDo.group        = "Mřížka odjezdů"
    s pDo.defaultValue = "23"
    s pDo.visibleWhen  = "CASOVA_OSA=Vlastní rozsah"
    d arr.%Push(pDo)

    // Roztáhnout data odjezdů (řádky + fonty) na výšku elementu, pokud je kratší rozsah hodin než kapacita.
    // Hlavička skupin dnů + sloupec hodin zůstávají původní velikost.
    d ..AddBoolParam(arr, "ROZTAHNOUT_NA_VYSKU", "Roztáhnout na výšku", "false", "Mřížka odjezdů")
}

/// ═══════════════════════════════════════════════════════════════════════════════
/// GetMockData - ukázková data pro preview
/// ═══════════════════════════════════════════════════════════════════════════════
/// Reálný dopravce linky: `DruhDopravceID` → vlastní firma (`$$gIdFa^MlJFapi`) / cizí dopravce
/// (`$$gDopFa^JRapi`). Firemní data žijí v systémovém NS (`sys("U","SystemDB")`), ne v datovém.
/// Defensivní - selhání legacy volání → prázdno.
///
/// ⚠️ Vrací **víc řádků** (název, ulice, město, telefon), ne "Dopravce: <název>" jako do 31. 8.
/// Zákazník uvození nechtěl a adresu ano (#13158, rozhodnutí Jan 31. 8.); tvar je shodný s AD
/// vývěskou, ať se dvě vývěsky vedle sebe neliší. Tím se to rozešlo se vzorem MHD2 `Export.cls`,
/// odkud tvar původně pocházel - je to vědomé, ne drift.
ClassMethod PopisDopravce(ByRef sys As %String, dataDB As %String, IdFa As %String, LinkaID As %String) As %String
{
    s radky = ""
    try {
        s sysnmsp = $g(sys("U","SystemDB"))
        s druh = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"DruhDopravceID"))
        i druh = 0 {
            // Vlastní firma (MlJFapiFa: 02 název, 03 upřesnění, 13 ulice, 14 město, 15 PSČ, 23 tel)
            k tmpArr  s err = $$gIdFa^MlJFapi(IdFa, sysnmsp, "", .tmpArr)
            i 'err {
                s nazev = $g(tmpArr("dta",2))
                i $g(tmpArr("dta",3)) '= "" s nazev = nazev _ $g(tmpArr("dta",3))
                s mesto = $zstrip($g(tmpArr("dta",15))_" "_$g(tmpArr("dta",14)), "<>W")
                s radky = ..RadkyDopravce(nazev, $g(tmpArr("dta",13)), mesto, $g(tmpArr("dta",23)))
            }
        } elseif druh = 1 {
            // Cizí dopravce (gDopFa: 1 sídlo, 2 ulice, 3 telefon, 7 obchodní název)
            s ciziFa = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"DopravceCiziFirmaID"))
            i ciziFa '= "" {
                k tmpArr  s err = $$gDopFa^JRapi(IdFa, dataDB, ciziFa, "", .tmpArr)
                i 'err s radky = ..RadkyDopravce($g(tmpArr("dta",7)), $g(tmpArr("dta",2)), $g(tmpArr("dta",1)), $g(tmpArr("dta",3)))
            }
        }
    } catch ex { s radky = "" }
    ret radky
}

/// Složí dopravce do řádků: název, ulice, sídlo/město, telefon. Prázdné položky se vynechají,
/// takže u dopravce bez adresy zbyde samotný název jako dřív.
ClassMethod RadkyDopravce(nazev As %String, ulice As %String, mesto As %String, tel As %String) As %String
{
    s nazev = $zstrip($g(nazev), "<>W")
    i nazev = "" q ""
    s text = nazev
    i $zstrip($g(ulice), "<>W") '= "" s text = text_$c(10)_$zstrip(ulice, "<>W")
    i $zstrip($g(mesto), "<>W") '= "" s text = text_$c(10)_$zstrip(mesto, "<>W")
    i $zstrip($g(tel), "<>W") '= ""   s text = text_$c(10)_"tel: "_$zstrip(tel, "<>W")
    q text
}

/// Vrátí ukázková data pro live preview v designeru.
/// Kompletní struktura se všemi kolekcemi a vnořenými poli.
ClassMethod GetMockData(ByRef sys As %String) As %Status
{
    s out = ##class(%DynamicObject).%New()

    // ── Skalární hodnoty ──────────────────────────────────────────────
    s vals = ##class(%DynamicObject).%New()
    d vals.%Set("CISLO_LINKY",            "42")
    d vals.%Set("LOKALNI_OZNACENI_LINKY", "A42")
    d vals.%Set("POPIS_LINKY",            "Líšeň–Bystrc")
    d vals.%Set("POPIS_DOPRAVCE",         "Dopravní podnik města Brna, a.s."_$c(10)_"Hlinky 151"_$c(10)_"656 46 Brno"_$c(10)_"tel: 543 171 111")
    d vals.%Set("POPIS_PLATNOSTI",        "01.01.2026 - 31.12.2026")
    d vals.%Set("DATUM_PLATNOSTI_OD",     "01.01.2026")
    d vals.%Set("DATUM_PLATNOSTI_DO",     "31.12.2026")
    d vals.%Set("SMER",                   "T")
    d vals.%Set("KONECNA_ZASTAVKA",             "Bystrc, zoo")
    d vals.%Set("TYP_JR",                 "")
    d vals.%Set("QR_KOD",                 "https://jr.example.com/42")
    d vals.%Set("POPIS_ZASTAVKA",         "Líšeň, Novolíšeňská")
    d vals.%Set("CISLO_ZASTAVKA",         "101-A")
    d out.%Set("values", vals)

    // ── Kolekce ─────────────────────────────────────────────────────────
    s rows = ##class(%DynamicObject).%New()

    // ZASTAVKY
    s zastavky = ##class(%DynamicArray).%New()
    s z1 = ..BuildZastavka("1", "Líšeň, Novolíšeňská", "101", 0, 1, 1, 0)
    d ..AddZona(z1, "A1")
    d ..AddAtribut(z1, "♿", "Bezbariérový přístup")
    d zastavky.%Push(z1)
    s z2 = ..BuildZastavka("2", "Líšeň, Mifkova", "102", 3, 2, 0, 1)
    d ..AddZona(z2, "A1") d ..AddPoznamkaZastavky(z2, "A", "text", "Končí v zast. Rudolfov")
    d zastavky.%Push(z2)
    s z3 = ..BuildZastavka("3", "Šlapanice", "103", 8, 3, 0, 0, 2)
    d ..AddZona(z3, "A2")
    d zastavky.%Push(z3)
    s z4 = ..BuildZastavka("4", "Královo Pole, nádraží", "104", 14, 4, 0, 0)
    d ..AddZona(z4, "A2") d ..AddAtribut(z4, "P", "Parkoviště P+R")
    d zastavky.%Push(z4)
    s z5 = ..BuildZastavka("5", "Bystrc, ZOO", "105", 22, 5, 0, 0)
    d ..AddZona(z5, "B1")
    d zastavky.%Push(z5)
    s z6 = ..BuildZastavka("6", "Bystrc, Ečerova", "106", 26, 6, 0, 1)
    d ..AddZona(z6, "B1") d ..AddPoznamkaZastavky(z6, "b", "text", "Zastávka na znamení v nočním provozu")
    d zastavky.%Push(z6)
    d rows.%Set("ZASTAVKY", zastavky)

    // SKUPINY_DNU
    s skupiny = ##class(%DynamicArray).%New()
    d skupiny.%Push(..BuildSkupinaDnu("1001", "PRACOVNÍ DNY", "mimo prázdniny", 1))
    d skupiny.%Push(..BuildSkupinaDnu("1002", "SOBOTY", "", 2))
    d skupiny.%Push(..BuildSkupinaDnu("1003", "NEDĚLE A SVÁTKY", "", 3))
    d skupiny.%Push(..BuildSkupinaDnu("1004", "PRÁZDNINY", "pracovní dny", 4))
    d skupiny.%Push(..BuildSkupinaDnu("1005", "NOČNÍ PROVOZ", "Pá+So", 5))
    d rows.%Set("SKUPINY_DNU", skupiny)

    // ODJEZDY
    s odjezdy = ##class(%DynamicArray).%New()
    s o1 = ..BuildOdjezd("1", "1001", 5, 15) d odjezdy.%Push(o1)
    s o2 = ..BuildOdjezd("1", "1001", 5, 30) d ..AddAnotace(o2, "text", "a") d odjezdy.%Push(o2)
    d odjezdy.%Push(..BuildOdjezd("1", "1001", 5, 45))
    d odjezdy.%Push(..BuildOdjezd("1", "1001", 6, 0))
    s o5 = ..BuildOdjezd("1", "1001", 6, 15) d ..AddAnotace(o5, "specialni", "♿") d odjezdy.%Push(o5)
    d odjezdy.%Push(..BuildOdjezd("1", "1001", 6, 30))
    s o7 = ..BuildOdjezd("1", "1001", 6, 45) d ..AddAnotace(o7, "nz", "22") d odjezdy.%Push(o7)
    d odjezdy.%Push(..BuildOdjezd("1", "1001", 7, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "1001", 7, 15))
    d odjezdy.%Push(..BuildOdjezd("1", "1001", 7, 30))
    d odjezdy.%Push(..BuildOdjezd("1", "1001", 7, 45))
    d odjezdy.%Push(..BuildOdjezd("1", "1001", 8, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "1001", 8, 20))
    d odjezdy.%Push(..BuildOdjezd("1", "1001", 8, 40))
    d odjezdy.%Push(..BuildOdjezd("1", "1001", 9, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "1001", 9, 30))
    d odjezdy.%Push(..BuildOdjezd("1", "1001", 10, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "1001", 10, 30))
    d odjezdy.%Push(..BuildOdjezd("1", "1001", 11, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "1001", 11, 30))
    d odjezdy.%Push(..BuildOdjezd("1", "1002", 7, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "1002", 8, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "1002", 9, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "1002", 10, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "1002", 11, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "1002", 12, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "1002", 13, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "1002", 14, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "1003", 8, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "1003", 10, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "1003", 12, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "1003", 14, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "1003", 16, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "1004", 6, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "1004", 7, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "1004", 8, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "1004", 9, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "1004", 10, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "1004", 11, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "1004", 12, 0))
    s on1 = ..BuildOdjezd("1", "1005", 23, 30) d odjezdy.%Push(on1)
    s on2 = ..BuildOdjezd("1", "1005", 0, 30) d ..AddAnotace(on2, "text", "a") d odjezdy.%Push(on2)
    d odjezdy.%Push(..BuildOdjezd("1", "1005", 1, 30))
    d odjezdy.%Push(..BuildOdjezd("1", "1005", 2, 30))
    d odjezdy.%Push(..BuildOdjezd("1", "1005", 3, 30))
    d rows.%Set("ODJEZDY", odjezdy)

    // POZNAMKY
    s poznamky = ##class(%DynamicArray).%New()
    d poznamky.%Push(..BuildPoznamka("specialni", "♿", "Spoj s bezbariérovým vozidlem"))
    d poznamky.%Push(..BuildPoznamka("specialni", "P", "Parkoviště P+R u zastávky"))
    d poznamky.%Push(..BuildPoznamka("nz", "22", "Nejede 24.12."))
    d poznamky.%Push(..BuildPoznamka("text", "a", "nezajíždí do zast. Pardubice"))
    d poznamky.%Push(..BuildPoznamka("text", "b", "zastávka na znamení v nočním provozu"))
    d poznamky.%Push(..BuildPoznamka("text", "A", "Končí v zast. Rudolfov"))
    d rows.%Set("POZNAMKY", poznamky)

    d out.%Set("rows", rows)
    s ^||ML("OUT", $i(^||ML("OUT"))) = out.%ToJSON()
    ret $$$OK
}

/// Vrátí ID první nezblokované skupiny linek pro danou IdFa.
/// Vrátí 0 pokud žádná aktivní skupina není.
/// Použito jako default pro Instance Picker - v budoucnu nahrazeno parametrem z Edisona.
ClassMethod GetDefaultSkupinaLinekID(IdFa As %String, dataDB As %String) As %Integer
{
    s id = ""
    for {
        s id = $o(^[dataDB]ALVA.MHD.ENUMS(IdFa,"SkupinyLinek","DTA",id))
        q:id=""
        if '+$g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"SkupinyLinek","DTA",id,"Blocked")) ret id
    }
    ret 0
}

}
