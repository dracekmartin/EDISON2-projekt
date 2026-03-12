using SkiaSharp;

namespace TemplateDesigner.Models;

/// <summary>
/// Utility metody pro práci se skupinami elementů.
/// </summary>
public static class ElementGroupHelper
{
    /// <summary>Generuje nový unikátní GroupId.</summary>
    public static string NewGroupId() => Guid.NewGuid().ToString("N")[..8];

    // Předgenerované barvy pro skupiny — dostatečně odlišné, poloprůhledné
    private static readonly SKColor[] GroupColors =
    [
        new(0xFF4CAF50), // zelená
        new(0xFFFF9800), // oranžová
        new(0xFF9C27B0), // fialová
        new(0xFF00BCD4), // cyan
        new(0xFFE91E63), // růžová
        new(0xFF3F51B5), // indigo
        new(0xFFCDDC39), // limetková
        new(0xFFFF5722), // deep orange
    ];

    /// <summary>Vrátí barvu pro vizuální indikaci skupiny (deterministická z groupId).</summary>
    public static SKColor GetGroupColor(string groupId)
    {
        int hash = 0;
        foreach (char c in groupId)
            hash = hash * 31 + c;
        return GroupColors[(hash & 0x7FFFFFFF) % GroupColors.Length];
    }
}
