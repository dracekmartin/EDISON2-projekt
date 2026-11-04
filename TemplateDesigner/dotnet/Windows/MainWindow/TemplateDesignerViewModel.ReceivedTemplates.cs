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

    /// <summary>Poslední souhrn přijatých zásilek; jeho čas si uživatel uloží jako viděný.</summary>
    private ReceivedSummary _receivedSummary = ReceivedSummary.None;

    /// <summary>Zavřeno v tomto běhu - jen pro starší server, který čas zásilky neposílá.</summary>
    private bool _receivedNoticeClosed;

    private RelayCommand? _dismissReceivedNotice;
    /// <summary>Křížek: pruh se uživateli ukáže znovu, až přijde novější zásilka - i po restartu (Jan 4. 11.).</summary>
    public RelayCommand DismissReceivedNoticeCommand => _dismissReceivedNotice ??= Command(MarkReceivedSeen);

    private AsyncRelayCommand? _showReceivedTemplates;
    /// <summary>Okno šablon rovnou v Přijatých šablonách. Zásilky tím uživatel viděl, pruh zmizí (Jan 4. 11.).</summary>
    public AsyncRelayCommand ShowReceivedTemplatesCommand => _showReceivedTemplates ??= CommandAsync(() =>
    {
        MarkReceivedSeen();
        return OpenTemplateWindowAsync(preselectAgendaCode: null, startNodeKey: TemplateCatalogNodeVM.ReceivedKey);
    });

    private void MarkReceivedSeen()
    {
        _receivedNoticeClosed = true;
        ReceivedNotice = null;
        if (_receivedSummary.Latest != "" && CurrentFirmIdfa is { Length: > 0 } firma)
            _ = Preferences?.SetReceivedSeenAsync(firma, _receivedSummary.Latest);
    }

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
        // Bez načteného nastavení by se zavřený pruh na chvíli ukázal.
        await WaitForPreferencesAsync();

        _receivedSummary = summary;
        string? seen = null;
        if (CurrentFirmIdfa is { } firma) Preferences?.ReceivedSeen.TryGetValue(firma, out seen);
        bool show = summary.Latest != "" ? summary.IsNewerThan(seen) : summary.Shipments > 0 && !_receivedNoticeClosed;
        ReceivedNotice = show ? summary.Notice : null;
    }
}
