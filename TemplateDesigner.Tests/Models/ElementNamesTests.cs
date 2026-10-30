using System.IO;
using System.Reflection;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Services;

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

    /// <summary>Paleta a nabídka vkládání jsou to, co uživatel vidí, a hláška se musí trefit do stejného
    /// slova. Od 30. 10. se obě staví z katalogu prvků, ze kterého bere jména i <see cref="ElementNames"/> -
    /// tlačítko prvku vrácené do okna napevno by mělo jméno vlastní a mohlo by se rozejít.</summary>
    [Fact]
    public void PaletaANabidka_SeStaviZKatalogu()
    {
        var okno = Cesta("dotnet/Windows/MainWindow");
        var xaml = File.ReadAllText(Path.Combine(okno, "MainWindow.xaml"));

        Assert.All(ElementCatalog.All, prvek => Assert.DoesNotContain($"Tag=\"{prvek.Type}\"", xaml));
        Assert.Contains("ElementCatalog.All", File.ReadAllText(Path.Combine(okno, "MainWindow.Menus.cs")));
        Assert.Contains("ElementCatalog.All", File.ReadAllText(Path.Combine(okno, "MainWindow.Palette.cs")));
    }

    [Fact]
    public void KatalogProAsistentaBerreJmenaZPalety()
    {
        // Asistent odpovídá o prvku, který má uživatel před sebou - musí ho pojmenovat stejně.
        // Do 21. 9. měl vlastní seznam a po přejmenování 1. 9. v něm zbyla tři stará jména.
        foreach (var typ in TypyPrvku())
            Assert.StartsWith(ElementNames.ForType(typ), CapabilityCatalog.FriendlyName(typ));
    }

    /// <summary>Do kterého JŘ prvek patří, říká asistentovi seznam prvků agend - vlastní výčet tvrdil,
    /// že Poznámky jsou jen zastávkové, i když je od 3. 9. má i linkový JŘ (do 30. 10.).</summary>
    [Fact]
    public void KatalogProAsistenta_RikaDoKterehoJrPrvekPatri()
    {
        Assert.EndsWith("(zastávkový i linkový JŘ)", CapabilityCatalog.FriendlyName(ElementTypes.NotesLegend));
        Assert.EndsWith("(zastávkový JŘ)", CapabilityCatalog.FriendlyName(ElementTypes.StopList));
        Assert.EndsWith("(linkový JŘ)", CapabilityCatalog.FriendlyName(ElementTypes.LineTable));
        Assert.Equal(ElementNames.ForType(ElementTypes.Text), CapabilityCatalog.FriendlyName(ElementTypes.Text));
    }

    [Fact]
    public void KazdyTypMaJmenoIPodleRetezce()
    {
        // ForType je cesta pro místa, kde je po ruce jen typ ze serializace. Vrácený typ
        // = chybějící větev switche.
        foreach (var typ in TypyPrvku())
            Assert.NotEqual(typ, ElementNames.ForType(typ));
    }

    /// <summary>Všechny konstanty <see cref="ElementTypes"/> - nový prvek se sem přidá sám.</summary>
    private static IEnumerable<string> TypyPrvku() =>
        typeof(ElementTypes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!);

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

    [Fact]
    public void OknoVlastnostiSeJmenujeJakoPrvek()
    {
        // Jeden tvar titulku pro všechna okna vlastností prvku, jako záložka „Vlastnosti" (`B194` E2).
        // Do 26. 10. „Vlastnosti textu" vedle „Odjezdy - nastavení" a „Linkový JŘ - nastavení".
        Assert.Equal("Vlastnosti - Minuty odjezdů", ElementNames.TitulekVlastnosti(new DepartureGridElement()));
        Assert.Equal("Vlastnosti - Tabulka spojů", ElementNames.TitulekVlastnosti(new LineTableElement()));
        Assert.Equal("Vlastnosti - Text", ElementNames.TitulekVlastnosti(new TextElement { Text = "Linka" }));
    }

    [Theory]
    [InlineData("DepartureGroupEditorDialog")]
    [InlineData("DynamicTableEditorDialog")]
    [InlineData("LineTableColumnsEditorDialog")]
    [InlineData("StopColumnsEditorDialog")]
    public void OknoVlastnostiNemaTitulekNapevno(string okno)
    {
        // Titulek dosadí okno z ElementNames; napevno v XAML se rozejde s paletou při přejmenování.
        var xaml = File.ReadAllText(Cesta($"dotnet/Windows/{okno}/{okno}.xaml"));
        Assert.DoesNotContain("Title=", xaml);
        var kod = File.ReadAllText(Cesta($"dotnet/Windows/{okno}/{okno}.xaml.cs"));
        Assert.Contains("ElementNames.TitulekVlastnosti(element)", kod);
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
