using System;
using System.Collections.Generic;
using System.Linq;
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
        Assert.Same(a, b);   // sdílený singleton - nedisposovat
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

    // ── Šířkové varianty rodin („Arial Narrow" = Arial + Condensed) ──────────────
    // DirectWrite je nehlásí jako rodiny, takže musí do nabídky přijít odvozené a resolve je musí
    // přeložit na rodinu + šířku. Testy nesmí záviset na fontech konkrétního stroje: varianty se
    // hledají v tom, co OS nabízí, a když stroj žádnou vícešířkovou rodinu nemá, nemají co ověřit.

    /// <summary>Rodiny hlášené OS (bez odvozených šířkových variant).</summary>
    private static HashSet<string> OsFamilies() =>
        new(SKFontManager.Default.GetFontFamilies(), StringComparer.OrdinalIgnoreCase);

    /// <summary>Odvozené šířkové varianty v nabídce = co v nabídce je, ale OS to jako rodinu nehlásí.</summary>
    private static List<string> WidthVariants()
    {
        var os = OsFamilies();
        return FontResolver.InstalledFamilies
            .Where(f => !os.Contains(f) && FontResolver.ResolveFamilyAndWidth(f).Width is not null)
            .ToList();
    }

    [Fact]
    public void SirkovaVarianta_JeVNabidcePodleJmenaZOs()
    {
        var variants = WidthVariants();
        if (variants.Count == 0) return;   // stroj bez vícešířkové rodiny - není co ověřovat

        foreach (var variant in variants)
        {
            Assert.True(FontResolver.IsInstalled(variant));

            // Jméno varianty = rodina + slovo o šířce, takže musí začínat jménem své rodiny.
            var (family, _) = FontResolver.ResolveFamilyAndWidth(variant);
            Assert.StartsWith(family, variant, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void SirkovaVarianta_ResolveDaUzkyRezSpravneRodiny()
    {
        var variants = WidthVariants();
        if (variants.Count == 0) return;

        foreach (var variant in variants)
        {
            var (family, width) = FontResolver.ResolveFamilyAndWidth(variant);

            // Váha z nabídky řezů té varianty - kombinace „váha 400 + tato šířka" nemusí existovat
            // (Gill Sans MT má nejužší řez jen tučný) a OS by pak vrátil nejbližší jinou šířku.
            int weight = FontWeightCatalog.Weights(variant).First();
            var typeface = FontResolver.Resolve(variant, new SKFontStyle(
                weight, (int)SKFontStyleWidth.Normal, SKFontStyleSlant.Upright));

            // Podstata opravy: dřív se na tohle jméno kreslil úplně jiný font (fallback na Arial).
            Assert.Equal(family, typeface.FamilyName, ignoreCase: true);
            Assert.Equal(width, typeface.FontStyle.Width);
        }
    }

    [Fact]
    public void SirkovaVarianta_ZachovaVahuASklonZKaskady()
    {
        var variant = WidthVariants().FirstOrDefault();
        if (variant is null) return;

        int weight = FontWeightCatalog.Weights(variant).First();
        var italic = FontResolver.Resolve(variant, new SKFontStyle(
            weight, (int)SKFontStyleWidth.Normal, SKFontStyleSlant.Italic));

        // Šířku dosadí varianta, ne kaskáda (ta posílá vždy normální).
        Assert.NotEqual((int)SKFontStyleWidth.Normal, italic.FontStyle.Width);
        Assert.NotEqual(SKFontStyleSlant.Upright, italic.FontStyle.Slant);   // sklon zůstává z kaskády
    }

    [Fact]
    public void ResolveFamilyAndWidth_BeznaRodinaZustavaBezeZmeny()
    {
        var family = OsFamilies().First();
        var (resolved, width) = FontResolver.ResolveFamilyAndWidth(family);

        Assert.Equal(family, resolved);
        Assert.Null(width);   // běžná rodina = šířku neomezovat (může být úzká sama o sobě)
    }

    [Fact]
    public void ResolveFamilyAndWidth_PrazdneNeboNeznameNeniVarianta()
    {
        Assert.Null(FontResolver.ResolveFamilyAndWidth(null).Width);
        Assert.Null(FontResolver.ResolveFamilyAndWidth("").Width);
        Assert.Null(FontResolver.ResolveFamilyAndWidth("NeexistujiciFontXYZ123").Width);
    }

    /// <summary>Zákazníkův případ (ticket 13006): Arial Narrow je řez rodiny Arial, ne rodina -
    /// ve výběru fontu musí být vidět a kreslit se úzce.</summary>
    [Fact]
    public void ArialNarrow_JeVNabidceAKresliSeUzce()
    {
        using var styles = SKFontManager.Default.GetFontStyles("Arial");
        bool maUzkyRez = Enumerable.Range(0, styles.Count)
            .Any(i => styles[i].Width == (int)SKFontStyleWidth.Condensed);
        if (!maUzkyRez) return;   // stroj bez úzkého Arialu (nebývá na CI)

        Assert.Contains("Arial Narrow", FontResolver.InstalledFamilies);

        var typeface = FontResolver.Resolve("Arial Narrow", SKFontStyle.Normal);
        Assert.Equal("Arial", typeface.FamilyName, ignoreCase: true);
        Assert.Equal((int)SKFontStyleWidth.Condensed, typeface.FontStyle.Width);

        // A hlavně: text se musí reálně vykreslit úzce (ne jen souhlasit metadata).
        using var narrow = new SKFont(typeface, 20f);
        using var normal = new SKFont(FontResolver.Resolve("Arial", SKFontStyle.Normal), 20f);
        Assert.True(narrow.MeasureText("Českobudějovická") < normal.MeasureText("Českobudějovická"));
    }

    [Fact]
    public void SirkovaVarianta_NabidkaRezuJenVahyTeSirky()
    {
        var variant = WidthVariants().FirstOrDefault();
        if (variant is null) return;

        var (family, width) = FontResolver.ResolveFamilyAndWidth(variant);
        using var styles = SKFontManager.Default.GetFontStyles(family);

        var expected = Enumerable.Range(0, styles.Count)
            .Select(i => styles[i])
            .Where(s => s.Slant == SKFontStyleSlant.Upright && s.Width == width!.Value)
            .Select(s => s.Weight)
            .Distinct().OrderBy(w => w).ToList();

        Assert.Equal(expected, FontWeightCatalog.InstalledWeights(variant));
    }
}
