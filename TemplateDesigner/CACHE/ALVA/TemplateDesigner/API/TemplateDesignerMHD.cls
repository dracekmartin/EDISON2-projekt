/// Implementace AgendaAPI pro agendu MHD / Jízdní řád.
/// Referenční implementace — obsahuje reálná pole MHD agendy
/// a příklad Fixed šablon tabulek.
/// 
/// appsettings.json:
///   { "code": "JR", "name": "Jízdní řád", "cacheClass": "ALVA.TemplateDesigner.API.TemplateDesignerMHD" }
Class ALVA.TemplateDesigner.API.TemplateDesignerMHD Extends ALVA.TemplateDesigner.API.AgendaJRAPI
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
    d ..AddField(fields, "TEST_POLOZKA",             "Test polozka",             "text")

    s def.fields = fields

    // ── Tabulkové kolekce (fieldCollections) ─────────────────────────
    s collections = ##class(%DynamicArray).%New()

    // Zastávky linky
    s gZast = ##class(%DynamicObject).%New()
    s gZast.name          = "Zastávky"
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
    d collections.%Push(gZast)

    // Spoje (pro tabulkové jízdní řády)
    s gSpoje = ##class(%DynamicObject).%New()
    s gSpoje.name          = "Spoje"
    s gSpoje.collectionKey = "SPOJE"
    s gSpojeFields = ##class(%DynamicArray).%New()
    d ..AddField(gSpojeFields, "CISLO_SPOJE",  "Číslo spoje",  "number")
    d ..AddField(gSpojeFields, "TYP_DNE",      "Typ dne",      "text")
    d ..AddField(gSpojeFields, "POZNAMKA",     "Poznámka",     "text")
    s gSpoje.fields = gSpojeFields
    d collections.%Push(gSpoje)

    s def.fieldCollections = collections

    s ^||ML("OUT", $i(^||ML("OUT"))) = def.%ToJSON()
    q 1
}

/// Vrátí seznam dostupných Fixed šablon tabulek pro MHD agendu.
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

/// Vrátí definici konkrétní Fixed šablony (sloupce + řádky).
ClassMethod GetTableTemplate(ByRef sys As %String) As %Status
{
    s key = $g(^||ML("IN", "key"))

    if key = "ZAHLAVI_LINKY" {
        d ..WriteTemplate2Col([
            ["Linka",         "{CISLO_LINKY}"],
            ["Označení",      "{LOKALNI_OZNACENI_LINKY}"],
            ["Směr",          "{POPIS_SMER}"],
            ["Platnost od",   "{DATUM_PLATNOSTI_OD}"],
            ["Platnost do",   "{DATUM_PLATNOSTI_DO}"]
        ])
    }
    elseif key = "PLATNOST_DOPRAVCE" {
        d ..WriteTemplate2Col([
            ["Platnost",      "{POPIS_PLATNOSTI}"],
            ["Dopravce",      "{POPIS_DOPRAVCE}"],
            ["Typ JR",        "{TYP_JR}"]
        ])
    }

    q 1
}

/// Vrátí parametry sestavy pro MHD/JR agendu.
ClassMethod GetParameters(ByRef sys As %String) As %Status
{
    s arr = ##class(%DynamicArray).%New()

    d ..AddBoolParam(arr, "TISK_SIPKY",  "Tisknout šipky směru", "true")
    d ..AddBoolParam(arr, "TISK_TARIFU", "Tarifní zóna",         "false")

    // Formát času — enum
    s p = ##class(%DynamicObject).%New()
    s p.name         = "FORMAT_CASU"
    s p.label        = "Formát času"
    s p.type         = "enum"
    s p.defaultValue = "H:mm"
    s opts = ##class(%DynamicArray).%New()
    d opts.%Push("H:mm")
    d opts.%Push("HH:mm")
    s p.options = opts
    d arr.%Push(p)

    s ^||ML("OUT", $i(^||ML("OUT"))) = arr.%ToJSON()
    q 1
}

/// Vrátí data pro tisk (runtime — implementuje reálné DB volání).
/// Tato metoda je zodpovědností konkrétní agendy a závisí na databázové struktuře.
ClassMethod GetData(ByRef sys As %String) As %Status
{
    // TODO: implementovat reálné načtení dat z databáze
    q 1
}

/// Vrátí ukázková data pro live preview v designeru.
ClassMethod GetMockData(ByRef sys As %String) As %Status
{
    s out = ##class(%DynamicObject).%New()

    // ── Skalární hodnoty ──────────────────────────────────────────────
    s vals = ##class(%DynamicObject).%New()
    d vals.%Set("CISLO_LINKY",            "42")
    d vals.%Set("LOKALNI_OZNACENI_LINKY", "JR-42")
    d vals.%Set("TITULEK",                "Jízdní řád linky 42 — Praha–Brno")
    d vals.%Set("POPIS_LINKY",            "Praha, Florenc — Brno, ÚAN Zvonařka")
    d vals.%Set("POPIS_DOPRAVCE",         "RegioJet a.s.")
    d vals.%Set("POPIS_PLATNOSTI",        "Platí od 15.12.2024 do 13.12.2025")
    d vals.%Set("DATUM_PLATNOSTI_OD",     "15.12.2024")
    d vals.%Set("DATUM_PLATNOSTI_DO",     "13.12.2025")
    d vals.%Set("POPIS_SMER",             "Praha → Brno")
    d vals.%Set("POPIS_SMER_CIS",         "Praha hl.n. → Brno ÚAN")
    d vals.%Set("TYP_JR",                 "Dálkový")
    d vals.%Set("POZNAMKA_POD_DOPRAVCE",  "Jízdenky na www.regiojet.cz")
    d vals.%Set("INFORMACE",              "Vlak jede bez přestupu")
    d vals.%Set("NEJEDOU_SPOJE",          "24.12., 25.12., 1.1.")
    d vals.%Set("TEST_POLOZKA",           "Testovací hodnota")
    s out.values = vals

    // ── Tabulkové kolekce ─────────────────────────────────────────────
    s rows = ##class(%DynamicObject).%New()

    // Zastávky
    s zastavky = ##class(%DynamicArray).%New()
    d zastavky.%Push(..MakeZastavka("Praha, Florenc",      "",      "06:00", 0))
    d zastavky.%Push(..MakeZastavka("Praha, Černý Most",   "06:08", "06:10", 0))
    d zastavky.%Push(..MakeZastavka("Průhonice",           "06:25", "06:26", 0))
    d zastavky.%Push(..MakeZastavka("Říčany",              "06:35", "06:36", 0))
    d zastavky.%Push(..MakeZastavka("Benešov u Prahy",     "07:05", "07:07", 1))
    d zastavky.%Push(..MakeZastavka("Humpolec",            "07:50", "07:52", 0))
    d zastavky.%Push(..MakeZastavka("Jihlava, aut. nádr.", "08:20", "08:25", 1))
    d zastavky.%Push(..MakeZastavka("Brno, ÚAN Zvonařka", "09:15", "",      0))
    s rows.ZASTAVKY = zastavky

    // Spoje
    s spoje = ##class(%DynamicArray).%New()
    d spoje.%Push(..MakeSpoj("101", "Po-Pa", ""))
    d spoje.%Push(..MakeSpoj("103", "Po-Pa", "Pouze do 30.6."))
    d spoje.%Push(..MakeSpoj("105", "So-Ne", ""))
    d spoje.%Push(..MakeSpoj("107", "So-Ne", "Jen So"))
    s rows.SPOJE = spoje

    s out.rows = rows

    s ^||ML("OUT", $i(^||ML("OUT"))) = out.%ToJSON()
    q 1
}

ClassMethod MakeZastavka(popis As %String, prijezd As %String, odjezd As %String, prestup As %Boolean) As %DynamicObject
{
    s z = ##class(%DynamicObject).%New()
    d z.%Set("POPIS_ZASTAVKA",     popis)
    d z.%Set("POPIS_ZASTAVKA_CIS", popis)
    d z.%Set("CISLO_ZASTAVKA",     "")
    d z.%Set("CAS_PRIJEZDU",       prijezd)
    d z.%Set("CAS_ODJEZDU",        odjezd)
    d z.%Set("PRESTUP",            $s(prestup:"Ano", 1:""))
    d z.%Set("POZNAMKA",           "")
    q z
}

ClassMethod MakeSpoj(cislo As %String, typDne As %String, poznamka As %String) As %DynamicObject
{
    s sp = ##class(%DynamicObject).%New()
    d sp.%Set("CISLO_SPOJE", cislo)
    d sp.%Set("TYP_DNE",     typDne)
    d sp.%Set("POZNAMKA",    poznamka)
    q sp
}

}
