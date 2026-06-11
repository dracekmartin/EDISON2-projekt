using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace EDControls.EDInputForm;

/// <summary>
/// Editable ComboBox s live filtrováním pro Suggest field type. Uživatel může psát
/// libovolný text — suggestions jsou nápověda, ne striktní enum.
///
/// <para>
/// Wire-up: bind <see cref="Suggestions"/> na <c>Field.ListItems</c> a
/// <c>Text</c> na <c>Field.Value</c>. Custom string (mimo list) je akceptován.
/// </para>
///
/// <para>
/// Implementační poznámka — event ordering gotcha: WPF při výběru z dropdownu
/// posílá events v pořadí <c>SelectionChanged → Text=display → TextChanged → DropDownClosed</c>.
/// Pokud bychom filtrovali na každý TextChanged, smazali bychom <c>SelectedItem</c> než
/// <c>DropDownClosed</c> stihne přečíst hodnotu. Proto rozlišujeme „user-typed" změny
/// (přes <c>PreviewKeyDown/Up</c>) od WPF internal změn — detail viz
/// <c>.claude/rules/implementation-patterns.md</c> v TD repo.
/// </para>
/// </summary>
public class SuggestComboBox : ComboBox
{
    public static readonly DependencyProperty SuggestionsProperty = DependencyProperty.Register(
        nameof(Suggestions),
        typeof(IEnumerable<ListItem>),
        typeof(SuggestComboBox),
        new PropertyMetadata(null, OnSuggestionsChanged));

    /// <summary>Seznam doporučení (key + label). User může napsat i hodnotu mimo list.</summary>
    public IEnumerable<ListItem>? Suggestions
    {
        get => (IEnumerable<ListItem>?)GetValue(SuggestionsProperty);
        set => SetValue(SuggestionsProperty, value);
    }

    private bool _userIsTyping;
    private bool _suppressChanges;

    public SuggestComboBox()
    {
        IsEditable          = true;
        IsTextSearchEnabled = false;
        StaysOpenOnEdit     = true;

        PreviewKeyDown += (_, _) => _userIsTyping = true;
        PreviewKeyUp   += (_, _) => _userIsTyping = false;

        AddHandler(TextBoxBase.TextChangedEvent,
            new TextChangedEventHandler((_, _) =>
            {
                if (_suppressChanges || !_userIsTyping) return;
                Refilter(Text);
                if (Items.Count > 0) IsDropDownOpen = true;
            }));

        DropDownClosed += (_, _) =>
        {
            // User selected item → SelectedItem set, Text = display ("KEY  —  Label").
            // Override Text na pure Tag value (the key) — to je co Field.Value očekává.
            if (SelectedItem is not ComboBoxItem item) return;
            var val = (string)(item.Tag ?? "");
            _suppressChanges = true;
            SelectedItem = null;
            Text = val;
            _suppressChanges = false;
        };

        Loaded += (_, _) => Refilter(Text);
    }

    private void Refilter(string? text)
    {
        Items.Clear();
        if (Suggestions is null) return;
        foreach (var item in Suggestions)
        {
            bool match = string.IsNullOrEmpty(text)
                || item.Key.Contains(text, StringComparison.OrdinalIgnoreCase)
                || item.Label.Contains(text, StringComparison.OrdinalIgnoreCase);
            if (!match) continue;
            string display = string.IsNullOrEmpty(item.Label) || item.Label == item.Key
                ? item.Key
                : $"{item.Key}  —  {item.Label}";
            Items.Add(new ComboBoxItem { Content = display, Tag = item.Key });
        }
    }

    private static void OnSuggestionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SuggestComboBox c) c.Refilter(c.Text);
    }
}
