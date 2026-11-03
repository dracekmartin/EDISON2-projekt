using EDControls.EDViewModel;
using TemplateDesigner.Models;
using TemplateDesigner.Services;

namespace TemplateDesigner;

/// <summary>Úvodní obrazovka po spuštění bez linky (<see cref="StartPanelViewModel"/>).</summary>
public partial class TemplateDesignerViewModel
{
    /// <summary>Úvodní obrazovka přes prázdné plátno po spuštění.</summary>
    public StartPanelViewModel StartPanel { get; }

    private AsyncRelayCommand? _showStartPanel;
    /// <summary>Nástroje → Úvodní obrazovka: kdykoli, i nad rozdělanou šablonou.</summary>
    public AsyncRelayCommand ShowStartPanelCommand => _showStartPanel ??= CommandAsync(() => ShowStartPanelAsync(zMenu: true));

    /// <summary>
    /// Ukáže úvodní obrazovku přes plátno. Po startu ji volá okno, jen když spuštění neotevírá
    /// konkrétní linku. Bez spojení na server ukáže jen hotovou šablonu M-line a prázdnou šablonu.
    /// </summary>
    /// <param name="zMenu">Uživatel si ji otevřel sám - ukáže se i nad neuloženou šablonou.</param>
    public async Task ShowStartPanelAsync(bool zMenu = false)
    {
        var sablona = State.CurrentTemplate;
        string? systemova = sablona.AgendaCode is { Length: > 0 } agenda
            ? (await Task.Run(() => EmbeddedSystemTemplate(agenda)))?.Name
            : null;

        IReadOnlyList<StartPanelItem> posledni = [], firemni = [];
        if (TemplateStorage is not null)
        {
            try
            {
                var vsechny = await TemplateStorage.ListAsync();
                await WaitForPreferencesAsync();
                (posledni, firemni) = StartPanelViewModel.BuildLists(vsechny, Preferences?.RecentTemplates ?? [],
                    sablona.AgendaCode, kod => AgendaEntry(kod)?.DisplayName ?? kod, DateTime.Now);
            }
            // Seznam je jen nabídka - bez něj obrazovka pořád nabídne okno Otevřít a hotovou šablonu.
            catch (CacheTemplateException) { }
        }

        // Mezitím mohl uživatel šablonu vyměnit nebo začít kreslit - pak už obrazovka nepatří.
        if (!ReferenceEquals(sablona, State.CurrentTemplate) || (IsDirty && !zMenu)) return;
        StartPanel.Show(CurrentAgendaDisplayName, TemplateStorage is not null, systemova, posledni, firemni);
    }

    /// <summary>Předvolby se načítají souběžně se startem okna; poslední šablony bez nich nejsou.
    /// Čeká nejvýš 3 s, pak obrazovka ukáže, co má.</summary>
    private Task WaitForPreferencesAsync()
    {
        if (Preferences is null || Preferences.IsLoaded) return Task.CompletedTask;
        var hotovo = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Preferences.Loaded += () => hotovo.TrySetResult();
        return Task.WhenAny(hotovo.Task, Task.Delay(TimeSpan.FromSeconds(3)));
    }

    /// <summary>Zapamatuje šablonu z databáze pro „Poslední otevřené šablony" na úvodní obrazovce.</summary>
    private void RememberTemplate(Guid templateId) => _ = Preferences?.AddRecentTemplateAsync(templateId);

    /// <summary>Prázdná šablona z úvodní obrazovky. Po startu na plátně už je a obrazovka se jen
    /// schová; nad rozdělanou nebo otevřenou šablonou založí novou jako Ctrl+N.</summary>
    internal async Task StartBlankFromStartPanelAsync()
    {
        bool cista = !IsDirty && CurrentFilePath is null && !_loadedFromCache
                     && State.CurrentTemplate.Sections.All(s => s.Elements.Count == 0);
        if (cista) StartPanel.Hide();
        else await NewTemplateAsync();
    }

    /// <summary>Otevře šablonu firmy z úvodní obrazovky.</summary>
    internal async Task OpenRecentTemplateAsync(Guid templateId)
    {
        if (TemplateStorage is null) return;
        if (!await ConfirmUnsavedChangesAsync()) return;
        try
        {
            var (_, template) = await TemplateStorage.LoadAsync(templateId);
            await ShowTemplateFromDatabaseAsync(template, isSystem: false, loadInstanceDefaults: true);
        }
        catch (CacheTemplateException ex) { PopupDisplayer.Error(ex); }
    }

    /// <summary>Změna agendy z úvodní obrazovky založí prázdnou šablonu nové agendy (a tím obrazovku
    /// zavře) - obrazovka se pak ukáže znovu, už se šablonami té nové agendy.</summary>
    internal async Task ChangeAgendaFromStartPanelAsync()
    {
        var puvodni = State.CurrentTemplate;
        await ChangeAgendaAsync();
        if (!ReferenceEquals(puvodni, State.CurrentTemplate)) await ShowStartPanelAsync();
    }

    /// <summary>Otevře hotovou šablonu M-line agendy jako kopii - Ctrl+S ji uloží jako šablonu firmy.</summary>
    internal async Task StartFromSystemTemplateAsync()
    {
        if (State.CurrentTemplate.AgendaCode is not { Length: > 0 } agenda) return;
        if (!await ConfirmUnsavedChangesAsync()) return;
        try
        {
            var template = await LoadSystemTemplateAsync(agenda);
            if (template is null)
            {
                StatusText = "Agenda nemá hotovou šablonu od M-line.";
                return;
            }
            await ShowTemplateFromDatabaseAsync(template, isSystem: true, loadInstanceDefaults: true);
        }
        catch (CacheTemplateException ex) { PopupDisplayer.Error(ex); }
    }

    /// <summary>Hotová šablona M-line pro agendu, null když agenda žádnou nemá.</summary>
    private async Task<Template?> LoadSystemTemplateAsync(string agenda)
    {
        var embedded = await Task.Run(() => EmbeddedSystemTemplate(agenda));
        if (embedded is null || TemplateStorage is null) return embedded;
        // V databázi může být novější verze z distribučního kanálu; když tam ještě není (seed
        // běží souběžně se startem), stačí ta z .exe.
        try { return (await TemplateStorage.LoadAsync(embedded.Id, CacheTemplateService.MlineIdFa)).Template; }
        catch (CacheTemplateException) { return embedded; }
    }

    /// <summary>Výchozí šablona M-line agendy ze .exe podle registru; agenda bez ní (nebo starší server) první v pořadí souborů.</summary>
    private Template? EmbeddedSystemTemplate(string agenda) =>
        EmbeddedTemplates.ForAgenda(EmbeddedTemplates.SystemFolder, agenda, AgendaEntry(agenda)?.VychoziSablonaMline);
}
