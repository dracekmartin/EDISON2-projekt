using TemplateDesigner.Commands;
using TemplateDesigner.Commands.Implementations;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Rendering;

namespace TemplateDesigner.Models;

/// <summary>
/// Formátování buněk pevné tabulky - kaskáda, vratný zápis a vlastnosti pro formátovací lištu.
/// Bez WPF, protože tohle je ta část, která se dá pokazit tiše (kaskáda, undo), a musí jít otestovat.
///
/// Pracuje vždy nad **množinou** buněk, i když je dnes jednoprvková: výběr oblasti tažením pak
/// přidá jen jiný zdroj souřadnic a nepřepisuje se ani jedna vratná operace.
/// </summary>
/// <param name="table">Tabulka, jejíž buňky se formátují.</param>
/// <param name="data">Zdroj dat náhledu - čte se **až při použití**, ne při vytvoření: mřížka se
/// mezitím může přepočítat (refetch dat) a zápis by pak trefil jinou buňku.</param>
/// <param name="cells">Cílové buňky (viditelné souřadnice); první je aktivní.</param>
public sealed class TableCellFormatting(
    TableElement table, Func<DataContext?> data, IReadOnlyList<(int Row, int Col)> cells)
{
    /// <summary>Vyřešený styl aktivní buňky - to, co uživatel reálně vidí (vlastní hodnoty přes
    /// zděděné ze sloupce a z „Stylu buněk" prvku).</summary>
    public ResolvedTextStyle ActiveStyle()
    {
        if (cells.Count == 0) return CellDefaults.Default;
        var (row, col) = cells[0];
        var inherited = table.ResolveCellParent(data(), col);
        return table.EnsureCell(data(), row, col)?.Style?.Resolve(inherited) ?? inherited;
    }

    /// <summary>
    /// Zapíše změnu stylu do všech cílových buněk a vrátí vratný příkaz (změna je už provedená,
    /// opětovné vykonání je bez efektu - stejný vzor jako u vratné změny struktury tabulky).
    /// Zpětný krok vrací **snímek pořízený před změnou**, takže každá buňka dostane zpět svou
    /// vlastní hodnotu; jedna společná hodnota z lišty by u různě naformátovaných buněk lhala.
    /// </summary>
    public IDesignerCommand? Apply(string label, Action<TextStyle> change)
    {
        if (cells.Count == 0) return null;

        var before = table.CaptureCells(data(), cells);
        foreach (var (row, col) in cells)
        {
            if (table.EnsureCell(data(), row, col) is not { } cell) continue;
            cell.Style ??= new TextStyle();
            change(cell.Style);
        }
        return Command(label, before);
    }

    /// <summary>
    /// Dorovná ostatní buňky výběru podle toho, co se právě změnilo na **aktivní** buňce.
    /// Formulář (panel i okno) zapisuje do jednoho objektu, ale uživatel, který má označenou
    /// oblast, čeká změnu na celé oblasti - jako v Excelu. Kopírují se **jen vlastnosti, které se
    /// reálně změnily**; zbytek si každá buňka nechává svůj, aby hromadná změna barvy nesrovnala
    /// i písmo.
    /// </summary>
    public void ApplyActiveChangesToOthers(TableElement.CellSnapshot before)
    {
        if (cells.Count <= 1) return;

        var (activeRow, activeCol) = cells[0];
        int index = before.Cells.FindIndex(c => c.Row == activeRow && c.Col == activeCol);
        if (index < 0) return;
        if (table.EnsureCell(data(), activeRow, activeCol) is not { } active) return;

        // Prázdný styl místo null - buňka bez vlastního stylu má všechny vlastnosti „nenastaveno",
        // takže se porovnává hodnota po hodnotě stejně jako u nastavené.
        var previous  = before.Cells[index];
        var wasStyle  = previous.Style ?? new TextStyle();
        var nowStyle  = active.Style ?? new TextStyle();

        var changed = typeof(TextStyle).GetProperties()
            .Where(p => p.CanWrite && !Equals(p.GetValue(nowStyle), p.GetValue(wasStyle)))
            .ToList();
        bool formatChanged = active.Format != previous.Format;
        if (changed.Count == 0 && !formatChanged) return;

        foreach (var (row, col) in cells.Skip(1))
        {
            if (table.EnsureCell(data(), row, col) is not { } cell) continue;
            if (formatChanged) cell.Format = active.Format;
            if (changed.Count == 0) continue;

            cell.Style ??= new TextStyle();
            foreach (var property in changed) property.SetValue(cell.Style, property.GetValue(nowStyle));
        }
    }

    /// <summary>Vratný příkaz z rozdílu proti snímku <paramref name="before"/> - pro změny, které
    /// do buňky zapsal někdo jiný (dialog vlastností buňky).</summary>
    public IDesignerCommand Command(string label, TableElement.CellSnapshot before) =>
        new SetPropertyCommand(label, before, table.CaptureCells(data(), cells),
            v => table.RestoreCells(data(), (TableElement.CellSnapshot)v!));

    public TableElement.CellSnapshot Capture() => table.CaptureCells(data(), cells);

    /// <summary>
    /// Hodnoty, které pro tuhle buňku vydá kaskáda (styl sloupce → „Styl všech buněk" prvku →
    /// zapečené výchozí), ve tvaru polí formuláře - klíč = property path (<c>Style.FontSize</c>).
    /// Formulář buňky je jimi předvyplněný, takže ukazuje, jak buňka reálně vypadá, ale zapíše
    /// jen to, co uživatel opravdu změní; jinak by potvrzení formuláře odpojilo buňku od tabulky.
    /// Čte se **reflexí přes vyřešený styl**, takže nové pole stylu se propíše samo.
    /// </summary>
    public Dictionary<string, string> InheritedFormValues()
    {
        var resolved = ActiveStyle();
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in typeof(ResolvedTextStyle).GetProperties())
            if (property.GetIndexParameters().Length == 0)
                values["Style." + property.Name] = ElementPropertyAccessor.ToFormValue(property.GetValue(resolved));
        return values;
    }

    /// <summary>
    /// Vlastnosti aktivní buňky pro formátovací lištu. Bez nich je lišta u tabulky prázdná a jedinou
    /// cestou k tučnému záhlaví by byl dialog - přitom „udělej z prvního řádku záhlaví" je nejčastější
    /// úkon. Zápis nese deskriptor sám (<see cref="PropertyDescriptor.MakeCommand"/>), protože sahá
    /// na víc buněk najednou.
    /// </summary>
    public List<PropertyDescriptor> Descriptors()
    {
        BoolPropertyDescriptor Toggle(string label, Func<ResolvedTextStyle, bool> get, Action<TextStyle, bool> set) =>
            new()
            {
                Label = label, Group = "Font",
                Get = () => get(ActiveStyle()),
                Set = v => Apply(label, s => set(s, v)),
                MakeCommand = v => Apply(label, s => set(s, (bool)v!))
            };

        EnumPropertyDescriptor Align(string label, (string Label, object Value)[] options,
            Func<ResolvedTextStyle, object> get, Action<TextStyle, object> set) =>
            new()
            {
                Label = label, Group = "Zarovnání", Options = options,
                Get = () => get(ActiveStyle()),
                Set = v => Apply(label, s => set(s, v)),
                MakeCommand = v => Apply(label, s => set(s, v!))
            };

        return
        [
            Toggle("Tučné",       s => s.Bold,          (s, v) => s.Bold = v),
            Toggle("Kurzíva",     s => s.Italic,        (s, v) => s.Italic = v),
            Toggle("Podtržení",   s => s.Underline,     (s, v) => s.Underline = v),
            Toggle("Přeškrtnutí", s => s.Strikethrough, (s, v) => s.Strikethrough = v),
            Align(EnumPropertyDescriptor.LabelZarovnaniVodorovne, EnumPropertyDescriptor.HAlignOptions,
                s => s.HAlign, (s, v) => s.HAlign = (TextHorizontalAlignment)v),
            Align(EnumPropertyDescriptor.LabelZarovnaniSvisle, EnumPropertyDescriptor.VAlignOptions,
                s => s.VAlign, (s, v) => s.VAlign = (TextVerticalAlignment)v),
        ];
    }
}
