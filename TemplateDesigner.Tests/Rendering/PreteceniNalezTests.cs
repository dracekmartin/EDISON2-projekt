using TemplateDesigner.Designer;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Layout;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Náhled musí u přetečení vědět, co a kde - ne jen že něco přetéká.
/// </summary>
/// <remarks>
/// Informace v náhledu existovala už dřív (přetečení se počítá pro každou stránku zvlášť), ale
/// zahazovala se do jediného <c>bool</c>. Tenhle test drží, že se dostane až do hlášky, včetně
/// názvu sekce a prvku (podnět kolega 3. 9.).
///
/// <para>⚠️ Přetečení vzniká **jen** tak, že sekce přesáhne stránku - prvek svou sekci přesáhnout
/// nemůže, engine jí dává <c>max(nastavená výška, obsah)</c>. Testy proto staví sekce vyšší než
/// tisková plocha, ne prvky vyšší než sekce.</para>
/// </remarks>
public class PreteceniNalezTests
{
    /// <summary>A5 na výšku: tisková plocha ~190 mm (210 mm minus okraje).</summary>
    private static DesignerState Stav(Section sekce) => new()
    {
        CurrentTemplate = new Template
        {
            Name = "test",
            PageSize = new PageSize { Width = 148, Height = 210 },
            Sections = [sekce],
        }
    };

    /// <summary>Popis, který by dostal stavový řádek.</summary>
    private static string? Popis(DesignerState stav)
    {
        var canvas = new PreviewCanvas(stav);
        var (nalezy, celkem) = canvas.CountOverflowingPages([new DataContext(new Dictionary<string, object?>())]);
        return OverflowWarnings.Summarize(nalezy, celkem);
    }

    [Fact]
    public void PrvekNafoukSekciZaStranku_HlaskaJmenujePrvekISekci()
    {
        // Sekce má nastavených 40 mm, ale prvek sahá do 300 mm → engine sekci nafoukne na 300
        // a ta přesáhne stránku. Vinníkem je prvek, ne nastavení sekce.
        var stav = Stav(new Section
        {
            Name = "Vývěska",
            DesignerHeight = 40,
            OverflowBehavior = SectionOverflowBehavior.KeepTogether,
            Elements = [new TextElement { X = 5, Y = 0, Width = 100, Height = 300, Text = "dlouhý text" }],
        });

        var popis = Popis(stav);

        Assert.NotNull(popis);
        Assert.Contains("Text", popis);             // jméno prvku z palety
        Assert.Contains("Vývěska", popis);          // jméno sekce, jak si ji uživatel pojmenoval
        Assert.Contains("nejvíc místa bere", popis);
        Assert.Contains("strana 1", popis);
    }

    [Fact]
    public void VejdeSe_ZadnaHlaska()
    {
        var stav = Stav(new Section
        {
            Name = "Vývěska",
            DesignerHeight = 100,
            OverflowBehavior = SectionOverflowBehavior.KeepTogether,
            Elements = [new TextElement { X = 5, Y = 5, Width = 100, Height = 10, Text = "krátký" }],
        });

        Assert.Null(Popis(stav));
    }

    [Fact]
    public void SekceKterouLzeRozdelit_Nehlasi()
    {
        // Sekci, kterou engine rozdělí přes stránky, se obsah neztratí - hlásit ji by znamenalo
        // varovat před něčím, co je v pořádku.
        var stav = Stav(new Section
        {
            Name = "Vývěska",
            DesignerHeight = 40,
            OverflowBehavior = SectionOverflowBehavior.SplitAcrossPages,
            Elements = [new TextElement { X = 5, Y = 0, Width = 100, Height = 300, Text = "dlouhý text" }],
        });

        Assert.Null(Popis(stav));
    }

    [Fact]
    public void NastavenaVyskaSekce_HlaskaPrvekNejmenuje()
    {
        // Sekci nafoukla nastavená výška, prvek se do ní vejde - ukazovat na prvek by poslalo
        // uživatele opravovat něco, co za to nemůže.
        var stav = Stav(new Section
        {
            Name = "Vývěska",
            DesignerHeight = 400,
            OverflowBehavior = SectionOverflowBehavior.KeepTogether,
            Elements = [new TextElement { X = 5, Y = 5, Width = 100, Height = 10, Text = "krátký" }],
        });

        var popis = Popis(stav);

        Assert.NotNull(popis);
        Assert.Contains("nevejde na stránku", popis);
        Assert.Contains("Vývěska", popis);
        Assert.DoesNotContain("nejvíc místa bere", popis);
    }

    [Fact]
    public void DvaPrvky_JmenujeSeTenSahajiciNejdal()
    {
        // Jmenuje se jeden vinník; pořadí prvků v sekci je náhodné vodítko, takže se musí vybrat
        // ten, který sahá nejdál dolů - jen jeho zmenšení sekci reálně sníží.
        var stav = Stav(new Section
        {
            Name = "Vývěska",
            DesignerHeight = 40,
            OverflowBehavior = SectionOverflowBehavior.KeepTogether,
            Elements =
            [
                new TextElement      { X = 5,  Y = 0, Width = 50, Height = 100, Text = "mírný" },
                new RectangleElement { X = 60, Y = 0, Width = 50, Height = 300 },
            ],
        });

        var popis = Popis(stav);

        Assert.NotNull(popis);
        Assert.Contains("Obdélník", popis);
        Assert.DoesNotContain("Text", popis);
    }

    [Fact]
    public void HlaskaNeniObecna()
    {
        // Pojistka proti návratu k pevnému textu z doby před 3. 9.
        var stav = Stav(new Section
        {
            Name = "Vývěska",
            DesignerHeight = 400,
            OverflowBehavior = SectionOverflowBehavior.KeepTogether,
            Elements = [new TextElement { X = 5, Y = 5, Width = 100, Height = 10, Text = "krátký" }],
        });

        var popis = Popis(stav);

        Assert.NotNull(popis);
        Assert.NotEqual("Obsah se nevejde na stránku", popis);
        Assert.Contains("mm", popis);       // konkrétní přesah, ne obecné varování
    }
}
