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
        // ─── List wire format ────────────────────────────────────────────────────
        internal const char ListItemSep = '|';
        internal const char ListKeyValSep = '~';

        // ─── Configuration ────────────────────────────────────────────────────────

        // MLRest a PopupDisplayer ze sdíleného EDServices.Current — nezávislé na Window.DataContext.
        // Dialogy hostující EDInputForm tak nemusí mít VM dědící z EDViewModel jen kvůli těmto
        // službám (mohou mít vlastní VM se svými properties, nebo žádný VM).
        private IMLRest? MLRest => EDViewModel.EDServices.Current.MLRest;
        private EDViewModel.IPopupDisplayer? PopupDisplayer => EDViewModel.EDServices.Current.PopupDisplayer;

        /// <summary>Caché třída implementující <c>Init</c> / <c>Validate</c> / <c>Save</c>.</summary>
        public string CacheClass { get; set; } = "";

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

        // ─── Dependency properties ───────────────────────────────────────────────

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
                typeof(EHelpTextMode),
                typeof(EDInputForm),
                new PropertyMetadata(EHelpTextMode.Tooltip));

        public EHelpTextMode HelpTextMode
        {
            get => (EHelpTextMode)GetValue(HelpTextModeProperty);
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

        // ─── Search filter & navigation ──────────────────────────────────────────

        private readonly List<Field> _searchMatches = [];

        private void WireToolbarSearch()
        {
            // Listen to SearchText changes
            var dpd = DependencyPropertyDescriptor.FromProperty(
                EDComponentToolbar.EDComponentToolbar.SearchTextProperty,
                typeof(EDComponentToolbar.EDComponentToolbar));
            dpd.AddValueChanged(Toolbar, (_, _) => ApplySearchFilter(Toolbar.SearchText));

            // Wire find next/previous commands
            Toolbar.FindNextCommand = new RelayCommand(FindNext);
            Toolbar.FindPreviousCommand = new RelayCommand(FindPrevious);
        }

        private void ApplySearchFilter(string searchText)
        {
            _searchMatches.Clear();
            if (Items == null) return;

            if (string.IsNullOrWhiteSpace(searchText))
            {
                foreach (var f in Items.OfType<Field>())
                    f.IsFilteredBySearch = false;
                Toolbar.TotalMatches = 0;
                Toolbar.CurrentMatchIndex = 0;
                return;
            }

            // Case + diacritics insensitive: "agen" matches "Ágen", "fontu" matches "Fontů" atd.
            var needle = NormalizeForSearch(searchText);

            foreach (var f in Items.OfType<Field>())
            {
                bool matches = NormalizeForSearch(f.Label ?? "").Contains(needle);
                f.IsFilteredBySearch = !matches;
                if (matches) _searchMatches.Add(f);
            }

            Toolbar.TotalMatches = _searchMatches.Count;
            Toolbar.CurrentMatchIndex = _searchMatches.Count > 0 ? 1 : 0;
            if (_searchMatches.Count > 0) ScrollFieldIntoView(_searchMatches[0]);
        }

        /// <summary>Normalize string for diacritics-insensitive matching:
        /// "Pošta" → "posta". Lowercase + strip combining marks (NFD form).</summary>
        private static string NormalizeForSearch(string s)
        {
            var normalized = s.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder(normalized.Length);
            foreach (var c in normalized)
            {
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            return sb.ToString();
        }

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

        // ─── Public API ───────────────────────────────────────────────────────────

        /// <summary>
        /// Volitelné initial parametry předané do Caché <c>Init</c> (čtené přes
        /// <c>^||ML("IN","key")</c>). Slouží pro pre-fill polí initial hodnotami
        /// z hostitelského contextu. Host nastaví před prvním Loaded; auto-init pak
        /// použije tyto params místo prázdné CVar.
        /// </summary>
        public CVar? InitParams { get; set; }

        /// <summary>
        /// Volá Caché <c>Init</c> pro načtení definice formuláře a hned poté Init-trigger
        /// validaci pro naplnění případných výchozích chyb. Použije <see cref="InitParams"/>
        /// pokud nastaveny.
        /// </summary>
        public void Init()
        {
            EnsureConfigured();
            Controller.Init(InitParams);
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

        internal static CVar SerializeItemsCVar(IEnumerable<Item>? items)
        {
            var cVar = new CVar();
            int i = 1;
            foreach (var item in items ?? [])
            {
                CList entry = item switch
                {
                    Group g => new CList("GROUP", g.Number, g.Key, g.GroupName, g.Visibility.ToWire(), g.HelpText ?? ""),
                    Field f => new CList(
                        "FIELD", f.Number, f.Key, f.Label,
                        f.FieldType.ToWire(),
                        f.Value ?? "",
                        f.ValidateOnChange ? "1" : "0",
                        f.Required ? "1" : "0",
                        f.ValidationError ?? "",
                        f.Editable ? "1" : "0",
                        f.Visibility.ToWire(),
                        f.HelpText ?? "",
                        SerializeListItems(f.ListItems),
                        f.FileFilter ?? "",
                        f.Min?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
                        f.Max?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
                        f.Format ?? "",
                        f.Unit ?? ""),
                    _ => new CList()
                };
                cVar[i.ToString()] = entry;
                i++;
            }
            return cVar;
        }

        internal static List<Item> ParseItems(CVar output)
        {
            var items = new List<Item>();
            Group? currentGroup = null;
            foreach (KeyValuePair<string, CVar> kvp in output)
            {
                if (kvp.Key is "ERR" or "helpTextMode") continue;
                CList row = kvp.Value;
                string type = row.GetString(1);
                if (type == "GROUP")
                {
                    currentGroup = new Group
                    {
                        Number     = int.TryParse(row.GetString(2), out int gn) ? gn : 0,
                        Key        = row.GetString(3),
                        GroupName  = row.GetString(4),
                        Visibility = row.Length >= 5 ? EItemVisibilityWire.FromWire(row.GetString(5)) : EItemVisibility.Visible,
                        HelpText   = row.Length >= 6 && row.GetString(6) != "" ? row.GetString(6) : null,
                    };
                    items.Add(currentGroup);
                }
                else if (type == "FIELD")
                {
                    var field = new Field
                    {
                        Number           = int.TryParse(row.GetString(2), out int fn) ? fn : 0,
                        Key              = row.GetString(3),
                        Label            = row.GetString(4),
                        FieldType        = EFieldTypeWire.FromWire(row.GetString(5)),
                        Value            = row.GetString(6) == "" ? null : row.GetString(6),
                        ValidateOnChange = row.GetString(7) == "1",
                        Required         = row.GetString(8) == "1",
                        ValidationError  = row.Length >= 9 && row.GetString(9) != "" ? row.GetString(9) : null,
                        Editable         = row.Length < 10 || row.GetString(10) != "0",
                        Visibility       = row.Length >= 11 ? EItemVisibilityWire.FromWire(row.GetString(11)) : EItemVisibility.Visible,
                        HelpText         = row.Length >= 12 && row.GetString(12) != "" ? row.GetString(12) : null,
                        ParentGroup      = currentGroup,
                    };
                    // List + ComboBox sdílejí listItems storage v pozici 13
                    if ((field.FieldType == EFieldType.List || field.FieldType == EFieldType.ComboBox)
                        && row.Length >= 13)
                        field.ListItems = ParseListItems(row.GetString(13));
                    if (field.FieldType == EFieldType.File && row.Length >= 14)
                        field.FileFilter = row.GetString(14);
                    // Positions 15-18: Decimal/Number extras (min, max, format, unit)
                    if (row.Length >= 15 && decimal.TryParse(row.GetString(15),
                            System.Globalization.NumberStyles.Number,
                            System.Globalization.CultureInfo.InvariantCulture, out var minVal))
                        field.Min = minVal;
                    if (row.Length >= 16 && decimal.TryParse(row.GetString(16),
                            System.Globalization.NumberStyles.Number,
                            System.Globalization.CultureInfo.InvariantCulture, out var maxVal))
                        field.Max = maxVal;
                    if (row.Length >= 17 && row.GetString(17) != "")
                        field.Format = row.GetString(17);
                    if (row.Length >= 18 && row.GetString(18) != "")
                        field.Unit = row.GetString(18);
                    items.Add(field);
                }
            }
            return items;
        }

        internal static EHelpTextMode ParseHelpTextMode(CVar output)
        {
            string? wire = output["helpTextMode"]?.Value?.ToString();
            return EHelpTextModeWire.FromWire(wire);
        }

        // ─── List items serialization ────────────────────────────────────────────

        internal static string SerializeListItems(List<ListItem> items)
        {
            if (items.Count == 0) return "";
            return string.Join(ListItemSep.ToString(),
                items.Select(i => i.Key == i.Label ? i.Key : i.Key + ListKeyValSep + i.Label));
        }

        internal static List<ListItem> ParseListItems(string wire)
        {
            if (string.IsNullOrEmpty(wire)) return [];
            return wire.Split(ListItemSep)
                .Where(entry => entry.Length > 0)
                .Select(entry =>
                {
                    int sep = entry.IndexOf(ListKeyValSep);
                    return sep >= 0
                        ? new ListItem(entry[..sep], entry[(sep + 1)..])
                        : new ListItem(entry, entry);
                })
                .ToList();
        }

        // ─── Klávesová navigace ───────────────────────────────────────────────────
        // Tab / Shift+Tab: native WPF — skáče přímo mezi vnitřními controly.
        // Enter (bez Ctrl): posun na další control (jako Tab); na Button → click.
        // Ctrl+Enter: bublá na OkCommand (window-level InputBinding).
        // Ctrl+číslice: buffer 200 ms, pak navigace na položku podle Number.

        private string _navBuffer = "";
        private System.Windows.Threading.DispatcherTimer? _navTimer;

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            // Enter (bez Ctrl) → posun na další control, případně klik na Button
            if (e.Key == Key.Return && (Keyboard.Modifiers & ModifierKeys.Control) == 0)
            {
                // Pokud jsme na Button poli, klikni a pak skoč dál
                if (GetCurrentField() is { FieldType: EFieldType.Button } btnField)
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

            // Ctrl+digit buffering
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

        // ─── Navigation helpers ─────────────────────────────────────────────────

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
                case EFieldType.Date:
                    FindVisualChild<DatePicker>(container)?.Focus();
                    break;
                case EFieldType.Bool:
                    FindVisualChild<CheckBox>(container)?.Focus();
                    break;
                case EFieldType.List:
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
                // Pokud je rodičovská skupina sbalená, rozbal ji
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

        // ─── Button click ────────────────────────────────────────────────────────

        private void ButtonField_Click(object sender, RoutedEventArgs e)
        {
            if (((System.Windows.Controls.Button)sender).DataContext is not Field field) return;
            if (!field.ValidateOnChange) return;
            Controller.Validate(field.Key);
        }

        // ─── File browse ──────────────────────────────────────────────────────────

        internal const string FolderModeSentinel = "$FOLDER";

        /// <summary>
        /// Implementace color pickeru. Host (např. TemplateDesigner) musí nastavit
        /// — EDControls je WPF-only, nemá vestavěný color dialog.
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

        private void FileField_BrowseClick(object sender, RoutedEventArgs e)
        {
            if (((System.Windows.Controls.Button)sender).DataContext is not Field field) return;
            if (!field.Editable) return;

            string filter = field.FileFilter ?? "";

            if (filter == FolderModeSentinel)
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

        // ─── Internal ─────────────────────────────────────────────────────────────

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
            if (!field.ValidateOnChange) return;

            Dispatcher.BeginInvoke(new Action(() => Controller.Validate(field.Key)));
        }
    }
}
