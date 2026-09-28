using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DragEventArgs = System.Windows.DragEventArgs;
using Point = System.Windows.Point;
using DataObject = System.Windows.DataObject;
using DragDropEffects = System.Windows.DragDropEffects;

namespace TemplateDesigner.Controls;

/// <summary>
/// Nákres uspořádání na list (`B186`) - přetahování stránek šablony do polí listu. Logika je ve
/// <see cref="UsporadaniListuViewModel"/>; tady jen myš a tažení.
/// </summary>
public partial class UsporadaniListuPanel : UserControl
{
    private const string FormatStranka = "TD.StrankaSablony";
    private const string FormatPole = "TD.PoleListu";

    private Point _start;
    private bool _tahne;

    /// <summary>Uložit uspořádání jako výchozí pro šablonu - napojí hostitel.</summary>
    public Action? UlozitDoSablony { get; set; }

    public UsporadaniListuPanel() => InitializeComponent();

    private UsporadaniListuViewModel? Vm => DataContext as UsporadaniListuViewModel;

    private void Stranka_MouseDown(object sender, MouseButtonEventArgs e) => _start = e.GetPosition(this);

    private void Stranka_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || !DostPohybu(e)) return;
        if (sender is FrameworkElement { Tag: StrankaSablonyVM stranka } zdroj)
            DragDrop.DoDragDrop(zdroj, new DataObject(FormatStranka, stranka.Index), DragDropEffects.Copy);
    }

    private void Pole_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _start = e.GetPosition(this);
        _tahne = false;
    }

    private void Pole_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || !DostPohybu(e)) return;
        if (sender is FrameworkElement { Tag: PoleListuVM { JePrazdne: false } pole } zdroj)
        {
            _tahne = true;
            DragDrop.DoDragDrop(zdroj, new DataObject(FormatPole, pole), DragDropEffects.Move);
        }
    }

    /// <summary>Klik bez tažení = nabídka stránek šablony pro to pole (cesta i bez myši-tažení).</summary>
    private void Pole_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_tahne || Vm is not { } vm || sender is not FrameworkElement { Tag: PoleListuVM pole } prvek) return;
        var menu = new ContextMenu();
        foreach (var stranka in vm.Stranky)
        {
            var item = new MenuItem { Header = stranka.Nazev };
            item.Click += (_, _) => vm.Umisti(pole.Zadni, pole.Index, stranka.Index);
            menu.Items.Add(item);
        }
        menu.Items.Add(new Separator());
        var prazdne = new MenuItem { Header = "Prázdné pole" };
        prazdne.Click += (_, _) => vm.Umisti(pole.Zadni, pole.Index, null);
        menu.Items.Add(prazdne);
        menu.PlacementTarget = prvek;
        menu.IsOpen = true;
    }

    private void Pole_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(FormatStranka) ? DragDropEffects.Copy
                  : e.Data.GetDataPresent(FormatPole) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void Pole_Drop(object sender, DragEventArgs e)
    {
        if (Vm is not { } vm || sender is not FrameworkElement { Tag: PoleListuVM cil }) return;
        if (e.Data.GetData(FormatStranka) is int stranka)
            vm.Umisti(cil.Zadni, cil.Index, stranka);
        else if (e.Data.GetData(FormatPole) is PoleListuVM zdroj)
            vm.Presun(zdroj.Zadni, zdroj.Index, cil.Zadni, cil.Index);
        e.Handled = true;
    }

    private void VycistitZadni_Click(object sender, RoutedEventArgs e) => Vm?.VycistiStranu(zadni: true);

    private void UlozitDoSablony_Click(object sender, RoutedEventArgs e) => UlozitDoSablony?.Invoke();

    private bool DostPohybu(MouseEventArgs e)
    {
        var p = e.GetPosition(this);
        return Math.Abs(p.X - _start.X) > SystemParameters.MinimumHorizontalDragDistance
            || Math.Abs(p.Y - _start.Y) > SystemParameters.MinimumVerticalDragDistance;
    }
}
