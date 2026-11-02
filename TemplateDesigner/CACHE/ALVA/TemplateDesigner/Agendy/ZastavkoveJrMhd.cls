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

Parameter OblastPrace = "JIZDNI_RADY";

Parameter Released = 1;

Parameter InstanceParamName = "LINKA_ID";

/// Od 30. 10. jako vývěska AD (`Jr.VariantyTras`); starý tisk MHD varianty neměl.
Parameter VariantyTras As %Boolean = 1;

Parameter ElementKind = "STOP";

Parameter InstanceTvary = "linka,linky,linku,linky,linek";

/// Vrátí schéma polí MHD agendy.
ClassMethod GetSchema(ByRef sys As %String) As %Status
{
    s err = ""
    try {
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
    } catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

/// Vrátí parametry sestavy pro MHD.
ClassMethod GetParameters(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        s arr = ##class(%DynamicArray).%New()

        // Linka se vybírá v Instance Pickeru (ne jako parametr sestavy) - viz GetAvailableInstances.
        // GetData dostává LINKA_ID z pickeru, ne z těchto parametrů.
        d ..AddStandardJRParameters(arr)

        s ^||ML("OUT", $i(^||ML("OUT"))) = arr.%ToJSON()
    } catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

/// Skupiny linek pro filtr výběru linek - jedno místo pro obě agendy MHD (`Jr.DataMhd`, `B56` S12).
ClassMethod GetAvailableSkupiny(ByRef sys As %String) As %Status
{
    q ##class(ALVA.TemplateDesigner.Agendy.Jr.DataMhd).GetAvailableSkupiny(.sys)
}

/// Linky pro výběr linek - jedno místo pro obě agendy MHD (`Jr.DataMhd`, `B56` S12).
ClassMethod GetAvailableInstances(ByRef sys As %String) As %Status
{
    q ##class(ALVA.TemplateDesigner.Agendy.Jr.DataMhd).GetAvailableInstances(.sys)
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
///   ("trasa",SmerID,BodID)                                     = $lb(ZastavkaID, CasJizdy, ZastavkaCislo)
///   ("trasa",SmerID,"slouceni",BodID,BodID2)                   = ""   konverzní tabulka sloučených bodů
///   ("trasa",SmerID,"slouceniInv",BodID2)                      = BodID hlavní bod po sloučení
///   ("trasa",SmerID,"netisknout",BodID)                        = ""   bod s příznakem „netisknout na zastávkovém JŘ"
///   ("poznamkyZast",SmerID,BodID,Znak)                         = $lb(TextPoznamky, TypPoznamky)
///   ("atributyZast",SmerID,BodID,AtributID)                    = $lb(Popis, Typ, SpecialniZnak, Znak)  (přestup: SpecialniZnak = písmeno A/L)
///   ("odjezdy",SmerID,BodID,SmoSloupec,Hodina,Minuta,SpojID)   = ""  (SmoSloupec 1-4 = sekce minut odjezdu dle SMOID)
///   ("smoSloupce",SmoSloupec)                                  = ""  (registr tiskových sloupců)
///   ("smoNadpis",SmoSloupec)                                   = $lb(nadpis1, nadpis2)
///   ("smoTypy",SmoSloupec,TypProvozuID)                        = počet spojů typu ve sloupci (detekce sloučení)
///   ("smoZnacka",TypProvozuID)                                 = $lb(znak, text)  značka typu mimo dominantní režim sloupce
///   ("spojTyp",SpojID)                                         = TypProvozuID spoje
///   ("spojTrasa",SmerID,SpojID)                                = TrasaBitmap
///   ("spojTrasa",SmerID,SpojID,BodID)                          = TrasaBitmapZBodu
///   ("prubeh",SmerID,SpojID,BodID)                             = $lb("S", čas) staví / $lb("<") projíždí - spoje se sloupcem minut, pro varianty tras
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
        d ..SestavitVyvesky(.sys, $g(^||ML("IN","LINKA_ID")))
    }
    catch (ex) { $$$CatchErrRest }
    k ^||TiskMHD                       // úklid working global i po chybě (self-healing kill je na začátku)
    ret $$$OK
}

/// Vývěsky jedné linky do `^||ML("OUT")`. Sdílí ji tisk (`GetData`) i náhled bez linky
/// (`GetMockData`), aby náhled ukazoval skutečná data stejné stavby; `jenPrvniStranka` vydá
/// jen první vývěsku (náhled nemusí počítat stránky celé linky).
ClassMethod SestavitVyvesky(ByRef sys As %String, LinkaID As %String, jenPrvniStranka As %Boolean = 0)
{
        $$$FillParams
        i 'LinkaID d ..ChybaDat("Není vybraná linka. Vyberte linky k tisku.")
        // Bez linky by níž $zdh prázdné platnosti spadl na <ILLEGAL VALUE> (`B194a` C11).
        i '$d(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID)) d ..ChybaDat("Vybraná linka v modulu MHD není, mohla být smazána. Vyberte linky znovu.")

        k ^||TiskMHD
        d ..ZacitDuvodyPrazdne()

        // ── Příprava konfigurace ────────────────────────────────────────
        s TrasaID = ##class(ALVA.MHD.LinkyFce).GetTrasaId(IdFa, dataDB, LinkaID)
        s platnostOd = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"PlatiOd"))
        s platnostDo = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"PlatiDo"))
        s VariantaChronometrazeID = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"VariantaChronometrazeID"))
        s platnostOdHrl = $zdh(platnostOd, 3)
        s platnostDoHrl = $zdh(platnostDo, 3)
        s PlatnostChronometrazeID = ##class(ALVA.MHD.ChronometrazeFce).GetPlatnostChronometrazeID(IdFa, dataDB, VariantaChronometrazeID, platnostOdHrl)

        // Linka bez nastavené trasy - fáze 1 by padla na <SUBSCRIPT>. Důvod pro uživatele jako u linkového
        // JŘ MHD, ne chyba: ta by při tisku víc linek zastavila i ty s daty (`B193g`, do 23. 10. ThrowStatus).
        if TrasaID = "" {
            d ..PrazdnaStranka("Linka nemá zadanou trasu. Doplňte trasu u linky v modulu MHD.")
            d ..VydatDuvodPrazdne()
            q
        }

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
        s ^||TiskMHD("config","jenPrvniStranka") = jenPrvniStranka

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
        // Vývěska bez odjezdů se vynechává (konečná) - linka bez jediné stránky je ta, odkud nejede nic odnikud.
        s maSpoje = ($o(^[dataDB]ALVA.MHD.LSO(IdFa,2,"IDX",1,1,1,LinkaID,"")) '= "")
        d ..VydatDuvodPrazdne($s('maSpoje:"Linka nemá žádné spoje. Doplňte spoje linky v modulu MHD.", 1:"Z žádné zastávky linky neodjíždí žádný spoj. Zkontrolujte spoje a jízdní doby linky v modulu MHD."))
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
                s Znak = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"Atribut","DTA",AtributID,"Znak"))
                s ^||TiskMHD("atributyZast",SmerID,BodID,AtributID) = $lb(Popis, 2, ..ZnakAtributu(SpecialniZnak, Znak), Znak)
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
                    s pForeColor = ##class(ALVA.TemplateDesigner.Agendy.Jr.VystupZastavkovy).RgbToHex($g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"PoznamkaSpoj","DTA",PoznamkaSpojID,"TiskJRMHDVozoveBarvaZnakuRGB")))
                    s pBackColor = ##class(ALVA.TemplateDesigner.Agendy.Jr.VystupZastavkovy).RgbToHex($g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"PoznamkaSpoj","DTA",PoznamkaSpojID,"TiskJRMHDVozoveBarvaPozadiRGB")))
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
                    s Znak = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"Atribut","DTA",AtributID,"Znak"))
                    s ^||TiskMHD("atributyZast",SmerID,BodID,AtributID) = $lb(Popis, 2, ..ZnakAtributu(SpecialniZnak, Znak), Znak)
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
    // Omezení ze dvou bitmap (legacy LinkyPrintFormatMHD :458). Do 12. 10. tu chybělo, takže
    // text omezení vyšel prázdný a automaticky přidělené negativní značky ve vývěskách chyběly.
    s chyba = $$mkOmzFromOnlyTwoBitmaps^JRapi(IdFa,dataDB,.arri,.arr)
    i +chyba q 0

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
            s casOdjezdu = "", projizdi = 0
            s subBodID = ""
            f {
                s subBodID = $o(^||TiskMHD("trasa",SmerID,"slouceni",BodID,subBodID), smerKrok) q:subBodID=""
                i subBodID = konecnyBod s jeKonecny = 1
                i '$d(tabulkaSpoje("arr",1,subBodID)) continue
                s bodVal = $g(tabulkaSpoje("arr",1,subBodID))
                // Průjezd → přeskočit (varianty tras ho ale tisknou jako „<")
                i $lg(bodVal,5) {
                    s projizdi = 1
                    continue
                }
                // Čas = max(příjezd, odjezd). Prázdno se porovnává zvlášť: 0:00 není větší než prázdný řetězec,
                // takže odjezd o půlnoci by zmizel (starý tisk ho vynechává, linka 53 CB_DP Nádraží).
                s prijezd = $lg(bodVal,3), odjezd = $lg(bodVal,4)
                s cas = $s(prijezd="":odjezd, odjezd="":prijezd, odjezd>prijezd:odjezd, 1:prijezd)
                i cas = "" continue
                i (casOdjezdu = "") || (cas > casOdjezdu) s casOdjezdu = cas
            }

            i casOdjezdu '= "" {
                s $e(TrasaBitmap,BodID) = 1
                i 'jeKonecny {
                    s hh = casOdjezdu \ 60, mm = casOdjezdu # 60
                    i smoCol '= "" s ^||TiskMHD("odjezdy",SmerID,BodID,smoCol,hh,mm,SpojID) = ""
                    s ^||TiskMHD("obsluhaKonec",SmerID,BodID,SpojID) = ""
                }
            }
            // Průběh spoje pro varianty tras - jen spoje se sloupcem minut, ostatní se na vývěsce netisknou.
            i smoCol '= "" {
                i casOdjezdu '= "" { s ^||TiskMHD("prubeh",SmerID,SpojID,BodID) = $lb("S", casOdjezdu) }
                elseif projizdi { s ^||TiskMHD("prubeh",SmerID,SpojID,BodID) = $lb("<") }
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

    // Počet obsluhovaných zastávek je údaj celého směru, ale stránky se emitují prokládaně
    // po zastávkách - musí se proto spočítat dopředu, než se začne tisknout.
    k pocetZastavek, vsechnyBody
    f SmerID = 0,1 {
        s pocetZastavek(SmerID) = 0
        i '$d(^||TiskMHD("trasa",SmerID)) continue
        s BodID = ""
        f {
            s BodID = $o(^||TiskMHD("trasa",SmerID,BodID)) q:BodID=""
            i 'BodID continue           // pomocné subscripty (slouceni, slouceniInv, netisknout)
            s vsechnyBody(BodID) = ""
            i '$d(^||TiskMHD("odjezdy",SmerID,BodID)) continue
            s ObsluhaBitmap = $g(^||TiskMHD("obsluha",SmerID,BodID))
            i '$e(ObsluhaBitmap, BodID) continue
            s pocetZastavek(SmerID) = pocetZastavek(SmerID) + 1
        }
    }

    // ⚠️ Výchozí pořadí má vnější smyčku ZASTÁVKA (bod trasy vzestupně = pořadí ve směru Tam)
    // a vnitřní SMĚR - obě vývěsky téže zastávky musí jít v PDF za sebou, aby se daly vytisknout
    // a pověsit na oba označníky jedním výjezdem. Tak to dělá starý tisk
    // (`JRapiLinPrtMhd1.mac:397-398`, Tc vnější, Smer vnitřní) i zastávkový JŘ z AD dat.
    // `B161`: druhá volba prohodí smyčky a zpáteční směr projde odzadu. Obě agendy musí nabízet
    // totéž, jinak by se tisk choval v každé jinak.
    i $g(^||ML("IN","PORADI_STRANEK")) = "Po směrech (tam vzestupně, zpět sestupně)" {
        f SmerID = 0,1 {
            s BodID = ""
            f {
                s BodID = $s(SmerID:$o(vsechnyBody(BodID),-1), 1:$o(vsechnyBody(BodID)))  q:BodID=""
                d ..VydatStranku(.sys, SmerID, BodID, .pocetZastavek)
            }
        }
    }
    else {
        s BodID = ""
        f {
            s BodID = $o(vsechnyBody(BodID)) q:BodID=""
            f SmerID = 0,1 {
                d ..VydatStranku(.sys, SmerID, BodID, .pocetZastavek)
            }
        }
    }
}

/// Vydá stránku jedné zastávky v jednom směru, pokud tam vůbec zastavuje. Volá se ze dvou různých
/// pořadí stránek (`B161`), proto samostatně - podmínky musí být v obou shodné.
ClassMethod VydatStranku(ByRef sys As %String, SmerID As %Integer, BodID As %String, ByRef pocetZastavek)
{
    i $g(^||TiskMHD("config","jenPrvniStranka")) && $d(^||ML("OUT",1)) ret
    i '$d(^||TiskMHD("trasa",SmerID,BodID)) ret
    // Zastávka nemá odjezdy → přeskočit
    i '$d(^||TiskMHD("odjezdy",SmerID,BodID)) ret
    s ObsluhaBitmap = $g(^||TiskMHD("obsluha",SmerID,BodID))
    i '$e(ObsluhaBitmap, BodID) ret

    s page = ..SestavitJednuStranku(.sys, SmerID, BodID, $g(pocetZastavek(SmerID)))
    s ^||ML("OUT",$i(^||ML("OUT"))) = page.%ToJSON()
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
    s LinkaID = $g(^||TiskMHD("config","LinkaID"))
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
    // Hlavičku linky, kterou posílají obě agendy MHD stejně, skládá Jr.DataMhd (`B56` S12).
    s vals = ##class(%DynamicObject).%New()
    d ##class(ALVA.TemplateDesigner.Agendy.Jr.DataMhd).AddHlavickaLinky(.sys, dataDB, IdFa, LinkaID, vals)
    // Popis dopravce na řádky pod název linky (jako vývěska AD); NAZEV_DOPRAVCE z hlavičky jde do úzkého rámečku záhlaví.
    d vals.%Set("POPIS_DOPRAVCE",         ##class(ALVA.TemplateDesigner.Agendy.Jr.DataMhd).PopisDopravce(.sys, dataDB, IdFa, LinkaID))
    // Jen pro starší šablony - v nabídce dat už není (Jan 23. 10.), tvar se proto nemění.
    d vals.%Set("POPIS_PLATNOSTI",        vals.%Get("DATUM_PLATNOSTI_OD")_" - "_vals.%Get("DATUM_PLATNOSTI_DO"))
    d vals.%Set("SMER",                   smerPopis)   // T/Z (A1); KONECNA_ZASTAVKA = konečná se nastaví po sestavení trasy
    d vals.%Set("POPIS_ZASTAVKA",         NazevZastavky)
    d vals.%Set("CISLO_ZASTAVKA",         CisloZastavky_"-"_Oznacnik)
    // TITULEK / POZNAMKA_POD_DOPRAVCE / INFORMACE: MHD pro ně nemá zdroj → nejsou ve schématu (viz AD, které je má z JRMAIN)
    // PRVNI_POSLEDNI_SPOJ (text prvního/posledního spoje) odstraněno 2026-07-20 - nepoužívané a drahé:
    // legacy MHD2 CalculatePrvniAndPosledniSpojeText se počítalo per stránka (~6,8 s z 7 s na velké lince,
    // full sken spojů + den-po-dni platnost). Kdyby ho někdo chtěl, reimplementovat nad daty fáze 4 v ^||TiskMHD.
    d vals.%Set("VARIANTY_TRAS",          ..BuildVariantyTras(SmerID, aktualniZastBodID, ObsluhaBitmap, .varianty, .pismenoSpoje))
    d page.%Set("values", vals)

    // ── Kolekce ──────────────────────────────────────────────────────────
    s rows = ##class(%DynamicObject).%New()

    // ZASTAVKY - vždy všechny v daném směru, s příznakem JE_AKTUALNI
    s zastavky = ##class(%DynamicArray).%New()
    s poradiZast = 0
    s konecnaNazev = ""                       // poslední zastávka ve směru = konečná (→ KONECNA_ZASTAVKA, A1)
    s vychoziNazev = ""                       // první vytištěná zastávka směru (→ VYCHOZI_ZASTAVKA, jako vývěska AD)
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

        s z = ##class(ALVA.TemplateDesigner.Agendy.Jr.VystupZastavkovy).BuildZastavka(bodIterator, zastNazev, zastCislo, casJizdy, poradiZast, jeAktualni, naZnameni, odsazeni)

        // Zóny
        s zonaidx = ""
        f {
            s zonaidx = $o(^[dataDB]ALVA.MHD.ZST(IdFa,1,"DTA",zastID,"DTA",1,"ZonyID",zonaidx)) q:zonaidx=""
            s ZonaID = $g(^[dataDB]ALVA.MHD.ZST(IdFa,1,"DTA",zastID,"DTA",1,"ZonyID",zonaidx,"Itm"))
            s ZonaCislo = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"Zony","DTA",ZonaID,"Cislo"))
            i ZonaCislo '= "" d ##class(ALVA.TemplateDesigner.Agendy.Jr.VystupZastavkovy).AddZona(z, ZonaCislo)
        }

        // Atributy zastávky (jen neprojete + aktuální jen N,V,x)
        i ((SmerID=0)&&(bodIterator'<aktualniZastBodID)) || ((SmerID=1)&&(bodIterator'>aktualniZastBodID)) {
            s AtributID = ""
            f {
                s AtributID = $o(^||TiskMHD("atributyZast",SmerID,bodIterator,AtributID)) q:AtributID=""
                // Aktuální zastávka: jen základní atributy (ID<=1003)
                i (bodIterator=aktualniZastBodID)&&(AtributID>1003) continue
                s atrVal = $g(^||TiskMHD("atributyZast",SmerID,bodIterator,AtributID))
                s SpecZnak = ##class(ALVA.TemplateDesigner.Agendy.Jr.VystupZastavkovy).ConvertSpecialniZnak($lg(atrVal,3))
                d ##class(ALVA.TemplateDesigner.Agendy.Jr.VystupZastavkovy).AddAtribut(z, SpecZnak, $lg(atrVal,1))
            }
        }

        // Poznámky zastávky (jen neprojete)
        i ((SmerID=0)&&(bodIterator>aktualniZastBodID)) || ((SmerID=1)&&(bodIterator<aktualniZastBodID)) {
            k znackyZastavky
            s znakPoznamky = ""
            f {
                s znakPoznamky = $o(^||TiskMHD("poznamkyZast",SmerID,bodIterator,znakPoznamky)) q:znakPoznamky=""
                s pozVal = $g(^||TiskMHD("poznamkyZast",SmerID,bodIterator,znakPoznamky))
                d ##class(ALVA.TemplateDesigner.Agendy.Jr.VystupZastavkovy).AddPoznamkaZastavky(z, znakPoznamky, "text", $lg(pozVal,1))
                s znackyZastavky(znakPoznamky) = ""
            }
            // Písmeno rozdílu trasy (alg. 1002) i u zastávky, kterou obsluhují jen spoje s tímto
            // písmenem - bez něj vysvětlivka „zastávky obsluhují pouze shodně označené spoje" nemá
            // na co ukázat. Jen „zastavuje navíc", u „končí dříve" zastávku legacy neznačí
            // (LinkyPrintFormatMHD.cls:764-789).
            i AutoPoznamkyRozdilTrasy && (AutoPoznamkyRozdilTrasyAlgoritmus = 1002) {
                s DiffBitmap = ""
                f {
                    s DiffBitmap = $o(^||TiskMHD("rozdilTrasyPism",SmerID,aktualniZastBodID,0,DiffBitmap), 1, pismeno) q:DiffBitmap=""
                    i '+$e(DiffBitmap, bodIterator) continue
                    i (pismeno = "") || $d(znackyZastavky(pismeno)) continue
                    d ##class(ALVA.TemplateDesigner.Agendy.Jr.VystupZastavkovy).AddPoznamkaZastavky(z, pismeno, "text", "zastávky obsluhují pouze shodně označené spoje")
                    s znackyZastavky(pismeno) = ""
                }
            }
        }

        s konecnaNazev = zastNazev
        s:vychoziNazev="" vychoziNazev = zastNazev
        s oznacnikRow = $g(^[dataDB]ALVA.MHD.ZST(IdFa,1,"DTA",zastID,"DTA",1,"Oznacnik"))
        d z.%Set("NASTUPISTE", oznacnikRow)   // TR3: nástupiště per zastávka (MHD = Oznacnik)
        d z.%Set("TRASA_MIN", ##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).BuildTrasaMin(.varianty, bodIterator))
        // Označník varianty: spoje MHD staví na zastávce u téhož označníku, takže tam, kde varianta staví, je to označník řádku.
        s oznacnikyVariant = ##class(%DynamicArray).%New()
        f n=1:1:##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).PocetSloupcu(.varianty) {
            d oznacnikyVariant.%Push($s(##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).MaZastaveni(.varianty, n, bodIterator):oznacnikRow, 1:""))
        }
        d z.%Set("NASTUPISTE_VARIANT", oznacnikyVariant)
        d zastavky.%Push(z)
    }
    d rows.%Set("ZASTAVKY", zastavky)
    d vals.%Set("VYCHOZI_ZASTAVKA", vychoziNazev)
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
        d skupiny.%Push(##class(ALVA.TemplateDesigner.Agendy.Jr.VystupZastavkovy).BuildSkupinaDnu(smoCol, $lg(nadpisLb,1), $lg(nadpisLb,2), smoCol))
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

                    s odj = ##class(ALVA.TemplateDesigner.Agendy.Jr.VystupZastavkovy).BuildOdjezd(aktualniZastBodID, smoCol, hh, mm)

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
                        s pForeColor = ##class(ALVA.TemplateDesigner.Agendy.Jr.VystupZastavkovy).RgbToHex($g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"PoznamkaSpoj","DTA",poznamkaID,"TiskJRMHDVozoveBarvaZnakuRGB")))
                        s pBackColor = ##class(ALVA.TemplateDesigner.Agendy.Jr.VystupZastavkovy).RgbToHex($g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"PoznamkaSpoj","DTA",poznamkaID,"TiskJRMHDVozoveBarvaPozadiRGB")))
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
                        s specZnak = ##class(ALVA.TemplateDesigner.Agendy.Jr.VystupZastavkovy).ConvertSpecialniZnak(specZnak)
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

                    // Písmeno varianty trasy vedle písmene rozdílu, jako ve vývěsce AD. Vysvětluje ho záhlaví
                    // sloupce variant, ne poznámka; bez toho sloupce ho klient podle kategorie schová.
                    i $g(pismenoSpoje(SpojID)) '= "" d ..AddAnotace(odj, "text", pismenoSpoje(SpojID), "", "", "varianta-trasy")

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
                        s pForeColor = ##class(ALVA.TemplateDesigner.Agendy.Jr.VystupZastavkovy).RgbToHex($g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"PoznamkaSpoj","DTA",poznamkaID,"TiskJRMHDVozoveBarvaZnakuRGB")))
                        s pBackColor = ##class(ALVA.TemplateDesigner.Agendy.Jr.VystupZastavkovy).RgbToHex($g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"PoznamkaSpoj","DTA",poznamkaID,"TiskJRMHDVozoveBarvaPozadiRGB")))
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
        d poznamky.%Push(##class(ALVA.TemplateDesigner.Agendy.Jr.VystupZastavkovy).BuildPoznamka("text", znakPoznamky, $lg(pozVal,1), $lg(pozVal,3), $lg(pozVal,4)))
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
        d poznamky.%Push(##class(ALVA.TemplateDesigner.Agendy.Jr.VystupZastavkovy).BuildPoznamka(pTyp, pZnak, pText, $lg(pVal,4), $lg(pVal,5)))
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
            s SpecZnak = ##class(ALVA.TemplateDesigner.Agendy.Jr.VystupZastavkovy).ConvertSpecialniZnak($lg(atrVal,3))
            i SpecZnak = "" continue
            i $d(poznamkyAtribut(SpecZnak)) continue
            s poznamkyAtribut(SpecZnak) = ""
            // „na znamení" řádek otagovat (rozpoznání dle popisu, konzistentně s TD IsNaZnameniAttr;
            // symbol se liší dle firmy) → přepínač „Na znamení" schová značku i tento řádek legendy (#7161).
            s popisAtr = $lg(atrVal,1)
            s katAtr = $s($zcvt($e(popisAtr,1,9),"L")="na znamen":"atribut-zastavky", ..JePrestup($lg(atrVal,4)):"prestup", 1:"")
            d poznamky.%Push(##class(ALVA.TemplateDesigner.Agendy.Jr.VystupZastavkovy).BuildPoznamka("specialni", SpecZnak, popisAtr, "", "", katAtr))
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
/// Výstupní JSON - společná stavba vývěsky je v `Jr.VystupZastavkovy`, tady jen to, co má jen MHD
/// ═══════════════════════════════════════════════════════════════════════════════
/// `kategorie` jen u písmene varianty trasy: podle ní ho klient schová. Ostatní značky MHD kategorii
/// neposílají, jinak by je filtr Poznámek podle kategorie začal schovávat (`legacy-nahrada.md §4`).
ClassMethod AddAnotace(odjezd As %DynamicObject, typ As %String, znak As %String, foreColor As %String = "", backColor As %String = "", kategorie As %String = "")
{
    s anotace = ##class(%DynamicObject).%New()
    d anotace.%Set("TYP", typ)
    d anotace.%Set("ZNAK", znak)
    d anotace.%Set("FORE_COLOR", foreColor)
    d anotace.%Set("BACK_COLOR", backColor)
    i kategorie '= "" d anotace.%Set("KATEGORIE", kategorie)
    d odjezd.%Get("ANOTACE").%Push(anotace)
}

/// Varianty tras vývěsky (`Jr.VariantyTras`, jako vývěska AD): sled zobrazených zastávek od vývěskové
/// dál podle průběhu spoje z fáze 4. Vrátí `VARIANTY_TRAS`; `varianty` pak plní řádky zastávek
/// a `pismenoSpoje(SpojID)` značku u minuty.
///
/// Zástupce varianty je **spoj** s nejdelší jízdou, ne trasa jako v AD: v MHD má časy každý spoj zvlášť
/// a spoje téže varianty jedou různě dlouho (sonda `VariantyTrasMhdProbe` 30. 10.: až o 16 min v CB_DP).
/// Starý tisk MHD varianty neměl, takže se není s čím porovnat (R6) - ověřené jsou jen vlastnosti výstupu.
ClassMethod BuildVariantyTras(SmerID As %Integer, aktualniZastBodID As %String, ObsluhaBitmap As %String, ByRef varianty, ByRef pismenoSpoje) As %DynamicArray
{
    s smerKrok = $s(SmerID:-1, 1:1), jeTam = 'SmerID
    k varianty, pismenoSpoje
    s SpojID = ""
    f {
        s SpojID = $o(^||TiskMHD("obsluhaKonec",SmerID,aktualniZastBodID,SpojID)) q:SpojID=""
        s naVyvesce = $g(^||TiskMHD("prubeh",SmerID,SpojID,aktualniZastBodID))
        i $lg(naVyvesce,1) '= "S" continue          // spoj bez sloupce minut se na vývěsce netiskne
        // Sled jen přes zastávky, které stránka ukazuje (jako seznam zastávek): bez skrytých bodů
        // a bez těch, kam odtud nic nejede - jinak by se spoje lišily v řádku, který nikdo nevidí.
        s sled = "", delka = 0, BodID = ""
        f {
            s BodID = $s(BodID="":aktualniZastBodID, 1:$o(^||TiskMHD("trasa",SmerID,BodID), smerKrok)) q:(BodID="")||('BodID)
            i (BodID '= aktualniZastBodID) && ($d(^||TiskMHD("trasa",SmerID,"netisknout",BodID)) || '$e(ObsluhaBitmap,BodID)) continue
            s prubeh = $g(^||TiskMHD("prubeh",SmerID,SpojID,BodID))
            s $p(sled,"~",BodID) = $lg(prubeh,1)
            i $lg(prubeh,1) = "S" {
                s doba = ..CasOdVyvesky($lg(prubeh,2), $lg(naVyvesce,2), 1)
                i doba > delka s delka = doba
            }
        }
        d ##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).AddSpoj(.varianty, SpojID, sled, delka, SpojID)
    }
    // Výchozí sada písmen, MHD nastavení u linky nemá (Jan 31. 10.: řešit, až to někdo nahlásí).
    s pismena = ##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).BuildSloupce(.varianty, "", .pismenoSpoje)
    // Z průběhu zástupce vyjde i úsek před vývěskou (projeté zastávky), jako v AD.
    f n=1:1:##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).PocetSloupcu(.varianty) {
        s zastupce = ##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).ZastupceSloupce(.varianty, n)
        s casVyvesky = $lg($g(^||TiskMHD("prubeh",SmerID,zastupce,aktualniZastBodID)),2)
        s BodID = ""
        f {
            s BodID = $o(^||TiskMHD("prubeh",SmerID,zastupce,BodID), 1, prubeh) q:BodID=""
            s doba = $s($lg(prubeh,1)="S":..CasOdVyvesky($lg(prubeh,2), casVyvesky, ##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).JeVUsekuVariant(BodID, aktualniZastBodID, jeTam)), 1:"")
            d ##class(ALVA.TemplateDesigner.Agendy.Jr.VariantyTras).AddZastaveni(.varianty, n, BodID, $lg(prubeh,1), doba)
        }
    }
    q pismena
}

/// Jízdní doba z vývěskové zastávky na zastávku s časem `cas` (minuty od půlnoci). Za vývěskou je
/// kladná, před ní (projetá zastávka) záporná jako v AD. Spoj přes půlnoc má za ní menší čas,
/// proto se doba s nesprávným znaménkem posune o den.
ClassMethod CasOdVyvesky(cas As %Integer, casVyvesky As %Integer, zaVyveskou As %Boolean) As %Integer
{
    s doba = cas - casVyvesky
    i zaVyveskou && (doba < 0) s doba = doba + 1440
    i 'zaVyveskou && (doba > 0) s doba = doba - 1440
    q doba
}

/// Přestup na autobus („A") a vlak („L") jde písmenem jako ve vývěsce AD a v linkovém JŘ - návrhář ho
/// přeloží na piktogram a řídí ho volba „Přestupy". Kód starého fontu (119 / 120) by nakreslil vagon
/// a „x", které se tiskne textem jako „na znamení". Ostatní atributy zůstávají kódem fontu, ten sedí.
ClassMethod ZnakAtributu(specialniZnak As %String, znak As %String) As %String
{
    q $s(..JePrestup(znak):znak, 1:specialniZnak)
}

ClassMethod JePrestup(znak As %String) As %Boolean
{
    q (znak = "A") || (znak = "L")
}

/// ═══════════════════════════════════════════════════════════════════════════════
/// Nabídka dat - všechna pole vývěsky MHD jsou ze slovníku `Jr.NabidkaDat`
/// ═══════════════════════════════════════════════════════════════════════════════
/// Přidá standardní skalární pole JŘ agendy do pole fields (pořadí shodně s vývěskou AD).
ClassMethod AddStandardJRFields(fields As %DynamicArray)
{
    d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, "CISLO_LINKY")
    d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, "LOKALNI_OZNACENI_LINKY")
    d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, "POPIS_LINKY")
    d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, "TYP_JR")
    d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, "POPIS_DOPRAVCE")
    d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, "NAZEV_DOPRAVCE")
    d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, "ADRESA_DOPRAVCE")
    d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, "TELEFON_DOPRAVCE")
    d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, "EMAIL_DOPRAVCE")
    d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, "ICO_DOPRAVCE")
    d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, "DATUM_PLATNOSTI_OD")
    d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, "DATUM_PLATNOSTI_DO")
    d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, "POPIS_ZASTAVKA")
    // V MHD nese číslo i s označníkem („101-A"), proto jiný popisek než týž klíč ve vývěsce AD (Jan 30. 10.).
    d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, "CISLO_ZASTAVKA", "Číslo a označník aktuální zastávky")
    d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, "SMER")
    d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, "VYCHOZI_ZASTAVKA")
    d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, "KONECNA_ZASTAVKA")
    d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, "QR_KOD")
}

/// Přidá standardní tabulkové kolekce JŘ agendy do pole collections.
ClassMethod AddStandardJRCollections(collections As %DynamicArray)
{
    s pole = ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddKolekce(collections, "ZASTAVKY")
    d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPoleKolekce(pole, "ZASTAVKY", "NAZEV_ZASTAVKY")
    d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPoleKolekce(pole, "ZASTAVKY", "CAS_JIZDY")
    d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPoleKolekce(pole, "ZASTAVKY", "PORADI")
    d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPoleKolekce(pole, "ZASTAVKY", "AKTUALNI_ZASTAVKA")
    d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPoleKolekce(pole, "ZASTAVKY", "ZONY")
    d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPoleKolekce(pole, "ZASTAVKY", "NASTUPISTE")

    s pole = ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddKolekce(collections, "SKUPINY_DNU")
    d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPoleKolekce(pole, "SKUPINY_DNU", "NADPIS")
    d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPoleKolekce(pole, "SKUPINY_DNU", "NADPIS2")
    d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPoleKolekce(pole, "SKUPINY_DNU", "PORADI")

    s pole = ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddKolekce(collections, "ODJEZDY")
    d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPoleKolekce(pole, "ODJEZDY", "HODINA")
    d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPoleKolekce(pole, "ODJEZDY", "MINUTA")

    s pole = ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddKolekce(collections, "POZNAMKY")
    d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPoleKolekce(pole, "POZNAMKY", "ZNAK")
    d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPoleKolekce(pole, "POZNAMKY", "TEXT")
}

/// Přidá standardní JŘ parametry do pole parametrů.
ClassMethod AddStandardJRParameters(arr As %DynamicArray)
{
    // `group` = vizuální seskupení v liště parametrů / dialogu (aditivní klíč - starší klient ignoruje).
    // Konvence: skupina = UI název elementu, který parametr ovlivňuje; celo-šablonové → „Obecné".
    // Jen parametry společné s vývěskou AD (`Jr.Parametry`); vlastní parametry vývěska MHD nemá.
    d arr.%Push(##class(ALVA.TemplateDesigner.Agendy.Jr.Parametry).ParametrPoradiStranek())
    d ##class(ALVA.TemplateDesigner.Agendy.Jr.Parametry).AddSipkyAProjeteZastavky(arr)
    d ##class(ALVA.TemplateDesigner.Agendy.Jr.Parametry).AddVariantyTras(arr)
    d ##class(ALVA.TemplateDesigner.Agendy.Jr.Parametry).AddCasovaOsaOdjezdu(arr)
    d ##class(ALVA.TemplateDesigner.Agendy.Jr.Parametry).AddRoztazeniNaVysku(arr)
}

/// ═══════════════════════════════════════════════════════════════════════════════
/// GetMockData - ukázková data pro preview
/// ═══════════════════════════════════════════════════════════════════════════════
/// Ukázková data pro live preview: první vývěska první linky výchozí skupiny, stejně jako ostatní
/// agendy JŘ berou první skutečnou linku. Vymyšlená ukázka se od dat lišila stavbou (neměla označník,
/// nesla „na znamení", které data nemají) a do PDF z ukázky se tiskla cizí linka.
ClassMethod GetMockData(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        s skupinaLinek = ##class(ALVA.TemplateDesigner.Agendy.Jr.DataMhd).GetDefaultSkupinaLinekID(IdFa, dataDB)
        s linkaId = $o(^[dataDB]ALVA.MHD.LSO(IdFa,1,"IDX",1,1,1,skupinaLinek,""))
        i linkaId '= "" d ..SestavitVyvesky(.sys, linkaId, 1)
        // Ukázka je jedna stránka; důvod prázdné linky patří tisku, náhled ho říká záložním textem.
        k ^||ML("OUT","DUVOD_PRAZDNE")

        i '$d(^||ML("OUT",1)) {
            s page = ##class(%DynamicObject).%New()
            s v = ##class(%DynamicObject).%New()
            d v.%Set("POPIS_LINKY", ..TextUkazkyBezDat())
            d page.%Set("values", v)
            s r = ##class(%DynamicObject).%New()
            d r.%Set("ZASTAVKY", ##class(%DynamicArray).%New())
            d r.%Set("SKUPINY_DNU", ##class(%DynamicArray).%New())
            d r.%Set("ODJEZDY", ##class(%DynamicArray).%New())
            d r.%Set("POZNAMKY", ##class(%DynamicArray).%New())
            d page.%Set("rows", r)
            s ^||ML("OUT", $i(^||ML("OUT"))) = page.%ToJSON()
        }
    }
    catch (ex) { $$$CatchErrRest }
    k ^||TiskMHD
    ret $$$OK
}

}
