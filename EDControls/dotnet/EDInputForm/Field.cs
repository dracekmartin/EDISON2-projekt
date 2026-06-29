using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using WpfVisibility = System.Windows.Visibility;

namespace EDControls.EDInputForm
{
    // ListItem (Key, Label) přesunut do root namespace EDControls (sdílený typ) — viz ListItem.cs.

    public class Field : Item
    {
        /// <summary>Stabilní identifikátor — klíč pro komunikaci s Caché.</summary>
        public string Key { get; set; } = "";

        public string Label { get; set; } = "";

        /// <summary>Pokud true, změna <c>Value</c> automaticky spouští validaci celého formuláře.</summary>
        public bool ValidateOnChange { get; set; } = false;

        /// <summary>Visible v DEBUG buildu když ValidateOnChange = true. EDInputForm zobrazí debug indikátor.</summary>
        public WpfVisibility ValidateIndicatorVisibility =>
#if DEBUG
            ValidateOnChange ? WpfVisibility.Visible : WpfVisibility.Collapsed;
#else
            WpfVisibility.Collapsed;
#endif

        /// <summary>Pokud true, vedle labelu se zobrazí hvězdička povinnosti.</summary>
        public bool Required { get; set; } = false;

        public WpfVisibility RequiredIndicatorVisibility =>
            Required ? WpfVisibility.Visible : WpfVisibility.Collapsed;

        public EFieldType FieldType { get; set; } = EFieldType.Text;

        /// <summary>Pokud false, pole je read-only (server-controlled).</summary>
        public bool Editable { get; set; } = true;

        /// <summary>Volby pro pole typu List. U ostatních typů prázdné.</summary>
        public List<ListItem> ListItems { get; set; } = [];

        /// <summary>
        /// Konfigurace pro pole typu File: filtr dialogu (např. <c>"PDF (*.pdf)|*.pdf"</c>),
        /// nebo speciální <c>"$FOLDER"</c> pro výběr složky. Prázdné = všechny soubory.
        /// Ignoruje se pro ostatní typy.
        /// </summary>
        public string FileFilter { get; set; } = "";

        /// <summary>
        /// Minimální povolená hodnota pro pole typu <see cref="EFieldType.Number"/> nebo
        /// <see cref="EFieldType.Decimal"/>. Null = bez dolní hranice. Validuje se v
        /// <see cref="GetTypeError"/> i serverem.
        /// </summary>
        public decimal? Min { get; set; }

        /// <summary>Maximální povolená hodnota (analogicky <see cref="Min"/>).</summary>
        public decimal? Max { get; set; }

        /// <summary>
        /// Formátovací string pro zobrazení Decimal hodnoty (např. <c>"N2"</c>, <c>"0.##"</c>).
        /// Aplikuje se v UI při ztrátě focusu. Null/prázdné = bez formátování.
        /// </summary>
        public string? Format { get; set; }

        /// <summary>
        /// Jednotka zobrazená jako suffix vedle inputu (např. <c>"mm"</c>, <c>"pt"</c>).
        /// Pouze visualní — nemění Value. Null/prázdné = bez jednotky.
        /// </summary>
        public string? Unit { get; set; }

        /// <summary>WPF binding helper — Visible pokud <see cref="Unit"/> není null/prázdné.</summary>
        public WpfVisibility UnitVisibility =>
            string.IsNullOrEmpty(Unit) ? WpfVisibility.Collapsed : WpfVisibility.Visible;

        /// <summary>
        /// ListItems pro strict List dropdown. U nepovinných polí má na začátku
        /// implicitní prázdnou položku (umožní hodnotu vymazat = "nevybráno");
        /// u povinných polí se prázdná položka vynechá (uživatel musí zvolit reálnou volbu).
        /// </summary>
        public List<ListItem> ListItemsForDisplay =>
            Required ? [.. ListItems] : [new ListItem("", ""), .. ListItems];

        private string? _value;
        public string? Value
        {
            get => _value;
            set
            {
                _value = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BoolValue));
                OnPropertyChanged(nameof(DateValue));
                // Immediate client-side type/range validation pro UX feedback.
                // Server response (ExecuteValidate → ApplyOutput) případně přepíše s podrobnější chybou.
                // GetTypeError vrací null pro prázdné hodnoty — required se kontroluje až při BeforeSave.
                ValidationError = GetTypeError();
            }
        }

        /// <summary>Two-way bool pohled na <c>Value</c> pro CheckBox binding. <c>"1"</c> = true, ostatní = false.</summary>
        public bool BoolValue
        {
            get => _value == "1";
            set => Value = value ? "1" : "0";
        }

        /// <summary>Two-way DateTime pohled na <c>Value</c> pro DatePicker binding. Wire formát: <c>"yyyy-MM-dd"</c>.</summary>
        public DateTime? DateValue
        {
            get => DateTime.TryParseExact(_value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                       DateTimeStyles.None, out var d) ? d : null;
            set => Value = value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";
        }

        /// <summary>
        /// Validační error pro povinné pole pokud je <see cref="Required"/> a prázdné.
        /// Bool a Button typy nemají "prázdný" stav — vždy validní.
        /// </summary>
        public string? GetRequiredError()
        {
            if (!Required) return null;
            if (FieldType == EFieldType.Bool) return null;
            if (FieldType == EFieldType.Button) return null;
            if (string.IsNullOrEmpty(_value)) return "Povinné pole";
            return null;
        }

        /// <summary>
        /// Klientská typová validace — vrátí chybu pokud hodnota neodpovídá typu, jinak null.
        /// Prázdné hodnoty netestuje — od toho je <see cref="Required"/>.
        /// </summary>
        public string? GetTypeError()
        {
            if (string.IsNullOrEmpty(_value)) return null;
            switch (FieldType)
            {
                case EFieldType.Number:
                    if (!int.TryParse(_value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var iv))
                        return "Musí být celé číslo";
                    return CheckRange(iv);
                case EFieldType.Decimal:
                    if (!decimal.TryParse(_value, NumberStyles.Number, CultureInfo.InvariantCulture, out var dv))
                        return "Musí být číslo";
                    return CheckRange(dv);
                case EFieldType.Date:
                    return DateTime.TryParseExact(_value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                               DateTimeStyles.None, out _) ? null : "Neplatné datum";
                case EFieldType.Bool:
                    return _value is "0" or "1" ? null : "Musí být 0 nebo 1";
                default:
                    return null;
            }
        }

        /// <summary>Validuje hodnotu proti <see cref="Min"/> / <see cref="Max"/>. Null = bez kontroly.</summary>
        private string? CheckRange(decimal value)
        {
            if (Min.HasValue && value < Min.Value)
                return $"Hodnota musí být ≥ {Min.Value.ToString(CultureInfo.InvariantCulture)}";
            if (Max.HasValue && value > Max.Value)
                return $"Hodnota musí být ≤ {Max.Value.ToString(CultureInfo.InvariantCulture)}";
            return null;
        }

        private string? _validationError;
        public string? ValidationError
        {
            get => _validationError;
            set
            {
                _validationError = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasError));
            }
        }

        public bool HasError => !string.IsNullOrEmpty(_validationError);

        /// <summary>
        /// Skupina ke které pole patří. Nastavuje EDInputForm po parsování.
        /// Pokud null, pole se chová jako vždy viditelné.
        /// </summary>
        internal Group? ParentGroup
        {
            get => _parentGroup;
            set
            {
                if (_parentGroup != null) _parentGroup.PropertyChanged -= OnGroupPropertyChanged;
                _parentGroup = value;
                if (_parentGroup != null) _parentGroup.PropertyChanged += OnGroupPropertyChanged;
                OnPropertyChanged(nameof(EffectiveVisibility));
            }
        }
        private Group? _parentGroup;

        private void OnGroupPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Group.Visibility))
                OnPropertyChanged(nameof(EffectiveVisibility));
        }

        /// <summary>
        /// Collapsed pokud pole je non-Visible, parent group je non-Visible, nebo neprošlo search filtrem.
        /// </summary>
        public override WpfVisibility EffectiveVisibility =>
            !IsFilteredBySearch
            && Visibility == EItemVisibility.Visible
            && (_parentGroup?.Visibility ?? EItemVisibility.Visible) == EItemVisibility.Visible
                ? WpfVisibility.Visible
                : WpfVisibility.Collapsed;
    }
}
