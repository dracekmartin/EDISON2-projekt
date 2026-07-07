using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using ALWA.SYS;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Schema;

namespace TemplateDesigner.Serialization;

/// <summary>
/// Volá Caché BaseAgenda (GetSchema, GetTableTemplates, GetTableTemplate) přes MLRest.
/// Nahrazuje CacheDataSchemaProvider — pracuje s JSON formátem místo pipe-delimited stringů.
///
/// Každá agenda má vlastní Caché třídu dědící z ALVA.TemplateDesigner.Agendy.BaseAgenda,
/// nakonfigurovanou v templatedesigner.appsettings.json jako "cacheClass": "ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd".
///
/// ⚠️ Metody jsou ZÁMĚRNĚ SYNCHRONNÍ (`_mlRest.Query`). Pomalé operace (`GetData`, `GetAvailableInstances`,
/// první `GetSchema`) musí volající obalit `await Task.Run(...)` — R1, jinak zamrzne UI thread.
/// Rychlé/cachované lookupy (`GetParameters`, opakované `GetSchema`) sync OK. Nová agenda: neplánuj
/// `GetData` přímo z UI threadu.
/// </summary>
public class CacheAgendaProvider : IDataSchemaProvider, ITableTemplateProvider
{
    private readonly MLRest _mlRest;

    /// <summary>agendaCode → cacheClass mapping z appsettings</summary>
    private readonly Dictionary<string, string> _classMap;

    private readonly List<AgendaInfo> _agendas;

    // Cache pro opakované volání
    private readonly Dictionary<string, DataSchema>                  _schemaCache     = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TableTemplateDefinition?>    _templateCache   = new(StringComparer.OrdinalIgnoreCase);
    // Klíč: agendaCode nebo "" pro "všechny agendy" (null argument)
    private readonly Dictionary<string, List<TableTemplateInfo>>     _templatesCache  = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DataContext?>                _mockDataCache   = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<TemplateParameter>>     _parametersCache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CacheAgendaProvider(MLRest mlRest, IEnumerable<(string Code, string Name, string CacheClass)> agendas)
    {
        _mlRest   = mlRest;
        var list  = agendas.ToList();  // materializace — IEnumerable se nesmí iterovat dvakrát
        _agendas  = list.Select(a => new AgendaInfo(a.Code, a.Name)).ToList();
        _classMap = list.ToDictionary(
            a => a.Code,
            a => a.CacheClass,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Nemodální oznámení chyby ČTENÍ z Caché (schema/parametry/mock/instance) — provider chybu
    /// nechá „prázdno" (null/[]), ale zavolá tento callback, aby UI ukázalo, že data chybí kvůli
    /// selhání (typicky VPN/Caché nedostupná), ne že jsou legitimně prázdná. Volá se JEN na
    /// RestStatus.Error / výjimku, ne na legitimní prázdný výsledek. Může fírnout z background
    /// threadu (Task.Run) — konzument si marshalne na UI thread. `GetData` chybu naopak HÁZÍ (B2).
    /// </summary>
    public Action<string>? OnReadError { get; set; }

    private void NotifyReadError(string context, string detail)
    {
        Debug.WriteLine($"[AgendaProvider] {context}: {detail}");
        OnReadError?.Invoke($"⚠ Databáze — {context} selhalo: {detail}");
    }

    // ─── IDataSchemaProvider ──────────────────────────────────────────────────

    public IReadOnlyList<AgendaInfo> GetAvailableAgendas() => _agendas;

    /// <summary>FQN Caché třídy agendy (např. pro EDInputForm AgendaParams).
    /// Null pokud agenda nemá Caché vazbu (GENERAL) nebo není známá.</summary>
    public string? GetCacheClass(string agendaCode) =>
        _classMap.TryGetValue(agendaCode, out var c) && !string.IsNullOrWhiteSpace(c) ? c : null;

    public DataSchema? GetSchema(string agendaCode)
    {
        if (string.IsNullOrEmpty(agendaCode)) return null;

        if (_schemaCache.TryGetValue(agendaCode, out var cached))
            return cached;

        if (!_classMap.TryGetValue(agendaCode, out var cacheClass))
            return null;

        // Agenda bez Caché třídy (např. GENERAL) — žádné DB schema, jen statický obsah šablony
        if (string.IsNullOrWhiteSpace(cacheClass)) return null;

        var agenda = _agendas.FirstOrDefault(a =>
            a.Code.Equals(agendaCode, StringComparison.OrdinalIgnoreCase));
        if (agenda is null) return null;

        try
        {
            CVar result = _mlRest.Query(cacheClass, "GetSchema", new CVar());

            if (_mlRest.RestStatus.State == MLRStatus.EState.Error)
            {
                NotifyReadError("načtení schématu", _mlRest.RestStatus.Message);
                return null;
            }

            // OUT[1] = JSON objekt se "fields" a "fieldCollections"
            string? json = result["1"]?.Value?.ToString();
            if (string.IsNullOrEmpty(json)) return null;

            var schema = ParseSchema(agendaCode, agenda.Name, json);
            if (schema is not null)
                _schemaCache[agendaCode] = schema;

            return schema;
        }
        catch (Exception ex)
        {
            NotifyReadError("načtení schématu", ex.Message);
            return null;
        }
    }

    // ─── ITableTemplateProvider ───────────────────────────────────────────────

    public IReadOnlyList<TableTemplateInfo> GetAvailableTemplates(string? agendaCode = null)
    {
        string cacheKey = agendaCode ?? "";
        if (_templatesCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var result = new List<TableTemplateInfo>();

        foreach (var (code, cacheClass) in _classMap)
        {
            if (agendaCode is not null &&
                !code.Equals(agendaCode, StringComparison.OrdinalIgnoreCase))
                continue;

            // Agenda bez Caché třídy (GENERAL) — žádné Fixed šablony
            if (string.IsNullOrWhiteSpace(cacheClass)) continue;

            try
            {
                CVar cvResult = _mlRest.Query(cacheClass, "GetTableTemplates", new CVar());

                if (_mlRest.RestStatus.State == MLRStatus.EState.Error)
                {
                    Debug.WriteLine($"[AgendaProvider] GetTableTemplates error: {_mlRest.RestStatus.Message}");
                    continue;
                }

                string? json = cvResult["1"]?.Value?.ToString();
                if (string.IsNullOrEmpty(json)) continue;

                var templates = JsonSerializer.Deserialize<List<TableTemplateInfo>>(json, JsonOpts);
                if (templates is not null)
                    result.AddRange(templates);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AgendaProvider] GetTableTemplates exception ({code}): {ex.Message}");
            }
        }

        _templatesCache[cacheKey] = result;
        return result;
    }

    public TableTemplateDefinition? GetTemplate(string key)
    {
        if (_templateCache.TryGetValue(key, out var cached))
            return cached;

        // Zkus každou třídu dokud nenajdeme šablonu
        foreach (var cacheClass in _classMap.Values)
        {
            // Agenda bez Caché třídy (GENERAL) — žádné šablony
            if (string.IsNullOrWhiteSpace(cacheClass)) continue;

            try
            {
                var inVar = new CVar();
                inVar["key"] = new CVar(key);

                CVar result = _mlRest.Query(cacheClass, "GetTableTemplate", inVar);

                if (_mlRest.RestStatus.State == MLRStatus.EState.Error)
                    continue;

                string? json = result["1"]?.Value?.ToString();
                if (string.IsNullOrEmpty(json)) continue;

                var def = ParseTableTemplate(json);
                if (def is not null)
                {
                    _templateCache[key] = def;
                    return def;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AgendaProvider] GetTableTemplate exception: {ex.Message}");
            }
        }

        _templateCache[key] = null;
        return null;
    }

    // ─── Parametry sestavy ───────────────────────────────────────────────────

    /// <summary>
    /// Volá GetParameters na Caché třídě a vrátí seznam parametrů sestavy.
    /// Prázdný seznam = agenda nemá parametry nebo metoda není implementována.
    /// </summary>
    public IReadOnlyList<TemplateParameter> GetParameters(string agendaCode)
    {
        if (string.IsNullOrEmpty(agendaCode)) return [];
        if (_parametersCache.TryGetValue(agendaCode, out var cached)) return cached;
        if (!_classMap.TryGetValue(agendaCode, out var cacheClass)) return [];

        // Agenda bez Caché třídy (GENERAL) — žádné parametry
        if (string.IsNullOrWhiteSpace(cacheClass)) { _parametersCache[agendaCode] = []; return []; }

        try
        {
            CVar result = _mlRest.Query(cacheClass, "GetParameters", new CVar());

            if (_mlRest.RestStatus.State == MLRStatus.EState.Error)
            {
                NotifyReadError("načtení parametrů", _mlRest.RestStatus.Message);
                return []; // chybu NEcachovat — příští volání zkusí znovu (po zotavení Caché se params objeví bez restartu)
            }

            string? json = result["1"]?.Value?.ToString();
            if (string.IsNullOrEmpty(json))
            {
                _parametersCache[agendaCode] = [];
                return [];
            }

            var parameters = ParseParameters(json);
            _parametersCache[agendaCode] = parameters;
            return parameters;
        }
        catch (Exception ex)
        {
            NotifyReadError("načtení parametrů", ex.Message);
            return []; // chybu NEcachovat — příští volání zkusí znovu
        }
    }

    private static List<TemplateParameter> ParseParameters(string json)
    {
        try
        {
            var node = JsonNode.Parse(json);
            if (node is not JsonArray arr) return [];

            var result = new List<TemplateParameter>();
            foreach (var item in arr)
            {
                if (item is null) continue;
                var name = item["name"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(name)) continue;

                var p = new TemplateParameter
                {
                    Name         = name,
                    Label        = item["label"]?.GetValue<string>() ?? name,
                    Type         = item["type"]?.GetValue<string>() ?? "string",
                    DefaultValue = item["defaultValue"]?.GetValue<string>(),
                    VisibleWhen  = item["visibleWhen"]?.GetValue<string>()
                };

                if (item["options"] is JsonArray opts)
                    p.Options = opts.Select(o => o?.GetValue<string>() ?? "")
                                    .Where(s => s.Length > 0)
                                    .ToList();

                result.Add(p);
            }
            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AgendaProvider] ParseParameters exception: {ex.Message}");
            return [];
        }
    }

    // ─── Dostupné instance (linky / faktury / ...) ───────────────────────────

    /// <summary>
    /// Volá GetAvailableInstances na Caché třídě a vrátí seznam instancí (např. linek pro MHD).
    /// Server-side filter — předaný search se aplikuje v Caché (case-insensitive substring).
    /// Prázdný seznam = agenda nemá koncept instancí (GENERAL) nebo žádné neodpovídají filtru.
    /// </summary>
    public IReadOnlyList<AgendaInstance> GetAvailableInstances(string agendaCode, string? search = null, int limit = 50)
    {
        if (string.IsNullOrEmpty(agendaCode)) return [];
        if (!_classMap.TryGetValue(agendaCode, out var cacheClass)) return [];
        if (string.IsNullOrWhiteSpace(cacheClass)) return [];

        try
        {
            var inVar = new CVar();
            inVar["search"] = new CVar(search ?? "");
            inVar["limit"]  = new CVar(limit.ToString());
            // Pozn.: filtr skupiny linek (SKUPINA_LINEK_ID) teče přes EDList dispatch (Instance.LoadList),
            // ne přes tuto metodu — Instance Picker načítá řádky EDListem. Tady zůstává default skupina.

            CVar result = _mlRest.Query(cacheClass, "GetAvailableInstances", inVar);
            if (_mlRest.RestStatus.State == MLRStatus.EState.Error)
            {
                NotifyReadError("načtení instancí", _mlRest.RestStatus.Message);
                return [];
            }

            string? json = result["1"]?.Value?.ToString();
            if (string.IsNullOrEmpty(json)) return [];

            var node = JsonNode.Parse(json);
            if (node is not JsonArray arr) return [];

            var list = new List<AgendaInstance>(arr.Count);
            foreach (var item in arr)
            {
                if (item is null) continue;
                var value   = item["value"]?.GetValue<string>();
                var display = item["display"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(display)) continue;
                var subtitle = item["subtitle"]?.GetValue<string>();
                list.Add(new AgendaInstance(value, display, subtitle));
            }
            return list;
        }
        catch (Exception ex)
        {
            NotifyReadError("načtení instancí", ex.Message);
            return [];
        }
    }

    /// <summary>
    /// Volá GetAvailableSkupiny na Caché třídě a vrátí seznam skupin linek pro filtr Instance Pickeru.
    /// Prázdný seznam = agenda nemá koncept skupin (GENERAL, faktury) → picker filtr skryje.
    /// </summary>
    public IReadOnlyList<AgendaSkupina> GetAvailableSkupiny(string agendaCode)
    {
        if (string.IsNullOrEmpty(agendaCode)) return [];
        if (!_classMap.TryGetValue(agendaCode, out var cacheClass)) return [];
        if (string.IsNullOrWhiteSpace(cacheClass)) return [];

        try
        {
            CVar result = _mlRest.Query(cacheClass, "GetAvailableSkupiny", new CVar());
            if (_mlRest.RestStatus.State == MLRStatus.EState.Error)
            {
                NotifyReadError("načtení skupin linek", _mlRest.RestStatus.Message);
                return [];
            }

            string? json = result["1"]?.Value?.ToString();
            if (string.IsNullOrEmpty(json)) return [];

            var node = JsonNode.Parse(json);
            if (node is not JsonArray arr) return [];

            var list = new List<AgendaSkupina>(arr.Count);
            foreach (var item in arr)
            {
                if (item is null) continue;
                var value = item["value"]?.GetValue<string>();
                var label = item["label"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(value)) continue;
                list.Add(new AgendaSkupina(value, string.IsNullOrWhiteSpace(label) ? value : label));
            }
            return list;
        }
        catch (Exception ex)
        {
            NotifyReadError("načtení skupin linek", ex.Message);
            return [];
        }
    }

    // ─── Reálná data pro tisk ───────────────────────────────────────────────

    /// <summary>
    /// Volá GetData na Caché třídě s parametry a vrátí seznam DataContextů — jeden per "stránka" dat.
    /// Pro JŘ: vstup = LinkaID → výstup = pole stránek (1 per zastávka × směr).
    /// Null = metoda selhala.
    /// </summary>
    public List<DataContext>? GetData(string agendaCode, Dictionary<string, object?> parameters)
    {
        if (string.IsNullOrEmpty(agendaCode)) return null;
        if (!_classMap.TryGetValue(agendaCode, out var cacheClass)) return null;

        // Agenda bez Caché třídy (GENERAL) — žádná reálná data, šablona se tiskne s parametry přímo
        if (string.IsNullOrWhiteSpace(cacheClass)) return null;

        try
        {
            var inVar = new CVar();
            foreach (var (key, value) in parameters)
                inVar[key] = new CVar(value?.ToString() ?? "");

            CVar result = _mlRest.Query(cacheClass, "GetData", inVar);

            // Chyba Caché NENÍ „prázdno" — vyhodit, ať ji volající (export/tisk) pozná a nespadne
            // tiše na mock data. `null` níže znamená výhradně „legitimně žádná data", ne selhání.
            if (_mlRest.RestStatus.State == MLRStatus.EState.Error)
                throw new CacheAgendaException(
                    $"Načtení dat agendy '{agendaCode}' z databáze selhalo: {_mlRest.RestStatus.Message}");

            // Každý OUT node = jedna stránka (JSON objekt { "values": {...}, "rows": {...} })
            var pages = new List<DataContext>();
            foreach (var kvp in result)
            {
                if (kvp.Key == "ERR") continue;
                string? json = kvp.Value?.Value?.ToString();
                if (string.IsNullOrEmpty(json)) continue;

                try
                {
                    var node = JsonNode.Parse(json);
                    if (node is null) continue;
                    var dc = ParseSinglePageData(node);
                    if (dc is not null) pages.Add(dc);
                }
                catch
                {
                    // Přeskočit nevalidní stránku
                }
            }
            return pages.Count > 0 ? pages : null;
        }
        catch (CacheAgendaException)
        {
            throw;   // už je to naše doménová chyba — nepřebalovat
        }
        catch (Exception ex)
        {
            // .NET výjimka (spojení, parsování wire) = také selhání, ne „prázdno".
            throw new CacheAgendaException(
                $"Chyba při načítání dat agendy '{agendaCode}' z databáze: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Parsuje jeden objekt stránky { "values": {...}, "rows": {...} } do DataContext.
    /// Sdílená logika pro GetData i GetMockData.
    /// </summary>
    private static DataContext? ParseSinglePageData(JsonNode node)
    {
        // Skalární hodnoty (+ vnořená pole/objekty jako JsonNode — např. VARIANTY_TRAS[])
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (node["values"]?.AsObject() is { } valObj)
            foreach (var kv in valObj)
            {
                if (kv.Value is JsonArray or JsonObject)
                    values[kv.Key] = kv.Value;  // page-level pole/objekt (VARIANTY_TRAS) — zachovat jako JsonNode
                else
                    values[kv.Key] = GetScalarAsString(kv.Value);
            }

        // Tabulkové kolekce
        var rowsDict = new Dictionary<string, IReadOnlyList<DataContext>>(StringComparer.OrdinalIgnoreCase);
        if (node["rows"]?.AsObject() is { } rowsObj)
        {
            foreach (var (collKey, rowsArr) in rowsObj)
            {
                if (rowsArr is not JsonArray arr) continue;
                var rows = new List<DataContext>();
                foreach (var rowNode in arr)
                {
                    if (rowNode is not JsonObject rowObj) continue;
                    var rowValues = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    foreach (var kv in rowObj)
                    {
                        if (kv.Value is JsonArray or JsonObject)
                            rowValues[kv.Key] = kv.Value;  // vnořená pole/objekty — uložit jako JsonNode
                        else
                            rowValues[kv.Key] = GetScalarAsString(kv.Value);
                    }
                    rows.Add(new DataContext(rowValues));
                }
                rowsDict[collKey] = rows;
            }
        }

        return new DataContext(values, rowsDict);
    }

    // ─── Mock data pro preview ────────────────────────────────────────────────

    /// <summary>
    /// Volá GetMockData na Caché třídě a vrátí DataContext pro live preview.
    /// Null = metoda není dostupná nebo selžala.
    /// </summary>
    public DataContext? GetMockData(string agendaCode)
    {
        if (string.IsNullOrEmpty(agendaCode)) return null;
        if (_mockDataCache.TryGetValue(agendaCode, out var cached)) return cached;
        if (!_classMap.TryGetValue(agendaCode, out var cacheClass)) return null;

        // Agenda bez Caché třídy (GENERAL) — žádná mock data, preview používá SampleData ze šablony
        if (string.IsNullOrWhiteSpace(cacheClass)) return null;

        try
        {
            CVar result = _mlRest.Query(cacheClass, "GetMockData", new CVar());
            if (_mlRest.RestStatus.State == MLRStatus.EState.Error) { NotifyReadError("načtení mock dat", _mlRest.RestStatus.Message); return null; }

            string? json = result["1"]?.Value?.ToString();
            if (string.IsNullOrEmpty(json)) return null;

            var data = ParseMockData(json);
            if (data is not null)
                _mockDataCache[agendaCode] = data;  // necachovat null — příští pokus zkusí znovu
            return data;
        }
        catch (Exception ex)
        {
            NotifyReadError("načtení mock dat", ex.Message);
            return null;
        }
    }

    private static DataContext? ParseMockData(string json)
    {
        try
        {
            var node = JsonNode.Parse(json);
            if (node is null) return null;
            return ParseSinglePageData(node);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AgendaProvider] ParseMockData exception: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Bezpečně extrahuje skalární hodnotu z JsonNode jako string.
    /// Caché %DynamicObject zapisuje čísla jako JSON numbers a booly jako JSON booleans,
    /// ale GetValue&lt;string&gt;() na ne-stringovém JsonValue vyhodí výjimku.
    /// </summary>
    private static string? GetScalarAsString(JsonNode? node)
    {
        if (node is not JsonValue jv) return null;
        // String — nejčastější případ
        if (jv.TryGetValue<string>(out var s)) return s;
        // Čísla (int, long, double) a bool — konverze na string
        if (jv.TryGetValue<long>(out var l)) return l.ToString();
        if (jv.TryGetValue<double>(out var d)) return d.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (jv.TryGetValue<bool>(out var b)) return b ? "true" : "false";
        // Fallback — JSON representace bez uvozovek
        return jv.ToJsonString().Trim('"');
    }

    // ─── JSON parsing ─────────────────────────────────────────────────────────

    private static DataSchema? ParseSchema(string agendaCode, string agendaName, string json)
    {
        try
        {
            var node = JsonNode.Parse(json);
            if (node is null) return null;

            var schema = new DataSchema
            {
                AgendaCode       = agendaCode,
                AgendaName       = agendaName,
                Fields           = [],
                FieldCollections = []
            };

            // Skalární pole
            var fields = node["fields"]?.AsArray();
            if (fields is not null)
                foreach (var f in fields)
                {
                    var field = ParseField(f);
                    if (field is not null) schema.Fields.Add(field);
                }

            // Tabulkové kolekce — "fieldCollections" je aktuální formát; "groups" je starší název
            // pro zpětnou kompatibilitu se staršími Caché třídami, které ještě nebyly aktualizovány
            var collections = node["fieldCollections"]?.AsArray();
            if (collections is null && node["groups"] is not null)
            {
                collections = node["groups"]?.AsArray();
                Debug.WriteLine($"[AgendaProvider] DEPRECATED: agenda '{agendaCode}' vrátila 'groups' místo 'fieldCollections'. Aktualizuj Caché třídu.");
            }
            if (collections is not null)
            {
                foreach (var c in collections)
                {
                    if (c is null) continue;
                    var key = c["collectionKey"]?.GetValue<string>() ?? "";
                    if (string.IsNullOrEmpty(key)) continue;  // kolekce bez klíče nelze použít

                    var collection = new DataFieldCollection
                    {
                        Name          = c["name"]?.GetValue<string>() ?? "",
                        CollectionKey = key,
                        Fields        = []
                    };

                    var cFields = c["fields"]?.AsArray();
                    if (cFields is not null)
                        foreach (var f in cFields)
                        {
                            var field = ParseField(f);
                            if (field is not null) collection.Fields.Add(field);
                        }

                    schema.FieldCollections.Add(collection);
                }
            }

            return schema;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AgendaProvider] ParseSchema exception: {ex.Message}");
            return null;
        }
    }

    private static DataField? ParseField(JsonNode? node)
    {
        if (node is null) return null;
        var name = node["name"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(name)) return null;

        return new DataField
        {
            FieldName   = name,
            Label       = node["label"]?.GetValue<string>() ?? name,
            DataType    = ParseFieldType(node["type"]?.GetValue<string>()),
            Example     = node["example"]?.GetValue<string>(),
            Description = node["description"]?.GetValue<string>()
        };
    }

    private static FieldDataType ParseFieldType(string? type) => type?.ToLower() switch
    {
        "number"   => FieldDataType.Number,
        "date"     => FieldDataType.Date,
        "time"     => FieldDataType.Time,
        "datetime" => FieldDataType.DateTime,
        "boolean"  => FieldDataType.Boolean,
        _          => FieldDataType.Text
    };

    private static TableTemplateDefinition? ParseTableTemplate(string json)
    {
        try
        {
            var node = JsonNode.Parse(json);
            if (node is null) return null;

            var def = new TableTemplateDefinition();

            var cols = node["columns"]?.AsArray();
            if (cols is not null)
                foreach (var c in cols)
                {
                    if (c is null) continue;
                    def.Columns.Add(new TableColumn
                    {
                        Header    = c["header"]?.GetValue<string>() ?? "",
                        Width     = c["width"]?.GetValue<float>() ?? 30f,
                        WidthMode = c["widthMode"]?.GetValue<string>()?.ToLower() == "percent"
                                        ? ColumnWidthMode.Percent : ColumnWidthMode.Mm,
                        Field     = c["field"]?.GetValue<string>(),
                        Format    = c["format"]?.GetValue<string>(),
                        Alignment = ParseAlignment(c["alignment"]?.GetValue<string>())
                    });
                }

            var rows = node["rows"]?.AsArray();
            if (rows is not null)
                foreach (var r in rows)
                {
                    if (r is null) continue;
                    var row = new TableRow
                    {
                        BackgroundColor      = r["backgroundColor"]?.GetValue<string>(),
                        VisibilityExpression = r["visibilityExpression"]?.GetValue<string>()
                    };

                    var cells = r["cells"]?.AsArray();
                    if (cells is not null)
                        foreach (var cell in cells)
                        {
                            if (cell is null) continue;
                            var fontWeight = cell["fontWeight"]?.GetValue<string>();
                            var textColor  = cell["textColor"]?.GetValue<string>();
                            var bgColor    = cell["backgroundColor"]?.GetValue<string>();
                            TextStyle? cellStyle = (fontWeight is not null || textColor is not null || bgColor is not null)
                                ? new TextStyle
                                {
                                    Bold = fontWeight == "Bold" ? true : null,
                                    TextColor = textColor,
                                    BackgroundColor = bgColor,
                                }
                                : null;

                            row.Cells.Add(new TableCell
                            {
                                Content = cell["content"]?.GetValue<string>() ?? "",
                                Format  = cell["format"]?.GetValue<string>(),
                                Style   = cellStyle,
                            });
                        }

                    def.Rows.Add(row);
                }

            return def;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AgendaProvider] ParseTableTemplate exception: {ex.Message}");
            return null;
        }
    }

    private static TextHorizontalAlignment ParseAlignment(string? s) => s?.ToLower() switch
    {
        "center" => TextHorizontalAlignment.Center,
        "right"  => TextHorizontalAlignment.Right,
        _        => TextHorizontalAlignment.Left
    };
}

/// <summary>
/// Selhání načtení dat agendy z Caché (chyba RestStatus nebo výjimka). Odlišuje skutečnou chybu
/// od „legitimně žádná data" (to `GetData` signalizuje <c>null</c>) — export/tisk pak na chybu
/// hlasitě upozorní místo tichého propadnutí na mock data. Zrcadlí `CacheTemplateException`.
/// </summary>
public class CacheAgendaException : Exception
{
    public CacheAgendaException(string message) : base(message) { }
    public CacheAgendaException(string message, Exception inner) : base(message, inner) { }
}
