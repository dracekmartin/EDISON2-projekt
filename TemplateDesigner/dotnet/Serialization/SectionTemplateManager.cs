using System.IO;
using TemplateDesigner.Models;

namespace TemplateDesigner.Serialization;

/// <summary>
/// Správce šablon sekcí.
/// V1: Ukládá do JSON souborů v section-templates/.
/// V2: Lze rozšířit na databázový provider (Caché).
/// </summary>
public class SectionTemplateManager
{
    private readonly string _templatesDir;

    public SectionTemplateManager()
    {
        _templatesDir = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "section-templates"
        );

        // Vytvoř složku pokud neexistuje
        Directory.CreateDirectory(_templatesDir);
    }

    /// <summary>Uloží sekci jako šablonu pod zadaným názvem.</summary>
    public void Save(Section section, string name)
    {
        var fileName = SanitizeFileName(name) + ".json";
        var path = Path.Combine(_templatesDir, fileName);

        // Deep clone aby se neuložily reference
        var clone = section.Clone();

        var json = TemplateSerializer.SerializeSection(clone);
        File.WriteAllText(path, json, System.Text.Encoding.UTF8);
    }

    /// <summary>Načte šablonu sekce podle názvu.</summary>
    public Section? Load(string name)
    {
        var fileName = SanitizeFileName(name) + ".json";
        var path = Path.Combine(_templatesDir, fileName);

        if (!File.Exists(path))
            return null;

        var json = File.ReadAllText(path, System.Text.Encoding.UTF8);
        return TemplateSerializer.DeserializeSection(json);
    }

    /// <summary>Vrátí seznam dostupných šablon (názvy bez přípony).</summary>
    public List<string> GetAvailableTemplates()
    {
        if (!Directory.Exists(_templatesDir))
            return new List<string>();

        return Directory.GetFiles(_templatesDir, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => n != null)
            .Select(n => n!)
            .OrderBy(n => n)
            .ToList();
    }

    /// <summary>Smaže šablonu.</summary>
    public bool Delete(string name)
    {
        var fileName = SanitizeFileName(name) + ".json";
        var path = Path.Combine(_templatesDir, fileName);

        if (!File.Exists(path))
            return false;

        File.Delete(path);
        return true;
    }

    /// <summary>Odstranění nepovolených znaků z názvu souboru.</summary>
    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        foreach (var c in invalid)
            name = name.Replace(c, '-');

        // Převést na lowercase pro konzistenci
        return name.ToLowerInvariant();
    }
}
