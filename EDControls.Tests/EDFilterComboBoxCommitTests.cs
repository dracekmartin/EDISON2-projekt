using System.ComponentModel;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Data;
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
            var combo = new EDCombo { StrictSelection = true };
            combo.Suggestions = [new ListItem("A", "Alfa"), new ListItem("B", "Beta")];
            BindingOperations.SetBinding(combo, EDCombo.SelectedKeyProperty,
                new Binding(nameof(KeyHolder.Value)) { Source = source, Mode = BindingMode.TwoWay });

            // Šipka vybere B: SelectionChanged zachytí pick a WPF přepíše Text na jeho display.
            combo.SelectedItem = combo.Items.OfType<ComboBoxItem>().Single(i => (string?)i.Tag == "B");
            Assert.Equal("B - Beta", combo.Text);

            // Enter: resolve se přeskočí (Text == display picku) a commit z DropDownClosed
            // fixuje pick zachycený v SelectionChanged.
            Invoke(combo, "CommitTypedTextOnEnter", []);
            Invoke(combo, "CommitSelection", []);

            Assert.Equal("B", source.Value);
        });
    }

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
