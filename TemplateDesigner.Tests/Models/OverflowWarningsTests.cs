using TemplateDesigner.Models;

namespace TemplateDesigner.Tests.Models;

/// <summary>
/// Hláška „obsah se nevejde" musí říct, KDE a CO - jinak uživatel ví jen to, že je problém.
/// </summary>
/// <remarks>
/// Do 3. 9. hlásil náhled pevné „Obsah se nevejde na stránku" (podnět kolega 3. 9.): u dávky
/// dvaceti vývěsek se z toho nedalo poznat, kterou stránku prohlédnout ani co na ní zvětšit.
/// </remarks>
public class OverflowWarningsTests
{
    private static PreteceniNalez Sekce(int strana, string sekce, float presah) =>
        new(strana, DruhPreteceni.NastavenaVyskaSekce, sekce, null, presah);

    private static PreteceniNalez Prvek(int strana, string sekce, string prvek, float presah) =>
        new(strana, DruhPreteceni.ObsahPrvku, sekce, prvek, presah);

    [Fact]
    public void NicNepreteka_ZadnaHlaska()
    {
        Assert.Null(OverflowWarnings.Summarize([], 3));
    }

    [Fact]
    public void ObsahPrvku_HlaskaJmenujePrvekSekciIPresah()
    {
        var text = OverflowWarnings.Summarize([Prvek(2, "Obsah", "Poznámky", 12.4f)], 5);

        Assert.NotNull(text);
        Assert.Contains("Poznámky", text);
        Assert.Contains("Obsah", text);
        Assert.Contains("12,4 mm", text);
        Assert.Contains("strana 2", text);
    }

    /// <summary>
    /// Hlásí se jen sekce, která má držet pohromadě, takže obsah narostlý z dat se vejde - jen se
    /// sekce musí smět rozdělit přes stránky. Bez té rady byla hláška slepá ulice: u linkového JŘ
    /// rozvrženého do dvou bloků z ní šlo jen zmenšovat písmo (nález Jan 7. 9.).
    /// </summary>
    [Fact]
    public void ObsahPrvku_HlaskaRadiPovolitRozdeleniSekce()
    {
        var text = OverflowWarnings.Summarize([Prvek(1, "Jízdní řád", "Linkový JŘ", 43.7f)], 2);

        Assert.NotNull(text);
        Assert.Contains("rozdělit přes stránky", text);
    }

    /// <summary>Sekci nafoukla vlastní nastavená výška - rozdělení by nepomohlo, musí se zmenšit.</summary>
    [Fact]
    public void NastavenaVyskaSekce_HlaskaRadiZmensitSekci()
    {
        var text = OverflowWarnings.Summarize([Sekce(1, "Vývěska", 8f)], 1);

        Assert.NotNull(text);
        Assert.DoesNotContain("rozdělit přes stránky", text);
        Assert.Contains("výšku sekce", text);
    }

    [Fact]
    public void NastavenaVyskaSekce_NejmenujePrvek()
    {
        // Sekci nafoukla její nastavená výška, prvky se do ní vejdou - jmenovat prvek by ukázalo
        // na nesprávné místo. Náprava je zmenšit sekci nebo zvětšit stránku.
        var text = OverflowWarnings.Summarize([Sekce(1, "Vývěska", 8f)], 1);

        Assert.NotNull(text);
        Assert.Contains("Vývěska", text);
        Assert.Contains("nevejde na stránku", text);
        Assert.DoesNotContain("nejvíc místa bere", text);
    }

    [Fact]
    public void TenzPrvekNaVsechStrankach_JedenZaznam_NeDvacet()
    {
        // Vývěska má stránku na zastávku, takže tentýž nevejdoucí prvek vypadne na každé z nich.
        // Výpis po stránkách by z jedné chyby udělal dvacet řádků ve stavovém řádku.
        var nalezy = Enumerable.Range(1, 20)
            .Select(i => Prvek(i, "Obsah", "Poznámky", 5f + i))
            .ToList();

        var text = OverflowWarnings.Summarize(nalezy, 20);

        Assert.NotNull(text);
        Assert.DoesNotContain(";", text);              // jediná část, ne dvacet
        Assert.Contains("20 z 20 stránek", text);
        Assert.Contains("první je 1", text);
        Assert.Contains("25 mm", text);                // největší přesah, ne první nalezený
    }

    [Fact]
    public void MaloStranek_VypisujiSeCisla()
    {
        var text = OverflowWarnings.Summarize(
            [Prvek(2, "Obsah", "Poznámky", 3f), Prvek(5, "Obsah", "Poznámky", 4f)], 8);

        Assert.NotNull(text);
        Assert.Contains("strany 2, 5", text);
    }

    [Fact]
    public void VicVinniku_NejhorsiJdePrvni()
    {
        // Uživatel opraví to, co je nejvíc vidět; obráceně by musel číst do konce řádku.
        var text = OverflowWarnings.Summarize(
            [Prvek(1, "Obsah", "Text", 2f), Prvek(1, "Obsah", "Seznam zastávek", 30f)], 1);

        Assert.NotNull(text);
        Assert.True(text.IndexOf("Seznam zastávek", StringComparison.Ordinal)
                  < text.IndexOf("Text", StringComparison.Ordinal),
            $"nejhorší přesah má být první, je: {text}");
    }

    [Fact]
    public void HlaskaNeniObecna_ObsahujeCislaAJmena()
    {
        // Pojistka proti návratu k pevnému textu: kdo hlášku vymění za obecnou, spadne tady.
        var text = OverflowWarnings.Summarize([Prvek(3, "Hlavička", "Obrázek", 6f)], 4);

        Assert.NotNull(text);
        Assert.NotEqual("Obsah se nevejde na stránku", text);
        Assert.Contains("Obrázek", text);
        Assert.Contains("3", text);
    }
}
