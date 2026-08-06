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
///   vybere celý text (první napsaný znak ho přepíše → hledání jiné položky bez mazání), Enter potvrdí
///   i napsaný text (exaktní nebo jednoznačná shoda) a neplatný text se po opuštění vrátí na aktuální výběr.</item>
/// </list>
/// </summary>
/// <remarks>
/// Filtrujeme jen na změny textu vyvolané klávesou (<see cref="UIElement.PreviewKeyDownEvent"/> flag),
/// ne na každý <c>TextChanged</c>. WPF totiž při výběru z dropdownu pošle
/// <c>SelectionChanged → Text=display → TextChanged → DropDownClosed</c>; filtrování na onen interní
/// <c>TextChanged</c> by přestavělo <c>Items</c> a smazalo <see cref="Selector.SelectedItem"/> dřív,
/// než ho <c>DropDownClosed</c> stihne přečíst. Aby výběr přežil i případný takový Refilter (např. když
/// se příznak psaní zasekne), commit nečte <c>SelectedItem</c>, ale Key zachycený už v
/// <c>SelectionChanged</c> (<c>_committedKey</c>) — ten dorazí před osudným <c>TextChanged</c>.
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

    public static readonly DependencyProperty KeyLabelSeparatorProperty = DependencyProperty.Register(
        nameof(KeyLabelSeparator), typeof(string), typeof(EDFilterComboBox),
        new PropertyMetadata(EDFilterComboBoxLogic.DisplaySeparator, OnKeyLabelSeparatorChanged));

    /// <summary>Oddělovač „Key ↔ Label" v zobrazení. Default <see cref="EDFilterComboBoxLogic.DisplaySeparator"/>; lze přenastavit per instanci.</summary>
    public string KeyLabelSeparator
    {
        get => (string)GetValue(KeyLabelSeparatorProperty);
        set => SetValue(KeyLabelSeparatorProperty, value);
    }

    // ---- chování (event wiring) ------------------------------------------

    // True jen mezi PreviewKeyDown/Up uživatele → rozliší jeho psaní od interních změn Textu (viz remarks).
    private bool _userIsTyping;
    // Po dobu programové změny Textu potlačí filtrování (jinak by SetText spustil Refilter).
    private bool _suppressChanges;
    // True jen když aktuální Text napsal uživatel jako filtr. Klidová hodnota (commit, binding, výchozí
    // stav) filtr NEPŘEDSTAVUJE → šipka pak ukazuje celý seznam, ne jen položky shodné s hodnotou.
    private bool _textIsUserFilter;
    // Key vybraný v aktuální relaci dropdownu, zachycený v SelectionChanged (viz CommitSelection).
    private string? _committedKey;

    public EDFilterComboBox()
    {
        IsEditable          = true;
        IsTextSearchEnabled = false;   // vlastní filtrování místo prefixového autocomplete
        StaysOpenOnEdit     = true;

        PreviewKeyUp += (_, _) => _userIsTyping = false;

        // Výběr položky zachyť hned (přijde PŘED Text=display → TextChanged → DropDownClosed), aby
        // pick přežil i případný Refilter, který mezitím vyprázdní Items a zahodí SelectedItem.
        // Odstranění položek (Items.Clear → jen RemovedItems) ignorujeme → drží poslední skutečný výběr.
        SelectionChanged += (_, e) =>
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is ComboBoxItem item)
                _committedKey = (string)(item.Tag ?? "");
        };

        // Psaní uživatele → přefiltruj dropdown a otevři ho (jen pokud něco zbylo).
        AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler((_, _) =>
        {
            if (_suppressChanges || !_userIsTyping) return;
            _textIsUserFilter = true;
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

        // Zavření dropdownu = okamžik, kdy se výběr fixuje (viz CommitSelection). Reset _userIsTyping,
        // aby se ztracený PreviewKeyUp (zaseknutý příznak) nepřenášel do další interakce.
        DropDownClosed += (_, _) => { _userIsTyping = false; CommitSelection(); };

        // Otevření = nová relace výběru (zapomeň předchozí pick). Není-li v poli napsaný filtr (jen
        // klidová hodnota), ukaž celý seznam - jinak by šipka nabídla pouze právě nastavenou položku
        // a přepnout na jinou by šlo až po smazání textu. Při otevření psaním filtruje už TextChanged.
        DropDownOpened += (_, _) =>
        {
            _committedKey = null;
            if (!_textIsUserFilter) Refilter(null);
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

        // Počáteční zobrazení: celý seznam (výchozí hodnota není filtr), striktní navíc ukáže aktuální výběr.
        Loaded += (_, _) =>
        {
            Refilter(_textIsUserFilter ? Text : null);
            if (StrictSelection) ShowSelectedKeyText();
        };
    }

    /// <summary>
    /// Navigaci a commit si obsluhujeme sami. <c>ComboBox.OnPreviewKeyDown</c> značí šipky, Enter
    /// a Esc jako <c>Handled</c> už ve fázi class handlerů, tedy PŘED instance handlery - handler
    /// registrovaný v ctoru by se s otevřeným dropdownem nikdy nezavolal. Přepsání virtuální
    /// metody běží místo té jeho, takže máme první slovo.
    /// </summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        _userIsTyping = !IsNavigationKey(e.Key);
        if (HandleNavigationKey(e.Key)) { e.Handled = true; return; }
        base.OnPreviewKeyDown(e);
    }

    /// <summary>
    /// Šipky / Enter / Esc. Vrací true, když je klávesa spotřebovaná.
    /// Navigace jde vědomě přes <see cref="Selector.SelectedIndex"/>, ne přes interní zvýraznění
    /// WPF: pick tak zachytí <c>SelectionChanged</c> do <c>_committedKey</c> a položka se rozsvítí
    /// (<c>IsSelected</c>). <c>Text</c> se přepíše na display, ale <c>_userIsTyping</c> je u
    /// navigačních kláves false, takže <see cref="Refilter"/> neproběhne.
    /// </summary>
    internal bool HandleNavigationKey(Key key)
    {
        switch (key)
        {
            case Key.Down:
            case Key.Up:
                // Otevřít MUSÍ být dřív než výběr - DropDownOpened nuluje _committedKey a Refilter
                // při něm vyprázdní Items (→ SelectedIndex = -1), takže kotvu hledáme až potom.
                if (!IsDropDownOpen) IsDropDownOpen = true;
                var from = SelectedIndex >= 0 ? SelectedIndex : IndexOfKey(SelectedKey);
                var next = EDFilterComboBoxLogic.NextIndex(Items.Count, from, key == Key.Down ? 1 : -1);
                if (next >= 0 && next != SelectedIndex) SelectedIndex = next;
                return true;

            case Key.Enter:
                CommitTypedTextOnEnter();
                // Otevřený dropdown zavři → DropDownClosed fixuje výběr (CommitSelection).
                if (IsDropDownOpen) { IsDropDownOpen = false; return true; }
                return StrictSelection;

            case Key.Escape:
                _committedKey = null;   // commit pak revertne na aktuální SelectedKey
                if (IsDropDownOpen) { IsDropDownOpen = false; return true; }
                // Zavřené pole: revert napsaného textu hned (nečekat na ztrátu fokusu), ale klávesu
                // NEspotřebovat - jinak by Esc v dialogu přestal fungovat jako Storno.
                if (StrictSelection) ShowSelectedKeyText();
                return false;

            default:
                return false;
        }
    }

    // Enter po napsání textu bez picku z dropdownu: dohledej položku podle textu (exakt, jinak
    // jediný kandidát filtru), jinak by commit napsaný výběr revertnul. Text == display zachyceného
    // picku → pick platí (šipky Text přepisují), nedohledávat. Zavřený dropdown (šipkami vybraná
    // položka nebo žádný match při psaní) → commit ručně, DropDownClosed nepřijde.
    private void CommitTypedTextOnEnter()
    {
        if (!StrictSelection) return;
        if (Text != DisplayFor(_committedKey)
            && EDFilterComboBoxLogic.ResolveTyped(Suggestions, Text, KeyLabelSeparator) is { } typed)
            _committedKey = typed.Key;
        if (!IsDropDownOpen) CommitSelection();
    }

    // Navigační/commit klávesy nemají spustit filtrování: Text se při nich přepíše na vybranou
    // položku (→ TextChanged), a Refilter by přestavěl Items a zahodil kotvu navigace uprostřed
    // procházení. Znakové klávesy filtrují dál (viz _userIsTyping guard v TextChanged handleru).
    private static bool IsNavigationKey(Key k) =>
        k is Key.Up or Key.Down or Key.PageUp or Key.PageDown or Key.Enter or Key.Escape or Key.Tab;

    // Zafixuje výběr po zavření dropdownu. Commituje _committedKey zachycený v SelectionChanged (NE
    // aktuální SelectedItem — ten může být mezitím zahozen Refilterem, viz remarks třídy). Striktní:
    // vybraný Key → SelectedKey + čitelný klidový display. Volný: Text = holý Key (čte ho konzument
    // přes Text binding, např. EDInputForm Field.Value). Bez výběru ve striktním režimu = napsaný
    // neplatný text (nebo jen otevřít+zavřít) → revert na aktuální SelectedKey.
    private void CommitSelection()
    {
        if (_committedKey is { } key)
        {
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
    // Programově nastavený text je klidová hodnota, ne uživatelův filtr (viz _textIsUserFilter).
    private void SetText(string text)
    {
        _suppressChanges = true;
        if (SelectedItem != null) SelectedItem = null;
        Text = text;
        _textIsUserFilter = false;
        _suppressChanges = false;
    }

    // Index položky dropdownu s daným Key (Tag), -1 když tam není. Kotva pro šipky z klidového
    // stavu: navigace pak začíná u aktuálního výběru, ne od první položky seznamu.
    private int IndexOfKey(string? key)
    {
        if (string.IsNullOrEmpty(key)) return -1;
        for (int i = 0; i < Items.Count; i++)
            if (Items[i] is ComboBoxItem { Tag: string tag } && tag == key) return i;
        return -1;
    }

    // „Key — Label" pro daný Key dohledaný v aktuálních Suggestions; neznámý Key vrátí beze změny.
    private string DisplayFor(string? key)
    {
        if (string.IsNullOrEmpty(key)) return "";
        var item = Suggestions?.FirstOrDefault(i => i.Key == key);
        return item is null ? key : EDFilterComboBoxLogic.Display(item, KeyLabelSeparator);
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
                Items.Add(new ComboBoxItem { Content = EDFilterComboBoxLogic.Display(item, KeyLabelSeparator), Tag = item.Key });
    }

    // Změna SelectedKey zvenčí (binding) → obnov klidové zobrazení na novou hodnotu.
    private static void OnSelectedKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is EDFilterComboBox { StrictSelection: true } c) c.ShowSelectedKeyText();
    }

    // Změna separátoru: přestav zobrazení (Items i klidový display) novým oddělovačem.
    private static void OnKeyLabelSeparatorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not EDFilterComboBox c) return;
        c.Refilter(c._textIsUserFilter ? c.Text : null);
        if (c.StrictSelection) c.ShowSelectedKeyText();
    }

    // Změna Suggestions: naplní Items (celý seznam, pokud uživatel právě nepíše filtr);
    // striktní navíc obnoví klidový display (Label se mohl právě stát dostupným).
    private static void OnSuggestionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not EDFilterComboBox c) return;
        c.Refilter(c._textIsUserFilter ? c.Text : null);
        if (c.StrictSelection) c.ShowSelectedKeyText();
    }
}
