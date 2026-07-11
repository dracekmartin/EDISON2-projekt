Include GENERAL

/// Agenda: **Linkový JŘ z MHD dat** — celá linka v jednom tisku (svislý seznam zastávek
/// × sloupce spojů), sourozenec `LinkovyJrAd`. Legacy ekvivalent: `ALVA.MHD.LinkyPrintFormatJR.MakeData`
/// (větev `VolanoZ="MHD"` v `ALVA.TISKJR.Data.LoadData`); stejný výstupní kontrakt jako mkPLinWin.
/// 
/// Datový zdroj (stejný jako `ZastavkoveJrMhd`):
/// - `^ALVA.MHD.LSO(IdFa,1/2/4,...)` — linka / spoje / trasa (ModelTrasaBodDetail)
/// - `^ALVA.MHD.ZST(IdFa,...)`       — zastávky (Cislo, ZonyID, Atributy)
/// - `^ALVA.MHD.ENUMS(IdFa,...)`     — číselníky (Zony, Atribut, PoznamkaSpoj, TypyProvozu)
/// - helpery `ALVA.MHD.*Fce` (GetTrasaId, GetNazevHrl, MakeTableSpoj, GetPoznamkySpoj,
///   GetAtributySpoj, GetJede, MakeRozpisTplProTypProvozuID, MakeOmezeniTXT)
/// 
/// Wire formát = shodný s `LinkovyJrAd` (konzumuje tentýž `LineTableElement`):
/// 1 stránka per (linka × směr); SPOJE.SKUPINA = TypProvozu (řazený dle ENUMS Poradi),
/// SKUPINY_DNU.NADPIS z TypyProvozu NadpisSMOPrvni/Druhy. Bez sloupců Km/Tč/MHD a bez
/// vyčká/navazuje (MHD zdroj je nemá — 1:1 s legacy MakeData).
/// 
/// ── Rozdíly/mezery vůči legacy MakeData (první řez) ──
/// - sloupec „OdjezdPříjezd" (typ 5, texty př/od) nahrazen ZASTAVKY.TYP="prijezd" podřádky
/// - výběr TypProvozu/Směru (arr,11) zatím bez parametru — tiskne vše
/// - NZ auto-poznámkování rozdílů platností (GetJede vs ProvozBitMapa → MakeOmezeniTXT) 1:1;
///   speciální znaky spoje (SpecZnak dny) a atributy B/C v řádku platnosti
Class ALVA.TemplateDesigner.Agendy.LinkovyJrMhd Extends ALVA.TemplateDesigner.Agendy.BaseAgenda
{

Parameter AgendaVariantCode As %String = "LINKOVY_JR_MHD";

Parameter Agenda As %String = "LINKOVY_JR";

Parameter AgendaLabel As %String = "Linkový JŘ";

Parameter Variant As %String = "MHD";

Parameter VariantLabel As %String = "MHD";

Parameter InstanceParamName As %String = "LINKA_ID";

Parameter ElementKind As %String = "LINE";

Parameter InstanceLabel As %String = "linka";

Parameter InstanceLabelPlural As %String = "linky";

/// Vrátí DataSchema — shodné s KnizniJrAd (stejný wire formát).
ClassMethod GetSchema(ByRef sys As %String) As %Status
{
    $$$FillParams

    s def = ##class(%DynamicObject).%New()

    s fields = ##class(%DynamicArray).%New()
    d ..AddField(fields, "CISLO_LINKY",           "Číslo linky",              "number")
    d ..AddField(fields, "POPIS_LINKY",           "Název linky",              "text")
    d ..AddField(fields, "POZNAMKA_NAD_LINKU",    "Poznámka nad linku",       "text")
    d ..AddField(fields, "POPIS_DOPRAVCE",        "Dopravce",                 "text")
    d ..AddField(fields, "PLATNOST_OD",           "Platnost od",              "text")
    d ..AddField(fields, "PLATNOST_DO",           "Platnost do",              "text")
    d ..AddField(fields, "POPIS_PLATNOSTI",       "Popis platnosti",          "text")
    d ..AddField(fields, "TYP_JR",                "Typ JŘ (razítko)",         "text")
    d ..AddField(fields, "QR_KOD",                "QR kód",                   "text")
    d ..AddField(fields, "SMER",                  "Směr (T/Z)",               "text")
    d ..AddField(fields, "POPIS_SMER",            "Konečná směru",            "text")
    d ..AddField(fields, "VYCHOZI_ZASTAVKA",      "Výchozí zastávka",         "text")
    s def.fields = fields

    s collections = ##class(%DynamicArray).%New()

    s g = ##class(%DynamicObject).%New()
    s g.name = "Zastávky", g.collectionKey = "ZASTAVKY"
    s gf = ##class(%DynamicArray).%New()
    d ..AddField(gf, "PORADI",     "Pořadí",             "number")
    d ..AddField(gf, "TYP",        "Typ řádku",          "text")
    d ..AddField(gf, "POPIS",      "Název zastávky",     "text")
    d ..AddField(gf, "ZONA",       "Zóna",               "text")
    s g.fields = gf
    d collections.%Push(g)

    s g = ##class(%DynamicObject).%New()
    s g.name = "Spoje", g.collectionKey = "SPOJE"
    s gf = ##class(%DynamicArray).%New()
    d ..AddField(gf, "CISLO_SPOJE", "Číslo spoje",       "number")
    d ..AddField(gf, "SKUPINA",     "Skupina (typ provozu)", "number")
    d ..AddField(gf, "NZ",          "Negativní značka",  "text")
    s g.fields = gf
    d collections.%Push(g)

    s g = ##class(%DynamicObject).%New()
    s g.name = "Skupiny dnů", g.collectionKey = "SKUPINY_DNU"
    s gf = ##class(%DynamicArray).%New()
    d ..AddField(gf, "ID",     "ID skupiny", "number")
    d ..AddField(gf, "NADPIS", "Nadpis",     "text")
    s g.fields = gf
    d collections.%Push(g)

    s g = ##class(%DynamicObject).%New()
    s g.name = "Poznámky", g.collectionKey = "POZNAMKY"
    s gf = ##class(%DynamicArray).%New()
    d ..AddField(gf, "DRUH", "Druh (1-10)",       "number")
    d ..AddField(gf, "TYP",  "Typ uvození",       "text")
    d ..AddField(gf, "ZNAK", "Znak",              "text")
    d ..AddField(gf, "TEXT", "Text",              "text")
    s g.fields = gf
    d collections.%Push(g)

    d def.%Set("fieldCollections", collections)

    s ^||ML("OUT", $i(^||ML("OUT"))) = def.%ToJSON()
    q $$$OK
}

/// Fixed šablony tabulek — knižní JŘ žádné nemá.
ClassMethod GetTableTemplates(ByRef sys As %String) As %Status
{
    s ^||ML("OUT", $i(^||ML("OUT"))) = "[]"
    q $$$OK
}

ClassMethod GetTableTemplate(ByRef sys As %String) As %Status
{
    s ^||ML("OUT", $i(^||ML("OUT"))) = "{}"
    q $$$OK
}

/// Parametry sestavy — zatím žádné (výběr typů provozu/směrů = budoucí rozšíření dle arr,11).
ClassMethod GetParameters(ByRef sys As %String) As %Status
{
    s ^||ML("OUT", $i(^||ML("OUT"))) = "[]"
    q $$$OK
}

/// Vrátí seznam linek pro Instance Picker — linky aktivní skupiny linek (vzor ZastavkoveJrMhd).
ClassMethod GetAvailableInstances(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams

        s search = $zcvt($g(^||ML("IN","search")), "L")
        s limit  = +$g(^||ML("IN","limit"))
        i 'limit s limit = 50

        s skupinaLinek = +$g(^||ML("IN","SKUPINA_LINEK_ID"))
        i 'skupinaLinek s skupinaLinek = ..DefaultSkupinaLinek(IdFa, dataDB)

        s arr = ##class(%DynamicArray).%New()
        s count = 0, linkaId = ""
        f {
            s linkaId = $o(^[dataDB]ALVA.MHD.LSO(IdFa,1,"IDX",1,1,1,skupinaLinek,linkaId))
            q:linkaId=""
            s cislo = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",linkaId,"DTA",1,"CisloVerejne"))
            s nazev = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",linkaId,"DTA",1,"NazevLinky"))
            s display = $s(cislo'="":cislo, 1:linkaId)
            i nazev '= "" s display = display_" — "_nazev
            i (search '= "") && ('$f($zcvt(display,"L"), search)) continue

            s obj = ##class(%DynamicObject).%New()
            d obj.%Set("value", linkaId)
            d obj.%Set("display", display)
            d arr.%Push(obj)
            s count = count + 1
            q:count>=limit
        }

        s ^||ML("OUT","1") = arr.%ToJSON()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

/// První nezablokovaná skupina linek (vzor ZastavkoveJrMhd.GetDefaultSkupinaLinekID).
ClassMethod DefaultSkupinaLinek(IdFa As %String, dataDB As %String) As %Integer
{
    s skupina = ""
    f {
        s skupina = $o(^[dataDB]ALVA.MHD.ENUMS(IdFa,"SkupinyLinek","DTA",skupina))
        q:skupina=""
        i '+$g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"SkupinyLinek","DTA",skupina,"Zablokovano")) q
    }
    i skupina = "" s skupina = $o(^[dataDB]ALVA.MHD.LSO(IdFa,1,"IDX",1,1,1,""))
    q +skupina
}

/// Vrátí data pro tisk: 1 stránka per (linka × směr) — směr 0 (T), pak 1 (Z).
ClassMethod GetData(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        k ^||TiskKnizniMhd

        s linkaId = $g(^||ML("IN","LINKA_ID"))
        i linkaId = "" s linkaId = $g(^||ML("IN","INSTANCE_ID"))
        i linkaId = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "LINKA_ID is required"))
        i '$d(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",linkaId)) $$$ThrowStatus($$$ERROR($$$GeneralError, "Linka "_linkaId_" neexistuje v ALVA.MHD.LSO"))

        f smerId = 0, 1 {
            s page = ..BuildStrankaLinky(.sys, IdFa, dataDB, linkaId, smerId)
            i page '= "" s ^||ML("OUT", $i(^||ML("OUT"))) = page.%ToJSON()
        }
    }
    catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

/// Ukázková data pro live preview — první linka výchozí skupiny (DEV vzor).
ClassMethod GetMockData(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        k ^||TiskKnizniMhd

        s skupinaLinek = ..DefaultSkupinaLinek(IdFa, dataDB)
        s linkaId = $o(^[dataDB]ALVA.MHD.LSO(IdFa,1,"IDX",1,1,1,skupinaLinek,""))
        s page = ""
        i linkaId '= "" s page = ..BuildStrankaLinky(.sys, IdFa, dataDB, linkaId, 0)

        i page = "" {
            s page = ##class(%DynamicObject).%New()
            s v = ##class(%DynamicObject).%New()
            d v.%Set("POPIS_LINKY", "(žádná MHD linka pro firmu "_IdFa_")")
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

/// Sestaví JSON stránku knižního MHD JŘ pro jednu linku a směr (0=T, 1=Z).
/// "" když směr nemá spoje. 1:1 datová část legacy MakeData (bez layout mechaniky).
ClassMethod BuildStrankaLinky(ByRef sys As %String, IdFa As %String, dataDB As %String, linkaId As %String, smerId As %Integer) As %DynamicObject
{
    // ── hlavička linky ──────────────────────────────────────────────
    s vals = ##class(%DynamicObject).%New()
    s cisloVerejne = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",linkaId,"DTA",1,"CisloVerejne"))
    d vals.%Set("CISLO_LINKY",         $s(cisloVerejne'="":cisloVerejne, 1:linkaId))
    d vals.%Set("POPIS_LINKY",         $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",linkaId,"DTA",1,"NazevLinky")))
    s platiOd = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",linkaId,"DTA",1,"PlatiOd"))
    s platiDo = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",linkaId,"DTA",1,"PlatiDo"))
    d vals.%Set("PLATNOST_OD",         platiOd)
    d vals.%Set("PLATNOST_DO",         platiDo)
    s popisPlatnosti = ""
    i platiOd '= "" {
        s popisPlatnosti = "Platí od "_platiOd
        i platiDo '= "" s popisPlatnosti = popisPlatnosti_" do "_platiDo
    }
    d vals.%Set("POPIS_PLATNOSTI",     popisPlatnosti)
    // razítko: Navrh / Vyluka na lince (1:1 zastávkový vzor)
    s typJr = ""
    i +$g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",linkaId,"DTA",1,"Vyluka")) s typJr = "VÝLUKOVÝ"
    i +$g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",linkaId,"DTA",1,"Navrh"))  s typJr = "NÁVRH"
    d vals.%Set("TYP_JR",              typJr)
    d vals.%Set("QR_KOD",              $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",linkaId,"DTA",1,"QRCode")))
    d vals.%Set("SMER",                $s(smerId=0:"T", 1:"Z"))
    d vals.%Set("POZNAMKA_NAD_LINKU",  "")
    d vals.%Set("POPIS_DOPRAVCE",      "")

    // ── trasa: body → zastávkové řádky (kategorie 1001, slučování sousedů, odjezd/příjezd) ──
    s trasaId = ##class(ALVA.MHD.LinkyFce).GetTrasaId(IdFa, dataDB, linkaId)
    i trasaId = "" q ""
    s hrlod = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",linkaId,"DTA",1,"PlatiOd"))
    s idsId = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",linkaId,"DTA",1,"IDSID"))
    s smerpar = $s(smerId=0:1, 1:-1)

    // 1. průchod: body → seznam (bod, zastávka, název, zóna, odjPri); filtr kategorie 1001
    k body                                       // body(i) = $lb(BodID, ZastavkaID, Nazev, Zona, odjPri)
    s pocetBodu = 0, bodId = ""
    f {
        s bodId = $o(^[dataDB]ALVA.MHD.LSO(IdFa,4,"DTA",trasaId,"DTA",1,"ModelTrasaBodDetail",bodId), smerpar)
        q:bodId=""
        s zastavkaId = +$g(^[dataDB]ALVA.MHD.LSO(IdFa,4,"DTA",trasaId,"DTA",1,"ModelTrasaBodDetail",bodId,"Val",$s(smerId=0:"ZastavkaTamID",1:"ZastavkaZpetID")))
        i 'zastavkaId continue
        i +$g(^[dataDB]ALVA.MHD.ZST(IdFa,1,"DTA",zastavkaId,"DTA",1,"KategorieZastavekID")) '= 1001 continue

        s nazev = $lg(##class(ALVA.MHD.ZastavkyFce).GetNazevHrl(IdFa, dataDB, zastavkaId, hrlod), 1)
        s cislo = $g(^[dataDB]ALVA.MHD.ZST(IdFa,1,"DTA",zastavkaId,"DTA",1,"Cislo"))

        // zóna: zóny zastávky filtrované na IDS linky (ENUMS Zony ZonaIDSID = IDSID linky)
        s zona = "", zonaCnt = ""
        f {
            s zonaCnt = $o(^[dataDB]ALVA.MHD.ZST(IdFa,1,"DTA",zastavkaId,"DTA",1,"ZonyID",zonaCnt))
            q:zonaCnt=""
            s zonaId = $g(^[dataDB]ALVA.MHD.ZST(IdFa,1,"DTA",zastavkaId,"DTA",1,"ZonyID",zonaCnt,"Itm"))
            i zonaId = "" continue
            i $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"Zony","DTA",zonaId,"ZonaIDSID")) '= idsId continue
            s zona = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"Zony","DTA",zonaId,"Cislo"))
            q:zona'=""
        }

        // odjezd/příjezd: atribut trasy 1011 na bodu (per směr)
        s odjPri = 0, atrCnt = ""
        f {
            s atrCnt = $o(^[dataDB]ALVA.MHD.LSO(IdFa,4,"DTA",trasaId,"DTA",1,"ModelTrasaBodDetail",bodId,"Val",$s(smerId=0:"AtributyTam",1:"AtributyZpet"),atrCnt))
            q:atrCnt=""
            i $g(^[dataDB]ALVA.MHD.LSO(IdFa,4,"DTA",trasaId,"DTA",1,"ModelTrasaBodDetail",bodId,"Val",$s(smerId=0:"AtributyTam",1:"AtributyZpet"),atrCnt,"Itm")) = 1011 s odjPri = 1
        }

        // slučování sousedních shodných zastávek (dle čísla zastávky, 1:1 legacy)
        i (pocetBodu > 0) && ($lg(body(pocetBodu),6) = cislo) && (cislo '= "") {
            s bodyMapa(bodId) = pocetBodu           // sloučený bod → řádek předchůdce
            continue
        }
        s pocetBodu = pocetBodu + 1
        s body(pocetBodu) = $lb(bodId, zastavkaId, nazev, zona, odjPri, cislo)
        s bodyMapa(bodId) = pocetBodu
    }
    i pocetBodu = 0 q ""

    // 2. průchod: zastávkové řádky vč. zdvojení příjezd/odjezd; rowMeta(i)=$lb(radekBodu, typ)
    s zastavky = ##class(%DynamicArray).%New()
    k rowMeta
    s pocetRadku = 0
    f i=1:1:pocetBodu {
        s odjPri = $lg(body(i),5)
        i odjPri {
            s pocetRadku = pocetRadku + 1
            s rowMeta(pocetRadku) = $lb(i, "P")
            d zastavky.%Push(..BuildZastavkaRadek(body(i), "prijezd", i))
        }
        s pocetRadku = pocetRadku + 1
        s rowMeta(pocetRadku) = $lb(i, "O")
        d zastavky.%Push(..BuildZastavkaRadek(body(i), "zastavka", i))
    }
    d vals.%Set("VYCHOZI_ZASTAVKA", $lg(body(1),3))
    d vals.%Set("POPIS_SMER",       $lg(body(pocetBodu),3))

    // ── spoje směru: MakeTableSpoj → časy per řádek; skupiny = TypProvozu ──
    d ..NactiSkupinyProvozu(IdFa, dataDB, .provozPoradi, .provozNadpis)

    // seřazení: (poradiProvozu, výchozí čas, spoj) — MHD legacy řadí uvnitř bloku TypProvozu
    k razeni
    s spojId = ""
    f {
        s spojId = $o(^[dataDB]ALVA.MHD.LSO(IdFa,2,"IDX",1,1,1,linkaId,spojId))
        q:spojId=""
        i +$g(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",spojId,"DTA",1,"SmerID")) '= smerId continue
        s typProvozu = +$g(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",spojId,"DTA",1,"TypProvozuID"))
        k arrtbsp
        d ##class(ALVA.MHD.SpojeFce).MakeTableSpoj(.sys, spojId, "", .arrtbsp)
        s prvniCnt = $o(arrtbsp("arr",1,""), smerpar)
        i prvniCnt = "" continue
        s vychoziCas = +$lg($g(arrtbsp("arr",1,prvniCnt)), 4)
        s razeni($g(provozPoradi(typProvozu), 999), typProvozu, vychoziCas, spojId) = ""
        m spojTabulka(spojId) = arrtbsp("arr",1)   // cache pro BuildSpojMhd
    }
    i '$d(razeni) q ""

    s spoje = ##class(%DynamicArray).%New()
    s skupinyDnu = ##class(%DynamicArray).%New()
    k nzSeen, poznSpoju, atributSeen, markerSeen
    s pocetPoznSpoju = 0
    s skupinaIdx = 0
    s pp = ""
    f {
        s pp = $o(razeni(pp))
        q:pp=""
        s typProvozu = ""
        f {
            s typProvozu = $o(razeni(pp, typProvozu))
            q:typProvozu=""
            s skupinaIdx = skupinaIdx + 1
            d skupinyDnu.%Push(..BuildSkupina(skupinaIdx, $g(provozNadpis(typProvozu))))
            s cas = ""
            f {
                s cas = $o(razeni(pp, typProvozu, cas))
                q:cas=""
                s spojId = ""
                f {
                    s spojId = $o(razeni(pp, typProvozu, cas, spojId))
                    q:spojId=""
                    s spojObj = ..BuildSpojMhd(.sys, IdFa, dataDB, linkaId, spojId, skupinaIdx, smerId, pocetRadku, .rowMeta, .body, .bodyMapa, .spojTabulka, .nzSeen, .poznSpoju, .pocetPoznSpoju, .atributSeen, .markerSeen)
                    i spojObj '= "" d spoje.%Push(spojObj)
                }
            }
        }
    }
    i spoje.%Size() = 0 q ""

    // ── poznámky (pořadí druhů: 1 dny, 2 NZ, 4 poznámky spoje, 5 atributy, zastávkové) ──
    s poznamky = ##class(%DynamicArray).%New()
    f pi=1:1:pocetPoznSpoju d poznamky.%Push(poznSpoju(pi))
    d ..PridatPoznamkyNZMhd(IdFa, dataDB, poznamky, .nzSeen)
    d ..PridatPoznamkyAtributuMhd(poznamky, .atributSeen)
    d ..PridatStaleZnackyMhd(poznamky, .markerSeen)
    d ..PridatAtributyZastavekMhd(IdFa, dataDB, .body, pocetBodu, poznamky)

    // ── sestavení stránky ───────────────────────────────────────────
    s out = ##class(%DynamicObject).%New()
    d out.%Set("values", vals)
    s rows = ##class(%DynamicObject).%New()
    d rows.%Set("ZASTAVKY", zastavky)
    d rows.%Set("SPOJE", spoje)
    d rows.%Set("SKUPINY_DNU", skupinyDnu)
    d rows.%Set("POZNAMKY", poznamky)
    d out.%Set("rows", rows)
    q out
}

/// JSON objekt zastávkového řádku z $lb body věty.
ClassMethod BuildZastavkaRadek(bodVeta As %List, typ As %String, poradi As %Integer) As %DynamicObject
{
    s z = ##class(%DynamicObject).%New()
    d z.%Set("PORADI",  poradi)
    d z.%Set("TYP",     typ)
    d z.%Set("CZST",    $lg(bodVeta,2))
    d z.%Set("POPIS",   $s(typ="prijezd":"", 1:$lg(bodVeta,3)))
    d z.%Set("ZONA",    $lg(bodVeta,4))
    d z.%Set("ATRIBUTY", ##class(%DynamicArray).%New())
    d z.%Set("KM",      ##class(%DynamicArray).%New())
    q z
}

/// Skupiny provozu: pořadí + nadpis z ENUMS TypyProvozu (NadpisSMOPrvni + NadpisSMODruhy).
ClassMethod NactiSkupinyProvozu(IdFa As %String, dataDB As %String, ByRef provozPoradi, ByRef provozNadpis)
{
    k provozPoradi, provozNadpis
    s tp = ""
    f {
        s tp = $o(^[dataDB]ALVA.MHD.ENUMS(IdFa,"TypyProvozu","DTA",tp))
        q:tp=""
        s provozPoradi(tp) = +$g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"TypyProvozu","DTA",tp,"Poradi"))
        s nadpis = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"TypyProvozu","DTA",tp,"NadpisSMOPrvni"))
        s nadpis2 = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"TypyProvozu","DTA",tp,"NadpisSMODruhy"))
        i nadpis2 '= "" s nadpis = nadpis_" "_nadpis2
        s provozNadpis(tp) = nadpis
    }
}

/// Sestaví JSON objekt jednoho MHD spoje: platnost (spec znaky + atributy), NZ, časy per řádek.
ClassMethod BuildSpojMhd(ByRef sys As %String, IdFa As %String, dataDB As %String, linkaId As %String, spojId As %String, skupina As %Integer, smerId As %Integer, pocetRadku As %Integer, ByRef rowMeta, ByRef body, ByRef bodyMapa, ByRef spojTabulka, ByRef nzSeen, ByRef poznSpoju, ByRef pocetPoznSpoju, ByRef atributSeen, ByRef markerSeen) As %DynamicObject
{
    s o = ##class(%DynamicObject).%New()
    d o.%Set("CISLO_SPOJE", +spojId)
    d o.%Set("SKUPINA", skupina)

    // NZ: přiřazená negativní značka + auto-poznámkování rozdílů platností (1:1 MakeData ř.496-527)
    s nz = ..NzSpoje(.sys, IdFa, dataDB, linkaId, spojId, .nzSeen)
    d o.%Set("NZ", nz)

    // řádek platnosti: speciální znaky spoje (dny ze SpecZnak) + atributy (B→BEV, C→CBE);
    // textové poznámky spoje → druh 4 legendy
    s platnost = ##class(%DynamicArray).%New()
    k arrPozn
    d ##class(ALVA.MHD.SpojeFce).GetPoznamkySpoj(.sys, spojId, "", .arrPozn)
    s pcnt = ""
    f {
        s pcnt = $o(arrPozn("arr",1,pcnt))
        q:pcnt=""
        s poznId = ""
        f {
            s poznId = $o(arrPozn("arr",1,pcnt,poznId))
            q:poznId=""
            s typPozn = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"PoznamkaSpoj","DTA",poznId,"TypPoznamkyID"))
            s text    = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"PoznamkaSpoj","DTA",poznId,"Text"))
            s popis   = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"PoznamkaSpoj","DTA",poznId,"PopisPoznamky"))
            i +typPozn {
                // speciální symbol — SpecZnak 1-7 = dny (Po-Ne), jinak ASCII znak
                s znak = ..ConvertSpecialniZnak($g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"PoznamkaSpoj","DTA",poznId,"SpecZnak")))
                i znak '= "" {
                    d platnost.%Push(znak)
                    d ..PridatPoznamkuSpoje(.poznSpoju, .pocetPoznSpoju,1, "znak", znak, popis)
                }
            }
            elseif text '= "" {
                d platnost.%Push(text)
                d ..PridatPoznamkuSpoje(.poznSpoju, .pocetPoznSpoju,4, "znak", text, popis)
            }
        }
    }
    // atributy spoje: B = bezbariérový (BEV), C = částečně bezbariérový (CBE)
    k arrAtr
    d ##class(ALVA.MHD.SpojeFce).GetAtributySpoj(.sys, spojId, "", .arrAtr)
    s acnt = ""
    f {
        s acnt = $o(arrAtr("arr",1,acnt))
        q:acnt=""
        s atrId = ""
        f {
            s atrId = $o(arrAtr("arr",1,acnt,atrId))
            q:atrId=""
            s znakAtr = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"Atribut","DTA",atrId,"Znak"))
            s popisAtr = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"Atribut","DTA",atrId,"Popis"))
            s klic = $case(znakAtr, "B":"BEV", "C":"CBE", :"")
            i klic '= "" {
                d platnost.%Push(klic)
                s atributSeen(klic) = ""
            }
        }
    }
    i nz '= "" d platnost.%Push(nz)
    d o.%Set("PLATNOST", platnost)

    // časy per řádek z MakeTableSpoj tabulky: $lb(ZastavkaID,Kategorie,CasPrijezdMM,CasOdjezdMM,isPrujezd,VzdalenostMM)
    // markery: "|" staví-nestaví (projíždí), "<" bod mezi začátkem a koncem jízdy kde spoj nejede
    k casyBodu                                   // casyBodu(radekBodu) = $lb(prijezdMM, odjezdMM, prujezd)
    s prvniRadek = "", posledniRadek = ""
    s tcnt = ""
    f {
        s tcnt = $o(spojTabulka(spojId, tcnt))
        q:tcnt=""
        s veta = spojTabulka(spojId, tcnt)
        s radek = $g(bodyMapa(tcnt))
        i radek = "" continue
        i $lg(veta,5) {
            i '$d(casyBodu(radek)) s casyBodu(radek) = $lb("", "", 1)   // projíždí
        }
        else {
            s casyBodu(radek) = $lb($lg(veta,3), $lg(veta,4), 0)
            i prvniRadek = "" s prvniRadek = radek
            s posledniRadek = radek
        }
    }
    i prvniRadek = "" q ""

    s casy = ##class(%DynamicArray).%New()
    f i=1:1:pocetRadku {
        s radekBodu = $lg(rowMeta(i),1), typRadku = $lg(rowMeta(i),2)
        i '$d(casyBodu(radekBodu)) {
            // mimo jízdu spoje: mezi začátkem a koncem = "<" (jede jinudy), jinak prázdno
            i (radekBodu > prvniRadek) && (radekBodu < posledniRadek) { d casy.%Push("<")  s markerSeen("<") = "" }
            else { d casy.%Push("") }
            continue
        }
        s cb = casyBodu(radekBodu)
        i $lg(cb,3) { d casy.%Push("|")  s markerSeen("|") = "" continue }
        s prijezd = +$lg(cb,1), odjezd = +$lg(cb,2)
        i typRadku = "P" {
            i radekBodu = prvniRadek { d casy.%Push("") }
            else { d casy.%Push(..FormatCas(prijezd)) }
        }
        else {
            i (radekBodu = posledniRadek) && ($lg(body(radekBodu),5)) { d casy.%Push("") }   // zdvojená konečná: jen příjezd
            else { d casy.%Push(..FormatCas($s(odjezd>prijezd:odjezd, 1:prijezd))) }
        }
    }
    d o.%Set("CASY", casy)
    d o.%Set("PRIPOJE", ##class(%DynamicArray).%New())   // MHD zdroj návaznosti nemá (1:1 legacy)

    q o
}

/// Negativní značka spoje + text omezení do legendy. RucniZadani spoje s bitmapou odlišnou od
/// kalendáře TypProvozu → OmezeniTXT (1:1 MakeData auto-poznámkování, GetJede + MakeOmezeniTXT).
ClassMethod NzSpoje(ByRef sys As %String, IdFa As %String, dataDB As %String, linkaId As %String, spojId As %String, ByRef nzSeen) As %String
{
    s nz = $g(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",spojId,"DTA",1,"NegativniZnacka"))
    i nz = "" q ""
    i $d(nzSeen(nz)) q nz

    // text omezení: rozdíl bitmapy spoje vs kalendář typu provozu
    s txt = ""
    i +$g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",linkaId,"DTA",1,"AutomatickyPoznamkovatOdlisnostiVPlatnostechSpoju")) {
        try {
            s provozBitmapa = $g(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",spojId,"DTA",1,"ProvozBitMapa"))
            s typProvozu = +$g(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",spojId,"DTA",1,"TypProvozuID"))
            s p1 = $p(provozBitmapa,"~",1), p2 = $p(provozBitmapa,"~",2), bmp = $p(provozBitmapa,"~",3)
            i bmp '= "" {
                k arrKal
                d ##class(ALVA.MHD.KalendarFce).GetJede(IdFa, dataDB, p1, p2, typProvozu, "", .arrKal)
                s cbmp = $lg($g(arrKal("dta",1)), 3)
                i (cbmp '= "") && (cbmp '= bmp) {
                    k arri, arrRozpis
                    d ##class(ALVA.MHD.KalendarFce).MakeRozpisTplProTypProvozuID(IdFa, dataDB, typProvozu, .arrRozpis)
                    s arri("dta",1) = p1, arri("dta",2) = p2, arri("dta",3) = cbmp, arri("dta",4) = bmp
                    d mkOmzFromOnlyTwoBitmaps^JRapi(.arri)
                    k arrTxt
                    d ##class(ALVA.MHD.KalendarFce).MakeOmezeniTXT(.arri, .arrTxt)
                    s txt = $g(arrTxt("dta",1))
                }
            }
        } catch (exNz) { s txt = "" }              // auto-poznámkování je best-effort; NZ znak zůstává
    }
    s nzSeen(nz) = txt
    q nz
}

/// Přidá poznámku spoje do bufferu (dedup přes znak+text).
ClassMethod PridatPoznamkuSpoje(ByRef poznSpoju, ByRef pocet, druh As %Integer, typ As %String, znak As %String, text As %String)
{
    i $d(poznSpoju("seen", druh, znak, text)) q
    s poznSpoju("seen", druh, znak, text) = ""
    s pocet = $i(pocet)
    s poznSpoju(pocet) = ..BuildPoznamka(druh, typ, znak, text)
}

/// Poznámky druhu 2 — NZ s textem omezení (nasbírané v NzSpoje).
ClassMethod PridatPoznamkyNZMhd(IdFa As %String, dataDB As %String, poznamky As %DynamicArray, ByRef nzSeen)
{
    s nz = ""
    f {
        s nz = $o(nzSeen(nz))
        q:nz=""
        s txt = nzSeen(nz)
        i txt '= "" d poznamky.%Push(..BuildPoznamka(2, "nz", nz, txt))
    }
}

/// Poznámky druhu 5 — atributy spoje (BEV/CBE).
ClassMethod PridatPoznamkyAtributuMhd(poznamky As %DynamicArray, ByRef atributSeen)
{
    i $d(atributSeen("BEV")) d poznamky.%Push(..BuildPoznamka(5, "znak", "BEV", "spoj s bezbariérově přístupným vozidlem"))
    i $d(atributSeen("CBE")) d poznamky.%Push(..BuildPoznamka(5, "znak", "CBE", "spoj s částečne bezbariérově přístupným vozidlem, nutná dopomoc průvodce"))
}

/// Poznámky druhu 7 — markery matice.
ClassMethod PridatStaleZnackyMhd(poznamky As %DynamicArray, ByRef markerSeen)
{
    i $d(markerSeen("|")) d poznamky.%Push(..BuildPoznamka(7, "znak", "|", "spoj příslušnou zastávkou projíždí"))
    i $d(markerSeen("<")) d poznamky.%Push(..BuildPoznamka(7, "znak", "<", "spoj jede po jiné trase"))
}

/// Atributy zastávek (ZST Atributy → ENUMS Atribut, filtr znaků N/V/x/A jako legacy) → legenda druhu 7
/// + znak k zastávce se v MHD tiskne u názvu (v prvním řezu jen legenda).
ClassMethod PridatAtributyZastavekMhd(IdFa As %String, dataDB As %String, ByRef body, pocetBodu As %Integer, poznamky As %DynamicArray)
{
    k seen
    f i=1:1:pocetBodu {
        s zastavkaId = $lg(body(i),2)
        s atrCnt = ""
        f {
            s atrCnt = $o(^[dataDB]ALVA.MHD.ZST(IdFa,1,"DTA",zastavkaId,"DTA",1,"Atributy",atrCnt))
            q:atrCnt=""
            s atrId = $g(^[dataDB]ALVA.MHD.ZST(IdFa,1,"DTA",zastavkaId,"DTA",1,"Atributy",atrCnt,"Itm"))
            i atrId = "" continue
            s znak = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"Atribut","DTA",atrId,"Znak"))
            i ",N,V,x,A," '[ (","_znak_",") continue     // legacy filtr — jen tyto se tisknou
            i $d(seen(znak)) continue
            s seen(znak) = ""
            s popis = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"Atribut","DTA",atrId,"Popis"))
            d poznamky.%Push(..BuildPoznamka(7, "znak", znak, popis))
        }
    }
}

/// JSON objekt jedné poznámky legendy.
ClassMethod BuildPoznamka(druh As %Integer, typ As %String, znak As %String, text As %String) As %DynamicObject
{
    s o = ##class(%DynamicObject).%New()
    d o.%Set("DRUH", druh)
    d o.%Set("TYP", typ)
    d o.%Set("ZNAK", znak)
    d o.%Set("TEXT", text)
    q o
}

ClassMethod BuildSkupina(id As %Integer, nadpis As %String) As %DynamicObject
{
    s o = ##class(%DynamicObject).%New()
    d o.%Set("ID", id)
    d o.%Set("NADPIS", nadpis)
    q o
}

/// Formát času HH:MM z minut od půlnoci.
ClassMethod FormatCas(minuty As %Integer) As %String
{
    s hod = (minuty\60)#24, min = minuty#60
    q $tr($j(hod,2)," ",0)_":"_$tr($j(min,2)," ",0)
}

/// SpecZnak → zobrazovaný znak: 1-7 = dny Po-Ne, >31 = ASCII znak (1:1 ZastavkoveJrMhd).
ClassMethod ConvertSpecialniZnak(cislo As %String) As %String
{
    i cislo = "" q ""
    i (cislo >= 1) && (cislo <= 7) q $p("Po,Út,St,Čt,Pá,So,Ne", ",", cislo)
    i cislo > 31 q $c(cislo)
    q cislo
}

}
