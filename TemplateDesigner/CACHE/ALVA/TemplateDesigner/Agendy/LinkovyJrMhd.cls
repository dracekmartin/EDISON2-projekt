Include GENERAL

/// Agenda: **Linkový JŘ z MHD dat** - celá linka v jednom tisku (svislý seznam zastávek
/// × sloupce spojů), sourozenec `LinkovyJrAd`. Legacy ekvivalent: `ALVA.MHD.LinkyPrintFormatJR.MakeData`
/// (větev `VolanoZ="MHD"` v `ALVA.TISKJR.Data.LoadData`); stejný výstupní kontrakt jako mkPLinWin.
/// 
/// Datový zdroj (stejný jako `ZastavkoveJrMhd`):
/// - `^ALVA.MHD.LSO(IdFa,1/2/4,...)` - linka / spoje / trasa (ModelTrasaBodDetail)
/// - `^ALVA.MHD.ZST(IdFa,...)`       - zastávky (Cislo, ZonyID, Atributy)
/// - `^ALVA.MHD.ENUMS(IdFa,...)`     - číselníky (Zony, Atribut, PoznamkaSpoj, TypyProvozu)
/// - helpery `ALVA.MHD.*Fce` (GetTrasaId, GetNazevHrl, MakeTableSpoj, GetPoznamkySpoj,
///   GetAtributySpoj, GetJede, MakeRozpisTplProTypProvozuID, MakeOmezeniTXT)
/// 
/// Wire formát = shodný s `LinkovyJrAd` (konzumuje tentýž `LineTableElement`):
/// 1 stránka per (linka × směr); SPOJE.SKUPINA = TypProvozu (řazený dle ENUMS Poradi),
/// SKUPINY_DNU.NADPIS z TypyProvozu NadpisSMOPrvni/Druhy. Bez sloupců Km/Tč/MHD a bez
/// vyčká/navazuje (MHD zdroj je nemá - 1:1 s legacy MakeData).
/// 
/// ── Rozdíly/mezery vůči legacy MakeData ──
/// - sloupec „OdjezdPříjezd" (typ 5, texty př/od) nahrazen ZASTAVKY.TYP="prijezd" podřádky
/// - výběr typů provozu (arr,11) = parametry TYP_PROVOZU_<id>; směr volí SMER_TISKU
/// - značky spoje a negativní značky skládá jako legacy (:490-695), porovnání viz
///   `Tests/LinkovyJrMhdTest`; z atributů spoje jen B/C, ostatní legacy tiskne id znaku (neověřeno)
Class ALVA.TemplateDesigner.Agendy.LinkovyJrMhd Extends ALVA.TemplateDesigner.Agendy.BaseAgenda
{

Parameter AgendaVariantCode As %String = "LINKOVY_JR_MHD";

Parameter Agenda As %String = "LINKOVY_JR";

Parameter AgendaLabel As %String = "Linkový JŘ";

Parameter Variant As %String = "MHD";

Parameter VariantLabel As %String = "MHD";

/// Zveřejněná 23. 10. do verze 27. 10. (`B191f`): tabulka sedí se starým tiskem na 65 linkách CB_DP
/// a KV_DP. Na datech zákazníka po vydání ověřit poznámky z trasy, příjezd/odjezd na zastávce
/// a atributy spoje mimo B/C - dev data je nemají (rozhodnutí Jan 23. 10.).
/// ⚠️ `Released` je serverová hodnota, ne v exe - nasazení třídy změní nabídku agend všem.
Parameter Released = 1;

Parameter InstanceParamName As %String = "LINKA_ID";

Parameter ElementKind As %String = "LINE";

Parameter InstanceLabel As %String = "linka";

Parameter InstanceLabelPlural As %String = "linky";

/// Vrátí DataSchema - shodné s LinkovyJrAd (stejný wire formát).
ClassMethod GetSchema(ByRef sys As %String) As %Status
{
    $$$FillParams

    s def = ##class(%DynamicObject).%New()

    s fields = ##class(%DynamicArray).%New()
    // Názvy i popisky polí drží slovník zastávkového JŘ (AgendaSchemaDriftTests) - táž věc se musí
    // v obou agendách jmenovat stejně, jinak binding {NAZEV_ZASTAVKY} v jedné funguje a ve druhé
    // tiše vykreslí prázdno.
    // Pořadí bloků jako ve všech agendách JŘ (Jan 27. 10.): linka → dopravce → platnost → zastávka
    // a směr → poznámky a doplňky. Nabídka dat pole nepřeřazuje, ukazuje je v tomhle pořadí.
    d ..AddField(fields, "CISLO_LINKY",           "Číslo linky (dle licence)", "number")
    d ..AddField(fields, "LOKALNI_OZNACENI_LINKY", "Veřejné označení linky",  "text")
    d ..AddField(fields, "POPIS_LINKY",           "Název linky",              "text")
    d ..AddField(fields, "TYP_JR",                "Typ jízdního řádu",        "text")
    // Bez „Poznámky nad linku": linka MHD ji má v datech (PoznamkaNadLinku), ale modul MHD ji
    // zatím nenabízí k zadání (Jan 23. 10.). Až půjde zadávat, pole se přidá zpátky.
    d ..AddField(fields, "POPIS_DOPRAVCE",        "Popis dopravce",           "text")
    // Po polích pod stejnými klíči jako ostatní agendy JŘ (`B194c` D4).
    d ..AddField(fields, "NAZEV_DOPRAVCE",        "Název dopravce",           "text")
    d ..AddField(fields, "ADRESA_DOPRAVCE",       "Adresa dopravce",          "text")
    d ..AddField(fields, "TELEFON_DOPRAVCE",      "Telefon dopravce",         "text")
    d ..AddField(fields, "EMAIL_DOPRAVCE",        "E-mail dopravce",          "text")
    d ..AddField(fields, "ICO_DOPRAVCE",          "IČO dopravce",             "text")
    // Bez „Popisu platnosti" (Jan 23. 10.): větu si šablona složí z dat od-do. Agenda nevyšla,
    // takže ho na rozdíl od vydaných agend neposílá ani v datech.
    d ..AddField(fields, "DATUM_PLATNOSTI_OD",    "Datum platnosti od",       "date")
    d ..AddField(fields, "DATUM_PLATNOSTI_DO",    "Datum platnosti do",       "date")
    d ..AddField(fields, "POPIS_ZASTAVKA",         "Název aktuální zastávky", "text")
    d ..AddField(fields, "AKTUALNI_ZASTAVKA_CZST", "Číslo aktuální zastávky", "number")
    d ..AddField(fields, "SMER",                  "Směr (T/Z)",               "text")
    d ..AddField(fields, "VYCHOZI_ZASTAVKA",      "Výchozí zastávka",         "text")
    d ..AddField(fields, "KONECNA_ZASTAVKA",      "Konečná zastávka",         "text")
    d ..AddField(fields, "QR_KOD",                "URL adresa pro QR kód",    "text")
    s def.fields = fields

    s collections = ##class(%DynamicArray).%New()

    s g = ##class(%DynamicObject).%New()
    s g.name = "Zastávky", g.collectionKey = "ZASTAVKY"
    s gf = ##class(%DynamicArray).%New()
    d ..AddField(gf, "PORADI",         "Pořadí",             "number")
    // Bez „Typu řádku" (Jan 27. 10.): interní kód zastavka/prijezd pro Tabulku spojů, data ho posílají dál.
    d ..AddField(gf, "CZST",           "Číslo zastávky",     "number")
    d ..AddField(gf, "NAZEV_ZASTAVKY", "Název zastávky",     "text")
    d ..AddField(gf, "ZONY",           "Zóny",               "text")
    d ..AddField(gf, "NASTUPISTE",     "Označník",           "text")
    d ..AddField(gf, "AKTUALNI_ZASTAVKA", "Je aktuální zastávka", "boolean")
    s g.fields = gf
    d collections.%Push(g)

    s g = ##class(%DynamicObject).%New()
    s g.name = "Spoje", g.collectionKey = "SPOJE"
    s gf = ##class(%DynamicArray).%New()
    // SKUPINA (spojovací klíč na SKUPINY_DNU) zůstává ve wire, ale NEvystavuje se jako sloupec -
    // stejně jako ZASTAVKA_ID/SKUPINA_DNU_ID u zastávkových odjezdů.
    d ..AddField(gf, "CISLO_SPOJE", "Číslo spoje",       "number")
    d ..AddField(gf, "NZ",          "Negativní značka",  "text")
    s g.fields = gf
    d collections.%Push(g)

    s g = ##class(%DynamicObject).%New()
    s g.name = "Skupiny dnů", g.collectionKey = "SKUPINY_DNU"
    s gf = ##class(%DynamicArray).%New()
    // ID (spojovací klíč) zůstává ve wire, ale NEvystavuje se jako sloupec.
    d ..AddField(gf, "NADPIS", "Nadpis skupiny", "text")
    s g.fields = gf
    d collections.%Push(g)

    s g = ##class(%DynamicObject).%New()
    s g.name = "Poznámky", g.collectionKey = "POZNAMKY"
    s gf = ##class(%DynamicArray).%New()
    d ..AddField(gf, "DRUH", "Druh poznámky",     "number")
    // Bez „Typu poznámky" (Jan 27. 10.): interní kód znak/nz/text pro prvek Poznámky, data ho posílají dál.
    d ..AddField(gf, "ZNAK", "Znak",              "text")
    d ..AddField(gf, "TEXT", "Text poznámky",     "text")
    s g.fields = gf
    d collections.%Push(g)

    d def.%Set("fieldCollections", collections)

    s ^||ML("OUT", $i(^||ML("OUT"))) = def.%ToJSON()
    q $$$OK
}

/// Fixed šablony tabulek - knižní JŘ žádné nemá.
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

/// Parametry sestavy. Směr pro tisk je týž parametr jako u AD varianty (definici drží ona).
/// Typy provozu jsou zaškrtávátko pro každý nezablokovaný typ firmy (`TYP_PROVOZU_<id>`), jako
/// starý dialog před tiskem (`LinkyPrintBIGFilterED.cls:115-123`) - typy firmy, ne linky, protože
/// parametry se berou bez znalosti instance.
ClassMethod GetParameters(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        s arr = ##class(%DynamicArray).%New()
        d arr.%Push(##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).ParametrSmeruTisku())
        // Až 3 strany - městská linka má víc spojů než meziměstská (Jan 22. 10.).
        d arr.%Push(##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).ParametrPoctuStran(0, 3))
        // Bez „Automaticky (dle linky)" - linka MHD to nastavení nemá; výchozí Tisknout jako starý tisk.
        d arr.%Push(##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).ParametrZastavkyBezZastaveni(0))
        // Bez Zvýraznit tarifní číslo - MHD sloupec Tč nemá (rozsah `B191d`, analýza §18.9).
        d arr.%Push(##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).ParametrZvyrazneniZastavky(0))
        d ..NactiSkupinyProvozu(IdFa, dataDB, .provozPoradi, .provozNadpis)
        // Stejný název mají v číselníku i různé typy (KV_DP: dvakrát „Sobota", dvakrát „Neděle"),
        // takové se v panelu rozliší číslem typu.
        k nazvy
        s tp = ""
        f {
            s tp = $o(^[dataDB]ALVA.MHD.ENUMS(IdFa,"TypyProvozu","DTA",tp))
            q:tp=""
            i +$g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"TypyProvozu","DTA",tp,"Blocked")) continue
            s nazev = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"TypyProvozu","DTA",tp,"Nazev"))
            s:nazev'="" nazvy(nazev) = $g(nazvy(nazev)) + 1
        }
        s tp = ""
        f {
            s tp = $o(^[dataDB]ALVA.MHD.ENUMS(IdFa,"TypyProvozu","DTA",tp))
            q:tp=""
            i +$g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"TypyProvozu","DTA",tp,"Blocked")) continue
            s nazev = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"TypyProvozu","DTA",tp,"Nazev"))
            s cislo = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"TypyProvozu","DTA",tp,"Cislo"), tp)
            i nazev = "" { s nazev = cislo }
            elseif $g(nazvy(nazev)) > 1 { s nazev = nazev_" (č. "_cislo_")" }
            s popis = "Spoje tohoto typu provozu se vytisknou jako samostatný blok. Vypnutý typ provozu se netiskne."
            i $g(provozNadpis(tp)) '= "" s popis = "Nadpis bloku na tisku: „"_provozNadpis(tp)_"“. "_popis
            // Skupina podle prvku jako ostatní parametry (`B194c` P4, do 26. 10. vlastní skupina „Typy
            // provozu pro tisk"); předpona v popisku je odliší od ostatních voleb Tabulky spojů.
            d ##class(ALVA.TemplateDesigner.Agendy.BaseAgenda).AddBoolParam(arr, ..ParametrTypuProvozu(tp), "Typ provozu: "_nazev, 1, "Tabulka spojů", popis, 1)
            // Firma má typů provozu desítky, linka pár - návrhář ukáže jen ty, které načtená linka má.
            d arr.%Get(arr.%Size() - 1).%Set("visibleWhenListedIn", "TYPY_PROVOZU_LINKY")
        }
        s ^||ML("OUT", $i(^||ML("OUT"))) = arr.%ToJSON()
    } catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

/// Název parametru sestavy pro typ provozu. ⚠️ Id typu provozu je číslo v rámci firmy - týž název
/// parametru znamená v jiné firmě jiný typ provozu.
ClassMethod ParametrTypuProvozu(tp As %String) As %String
{
    q "TYP_PROVOZU_"_tp
}

/// Tiskne se typ provozu? Vypnutý je jen výslovným "false"; chybějící hodnota (starší klient,
/// zablokovaný typ, který parametr nemá) = tisknout, jako před zavedením výběru.
ClassMethod TypProvozuSeTiskne(tp As %String) As %Boolean
{
    q $zcvt($g(^||ML("IN", ..ParametrTypuProvozu(tp))), "L") '= "false"
}

/// Skupiny linek pro filtr výběru instancí - tytéž jako u vývěsky MHD (týž číselník).
/// Bez téhle metody dialog přepínač skryje a uživatel zůstane na výchozí skupině.
ClassMethod GetAvailableSkupiny(ByRef sys As %String) As %Status
{
    q ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd).GetAvailableSkupiny(.sys)
}

/// Linky pro výběr instancí stejně jako u vývěsky MHD (číslo, název, platnost, i linky bez trasy).
/// Do 23. 10. tu byla vlastní kopie, která se rozešla v příznaku zablokované skupiny.
ClassMethod GetAvailableInstances(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        s ^||ML("OUT","1") = ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd).SeznamLinek(IdFa, dataDB).%ToJSON()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

/// Vrátí data pro tisk: 1 stránka per (linka × směr) - směr 0 (T), pak 1 (Z); `SMER_TISKU` vybere jen jeden.
ClassMethod GetData(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        k ^||TiskKnizniMhd
        d ..ZacitDuvodyPrazdne()

        s linkaId = $g(^||ML("IN","LINKA_ID"))
        i linkaId = "" s linkaId = $g(^||ML("IN","INSTANCE_ID"))
        i linkaId = "" d ..ChybaDat("Není vybraná linka. Vyberte linky k tisku.")
        i '$d(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",linkaId)) d ..ChybaDat("Vybraná linka v modulu MHD není, mohla být smazána. Vyberte linky znovu.")

        s smery = ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).SmeryKTisku($g(^||ML("IN","SMER_TISKU")))
        d ..PriradNegativniZnacky(.sys, IdFa, dataDB, linkaId, smery, .znacky)
        s typyLinky = ..TypyProvozuLinky(IdFa, dataDB, linkaId)
        f si = 1:1:$l(smery, ",") {
            s smerId = $s($p(smery, ",", si) = "Z":1, 1:0)
            s page = ..BuildStrankaLinky(.sys, IdFa, dataDB, linkaId, smerId, .znacky)
            i page = "" continue
            d page.values.%Set("TYPY_PROVOZU_LINKY", typyLinky)
            s ^||ML("OUT", $i(^||ML("OUT"))) = page.%ToJSON()
        }
        // Linka bez jediné stránky: důvod a rada místo prázdna (starý tisk tu spadl na `<SUBSCRIPT>`).
        d ..VydatDuvodPrazdne()
    }
    catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

/// Spoje mají časy, ale ani jeden na zastávce, kterou linkový JŘ tiskne (KV_DP 1106).
ClassMethod DuvodBezZastaveni() As %String
{
    q "Žádný spoj linky nestaví na zastávce kategorie Zastávka - časy mají jen body trasy jiné kategorie. Zkontrolujte zastávky na trase linky v modulu MHD."
}

/// Parametry typů provozu, které mají na lince aspoň jeden spoj (oba směry), jako JSON pole názvů
/// parametrů. Nezávisí na výběru typů ani směru tisku, jinak by vypnutý typ z panelu zmizel a nešel
/// by zapnout zpátky.
ClassMethod TypyProvozuLinky(IdFa As %String, dataDB As %String, linkaId As %String) As %DynamicArray
{
    k typy
    s spojId = ""
    f {
        s spojId = $o(^[dataDB]ALVA.MHD.LSO(IdFa,2,"IDX",1,1,1,linkaId,spojId))
        q:spojId=""
        s typy(+$g(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",spojId,"DTA",1,"TypProvozuID"))) = ""
    }
    s arr = ##class(%DynamicArray).%New(), tp = ""
    f {
        s tp = $o(typy(tp))
        q:tp=""
        d arr.%Push(..ParametrTypuProvozu(tp))
    }
    q arr
}

/// Ukázková data pro live preview - první linka výchozí skupiny (DEV vzor).
ClassMethod GetMockData(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        k ^||TiskKnizniMhd

        s skupinaLinek = ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd).GetDefaultSkupinaLinekID(IdFa, dataDB)
        s linkaId = $o(^[dataDB]ALVA.MHD.LSO(IdFa,1,"IDX",1,1,1,skupinaLinek,""))
        s page = ""
        i linkaId '= "" {
            d ..PriradNegativniZnacky(.sys, IdFa, dataDB, linkaId, "T", .znacky)
            s page = ..BuildStrankaLinky(.sys, IdFa, dataDB, linkaId, 0, .znacky)
            // Jako GetData: bez toho by panel bez vybrané linky ukázal všechny typy provozu firmy.
            i page '= "" d page.values.%Set("TYPY_PROVOZU_LINKY", ..TypyProvozuLinky(IdFa, dataDB, linkaId))
        }

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

/// Sestaví JSON stránku knižního MHD JŘ pro jednu linku a směr (0=T, 1=Z).
/// "" když směr nemá spoje. `znacky` = výsledek `PriradNegativniZnacky` pro celou linku.
ClassMethod BuildStrankaLinky(ByRef sys As %String, IdFa As %String, dataDB As %String, linkaId As %String, smerId As %Integer, ByRef znacky) As %DynamicObject
{
    // ── hlavička linky ──────────────────────────────────────────────
    s vals = ##class(%DynamicObject).%New()
    // Rozdělení licenční × veřejné číslo je shodné se zastávkovou vývěskou (ZastavkoveJrMhd:318) -
    // do 17. 8. tu bylo ve CISLO_LINKY veřejné číslo, takže táž linka měla na vývěsce a v linkovém
    // JŘ jiné číslo, aniž to šlo poznat z popisku pole.
    d vals.%Set("CISLO_LINKY",         $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",linkaId,"DTA",1,"CisloLinkyDleLicence")))
    d vals.%Set("LOKALNI_OZNACENI_LINKY", $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",linkaId,"DTA",1,"CisloVerejne")))
    d vals.%Set("POPIS_LINKY",         $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",linkaId,"DTA",1,"NazevLinky")))
    // LSO drží datum v ISO tvaru; ven jde ve stejném tvaru jako ze zastávkové vývěsky
    // (ZastavkoveJrMhd:1104), jinak by táž šablona vytiskla jednou "1.11.2018" a podruhé "2018-11-01".
    s platiOd = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",linkaId,"DTA",1,"PlatiOd"))
    s platiDo = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",linkaId,"DTA",1,"PlatiDo"))
    i platiOd '= "" { s platiOd = ..FormatDatum($zdh(platiOd, 3)) }
    i platiDo '= "" { s platiDo = ..FormatDatum($zdh(platiDo, 3)) }
    d vals.%Set("DATUM_PLATNOSTI_OD",  platiOd)
    d vals.%Set("DATUM_PLATNOSTI_DO",  platiDo)
    // Razítko jako vývěska MHD (do 23. 10. tu při návrhu i výluce vyhrál návrh); starý knižní tisk ho nemá.
    d vals.%Set("TYP_JR",              ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd).TypJrLinky(IdFa, dataDB, linkaId))
    d vals.%Set("QR_KOD",              $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",linkaId,"DTA",1,"QRCode")))
    d vals.%Set("SMER",                $s(smerId=0:"T", 1:"Z"))
    // Dopravce z téhož zdroje jako vývěska MHD, jen jedním řádkem jako linkový AD. Starý knižní
    // tisk MHD ho neposílal (`LinkyPrintFormatJR.cls:1100-1106`); tatáž linka ho ale má na vývěsce
    // (rozhodnutí Jan 23. 10.).
    d vals.%Set("POPIS_DOPRAVCE",      ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd).PopisDopravce(.sys, dataDB, IdFa, linkaId, 1))
    d ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd).DopravceSkalary(.sys, dataDB, IdFa, linkaId, vals)

    // ── trasa: body → zastávkové řádky (kategorie 1001, slučování sousedů, odjezd/příjezd) ──
    s trasaId = ##class(ALVA.MHD.LinkyFce).GetTrasaId(IdFa, dataDB, linkaId)
    i trasaId = "" q ..PrazdnaStranka("Linka nemá zadanou trasu. Doplňte trasu u linky v modulu MHD.")
    // Název zastávky k datu platnosti linky chce $h, LSO drží ISO text (legacy $$$EDDN, :327).
    // Do 12. 10. tu šel text a vybral se jiný název (KV_DP 1438: „KV Arena" místo „MATTONI Arena").
    s hrlod = $zdh($g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",linkaId,"DTA",1,"PlatiOd")),3,,,,,,,-1)
    s idsId = $g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",linkaId,"DTA",1,"IDSID"))
    s smerpar = $s(smerId=0:1, 1:-1)

    // 1. průchod: body → seznam (bod, zastávka, název, zóna, odjPri); filtr kategorie 1001
    k body, popisyZnacek, popisyPoznamekTrasy    // body(i) = $lb(BodID, ZastavkaID, Nazev, Zona, odjPri, Cislo, Oznacnik, ZnackyBodu, PoznamkyBodu)
    s pocetBodu = 0, bodId = ""
    f {
        s bodId = $o(^[dataDB]ALVA.MHD.LSO(IdFa,4,"DTA",trasaId,"DTA",1,"ModelTrasaBodDetail",bodId), smerpar)
        q:bodId=""
        s zastavkaId = +$g(^[dataDB]ALVA.MHD.LSO(IdFa,4,"DTA",trasaId,"DTA",1,"ModelTrasaBodDetail",bodId,"Val",$s(smerId=0:"ZastavkaTamID",1:"ZastavkaZpetID")))
        i 'zastavkaId continue
        i +$g(^[dataDB]ALVA.MHD.ZST(IdFa,1,"DTA",zastavkaId,"DTA",1,"KategorieZastavekID")) '= 1001 continue

        s nazev = $lg(##class(ALVA.MHD.ZastavkyFce).GetNazevHrl(IdFa, dataDB, zastavkaId, hrlod), 1)
        s cislo = $g(^[dataDB]ALVA.MHD.ZST(IdFa,1,"DTA",zastavkaId,"DTA",1,"Cislo"))

        s zona = ..ZonyZastavky(IdFa, dataDB, zastavkaId, idsId)

        // odjezd/příjezd: atribut trasy 1011 na bodu (per směr)
        s odjPri = 0, atrCnt = ""
        f {
            s atrCnt = $o(^[dataDB]ALVA.MHD.LSO(IdFa,4,"DTA",trasaId,"DTA",1,"ModelTrasaBodDetail",bodId,"Val",$s(smerId=0:"AtributyTam",1:"AtributyZpet"),atrCnt))
            q:atrCnt=""
            i $g(^[dataDB]ALVA.MHD.LSO(IdFa,4,"DTA",trasaId,"DTA",1,"ModelTrasaBodDetail",bodId,"Val",$s(smerId=0:"AtributyTam",1:"AtributyZpet"),atrCnt,"Itm")) = 1011 s odjPri = 1
        }

        s znackyBodu = ..ZnackyBodu(IdFa, dataDB, zastavkaId, trasaId, bodId, smerId, .popisyZnacek)
        s poznamkyBodu = ..PoznamkyBodu(IdFa, dataDB, trasaId, bodId, smerId, .popisyPoznamekTrasy)

        // slučování sousedních shodných zastávek (dle čísla zastávky, 1:1 legacy); značky
        // a poznámky sloučených bodů trasy patří řádku, do kterého se sloučily (legacy :421-483)
        i (pocetBodu > 0) && ($lg(body(pocetBodu),6) = cislo) && (cislo '= "") {
            s bodyMapa(bodId) = pocetBodu           // sloučený bod → řádek předchůdce
            s $li(body(pocetBodu),8) = ..SlouceneZnacky($lg(body(pocetBodu),8), znackyBodu)
            s $li(body(pocetBodu),9) = ..SloucenePoznamky($lg(body(pocetBodu),9), poznamkyBodu)
            continue
        }
        s pocetBodu = pocetBodu + 1
        s body(pocetBodu) = $lb(bodId, zastavkaId, nazev, zona, odjPri, cislo, $g(^[dataDB]ALVA.MHD.ZST(IdFa,1,"DTA",zastavkaId,"DTA",1,"Oznacnik")), znackyBodu, poznamkyBodu)
        s bodyMapa(bodId) = pocetBodu
    }
    i pocetBodu = 0 q ..PrazdnaStranka("Na trase linky není žádná zastávka kategorie Zastávka. Linkový JŘ tiskne jen zastávky této kategorie, ostatní body trasy přeskočí - zkontrolujte zastávky na trase linky v modulu MHD.")

    // ── spoje směru: MakeTableSpoj → časy per řádek; skupiny = TypProvozu ──
    // Načítají se před řádky zastávek, protože Zastávky, kde žádný spoj nestaví, řádky ubírá.
    d ..NactiSkupinyProvozu(IdFa, dataDB, .provozPoradi, .provozNadpis)

    // seřazení: (poradiProvozu, výchozí čas, spoj) - MHD legacy řadí uvnitř bloku TypProvozu;
    // čas po půlnoci se bere jako ráno (#1440, legacy :931), doladění pořadí viz SeradJakoLegacy
    k razeni
    s spojId = ""
    f {
        s spojId = $o(^[dataDB]ALVA.MHD.LSO(IdFa,2,"IDX",1,1,1,linkaId,spojId))
        q:spojId=""
        i +$g(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",spojId,"DTA",1,"SmerID")) '= smerId continue
        s spojuSmeru = $g(spojuSmeru) + 1
        s typProvozu = +$g(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",spojId,"DTA",1,"TypProvozuID"))
        // Vypnutý typ provozu se netiskne a nedělá blok (legacy :537-538).
        i '..TypProvozuSeTiskne(typProvozu) s vypnuteTypem = $g(vypnuteTypem) + 1 continue
        k arrtbsp
        d ##class(ALVA.MHD.SpojeFce).MakeTableSpoj(.sys, spojId, "", .arrtbsp)
        s prvniCnt = $o(arrtbsp("arr",1,""), smerpar)
        i prvniCnt = "" continue
        s vychoziCas = +$lg($g(arrtbsp("arr",1,prvniCnt)), 4) # 1440
        s razeni($g(provozPoradi(typProvozu), 999), typProvozu, vychoziCas, spojId) = ""
        m spojTabulka(spojId) = arrtbsp("arr",1)   // cache pro BuildSpojMhd
    }
    i '$d(razeni) q ..PrazdnaStranka($s('$g(spojuSmeru):"Linka nemá ve vybraném směru žádné spoje.", $g(vypnuteTypem)=spojuSmeru:"Vybrané typy provozu nemají na lince žádné spoje. V parametrech ve skupině Tabulka spojů zapněte aspoň jeden typ provozu, který linka má.", 1:"Spoje linky nemají spočítané časy, nejspíš chybí jízdní doby. Doplňte jízdní doby linky v modulu MHD."))

    // Zastávky, kde žádný tištěný spoj nestaví (jen projíždí nebo jede jinudy), se vynechají jako
    // u AD. Rozhodují spoje vybraných typů provozu, ne celá linka.
    i $g(^||ML("IN","ZASTAVKY_BEZ_ZASTAVENI")) = "Netisknout" d ..JenZastavujici(.body, .pocetBodu, .bodyMapa, .spojTabulka)
    i pocetBodu = 0 q ..PrazdnaStranka(..DuvodBezZastaveni())

    // 2. průchod: zastávkové řádky vč. zdvojení příjezd/odjezd; rowMeta(i)=$lb(radekBodu, typ)
    s zastavky = ##class(%DynamicArray).%New()
    k rowMeta, zvyraznene, zvyrazneneNazev
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
        s:..JeZvyraznena($lg(body(i),3)) zvyraznene(i) = $lg(body(i),6), zvyrazneneNazev(i) = $lg(body(i),3)
    }
    d vals.%Set("VYCHOZI_ZASTAVKA", $lg(body(1),3))
    d vals.%Set("KONECNA_ZASTAVKA",       $lg(body(pocetBodu),3))
    // Číslo zvýrazněné zastávky pro adresu v QR jen při jediné shodě, jako u AD (`LinkovyJrAd`) -
    // kus názvu může trefit víc zastávek a adresa by pak ukázala na jedno z víc míst.
    s zv = $o(zvyraznene(""))
    s jednoznacna = (zv '= "") && ($o(zvyraznene(zv)) = "")
    // Název bez značek poznámek (řádek je nese u názvu), stejně jako výchozí a konečná zastávka.
    d vals.%Set("POPIS_ZASTAVKA",         $s(jednoznacna:zvyrazneneNazev(zv), 1:""))
    d vals.%Set("AKTUALNI_ZASTAVKA_CZST", $s(jednoznacna:zvyraznene(zv), 1:""))

    s spoje = ##class(%DynamicArray).%New()
    s skupinyDnu = ##class(%DynamicArray).%New()
    k nzSeen, poznSpoju, atributSeen, markerSeen   // nzSeen(nz) = text do legendy, jen NZ spojů této stránky
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
            k poradi
            s cas = ""
            f {
                s cas = $o(razeni(pp, typProvozu, cas))
                q:cas=""
                s spojId = ""
                f {
                    s spojId = $o(razeni(pp, typProvozu, cas, spojId))
                    q:spojId=""
                    s poradi($i(poradi)) = spojId
                }
            }
            d ..SeradJakoLegacy(.poradi, .spojTabulka, .bodyMapa, pocetBodu)
            f i=1:1:poradi {
                s spojObj = ..BuildSpojMhd(.sys, IdFa, dataDB, poradi(i), skupinaIdx, pocetRadku, .rowMeta, .body, .bodyMapa, .spojTabulka, .znacky, .nzSeen, .poznSpoju, .pocetPoznSpoju, .atributSeen, .markerSeen)
                i spojObj '= "" d spoje.%Push(spojObj)
            }
        }
    }
    i spoje.%Size() = 0 q ..PrazdnaStranka(..DuvodBezZastaveni())

    // ── poznámky (pořadí druhů: 1 dny, 2 NZ, 4 poznámky spoje, 5 atributy, zastávkové) ──
    s poznamky = ##class(%DynamicArray).%New()
    f pi=1:1:pocetPoznSpoju d poznamky.%Push(poznSpoju(pi))
    // Poznámky z trasy tištěných zastávek - mezi textové poznámky (druh 4) jako ve starém tisku
    // (legacy :1212-1219); stejná poznámka u spoje i u zastávky jen jednou, „jen značka" bez vysvětlivky.
    d ..PridatPoznamkyTrasyMhd(.body, pocetBodu, poznamky, .popisyPoznamekTrasy, .poznSpoju)
    d ..PridatPoznamkyNZMhd(IdFa, dataDB, poznamky, .nzSeen)
    d ..PridatPoznamkyAtributuMhd(poznamky, .atributSeen)
    d ..PridatStaleZnackyMhd(poznamky, .markerSeen)
    d ..PridatAtributyZastavekMhd(.body, pocetBodu, poznamky, .popisyZnacek)

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
    // Číslo zastávky z číselníku MHD, ne interní ID - stejný význam jako CZST u AD.
    d z.%Set("CZST",    $lg(bodVeta,6))
    d z.%Set("NAZEV_ZASTAVKY", $s(typ="prijezd":"", 1:..NazevSPoznamkami($lg(bodVeta,3), $lg(bodVeta,9))))
    // Klíč je množné ZONY jako u zastávkového JŘ; tam je to pole zón, tady jedna hodnota -
    // klient obojí čte přes GetString (pole spojí "/"), takže se binding chová stejně.
    d z.%Set("ZONY",    $lg(bodVeta,4))
    // Označník jako u zastávkového JŘ MHD (ZST Oznacnik).
    d z.%Set("NASTUPISTE", $lg(bodVeta,7))
    // Značky zastávky u názvu - písmena jako u AD (prvek je kreslí piktogramem), legacy :1218-1225.
    s atributy = ##class(%DynamicArray).%New(), znacky = $s(typ="prijezd":"", 1:$lg(bodVeta,8))
    f i=1:1:$l(znacky) d atributy.%Push($e(znacky,i), "string")
    d z.%Set("ATRIBUTY", atributy)
    d z.%Set("KM",      ##class(%DynamicArray).%New())
    // Příznak nese i řádek příjezdu - je to táž zastávka. Vzhled určí pravidlo zvýraznění šablony.
    d z.%Set("AKTUALNI_ZASTAVKA", $s(..JeZvyraznena($lg(bodVeta,3)):"true", 1:"false"))
    q z
}

/// Zóny zastávky v integrovaném systému linky (ENUMS Zony ZonaIDSID = IDSID linky), víc zón
/// spojených „/". ⚠️ Vědomě jinak než starý tisk, který bere jen poslední zónu (legacy :353-358):
/// hraniční zastávka patří do obou zón a cestující to má vidět (Jan 22. 10.). Zastávkový JŘ MHD
/// systém nefiltruje a posílá všechny zóny zastávky - každá agenda drží svůj starý modul (`B194` T8).
ClassMethod ZonyZastavky(IdFa As %String, dataDB As %String, zastavkaId As %String, idsId As %String) As %String
{
    s zony = "", c = ""
    f {
        s c = $o(^[dataDB]ALVA.MHD.ZST(IdFa,1,"DTA",zastavkaId,"DTA",1,"ZonyID",c))
        q:c=""
        s zonaId = $g(^[dataDB]ALVA.MHD.ZST(IdFa,1,"DTA",zastavkaId,"DTA",1,"ZonyID",c,"Itm"))
        continue:zonaId=""
        continue:$g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"Zony","DTA",zonaId,"ZonaIDSID"))'=idsId
        s cislo = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"Zony","DTA",zonaId,"Cislo"))
        continue:cislo=""
        continue:$lf($lfs(zony,"/"),cislo)
        s zony = $s(zony="":cislo, 1:zony_"/"_cislo)
    }
    q zony
}

/// Je zastávka zvýrazněná parametrem „Zvýraznit zastávku"? Kus názvu bez ohledu na velikost písmen,
/// jako u AD (`LinkovyJrAd.JeZvyraznena`); tarifní číslo MHD nemá, takže zúžit na jeden řádek nejde.
ClassMethod JeZvyraznena(nazevZastavky As %String) As %Boolean
{
    s hledana = $zcvt($zstrip($g(^||ML("IN","ZVYRAZNIT_ZASTAVKU")), "<>W"), "L")
    i hledana = "" q 0
    q $zcvt(nazevZastavky, "L") [ hledana
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

/// Sestaví JSON objekt jednoho MHD spoje: značky platnosti, NZ, časy per řádek.
/// Značky skládá jako legacy MakeData (:536-695): dny z porovnání bitmap, poznámky spoje
/// určené pro knižní tisk, atributy B/C. Textové poznámky jdou za symboly (legacy je tiskne
/// samostatným řádkem pod nimi) a jejich výčet nese `TEXTOVE_ZNACKY`.
ClassMethod BuildSpojMhd(ByRef sys As %String, IdFa As %String, dataDB As %String, spojId As %String, skupina As %Integer, pocetRadku As %Integer, ByRef rowMeta, ByRef body, ByRef bodyMapa, ByRef spojTabulka, ByRef znacky, ByRef nzSeen, ByRef poznSpoju, ByRef pocetPoznSpoju, ByRef atributSeen, ByRef markerSeen) As %DynamicObject
{
    s o = ##class(%DynamicObject).%New()
    // Tiskne se číslo spoje z dat, ne interní ID (legacy :536, :1167).
    s cislo = $g(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",spojId,"DTA",1,"CisloSpoje"))
    i cislo = "" s cislo = spojId
    d o.%Set("CISLO_SPOJE", cislo, $s(cislo = +cislo:"number", 1:"string"))
    d o.%Set("SKUPINA", skupina)

    // NZ 0 = nepřidělená (legacy podmínka `i spojnz`, :866)
    s nz = $g(znacky("spoj", spojId))
    i 'nz s nz = ""
    i (nz '= "") && ('$d(nzSeen(nz))) s nzSeen(nz) = $g(znacky("text", nz))
    d o.%Set("NZ", nz)

    s platnost = ##class(%DynamicArray).%New(), texty = ##class(%DynamicArray).%New()
    s dny = $g(znacky("dny", spojId))
    f i=1:1:$l(dny) {
        s den = $e(dny, i)
        i (den < 1) || (den > 7) continue
        d platnost.%Push(den, "string")
        d ..PridatPoznamkuSpoje(.poznSpoju, .pocetPoznSpoju, 1, "znak", den, ..TextDne(den))
    }
    k arrPozn
    d ##class(ALVA.MHD.SpojeFce).GetPoznamkySpoj(.sys, spojId, "", .arrPozn)
    s poznId = ""
    f {
        s poznId = $o(arrPozn("arr",1,poznId))
        q:poznId=""
        i '$g(arrPozn("arr",1,poznId,"TiskJRMHDKnizni")) continue
        s typPozn = +$g(arrPozn("arr",1,poznId,"TypPoznamkyID"))
        // Speciální znaky a NZ při auto-poznámkování nahrazuje porovnání bitmap (legacy :540-548).
        i $g(znacky("auto")) && ((typPozn = 1) || (typPozn = 2)) continue
        s popis = $g(arrPozn("arr",1,poznId,"PopisPoznamky"))
        i typPozn = 0 {
            s text = $g(arrPozn("arr",1,poznId,"Text"))
            i text '= "" {
                d texty.%Push(text, "string")
                d ..PridatPoznamkuSpoje(.poznSpoju, .pocetPoznSpoju, 4, "text", text, popis)
            }
        }
        elseif typPozn = 1 {
            s znak = ..ZnakSpecialni($g(arrPozn("arr",1,poznId,"SpecZnak")))
            i znak '= "" {
                d platnost.%Push(znak, "string")
                d ..PridatPoznamkuSpoje(.poznSpoju, .pocetPoznSpoju, 1, "znak", znak, popis)
            }
        }
    }
    // Atributy spoje i jeho oběhu: B = bezbariérový (BEV), C = částečně bezbariérový (CBE).
    k arrAtr
    d ##class(ALVA.MHD.SpojeFce).GetAtributySpoj(.sys, spojId, "", .arrAtr)
    s atrId = ""
    f {
        s atrId = $o(arrAtr("arr",1,atrId))
        q:atrId=""
        s klic = $case($g(arrAtr("arr",1,atrId,"Znak")), "B":"BEV", "C":"CBE", :"")
        i klic '= "" {
            d platnost.%Push(klic, "string")
            s atributSeen(klic) = ""
        }
    }
    s it = texty.%GetIterator()
    while it.%GetNext(.k, .text) { d platnost.%Push(text, "string") }
    d o.%Set("PLATNOST", platnost)
    // Volný text zákazníka se kreslí písmem, ne piktogramem - „A" nebo „K" by jinak prvek
    // přeložil na autobus a kolo.
    i texty.%Size() d o.%Set("TEXTOVE_ZNACKY", texty)

    // markery: "|" staví-nestaví (projíždí), "<" bod mezi začátkem a koncem jízdy kde spoj nejede
    i '..CasyBodu(spojId, .spojTabulka, .bodyMapa, .casyBodu, .prvniRadek, .posledniRadek, .bodOd, .bodDo) q ""

    s casy = ##class(%DynamicArray).%New()
    f i=1:1:pocetRadku {
        s radekBodu = $lg(rowMeta(i),1), typRadku = $lg(rowMeta(i),2)
        i '$d(casyBodu(radekBodu)) {
            // mimo jízdu spoje: mezi krajními body jeho trasy = "<" (jede jinudy), jinak prázdno.
            // Měří se body trasy, ne zastávky, kde spoj staví - jinak by na konci, kde spoj projíždí
            // body mimo seznam zastávek, „<" chybělo (KV_DP 1438, 1520).
            s bod = $lg(body(radekBodu),1)
            i (bod > bodOd) && (bod < bodDo) { d casy.%Push("<")  s markerSeen("<") = "" }
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

/// Časy spoje po řádcích zastávek z MakeTableSpoj tabulky ($lb(ZastavkaID,Kategorie,CasPrijezdMM,
/// CasOdjezdMM,isPrujezd,VzdalenostMM) po bodech trasy). casyBodu(radekBodu) = $lb(prijezdMM,
/// odjezdMM, projizdi); prvniRadek/posledniRadek = krajní řádky, kde spoj staví. 0 = nestaví nikde.
/// bodOd/bodDo = krajní body trasy, kterými spoj jede - i ty, kde nestaví nebo které nejsou
/// zastávkou; mezi nimi je „jede jinudy" (legacy begcnt/endcnt, :705-757).
ClassMethod CasyBodu(spojId As %String, ByRef spojTabulka, ByRef bodyMapa, ByRef casyBodu, ByRef prvniRadek, ByRef posledniRadek, ByRef bodOd, ByRef bodDo) As %Boolean
{
    k casyBodu
    s prvniRadek = "", posledniRadek = ""
    s bodOd = $o(spojTabulka(spojId, "")), bodDo = $o(spojTabulka(spojId, ""), -1)
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
    q:prvniRadek="" 0
    // Body se procházejí podle ID, ale směr zpět má řádky v opačném pořadí - první a poslední
    // zastávka spoje jsou tedy krajní řádky, ne první a poslední nalezený. Do 12. 10. tu byly
    // prohozené a ve směru zpět chyběly „jede jinudy" i krajní příjezd a odjezd.
    i prvniRadek > posledniRadek s x = prvniRadek, prvniRadek = posledniRadek, posledniRadek = x
    q 1
}

/// Nechá v `body` jen zastávky, kde aspoň jeden spoj z `spojTabulka` staví (má čas, ne jen průjezd),
/// a přečísluje je. `bodyMapa` se přepočítá celá - sloučené sousední body trasy ukazují na řádek
/// předchůdce, nejen bod, který řádek založil; vynechaný řádek v ní nezůstane.
ClassMethod JenZastavujici(ByRef body, ByRef pocetBodu As %Integer, ByRef bodyMapa, ByRef spojTabulka)
{
    k zastavuje
    s spojId = ""
    f {
        s spojId = $o(spojTabulka(spojId))
        q:spojId=""
        continue:'..CasyBodu(spojId, .spojTabulka, .bodyMapa, .casyBodu)
        s i = ""
        f {
            s i = $o(casyBodu(i))
            q:i=""
            s:'$lg(casyBodu(i),3) zastavuje(i) = 1
        }
    }
    k puvodni, noveCislo m puvodni = body
    k body
    s pocet = 0
    f i=1:1:pocetBodu {
        continue:'$g(zastavuje(i))
        s pocet = pocet + 1, body(pocet) = puvodni(i), noveCislo(i) = pocet
    }
    s bodId = ""
    f {
        s bodId = $o(bodyMapa(bodId))
        q:bodId=""
        i $d(noveCislo(bodyMapa(bodId))) { s bodyMapa(bodId) = noveCislo(bodyMapa(bodId)) }
        else { k bodyMapa(bodId) }
    }
    s pocetBodu = pocet
}

/// Pořadí spojů v bloku jako legacy (:951-990): od poslední zastávky trasy k první se v každém
/// řádku prohodí soused, který tam má dřívější čas (po půlnoci jako ráno), dokud se něco mění,
/// nejvýš 101 kol. Spoje začínající uprostřed trasy se tak zařadí podle společných zastávek,
/// ne podle času, kdy vyjely. poradi(i) = SpojID, na vstupu seřazené podle výchozího času.
ClassMethod SeradJakoLegacy(ByRef poradi, ByRef spojTabulka, ByRef bodyMapa, pocetRadku As %Integer)
{
    s pocet = $g(poradi)
    q:pocet<2
    k cas
    f i=1:1:pocet {
        s spojId = poradi(i)
        continue:'..CasyBodu(spojId, .spojTabulka, .bodyMapa, .casyBodu, .prvni, .posledni, .bodOd, .bodDo)
        s radek = ""
        f {
            s radek = $o(casyBodu(radek), 1, cb)
            q:radek=""
            i $lg(cb,3) continue
            s cas(spojId, radek) = $s(+$lg(cb,2) > +$lg(cb,1):+$lg(cb,2), 1:+$lg(cb,1)) # 1440
        }
    }
    f cyklus=1:1:101 {
        s zmena = 0
        f radek=pocetRadku:-1:1 {
            f i=1:1:pocet-1 {
                i '$d(cas(poradi(i), radek)) continue
                s j = 0
                f k=i+1:1:pocet {
                    i $d(cas(poradi(k), radek)) s j = k  q
                }
                i 'j continue
                i cas(poradi(i), radek) > cas(poradi(j), radek) {
                    s x = poradi(i), poradi(i) = poradi(j), poradi(j) = x, zmena = 1
                }
            }
        }
        i 'zmena q
    }
}

/// Negativní značky celé linky naráz, jako legacy MakeData (:490-649) - čísla přidělená
/// z rozsahu musí být táž v obou směrech a pořadí přidělování jde po ID spoje přes celou linku.
/// OUT znacky("spoj",SpojID) = NZ, ("text",NZ) = text do legendy, ("dny",SpojID) = dny týdenní
/// platnosti ("1".."7"), ("auto") = linka má auto-poznámkování rozdílů platností.
/// - auto: spoj s ručně zadanou platností dostane NZ podle textu omezení - nejdřív z NZ zadaných
///   u spojů linky, pak z už přidělených, jinak první volná od `RozsahNegativnichZnacekOd`
/// - jinak: NZ z první poznámky spoje typu 2 určené pro knižní tisk
ClassMethod PriradNegativniZnacky(ByRef sys As %String, IdFa As %String, dataDB As %String, linkaId As %String, smery As %String, ByRef znacky)
{
    k znacky
    s auto = +$g(^[dataDB]ALVA.MHD.LSO(IdFa,1,"DTA",linkaId,"DTA",1,"AutomatickyPoznamkovatOdlisnostiVPlatnostechSpoju"))
    s znacky("auto") = auto
    s nzOd = +$g(^[dataDB]ALVA.MHD.SETTINGS(IdFa,"Obecne","DTA",1001,"RozsahNegativnichZnacekOd"))

    // Číselník textů z NZ zadaných u spojů - bere celou linku, oba směry (legacy :497-528).
    k ciselnik
    i auto {
        s spojId = ""
        f {
            s spojId = $o(^[dataDB]ALVA.MHD.LSO(IdFa,2,"IDX",1,1,1,linkaId,spojId))
            q:spojId=""
            s nz = $g(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",spojId,"DTA",1,"NegativniZnacka"))
            i nz = "" continue
            i '$g(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",spojId,"DTA",1,"RucniZadani")) continue
            s txt = ..OmezeniSpoje(IdFa, dataDB, spojId, .dny)
            i txt '= "" s ciselnik(nz) = txt
        }
    }

    s spojId = ""
    f {
        s spojId = $o(^[dataDB]ALVA.MHD.LSO(IdFa,2,"IDX",1,1,1,linkaId,spojId))
        q:spojId=""
        s smer = $s(+$g(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",spojId,"DTA",1,"SmerID")):"Z", 1:"T")
        i (","_smery_",") '[ (","_smer_",") continue
        // Volné značky z rozsahu dostávají jen tištěné spoje, jako v legacy (filtr typu :538 je
        // před přidělením); číselník výše bere celou linku i s vypnutými typy (legacy :497-528).
        i '..TypProvozuSeTiskne(+$g(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",spojId,"DTA",1,"TypProvozuID"))) continue
        s nz = "", txt = ""
        i auto {
            i '$g(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",spojId,"DTA",1,"RucniZadani")) continue
            s txt = ..OmezeniSpoje(IdFa, dataDB, spojId, .dny)
            i dny '= "" s znacky("dny", spojId) = dny
            i txt = "" continue
            s k = ""
            f {
                s k = $o(ciselnik(k))
                q:(k="")||(nz'="")
                i ciselnik(k) = txt s nz = k
            }
            i nz = "" {
                s k = ""
                f {
                    s k = $o(znacky("text", k))
                    q:(k="")||(nz'="")
                    i znacky("text", k) = txt s nz = k
                }
            }
            i nz = "" s nz = ..VolnaZnacka(IdFa, dataDB, nzOd, .ciselnik, .znacky)
        }
        else {
            k arrPozn
            d ##class(ALVA.MHD.SpojeFce).GetPoznamkySpoj(.sys, spojId, "", .arrPozn)
            s poznId = ""
            f {
                s poznId = $o(arrPozn("arr",1,poznId))
                q:(poznId="")||(nz'="")
                i +$g(arrPozn("arr",1,poznId,"TypPoznamkyID")) '= 2 continue
                i $g(arrPozn("arr",1,poznId,"NZ")) = "" continue
                i '$g(arrPozn("arr",1,poznId,"TiskJRMHDKnizni")) continue
                s nz = arrPozn("arr",1,poznId,"NZ")
                s txt = $s($g(arrPozn("arr",1,poznId,"TiskJRMHDKnizniPouzeZnacku")):"", 1:$g(arrPozn("arr",1,poznId,"PopisPoznamky")))
            }
            i nz = "" continue
        }
        s znacky("text", nz) = txt, znacky("spoj", spojId) = nz
    }
}

/// Text omezení spoje s ručně zadanou platností proti kalendáři jeho typu provozu; "" = neliší se.
/// `dny` = dny týdenní platnosti, které z porovnání vyšly. Volání a pořadí 1:1 legacy :550-583 -
/// do 12. 10. se tu funkce volaly s jinými argumenty a chybu polykal catch, takže text byl vždy prázdný.
ClassMethod OmezeniSpoje(IdFa As %String, dataDB As %String, spojId As %String, ByRef dny As %String) As %String
{
    s dny = ""
    s provozBitmapa = $g(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",spojId,"DTA",1,"ProvozBitMapa"))
    s typProvozu = $g(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",spojId,"DTA",1,"TypProvozuID"))
    s bmp = $p(provozBitmapa,"~",3)
    k arr
    s isok = ##class(ALVA.MHD.KalendarFce).GetJede(IdFa, dataDB, $p(provozBitmapa,"~",1), $p(provozBitmapa,"~",2), typProvozu, "", "", .arr)
    i 'isok d ..ChybaSpoje(IdFa, dataDB, spojId, "kalendář typu provozu", isok)
    s cbmp = $lg($g(arr("dta",1)), 3)
    i cbmp = bmp q ""

    k arri, arr
    s arri("dta",1) = $p(provozBitmapa,"~",1), arri("dta",2) = $p(provozBitmapa,"~",2)
    s arri("dta",3) = bmp, arri("dta",4) = cbmp
    s isok = ##class(ALVA.MHD.KalendarFce).MakeRozpisTplProTypProvozuID(IdFa, dataDB, typProvozu, .arri)
    i 'isok d ..ChybaSpoje(IdFa, dataDB, spojId, "rozpis typu provozu", isok)
    s chyba = $$mkOmzFromOnlyTwoBitmaps^JRapi(IdFa, dataDB, .arri, .arr)
    i +chyba d ..ChybaSpoje(IdFa, dataDB, spojId, "omezení proti kalendáři", chyba)
    s dny = $g(arr("dta",1))
    k arri m arri = arr s arri("par",1) = 0 k arr
    s isok = ##class(ALVA.MHD.KalendarFce).MakeOmezeniTXT(IdFa, dataDB, .arri, .arr)
    i 'isok d ..ChybaSpoje(IdFa, dataDB, spojId, "text omezení", isok)
    q $g(arr("dta",1))
}

/// Spoj, u kterého modul MHD nedokáže říct, kdy jede - chyba v datech typu provozu, kterou uživatel
/// opraví v modulu MHD (`BaseAgenda.ChybaDat`). Číslo spoje veřejné, ne interní ID.
ClassMethod ChybaSpoje(IdFa As %String, dataDB As %String, spojId As %String, co As %String, vysledek As %String)
{
    s cislo = $g(^[dataDB]ALVA.MHD.LSO(IdFa,2,"DTA",spojId,"DTA",1,"CisloSpoje"))
    s detail = $zstrip($s(vysledek["~":$p(vysledek,"~",2,$l(vysledek,"~")), 1:""), "<>W")
    d ..ChybaDat("Spoj "_$s(cislo'="":cislo, 1:spojId)_": nejde určit, kdy jede ("_co_$s(detail'="":": "_detail, 1:"")_"). Zkontrolujte typ provozu spoje v modulu MHD.")
}

/// První NZ od `od`, kterou nemá žádný typ provozu, číselník ani už přidělený text (legacy :606-618).
ClassMethod VolnaZnacka(IdFa As %String, dataDB As %String, od As %Integer, ByRef ciselnik, ByRef znacky) As %Integer
{
    f nz = od:1 {
        s obsazena = 0, tp = ""
        f {
            s tp = $o(^[dataDB]ALVA.MHD.ENUMS(IdFa,"TypyProvozu","DTA",tp))
            q:tp=""
            i $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"TypyProvozu","DTA",tp,"NegativniZnacka")) = nz s obsazena = 1
        }
        i obsazena continue
        i $d(ciselnik(nz)) || $d(znacky("text", nz)) continue
        q
    }
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

/// Poznámky druhu 2 - NZ spojů stránky s textem omezení; NZ bez textu se nevypisuje (legacy :1384).
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

/// Poznámky druhu 5 - atributy spoje (BEV/CBE).
ClassMethod PridatPoznamkyAtributuMhd(poznamky As %DynamicArray, ByRef atributSeen)
{
    i $d(atributSeen("BEV")) d poznamky.%Push(..BuildPoznamka(5, "znak", "BEV", "spoj s bezbariérově přístupným vozidlem"))
    i $d(atributSeen("CBE")) d poznamky.%Push(..BuildPoznamka(5, "znak", "CBE", "spoj s částečně bezbariérově přístupným vozidlem, nutná dopomoc průvodce"))
}

/// Poznámky druhu 7 - markery matice.
ClassMethod PridatStaleZnackyMhd(poznamky As %DynamicArray, ByRef markerSeen)
{
    i $d(markerSeen("|")) d poznamky.%Push(..BuildPoznamka(7, "znak", "|", "spoj příslušnou zastávkou projíždí"))
    i $d(markerSeen("<")) d poznamky.%Push(..BuildPoznamka(7, "znak", "<", "spoj jede po jiné trase"))
}

/// Značky bodu trasy k názvu zastávky jako řetězec písmen seřazený podle písmene: atributy
/// zastávky v číselníku a atributy bodu trasy ve směru, jen N/V/x/A jako legacy (:403-416,
/// :442-483; vlak „L" tam vyřadil zákazník). popisy(znak) = text do legendy.
/// Poznámky z trasy u bodu (legacy :418-436 tam, :450-470 zpět): textové poznámky ke spojům
/// (`TypPoznamkyID` prázdné) určené pro knižní tisk, přiřazené bodu pro směr. Vrací jejich značky
/// jako $lb seřazené a bez opakování; `popisy(znak)` = vysvětlivka do legendy, u „jen značka" prázdná.
/// ⚠️ Neověřeno proti starému tisku na datech: dev data (KV_DP, CB_DP) žádnou nemají.
ClassMethod PoznamkyBodu(IdFa As %String, dataDB As %String, trasaId As %String, bodId As %String, smerId As %Integer, ByRef popisy) As %List
{
    k znaky
    s klic = $s(smerId=0:"PoznamkyTam", 1:"PoznamkyZpet"), c = ""
    f {
        s c = $o(^[dataDB]ALVA.MHD.LSO(IdFa,4,"DTA",trasaId,"DTA",1,"ModelTrasaBodDetail",bodId,"Val",klic,c))
        q:c=""
        s pozn = $g(^[dataDB]ALVA.MHD.LSO(IdFa,4,"DTA",trasaId,"DTA",1,"ModelTrasaBodDetail",bodId,"Val",klic,c,"Itm"))
        continue:pozn=""
        continue:'$g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"PoznamkaSpoj","DTA",pozn,"TiskJRMHDKnizni"))
        continue:+$g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"PoznamkaSpoj","DTA",pozn,"TypPoznamkyID"))
        s znak = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"PoznamkaSpoj","DTA",pozn,"Text"))
        continue:znak=""
        s znaky(znak) = ""
        continue:$d(popisy(znak))
        s popisy(znak) = $s($g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"PoznamkaSpoj","DTA",pozn,"TiskJRMHDKnizniPouzeZnacku")):"", 1:$g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"PoznamkaSpoj","DTA",pozn,"PopisPoznamky")))
    }
    s out = "", znak = ""
    f {
        s znak = $o(znaky(znak))
        q:znak=""
        s out = out_$lb(znak)
    }
    q out
}

/// Vysvětlivky poznámek z trasy zastávek, které se tisknou, do legendy (druh 4).
ClassMethod PridatPoznamkyTrasyMhd(ByRef body, pocetBodu As %Integer, poznamky As %DynamicArray, ByRef popisy, ByRef poznSpoju)
{
    k znaky
    f i=1:1:pocetBodu {
        s p = $lg(body(i),9)
        f j=1:1:$ll(p) s znaky($lg(p,j)) = ""
    }
    s znak = ""
    f {
        s znak = $o(znaky(znak))
        q:znak=""
        s text = $g(popisy(znak))
        continue:text=""
        continue:$d(poznSpoju("seen", 4, znak, text))
        d poznamky.%Push(..BuildPoznamka(4, "text", znak, text))
    }
}

/// Poznámky dvou sloučených bodů trasy jedním seznamem, seřazené a bez opakování.
ClassMethod SloucenePoznamky(a As %List, b As %List) As %List
{
    k znaky
    f i=1:1:$ll(a) s znaky($lg(a,i)) = ""
    f i=1:1:$ll(b) s znaky($lg(b,i)) = ""
    s out = "", znak = ""
    f {
        s znak = $o(znaky(znak))
        q:znak=""
        s out = out_$lb(znak)
    }
    q out
}

/// Název zastávky se značkami poznámek z trasy jako ve starém tisku: „Tržnice, ab" (legacy :812-821).
ClassMethod NazevSPoznamkami(nazev As %String, poznamky As %List) As %String
{
    q:$ll(poznamky)=0 nazev
    q nazev_", "_$lts(poznamky, "")
}

ClassMethod ZnackyBodu(IdFa As %String, dataDB As %String, zastavkaId As %String, trasaId As %String, bodId As %String, smerId As %Integer, ByRef popisy) As %String
{
    k seen
    s atrCnt = ""
    f {
        s atrCnt = $o(^[dataDB]ALVA.MHD.ZST(IdFa,1,"DTA",zastavkaId,"DTA",1,"Atributy",atrCnt))
        q:atrCnt=""
        d ..PridatZnackuBodu(IdFa, dataDB, $g(^[dataDB]ALVA.MHD.ZST(IdFa,1,"DTA",zastavkaId,"DTA",1,"Atributy",atrCnt,"Itm")), .seen, .popisy)
    }
    s klic = $s(smerId=0:"AtributyTam", 1:"AtributyZpet"), atrCnt = ""
    f {
        s atrCnt = $o(^[dataDB]ALVA.MHD.LSO(IdFa,4,"DTA",trasaId,"DTA",1,"ModelTrasaBodDetail",bodId,"Val",klic,atrCnt))
        q:atrCnt=""
        d ..PridatZnackuBodu(IdFa, dataDB, $g(^[dataDB]ALVA.MHD.LSO(IdFa,4,"DTA",trasaId,"DTA",1,"ModelTrasaBodDetail",bodId,"Val",klic,atrCnt,"Itm")), .seen, .popisy)
    }
    s out = "", znak = ""
    f {
        s znak = $o(seen(znak))
        q:znak=""
        s out = out_znak
    }
    q out
}

ClassMethod PridatZnackuBodu(IdFa As %String, dataDB As %String, atrId As %String, ByRef seen, ByRef popisy)
{
    q:atrId=""
    s znak = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"Atribut","DTA",atrId,"Znak"))
    q:",N,V,x,A,"'[(","_znak_",")
    s seen(znak) = "", popisy(znak) = $g(^[dataDB]ALVA.MHD.ENUMS(IdFa,"Atribut","DTA",atrId,"Popis"))
}

/// Sjednocení dvou řetězců značek, seřazené podle písmene.
ClassMethod SlouceneZnacky(a As %String, b As %String) As %String
{
    k seen
    s spolu = a_b
    f i=1:1:$l(spolu) s seen($e(spolu,i)) = ""
    s out = "", znak = ""
    f {
        s znak = $o(seen(znak))
        q:znak=""
        s out = out_znak
    }
    q out
}

/// Značky zastávek stránky → legenda druhu 7. Druh 7 jako u AD; starý MHD tisk je řadí
/// k atributům (druh 5) - vědomá odchylka, druh řídí skupinu legendy.
ClassMethod PridatAtributyZastavekMhd(ByRef body, pocetBodu As %Integer, poznamky As %DynamicArray, ByRef popisy)
{
    k seen
    f i=1:1:pocetBodu {
        s znacky = $lg(body(i),8)
        f j=1:1:$l(znacky) s seen($e(znacky,j)) = ""
    }
    s znak = ""
    f {
        s znak = $o(seen(znak))
        q:znak=""
        d poznamky.%Push(..BuildPoznamka(7, "znak", znak, $g(popisy(znak))))
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

/// SpecZnak poznámky spoje → klíč značky v řádku platnosti, tytéž klíče jako u AD varianty.
/// Legacy (:666-675) posílá SpecZnak jako id číselníku znaků a dny 1-7 posouvá na id 8-14
/// (týdenní platnost 1-7); id 15/16/22/56 = „+", „X", BEV, CBE (hlavička legacy :155-190).
/// Ostatní id zůstávají, jak přišla - v datech dev serveru žádné nejsou (neověřeno).
ClassMethod ZnakSpecialni(id As %String) As %String
{
    i id = "" q ""
    i (id >= 1) && (id <= 7) q id
    i (id >= 8) && (id <= 14) q id - 7
    q $case(id, 15:"+", 16:"X", 22:"BEV", 56:"CBE", :id)
}

/// Text legendy ke dni týdenní platnosti z porovnání bitmap (legacy :570-577).
ClassMethod TextDne(den As %Integer) As %String
{
    q $p("jede v pondělí,jede v úterý,jede ve středu,jede ve čtvrtek,jede v pátek,jede v sobotu,jede v neděli", ",", den)
}

}
