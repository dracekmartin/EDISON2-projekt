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

    /// <summary>Vratný příkaz z rozdílu proti snímku <paramref name="before"/> - pro změny, které
    /// do buňky zapsal někdo jiný (dialog vlastností buňky).</summary>
    public IDesignerCommand Command(string label, TableElement.CellSnapshot before) =>
        new SetPropertyCommand(label, before, table.CaptureCells(data(), cells),
            v => table.RestoreCells(data(), (TableElement.CellSnapshot)v!));

    public TableElement.CellSnapshot Capture() => table.CaptureCells(data(), cells);

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
            Align("Vodorovně", EnumPropertyDescriptor.HAlignOptions,
                s => s.HAlign, (s, v) => s.HAlign = (TextHorizontalAlignment)v),
            Align("Svisle", EnumPropertyDescriptor.VAlignOptions,
                s => s.VAlign, (s, v) => s.VAlign = (TextVerticalAlignment)v),
        ];
    }
}
