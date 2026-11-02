using System.IO;
using TemplateDesigner.Models;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Services;

/// <summary>Šablony zabalené v .exe jako embedded resource, podle složky v projektu.</summary>
public static class EmbeddedTemplates
{
    /// <summary>Systémové šablony - <see cref="SystemTemplateSeeder"/> je nalévá do MLINE větve,
    /// uživatel je vidí v Otevřít.</summary>
    public const string SystemFolder = "SystemTemplates";

    /// <summary>
    /// Šablony jen pro PDF ze spuštění (<c>Vystup=PDF</c>). Do databáze se nenalévají, takže je
    /// v návrháři nikdo neotevře ani neuloží kopii - rozdílový JŘ zatím není veřejný
    /// (rozhodnutí Jan 23. 10.).
    /// </summary>
    public const string LaunchFolder = "LaunchTemplates";

    /// <summary>Jména resource JSON šablon ze složky, seřazená podle jména souboru.</summary>
    public static IEnumerable<string> ResourceNames(string folder)
    {
        var marker = "." + folder + ".";
        return typeof(EmbeddedTemplates).Assembly.GetManifestResourceNames()
            .Where(n => n.Contains(marker, StringComparison.Ordinal)
                        && n.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal);
    }

    public static string Read(string resourceName)
    {
        using var stream = typeof(EmbeddedTemplates).Assembly.GetManifestResourceStream(resourceName)
                           ?? throw new FileNotFoundException(resourceName);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Šablona agendy ve složce: ta s Id <paramref name="preferredId"/> (výchozí šablona M-line agendy z registru),
    /// jinak první podle jména souboru; null když tam agenda žádnou nemá.
    /// </summary>
    public static Template? ForAgenda(string folder, string agendaCode, string? preferredId = null)
    {
        Template? first = null;
        foreach (var name in ResourceNames(folder))
        {
            var template = TemplateSerializer.Deserialize(Read(name));
            if (!string.Equals(template.AgendaCode, agendaCode, StringComparison.OrdinalIgnoreCase)) continue;
            if (Guid.TryParse(preferredId, out var id) && template.Id == id) return template;
            first ??= template;
        }
        return first;
    }
}
