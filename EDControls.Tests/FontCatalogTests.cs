using EDControls.UserFonts;

namespace EDControls.Tests;

public class FontCatalogTests
{
    private static readonly string[] TypicalWindows =
    [
        "Arial", "Calibri", "Segoe UI", "Tahoma", "Verdana", "Wingdings", "Marlett", "Webdings",
    ];

    [Fact]
    public void Families_KeepsOnlyWhitelistedInstalled()
    {
        var families = FontCatalog.Families(TypicalWindows, current: null);

        Assert.DoesNotContain("Wingdings", families);
        Assert.DoesNotContain("Marlett", families);
        Assert.DoesNotContain("Webdings", families);
        Assert.Contains("Calibri", families);
    }

    [Fact]
    public void Families_PutsDefaultFirst()
    {
        // Pořadí whitelistu, ne pořadí enumerace systému — výchozí musí být první volba.
        Assert.Equal(FontCatalog.DefaultFamily, FontCatalog.Families(TypicalWindows, current: null)[0]);
    }

    [Fact]
    public void Families_SkipsWhitelistedButMissingFont()
    {
        var families = FontCatalog.Families(["Segoe UI", "Arial"], current: null);

        Assert.DoesNotContain("Calibri", families);
    }

    [Fact]
    public void Families_KeepsCurrentOutsideWhitelist()
    {
        // Starší font.json / ruční edit — combo musí ukazovat, co je opravdu nastavené.
        var families = FontCatalog.Families(TypicalWindows, current: "Wingdings");

        Assert.Contains("Wingdings", families);
        Assert.Equal(FontCatalog.DefaultFamily, families[0]);
    }

    [Fact]
    public void Families_DoesNotDuplicateCurrentFromWhitelist()
    {
        var families = FontCatalog.Families(TypicalWindows, current: "calibri");

        Assert.Single(families, f => string.Equals(f, "Calibri", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Families_FallsBackToDefaultWhenNothingInstalled()
    {
        Assert.Equal([FontCatalog.DefaultFamily], FontCatalog.Families([], current: null));
    }

    [Fact]
    public void SizesPt_ContainsDefaultOnceAndIsSorted()
    {
        var sizes = FontCatalog.SizesPt(10.5);

        Assert.Single(sizes, s => s == 10.5);
        Assert.Equal(sizes.OrderBy(s => s), sizes);
    }

    [Fact]
    public void SizesPt_DoesNotDuplicateDefaultAlreadyOffered()
    {
        var sizes = FontCatalog.SizesPt(12);

        Assert.Single(sizes, s => s == 12);
    }
}
