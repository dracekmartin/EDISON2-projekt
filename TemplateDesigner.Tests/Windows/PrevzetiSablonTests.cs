using EDControls;
using EDControls.EDList;
using EDControls.EDViewModel;
using NSubstitute;
using TemplateDesigner;
using TemplateDesigner.Serialization;
using TemplateDesigner.Services;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Příjem šablon od jiné firmy (`B189`, R9): převzetí a odmítnutí celé zásilky (<see cref="ReceivedShipments"/>),
/// uzel Přijaté šablony ve stromu, pruh v hlavním okně a čtení odpovědí serveru.
/// </summary>
public class PrevzetiSablonTests
{
    private readonly IPopupDisplayer _popup = Substitute.For<IPopupDisplayer>();
    private readonly ITemplateSharing _sdileni = Substitute.For<ITemplateSharing>();
    private readonly ReceivedShipments _zasilky;

    public PrevzetiSablonTests()
    {
        EDServices.Initialize(new EDServices(Substitute.For<IMLRest>(), Substitute.For<IWindowNavigator>(), _popup));
        _zasilky = new ReceivedShipments(_sdileni, _popup);
        _sdileni.AcceptShipmentAsync(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<string>>())
            .Returns([new TakenTemplate(Guid.NewGuid(), "Linky A4", "LINKOVY_JR_AD")]);
    }

    private static ReceivedTemplate Sablona(string soubor = "S1", bool agendaZnama = true, string verze = "1.3.0",
        ExistingTakenTemplate? existujici = null) =>
        new(soubor, "Linky A4", "LINKOVY_JR_AD", "Linkový JŘ · AD", verze, agendaZnama, existujici);

    private void Zasilka(params ReceivedTemplate[] sablony) => Zasilka("Z1", sablony);

    private void Zasilka(string id, params ReceivedTemplate[] sablony) =>
        _sdileni.GetShipmentTemplatesAsync(id).Returns(sablony);

    private static Shipment Z(string id = "Z1", string nazev = "Linky") => new(id, nazev, "ČSAD");

    private static ExistingTakenTemplate Prevzata(bool upravena = false) => new(Guid.NewGuid(), "Naše linky", upravena);

    [Fact]
    public async Task NovaSablona_PrevezmeSeBezDotazuARekneKdeJe()
    {
        Zasilka(Sablona());

        Assert.True(await _zasilky.AcceptAsync([Z()]));

        await _sdileni.Received(1).AcceptShipmentAsync("Z1", Arg.Is<IReadOnlyCollection<string>>(p => p.Count == 0));
        _popup.DidNotReceiveWithAnyArgs().ConfirmYesNoCancel(default!);
        _popup.Received(1).Info("Šablona „Linky A4“ je převzatá. Najdete ji mezi vlastními šablonami v agendě Linkový JŘ · AD.");
    }

    [Fact]
    public async Task NeznamaAgenda_ZastaviCelouZasilku()
    {
        Zasilka(Sablona("S1"), Sablona("S2", agendaZnama: false));

        Assert.False(await _zasilky.AcceptAsync([Z()]));

        await _sdileni.DidNotReceiveWithAnyArgs().AcceptShipmentAsync(default!, default!);
        _popup.Received(1).Info(Arg.Is<string>(t => t.Contains("„Linky“ nejde převzít") && t.Contains("LINKOVY_JR_AD")));
    }

    [Fact]
    public async Task NovejsiFormatNezNavrhar_ZastaviZasilku()
    {
        Zasilka(Sablona(verze: "99.0.0"));

        Assert.False(await _zasilky.AcceptAsync([Z()]));

        await _sdileni.DidNotReceiveWithAnyArgs().AcceptShipmentAsync(default!, default!);
        _popup.Received(1).Info(Arg.Is<string>(t => t.Contains("novější verze návrháře")));
    }

    [Fact]
    public void FormatStejnyJakoNavrhar_Projde() =>
        Assert.Null(ReceivedShipments.ReasonNotAccepted([Sablona(verze: TemplateMigrator.CurrentVersion)], TemplateMigrator.CurrentVersion));

    [Theory]
    [InlineData(true, new[] { "S1" })]
    [InlineData(false, new string[0])]
    public async Task UzPrevzata_AnoPrepiseNeUloziJakoNovou(bool ano, string[] prepsat)
    {
        Zasilka(Sablona(existujici: Prevzata()));
        _popup.ConfirmYesNoCancel(default!).ReturnsForAnyArgs(ano);

        Assert.True(await _zasilky.AcceptAsync([Z()]));

        await _sdileni.Received(1).AcceptShipmentAsync("Z1", Arg.Is<IReadOnlyCollection<string>>(p => p.SequenceEqual(prepsat)));
    }

    [Fact]
    public async Task UzPrevzata_StornoNicNeprevezme()
    {
        Zasilka(Sablona(existujici: Prevzata(upravena: true)));
        _popup.ConfirmYesNoCancel(default!).ReturnsForAnyArgs((bool?)null);

        Assert.False(await _zasilky.AcceptAsync([Z()]));

        await _sdileni.DidNotReceiveWithAnyArgs().AcceptShipmentAsync(default!, default!);
        _popup.DidNotReceiveWithAnyArgs().Info(default!);
    }

    // ── Víc zásilek naráz ──

    [Fact]
    public async Task VicZasilek_PrevezmeVsechnyAJednouZpravouRekneKde()
    {
        Zasilka("Z1", Sablona("S1"));
        Zasilka("Z2", new ReceivedTemplate("S2", "Vývěska", "ZASTAVKOVY_JR_AD", "Zastávkový JŘ · AD", "1.3.0", true, null));

        Assert.True(await _zasilky.AcceptAsync([Z("Z1"), Z("Z2")]));

        await _sdileni.Received(1).AcceptShipmentAsync("Z1", Arg.Any<IReadOnlyCollection<string>>());
        await _sdileni.Received(1).AcceptShipmentAsync("Z2", Arg.Any<IReadOnlyCollection<string>>());
        _popup.Received(1).Info("Převzato šablon: 2. Najdete je mezi vlastními šablonami v agendách Linkový JŘ · AD, "
                                + "Zastávkový JŘ · AD.");
    }

    [Fact]
    public async Task VicZasilek_NeprevzatelnaNezastaviOstatni()
    {
        Zasilka("Z1", Sablona(agendaZnama: false));
        Zasilka("Z2", Sablona());

        Assert.True(await _zasilky.AcceptAsync([Z("Z1", "Cizí agenda"), Z("Z2")]));

        await _sdileni.DidNotReceive().AcceptShipmentAsync("Z1", Arg.Any<IReadOnlyCollection<string>>());
        await _sdileni.Received(1).AcceptShipmentAsync("Z2", Arg.Any<IReadOnlyCollection<string>>());
        _popup.Received(1).Info(Arg.Is<string>(t => t.Contains("je převzatá") && t.Contains("Zásilku „Cizí agenda“ nejde převzít")));
    }

    [Fact]
    public async Task VicZasilek_ChybaServeruUJedneNezastaviOstatni()
    {
        _sdileni.GetShipmentTemplatesAsync("Z1").Returns(
            Task.FromException<IReadOnlyList<ReceivedTemplate>>(new CacheTemplateException("ISYCloud neodpovídá.")));
        Zasilka("Z2", Sablona());

        Assert.True(await _zasilky.AcceptAsync([Z("Z1", "První"), Z("Z2")]));

        await _sdileni.Received(1).AcceptShipmentAsync("Z2", Arg.Any<IReadOnlyCollection<string>>());
        _popup.Received(1).Info(Arg.Is<string>(t => t.Contains("Zásilku „První“ se nepodařilo převzít: ISYCloud neodpovídá.")));
    }

    [Fact]
    public async Task VicZasilek_StornoZastaviZbyleAleRekneCoSePrevzalo()
    {
        Zasilka("Z1", Sablona());
        Zasilka("Z2", Sablona(existujici: Prevzata()));
        Zasilka("Z3", Sablona());
        _popup.ConfirmYesNoCancel(default!).ReturnsForAnyArgs((bool?)null);

        Assert.True(await _zasilky.AcceptAsync([Z("Z1"), Z("Z2"), Z("Z3")]));

        await _sdileni.Received(1).AcceptShipmentAsync("Z1", Arg.Any<IReadOnlyCollection<string>>());
        await _sdileni.DidNotReceive().AcceptShipmentAsync("Z2", Arg.Any<IReadOnlyCollection<string>>());
        await _sdileni.DidNotReceive().GetShipmentTemplatesAsync("Z3");
        _popup.Received(1).Info(Arg.Is<string>(t => t.Contains("je převzatá") && t.EndsWith("Ostatní zásilky zůstaly k převzetí.")));
    }

    [Fact]
    public async Task VicZasilek_ObsahDalsiSeCteAzPoPrevzetiPredchozi()
    {
        // Dvě verze téže šablony ve dvou zásilkách: druhá se musí zeptat na přepsání té, kterou převzala první.
        Zasilka("Z1", Sablona());
        Zasilka("Z2", Sablona());

        await _zasilky.AcceptAsync([Z("Z1"), Z("Z2")]);

        Received.InOrder(() =>
        {
            _sdileni.GetShipmentTemplatesAsync("Z1");
            _sdileni.AcceptShipmentAsync("Z1", Arg.Any<IReadOnlyCollection<string>>());
            _sdileni.GetShipmentTemplatesAsync("Z2");
            _sdileni.AcceptShipmentAsync("Z2", Arg.Any<IReadOnlyCollection<string>>());
        });
    }

    [Fact]
    public async Task VicZasilek_OdmitnutiJednimPotvrzenim()
    {
        _popup.Confirm(default!).ReturnsForAnyArgs(true);

        Assert.True(await _zasilky.RejectAsync([Z("Z1", "A"), Z("Z2", "B")]));

        _popup.Received(1).Confirm(Arg.Is<string>(t => t.StartsWith("Odmítnout 2 zásilky („A“, „B“)?")));
        await _sdileni.Received(1).RejectShipmentAsync("Z1");
        await _sdileni.Received(1).RejectShipmentAsync("Z2");
    }

    [Fact]
    public void AkceNadZasilkami_ZaskrtnuteJinakVybrana()
    {
        var a = Radek("Z1");
        var b = Radek("Z2");
        var sablona = new EDListRow("T1", new() { ["name"] = "Vlastní" });

        Assert.Equal(["Z1", "Z2"], CacheTemplatePickerDialogViewModel.ShipmentsOf([a, b], selected: b).Select(z => z.Id));
        Assert.Equal(["Z2"], CacheTemplatePickerDialogViewModel.ShipmentsOf([], selected: b).Select(z => z.Id));
        Assert.Empty(CacheTemplatePickerDialogViewModel.ShipmentsOf([], selected: null));
        Assert.Empty(CacheTemplatePickerDialogViewModel.ShipmentsOf([], selected: sablona));
        Assert.Equal(new Shipment("Z1", "Linky A4", "ČSAD Autobusy"), CacheTemplatePickerDialogViewModel.ShipmentsOf([a], null)[0]);
    }

    [Fact]
    public void UpravenaPrevzata_OtazkaRikaZeUpravySeZtrati()
    {
        var upravena = ReceivedShipments.OverwriteQuestion(
            Sablona(existujici: new ExistingTakenTemplate(Guid.NewGuid(), "Naše linky", Modified: true)), "ČSAD");
        var neupravena = ReceivedShipments.OverwriteQuestion(
            Sablona(existujici: new ExistingTakenTemplate(Guid.NewGuid(), "Naše linky", Modified: false)), "ČSAD");

        Assert.Contains("upravili", upravena);
        Assert.Contains("úpravy se ztratí", upravena);
        Assert.DoesNotContain("upravili", neupravena);
        Assert.All([upravena, neupravena], t => Assert.Contains("„Naše linky“", t));
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public async Task Odmitnuti_JenPoPotvrzeni(bool potvrdi, int volani)
    {
        _popup.Confirm(default!).ReturnsForAnyArgs(potvrdi);

        Assert.Equal(potvrdi, await _zasilky.RejectAsync([Z("Z1", "Linky A4")]));

        await _sdileni.Received(volani).RejectShipmentAsync("Z1");
        _popup.Received(1).Confirm(Arg.Is<string>(t => t.StartsWith("Odmítnout zásilku „Linky A4“ od firmy ČSAD?")));
    }

    // ── Strom a okno ──

    [Fact]
    public void PrijateSablony_JenSeSdilenimNaKonciStromu()
    {
        var bez = TemplateCatalogNodeVM.Build(new TemplateCatalog([], Sharing: false));
        var se = TemplateCatalogNodeVM.Build(new TemplateCatalog([], Sharing: true), receivedCount: 2);

        Assert.DoesNotContain(bez, n => n.Kind == TemplateCatalogNodeKind.Received);
        var uzel = se[^1];
        Assert.Equal((TemplateCatalogNodeVM.ReceivedKey, "Přijaté šablony", (int?)2), (uzel.Key, uzel.Label, uzel.Count));
        Assert.Null(uzel.NewTemplateAgendaCode);
    }

    [Fact]
    public void PrijateSablony_PocetSeDoplniPozdeji()
    {
        var uzel = TemplateCatalogNodeVM.Build(new TemplateCatalog([], Sharing: true))[^1];
        Assert.Null(uzel.Count);

        uzel.SetCount(3);

        Assert.Equal(3, uzel.Count);
    }

    private static EDListRow Radek(string id = "Z1", string zprava = "", string odeslal = "novak") => new(id, new()
    {
        ["prijata"] = "1", ["name"] = "Linky A4", ["zdroj"] = "ČSAD Autobusy", ["odeslal"] = odeslal,
        ["zprava"] = zprava, ["sablony"] = "Linky A4, Linky A5",
    });

    [Fact]
    public void NapovedaZasilky_OdKohoCoAZprava()
    {
        var text = CacheTemplatePickerDialogViewModel.ReceivedHint(Radek(zprava: "Nové vzory"), 1);

        Assert.Contains("Od firmy ČSAD Autobusy (poslal novak): Linky A4, Linky A5.", text);
        Assert.Contains("Zpráva: Nové vzory", text);
        Assert.EndsWith("Převezměte ji akcí Převzít, nebo ji odmítněte.", text);
        Assert.DoesNotContain("Zpráva", CacheTemplatePickerDialogViewModel.ReceivedHint(Radek(), 1));
    }

    [Fact]
    public void NapovedaZasilky_SeZaskrtnutymiRekneZeAkcePlatiProNe() =>
        Assert.EndsWith("Zaškrtnuté zásilky (2) převezmete akcí Převzít, nebo je odmítnete.",
            CacheTemplatePickerDialogViewModel.ReceivedHint(Radek(), 3, checkedCount: 2));

    [Fact]
    public void NapovedaBezZasilek_ReknePrazdno()
    {
        Assert.Contains("neposlala", CacheTemplatePickerDialogViewModel.ReceivedHint(null, 0));
        Assert.Contains("Převzít", CacheTemplatePickerDialogViewModel.ReceivedHint(null, 2));
        Assert.Contains("Převzít", CacheTemplatePickerDialogViewModel.ReceivedHint(null, null));
    }

    // ── Pruh v hlavním okně a odpovědi serveru ──

    [Theory]
    [InlineData(0, 0, null)]
    [InlineData(1, 1, "Firma ČSAD vám poslala šablonu „Linky A4“.")]
    [InlineData(1, 3, "Firma ČSAD vám poslala 3 šablony.")]
    [InlineData(1, 5, "Firma ČSAD vám poslala 5 šablon.")]
    [InlineData(2, 1, "Jiné firmy vám poslaly šablony: 2 zásilky k převzetí.")]
    [InlineData(5, 1, "Jiné firmy vám poslaly šablony: 5 zásilek k převzetí.")]
    public void Pruh_VetaPodlePoctu(int zasilek, int sablon, string? veta) =>
        Assert.Equal(veta, new ReceivedSummary(zasilek, "ČSAD", "Linky A4", sablon).Notice);

    [Fact]
    public void Souhrn_CteOdpovedServeru()
    {
        // Tvar odpovědi `GetSouhrnPrijatych` (ověřený voláním serveru 3. 11.).
        var souhrn = ReceivedSummary.Parse("""{"pocet":1,"odesilatel":"CB_CSADAD (DEV server)","nazev":"Obrazek test","sablon":1}""");

        Assert.Equal(new ReceivedSummary(1, "CB_CSADAD (DEV server)", "Obrazek test", 1), souhrn);
        Assert.Equal(ReceivedSummary.None, ReceivedSummary.Parse("není JSON"));
    }

    [Fact]
    public void ObsahZasilky_CteSablonyIPrevzatou()
    {
        // Tvar odpovědi `GetObsahZasilky` (ověřený voláním serveru 3. 11.), druhá šablona s převzatou verzí.
        var id = Guid.NewGuid();
        var sablony = ReceivedTemplate.ParseList($$$"""
            {"sablony":[
              {"souborId":"45ab","name":"Obrazek test","agendaCode":"ZASTAVKOVY_JR_AD","version":"1.3.0",
               "agenda":"Zastávkový JŘ · AD","agendaZnama":true},
              {"souborId":"46cd","name":"Linky","agendaCode":"X","version":"1.18.0","agenda":"X","agendaZnama":false,
               "existujici":{"templateId":"{{{id}}}","name":"Naše linky","upravena":true}}]}
            """);

        Assert.Equal(new ReceivedTemplate("45ab", "Obrazek test", "ZASTAVKOVY_JR_AD", "Zastávkový JŘ · AD", "1.3.0", true, null),
            sablony[0]);
        Assert.False(sablony[1].AgendaKnown);
        Assert.Equal(new ExistingTakenTemplate(id, "Naše linky", Modified: true), sablony[1].Existing);
    }

    [Fact]
    public void Prevzate_CteUlozeneSablony()
    {
        var id = Guid.NewGuid();
        var prevzate = TakenTemplate.ParseList($$"""[{"templateId":"{{id}}","name":"Linky","agendaCode":"LINKOVY_JR_AD"},{"templateId":"x"}]""");

        Assert.Equal([new TakenTemplate(id, "Linky", "LINKOVY_JR_AD")], prevzate);
    }
}
