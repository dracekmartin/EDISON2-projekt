using EDControls;
using EDControls.EDViewModel;
using NSubstitute;
using TemplateDesigner.Models;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Parametr, jehož hodnota patří vybrané lince (srovnávací linka rozdílového JŘ), se nepamatuje
/// a při výběru jiné linky se vrátí na výchozí - z minula by ukazoval na jinou trasu
/// (nález Jan 9. 10.).
/// </summary>
public class ParametryInstanceTests
{
    public ParametryInstanceTests() =>
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

    private static TemplateDesignerViewModel Vm(Dictionary<string, string>? ulozene = null)
    {
        var vm = new TemplateDesignerViewModel(null, null, []);
        vm.State.CurrentTemplate = new Template
        {
            AgendaCode = "LINKOVY_JR_AD_ROZDILOVY",
            Parameters = [Skupina, Linka],
            PrintParameters = ulozene,
            Sections = [new Section()],
        };
        vm.LoadAgendaParameters("LINKOVY_JR_AD_ROZDILOVY");
        return vm;
    }

    [Fact]
    public void PriznakSeNacteZeServeru()
    {
        var p = CacheAgendaProvider.ParseParameters("""
            [ { "name": "POROVNAT_LINKU", "type": "text", "perInstance": 1 },
              { "name": "POROVNAT_SKUPINU", "type": "text" } ]
            """);

        Assert.True(p.Single(x => x.Name == "POROVNAT_LINKU").PerInstance);
        Assert.False(p.Single(x => x.Name == "POROVNAT_SKUPINU").PerInstance);
    }

    [Fact]
    public void JinaLinka_VratiHodnotuNaVychozi()
    {
        var vm = Vm();
        vm.PreviewParamValues["POROVNAT_LINKU"] = "380760";
        vm.PreviewParamValues["POROVNAT_SKUPINU"] = "207";

        Assert.True(vm.VynulujParametryInstance());

        Assert.Equal("", vm.PreviewParamValues["POROVNAT_LINKU"]);
        Assert.Equal("207", vm.PreviewParamValues["POROVNAT_SKUPINU"]);   // skupina platí i pro další linky
        Assert.False(vm.VynulujParametryInstance());                      // nic dalšího k vynulování
    }

    [Fact]
    public async Task HodnotaUlozenaVSablone_SeNepouzije()
    {
        // Starší šablona mohla hodnotu uložit - otevření ji nesmí vrátit.
        var vm = Vm(new() { ["POROVNAT_LINKU"] = "380760", ["POROVNAT_SKUPINU"] = "203" });

        await vm.ApplyPersistedPrintParamsAsync();

        Assert.Equal("", vm.PreviewParamValues["POROVNAT_LINKU"]);
        Assert.Equal("203", vm.PreviewParamValues["POROVNAT_SKUPINU"]);
    }
}
