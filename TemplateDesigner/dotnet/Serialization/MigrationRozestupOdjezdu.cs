using System.Text.Json.Nodes;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Serialization;

/// <summary>
/// 1.11.0 → 1.12.0: „Rozestup odjezdů" je nově ve výchozím stavu **Rovnoměrný** (`B143`), protože
/// tak vývěsku chce skoro každý - minuty mají stát ve svislici a značka u jednoho spoje nemá
/// rozhodit zarovnání ostatních.
///
/// <para>Hotové šablony ale nesmí změnit tisk. Ty uložené **před 12. 8. 2026** hodnotu v JSON
/// vůbec nemají (vlastnost tehdy neexistovala), takže by jim nový výchozí stav mlčky přepsal
/// sazbu. Tahle migrace jim proto dopíše „Podle obsahu", tedy to, podle čeho se tiskly.</para>
///
/// <para>Idempotentní: prvek s vlastní hodnotou se nechává být.</para>
/// </summary>
public class MigrationRozestupOdjezdu : ITemplateMigration
{
    public string FromVersion => "1.11.0";

    public string Description => "výchozí rozestup odjezdů je Rovnoměrný; staré šablony si drží Podle obsahu";

    public JsonNode Migrate(JsonNode root)
    {
        if (root["sections"] is not JsonArray sections) return root;

        foreach (var section in sections)
        {
            if (section?["elements"] is not JsonArray elements) continue;
            foreach (var el in elements)
            {
                if (el is not JsonObject o) continue;
                if (o["type"]?.GetValue<string>() != ElementTypes.DepartureGrid) continue;
                if (!o.ContainsKey("minuteSpacing"))
                    o["minuteSpacing"] = nameof(DepartureSpacing.ByContent);
            }
        }
        return root;
    }
}
