using System;
using System.Windows;
using System.Windows.Controls;

namespace EDControls.EDFilter;

/// <summary>
/// Abstraktní základ filter polí v <see cref="EDFilter"/>: potomek implementuje
/// <see cref="GetValue"/> a při změně vystřelí <see cref="ValueChanged"/>.
///
/// Konvence: <see cref="Key"/> odpovídá Caché IN filter sub-subscriptu -
/// <c>^||ML("IN","filter",Key) = GetValue()</c>. Prázdná hodnota → klíč se neposílá.
/// </summary>
public abstract class EDFilterFieldBase : UserControl
{
    /// <summary>Klíč pod kterým hodnota poletí do Caché v <c>^||ML("IN","filter",Key)</c>.</summary>
    public static readonly DependencyProperty KeyProperty =
        DependencyProperty.Register(nameof(Key), typeof(string), typeof(EDFilterFieldBase),
            new PropertyMetadata(""));

    public string Key
    {
        get => (string)GetValue(KeyProperty);
        set => SetValue(KeyProperty, value);
    }

    /// <summary>Vizuální popisek nad/vedle pole.</summary>
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(EDFilterFieldBase),
            new PropertyMetadata(null));

    public string? Label
    {
        get => (string?)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Vrátí aktuální hodnotu pole jako string (prázdné = filter klíč se neposílá).</summary>
    public abstract string GetValue();

    /// <summary>Vyvoláno při změně hodnoty pole. EDFilter ho subscribuje a trigger reload.</summary>
    public event EventHandler? ValueChanged;

    protected void RaiseValueChanged() => ValueChanged?.Invoke(this, EventArgs.Empty);
}
