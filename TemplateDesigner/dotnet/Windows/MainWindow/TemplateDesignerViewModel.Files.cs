using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using EDControls;
using EDControls.EDViewModel;
using TemplateDesigner.Commands;
using TemplateDesigner.Commands.Implementations;
using TemplateDesigner.Config;
using TemplateDesigner.Designer;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Expressions;
using TemplateDesigner.Models.Layout;
using TemplateDesigner.Models.Schema;
using TemplateDesigner.Serialization;
using TemplateDesigner.Services;
using ALWA.SYS;

namespace TemplateDesigner;

/// <summary>Soubor šablony a úložiště šablon v databázi: nová, otevřít, uložit, import a export.</summary>
public partial class TemplateDesignerViewModel
{
    // ── File operations ───────────────────────────────────────────────────────
    private async Task NewTemplateAsync()
    {
        if (!await ConfirmUnsavedChangesAsync()) return;
        // Nová šablona zdědí rozpracovanou agendu (rozhodnutí Jan 5. 8.): kdo kreslí vývěsky pro
        // jednu agendu, nemusí ji po Ctrl+N vybírat znovu. Bez agendy nemá šablona nabídku datových
        // polí ani náhled a nováčkovi není jasné, co dělá špatně. Přepnutí zůstává na Ctrl+Shift+A.
        ReplaceWithBlankTemplate(agendaCode: State.CurrentTemplate.AgendaCode);
    }

    /// <summary>
    /// Sdílený helper pro &quot;clean slate&quot;: nahradí aktivní šablonu prázdnou
    /// (volitelně se zadanou agendou) a resetuje veškerý designový stav.
    /// Volající MUSÍ předtím zavolat <see cref="ConfirmUnsavedChanges"/>.
    /// </summary>
    private void ReplaceWithBlankTemplate(string? agendaCode, Action<Template>? configure = null)
    {
        string? puvodniAgenda = State.CurrentTemplate.AgendaCode;
        var t = new Template();
        if (agendaCode is not null) t.AgendaCode = agendaCode;
        bool zmenaAgendy = !string.Equals(puvodniAgenda, t.AgendaCode, StringComparison.OrdinalIgnoreCase);
        // Volitelná konfigurace před přidáním výchozí sekce (např. převzetí velikosti/okrajů
        // stránky při resetu z editoru vlastností stránky) - sekce se přidá až s finální PageSize.
        configure?.Invoke(t);
        State.CurrentTemplate = t;
        // Výchozí sekce, ať uživatel nezačíná na prázdné šabloně bez plochy k editaci.
        // Dvě, ne jedna - důvod viz `CreateDefaultSections`.
        State.CurrentTemplate.Sections.AddRange(CreateDefaultSections(State.CurrentTemplate.PageSize));
        CurrentFilePath = null;
        _loadedFromCache = false;
        IsDirty = false;
        // Aktivní instance jsou agenda-specifické - při změně agendy ztrácí smysl. Uvnitř téže
        // agendy ale zůstávají: po Ctrl+N nemá uživatel důvod vybírat tutéž linku znovu.
        if (zmenaAgendy) ClearActiveInstances();
        State.SelectedElements.Clear();
        State.UndoStack.Clear();
        State.RedoStack.Clear();
        NotifyUndoRedo();
        State.ActiveSection = State.CurrentTemplate.Sections.FirstOrDefault();
        RebuildSections();
        RefreshFieldsList();
        RequestTitleUpdate?.Invoke();
        RequestCanvasSizeUpdate?.Invoke();
        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
        RequestPreviewRefresh?.Invoke();
    }

    /// <summary>
    /// Jak se má naložit s obsahem při změně formátu stránky.
    /// </summary>
    public enum PageFormatContent
    {
        /// <summary>Přepočítat prvky na novou velikost.</summary>
        Rescale,

        /// <summary>Změnit jen stránku, velikosti prvků nechat.</summary>
        Keep,

        /// <summary>Uživatel změnu zrušil.</summary>
        Cancel,
    }

    /// <summary>
    /// Zeptá se, co s obsahem při změně formátu stránky.
    /// </summary>
    /// <remarks>
    /// ⚠️ Sdílený dialog má tlačítka Ano/Ne/Storno napevno, takže význam musí nést text otázky -
    /// akční popisky by znamenaly sáhnout do EDControls a publikovat i druhou aplikaci.
    /// Prázdná šablona se neptá vůbec: není co přeškálovat.
    /// </remarks>
    public PageFormatContent AskPageFormatContent()
    {
        if (State.CurrentTemplate.Sections.All(sec => sec.Elements.Count == 0))
            return PageFormatContent.Rescale;

        const string otazka =
            "Změnili jste formát stránky. Má se obsah šablony přepočítat na novou velikost?";
        const string ano =
            "Ano - prvky se zvětší nebo zmenší podle nové stránky; jejich nastavení zůstane";
        const string ne =
            "Ne - změní se jen stránka, velikosti prvků zůstanou beze změny";
        string zprava = otazka + Environment.NewLine + Environment.NewLine
            + ano + Environment.NewLine + ne;

        return PopupDisplayer.ConfirmYesNoCancel(zprava,
            "Změna formátu stránky") switch
        {
            true => PageFormatContent.Rescale,
            false => PageFormatContent.Keep,
            _ => PageFormatContent.Cancel,
        };
    }

    /// <summary>
    /// Změna velikosti/orientace stránky se <b>zachováním obsahu</b>: prvky se přepočítají na novou
    /// velikost, jejich nastavení se nemění. Vrátí false, když uživatel změnu zrušil.
    /// </summary>
    /// <remarks>
    /// Dřív se šablona v tomhle místě vymazala a založila prázdná - viz <see cref="ChangePageFormatCommand"/>,
    /// proč to tak bylo a co se změnilo. Zůstává to jako **jeden vratný krok**, takže Ctrl+Z vrátí
    /// rozměr stránky i obsah najednou.
    /// </remarks>
    public async Task<bool> ChangePageFormatAsync(IReadOnlyDictionary<string, string> pageFields)
    {
        var volba = AskPageFormatContent();
        if (volba == PageFormatContent.Cancel) return false;

        var t = State.CurrentTemplate;
        var ps = t.PageSize;
        var plan = volba == PageFormatContent.Rescale
            ? PageFormatChange.Plan(t,
                MmFromFields(pageFields, "PageSize.Width", ps.Width),
                MmFromFields(pageFields, "PageSize.Height", ps.Height),
                MmFromFields(pageFields, "PageSize.MarginLeft", ps.MarginLeft),
                MmFromFields(pageFields, "PageSize.MarginRight", ps.MarginRight),
                MmFromFields(pageFields, "PageSize.MarginTop", ps.MarginTop),
                MmFromFields(pageFields, "PageSize.MarginBottom", ps.MarginBottom))
            : PageFormatChange.KeepSizes(t);

        Execute(new ChangePageFormatCommand(t, pageFields, plan));
        await Task.CompletedTask;
        return true;
    }

    private static float MmFromFields(IReadOnlyDictionary<string, string> fields, string key, float fallback) =>
        fields.TryGetValue(key, out var s)
        && float.TryParse(s, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var v)
            ? v : fallback;

    private async Task OpenInstancePickerAsync()
    {
        if (CacheProvider is null)
        {
            PopupDisplayer.Info("Databáze není nakonfigurována - výběr instancí není dostupný.");
            return;
        }
        var agendaCode = State.CurrentTemplate.AgendaCode;
        if (string.IsNullOrEmpty(agendaCode))
        {
            PopupDisplayer.Info("Nejprve vyberte agendu (Ctrl+Shift+A).");
            return;
        }

        // Skupiny linek pro filtr v pickeru (prázdné = agenda bez skupin → filtr skrytý).
        // Async (R1): cachované, ale první volání jde po síti - na pomalé lince by zmrazilo aplikaci.
        var provider = CacheProvider;
        var skupiny = await Task.Run(() => provider.GetAvailableSkupiny(agendaCode));
        var vm = new InstancePickerDialogViewModel(agendaCode, _activeInstances, skupiny, _activeSkupinaId, CurrentInstanceLabelPlural);
        if (Dialogs?.ShowInstancePicker(vm) != true) return;

        _activeSkupinaId = vm.SelectedSkupinaId;
        _activeSkupinaLabel = vm.SelectedSkupinaLabel;
        _activeInstances = vm.SelectedInstances.ToList();
        _activeInstanceIndex = 0;
        _launchParams = null;     // srovnávací linka ze spuštění patřila předchozí lince
        // Před načtením dat: hodnota vázaná na předchozí linku by šla do GetData nové.
        bool vynulovano = VynulujParametryInstance();
        // Výběr z pickeru = táž šablona, jiné linky → nechat stará data do dosednutí (bez mock bliku).
        OnActiveInstancesChanged(reloadData: true, clearRealData: false);
        if (vynulovano) RebuildParametersPanel();
        OnPropertyChanged(nameof(ActiveSkupinaText));
        OnPropertyChanged(nameof(HasActiveSkupina));

        // Persist výběr pro tuto šablonu - při příštím otevření se předvyplní.
        if (Preferences is not null)
        {
            var ids = _activeInstances.Select(i => i.Value).ToList();
            _ = Preferences.SaveInstanceDefaultsAsync(State.CurrentTemplate.Id, ids);
        }
    }

    private async Task ChangeAgendaAsync()
    {
        // Registr agend (dvouosý: agenda × varianta) - dialog staví seskupený výběr jen ze
        // ZVEŘEJNĚNÝCH agend (Released; pilotní jako Linkový JŘ / Faktury se uživatelům nenabízí).
        // Aktuální agenda šablony se ukáže vždy - i nezveřejněná (interní/vývojová šablona).
        var current = State.CurrentTemplate.AgendaCode;
        // Vývojář (lokální `dev.hiddenAgendas`) vidí i nezveřejněné agendy - jinak by šablonu
        // v rozpracované agendě nešlo založit jinak než spuštěním ze shellu.
        bool skryte = AppSettings.Current?.Dev.HiddenAgendas == true;
        var visible = _agendaEntries
            .Where(a => a.Released || skryte || a.Code.Equals(current, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (visible.Count == 0)
        {
            PopupDisplayer.Info("Žádné agendy nejsou nakonfigurované.");
            return;
        }

        var vm = new ChangeAgendaDialogViewModel(visible, current);
        if (Dialogs?.ShowChangeAgenda(vm) != true || vm.ChosenCode is null) return;

        // Stejná agenda - nic neměním (vyhne se zbytečnému potvrzování neuložených změn).
        if (string.Equals(State.CurrentTemplate.AgendaCode, vm.ChosenCode, StringComparison.OrdinalIgnoreCase))
            return;

        if (!await ConfirmUnsavedChangesAsync()) return;
        ReplaceWithBlankTemplate(vm.ChosenCode);
    }

    /// <summary>Import šablony z JSON souboru. Načtená šablona se chová jako nová neuložená (původ
    /// = není v DB), takže následný Uložit ji založí do databáze.</summary>
    private async Task ImportFromJsonAsync()
    {
        if (!await ConfirmUnsavedChangesAsync()) return;
        var path = Dialogs?.PickOpenFile("Importovat šablonu z JSON", TemplateSerializer.FileFilter);
        if (path is null) return;
        await OpenTemplateFromFileAsync(path);
    }

    /// <summary>
    /// Otevře šablonu ze zadané cesty. Společná implementace pro File→Open a Recent Files menu.
    /// Šablona ze souboru nahradí otevřenou i s agendou, jen když projde <see cref="TemplateImportCheck"/>.
    /// </summary>
    public async Task OpenTemplateFromFileAsync(string filePath)
    {
        try
        {
            var oldAgenda = State.CurrentTemplate.AgendaCode;
            var nactena = await TemplateSerializer.LoadAsync(filePath);
            if (TemplateImportCheck.ProcNelzePrijmout(nactena, _agendaEntries, oldAgenda,
                    AppSettings.Current?.Dev.HiddenAgendas == true) is { } duvod)
            {
                PopupDisplayer.Info(duvod);
                return;
            }
            State.CurrentTemplate = nactena;
            // Pokud nová šablona má jinou agendu, vyčistit aktivní instance - jejich
            // IDs patří staré agendě a v nové by GetData padl.
            if (!string.Equals(oldAgenda, State.CurrentTemplate.AgendaCode, StringComparison.OrdinalIgnoreCase))
                ClearActiveInstances();
            CurrentFilePath = filePath;
            _loadedFromCache = false;
            IsDirty = false;
            State.SelectedElements.Clear();
            State.UndoStack.Clear();
            State.RedoStack.Clear();
            NotifyUndoRedo();
            ApplyEditorPreferencesFromTemplate();
            State.ActiveSection = State.CurrentTemplate.Sections.FirstOrDefault();
            RebuildSections();
            await RefreshFieldsList();
            await TryLoadInstanceDefaultsAsync();
            await ApplyPersistedPrintParamsAsync();
            if (Preferences is not null) await Preferences.AddRecentFileAsync(filePath);
            NotifyMigrationIfNeeded();
            RequestTitleUpdate?.Invoke();
            RequestCanvasSizeUpdate?.Invoke();
            RequestPropertiesPanelUpdate?.Invoke();
        }
        catch (TemplateDeserializationException ex) when (ex.NeniSablona)
        {
            // Vybraný soubor vůbec není šablona (typicky PDF předlohy) - chyba výběru, ne aplikace.
            // Rozdělaná šablona zůstává v okně: přepisuje se až po úspěšném načtení.
            PopupDisplayer.Info(ex.Message);
        }
        catch (TemplateDeserializationException ex)
        {
            PopupDisplayer.Error(ex);
        }
        catch (FileNotFoundException)
        {
            PopupDisplayer.Info($"Soubor nenalezen: {filePath}");
            if (Preferences is not null) await Preferences.RemoveRecentFileAsync(filePath);
        }
    }

    /// <summary>
    /// Načte uložený výběr instancí pro aktuální šablonu (pokud existují a CacheProvider je k dispozici).
    /// </summary>
    private async Task TryLoadInstanceDefaultsAsync()
    {
        if (Preferences is null || CacheProvider is null) return;
        var agendaCode = State.CurrentTemplate.AgendaCode;
        if (string.IsNullOrEmpty(agendaCode)) return;

        // ⚠️ Co má uživatel právě vybrané, přebíjí uložené linky šablony. Bez toho mu přepnutí
        // šablony v téže agendě shodilo výběr, který si před chvílí udělal, a musel ho klikat znovu
        // (nahlásil Jan 14. 9.). Uložené linky se tak uplatní tam, kde k něčemu jsou - když
        // uživatel žádnou vybranou nemá.
        if (_activeInstances.Count > 0) return;

        var savedIds = await Preferences.LoadInstanceDefaultsAsync(State.CurrentTemplate.Id);
        if (savedIds.Count == 0) return;

        try
        {
            var available = await Task.Run(() => CacheProvider.GetAvailableInstances(agendaCode!, null));
            var nalezene = available.Where(i => savedIds.Contains(i.Value)).ToList();
            // Dohledává se jen v prvních 50 linkách výchozí skupiny (`CacheAgendaProvider`), takže
            // uložená linka se najít nemusí. To není důvod výběr mazat - radši ho nechat být.
            if (nalezene.Count == 0) return;

            _activeInstances = nalezene;
            _activeInstanceIndex = 0;
            OnActiveInstancesChanged(reloadData: true);
        }
        catch
        {
            // Saved instances nejsou kompatibilní s aktuálním stavem DB - ignorujeme tiše
        }
    }

    /// <summary>
    /// Šablona převedená při otevření na nový formát (Template.WasMigrated) - jen ladicí výpis.
    /// </summary>
    /// <remarks>
    /// ⚠️ **Migrace NEZNAMENÁ neuloženou změnu** (rozhodnutí Jan 14. 9.). Do té doby se tu
    /// nastavovalo `IsDirty = true`, aby se nový formát propsal - jenže migruje se skoro každá
    /// šablona (formát je `1.12.0`, běžně uložené jsou starší), takže se uživatele po pouhém
    /// otevření ptalo na uložení změn, které neudělal. Na takovou otázku nejde odpovědět správně
    /// a naučí to lidi proklikávat dialogy. Migrace je idempotentní a pouští se při každém načtení,
    /// takže se nic neztratí: nový formát se zapíše, až šablonu uživatel opravdu upraví a uloží.
    /// <para>Ze stejného důvodu se neukazuje ani ve stavovém řádku (rozhodnutí Jan 24. 9.) -
    /// uživatel s ní nemá co dělat a tisk se nemění.</para>
    /// </remarks>
    private void NotifyMigrationIfNeeded()
    {
        if (State.CurrentTemplate.WasMigrated)
            System.Diagnostics.Debug.WriteLine($"[Šablona] převedena na formát {State.CurrentTemplate.Version} při otevření");
    }

    /// <summary>Aplikuje grid/snap nastavení uložené v šabloně. Null hodnoty → ponechat aktuální.</summary>
    private void ApplyEditorPreferencesFromTemplate()
    {
        var prefs = State.CurrentTemplate.EditorPreferences;
        if (prefs is null) return;
        // VM proxy settery firnou PropertyChanged → bound toggle buttony (ShowGrid, SnapToGrid,
        // SnapToElements) se aktualizují v toolbaru.
        if (prefs.GridSize       is { } gs) GridSize       = gs;
        if (prefs.SnapToGrid     is { } sg) SnapToGrid     = sg;
        if (prefs.SnapToElements is { } se) SnapToElements = se;
        if (prefs.ShowGrid       is { } sh) ShowGrid       = sh;
        // CmbGridSize není bindovaný (event-driven dropdown) - explicit sync přes callback.
        RequestGridSizeUiSync?.Invoke(State.GridSize);
    }

    /// <summary>Zaznamená aktuální grid/snap nastavení do šablony před uložením.</summary>
    private void CaptureEditorPreferencesToTemplate()
    {
        State.CurrentTemplate.EditorPreferences = new TemplateEditorPreferences
        {
            GridSize       = State.GridSize,
            SnapToGrid     = State.SnapToGrid,
            SnapToElements = State.SnapToElements,
            ShowGrid       = State.ShowGrid
        };
    }

    /// <summary>Loads a template that was generated externally (e.g. AI assistant).</summary>
    public async Task LoadGeneratedTemplateAsync(Template template)
    {
        if (!LoadGeneratedTemplate(template)) return;
        // Hodnoty nastavení sestavy, které vznikly při importu z předlohy, se musí projevit
        // i v ovladačích - jinak se tiskne něco jiného, než co je v nastavení vidět (viz
        // ApplyPersistedPrintParamsAsync). Import do 19. 8. touhle cestou vůbec neprošel.
        await RefreshFieldsList();
        await ApplyPersistedPrintParamsAsync();
    }

    /// <returns>False = šablonu nejde přijmout (stejná kontrola jako import ze souboru - šablonu
    /// skládá model, takže může míchat agendy), uživatel dostal důvod a otevřená šablona zůstala.</returns>
    public bool LoadGeneratedTemplate(Template template)
    {
        var puvodniAgenda = State.CurrentTemplate.AgendaCode;
        if (TemplateImportCheck.ProcNelzePrijmout(template, _agendaEntries, puvodniAgenda,
                AppSettings.Current?.Dev.HiddenAgendas == true) is { } duvod)
        {
            PopupDisplayer.Info(duvod);
            return false;
        }
        State.CurrentTemplate = template;
        // Data a vybrané linky patří agendě předchozí šablony - jako u otevření z databáze nebo
        // souboru. Jinak se nová šablona do přenačtení kreslí nad daty jiného tvaru.
        if (!string.Equals(puvodniAgenda, template.AgendaCode, StringComparison.OrdinalIgnoreCase))
            ClearActiveInstances();
        State.SelectedElements.Clear();
        State.UndoStack.Clear();
        State.RedoStack.Clear();
        NotifyUndoRedo();
        State.ActiveSection = State.CurrentTemplate.Sections.FirstOrDefault();
        CurrentFilePath = null;
        IsDirty = true;

        RebuildSections();
        InvalidateAll();
        RefreshFieldsList();
        return true;
    }

    private void InvalidateAll()
    {
        RequestTitleUpdate?.Invoke();
        RequestCanvasSizeUpdate?.Invoke();
        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
        RequestPreviewRefresh?.Invoke();
    }

    private async Task SaveTemplate()
    {
        // DB je primární úložiště. DB šablona → update; nová/importovaná → SaveAs do DB
        // (SaveToCacheAsync interně řeší new→SaveAs). Bez Caché → popup s nabídkou JSON exportu (U5).
        if (TemplateStorage is null) { await OfferJsonExportFallbackAsync(); return; }
        await SaveToCacheAsync();
    }

    /// <summary>Fallback když DB není dostupná (U5): explicitní popup s nabídkou uložit jako JSON,
    /// místo tichého status baru. Vrátí true pokud se reálně exportovalo.</summary>
    private async Task<bool> OfferJsonExportFallbackAsync()
    {
        if (!PopupDisplayer.Confirm(
                "Databáze není dostupná - nelze uložit do ní. Uložit šablonu jako soubor JSON?"))
            return false;
        return await ExportToJsonAsync();
    }

    /// <summary>Export šablony do JSON souboru. NEmění původ (šablona dál patří do DB) - je to
    /// kopie ven, obdoba PDF/PNG exportu, ne „save target". Vrátí true pokud se soubor uložil.</summary>
    private async Task<bool> ExportToJsonAsync()
    {
        var path = Dialogs?.PickSaveFile("Exportovat šablonu do JSON", TemplateSerializer.FileFilter,
            State.CurrentTemplate.Name);
        if (path is null) return false;
        SyncParametersToTemplate();
        CaptureEditorPreferencesToTemplate();
        await TemplateSerializer.SaveAsync(State.CurrentTemplate, path);
        StatusText = $"Vyexportováno do {System.IO.Path.GetFileName(path)}";
        return true;
    }

    // ── Caché storage ─────────────────────────────────────────────────────────

    /// <summary>True pokud aktuální šablona byla naposledy načtená/uložená do Caché.</summary>
    private bool _loadedFromCache;

    /// <summary>True = šablona je uložená v databázi (Ctrl+S ji aktualizuje). False = nová/importovaná,
    /// ještě není v DB (Ctrl+S ji založí). Pro indikátor původu v titulku.</summary>
    public bool IsCacheBacked => _loadedFromCache;

    private async Task OpenFromCacheAsync()
    {
        if (TemplateStorage is null) return;

        // Confirmace neuložených změn AŽ po explicitním Otevřít - dialog funguje i jako
        // správce (smazat / přejmenovat) bez intent načíst do editoru.
        var vm = new CacheTemplatePickerDialogViewModel(TemplateStorage, _agendaEntries);
        if (Dialogs?.ShowCacheTemplatePicker(vm) != true || vm.SelectedTemplateId is null) return;

        if (!await ConfirmUnsavedChangesAsync()) return;

        try
        {
            // Systémová (M-line) šablona se čte z MLINE větve; jinak z firmy (default).
            var sourceIdFa = vm.SelectedIsSystem ? Services.CacheTemplateService.MlineIdFa : null;
            var (_, template) = await TemplateStorage.LoadAsync(vm.SelectedTemplateId.Value, sourceIdFa);
            await ShowTemplateFromDatabaseAsync(template, isSystem: vm.SelectedIsSystem, loadInstanceDefaults: true);
        }
        catch (CacheTemplateException ex) { PopupDisplayer.Error(ex); }
    }

    /// <summary>Otevře šablonu načtenou z databáze (výběr šablony i spuštění z modulu JŘ).</summary>
    private async Task ShowTemplateFromDatabaseAsync(Template template, bool isSystem, bool loadInstanceDefaults)
    {
        var oldAgenda = State.CurrentTemplate.AgendaCode;
        State.CurrentTemplate = template;
        // Pokud nová šablona má jinou agendu, vyčistit aktivní instance - jejich
        // IDs patří staré agendě a v nové by GetData padl.
        if (!string.Equals(oldAgenda, State.CurrentTemplate.AgendaCode, StringComparison.OrdinalIgnoreCase))
            ClearActiveInstances();
        CurrentFilePath = null;
        // Systémová = read-only originál → otevřít jako „kopie": Ctrl+S vynutí „Uložit jako"
        // do firmy (nový GUID). Vlastní firma šablona → normální update.
        _loadedFromCache = !isSystem;
        if (!isSystem) RememberTemplate(template.Id);
        IsDirty = false;
        State.SelectedElements.Clear();
        State.UndoStack.Clear();
        State.RedoStack.Clear();
        NotifyUndoRedo();
        ApplyEditorPreferencesFromTemplate();
        State.ActiveSection = State.CurrentTemplate.Sections.FirstOrDefault();
        RebuildSections();
        await RefreshFieldsList();
        if (loadInstanceDefaults) await TryLoadInstanceDefaultsAsync();
        await ApplyPersistedPrintParamsAsync();
        NotifyMigrationIfNeeded();
        RequestTitleUpdate?.Invoke();
        RequestCanvasSizeUpdate?.Invoke();
        RequestPropertiesPanelUpdate?.Invoke();
    }
}
