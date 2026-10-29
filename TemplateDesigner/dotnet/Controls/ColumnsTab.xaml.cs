using System.Windows;
using System.Windows.Controls;

namespace TemplateDesigner.Controls;

/// <summary>
/// Záložka Sloupce sdílená okny všech tabulek se sloupci (Seznam zastávek, Minuty odjezdů, Tabulka
/// spojů, Tabulka z dat). DataContext je <see cref="ColumnsTabViewModel"/>. Okno prvku do ní dodá
/// jen pole navíc - šablonu pod záhlaví a šířku (<see cref="ExtraColumnTemplate"/>) a pod styl buněk
/// (<see cref="ExtraDataTemplate"/>); obě dostanou vybraný řádek jako DataContext.
/// </summary>
public partial class ColumnsTab : UserControl
{
    public ColumnsTab() => InitializeComponent();

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
