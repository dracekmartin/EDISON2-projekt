using System.IO;
using EDControls;
using EDControls.EDViewModel;
using NSubstitute;
using TemplateDesigner.Config;

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
            () => throw new InvalidOperationException("bez šablony se návrhář nemá vytvářet"))
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
}
