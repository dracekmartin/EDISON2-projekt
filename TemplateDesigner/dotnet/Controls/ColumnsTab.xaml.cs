using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Controls;

/// <summary>
/// Záložka Sloupce sdílená okny všech tabulek se sloupci (Seznam zastávek, Minuty odjezdů, Tabulka
/// spojů, Tabulka z dat). DataContext je <see cref="ColumnsTabViewModel"/>. Okno prvku do ní dodá
/// jen pole navíc - šablonu pod záhlaví a šířku (<see cref="ExtraColumnTemplate"/>) a pod styl buněk
/// (<see cref="ExtraDataTemplate"/>); obě dostanou vybraný řádek jako DataContext.
/// </summary>
public partial class ColumnsTab : UserControl
{
    private ColumnsTabViewModel? _vm;
    private ColumnRowVM? _styleRow;
    private ColumnVisualStyle? _styleTarget;

    public ColumnsTab()
    {
        InitializeComponent();
        // Formulář zapisuje rovnou do kopie stylu vybraného sloupce; prvek se mění až po OK okna
        // jako jeden vratný krok, takže tady se příkaz jen provede a hodnoty se přenesou do řádku.
        StyleForm.SetContext(command =>
        {
            command.Execute();
            if (_styleRow is not null && _styleTarget is not null) _styleRow.ApplyStyle(_styleTarget);
        }, () => { });
        DataContextChanged += (_, _) => Attach(DataContext as ColumnsTabViewModel);
    }

    private void Attach(ColumnsTabViewModel? vm)
    {
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnVmChanged;
            _vm.StylesReset -= ShowStyleForm;
        }
        _vm = vm;
        if (_vm is null) return;
        _vm.PropertyChanged += OnVmChanged;
        _vm.StylesReset += ShowStyleForm;
        ShowStyleForm();
    }

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ColumnsTabViewModel.SelectedRow)) ShowStyleForm();
    }

    /// <summary>Naplní formulář stylem vybraného sloupce - při výběru jiného sloupce i po resetu.</summary>
    private void ShowStyleForm()
    {
        if (_vm is not { UsesStyleForm: true, SelectedRow: { } row }) return;
        _styleRow = row;
        _styleTarget = row.CreateStyleTarget();
        StyleForm.InheritedValues = row.InheritedStyleValues();
        StyleForm.ShowElement(_styleTarget, ColumnRowVM.StyleFormClass);
    }

    public static readonly DependencyProperty ExtraColumnTemplateProperty =
        DependencyProperty.Register(nameof(ExtraColumnTemplate), typeof(DataTemplate), typeof(ColumnsTab));

    /// <summary>Pole sloupce navíc pod záhlavím a šířkou (Pole, Výraz, Formát u Tabulky z dat).</summary>
    public DataTemplate? ExtraColumnTemplate
    {
        get => (DataTemplate?)GetValue(ExtraColumnTemplateProperty);
        set => SetValue(ExtraColumnTemplateProperty, value);
    }

    public static readonly DependencyProperty ExtraDataTemplateProperty =
        DependencyProperty.Register(nameof(ExtraDataTemplate), typeof(DataTemplate), typeof(ColumnsTab));

    /// <summary>Nastavení navíc pod stylem buněk (Střídání řádků skupiny dnů u Minut).</summary>
    public DataTemplate? ExtraDataTemplate
    {
        get => (DataTemplate?)GetValue(ExtraDataTemplateProperty);
        set => SetValue(ExtraDataTemplateProperty, value);
    }
}
