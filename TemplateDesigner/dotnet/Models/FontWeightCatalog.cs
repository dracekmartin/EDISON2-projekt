using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

namespace TemplateDesigner.Models;

/// <summary>
/// Jediný zdroj pravdy „jaké řezy (váhy) rodina reálně má" + názvosloví vah. Sdílené oběma
/// editačními cestami fontu: EDInputForm klient (<c>ElementFormBinder.PopulateFontWeightFields</c>)
/// i WPF <c>CellStyleEditor</c>. Váhy se čtou z nainstalovaného fontu přes <see cref="SKFontManager"/>
/// (OS věc - Caché je nezná), takže se nabídnou jen skutečné řezy (ne fixní seznam). U šířkových
/// variant rodiny („Arial Narrow") se filtruje na jejich šířku - viz <see cref="FontResolver"/>.
/// </summary>
public static class FontWeightCatalog
{
    // Standardní názvy vah (číslo → label). Neznámé číslo → „Váha N".
    private static readonly (int W, string Label)[] Names =
    [
        (100, "Tenké (Thin)"), (200, "Extra Light"), (300, "Light"), (350, "Semilight"),
        (400, "Normal"), (500, "Medium"), (600, "Semi-Bold"),
        (700, "Bold"), (800, "Extra Bold"), (900, "Heavy / Black"),
    ];

    /// <summary>Popisek váhy (400 → „Normal"). Neznámá → „Váha N".</summary>
    public static string Label(int weight)
    {
        foreach (var (w, label) in Names) if (w == weight) return label;
        return $"Váha {weight}";
    }

    /// <summary>Reálné (vzpřímené) váhy nainstalované rodiny, seřazené. Kurzívu řeší checkbox,
    /// proto jen Upright. Prázdná/nedostupná rodina → fallback Normal+Bold.</summary>
    public static IReadOnlyList<int> Weights(string? family)
    {
        var real = InstalledWeights(family);
        return real.Count > 0 ? real : [400, 700];
    }

    /// <summary>Váhy rodiny **bez fallbacku** - prázdné, když rodina není nainstalovaná nebo není
    /// zadaná. Rozlišení „řezy skutečně známe" vs „hádáme" je potřeba tam, kde se podle nich
    /// zahazuje uživatelova hodnota - podle odhadu se zahazovat nesmí.</summary>
    public static IReadOnlyList<int> InstalledWeights(string? family)
    {
        if (string.IsNullOrWhiteSpace(family)) return [];

        // Šířková varianta („Arial Narrow") je řez rodiny Arial, ne vlastní rodina - řezy se tedy
        // čtou z rodiny a filtrují na její šířku, jinak by nabídka obsahovala i váhy, které úzká
        // varianta nemá (Arial má Black jen v normální šířce).
        var (osFamily, width) = FontResolver.ResolveFamilyAndWidth(family);

        var weights = new SortedSet<int>();
        try
        {
            using var set = SKFontManager.Default.GetFontStyles(osFamily);
            for (int i = 0; i < set.Count; i++)
            {
                var style = set[i];
                if (style.Slant != SKFontStyleSlant.Upright) continue;
                if (width is int w && style.Width != w) continue;
                weights.Add(style.Weight);
            }
        }
        catch { /* nedostupná rodina → prázdno (volající rozhodne, co s tím) */ }
        return weights.ToList();
    }

    /// <summary>Váhy rodiny jako (číslo, popisek) - pro naplnění dropdownů.</summary>
    public static IReadOnlyList<(int Weight, string Label)> Items(string? family) =>
        Weights(family).Select(w => (w, Label(w))).ToList();
}
