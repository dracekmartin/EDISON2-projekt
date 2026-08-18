using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Models;

/// <summary>
/// Upozornění na font, který šablona chce a počítač ho nemá. Bez něj se tiše kreslí Arial,
/// takže výstup vypadá v pořádku a jen je jiný, než autor zamýšlel - a u kolegy jiný než u autora.
///
/// <para>Testy schválně sahají na font zanořený hluboko (styl sloupce, styl buňky), ne jen na styl
/// prvku: detekce má být rot-proof vůči nové stylové vrstvě, protože ruční výčet míst už jednou
/// v tomhle projektu zestárl.</para>
/// </summary>
public class FontWarningsTests
{
    /// <summary>Jméno, které na žádném stroji nebude - test nesmí záviset na tom, co je kde nainstalované.</summary>
    private const string NeexistujiciFont = "Zzz Neexistujici Font 12345";

    private static Template Sablona(params TemplateElement[] elements)
    {
        var section = new Section { Name = "S" };
        foreach (var e in elements) section.Elements.Add(e);
        var t = new Template { Name = "T" };
        t.Sections.Add(section);
        return t;
    }

    [Fact]
    public void CistaSablona_Nevaruje()
    {
        var t = Sablona(new TextElement { Text = "ahoj" });
        Assert.Empty(FontWarnings.MissingFamilies(t));
        Assert.Null(FontWarnings.Summarize(t));
    }

    [Fact]
    public void NainstalovanyFont_Nevaruje()
    {
        var t = Sablona(new TextElement { Text = "ahoj", Style = new TextStyle { FontFamily = "Arial" } });
        Assert.Empty(FontWarnings.MissingFamilies(t));
    }

    [Fact]
    public void ChybejiciFontVeStyluPrvku_Varuje()
    {
        var t = Sablona(new TextElement { Text = "ahoj", Style = new TextStyle { FontFamily = NeexistujiciFont } });

        Assert.Equal([NeexistujiciFont], FontWarnings.MissingFamilies(t));
        var hlaska = FontWarnings.Summarize(t);
        Assert.NotNull(hlaska);
        Assert.Contains(NeexistujiciFont, hlaska);
        Assert.Contains("Arial", hlaska);   // uživateli se musí říct, co se stane místo toho
    }

    /// <summary>
    /// Jádro věci: font zanořený ve stylu SLOUPCE, ne prvku. Ruční výčet stylových míst by tohle
    /// vynechal - a přesně tahle třída zapomenutí je důvod, proč se hledá průchodem celou šablonou.
    /// </summary>
    [Fact]
    public void ChybejiciFontVeStyluSloupce_Varuje()
    {
        var tabulka = new DynamicTableElement();
        tabulka.Columns.Add(new TableColumn
        {
            Header = "Sloupec",
            DataStyle = new TextStyle { FontFamily = NeexistujiciFont }
        });

        Assert.Equal([NeexistujiciFont], FontWarnings.MissingFamilies(Sablona(tabulka)));
    }

    [Fact]
    public void ChybejiciFontVeVychozimStyluDokumentu_Varuje()
    {
        var t = Sablona(new TextElement { Text = "ahoj" });
        t.DefaultTextStyle = new TextStyle { FontFamily = NeexistujiciFont };

        Assert.Equal([NeexistujiciFont], FontWarnings.MissingFamilies(t));
    }

    [Fact]
    public void TentyzFontNaVicMistech_JeVHlasceJenJednou()
    {
        var t = Sablona(
            new TextElement { Text = "a", Style = new TextStyle { FontFamily = NeexistujiciFont } },
            new TextElement { Text = "b", Style = new TextStyle { FontFamily = NeexistujiciFont } });

        Assert.Single(FontWarnings.MissingFamilies(t));
    }

    [Fact]
    public void ZadnaSablona_Nevaruje()
    {
        Assert.Empty(FontWarnings.MissingFamilies(null));
        Assert.Null(FontWarnings.Summarize(null));
    }
}

/// <summary>
/// Že se varování vůbec spustí, drží jediný hák: setter <c>DesignerState.CurrentTemplate</c>.
/// Kdyby ho někdo obešel (nebo hák odstranil), detekce sice dál funguje, ale nikdo ji nezavolá -
/// a to je přesně ten druh regrese, který není vidět.
/// </summary>
public class TemplateSwapHookTests
{
    [Fact]
    public void VymenaSablony_OhlasiSe()
    {
        var state = new TemplateDesigner.Designer.DesignerState();
        int volani = 0;
        state.TemplateSwapped = () => volani++;

        state.CurrentTemplate = new Template { Name = "A" };
        state.CurrentTemplate = new Template { Name = "B" };

        Assert.Equal(2, volani);
    }

    [Fact]
    public void BezOdberatele_VymenaNespadne()
    {
        var state = new TemplateDesigner.Designer.DesignerState();
        state.CurrentTemplate = new Template { Name = "A" };   // TemplateSwapped je null
    }
}
