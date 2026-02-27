using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SkiaSharp;
using TemplateDesigner.Commands;
using TemplateDesigner.Commands.Implementations;
using TemplateDesigner.Models;

namespace TemplateDesigner.Controls;

public partial class PropertiesPanel : UserControl
{
    private Action<IDesignerCommand>? _execute;
    private Action?                   _invalidate;
    private TemplateElement?          _currentElement;

    // Příznak zabraňující vzniku commandů při programatickém přestavění panelu
    private bool _suppressChanges;

    public PropertiesPanel() => InitializeComponent();

    /// <summary>Nastaví callback pro vykonání commandu a invalidaci canvasu.</summary>
    public void SetContext(Action<IDesignerCommand> execute, Action invalidate)
    {
        _execute    = execute;
        _invalidate = invalidate;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Veřejné metody volané z MainWindow

    public void ShowElement(TemplateElement element)
    {
        _currentElement = element;
        ShowDescriptors(element.GetPropertyDescriptors());
    }

    /// <summary>
    /// Zobrazí libovolný seznam deskriptorů — umožňuje editaci i objektů,
    /// které nejsou TemplateElement (sekce, stránka apod.).
    /// </summary>
    public void ShowDescriptors(IReadOnlyList<PropertyDescriptor> descriptors)
    {
        _suppressChanges = true;
        ContentPanel.Children.Clear();
        _suppressChanges = false;

        if (descriptors.Count == 0) return;

        string? currentGroup = null;
        foreach (var d in descriptors)
        {
            if (d.Group != currentGroup)
            {
                AddGroupHeader(d.Group);
                currentGroup = d.Group;
            }
            AddRow(d);
        }
    }

    public void ShowMessage(string message)
    {
        _suppressChanges = true;
        _currentElement  = null;
        ContentPanel.Children.Clear();
        _suppressChanges = false;

        ContentPanel.Children.Add(new TextBlock
        {
            Text       = message,
            Foreground = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)),
            Margin     = new Thickness(0, 6, 0, 0),
            FontSize   = 11
        });
    }

    // ─────────────────────────────────────────────────────────────────────
    // Stavba UI

    private void AddGroupHeader(string name)
    {
        ContentPanel.Children.Add(new TextBlock
        {
            Text       = name,
            FontWeight = FontWeights.SemiBold,
            FontSize   = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0x21, 0x96, 0xF3)),
            Margin     = new Thickness(0, 8, 0, 2)
        });
    }

    private void AddRow(PropertyDescriptor d)
    {
        switch (d)
        {
            case StringPropertyDescriptor sd: AddStringRow(sd);  break;
            case FloatPropertyDescriptor  fd: AddFloatRow(fd);   break;
            case BoolPropertyDescriptor   bd: AddBoolRow(bd);    break;
            case ColorPropertyDescriptor  cd: AddColorRow(cd);   break;
            case EnumPropertyDescriptor   ed: AddEnumRow(ed);    break;
        }
    }

    // ── String ───────────────────────────────────────────────────────────

    private void AddStringRow(StringPropertyDescriptor sd)
    {
        var panel = MakeRow();

        var label = MakeLabel(sd.Label);
        DockPanel.SetDock(label, Dock.Left);
        panel.Children.Add(label);

        var box = new TextBox
        {
            Text         = sd.Get(),
            AcceptsReturn   = sd.MultiLine,
            TextWrapping    = sd.MultiLine ? TextWrapping.Wrap : TextWrapping.NoWrap,
            Height          = sd.MultiLine ? 54 : double.NaN,
            VerticalScrollBarVisibility = sd.MultiLine
                ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled,
            FontSize = 11,
            Padding  = new Thickness(3, 2, 3, 2),
            MaxLength = 2000
        };

        string oldVal = sd.Get();
        box.LostFocus += (_, _) =>
        {
            if (_suppressChanges) return;
            string newVal = box.Text;
            if (newVal == sd.Get()) return;
            string captured = oldVal;
            Commit(sd.Label, captured, newVal, v => { sd.Set((string)v!); _invalidate?.Invoke(); });
            oldVal = newVal;
        };

        panel.Children.Add(box);
        ContentPanel.Children.Add(panel);
    }

    // ── Float ────────────────────────────────────────────────────────────

    private void AddFloatRow(FloatPropertyDescriptor fd)
    {
        var panel = MakeRow();

        var label = MakeLabel(fd.Label);
        DockPanel.SetDock(label, Dock.Left);
        panel.Children.Add(label);

        if (!string.IsNullOrEmpty(fd.Unit))
        {
            var unit = new TextBlock
            {
                Text              = fd.Unit,
                Width             = 28,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground        = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
                FontSize          = 11,
                Margin            = new Thickness(3, 0, 0, 0)
            };
            DockPanel.SetDock(unit, Dock.Right);
            panel.Children.Add(unit);
        }

        var box = new TextBox
        {
            Text     = fd.Get().ToString(fd.Format),
            FontSize = 11,
            Padding  = new Thickness(3, 2, 3, 2)
        };

        float oldVal = fd.Get();
        box.LostFocus += (_, _) =>
        {
            if (_suppressChanges) return;
            if (!float.TryParse(box.Text.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
            {
                box.Text = fd.Get().ToString(fd.Format);
                return;
            }
            parsed = Math.Clamp(parsed, fd.Min, fd.Max);
            if (Math.Abs(parsed - fd.Get()) < 0.0001f) return;
            float captured = oldVal;
            Commit(fd.Label, captured, parsed, v => { fd.Set((float)v!); _invalidate?.Invoke(); });
            oldVal    = parsed;
            box.Text  = parsed.ToString(fd.Format);
        };

        panel.Children.Add(box);
        ContentPanel.Children.Add(panel);
    }

    // ── Bool ─────────────────────────────────────────────────────────────

    private void AddBoolRow(BoolPropertyDescriptor bd)
    {
        var cb = new CheckBox
        {
            Content   = bd.Label,
            IsChecked = bd.Get(),
            FontSize  = 11,
            Margin    = new Thickness(90, 2, 0, 2)
        };

        cb.Click += (_, _) =>
        {
            if (_suppressChanges) return;
            bool newVal = cb.IsChecked == true;
            bool oldVal = !newVal;
            Commit(bd.Label, oldVal, newVal, v => { bd.Set((bool)v!); _invalidate?.Invoke(); });
        };

        ContentPanel.Children.Add(cb);
    }

    // ── Color ────────────────────────────────────────────────────────────

    private void AddColorRow(ColorPropertyDescriptor cd)
    {
        var panel = MakeRow();

        var label = MakeLabel(cd.Label);
        DockPanel.SetDock(label, Dock.Left);
        panel.Children.Add(label);

        // Barevný čtvereček — klik otevře color dialog
        var swatch = new Button
        {
            Width      = 22,
            Height     = 22,
            Padding    = new Thickness(2),
            Margin     = new Thickness(0, 0, 4, 0),
            Background = HexToBrush(cd.Get()),
            ToolTip    = "Vybrat barvu",
            Content    = new Border
            {
                Background = HexToBrush(cd.Get()),
                Width = 14, Height = 14,
                CornerRadius = new CornerRadius(2)
            }
        };
        DockPanel.SetDock(swatch, Dock.Left);
        panel.Children.Add(swatch);

        var box = new TextBox
        {
            Text       = cd.Get(),
            FontFamily = new FontFamily("Consolas"),
            FontSize   = 11,
            Padding    = new Thickness(3, 2, 3, 2)
        };

        string oldVal = cd.Get();

        void ApplyColor(string newHex)
        {
            string captured = oldVal;
            Commit(cd.Label, captured, newHex, v =>
            {
                cd.Set((string)v!);
                UpdateSwatch(swatch, (string)v!);
                box.Text = (string)v!;
                _invalidate?.Invoke();
            });
            oldVal = newHex;
            UpdateSwatch(swatch, newHex);
        }

        box.LostFocus += (_, _) =>
        {
            if (_suppressChanges) return;
            string newVal = box.Text.Trim();
            if (newVal == cd.Get()) return;
            if (!newVal.StartsWith('#')) newVal = '#' + newVal;
            if (newVal.Length < 7) { box.Text = cd.Get(); return; }
            ApplyColor(newVal);
        };

        swatch.Click += (_, _) =>
        {
            var dlg = new System.Windows.Forms.ColorDialog { FullOpen = true, AnyColor = true };
            try
            {
                var c = SKColor.Parse(cd.Get());
                dlg.Color = System.Drawing.Color.FromArgb(c.Red, c.Green, c.Blue);
            }
            catch { /* ignore */ }

            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var c      = dlg.Color;
                string hex = $"#FF{c.R:X2}{c.G:X2}{c.B:X2}";
                box.Text   = hex;
                ApplyColor(hex);
            }
        };

        panel.Children.Add(box);
        ContentPanel.Children.Add(panel);
    }

    // ── Enum ─────────────────────────────────────────────────────────────

    private void AddEnumRow(EnumPropertyDescriptor ed)
    {
        var panel = MakeRow();

        var label = MakeLabel(ed.Label);
        DockPanel.SetDock(label, Dock.Left);
        panel.Children.Add(label);

        var combo = new ComboBox { FontSize = 11 };
        foreach (var (optLabel, optValue) in ed.Options)
            combo.Items.Add(new ComboBoxItem { Content = optLabel, Tag = optValue });

        // Nastav aktuální hodnotu PŘED přidáním handleru
        object current = ed.Get();
        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (Equals(((ComboBoxItem)combo.Items[i]).Tag, current))
            { combo.SelectedIndex = i; break; }
        }

        combo.SelectionChanged += (_, _) =>
        {
            if (_suppressChanges) return;
            if (combo.SelectedItem is not ComboBoxItem item) return;
            object newVal = item.Tag!;
            object oldVal = ed.Get();
            if (Equals(newVal, oldVal)) return;
            Commit(ed.Label, oldVal, newVal, v => { ed.Set(v!); _invalidate?.Invoke(); });
        };

        panel.Children.Add(combo);
        ContentPanel.Children.Add(panel);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Helpers

    private void Commit(string label, object? oldVal, object? newVal, Action<object?> apply)
        => _execute?.Invoke(new SetPropertyCommand(label, oldVal, newVal, apply));

    private static DockPanel MakeRow() => new() { Margin = new Thickness(0, 1, 0, 1), LastChildFill = true };

    private static TextBlock MakeLabel(string text) => new()
    {
        Text              = text + ":",
        Width             = 90,
        VerticalAlignment = VerticalAlignment.Center,
        Foreground        = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
        FontSize          = 11,
        Margin            = new Thickness(0, 0, 4, 0)
    };

    private static SolidColorBrush HexToBrush(string hex)
    {
        try
        {
            var c = SKColor.Parse(hex);
            return new SolidColorBrush(Color.FromArgb(c.Alpha, c.Red, c.Green, c.Blue));
        }
        catch { return new SolidColorBrush(Colors.White); }
    }

    private static void UpdateSwatch(Button btn, string hex)
    {
        var brush = HexToBrush(hex);
        btn.Background = brush;
        if (btn.Content is Border b) b.Background = brush;
    }
}
