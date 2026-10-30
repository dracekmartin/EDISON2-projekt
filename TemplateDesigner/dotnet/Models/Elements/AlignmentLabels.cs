namespace TemplateDesigner.Models.Elements;

/// <summary>
/// Popisky voleb zarovnání. Do 30. 10. byly opsané v nabídce panelu, v editoru stylu sloupce, v šedé nápovědě
/// zděděné hodnoty, v bublinách lišty formátu a ve dvou formulářích v Caché. Formuláře zůstávají v Caché
/// (opravují se bez nového exe), jejich volby porovnává s touhle třídou <c>VolbyZarovnaniTests</c>.
/// </summary>
public static class AlignmentLabels
{
    public static IReadOnlyList<(string Label, TextHorizontalAlignment Value)> Horizontal { get; } =
    [
        ("Vlevo",    TextHorizontalAlignment.Left),
        ("Na střed", TextHorizontalAlignment.Center),
        ("Vpravo",   TextHorizontalAlignment.Right),
    ];

    /// <summary>Do bloku umí jen text; buňka tabulky ho nekreslí, proto není mezi <see cref="Horizontal"/>.</summary>
    public static (string Label, TextHorizontalAlignment Value) Justify { get; } = ("Do bloku", TextHorizontalAlignment.Justify);

    public static IReadOnlyList<(string Label, TextVerticalAlignment Value)> Vertical { get; } =
    [
        ("Nahoře",   TextVerticalAlignment.Top),
        ("Na střed", TextVerticalAlignment.Middle),
        ("Dole",     TextVerticalAlignment.Bottom),
    ];

    public static string Label(TextHorizontalAlignment value) =>
        value == Justify.Value ? Justify.Label : Horizontal.FirstOrDefault(o => o.Value == value).Label ?? "";

    public static string Label(TextVerticalAlignment value) =>
        Vertical.FirstOrDefault(o => o.Value == value).Label ?? "";
}
