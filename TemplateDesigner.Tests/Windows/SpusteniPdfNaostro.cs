using System.IO;
using EDControls;
using EDControls.EDViewModel;
using NSubstitute;
using TemplateDesigner.Config;
using TemplateDesigner.Serialization;
using TemplateDesigner.Services;
using UglyToad.PdfPig;
using Xunit.Abstractions;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Ruční zkouška PDF ze spuštění proti dev serveru: přesně ten vstup, který posílá porovnání linek
/// v ISYJR2, a z něj PDF bez hlavního okna. Ověří, že PDF vznikne a že se opravdu porovnávalo
/// (hlavička nehlásí „Neporovnáno").
/// </summary>
/// <remarks>
/// Bez proměnné <c>TD_SPUSTENI_PDF</c> (složka pro PDF) nic nedělá. Připojení
/// z <c>templatedesigner.appsettings.local.json</c> (VPN), firma přes <c>TD_IDFA</c>/<c>TD_NAMESPACE</c>:
/// <code>
/// TD_SPUSTENI_PDF=slozka TD_IDFA=CB_CSADAD TD_NAMESPACE=CB_CSADAD
/// [TD_SPUSTENI_VSTUP="Agenda=...;Vystup=PDF"] dotnet test TemplateDesigner.Tests --filter SpusteniPdfNaostro
/// </code>
/// </remarks>
public class SpusteniPdfNaostro(ITestOutputHelper output)
{
    // CB_CSADAD: 307 = objížďka Blatná-Vrbenská (380735, 380760), 207 = řádné změny ST.
    private const string VychoziVstup =
        "Agenda=LINKOVY_JR_AD_ROZDILOVY;SKUPINA_LINEK_ID=307;LINKA_ID=0380760;POROVNAT_SKUPINU=207;POROVNAT_LINKU=0380760;Vystup=PDF";

    [Fact]
    public async Task Zkouska()
    {
        string? slozka = Environment.GetEnvironmentVariable("TD_SPUSTENI_PDF");
        if (string.IsNullOrEmpty(slozka)) return;
        var input = LaunchInput.Parse(Environment.GetEnvironmentVariable("TD_SPUSTENI_VSTUP") ?? VychoziVstup);

        var settings = AppSettings.Load();
        if (settings.Cache is { } cache)
        {
            if (Environment.GetEnvironmentVariable("TD_IDFA") is { Length: > 0 } idfa) cache.Idfa = idfa;
            if (Environment.GetEnvironmentVariable("TD_NAMESPACE") is { Length: > 0 } ns) cache.Namespace = ns;
        }
        var mlRest = CacheSchemaWiring.TryCreateMlRest(settings) ?? throw new Exception("Caché není nakonfigurovaná");
        var popup = Substitute.For<IPopupDisplayer>();
        EDServices.Initialize(new EDServices(new MLRestAdapter(mlRest), Substitute.For<IWindowNavigator>(), popup));
        var agendas = await new CacheAgendaRegistry(new MLRestAdapter(mlRest)).ListAsync();
        // Načtení schématu pokračuje přes kontext synchronizace (v aplikaci dispatcher okna).
        SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

        var sablona = EmbeddedTemplates.ForAgenda(EmbeddedTemplates.LaunchFolder, input.AgendaCode!)!;
        var vm = new TemplateDesignerViewModel(mlRest, null, agendas, input.AgendaCode);
        var (cesta, duvod) = await vm.ExportLaunchPdfAsync(input, sablona, slozka);

        Assert.True(cesta is not null, duvod);
        output.WriteLine(cesta);
        using var pdf = PdfDocument.Open(cesta);
        var text = string.Join("\n", pdf.GetPages().Select(p => p.Text));
        output.WriteLine($"stran: {pdf.NumberOfPages}");
        output.WriteLine(text.Length > 1500 ? text[..1500] : text);
        Assert.True(pdf.NumberOfPages > 0);
        Assert.DoesNotContain("Neporovnáno", text);
    }
}
