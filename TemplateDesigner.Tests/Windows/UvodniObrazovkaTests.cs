using System.Text.Json.Nodes;
using System.Windows;
using EDControls;
using EDControls.EDViewModel;
using EDControls.UserPrefs;
using NSubstitute;
using TemplateDesigner.Commands.Implementations;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Services;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Úvodní obrazovka po spuštění bez linky (zkušební verze, Jan 29.-30. 10.): co nabídne a že zmizí,
/// jakmile uživatel začne pracovat - zkušeného uživatele nesmí zdržovat.
/// </summary>
/// <remarks>
/// Obrazovka se tu ukazuje přímo (<c>StartPanel.Show</c>), ne přes <c>ShowStartPanelAsync</c>:
/// návrhář v testu si spojení bere z nastavení v programu a na stroji s lokálním připojením by
/// seznam šablon tahal z dev serveru.
/// </remarks>
public class UvodniObrazovkaTests
{
    private readonly IPopupDisplayer _popup = Substitute.For<IPopupDisplayer>();

    public UvodniObrazovkaTests() =>
        EDServices.Initialize(new EDServices(Substitute.For<IMLRest>(), Substitute.For<IWindowNavigator>(), _popup));

    private static readonly DateTime Dnes = new(2026, 10, 30, 10, 0, 0, DateTimeKind.Local);

    private static CacheTemplateMetadata Sablona(string nazev, string agenda, DateTime upraveno, string? kdo = null) =>
        new() { TemplateId = Guid.NewGuid(), Name = nazev, AgendaCode = agenda, ModifiedAt = upraveno, ModifiedBy = kdo };

    private static string NazevAgendy(string kod) => kod switch
    {
        "ZASTAVKOVY_JR_AD" => "Zastávkový JŘ · AD",
        "LINKOVY_JR_AD" => "Linkový JŘ · AD",
        _ => kod,
    };

    private static StartPanelViewModel Obrazovka() =>
        new(_ => Task.CompletedTask, () => Task.CompletedTask, () => Task.CompletedTask, () => Task.CompletedTask,
            () => Task.CompletedTask);

    [Fact]
    public void Pokracovat_VPoradiPrace_JenSablonyKtereVDatabaziJsou()
    {
        var vyveska = Sablona("Vývěska A4", "ZASTAVKOVY_JR_AD", Dnes.AddDays(-5));
        var knizni = Sablona("Knižní JŘ", "LINKOVY_JR_AD", Dnes.AddDays(-1));
        var smazana = Guid.NewGuid();

        var (posledni, _) = StartPanelViewModel.BuildLists([vyveska, knizni], [knizni.TemplateId, smazana, vyveska.TemplateId],
            "ZASTAVKOVY_JR_AD", NazevAgendy, Dnes);

        Assert.Equal(["Knižní JŘ", "Vývěska A4"], posledni.Select(p => p.Name));
        Assert.Equal("Linkový JŘ · AD · upraveno včera", posledni[0].Detail);
    }

    [Fact]
    public void SablonyFirmy_JenAgendy_OdNejnovejsi_BezTechVPokracovat()
    {
        var agenda = "ZASTAVKOVY_JR_AD";
        var sablony = Enumerable.Range(1, 7).Select(i => Sablona($"Vývěska {i}", agenda, Dnes.AddDays(-i))).ToList();
        sablony.Add(Sablona("Knižní JŘ", "LINKOVY_JR_AD", Dnes));

        var (posledni, firmy) = StartPanelViewModel.BuildLists(sablony, [sablony[0].TemplateId], agenda, NazevAgendy, Dnes);

        Assert.Equal(["Vývěska 1"], posledni.Select(p => p.Name));
        Assert.Equal(["Vývěska 2", "Vývěska 3", "Vývěska 4", "Vývěska 5", "Vývěska 6"], firmy.Select(p => p.Name));
    }

    /// <summary>Šablona bez kódu agendy je obecná - tak se i ukládá do databáze.</summary>
    [Fact]
    public void SablonaBezKoduAgendy_NabidneObecneSablony()
    {
        var obecna = Sablona("Oběžník", CacheTemplateService.GeneralAgendaCode, Dnes.AddDays(-40), "JM");

        var (_, firmy) = StartPanelViewModel.BuildLists([obecna], [], null, NazevAgendy, Dnes);

        // Bez čísla uživatele v závorce - v metadatech není jméno.
        Assert.Equal("upraveno 20. 9.", Assert.Single(firmy).Detail);
    }

    [Fact]
    public void PrvniUprava_ObrazovkuZavre()
    {
        var vm = new TemplateDesignerViewModel();
        vm.StartPanel.Show("Obecná (bez dat)", false, null, [], []);

        vm.Execute(new AddElementCommand(vm.State.CurrentTemplate.Sections[0], new TextElement { Text = "A" }));

        Assert.False(vm.StartPanel.IsVisible);
    }

    [Fact]
    public void VymenaSablony_ObrazovkuZavre()
    {
        var vm = new TemplateDesignerViewModel();
        vm.StartPanel.Show("Obecná (bez dat)", false, null, [], []);

        vm.State.CurrentTemplate = new Template();

        Assert.False(vm.StartPanel.IsVisible);
    }

    /// <summary>Obrazovka otevřená z menu nad neuloženou šablonou: IsDirty se úpravou nezmění,
    /// a obrazovka přesto musí zmizet.</summary>
    [Fact]
    public void UpravaNeulozeneSablony_ObrazovkuZavre()
    {
        var vm = new TemplateDesignerViewModel();
        vm.Execute(new AddElementCommand(vm.State.CurrentTemplate.Sections[0], new TextElement { Text = "A" }));
        vm.StartPanel.Show("Obecná (bez dat)", false, null, [], []);

        vm.Execute(new AddElementCommand(vm.State.CurrentTemplate.Sections[0], new TextElement { Text = "B" }));

        Assert.False(vm.StartPanel.IsVisible);
    }

    /// <summary>Po spuštění prázdná šablona na plátně už je - obrazovka se jen schová.</summary>
    [Fact]
    public async Task PrazdnaSablona_NaCistemPlatne_JenObrazovkuZavre()
    {
        var vm = new TemplateDesignerViewModel();
        var sablona = vm.State.CurrentTemplate;
        vm.StartPanel.Show("Obecná (bez dat)", false, null, [], []);

        await vm.StartBlankFromStartPanelAsync();

        Assert.False(vm.StartPanel.IsVisible);
        Assert.Same(sablona, vm.State.CurrentTemplate);
    }

    /// <summary>Z menu nad šablonou s obsahem založí novou jako Ctrl+N.</summary>
    [Fact]
    public async Task PrazdnaSablona_NadSablonouSObsahem_ZalozNovou()
    {
        var vm = new TemplateDesignerViewModel();
        var sablona = vm.State.CurrentTemplate;
        sablona.Sections[0].Elements.Add(new TextElement { Text = "Obsah" });
        vm.StartPanel.Show("Obecná (bez dat)", false, null, [], []);

        await vm.StartBlankFromStartPanelAsync();

        Assert.NotSame(sablona, vm.State.CurrentTemplate);
        Assert.All(vm.State.CurrentTemplate.Sections, s => Assert.Empty(s.Elements));
        Assert.False(vm.StartPanel.IsVisible);
    }

    /// <summary>Firma bez vlastních šablon (nový uživatel) uvidí v seznamu výchozí šablonu M-line -
    /// odliší ji jen ikona, žádné „Doporučeno" (Jan 30. 10.).</summary>
    [Fact]
    public void BezSablonFirmy_NabidneVychoziSablonu()
    {
        var obrazovka = Obrazovka();

        obrazovka.Show("Linkový JŘ · AD", canOpenFromDatabase: true, "Linkový JŘ AD", [], []);

        var vychozi = Assert.Single(obrazovka.AgendaTemplates);
        Assert.True(vychozi.IsMline);
        Assert.Equal("výchozí šablona M-line", vychozi.Detail);
    }

    /// <summary>Obecná agenda bez šablon firmy nemá co nabídnout - řekne to jedním řádkem.</summary>
    [Fact]
    public void ObecnaAgendaBezSablon_RekneZeZadneNejsou()
    {
        var obrazovka = Obrazovka();

        obrazovka.Show("Obecná (bez dat)", canOpenFromDatabase: true, null, [], []);

        Assert.True(obrazovka.HasNoAgendaTemplates);
    }

    /// <summary>Výchozí šablona M-line je jedna ze šablon agendy (otevře se a upraví jako šablona
    /// firmy), ne „nová šablona" - na konci seznamu, aby zkušenému nepřekážela (Jan 30. 10.).</summary>
    [Fact]
    public void VychoziSablonaMline_NaKonciSeznamu_OtevreKopii()
    {
        Guid? otevrena = null;
        bool vychozi = false;
        var obrazovka = new StartPanelViewModel(id => { otevrena = id; return Task.CompletedTask; }, () => Task.CompletedTask,
            () => { vychozi = true; return Task.CompletedTask; }, () => Task.CompletedTask, () => Task.CompletedTask);
        var firemni = new StartPanelItem(Guid.NewGuid(), "Knižní JŘ", "upraveno včera");

        obrazovka.Show("Linkový JŘ · AD", canOpenFromDatabase: true, "Linkový JŘ AD", [], [firemni]);

        Assert.Equal(["Knižní JŘ", "Linkový JŘ AD"], obrazovka.AgendaTemplates.Select(s => s.Name));
        Assert.True(obrazovka.AgendaTemplates[^1].IsMline);

        obrazovka.OpenTemplateCommand.Execute(obrazovka.AgendaTemplates[^1]);
        Assert.True(vychozi);
        Assert.Null(otevrena);

        obrazovka.OpenTemplateCommand.Execute(firemni);
        Assert.Equal(firemni.TemplateId, otevrena);
    }

    [Fact]
    public void Krizek_ObrazovkuZavre()
    {
        var obrazovka = Obrazovka();
        obrazovka.Show("Obecná (bez dat)", false, null, [], []);

        obrazovka.CloseCommand.Execute(null);

        Assert.False(obrazovka.IsVisible);
    }

    [Fact]
    public void BezServeru_NenabidneDatabaziAniHotovouSablonuKteraNeni()
    {
        var obrazovka = Obrazovka();
        obrazovka.Show("Obecná (bez dat)", canOpenFromDatabase: false, systemTemplateName: null, [], []);

        Assert.False(obrazovka.OpenFromDatabaseCommand.CanExecute(null));
        Assert.Empty(obrazovka.AgendaTemplates);
        Assert.True(obrazovka.StartBlankCommand.CanExecute(null));
    }

    /// <summary>Data z SQL a CSV návrhář zatím neumí - nabídka uživateli řekne, ať se ozve.</summary>
    [Fact]
    public void DataOdjinud_RekneZeZatimNejdou()
    {
        var obrazovka = Obrazovka();

        obrazovka.ConnectSqlCommand.Execute(null);
        obrazovka.LoadCsvCommand.Execute(null);

        _popup.Received(1).Info(Arg.Is<string>(t => t.StartsWith("Připojení k databázi SQL") && t.Contains("zatím neumí")), Arg.Any<Window?>());
        _popup.Received(1).Info(Arg.Is<string>(t => t.StartsWith("Načtení dat ze souboru CSV")), Arg.Any<Window?>());
    }

    [Fact]
    public async Task PosledniSablony_NejnovejsiPrvni_BezDuplicit_NejvysDeset()
    {
        var prefs = Substitute.For<IUserPrefsService>();
        var service = new TdPreferencesService(prefs);
        var ids = Enumerable.Range(0, 12).Select(_ => Guid.NewGuid()).ToList();

        foreach (var id in ids) await service.AddRecentTemplateAsync(id);
        await service.AddRecentTemplateAsync(ids[5]);

        Assert.Equal(10, service.RecentTemplates.Count);
        Assert.Equal(ids[5], service.RecentTemplates[0]);
        Assert.Equal(ids[11], service.RecentTemplates[1]);
        Assert.Single(service.RecentTemplates, ids[5]);
        await prefs.Received().SaveAsync("TemplateDesigner", "RecentTemplates", Arg.Any<JsonNode>());
    }
}
