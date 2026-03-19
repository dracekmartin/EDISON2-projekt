using ALWA.SYS;
using EDControls.EDInputForm;
using EDControls.EDViewModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace EDControls.EDInputForm
{
    public partial class EDInputForm : UserControl
    {
        // ─── Configuration ────────────────────────────────────────────────────────

        private IMLRest? MLRest => (Window.GetWindow(this)?.DataContext as EDViewModel.EDViewModel)?.MLRest;

        /// <summary>Caché class that implements Init / Validate / Save.</summary>
        public string CacheClass { get; set; } = "";

        // ─── Items dependency property ────────────────────────────────────────────

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

        public EDInputForm()
        {
            InitializeComponent();
            Loaded += (_, _) => { if (!string.IsNullOrEmpty(CacheClass)) Init(); };
        }

        // ─── Public API ───────────────────────────────────────────────────────────

        /// <summary>
        /// Calls Caché Init to load the form definition, then immediately runs
        /// an Init-trigger validation pass to populate any default errors.
        /// </summary>
        public void Init()
        {
            EnsureConfigured();
            CVar output = MLRest!.QueryOrThrow(CacheClass, "Init", new CVar());
            var newItems = ParseItems(output);
            PreserveExpansionState(Items, newItems);
            Items = newItems;
            ExecuteValidate(EValidationTrigger.Init);
        }

        /// <summary>
        /// Runs a Change-trigger validation pass and returns true when there are
        /// no validation errors.
        /// </summary>
        public bool Validate() => ExecuteValidate(EValidationTrigger.Change);

        /// <summary>
        /// Validates with BeforeSave trigger and, if there are no errors, calls
        /// Caché Save.  Returns true on success.
        /// </summary>
        public bool Save()
        {
            if (!ExecuteValidate(EValidationTrigger.BeforeSave)) return false;

            CVar output = MLRest!.QueryOrThrow(CacheClass, "Save", SerializeItemsCVar(Items));
            Items = ParseItems(output);
            return true;
        }

        // ─── Serialization helpers ────────────────────────────────────────────────

        private static void PreserveExpansionState(IEnumerable<Item>? oldItems, List<Item> newItems)
        {
            if (oldItems == null) return;
            var expanded = oldItems.OfType<Group>().ToDictionary(g => g.GroupName, g => g.IsExpanded);
            foreach (var g in newItems.OfType<Group>())
                if (expanded.TryGetValue(g.GroupName, out bool was))
                    g.IsExpanded = was;
        }

        internal static CVar SerializeItemsCVar(IEnumerable<Item>? items)
        {
            var cVar = new CVar();
            int i = 1;
            foreach (var item in items ?? [])
            {
                CList entry = item switch
                {
                    Group g => new CList("GROUP", g.Number, g.GroupName, g.IsVisible ? "1" : "0"),
                    Field f => new CList(
                        "FIELD", f.Number, f.Key, f.Label,
                        f.FieldType == EFieldType.Number ? "NUMBER" : "TEXT",
                        f.Value ?? "",
                        f.ValidateOnChange ? "1" : "0",
                        f.Required ? "1" : "0"),
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
                if (kvp.Key == "ERR") continue;
                CList row = kvp.Value;
                string type = row.GetString(1);
                if (type == "GROUP")
                {
                    currentGroup = new Group
                    {
                        Number    = int.TryParse(row.GetString(2), out int gn) ? gn : 0,
                        GroupName = row.GetString(3),
                        IsVisible = row.Length < 4 || row.GetString(4) != "0",
                    };
                    items.Add(currentGroup);
                }
                else if (type == "FIELD")
                {
                    items.Add(new Field
                    {
                        Number           = int.TryParse(row.GetString(2), out int fn) ? fn : 0,
                        Key              = row.GetString(3),
                        Label            = row.GetString(4),
                        FieldType        = row.GetString(5) == "NUMBER" ? EFieldType.Number : EFieldType.Text,
                        Value            = row.GetString(6) == "" ? null : row.GetString(6),
                        ValidateOnChange = row.GetString(7) == "1",
                        Required         = row.GetString(8) == "1",
                        ValidationError  = row.Length >= 9 && row.GetString(9) != "" ? row.GetString(9) : null,
                        ParentGroup      = currentGroup,
                    });
                }
            }
            return items;
        }

        // ─── Keyboard navigation ──────────────────────────────────────────────────
        // Ctrl+digits: buffer digits for 600 ms, then navigate to the item whose
        // Number equals the accumulated value.  Ctrl+1,2 → item 12, etc.

        private string _navBuffer = "";
        private System.Windows.Threading.DispatcherTimer? _navTimer;

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);
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

        private void NavigateToNumber(int number)
        {
            var item = Items?.FirstOrDefault(i => i.Number == number);
            if (item == null) return;

            var container = TheItemsControl.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
            if (container == null) return;

            if (item is Field)
                FindVisualChild<TextBox>(container)?.Focus();
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

        // ─── Internal ─────────────────────────────────────────────────────────────

        private bool ExecuteValidate(EValidationTrigger trigger)
        {
            EnsureConfigured();
            var input = SerializeItemsCVar(Items);
            input["trigger"] = new CVar(trigger.ToString());

            CVar output = MLRest!.QueryOrThrow(CacheClass, "Validate", input);
            var newItems = ParseItems(output);
            PreserveExpansionState(Items, newItems);
            Items = newItems;
            return Items.OfType<Field>().All(f => !f.HasError);
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

            int autoValidateCount = e.NewValue is IEnumerable<Item> ni ? ni.OfType<Field>().Count(f => f.ValidateOnChange) : 0;
            Debug.WriteLine($"[EDInputForm] Items changed — subscribed to {autoValidateCount} auto-validate field(s)");
        }

        private void Field_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(Field.Value)) return;
            if (sender is not Field field) return;

            Debug.WriteLine($"[EDInputForm] '{field.Key}' Value changed. ValidateOnChange={field.ValidateOnChange}");

            if (!field.ValidateOnChange) return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                Debug.WriteLine("[EDInputForm] Auto-validating on Change");
                ExecuteValidate(EValidationTrigger.Change);
            }));
        }
    }
}
