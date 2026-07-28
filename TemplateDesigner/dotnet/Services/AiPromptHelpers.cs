using System.Text;
using TemplateDesigner.Models.Schema;

namespace TemplateDesigner.Services;

/// <summary>
/// Sdílené kusy promptů AI importu (AiImportEnricher + AiVisionImportService) -
/// jeden zdroj formátování DataSchema, aby oba prompty nedriftovaly.
/// </summary>
internal static class AiPromptHelpers
{
    /// <summary>
    /// Připojí sekci DATASCHEMA (skalární pole + kolekce + instrukce „jen tato pole"),
    /// nebo hlášku, že schéma není k dispozici (→ žádné mapování).
    /// </summary>
    public static void AppendDataSchemaSection(StringBuilder sb, DataSchema? schema)
    {
        if (schema is null)
        {
            sb.AppendLine("DATASCHEMA: není k dispozici - texty ponech bez mapování (dbField=null).");
            return;
        }

        sb.AppendLine($"DATASCHEMA (agenda: {schema.AgendaCode} - {schema.AgendaName}):");

        if (schema.Fields.Count > 0)
        {
            sb.AppendLine("Skalární pole:");
            foreach (var f in schema.Fields)
            {
                sb.Append($"  - {f.FieldName}");
                if (f.DataType != FieldDataType.Text) sb.Append($" ({f.DataType.ToString().ToLower()})");
                if (!string.IsNullOrEmpty(f.Label)) sb.Append($" - {f.Label}");
                if (!string.IsNullOrEmpty(f.Example)) sb.Append($", např. \"{f.Example}\"");
                sb.AppendLine();
            }
        }

        if (schema.FieldCollections?.Count > 0)
        {
            sb.AppendLine("Kolekce (tabulková data):");
            foreach (var c in schema.FieldCollections)
            {
                sb.AppendLine($"  - {c.CollectionKey} - {c.Name}:");
                foreach (var f in c.Fields)
                {
                    sb.Append($"    - {f.FieldName}");
                    if (!string.IsNullOrEmpty(f.Label)) sb.Append($" - {f.Label}");
                    sb.AppendLine();
                }
            }
        }

        sb.AppendLine();
        sb.AppendLine("DŮLEŽITÉ: Použij POUZE pole z tohoto seznamu. Nevymýšlej vlastní.");
    }
}
