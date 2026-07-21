using EDControls;
using EDControls.EDFilterComboBox;

namespace EDControls.Tests;

/// <summary>Testy čisté logiky EDFilterComboBox: zobrazovaný řetězec + filtrovací predikát.</summary>
public class EDFilterComboBoxLogicTests
{
    [Fact]
    public void Display_KeyAndLabel_JoinsWithSeparator()
    {
        Assert.Equal("A" + EDFilterComboBoxLogic.DisplaySeparator + "Aktivní",
            EDFilterComboBoxLogic.Display(new ListItem("A", "Aktivní")));
    }

    [Theory]
    [InlineData("A", "", "A")]        // prázdný Label → jen Key
    [InlineData("A", "A", "A")]       // Label == Key → jen Key (ne "A — A")
    public void Display_NoMeaningfulLabel_ReturnsKeyOnly(string key, string label, string expected)
    {
        Assert.Equal(expected, EDFilterComboBoxLogic.Display(new ListItem(key, label)));
    }

    [Fact]
    public void Display_CustomSeparator_JoinsWithIt()
    {
        Assert.Equal("1 | M-line", EDFilterComboBoxLogic.Display(new ListItem("1", "M-line"), " | "));
    }

    [Theory]
    [InlineData("", true)]            // prázdný text → vše projde
    [InlineData(null, true)]
    [InlineData("cb", true)]          // shoda na Key (case-insensitive)
    [InlineData("BUDĚ", true)]        // shoda na Label (case-insensitive)
    [InlineData("xyz", false)]        // žádná shoda
    public void Matches_KeyOrLabel_CaseInsensitive(string? text, bool expected)
    {
        var item = new ListItem("CB", "České Budějovice");
        Assert.Equal(expected, EDFilterComboBoxLogic.Matches(item, text));
    }
}
