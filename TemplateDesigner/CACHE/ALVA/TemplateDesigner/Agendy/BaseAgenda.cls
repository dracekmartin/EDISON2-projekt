/// Abstraktní základ pro implementaci agendy v TemplateDesigneru.
///
/// Agenda = zdroj dat a doménová logika pro konkrétní typ tiskové sestavy:
/// - **MHD** — zastávkové jízdní řády městské hromadné dopravy (linky, zastávky, spoje)
/// - **FAKTURY** — fakturační sestavy (faktury, položky, DPH)
/// - připravované: Meziměstský JŘ, Knižní JŘ, Vozový JŘ
///
/// Každá agenda dědí z této třídy a implementuje povinné Abstract metody:
/// - GetSchema           — definice dostupných polí (skalární + tabulkové kolekce)
/// - GetData             — reálná data pro tisk (per IDFa, per instance)
/// - GetMockData         — vzorová data pro live preview v designeru
/// - GetTableTemplates   — seznam Fixed šablon tabulek (záhlaví, platnost, ...)
/// - GetTableTemplate    — definice konkrétní Fixed šablony (sloupce + řádky)
///
/// Volitelně přepsatelné (default = prázdný seznam):
/// - GetParameters       — parametry pro PrintDialog (zaškrtávátka, výběry, ...)
/// - GetAvailableInstances — seznam instancí pro Instance Picker
///
/// Caché kompilátor vynucuje implementaci Abstract metod; bez nich třída neprojde compile.
///
/// Konfigurace v appsettings.json:
///   { "code": "MHD", "name": "Zastávkový JŘ MHD", "cacheClass": "ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd" }
Class ALVA.TemplateDesigner.Agendy.BaseAgenda Extends %RegisteredObject [ Abstract ]
{

/// Vrátí DataSchema pro agendu: skalární pole (fields) + tabulkové kolekce (fieldCollections).
/// OUT[1] = JSON objekt:
///   { "fields": [...], "fieldCollections": [...] }
/// fields       = skalární pole; každá hodnota existuje jednou na celou sestavu
/// fieldCollections = kolekce sad polí; každá kolekce tvoří zdroj řádků pro Dynamic tabulku
ClassMethod GetSchema(ByRef sys As %String) As %Status [ Abstract ]
{
}

/// Vrátí seznam dostupných Fixed šablon tabulek pro tuto agendu.
/// OUT[1] = JSON pole objektů:
///   [ { "key": "ZAHLAVI_LINKY", "label": "Záhlaví linky" }, ... ]
/// key = interní identifikátor; česky, velkými písmeny, podtržítko jako oddělovač
ClassMethod GetTableTemplates(ByRef sys As %String) As %Status [ Abstract ]
{
}

/// Vrátí kompletní definici Fixed šablony (sloupce + řádky).
/// IN:  key = klíč šablony (např. "ZAHLAVI_LINKY")
/// OUT[1] = JSON objekt:
///   { "columns": [...], "rows": [...] }
ClassMethod GetTableTemplate(ByRef sys As %String) As %Status [ Abstract ]
{
}

/// Vrátí data pro tisk: skalární hodnoty + kolekce řádků.
/// IN:  parametry závislé na agendě (ID záznamu, datum, ...)
/// OUT[1..N] = stejný JSON formát jako GetMockData, ale s reálnými daty.
///             Každý OUT node = jedna samostatná stránka:
///               { "values": { "FIELD": "value", ... }, "rows": { "COLL": [...] } }
///             Agendy s jediným výstupem zapisují pouze OUT[1].
///             Multi-page agendy (např. zastávkový JŘ MHD: 1 stránka per zastávka × směr)
///             iterují a zapisují do OUT[$i(^||ML("OUT"))].
ClassMethod GetData(ByRef sys As %String) As %Status [ Abstract ]
{
}

/// Vrátí parametry sestavy dostupné pro tuto agendu.
/// Výchozí implementace vrátí prázdný seznam — přepsat v konkrétní agendě.
/// OUT[1] = JSON pole:
///   [
///     { "name": "TISK_SIPKY",  "label": "Tisknout šipky",  "type": "bool",   "defaultValue": "true" },
///     { "name": "FORMAT_CASU", "label": "Formát času",      "type": "enum",   "defaultValue": "H:mm",
///       "options": ["H:mm", "HH:mm"] }
///   ]
/// Typy: "bool", "enum", "string", "number", "date"
/// defaultValue je vždy string — C# strana konvertuje dle type.
ClassMethod GetParameters(ByRef sys As %String) As %Status
{
    s ^||ML("OUT", $i(^||ML("OUT"))) = "[]"
    q 1
}

/// Vrátí seznam dostupných instancí agendy (např. linky pro MHD, faktury pro FAKTURY).
/// Slouží pro Instance Picker v TD — uživatel vybere 1-N instancí, nad kterými pracuje.
/// 
/// Výchozí implementace vrací prázdný seznam — agenda bez konceptu instancí (GENERAL,
/// případně FAKTURY před implementací) ho nemusí přepisovat.
/// 
/// IN["search"] = volitelný filtr (substring, case-insensitive)
/// IN["limit"]  = max počet řádků (default 50)
/// OUT["1"] = JSON pole:
///   [
///     { "value": "42", "display": "42 — Praha–Brno", "subtitle": "Platnost od 1.1.2024" },
///     ...
///   ]
/// 
/// value    = opaque ID předaný do GetData (IN["LINKA_ID"] u MHD)
/// display  = hlavní zobrazovaný text (číslo + název)
/// subtitle = volitelný menší text pod displayem (platnost, dopravce, atd.)
ClassMethod GetAvailableInstances(ByRef sys As %String) As %Status
{
    s ^||ML("OUT","1") = "[]"
    q 1
}

/// Vrátí ukázková (mock) data pro live preview v designeru.
/// OUT[1] = JSON objekt:
///   {
///     "values": { "FIELD": "value", ... },
///     "rows": {
///       "COLLECTION_KEY": [ { "FIELD": "value", ... }, ... ],
///       ...
///     }
///   }
ClassMethod GetMockData(ByRef sys As %String) As %Status [ Abstract ]
{
}

// ── Helper metody pro agendy ─────────────────────────────────────────────────

/// Přidá jedno pole do JSON array polí.
/// arr     = %DynamicArray kam se přidává
/// name    = název pole (např. "CISLO_FAKTURY")
/// label   = popisek (např. "Číslo faktury")
/// type    = typ pole: "text", "number", "date", "time", "dateTime", "boolean"
/// example = (volitelné) příklad hodnoty
ClassMethod AddField(arr As %DynamicArray, name As %String, label As %String, type As %String, example As %String = "")
{
    s field = ##class(%DynamicObject).%New()
    s field.name  = name
    s field.label = label
    s field.type  = type
    if example '= "" {
        s field.example = example
    }
    d arr.%Push(field)
}

/// Přidá bool parametr do pole parametrů.
/// arr        = %DynamicArray kam se přidává
/// name       = název parametru (např. "TISK_POZNAMKY")
/// label      = popisek (např. "Tisknout poznámku")
/// defaultVal = výchozí hodnota (0/1 nebo true/false)
ClassMethod AddBoolParam(arr As %DynamicArray, name As %String, label As %String, defaultVal As %Boolean)
{
    s param = ##class(%DynamicObject).%New()
    s param.name  = name
    s param.label = label
    s param.type  = "bool"
    s param.defaultValue = $s(defaultVal:"true", 1:"false")
    d arr.%Push(param)
}

/// Přidá number parametr do pole parametrů.
ClassMethod AddNumberParam(arr As %DynamicArray, name As %String, label As %String, defaultVal As %String)
{
    s param = ##class(%DynamicObject).%New()
    s param.name  = name
    s param.label = label
    s param.type  = "number"
    s param.defaultValue = defaultVal
    d arr.%Push(param)
}

/// Přidá mock hodnotu do objektu skalárních hodnot.
/// vals = %DynamicObject kam se přidává
/// name = název pole (např. "CISLO_FAKTURY")
/// val  = hodnota (string nebo number)
ClassMethod AddMockValue(vals As %DynamicObject, name As %String, val As %String)
{
    d vals.%Set(name, val)
}

/// Zapíše šablonu se dvěma sloupci (Položka | Hodnota) do OUT.
/// rows = COS array ve formátu:
///   [["Label 1", "{FIELD1}", "format"], ["Label 2", "{FIELD2}", "format"], ...]
/// Třetí parametr (format) je volitelný.
ClassMethod WriteTemplate2Col(rows As %List)
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
    f i=1:1:$ll(rows) {
        s pair = $lg(rows, i)
        s row = ##class(%DynamicObject).%New()
        s cells = ##class(%DynamicArray).%New()

        s cell1 = ##class(%DynamicObject).%New()
        d cell1.%Set("content", $lg(pair, 1))
        d cells.%Push(cell1)

        s cell2 = ##class(%DynamicObject).%New()
        d cell2.%Set("content", $lg(pair, 2))
        if $ll(pair) > 2 && ($lg(pair, 3) '= "") {
            d cell2.%Set("format", $lg(pair, 3))
        }
        d cells.%Push(cell2)

        s row.cells = cells
        d defRows.%Push(row)
    }
    s def.rows = defRows

    s ^||ML("OUT", $i(^||ML("OUT"))) = def.%ToJSON()
}

}
