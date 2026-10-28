using System;
using System.Collections.ObjectModel;
using System.Linq;
using EDControls.EDViewModel;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Schema;
using TemplateDesigner.Rendering;

namespace TemplateDesigner;

// ─── ViewModel pro řádek sloupce (mapuje TableColumn) ────────────────────────

public class DynamicTableColumnVM : Controls.ColumnRowVM
{
    private string _field = "";
    private string _expression = "";
    private string _format = "";

    private DynamicTableColumnVM(TableColumn c, CellStyleVM headerStyle, CellStyleVM dataStyle) : base(headerStyle, dataStyle)
    {
        Alignment   = c.Alignment;
        WidthMode   = c.WidthMode;
        Header      = c.Header;
        _field      = c.Field ?? "";
        _expression = c.Expression ?? "";
        _format     = c.Format ?? "";
        Width       = c.Width > 0f ? c.Width : null;   // 0 → auto
        Visible     = c.Visible;
    }

    public string Field      { get => _field;      set { _field = value;      OnChanged(); OnChanged(nameof(DisplayName)); } }
    public string Expression { get => _expression; set { _expression = value; OnChanged(); OnChanged(nameof(DisplayName)); } }
    public string Format     { get => _format;     set { _format = value;     OnChanged(); } }

    /// <summary>Zarovnání a režim šířky okno nenabízí - musí je ale vrátit beze změny. Do 28. 10.
    /// je OK zahodilo (zarovnání spadlo na Vlevo, 40 % se změnilo na 40 mm).</summary>
    public TextHorizontalAlignment Alignment { get; }
    public ColumnWidthMode WidthMode { get; }

    public override string WidthUnit => WidthMode == ColumnWidthMode.Percent ? "%" : "mm";

    public override float FixedWidthMm(float elementWidthMm) =>
        WidthMode == ColumnWidthMode.Percent ? (Width ?? 0f) / 100f * elementWidthMm : Width ?? 0f;

    public override string DisplayName => !string.IsNullOrWhiteSpace(Header) ? Header
                                        : !string.IsNullOrWhiteSpace(_field) ? "{" + _field + "}"
                                        : !string.IsNullOrWhiteSpace(_expression) ? "= " + _expression
                                        : "(nový sloupec)";

    public TableColumn ToModel() => new()
    {
        Header      = Header,
        Field       = string.IsNullOrWhiteSpace(Field) ? null : Field,
        Expression  = string.IsNullOrWhiteSpace(Expression) ? null : Expression.Trim(),
        Format      = string.IsNullOrWhiteSpace(Format) ? null : Format.Trim(),
        Width       = Width ?? 0f,               // 0 = auto (rozdělí šířku elementu)
        WidthMode   = WidthMode,
        Alignment   = Alignment,
        Visible     = Visible,
        HeaderStyle = HeaderStyle.ToModel(),
        DataStyle   = DataStyle.ToModel()
    };

    /// <summary>Zděděná vrstva se záložním zarovnáním sloupce jako při tisku (<see cref="TableLayout.ResolveCascade"/>).</summary>
    public static DynamicTableColumnVM FromModel(TableColumn c, DynamicTableElement element) => new(c,
        CellStyleVM.FromModel(c.HeaderStyle, TableLayout.ResolveCascade(null, element.ResolveColumnHeaderParent(),
            c.Alignment, elementDefinesHAlign: element.HeaderStyle?.HAlign is not null)),
        CellStyleVM.FromModel(c.DataStyle, TableLayout.ResolveCascade(null, element.ResolveColumnDataParent(),
            c.Alignment, elementDefinesHAlign: element.DataStyle?.HAlign is not null)));
}

// ─── Dialog ViewModel ────────────────────────────────────────────────────────

class DynamicTableEditorDialogViewModel : EDViewModel
{
    // Tab 1 „Obecné" = embedded EDInputForm (DynamicTableElementEdit.cls) - řeší code-behind
    // přes ElementFormBinder. VM drží jen záložku Sloupce (sdílená ColumnsTab).

    public Controls.ColumnsTabViewModel Columns { get; }

    /// <summary>Sloupce okna (řádky záložky Sloupce).</summary>
    public IEnumerable<DynamicTableColumnVM> ColumnVMs => Columns.Rows.Cast<DynamicTableColumnVM>();

    /// <summary>Dostupná datová pole aktuální kolekce (pro našeptávání „Pole").</summary>
    public ObservableCollection<string> AvailableFields { get; } = [];

    public List<TableColumn> ResultColumns { get; private set; } = [];

    private readonly DynamicTableElement _element;

    public DynamicTableEditorDialogViewModel(
        DynamicTableElement element,
        IReadOnlyList<DataFieldCollection> collections)
    {
        _element = element;

        Columns = new Controls.ColumnsTabViewModel(element.Columns.Select(col => DynamicTableColumnVM.FromModel(col, element)))
        {
            ElementWidth = () => element.Width,
            CanFit = true,
            MultilineHeader = true,
            NewRow = () => DynamicTableColumnVM.FromModel(new TableColumn { Header = "", Width = 0f }, element),
        };

        // Nabídka polí „Pole" pro aktuální zdroj (načtený element) - jen našeptávání, neseeduje sloupce.
        var current = collections.FirstOrDefault(c => c.CollectionKey == element.DataSource);
        foreach (var fld in current?.Fields ?? [])
            AvailableFields.Add(fld.FieldName);
    }

    /// <summary>Zdroj, ze kterého byly sloupce naposledy auto-seedovány (null = sloupce jsou ruční /
    /// z uložené šablony, ne auto). Řídí re-seed při změně zdroje.</summary>
    private string? _seededFromKey;

    /// <summary>
    /// Reakce na výběr zdroje dat: obnoví nabídku polí (našeptávání „Pole") a naseeduje sloupce z polí
    /// kolekce (Header = label, Field = název, auto šířka). Sloupce nahradí jen když jsou prázdné, nebo
    /// když byly auto-seedovány z JINÉHO zdroje (uživatel přepnul zdroj). Ruční / z uložené šablony
    /// (bez auto-seed značky) nikdy nepřepíše.
    /// </summary>
    public void SeedColumnsFromCollection(IReadOnlyList<DataField> fields, string sourceKey)
    {
        AvailableFields.Clear();
        foreach (var f in fields) AvailableFields.Add(f.FieldName);

        bool autoSeeded    = _seededFromKey is not null;
        bool sourceChanged = _seededFromKey != sourceKey;

        // Nesahat: prázdné není (máme sloupce) A nejde o přepnutí auto-seedovaného zdroje.
        if (Columns.Rows.Count > 0 && !(autoSeeded && sourceChanged)) return;

        Columns.ReplaceRows(DynamicTableElement.BuildColumnsFromFields(fields)
            .Select(col => DynamicTableColumnVM.FromModel(col, _element)));
        _seededFromKey = sourceKey;
    }

    protected override void OnOk()
    {
        // Element-level styl + ostatní general props řeší embedded EDInputForm (code-behind).
        ResultColumns = ColumnVMs.Select(vm => vm.ToModel()).ToList();
        CloseDialog(true);
    }
}
