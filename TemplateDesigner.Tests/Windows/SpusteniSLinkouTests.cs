using EDControls;
using EDControls.EDViewModel;
using NSubstitute;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Schema;
using TemplateDesigner.Services;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Spuštění z porovnání linek v modulu JŘ: srovnávací skupina a linka ze spuštění platí, i když
/// je šablona nebo předvolba uživatele nese jinak, a přestanou platit, až uživatel vybere jinou linku.
/// </summary>
public class SpusteniSLinkouTests
{
    public SpusteniSLinkouTests() =>
        EDServices.Initialize(new EDServices(Substitute.For<IMLRest>(),
            Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()));

    private static readonly TemplateParameter Linka = new()
    {
        Name = "POROVNAT_LINKU", Type = "text", DefaultValue = "", PerInstance = true, ServerSide = true,
    };

    private static readonly TemplateParameter Skupina = new()
    {
        Name = "POROVNAT_SKUPINU", Type = "text", DefaultValue = "", ServerSide = true,
    };

    private static readonly Dictionary<string, string> Spusteni = new()
    {
        ["SKUPINA_LINEK_ID"] = "307", ["LINKA_ID"] = "0380760",
        ["POROVNAT_SKUPINU"] = "207", ["POROVNAT_LINKU"] = "0380760",
    };

    private static TemplateDesignerViewModel Vm()
    {
        var vm = new TemplateDesignerViewModel(null, null, []);
        vm.State.CurrentTemplate = new Template
        {
            AgendaCode = "LINKOVY_JR_AD_ROZDILOVY",
            Parameters = [Skupina, Linka],
            // Šablona si pamatuje jinou srovnávací skupinu - spuštění ji musí přebít.
            PrintParameters = new() { ["POROVNAT_SKUPINU"] = "203" },
            Sections = [new Section()],
        };
        vm.LoadAgendaParameters("LINKOVY_JR_AD_ROZDILOVY");
        return vm;
    }

    [Fact]
    public async Task HodnotySpusteni_PrebijiSablonu()
    {
        var vm = Vm();
        vm.UseLaunchParams(vm.State.CurrentTemplate.Id, Spusteni);

        await vm.ApplyPersistedPrintParamsAsync();

        Assert.Equal("207", vm.PreviewParamValues["POROVNAT_SKUPINU"]);
        Assert.Equal("0380760", vm.PreviewParamValues["POROVNAT_LINKU"]);
        Assert.False(vm.IsDirty);   // načtení, ne úprava uživatele
    }

    [Fact]
    public async Task OpakovaneNacteniPredvoleb_HodnotySpusteniNesmaze()
    {
        // Předvolby se při pomalém spojení načtou až po otevření šablony a projdou toutéž cestou.
        var vm = Vm();
        vm.UseLaunchParams(vm.State.CurrentTemplate.Id, Spusteni);
        await vm.ApplyPersistedPrintParamsAsync();

        await vm.ApplyPersistedPrintParamsAsync();

        Assert.Equal("207", vm.PreviewParamValues["POROVNAT_SKUPINU"]);
        Assert.Equal("0380760", vm.PreviewParamValues["POROVNAT_LINKU"]);
    }

    [Fact]
    public async Task JinaSablona_HodnotySpusteniNepouzije()
    {
        var vm = Vm();
        vm.UseLaunchParams(Guid.NewGuid(), Spusteni);

        await vm.ApplyPersistedPrintParamsAsync();

        Assert.Equal("203", vm.PreviewParamValues["POROVNAT_SKUPINU"]);
        Assert.Equal("", vm.PreviewParamValues["POROVNAT_LINKU"]);
    }

    [Fact]
    public async Task ZmenaParametruUzivatelem_HodnotySpusteniUkonci()
    {
        var vm = Vm();
        vm.UseLaunchParams(vm.State.CurrentTemplate.Id, Spusteni);
        await vm.ApplyPersistedPrintParamsAsync();

        vm.SetAgendaParamValue("POROVNAT_SKUPINU", "209");
        await vm.ApplyPersistedPrintParamsAsync();

        Assert.Equal("203", vm.PreviewParamValues["POROVNAT_SKUPINU"]);   // zase platí šablona
    }

    [Theory]
    [InlineData("0380760")]
    [InlineData("380760")]
    public void Linka_SeNajdeSVedouciNulouIBez(string zeSpusteni)
    {
        AgendaInstance[] nalezene =
        [
            new("0380761", "380761 - Jiná", null),
            new("0380760", "380760 - Tábor - Písek", null),
        ];

        var linka = TemplateDesignerViewModel.MatchLaunchInstance(nalezene, zeSpusteni);

        Assert.Equal("0380760", linka.Value);
        Assert.Equal("380760 - Tábor - Písek", linka.Display);
    }

    [Fact]
    public void NenalezenaLinka_ZustaneSKlicem()
    {
        var linka = TemplateDesignerViewModel.MatchLaunchInstance([], "0380760");

        Assert.Equal("0380760", linka.Value);   // GetData si klíč dorovná sám
        Assert.Equal("380760", linka.Display);
    }

    [Fact]
    public void SablonaFirmy_BereSeNejnovejiUpravena()
    {
        CacheTemplateMetadata Meta(int den, DateTime? zmena) => new()
        {
            TemplateId = new Guid(den, 0, 0, new byte[8]),
            CreatedAt = new DateTime(2026, 10, den), ModifiedAt = zmena,
        };
        var stara = Meta(1, new DateTime(2026, 10, 5));
        var nova = Meta(2, new DateTime(2026, 10, 20));
        var neupravena = Meta(10, null);   // jen vznik 10. 10.

        Assert.Same(nova, TemplateDesignerViewModel.NewestTemplate([stara, neupravena, nova]));
        Assert.Null(TemplateDesignerViewModel.NewestTemplate([]));
    }
}
