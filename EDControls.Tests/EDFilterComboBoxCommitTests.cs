using System.ComponentModel;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using EDControls;
using EDCombo = EDControls.EDFilterComboBox.EDFilterComboBox;

namespace EDControls.Tests;

/// <summary>
/// Regresní testy commit cesty EDCombo (WPF → STA vlákno). Hlídají, že výběr přežije
/// Refilter, který mezi SelectionChanged a DropDownClosed zahodí SelectedItem — dřív se v takové
/// session přepnutí „nic neudělalo" (commit četl už prázdný SelectedItem).
/// </summary>
public class EDFilterComboBoxCommitTests
{
    private sealed class KeyHolder : INotifyPropertyChanged
    {
        private string? _value;
        public string? Value
        {
            get => _value;
            set { _value = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value))); }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    [Fact]
    public void Commit_AfterRefilterClearsSelectedItem_StillPushesPickedKeyToTwoWaySource()
    {
        RunSta(() =>
        {
            var source = new KeyHolder { Value = "A" };
            var combo = new EDCombo { StrictSelection = true };
            combo.Suggestions = [new ListItem("A", "Alfa"), new ListItem("B", "Beta")];
            BindingOperations.SetBinding(combo, EDCombo.SelectedKeyProperty,
                new Binding(nameof(KeyHolder.Value)) { Source = source, Mode = BindingMode.TwoWay });

            // Uživatel vybere B (SelectionChanged přijde před osudným TextChanged → zachytí Key).
            combo.SelectedItem = combo.Items.OfType<ComboBoxItem>().Single(i => (string?)i.Tag == "B");

            // Race: Refilter přestaví Items a zahodí SelectedItem PŘED tím, než commit proběhne.
            Invoke(combo, "Refilter", [null]);
            Assert.Null(combo.SelectedItem);

            Invoke(combo, "CommitSelection", []);

            Assert.Equal("B", source.Value);   // pick se propsal do TwoWay zdroje i bez SelectedItem
        });
    }

    [Fact]
    public void Commit_WithoutPick_RevertsAndLeavesSourceUnchanged()
    {
        RunSta(() =>
        {
            var source = new KeyHolder { Value = "A" };
            var combo = new EDCombo { StrictSelection = true };
            combo.Suggestions = [new ListItem("A", "Alfa"), new ListItem("B", "Beta")];
            BindingOperations.SetBinding(combo, EDCombo.SelectedKeyProperty,
                new Binding(nameof(KeyHolder.Value)) { Source = source, Mode = BindingMode.TwoWay });

            // Jen otevřít+zavřít bez výběru (žádný SelectionChanged s položkou) → revert, zdroj beze změny.
            Invoke(combo, "CommitSelection", []);

            Assert.Equal("A", source.Value);
        });
    }

    [Fact]
    public void Enter_PoNapsaniJednoznacnehoTextu_CommitujeKeyDoTwoWayZdroje()
    {
        RunSta(() =>
        {
            var source = new KeyHolder { Value = "A" };
            var combo = new EDCombo { StrictSelection = true };
            combo.Suggestions = [new ListItem("A", "Alfa"), new ListItem("B", "Beta")];
            BindingOperations.SetBinding(combo, EDCombo.SelectedKeyProperty,
                new Binding(nameof(KeyHolder.Value)) { Source = source, Mode = BindingMode.TwoWay });

            // Uživatel napíše jednoznačný text a dá Enter bez výběru z dropdownu (dropdown zavřený
            // → commit proběhne přímo v Enter handleru, DropDownClosed nepřijde).
            combo.Text = "bet";
            Invoke(combo, "CommitTypedTextOnEnter", []);

            Assert.Equal("B", source.Value);
        });
    }

    [Fact]
    public void Enter_NejednoznacnyText_RevertujeNaPuvodniKey()
    {
        RunSta(() =>
        {
            var source = new KeyHolder { Value = "A" };
            var combo = new EDCombo { StrictSelection = true };
            combo.Suggestions = [new ListItem("A", "Alfa"), new ListItem("B", "Alba")];
            BindingOperations.SetBinding(combo, EDCombo.SelectedKeyProperty,
                new Binding(nameof(KeyHolder.Value)) { Source = source, Mode = BindingMode.TwoWay });

            combo.Text = "al";   // matchuje Alfa i Alba, žádný exakt
            Invoke(combo, "CommitTypedTextOnEnter", []);

            Assert.Equal("A", source.Value);                     // zdroj beze změny
            Assert.Equal("A - Alfa", combo.Text);                // display revertnul na výběr
        });
    }

    [Fact]
    public void Enter_PoVyberuSipkou_NeprepisujeZachycenyPick()
    {
        RunSta(() =>
        {
            var source = new KeyHolder { Value = "A" };
            var combo = Bound(source);

            // Šipka dolů: z kotvy na aktuálním výběru (A) se posune na B a commituje Enterem.
            combo.HandleNavigationKey(Key.Down);
            Assert.Equal("B - Beta", combo.Text);

            combo.HandleNavigationKey(Key.Enter);

            Assert.Equal("B", source.Value);
        });
    }

    // HD 13132/3: Enter po šipce se ZAVŘENÝM dropdownem propadal early-returnem
    // (Text už byl display picku) a commit se nikdy nezavolal - LostKeyboardFocus pak revertnul.
    [Fact]
    public void Enter_PoSipceSeZavrenymDropdownem_Commituje()
    {
        RunSta(() =>
        {
            var source = new KeyHolder { Value = "A" };
            var combo = Bound(source);

            // Přesně to, co udělá WPF šipkou nad zavřeným dropdownem: změní SelectedItem.
            combo.SelectedItem = combo.Items.OfType<ComboBoxItem>().Single(i => (string?)i.Tag == "B");
            Assert.False(combo.IsDropDownOpen);

            combo.HandleNavigationKey(Key.Enter);

            Assert.Equal("B", source.Value);
        });
    }

    // Pozn.: IsDropDownOpen tu nelze ověřit - ComboBox ho coercuje na false, dokud control není
    // naloadovaný v okně. Testujeme tedy posun výběru, který je na otevření nezávislý.
    [Fact]
    public void SipkaDolu_ZacneUAktualnihoVyberu()
    {
        RunSta(() =>
        {
            var combo = Bound(new KeyHolder { Value = "A" });

            combo.HandleNavigationKey(Key.Down);

            Assert.Equal("B", SelectedTag(combo));
        });
    }

    [Fact]
    public void SipkaDolu_BezVyberu_VezmePrvniPolozku()
    {
        RunSta(() =>
        {
            var combo = Bound(new KeyHolder());   // žádný SelectedKey

            combo.HandleNavigationKey(Key.Down);

            Assert.Equal("A", SelectedTag(combo));
        });
    }

    [Fact]
    public void Sipky_NaKrajichStoji()
    {
        RunSta(() =>
        {
            var combo = Bound(new KeyHolder { Value = "A" });

            combo.HandleNavigationKey(Key.Up);      // z kotvy A nahoru → stojí na A
            Assert.Equal("A", SelectedTag(combo));

            combo.HandleNavigationKey(Key.Down);    // A → B (poslední)
            combo.HandleNavigationKey(Key.Down);    // stojí na B
            Assert.Equal("B", SelectedTag(combo));
        });
    }

    [Fact]
    public void Escape_ZahodiRozpracovanyVyberARevertneDisplay()
    {
        RunSta(() =>
        {
            var source = new KeyHolder { Value = "A" };
            var combo = Bound(source);
            combo.HandleNavigationKey(Key.Down);    // rozpracovaný výběr B

            // Esc klávesu záměrně nespotřebuje (Storno v dialogu musí fungovat dál).
            Assert.False(combo.HandleNavigationKey(Key.Escape));

            Assert.Equal("A", source.Value);        // zdroj beze změny
            Assert.Equal("A - Alfa", combo.Text);   // display revertnul
        });
    }

    // Volný režim nesmí Enter polykat - EDInputForm ho používá na navigaci mezi poli.
    [Fact]
    public void VolnyRezim_EnterNechaKlavesuProbublat()
    {
        RunSta(() =>
        {
            var combo = new EDCombo { Suggestions = [new ListItem("A", "Alfa")] };
            Assert.False(combo.HandleNavigationKey(Key.Enter));
        });
    }

    [Fact]
    public void Suggestions_WithPresetValue_OffersWholeListNotJustTheMatchingItem()
    {
        RunSta(() =>
        {
            // Volný režim: Text nese aktuální hodnotu. Ta NENÍ filtr - jinak by šipka nabídla jen
            // právě nastavenou položku a přepnout na jinou by šlo až po smazání textu.
            var combo = new EDCombo { Text = "A" };
            combo.Suggestions = [new ListItem("A", "Alfa"), new ListItem("B", "Beta")];

            Assert.Equal(2, combo.Items.Count);
        });
    }

    [Fact]
    public void Reopen_AfterUserTypedFilter_KeepsFilteredList()
    {
        RunSta(() =>
        {
            var combo = new EDCombo { StrictSelection = true };
            combo.Suggestions = [new ListItem("A", "Alfa"), new ListItem("B", "Beta")];

            // Napsaný filtr má přežít do otevřeného dropdownu (jinak by psaní nic nefiltrovalo).
            SetField(combo, "_textIsUserFilter", true);
            Invoke(combo, "Refilter", ["B"]);
            Assert.Equal(1, combo.Items.Count);

            combo.IsDropDownOpen = true;   // → DropDownOpened; napsaný filtr se nesmí zahodit
            Assert.Equal(1, combo.Items.Count);
        });
    }

    private static EDCombo Bound(KeyHolder source)
    {
        var combo = new EDCombo { StrictSelection = true };
        combo.Suggestions = [new ListItem("A", "Alfa"), new ListItem("B", "Beta")];
        BindingOperations.SetBinding(combo, EDCombo.SelectedKeyProperty,
            new Binding(nameof(KeyHolder.Value)) { Source = source, Mode = BindingMode.TwoWay });
        return combo;
    }

    private static string? SelectedTag(EDCombo combo) => (combo.SelectedItem as ComboBoxItem)?.Tag as string;

    private static void SetField(EDCombo combo, string field, object value) =>
        typeof(EDCombo).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(combo, value);

    private static void Invoke(EDCombo combo, string method, object?[] args) =>
        typeof(EDCombo).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(combo, args);

    // Spustí akci na dedikovaném STA vlákně (WPF controly to vyžadují) a propaguje výjimku zpět.
    private static void RunSta(Action action)
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { captured = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (captured != null) throw captured;
    }
}
