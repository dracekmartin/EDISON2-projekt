/// Abstraktní základ pro API rozhraní TemplateDesigneru.
/// Každá agenda (JR, FAKTURY, ...) musí mít vlastní třídu dědící z této.
/// Caché kompilátor vynucuje implementaci všech Abstract metod.
/// 
/// Konfigurace v appsettings.json:
///   { "code": "JR", "name": "Jízdní řád", "cacheClass": "ALVA.TemplateDesigner.API.TemplateDesignerMHD" }
Class ALVA.TemplateDesigner.API.AgendaAPI Extends %RegisteredObject [ Abstract ]
{

/// Vrátí DataSchema pro agendu: skalární pole (fields) + skupiny tabulek (groups).
/// OUT[1] = JSON objekt:
///   { "fields": [...], "groups": [...] }
/// Viz formát DataField a DataFieldGroup v dokumentaci.
ClassMethod GetSchema(ByRef sys As %String) As %Status [ Abstract ]
{
}

/// Vrátí seznam dostupných Fixed šablon tabulek pro tuto agendu.
/// OUT[1] = JSON pole objektů:
///   [ { "key": "JR_ZAHLAVI", "label": "Záhlaví JR" }, ... ]
ClassMethod GetTableTemplates(ByRef sys As %String) As %Status [ Abstract ]
{
}

/// Vrátí kompletní definici Fixed šablony (sloupce + řádky).
/// IN:  key = klíč šablony (např. "JR_ZAHLAVI")
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

}
