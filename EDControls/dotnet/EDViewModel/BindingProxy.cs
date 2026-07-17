using System.Windows;

namespace EDControls.EDViewModel;

/// <summary>
/// Most pro binding mimo vizuální strom (např. <c>CollectionContainer</c> v
/// <c>CompositeCollection</c> nedědí DataContext). Deklaruje se v Resources —
/// Freezable dostane inheritance context, takže <c>Data="{Binding}"</c> vidí DataContext
/// elementu; konzument pak binduje <c>{Binding Data.X, Source={StaticResource ...}}</c>.
/// </summary>
public sealed class BindingProxy : Freezable
{
    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(Data), typeof(object), typeof(BindingProxy));

    public object? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    protected override Freezable CreateInstanceCore() => new BindingProxy();
}
