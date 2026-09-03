using System.Text.Json.Nodes;
using EDControls;
using EDControls.EDViewModel;
using EDControls.UserPrefs;
using NSubstitute;
using TemplateDesigner.Serialization;
using TemplateDesigner.Services;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Skrytá (nezveřejněná) agenda se nesmí vrátit pamětí aplikace.
/// </summary>
/// <remarks>
/// Linkové JŘ se před vydáním skryly, ale Jan je v aplikaci pořád viděl - a bylo to takhle:
/// naposledy použitou agendu si aplikace ukládá do uživatelských preferencí a po startu ji
/// obnovila **bez ohledu na to, jestli je zveřejněná**. Stala se tím agendou šablony a výběr agend
/// ji pak ukázal, protože aktuální agendu zobrazuje vždy (aby šly otevřít hotové šablony).
/// Skrytí se tedy dalo obejít tím, že si ji člověk kdysi vyzkoušel.
/// </remarks>
public class SkrytaAgendaTests
{
    public SkrytaAgendaTests() =>
        EDServices.Initialize(new EDServices(Substitute.For<IMLRest>(),
            Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()));

    private static readonly AgendaRegistryEntry Zastavkovy =
        new("ZASTAVKOVY_JR_AD", "Zastávkový JŘ", "ALVA.Test.Zastavkovy", Released: true);

    private static readonly AgendaRegistryEntry Linkovy =
        new("LINKOVY_JR_AD", "Linkový JŘ", "ALVA.Test.Linkovy", Released: false);

    /// <summary>Preference s uloženou naposledy použitou agendou.</summary>
    private static async Task<TdPreferencesService> PrefsSPosledniAgendou(string kod)
    {
        var prefs = Substitute.For<IUserPrefsService>();
        prefs.LoadAsync(Arg.Any<string>(), Arg.Any<string>())
             .Returns(_ => Task.FromResult<JsonNode?>(JsonNode.Parse($$"""{"LastAgendaCode":"{{kod}}"}""")));

        var service = new TdPreferencesService(prefs);
        await service.LoadAsync();
        return service;
    }

    [Fact]
    public async Task SkrytaAgendaZPreferenci_SeNeobnovi()
    {
        var prefs = await PrefsSPosledniAgendou(Linkovy.Code);
        var vm = new TemplateDesignerViewModel(null, prefs, [Zastavkovy, Linkovy]);

        Assert.NotEqual(Linkovy.Code, vm.State.CurrentTemplate.AgendaCode);
    }

    [Fact]
    public async Task ZverejnenaAgendaZPreferenci_SeObnovi()
    {
        var prefs = await PrefsSPosledniAgendou(Zastavkovy.Code);
        var vm = new TemplateDesignerViewModel(null, prefs, [Zastavkovy, Linkovy]);

        Assert.Equal(Zastavkovy.Code, vm.State.CurrentTemplate.AgendaCode);
    }

    [Fact]
    public void VychoziSablonaMaZverejnenouAgendu()
    {
        // Konstanta ve výchozí šabloně obcházela skrytí: nová šablona startovala na skryté agendě
        // a výběr ji pak ukázal, protože aktuální agendu zobrazuje vždy. Tenhle test to zavře -
        // kdo konstantu přepíše na skrytou agendu, spadne tady.
        var kod = TemplateDesignerViewModel.CreateSampleTemplate().AgendaCode;

        Assert.Equal("ZASTAVKOVY_JR_AD", kod);
    }

    [Fact]
    public async Task SkrytaAgendaZLaunchArgumentu_SeNastavi()
    {
        // Kód ze shellu je explicitní příkaz zvenčí; skrytá agenda tam znamená chybu konfigurace
        // shellu, ne zapamatovaný stav uživatele - proto se tady filtr záměrně neuplatní.
        var prefs = await PrefsSPosledniAgendou(Zastavkovy.Code);
        var vm = new TemplateDesignerViewModel(null, prefs, [Zastavkovy, Linkovy], Linkovy.Code);

        Assert.Equal(Linkovy.Code, vm.State.CurrentTemplate.AgendaCode);
    }
}
