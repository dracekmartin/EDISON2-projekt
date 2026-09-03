using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Vymazané podbarvení záhlaví znamená „nekreslit nic", ne „vezmi šedou zálohu".
/// </summary>
/// <remarks>
/// Kolega vymazal výchozí šedou u záhlaví poznámek a ta se vrátila (#13158). Šedá v prvku vůbec
/// nebyla - byla zapečená jako záloha kaskády, takže prázdná hodnota znamenala „nic neurčuji".
/// Tyhle testy drží, že prázdno je od té chvíle jednoznačné a že si nový prvek šedou nese zapsanou
/// (jinak by ji migrace neměla proti čemu porovnat a vzhled by se zákazníkům změnil).
/// </remarks>
public class ZahlaviBezPodbarveniTests
{
    [Fact]
    public void NovyPrvekJizdnihoRadu_NeseSedouZapsanou()
    {
        Assert.Equal(ScheduleElementBase.NoveZahlaviPozadi, new NotesLegendElement().HeaderStyle.BackgroundColor);
        Assert.Equal(ScheduleElementBase.NoveZahlaviPozadi, new StopListElement().HeaderStyle.BackgroundColor);
        Assert.Equal(ScheduleElementBase.NoveZahlaviPozadi, new DepartureGridElement().HeaderStyle.BackgroundColor);
        Assert.Equal(ScheduleElementBase.NoveZahlaviPozadi, new LineTableElement().HeaderStyle.BackgroundColor);
    }

    [Fact]
    public void NovaTabulka_NeseSvujOdstinZapsany()
    {
        Assert.Equal(TableElement.NoveZahlaviPozadi, TableElement.CreateGrid(2, 2).HeaderStyle?.BackgroundColor);
        Assert.Equal(DynamicTableElement.NoveZahlaviPozadi, new DynamicTableElement().HeaderStyle?.BackgroundColor);
    }

    [Fact]
    public void KaskadaJizdnihoRadu_NicNedoplniKdyzPrvekPozadiNema()
    {
        // Konec kaskády už podbarvení nedrží - jinak by prázdno nebylo jak vyjádřit.
        Assert.Equal("#00FFFFFF", ScheduleElementBase.ScheduleHeaderDefaults.BackgroundColor);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("#00FFFFFF")]
    public void VymazanePozadi_SeNevykresli(string? vymazano)
    {
        var el = new NotesLegendElement { HeaderStyle = new TextStyle { BackgroundColor = vymazano } };
        var vysledek = el.HeaderStyle.Resolve(ScheduleElementBase.ScheduleHeaderDefaults);
        Assert.Equal(0, ColorHex.ParseOrTransparent(vysledek.BackgroundColor).Alpha);
    }

    [Fact]
    public void ZapsaneSedePozadi_SeVykresli()
    {
        var el = new NotesLegendElement();
        var vysledek = el.HeaderStyle.Resolve(ScheduleElementBase.ScheduleHeaderDefaults);
        var barva = ColorHex.ParseOrTransparent(vysledek.BackgroundColor);
        Assert.Equal(255, barva.Alpha);
        Assert.Equal(SKColor.Parse(ScheduleElementBase.NoveZahlaviPozadi), barva);
    }

    [Fact]
    public void SloupecBezPozadi_DalDediZPrvku()
    {
        // Dědění stylu mezi sloupcem a prvkem se nemění - jinak by každý sloupec musel mít barvu.
        var el = new NotesLegendElement { HeaderStyle = new TextStyle { BackgroundColor = "#FF3366CC" } };
        var rodic = el.HeaderStyle.Resolve(ScheduleElementBase.ScheduleHeaderDefaults);
        var sloupec = new TextStyle { Bold = true }.Resolve(rodic);
        Assert.Equal("#FF3366CC", sloupec.BackgroundColor);
    }
}
