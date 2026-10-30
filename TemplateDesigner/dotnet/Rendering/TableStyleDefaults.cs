using TemplateDesigner.Models;

namespace TemplateDesigner.Rendering;

/// <summary>
/// Jak vypadá buňka tabulky, když jí uživatel nic nenastaví - konec kaskády pro pevnou tabulku
/// i tabulku z dat. Oba prvky mají stejné výchozí hodnoty a do 21. 9. je měly opsané každý u sebe,
/// takže změna odstínu nebo oprava kaskády se propsala jen do jednoho z nich.
/// </summary>
/// <remarks>
/// Jízdněřádové prvky sem nepatří - mají vlastní, jinak vypadající výchozí hodnoty
/// (<see cref="Models.Elements.ScheduleElementBase"/>). Sdílí se jen to, co je opravdu totéž.
/// </remarks>
public static class TableStyleDefaults
{
    // Gettery (ne static readonly) - aby font dědily z aktuálního CellDefaults.Default (ambient
    // = výchozí font šablony). Tyhle rodiče font nepřebíjejí, jen Bold/barvy.

    /// <summary>Výchozí parent styl pro záhlaví tabulky. Bold + tmavě šedý text (#FF333333).</summary>
    public static ResolvedTextStyle Header => CellDefaults.Default with
    {
        Bold = true,
        TextColor = "#FF333333",
    };

    /// <summary>Výchozí parent styl pro datové buňky. Tmavě šedý text (#FF222222) na průhledném pozadí.</summary>
    public static ResolvedTextStyle Data => CellDefaults.Default with
    {
        TextColor = "#FF222222",
    };

    /// <summary>Výchozí parent styl pro souhrnné řádky. Bold + světle šedé pozadí (#FFF0F0F0).</summary>
    public static ResolvedTextStyle Summary => CellDefaults.Default with
    {
        Bold = true,
        TextColor = "#FF222222",
        BackgroundColor = "#FFF0F0F0",
    };

    // Podbarvení záhlaví se do Header záměrně nepíše: prázdná hodnota u prvku má znamenat
    // „nekreslit nic", a zapečená záloha v kaskádě by ji vrátila zpátky na šedou. Novému prvku
    // se šedá zapisuje, existujícím ji dopsala migrace 1.11.0.
    /// <summary>
    /// Podbarvení záhlaví, které dostane <b>nově vložený</b> prvek s tabulkou - prvky jízdních řádů i obě tabulky.
    /// Zapisuje se do prvku, není to záloha kaskády - jinak by nešlo rozlišit „nechal jsem to být" od „nechci
    /// žádné pozadí" a vymazání barvy by se po uložení vrátilo (nález Pavel 1. 9., #13158). Do 30. 10. měly
    /// tabulky o odstín světlejší `#FFE8E8E8` bez důvodu; staré šablony si ho nesou uložený.
    /// </summary>
    public const string NoveZahlaviPozadi = "#FFE0E0E0";
}
