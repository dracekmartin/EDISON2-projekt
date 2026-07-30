using System.Collections.Generic;
using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Schema;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Models;

/// <summary>
/// Barva z dat („pozadí = barva této linky"): hodnota v šabloně není hex, ale odkaz na pole
/// agendy (<c>{BARVA_LINKY}</c>), který se rozloží až daty kreslené stránky. Když linka barvu
/// nastavenou nemá (nebo se kreslí bez dat, třeba v návrháři), použije se bílá - podbarvení tak
/// zůstane čitelné a nevypadá jako chyba.
/// </summary>
public class DataBoundColorTests
{
    private static readonly SKColor White = SKColors.White;

    private static DataContext Data(string? barva) =>
        new(new Dictionary<string, object?> { ["BARVA_LINKY"] = barva ?? "" });

    [Theory]
    [InlineData("{BARVA_LINKY}")]
    [InlineData("  {BARVA_LINKY}  ")]
    public void OdkazNaPole_JeRozpoznan(string value) => Assert.True(ColorHex.IsDataBound(value));

    [Theory]
    [InlineData("#FF3366")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("{}")]
    [InlineData("#FF33{X}")]        // míchanina není odkaz - barva je buď pevná, nebo z dat
    [InlineData("{A}{B}")]
    public void CokoliJinehoNeniOdkaz(string? value) => Assert.False(ColorHex.IsDataBound(value));

    [Fact]
    public void VKresleniSDaty_SePouzijeBarvaZDat()
    {
        using var scope = ColorHex.DataScope(Data("#FF3366CC"));

        Assert.Equal(SKColor.Parse("#FF3366CC"), ColorHex.ParseOrBlack("{BARVA_LINKY}"));
        Assert.Equal(SKColor.Parse("#FF3366CC"), ColorHex.ParseOrTransparent("{BARVA_LINKY}"));
    }

    [Fact]
    public void PrazdnaBarvaLinky_JeBila()
    {
        using var scope = ColorHex.DataScope(Data(""));
        Assert.Equal(White, ColorHex.ParseOrBlack("{BARVA_LINKY}"));
    }

    [Fact]
    public void NesmyslnaBarvaZDat_JeBila()
    {
        using var scope = ColorHex.DataScope(Data("tmavě modrá"));
        Assert.Equal(White, ColorHex.ParseOrBlack("{BARVA_LINKY}"));
    }

    [Fact]
    public void BezDat_JeBila()
    {
        // Návrhář kreslí bez dat, čtvereček v dialogu taky - nesmí spadnout ani ztmavit na černou.
        Assert.Equal(White, ColorHex.ParseOrBlack("{BARVA_LINKY}"));
        Assert.Equal(White, ColorHex.ParseOrTransparent("{BARVA_LINKY}"));
    }

    [Fact]
    public void NeznamePole_JeBila()
    {
        using var scope = ColorHex.DataScope(Data("#FF3366CC"));
        Assert.Equal(White, ColorHex.ParseOrBlack("{BARVA_NECEHO_JINEHO}"));
    }

    [Fact]
    public void DataPlatiJenPoDobuKresleni()
    {
        using (var scope = ColorHex.DataScope(Data("#FF3366CC")))
            Assert.Equal(SKColor.Parse("#FF3366CC"), ColorHex.ParseOrBlack("{BARVA_LINKY}"));

        Assert.Equal(White, ColorHex.ParseOrBlack("{BARVA_LINKY}"));   // po dokreslení už data neplatí
    }

    [Fact]
    public void VnorenyRozsah_ObnoviPredchoziData()
    {
        using var outer = ColorHex.DataScope(Data("#FF111111"));
        using (var inner = ColorHex.DataScope(Data("#FF222222")))
            Assert.Equal(SKColor.Parse("#FF222222"), ColorHex.ParseOrBlack("{BARVA_LINKY}"));

        Assert.Equal(SKColor.Parse("#FF111111"), ColorHex.ParseOrBlack("{BARVA_LINKY}"));
    }

    [Fact]
    public void OdkazJePlatnaHodnotaPole_NehlasiSeJakoChyba()
    {
        // Jinak by kontrola formátu barvy (ticket 13078) označila legitimní hodnotu za neplatnou.
        Assert.True(ColorHex.IsValid("{BARVA_LINKY}"));
    }

    [Fact]
    public void PozadiElementu_VezmeBarvuLinky()
    {
        var element = new TextElement { BackgroundColor = "{BARVA_LINKY}" };

        using var scope = ColorHex.DataScope(Data("#FF3366CC"));
        Assert.Equal(SKColor.Parse("#FF3366CC"), element.SkBackgroundColor);
    }

    [Fact]
    public void OdkazPreziUlozeniAOtevreni()
    {
        var template = new Template { Name = "Barva linky" };
        var section = new Section { Name = "Záhlaví", DesignerHeight = 20f };
        section.Elements.Add(new TextElement { BackgroundColor = "{BARVA_LINKY}" });
        template.Sections.Add(section);

        var reopened = TemplateSerializer.Deserialize(TemplateSerializer.Serialize(template));

        Assert.Equal("{BARVA_LINKY}", reopened.Sections[0].Elements[0].BackgroundColor);
    }

    [Fact]
    public void SchemaCteTypBarva()
    {
        // Wire kontrakt: agenda hlásí barevné pole typem "color" (aditivní, staré typy beze změny).
        const string json = """
            { "fields": [
                { "name": "BARVA_LINKY", "label": "Barva linky", "type": "color" },
                { "name": "POPIS_LINKY", "label": "Popis linky", "type": "text" }
            ] }
            """;

        var schema = CacheAgendaProvider.ParseSchema("ZASTAVKOVY_JR_AD", "Zastávkový JŘ", json);

        Assert.NotNull(schema);
        Assert.Equal(FieldDataType.Color, schema!.Fields[0].DataType);
        Assert.Equal(FieldDataType.Text, schema.Fields[1].DataType);
    }
}
