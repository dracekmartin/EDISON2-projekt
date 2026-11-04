using System.IO;
using EDControls;
using EDControls.EDViewModel;
using EDTesting;
using NSubstitute;
using TemplateDesigner.Config;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// PDF ze spuštění bez hlavního okna (<c>Vystup=PDF</c>, rozdílový JŘ z porovnání linek v modulu JŘ).
/// Celek s daty serveru je v <see cref="SpusteniPdfNaostro"/>.
/// </summary>
public class SpusteniPdfTests : IDisposable
{
    private readonly IPopupDisplayer _popup = Substitute.For<IPopupDisplayer>();
    private readonly string _slozka = Path.Combine(Path.GetTempPath(), "TD-SpusteniPdfTests-" + Guid.NewGuid());

    public SpusteniPdfTests() =>
        EDServices.Initialize(new EDServices(Substitute.For<IMLRest>(), Substitute.For<IWindowNavigator>(), _popup));

    public void Dispose()
    {
        try { Directory.Delete(_slozka, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void JmenoPdf_BezZakazanychZnaku_StarySouborSePrepise()
    {
        var prvni = TemplateDesignerViewModel.LaunchPdfPath(_slozka, "Linkový JŘ (AD) - rozdílový A4 - 380760 - Tábor/Písek");
        File.WriteAllText(prvni, "starý");

        var druhy = TemplateDesignerViewModel.LaunchPdfPath(_slozka, "Linkový JŘ (AD) - rozdílový A4 - 380760 - Tábor/Písek");

        Assert.Equal("Linkový JŘ (AD) - rozdílový A4 - 380760 - Tábor_Písek.pdf", Path.GetFileName(prvni));
        Assert.Equal(prvni, druhy);
        Assert.False(File.Exists(druhy));   // místo pro nové PDF, starý soubor je pryč
    }

    [Fact]
    public void OtevrenyStarySoubor_NovePdfDostaneCas()
    {
        var cesta = TemplateDesignerViewModel.LaunchPdfPath(_slozka, "Rozdílový JŘ");
        using (new FileStream(cesta, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var nova = TemplateDesignerViewModel.LaunchPdfPath(_slozka, "Rozdílový JŘ");

            Assert.NotEqual(cesta, nova);
            Assert.StartsWith("Rozdílový JŘ ", Path.GetFileName(nova));
        }
    }

    [Fact]
    public async Task AgendaBezSablony_RekneProcAOknoZavre()
    {
        bool otevreno = false, zavreno = false;
        var vm = new LaunchPdfWindowViewModel(LaunchInput.Parse("Agenda=NEEXISTUJE;LINKA_ID=1;Vystup=PDF"),
            Task.FromResult(new AgendaRegistryLoad([])),
            _ => throw new InvalidOperationException("bez šablony se návrhář nemá vytvářet"))
        {
            Folder = _slozka,
            OpenPdf = _ => otevreno = true,
        };
        vm.RequestClose += _ => zavreno = true;

        await vm.RunAsync();

        _popup.Received(1).Info(Arg.Is<string>(s => s.Contains("NEEXISTUJE")), Arg.Any<System.Windows.Window?>());
        Assert.False(otevreno);
        Assert.True(zavreno);
    }

    [Fact]
    public void SlozkaZeSpusteni_PromenneProstrediSeRozbali_BezNiVychozi()
    {
        var promenna = "TD_SLOZKA_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(promenna, _slozka);
        try
        {
            Assert.Equal(Path.Combine(_slozka, "Porovnani"), LaunchPdfWindowViewModel.OutputFolder(
                LaunchInput.Parse($"Agenda=X;Slozka=%{promenna}%\\Porovnani;Vystup=PDF"), "vychozi"));
            Assert.Equal("vychozi", LaunchPdfWindowViewModel.OutputFolder(LaunchInput.Parse("Agenda=X;Vystup=PDF"), "vychozi"));
            Assert.Equal("vychozi", LaunchPdfWindowViewModel.OutputFolder(LaunchInput.Parse("Agenda=X;Slozka= ;Vystup=PDF"), "vychozi"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(promenna, null);
        }
    }

    [Fact]
    public async Task DoSlozkyNejdeZapisovat_RekneKteraAPdfNevznikne()
    {
        // Složka nejde založit: v cestě stojí soubor. Zjistit se to musí před načtením dat.
        Directory.CreateDirectory(_slozka);
        var soubor = Path.Combine(_slozka, "soubor");
        File.WriteAllText(soubor, "");
        var cil = Path.Combine(soubor, "Porovnani");
        var vm = new LaunchPdfWindowViewModel(LaunchInput.Parse($"{Porovnani.Replace(";Vystup=PDF", "")};Slozka={cil};Vystup=PDF"),
            Task.FromResult(new AgendaRegistryLoad([])),
            _ => throw new InvalidOperationException("bez zapisovatelné složky se návrhář nemá vytvářet"))
        {
            Folder = _slozka,
            OpenPdf = _ => throw new InvalidOperationException("PDF nemá vzniknout"),
        };

        await vm.RunAsync();

        _popup.Received(1).Info(Arg.Is<string>(s => s.Contains($"Do složky {cil} nejde zapisovat") && s.Contains("Porovnání linek")),
            Arg.Any<System.Windows.Window?>());
        Assert.False(LaunchPdfWindowViewModel.CanWriteTo("Porovnani"));   // relativní cesta by mířila kamkoli
        Assert.True(LaunchPdfWindowViewModel.CanWriteTo(_slozka));
    }

    [Fact]
    public async Task HromadnyTisk_DoSlozkyNejdeZapisovat_RadiFormularTisku()
    {
        // Hromadný tisk z okna JŘ volí složku ve formuláři tisku, ne v okně Porovnání linek.
        Directory.CreateDirectory(_slozka);
        var soubor = Path.Combine(_slozka, "soubor");
        File.WriteAllText(soubor, "");
        var vm = new LaunchPdfWindowViewModel(LaunchInput.Parse($"Agenda=LINKOVY_JR_AD;LINKA_ID=0330530,0330531;Slozka={soubor}\\Tisk;Vystup=PDF"),
            Task.FromResult(new AgendaRegistryLoad([])),
            _ => throw new InvalidOperationException("bez zapisovatelné složky se návrhář nemá vytvářet"))
        {
            Folder = _slozka,
            OpenPdf = _ => throw new InvalidOperationException("PDF nemá vzniknout"),
        };

        await vm.RunAsync();

        _popup.Received(1).Info(Arg.Is<string>(s => s.Contains("nejde zapisovat") && s.Contains("formuláři tisku")),
            Arg.Any<System.Windows.Window?>());
    }

    /// <summary>Formulář tisku okna JŘ posílá členění a název souboru pod klíči tiskového okna návrháře (Jan 4. 11.);
    /// vzor se složenými závorkami projde beze změny.</summary>
    [Fact]
    public void CleneniANazevSouboruZeSpusteni()
    {
        var vstup = LaunchInput.Parse("Agenda=LINKOVY_JR_AD;LINKA_ID=1,2;__EXPORT_SPLIT=PER_LINE;__EXPORT_FILENAME={CISLO_LINKY}_JR;Vystup=PDF");

        Assert.Equal("PER_LINE", vstup.Get(LaunchInput.SplitKey));
        Assert.Equal("{CISLO_LINKY}_JR", vstup.Get(LaunchInput.FileNameKey));
        Assert.Null(LaunchInput.Parse("Agenda=LINKOVY_JR_AD;LINKA_ID=1;Vystup=PDF").Get(LaunchInput.SplitKey));
    }

    /// <summary>Soubory po linkách se neotevírají, uživatel dostane počet a složku.</summary>
    [Theory]
    [InlineData(3, "Uloženo 3 soubory PDF do složky JR.")]
    [InlineData(12, "Uloženo 12 souborů PDF do složky JR.")]
    public void SouboryPoLinkach_PotvrzeniSPoctemASlozkou(int souboru, string text) =>
        Assert.Equal(text, LaunchPdfWindowViewModel.UlozenoText(souboru, "JR"));

    [Fact]
    public void LinkyZeSpusteni_CarkouVPoradiVolajiciho_BezPrazdnychAOpakovanych()
    {
        var vstup = LaunchInput.Parse("Agenda=LINKOVY_JR_AD;LINKA_ID=0330531, 0330530,,0330531;Vystup=PDF");

        Assert.Equal(["0330531", "0330530"], vstup.Instances("LINKA_ID"));
        Assert.Equal(["0380760"], LaunchInput.Parse(Porovnani).Instances("LINKA_ID"));
        Assert.Empty(vstup.Instances("INSTANCE_ID"));
    }

    [Fact]
    public void LinkyBezDat_SeznamSDuvodemZkracenyNaDeset()
    {
        var vynechane = Enumerable.Range(1, 12).Select(i => ($"33050{i}", i == 1 ? null : (string?)$"důvod {i}")).ToList();

        var seznam = TemplateDesignerViewModel.SeznamVynechanych(vynechane);

        var radky = seznam.Split('\n');
        Assert.Equal(11, radky.Length);
        Assert.Equal("330501: nemá data.", radky[0]);
        Assert.Equal("330502: důvod 2", radky[1]);
        Assert.Equal("… a dalších 2", radky[^1]);
    }

    // Vstup z porovnání linek v ISYJR2 (TiskJRHelper.OpenRozdilovyJR).
    private const string Porovnani =
        "Agenda=LINKOVY_JR_AD_ROZDILOVY;SKUPINA_LINEK_ID=307;LINKA_ID=0380760;POROVNAT_SKUPINU=207;POROVNAT_LINKU=0380760;Vystup=PDF";

    private readonly List<Exception> _nahlaseno = [];

    private LaunchPdfWindowViewModel OknoPorovnani(AgendaRegistryLoad registr) =>
        new(LaunchInput.Parse(Porovnani), Task.FromResult(registr),
            _ => throw new InvalidOperationException("bez agendy ze serveru se návrhář nemá vytvářet"))
        {
            Folder = _slozka,
            OpenPdf = _ => throw new InvalidOperationException("PDF nemá vzniknout"),
            ReportFailure = _nahlaseno.Add,
        };

    [Fact]
    public async Task ServerNedostupny_RekneZeSeNespojilAComuPredatIT()
    {
        // Firewall zablokoval návrháři spojení a hláška dřív tvrdila, že spuštění neurčuje linku.
        // Hlášku čte provoz, adresa serveru ani text od systému v ní být nemají.
        const string zakazano = "Došlo k pokusu o přístup k soketu způsobem zakázaným jeho přístupovými oprávněními. " +
                                "[::ffff:10.0.0.1]:57772 (10.0.0.1:57772)";
        var vypadek = new Exception(MLRestErrors.Transport("UnknownError", zakazano));
        var hlasky = new List<string>();
        _popup.Info(Arg.Do<string>(hlasky.Add), Arg.Any<System.Windows.Window?>());
        bool zavreno = false;
        var vm = OknoPorovnani(new AgendaRegistryLoad([], vypadek));
        vm.RequestClose += _ => zavreno = true;

        await vm.RunAsync();

        var hlaska = Assert.Single(hlasky);
        Assert.Contains("nespojil se serverem", hlaska);
        Assert.Contains("firewall", hlaska);
        Assert.Contains("TemplateDesigner.exe", hlaska);
        Assert.DoesNotContain("10.0.0.1", hlaska);
        Assert.DoesNotContain("57772", hlaska);
        Assert.DoesNotContain("soketu", hlaska);
        Assert.Same(vypadek, Assert.Single(_nahlaseno));
        _popup.DidNotReceiveWithAnyArgs().Error(default!);
        Assert.True(zavreno);
    }

    [Fact]
    public async Task ChybaServeru_JdeJakoChybaProgramu()
    {
        var chyba = new Exception(MLRestErrors.ServerError("ERROR #5001: <UNDEFINED> zListAgendy+3"));

        await OknoPorovnani(new AgendaRegistryLoad([], chyba)).RunAsync();

        _popup.Received(1).Error(chyba, Arg.Any<System.Windows.Window?>());
        _popup.DidNotReceiveWithAnyArgs().Info(default!);
    }

    [Fact]
    public async Task ServerAgendyNezna_RekneKterou()
    {
        await OknoPorovnani(new AgendaRegistryLoad([new AgendaRegistryEntry("GENERAL", "Obecná (bez dat)", "")])).RunAsync();

        _popup.Received(1).Info(Arg.Is<string>(s => s.Contains("LINKOVY_JR_AD_ROZDILOVY") && !s.Contains("linku")),
            Arg.Any<System.Windows.Window?>());
    }
}
