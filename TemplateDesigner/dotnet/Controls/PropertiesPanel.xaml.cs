using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SkiaSharp;
using TemplateDesigner.Commands;
using TemplateDesigner.Commands.Implementations;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Schema;

namespace TemplateDesigner.Controls;

public partial class PropertiesPanel : UserControl
{
    private Action<IDesignerCommand>? _execute;
    private Action?                   _invalidate;
    private TemplateElement?          _currentElement;
    private IReadOnlyList<PropertyDescriptor>? _currentDescriptors;
    private bool                      _hasVisibleWhen;

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
    /// Zobrazí libovolný seznam deskriptorů - umožňuje editaci i objektů,
    /// které nejsou TemplateElement (sekce, stránka apod.).
    /// </summary>
    public void ShowDescriptors(IReadOnlyList<PropertyDescriptor> descriptors)
    {
        _currentDescriptors = descriptors;
        _hasVisibleWhen = descriptors.Any(d => d.VisibleWhen is not null);

        _suppressChanges = true;
        ContentPanel.Children.Clear();
        _suppressChanges = false;

        if (descriptors.Count == 0) return;

        // Filtruj skryté (VisibleWhen() vrátí false). Null VisibleWhen = vždy viditelný.
        var visible = descriptors.Where(d => d.VisibleWhen is null || d.VisibleWhen()).ToList();
        if (visible.Count == 0) return;

        // Stable sort by group: skupiny v pořadí prvního výskytu, řádky uvnitř skupiny
        // zachovají původní pořadí. Brání duplikovaným hlavičkám když descriptory přicházejí
        // z různých helperů a deklarační pořadí groups není sjednocené.
        var groupOrder = new Dictionary<string, int>();
        for (int i = 0; i < visible.Count; i++)
            groupOrder.TryAdd(visible[i].Group, groupOrder.Count);

        var sorted = visible
            .Select((d, i) => (d, i))
            .OrderBy(x => groupOrder[x.d.Group])
            .ThenBy(x => x.i)
            .Select(x => x.d);

        string? currentGroup = null;
        foreach (var d in sorted)
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
            // TextWithPickerPropertyDescriptor a SuggestStringPropertyDescriptor musí být před StringPropertyDescriptor (jsou podtřídami / dřívější match)
            case TextWithPickerPropertyDescriptor tpd:  AddTextWithPickerRow(tpd); break;
            case SuggestStringPropertyDescriptor  ssd:  AddSuggestRow(ssd);        break;
            case StringPropertyDescriptor sd: AddStringRow(sd);  break;
            case FloatPropertyDescriptor  fd: AddFloatRow(fd);   break;
            case BoolPropertyDescriptor   bd: AddBoolRow(bd);    break;
            case ColorPropertyDescriptor  cd: AddColorRow(cd);   break;
            case EnumPropertyDescriptor   ed: AddEnumRow(ed);    break;
            case ButtonPropertyDescriptor bd: AddButtonRow(bd);  break;
        }
    }

    // ── Button ───────────────────────────────────────────────────────────

    private void AddButtonRow(ButtonPropertyDescriptor bd)
    {
        var btn = new Button
        {
            Content             = bd.ButtonLabel,
            Margin              = new Thickness(0, 3, 0, 3),
            Padding             = new Thickness(8, 4, 8, 4),
            FontSize            = 11,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch
        };
        btn.Click += (_, _) => bd.OnClick?.Invoke();
        ContentPanel.Children.Add(btn);
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

        if (sd.Validate is not null)
        {
            box.TextChanged += (_, _) => ApplyValidation(box, sd.Validate(box.Text));
        }

        panel.Children.Add(box);
        ContentPanel.Children.Add(panel);
    }

    // ── Suggest (editovatelný ComboBox s filtrováním) ─────────────────────

    private void AddSuggestRow(SuggestStringPropertyDescriptor sd)
    {
        var panel = MakeRow();

        var label = MakeLabel(sd.Label);
        DockPanel.SetDock(label, Dock.Left);
        panel.Children.Add(label);

        var allItems = sd.GetSuggestions().ToList();

        var combo = new ComboBox
        {
            IsEditable          = true,
            IsTextSearchEnabled = false,
            StaysOpenOnEdit     = true,
            Text                = sd.Get(),
            FontSize            = 11,
            Padding             = new Thickness(1, 1, 1, 1),
        };

        void Refilter(string text)
        {
            combo.Items.Clear();
            foreach (var (val, lbl) in allItems)
            {
                bool match = string.IsNullOrEmpty(text)
                    || val.Contains(text, StringComparison.OrdinalIgnoreCase)
                    || lbl.Contains(text, StringComparison.OrdinalIgnoreCase);
                if (!match) continue;
                var display = string.IsNullOrEmpty(lbl) || lbl == val ? val : $"{val}  -  {lbl}";
                combo.Items.Add(new ComboBoxItem { Content = display, Tag = val });
            }
        }

        Refilter("");

        // Refiltrujeme jen při stisku klávesy (ne při interní změně WPF způsobené výběrem položky).
        // Pokud bychom filtrovali i při SelectionChanged→TextChanged, Refilter() smaže Items
        // a SelectedItem se stane null - DropDownClosed pak nemá co číst.
        bool userIsTyping = false;
        combo.PreviewKeyDown += (_, _) => userIsTyping = true;
        combo.PreviewKeyUp   += (_, _) => userIsTyping = false;

        combo.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
            new TextChangedEventHandler((_, _) =>
            {
                if (_suppressChanges || !userIsTyping) return;
                Refilter(combo.Text);
                if (combo.Items.Count > 0) combo.IsDropDownOpen = true;
            }));

        // DropDownClosed: WPF nastavil Text na Content (display string) - přepíšeme na Tag (hodnotu).
        // SelectedItem je zachován, protože Refilter() se při výběru nespustil.
        combo.DropDownClosed += (_, _) =>
        {
            if (combo.SelectedItem is not ComboBoxItem item) return;
            var val = (string)item.Tag!;
            _suppressChanges = true;
            combo.SelectedItem = null;
            combo.Text = val;
            _suppressChanges = false;
        };

        string oldVal = sd.Get();
        combo.LostFocus += (_, _) =>
        {
            if (_suppressChanges) return;
            string newVal = combo.Text;
            if (newVal == sd.Get()) return;
            string captured = oldVal;
            Commit(sd.Label, captured, newVal, v => { sd.Set((string)v!); _invalidate?.Invoke(); });
            oldVal = newVal;
        };

        panel.Children.Add(combo);
        ContentPanel.Children.Add(panel);
    }

    // ── Text s picker tlačítkem + validace placeholderů ──────────────────

    private static readonly Regex PlaceholderRegex =
        new(@"\{(\w+)\}", RegexOptions.Compiled);

    private void AddTextWithPickerRow(TextWithPickerPropertyDescriptor tpd)
    {
        var panel = MakeRow();

        var label = MakeLabel(tpd.Label);
        DockPanel.SetDock(label, Dock.Left);
        panel.Children.Add(label);

        var box = new TextBox
        {
            Text        = tpd.Get(),
            AcceptsReturn   = tpd.MultiLine,
            TextWrapping    = tpd.MultiLine ? TextWrapping.Wrap : TextWrapping.NoWrap,
            Height          = tpd.MultiLine ? 54 : double.NaN,
            VerticalScrollBarVisibility = tpd.MultiLine
                ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled,
            FontSize  = 11,
            Padding   = new Thickness(3, 2, 3, 2),
            MaxLength = 2000
        };


        // Picker tlačítko - pouze pokud máme schéma
        if (tpd.AvailableFields is { Count: > 0 } fields)
        {
            var btn = new Button
            {
                Content  = "⊞",
                Width    = 24,
                FontSize = 12,
                Padding  = new Thickness(0),
                Margin   = new Thickness(2, 0, 0, 0),
                ToolTip  = "Vložit databázové pole"
            };
            DockPanel.SetDock(btn, Dock.Right);
            panel.Children.Add(btn);

            btn.Click += (_, _) =>
            {
                var menu = new ContextMenu();
                foreach (var field in fields)
                {
                    var item = new MenuItem { Header = $"{field.FieldName}  -  {field.Label}" };
                    item.Click += (_, _) =>
                    {
                        string oldText = tpd.Get();
                        string newText = oldText + $"{{{field.FieldName}}}";
                        Commit(tpd.Label, oldText, newText, v =>
                        {
                            tpd.Set((string)v!);
                            box.Text = (string)v!;
                            _invalidate?.Invoke();
                            UpdateFieldValidation(box.Text, fields, box);
                        });
                    };
                    menu.Items.Add(item);
                }
                menu.PlacementTarget = btn;
                menu.IsOpen = true;
            };
        }

        string oldVal = tpd.Get();
        box.LostFocus += (_, _) =>
        {
            if (_suppressChanges) return;
            string newVal = box.Text;
            if (newVal == tpd.Get()) return;
            string captured = oldVal;
            Commit(tpd.Label, captured, newVal, v => { tpd.Set((string)v!); _invalidate?.Invoke(); });
            oldVal = newVal;
        };

        box.TextChanged += (_, _) =>
        {
            if (_suppressChanges || tpd.AvailableFields is null) return;
            UpdateFieldValidation(box.Text, tpd.AvailableFields, box);
        };

        panel.Children.Add(box);
        ContentPanel.Children.Add(panel);

        if (tpd.AvailableFields is not null)
            UpdateFieldValidation(tpd.Get(), tpd.AvailableFields, box);
    }

    private static void UpdateFieldValidation(string text, IReadOnlyList<DataField> fields, TextBox box)
    {
        var known = fields.Select(f => f.FieldName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknown = PlaceholderRegex.Matches(text)
            .Select(m => m.Groups[1].Value)
            .Where(n => !known.Contains(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        string? error = unknown.Count == 0
            ? null
            : "Neznámá pole: " + string.Join(", ", unknown.Select(n => $"{{{n}}}"));
        ApplyValidation(box, error);
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
            if (fd.Unit == "mm") parsed = MmMath.Round(parsed);
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

        // Barevný čtvereček - klik otevře color dialog
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
            oldVal = newHex;
            Commit(cd.Label, captured, newHex, v =>
            {
                cd.Set((string)v!);
                UpdateSwatch(swatch, (string)v!);
                box.Text = (string)v!;
                _invalidate?.Invoke();
            });
        }

        box.LostFocus += (_, _) =>
        {
            if (_suppressChanges) return;
            string newVal = box.Text.Trim();
            if (newVal == cd.Get()) return;
            if (!newVal.StartsWith('#')) newVal = '#' + newVal;

            // Nesmysl (vymazáno, překlep) se nezapisuje - vrátí se poslední platná barva. Bez toho
            // se do šablony uložila hodnota, kterou pak neumí přečíst kreslení.
            if (!ColorHex.IsValid(newVal)) { box.Text = cd.Get(); return; }
            ApplyColor(newVal);
        };

        swatch.Click += (_, _) =>
        {
            var owner = Window.GetWindow(this);
            if (owner is null) return;
            var hex = ColorPickerHelper.PickColor(owner, cd.Get());
            if (hex is null) return;
            box.Text = hex;
            ApplyColor(hex);
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

        var combo = new ComboBox { FontSize = 11, MinWidth = 220 };
        foreach (var (optLabel, optValue) in ed.Options)
            combo.Items.Add(new ComboBoxItem { Content = optLabel, Tag = optValue });

        // Dropdown by se měl auto-fit širším než klade prostor pro klid celého optLabelu při otevření
        combo.Loaded += (_, _) =>
        {
            double max = 0;
            foreach (ComboBoxItem it in combo.Items)
                if (it.ActualWidth > max) max = it.ActualWidth;
            if (max > combo.ActualWidth - 30) // pokud labely širší než closed combobox
                combo.SetValue(ComboBox.MinWidthProperty, max + 40);
        };

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

    private static readonly SolidColorBrush ErrorBorderBrush =
        new(Color.FromRgb(0xCC, 0x00, 0x00));

    private static void ApplyValidation(TextBox box, string? error)
    {
        if (error is not null)
        {
            box.BorderBrush = ErrorBorderBrush;
            box.ToolTip = error;
        }
        else
        {
            box.ClearValue(TextBox.BorderBrushProperty);
            box.ToolTip = null;
        }
    }

    private void Commit(string label, object? oldVal, object? newVal, Action<object?> apply)
    {
        _execute?.Invoke(new SetPropertyCommand(label, oldVal, newVal, apply));

        // Pokud má panel deskriptory s VisibleWhen, rebuildni - změna jedné hodnoty
        // mohla ovlivnit viditelnost jiných řádků (např. HodinyOd/Do závisí na TimeAxisMode).
        if (_hasVisibleWhen && _currentDescriptors is not null)
            Dispatcher.BeginInvoke(new Action(() => ShowDescriptors(_currentDescriptors)));
    }

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
        var c = ColorHex.Parse(hex, SkiaSharp.SKColors.White);
        return new SolidColorBrush(Color.FromArgb(c.Alpha, c.Red, c.Green, c.Blue));
    }

    private static void UpdateSwatch(Button btn, string hex)
    {
        var brush = HexToBrush(hex);
        btn.Background = brush;
        if (btn.Content is Border b) b.Background = brush;
    }

}
