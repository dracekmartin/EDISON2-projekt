using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using SkiaSharp;
using EDControls.EDInputForm;
using EDControls.EDTheme;
using TemplateDesigner.Commands;
using TemplateDesigner.Commands.Implementations;
using TemplateDesigner.Designer;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Config;
using TemplateDesigner.Serialization;
using ALWA.SYS;

namespace TemplateDesigner;

/// <summary>Formulář parametrů sestavy v levém panelu.</summary>
public partial class MainWindow
{
    // ── EDInputForm parametrů v levém tabu (AgendaParams, includeExport=0) ───────
    // Vzor stejný jako PrintDialog: CacheClass + InitParams → Init → SchemaLoaded → napojení polí.

    private bool _paramsTabInitHooked;

    /// <summary>Přenačte formulář parametrů v levém tabu z aktuální agendy + hodnot (sdílený dict).
    /// Volá se po změně agendy/šablony (VM callback) i po zavření tiskového dialogu (sync hodnot).</summary>
    private void ReloadParamsTabForm()
    {
        if (_vm is null) return;

        // Agenda bez parametrů (GENERAL / bez GetParameters) → vyčistit CacheClass, ať se při
        // příštím Loaded neprovede Init se stale InitParams předchozí agendy.
        if (!_vm.HasParameters || _vm.CurrentAgendaCacheClass is null)
        {
            ParamsTabForm.CacheClass = "";
            return;
        }

        ParamsTabForm.InitParams = _vm.BuildAgendaParamsInitParams();
        if (!_paramsTabInitHooked)
        {
            ParamsTabForm.SchemaLoaded += OnParamsTabInitialized;
            _paramsTabInitHooked = true;
        }
        const string paramsFormClass = "ALVA.TemplateDesigner.EDInputForm.AgendaParams";

        // Init() = synchronní Caché round-trip. Voláme ho jen když je control už Loaded (přepnutí
        // agendy za běhu). Při prvním zobrazení tabu (a v konstruktoru okna) ho spustí EDInputForm
        // Loaded auto-init s aktuálními InitParams - jinak by transientní chyba Caché propadla ven
        // z konstruktoru okna a aplikace by se neotevřela (R1/R2). Chyba jde do status baru, ne do pádu.
        if (!ParamsTabForm.IsLoaded)
        {
            // ⚠️ Auto-init běží jednou za CacheClass a ta je pro všechny agendy stejná: po změně
            // agendy mimo tab by se ukázaly parametry té předchozí. Prázdná třída ho vynutí znovu.
            ParamsTabForm.CacheClass = "";
            ParamsTabForm.CacheClass = paramsFormClass;
            return;
        }
        ParamsTabForm.CacheClass = paramsFormClass;
        try { ParamsTabForm.Init(); }
        catch (System.Exception ex) { _vm.StatusText = $"⚠ Parametry sestavy se nepodařilo načíst: {ex.Message}"; }
    }

    private void OnParamsTabInitialized()
    {
        foreach (var field in (ParamsTabForm.Items ?? []).OfType<Field>())
            field.PropertyChanged += OnParamsTabFieldChanged;
        ApplyParamsTabVisibility();
    }

    private void OnParamsTabFieldChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_vm is null || sender is not Field field || e.PropertyName != nameof(Field.Value)) return;
        _vm.SetAgendaParamValue(field.Key, field.Value ?? "");
        RefreshParamsTabInitParams();  // jinak se hodnota po přepnutí tabu vrátí na starou
        ApplyParamsTabVisibility();   // visibleWhen (např. Hodiny od/do jen ve „Vlastní rozsah")
    }

    /// <summary>Přepíše hodnotu jednoho pole v tabu parametrů (bez `Init()`, který je synchronní
    /// round-trip na Caché). Zápis vyvolá FieldChanged, ale ViewModel stejnou hodnotu zahodí.</summary>
    private void SyncParamsTabField(string key, string value)
    {
        RefreshParamsTabInitParams();
        var field = (ParamsTabForm.Items ?? []).OfType<Field>().FirstOrDefault(f => f.Key == key);
        if (field is not null && field.Value != value) field.Value = value;
    }

    /// <summary>
    /// Přenese aktuální hodnoty parametrů do <c>InitParams</c>. **Nutné po každé změně hodnoty**:
    /// formulář se sám inicializuje na `Loaded`, tedy i při návratu na tab, a bere je odtud - se
    /// starým snímkem by se zobrazená hodnota vrátila na tu, se kterou se tab stavěl naposledy,
    /// zatímco náhled i tisk už jedou podle nové.
    /// </summary>
    private void RefreshParamsTabInitParams()
    {
        if (_vm is null || string.IsNullOrEmpty(ParamsTabForm.CacheClass)) return;
        ParamsTabForm.InitParams = _vm.BuildAgendaParamsInitParams();
    }

    private void ApplyParamsTabVisibility()
    {
        if (_vm is null) return;
        foreach (var field in (ParamsTabForm.Items ?? []).OfType<Field>())
        {
            var target = _vm.IsAgendaParamVisible(field.Key) ? ItemVisibility.Visible : ItemVisibility.Collapsed;
            if (field.Visibility != target) field.Visibility = target;
        }
    }

    private bool _closeConfirmed;

    private async void Window_Closing(object sender, CancelEventArgs e)
    {
        if (_closeConfirmed || _vm is null) return;
        if (!_vm.IsDirty) return;   // čisté okno → nech zavřít normálně (žádný cancel, žádný async)

        // Dirty: uložení do DB je async → zrušit toto zavření, počkat na potvrzení, pak zavřít
        // programově. Close() PŘES Dispatcher - až po doběhnutí Closing eventu; jinak WPF hodí
        // „nelze volat Close při zavírání" i když confirm skončí synchronně (volba „Ne").
        e.Cancel = true;
        try
        {
            if (await _vm.ConfirmUnsavedChangesAsync())
            {
                _closeConfirmed = true;
                Dispatcher.BeginInvoke(new Action(Close));
            }
        }
        catch (System.Exception ex)
        {
            // R2: uložení do DB mohlo selhat (výpadek sítě) → hláška, okno zůstane otevřené.
            _vm.PopupDisplayer.Error(ex);
        }
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void MenuPageProps_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is not null) _ = OpenPropertiesForPage(_vm.State.CurrentTemplate);
    }

    private void MenuUndo_Click(object sender, RoutedEventArgs e)  => _vm!.UndoCommand.Execute(null);
    private void MenuRedo_Click(object sender, RoutedEventArgs e)  => _vm!.RedoCommand.Execute(null);
    private void MenuDelete_Click(object sender, RoutedEventArgs e) => _vm!.DeleteCommand.Execute(null);
    private void MenuHelp_Click(object sender, RoutedEventArgs e)  => ShowHelp();

    /// <summary>Naplní DEV submenu firem z pevného seznamu ve VM. Viditelné jen se zapnutým
    /// <c>dev.firmSwitch</c> v configu a jen když je připojení k databázi.</summary>
    private void BuildDevFirmMenu()
    {
        bool devMode = AppSettings.Current?.Dev.FirmSwitch == true;
        bool show = devMode && _vm?.CurrentFirmIdfa is not null;
        bool importPdf = AppSettings.Current?.Dev.PdfImport == true;
        MenuDevSwitchFirm.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        MenuImportPdf.Visibility = importPdf ? Visibility.Visible : Visibility.Collapsed;
        MenuDevSep.Visibility = show || importPdf ? Visibility.Visible : Visibility.Collapsed;
        if (!show) return;

        MenuDevSwitchFirm.Items.Clear();
        foreach (var (code, label) in TemplateDesignerViewModel.DevFirms)
        {
            var item = new MenuItem { Header = label, Tag = code, IsCheckable = true };
            item.Click += MenuDevSwitchFirm_Click;
            MenuDevSwitchFirm.Items.Add(item);
        }
        RefreshDevFirmMenuChecks();
    }

    private void MenuDevSwitchFirm_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is null || sender is not MenuItem item || item.Tag is not string code) return;
        _vm.DevSwitchFirm(code);
        RefreshDevFirmMenuChecks();
    }

    /// <summary>Označí zaškrtnutím aktuální firmu; ostatní odškrtne.</summary>
    private void RefreshDevFirmMenuChecks()
    {
        var current = _vm?.CurrentFirmIdfa;
        foreach (var mi in MenuDevSwitchFirm.Items.OfType<MenuItem>())
            mi.IsChecked = mi.Tag is string c && string.Equals(c, current, StringComparison.Ordinal);
    }

    private async void MenuAiAssistant_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        try
        {
            var (apiKey, aiReason) = await TryGetAiApiKeyAsync();
            var aiVm   = new AiTemplateAssistantDialogViewModel(apiKey, _vm.SchemaProvider, aiReason);
            var dialog = new AiTemplateAssistantDialog(aiVm) { Owner = this };

            if (dialog.ShowDialog() == true && aiVm.GeneratedTemplate is not null)
            {
                if (!await _vm.ConfirmUnsavedChangesAsync()) return;
                _vm.LoadGeneratedTemplate(aiVm.GeneratedTemplate);
            }
        }
        catch (System.Exception ex)
        {
            _vm.PopupDisplayer.Error(ex);   // R2: async void → chyba přes popup, ne pád
        }
    }

    private bool _aiKeyFetchRunning;

    /// <summary>
    /// Vyzvedne AI klíč (lokální / mlweb přes EdisonAiService). Klíč null = AI nedostupné,
    /// Reason nese uživatelský důvod (limit, chybí konfigurace…) - dialogy ho zobrazí,
    /// ale samy fungují dál (generátor, PDF import mají AI jako podmíněný krok).
    /// Cold-start = Caché config + HTTP resolve (sekundy, timeout 60 s) → wait-cursor + status,
    /// guard proti opakovanému kliku do menu během volání.
    /// </summary>
    private async Task<(string? Key, string? Reason)> TryGetAiApiKeyAsync()
    {
        if (_aiKeyFetchRunning) return (null, "Probíhá předchozí požadavek - chvíli strpení.");
        _aiKeyFetchRunning = true;
        Mouse.OverrideCursor = Cursors.Wait;
        if (_vm is not null) _vm.StatusText = "Ověřuji dostupnost AI služby…";
        try
        {
            return (await Services.EdisonAiService.GetApiKeyAsync(), null);
        }
        catch (Services.EdisonAiException ex)
        {
            return (null, ex.Message);
        }
        finally
        {
            Mouse.OverrideCursor = null;
            if (_vm is not null) _vm.StatusText = "";
            _aiKeyFetchRunning = false;
        }
    }

    private async void MenuImportPdf_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        try
        {
            var (apiKey, aiReason) = await TryGetAiApiKeyAsync();
            var vm = new PdfImportDialogViewModel(_vm.SchemaProvider, apiKey, _vm.State.CurrentTemplate?.AgendaCode, aiReason,
                _vm.ZnamyDruhAgendy);
            var dialog = new PdfImportDialog(vm) { Owner = this };

            if (dialog.ShowDialog() == true && vm.ImportedTemplate is not null)
            {
                if (!await _vm.ConfirmUnsavedChangesAsync()) return;
                await _vm.LoadGeneratedTemplateAsync(vm.ImportedTemplate);
            }
        }
        catch (System.Exception ex)
        {
            _vm.PopupDisplayer.Error(ex);   // R2: async void → chyba přes popup, ne pád
        }
    }

    /// <summary>Aktualizuje strom pokud je viditelný (po změně výběru na canvasu).</summary>
    private void RefreshTreeIfVisible()
    {
        if (TabTree?.IsSelected == true)
            TreePanel.Rebuild();
    }

    private void BtnZForward_Click(object sender, RoutedEventArgs e)  => _vm!.ChangeZIndex(+1, false);
    private void BtnZBackward_Click(object sender, RoutedEventArgs e) => _vm!.ChangeZIndex(-1, false);
    private void BtnZFront_Click(object sender, RoutedEventArgs e)    => _vm!.ChangeZIndex(+1, true);
    private void BtnZBack_Click(object sender, RoutedEventArgs e)     => _vm!.ChangeZIndex(-1, true);

    private void BtnAlignLeft_Click(object sender, RoutedEventArgs e)    => _vm!.AlignElements(AlignMode.Left);
    private void BtnAlignRight_Click(object sender, RoutedEventArgs e)   => _vm!.AlignElements(AlignMode.Right);
    private void BtnAlignCenterH_Click(object sender, RoutedEventArgs e) => _vm!.AlignElements(AlignMode.CenterH);
    private void BtnAlignTop_Click(object sender, RoutedEventArgs e)     => _vm!.AlignElements(AlignMode.Top);
    private void BtnAlignBottom_Click(object sender, RoutedEventArgs e)  => _vm!.AlignElements(AlignMode.Bottom);
    private void BtnAlignMiddleV_Click(object sender, RoutedEventArgs e) => _vm!.AlignElements(AlignMode.MiddleV);
    private void BtnDistributeH_Click(object sender, RoutedEventArgs e)  => _vm!.AlignElements(AlignMode.DistributeH);
    private void BtnDistributeV_Click(object sender, RoutedEventArgs e)  => _vm!.AlignElements(AlignMode.DistributeV);
    private void MenuNormalizePositions_Click(object sender, RoutedEventArgs e) => OpenNormalizePositionsDialog();
    private void MenuCopyStyle_Click(object sender, RoutedEventArgs e)          => _vm?.CopyStyle();
    private void MenuPasteStyle_Click(object sender, RoutedEventArgs e)         => _vm?.PasteStyle();
    private void MenuPrevzitVychoziFont_Click(object sender, RoutedEventArgs e) => _vm?.PrevzitVychoziFont();
}
