using System.Windows;
using System.Windows.Controls;
using TemplateDesigner.Models;

namespace TemplateDesigner.Controls;

/// <summary>Ikona závažnosti hlášky vpravo dole (`B178`).</summary>
public partial class HlaseniIkona : UserControl
{
    public static readonly DependencyProperty ZavaznostProperty = DependencyProperty.Register(
        nameof(Zavaznost), typeof(ZavaznostHlaseni), typeof(HlaseniIkona),
        new PropertyMetadata(ZavaznostHlaseni.Informace, (d, _) => ((HlaseniIkona)d).Ukaz()));

    public ZavaznostHlaseni Zavaznost
    {
        get => (ZavaznostHlaseni)GetValue(ZavaznostProperty);
        set => SetValue(ZavaznostProperty, value);
    }

    public HlaseniIkona()
    {
        InitializeComponent();
        Ukaz();
    }

    private void Ukaz()
    {
        Chyba.Visibility      = Zavaznost == ZavaznostHlaseni.Chyba      ? Visibility.Visible : Visibility.Collapsed;
        Upozorneni.Visibility = Zavaznost == ZavaznostHlaseni.Upozorneni ? Visibility.Visible : Visibility.Collapsed;
        Informace.Visibility  = Zavaznost == ZavaznostHlaseni.Informace  ? Visibility.Visible : Visibility.Collapsed;
    }
}
