using TemplateDesigner.Models.Schema;

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

    /// <summary>Volitelná validace — vrátí chybovou zprávu nebo null pokud je vstup OK.</summary>
    public Func<string, string?>? Validate { get; init; }
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

/// <summary>
/// Rozšíření StringPropertyDescriptor pro texty s placeholdery {FIELD}.
/// Panel navíc zobrazí tlačítko pro výběr pole ze schématu a validační upozornění
/// na neznámé reference.
/// </summary>
public class TextWithPickerPropertyDescriptor : StringPropertyDescriptor
{
    /// <summary>
    /// Dostupná pole agendy — zobrazí picker tlačítko "⊞" a validaci placeholderů.
    /// Null nebo prázdný seznam = chová se stejně jako StringPropertyDescriptor.
    /// </summary>
    public IReadOnlyList<DataField>? AvailableFields { get; init; }
}

/// <summary>
/// String pole s filtrovatelným dropdownem — uživatel může psát nebo vybrat z nabídky.
/// Hodnoty se filtrují podle vepsaného textu (substring match).
/// Dědí Get/Set/Label/Group ze StringPropertyDescriptor.
/// </summary>
public class SuggestStringPropertyDescriptor : StringPropertyDescriptor
{
    /// <summary>
    /// Dostupné možnosti — (Value, Label). Value se ukládá, Label se zobrazuje v dropdownu.
    /// Pokud je Label stejný jako Value nebo prázdný, zobrazí se jen Value.
    /// </summary>
    public required Func<IEnumerable<(string Value, string Label)>> GetSuggestions { get; init; }
}

/// <summary>
/// Tlačítko v panelu vlastností — kliknutím spustí akci (např. otevření editoru).
/// Panel zobrazí celou šířku tlačítko v příslušné skupině.
/// </summary>
public class ButtonPropertyDescriptor : PropertyDescriptor
{
    public required string ButtonLabel { get; init; }

    /// <summary>
    /// Strojový identifikátor akce — pro spolehlivé routování v ElementPropertiesWindowViewModel.
    /// </summary>
    public string? ActionId { get; init; }

    /// <summary>
    /// Akce volaná při kliknutí. Null = tlačítko je zobrazeno ale nedělá nic.
    /// </summary>
    public Action? OnClick { get; init; }

    // ── Známé ActionId hodnoty ────────────────────────────────────────────
    public const string ActionOpenTableEditor = "OpenTableEditor";
    public const string ActionOpenStopColumnsEditor = "OpenStopColumnsEditor";
    public const string ActionOpenDepartureGridEditor = "OpenDepartureGridEditor";
    public const string ActionUploadImage = "UploadImage";
}
