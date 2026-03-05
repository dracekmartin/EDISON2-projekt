using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using TemplateDesigner.Models.Schema;

namespace TemplateDesigner.Serialization;

/// <summary>
/// Načítá schémata agend z *.schema.json souborů ve složce schemas/
/// (relativní k spuštěné aplikaci). Cachuje výsledky v paměti.
/// </summary>
public class JsonFileDataSchemaProvider : IDataSchemaProvider
{
    private readonly string _schemasDirectory;
    private readonly Dictionary<string, DataSchema> _cache = new();
    private List<AgendaInfo>? _agendaList;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public JsonFileDataSchemaProvider(string schemasDirectory)
    {
        _schemasDirectory = schemasDirectory;
    }

    public IReadOnlyList<AgendaInfo> GetAvailableAgendas()
    {
        if (_agendaList is not null)
            return _agendaList;

        _agendaList = [];

        if (!Directory.Exists(_schemasDirectory))
            return _agendaList;

        foreach (var file in Directory.EnumerateFiles(_schemasDirectory, "*.schema.json"))
        {
            try
            {
                var schema = LoadSchemaFromFile(file);
                if (schema is not null)
                    _agendaList.Add(new AgendaInfo(schema.AgendaCode, schema.AgendaName));
            }
            catch { /* poškozený soubor přeskočíme */ }
        }

        return _agendaList;
    }

    public DataSchema? GetSchema(string agendaCode)
    {
        if (_cache.TryGetValue(agendaCode, out var cached))
            return cached;

        if (!Directory.Exists(_schemasDirectory))
            return null;

        // Hledáme soubor podle agendaCode (case-insensitive)
        var file = Directory.EnumerateFiles(_schemasDirectory, "*.schema.json")
            .FirstOrDefault(f =>
                Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(f))
                    .Equals(agendaCode, StringComparison.OrdinalIgnoreCase));

        if (file is null)
        {
            // Fallback: projdi všechny soubory a hledej podle obsahu
            file = Directory.EnumerateFiles(_schemasDirectory, "*.schema.json")
                .FirstOrDefault(f =>
                {
                    try
                    {
                        var s = LoadSchemaFromFile(f);
                        return s?.AgendaCode.Equals(agendaCode, StringComparison.OrdinalIgnoreCase) == true;
                    }
                    catch { return false; }
                });
        }

        if (file is null)
            return null;

        var schema = LoadSchemaFromFile(file);
        if (schema is not null)
            _cache[agendaCode] = schema;

        return schema;
    }

    private DataSchema? LoadSchemaFromFile(string path)
    {
        var json = File.ReadAllText(path, System.Text.Encoding.UTF8);
        var raw  = JsonSerializer.Deserialize<RawSchema>(json, JsonOptions);
        if (raw is null) return null;

        var schema = new DataSchema
        {
            AgendaCode = raw.AgendaCode ?? "",
            AgendaName = raw.AgendaName ?? raw.AgendaCode ?? "",
            Version    = raw.Version    ?? "1.0",
        };

        // Flat fields (V1)
        if (raw.Fields is { Count: > 0 })
        {
            schema.Fields = raw.Fields.Select(MapField).ToList();
        }

        // FieldCollections (V2+)
        if (raw.FieldCollections is { Count: > 0 })
        {
            schema.FieldCollections = raw.FieldCollections
                .Where(c => !string.IsNullOrEmpty(c.CollectionKey))
                .Select(c => new DataFieldCollection
                {
                    Name          = c.Name ?? "",
                    CollectionKey = c.CollectionKey!,
                    Fields        = c.Fields?.Select(MapField).ToList() ?? []
                }).ToList();

            // Pokud nejsou flat fields, slouč pole ze všech kolekcí do flat listu
            if (schema.Fields.Count == 0)
                schema.Fields = schema.FieldCollections.SelectMany(c => c.Fields).ToList();
        }

        return schema;
    }

    private static DataField MapField(RawField f) => new()
    {
        FieldName   = f.Name        ?? "",
        Label       = f.Label       ?? f.Name ?? "",
        DataType    = f.Type,
        Example     = f.Example,
        Description = f.Description
    };

    // ── Interní raw typy pro deserializaci ──
    // JSON soubor používá zkrácené názvy (name, label, type) místo (FieldName, Label, DataType)

    private class RawSchema
    {
        public string? AgendaCode { get; set; }
        public string? AgendaName { get; set; }
        public string? Version    { get; set; }
        public List<RawField>?           Fields           { get; set; }
        public List<RawFieldCollection>? FieldCollections { get; set; }
    }

    private class RawField
    {
        public string?       Name        { get; set; }
        public string?       Label       { get; set; }
        public FieldDataType Type        { get; set; } = FieldDataType.Text;
        public string?       Example     { get; set; }
        public string?       Description { get; set; }
    }

    private class RawFieldCollection
    {
        public string?         Name          { get; set; }
        public string?         CollectionKey { get; set; }
        public List<RawField>? Fields        { get; set; }
    }
}
