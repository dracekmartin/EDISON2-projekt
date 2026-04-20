namespace TemplateDesigner.Models;

/// <summary>
/// Pomocné metody pro mm aritmetiku — eliminuje IEEE-754 float artefakty
/// (např. 10.0 * 1.05 = 10.500001) při zachování záměrně zadaných sub-mm hodnot.
/// </summary>
public static class MmMath
{
    /// <summary>Zaokrouhlí mm hodnotu na 0.1 mm.</summary>
    public static float Round(float mm) =>
        MathF.Round(mm, 1, MidpointRounding.AwayFromZero);

    /// <summary>Zaokrouhlí nullable mm hodnotu na 0.1 mm.</summary>
    public static float? Round(float? mm) =>
        mm is null ? null : Round(mm.Value);
}
