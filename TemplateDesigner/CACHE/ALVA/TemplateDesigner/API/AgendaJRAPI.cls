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
    s z = ##class(%DynamicObject).%New()
    d z.%Set("ID", id)
    d z.%Set("POPIS", popis)
    d z.%Set("TC", tc)
    d z.%Set("CAS_JIZDY", casJizdy)
    d z.%Set("PORADI", poradi)
    d z.%Set("JE_AKTUALNI", $s(jeAktualni:"true", 1:"false"))
    d z.%Set("NA_ZNAMENI", $s(naZnameni:"true", 1:"false"))
    d z.%Set("ODSAZENI", odsazeni)
    d z.%Set("ZAKLADNI_TRASA", "true")
    // Vnořená pole — inicializace jako prázdné arraye
    d z.%Set("ZONY", ##class(%DynamicArray).%New())
    d z.%Set("PRUBEHY", ##class(%DynamicArray).%New())
    d z.%Set("ATRIBUTY", ##class(%DynamicArray).%New())
    d z.%Set("POZNAMKY_ZASTAVKY", ##class(%DynamicArray).%New())
    q z
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
    s a = ##class(%DynamicObject).%New()
    d a.%Set("ZNAK", znak)
    d a.%Set("POPIS", popis)
    d zastavka.%Get("ATRIBUTY").%Push(a)
}

/// Přidá poznámku vázanou k zastávce.
/// typ: "text", "nz", "specialni"
ClassMethod AddPoznamkaZastavky(zastavka As %DynamicObject, znak As %String, typ As %String = "text", text As %String = "")
{
    s p = ##class(%DynamicObject).%New()
    d p.%Set("ZNAK", znak)
    d p.%Set("TYP", typ)
    d p.%Set("TEXT", text)
    d zastavka.%Get("POZNAMKY_ZASTAVKY").%Push(p)
}

// ── Stavba skupin dnů ──────────────────────────────────────────────────────

/// Vytvoří %DynamicObject skupiny dnů.
ClassMethod BuildSkupinaDnu(id As %String, nadpis As %String, nadpis2 As %String = "", poradi As %Integer = 1) As %DynamicObject
{
    s s = ##class(%DynamicObject).%New()
    d s.%Set("ID", id)
    d s.%Set("NADPIS", nadpis)
    d s.%Set("NADPIS2", nadpis2)
    d s.%Set("PORADI", poradi)
    q s
}

// ── Stavba odjezdů ──────────────────────────────────────────────────────────

/// Vytvoří %DynamicObject odjezdu s inicializovaným prázdným polem ANOTACE.
ClassMethod BuildOdjezd(zastavkaId As %String, skupinaDnuId As %String, hodina As %Integer, minuta As %Integer, zkrOznLinky As %String = "") As %DynamicObject
{
    s o = ##class(%DynamicObject).%New()
    d o.%Set("ZASTAVKA_ID", zastavkaId)
    d o.%Set("SKUPINA_DNU_ID", skupinaDnuId)
    d o.%Set("HODINA", hodina)
    d o.%Set("MINUTA", minuta)
    d o.%Set("ZKRACENE_OZNACENI_LINKY", zkrOznLinky)
    d o.%Set("FORE_COLOR", "")
    d o.%Set("BACK_COLOR", "")
    d o.%Set("ANOTACE", ##class(%DynamicArray).%New())
    q o
}

/// Přidá anotaci k odjezdu.
/// typ: "text", "nz", "specialni"
ClassMethod AddAnotace(odjezd As %DynamicObject, typ As %String, znak As %String)
{
    s a = ##class(%DynamicObject).%New()
    d a.%Set("TYP", typ)
    d a.%Set("ZNAK", znak)
    d odjezd.%Get("ANOTACE").%Push(a)
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
    s p = ##class(%DynamicObject).%New()
    d p.%Set("TYP", typ)
    d p.%Set("ZNAK", znak)
    d p.%Set("TEXT", text)
    q p
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
    s gZast = ##class(%DynamicObject).%New()
    s gZast.name          = "Zastávky"
    s gZast.collectionKey = "ZASTAVKY"
    s gZastFields = ##class(%DynamicArray).%New()
    d ..AddField(gZastFields, "ID",                "ID zastávky",         "text")
    d ..AddField(gZastFields, "POPIS",             "Název zastávky",      "text")
    d ..AddField(gZastFields, "TC",                "Tarifní číslo",       "text")
    d ..AddField(gZastFields, "CAS_JIZDY",         "Čas jízdy",           "number")
    d ..AddField(gZastFields, "NA_ZNAMENI",        "Na znamení",          "boolean")
    s gZast.fields = gZastFields
    d collections.%Push(gZast)

    // SKUPINY_DNU
    s gSk = ##class(%DynamicObject).%New()
    s gSk.name          = "Skupiny dnů"
    s gSk.collectionKey = "SKUPINY_DNU"
    s gSkFields = ##class(%DynamicArray).%New()
    d ..AddField(gSkFields, "ID",      "ID skupiny",     "text")
    d ..AddField(gSkFields, "NADPIS",  "Nadpis skupiny", "text")
    d ..AddField(gSkFields, "NADPIS2", "Nadpis 2",       "text")
    s gSk.fields = gSkFields
    d collections.%Push(gSk)

    // ODJEZDY
    s gOdj = ##class(%DynamicObject).%New()
    s gOdj.name          = "Odjezdy"
    s gOdj.collectionKey = "ODJEZDY"
    s gOdjFields = ##class(%DynamicArray).%New()
    d ..AddField(gOdjFields, "ZASTAVKA_ID",     "ID zastávky",       "text")
    d ..AddField(gOdjFields, "SKUPINA_DNU_ID",  "ID skupiny dnů",    "text")
    d ..AddField(gOdjFields, "HODINA",          "Hodina",            "number")
    d ..AddField(gOdjFields, "MINUTA",          "Minuta",            "number")
    s gOdj.fields = gOdjFields
    d collections.%Push(gOdj)

    // POZNAMKY
    s gPoz = ##class(%DynamicObject).%New()
    s gPoz.name          = "Poznámky"
    s gPoz.collectionKey = "POZNAMKY"
    s gPozFields = ##class(%DynamicArray).%New()
    d ..AddField(gPozFields, "TYP",  "Typ poznámky", "text")
    d ..AddField(gPozFields, "ZNAK", "Znak",          "text")
    d ..AddField(gPozFields, "TEXT", "Text poznámky",  "text")
    s gPoz.fields = gPozFields
    d collections.%Push(gPoz)
}

/// Přidá standardní JŘ parametry do pole parametrů.
ClassMethod AddStandardJRParameters(arr As %DynamicArray)
{
    d ..AddBoolParam(arr, "TISK_SIPKY",          "Tisknout šipky směru",      "true")
    d ..AddBoolParam(arr, "TISK_TARIFU",         "Tarifní zóna",              "false")
    d ..AddBoolParam(arr, "TISK_PROJETYCH_ZAST", "Tisknout projeté zastávky", "false")

    // Časová osa
    s p = ##class(%DynamicObject).%New()
    s p.name         = "CASOVA_OSA"
    s p.label        = "Časová osa odjezdů"
    s p.type         = "enum"
    s p.defaultValue = "compact"
    s opts = ##class(%DynamicArray).%New()
    d opts.%Push("compact")
    d opts.%Push("full")
    d opts.%Push("range")
    s p.options = opts
    d arr.%Push(p)

    // Hodiny od/do — relevantní jen pro CASOVA_OSA = range
    d ..AddNumberParam(arr, "HODINY_OD", "Hodiny od", "4")
    d ..AddNumberParam(arr, "HODINY_DO", "Hodiny do", "24")
}

}
