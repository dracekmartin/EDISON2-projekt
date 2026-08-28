using ALWA.SYS;
using EDControls.EDInputForm;
using EDControls.EDViewModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace EDControls.EDInputForm
{
    public partial class EDInputForm : UserControl
    {
        /// <summary>
        /// Datová pole nabízená tlačítkem „vložit pole" u textových polí s příznakem
        /// <see cref="Field.Placeholders"/>. **Čistě klientský stav** - server tenhle seznam nezná
        /// (nezná agendu šablony), takže po drátě NESMÍ cestovat: formulář chodí tam a zpět
        /// (Validate) a cokoli, co server neumí vrátit, by se cestou vyprázdnilo.
        /// </summary>
        public IReadOnlyList<ListItem> PlaceholderFields { get; set; } = [];

        // MLRest a PopupDisplayer ze sdíleného EDServices.Current - nezávislé na Window.DataContext.
        // Dialogy hostující EDInputForm tak nemusí mít VM dědící z EDViewModel jen kvůli těmto
        // službám (mohou mít vlastní VM se svými properties, nebo žádný VM).
        private IMLRest? MLRest => EDViewModel.EDServices.Current.MLRest;
        private EDViewModel.IPopupDisplayer? PopupDisplayer => EDViewModel.EDServices.Current.PopupDisplayer;

        /// <summary>Caché třída implementující <c>Init</c> / <c>Validate</c> / <c>Save</c>.
        /// Změna za běhu (např. inline panel přepne typ elementu) zahodí controller → příští
        /// <see cref="Init"/> ho vytvoří s novou třídou (jinak by reloadoval staré schema).</summary>
        public string CacheClass
        {
            get => _cacheClass;
            set
            {
                if (_cacheClass == value) return;
                _cacheClass = value;
                _controller = null;
            }
        }
        private string _cacheClass = "";

        private EDInputFormController? _controller;

        private EDInputFormController Controller
        {
            get
            {
                if (_controller == null)
                {
                    _controller = new EDInputFormController(
                        MLRest ?? throw new InvalidOperationException("EDInputForm: MLRest not available."),
                        CacheClass);
                    _controller.StateChanged += () =>
                    {
                        Items = _controller.Items;
                        HelpTextMode = _controller.HelpTextMode;
                    };
                }
                return _controller;
            }
        }

        public static readonly DependencyProperty ItemsProperty =
            DependencyProperty.Register(
                nameof(Items),
                typeof(IEnumerable<Item>),
                typeof(EDInputForm),
                new PropertyMetadata(null, OnItemsChanged));

        public IEnumerable<Item>? Items
        {
            get => (IEnumerable<Item>?)GetValue(ItemsProperty);
            set => SetValue(ItemsProperty, value);
        }

        public static readonly DependencyProperty HelpTextModeProperty =
            DependencyProperty.Register(
                nameof(HelpTextMode),
                typeof(HelpTextMode),
                typeof(EDInputForm),
                new PropertyMetadata(HelpTextMode.Tooltip));

        public HelpTextMode HelpTextMode
        {
            get => (HelpTextMode)GetValue(HelpTextModeProperty);
            set => SetValue(HelpTextModeProperty, value);
        }


        public EDInputForm()
        {
            InitializeComponent();
            Loaded += (_, _) =>
            {
                if (!string.IsNullOrEmpty(CacheClass)) Init();
                WireToolbarSearch();
            };
        }

        private readonly List<Field> _searchMatches = [];

        /// <summary>Stav rozbalení skupin před začátkem hledání - pro obnovení po smazání dotazu.</summary>
        private Dictionary<Group, bool>? _expandStateBeforeSearch;

        private void WireToolbarSearch()
        {
            var dpd = DependencyPropertyDescriptor.FromProperty(
                EDComponentToolbar.EDComponentToolbar.SearchTextProperty,
                typeof(EDComponentToolbar.EDComponentToolbar));
            dpd.AddValueChanged(Toolbar, (_, _) => ApplySearchFilter(Toolbar.SearchText));

            Toolbar.FindNextCommand = new RelayCommand(FindNext);
            Toolbar.FindPreviousCommand = new RelayCommand(FindPrevious);
            Toolbar.HelpCommand = new RelayCommand(ShowShortcutsHelp);
        }

        /// <summary>Jediný zdroj pravdy pro popup nápovědy - vypisují se jen reálně nadrátované zkratky.</summary>
        internal static readonly (string Keys, string Description)[] KeyboardShortcuts =
        [
            ("Ctrl+F", "Skok do vyhledávacího pole"),
            ("Enter", "Přechod na další pole (na tlačítku ho stiskne)"),
            ("Ctrl+Enter", "Potvrzení / uložení dialogu"),
            ("Ctrl+<číslo>", "Skok na položku podle jejího čísla (víceciferné do 200 ms)"),
        ];

        internal static string FormatShortcutsHelp()
        {
            var sb = new System.Text.StringBuilder("Klávesové zkratky:\n");
            foreach (var (keys, description) in KeyboardShortcuts)
                sb.Append('\n').Append(keys).Append("  -  ").Append(description);
            return sb.ToString();
        }

        private void ShowShortcutsHelp() => PopupDisplayer?.Info(FormatShortcutsHelp());

        private void ApplySearchFilter(string searchText)
        {
            _searchMatches.Clear();
            if (Items == null) return;

            var groups = Items.OfType<Group>().ToList();
            var fields = Items.OfType<Field>().ToList();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                foreach (var f in fields) f.IsFilteredBySearch = false;
                foreach (var g in groups) g.IsFilteredBySearch = false;
                // Obnovit stav rozbalení skupin z doby před hledáním.
                if (_expandStateBeforeSearch is not null)
                {
                    foreach (var kvp in _expandStateBeforeSearch)
                        kvp.Key.IsExpanded = kvp.Value;
                    _expandStateBeforeSearch = null;
                }
                Toolbar.TotalMatches = 0;
                Toolbar.CurrentMatchIndex = 0;
                return;
            }

            // Při prvním hledání si zapamatovat rozbalení skupin, ať ho po smazání dotazu obnovíme.
            _expandStateBeforeSearch ??= groups.ToDictionary(g => g, g => g.IsExpanded);

            _searchMatches.AddRange(ComputeSearchMatches(groups, fields, searchText));

            Toolbar.TotalMatches = _searchMatches.Count;
            Toolbar.CurrentMatchIndex = _searchMatches.Count > 0 ? 1 : 0;
            if (_searchMatches.Count > 0) ScrollFieldIntoView(_searchMatches[0]);
        }

        /// <summary>
        /// Nastaví <see cref="Item.IsFilteredBySearch"/> na polích i skupinách podle hledaného textu
        /// a vrátí nalezená pole. Pole projde, pokud matchne jeho label NEBO název jeho skupiny
        /// (pak ukážeme celou skupinu). Skupina je viditelná, pokud matchne její název nebo obsahuje
        /// aspoň jedno zobrazené pole; zobrazenou skupinu rozbalíme, ať jsou nalezená pole vidět.
        /// Case + diacritics insensitive ("agen" matchne "Ágen").
        /// </summary>
        internal static List<Field> ComputeSearchMatches(
            IReadOnlyList<Group> groups, IReadOnlyList<Field> fields, string searchText)
        {
            var matches = new List<Field>();
            var needle = NormalizeForSearch(searchText);

            foreach (var f in fields)
            {
                bool fieldMatches = NormalizeForSearch(f.Label ?? "").Contains(needle);
                bool groupMatches = f.ParentGroup is { } pg && NormalizeForSearch(pg.GroupName).Contains(needle);
                bool pass = fieldMatches || groupMatches;
                f.IsFilteredBySearch = !pass;
                if (pass) matches.Add(f);
            }

            foreach (var g in groups)
            {
                bool groupMatches = NormalizeForSearch(g.GroupName).Contains(needle);
                bool hasVisibleField = fields.Any(f => f.ParentGroup == g && !f.IsFilteredBySearch);
                bool show = groupMatches || hasVisibleField;
                g.IsFilteredBySearch = !show;
                if (show) g.IsExpanded = true;
            }

            return matches;
        }

        private static string NormalizeForSearch(string s) => SearchNormalization.NormalizeForSearch(s);

        private void FindNext()
        {
            if (_searchMatches.Count == 0) return;
            int next = Toolbar.CurrentMatchIndex >= _searchMatches.Count ? 1 : Toolbar.CurrentMatchIndex + 1;
            Toolbar.CurrentMatchIndex = next;
            ScrollFieldIntoView(_searchMatches[next - 1]);
        }

        private void FindPrevious()
        {
            if (_searchMatches.Count == 0) return;
            int prev = Toolbar.CurrentMatchIndex <= 1 ? _searchMatches.Count : Toolbar.CurrentMatchIndex - 1;
            Toolbar.CurrentMatchIndex = prev;
            ScrollFieldIntoView(_searchMatches[prev - 1]);
        }

        private void ScrollFieldIntoView(Field field)
        {
            var container = TheItemsControl.ItemContainerGenerator.ContainerFromItem(field) as FrameworkElement;
            container?.BringIntoView();
        }

        /// <summary>
        /// Volitelné initial parametry předané do Caché <c>Init</c> (čtené přes
        /// <c>^||ML("IN","key")</c>). Slouží pro pre-fill polí initial hodnotami
        /// z hostitelského contextu. Host nastaví před prvním Loaded; auto-init pak
        /// použije tyto params místo prázdné CVar.
        /// </summary>
        public CVar? InitParams { get; set; }

        /// <summary>
        /// Vyvoláno po dokončení <see cref="Init"/> (Items naplněny z Caché schema).
        /// Host může pre-fillnout Field.Value z vlastního kontextu (např. generický
        /// ElementEditDialog čte hodnoty z elementu přes reflexi) a pak zavolat
        /// <see cref="Validate"/> pro re-validaci s naplněnými hodnotami.
        /// </summary>
        public event Action? SchemaLoaded;

        /// <inheritdoc cref="SchemaLoaded"/>
        [Obsolete("Stíní FrameworkElement.Initialized - použij SchemaLoaded.")]
        public new event Action? Initialized;

        /// <summary>Klik na BUTTON pole - host dostane klíč pole a může spustit client-side akci
        /// (např. otevřít sub-dialog). Doplňuje server-side Validate (ValidateOnChange).</summary>
        public event Action<string>? ButtonClicked;

        /// <summary>Změna hodnoty libovolného pole (klíč pole) - host může aplikovat živě (např.
        /// inline properties panel zapisuje do elementu per změnu, ne až na OK). Nezávislé na
        /// ValidateOnChange (fire vždy při změně Field.Value).</summary>
        public event Action<string>? FieldChanged;

        /// <summary>
        /// Volá Caché <c>Init</c> pro načtení definice formuláře a hned poté Init-trigger
        /// validaci pro naplnění případných výchozích chyb. Použije <see cref="InitParams"/>
        /// pokud nastaveny. Po dokončení vyvolá <see cref="SchemaLoaded"/>.
        /// </summary>
        public void Init()
        {
            EnsureConfigured();
            Controller.Init(InitParams);
            SchemaLoaded?.Invoke();
#pragma warning disable CS0618 // deprekovaný alias - dokud existuje, musí fajrovat taky
            Initialized?.Invoke();
#pragma warning restore CS0618
        }

        /// <summary>Spustí Change-trigger validaci. Vrátí true pokud nejsou chyby.</summary>
        public bool Validate()
        {
            EnsureConfigured();
            return Controller.Validate();
        }

        /// <summary>
        /// Validuje s BeforeSave triggerem a pokud nejsou chyby, volá Caché <c>Save</c>.
        /// Vrátí true při úspěchu.
        /// </summary>
        public bool Save()
        {
            EnsureConfigured();
            bool result = Controller.Save();
            if (!result)
            {
                var message = EDInputFormController.FormatValidationErrors(Controller.Items);
                if (message != null)
                    PopupDisplayer?.Info(message);
            }
            return result;
        }

        // Tab / Shift+Tab: native WPF - skáče přímo mezi vnitřními controly.
        // Enter (bez Ctrl): posun na další control (jako Tab); na Button → click.
        // Ctrl+Enter: bublá na OkCommand (window-level InputBinding).
        // Ctrl+číslice: buffer 200 ms, pak navigace na položku podle Number.

        private string _navBuffer = "";
        private System.Windows.Threading.DispatcherTimer? _navTimer;

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            if (e.Key == Key.Return && (Keyboard.Modifiers & ModifierKeys.Control) == 0)
            {
                if (GetCurrentField() is { FieldType: FieldType.Button } btnField)
                {
                    if (TheItemsControl.ItemContainerGenerator.ContainerFromItem(btnField)
                            is FrameworkElement container)
                        FindVisualChild<System.Windows.Controls.Button>(container)?.RaiseEvent(
                            new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                }

                (Keyboard.FocusedElement as UIElement)?.MoveFocus(
                    new TraversalRequest(FocusNavigationDirection.Next));
                e.Handled = true;
                return;
            }

            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;

            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            int digit = key switch
            {
                Key.D0 or Key.NumPad0 => 0,
                Key.D1 or Key.NumPad1 => 1,
                Key.D2 or Key.NumPad2 => 2,
                Key.D3 or Key.NumPad3 => 3,
                Key.D4 or Key.NumPad4 => 4,
                Key.D5 or Key.NumPad5 => 5,
                Key.D6 or Key.NumPad6 => 6,
                Key.D7 or Key.NumPad7 => 7,
                Key.D8 or Key.NumPad8 => 8,
                Key.D9 or Key.NumPad9 => 9,
                _ => -1
            };

            if (digit == -1) return;

            _navBuffer += digit.ToString();
            e.Handled = true;

            _navTimer?.Stop();
            _navTimer = new System.Windows.Threading.DispatcherTimer
                { Interval = TimeSpan.FromMilliseconds(200) };
            _navTimer.Tick += (_, _) =>
            {
                _navTimer.Stop();
                if (int.TryParse(_navBuffer, out int number))
                    NavigateToNumber(number);
                _navBuffer = "";
            };
            _navTimer.Start();
        }

        private Field? GetCurrentField()
        {
            if (Keyboard.FocusedElement is not DependencyObject focused) return null;
            foreach (var field in Items?.OfType<Field>() ?? [])
            {
                if (TheItemsControl.ItemContainerGenerator.ContainerFromItem(field)
                        is FrameworkElement container && container.IsAncestorOf(focused))
                    return field;
            }
            return null;
        }

        private void FocusField(Field field)
        {
            if (TheItemsControl.ItemContainerGenerator.ContainerFromItem(field)
                    is not FrameworkElement container) return;
            switch (field.FieldType)
            {
                case FieldType.Date:
                    FindVisualChild<DatePicker>(container)?.Focus();
                    break;
                case FieldType.Bool:
                    FindVisualChild<CheckBox>(container)?.Focus();
                    break;
                case FieldType.List:
                    FindVisualChild<ComboBox>(container)?.Focus();
                    break;
                default:
                    FindVisualChild<TextBox>(container)?.Focus();
                    break;
            }
        }

        private void NavigateToNumber(int number)
        {
            var item = Items?.FirstOrDefault(i => i.Number == number);
            if (item == null) return;

            if (item is Field field)
            {
                if (field.ParentGroup is { IsExpanded: false } group)
                    group.IsExpanded = true;
                FocusField(field);
            }
            else if (item is Group g)
                g.IsExpanded = !g.IsExpanded;
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t) return t;
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        private void ButtonField_Click(object sender, RoutedEventArgs e)
        {
            if (((System.Windows.Controls.Button)sender).DataContext is not Field field) return;
            ButtonClicked?.Invoke(field.Key);                 // client-side akce (host)
            if (field.ValidateOnChange) Controller.Validate(field.Key);   // server-side akce
        }

        internal const string FolderModeMarker = "$FOLDER";

        /// <summary>
        /// Implementace color pickeru. Host (např. TemplateDesigner) musí nastavit
        /// - EDControls je WPF-only, nemá vestavěný color dialog.
        /// TD wire-up: <c>EDInputForm.PickColorImpl = (owner, hex) => ColorPickerHelper.PickColor(owner, hex);</c>
        /// Když null, klik na color picker button nic neudělá (user může psát hex ručně).
        /// </summary>
        public static Func<Window, string, string?>? PickColorImpl { get; set; }

        private void ColorField_PickClick(object sender, RoutedEventArgs e)
        {
            if (((System.Windows.Controls.Button)sender).DataContext is not Field field) return;
            if (!field.Editable) return;
            if (PickColorImpl is null) return; // No-op pokud host neregistroval

            var owner = Window.GetWindow(this);
            if (owner is null) return;

            string currentHex = field.Value ?? "#FF000000";
            string? newHex = PickColorImpl(owner, currentHex);
            if (!string.IsNullOrEmpty(newHex)) field.Value = newHex;
        }

        /// <summary>
        /// Implementace výběru obrázku. Host (TemplateDesigner) musí nastavit - EDControls
        /// nemá obrázkové zpracování (file dialog + downscale + Base64). Vrací Base64 string
        /// nového obrázku, nebo null pokud uživatel zrušil. Analogie <see cref="PickColorImpl"/>.
        /// Když null, tlačítko „Nahrát…" nic neudělá.
        /// </summary>
        public static Func<Window, string?>? PickImageImpl { get; set; }

        private void ImageField_PickClick(object sender, RoutedEventArgs e)
        {
            if (((System.Windows.Controls.Button)sender).DataContext is not Field field) return;
            if (!field.Editable) return;
            if (PickImageImpl is null) return; // No-op pokud host neregistroval

            var owner = Window.GetWindow(this);
            if (owner is null) return;

            string? base64 = PickImageImpl(owner);
            if (!string.IsNullOrEmpty(base64)) field.Value = base64;
        }

        private void ImageField_ClearClick(object sender, RoutedEventArgs e)
        {
            if (((System.Windows.Controls.Button)sender).DataContext is not Field field) return;
            if (!field.Editable) return;
            field.Value = "";
        }

        /// <summary>
        /// „Vložit pole" u textového pole s placeholdery: nabídne <see cref="PlaceholderFields"/>
        /// a vybraný token vloží **na pozici kurzoru** - hodnota bývá
        /// věta s tokeny uvnitř („Linka {CISLO_LINKY}: {POPIS_LINKY}"), ne jen samotný token,
        /// takže nahradit celou hodnotu by nestačilo.
        /// </summary>
        private void PlaceholderField_InsertClick(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button btn || btn.DataContext is not Field field) return;
            if (!field.Editable) return;
            if (PlaceholderFields.Count == 0)
            {
                EDViewModel.EDServices.Current.PopupDisplayer.Info(
                    "Není z čeho vybírat - tahle šablona nemá napojená data.");
                return;
            }

            var token = PlaceholderPicker.Pick(btn, PlaceholderFields);
            if (token is null) return;

            // Kurzor drží TextBox vlevo od tlačítka; bez něj (nemá fokus) se vkládá na konec.
            var box = FindTextBox(btn);
            string current = field.Value ?? "";
            int caret = box?.CaretIndex ?? current.Length;
            caret = Math.Clamp(caret, 0, current.Length);

            field.Value = current.Insert(caret, token);
            if (box is not null)
            {
                box.Text = field.Value;           // binding je na LostFocus, ať se to projeví hned
                box.CaretIndex = caret + token.Length;
                box.Focus();
            }
        }

        private static System.Windows.Controls.TextBox? FindTextBox(DependencyObject from)
        {
            if (VisualTreeHelper.GetParent(from) is not { } parent) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
                if (VisualTreeHelper.GetChild(parent, i) is System.Windows.Controls.TextBox tb) return tb;
            return null;
        }

        private void FileField_BrowseClick(object sender, RoutedEventArgs e)
        {
            if (((System.Windows.Controls.Button)sender).DataContext is not Field field) return;
            if (!field.Editable) return;

            string filter = field.FileFilter ?? "";

            if (filter == FolderModeMarker)
            {
                var dialog = new Microsoft.Win32.OpenFolderDialog { Title = field.Label };
                if (!string.IsNullOrEmpty(field.Value) && System.IO.Directory.Exists(field.Value))
                    dialog.InitialDirectory = field.Value;
                if (dialog.ShowDialog(Window.GetWindow(this)) == true)
                    field.Value = dialog.FolderName;
            }
            else
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = field.Label,
                    Filter = string.IsNullOrEmpty(filter) ? "Všechny soubory (*.*)|*.*" : filter
                };
                if (!string.IsNullOrEmpty(field.Value) && System.IO.File.Exists(field.Value))
                    dialog.FileName = field.Value;
                if (dialog.ShowDialog(Window.GetWindow(this)) == true)
                    field.Value = dialog.FileName;
            }

        }

        private void EnsureConfigured()
        {
            if (MLRest == null || string.IsNullOrEmpty(CacheClass))
                throw new InvalidOperationException("EDInputForm: CacheClass must be set before use.");
        }

        private static void OnItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not EDInputForm control) return;

            if (e.OldValue is IEnumerable<Item> oldItems)
                foreach (var field in oldItems.OfType<Field>())
                    field.PropertyChanged -= control.Field_PropertyChanged;

            if (e.NewValue is IEnumerable<Item> newItems)
                foreach (var field in newItems.OfType<Field>())
                    field.PropertyChanged += control.Field_PropertyChanged;

        }

        private void Field_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(Field.Value)) return;
            if (sender is not Field field) return;

            // Živý write-back pro host (inline panel) - nezávislé na server-side validaci.
            FieldChanged?.Invoke(field.Key);

            if (!field.ValidateOnChange) return;
            Dispatcher.BeginInvoke(new Action(() => Controller.Validate(field.Key)));
        }
    }
}
