namespace TemplateDesigner.Models;

/// <summary>
/// Ambient (dokumentový) výchozí styl textu — kořen kaskády stylů celé šablony.
/// Nastavuje se při změně aktuální šablony (load / new / edit defaultu); čtou ho „dno" kaskády
/// (<see cref="ResolvedTextStyle.Default"/>, <c>CellDefaults.Default</c> a odvození rodiče), takže
/// se template default propíše do všech elementů, které styl nepřepíší — bez threadingu přes
/// desítky <c>Resolve()</c> call-sites.
///
/// <para><b>Pozor:</b> jde o procesně-globální stav. Je to bezpečné, protože WPF render je
/// jednovláknový a TemplateDesigner pracuje s jednou šablonou naráz. Při přepnutí šablony se
/// MUSÍ přenastavit (i na null), jinak by font z předchozí šablony „přetekl".</para>
///
/// <para>Drží celý <see cref="TextStyle"/> (ne jen font) — rozšíření o velikost/barvu/… je pak
/// jen přidání pole do UI, bez změny této infrastruktury.</para>
/// </summary>
public static class DocumentDefaults
{
    /// <summary>Výchozí styl aktuální šablony. Null = žádný override (zapečené defaulty).</summary>
    public static TextStyle? Style { get; set; }

    /// <summary>
    /// Overlay vyplněných hodnot dokumentového defaultu na zapečený základ kaskády.
    /// Volá se z „dna" kaskády. Null Style → vrátí základ beze změny.
    /// </summary>
    public static ResolvedTextStyle Apply(ResolvedTextStyle baked)
    {
        var s = Style;
        if (s is null) return baked;
        return baked with
        {
            // Prázdný/whitespace dokumentový font = žádný override (zděď zapečený default), jinak ořež okraje.
            FontFamily      = string.IsNullOrWhiteSpace(s.FontFamily) ? baked.FontFamily : s.FontFamily.Trim(),
            FontSize        = s.FontSize        ?? baked.FontSize,
            Bold            = s.Bold            ?? baked.Bold,
            Italic          = s.Italic          ?? baked.Italic,
            Underline       = s.Underline       ?? baked.Underline,
            Strikethrough   = s.Strikethrough   ?? baked.Strikethrough,
            TextColor       = s.TextColor       ?? baked.TextColor,
            BackgroundColor = s.BackgroundColor ?? baked.BackgroundColor,
            HAlign          = s.HAlign          ?? baked.HAlign,
            VAlign          = s.VAlign          ?? baked.VAlign,
            PaddingTop      = s.PaddingTop      ?? baked.PaddingTop,
            PaddingRight    = s.PaddingRight    ?? baked.PaddingRight,
            PaddingBottom   = s.PaddingBottom   ?? baked.PaddingBottom,
            PaddingLeft     = s.PaddingLeft     ?? baked.PaddingLeft,
            LineSpacing     = s.LineSpacing     ?? baked.LineSpacing,
        };
    }
}
