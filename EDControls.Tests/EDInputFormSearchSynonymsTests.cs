using System.Windows;
using System.Windows.Controls;
using ALWA.SYS;
using EDControls.EDInputForm;
using EDControls.EDViewModel;
using EDTesting;
using NSubstitute;
using Form = EDControls.EDInputForm.EDInputForm;

namespace EDControls.Tests;

/// <summary>
/// Hledání ve formuláři se slovníkem synonym (<see cref="SearchSynonyms"/>). Slovník zapíná aplikace na svých
/// oknech, ne formulář: bez něj hledá formulář jako dřív, takže se nic nezmění ve zbytku EDISONu, kde by
/// „písmo = font" nedávalo smysl.
/// </summary>
public class EDInputFormSearchSynonymsTests
{
    private static readonly SearchSynonyms Slovnik = new(new Dictionary<string, string[]>
    {
        ["písmo"] = ["Font", "Velikost"],
        ["podbarvení"] = ["Barva pozadí"],
    });

    /// <summary>Formulář ve tvaru formuláře prvku návrháře sestav.</summary>
    private static (List<Group> groups, List<Field> fields) Formular()
    {
        var zahlavi = new Group { Number = 1, Key = "zahlavi", GroupName = "Záhlaví" };
        var bunky = new Group { Number = 5, Key = "bunky", GroupName = "Datové buňky" };
        return ([zahlavi, bunky],
        [
            new Field { Number = 2, Key = "font", Label = "Font", ParentGroup = zahlavi },
            new Field { Number = 3, Key = "vyska", Label = "Nejmenší výška", ParentGroup = zahlavi },
            new Field { Number = 4, Key = "pozadi", Label = "Barva pozadí", ParentGroup = zahlavi },
            new Field { Number = 6, Key = "velikost", Label = "Velikost", ParentGroup = bunky },
            new Field { Number = 7, Key = "text", Label = "Barva textu", ParentGroup = bunky },
        ]);
    }

    private static string[] Nalezene(string dotaz, SearchSynonyms? slovnik)
    {
        var (groups, fields) = Formular();
        return [.. Form.ComputeSearchMatches(groups, fields, dotaz, slovnik).Select(f => f.Key)];
    }

    [Fact]
    public void BezSlovniku_HledaJakoDriv()
    {
        Assert.Empty(Nalezene("písmo", null));
        Assert.Empty(Nalezene("výška záhlaví", null));       // celý výraz jako jeden kus
        Assert.Equal(["pozadi", "text"], Nalezene("barva", null));
    }

    [Fact]
    public void Synonymum_NajdePopisky()
    {
        Assert.Equal(["font", "velikost"], Nalezene("písmo", Slovnik));
        Assert.Equal(["pozadi"], Nalezene("podbarvení", Slovnik));
    }

    [Fact]
    public void Synonymum_PlatiIProJinyTvarARozepsaneSlovo()
    {
        Assert.Equal(["font", "velikost"], Nalezene("písma", Slovnik));
        Assert.Equal(["font", "velikost"], Nalezene("pís", Slovnik));
    }

    /// <summary>Každé slovo zvlášť, přes popisek i skupinu, bez koncovky: „výška záhlaví" najde Nejmenší výška ve skupině Záhlaví.</summary>
    [Fact]
    public void SlovaZvlast_PresPopisekISkupinu()
    {
        Assert.Equal(["vyska"], Nalezene("výška záhlaví", Slovnik));
        Assert.Equal(["text"], Nalezene("barva textu", Slovnik));
        Assert.Empty(Nalezene("barva písmo xyz", Slovnik));   // musí sedět všechna slova
    }

    [Fact]
    public void KratkeSlovo_SynonymaNerozbali()
    {
        // „p" by jinak nabídlo všechno ze slovníku na „p".
        Assert.Equal(["pozadi"], Nalezene("p", Slovnik));    // jen „p" v popisku (pozadí), synonyma ne
    }

    [Fact]
    public void SkupinaSeUkaze_KdyzSeShodujeJejiNazev()
    {
        var (groups, fields) = Formular();
        Form.ComputeSearchMatches(groups, fields, "datove bunky", Slovnik);

        Assert.False(groups[1].IsFilteredBySearch);
        Assert.True(groups[0].IsFilteredBySearch);
    }

    /// <summary>Slovník se dědí z nadřazeného prvku (okna) a dosazený dodatečně přefiltruje rozepsané hledání.</summary>
    [Fact]
    public void SlovnikZOkna_PlatiProFormularUvnitr()
    {
        RunSta(() =>
        {
            var form = NewForm();
            var okno = new Border { Child = form };
            form.Init();
            form.Toolbar.SearchText = "písmo";
            Assert.True(FieldOf(form, "font").IsFilteredBySearch);           // zatím bez slovníku

            Form.SetSearchSynonyms(okno, Slovnik);

            Assert.Same(Slovnik, Form.GetSearchSynonyms(form));
            Assert.False(FieldOf(form, "font").IsFilteredBySearch);
            Assert.False(FieldOf(form, "velikost").IsFilteredBySearch);
            Assert.True(FieldOf(form, "pozadi").IsFilteredBySearch);
        });
    }

    private static Field FieldOf(Form form, string key) =>
        form.Items!.OfType<Field>().Single(f => f.Key == key);

    private static Form NewForm()
    {
        var mock = MLRestMocks.Create();
        mock.QueryOrThrow(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CVar>()).Returns(_ => Schema());
        var services = new EDServices(mock, Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>());
        return new Form { CacheClass = "ALVA.A", ServicesOverride = services };
    }

    private static CVar Schema()
    {
        var data = new CVar();
        data["1"] = new CList("FIELD", "1", "font", "Font", "TEXT", "Arial", "0", "0");
        data["2"] = new CList("FIELD", "2", "velikost", "Velikost", "NUMBER", "9", "0", "0");
        data["3"] = new CList("FIELD", "3", "pozadi", "Barva pozadí", "TEXT", "", "0", "0");
        return data;
    }

    private const string Comp = "pack://application:,,,/EDControls;component/";

    // EDInputForm.xaml čte StaticResource EDIcons/EDStrings → Application se slovníky appky.
    private static void EnsureApplication()
    {
        if (Application.Current is not null) return;

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        foreach (var dict in new[] { "EDTheme/EDTheme.xaml", "EDControlStyles/EDControlStyles.xaml",
                                     "EDIcons/EDIcons.xaml", "EDStrings/EDStrings.xaml" })
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(Comp + dict) });
    }

    private static void RunSta(Action action) =>
        Sta.RunOnAppThread(() => { EnsureApplication(); action(); });
}
