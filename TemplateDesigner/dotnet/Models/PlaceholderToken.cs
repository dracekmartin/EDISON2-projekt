namespace TemplateDesigner.Models;

/// <summary>
/// Rozpoznání rozepsaného pole <c>{POLE</c> pod kurzorem - podklad pro našeptávač polí při psaní
/// do buňky tabulky.
/// <para>
/// Žije mimo okno schválně: je to jediná netriviální část našeptávače a **spadla** (12. 8.,
/// záporná délka podřetězce, když se kurzor po doplnění ocitl před závorkou). V code-behindu se
/// takový výpočet nedá otestovat jinak než proklikáním.
/// </para>
/// </summary>
public static class PlaceholderToken
{
    /// <summary>
    /// Rozepsané pole u kurzoru: pozice otevírací závorky a text napsaný za ní. Null = kurzor
    /// v poli nestojí (žádná závorka před ním, nebo je pole už uzavřené).
    /// Bere **nejbližší** otevírací závorku před kurzorem, takže rozepsané pole uprostřed textu
    /// i za dřívějším polem funguje.
    /// </summary>
    public static (int Start, string Prefix)? Typing(string? text, int caret)
    {
        string s = text ?? "";
        caret = Math.Clamp(caret, 0, s.Length);
        if (caret == 0) return null;

        int open  = s.LastIndexOf('{', caret - 1);
        int close = s.LastIndexOf('}', caret - 1);
        if (open < 0 || close > open) return null;

        return (open, s[(open + 1)..caret]);
    }
}
