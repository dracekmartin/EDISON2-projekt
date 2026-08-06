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
    [InlineData("ceske", true)]       // psaní bez diakritiky najde Label s diakritikou
    [InlineData("BUDE", true)]
    public void Matches_KeyOrLabel_CaseInsensitive(string? text, bool expected)
    {
        var item = new ListItem("CB", "České Budějovice");
        Assert.Equal(expected, EDFilterComboBoxLogic.Matches(item, text));
    }

    [Fact]
    public void Matches_TextSDiakritikou_NajdeLabelBezDiakritiky()
    {
        Assert.True(EDFilterComboBoxLogic.Matches(new ListItem("PL", "Plzen"), "plzeň"));
    }

    private static readonly ListItem[] Firmy =
    [
        new("CB", "České Budějovice"),
        new("PL", "Plzeň"),
        new("PB", "Příbram"),
    ];

    [Theory]
    [InlineData("cb", "CB")]                        // exaktní Key (case-insensitive)
    [InlineData("CB - České Budějovice", "CB")]     // exaktní Display
    [InlineData("cb - ceske budejovice", "CB")]     // exaktní Display bez diakritiky
    [InlineData("plzeň", "PL")]                     // exaktní Label
    [InlineData("budejo", "CB")]                    // jediný kandidát filtru
    [InlineData(" cb ", "CB")]                      // trim okrajů
    public void ResolveTyped_JednoznacnyText_VratiPolozku(string text, string expectedKey)
    {
        Assert.Equal(expectedKey, EDFilterComboBoxLogic.ResolveTyped(Firmy, text, " - ")?.Key);
    }

    [Theory]
    [InlineData("p")]      // víc kandidátů bez exaktní shody (Plzeň i Příbram)
    [InlineData("xyz")]    // žádná shoda
    [InlineData("")]       // prázdný text
    [InlineData(null)]
    public void ResolveTyped_NejednoznacnyNeboPrazdnyText_VratiNull(string? text)
    {
        Assert.Null(EDFilterComboBoxLogic.ResolveTyped(Firmy, text, " - "));
    }

    [Fact]
    public void ResolveTyped_ExaktniKeyMaPrednostPredFiltrem()
    {
        // "PL" je exaktní Key a zároveň substring "PL..." obou P-firem → vyhrává exakt.
        Assert.Equal("PL", EDFilterComboBoxLogic.ResolveTyped(Firmy, "PL", " - ")?.Key);
    }

    [Fact]
    public void ResolveTyped_NullSuggestions_VratiNull()
    {
        Assert.Null(EDFilterComboBoxLogic.ResolveTyped(null, "cb", " - "));
    }
}
