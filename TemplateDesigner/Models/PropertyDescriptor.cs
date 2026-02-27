namespace TemplateDesigner.Models;

// ─── Základ ────────────────────────────────────────────────────────────────────

/// <summary>
/// Deskriptor jedné editovatelné vlastnosti elementu.
/// Panel vlastností generuje UI řádky z těchto deskriptorů — přidání nové
/// vlastnosti znamená jen přidat deskriptor do GetPropertyDescriptors(),
/// žádná změna v panelu samotném není potřeba.
/// </summary>
public abstract class PropertyDescriptor
{
    public required string Label { get; init; }

    /// <summary>Název skupiny pro vizuální oddělení (např. "Font", "Vzhled", "Pozice").</summary>
    public required string Group { get; init; }
}

// ─── Konkrétní typy ────────────────────────────────────────────────────────────

public class StringPropertyDescriptor : PropertyDescriptor
{
    public required Func<string>   Get { get; init; }
    public required Action<string> Set { get; init; }

    /// <summary>True = víceřádkový TextBox (pro delší texty).</summary>
    public bool MultiLine { get; init; }
}

public class FloatPropertyDescriptor : PropertyDescriptor
{
    public required Func<float>   Get { get; init; }
    public required Action<float> Set { get; init; }

    public float  Min    { get; init; } = 0f;
    public float  Max    { get; init; } = 9999f;

    /// <summary>Jednotka zobrazená za polem, např. "mm" nebo "pt".</summary>
    public string Unit   { get; init; } = "";

    /// <summary>C# formátovací řetězec pro zobrazení čísla.</summary>
    public string Format { get; init; } = "0.##";
}

public class BoolPropertyDescriptor : PropertyDescriptor
{
    public required Func<bool>   Get { get; init; }
    public required Action<bool> Set { get; init; }
}

/// <summary>Barva ve formátu #AARRGGBB. Panel zobrazí barevný čtvereček + hex TextBox.</summary>
public class ColorPropertyDescriptor : PropertyDescriptor
{
    public required Func<string>   Get { get; init; }
    public required Action<string> Set { get; init; }
}

public class EnumPropertyDescriptor : PropertyDescriptor
{
    public required Func<object>   Get     { get; init; }
    public required Action<object> Set     { get; init; }

    /// <summary>Možnosti zobranzené v ComboBoxu: (zobrazený popisek, hodnota enum).</summary>
    public required (string Label, object Value)[] Options { get; init; }
}
