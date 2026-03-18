Include (ALVAMHD, GENERAL)

/// Konkrétní implementace AgendaJRAPI pro MHD jízdní řády.
/// Refaktor z legacy ALVA.MHD.LinkyPrintFormatMHD.MakeData() — čistý JSON výstup.
/// 
/// appsettings.json:
///   { "code": "MHD", "name": "MHD Jízdní řád", "cacheClass": "ALVA.TemplateDesigner.API.TiskMHD" }
/// 
/// GetData vstup:  IN["LINKA_ID"] = číslo linky
/// GetData výstup: OUT[1] = JSON pole stránek, každá stránka = { "values": {...}, "rows": {...} }
///                 Jedna stránka = jedna zastávka × jeden směr
/// 
/// Wire formát: viz schedule-elements-architecture.md (ZASTAVKY, SKUPINY_DNU, ODJEZDY, POZNAMKY)
/// 
/// Pracovní globál ^||TiskMHD (process-private, kilnuto na začátku i konci GetData):
///   ("trasa",SmerID,BodID)                                     = $lb(ZastavkaID, CasJizdy, ZastavkaCislo)
///   ("trasa",SmerID,"slouceni",BodID,BodID2)                   = ""   konverzní tabulka sloučených bodů
///   ("trasa",SmerID,"slouceniInv",BodID2)                      = BodID hlavní bod po sloučení
///   ("poznamkyZast",SmerID,BodID,Znak)                         = $lb(TextPoznamky, TypPoznamky)
///   ("atributyZast",SmerID,BodID,AtributID)                    = $lb(Popis, Typ, SpecialniZnak)
///   ("odjezdy",SmerID,BodID,TypProvozuID,Hodina,Minuta,SpojID) = ""
///   ("typyProvozu",TypProvozuID)                               = ""
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
Class ALVA.TemplateDesigner.API.TiskMHD Extends ALVA.TemplateDesigner.API.AgendaJRAPI
{

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
    q 1
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
    q 1
}

/// Vrátí definici Fixed šablony.
ClassMethod GetTableTemplate(ByRef sys As %String) As %Status
{
    s key = $g(^||ML("IN", "key"))

    if key = "ZAHLAVI_LINKY" {
        d ..WriteTemplate2Col([
            ["Linka",       "{CISLO_LINKY}"],
            ["Označení",    "{LOKALNI_OZNACENI_LINKY}"],
            ["Směr",        "{POPIS_SMER}"],
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

    q 1
}

/// Vrátí parametry sestavy pro MHD.
ClassMethod GetParameters(ByRef sys As %String) As %Status
{
    s arr = ##class(%DynamicArray).%New()

    // Vstupní parametr — ID linky (povinný pro GetData)
    s param = ##class(%DynamicObject).%New()
    s param.name = "LINKA_ID"
    s param.label = "ID linky"
    s param.type = "string"
    s param.defaultValue = ""
    d arr.%Push(param)

    d ..AddStandardJRParameters(arr)

    // MHD-specifické
    d ..AddBoolParam(arr, "BAREVNE_ODLISENI", "Barevné odlišení", "false")

    s ^||ML("OUT", $i(^||ML("OUT"))) = arr.%ToJSON()
    q 1
}

// ═══════════════════════════════════════════════════════════════════════════════

// GetData — hlavní metoda pro tisk

// ═══════════════════════════════════════════════════════════════════════════════

/// Vrátí kompletní data pro tisk zastávkového MHD JŘ.
/// Vstup:  IN["LINKA_ID"] = ID linky
/// Výstup: OUT[1] = JSON pole stránek (jedna stránka = zastávka × směr)
/// 
/// Každá stránka má strukturu:
///   { "values": { skalární pole }, "rows": { ZASTAVKY: [...], SKUPINY_DNU: [...], ODJEZDY: [...], POZNAMKY: [...] } }
ClassMethod GetData(ByRef sys As %String) As %Status
{
    try {
        $$$FillParams

        s LinkaID = $g(^||ML("IN","LINKA_ID"))
        i 'LinkaID $$$ThrowStatus($$$ERROR($$$GeneralError, "LINKA_ID is required"))

        k ^||TiskMHD

        s TrasaID = ##class(ALVA.MHD.LinkyFce).GetTrasaId(IdFa, dataDB, LinkaID)
        s platnostOd = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"PlatiOd"))
        s platnostDo = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"PlatiDo"))
        s VariantaChronometrazeID = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"VariantaChronometrazeID"))
        s platnostOdHrl = $zdh(platnostOd, 3)
        s PlatnostChronometrazeID = ##class(ALVA.MHD.ChronometrazeFce).GetPlatnostChronometrazeID(IdFa, dataDB, VariantaChronometrazeID, platnostOdHrl)
        // Nastavení auto-poznámkování
        s AutoPoznRozdTrsSp = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"AutomatickyPoznamkovatRozdilyVTraseSpoju"))
        s AutoPoznRozdTrsSpAlg = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"AutomatickyPoznamkovatRozdilyVTraseSpojuAlgoritmus"))
        s AutoPoznRozdTrsSpPism = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"AutomatickyPoznamkovatRozdilyVTraseSpojuPismena"))
        i 'AutoPoznRozdTrsSpAlg s AutoPoznRozdTrsSpAlg = 1001
        i AutoPoznRozdTrsSpPism = "" s AutoPoznRozdTrsSpPism = "abcdefghijklmnopqrstuvwxyz"
        s AutoPoznOdlPlatnSp = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"AutomatickyPoznamkovatOdlisnostiVPlatnostechSpoju"))

        // NZ rozsah
        s nzOd = $g(^[dataDB]ALVA.MHD.SETTINGS(IdFa,"Obecne","DTA",1001,"RozsahNegativnichZnacekOd"))
        s nzDo = $g(^[dataDB]ALVA.MHD.SETTINGS(IdFa,"Obecne","DTA",1001,"RozsahNegativnichZnacekDo"))

        // ═══════════════════════════════════════════════════════════════════
        // FÁZE 1: Příprava trasy pro oba směry
        // ═══════════════════════════════════════════════════════════════════
        d ..PripravitTrasu(IdFa, dataDB, TrasaID, PlatnostChronometrazeID)

        // ═══════════════════════════════════════════════════════════════════
        // FÁZE 2: Poznámky a atributy zastávek (z trasy + číselníku)
        // ═══════════════════════════════════════════════════════════════════
        d ..NacistPoznamkyAAtributyZastavek(IdFa, dataDB, TrasaID)

        // ═══════════════════════════════════════════════════════════════════
        // FÁZE 3: Auto-poznámkování rozdílů v platnostech (NZ)
        // ═══════════════════════════════════════════════════════════════════
        i AutoPoznOdlPlatnSp {
            d ..AutoPoznamkovatPlatnosti(IdFa, dataDB, LinkaID, nzOd, nzDo)
        }

        // ═══════════════════════════════════════════════════════════════════
        // FÁZE 4: Promítnutí spojů do zastávek
        // ═══════════════════════════════════════════════════════════════════
        d ..PromitnutSpoje(.sys, IdFa, dataDB, LinkaID, AutoPoznOdlPlatnSp, AutoPoznRozdTrsSpPism, nzOd, nzDo)

        // ═══════════════════════════════════════════════════════════════════
        // FÁZE 5: Bitmapy obsluhovaných zastávek + rozdíly tras
        // ═══════════════════════════════════════════════════════════════════
        d ..VypocitatBitmapyObsluhy(AutoPoznRozdTrsSp, AutoPoznRozdTrsSpAlg, AutoPoznRozdTrsSpPism)

        // ═══════════════════════════════════════════════════════════════════
        // FÁZE 6: Sestavení JSON výstupu — pole stránek
        // ═══════════════════════════════════════════════════════════════════
        // Konverze datumů na horolog pro formátování
        s platnostOdHrl2 = $zdh(platnostOd, 3)
        s platnostDoHrl = $zdh(platnostDo, 3)
        d ..SestavitStranky(IdFa, dataDB, LinkaID, TrasaID, platnostOdHrl2, platnostDoHrl, AutoPoznRozdTrsSp, AutoPoznRozdTrsSpAlg, AutoPoznRozdTrsSpPism)
        k ^||TiskMHD
    }
    catch (ex) { $$$CatchErrRest }
    ret 1
}

// ═══════════════════════════════════════════════════════════════════════════════

// Fáze 1: Příprava trasy

// ═══════════════════════════════════════════════════════════════════════════════

/// Načte body trasy pro oba směry, chronometráže, sloučí totožné zastávky.
/// Výstup: ^||TiskMHD("trasa",SmerID,BodID) = $lb(ZastavkaID, CasJizdy, ZastavkaCislo)
ClassMethod PripravitTrasu(IdFa As %String, dataDB As %String, TrasaID As %String, PlatnostChronometrazeID As %String)
{
    f SmerID = 0,1 {
        s smerKrok = 1 i SmerID s smerKrok = -1
        s predchoziZastavkaID = 0, kumulativniCas = 0

        s BodID = "" f {
            s BodID = $o(^[dataDB]ALVA.MHD.LSO(IdFa,4,"DTA",TrasaID,"DTA",1,"ModelTrasaBodDetail",BodID), smerKrok) i BodID = "" q

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

    s BodID = "" f {
        s val = "" s BodID = $o(^||TiskMHD("trasa",SmerID,BodID), -smerKrok, val) i BodID = "" q
        i BodID = "slouceni" continue
        s dalsiBodID = BodID
        s pokracovat = 0

        f {
            s dalsiVal = "" s dalsiBodID = $o(^||TiskMHD("trasa",SmerID,dalsiBodID), -smerKrok, dalsiVal) i (dalsiBodID = "") ! (pokracovat) q
            i dalsiBodID = "slouceni" continue
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

// ═══════════════════════════════════════════════════════════════════════════════

// Fáze 2: Poznámky a atributy zastávek

// ═══════════════════════════════════════════════════════════════════════════════

/// Načte poznámky a atributy pro každou zastávku z číselníku a z trasy.
ClassMethod NacistPoznamkyAAtributyZastavek(IdFa As %String, dataDB As %String, TrasaID As %String)
{
    f SmerID = 0,1 {
        s BodID = "" f {
            s val = "" s BodID = $o(^||TiskMHD("trasa",SmerID,BodID), 1, val) i BodID = "" q
            i BodID = "slouceni" continue
            s ZastavkaID = $lg(val, 1)
            i ZastavkaID = "" continue

            // Atributy zastávky z číselníku
            s atributCnt = "" f {
                s atributCnt = $o(^[dataDB]ALVA.MHD.ZST(IdFa,1,"DTA",ZastavkaID,"DTA",1,"Atributy",atributCnt)) i atributCnt = "" q
                s AtributID = $g(^[dataDB]ALVA.MHD.ZST(IdFa,1,"DTA",ZastavkaID,"DTA",1,"Atributy",atributCnt,"Itm"))
                s Popis = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"Atribut","DTA",AtributID,"Popis"))
                s SpecialniZnak = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"Atribut","DTA",AtributID,"SpecZnak"))
                i SpecialniZnak = "" continue
                s ^||TiskMHD("atributyZast",SmerID,BodID,AtributID) = $lb(Popis, 2, SpecialniZnak)
            }

            // Poznámky a atributy z trasy linky (per sloučený bod)
            s smerKlic = $s(SmerID=0:"Tam", 1:"Zpet")
            s subBodID = "" f {
                s subBodID = $o(^||TiskMHD("trasa",SmerID,"slouceni",BodID,subBodID)) i subBodID = "" q

                // Poznámky
                s pozCnt = "" f {
                    s pozCnt = $o(^[dataDB]ALVA.MHD.LSO(IdFa,4,"DTA",TrasaID,"DTA",1,"ModelTrasaBodDetail",subBodID,"Val","Poznamky"_smerKlic,pozCnt)) i pozCnt = "" q
                    s PoznamkaSpojID = $g(^[dataDB]ALVA.MHD.LSO(IdFa,4,"DTA",TrasaID,"DTA",1,"ModelTrasaBodDetail",subBodID,"Val","Poznamky"_smerKlic,pozCnt,"Itm"))
                    // Filtr: jen poznámky určené pro tisk zastávkového JŘ
                    i '$g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"PoznamkaSpoj","DTA",PoznamkaSpojID,"TiskJRMHDZastavkove")) continue
                    s pouzeZnacku = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"PoznamkaSpoj","DTA",PoznamkaSpojID,"TiskJRMHDZastavkovePouzeZnacku"))
                    s TypPoznamkyID = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"PoznamkaSpoj","DTA",PoznamkaSpojID,"TypPoznamkyID"))
                    s Text = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"PoznamkaSpoj","DTA",PoznamkaSpojID,"Text"))
                    s PopisPoznamky = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"PoznamkaSpoj","DTA",PoznamkaSpojID,"PopisPoznamky"))
                    i pouzeZnacku s PopisPoznamky = ""
                    i Text = "" continue
                    // Textová poznámka (TypPoznamkyID=0)
                    i 'TypPoznamkyID {
                        s ^||TiskMHD("poznamkyZast",SmerID,BodID,Text) = $lb(PopisPoznamky, 0)
                    }
                }

                // Atributy z trasy
                s atrCnt = "" f {
                    s atrCnt = $o(^[dataDB]ALVA.MHD.LSO(IdFa,4,"DTA",TrasaID,"DTA",1,"ModelTrasaBodDetail",subBodID,"Val","Atributy"_smerKlic,atrCnt)) i atrCnt = "" q
                    s AtributID = $g(^[dataDB]ALVA.MHD.LSO(IdFa,4,"DTA",TrasaID,"DTA",1,"ModelTrasaBodDetail",subBodID,"Val","Atributy"_smerKlic,atrCnt,"Itm"))
                    s Popis = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"Atribut","DTA",AtributID,"Popis"))
                    s SpecialniZnak = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"Atribut","DTA",AtributID,"SpecZnak"))
                    i SpecialniZnak = "" continue
                    s ^||TiskMHD("atributyZast",SmerID,BodID,AtributID) = $lb(Popis, 2, SpecialniZnak)
                }
            }
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════════════

// Fáze 3: Auto-poznámkování platností

// ═══════════════════════════════════════════════════════════════════════════════

/// Automatické přidělení NZ (negativních značek) spojům s omezenou platností.
/// Porovnává bitmapu platnosti spoje s bitmapou typu provozu.
ClassMethod AutoPoznamkovatPlatnosti(IdFa As %String, dataDB As %String, LinkaID As %String, nzOd As %Integer, nzDo As %Integer)
{
    s SpojID = "" f {
        s SpojID = $o(^[dataDB]ALVA.MHD.LSO(IdFa,2,"IDX",1,1,1,LinkaID,SpojID)) i SpojID = "" q
        s NegativniZnacka = $g(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",SpojID,"DTA",1,"NegativniZnacka"))
        s TypProvozuID = $g(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",SpojID,"DTA",1,"TypProvozuID"))
        i NegativniZnacka = "" continue
        i '$g(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",SpojID,"DTA",1,"RucniZadani")) continue

        // Porovnání bitmap: spoj vs typ provozu
        s ProvozBitMapa = $g(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",SpojID,"DTA",1,"ProvozBitMapa"))
        s bitmapSpoje = $p(ProvozBitMapa,"~",3)
        k arr s isok = ##class(ALVA.MHD.KalendarFce).GetJede(IdFa,dataDB,$p(ProvozBitMapa,"~",1),$p(ProvozBitMapa,"~",2),TypProvozuID,"","",.arr)
        i 'isok continue
        s bitmapTypuProvozu = $lg($g(arr("dta",1)),3)
        k arr

        i bitmapTypuProvozu = bitmapSpoje continue

        // Bitmapy se liší → generovat NZ text
        k arri, arr
        s arri("dta",1) = $p(ProvozBitMapa,"~",1)
        s arri("dta",2) = $p(ProvozBitMapa,"~",2)
        s arri("dta",3) = bitmapSpoje
        s arri("dta",4) = bitmapTypuProvozu
        s isok = ##class(ALVA.MHD.KalendarFce).MakeRozpisTplProTypProvozuID(IdFa,dataDB,TypProvozuID,.arri)
        i 'isok continue
        s err = ""
        k arri m arri = arr s arri("par",1) = 0 k arr
        s isok = ##class(ALVA.MHD.KalendarFce).MakeOmezeniTXT(IdFa,dataDB,.arri,.arr)
        i 'isok continue

        s OmezeniTXT = $g(arr("dta",1))
        i OmezeniTXT '= "" {
            s ^||TiskMHD("nzTexty",NegativniZnacka) = OmezeniTXT
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════════════

// Fáze 4: Promítnutí spojů

// ═══════════════════════════════════════════════════════════════════════════════

/// Iteruje všechny spoje linky, přiřadí odjezdové časy do zastávek,
/// načte poznámky a atributy spoje, zpracuje NZ.
ClassMethod PromitnutSpoje(ByRef sys As %String, IdFa As %String, dataDB As %String, LinkaID As %String, AutoPoznOdlPlatnSp As %Boolean, AutoPoznRozdTrsSpPism As %String, nzOd As %Integer, nzDo As %Integer)
{
    s SpojID = "" f {
        s SpojID = $o(^[dataDB]ALVA.MHD.LSO(IdFa,2,"IDX",1,1,1,LinkaID,SpojID)) i SpojID = "" q
        s SmerID = $g(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",SpojID,"DTA",1,"SmerID"))
        s TypProvozuID = $g(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",SpojID,"DTA",1,"TypProvozuID"))
        s ^||TiskMHD("typyProvozu",TypProvozuID) = ""

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
        s BodID = "" f {
            s BodID = $o(^||TiskMHD("trasa",SmerID,BodID)) i BodID = "" q
            i BodID = "slouceni" continue
            s jeKonecny = (BodID = konecnyBod)

            // Najít čas spoje v tomto bodu (včetně sloučených)
            s casOdjezdu = ""
            s subBodID = "" f {
                s subBodID = $o(^||TiskMHD("trasa",SmerID,"slouceni",BodID,subBodID), smerKrok) i subBodID = "" q
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
                    s ^||TiskMHD("odjezdy",SmerID,BodID,TypProvozuID,hh,mm,SpojID) = ""
                    s ^||TiskMHD("obsluhaKonec",SmerID,BodID,SpojID) = ""
                }
            }
        }
        s ^||TiskMHD("spojTrasa",SmerID,SpojID) = TrasaBitmap

        // Poznámky spoje
        k myarr s isok = ##class(ALVA.MHD.SpojeFce).GetPoznamkySpoj(.sys,SpojID,"",.myarr)
        i 'isok continue
        // Pro auto-poznámkování platností odstranit spec.znaky a NZ
        i AutoPoznOdlPlatnSp {
            s myID = "" f {
                s myID = $o(myarr("arr",1,myID)) i myID = "" q
                s typPozn = $g(myarr("arr",1,myID,"TypPoznamkyID"))
                i (typPozn = 1) ! (typPozn = 2) k myarr("arr",1,myID)
            }
        }
        k ^||TiskMHD("spojPoznamky",SpojID) m ^||TiskMHD("spojPoznamky",SpojID) = myarr

        // Atributy spoje
        k myarr s isok = ##class(ALVA.MHD.SpojeFce).GetAtributySpoj(.sys,SpojID,"",.myarr)
        i 'isok continue
        // Filtr: vyhodit netisknuté atributy (parametr NETISKNUTE_ATRIBUTY)
        s AtributID = "" f {
            s AtributID = $o(myarr("arr",1,AtributID)) i AtributID = "" q
            i $d(^||ML("IN","par_2_"_AtributID)) k myarr("arr",1,AtributID)
        }
        k ^||TiskMHD("spojAtributy",SpojID) m ^||TiskMHD("spojAtributy",SpojID) = myarr

        // NZ přiřazení
        d ..PriraditNZSpoji(IdFa, dataDB, SpojID, TypProvozuID, AutoPoznOdlPlatnSp, nzOd, nzDo)
    }
}

/// Přiřadí negativní značku spoji (automaticky nebo z ručního zadání).
ClassMethod PriraditNZSpoji(IdFa As %String, dataDB As %String, SpojID As %String, TypProvozuID As %String, AutoPoznOdlPlatnSp As %Boolean, nzOd As %Integer, nzDo As %Integer)
{
    i AutoPoznOdlPlatnSp {
        // Automatické: z bitmapy platnosti
        i '$g(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",SpojID,"DTA",1,"RucniZadani")) q

        s ProvozBitMapa = $g(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",SpojID,"DTA",1,"ProvozBitMapa"))
        s bitmapSpoje = $p(ProvozBitMapa,"~",3)
        k arr s isok = ##class(ALVA.MHD.KalendarFce).GetJede(IdFa,dataDB,$p(ProvozBitMapa,"~",1),$p(ProvozBitMapa,"~",2),TypProvozuID,"","",.arr)
        i 'isok q
        s bitmapTypuProvozu = $lg($g(arr("dta",1)),3)
        k arr

        i bitmapTypuProvozu = bitmapSpoje q

        // Generovat text omezení
        k arri, arr
        s arri("dta",1) = $p(ProvozBitMapa,"~",1)
        s arri("dta",2) = $p(ProvozBitMapa,"~",2)
        s arri("dta",3) = bitmapSpoje
        s arri("dta",4) = bitmapTypuProvozu
        s isok = ##class(ALVA.MHD.KalendarFce).MakeRozpisTplProTypProvozuID(IdFa,dataDB,TypProvozuID,.arri)
        i 'isok q
        #; s err = $mkOmzFromOnlyTwoBitmaps^JRapi(IdFa,dataDB,.arri,.arr)
        #; i +err q
        s err = ""

        // Týdenní platnost
        s tydenniPlatnost = $g(arr("dta",1))
        i tydenniPlatnost '= "" s ^||TiskMHD("spojTydenni",SpojID) = tydenniPlatnost

        k arri m arri = arr s arri("par",1) = 0 k arr
        s isok = ##class(ALVA.MHD.KalendarFce).MakeOmezeniTXT(IdFa,dataDB,.arri,.arr)
        i 'isok q

        s OmezeniTXT = $g(arr("dta",1))
        i OmezeniTXT = "" q

        // Zatřídit značku: hledat existující se stejným textem
        s spojNZ = ""
        // Nejprve v číselníku NZ
        s nz = "" f {
            s nz = $o(^||TiskMHD("nzTexty",nz)) i (nz = "") ! (spojNZ '= "") q
            i $g(^||TiskMHD("nzTexty",nz)) = OmezeniTXT s spojNZ = nz
        }
        // Pak v přiřazených NZ
        i spojNZ = "" {
            s nz = "" f {
                s nz = $o(^||TiskMHD("nzPrirazen",nz)) i (nz = "") ! (spojNZ '= "") q
                i $g(^||TiskMHD("nzPrirazen",nz)) = OmezeniTXT s spojNZ = nz
            }
        }
        // Nová značka
        i spojNZ = "" {
            f spojNZ = nzOd:1 {
                // Kontrola: není obsazená u typu provozu
                s obsazena = 0
                s myTyp = "" f {
                    s myTyp = $o(^[dataDB]ALVA.MHD.ENUMS(IdFa,"TypyProvozu","DTA",myTyp)) i myTyp = "" q
                    i $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"TypyProvozu","DTA",myTyp,"NegativniZnacka")) = spojNZ s obsazena = 1
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
        s myID = "" f {
            s myID = $o(^||TiskMHD("spojPoznamky",SpojID,"arr",1,myID)) i (myID = "") ! (PoznamkaID '= "") q
            i $g(^||TiskMHD("spojPoznamky",SpojID,"arr",1,myID,"TypPoznamkyID")) = 2 {
                i $g(^||TiskMHD("spojPoznamky",SpojID,"arr",1,myID,"TiskJRMHDZastavkove")) s PoznamkaID = myID
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

// ═══════════════════════════════════════════════════════════════════════════════

// Fáze 5: Bitmapy obsluhy

// ═══════════════════════════════════════════════════════════════════════════════

/// Pro každou zastávku v obou směrech vypočítá bitmapu obsluhovaných zastávek
/// a AllStopTrasaBitmap (1=všechny spoje, 9=jen některé).
ClassMethod VypocitatBitmapyObsluhy(AutoPoznRozdTrsSp As %Boolean, AutoPoznRozdTrsSpAlg As %Integer, AutoPoznRozdTrsSpPism As %String)
{
    f SmerID = 0,1 {
        s smerKrok = 1 i SmerID s smerKrok = -1

        s BodID = "" f {
            s BodID = $o(^||TiskMHD("trasa",SmerID,BodID)) i BodID = "" q
            i BodID = "slouceni" continue

            // Bitmapa obsluhovaných zastávek z tohoto bodu
            s ObsluhaBitmap = ""
            s SpojID = "" f {
                s SpojID = $o(^||TiskMHD("obsluhaKonec",SmerID,BodID,SpojID)) i SpojID = "" q
                s TrasaSpojBitmap = $g(^||TiskMHD("spojTrasa",SmerID,SpojID))

                // Spoj v zastávce nestaví → přeskočit
                i '$e(TrasaSpojBitmap, BodID) continue

                // Konečný bod spoje: najít poslední obsluhovanou zastávku
                s konecnyBod = BodID
                s dalsiBod = BodID f {
                    s dalsiBod = $o(^||TiskMHD("trasa",SmerID,dalsiBod), smerKrok) i dalsiBod = "" q
                    i dalsiBod = "slouceni" continue
                    i $e(TrasaSpojBitmap, dalsiBod) {
                        s $e(ObsluhaBitmap, dalsiBod) = 1
                        s $e(ObsluhaBitmap, BodID) = 1
                        s konecnyBod = dalsiBod
                    }
                }
                s ^||TiskMHD("obsluhaKonec",SmerID,BodID,SpojID) = konecnyBod
                // Trasa spoje z bodu
                s TrasaSpojZBodu = ""
                s dalsiBod = BodID f {
                    s dalsiBod = $o(^||TiskMHD("trasa",SmerID,dalsiBod), smerKrok) i dalsiBod = "" q
                    i dalsiBod = "slouceni" continue
                    i dalsiBod = konecnyBod q
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
            s SpojID = "" f {
                s SpojID = $o(^||TiskMHD("obsluhaKonec",SmerID,BodID,SpojID)) i SpojID = "" q
                s TrasaSpojZBodu = $g(^||TiskMHD("spojTrasa",SmerID,SpojID,BodID))
                s konecnyBod = $g(^||TiskMHD("obsluhaKonec",SmerID,BodID,SpojID))
                f mycnt = BodID:smerKrok:konecnyBod {
                    i ('$e(TrasaSpojZBodu, mycnt)) & (+$e(AllStopBitmap, mycnt)) {
                        s $e(AllStopBitmap, mycnt) = 9
                    }
                }
            }
            s ^||TiskMHD("obsluhaVse",SmerID,BodID) = AllStopBitmap

            // Rozdílové bitmapy tras pro auto-poznámkování (alg. 1002)
            i AutoPoznRozdTrsSp & (AutoPoznRozdTrsSpAlg = 1002) {
                s pismCnt = 0
                s SpojID = "" f {
                    s SpojID = $o(^||TiskMHD("obsluhaKonec",SmerID,BodID,SpojID)) i SpojID = "" q
                    s TrasaSpojZBodu = $g(^||TiskMHD("spojTrasa",SmerID,SpojID,BodID))
                    s konecnyBod = $g(^||TiskMHD("obsluhaKonec",SmerID,BodID,SpojID))

                    // Zastavuje navíc (kde AllStopBitmap=9)
                    s DiffBitmap = ""
                    f mycnt = BodID:smerKrok:konecnyBod {
                        i (+$e(TrasaSpojZBodu, mycnt)) & (+$e(AllStopBitmap, mycnt) = 9) {
                            s $e(DiffBitmap, mycnt) = 1
                        }
                    }
                    i DiffBitmap '= "" {
                        s ^||TiskMHD("rozdilTrasy",SmerID,BodID,0,DiffBitmap,SpojID) = ""
                    }

                    // Konečný interval (končí dříve)
                    s lastBod = $o(^||TiskMHD("trasa",SmerID,""),-smerKrok)
                    i ((smerKrok=1)&(konecnyBod < lastBod)) ! ((smerKrok=-1)&(konecnyBod > lastBod)) {
                        s EndDiffBitmap = ""
                        s $e(EndDiffBitmap, konecnyBod) = 1
                        s ^||TiskMHD("rozdilTrasy",SmerID,BodID,1,EndDiffBitmap,SpojID) = ""
                    }
                }

                // Přiřadit písmena k rozdílovým bitmapám
                s pismCnt = 0
                s mytyp = "" f {
                    s mytyp = $o(^||TiskMHD("rozdilTrasy",SmerID,BodID,mytyp)) i mytyp = "" q
                    s DiffBitmap = "" f {
                        s DiffBitmap = $o(^||TiskMHD("rozdilTrasy",SmerID,BodID,mytyp,DiffBitmap)) i DiffBitmap = "" q
                        s pismCnt = pismCnt + 1
                        s pism = $e(AutoPoznRozdTrsSpPism, pismCnt)
                        i pism = "" s pism = "#"
                        s ^||TiskMHD("rozdilTrasyPism",SmerID,BodID,mytyp,DiffBitmap) = pism
                    }
                }
            }
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════════════

// Fáze 6: Sestavení JSON výstupu

// ═══════════════════════════════════════════════════════════════════════════════

/// Hlavní výstupní smyčka: pro každou zastávku × směr sestaví jednu stránku JSON.
/// Každá stránka se zapíše jako samostatný OUT node (řeší limit délky stringu).
ClassMethod SestavitStranky(IdFa As %String, dataDB As %String, LinkaID As %String, TrasaID As %String, platnostOd As %Integer, platnostDo As %Integer, AutoPoznRozdTrsSp As %Boolean, AutoPoznRozdTrsSpAlg As %Integer, AutoPoznRozdTrsSpPism As %String)
{
    s CisloLinky = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"CisloLinkyDleLicence"))
    s LokalniOznaceni = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"CisloVerejne"))
    s NazevLinky = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"NazevLinky"))
    s QRKod = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"QRCode"))
    s TypJR = ""
    i +$g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"Navrh")) s TypJR = "NÁVRH"
    i +$g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",LinkaID,"DTA",1,"Vyluka")) s TypJR = "VÝLUKOVÝ"

    // Explicitní pořadí: nejdřív Tam (0), pak Zpět (1)
    f SmerID = 0,1 {
        i '$d(^||TiskMHD("trasa",SmerID)) continue

        s smerPopis = $s(SmerID=0:"T", 1:"Z")

        // Sesbírat BodID v pořadí jízdy (Tam=vzestupně, Zpět=sestupně)
        // a uložit do pole pro konzistentní iteraci
        k orderedBods s orderedBodCnt = 0
        s BodID = "" f {
            s BodID = $o(^||TiskMHD("trasa",SmerID,BodID)) i BodID = "" q
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

            s page = ..SestavitJednuStranku(IdFa, dataDB, LinkaID, TrasaID, SmerID, aktualniZastBodID, CisloLinky, LokalniOznaceni, NazevLinky, QRKod, TypJR, platnostOd, platnostDo, smerPopis, pocetZastavek, AutoPoznRozdTrsSp, AutoPoznRozdTrsSpAlg, AutoPoznRozdTrsSpPism)
            s ^||ML("OUT",$i(^||ML("OUT"))) = page.%ToJSON()
        }
    }
}

/// Sestaví jednu stránku (= jedna zastávka × jeden směr).
ClassMethod SestavitJednuStranku(IdFa As %String, dataDB As %String, LinkaID As %String, TrasaID As %String, SmerID As %Integer, aktualniZastBodID As %String, CisloLinky As %String, LokalniOznaceni As %String, NazevLinky As %String, QRKod As %String, TypJR As %String, platnostOd As %Integer, platnostDo As %Integer, smerPopis As %String, pocetZastavek As %Integer, AutoPoznRozdTrsSp As %Boolean, AutoPoznRozdTrsSpAlg As %Integer, AutoPoznRozdTrsSpPism As %String) As %DynamicObject
{
    s page = ##class(%DynamicObject).%New()
    s smerKrok = 1 i SmerID s smerKrok = -1

    s aktualniZastVal = $g(^||TiskMHD("trasa",SmerID,aktualniZastBodID))
    s aktualniZastavkaID = $lg(aktualniZastVal, 1)
    s aktualniZastavkaCas = $lg(aktualniZastVal, 2)
    s NazevZastavky = $lg(##class(ALVA.MHD.ZastavkyFce).GetNazevHrl(IdFa, dataDB, aktualniZastavkaID, platnostOd), 1)
    s CisloZastavky = $g(^[dataDB]ALVA.MHD.ZST(IdFa,1,"DTA",aktualniZastavkaID,"DTA",1,"Cislo"))
    s Oznacnik = $g(^[dataDB]ALVA.MHD.ZST(IdFa,1,"DTA",aktualniZastavkaID,"DTA",1,"Oznacnik"))

    s ObsluhaBitmap = $g(^||TiskMHD("obsluha",SmerID,aktualniZastBodID))

    // ── Skalární hodnoty ─────────────────────────────────────────────────
    s vals = ##class(%DynamicObject).%New()
    d vals.%Set("CISLO_LINKY",            CisloLinky)
    d vals.%Set("LOKALNI_OZNACENI_LINKY", LokalniOznaceni)
    d vals.%Set("TITULEK",                "")
    d vals.%Set("POPIS_LINKY",            NazevLinky)
    d vals.%Set("POPIS_DOPRAVCE",         "")
    d vals.%Set("POPIS_PLATNOSTI",        $zd(platnostOd,4)_" - "_$zd(platnostDo,4))
    d vals.%Set("DATUM_PLATNOSTI_OD",     $zd(platnostOd,4))
    d vals.%Set("DATUM_PLATNOSTI_DO",     $zd(platnostDo,4))
    d vals.%Set("POPIS_SMER",             smerPopis)
    d vals.%Set("TYP_JR",                 TypJR)
    d vals.%Set("QR_KOD",                 QRKod)
    d vals.%Set("POPIS_ZASTAVKA",         NazevZastavky)
    d vals.%Set("CISLO_ZASTAVKA",         CisloZastavky_"-"_Oznacnik)
    d vals.%Set("POZNAMKA_POD_DOPRAVCE",  "")
    d vals.%Set("INFORMACE",              "")
    d vals.%Set("NEJEDOU_SPOJE",          "")
    d page.%Set("values", vals)

    // ── Kolekce ──────────────────────────────────────────────────────────
    s rows = ##class(%DynamicObject).%New()

    // ZASTAVKY — vždy všechny v daném směru, s příznakem JE_AKTUALNI
    s zastavky = ##class(%DynamicArray).%New()
    s posledniBod = ""
    s poradiZast = 0
    s mycnt = "" f {
        s mycnt = $o(^||TiskMHD("trasa",SmerID,mycnt), smerKrok) i mycnt = "" q
        i mycnt = "slouceni" continue
        // Za aktuální: jen obsluhovatelné zastávky
        i (SmerID=0)&(mycnt>aktualniZastBodID)&('$e(ObsluhaBitmap,mycnt)) continue
        i (SmerID=1)&(mycnt<aktualniZastBodID)&('$e(ObsluhaBitmap,mycnt)) continue

        s zastVal = $g(^||TiskMHD("trasa",SmerID,mycnt))
        s zastID = $lg(zastVal, 1)
        s zastCas = $lg(zastVal, 2)
        s zastCislo = $lg(zastVal, 3)

        s poradiZast = poradiZast + 1
        s jeAktualni = (mycnt = aktualniZastBodID)
        s casJizdy = 0
        i jeAktualni ! ((SmerID=0)&(mycnt>aktualniZastBodID)) ! ((SmerID=1)&(mycnt<aktualniZastBodID)) {
            s casJizdy = zastCas - aktualniZastavkaCas
        }

        s zastNazev = $lg(##class(ALVA.MHD.ZastavkyFce).GetNazevHrl(IdFa, dataDB, zastID, platnostOd), 1)

        // Odsazení (legacy: dta 10)
        s odsazeni = 0

        // Na znamení
        s naZnameni = 0

        s z = ..BuildZastavka(mycnt, zastNazev, zastCislo, casJizdy, poradiZast, jeAktualni, naZnameni, odsazeni)

        // Zóny
        s zonaidx = "" f {
            s zonaidx = $o(^[dataDB]ALVA.MHD.ZST(IdFa,1,"DTA",zastID,"DTA",1,"ZonyID",zonaidx)) i zonaidx = "" q
            s ZonaID = $g(^[dataDB]ALVA.MHD.ZST(IdFa,1,"DTA",zastID,"DTA",1,"ZonyID",zonaidx,"Itm"))
            s ZonaCislo = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"Zony","DTA",ZonaID,"Cislo"))
            i ZonaCislo '= "" d ..AddZona(z, ZonaCislo)
        }

        // Atributy zastávky (jen neprojete + aktuální jen N,V,x)
        i ((SmerID=0)&(mycnt'<aktualniZastBodID)) ! ((SmerID=1)&(mycnt'>aktualniZastBodID)) {
            s AtributID = "" f {
                s AtributID = $o(^||TiskMHD("atributyZast",SmerID,mycnt,AtributID)) i AtributID = "" q
                // Aktuální zastávka: jen základní atributy (ID<=1003)
                i (mycnt=aktualniZastBodID)&(AtributID>1003) continue
                s atrVal = $g(^||TiskMHD("atributyZast",SmerID,mycnt,AtributID))
                s SpecZnak = ..ConvertSpecialniZnak($lg(atrVal,3))
                d ..AddAtribut(z, SpecZnak, $lg(atrVal,1))
            }
        }

        // Poznámky zastávky (jen neprojete)
        i ((SmerID=0)&(mycnt>aktualniZastBodID)) ! ((SmerID=1)&(mycnt<aktualniZastBodID)) {
            s pozZnak = "" f {
                s pozZnak = $o(^||TiskMHD("poznamkyZast",SmerID,mycnt,pozZnak)) i pozZnak = "" q
                s pozVal = $g(^||TiskMHD("poznamkyZast",SmerID,mycnt,pozZnak))
                d ..AddPoznamkaZastavky(z, pozZnak, "text", $lg(pozVal,1))
            }
        }

        d zastavky.%Push(z)
        s posledniBod = mycnt
    }
    d rows.%Set("ZASTAVKY", zastavky)

    // Aktualizovat poslední zastávku v skalárech
    i posledniBod '= "" {
        s posledniVal = $g(^||TiskMHD("trasa",SmerID,posledniBod))
        s posledniNazev = $lg(##class(ALVA.MHD.ZastavkyFce).GetNazevHrl(IdFa, dataDB, $lg(posledniVal,1), platnostOd), 1)
    }

    // SKUPINY_DNU — z typů provozu
    s skupiny = ##class(%DynamicArray).%New()
    s TypProvozuID = "" f {
        s TypProvozuID = $o(^||TiskMHD("typyProvozu",TypProvozuID)) i TypProvozuID = "" q
        // Má tato zastávka odjezdy tohoto typu?
        i '$d(^||TiskMHD("odjezdy",SmerID,aktualniZastBodID,TypProvozuID)) continue
        s SMOID = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"TypyProvozu","DTA",TypProvozuID,"SMOID"))
        i SMOID = "" continue
        s nadpis1 = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"TypyProvozu","DTA",TypProvozuID,"NadpisSMOPrvni"))
        s nadpis2 = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"TypyProvozu","DTA",TypProvozuID,"NadpisSMODruhy"))
        s poradi = +$e(SMOID,4)
        d skupiny.%Push(..BuildSkupinaDnu(SMOID, nadpis1, nadpis2, poradi))
    }
    d rows.%Set("SKUPINY_DNU", skupiny)

    // ODJEZDY
    s odjezdy = ##class(%DynamicArray).%New()
    // Lokální sběr poznámek pro tuto stránku
    k strankovePoznamky

    s TypProvozuID = "" f {
        s TypProvozuID = $o(^||TiskMHD("odjezdy",SmerID,aktualniZastBodID,TypProvozuID)) i TypProvozuID = "" q
        s SMOID = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"TypyProvozu","DTA",TypProvozuID,"SMOID"))
        i SMOID = "" continue

        s hh = "" f {
            s hh = $o(^||TiskMHD("odjezdy",SmerID,aktualniZastBodID,TypProvozuID,hh)) i hh = "" q
            s mm = "" f {
                s mm = $o(^||TiskMHD("odjezdy",SmerID,aktualniZastBodID,TypProvozuID,hh,mm)) i mm = "" q
                s SpojID = "" f {
                    s SpojID = $o(^||TiskMHD("odjezdy",SmerID,aktualniZastBodID,TypProvozuID,hh,mm,SpojID)) i SpojID = "" q

                    s odj = ..BuildOdjezd(aktualniZastBodID, SMOID, hh, mm)

                    // ── Anotace odjezdu ──

                    // Týdenní platnost (automatická)
                    s tydPlatnost = $g(^||TiskMHD("spojTydenni",SpojID))
                    f plIdx = 1:1:$l(tydPlatnost) {
                        s denZnak = $e(tydPlatnost, plIdx)
                        i denZnak = "" continue
                        d ..AddAnotace(odj, "specialni", ..ConvertSpecialniZnak(denZnak))
                        // Poznámka pro den
                        s denText = ..GetDenVTydnuText(denZnak)
                        i denText '= "" s strankovePoznamky(denZnak) = $lb(denText, 2)
                    }

                    // Speciální znaky ze spoje (ručně zadané)
                    s myID = "" f {
                        s myID = $o(^||TiskMHD("spojPoznamky",SpojID,"arr",1,myID)) i myID = "" q
                        i $g(^||TiskMHD("spojPoznamky",SpojID,"arr",1,myID,"TypPoznamkyID")) '= 1 continue
                        i '$g(^||TiskMHD("spojPoznamky",SpojID,"arr",1,myID,"TiskJRMHDZastavkove")) continue
                        s specZnak = $g(^||TiskMHD("spojPoznamky",SpojID,"arr",1,myID,"SpecZnak"))
                        i specZnak = "" continue
                        d ..AddAnotace(odj, "specialni", specZnak)
                        s PopisPozn = $g(^||TiskMHD("spojPoznamky",SpojID,"arr",1,myID,"PopisPoznamky"))
                        i '$g(^||TiskMHD("spojPoznamky",SpojID,"arr",1,myID,"TiskJRMHDZastavkovePouzeZnacku")) {
                            s strankovePoznamky(specZnak) = $lb(PopisPozn, 2)
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

                    // Atributy spoje
                    s myID = "" f {
                        s myID = $o(^||TiskMHD("spojAtributy",SpojID,"arr",1,myID)) i myID = "" q
                        s specZnak = $g(^||TiskMHD("spojAtributy",SpojID,"arr",1,myID,"SpecZnak"))
                        s Popis = $g(^||TiskMHD("spojAtributy",SpojID,"arr",1,myID,"Popis"))
                        d ..AddAnotace(odj, "specialni", specZnak)
                        s strankovePoznamky(specZnak) = $lb(Popis, 2)
                    }

                    // Auto-poznámkování tras (alg. 1001)
                    i AutoPoznRozdTrsSp & (AutoPoznRozdTrsSpAlg = 1001) {
                        d ..DoplnitPoznamkuRozdilTrasy1001(IdFa, dataDB, odj, SmerID, aktualniZastBodID, SpojID, platnostOd, AutoPoznRozdTrsSpPism, .strankovePoznamky)
                    }

                    // Auto-poznámkování tras (alg. 1002)
                    i AutoPoznRozdTrsSp & (AutoPoznRozdTrsSpAlg = 1002) {
                        d ..DoplnitPoznamkuRozdilTrasy1002(odj, SmerID, aktualniZastBodID, SpojID, .strankovePoznamky)
                    }

                    // Textové poznámky spoje
                    s myID = "" f {
                        s myID = $o(^||TiskMHD("spojPoznamky",SpojID,"arr",1,myID)) i myID = "" q
                        i $g(^||TiskMHD("spojPoznamky",SpojID,"arr",1,myID,"TypPoznamkyID")) '= 0 continue
                        i '$g(^||TiskMHD("spojPoznamky",SpojID,"arr",1,myID,"TiskJRMHDZastavkove")) continue
                        s znakPozn = $g(^||TiskMHD("spojPoznamky",SpojID,"arr",1,myID,"Text"))
                        i znakPozn = "" continue
                        d ..AddAnotace(odj, "text", znakPozn)
                        s PopisPozn = $g(^||TiskMHD("spojPoznamky",SpojID,"arr",1,myID,"PopisPoznamky"))
                        i '$g(^||TiskMHD("spojPoznamky",SpojID,"arr",1,myID,"TiskJRMHDZastavkovePouzeZnacku")) {
                            s strankovePoznamky(znakPozn) = $lb(PopisPozn, 0)
                        }
                    }

                    d odjezdy.%Push(odj)
                }
            }
        }
    }
    d rows.%Set("ODJEZDY", odjezdy)

    // POZNAMKY — seřazené: znaky (text) → NZ → speciální
    s poznamky = ##class(%DynamicArray).%New()

    // Znaky z trasy (textové poznámky)
    s pozZnak = "" f {
        s pozZnak = $o(^||TiskMHD("poznamkyZast",SmerID,aktualniZastBodID,pozZnak)) i pozZnak = "" q
        s pozVal = $g(^||TiskMHD("poznamkyZast",SmerID,aktualniZastBodID,pozZnak))
        d poznamky.%Push(..BuildPoznamka("text", pozZnak, $lg(pozVal,1)))
    }

    // Poznámky ze stránky (odjezdy) — seřazené
    s znak = "" f {
        s znak = $o(strankovePoznamky(znak)) i znak = "" q
        s pVal = $g(strankovePoznamky(znak))
        s pTypCislo = $lg(pVal, 2)
        s pTyp = $s(pTypCislo=0:"text", pTypCislo=1:"nz", 1:"specialni")
        s pZnak = $s(pTypCislo=1:$lg(pVal,3), 1:znak)
        s pText = $lg(pVal, 1)
        d poznamky.%Push(..BuildPoznamka(pTyp, pZnak, pText))
    }

    // Atributy zastávky jako poznámky (speciální znaky)
    s AtributID = "" f {
        s AtributID = $o(^||TiskMHD("atributyZast",SmerID,aktualniZastBodID,AtributID)) i AtributID = "" q
        s atrVal = $g(^||TiskMHD("atributyZast",SmerID,aktualniZastBodID,AtributID))
        s SpecZnak = ..ConvertSpecialniZnak($lg(atrVal,3))
        d poznamky.%Push(..BuildPoznamka("specialni", SpecZnak, $lg(atrVal,1)))
    }

    d rows.%Set("POZNAMKY", poznamky)
    d page.%Set("rows", rows)

    q page
}

// ═══════════════════════════════════════════════════════════════════════════════

// Pomocné metody pro auto-poznámkování tras

// ═══════════════════════════════════════════════════════════════════════════════

/// Auto-poznámkování alg. 1001: textová poznámka o rozdílech v trase spoje.
ClassMethod DoplnitPoznamkuRozdilTrasy1001(IdFa As %String, dataDB As %String, odj As %DynamicObject, SmerID As %Integer, aktualniZastBodID As %String, SpojID As %String, platnostOd As %String, AutoPoznRozdTrsSpPism As %String, ByRef strankovePoznamky)
{
    s TrasaSpojZBodu = $g(^||TiskMHD("spojTrasa",SmerID,SpojID,aktualniZastBodID))
    s TisknutaTrasaBitmap = $g(^||TiskMHD("obsluha",SmerID,aktualniZastBodID))

    i TisknutaTrasaBitmap = TrasaSpojZBodu q

    // Generovat text
    k arri, arro
    s arri("dta",1) = SmerID
    s arri("dta",2) = aktualniZastBodID
    s arri("dta",3) = TisknutaTrasaBitmap
    s arri("dta",4) = TisknutaTrasaBitmap
    s arri("dta",5) = TrasaSpojZBodu
    s arri("dta",6) = platnostOd
    s tmpcnt = "" f {
        s tmpcnt = $o(^||TiskMHD("trasa",SmerID,tmpcnt)) i tmpcnt = "" q
        i tmpcnt = "slouceni" continue
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
    s existujiciZnak = "" f {
        s existujiciZnak = $o(strankovePoznamky(existujiciZnak)) i (existujiciZnak = "") ! (idznk '= "") q
        i $lg($g(strankovePoznamky(existujiciZnak)),1) = poznamkaText s idznk = existujiciZnak
    }

    // Přiřadit nový znak
    i idznk = "" {
        f myinx = 1:1:$l(AutoPoznRozdTrsSpPism) {
            i idznk '= "" continue
            s kandidat = $e(AutoPoznRozdTrsSpPism, myinx)
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
ClassMethod DoplnitPoznamkuRozdilTrasy1002(odj As %DynamicObject, SmerID As %Integer, aktualniZastBodID As %String, SpojID As %String, ByRef strankovePoznamky)
{
    s mytyp = "" f {
        s mytyp = $o(^||TiskMHD("rozdilTrasy",SmerID,aktualniZastBodID,mytyp)) i mytyp = "" q
        s DiffBitmap = "" f {
            s DiffBitmap = $o(^||TiskMHD("rozdilTrasy",SmerID,aktualniZastBodID,mytyp,DiffBitmap)) i DiffBitmap = "" q
            // Kontrola: tento spoj je v této rozdílové skupině?
            i '$d(^||TiskMHD("rozdilTrasy",SmerID,aktualniZastBodID,mytyp,DiffBitmap,SpojID)) continue
            s pism = $g(^||TiskMHD("rozdilTrasyPism",SmerID,aktualniZastBodID,mytyp,DiffBitmap))
            i pism '= "" {
                d ..AddAnotace(odj, "text", pism)
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

// ═══════════════════════════════════════════════════════════════════════════════

// GetMockData — ukázková data pro preview

// ═══════════════════════════════════════════════════════════════════════════════

/// Vrátí ukázková data pro live preview v designeru.
/// Kompletní struktura se všemi kolekcemi a vnořenými poli.
ClassMethod GetMockData(ByRef sys As %String) As %Status
{
    s out = ##class(%DynamicObject).%New()

    // ── Skalární hodnoty ──────────────────────────────────────────────
    s vals = ##class(%DynamicObject).%New()
    d vals.%Set("CISLO_LINKY",            "42")
    d vals.%Set("LOKALNI_OZNACENI_LINKY", "A42")
    d vals.%Set("TITULEK",                "MHD v Brně")
    d vals.%Set("POPIS_LINKY",            "Líšeň–Bystrc")
    d vals.%Set("POPIS_DOPRAVCE",         "DPmB")
    d vals.%Set("POPIS_PLATNOSTI",        "01.01.2026 - 31.12.2026")
    d vals.%Set("DATUM_PLATNOSTI_OD",     "01.01.2026")
    d vals.%Set("DATUM_PLATNOSTI_DO",     "31.12.2026")
    d vals.%Set("POPIS_SMER",             "Bystrc, zoo")
    d vals.%Set("TYP_JR",                 "")
    d vals.%Set("QR_KOD",                 "https://jr.example.com/42")
    d vals.%Set("POPIS_ZASTAVKA",         "Líšeň, Novolíšeňská")
    d vals.%Set("CISLO_ZASTAVKA",         "101-A")
    d vals.%Set("POZNAMKA_POD_DOPRAVCE",  "")
    d vals.%Set("INFORMACE",              "")
    d vals.%Set("NEJEDOU_SPOJE",          "První spoj v 5:15, poslední v 23:30")
    d out.%Set("values", vals)

    // ── Kolekce ─────────────────────────────────────────────────────────
    s rows = ##class(%DynamicObject).%New()

    // ZASTAVKY
    s zastavky = ##class(%DynamicArray).%New()
    s z1 = ..BuildZastavka("1", "Líšeň, Novolíšeňská", "101", 0, 1, 1, 0)
    d ..AddZona(z1, "A1") d ..AddPrubeh(z1, "a") d ..AddPrubeh(z1, "b")
    d ..AddAtribut(z1, "♿", "Bezbariérový přístup")
    d zastavky.%Push(z1)
    s z2 = ..BuildZastavka("2", "Líšeň, Mifkova", "102", 3, 2, 0, 1)
    d ..AddZona(z2, "A1") d ..AddPoznamkaZastavky(z2, "A", "text", "Končí v zast. Rudolfov")
    d zastavky.%Push(z2)
    s z3 = ..BuildZastavka("3", "Šlapanice", "103", 8, 3, 0, 0, 2)
    d ..AddZona(z3, "A2") d ..AddPrubeh(z3, "a")
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
    d skupiny.%Push(..BuildSkupinaDnu("SMO1", "PRACOVNÍ DNY", "mimo prázdniny", 1))
    d skupiny.%Push(..BuildSkupinaDnu("SMO2", "SOBOTY", "", 2))
    d skupiny.%Push(..BuildSkupinaDnu("SMO3", "NEDĚLE A SVÁTKY", "", 3))
    d skupiny.%Push(..BuildSkupinaDnu("SMO4", "PRÁZDNINY", "pracovní dny", 4))
    d skupiny.%Push(..BuildSkupinaDnu("SMO5", "NOČNÍ PROVOZ", "Pá+So", 5))
    d rows.%Set("SKUPINY_DNU", skupiny)

    // ODJEZDY
    s odjezdy = ##class(%DynamicArray).%New()
    s o1 = ..BuildOdjezd("1", "SMO1", 5, 15) d odjezdy.%Push(o1)
    s o2 = ..BuildOdjezd("1", "SMO1", 5, 30) d ..AddAnotace(o2, "text", "a") d odjezdy.%Push(o2)
    d odjezdy.%Push(..BuildOdjezd("1", "SMO1", 5, 45))
    d odjezdy.%Push(..BuildOdjezd("1", "SMO1", 6, 0))
    s o5 = ..BuildOdjezd("1", "SMO1", 6, 15) d ..AddAnotace(o5, "specialni", "♿") d odjezdy.%Push(o5)
    d odjezdy.%Push(..BuildOdjezd("1", "SMO1", 6, 30))
    s o7 = ..BuildOdjezd("1", "SMO1", 6, 45) d ..AddAnotace(o7, "nz", "22") d odjezdy.%Push(o7)
    d odjezdy.%Push(..BuildOdjezd("1", "SMO1", 7, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "SMO1", 7, 15))
    d odjezdy.%Push(..BuildOdjezd("1", "SMO1", 7, 30))
    d odjezdy.%Push(..BuildOdjezd("1", "SMO1", 7, 45))
    d odjezdy.%Push(..BuildOdjezd("1", "SMO1", 8, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "SMO1", 8, 20))
    d odjezdy.%Push(..BuildOdjezd("1", "SMO1", 8, 40))
    d odjezdy.%Push(..BuildOdjezd("1", "SMO1", 9, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "SMO1", 9, 30))
    d odjezdy.%Push(..BuildOdjezd("1", "SMO1", 10, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "SMO1", 10, 30))
    d odjezdy.%Push(..BuildOdjezd("1", "SMO1", 11, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "SMO1", 11, 30))
    d odjezdy.%Push(..BuildOdjezd("1", "SMO2", 7, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "SMO2", 8, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "SMO2", 9, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "SMO2", 10, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "SMO2", 11, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "SMO2", 12, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "SMO2", 13, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "SMO2", 14, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "SMO3", 8, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "SMO3", 10, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "SMO3", 12, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "SMO3", 14, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "SMO3", 16, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "SMO4", 6, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "SMO4", 7, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "SMO4", 8, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "SMO4", 9, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "SMO4", 10, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "SMO4", 11, 0))
    d odjezdy.%Push(..BuildOdjezd("1", "SMO4", 12, 0))
    s on1 = ..BuildOdjezd("1", "SMO5", 23, 30) d odjezdy.%Push(on1)
    s on2 = ..BuildOdjezd("1", "SMO5", 0, 30) d ..AddAnotace(on2, "text", "a") d odjezdy.%Push(on2)
    d odjezdy.%Push(..BuildOdjezd("1", "SMO5", 1, 30))
    d odjezdy.%Push(..BuildOdjezd("1", "SMO5", 2, 30))
    d odjezdy.%Push(..BuildOdjezd("1", "SMO5", 3, 30))
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
    q 1
}

}
