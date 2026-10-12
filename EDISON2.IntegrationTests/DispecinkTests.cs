using System.Text.RegularExpressions;
using System.Windows;
using ALWA.SYS;
using EDControls;
using EDControls.EDViewModel;
using EDISONMAIN2.LegacyMiniApps;
using NSubstitute;

namespace EDISON2.IntegrationTests;

/// <summary>
/// Miniaplikace Dispečink: skutečné <c>GetPracoviste</c> a <c>GetJizdy</c> do produkčního
/// <see cref="DispecinkMiniAppViewModel"/>, který čte pole podle pozic.
/// </summary>
[Trait("Category", "Integration")]
public class DispecinkTests
{
    private const string CacheClass = "ALVA.MHD2.PlanDopravy.API.DispecinkMiniApp";
    private const string Pracoviste = "1001";

    // Pole GetJizdy podle významu: prohozená pole dají hodnotu, která do cílového pole nepatří.
    private static readonly Regex Cas = new(@"^\d{2}:\d{2}$");
    private static readonly (int Field, Regex Shape)[] Fields =
    [
        (2, new(@"^\d*$")),           // linka
        (3, new(@"^\p{L}+$")),        // typ směny
        (4, new(@"/")),               // turnusová služba (A19/01)
        (5, Cas),                     // nástup
        (6, Cas),                     // konec
        (8, new(@"^[0-9A-Z ]*$")),    // RZ vozidla
        (9, new(@"^\d*$")),           // evidenční číslo vozidla
        (10, new(@"^[01]$")),         // vyjeto
    ];

    // Synchronně na téže instanci: příkazy VM doběhnou dřív, než test čte kolekce.
    private sealed class InlineRunner(IMLRest rest) : IRestCallRunner
    {
        public Task<T> RunAsync<T>(Func<IMLRest, T> call) => Task.FromResult(call(rest));
    }

    // Historická data DEV pod CB_DP: dnešní den jízdy mít nemusí. 19. 12. 2018 má vyjeté jízdy, 20. 12. 2018 vozidla.
    [DevServerTheory]
    [InlineData("2018-12-19", false, true)]
    [InlineData("2018-12-20", true, false)]
    public void GetJizdy_ProductionViewModel_ReadsTenFieldsAtTheirPositions(string den, bool sVozidlem, bool sVyjetim)
    {
        var rest = DevServer.Connect(dataNamespace: DevServer.ReadDataNamespace);

        // Do zpráv jen klíč řádku a číslo pole: řádek nese jména řidičů.
        var raw = RawRows(rest.QueryOrThrow(CacheClass, "GetJizdy", Input(den)));
        Assert.NotEmpty(raw);
        foreach (var (key, row) in raw)
        {
            Assert.True(row.Count == 10, $"GetJizdy řádek {key}: {row.Count} polí, čekáno 10.");
            foreach (var (field, shape) in Fields)
                Assert.True(shape.IsMatch(row.GetString(field)), $"GetJizdy řádek {key}: pole {field} nemá tvar svého významu.");
        }
        if (sVozidlem) Assert.True(raw.Any(r => r.Row.GetString(8).Length > 0), $"{den}: žádná jízda s vozidlem, pole 8 a 9 se neověří.");
        if (sVyjetim) Assert.True(raw.Any(r => r.Row.GetString(10) == "1"), $"{den}: žádná vyjetá jízda, pole 10 se neověří.");

        var vm = OpenedViewModel(rest, out var error);
        vm.SelectedPracoviste = vm.Pracoviste.First(item => item.Id == Pracoviste);
        vm.Datum = DateTime.ParseExact(den, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        vm.LoadCommand.Execute(null);
        Assert.True(error() is null, $"LoadCommand skončil chybou: {error()?.Message}");

        Assert.Equal(raw.Count, vm.Jizdy.Count);
        for (int i = 0; i < raw.Count; i++)
        {
            var row = raw[i].Row;
            var jizda = vm.Jizdy[i];
            string[] shell = [jizda.Smena, jizda.Linka, jizda.TypSmeny, jizda.Ts, jizda.Nastup, jizda.Konec, jizda.Ridic, jizda.VuzRz, jizda.VuzEvc];
            for (int field = 1; field <= 9; field++)
                Assert.True(shell[field - 1] == row.GetString(field), $"VM řádek {raw[i].Key}: pole {field} nesouhlasí se serverem.");
            Assert.True(jizda.Vyjeto == (row.GetString(10) == "1"), $"VM řádek {raw[i].Key}: Vyjeto nesouhlasí s polem 10.");
        }
    }

    [DevServerFact]
    public void GetPracoviste_ProductionViewModel_ReadsIdAndName()
    {
        var rest = DevServer.Connect(dataNamespace: DevServer.ReadDataNamespace);

        var raw = RawRows(rest.QueryOrThrow(CacheClass, "GetPracoviste", new CVar()));
        Assert.NotEmpty(raw);
        foreach (var (key, row) in raw)
            Assert.True(row.Count == 2 && int.TryParse(row.GetString(1), out _) && row.GetString(2).Length > 0,
                $"GetPracoviste řádek {key}: {row.Count} polí, čekáno číselné id a název.");

        var vm = OpenedViewModel(rest, out _);
        Assert.Equal(raw.Select(r => (r.Row.GetString(1), r.Row.GetString(2))), vm.Pracoviste.Select(p => (p.Id, p.Nazev)));
        Assert.Equal(raw[0].Row.GetString(1), vm.SelectedPracoviste?.Id);
    }

    // Otevřený panel: OnOpened načte pracoviště. Příkaz VM výjimku spolkne do popupu, test ji z něj vytáhne.
    private static DispecinkMiniAppViewModel OpenedViewModel(IMLRest rest, out Func<Exception?> error)
    {
        Exception? caught = null;
        var popup = Substitute.For<IPopupDisplayer>();
        popup.When(p => p.Error(Arg.Any<Exception>(), Arg.Any<Window?>())).Do(call => caught = call.Arg<Exception>());
        var vm = new DispecinkMiniAppViewModel(
            new EDServices(rest, Substitute.For<IWindowNavigator>(), popup), new InlineRunner(rest));
        error = () => caught;

        vm.OnOpened();
        Assert.True(caught is null, $"OnOpened skončil chybou: {caught?.Message}");
        Assert.Contains(vm.Pracoviste, item => item.Id == Pracoviste);
        return vm;
    }

    private static CVar Input(string den)
    {
        var input = new CVar();
        input["pracoviste"] = new CVar(Pracoviste);
        input["datum"] = new CVar(den);
        return input;
    }

    private static List<(string Key, CList Row)> RawRows(CVar output)
    {
        var rows = new List<(string Key, CList Row)>();
        foreach (KeyValuePair<string, CVar> kvp in output)
            if (kvp.Key != "ERR") rows.Add((kvp.Key, kvp.Value));
        return rows;
    }
}
