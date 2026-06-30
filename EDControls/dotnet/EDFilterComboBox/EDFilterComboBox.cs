using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace EDControls.EDFilterComboBox;

/// <summary>
/// Editovatelný ComboBox, který za běhu filtruje rozbalený seznam podle psaného textu
/// (shoda na Key i Label, case-insensitive). Dva režimy:
/// <list type="bullet">
///   <item><b>volný</b> (<see cref="StrictSelection"/> = false, výchozí): hodnotou je <c>Text</c>;
///   uživatel smí napsat i text mimo <see cref="Suggestions"/> — ty jsou jen nápověda.</item>
///   <item><b>striktní</b> (<see cref="StrictSelection"/> = true): hodnotou je <see cref="SelectedKey"/>;
///   commituje se jen platná položka, v klidu se zobrazí čitelný „Key — Label", při vstupu do pole se
///   vybere celý text (první napsaný znak ho přepíše → hledání jiné položky bez mazání) a neplatný
///   text se po opuštění vrátí na aktuální výběr.</item>
/// </list>
/// </summary>
/// <remarks>
/// Filtrujeme jen na změny textu vyvolané klávesou (<see cref="UIElement.PreviewKeyDownEvent"/> flag),
/// ne na každý <c>TextChanged</c>. WPF totiž při výběru z dropdownu pošle
/// <c>SelectionChanged → Text=display → TextChanged → DropDownClosed</c>; filtrování na onen interní
/// <c>TextChanged</c> by přestavělo <c>Items</c> a smazalo <see cref="Selector.SelectedItem"/> dřív,
/// než ho <c>DropDownClosed</c> stihne přečíst.
/// </remarks>
public class EDFilterComboBox : ComboBox
{
    // ---- Dependency properties -------------------------------------------

    public static readonly DependencyProperty SuggestionsProperty = DependencyProperty.Register(
        nameof(Suggestions), typeof(IEnumerable<ListItem>), typeof(EDFilterComboBox),
        new PropertyMetadata(null, OnSuggestionsChanged));

    /// <summary>Nabízené položky (Key + Label). Ve volném režimu jsou jen nápověda.</summary>
    public IEnumerable<ListItem>? Suggestions
    {
        get => (IEnumerable<ListItem>?)GetValue(SuggestionsProperty);
        set => SetValue(SuggestionsProperty, value);
    }

    public static readonly DependencyProperty StrictSelectionProperty = DependencyProperty.Register(
        nameof(StrictSelection), typeof(bool), typeof(EDFilterComboBox), new PropertyMetadata(false));

    /// <summary>Režim výběru — viz dokumentace třídy. Default false = volný režim.</summary>
    public bool StrictSelection
    {
        get => (bool)GetValue(StrictSelectionProperty);
        set => SetValue(StrictSelectionProperty, value);
    }

    public static readonly DependencyProperty SelectedKeyProperty = DependencyProperty.Register(
        nameof(SelectedKey), typeof(string), typeof(EDFilterComboBox),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedKeyChanged));

    /// <summary>Vybraný Key ve striktním režimu (TwoWay). Ve volném režimu se nepoužívá.</summary>
    public string? SelectedKey
    {
        get => (string?)GetValue(SelectedKeyProperty);
        set => SetValue(SelectedKeyProperty, value);
    }

    // ---- chování (event wiring) ------------------------------------------

    // True jen mezi PreviewKeyDown/Up uživatele → rozliší jeho psaní od interních změn Textu (viz remarks).
    private bool _userIsTyping;
    // Po dobu programové změny Textu potlačí filtrování (jinak by SetText spustil Refilter).
    private bool _suppressChanges;

    public EDFilterComboBox()
    {
        IsEditable          = true;
        IsTextSearchEnabled = false;   // vlastní filtrování místo prefixového autocomplete
        StaysOpenOnEdit     = true;

        PreviewKeyDown += (_, _) => _userIsTyping = true;
        PreviewKeyUp   += (_, _) => _userIsTyping = false;

        // Psaní uživatele → přefiltruj dropdown a otevři ho (jen pokud něco zbylo).
        AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler((_, _) =>
        {
            if (_suppressChanges || !_userIsTyping) return;
            Refilter(Text);
            if (Items.Count > 0 && !IsDropDownOpen)
            {
                IsDropDownOpen = true;
                // Editovatelný ComboBox při otevření dropdownu vybere text v poli (default WPF) → další
                // znak by ho přepsal. Vrať kurzor na konec SYNCHRONNĚ (ne odloženě) — odložené vrácení
                // prohrávalo závod s rychlým psaním (druhý znak dorazil dřív a přepsal první).
                if (_editableTextBox is { } tb) { tb.SelectionLength = 0; tb.CaretIndex = tb.Text.Length; }
            }
        }));

        // Zavření dropdownu = okamžik, kdy se výběr fixuje (viz CommitSelection).
        DropDownClosed += (_, _) => CommitSelection();

        // Otevření BEZ napsaného filtru (text = klidový display) → ukaž celý seznam. Uživatel mohl
        // dřív psát a nevybrat → Items zůstaly filtrované; šipka má ukázat vše. Při otevření psaním
        // je text ≠ display, Items filtruje už TextChanged → neresetujeme.
        DropDownOpened += (_, _) =>
        {
            if (StrictSelection && Text == DisplayFor(SelectedKey)) Refilter(null);
        };

        // Select-all na fokus řeší OnApplyTemplate napojením na PART_EditableTextBox (synchronně,
        // s pohlcením prvního kliku) — odložený SelectAll na ComboBoxu se spouštěl až po prvním stisku
        // klávesy (první písmeno se "snědlo", druhé přepsalo).

        // Striktní režim: napsaný neplatný text se po OPUŠTĚNÍ controlu vrátí na aktuální výběr.
        // Odloženě + !IsKeyboardFocusWithin: revert až když fokus reálně opustil control.
        LostKeyboardFocus += (_, _) =>
        {
            if (!StrictSelection) return;
            Dispatcher.BeginInvoke(DispatcherPriority.Input,
                new Action(() => { if (!IsKeyboardFocusWithin && !IsDropDownOpen) ShowSelectedKeyText(); }));
        };

        // Počáteční zobrazení: striktní naplní seznam (pro šipku) + ukáže aktuální výběr, volný naplní dropdown.
        Loaded += (_, _) =>
        {
            Refilter(StrictSelection ? null : Text);
            if (StrictSelection) ShowSelectedKeyText();
        };
    }

    // Zafixuje výběr po zavření dropdownu. Striktní: vybraný Key → SelectedKey + čitelný klidový display.
    // Volný: Text = holý Key (čte ho konzument přes Text binding, např. EDInputForm Field.Value).
    // Bez vybrané položky ve striktním režimu = napsaný neplatný text → revert na aktuální SelectedKey.
    private void CommitSelection()
    {
        if (SelectedItem is ComboBoxItem item)
        {
            var key = (string)(item.Tag ?? "");
            if (StrictSelection)
            {
                SetCurrentValue(SelectedKeyProperty, key);   // → OnSelectedKeyChanged → ShowSelectedKeyText
                ShowSelectedKeyText();                        // i když se Key nezměnil (rollback konzumenta)
            }
            else
            {
                SetText(key);
            }
        }
        else if (StrictSelection)
        {
            ShowSelectedKeyText();
        }
    }

    // ---- zobrazení & filtrování ------------------------------------------

    // Klidové zobrazení striktního režimu: čitelný „Key — Label" aktuálního SelectedKey.
    // Kurzor na konec — jinak po revertu (klik mimo) zůstane blikat na začátku pole.
    private void ShowSelectedKeyText()
    {
        SetText(DisplayFor(SelectedKey));
        if (_editableTextBox is { } tb) tb.CaretIndex = tb.Text.Length;
    }

    private TextBox? _editableTextBox;

    // Napojí select-all-on-focus přímo na editovatelný textbox šablony. Synchronně (ne odloženě),
    // jinak by se SelectAll spustil až po prvním stisku klávesy a "snědl" první písmeno.
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (_editableTextBox != null)
        {
            _editableTextBox.GotKeyboardFocus -= OnEditableGotFocus;
            _editableTextBox.PreviewMouseLeftButtonDown -= OnEditablePreviewMouseDown;
        }
        _editableTextBox = GetTemplateChild("PART_EditableTextBox") as TextBox;
        if (_editableTextBox != null)
        {
            _editableTextBox.GotKeyboardFocus += OnEditableGotFocus;
            _editableTextBox.PreviewMouseLeftButtonDown += OnEditablePreviewMouseDown;
        }
    }

    // Tab / programový fokus → vyber celý text (první znak ho přepíše).
    private void OnEditableGotFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (StrictSelection) _editableTextBox?.SelectAll();
    }

    // První klik (pole ještě nemá fokus) → zaostři + vyber vše + pohlť klik, aby kurzor výběr nezrušil.
    // Další kliky (už zaostřeno) necháme být → uživatel může umístit kurzor.
    private void OnEditablePreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (StrictSelection && _editableTextBox is { IsKeyboardFocusWithin: false } tb)
        {
            tb.Focus();
            tb.SelectAll();
            e.Handled = true;
        }
    }

    // Nastaví Text bez spuštění filtrování; SelectedItem=null → Text je jediný zdroj pravdy zobrazení.
    private void SetText(string text)
    {
        _suppressChanges = true;
        if (SelectedItem != null) SelectedItem = null;
        Text = text;
        _suppressChanges = false;
    }

    // „Key — Label" pro daný Key dohledaný v aktuálních Suggestions; neznámý Key vrátí beze změny.
    private string DisplayFor(string? key)
    {
        if (string.IsNullOrEmpty(key)) return "";
        var item = Suggestions?.FirstOrDefault(i => i.Key == key);
        return item is null ? key : EDFilterComboBoxLogic.Display(item);
    }

    // Přestaví obsah dropdownu na položky odpovídající psanému textu (prázdný text = vše).
    // Každá položka je ComboBoxItem: Content = čitelný „Key — Label", ale Tag nese čistý Key —
    // CommitSelection tak přečte vybraný Key nezávisle na zobrazeném textu.
    private void Refilter(string? text)
    {
        Items.Clear();
        if (Suggestions is null) return;
        foreach (var item in Suggestions)
            if (EDFilterComboBoxLogic.Matches(item, text))
                Items.Add(new ComboBoxItem { Content = EDFilterComboBoxLogic.Display(item), Tag = item.Key });
    }

    // Změna SelectedKey zvenčí (binding) → obnov klidové zobrazení na novou hodnotu.
    private static void OnSelectedKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is EDFilterComboBox { StrictSelection: true } c) c.ShowSelectedKeyText();
    }

    // Změna Suggestions: naplní Items (striktní = celý seznam pro šipku, volný = dle textu);
    // striktní navíc obnoví klidový display (Label se mohl právě stát dostupným).
    private static void OnSuggestionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not EDFilterComboBox c) return;
        c.Refilter(c.StrictSelection ? null : c.Text);
        if (c.StrictSelection) c.ShowSelectedKeyText();
    }
}
