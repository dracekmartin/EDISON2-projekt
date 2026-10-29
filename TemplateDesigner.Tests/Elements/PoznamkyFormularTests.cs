using System.IO;
using System.Runtime.CompilerServices;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Formulář Poznámek nenabízí, co prvek nekreslí, a zapnutí záhlaví má ve skupině Záhlaví jako tabulky
/// (`B194d` E3b, rozhodnutí Jan 28. 10.). Poznámky jinak kostru tabulek nepřebírají - nejsou tabulka.
/// </summary>
public class PoznamkyFormularTests
{
    /// <summary>Pozadí buněk Poznámky nekreslí nikde - pole by nic nedělalo.</summary>
    [Fact]
    public void BarvaPozadiBunek_SeNenabizi()
    {
        Assert.DoesNotContain("DataStyle.BackgroundColor", Formular());
        Assert.DoesNotContain(new NotesLegendElement().GetPropertyDescriptors(), d => d.Label == "Barva pozadí buněk");
    }

    [Fact]
    public void ZapnutiZahlavi_JeVeSkupineZahlavi_BezSkupinyObecne()
    {
        Assert.DoesNotContain("\"general\"", Formular());
        Assert.Equal("Záhlaví", new NotesLegendElement().GetPropertyDescriptors().Single(d => d.Label == "Záhlaví").Group);
    }

    private static string Formular() => File.ReadAllText(Path.Combine(RepoRoot(),
        "TemplateDesigner", "CACHE", "ALVA", "TemplateDesigner", "EDInputForm", "NotesLegendElementEdit.cls"));

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EDISON2.sln"))) dir = dir.Parent;
        dir ??= new FileInfo(ThisFile()).Directory!.Parent!.Parent;
        return dir.FullName;
    }

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
