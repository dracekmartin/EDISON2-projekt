using ALWA.SYS;
using EDControls.EDInputForm;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace EDControls.EDInputForm
{
    public partial class EDInputForm : UserControl
    {
        // ─── Configuration ────────────────────────────────────────────────────────

        /// <summary>MLRest instance used for all Caché calls.</summary>
        public MLRest? MLRest { get; set; }

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
            Items = ParseItems(output);
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

        /// <summary>
        /// Serializes form items to a CVar for sending to Caché.
        /// Each item becomes a CList at key "1", "2", etc.:
        ///   GROUP → $lb("GROUP", groupName)
        ///   FIELD → $lb("FIELD", key, label, type, value, validateOnChange, required)
        /// Caché reads ^||ML("IN","1"), ^||ML("IN","2"), ...
        /// </summary>
        private static CVar SerializeItemsCVar(IEnumerable<Item>? items)
        {
            var cVar = new CVar();
            int i = 1;
            foreach (var item in items ?? [])
            {
                CList entry = item switch
                {
                    Group g => new CList("GROUP", g.GroupName),
                    Field f => new CList(
                        "FIELD", f.Key, f.Label,
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

        /// <summary>
        /// Parses Caché output CVar to a list of form items.
        /// Caché writes ^||ML("OUT","1"), ^||ML("OUT","2"), ...
        /// Each value is a CList:
        ///   GROUP → $lb("GROUP", groupName)
        ///   FIELD → $lb("FIELD", key, label, type, value, validateOnChange, required [, validationError])
        /// </summary>
        private static List<Item> ParseItems(CVar output)
        {
            var items = new List<Item>();
            foreach (KeyValuePair<string, CVar> kvp in output)
            {
                if (kvp.Key == "ERR") continue;
                CList row = kvp.Value;
                string type = row.GetString(1);
                if (type == "GROUP")
                {
                    items.Add(new Group { GroupName = row.GetString(2) });
                }
                else if (type == "FIELD")
                {
                    items.Add(new Field
                    {
                        Key              = row.GetString(2),
                        Label            = row.GetString(3),
                        FieldType        = row.GetString(4) == "NUMBER" ? EFieldType.Number : EFieldType.Text,
                        Value            = row.GetString(5) == "" ? null : row.GetString(5),
                        ValidateOnChange = row.GetString(6) == "1",
                        Required         = row.GetString(7) == "1",
                        ValidationError  = row.Length >= 8 && row.GetString(8) != "" ? row.GetString(8) : null
                    });
                }
            }
            return items;
        }

        // ─── Internal ─────────────────────────────────────────────────────────────

        private bool ExecuteValidate(EValidationTrigger trigger)
        {
            EnsureConfigured();
            var input = SerializeItemsCVar(Items);
            input["trigger"] = new CVar(trigger.ToString());

            CVar output = MLRest!.QueryOrThrow(CacheClass, "Validate", input);
            Items = ParseItems(output);
            return Items.OfType<Field>().All(f => !f.HasError);
        }

        private void EnsureConfigured()
        {
            if (MLRest == null || string.IsNullOrEmpty(CacheClass))
                throw new InvalidOperationException("EDInputForm: MLRest and CacheClass must be set before use.");
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

            // Defer outside the WPF binding-update pipeline so the call stack is clean.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                Debug.WriteLine("[EDInputForm] Auto-validating on Change");
                ExecuteValidate(EValidationTrigger.Change);
            }));
        }
    }


}
