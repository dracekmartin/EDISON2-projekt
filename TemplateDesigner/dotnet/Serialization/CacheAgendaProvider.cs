using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using ALWA.SYS;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Schema;

namespace TemplateDesigner.Serialization;

/// <summary>
/// Volá Caché AgendaAPI (GetSchema, GetTableTemplates, GetTableTemplate) přes MLRest.
/// Nahrazuje CacheDataSchemaProvider — pracuje s JSON formátem místo pipe-delimited stringů.
///
/// Každá agenda má vlastní Caché třídu dědící z ALVA.TemplateDesigner.API.AgendaAPI,
/// nakonfigurovanou v appsettings.json jako "cacheClass": "ALVA.JR.TiskAPI".
/// </summary>
public class CacheAgendaProvider : IDataSchemaProvider, ITableTemplateProvider
{
    private readonly MLRest _mlRest;

    /// <summary>agendaCode → cacheClass mapping z appsettings</summary>
    private readonly Dictionary<string, string> _classMap;

    private readonly List<AgendaInfo> _agendas;

    // Cache pro opakované volání
    private readonly Dictionary<string, DataSchema>               _schemaCache     = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TableTemplateDefinition?> _templateCache   = new(StringComparer.OrdinalIgnoreCase);
    // Klíč: agendaCode nebo "" pro "všechny agendy" (null argument)
    private readonly Dictionary<string, List<TableTemplateInfo>>  _templatesCache  = new(StringComparer.OrdinalIgnoreCase);

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

    // ─── IDataSchemaProvider ──────────────────────────────────────────────────

    public IReadOnlyList<AgendaInfo> GetAvailableAgendas() => _agendas;

    public DataSchema? GetSchema(string agendaCode)
    {
        if (_schemaCache.TryGetValue(agendaCode, out var cached))
            return cached;

        if (!_classMap.TryGetValue(agendaCode, out var cacheClass))
            return null;

        var agenda = _agendas.FirstOrDefault(a =>
            a.Code.Equals(agendaCode, StringComparison.OrdinalIgnoreCase));
        if (agenda is null) return null;

        try
        {
            CVar result = _mlRest.Query(cacheClass, "GetSchema", new CVar());

            if (_mlRest.RestStatus.State == MLRStatus.EState.Error)
            {
                Debug.WriteLine($"[AgendaProvider] GetSchema error: {_mlRest.RestStatus.Message}");
                return null;
            }

            // OUT[1] = JSON objekt se "fields" a "groups"
            string? json = result["1"]?.Value?.ToString();
            if (string.IsNullOrEmpty(json)) return null;

            var schema = ParseSchema(agendaCode, agenda.Name, json);
            if (schema is not null)
                _schemaCache[agendaCode] = schema;

            return schema;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AgendaProvider] GetSchema exception: {ex.Message}");
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

    // ─── JSON parsing ─────────────────────────────────────────────────────────

    private static DataSchema? ParseSchema(string agendaCode, string agendaName, string json)
    {
        try
        {
            var node = JsonNode.Parse(json);
            if (node is null) return null;

            var schema = new DataSchema
            {
                AgendaCode = agendaCode,
                AgendaName = agendaName,
                Fields     = [],
                Groups     = []
            };

            // Skalární pole
            var fields = node["fields"]?.AsArray();
            if (fields is not null)
            {
                foreach (var f in fields)
                {
                    var field = ParseField(f);
                    if (field is not null) schema.Fields.Add(field);
                }
            }

            // Skupiny tabulek
            var groups = node["groups"]?.AsArray();
            if (groups is not null)
            {
                foreach (var g in groups)
                {
                    if (g is null) continue;
                    var group = new DataFieldGroup
                    {
                        Name          = g["name"]?.GetValue<string>() ?? "",
                        Scope         = g["scope"]?.GetValue<string>()?.ToLower() == "table"
                                            ? FieldScope.Table : FieldScope.Scalar,
                        CollectionKey = g["collectionKey"]?.GetValue<string>(),
                        Fields        = []
                    };

                    var gFields = g["fields"]?.AsArray();
                    if (gFields is not null)
                        foreach (var f in gFields)
                        {
                            var field = ParseField(f);
                            if (field is not null) group.Fields.Add(field);
                        }

                    schema.Groups.Add(group);
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
                            row.Cells.Add(new TableCell
                            {
                                Content    = cell["content"]?.GetValue<string>() ?? "",
                                Format     = cell["format"]?.GetValue<string>(),
                                FontWeight = cell["fontWeight"]?.GetValue<string>(),
                                TextColor  = cell["textColor"]?.GetValue<string>(),
                                BackgroundColor = cell["backgroundColor"]?.GetValue<string>()
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
