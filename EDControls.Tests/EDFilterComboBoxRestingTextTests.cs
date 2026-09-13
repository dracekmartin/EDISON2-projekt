using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EDControls;
using EDCombo = EDControls.EDFilterComboBox.EDFilterComboBox;

namespace EDControls.Tests;

/// <summary>
/// Klidový „Key - Label" striktního EDCombo se needituje po částech. Dvojklik do výběru firmy dřív
/// umístil kurzor a psaní se k názvu připisovalo - filtr nenašel nic a pole vypadalo jako volný text.
/// </summary>
public class EDFilterComboBoxRestingTextTests
{
    private static EDCombo Strict(bool userFilterTyped)
    {
        var combo = new EDCombo { StrictSelection = true };
        combo.Suggestions = [new ListItem("A", "Alfa"), new ListItem("B", "Beta")];
        combo._textIsUserFilter = userFilterTyped;
        return combo;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Click_OnRestingText_SelectsAllRegardlessOfFocus(bool textBoxHasFocus)
    {
        EDTesting.Sta.Run(() => Assert.True(Strict(userFilterTyped: false).SelectsAllOnClick(textBoxHasFocus)));
    }

    [Fact]
    public void Click_InTypedFilter_PlacesCaretOnceFocused()
    {
        EDTesting.Sta.Run(() =>
        {
            var combo = Strict(userFilterTyped: true);
            Assert.True(combo.SelectsAllOnClick(textBoxHasFocus: false));   // první klik: fokus + vybrat vše
            Assert.False(combo.SelectsAllOnClick(textBoxHasFocus: true));   // další klik: kurzor do filtru
        });
    }

    [Fact]
    public void FreeMode_NeverInterceptsClicksOrTyping()
    {
        EDTesting.Sta.Run(() =>
        {
            var combo = new EDCombo { StrictSelection = false };
            Assert.False(combo.SelectsAllOnClick(textBoxHasFocus: false));
            Assert.False(combo.ReplacesRestingText());
        });
    }

    [Fact]
    public void Disabled_WhileFocused_RefocusesOnReenable()
    {
        EDTesting.Sta.Run(() =>
        {
            // Přepnutí firmy: shell dá IsEnabled=false, WPF pak (odloženě) fokus odebere → LostKeyboardFocus
            // přijde už na vypnutém controlu. Po zapnutí se fokus vrací, aby šlo přepnout zpět z klávesnice.
            var combo = Strict(userFilterTyped: false);
            combo.IsEnabled = false;
            combo.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, combo, null)
                { RoutedEvent = UIElement.LostKeyboardFocusEvent });
            Assert.True(combo.RefocusPending);

            combo.IsEnabled = true;
            Assert.False(combo.RefocusPending);   // spotřebováno → naplánované vrácení fokusu
        });
    }

    [Fact]
    public void Disabled_WithoutFocus_DoesNotStealFocusOnReenable()
    {
        EDTesting.Sta.Run(() =>
        {
            var combo = Strict(userFilterTyped: false);
            combo.IsEnabled = false;
            Assert.False(combo.RefocusPending);
            combo.IsEnabled = true;
            Assert.False(combo.RefocusPending);
        });
    }

    [Fact]
    public void Typing_ReplacesRestingTextOnly()
    {
        EDTesting.Sta.Run(() =>
        {
            Assert.True(Strict(userFilterTyped: false).ReplacesRestingText());
            Assert.False(Strict(userFilterTyped: true).ReplacesRestingText());
        });
    }

    [Fact]
    public void PreviewTextInput_WithCaretInsideRestingText_SelectsAllBeforeInsert()
    {
        EDTesting.Sta.Run(() =>
        {
            var combo = Strict(userFilterTyped: false);
            combo.SelectedKey = "A";
            Assert.True(combo.ApplyTemplate());
            var tb = (TextBox)combo.Template.FindName("PART_EditableTextBox", combo);
            Assert.Equal("A - Alfa", tb.Text);
            tb.SelectionLength = 0;
            tb.CaretIndex = tb.Text.Length;   // stav po dvojkliku nebo End

            // Tunelující PreviewTextInput projde přes ComboBox dřív, než TextBox znak vloží.
            tb.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice,
                new TextComposition(InputManager.Current, tb, "b")) { RoutedEvent = TextCompositionManager.PreviewTextInputEvent });

            Assert.Equal(tb.Text.Length, tb.SelectionLength);   // vybráno vše → vložený znak text nahradí
        });
    }
}
