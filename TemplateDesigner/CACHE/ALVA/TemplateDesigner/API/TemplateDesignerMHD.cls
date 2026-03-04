/// Implementace AgendaAPI pro agendu MHD / Jízdní řád.
/// Referenční implementace — obsahuje reálná pole MHD agendy
/// a příklad Fixed šablon tabulek.
/// 
/// appsettings.json:
///   { "code": "JR", "name": "Jízdní řád", "cacheClass": "ALVA.TemplateDesigner.API.TemplateDesignerMHD" }
Class ALVA.TemplateDesigner.API.TemplateDesignerMHD Extends ALVA.TemplateDesigner.API.AgendaAPI
{

/// Vrátí schéma polí MHD agendy: skalární pole + skupiny tabulek.
ClassMethod GetSchema(ByRef sys As %String) As %Status
{
    s def = ##class(%DynamicObject).%New()

    // ── Skalární pole ─────────────────────────────────────────────────
    s fields = ##class(%DynamicArray).%New()

    d ..AddField(fields, "CISLO_LINKY",              "Číslo linky",              "number")
    d ..AddField(fields, "LOKALNI_OZNACENI_LINKY",   "Označení linky",           "text")
    d ..AddField(fields, "TITULEK",                  "Titulek sestavy",          "text")
    d ..AddField(fields, "POPIS_LINKY",              "Popis linky",              "text")
    d ..AddField(fields, "POPIS_DOPRAVCE",           "Popis dopravce",           "text")
    d ..AddField(fields, "POPIS_PLATNOSTI",          "Popis platnosti",          "text")
    d ..AddField(fields, "DATUM_PLATNOSTI_OD",       "Datum platnosti od",       "date")
    d ..AddField(fields, "DATUM_PLATNOSTI_DO",       "Datum platnosti do",       "date")
    d ..AddField(fields, "POPIS_SMER",               "Popis směru",              "text")
    d ..AddField(fields, "POPIS_SMER_CIS",           "Popis směru CIS",          "text")
    d ..AddField(fields, "TYP_JR",                   "Typ jízdního řádu",        "text")
    d ..AddField(fields, "POZNAMKA_POD_DOPRAVCE",    "Poznámka pod dopravce",    "text")
    d ..AddField(fields, "INFORMACE",                "Informace",                "text")
    d ..AddField(fields, "NEJEDOU_SPOJE",            "Nejedou spoje",            "text")
    d ..AddField(fields, "TEST_POLOZKA",            "Test polozka",            "text")

    s def.fields = fields

    // ── Skupiny tabulek (scope = table) ──────────────────────────────
    s groups = ##class(%DynamicArray).%New()

    // Zastávky linky
    s gZast = ##class(%DynamicObject).%New()
    s gZast.name          = "Zastávky"
    s gZast.scope         = "table"
    s gZast.collectionKey = "ZASTAVKY"
    s gZastFields = ##class(%DynamicArray).%New()
    d ..AddField(gZastFields, "CISLO_ZASTAVKA",     "Číslo zastávky",     "number")
    d ..AddField(gZastFields, "POPIS_ZASTAVKA",     "Název zastávky",     "text")
    d ..AddField(gZastFields, "POPIS_ZASTAVKA_CIS", "Název zastávky CIS", "text")
    d ..AddField(gZastFields, "CAS_PRIJEZDU",       "Čas příjezdu",       "time")
    d ..AddField(gZastFields, "CAS_ODJEZDU",        "Čas odjezdu",        "time")
    d ..AddField(gZastFields, "PRESTUP",            "Přestup",            "boolean")
    d ..AddField(gZastFields, "POZNAMKA",           "Poznámka",           "text")
    s gZast.fields = gZastFields
    d groups.%Push(gZast)

    // Spoje (pro tabulkové jízdní řády)
    s gSpoje = ##class(%DynamicObject).%New()
    s gSpoje.name          = "Spoje"
    s gSpoje.scope         = "table"
    s gSpoje.collectionKey = "SPOJE"
    s gSpojeFields = ##class(%DynamicArray).%New()
    d ..AddField(gSpojeFields, "CISLO_SPOJE",  "Číslo spoje",  "number")
    d ..AddField(gSpojeFields, "TYP_DNE",      "Typ dne",      "text")
    d ..AddField(gSpojeFields, "POZNAMKA",     "Poznámka",     "text")
    s gSpoje.fields = gSpojeFields
    d groups.%Push(gSpoje)

    s def.groups = groups

    s ^||ML("OUT", $i(^||ML("OUT"))) = def.%ToJSON()
    q 1
}

/// Vrátí seznam dostupných Fixed šablon tabulek pro MHD agendu.
ClassMethod GetTableTemplates(ByRef sys As %String) As %Status
{
    s arr = ##class(%DynamicArray).%New()

    s t1 = ##class(%DynamicObject).%New()
    s t1.key   = "MHD_ZAHLAVI"
    s t1.label = "Záhlaví JR (2 sloupce)"
    d arr.%Push(t1)

    s t2 = ##class(%DynamicObject).%New()
    s t2.key   = "MHD_PLATNOST"
    s t2.label = "Platnost a dopravce"
    d arr.%Push(t2)

    s ^||ML("OUT", $i(^||ML("OUT"))) = arr.%ToJSON()
    q 1
}

/// Vrátí definici konkrétní Fixed šablony (sloupce + řádky).
ClassMethod GetTableTemplate(ByRef sys As %String) As %Status
{
    s key = $g(^||ML("IN", "key"))

    if key = "MHD_ZAHLAVI" {
        d ..WriteTemplate2Col([
            ["Linka",         "{CISLO_LINKY}"],
            ["Označení",      "{LOKALNI_OZNACENI_LINKY}"],
            ["Směr",          "{POPIS_SMER}"],
            ["Platnost od",   "{DATUM_PLATNOSTI_OD}"],
            ["Platnost do",   "{DATUM_PLATNOSTI_DO}"]
        ])
    }
    elseif key = "MHD_PLATNOST" {
        d ..WriteTemplate2Col([
            ["Platnost",      "{POPIS_PLATNOSTI}"],
            ["Dopravce",      "{POPIS_DOPRAVCE}"],
            ["Typ JR",        "{TYP_JR}"]
        ])
    }

    q 1
}

/// Vrátí data pro tisk (runtime — implementuje reálné DB volání).
/// Tato metoda je zodpovědností konkrétní agendy a závisí na databázové struktuře.
ClassMethod GetData(ByRef sys As %String) As %Status
{
    // TODO: implementovat reálné načtení dat z databáze
    // Vzor:
    //   s id = $g(^||ML("IN","id"))
    //   s scalars = ##class(%DynamicObject).%New()
    //   s scalars.CISLO_LINKY = ..GetCisloLinky(id)
    //   // ... ostatní skalární pole
    //   s ^||ML("OUT", $i(^||ML("OUT"))) = scalars.%ToJSON()
    //
    //   // Kolekce zastávek
    //   // for každou zastávku:
    //   //   s row = ##class(%DynamicObject).%New()
    //   //   s row.collection = "ZASTAVKY"
    //   //   s row.POPIS_ZASTAVKA = ...
    //   //   s ^||ML("OUT", $i(^||ML("OUT"))) = row.%ToJSON()
    q 1
}

// ── Helpers ──────────────────────────────────────────────────────────────────

/// Přidá jedno pole do JSON array polí.
ClassMethod AddField(arr As %DynamicArray, name As %String, label As %String, type As %String)
{
    s f = ##class(%DynamicObject).%New()
    s f.name  = name
    s f.label = label
    s f.type  = type
    d arr.%Push(f)
}

/// Zapíše šablonu se dvěma sloupci (Položka | Hodnota) do OUT.
/// rows = %DynamicArray of [label, value] párů
ClassMethod WriteTemplate2Col(rows As %DynamicArray)
{
    s def = ##class(%DynamicObject).%New()

    // Sloupce
    s cols = ##class(%DynamicArray).%New()
    s c1 = ##class(%DynamicObject).%New()
    s c1.header    = "Položka"
    s c1.width     = 70
    s c1.widthMode = "Mm"
    s c1.alignment = "Left"
    d cols.%Push(c1)
    s c2 = ##class(%DynamicObject).%New()
    s c2.header    = "Hodnota"
    s c2.width     = 80
    s c2.widthMode = "Mm"
    s c2.alignment = "Left"
    d cols.%Push(c2)
    s def.columns = cols

    // Řádky
    s defRows = ##class(%DynamicArray).%New()
    f i=0:1:rows.%Size()-1 {
        s pair = rows.%Get(i)
        s row = ##class(%DynamicObject).%New()
        s cells = ##class(%DynamicArray).%New()
        s cell1 = ##class(%DynamicObject).%New()  s cell1.content = pair.%Get(0)
        s cell2 = ##class(%DynamicObject).%New()  s cell2.content = pair.%Get(1)
        d cells.%Push(cell1)
        d cells.%Push(cell2)
        s row.cells = cells
        d defRows.%Push(row)
    }
    s def.rows = defRows

    s ^||ML("OUT", $i(^||ML("OUT"))) = def.%ToJSON()
}

}
