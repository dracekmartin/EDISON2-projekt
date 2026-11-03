using EDControls;
using EDControls.EDViewModel;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TemplateDesigner.Services;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Okno „Odeslat jiné firmě" (`B189`, R9): uživatel zaškrtne vlastní šablony a firmy, server je pošle přes sdílení
/// dat ISYCloudu. Spojení s ISYCloudem tu není - služba je náhradní a test hlídá, co okno pošle a co ukáže.
/// </summary>
public class OdeslaniSablonTests
{
    private readonly IPopupDisplayer _popup = Substitute.For<IPopupDisplayer>();
    private readonly ITemplateSharing _sdileni = Substitute.For<ITemplateSharing>();

    private static readonly Guid Linkova = Guid.NewGuid(), Vyveska = Guid.NewGuid(), Druha = Guid.NewGuid();

    public OdeslaniSablonTests()
    {
        EDServices.Initialize(new EDServices(Substitute.For<IMLRest>(), Substitute.For<IWindowNavigator>(), _popup));
        _sdileni.GetRecipientsAsync().Returns([
            new SharingRecipient("CB_DPMCB", "Dopravní podnik České Budějovice", "DPMČB"),
            new SharingRecipient("CB_CSADJH", "ČSAD Jindřichův Hradec", "ČSAD JH"),
        ]);
    }

    private SendTemplatesDialogViewModel Okno(Guid? vybrana = null) => new(_sdileni,
    [
        (Vyveska, "Vývěska", "Zastávkový JŘ · AD"),
        (Linkova, "Linky A4", "Linkový JŘ · AD"),
        (Druha, "Linky A5", "Linkový JŘ · AD"),
    ], vybrana);

    [Fact]
    public void VybranaSablonaJePredemZaskrtnuta_SerazenePodleAgendyANazvu()
    {
        var okno = Okno(Vyveska);

        Assert.Equal(["Linky A4", "Linky A5", "Vývěska"], okno.Templates.Select(t => t.Title));
        Assert.Equal([Vyveska.ToString()], okno.Templates.Where(t => t.IsChecked).Select(t => t.Key));
    }

    [Fact]
    public async Task OdeslatJdeAzSeSablonouIFirmou()
    {
        var okno = Okno();
        await okno.LoadAsync();
        Assert.False(okno.CanSend());

        okno.Templates[0].IsChecked = true;
        Assert.False(okno.CanSend());
        okno.VisibleRecipients[0].IsChecked = true;
        Assert.True(okno.CanSend());
        Assert.Equal("1 šablona, 1 firma", okno.Summary);

        okno.Templates[1].IsChecked = true;
        okno.Templates[2].IsChecked = true;
        Assert.Equal("3 šablony, 1 firma", okno.Summary);
    }

    [Fact]
    public async Task FirmyJsouSerazeneAHledajiSePodleNazvuZakaznikaIZkratky()
    {
        var okno = Okno();
        await okno.LoadAsync();

        okno.RecipientFilter = "čsad";
        Assert.Equal(["CB_CSADJH"], okno.VisibleRecipients.Select(r => r.Key));
        okno.RecipientFilter = "dpmčb";
        Assert.Equal(["CB_DPMCB"], okno.VisibleRecipients.Select(r => r.Key));
        okno.RecipientFilter = "cb_ budějovice";
        Assert.Equal(["CB_DPMCB"], okno.VisibleRecipients.Select(r => r.Key));
        okno.RecipientFilter = "";
        Assert.Equal(2, okno.VisibleRecipients.Count);
    }

    /// <summary>Zaškrtnutí přežije hledání - firma zaškrtnutá před zúžením seznamu se pošle.</summary>
    [Fact]
    public async Task ZaskrtnutaFirmaSePosleIPoZmeneHledani()
    {
        var okno = Okno(Linkova);
        await okno.LoadAsync();
        okno.RecipientFilter = "jindřich";
        okno.VisibleRecipients[0].IsChecked = true;
        okno.RecipientFilter = "budějovice";
        _sdileni.SendTemplatesAsync(default!, default!, default!).ReturnsForAnyArgs(new SharingSendResult(1, 1));

        okno.SendCommand.Execute(null);

        await _sdileni.Received(1).SendTemplatesAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { Linkova })),
            Arg.Is<IReadOnlyCollection<string>>(f => f.SequenceEqual(new[] { "CB_CSADJH" })), "");
    }

    [Fact]
    public async Task OdeslaniPosleZpravuPotvrdiAZavreOkno()
    {
        var okno = Okno(Linkova);
        await okno.LoadAsync();
        okno.Templates.Single(t => t.Key == Druha.ToString()).IsChecked = true;
        foreach (var firma in okno.VisibleRecipients) firma.IsChecked = true;
        okno.Message = "  Posíláme nové šablony.  ";
        _sdileni.SendTemplatesAsync(default!, default!, default!).ReturnsForAnyArgs(new SharingSendResult(2, 1));
        bool? zavreno = null;
        okno.RequestClose += r => zavreno = r;

        okno.SendCommand.Execute(null);

        await _sdileni.Received(1).SendTemplatesAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.ToHashSet().SetEquals(new[] { Linkova, Druha })),
            Arg.Is<IReadOnlyCollection<string>>(f => f.ToHashSet().SetEquals(new[] { "CB_DPMCB", "CB_CSADJH" })),
            "Posíláme nové šablony.");
        _popup.Received(1).Info(Arg.Is<string>(t => t.StartsWith("Odesláno: 2 šablony pro ")));
        Assert.True(zavreno);
    }

    /// <summary>Chyba sdílení (firma bez povolení, nedostupný ISYCloud) se ukáže a okno zůstane, výběr se neztratí.</summary>
    [Fact]
    public async Task ChybaOdeslaniNechaOknoOtevrene()
    {
        var okno = Okno(Linkova);
        await okno.LoadAsync();
        okno.VisibleRecipients[0].IsChecked = true;
        _sdileni.SendTemplatesAsync(default!, default!, default!)
            .ThrowsAsyncForAnyArgs(new CacheTemplateException("Firma nemá povolené sdílení dat. Povolí ho podpora M-line."));
        bool zavreno = false;
        okno.RequestClose += _ => zavreno = true;

        okno.SendCommand.Execute(null);

        _popup.Received(1).Error(Arg.Is<Exception>(e => e.Message.StartsWith("Firma nemá povolené")));
        Assert.False(zavreno);
        Assert.True(okno.CanSend());
    }

    [Fact]
    public async Task NedostupnyIsyCloudUkazeDuvodMistoFirem()
    {
        _sdileni.GetRecipientsAsync().ThrowsAsync(new CacheTemplateException("ISYCloud pro sdílení dat není dostupný. Zkuste to později."));
        var okno = Okno();

        await okno.LoadAsync();

        Assert.False(okno.IsLoading);
        Assert.True(okno.HasRecipientsError);
        Assert.StartsWith("ISYCloud pro sdílení dat není dostupný", okno.RecipientsError);
        Assert.Empty(okno.VisibleRecipients);
    }

    [Fact]
    public async Task ZadnaJinaFirmaSeSdilenim()
    {
        _sdileni.GetRecipientsAsync().Returns([]);
        var okno = Okno();

        await okno.LoadAsync();

        Assert.Equal("Žádná jiná firma nemá sdílení dat povolené.", okno.RecipientsError);
    }

    [Theory]
    [InlineData(1, "1 šablona")]
    [InlineData(4, "4 šablony")]
    [InlineData(5, "5 šablon")]
    public void PotvrzeniOdeslaniSklonujePocet(int pocet, string ocekavano) =>
        Assert.StartsWith($"Odesláno: {ocekavano} pro DPMČB, ČSAD JH.",
            SendTemplatesDialogViewModel.SentText(pocet, ["DPMČB", "ČSAD JH"]));

    /// <summary>Okno jde sestavit (XAML, zdroje, vazby na view model) - jinak by spadlo až uživateli při otevření.
    /// Firmy se načítají až po zobrazení, tady se okno nezobrazuje.</summary>
    [Fact]
    public void OknoSeSestavi() =>
        EDTesting.Sta.RunOnAppThread(() =>
        {
            ZdrojeAplikace();
            var okno = new SendTemplatesDialog(Okno(Linkova));
            Assert.Equal("Odeslat šablony jiné firmě", okno.Title);
            var seznamy = NajdiVse<System.Windows.Controls.ListBox>(okno).ToList();
            Assert.Equal(2, seznamy.Count);
            Assert.Equal(3, seznamy[0].Items.Count);
            okno.Close();
        });

    private static void ZdrojeAplikace()
    {
        var app = System.Windows.Application.Current
                  ?? new System.Windows.Application { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
        if (app.TryFindResource("EDStrings.Cancel") is not null) return;
        foreach (var slovnik in new[] { "EDTheme/EDTheme.xaml", "EDControlStyles/EDControlStyles.xaml",
                                        "EDIcons/EDIcons.xaml", "EDStrings/EDStrings.xaml" })
            app.Resources.MergedDictionaries.Add(new System.Windows.ResourceDictionary
                { Source = new Uri("pack://application:,,,/EDControls;component/" + slovnik) });
    }

    private static IEnumerable<T> NajdiVse<T>(System.Windows.DependencyObject koren) where T : System.Windows.DependencyObject
    {
        foreach (var dite in System.Windows.LogicalTreeHelper.GetChildren(koren).OfType<System.Windows.DependencyObject>())
        {
            if (dite is T nalez) yield return nalez;
            foreach (var hlubsi in NajdiVse<T>(dite)) yield return hlubsi;
        }
    }

    // ── Odpovědi serveru ──────────────────────────────────────────────────────

    [Fact]
    public void StavSdileni()
    {
        Assert.Equal(new SharingState(true, "ČSAD", ""), SharingState.Parse("""{"aktivni":true,"nazev":"ČSAD","duvod":""}"""));
        Assert.Equal(new SharingState(false, "", "Firma nemá povolené sdílení dat."),
            SharingState.Parse("""{"aktivni":false,"nazev":"","duvod":"Firma nemá povolené sdílení dat."}"""));
        Assert.False(SharingState.Parse("").Active);
        Assert.False(SharingState.Parse("nejson").Active);
    }

    [Fact]
    public void PrijemciSerazeniBezZkratkyVynechani()
    {
        var prijemci = SharingRecipient.ParseList(
            """[{"idfa":"CB_B","nazev":"Beta","zakaznik":null},{"idfa":"","nazev":"Bez zkratky"},{"idfa":"CB_A","nazev":"alfa","zakaznik":"Zákazník"}]""");

        Assert.Equal(["CB_A", "CB_B"], prijemci.Select(p => p.IdFa));
        Assert.Equal("", prijemci[1].Customer);
        Assert.Empty(SharingRecipient.ParseList(null));
    }

    [Fact]
    public void StromRikaJestliOknoNabiziSdileni()
    {
        Assert.True(TemplateCatalog.Parse("""{"oblasti":[],"sdileni":true}""").Sharing);
        Assert.False(TemplateCatalog.Parse("""{"oblasti":[]}""").Sharing);
    }

    [Fact]
    public void NazevAgendySablonyJakoVeStromu()
    {
        var strom = TemplateCatalog.Parse("""
            {"oblasti":[{"code":"JR","label":"Jízdní řády","agendy":[
              {"code":"LINKOVY_JR","label":"Linkový JŘ","varianty":[{"code":"LINKOVY_JR_AD","label":"AD"}]},
              {"code":"GENERAL","label":"Obecná (bez dat)","varianty":[{"code":"GENERAL","label":""}]}]}]}
            """);

        Assert.Equal("Linkový JŘ · AD", strom.AgendaTitle("LINKOVY_JR_AD"));
        Assert.Equal("Obecná (bez dat)", strom.AgendaTitle("GENERAL"));
        Assert.Equal("NEZNAMA", strom.AgendaTitle("NEZNAMA"));
    }
}
