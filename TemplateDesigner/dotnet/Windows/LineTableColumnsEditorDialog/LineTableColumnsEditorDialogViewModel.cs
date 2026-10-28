using EDControls.EDViewModel;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Rendering;

namespace TemplateDesigner;

// ─── ViewModel pro řádek sloupce Tabulky spojů ───────────────────────────────

/// <summary>
/// Řádek sloupce Tabulky spojů - sdílí model <see cref="StopColumn"/> se Seznamem zastávek.
/// Zarovnání se prosazuje přes HAlign ve stylu; <see cref="Alignment"/> je jen záloha sloupce
/// (zastávka vlevo, ostatní na střed), kterou okno vrací beze změny.
/// </summary>
public class LineTableColumnVM : Controls.ColumnRowVM
{
    private LineTableColumnVM(StopColumn c, CellStyleVM headerStyle, CellStyleVM dataStyle) : base(headerStyle, dataStyle)
    {
        Key       = c.Key;
        Alignment = c.Alignment;
        Header    = c.Header;
        Width     = c.Width;
        Visible   = c.Visible;
    }

    public string Key { get; }
    public TextHorizontalAlignment Alignment { get; }

    // Čitelný název dle klíče (Header je tištěná zkratka)
    public override string DisplayName => Key switch
    {
        ColumnKeys.Zone         => "Zóna",
        ColumnKeys.Km           => "Kilometry",
        ColumnKeys.Tc           => "Tarifní číslo (TČ)",
        ColumnKeys.OriginalZone => "Zóna původní verze",
        ColumnKeys.OriginalTc   => "Tarifní číslo (TČ) původní verze",
        ColumnKeys.StopName     => "Zastávka",
        ColumnKeys.Mhd          => "MHD / IDS",
        ColumnKeys.Trip         => "Spoje (časové sloupce)",
        ColumnKeys.Arrow        => "Šipka směru",
        _                       => string.IsNullOrWhiteSpace(Header) ? Key : Header,
    };

    /// <summary>Sloupec spoje se tiskne vždy a vždy za popisnými sloupci (`LineTableElement.TripColumn`
    /// viditelnost ani pořadí nečte), takže ho nejde skrýt ani posunout.</summary>
    private bool JeSpoj => Key == LineTableElement.TripKey;
    private bool JeSipka => Key == ColumnKeys.Arrow;

    public override bool CanHide => !JeSpoj;
    public override bool CanMove => !JeSpoj;

    /// <summary>Přesah hlídají jen popisné sloupce - spoje mají šířku podle obsahu a dělí se do bloků.</summary>
    public override bool CountsTowardWidth => !JeSpoj;

    public override string? WidthHint => JeSpoj
        ? "Šířka každého sloupce spoje. Prázdná = podle obsahu (čas a značky)."
        : "Prázdná šířka = automaticky: popisné sloupce bez šířky si rozdělí místo, které zbude po sloupcích spojů.";

    /// <remarks>
    /// ⚠️ U <b>šipky směru</b> se v datech nekreslí text, ale <b>šipka</b>, takže font, řez, zarovnání
    /// ani odsazení nemají co ovlivnit - zbývá barva (kreslí se jí šipka) a pozadí buňky. Nález Jan
    /// 28. 8.: nastavil barvu ve stylu záhlaví, protože u sloupce bez textu se nedalo poznat, že
    /// šipku řídí zóna datových buněk. Skrytí nesmyslných polí i vlastní nadpis zóny tu cestu ukazují.
    /// </remarks>
    public override CellStyleEditOptions DataEditOptions => JeSipka
        ? new CellStyleEditOptions
        {
            AllowFontFamily = false, AllowFontSize = false, AllowBold = false, AllowItalic = false,
            AllowUnderline = false, AllowStrikethrough = false,
            AllowHAlign = false, AllowVAlign = false, AllowPadding = false,
            AllowTextColor = true, AllowBackgroundColor = true,
        }
        : CellStyleEditOptions.AllEnabled;

    public override string DataZoneTitle => JeSipka ? "Šipka" : base.DataZoneTitle;

    public override string DataZoneHint => JeSipka
        ? "Šipka se kreslí barvou textu. Font ani zarovnání na ni nemají vliv."
        : "";

    public StopColumn ToModel() => new()
    {
        Key       = Key,
        Header    = Header,
        Width     = Width,
        Visible   = Visible,
        Alignment = Alignment,
        Style     = BuildStyle()
    };

    private ColumnVisualStyle? BuildStyle()
    {
        var style = new ColumnVisualStyle { Header = HeaderStyle.ToModel(), Data = DataStyle.ToModel() };
        return style.IsEmpty ? null : style;
    }

    /// <summary>Zděděná vrstva se záložním zarovnáním sloupce jako při tisku
    /// (<see cref="TableLayout.ResolveCascade"/>) - jinak by šedá nápověda ukazovala Vlevo u sloupce,
    /// který se tiskne na střed.</summary>
    public static LineTableColumnVM FromModel(StopColumn c, LineTableElement element)
    {
        var (header, data) = Zdedene(c.Alignment, element);
        return new(c, CellStyleVM.FromModel(c.Style?.Header, header), CellStyleVM.FromModel(c.Style?.Data, data));
    }

    /// <summary>Zděděná vrstva podle prvku, jak ho právě nastavuje záložka Obecné.</summary>
    public void ObnovZdedene(LineTableElement element)
    {
        var (header, data) = Zdedene(Alignment, element);
        UpdateInherited(header, data);
    }

    private static (ResolvedTextStyle Header, ResolvedTextStyle Data) Zdedene(TextHorizontalAlignment zaloha,
        LineTableElement element) =>
        (TableLayout.ResolveCascade(null, element.ResolveColumnHeaderParent(), zaloha,
             elementDefinesHAlign: element.HeaderStyle.HAlign is not null),
         TableLayout.ResolveCascade(null, element.ResolveColumnDataParent(), zaloha,
             elementDefinesHAlign: element.DataStyle.HAlign is not null));
}

// ─── Dialog ViewModel ───────────────────────────────────────────────────────

class LineTableColumnsEditorDialogViewModel : EDViewModel
{
    // Tab 1 „Obecné" = embedded EDInputForm (LineTableElementEdit.cls) - řeší code-behind.
    // VM drží jen záložku Sloupce (sdílená ColumnsTab).

    public Controls.ColumnsTabViewModel Columns { get; }

    public List<StopColumn> ResultColumns { get; private set; } = [];

    private readonly List<StopColumn> _puvodni;
    private readonly string _priOtevreni;

    public LineTableColumnsEditorDialogViewModel(LineTableElement element, List<StopColumn> columns)
    {
        _puvodni = columns;
        var rows = columns.Select(col => LineTableColumnVM.FromModel(col, element)).ToList();

        // Doplň chybějící známé sloupce (uložená šablona může mít podmnožinu) - ať uživatel vidí
        // všechny dostupné sloupce a může je zapnout checkboxem. Doplněné jsou skryté.
        var presentKeys = columns.Select(c => c.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var def in LineTableElement.GetDefaultColumns())
            if (!presentKeys.Contains(def.Key))
            {
                def.Visible = false;
                rows.Add(LineTableColumnVM.FromModel(def, element));
            }

        // Přizpůsobit tu není záměrně (rozhodnutí Jan 26. 10., `B194` E13b): zadávají se jen popisné
        // sloupce, šířku spojů dopočítá tabulka, takže by tlačítko spojům jen vzalo místo.
        Columns = new Controls.ColumnsTabViewModel(rows)
        {
            ElementWidth = () => element.Width,
            OverflowNote = "Na sloupce spojů nezbude místo.",
            MultilineHeader = true,
        };
        _priOtevreni = Controls.ColumnsTabViewModel.Snapshot(Vysledek());
    }

    /// <summary>Přepočítá zděděné styly sloupců podle náhledu prvku se změnami ze záložky Obecné.</summary>
    public void ObnovZdedene(LineTableElement nahled)
    {
        foreach (var row in Columns.Rows.Cast<LineTableColumnVM>()) row.ObnovZdedene(nahled);
    }

    protected override void OnOk()
    {
        ResultColumns =Controls.ColumnsTabViewModel.ResultOrOriginal(_puvodni, _priOtevreni, Vysledek());
        CloseDialog(true);
    }

    private List<StopColumn> Vysledek() => [.. Columns.Rows.Cast<LineTableColumnVM>().Select(vm => vm.ToModel())];
}
