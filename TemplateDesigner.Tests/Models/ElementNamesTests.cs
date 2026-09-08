using System.IO;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Models;

/// <summary>
/// Jméno prvku v hlášce musí být totéž, jaké uživatel vidí v paletě - jinak neví, o co jde.
/// </summary>
/// <remarks>
/// Jména byla do 3. 9. opsaná na třech místech (hláška o zmenšeném písmu, import z PDF, paleta).
/// Přejmenování dvou prvků 1. 9. („Mřížka odjezdů" → „Minuty odjezdů", „Legenda poznámek" →
/// „Poznámky") se tím muselo dělat trojmo a v příručce zbyly staré názvy ještě 2. 9.
/// </remarks>
public class ElementNamesTests
{
    [Fact]
    public void KazdyTypPrvkuMaJmeno()
    {
        // Obecné „prvek" je záloha pro neznámý typ, ne odpověď pro ty, které v aplikaci jsou.
        TemplateElement[] prvky =
        [
            new TextElement(), new RectangleElement(), new LineElement(), new ImageElement(),
            new BarcodeElement(), new TableElement(), new DynamicTableElement(),
            new StopListElement(), new DepartureGridElement(), new NotesLegendElement(),
            new LineTableElement(),
        ];

        foreach (var el in prvky)
            Assert.NotEqual("prvek", ElementNames.ForUser(el));
    }

    [Theory]
    [InlineData("Minuty odjezdů")]
    [InlineData("Poznámky")]
    [InlineData("Seznam zastávek")]
    [InlineData("Tabulka (pevná)")]
    [InlineData("Tabulka (z dat)")]
    [InlineData("Obdélník")]
    [InlineData("Obrázek")]
    public void JmenoSedisPaletou(string jmeno)
    {
        // Paleta a nabídka vkládání jsou to, co uživatel vidí; hláška se musí trefit do stejného
        // slova. ⚠️ Přejmenovat prvek znamená změnit obojí - jinak spadne tady.
        var paleta = File.ReadAllText(Cesta("dotnet/Windows/MainWindow/MainWindow.xaml.cs"))
                   + File.ReadAllText(Cesta("dotnet/Windows/MainWindow/MainWindow.xaml"));

        Assert.Contains(jmeno, paleta);
    }

    [Fact]
    public void StareNazvyPrvkuJsouPryc()
    {
        // Přejmenování z 1. 9.: dva prvky mají nová jména a stará se nesmí vrátit ani do hlášek.
        TemplateElement[] prvky = [new DepartureGridElement(), new NotesLegendElement()];

        foreach (var el in prvky)
        {
            var jmeno = ElementNames.ForUser(el);
            Assert.DoesNotContain("Mřížka", jmeno);
            Assert.DoesNotContain("Legenda", jmeno);
        }
    }

    /// <summary>Cesta ke zdroji v repu - test čte jen obsah repa, ne stav stroje.</summary>
    private static string Cesta(string relativni)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "TemplateDesigner")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "TemplateDesigner", relativni);
    }
}
