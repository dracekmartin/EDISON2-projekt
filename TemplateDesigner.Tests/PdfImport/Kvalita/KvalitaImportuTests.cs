using System.IO;
using TemplateDesigner.Designer;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.PdfImport;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.PdfImport.Kvalita;

/// <summary>
/// Měření kvality importu samo - bez modelu. Šablona porovnaná s očekáváním ze sebe samé musí projít;
/// kdyby neprošla, měření by hlásilo chyby importu, které nejsou.
/// </summary>
public class KvalitaImportuTests
{
    public static TheoryData<string> Sablony => new()
    {
        "TemplateDesigner/dotnet/SystemTemplates/linkovy-jr-ad-postskript-a5.json",
        "TemplateDesigner/dotnet/SystemTemplates/linkovy-jr-ad-poznamky-vedle-a4.json",
        "TemplateDesigner/templates/vzor-940-idzk.json",
    };

    private static Template Nacti(string soubor) =>
        TemplateSerializer.Deserialize(File.ReadAllText(Path.Combine(RepoRoot(), soubor)))!;

    private static ExtractedPage Vytiskni(Template t, DataContext data)
    {
        var pdf = Path.Combine(Path.GetTempPath(), $"td-kvalita-{Guid.NewGuid():N}.pdf");
        try
        {
            new PreviewCanvas(new DesignerState { CurrentTemplate = t }).RenderMultiDataToPdf(pdf, [data]);
            using var doc = UglyToad.PdfPig.PdfDocument.Open(pdf);
            return PdfPageExtractor.Extract(doc, 1);
        }
        finally { File.Delete(pdf); }
    }

    /// <summary>Poloha textu a šířka tabulky se tu nekontrolují: zdroj má texty zarovnané v prvku
    /// vlevo, import je vystředí (porovnává se střed napsaného textu se středem prvku), a tabulka
    /// se kreslí užší než prvek (porovnává se šířka na papíře).</summary>
    [Theory]
    [MemberData(nameof(Sablony))]
    public void SablonaProtiSobeProjdeKontrolami(string soubor)
    {
        var sablona = Nacti(soubor);
        var data = KvalitaImportuData.Linka();
        var o = KvalitaImportu.ZeSablony(sablona, data, Vytiskni(sablona, data), soubor);

        var h = KvalitaImportu.Vyhodnot(sablona, o);

        Assert.True(o.Texty.Count >= 4, $"očekávání má jen {o.Texty.Count} texty");
        Assert.All(h.Kontroly.Where(k => !k.Nazev.EndsWith("na místě") && !(k.Oblast == "Tabulka" && k.Nazev == "šířka")),
            k => Assert.True(k.Ok, $"{k.Oblast}: {k.Nazev} - {k.Detail}"));
    }

    /// <summary>Středy textů z výtisku - text se slovy uvnitř prvku, nevytištěný se vynechá.</summary>
    [Fact]
    public void StredTextuSeBereZVytisku()
    {
        var sablona = Nacti("TemplateDesigner/dotnet/SystemTemplates/linkovy-jr-ad-postskript-a5.json");
        var data = KvalitaImportuData.Linka();
        var o = KvalitaImportu.ZeSablony(sablona, data, Vytiskni(sablona, data), "a5");

        var trasa = Assert.Single(o.Texty, t => t.Obsah == "{POPIS_LINKY}");
        var prvek = sablona.Sections.SelectMany(s => s.Elements).OfType<TextElement>().Single(t => t.Text == "{POPIS_LINKY}");
        Assert.InRange(trasa.X, sablona.PageSize.MarginLeft + prvek.X, sablona.PageSize.MarginLeft + prvek.X + prvek.Width);
    }

    [Fact]
    public void PosunutyTextAZameneneSloupceSeOdhali()
    {
        const string soubor = "TemplateDesigner/dotnet/SystemTemplates/linkovy-jr-ad-postskript-a5.json";
        var zdroj = Nacti(soubor);
        var data = KvalitaImportuData.Linka();
        var o = KvalitaImportu.ZeSablony(zdroj, data, Vytiskni(zdroj, data), "a5");
        var trasa = o.Texty.Single(t => t.Obsah == "{POPIS_LINKY}");

        // Výsledek „importu": trasa jako vystředěný prvek přesně na místě, pak o 6 mm vedle.
        var vysledek = Nacti(soubor);
        var text = vysledek.Sections.SelectMany(s => s.Elements).OfType<TextElement>().Single(t => t.Text == "{POPIS_LINKY}");
        float h = text.Height ?? text.EffectiveDesignerHeight;
        text.X = trasa.X - vysledek.PageSize.MarginLeft - text.Width / 2;
        text.Y = trasa.Y - vysledek.PageSize.MarginTop - h / 2;
        Assert.Contains(KvalitaImportu.Vyhodnot(vysledek, o).Kontroly, k => k.Ok && k.Nazev.Contains("Kaplice - Velešín") && k.Nazev.EndsWith("na místě"));

        text.X += 6;
        var tabulka = vysledek.Sections.SelectMany(s => s.Elements).OfType<LineTableElement>().Single();
        var viditelne = tabulka.Columns.Where(c => c.Visible && o.Tabulka!.Sloupce.Contains(c.Key)).ToList();
        int a = tabulka.Columns.IndexOf(viditelne[0]), b = tabulka.Columns.IndexOf(viditelne[1]);
        (tabulka.Columns[a], tabulka.Columns[b]) = (tabulka.Columns[b], tabulka.Columns[a]);

        var hodnoceni = KvalitaImportu.Vyhodnot(vysledek, o);

        Assert.Contains(hodnoceni.Kontroly, k => !k.Ok && k.Nazev.Contains("Kaplice - Velešín") && k.Nazev.EndsWith("na místě"));
        Assert.Contains(hodnoceni.Kontroly, k => !k.Ok && k.Nazev == "pořadí sloupců");
    }

    /// <summary>Ručně zapsaná očekávání skutečných předloh se musí dát načíst.</summary>
    [Fact]
    public void OcekavaniSkutecnychPredlohSeNactou()
    {
        var soubory = Directory.GetFiles(Path.Combine(RepoRoot(), "TemplateDesigner.Tests", "PdfImport", "Kvalita", "sada"),
            "*.ocekavani.json");

        Assert.NotEmpty(soubory);
        Assert.All(soubory, f => Assert.NotNull(KvalitaImportu.Nacti(f).Tabulka));
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EDISON2.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Kořen repa (EDISON2.sln) nenalezen.");
    }
}
