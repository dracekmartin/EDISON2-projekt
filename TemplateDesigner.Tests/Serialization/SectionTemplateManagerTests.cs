using System.IO;
using TemplateDesigner.Models;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Šablony sekcí leží vedle programu, a ten u zákazníka bývá na síťovém disku bez práva zápisu
/// (`\\srv\edison`). Do 12. 10. se složka zakládala už při startu a návrhář se u takového
/// uživatele vůbec nespustil (HO_CSAD 9. 10., UnauthorizedAccessException).
/// </summary>
public class SectionTemplateManagerTests
{
    [Fact]
    public void BezPravaZapisu_StartProjde_SeznamJePrazdny()
    {
        // Složku nejde založit: na jejím místě v cestě je soubor - týž výsledek jako chybějící právo.
        var soubor = Path.Combine(Path.GetTempPath(), "td-sekce-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(soubor, "");
        try
        {
            var manager = new SectionTemplateManager(Path.Combine(soubor, "section-templates"));

            Assert.Empty(manager.GetAvailableTemplates());
            Assert.Null(manager.Load("cokoli"));
            Assert.ThrowsAny<IOException>(() => manager.Save(new Section { Name = "Hlavička" }, "hlavicka"));
        }
        finally
        {
            File.Delete(soubor);
        }
    }

    [Fact]
    public void SlozkaVznikneAzPriUlozeni()
    {
        var slozka = Path.Combine(Path.GetTempPath(), "td-sekce-" + Guid.NewGuid().ToString("N"));
        try
        {
            var manager = new SectionTemplateManager(slozka);
            Assert.False(Directory.Exists(slozka), "složka nesmí vzniknout jen tím, že se návrhář spustí");

            manager.Save(new Section { Name = "Hlavička" }, "hlavicka");

            Assert.Equal(["hlavicka"], manager.GetAvailableTemplates());
            Assert.NotNull(manager.Load("hlavicka"));
        }
        finally
        {
            if (Directory.Exists(slozka)) Directory.Delete(slozka, recursive: true);
        }
    }
}
