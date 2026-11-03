using EDControls.EDViewModel;
using TemplateDesigner.Services;

namespace TemplateDesigner;

/// <summary>Pruh nad plátnem o šablonách, které firmě poslaly jiné firmy (`B189`, R9).</summary>
public partial class TemplateDesignerViewModel
{
    private string? _receivedNotice;
    /// <summary>Věta pruhu; null = pruh není vidět.</summary>
    public string? ReceivedNotice
    {
        get => _receivedNotice;
        private set { if (Set(ref _receivedNotice, value)) OnPropertyChanged(nameof(HasReceivedNotice)); }
    }

    public bool HasReceivedNotice => ReceivedNotice is not null;

    /// <summary>Zavřený pruh se ukáže znovu, až přibude další zásilka.</summary>
    private int _dismissedShipments;

    private RelayCommand? _dismissReceivedNotice;
    public RelayCommand DismissReceivedNoticeCommand => _dismissReceivedNotice ??= Command(() =>
    {
        _dismissedShipments = _receivedShipments;
        ReceivedNotice = null;
    });

    private AsyncRelayCommand? _showReceivedTemplates;
    /// <summary>Okno šablon rovnou v Přijatých šablonách.</summary>
    public AsyncRelayCommand ShowReceivedTemplatesCommand => _showReceivedTemplates ??=
        CommandAsync(() => OpenTemplateWindowAsync(preselectAgendaCode: null, startNodeKey: TemplateCatalogNodeVM.ReceivedKey));

    private int _receivedShipments;

    /// <summary>
    /// Zeptá se serveru na nevyřízené zásilky šablon. Nikdy neblokuje ani nespadne: bez sdílení, bez sítě nebo při výpadku
    /// ISYCloudu se pruh prostě neukáže. Server bez sdílení a firma, která ho nikdy nepoužila, ISYCloud vůbec nevolají.
    /// </summary>
    public async Task CheckReceivedTemplatesAsync()
    {
        if (TemplateStorage is not { } storage) return;
        ReceivedSummary summary;
        // Na klonu spojení: kontrola běží vedle toho, co uživatel zrovna dělá.
        try { summary = await (await Task.Run(storage.Clone)).GetReceivedSummaryAsync(); }
        catch (Exception) { summary = ReceivedSummary.None; }
        _receivedShipments = summary.Shipments;
        _dismissedShipments = Math.Min(_dismissedShipments, summary.Shipments);
        ReceivedNotice = summary.Shipments > _dismissedShipments ? summary.Notice : null;
    }
}
