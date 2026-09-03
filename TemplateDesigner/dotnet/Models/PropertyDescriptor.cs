using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Schema;

namespace TemplateDesigner.Models;

// ─── Základ ────────────────────────────────────────────────────────────────────

/// <summary>
/// Deskriptor jedné editovatelné vlastnosti elementu.
/// Panel vlastností generuje UI řádky z těchto deskriptorů - přidání nové
/// vlastnosti znamená jen přidat deskriptor do GetPropertyDescriptors(),
/// žádná změna v panelu samotném není potřeba.
/// </summary>
public abstract class PropertyDescriptor
{
    public required string Label { get; init; }

    /// <summary>Název skupiny pro vizuální oddělení (např. "Font", "Vzhled", "Pozice").</summary>
    public required string Group { get; init; }

    /// <summary>
    /// Smí se na tuhle vlastnost ptát import z předlohy? Výchozí <c>false</c> - ptáme se jen na to,
    /// co jde z papíru <b>potvrdit</b>.
    ///
    /// <para>Rozhodovací pravidlo: <b>nepřítomnost nic nedokazuje.</b> Že na vývěsce nejsou značky
    /// přestupů, může znamenat vypnutou volbu i linku bez přestupů - to z jedné předlohy nerozlišíš
    /// a špatný odhad se vytiskne. Naopak strana šipky, počet oddílů vedle sebe nebo počet sloupců
    /// legendy jsou z obrázku jednoznačné.</para>
    ///
    /// <para>Příznak je <b>u definice vlastnosti</b>, ne ve zvlášť vedeném seznamu - jinak by se to
    /// s prvky rozešlo, což je přesně důvod, proč import zamrzl v červenci. Nová vlastnost se tím
    /// rozhoduje při vzniku a mlčky se do otázky nedostane.</para>
    ///
    /// <para>Velikosti písma, výšky řádků a šířky sloupců příznak <b>nemají záměrně</b>: ty se z PDF
    /// měří přesně, takže ptát se na ně modelu by výsledek jen zhoršilo.</para>
    /// </summary>
    public bool FromImage { get; init; }

    /// <summary>
    /// Volitelná podmínka viditelnosti deskriptoru v panelu.
    /// Null = vždy viditelný. Vrátí false = řádek se skryje (např. závisí na jiné property).
    /// Po commitu hodnoty v panelu se kontroluje znovu - panel se rebuildne pokud existuje aspoň jeden deskriptor s VisibleWhen.
    /// </summary>
    public Func<bool>? VisibleWhen { get; init; }

    /// <summary>
    /// Vlastní vratný příkaz pro zápis hodnoty; null = obalit hodnotu <c>SetPropertyCommand</c>em.
    /// Potřebné tam, kde jedna změna sahá na **víc cílů najednou** (formátování skupiny buněk):
    /// zpětný krok musí každému vrátit jeho původní hodnotu, ne všem tu jednu z deskriptoru.
    /// Metoda změnu **rovnou provede** a vrátí příkaz, jehož opětovné vykonání je bez efektu
    /// (stejný vzor jako vratná změna struktury tabulky).
    /// </summary>
    public Func<object?, Commands.IDesignerCommand?>? MakeCommand { get; init; }
}

// ─── Konkrétní typy ────────────────────────────────────────────────────────────

public class StringPropertyDescriptor : PropertyDescriptor
{
    public required Func<string>   Get { get; init; }
    public required Action<string> Set { get; init; }

    /// <summary>True = víceřádkový TextBox (pro delší texty).</summary>
    public bool MultiLine { get; init; }

    /// <summary>Volitelná validace - vrátí chybovou zprávu nebo null pokud je vstup OK.</summary>
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

    // ── Sdílené předdefinované Options pro běžné enumy (zarovnání) ──
    // DRY napříč elementy; pokud potřebuješ jiné popisky, definuj inline.

    public static readonly (string Label, object Value)[] HAlignOptions =
    [
        ("Vlevo",    TextHorizontalAlignment.Left),
        ("Na střed", TextHorizontalAlignment.Center),
        ("Vpravo",   TextHorizontalAlignment.Right),
    ];

    public static readonly (string Label, object Value)[] VAlignOptions =
    [
        ("Nahoře",   TextVerticalAlignment.Top),
        ("Na střed", TextVerticalAlignment.Middle),
        ("Dole",     TextVerticalAlignment.Bottom),
    ];
}

/// <summary>
/// Rozšíření StringPropertyDescriptor pro texty s placeholdery {FIELD}.
/// Panel navíc zobrazí tlačítko pro výběr pole ze schématu a validační upozornění
/// na neznámé reference.
/// </summary>
public class TextWithPickerPropertyDescriptor : StringPropertyDescriptor
{
    /// <summary>
    /// Dostupná pole agendy - zobrazí picker tlačítko "⊞" a validaci placeholderů.
    /// Null nebo prázdný seznam = chová se stejně jako StringPropertyDescriptor.
    /// </summary>
    public IReadOnlyList<DataField>? AvailableFields { get; init; }
}

/// <summary>
/// String pole s filtrovatelným dropdownem - uživatel může psát nebo vybrat z nabídky.
/// Hodnoty se filtrují podle vepsaného textu (substring match).
/// Dědí Get/Set/Label/Group ze StringPropertyDescriptor.
/// </summary>
public class SuggestStringPropertyDescriptor : StringPropertyDescriptor
{
    /// <summary>
    /// Dostupné možnosti - (Value, Label). Value se ukládá, Label se zobrazuje v dropdownu.
    /// Pokud je Label stejný jako Value nebo prázdný, zobrazí se jen Value.
    /// </summary>
    public required Func<IEnumerable<(string Value, string Label)>> GetSuggestions { get; init; }
}

/// <summary>
/// Tlačítko v panelu vlastností - kliknutím spustí akci (např. otevření editoru).
/// Panel zobrazí celou šířku tlačítko v příslušné skupině.
/// </summary>
public class ButtonPropertyDescriptor : PropertyDescriptor
{
    public required string ButtonLabel { get; init; }

    /// <summary>
    /// Strojový identifikátor akce - pro spolehlivé routování v ElementPropertiesWindowViewModel.
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
    public const string ActionMatchImageAspect = "MatchImageAspect";
    public const string ActionVyplnitZbytekStranky = "VyplnitZbytekStranky";
}
