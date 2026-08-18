using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.PdfImport;
using TemplateDesigner.Services;

namespace TemplateDesigner.Tests.PdfImport;

/// <summary>
/// Sloupce seznamu zastávek, které vzniknou importem z předlohy. Import měl vlastní popisky
/// a vlastní klíč pro tarifní číslo, který v datech neexistuje - sloupec pak zabíral šířku
/// a při tisku zůstal prázdný.
/// </summary>
public class ImportedStopColumnsTests
{
    private static AiVisionElement StopList(params string[] columns) => new()
    {
        Type = "StopList",
        Columns = [.. columns],
        HasHeader = true
    };

    private static List<StopColumn> Build(params string[] columns) =>
        AiGuidedScheduleBuilder.BuildStopColumnsFromAi(StopList(columns), 60f);

    /// <summary>Klíče, které <see cref="StopListElement.GetCellText"/> umí naplnit daty.</summary>
    private static readonly HashSet<string> RenderableKeys =
    [
        StopFields.StopName, StopFields.TravelTime, StopFields.Poradi, StopFields.JeAktualni,
        StopFields.Zone, StopFields.Nastupiste, StopFields.Sloupek, StopFields.SloupekPopis,
        StopFields.TravelTimeVariants
    ];

    [Fact]
    public void KazdySloupecZImportuMaDataVModelu()
    {
        var columns = Build("TravelTime", "TariffNumber", "Platform", "StopName", "Zone");

        Assert.All(columns, c => Assert.Contains(c.Key, RenderableKeys));
    }

    [Fact]
    public void TarifniCisloSeMapujeNaPoradi()
    {
        var columns = Build("TravelTime", "TariffNumber", "StopName");

        Assert.Contains(columns, c => c.Key == StopFields.Poradi);
        Assert.DoesNotContain(columns, c => c.Key == "TariffNumber");
    }

    [Theory]
    [InlineData("TravelTime")]
    [InlineData("StopName")]
    [InlineData("Platform")]
    [InlineData("Zone")]
    public void PopiskySeShodujiSVychozimiSloupciPrvku(string aiColumn)
    {
        var defaults = StopListElement.GetDefaultColumns();
        var columns = Build("TravelTime", aiColumn, "StopName");

        foreach (var col in columns)
        {
            var match = defaults.FirstOrDefault(d => d.Key == col.Key);
            if (match is not null)
                Assert.Equal(match.Header, col.Header);
        }
    }

    [Fact]
    public void SloupecVariantJizdnichDobVznikneZaSloupcemCasu()
    {
        var columns = Build("TravelTime", "StopName");

        int timeIdx = columns.FindIndex(c => c.Key == StopFields.TravelTime);
        int variantIdx = columns.FindIndex(c => c.Key == StopFields.TravelTimeVariants);

        Assert.True(timeIdx >= 0);
        Assert.Equal(timeIdx + 1, variantIdx);
    }

    /// <summary>Bez sloupce času nemá varianta k čemu patřit - nesmí se vnutit sama.</summary>
    [Fact]
    public void BezSloupceCasuVariantyNevzniknou()
    {
        var columns = Build("StopName");

        Assert.DoesNotContain(columns, c => c.Key == StopFields.TravelTimeVariants);
    }

    [Fact]
    public void VlozeniVariantJeIdempotentni()
    {
        var columns = Build("TravelTime", "StopName");
        StopColumnHeaders.InsertVariantColumn(columns);

        Assert.Single(columns, c => c.Key == StopFields.TravelTimeVariants);
    }
}
