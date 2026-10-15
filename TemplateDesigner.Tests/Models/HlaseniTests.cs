using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Rendering;

namespace TemplateDesigner.Tests.Models;

/// <summary>
/// `B178`: hlášky vpravo dole mají závažnost podle následku na papíře a nesou prvek, ke kterému
/// patří - klik na řádek seznamu ho vybere.
/// </summary>
public class HlaseniTests
{
    private static Template Sablona(TemplateElement prvek) => new()
    {
        Sections = [new Section { Name = "Obsah", DesignerHeight = 50, Elements = [prvek] }],
    };

    private static readonly DataContext PrazdnaData = new(new Dictionary<string, object?>());

    /// <summary>Zmenšení písma si zákazníci sami vyžádali - je to informace, ne vada.</summary>
    [Fact]
    public void ZmensenePismo_JeInformaceSPrvkem()
    {
        var text = new TextElement
        {
            X = 0, Y = 0, Width = 40, Height = 6, WordWrap = true,
            Text = string.Join(" ", Enumerable.Repeat("slovo", 12)),
            Style = new TextStyle { FontFamily = "Arial", FontSize = 12f },
        };

        var h = Assert.Single(ContentShrinkWarnings.Seznam(Sablona(text), [PrazdnaData]));

        Assert.Equal(ZavaznostHlaseni.Informace, h.Zavaznost);
        Assert.Equal(text.Id, h.PrvekId);
    }

    /// <summary>Když se text nevejde ani po zmenšení, na papíře chybí - chyba.</summary>
    [Fact]
    public void OriznutyText_JeChyba()
    {
        var text = new TextElement
        {
            X = 0, Y = 0, Width = 40, Height = 6, WordWrap = true,
            Text = string.Join(" ", Enumerable.Repeat("dlouhé slovo", 200)),
            Style = new TextStyle { FontFamily = "Arial", FontSize = 20f },
        };

        var h = Assert.Single(ContentShrinkWarnings.Seznam(Sablona(text), [PrazdnaData]));

        Assert.Equal(ZavaznostHlaseni.Chyba, h.Zavaznost);
    }

    [Fact]
    public void MalyKod_JeUpozorneniSPrvkem()
    {
        var kod = new BarcodeElement
        {
            X = 0, Y = 0, Width = 5, Height = 5, BarcodeType = BarcodeElementType.QR,
            Content = "https://example.com/jizdni-rad?linka=330530&zastavka=42",
        };

        var h = Assert.Single(BarcodeSizeWarnings.Seznam(Sablona(kod), [PrazdnaData]));

        Assert.Equal(ZavaznostHlaseni.Upozorneni, h.Zavaznost);
        Assert.Equal(kod.Id, h.PrvekId);
    }

    [Fact]
    public void OriznuteBunky_NesouPrvek()
    {
        var id = Guid.NewGuid();
        var log = new ClippedCells { CurrentElement = "Seznam zastávek", CurrentElementId = id };
        log.Report("Kroměříž");

        var h = Assert.Single(log.Seznam());
        Assert.Equal(ZavaznostHlaseni.Chyba, h.Zavaznost);
        Assert.Equal(id, h.PrvekId);
    }

    [Fact]
    public void Preteceni_NeseViniciPrvek()
    {
        var id = Guid.NewGuid();
        var nalez = new PreteceniNalez(2, DruhPreteceni.ObsahPrvku, "Poznámky", "Poznámky", 4f, id);

        var h = Assert.Single(OverflowWarnings.Seznam([nalez], 3));
        Assert.Equal(ZavaznostHlaseni.Chyba, h.Zavaznost);
        Assert.Equal(id, h.PrvekId);
    }
}

/// <summary>Textový prvek se v hláškách pozná podle obsahu - „Text" samo je nerozliší.</summary>
public class ElementNamesSPopisemTests
{
    private static string Popis(string text) =>
        ElementNames.SPopisem(new TemplateDesigner.Models.Elements.TextElement { Text = text });

    [Theory]
    [InlineData("{POPIS_DOPRAVCE}", "Text „{POPIS_DOPRAVCE}“")]
    [InlineData("Platí od {DATUM_PLATNOSTI_OD}", "Text „Platí od {DATUM_PLATNOSTI_OD}“")]
    [InlineData("Přepravu zajišťuje :   {POPIS_DOPRAVCE} a další text", "Text „Přepravu zajišťuje : {POPIS_DOPRAVCE}…“")]
    public void TextPrvku_KratkyCely_DlouhyTriSlova(string text, string ocekavano)
    {
        Assert.Equal(ocekavano, Popis(text));
    }

    [Fact]
    public void JinyPrvek_JenNazev()
    {
        Assert.Equal("Obrázek", ElementNames.SPopisem(new TemplateDesigner.Models.Elements.ImageElement()));
    }
}
