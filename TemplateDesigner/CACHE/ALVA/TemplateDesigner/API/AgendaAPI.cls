/// Abstraktní základ pro API rozhraní TemplateDesigneru.
/// Každá agenda (JR, FAKTURY, ...) musí mít vlastní třídu dědící z této.
/// Caché kompilátor vynucuje implementaci všech Abstract metod.
/// 
/// Konfigurace v appsettings.json:
///   { "code": "JR", "name": "Jízdní řád", "cacheClass": "ALVA.TemplateDesigner.API.TemplateDesignerMHD" }
Class ALVA.TemplateDesigner.API.AgendaAPI Extends %RegisteredObject [ Abstract ]
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
/// OUT[1]   = JSON objekt skalárních hodnot: { "CISLO_LINKY": "42", ... }
/// OUT[2..] = JSON objekty řádků kolekcí:   { "collection": "ZASTAVKY", "POPIS": "Praha hl.n.", ... }
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

}
