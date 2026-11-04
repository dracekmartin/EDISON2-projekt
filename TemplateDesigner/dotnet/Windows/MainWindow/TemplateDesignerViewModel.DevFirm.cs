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

/// <summary>Vývojářské přepínání datové firmy za běhu.</summary>
public partial class TemplateDesignerViewModel
{
    // ── DEV: přepínání datové firmy za běhu ──────────────────────────────────
    // Jen pro vývojáře (gate v UI = lokální dev klíč, stejně jako AI asistent). Umožní
    // testovat agendy proti více firmám bez editace appsettings a restartu. Pro tyto firmy
    // platí Idfa == datový namespace (ověřeno). Bez licenčního verify (na rozdíl od EDISON
    // shellu) - dev nástroj, ne produkční přepnutí firmy.

    /// <summary>Pevný dev seznam firem, se kterými se běžně pracuje. Idfa i datový namespace = kód.</summary>
    public static readonly IReadOnlyList<(string Code, string Label)> DevFirms =
    [
        ("ZL_KOVED",  "ZL_KOVED - KOVED / IDZK (AD)"),
        ("CB_CSADAD", "CB_CSADAD - ČSAD Autobusy (AD)"),
        ("CB_GWBUS",  "CB_GWBUS - GW BUS (AD)"),
        ("KV_DP",     "KV_DP - DP Karlovy Vary (MHD)"),
        // Příjemce šablon od CB_CSADAD při zkoušení sdílení proti testovacímu ISYCloudu (`B189`).
        ("CB_DP",     "CB_DP - DP České Budějovice (MHD)"),
        // Slovenská data: jediná firma, kde se pozná rozdíl mezi českou a slovenskou variantou
        // symbolového fontu (na znamení = dlaň místo křížku) a kde jsou vyplněné per-linkové
        // seznamy „nevypisovat značku ve sloupci". Podněty #12843 se testují tady.
        ("SKNI_MHDNITRA", "SKNI_MHDNITRA - MHD Nitra (AD, slovenská data)"),
    ];

    /// <summary>Aktuální datová firma (Idfa). Null bez připojení k databázi.</summary>
    public string? CurrentFirmIdfa => _cacheMlRest?.Idfa;

    /// <summary>DEV: přepne datovou firmu (Idfa + namespace) a znovu načte schéma i data.</summary>
    public void DevSwitchFirm(string code)
    {
        if (_cacheMlRest is null)
        {
            StatusText = "Přepnutí firmy není dostupné bez připojení k databázi.";
            return;
        }
        if (string.Equals(_cacheMlRest.Idfa, code, StringComparison.Ordinal))
        {
            StatusText = $"Firma {code} je už aktivní.";
            return;
        }
        _cacheMlRest.Idfa = code;
        _cacheMlRest.DataNamespace = code;
        _previewDataGeneration++;                 // guard proti dosednutí stale dat běžícího fetche
        CacheProvider?.InvalidateCaches();
        ClearActiveInstances();                   // instance jsou per-firma; spustí i reload dat
        RefreshFieldsList();                      // schéma + parametry pro novou firmu
        OnPropertyChanged(nameof(CurrentFirmIdfa));
        StatusText = $"[DEV] Přepnuto na firmu {code}.";
        _receivedNoticeClosed = false;            // zásilky šablon jsou per-firma
        _ = CheckReceivedTemplatesAsync();
    }

    /// <summary>Najde v nových stránkách tu odpovídající aktuálně zobrazené - dle zastávky×směru
    /// (přesná shoda vč. označníku má přednost; bez ní první stránka téže zastávky). Agendy bez
    /// identifikačních polí (faktury…) padají na původní pořadové číslo (clamp). Public kvůli testům.</summary>
    public static int FindMatchingPageIndex(List<DataContext> newPages, DataContext? current, int oldIndex)
    {
        if (current is not null)
        {
            string stop = current.GetString("CISLO_ZASTAVKA");
            string smer = current.GetString("SMER");
            if (stop.Length > 0)
            {
                string nast = current.GetString("NASTUPISTE");
                int bezOznacniku = -1;
                for (int i = 0; i < newPages.Count; i++)
                {
                    if (newPages[i].GetString("CISLO_ZASTAVKA") != stop || newPages[i].GetString("SMER") != smer) continue;
                    if (newPages[i].GetString("NASTUPISTE") == nast) return i;
                    if (bezOznacniku < 0) bezOznacniku = i;
                }
                if (bezOznacniku >= 0) return bezOznacniku;
            }
        }
        return Math.Clamp(oldIndex, 0, Math.Max(0, newPages.Count - 1));
    }

    /// <summary>Caché GetData pro aktuální instanci → _realPreviewData. Async, volá se fire-and-forget.</summary>
    /// <param name="keepPage">true = zachovat aktuálně zobrazenou stránku dat (re-fetch téže instance -
    /// změna server parametru, F5); false = skočit na první (přepnutí instance/agendy).</param>
    private async Task RefreshDataForCurrentInstanceAsync(bool keepPage = false)
    {
        var instance = CurrentActiveInstance;
        if (CacheProvider is null || instance is null) return;
        var agendaCode = State.CurrentTemplate.AgendaCode;
        if (string.IsNullOrEmpty(agendaCode)) return;

        // Bump generace: starší souběžný fetch (pomalejší odpověď se starou definicí/parametry)
        // se po dokončení zahodí, místo aby přepsal čerstvější data.
        int gen = ++_previewDataGeneration;
        try
        {
            // Průběh u štítku linky, ne ve stavovém řádku - tam se střídal s ostatními texty a blikal
            // (nález Jan 24. 9.). A jen u delšího načtení, krátké by bliklo i tak.
            _ = Task.Delay(500).ContinueWith(_ =>
            {
                if (gen == _previewDataGeneration && _nacitaniBezi) NacitaniDat = true;
            }, TaskScheduler.FromCurrentSynchronizationContext());
            _nacitaniBezi = true;
            var paramName = InstanceParamName(agendaCode);
            // Print params (vč. server-side, např. ROZDELENI_SLOUPCU) → GetData; instance přebíjí klíč linky.
            var dataInputs = CurrentDataInputs();
            var getDataParams = ComposeGetDataParams(_previewParams, paramName, instance.Value, _activeSkupinaId, dataInputs);
            List<DataContext>? pages;
            string? duvodPrazdneho;
            try { (pages, duvodPrazdneho) = await Task.Run(() => CacheProvider.GetDataSDuvodem(agendaCode, getDataParams)); }
            finally { if (gen == _previewDataGeneration) { _nacitaniBezi = false; NacitaniDat = false; } }

            // Mezitím se přepnula agenda/instance (nebo se vyčistilo) → tento výsledek je
            // zastaralý a nesmí dosednout do nové šablony.
            if (gen != _previewDataGeneration) return;
            _lastDataInputs = dataInputs;   // data odteď odpovídají těmhle údajům ze šablony

            if (pages is null || pages.Count == 0)
            {
                _realPreviewData = null;
                Models.ColorHex.UiData = null;
                // Agenda řekne, co v datech chybí a co s tím (`DUVOD_PRAZDNE`) - jinak by uživatel
                // hledal chybu v návrháři. Hláška, ne jen text dole (`B193g`).
                NastavHlaseni(ZdrojHlaseni.Data, [Models.KontrolaVystupu.LinkaBezDat(instance.Display, duvodPrazdneho, CurrentInstance)]);
                StatusText = $"Žádná data: {instance.Display}";
            }
            else
            {
                var culture = GetTemplateCulture();
                var newData = pages.Select(d => d.WithCulture(culture)).ToList();
                // Odjezdy mimo sloupce na vývěsce chybí (chyba); zmenšené písmo a malý kód jsou z dat
                // všech stránek, takže se počítají tady, ne při kreslení jedné z nich.
                NastavHlaseni(ZdrojHlaseni.Data,
                [
                    .. DepartureColumnWarnings.Seznam(newData),
                    .. ContentShrinkWarnings.Seznam(State.CurrentTemplate, newData, vcetneOrezuTextu: false),
                    .. BarcodeSizeWarnings.Seznam(State.CurrentTemplate, newData),
                ]);
                // Re-fetch téže instance drží LOGICKOU stránku (zastávka×směr), ne pořadové číslo -
                // např. „Rozdělit vývěsku dle označníků" posouvá čísla stránek o přidané označníky.
                var currentPage = (keepPage && _realPreviewData is not null
                                   && _realPreviewIndex >= 0 && _realPreviewIndex < _realPreviewData.Count)
                    ? _realPreviewData[_realPreviewIndex] : null;
                _realPreviewData = newData;
                Models.ColorHex.UiData = null;   // naplní ho až kreslení nové stránky
                _realPreviewIndex = keepPage ? FindMatchingPageIndex(newData, currentPage, _realPreviewIndex) : 0;
                // Linka je na štítku a stránky v přepínači nad náhledem - úspěch nic nehlásí,
                // jen smaže „Načítám data…" (rozhodnutí Jan 24. 9.).
                // (dřív tu byl text „linka: N stránek" - duplikoval štítek a přepínač stránek)
            }
            NotifyPreviewPagesChanged();
            // Vzory sloupců chodí až s daty - teprve teď jde poznat, jestli definice v šabloně
            // odpovídá některému z nich, a srovnat podle toho parametr rozvržení.
            SyncLayoutParamFromTemplate();
            RequestPreviewRefresh?.Invoke();
        }
        catch (CacheAgendaDataException ex)
        {
            // Chyba v datech linky (`B194a` C11): mezi hlášky jako linka bez dat - říká, co opravit.
            if (gen != _previewDataGeneration) return;
            _realPreviewData = null;
            Models.ColorHex.UiData = null;
            NastavHlaseni(ZdrojHlaseni.Data, [Models.KontrolaVystupu.LinkaChybaDat(instance.Display, ex.Message)]);
            StatusText = $"{NazevInstance.SVelkym(CurrentInstance.Akuzativ)} nejde vytisknout: {instance.Display}";
            NotifyPreviewPagesChanged();
            RequestPreviewRefresh?.Invoke();
        }
        catch (Exception ex)
        {
            if (gen != _previewDataGeneration) return;
            StatusText = $"⚠ Chyba načítání: {ex.Message}";
        }
    }

    /// <summary>Registr agend (capability metadata z Caché) - lookup InstanceParamName / ElementKind dle kódu.</summary>
    private readonly IReadOnlyList<AgendaRegistryEntry> _agendaEntries;

    private AgendaRegistryEntry? AgendaEntry(string? agendaCode) =>
        string.IsNullOrEmpty(agendaCode) ? null
        : _agendaEntries.FirstOrDefault(a => a.Code.Equals(agendaCode, StringComparison.OrdinalIgnoreCase));

    /// <summary>Jméno IN parametru GetData pro danou agendu (z registru; default INSTANCE_ID) -
    /// místo hardcoded větvení podle názvu agendy.</summary>
    private string InstanceParamName(string agendaCode) => AgendaEntry(agendaCode)?.InstanceParamName ?? "INSTANCE_ID";

    /// <summary>Instance-param aktuální agendy (LINKA_ID / INSTANCE_ID) - čte code-behind (PrintDialog).</summary>
    public string CurrentInstanceParamName => InstanceParamName(State.CurrentTemplate.AgendaCode ?? "");

    /// <summary>Druh JŘ elementů aktuální agendy (STOP/LINE/"") - řídí kontextové menu + paletu okna.</summary>
    public string CurrentAgendaElementKind => AgendaEntry(State.CurrentTemplate.AgendaCode)?.ElementKind ?? "";

    /// <summary>Čte agenda šablony vlastní definici sloupců? Neznámá agenda (registr se nenačetl) ano -
    /// záložku radši ukázat než schovat tam, kde funguje.</summary>
    public bool CurrentAgendaCustomColumns => AgendaEntry(State.CurrentTemplate.AgendaCode)?.CustomColumns ?? true;

    /// <summary>Posílá agenda šablony u zastávek časy variant tras? Neznámá agenda ano (viz výše).</summary>
    public bool CurrentAgendaVariantyTras => AgendaEntry(State.CurrentTemplate.AgendaCode)?.VariantyTras ?? true;

    /// <summary>Je pole v řádcích kolekce podle schématu agendy šablony? Bez schématu nebo bez té kolekce
    /// ano - radši nabídnout než schovat tam, kde funguje.</summary>
    public bool AgendaPosilaPoleKolekce(string kolekce, string pole)
    {
        var sloupce = SchemaProvider.GetSchema(State.CurrentTemplate.AgendaCode ?? "")?.FieldCollections?
            .FirstOrDefault(c => c.CollectionKey == kolekce);
        return sloupce is null || sloupce.Fields.Any(f => f.FieldName == pole);
    }

    /// <summary>Zobrazovací název aktuální konfigurace „Agenda · Varianta" (např. „Zastávkový JŘ · AD");
    /// fallback = syrový kód (registr nedostupný / neznámý kód). Čte titulek okna.</summary>
    public string CurrentAgendaDisplayName =>
        AgendaEntry(State.CurrentTemplate.AgendaCode)?.DisplayName ?? State.CurrentTemplate.AgendaCode ?? "";

    /// <summary>Název instance aktuální agendy (JŘ = „linka") z registru agend, pro všechny texty okna
    /// i hlášek, které o instanci mluví. Bez agendy v registru obecné „instance".</summary>
    public NazevInstance CurrentInstance => AgendaEntry(State.CurrentTemplate.AgendaCode)?.Nazev ?? NazevInstance.Obecny;

    /// <summary>Tooltipy navigace/výběru s doménovým názvem (např. „Předchozí linka", „Vybrat linky").</summary>
    public string PreviousInstanceTooltip => $"Předchozí {CurrentInstance.Jednotne}";
    public string NextInstanceTooltip     => $"Další {CurrentInstance.Jednotne}";
    public string InstancePickerHint      => $"Vybrat {CurrentInstance.AkuzativMnozne} - {KeyboardShortcuts.SelectInstances.Gesture}";

    /// <summary>Položka menu a bublina kontroly výstupu („Zkontrolovat vybrané linky…").</summary>
    public string KontrolaMenuText => $"Zkontrolovat {CurrentInstance.VybraneAkuzativ}…";
    public string KontrolaTooltip => $"Zkontrolovat {CurrentInstance.Shoda("všechny", "všechny", "všechny", "všechna")} {CurrentInstance.VybraneAkuzativ} - co se kde nevejde nebo vytiskne jinak";

    private void NotifyInstanceLabelsChanged()
    {
        OnPropertyChanged(nameof(CurrentInstance));
        OnPropertyChanged(nameof(KontrolaMenuText));
        OnPropertyChanged(nameof(KontrolaTooltip));
        OnPropertyChanged(nameof(ActiveInstanceText));
        OnPropertyChanged(nameof(PreviousInstanceTooltip));
        OnPropertyChanged(nameof(NextInstanceTooltip));
        OnPropertyChanged(nameof(InstancePickerHint));
        OnPropertyChanged(nameof(InstanceFilterHint));
        OnPropertyChanged(nameof(IsSampleDataPreview));
    }
}
