/// Abstraktní základ pro implementaci agendy v TemplateDesigneru.
///
/// Agenda = zdroj dat a doménová logika pro konkrétní typ tiskové sestavy:
/// - **MHD** - zastávkové jízdní řády městské hromadné dopravy (linky, zastávky, spoje)
/// - **FAKTURY** - fakturační sestavy (faktury, položky, DPH)
/// - připravované: Meziměstský JŘ, Knižní JŘ, Vozový JŘ
///
/// Každá agenda dědí z této třídy a implementuje povinné Abstract metody:
/// - GetSchema           - definice dostupných polí (skalární + tabulkové kolekce)
/// - GetData             - reálná data pro tisk (per IDFa, per instance)
/// - GetMockData         - vzorová data pro live preview v designeru
/// - GetTableTemplates   - seznam Fixed šablon tabulek (záhlaví, platnost, ...)
/// - GetTableTemplate    - definice konkrétní Fixed šablony (sloupce + řádky)
///
/// Volitelně přepsatelné (default = prázdný seznam):
/// - GetParameters       - parametry pro PrintDialog (zaškrtávátka, výběry, ...)
/// - GetAvailableInstances - seznam instancí pro Instance Picker
///
/// Caché kompilátor vynucuje implementaci Abstract metod; bez nich třída neprojde compile.
///
/// Registrace agendy (Wave 2, 2026-05-20; dvouosý model 2026-07-11):
///   Agenda se registruje **deklarativně** přes class parametry - kombinovaný klíč
///   AgendaVariantCode + osy Agenda/Variant (+ labely). ALVA.TemplateDesigner.API.AgendyRegistr
///   prochází %Dictionary, najde všechny subclassy BaseAgenda a vrátí jejich metadata
///   jako JSON list. Vytvoření třídy = registrace konfigurace (agenda × varianta).
///
///   GENERAL agenda (šablona bez DB vazby) nemá Caché class - .NET strana ji inject
///   syntheticky v IAgendaRegistry impl.
Class ALVA.TemplateDesigner.Agendy.BaseAgenda Extends %RegisteredObject [ Abstract ]
{

/// Kombinovaný kód konfigurace agenda+varianta (např. "ZASTAVKOVY_JR_AD", "FAKTURY").
/// Override v každé subclass. **Stabilní persistovaný klíč** - žije v uložených šablonách
/// (Template JSON `agendaCode`), DB indexu ByAgenda, per-uživatel preferencích a EDISON
/// launch kontraktu (`Agenda=<kód>`); hodnoty se NIKDY nemění (jen aditivně přibývají).
/// Konvence: <AGENDA>[_<VARIANTA>], velkými písmeny, podtržítka.
Parameter AgendaVariantCode As %String = "";

/// Kód agendy = nejvyšší úroveň členění ("ZASTAVKOVY_JR", "LINKOVY_JR", "FAKTURY", "GENERAL").
/// Dvouosý model (2026-07-11): agenda (co za sestavu dělám) × varianta (datový zdroj).
/// Zrcadlí legacy: TiskMHD/TiskJR = typ jako aplikace + skrytý přepínač VolanoZ (JRMAIN×MHD);
/// budoucí RG agendy (Fakturace, MHD provozní, Mzdy…) přibudou jako další agendy bez variant.
Parameter Agenda As %String = "";

/// User-facing label AGENDY (např. "Zastávkový JŘ", "Faktury") - bez varianty.
/// Override v každé subclass. (Do 2026-07-11 nesl kombinovaný label vč. "(AD)" -
/// kombinovaný label pro starší klienty počítá AgendyRegistr do wire klíče `label`.)
Parameter AgendaLabel As %String = "";

/// Kód varianty agendy = datový zdroj ("AD", "MHD"); "" = agenda bez variant (FAKTURY).
/// Legacy ekvivalent: přepínač VolanoZ (JRMAIN×MHD) v TiskMHD/TiskJR.
Parameter Variant As %String = "";

/// User-facing label varianty (např. "AD (linková doprava)", "MHD"); "" = bez varianty.
Parameter VariantLabel As %String = "";

/// Zveřejněná pro uživatele (1) vs interní/pilotní (0, default). Nezveřejněná agenda se
/// nenabízí v UI výběru (Změnit agendu, filtr šablon), ale zůstává plně funkční - existující
/// šablony s jejím kódem se otevřou, EDISON launch funguje. Zveřejnění = vědomý krok (přepnout
/// na 1), nová agenda „neuteče" k uživatelům omylem.
Parameter Released As %Boolean = 0;

/// Jméno IN parametru GetData, kterým se předává vybraná instance (linka/faktura).
/// Zastávkové a knižní JŘ = "LINKA_ID", ostatní default "INSTANCE_ID". Okno ho čte
/// z registru (AgendaRegistryEntry) → žádné hardcoded větvení dle kódu agendy.
Parameter InstanceParamName As %String = "INSTANCE_ID";

/// Druh JŘ elementů, které agenda podporuje - řídí kontextové menu/paletu okna.
/// "STOP" = zastávkové (StopList/DepartureGrid/NotesLegend), "LINE" = knižní/linkové
/// (LineTable), "" = bez JŘ kontextu (okno ukáže všechny). C# vlastní mapování druh→typy.
Parameter ElementKind As %String = "";

/// User-facing název „instance" agendy (jedn./mn. č., 1. pád) - okno ho zobrazí místo
/// obecného „instance" (JŘ = „linka"/„linky", faktury = „faktura"/„faktury"). Override
/// v subclass. Default „instance" pro agendu bez doménového pojmu.
Parameter InstanceLabel As %String = "instance";

Parameter InstanceLabelPlural As %String = "instance";

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
/// Výchozí implementace vrátí prázdný seznam - přepsat v konkrétní agendě.
/// OUT[1] = JSON pole:
///   [
///     { "name": "TISK_SIPKY",  "label": "Tisknout šipky",  "type": "bool",   "defaultValue": "true" },
///     { "name": "FORMAT_CASU", "label": "Formát času",      "type": "enum",   "defaultValue": "H:mm",
///       "options": ["H:mm", "HH:mm"] }
///   ]
/// Typy: "bool", "enum", "string", "number", "date"
/// defaultValue je vždy string - C# strana konvertuje dle type.
ClassMethod GetParameters(ByRef sys As %String) As %Status
{
    s ^||ML("OUT", $i(^||ML("OUT"))) = "[]"
    q 1
}

/// Vrátí seznam dostupných instancí agendy (např. linky pro MHD, faktury pro FAKTURY).
/// Slouží pro Instance Picker v TD - uživatel vybere 1-N instancí, nad kterými pracuje.
/// 
/// Výchozí implementace vrací prázdný seznam - agenda bez konceptu instancí (GENERAL,
/// případně FAKTURY před implementací) ho nemusí přepisovat.
/// 
/// IN["search"] = volitelný filtr (substring, case-insensitive)
/// IN["limit"]  = max počet řádků (default 50)
/// OUT["1"] = JSON pole:
///   [
///     { "value": "42", "display": "42 - Praha–Brno", "subtitle": "Platnost od 1.1.2024" },
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

/// Vrátí seznam skupin linek pro filtr v Instance Pickeru (vybere se právě jedna → načtou se její
/// instance přes GetAvailableInstances(SKUPINA_LINEK_ID)). OUT[1] = JSON [{ "value": id, "label": název }].
/// Default = prázdné pole → agenda nemá koncept skupin (picker filtr skryje). JR agendy override.
ClassMethod GetAvailableSkupiny(ByRef sys As %String) As %Status
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
/// group      = volitelná skupina pro vizuální seskupení v UI (aditivní klíč kontraktu)
ClassMethod AddBoolParam(arr As %DynamicArray, name As %String, label As %String, defaultVal As %Boolean, group As %String = "")
{
    s param = ##class(%DynamicObject).%New()
    s param.name  = name
    s param.label = label
    s param.type  = "bool"
    // Call-sites předávají i string "true" - numericky je 0, holé $s(defaultVal:...) by ho vyhodnotilo
    // jako false (historicky tichý bug: TISK_SIPKY default na wire "false" proti záměru).
    s param.defaultValue = $s((defaultVal=1)||(defaultVal="true"):"true", 1:"false")
    s:group'="" param.group = group
    d arr.%Push(param)
}

/// Přidá number parametr do pole parametrů.
ClassMethod AddNumberParam(arr As %DynamicArray, name As %String, label As %String, defaultVal As %String, group As %String = "")
{
    s param = ##class(%DynamicObject).%New()
    s param.name  = name
    s param.label = label
    s param.type  = "number"
    s param.defaultValue = defaultVal
    s:group'="" param.group = group
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
