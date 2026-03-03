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
            object[] output = MLRest!.Query(CacheClass, "Init", Array.Empty<object>());
            MLRest.RestStatus.ThrowIfError();
            Items = ParseItems(output, MLRest.Delimiter);
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

            object[] output = MLRest!.Query(CacheClass, "Save", SerializeItems(Items, MLRest.Delimiter));
            MLRest.RestStatus.ThrowIfError();
            Items = ParseItems(output, MLRest.Delimiter);
            return true;
        }

        // ─── Serialization helpers ────────────────────────────────────────────────

        private static object[] SerializeItems(IEnumerable<Item>? items, char delimiter)
        {
            var d = delimiter;
            return [.. (items ?? [])
                .Select<Item, object>(item => item switch
                {
                    Group g => $"GROUP{d}{g.GroupName}",
                    Field f => $"FIELD{d}{f.Key}{d}{f.Label}{d}{(f.FieldType == EFieldType.Number ? "NUMBER" : "TEXT")}{d}{f.Value ?? ""}{d}{(f.ValidateOnChange ? "1" : "0")}{d}{(f.Required ? "1" : "0")}",
                    _           => ""
                })];
        }

        private static List<Item> ParseItems(object[] output, char delimiter)
        {
            var items = new List<Item>();
            for (int i = 1; i < output.Length; i++)
            {
                string[] parts = output[i]?.ToString()?.Split(delimiter) ?? [];
                if (parts.Length == 0) continue;

                if (parts[0] == "GROUP" && parts.Length >= 2)
                {
                    items.Add(new Group { GroupName = parts[1] });
                }
                else if (parts[0] == "FIELD" && parts.Length >= 4)
                {
                    items.Add(new Field
                    {
                        Key              = parts[1],
                        Label            = parts[2],
                        FieldType        = parts[3] == "NUMBER" ? EFieldType.Number : EFieldType.Text,
                        Value            = parts.Length > 4 ? parts[4] : null,
                        ValidateOnChange = parts.Length > 5 && parts[5] == "1",
                        Required         = parts.Length > 6 && parts[6] == "1",
                        ValidationError  = parts.Length > 7 && parts[7] != "" ? parts[7] : null
                    });
                }
            }
            return items;
        }

        // ─── Internal ─────────────────────────────────────────────────────────────

        private bool ExecuteValidate(EValidationTrigger trigger)
        {
            EnsureConfigured();
            var input = new object[] { trigger.ToString() }
                .Concat(SerializeItems(Items, MLRest!.Delimiter))
                .ToArray();

            object[] output = MLRest.Query(CacheClass, "Validate", input);
            MLRest.RestStatus.ThrowIfError();

            Items = ParseItems(output, MLRest.Delimiter);
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
