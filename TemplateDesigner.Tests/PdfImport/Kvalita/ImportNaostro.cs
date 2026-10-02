using EDControls;
using EDControls.EDViewModel;
using NSubstitute;
using TemplateDesigner.Config;
using TemplateDesigner.Models;
using TemplateDesigner.PdfImport;
using TemplateDesigner.Serialization;
using TemplateDesigner.Services;

namespace TemplateDesigner.Tests.PdfImport.Kvalita;

/// <summary>
/// Import z PDF naostro - dev server (<c>templatedesigner.appsettings.local.json</c>, VPN), skutečný
/// model, bez návrháře. Jen pro ruční zkoušky, které zapíná proměnná prostředí; každé volání je
/// placený dotaz na model.
/// </summary>
public static class ImportNaostro
{
    private static CacheAgendaProvider? _provider;
    private static string? _klic;

    private static async Task<(CacheAgendaProvider Provider, string Klic)> PripojAsync()
    {
        if (_provider is not null && _klic is not null) return (_provider, _klic);

        var settings = AppSettings.Load();
        var mlRest = CacheSchemaWiring.TryCreateMlRest(settings) ?? throw new Exception("Caché není nakonfigurovaná");
        EDServices.Initialize(new EDServices(new MLRestAdapter(mlRest),
            Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()));

        _klic = await EdisonAiService.GetApiKeyAsync();
        var agendas = await new CacheAgendaRegistry(new MLRestAdapter(mlRest)).ListAsync();
        _provider = CacheSchemaWiring.TryCreate(mlRest, agendas) ?? throw new Exception("Bez agend");
        return (_provider, _klic);
    }

    /// <summary>Šablona, jakou by dostal uživatel z dialogu (včetně jeho dokončovacích úprav).</summary>
    public static async Task<(Template Sablona, AiVisionImportPipeline Pipeline)> ImportujAsync(
        string pdf, string agenda, int strana, Action<string>? stav = null)
    {
        var (provider, klic) = await PripojAsync();
        var typ = ScheduleImportTypes.ForAgenda(agenda);
        var pipeline = new AiVisionImportPipeline(klic, provider, agenda, provider.GetParameters(agenda));
        var sablona = await pipeline.ImportAsync(pdf, [strana], typ, true, stav ?? (_ => { }));

        sablona.AgendaCode = agenda;
        PdfImportDialogViewModel.NormalizeImportedPage(sablona, lineSchedule: typ == ScheduleImportType.LinkovyJr);
        PdfImportDialogViewModel.ApplyDefaultImportBorders(sablona);
        return (sablona, pipeline);
    }
}
