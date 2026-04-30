namespace TemplateDesigner.Rendering;

/// <summary>
/// UX omezení editace stylu buňky. Dialog zobrazí/skryje/zakáže UI podle těchto flagů.
/// Neaplikuje se na renderer — omezení je čistě UX, ne business rule.
/// </summary>
public record CellStyleEditOptions
{
    public bool AllowFontFamily       { get; init; } = true;
    public bool AllowFontSize         { get; init; } = true;
    public bool AllowBold             { get; init; } = true;
    public bool AllowItalic           { get; init; } = true;
    public bool AllowUnderline        { get; init; } = true;
    public bool AllowStrikethrough    { get; init; } = true;
    public bool AllowTextColor        { get; init; } = true;
    public bool AllowBackgroundColor  { get; init; } = true;
    public bool AllowHAlign           { get; init; } = true;
    public bool AllowVAlign           { get; init; } = true;
    public bool AllowPadding          { get; init; } = true;

    /// <summary>Vše povolené — výchozí chování.</summary>
    public static readonly CellStyleEditOptions AllEnabled = new();
}
