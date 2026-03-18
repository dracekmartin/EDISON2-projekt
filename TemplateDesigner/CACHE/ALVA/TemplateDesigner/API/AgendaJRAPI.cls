/// Abstraktní mezistupeň v hierarchii pro JŘ agendy.
/// Poskytuje sdílené helpery pro stavbu JSON výstupu (ZASTAVKY, SKUPINY_DNU,
/// ODJEZDY, POZNAMKY) a standardní JŘ schema pole.
/// 
/// Hierarchie:
///   AgendaAPI (abstract)
///   └── AgendaJRAPI (abstract) — JŘ helpery
///       ├── ALVA.MHD.TiskAPI — MHD zastávkový JŘ
///       └── TemplateDesignerMHD (refaktor)
Class ALVA.TemplateDesigner.API.AgendaJRAPI Extends ALVA.TemplateDesigner.API.AgendaAPI [ Abstract ]
{

// ── Stavba zastávek ─────────────────────────────────────────────────────────

/// Vytvoří %DynamicObject zastávky s inicializovanými vnořenými poli.
/// ZONY, PRUBEHY, ATRIBUTY, POZNAMKY_ZASTAVKY se inicializují jako prázdné pole.
ClassMethod BuildZastavka(id As %String, popis As %String, tc As %String = "", casJizdy As %Integer = 0, poradi As %Integer = 1, jeAktualni As %Boolean = 0, naZnameni As %Boolean = 0, odsazeni As %Integer = 0) As %DynamicObject
{
    s zastavka = ##class(%DynamicObject).%New()
    d zastavka.%Set("ID", id)
    d zastavka.%Set("POPIS", popis)
    d zastavka.%Set("TC", tc)
    d zastavka.%Set("CAS_JIZDY", casJizdy)
    d zastavka.%Set("PORADI", poradi)
    d zastavka.%Set("JE_AKTUALNI", $s(jeAktualni:"true", 1:"false"))
    d zastavka.%Set("NA_ZNAMENI", $s(naZnameni:"true", 1:"false"))
    d zastavka.%Set("ODSAZENI", odsazeni)
    d zastavka.%Set("ZAKLADNI_TRASA", "true")
    // Vnořená pole — inicializace jako prázdné arraye
    d zastavka.%Set("ZONY", ##class(%DynamicArray).%New())
    d zastavka.%Set("PRUBEHY", ##class(%DynamicArray).%New())
    d zastavka.%Set("ATRIBUTY", ##class(%DynamicArray).%New())
    d zastavka.%Set("POZNAMKY_ZASTAVKY", ##class(%DynamicArray).%New())
    q zastavka
}

/// Přidá zónu do zastávky.
ClassMethod AddZona(zastavka As %DynamicObject, zona As %String)
{
    d zastavka.%Get("ZONY").%Push(zona)
}

/// Přidá průběhové písmeno do zastávky.
ClassMethod AddPrubeh(zastavka As %DynamicObject, prubeh As %String)
{
    d zastavka.%Get("PRUBEHY").%Push(prubeh)
}

/// Přidá atribut (ikonu/symbol) k zastávce.
ClassMethod AddAtribut(zastavka As %DynamicObject, znak As %String, popis As %String = "")
{
    s atribut = ##class(%DynamicObject).%New()
    d atribut.%Set("ZNAK", znak)
    d atribut.%Set("POPIS", popis)
    d zastavka.%Get("ATRIBUTY").%Push(atribut)
}

/// Přidá poznámku vázanou k zastávce.
/// typ: "text", "nz", "specialni"
ClassMethod AddPoznamkaZastavky(zastavka As %DynamicObject, znak As %String, typ As %String = "text", text As %String = "")
{
    s poznamka = ##class(%DynamicObject).%New()
    d poznamka.%Set("ZNAK", znak)
    d poznamka.%Set("TYP", typ)
    d poznamka.%Set("TEXT", text)
    d zastavka.%Get("POZNAMKY_ZASTAVKY").%Push(poznamka)
}

// ── Stavba skupin dnů ──────────────────────────────────────────────────────

/// Vytvoří %DynamicObject skupiny dnů.
ClassMethod BuildSkupinaDnu(id As %String, nadpis As %String, nadpis2 As %String = "", poradi As %Integer = 1) As %DynamicObject
{
    s skupina = ##class(%DynamicObject).%New()
    d skupina.%Set("ID", id)
    d skupina.%Set("NADPIS", nadpis)
    d skupina.%Set("NADPIS2", nadpis2)
    d skupina.%Set("PORADI", poradi)
    q skupina
}

// ── Stavba odjezdů ──────────────────────────────────────────────────────────

/// Vytvoří %DynamicObject odjezdu s inicializovaným prázdným polem ANOTACE.
ClassMethod BuildOdjezd(zastavkaId As %String, skupinaDnuId As %String, hodina As %Integer, minuta As %Integer, zkrOznLinky As %String = "") As %DynamicObject
{
    s odjezd = ##class(%DynamicObject).%New()
    d odjezd.%Set("ZASTAVKA_ID", zastavkaId)
    d odjezd.%Set("SKUPINA_DNU_ID", skupinaDnuId)
    d odjezd.%Set("HODINA", hodina)
    d odjezd.%Set("MINUTA", minuta)
    d odjezd.%Set("ZKRACENE_OZNACENI_LINKY", zkrOznLinky)
    d odjezd.%Set("FORE_COLOR", "")
    d odjezd.%Set("BACK_COLOR", "")
    d odjezd.%Set("ANOTACE", ##class(%DynamicArray).%New())
    q odjezd
}

/// Přidá anotaci k odjezdu.
/// typ: "text", "nz", "specialni"
ClassMethod AddAnotace(odjezd As %DynamicObject, typ As %String, znak As %String)
{
    s anotace = ##class(%DynamicObject).%New()
    d anotace.%Set("TYP", typ)
    d anotace.%Set("ZNAK", znak)
    d odjezd.%Get("ANOTACE").%Push(anotace)
}

/// Nastaví barvy odjezdu (data-driven).
/// foreColor, backColor: #AARRGGBB formát nebo "" pro výchozí
ClassMethod SetOdjezdColors(odjezd As %DynamicObject, foreColor As %String = "", backColor As %String = "")
{
    d odjezd.%Set("FORE_COLOR", foreColor)
    d odjezd.%Set("BACK_COLOR", backColor)
}

// ── Stavba poznámek ──────────────────────────────────────────────────────────

/// Vytvoří %DynamicObject poznámky pro legendu.
/// typ: "text", "nz", "specialni"
ClassMethod BuildPoznamka(typ As %String, znak As %String, text As %String) As %DynamicObject
{
    s poznamka = ##class(%DynamicObject).%New()
    d poznamka.%Set("TYP", typ)
    d poznamka.%Set("ZNAK", znak)
    d poznamka.%Set("TEXT", text)
    q poznamka
}

// ── Konverze speciálních znaků ──────────────────────────────────────────────

/// Konvertuje číselný kód speciálního znaku na Unicode string.
/// Rozhodnutí: Caché posílá hotové Unicode znaky — element jen renderuje.
ClassMethod ConvertSpecialniZnak(cislo As %String) As %String
{
    // Dny v týdnu
    if cislo = 1 q "Po"
    if cislo = 2 q "Út"
    if cislo = 3 q "St"
    if cislo = 4 q "Čt"
    if cislo = 5 q "Pá"
    if cislo = 6 q "So"
    if cislo = 7 q "Ne"
    // Rozšířit dle číselníku ALVA.MHD.ENUMS("SpecZnak",...)
    q cislo
}

// ── Standardní JŘ schema pole ───────────────────────────────────────────────

/// Přidá standardní skalární pole JŘ agendy do pole fields.
ClassMethod AddStandardJRFields(fields As %DynamicArray)
{
    d ..AddField(fields, "CISLO_LINKY",            "Číslo linky",            "number")
    d ..AddField(fields, "LOKALNI_OZNACENI_LINKY", "Označení linky",         "text")
    d ..AddField(fields, "TITULEK",                "Titulek sestavy",        "text")
    d ..AddField(fields, "POPIS_LINKY",            "Popis linky",            "text")
    d ..AddField(fields, "POPIS_DOPRAVCE",         "Popis dopravce",         "text")
    d ..AddField(fields, "POPIS_PLATNOSTI",        "Popis platnosti",        "text")
    d ..AddField(fields, "DATUM_PLATNOSTI_OD",     "Datum platnosti od",     "date")
    d ..AddField(fields, "DATUM_PLATNOSTI_DO",     "Datum platnosti do",     "date")
    d ..AddField(fields, "POPIS_SMER",             "Popis směru",            "text")
    d ..AddField(fields, "TYP_JR",                 "Typ jízdního řádu",      "text")
    d ..AddField(fields, "QR_KOD",                 "QR kód URL",             "text")
    d ..AddField(fields, "POPIS_ZASTAVKA",         "Aktuální zastávka",      "text")
    d ..AddField(fields, "CISLO_ZASTAVKA",         "Číslo zastávky",         "text")
    d ..AddField(fields, "POZNAMKA_POD_DOPRAVCE",  "Poznámka pod dopravce",  "text")
    d ..AddField(fields, "INFORMACE",              "Informace",              "text")
    d ..AddField(fields, "NEJEDOU_SPOJE",          "Nejedou spoje",          "text")
}

/// Přidá standardní tabulkové kolekce JŘ agendy do pole collections.
ClassMethod AddStandardJRCollections(collections As %DynamicArray)
{
    // ZASTAVKY
    s colZastavky = ##class(%DynamicObject).%New()
    s colZastavky.name          = "Zastávky"
    s colZastavky.collectionKey = "ZASTAVKY"
    s colZastavkyFields = ##class(%DynamicArray).%New()
    d ..AddField(colZastavkyFields, "ID",                "ID zastávky",         "text")
    d ..AddField(colZastavkyFields, "POPIS",             "Název zastávky",      "text")
    d ..AddField(colZastavkyFields, "TC",                "Tarifní číslo",       "text")
    d ..AddField(colZastavkyFields, "CAS_JIZDY",         "Čas jízdy",           "number")
    d ..AddField(colZastavkyFields, "NA_ZNAMENI",        "Na znamení",          "boolean")
    s colZastavky.fields = colZastavkyFields
    d collections.%Push(colZastavky)

    // SKUPINY_DNU
    s colSkupiny = ##class(%DynamicObject).%New()
    s colSkupiny.name          = "Skupiny dnů"
    s colSkupiny.collectionKey = "SKUPINY_DNU"
    s colSkupinyFields = ##class(%DynamicArray).%New()
    d ..AddField(colSkupinyFields, "ID",      "ID skupiny",     "text")
    d ..AddField(colSkupinyFields, "NADPIS",  "Nadpis skupiny", "text")
    d ..AddField(colSkupinyFields, "NADPIS2", "Nadpis 2",       "text")
    s colSkupiny.fields = colSkupinyFields
    d collections.%Push(colSkupiny)

    // ODJEZDY
    s colOdjezdy = ##class(%DynamicObject).%New()
    s colOdjezdy.name          = "Odjezdy"
    s colOdjezdy.collectionKey = "ODJEZDY"
    s colOdjezdyFields = ##class(%DynamicArray).%New()
    d ..AddField(colOdjezdyFields, "ZASTAVKA_ID",     "ID zastávky",       "text")
    d ..AddField(colOdjezdyFields, "SKUPINA_DNU_ID",  "ID skupiny dnů",    "text")
    d ..AddField(colOdjezdyFields, "HODINA",          "Hodina",            "number")
    d ..AddField(colOdjezdyFields, "MINUTA",          "Minuta",            "number")
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
    d ..AddBoolParam(arr, "TISK_SIPKY",          "Tisknout šipky směru",      "true")
    d ..AddBoolParam(arr, "TISK_TARIFU",         "Tarifní zóna",              "false")
    d ..AddBoolParam(arr, "TISK_PROJETYCH_ZAST", "Tisknout projeté zastávky", "false")

    // Časová osa
    s param = ##class(%DynamicObject).%New()
    s param.name         = "CASOVA_OSA"
    s param.label        = "Časová osa odjezdů"
    s param.type         = "enum"
    s param.defaultValue = "compact"
    s opts = ##class(%DynamicArray).%New()
    d opts.%Push("compact")
    d opts.%Push("full")
    d opts.%Push("range")
    s param.options = opts
    d arr.%Push(param)

    // Hodiny od/do — relevantní jen pro CASOVA_OSA = range
    d ..AddNumberParam(arr, "HODINY_OD", "Hodiny od", "4")
    d ..AddNumberParam(arr, "HODINY_DO", "Hodiny do", "24")
}

}
