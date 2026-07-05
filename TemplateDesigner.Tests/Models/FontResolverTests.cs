using SkiaSharp;
using TemplateDesigner.Models;

namespace TemplateDesigner.Tests.Models;

/// <summary>
/// Guard pro FontResolver po zavedení embedded fontů: resolve vždy vrátí ne-null sdílený typeface,
/// nenainstalovaná/prázdná rodina padne na fallback, opakovaný resolve vrací tutéž (cachovanou) instanci.
/// Embedded-specifická cesta se ověřuje ručně po přidání fontu do Fonts/ (bez fontu = OS chování).
/// </summary>
public class FontResolverTests
{
    [Fact]
    public void Resolve_VzdyVratiNeNull()
    {
        Assert.NotNull(FontResolver.Resolve(null, SKFontStyle.Normal));
        Assert.NotNull(FontResolver.Resolve("", SKFontStyle.Normal));
        Assert.NotNull(FontResolver.Resolve("Arial", SKFontStyle.Bold));
        Assert.NotNull(FontResolver.Resolve("NeexistujiciFontXYZ123", SKFontStyle.Normal));
    }

    [Fact]
    public void Resolve_JeCachovany_StejnaInstanceProStejnyKlic()
    {
        var a = FontResolver.Resolve("Arial", SKFontStyle.Normal);
        var b = FontResolver.Resolve("Arial", SKFontStyle.Normal);
        Assert.Same(a, b);   // sdílený singleton — nedisposovat
    }

    [Fact]
    public void IsInstalled_PrazdneFalse()
    {
        Assert.False(FontResolver.IsInstalled(null));
        Assert.False(FontResolver.IsInstalled("   "));
    }

    [Fact]
    public void InstalledFamilies_NeniNull() =>
        Assert.NotNull(FontResolver.InstalledFamilies);
}
