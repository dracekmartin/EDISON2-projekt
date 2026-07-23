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
