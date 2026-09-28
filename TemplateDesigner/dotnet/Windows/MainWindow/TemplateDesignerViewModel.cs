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

// ── Align mode enum (public so code-behind can reference it) ─────────────────
public enum AlignMode { Left, Right, CenterH, Top, Bottom, MiddleV, DistributeH, DistributeV }
public enum DuplicateDirection { Up, Down, Left, Right }

// ─────────────────────────────────────────────────────────────────────────────
// Main ViewModel
// ─────────────────────────────────────────────────────────────────────────────

public class TemplateDesignerViewModel : EDViewModel
{
    // ── Designer state (public - code-behind accesses canvas/resize directly) ─
    public DesignerState State { get; } = new();

    // ── File & path ───────────────────────────────────────────────────────────
    public string? CurrentFilePath { get; private set; }

    // ── Dirty tracking ──────────────────────────────────────────────────────
    private bool _isDirty;
    public bool IsDirty
    {
        get => _isDirty;
        private set { if (_isDirty != value) { _isDirty = value; RequestTitleUpdate?.Invoke(); } }
    }

    // ── Providers ─────────────────────────────────────────────────────────────
    public  IDataSchemaProvider    SchemaProvider        { get; }
    public  CacheAgendaProvider?   CacheProvider         { get; }
    public  ITableTemplateProvider? TableTemplateProvider => CacheProvider;

    /// <summary>Datové MLRest připojení, které používá <see cref="CacheProvider"/> (a úložiště šablon).
    /// Drženo kvůli dev přepínání firem za běhu (<see cref="DevSwitchFirm"/>).</summary>
    private readonly MLRest? _cacheMlRest;
    public  Services.CacheTemplateService? TemplateStorage { get; }

    /// <summary>Pasivní AI průvodce (záložka v levém panelu). Čte živý kontext při každém dotazu.</summary>
    public  Windows.AiGuideDialog.AiGuidePanelViewModel AiGuide { get; }
    private readonly SectionTemplateManager _sectionTemplates;

    // ── Preview params ────────────────────────────────────────────────────────
    private List<TemplateParameter>             _agendaParameters    = [];
    private readonly Dictionary<string,object?> _previewParams       = new();
    private Dictionary<string,object?>?         _cachedParamValues;
    private string?                             _lastParamsAgendaCode;

    // ── Active instances (linky pro MHD, faktury pro FAKTURY, ...) ────────────
    // Mimo Template - session state vázané na aktuální agendu. Preview rendruje vždy
    // jednu (aktivní index), tisk batch projíždí všechny.
    private List<AgendaInstance> _activeInstances = [];
    public IReadOnlyList<AgendaInstance> ActiveInstances => _activeInstances;

    /// <summary>Zvolená skupina linek (SKUPINA_LINEK_ID) - filtr instancí. Null = server default
    /// (první nezablokovaná). Session scope (mimo Template), reset při změně agendy. Propaguje se
    /// do pickeru i do GetData (aby se linka fetchla ze správné skupiny).</summary>
    private string? _activeSkupinaId;

    /// <summary>Zvolená skupina linek pro cesty mimo VM (tiskový dialog si staví GetData sám).</summary>
    public string? ActiveSkupinaId => _activeSkupinaId;

    private int _activeInstanceIndex;
    public int ActiveInstanceIndex
    {
        get => _activeInstanceIndex;
        set
        {
            if (_activeInstances.Count == 0) { _activeInstanceIndex = 0; return; }
            var clamped = Math.Clamp(value, 0, _activeInstances.Count - 1);
            if (_activeInstanceIndex != clamped)
            {
                _activeInstanceIndex = clamped;
                // Přepnutí instance (◀▶) v téže šabloně → nechat stará data do dosednutí nových (bez mock bliku).
                OnActiveInstancesChanged(reloadData: true, clearRealData: false);
            }
        }
    }

    public AgendaInstance? CurrentActiveInstance =>
        _activeInstances.Count > 0 && _activeInstanceIndex < _activeInstances.Count
            ? _activeInstances[_activeInstanceIndex] : null;

    public string ActiveInstanceText
    {
        get
        {
            var lbl = CapitalizeFirst(CurrentInstanceLabel);
            // Bez vybrané instance kreslí náhled ukázková data - řekni to tady, aby se nedal splést
            // s hotovou vývěskou. U šablony bez vazby na databázi se žádná reálná data nečekají,
            // takže by „ukázková data" lhala (rozhodnutí Jan 5. 8.).
            if (_activeInstances.Count == 0)
                return IsSampleDataPreview
                    ? $"{lbl}: - (ukázková data, klikněte pro výběr)"
                    : $"{lbl}: - (klikněte pro výběr)";
            var cur = CurrentActiveInstance;
            return _activeInstances.Count == 1
                ? $"{lbl}: {cur?.Display}"
                : $"{lbl}: {cur?.Display} ({_activeInstanceIndex + 1}/{_activeInstances.Count})";
        }
    }

    public bool HasMultipleActiveInstances => _activeInstances.Count > 1;

    // ── Rozbalovací seznam vybraných instancí ─────────────────────────────────

    /// <summary>Jedna položka rozbalovacího seznamu vybraných instancí.</summary>
    /// <remarks>Nese jen to, čím se instance pozná („číslo - název"). Doplňující popis z výběru
    /// instancí (platnost) sem nepatří, seznam slouží k přepnutí, ne k prohlížení (Jan 21. 9.).</remarks>
    public record InstanceVolba(int Index, string Display, bool JeAktualni);

    /// <summary>Od kolika vybraných instancí má seznam nahoře hledací pole.</summary>
    private const int InstanciNaHledani = 12;

    private bool _instanceListOpen;

    /// <summary>
    /// Kdy se seznam naposledy zavřel - klik na chip při rozbaleném seznamu ho totiž nejdřív
    /// zavře (klik mimo popup) a teprve pak dorazí na chip, takže by se hned zase otevřel.
    /// </summary>
    private DateTime _instanceListZavrenoV = DateTime.MinValue;

    /// <summary>Je rozbalený seznam vybraných instancí?</summary>
    public bool IsInstanceListOpen
    {
        get => _instanceListOpen;
        set
        {
            if (_instanceListOpen == value) return;
            _instanceListOpen = value;
            if (!value) { _instanceFilter = ""; _instanceListZavrenoV = DateTime.UtcNow; }
            OnPropertyChanged(nameof(IsInstanceListOpen));
            OnPropertyChanged(nameof(InstanceFilter));
            OnPropertyChanged(nameof(InstanceVolby));
        }
    }

    private string _instanceFilter = "";

    /// <summary>Hledaný text v rozbaleném seznamu instancí.</summary>
    public string InstanceFilter
    {
        get => _instanceFilter;
        set
        {
            if (_instanceFilter == value) return;
            _instanceFilter = value;
            OnPropertyChanged(nameof(InstanceFilter));
            OnPropertyChanged(nameof(InstanceVolby));
        }
    }

    public bool ShowInstanceFilter => _activeInstances.Count > InstanciNaHledani;

    /// <summary>Vybrané instance pro rozbalovací seznam, zúžené hledaným textem.</summary>
    public IReadOnlyList<InstanceVolba> InstanceVolby =>
        SestavVolby(_activeInstances, _activeInstanceIndex, _instanceFilter);

    /// <summary>
    /// Položky seznamu: pořadí drží původní index, aby hledání nepřepínalo na jinou instanci.
    /// </summary>
    internal static IReadOnlyList<InstanceVolba> SestavVolby(
        IReadOnlyList<AgendaInstance> instance, int aktualni, string hledani) =>
        [.. instance
            .Select((inst, i) => new InstanceVolba(i, inst.Display, i == aktualni))
            .Where(v => hledani == "" || v.Display.Contains(hledani, StringComparison.CurrentCultureIgnoreCase))];

    /// <summary>Popisek hledacího pole v seznamu.</summary>
    public string InstanceFilterHint => $"Hledat {CurrentInstanceLabelPlural}";

    /// <summary>Popisek zvolené skupiny linek pro status (prázdné = default/žádná skupina).</summary>
    private string? _activeSkupinaLabel;

    /// <summary>Status chip skupiny - viditelný jen když je skupina zvolená.</summary>
    public string ActiveSkupinaText => string.IsNullOrEmpty(_activeSkupinaLabel)
        ? "" : $"Skupina: {_activeSkupinaLabel}";

    public bool HasActiveSkupina => !string.IsNullOrEmpty(_activeSkupinaLabel);

    /// <summary>Vyčistí aktivní instance - volá se při změně agendy / načtení šablony s jinou agendou.</summary>
    private void ClearActiveInstances()
    {
        _activeInstances = [];
        _activeSkupinaId = null;
        _activeSkupinaLabel = null;
        OnPropertyChanged(nameof(ActiveSkupinaText));
        OnPropertyChanged(nameof(HasActiveSkupina));
        _activeInstanceIndex = 0;
        // reloadData:true záměrně i když instance byly prázdné: vyčistí i _realPreviewData
        // (vázaná na starou instanci/agendu - jinak se stará data renderují přes novou
        // šablonu = "zmizelé" preview) a bumpne generaci → invaliduje in-flight refresh.
        // RefreshDataForCurrentInstanceAsync sám hned vrátí (žádná aktivní instance).
        OnActiveInstancesChanged(reloadData: true);
    }

    /// <summary>Generace preview dat - chrání proti dosednutí zastaralého async GetData
    /// (přepnu agendu/instanci, zatímco běží předchozí fetch staré). Bump při každé změně.</summary>
    private int _previewDataGeneration;
    private bool _nacitaniBezi;

    private bool _nacitaniDat;
    /// <summary>Načítají se data linky déle než půl sekundy - štítek linky ukáže „načítám…".</summary>
    public bool NacitaniDat { get => _nacitaniDat; private set => Set(ref _nacitaniDat, value); }

    /// <param name="clearRealData">true (změna agendy/šablony) = stará data vyčistit hned - patří jiné
    /// šabloně, jinak by se kreslila přes novou (náhled do dosednutí ukáže mock). false (přepnutí instance
    /// v téže šabloně) = nechat předchozí data zobrazená, dokud nedosedne nová z GetData - jinak by náhled
    /// u pomalejšího MHD (2-3 s) „blikal" na hardcoded mock. Swap na nová data dělá RefreshDataForCurrentInstanceAsync.</param>
    private void OnActiveInstancesChanged(bool reloadData, bool clearRealData = true)
    {
        _previewDataGeneration++;
        OnPropertyChanged(nameof(ActiveInstanceText));
        OnPropertyChanged(nameof(CurrentActiveInstance));
        OnPropertyChanged(nameof(HasMultipleActiveInstances));
        OnPropertyChanged(nameof(InstanceVolby));
        OnPropertyChanged(nameof(ShowInstanceFilter));
        if (reloadData)
        {
            if (clearRealData)
            {
                _realPreviewData = null;
                NastavHlaseni(ZdrojHlaseni.Data, []);   // hlášky patří k zahozeným datům
                _realPreviewIndex = 0;
                Models.ColorHex.UiData = null;   // ať náhled barvy z dat neukáže barvu staré instance
                NotifyPreviewPagesChanged();
                RequestPreviewRefresh?.Invoke();
            }
            _ = RefreshDataForCurrentInstanceAsync();
        }
        CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>„Obnovit data" (F5 / tlačítko ↻ u instance chipu): vyčistí cache provideru a znovu
    /// načte schéma, parametry i tisková data aktuální instance - data změněná na serveru (značky,
    /// poznámky) se projeví bez restartu.</summary>
    private void RefreshData()
    {
        _previewDataGeneration++;               // invaliduje případný běžící fetch → guard proti dosednutí stale dat
        CacheProvider?.InvalidateCaches();
        RefreshFieldsList();                    // schéma polí + parametry (teď fresh)
        _ = RefreshDataForCurrentInstanceAsync(keepPage: true); // re-fetch GetData pro aktuální linku
    }

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
            var columnsDefJson = CurrentColumnsDefinitionJson();
            var turnusy = CurrentTurnusyKeSpojum();
            var getDataParams = ComposeGetDataParams(_previewParams, paramName, instance.Value, _activeSkupinaId, columnsDefJson, turnusy, JazykSablony);
            List<DataContext>? pages;
            try { pages = await Task.Run(() => CacheProvider.GetData(agendaCode, getDataParams)); }
            finally { if (gen == _previewDataGeneration) { _nacitaniBezi = false; NacitaniDat = false; } }

            // Mezitím se přepnula agenda/instance (nebo se vyčistilo) → tento výsledek je
            // zastaralý a nesmí dosednout do nové šablony.
            if (gen != _previewDataGeneration) return;
            _lastColumnsDefinitionSnapshot = CurrentWireSnapshot();   // data odteď odpovídají definici i jazyku

            if (pages is null || pages.Count == 0)
            {
                _realPreviewData = null;
                Models.ColorHex.UiData = null;
                NastavHlaseni(ZdrojHlaseni.Data, []);
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

    /// <summary>Zobrazovací název aktuální konfigurace „Agenda · Varianta" (např. „Zastávkový JŘ · AD");
    /// fallback = syrový kód (registr nedostupný / neznámý kód). Čte titulek okna.</summary>
    public string CurrentAgendaDisplayName =>
        AgendaEntry(State.CurrentTemplate.AgendaCode)?.DisplayName ?? State.CurrentTemplate.AgendaCode ?? "";

    /// <summary>User-facing název „instance" aktuální agendy (JŘ = „linka", faktury = „faktura",
    /// jinak „instance") - z registru agend. Okno ho zobrazí místo obecného „instance".</summary>
    public string CurrentInstanceLabel       => AgendaEntry(State.CurrentTemplate.AgendaCode)?.InstanceLabel ?? "instance";
    public string CurrentInstanceLabelPlural => AgendaEntry(State.CurrentTemplate.AgendaCode)?.InstanceLabelPlural ?? "instance";

    /// <summary>Tooltipy navigace/výběru s doménovým názvem (např. „Předchozí linka", „…výběr - linky").</summary>
    public string PreviousInstanceTooltip => $"Předchozí {CurrentInstanceLabel}";
    public string NextInstanceTooltip     => $"Další {CurrentInstanceLabel}";
    public string InstancePickerHint      => $"Klikněte pro výběr - {CurrentInstanceLabelPlural} (Ctrl+Shift+I)";

    private static string CapitalizeFirst(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];

    private void NotifyInstanceLabelsChanged()
    {
        OnPropertyChanged(nameof(CurrentInstanceLabel));
        OnPropertyChanged(nameof(CurrentInstanceLabelPlural));
        OnPropertyChanged(nameof(ActiveInstanceText));
        OnPropertyChanged(nameof(PreviousInstanceTooltip));
        OnPropertyChanged(nameof(NextInstanceTooltip));
        OnPropertyChanged(nameof(InstancePickerHint));
        OnPropertyChanged(nameof(InstanceFilterHint));
        OnPropertyChanged(nameof(IsSampleDataPreview));
    }

    // ── Real preview data ────────────────────────────────────────────────────
    private List<DataContext>?                  _realPreviewData;
    private int                                _realPreviewIndex;

    /// <summary>Počet dostupných stránek reálných dat (0 = mock mód).</summary>
    public int RealPreviewPageCount => _realPreviewData?.Count ?? 0;

    /// <summary>Probíhá výměna položek pageru (NotifyPreviewPagesChanged) - ComboBox při výměně
    /// ItemsSource synchronně pushne SelectedIndex=-1; guard ho nenechá přepsat drženou stránku.</summary>
    private bool _previewPagesSyncing;

    /// <summary>Index aktuálně zobrazené stránky reálných dat (0-based).</summary>
    public int RealPreviewIndex
    {
        get => _realPreviewIndex;
        set
        {
            if (_realPreviewData is null || _realPreviewData.Count == 0) return;
            if (_previewPagesSyncing && value < 0) return;   // -1 z výměny ItemsSource - ignorovat, resync přijde po ní
            var clamped = Math.Clamp(value, 0, _realPreviewData.Count - 1);
            if (Set(ref _realPreviewIndex, clamped))
            {
                RequestPreviewRefresh?.Invoke();
                OnPropertyChanged(nameof(PageNavigationText));
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
            else if (value != clamped)
            {
                // Binding poslal hodnotu mimo rozsah (ComboBox při výměně ItemsSource pushne -1)
                // a clamp nezměnil stav → notify vrátí ComboBox zpět na platný index.
                OnPropertyChanged(nameof(RealPreviewIndex));
            }
        }
    }

    /// <summary>Text stránkování "3 / 12" pro UI.</summary>
    public string PageNavigationText => RealPreviewPageCount > 0
        ? $"{RealPreviewIndex + 1} / {RealPreviewPageCount}"
        : "-";

    public bool HasMultiplePages => RealPreviewPageCount > 1;

    private int _pocetStranSazby;
    /// <summary>
    /// Na kolik stran se tiskne **právě zobrazená** položka - druhá osa stránkování (`B131`).
    /// </summary>
    /// <remarks>
    /// Nabídka nad náhledem přepíná stránky dat, kdežto strany sazby té jedné položky leží pod
    /// sebou ve scrollu. Bez tohohle údaje uživatel netuší, že pod první stranou něco je - a naopak
    /// hledá zpětné spoje na druhé straně, když jsou na druhé stránce dat.
    /// </remarks>
    public int PocetStranSazby
    {
        get => _pocetStranSazby;
        set
        {
            if (Set(ref _pocetStranSazby, value))
            {
                OnPropertyChanged(nameof(PocetStranSazbyText));
                OnPropertyChanged(nameof(HasPocetStranSazby));
            }
        }
    }

    /// <summary>Jediná strana se nehlásí - text, který nic neříká, jen zabírá lištu.
    /// Nehlásí se ani počet, který už říká přepínač stránek šablony („Stránka 1 z 2").</summary>
    public bool HasPocetStranSazby => _pocetStranSazby > 1 && !(MaPredlohy && _pocetStranSazby == PocetPredloh);

    public string PocetStranSazbyText => _pocetStranSazby switch
    {
        < 2 => "",
        < 5 => $"tiskne se na {_pocetStranSazby} strany",
        _   => $"tiskne se na {_pocetStranSazby} stran"
    };

    /// <summary>Viditelnost lišty stránkování dat - i pro jedinou stránku (combobox říká, CO preview
    /// zobrazuje - zastávka × směr); skrytá jen v mock módu (žádná reálná data).</summary>
    public bool HasPageNavigation => RealPreviewPageCount > 0 && _prepinaniDatMaSmysl;

    private bool _prepinaniDatMaSmysl;

    /// <summary>
    /// Přepočítá, jestli nabídka stránek dat nad náhledem něco přepíná (`B187`). Volá se po výměně
    /// dat i po každé změně šablony - pole ze závorek přibude třeba jen úpravou textu.
    /// </summary>
    public void ObnovStrankovaniDat()
    {
        bool ma = _realPreviewData is { Count: > 0 } data
            && ZavislostNaDatech.PrepinaniMaSmysl(State.CurrentTemplate, data);
        if (ma == _prepinaniDatMaSmysl) return;
        _prepinaniDatMaSmysl = ma;
        OnPropertyChanged(nameof(HasPageNavigation));
        OnPropertyChanged(nameof(MaListuNadNahledem));
    }

    /// <summary>
    /// Náhled kreslí ukázková data, ne data z databáze. Říká to **štítek instance** ve stavovém
    /// řádku, ne nic navíc u náhledu (rozhodnutí Jan 5. 8. - informace už tam byla, chybělo jen
    /// spojení „nevybraná linka ⇒ v náhledu jsou vymyšlená data"). Platí jen pro agendu, která
    /// reálná data vůbec umí: u šablony bez vazby na databázi by hlášení „ukázková data" lhalo.
    /// </summary>
    public bool IsSampleDataPreview =>
        ShouldReportSampleData(
            RealPreviewPageCount,
            State.CurrentTemplate.AgendaCode,
            State.CurrentTemplate.AgendaCode is { Length: > 0 } c && CacheProvider?.GetCacheClass(c) is not null);

    /// <summary>Testovatelné jádro <see cref="IsSampleDataPreview"/>.</summary>
    internal static bool ShouldReportSampleData(int realPageCount, string? agendaCode, bool agendaHasData) =>
        realPageCount == 0 && agendaCode is { Length: > 0 } && agendaHasData;

    /// <summary>
    /// Notifikace po výměně reálných preview dat. Pořadí je záměrné: <see cref="PreviewPageItems"/>
    /// PŘED <see cref="RealPreviewIndex"/> - výměna ItemsSource comboboxu synchronně pushne
    /// SelectedIndex=-1 do VM a zpětná korekce ze setteru se během reentrantního binding update
    /// zahodí (combobox by zůstal prázdný). Následná notifikace indexu ho srovná na platnou hodnotu.
    /// </summary>
    private void NotifyPreviewPagesChanged()
    {
        _prepinaniDatMaSmysl = _realPreviewData is { Count: > 0 } data
            && ZavislostNaDatech.PrepinaniMaSmysl(State.CurrentTemplate, data);
        _previewPagesSyncing = true;
        try
        {
            OnPropertyChanged(nameof(RealPreviewPageCount));
            OnPropertyChanged(nameof(PreviewPageItems));
            OnPropertyChanged(nameof(RealPreviewIndex));
            OnPropertyChanged(nameof(PageNavigationText));
            OnPropertyChanged(nameof(HasMultiplePages));
            OnPropertyChanged(nameof(HasPageNavigation));
            OnPropertyChanged(nameof(MaListuNadNahledem));
            OnPropertyChanged(nameof(IsSampleDataPreview));
            OnPropertyChanged(nameof(PreviewItemNoun));
            OnPropertyChanged(nameof(PreviousPageTooltip));
            OnPropertyChanged(nameof(NextPageTooltip));
            OnPropertyChanged(nameof(ActiveInstanceText));   // nese text o ukázkových datech
        }
        finally { _previewPagesSyncing = false; }
        // WPF umí nastavení SelectedIndex během výměny ItemsSource spolknout (combobox zůstane
        // prázdný do ručního ◀/▶) → code-behind ho po usazení bindingu srovná deferovaně.
        RequestPreviewPagerSync?.Invoke();
    }

    /// <summary>Callback do code-behindu: po výměně stránek dat srovnej SelectedIndex pageru
    /// (deferovaně přes Dispatcher) - viz komentář v NotifyPreviewPagesChanged.</summary>
    public Action? RequestPreviewPagerSync { get; set; }

    /// <summary>Položky comboboxu stránkování dat - přímý skok na položku bez proklikávání ◀▶.</summary>
    public IReadOnlyList<string> PreviewPageItems =>
        _realPreviewData is null ? []
        : [.. _realPreviewData.Select((p, i) => BuildPreviewPageLabel(p, i, _realPreviewData.Count))];

    /// <summary>Jak se jmenuje to, co nabídka nad náhledem přepíná - pro popisky tlačítek ◀▶.</summary>
    public string PreviewItemNoun => _realPreviewData is { Count: > 0 }
        ? NazevPolozky(_realPreviewData[0]).ToLowerInvariant()
        : "část";

    public string PreviousPageTooltip => $"Předchozí {PreviewItemNoun}";
    public string NextPageTooltip     => $"Další {PreviewItemNoun}";

    /// <summary>
    /// Jak se jmenuje to, co je na jedné stránce dat - podle toho, co stránka nese.
    /// </summary>
    /// <remarks>
    /// <para><b>Zastávkový JŘ</b> = zastávka × směr, pozná se podle <c>POPIS_ZASTAVKA</c>, které
    /// linkové agendy neposílají.</para>
    /// <para><b>Linkový JŘ</b> = jeden směr linky. <b>Obousměrná stránka</b> nese oba směry naráz
    /// a <c>SMER</c> je u ní vědomě prázdné (`LinkovyJrAd.cls` kolem ř. 559) - to ji spolehlivě
    /// odliší, takže se nejmenuje „Směr", ale „Linka", a není na ni potřeba nové pole na drátě.</para>
    /// <para>Když stránka nenese ani jedno, zůstává obecná „Část" (agendy typu faktur).</para>
    /// </remarks>
    internal static string NazevPolozky(DataContext page)
    {
        if (page.GetString("POPIS_ZASTAVKA").Trim().Length > 0) return "Zastávka";
        if (page.GetString("VYCHOZI_ZASTAVKA").Trim().Length > 0
            || page.GetString("KONECNA_ZASTAVKA").Trim().Length > 0)
            return page.GetString("SMER").Trim().Length == 0 ? "Linka" : "Směr";
        return "Část";
    }

    /// <summary>
    /// Popisek položky pro nabídku nad náhledem: co to je, kolikátá z kolika a čeho se týká.
    /// </summary>
    /// <remarks>
    /// ⚠️ **Podstatné jméno tam je proto, aby se dvě různá „víc stránek" nepletla** (`B131`, nález
    /// Jan 7. 9.). Nabídka přepíná **stránky dat** (u vývěsky zastávka × směr, u linky směr),
    /// kdežto **strany sazby** té jedné položky leží pod sebou ve scrollu. Do 10. 9. se obojí
    /// jmenovalo „stránka" a čítač „/ 2" se četl jako „celkem 2 strany" - uživatel pak hledal
    /// zpětné spoje a měl je za nevytištěné, protože byly na druhé stránce dat.
    ///
    /// <para>U jediné položky se čítač vynechá - „1 z 1" nic neříká a přepínat není co.</para>
    ///
    /// <para>⚠️ Do 31. 8. to u linkového JŘ hlásilo „Stránka N" i tam, kde stránka směr nesla:
    /// popisek se stavěl výhradně z <c>POPIS_ZASTAVKA</c>, které linkové agendy ve schématu
    /// nemají.</para>
    /// </remarks>
    public static string BuildPreviewPageLabel(DataContext page, int index, int celkem)
    {
        var konecna  = page.GetString("KONECNA_ZASTAVKA").Trim();
        var zastavka = page.GetString("POPIS_ZASTAVKA").Trim();
        var vychozi  = page.GetString("VYCHOZI_ZASTAVKA").Trim();

        string popis = "";
        if (zastavka.Length > 0)
            popis = konecna.Length == 0 ? zastavka : $"{zastavka} → {konecna}";
        else if (vychozi.Length > 0 && konecna.Length > 0)
        {
            string sipka = page.GetString("SMER").Trim().Length == 0 ? "↔" : "→";
            popis = $"{vychozi} {sipka} {konecna}";
        }
        else if (vychozi.Length > 0) popis = vychozi;
        else if (konecna.Length > 0) popis = konecna;

        string nazev = NazevPolozky(page);

        // Směr se jmenuje, ne čísluje - „tam" a „zpět" je zavedené názvosloví a uživatel z něj
        // pozná, na co kouká, kdežto „1 z 2" ne (rozhodnutí Jan 10. 9.). Píše se i u jediné
        // položky: říká něco navíc, na rozdíl od čítače „1 z 1".
        if (nazev == "Směr" && SmerTamNeboZpet(page) is { } smer)
            return popis.Length > 0 ? $"{smer}: {popis}" : smer;

        if (celkem <= 1) return popis.Length > 0 ? popis : nazev;

        string cislo = $"{nazev} {index + 1} z {celkem}";
        return popis.Length > 0 ? $"{cislo}: {popis}" : cislo;
    }

    /// <summary>„Směr tam" / „Směr zpět" podle pole <c>SMER</c> (T/Z), nebo null u jiné hodnoty.</summary>
    private static string? SmerTamNeboZpet(DataContext page) =>
        page.GetString("SMER").Trim().ToUpperInvariant() switch
        {
            "T" => "Směr tam",
            "Z" => "Směr zpět",
            _   => null
        };

    private RelayCommand? _prevPage, _nextPage;
    public RelayCommand PreviousPageCommand => _prevPage ??= Command(
        () => RealPreviewIndex--,
        () => RealPreviewIndex > 0);
    public RelayCommand NextPageCommand => _nextPage ??= Command(
        () => RealPreviewIndex++,
        () => RealPreviewIndex < RealPreviewPageCount - 1);

    /// <summary>Parametry agendy pro PrintDialog.</summary>
    public IReadOnlyList<TemplateParameter> AgendaParameters => _agendaParameters;

    // ── Sbalitelná lišta parametrů nad preview ────────────────────────────────
    private bool _paramsBarExpanded;

    /// <summary>Rozbalení lišty parametrů nad preview. Default sbaleno (preview má celou výšku);
    /// stav se persistuje v MainStatePref.</summary>
    public bool ParamsBarExpanded
    {
        get => _paramsBarExpanded;
        set
        {
            if (Set(ref _paramsBarExpanded, value))
            {
                OnPropertyChanged(nameof(ParamsBarToggleText));
                OnPropertyChanged(nameof(ParamsBarCollapsed));
            }
        }
    }

    public bool   ParamsBarCollapsed  => !_paramsBarExpanded;
    public string ParamsBarToggleText => _paramsBarExpanded ? "⚙ Parametry ▴" : "⚙ Parametry ▾";

    /// <summary>Souhrn hodnot odchylných od defaultu pro sbalenou lištu - uživatel vidí na první
    /// pohled, že má něco přepnuté, aniž by lištu rozbaloval. Prázdný výběr = „výchozí nastavení".</summary>
    public string ParamsSummaryText
    {
        get
        {
            var parts = new List<string>();
            foreach (var p in _agendaParameters)
            {
                if (!_previewParams.TryGetValue(p.Name, out var val) || val is null) continue;
                var cur = val.ToString() ?? "";
                var def = p.DefaultValue ?? "";
                if (p.Type == "bool")
                {
                    bool curB = IsTruthy(cur), defB = IsTruthy(def);
                    if (curB != defB) parts.Add(curB ? p.Label : $"{p.Label}: vyp");
                }
                else if (!string.Equals(cur, def, StringComparison.Ordinal))
                {
                    parts.Add($"{p.Label}: {cur}");
                }
            }
            return parts.Count == 0 ? "výchozí nastavení" : string.Join(" · ", parts);
        }
    }

    private static bool IsTruthy(string s) =>
        s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "1";

    private void NotifyParamsSummaryChanged() => OnPropertyChanged(nameof(ParamsSummaryText));

    /// <summary>
    /// Sdílený dictionary parametrů (preview lišta + PrintDialog operují nad stejnou instancí).
    /// Hodnoty se persistují přes <see cref="LoadPrintParamsAndApplyAsync"/> a <see cref="SavePrintParamsAsync"/>.
    /// </summary>
    public Dictionary<string, object?> PreviewParamValues => _previewParams;

    // ── Clipboard ─────────────────────────────────────────────────────────────
    private List<TemplateElement> _clipboard = [];

    // ── Observable collections (drive XAML data-binding) ──────────────────────
    public ObservableCollection<SectionVM>          Sections          { get; } = [];
    public ObservableCollection<SchemaGroupVM>      SchemaGroups      { get; } = [];
    public ObservableCollection<ParameterControlVM> PreviewParameters { get; } = [];

    // ── Bindable properties ───────────────────────────────────────────────────

    private bool _showFieldsEmptyState;
    /// <summary>Panel „Datová pole" nemá co zobrazit z agendy (bez agendy/DB nebo obecná šablona) → ukázat empty-state místo prázdna.</summary>
    public bool ShowFieldsEmptyState { get => _showFieldsEmptyState; private set => Set(ref _showFieldsEmptyState, value); }
    public bool CanUndo => State.CanUndo;
    public bool CanRedo => State.CanRedo;

    private string _statusText = "";
    /// <summary>Transientní „akce/progres" zóna status baru (uloženo / chyba / export N/N / migrace).
    /// Odděleno od <see cref="SelectionStatusText"/>, aby se navzájem nepřepisovaly - akční hláška
    /// zůstane viditelná i když uživatel mezitím klikne jinam a změní se stav výběru.</summary>
    public string StatusText
    {
        get => _statusText;
        set => Set(ref _statusText, value);
    }

    // ── Hlášky vpravo dole (`B178`) ─────────────────────────────────────────

    /// <summary>Odkud hláška přišla - každý zdroj se přepočítává v jinou chvíli, proto zvlášť.</summary>
    public enum ZdrojHlaseni { Data, Font, Preteceni, Orez, Sloupce, Sazba }

    private readonly Dictionary<ZdrojHlaseni, IReadOnlyList<Hlaseni>> _hlaseniZeZdroju = [];

    /// <summary>
    /// Nastaví hlášky jednoho zdroje. Data (sloupce odjezdů, zmenšené písmo, malé kódy) se počítají
    /// při načtení dat, font při výměně šablony, přetečení / ořez / úzké sloupce při kreslení náhledu.
    /// </summary>
    public void NastavHlaseni(ZdrojHlaseni zdroj, IReadOnlyList<Hlaseni> hlaseni)
    {
        var stare = _hlaseniZeZdroju.GetValueOrDefault(zdroj) ?? [];
        if (stare.SequenceEqual(hlaseni)) return;
        _hlaseniZeZdroju[zdroj] = hlaseni;
        Hlaseni = [.. _hlaseniZeZdroju.Values.SelectMany(h => h)
            .OrderByDescending(h => h.Zavaznost)];
        OnPropertyChanged(nameof(Hlaseni));
        OnPropertyChanged(nameof(HasHlaseni));
        foreach (var n in new[] { nameof(PocetChyb), nameof(PocetUpozorneni), nameof(PocetInformaci),
                                  nameof(HasChyby), nameof(HasUpozorneni), nameof(HasInformace),
 })
            OnPropertyChanged(n);
        if (Hlaseni.Count == 0) IsHlaseniListOpen = false;
    }

    /// <summary>Všechny hlášky, nejvážnější první.</summary>
    public IReadOnlyList<Hlaseni> Hlaseni { get; private set; } = [];

    public bool HasHlaseni => Hlaseni.Count > 0;
    public bool HasChyby => Pocet(ZavaznostHlaseni.Chyba) > 0;
    public bool HasUpozorneni => Pocet(ZavaznostHlaseni.Upozorneni) > 0;
    public bool HasInformace => Pocet(ZavaznostHlaseni.Informace) > 0;

    // Jeden kompaktní blok (rozhodnutí Jan 24. 9.): ikony s počty podle závažnosti, celé znění
    // je v rozbaleném seznamu.
    public int PocetChyb => Pocet(ZavaznostHlaseni.Chyba);
    public int PocetUpozorneni => Pocet(ZavaznostHlaseni.Upozorneni);
    public int PocetInformaci => Pocet(ZavaznostHlaseni.Informace);

    private int Pocet(ZavaznostHlaseni z) => Hlaseni.Count(h => h.Zavaznost == z);

    private bool _hlaseniListOpen;
    private DateTime _hlaseniListZavrenoV = DateTime.MinValue;

    /// <summary>Je rozbalený seznam hlášek? Klik na odznak ho otevře, klik mimo zavře.</summary>
    public bool IsHlaseniListOpen
    {
        get => _hlaseniListOpen;
        set
        {
            if (_hlaseniListOpen == value) return;
            _hlaseniListOpen = value;
            if (!value) _hlaseniListZavrenoV = DateTime.UtcNow;
            OnPropertyChanged(nameof(IsHlaseniListOpen));
        }
    }

    /// <summary>Klik na řádek seznamu vybere prvek, ke kterému hláška patří. Host (okno) ho najde
    /// v šabloně a aktivuje jeho sekci.</summary>
    public Action<Guid>? RequestSelectElement { get; set; }

    private string _selectionStatusText = "";
    /// <summary>Klidová „stav výběru" zóna status baru (aktuální element / sekce). Píše ji jen
    /// <see cref="UpdateStatusForSelection"/>; mění se volně s výběrem, nepřepíše akční hlášku.</summary>
    public string SelectionStatusText
    {
        get => _selectionStatusText;
        set => Set(ref _selectionStatusText, value);
    }


    private string _agendaNameText = "Agenda: -";
    public string AgendaNameText
    {
        get => _agendaNameText;
        set => Set(ref _agendaNameText, value);
    }

    private bool _hasParameters;
    public bool HasParameters
    {
        get => _hasParameters;
        private set => Set(ref _hasParameters, value);
    }

    public bool ShowGrid
    {
        get => State.ShowGrid;
        set { State.ShowGrid = value; OnPropertyChanged(); RequestCanvasRefresh?.Invoke(); }
    }

    public bool ShowSectionHeaders
    {
        get => State.ShowSectionHeaders;
        set { State.ShowSectionHeaders = value; OnPropertyChanged(); RequestCanvasRefresh?.Invoke(); RequestCanvasSizeUpdate?.Invoke(); }
    }

    public bool ShowRuler
    {
        get => State.ShowRuler;
        set { State.ShowRuler = value; OnPropertyChanged(); RequestCanvasRefresh?.Invoke(); }
    }

    public bool SnapToGrid
    {
        get => State.SnapToGrid;
        set { State.SnapToGrid = value; OnPropertyChanged(); }
    }

    public bool SnapToElements
    {
        get => State.SnapToElements;
        set { State.SnapToElements = value; OnPropertyChanged(); }
    }

    public float GridSize
    {
        get => State.GridSize;
        set { State.GridSize = value; OnPropertyChanged(); RequestCanvasRefresh?.Invoke(); }
    }

    public float SnapGapMm
    {
        get => State.SnapGapMm;
        set { State.SnapGapMm = value; OnPropertyChanged(); }
    }

    // ── Callbacks wired by code-behind ────────────────────────────────────────
    public Action? RequestCanvasRefresh      { get; set; }
    public Action? RequestTitleUpdate        { get; set; }
    public Action? RequestCanvasSizeUpdate   { get; set; }
    public Action? RequestPropertiesPanelUpdate { get; set; }
    public Action? RequestPreviewRefresh     { get; set; }
    /// <summary>Sync GridSize ComboBox v toolbaru po načtení šablony (ComboBox není bindovaný).</summary>
    public Action<float>? RequestGridSizeUiSync { get; set; }

    /// <summary>Nemodální hláška při chybě čtení z Caché (schema/params/mock/instance). Code-behind
    /// ji marshalne na UI thread (může fírnout z background Task.Run) a zobrazí ve status baru.</summary>
    public Action<string>? RequestReadErrorStatus { get; set; }

    // ── Events ────────────────────────────────────────────────────────────────
    /// <summary>Fired after Sections is rebuilt so code-behind can restore ListBox selection.</summary>
    public event Action? SectionsRebuilt;

    // ── Commands ──────────────────────────────────────────────────────────────
    private RelayCommand? _undo, _redo, _delete, _copy, _paste, _cut, _copyStyle, _pasteStyle, _prevInstance, _nextInstance, _refreshData, _toggleParamsBar, _instanceList;
    private RelayCommand<int>? _selectInstance;
    private AsyncRelayCommand? _save, _saveAs, _open, _openFromCache, _saveToCache, _saveToCacheAs, _importJson, _exportJson, _exportPdf, _exportPng, _new, _changeAgenda, _openInstancePicker;

    public RelayCommand UndoCommand          => _undo      ??= Command(Undo,          () => CanUndo);
    public RelayCommand RedoCommand          => _redo      ??= Command(Redo,          () => CanRedo);
    // DB je primární úložiště: Uložit / Otevřít / Uložit jako jdou do databáze. JSON = import/export.
    public AsyncRelayCommand SaveCommand     => _save      ??= CommandAsync(SaveTemplate);
    public AsyncRelayCommand SaveAsCommand   => _saveAs    ??= CommandAsync(SaveToCacheAsAsync, () => TemplateStorage is not null);
    public AsyncRelayCommand NewCommand      => _new       ??= CommandAsync(NewTemplateAsync);
    public AsyncRelayCommand OpenCommand     => _open      ??= CommandAsync(OpenFromCacheAsync, () => TemplateStorage is not null);
    public AsyncRelayCommand ImportJsonCommand => _importJson ??= CommandAsync(ImportFromJsonAsync);
    public AsyncRelayCommand ExportJsonCommand => _exportJson ??= CommandAsync(ExportToJsonAsync);
    public AsyncRelayCommand OpenFromCacheCommand  => _openFromCache  ??= CommandAsync(OpenFromCacheAsync,  () => TemplateStorage is not null);
    public AsyncRelayCommand SaveToCacheCommand    => _saveToCache    ??= CommandAsync(SaveToCacheAsync,    () => TemplateStorage is not null);
    public AsyncRelayCommand SaveToCacheAsCommand  => _saveToCacheAs  ??= CommandAsync(SaveToCacheAsAsync,  () => TemplateStorage is not null);
    public AsyncRelayCommand ChangeAgendaCommand   => _changeAgenda   ??= CommandAsync(ChangeAgendaAsync);
    public AsyncRelayCommand OpenInstancePickerCommand => _openInstancePicker ??= CommandAsync(OpenInstancePickerAsync);
    // Šipka rozbaluje seznam vybraných instancí, jméno instance uprostřed chipu otevírá jejich
    // výběr jako dřív (rozhodnutí Jan 21. 9.) - dvě různé věci, dvě různá místa.
    public RelayCommand OpenInstanceListCommand => _instanceList ??= Command(() =>
    {
        if (DateTime.UtcNow - _instanceListZavrenoV < TimeSpan.FromMilliseconds(250)) return;
        IsInstanceListOpen = !IsInstanceListOpen;
    });
    public RelayCommand OpenHlaseniListCommand => _hlaseniList ??= Command(() =>
    {
        if (DateTime.UtcNow - _hlaseniListZavrenoV < TimeSpan.FromMilliseconds(250)) return;
        IsHlaseniListOpen = !IsHlaseniListOpen;
    });
    private RelayCommand? _hlaseniList;
    public RelayCommand<Hlaseni> VybratPrvekHlaseniCommand => _vybratPrvekHlaseni ??= Command<Hlaseni>(h =>
    {
        if (h?.PrvekId is not { } id) return;
        IsHlaseniListOpen = false;
        RequestSelectElement?.Invoke(id);
    });
    private RelayCommand<Hlaseni>? _vybratPrvekHlaseni;
    public RelayCommand<int> SelectInstanceCommand => _selectInstance ??= Command<int>(i =>
    {
        IsInstanceListOpen = false;
        ActiveInstanceIndex = i;
    });
    public RelayCommand PreviousInstanceCommand    => _prevInstance   ??= Command(() => ActiveInstanceIndex--, () => HasMultipleActiveInstances && _activeInstanceIndex > 0);
    public RelayCommand NextInstanceCommand        => _nextInstance   ??= Command(() => ActiveInstanceIndex++, () => HasMultipleActiveInstances && _activeInstanceIndex < _activeInstances.Count - 1);
    public RelayCommand RefreshDataCommand         => _refreshData    ??= Command(RefreshData);
    public RelayCommand ToggleParamsBarCommand     => _toggleParamsBar ??= Command(() => ParamsBarExpanded = !ParamsBarExpanded);
    public AsyncRelayCommand ExportPdfCommand => _exportPdf ??= CommandAsync(ExportPreviewPdfAsync, () => !_exportRunning);
    public AsyncRelayCommand ExportPngCommand => _exportPng ??= CommandAsync(ExportPreviewPngAsync, () => !_exportRunning);
    public new RelayCommand DeleteCommand => _delete    ??= Command(DeleteSelected);
    public RelayCommand CopyCommand     => _copy      ??= Command(CopySelected);
    public RelayCommand CutCommand      => _cut       ??= Command(CutSelected);
    public RelayCommand PasteCommand    => _paste     ??= Command(PasteClipboard);
    public RelayCommand CopyStyleCommand  => _copyStyle  ??= Command(CopyStyle);
    public RelayCommand PasteStyleCommand => _pasteStyle ??= Command(PasteStyle, () => State.StyleClipboard is not null);

    // ── Constructor ───────────────────────────────────────────────────────────
    /// <summary>
    /// Per-user state (zoom, grid, splittery, recent files, instance defaults).
    /// Null v testech / standalone bez Caché - get pro UI binding loaded files menu;
    /// service interní fallback na defaults i bez Caché.
    /// </summary>
    public TdPreferencesService? Preferences { get; }

    public TemplateDesignerViewModel(MLRest? mlRest = null,
                                     TdPreferencesService? preferences = null,
                                     IReadOnlyList<AgendaRegistryEntry>? agendas = null,
                                     string? startupAgendaCode = null)
    {
        if (mlRest is not null) _services.MLRest = new MLRestAdapter(mlRest);

        // Výměna šablony je jediný okamžik, kdy může přibýt font, který tenhle počítač nemá.
        // Psaní vlastního názvu do pole fontu se sem záměrně nenapojuje: nabídka jmenuje jen
        // nainstalované a záměna je vidět hned na plátně; před tiskem to dialog kontroluje znovu.
        State.TemplateSwapped = () =>
        {
            NastavHlaseni(ZdrojHlaseni.Font,
                FontWarnings.Hlaska(State.CurrentTemplate) is { } font ? [font] : []);
        };

        Preferences = preferences;

        var settings   = AppSettings.Load();
        var schemasDir = Path.Combine(AppContext.BaseDirectory, "schemas");
        _sectionTemplates = new SectionTemplateManager();

        var resolvedMlRest = mlRest ?? CacheSchemaWiring.TryCreateMlRest(settings);
        _cacheMlRest = resolvedMlRest;
        var resolvedAgendas = agendas ?? [];
        _agendaEntries = resolvedAgendas;
        CacheProvider = resolvedMlRest is not null && resolvedAgendas.Count > 0
            ? CacheSchemaWiring.TryCreate(resolvedMlRest, resolvedAgendas)
            : null;
        // Chyba čtení z Caché → nemodální hláška ve status baru (code-behind marshalne na UI thread).
        if (CacheProvider is not null)
            CacheProvider.OnReadError = msg => RequestReadErrorStatus?.Invoke(msg);
        TemplateStorage = resolvedMlRest is not null
            ? new Services.CacheTemplateService(resolvedMlRest)
            : null;

        SchemaProvider = CacheProvider is not null
            ? new CompositeDataSchemaProvider(CacheProvider, new JsonFileDataSchemaProvider(schemasDir))
            : (IDataSchemaProvider)new JsonFileDataSchemaProvider(schemasDir);

        State.CurrentTemplate = CreateSampleTemplate();

        // Startovní agenda - priorita: (1) EDISONMAIN2 kontrakt (inputParam "Agenda=<kód>" dle
        // uzlu stromu shellu / MHD2 tlačítka), (2) poslední použitá agenda uživatele (per-user
        // prefs), (3) fallback ze CreateSampleTemplate. Neznámý kód se přeskočí.
        string? ResolveKnown(string? code, bool jenZverejnene) => code is null ? null
            : resolvedAgendas.FirstOrDefault(a =>
                string.Equals(a.Code, code, StringComparison.OrdinalIgnoreCase)
                && (!jenZverejnene || a.Released))?.Code;
        // ⚠️ Poslední použitá agenda se obnoví jen když je ZVEŘEJNĚNÁ. Bez té podmínky se skrytá
        // agenda vrátila každému, kdo ji kdy vyzkoušel: uložila se do preferencí, po startu se
        // obnovila jako agenda šablony a výběr ji pak ukázal (tam se aktuální agenda zobrazuje
        // vždy, aby šly otevřít hotové šablony). Skrytí linkových JŘ se tím dalo obejít
        // pamětí aplikace - nález Jan 1. 9. při kontrole před vydáním.
        //
        // Kód z launch kontraktu shellu filtr NEmá: to je explicitní příkaz zvenčí a případná
        // skrytá agenda tam znamená chybu konfigurace shellu, ne zapamatovaný stav uživatele.
        var startAgenda = ResolveKnown(startupAgendaCode, jenZverejnene: false)
                          ?? ResolveKnown(preferences?.MainState?.LastAgendaCode, jenZverejnene: true);
        if (startAgenda is not null) State.CurrentTemplate.AgendaCode = startAgenda;

        RebuildSections();

        AiGuide = new Windows.AiGuideDialog.AiGuidePanelViewModel(() =>
        {
            var code = State.CurrentTemplate.AgendaCode;
            var name = code is null ? null
                : SchemaProvider.GetAvailableAgendas()
                      .FirstOrDefault(a => string.Equals(a.Code, code, StringComparison.OrdinalIgnoreCase))?.Name;
            var selType = State.SelectedElements.Count == 1
                ? State.SelectedElements[0].GetType().Name : null;
            return new Windows.AiGuideDialog.AiGuideContext(
                code, name, selType, State.CurrentTemplate.Sections.Count, CurrentAgendaElementKind);
        });

        // Aplikuj loaded MainState na State + VM properties. Volá se po inicializaci State,
        // aby loaded hodnoty přepsaly defaults z DesignerState ctoru.
        ApplyLoadedPreferences();
    }

    /// <summary>
    /// Aplikuje MainStatePref z <see cref="Preferences"/> na DesignerState + bindovatelné VM properties.
    /// Volá se v konstruktoru a znovu po dokončení LoadAsync (event Loaded) - pro cold-start scénář
    /// kdy LoadAsync nestihl dokončit před Show().
    /// </summary>
    public void ApplyLoadedPreferences()
    {
        var pref = Preferences?.MainState;
        if (pref is null) return;

        State.Zoom               = pref.Zoom;
        State.ShowGrid           = pref.ShowGrid;
        State.SnapToGrid         = pref.SnapToGrid;
        State.SnapToElements     = pref.SnapToElements;
        State.ShowSectionHeaders = pref.ShowSectionHeaders;
        State.ShowRuler          = pref.ShowRuler;
        State.GridSize           = pref.GridSize;
        State.SnapGapMm          = pref.SnapGapMm;
        ParamsBarExpanded        = pref.ParamsBarExpanded;

        // Property notifications pro bindings (toolbar toggle buttony)
        OnPropertyChanged(nameof(ShowGrid));
        OnPropertyChanged(nameof(SnapToGrid));
        OnPropertyChanged(nameof(SnapToElements));
        OnPropertyChanged(nameof(ShowSectionHeaders));
        OnPropertyChanged(nameof(ShowRuler));
        OnPropertyChanged(nameof(GridSize));
        OnPropertyChanged(nameof(SnapGapMm));
    }

    /// <summary>
    /// Snapshot aktuálního state z DesignerState do MainStatePref a uloží do Caché.
    /// Volá se z MainWindow.OnClosing. Splitter šířky a tab index doplňuje code-behind
    /// (VM nezná konkrétní WPF kontrolky).
    /// </summary>
    public Task SaveMainStateAsync(double? leftPanelPx, double? previewPanelPx, int activeTabIndex, bool showPreviewPane)
    {
        if (Preferences is null) return Task.CompletedTask;

        var snapshot = new MainStatePref(
            Zoom:               State.Zoom,
            ShowGrid:           State.ShowGrid,
            SnapToGrid:         State.SnapToGrid,
            SnapToElements:     State.SnapToElements,
            ShowSectionHeaders: State.ShowSectionHeaders,
            ShowRuler:          State.ShowRuler,
            GridSize:           State.GridSize,
            SnapGapMm:          State.SnapGapMm,
            ShowPreviewPane:    showPreviewPane,
            LeftPanelWidthPx:   leftPanelPx,
            PreviewPanelWidthPx: previewPanelPx,
            ActiveLeftTabIndex: activeTabIndex,
            LastAgendaCode:     State.CurrentTemplate.AgendaCode,
            ParamsBarExpanded:  ParamsBarExpanded);

        return Preferences.SaveMainStateAsync(snapshot);
    }

    // ── Execute wrapper ───────────────────────────────────────────────────────
    public void Execute(IDesignerCommand cmd)
    {
        State.Execute(cmd);
        IsDirty = true;
        NotifyUndoRedo();
        RequestPropertiesPanelUpdate?.Invoke();
        // Jediné místo, kde příkaz může pohnout definicí sloupců (dialog, přidání/smazání/vložení
        // prvku odjezdů) - definice se posílá do GetData, bez re-fetche by náhled ukazoval data
        // staré definice a tisk by vyjel jinak.
        RefetchIfColumnsDefinitionChanged();
    }

    /// <summary>Vykoná seznam příkazů jako jeden undo krok (1 příkaz → přímo, víc → CompositeCommand).
    /// Prázdný seznam = no-op.</summary>
    private void ExecuteAsComposite(string label, List<IDesignerCommand> cmds)
    {
        if (cmds.Count == 0) return;
        Execute(cmds.Count == 1 ? cmds[0] : new CompositeCommand(label, cmds));
    }

    // ── Aplikace výsledků editorů sloupců (dialog otevírá code-behind; sestavení
    //    a vykonání příkazů = business logika, patří sem do VM). Metody neznají WPF. ──

    /// <summary>Odjezdová mřížka: tab „Obecné" (EDInputForm) + per-column styly + styl sloupce hodin
    /// jako jeden undo krok.</summary>
    public void ApplyDepartureGridEdit(DepartureGridElement element, IDesignerCommand? generalCommand,
        IEnumerable<IDesignerCommand> styleCommands, List<DepartureGroupColumnStyle> resultGroupColumns,
        bool hasGroups, ColumnVisualStyle? resultHourColumnStyle,
        List<DepartureColumnDefinition>? resultCustomColumns)
    {
        var cmds = new List<IDesignerCommand>();
        if (generalCommand is not null) cmds.Add(generalCommand);
        cmds.AddRange(styleCommands);
        if (hasGroups)
            cmds.Add(new SetPropertyCommand("Skupiny dnů", element.GroupColumns, resultGroupColumns,
                v => element.GroupColumns = (List<DepartureGroupColumnStyle>)v!));
        cmds.Add(new SetPropertyCommand("Styl sloupce hodin", element.HourColumnStyle, resultHourColumnStyle,
            v => element.HourColumnStyle = (ColumnVisualStyle?)v));
        cmds.Add(new SetPropertyCommand("Vlastní sloupce", element.CustomColumns, resultCustomColumns,
            v => element.CustomColumns = (List<DepartureColumnDefinition>?)v));
        // Re-fetch i srovnání parametru rozvržení řeší Execute (RefetchIfColumnsDefinitionChanged).
        ExecuteAsComposite("Nastavení odjezdů", cmds);
    }

    /// <summary>Zvýraznění řádků (seznam pravidel) - jeden undoable krok. Prázdný seznam → null (čistý JSON).</summary>
    public void ApplyHighlightRulesEdit(IRowHighlightable element, List<HighlightRule> resultRules)
    {
        List<HighlightRule>? newVal = resultRules.Count > 0 ? resultRules : null;
        Execute(new SetPropertyCommand("Zvýraznění řádků", element.HighlightRules, newVal,
            v => element.HighlightRules = (List<HighlightRule>?)v));
    }

    /// <summary>Zastávkový StopList: tab „Obecné" + sloupce jako jeden undo krok.</summary>
    public void ApplyStopColumnsEdit(StopListElement element, IDesignerCommand? generalCommand, List<StopColumn> resultColumns)
    {
        var cmds = new List<IDesignerCommand>();
        if (generalCommand is not null) cmds.Add(generalCommand);
        cmds.Add(new SetPropertyCommand("Sloupce zastávek", element.Columns, resultColumns,
            v => element.Columns = (List<StopColumn>)v!));
        ExecuteAsComposite("Nastavení zastávek", cmds);
    }

    /// <summary>Linkový JŘ LineTable: tab „Obecné" + sloupce jako jeden undo krok.</summary>
    public void ApplyLineTableColumnsEdit(LineTableElement element, IDesignerCommand? generalCommand, List<StopColumn> resultColumns)
    {
        var cmds = new List<IDesignerCommand>();
        if (generalCommand is not null) cmds.Add(generalCommand);
        cmds.Add(new SetPropertyCommand("Sloupce linkového JŘ", element.Columns, resultColumns,
            v => element.Columns = (List<StopColumn>)v!));
        ExecuteAsComposite("Nastavení linkového JŘ", cmds);
    }

    /// <summary>Dynamická tabulka: tab „Obecné" + sloupce jako jeden undo krok.</summary>
    public void ApplyDynamicTableEdit(DynamicTableElement element, IDesignerCommand? generalCommand, List<TableColumn> resultColumns)
    {
        var cmds = new List<IDesignerCommand>();
        if (generalCommand is not null) cmds.Add(generalCommand);
        cmds.Add(new SetPropertyCommand("Sloupce tabulky", element.Columns, resultColumns,
            v => element.Columns = (List<TableColumn>)v!));
        ExecuteAsComposite("Nastavení tabulky", cmds);
    }

    /// <summary>Statická tabulka (Fixed/Dynamic): sloupce + řádky (Fixed) + souhrnné řádky jako jeden undo krok.</summary>

    private void Undo()
    {
        State.Undo();
        IsDirty = true;
        CleanupSelection();
        NotifyUndoRedo();
        RebuildSections();
        RequestCanvasRefresh?.Invoke();
        RequestPropertiesPanelUpdate?.Invoke();
        RefetchIfColumnsDefinitionChanged();
    }

    private void Redo()
    {
        State.Redo();
        IsDirty = true;
        CleanupSelection();
        NotifyUndoRedo();
        RebuildSections();
        RequestCanvasRefresh?.Invoke();
        RequestPropertiesPanelUpdate?.Invoke();
        RefetchIfColumnsDefinitionChanged();
    }

    /// <summary>Definice vlastních sloupců je jediný kus šablony, na kterém závisí DATA (posílá se
    /// do GetData) - po každé změně, která ji mohla pohnout (dialog sloupců, undo/redo), se musí
    /// znovu načíst, jinak náhled ukazuje sloupce staré definice.</summary>
    private string? _lastColumnsDefinitionSnapshot;

    /// <summary>Co ze šablony ovlivňuje **data** z `GetData`: definice sloupců a jazyk textů značek.
    /// Když se to pohne, náhled musí přenačíst - jinak ukazuje data staré definice / starý jazyk.</summary>
    private string CurrentWireSnapshot() =>
        ComposeWireSnapshot(CurrentColumnsDefinitionJson(), CurrentTurnusyKeSpojum(), JazykSablony);

    internal static string ComposeWireSnapshot(string? columnsDefinitionJson, bool turnusyKeSpojum,
        string jazyk) =>
        (columnsDefinitionJson ?? "") + "|" + (turnusyKeSpojum ? "T" : "-") + "|" + jazyk;

    private void RefetchIfColumnsDefinitionChanged()
    {
        // Definice se mohla pohnout jakkoli - dialogem, přidáním či smazáním prvku, vložením ze
        // schránky, undo/redo. Parametr rozvržení ji zrcadlí, takže se srovnává tady, na jediném
        // místě, které tyhle změny vidí všechny. (Bez toho zůstal po přidání prvku odjezdů na
        // „Automaticky", i když prvek dostal sloupce předvyplněné.)
        SyncLayoutParamFromTemplate();

        // Snapshot zapisuje až ÚSPĚŠNÝ fetch (RefreshDataForCurrentInstanceAsync) - kdyby se
        // zapsal tady a fetch spadl, další kontroly by se tvářily, že data definici odpovídají.
        if (CurrentWireSnapshot() == _lastColumnsDefinitionSnapshot) return;
        if (CurrentActiveInstance is not null) { _ = RefreshDataForCurrentInstanceAsync(keepPage: true); return; }

        // Bez vybrané instance běží náhled na ukázkových datech - ta se musí přenačíst taky, jinak
        // uživatel vidí staré rozvržení a myslí si, že vlastní sloupce nefungují (#13158).
        // Prefetch se sám vrátí, když už je vzorek pro tuhle definici v cache.
        _ = PrefetchMockDataAsync(State.CurrentTemplate.AgendaCode ?? "");
    }

    /// <summary>
    /// Odebere z výběru elementy, které už nejsou v žádné sekci šablony
    /// (např. po undo AddElement nebo undo Split).
    /// </summary>
    private void CleanupSelection()
    {
        var allElements = State.CurrentTemplate.Sections.SelectMany(s => s.Elements).ToHashSet();
        State.SelectedElements.RemoveAll(e => !allElements.Contains(e));
    }

    private void NotifyUndoRedo()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    // ── Sections ──────────────────────────────────────────────────────────────
    public void RebuildSections()
    {
        // Sync ambientního dokumentového defaultu (font…) na aktuální šablonu. RebuildSections se volá
        // po každém (re)loadu šablony → ambient vždy odpovídá aktuální šabloně (i reset na null u nové).
        DocumentDefaults.Style = State.CurrentTemplate.DefaultTextStyle;

        Sections.Clear();
        var template = State.CurrentTemplate;
        var stranky = DesignerPages.ObsahStranek(template);
        if (stranky.Count < 2)
        {
            foreach (var s in template.Sections)
                Sections.Add(new SectionVM(s, this));
        }
        else
        {
            // Šablona o víc stránkách (`B184`): nahoře společné sekce (hlavička, patička - patří
            // dokladu, jsou na každé předloze, kde se tisknou), pod nimi jen sekce otevřené
            // předlohy. Pořadí v šabloně se nemění, jen zobrazení.
            int otevrena = OtevrenaPredloha;
            bool prvni = true;
            foreach (var s in template.Sections.Where(DesignerPages.JeOpakovana))
            {
                Sections.Add(new SectionVM(s, this) { ZahlaviStranky = prvni ? "Společné (všechny stránky)" : null });
                prvni = false;
            }
            for (int j = 0; j < stranky[otevrena].Count; j++)
                Sections.Add(new SectionVM(stranky[otevrena][j], this)
                {
                    ZahlaviStranky = j == 0 ? stranky[otevrena][0].Name : null,
                    ZahlaviAktivni = j == 0,
                });
        }
        OnPredlohyZmeneny();
        SectionsRebuilt?.Invoke();
    }

    public void ActivateSection(Section? section)
    {
        State.ActiveSection = section;
        // Sekce z jiné předlohy (výběr ze stromu, z hlášky, ze seznamu) předlohu otevře - jinak by
        // byla vybraná sekce, která na plátně není. Společná sekce je na každé, nic se nepřepíná.
        if (section is not null && !DesignerPages.JeOpakovana(section))
        {
            int index = DesignerPages.IndexStranky(State.CurrentTemplate, section);
            if (index != OtevrenaPredloha) NastavPredlohu(index, zNahledu: false);
        }
        RebuildSections();
        RequestCanvasRefresh?.Invoke();
    }

    // ── Předlohy stránek (`B184`) ─────────────────────────────────────────────

    /// <summary>Otevřená předloha stránky, srovnaná do rozsahu (šablona se mohla zkrátit).</summary>
    public int OtevrenaPredloha =>
        Math.Clamp(State.Predloha, 0, DesignerPages.ObsahStranek(State.CurrentTemplate).Count - 1);

    public int  PocetPredloh => DesignerPages.ObsahStranek(State.CurrentTemplate).Count;

    /// <summary>Přepínač předloh je vidět jen u šablony o víc stránkách - jednostránková (jízdní
    /// řády) vypadá jako dřív (rozhodnutí Jan 25. 9.).</summary>
    public bool MaPredlohy => PocetPredloh > 1;

    /// <summary>Lišta nad náhledem je vidět, když je co přepínat: stránky dat nebo stránky šablony.</summary>
    public bool MaListuNadNahledem => HasPageNavigation || MaPredlohy;

    /// <summary>„Stránka 2 z 3". Jméno sekce ani „podle dat" se nepíše - dublovalo seznam sekcí
    /// a náhled a přepínač se nedal přečíst (nález Jan 28. 9.).</summary>
    public string PredlohaText => $"Stránka {OtevrenaPredloha + 1} z {PocetPredloh}";

    /// <summary>Náhled ukazuje všechny stránky šablony (jinak jen otevřenou).</summary>
    public bool NahledVsechStranek
    {
        get => State.NahledVsechStranek;
        set
        {
            if (State.NahledVsechStranek == value) return;
            State.NahledVsechStranek = value;
            OnPropertyChanged();
            PredlohaZmenena?.Invoke(OtevrenaPredloha, false);
        }
    }

    /// <summary>Předloha se přepnula; true = přepnul ji posun náhledu (náhled už se neposouvá).</summary>
    public Action<int, bool>? PredlohaZmenena { get; set; }

    private RelayCommand? _predchoziPredloha, _dalsiPredloha;
    public RelayCommand PredchoziPredlohaCommand => _predchoziPredloha ??= Command(
        () => OtevriPredlohu(OtevrenaPredloha - 1), () => OtevrenaPredloha > 0);
    public RelayCommand DalsiPredlohaCommand => _dalsiPredloha ??= Command(
        () => OtevriPredlohu(OtevrenaPredloha + 1), () => OtevrenaPredloha < PocetPredloh - 1);

    /// <summary>
    /// Otevře předlohu na plátně: vybere se její první sekce (tam jde „Přidat sekci") a zruší se
    /// výběr prvků, které na ní nejsou.
    /// </summary>
    public void OtevriPredlohu(int index, bool zNahledu = false)
    {
        index = Math.Clamp(index, 0, PocetPredloh - 1);
        if (index == OtevrenaPredloha && State.Predloha == index) return;
        NastavPredlohu(index, zNahledu);
        var naPredloze = DesignerPages.SekceStranky(State.CurrentTemplate, index);
        if (State.ActiveSection is null || !naPredloze.Contains(State.ActiveSection))
            State.ActiveSection = DesignerPages.ObsahStranek(State.CurrentTemplate)[index].FirstOrDefault();
        State.SelectedElements.RemoveAll(el => !naPredloze.Any(sekce => sekce.Elements.Contains(el)));
        RebuildSections();
        RequestCanvasRefresh?.Invoke();
    }

    private void NastavPredlohu(int index, bool zNahledu)
    {
        State.Predloha = index;
        OnPredlohyZmeneny();
        RequestCanvasSizeUpdate?.Invoke();
        PredlohaZmenena?.Invoke(index, zNahledu);
    }

    private void OnPredlohyZmeneny()
    {
        OnPropertyChanged(nameof(MaPredlohy));
        OnPropertyChanged(nameof(MaListuNadNahledem));
        OnPropertyChanged(nameof(HasPocetStranSazby));
        OnPropertyChanged(nameof(PredlohaText));
        CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>
    /// Přidá sekci a uvolní pro ni místo: nejdřív z volného zbytku stránky, teprve pak zkrácením
    /// ostatních sekcí. Vratné jedním krokem zpět.
    /// </summary>
    /// <remarks>
    /// ⚠️ Výpočet drží <see cref="SectionLayoutMath.RozdelMistoProNovouSekci"/> - sdílí dno
    /// (obsah sekce) s tažením hrany i s dialogem výšky. Do 10. 9. tahle cesta krátila vždycky
    /// a měla vlastní dno na konstantě 5 mm, takže jako jediná uměla zmáčknout sekci pod její
    /// obsah, i když byla stránka poloprázdná (`B129`).
    /// </remarks>
    public void AddSection(Section section)
    {
        var commands = new List<IDesignerCommand>();
        var template = State.CurrentTemplate;

        // Stránky, na které nová sekce padne (`B184`): obsah na konec aktivní stránky, hlavička
        // a patička na všechny stránky, kde se tisknou - místo musí najít na každé z nich.
        int aktivni = OtevrenaPredloha;
        bool opakovana = DesignerPages.JeOpakovana(section);
        int stranek = DesignerPages.ObsahStranek(template).Count;
        var dotcene = opakovana
            ? Enumerable.Range(0, stranek).Where(i => DesignerPages.TiskneSeNaStrance(section, i, stranek))
                .Select(i => DesignerPages.SekceStranky(template, i)).ToList()
            : [DesignerPages.SekceStranky(template, aktivni)];
        if (dotcene.Count == 0) dotcene.Add(DesignerPages.SekceStranky(template, aktivni));

        float pozadovana = section.DesignerHeight ?? 30f;
        // Výška, která se vejde na všechny dotčené stránky; teprve s ní se počítá, co zkrátit.
        float vyska = dotcene.Min(p => SectionLayoutMath.RozdelMistoProNovouSekci(template, pozadovana, p).VyskaNoveSekce);
        var zkraceni = new Dictionary<Section, float>();
        foreach (var p in dotcene)
            foreach (var (sekce, novaVyska) in SectionLayoutMath.RozdelMistoProNovouSekci(template, vyska, p).Zkraceni)
                zkraceni[sekce] = zkraceni.TryGetValue(sekce, out var dosavadni) ? MathF.Min(dosavadni, novaVyska) : novaVyska;
        var misto = new SectionLayoutMath.MistoProNovouSekci(vyska,
            [.. zkraceni.Select(z => new SectionLayoutMath.ZkraceniSekce(z.Key, z.Value))]);

        foreach (var (sekce, novaVyska) in misto.Zkraceni)
        {
            float stara = sekce.DesignerHeight!.Value;
            commands.Add(new SetPropertyCommand(
                $"Výška sekce {sekce.Name}", stara, novaVyska,
                v => sekce.DesignerHeight = (float)v!));
        }

        // Sekci s dynamickou výškou (`DesignerHeight == null`) se výška nevnucuje - tu počítá
        // engine z obsahu.
        if (section.DesignerHeight is not null)
            section.DesignerHeight = misto.VyskaNoveSekce;

        commands.Add(new AddSectionCommand(template, section,
            opakovana ? null : DesignerPages.PoziceNaKonciStranky(template, aktivni)));

        if (commands.Count > 1)
            Execute(new CompositeCommand("Přidat sekci", commands));
        else
            Execute(commands[0]);

        ActivateSection(section);
        RequestCanvasSizeUpdate?.Invoke();

        if (section.DesignerHeight is not null
            && MathF.Abs(misto.VyskaNoveSekce - pozadovana) > SectionLayoutMath.TolerancePrekryvuMm)
        {
            StatusText = $"Sekce {section.Name} přidána s výškou {misto.VyskaNoveSekce:0.#} mm "
                       + "- na víc už na stránce nebylo místo. Zkraťte jinou sekci, jestli má být vyšší.";
        }
    }

    /// <summary>
    /// Přidá stránku dokladu: sekci se „Začít na nové stránce", která zabere celou volnou plochu
    /// nové stránky (bez hlavičky a patičky, které se na ni tisknou). Ostatní sekce se nemění.
    /// </summary>
    /// <remarks>
    /// Nejde přes <see cref="AddSection"/> - ten hledá místo na dosavadní poslední stránce a kvůli
    /// nové by zkrátil její sekce. Nová stránka je prázdná, takže místo krátit nemusí.
    /// </remarks>
    public void AddPage()
    {
        var template = State.CurrentTemplate;
        int stranek = DesignerPages.ObsahStranek(template).Count;
        bool prvniJePrazdna = stranek == 1 && DesignerPages.ObsahStranek(template)[0].Count == 0;
        int nova = prvniJePrazdna ? 0 : stranek;
        int pocet = nova + 1;
        float opakovane = template.Sections
            .Where(s => DesignerPages.JeOpakovana(s) && DesignerPages.TiskneSeNaStrance(s, nova, pocet))
            .Sum(s => s.EffectiveDesignerHeight);

        var section = new Section
        {
            Name = $"Strana {nova + 1}",
            PrintBehavior = SectionPrintBehavior.Once,
            OverflowBehavior = SectionOverflowBehavior.KeepTogether,
            ZacitNaNoveStrance = !prvniJePrazdna,
            DesignerHeight = MathF.Max(SectionLayoutMath.MinimalniVyskaNoveSekceMm,
                template.PageSize.PrintableHeight - opakovane),
        };
        Execute(new AddSectionCommand(template, section));
        ActivateSection(section);   // otevře novou předlohu
        RequestCanvasSizeUpdate?.Invoke();
    }

    /// <summary>
    /// Roztáhne sekci na celé zbývající místo stránky. Vratné krokem zpět.
    /// </summary>
    /// <remarks>
    /// Sekce nad ní se nehne, sekce pod ní se **posune a podrží svou výšku**; prvky jdou se svou
    /// sekcí samy, protože jejich souřadnice jsou k ní relativní (rozhodnutí Jan 3. 9.).
    ///
    /// <para>⚠️ Je to **akce v místní nabídce**, ne pole ve formuláři sekce - formulář sekce přichází
    /// z Caché (`SectionEdit.cls`), takže tlačítko přidané do C# descriptorů se vůbec nezobrazí.
    /// Do 3. 9. tam bylo právě takhle položené a nešlo na něj kliknout.</para>
    /// </remarks>
    public void VyplnitZbytekStranky(Section section)
    {
        if (section.DesignerHeight is not { } stara) return;   // dynamická výška roste sama

        float nova = SectionLayoutMath.ZbyvajiciMistoMm(State.CurrentTemplate, section);
        if (MathF.Abs(nova - stara) < 0.05f) return;           // ať krok zpět nesbírá prázdné změny

        Execute(new SetPropertyCommand("výška sekce", stara, nova,
            v => section.DesignerHeight = (float?)v));
        RebuildSections();
        RequestCanvasSizeUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
        RequestPreviewRefresh?.Invoke();
    }

    /// <summary>
    /// Srazí ručně zadanou výšku sekce do týchže mezí, jaké platí pro tažení a pro „Vyplnit zbytek
    /// stránky". Vrací true, když hodnotu opravdu změnil.
    /// </summary>
    /// <remarks>
    /// ⚠ Formulář sekce přichází z Caché (`SectionEdit.cls`) a **nezná ostatní sekce šablony**,
    /// takže má statický rozsah 5-9999 mm. Bez tohohle sražení šla mez obejít cestou, kterou
    /// uživatel zkusí jako první: tažením sekci pod obsah nezmenšil, ale číslem ano - a prvek se
    /// pak v návrháři oříznul, přestože se na papíře vytiskl celý (nález Jan 4. 9.).
    ///
    /// <para>Korekce se zapisuje **mimo příkaz** - krok zpět vrátí hodnotu z dialogu, takže se stav
    /// nerozejde. Vratná zvlášť by znamenala mačkat zpět dvakrát.</para>
    /// </remarks>
    public bool SrazVyskuSekceDoMezi(Section section)
    {
        if (section.DesignerHeight is not { } zadana) return false;   // dynamická výška roste sama

        float dno     = SectionLayoutMath.MinimalniVyskaProSekci(section);
        float strop   = SectionLayoutMath.ZbyvajiciMistoMm(State.CurrentTemplate, section);
        float srazena = Math.Clamp(zadana, dno, MathF.Max(dno, strop));
        if (srazena == zadana) return false;

        section.DesignerHeight = srazena;
        // Setinové smetí srazit mlčky - hláška o „zmenšení na 21,9 mm" u rozdílu 0,02 mm nic
        // neříká. Srazit se ale musí vždy, jinak smetí zůstane v šabloně a engine ho pak řeší
        // tolerancí (`SectionLayoutMath.TolerancePrekryvuMm`) na každé stránce znovu.
        if (MathF.Abs(srazena - zadana) < SectionLayoutMath.TolerancePrekryvuMm) return true;

        StatusText = srazena > zadana
            ? $"Výška sekce {section.Name} zvětšena na {srazena:0.#} mm - menší by uřízla její obsah"
            : $"Výška sekce {section.Name} zmenšena na {srazena:0.#} mm - větší by nevešla na stránku";
        return true;
    }

    public void MoveSection(Section section, int direction)
    {
        int idx    = State.CurrentTemplate.Sections.IndexOf(section);
        int newIdx = idx + direction;
        if (newIdx < 0 || newIdx >= State.CurrentTemplate.Sections.Count) return;

        Execute(new MoveSectionCommand(State.CurrentTemplate, section, direction));
        RebuildSections();
        RequestCanvasSizeUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    public void ReorderSection(Section section, int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex) return;
        if (fromIndex < 0 || toIndex < 0) return;
        if (toIndex >= State.CurrentTemplate.Sections.Count) return;

        Execute(new ReorderSectionCommand(State.CurrentTemplate, section, fromIndex, toIndex));
        RebuildSections();
        RequestCanvasSizeUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    public void DeleteSection(Section section)
    {
        if (State.CurrentTemplate.Sections.Count <= 1)
        {
            PopupDisplayer.Info("Šablona musí mít alespoň jednu sekci.");
            return;
        }

        int elementCount = section.Elements.Count;
        if (elementCount > 0)
        {
            if (!PopupDisplayer.Confirm($"Opravdu smazat sekci „{section.Name}“ s {elementCount} elementy?"))
                return;
        }

        State.SelectedElements.RemoveAll(el => section.Elements.Contains(el));

        if (State.ActiveSection == section)
            State.ActiveSection = State.CurrentTemplate.Sections.FirstOrDefault(s => s != section);

        var commands = new List<IDesignerCommand>();
        commands.Add(new DeleteSectionCommand(State.CurrentTemplate, section));

        // Redistribuovat uvolněnou výšku mezi zbývající fixed-height sekce
        float freed = section.DesignerHeight ?? 0f;
        var remaining = State.CurrentTemplate.Sections
            .Where(s => s != section && s.DesignerHeight.HasValue)
            .ToList();

        if (freed > 0 && remaining.Count > 0)
        {
            float totalRemaining = remaining.Sum(s => s.DesignerHeight!.Value);
            foreach (var s in remaining)
            {
                float oldH = s.DesignerHeight!.Value;
                float share = totalRemaining > 0 ? oldH / totalRemaining * freed : freed / remaining.Count;
                float newH = oldH + share;
                if (Math.Abs(newH - oldH) > 0.01f)
                    commands.Add(new SetPropertyCommand(
                        $"Výška sekce {s.Name}", oldH, newH,
                        v => s.DesignerHeight = (float)v!));
            }
        }

        Execute(commands.Count == 1 ? commands[0] : new CompositeCommand("Smazat sekci", commands));
        RebuildSections();
        RequestCanvasSizeUpdate?.Invoke();
        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    public void SaveSectionTemplate(Section section, string name)
    {
        try
        {
            _sectionTemplates.Save(section, name);
            PopupDisplayer.Info($"Šablona „{name}“ byla uložena.");
        }
        catch (Exception ex)
        {
            PopupDisplayer.Error(ex);
        }
    }

    public IReadOnlyList<string> GetCustomSectionTemplateNames() =>
        _sectionTemplates.GetAvailableTemplates();

    public Section? LoadCustomSectionTemplate(string name) =>
        _sectionTemplates.Load(name)?.Clone();

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
        // Výběr z pickeru = táž šablona, jiné linky → nechat stará data do dosednutí (bez mock bliku).
        OnActiveInstancesChanged(reloadData: true, clearRealData: false);
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
        var visible = _agendaEntries
            .Where(a => a.Released || a.Code.Equals(current, StringComparison.OrdinalIgnoreCase))
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
    /// </summary>
    public async Task OpenTemplateFromFileAsync(string filePath)
    {
        try
        {
            var oldAgenda = State.CurrentTemplate.AgendaCode;
            State.CurrentTemplate = await TemplateSerializer.LoadAsync(filePath);
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
        LoadGeneratedTemplate(template);
        // Hodnoty nastavení sestavy, které vznikly při importu z předlohy, se musí projevit
        // i v ovladačích - jinak se tiskne něco jiného, než co je v nastavení vidět (viz
        // ApplyPersistedPrintParamsAsync). Import do 19. 8. touhle cestou vůbec neprošel.
        await RefreshFieldsList();
        await ApplyPersistedPrintParamsAsync();
    }

    public void LoadGeneratedTemplate(Template template)
    {
        State.CurrentTemplate = template;
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
            var oldAgenda = State.CurrentTemplate.AgendaCode;
            // Systémová (M-line) šablona se čte z MLINE větve; jinak z firmy (default).
            var sourceIdFa = vm.SelectedIsSystem ? Services.CacheTemplateService.MlineIdFa : null;
            var (_, template) = await TemplateStorage.LoadAsync(vm.SelectedTemplateId.Value, sourceIdFa);
            State.CurrentTemplate = template;
            // Pokud nová šablona má jinou agendu, vyčistit aktivní instance - jejich
            // IDs patří staré agendě a v nové by GetData padl.
            if (!string.Equals(oldAgenda, State.CurrentTemplate.AgendaCode, StringComparison.OrdinalIgnoreCase))
                ClearActiveInstances();
            CurrentFilePath = null;
            // Systémová = read-only originál → otevřít jako „kopie": Ctrl+S vynutí „Uložit jako"
            // do firmy (nový GUID). Vlastní firma šablona → normální update.
            _loadedFromCache = !vm.SelectedIsSystem;
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
            NotifyMigrationIfNeeded();
            RequestTitleUpdate?.Invoke();
            RequestCanvasSizeUpdate?.Invoke();
            RequestPropertiesPanelUpdate?.Invoke();
        }
        catch (CacheTemplateException ex) { PopupDisplayer.Error(ex); }
    }

    /// <summary>Uloží do DB (nová/z disku → Save As dialog). Vrátí true při úspěchu, false při
    /// zrušení (Save As dialog) nebo chybě - volající (confirm neuložených změn) podle toho pokračuje.</summary>
    private async Task<bool> SaveToCacheAsync()
    {
        if (TemplateStorage is null) return false;
        // Pokud šablona ještě v Caché není (z disku nebo nová) → spustit Save As
        if (!_loadedFromCache) return await SaveToCacheAsAsync();

        SyncParametersToTemplate();
        CaptureEditorPreferencesToTemplate();
        try
        {
            await TemplateStorage.SaveAsync(State.CurrentTemplate);
            IsDirty = false;
            RequestTitleUpdate?.Invoke();
            PopupDisplayer.Info($"Šablona „{State.CurrentTemplate.Name}\" byla uložena do databáze.");
            return true;
        }
        catch (CacheTemplateException ex) { PopupDisplayer.Error(ex); return false; }
    }

    private async Task<bool> SaveToCacheAsAsync()
    {
        if (TemplateStorage is null) return false;

        var vm = new SaveToCacheDialogViewModel(
            State.CurrentTemplate.Name,
            State.CurrentTemplate.AgendaCode);
        if (Dialogs?.ShowSaveToCache(vm) != true) return false;

        // Kolize názvu: bez kontroly by vznikla tichá duplicita (dvě šablony stejného jména,
        // v přehledu nerozlišitelné). Best-effort - když list selže, uložení neblokujeme.
        try
        {
            var existing = await TemplateStorage.ListAsync(vm.AgendaCode);
            var clash = existing.FirstOrDefault(t =>
                string.Equals(t.Name, vm.Name, StringComparison.OrdinalIgnoreCase)
                && t.TemplateId != State.CurrentTemplate.Id);
            if (clash is not null && !PopupDisplayer.Confirm(
                    $"Šablona „{vm.Name}\" už v databázi existuje (upravena {clash.ModifiedAt}).\n" +
                    "Uložit i tak jako další šablonu se stejným názvem?"))
                return false;
        }
        catch (CacheTemplateException) { /* kontrola je best-effort */ }

        // Pro "Save As" → nový TemplateID, aby nepřepsal originál v Caché
        State.CurrentTemplate.Id = Guid.NewGuid();
        State.CurrentTemplate.Name = vm.Name;
        State.CurrentTemplate.AgendaCode = vm.AgendaCode;

        SyncParametersToTemplate();
        CaptureEditorPreferencesToTemplate();
        try
        {
            await TemplateStorage.SaveAsync(State.CurrentTemplate);
            _loadedFromCache = true;
            CurrentFilePath = null;
            IsDirty = false;
            RequestTitleUpdate?.Invoke();
            PopupDisplayer.Info($"Šablona „{State.CurrentTemplate.Name}\" byla uložena do databáze.");
            return true;
        }
        catch (CacheTemplateException ex) { PopupDisplayer.Error(ex); return false; }
    }

    /// <summary>
    /// Zapíše do šablony definici parametrů a hodnoty, které uživatel nastavil.
    /// </summary>
    /// <remarks>
    /// ⚠️ **Hodnota, kterou server právě nenabízí, se v šabloně NECHÁVÁ** (`B156`, hlásil TRANSDEV
    /// 16. 9. jako „po aktualizaci se mi nastavení resetují"). Do 16. 9. se seznam stavěl od nuly
    /// jen z aktuálních parametrů, takže cokoli mimo ně z šablony tiše zmizelo - a při prázdném
    /// seznamu se smazalo všechno.
    /// <para>Nešlo o teoretický okraj: volba „Písmena variant u odjezdů" se 17. 8. zrušila
    /// (`B90`) a 11. 9. vrátila pod týmž klíčem (`B141`). Kdo mezitím šablonu uložil, přišel
    /// o svou hodnotu a po návratu volby dostal výchozí stav. Stačí k tomu i výpadek serveru
    /// v okamžiku ukládání, dočasně skrytá agenda nebo starší `.cls` na serveru.</para>
    /// <para>Cena je mrtvý klíč po parametru, který se už nikdy nevrátí - ten nikomu nevadí,
    /// protože se čte jen podle aktuálního seznamu (<see cref="ApplyTemplatePrintParamsToPreview"/>).</para>
    /// </remarks>
    private void SyncParametersToTemplate()
    {
        // Definice parametrů se drží jen když ji server vrátil - je to popis nabídky, ne data.
        if (_agendaParameters.Count > 0)
            State.CurrentTemplate.Parameters = [.. _agendaParameters];

        // Hodnoty obsahových tiskových parametrů patří šabloně (sdílené mezi uživateli, mají přednost
        // před per-agenda). Iterujeme jen _agendaParameters → export klíče (název/členění/imposice)
        // se sem záměrně nedostanou (nejsou mezi agenda parametry).
        var nove = new Dictionary<string, string>();
        foreach (var p in _agendaParameters)
            if (_previewParams.TryGetValue(p.Name, out var v))
                nove[p.Name] = ParamToFieldString(v, p.Type);

        State.CurrentTemplate.PrintParameters =
            SlucHodnotyParametru(State.CurrentTemplate.PrintParameters, nove);
    }

    /// <summary>
    /// Nové hodnoty přepíšou staré, **zbytek zůstane**. Vrací null, když by výsledek byl prázdný.
    /// </summary>
    /// <remarks>
    /// Jádro opravy `B156`: klíč, který server právě nenabízí, se nesmí zahodit. Do 16. 9. se
    /// výsledek stavěl jen z nových hodnot, takže dočasně odebraný parametr (nebo výpadek serveru
    /// při ukládání) uživateli tiše smazal uložené nastavení.
    /// </remarks>
    internal static Dictionary<string, string>? SlucHodnotyParametru(
        IReadOnlyDictionary<string, string>? stare, IReadOnlyDictionary<string, string> nove)
    {
        var vysledek = stare is { Count: > 0 } ? new Dictionary<string, string>(stare) : [];
        foreach (var (klic, hodnota) in nove) vysledek[klic] = hodnota;
        return vysledek.Count > 0 ? vysledek : null;
    }

    /// <summary>True během běžícího exportu - blokuje druhý souběžný export (PDF i PNG),
    /// jinak uživatel může dál normálně pracovat (export renderuje snapshot šablony).</summary>
    private bool _exportRunning;

    private async Task ExportPreviewPdfAsync()
    {
        var defaultName = (Path.GetFileNameWithoutExtension(CurrentFilePath)
                         ?? (string.IsNullOrWhiteSpace(State.CurrentTemplate.Name)
                             ? "sablona" : State.CurrentTemplate.Name)) + ".pdf";
        var path = Dialogs?.PickSaveFile("Exportovat náhled jako PDF", "PDF soubory (*.pdf)|*.pdf", defaultName, "pdf");
        if (path is null) return;
        if (RenderPdfToFile is null) return;

        // Imposice ze sdílených exportních polí (persistované per-agenda, editují se v PrintDialogu) -
        // zachytit na UI threadu spolu se snapshotem. Chybná mřížka = uživatelská konfigurace → status bar.
        SheetLayoutOptions? sheets;
        try { sheets = SheetComposer.FromParamValues(PreviewParamValues, State.CurrentTemplate.PageSize); }
        catch (ArgumentException ex) { StatusText = $"⚠ {ex.Message}"; return; }

        // Měřítko tisku se drží ve stejných exportních polích jako imposice (editují se
        // v tiskovém dialogu, persistují per agenda) - export z hlavního okna ho musí ctít taky,
        // jinak dá jiný výstup než Ctrl+P.
        float meritko = SheetComposer.MeritkoZParamValues(PreviewParamValues);

        // Fetch (N× GetData přes MLRest) i render běží mimo UI thread (R1). UI zůstává
        // plně interaktivní - render jede nad SNAPSHOTEM šablony (stav v okamžiku kliknutí),
        // takže editace během exportu výstup neovlivní. Progress jde do status baru.
        _exportRunning = true;
        try
        {
            var snapshot = TemplateSerializer.DeepClone(State.CurrentTemplate);
            var fetchProgress = new Progress<string>(s => StatusText = s);
            var pages = await BuildAllPreviewDataPagesAsync(fetchProgress);

            var renderProgress = new Progress<(int done, int total)>(p =>
                StatusText = $"Generuji PDF: {p.done}/{p.total} datových stránek…");
            await Task.Run(() => RenderPdfToFile(path, pages, snapshot, renderProgress, sheets, meritko));

            var ps = State.CurrentTemplate.PageSize;
            StatusText = $"PDF uloženo: {Path.GetFileName(path)} ({pages.Count} datových stránek)"
                       + (sheets is null ? "" : $" · {sheets.ShortLabel}")
                       + (Math.Abs(meritko - 1f) < 0.001f ? ""
                          : $" · měřítko {meritko:0.##}× ({ps.Width * meritko:0.#}×{ps.Height * meritko:0.#} mm)");
            OpenExportedFile(path);
        }
        finally { _exportRunning = false; }
    }

    /// <summary>View vrstva (code-behind) napojí WPF implementaci - modální/souborové dialogy + schránka.
    /// Přes tuto abstrakci VM nereferencuje WPF (R4). Null v testech → dialogové akce se tiše přeskočí.</summary>
    public IDesignerDialogService? Dialogs { get; set; }

    /// <summary>Code-behind napojí na PreviewCanvas.RenderMultiDataToPdf. Voláno z background threadu
    /// se snapshot šablonou (živá se může mezitím editovat). Poslední parametr = imposice (null = vypnutá).</summary>
    public Action<string, IReadOnlyList<DataContext>, Template, IProgress<(int done, int total)>, SheetLayoutOptions?, float>? RenderPdfToFile { get; set; }

    private async Task ExportPreviewPngAsync()
    {
        var defaultName = (Path.GetFileNameWithoutExtension(CurrentFilePath)
                         ?? (string.IsNullOrWhiteSpace(State.CurrentTemplate.Name)
                             ? "sablona" : State.CurrentTemplate.Name)) + ".png";
        var path = Dialogs?.PickSaveFile("Exportovat náhled jako PNG", "PNG soubory (*.png)|*.png", defaultName, "png");
        if (path is null) return;
        if (RenderPngToFile is null) return;

        _exportRunning = true;
        try
        {
            var snapshot = TemplateSerializer.DeepClone(State.CurrentTemplate);
            var fetchProgress = new Progress<string>(s => StatusText = s);
            var pages = await BuildAllPreviewDataPagesAsync(fetchProgress);

            var renderProgress = new Progress<(int done, int total)>(p =>
                StatusText = $"Generuji PNG: {p.done}/{p.total} stránek…");
            int count = await Task.Run(() => RenderPngToFile(path, pages, snapshot, renderProgress));

            StatusText = count == 1
                ? $"PNG uloženo: {Path.GetFileName(path)}"
                : $"PNG uloženo: {count} souborů ({Path.GetFileNameWithoutExtension(path)}_001.png …)";
            if (count == 1) OpenExportedFile(path);
        }
        finally { _exportRunning = false; }
    }

    /// <summary>Code-behind napojí na PreviewCanvas.RenderMultiDataToPng. Voláno z background threadu
    /// se snapshot šablonou (živá se může mezitím editovat).</summary>
    public Func<string, IReadOnlyList<DataContext>, Template, IProgress<(int done, int total)>, int>? RenderPngToFile { get; set; }

    private static void OpenExportedFile(string filePath)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(filePath) { UseShellExecute = true }); }
        catch { /* viewer nemusí být nainstalován */ }
    }

    /// <summary>Potvrzení neuložených změn před destruktivní akcí (Nový/Otevřít/změna agendy/zavření).
    /// Ano → uloží do DB (stejná cesta jako Ctrl+S), Ne → pokračovat bez uložení, Storno → zrušit.
    /// Async, protože uložení do DB je async (MLRest). Vrátí true = lze pokračovat.</summary>
    public async Task<bool> ConfirmUnsavedChangesAsync()
    {
        if (!IsDirty) return true;

        var result = PopupDisplayer.ConfirmYesNoCancel(
            "Šablona obsahuje neuložené změny. Uložit je?",
            title: "Neuložené změny");

        return result switch
        {
            true  => await SaveAndContinueAsync(),  // Ano = uložit (do DB) a pokračovat
            false => true,                           // Ne = pokračovat bez uložení (zahodit změny)
            null  => false                           // Storno = zrušit
        };
    }

    /// <summary>Uloží do DB (jako Ctrl+S), ne do JSON souboru - DB je primární úložiště. Bez DB →
    /// popup s nabídkou JSON exportu (U5). Vrátí true pokud se reálně uložilo (jinak nepokračovat).</summary>
    private async Task<bool> SaveAndContinueAsync()
    {
        if (TemplateStorage is null) return await OfferJsonExportFallbackAsync();
        return await SaveToCacheAsync();
    }

    // ── Clipboard ─────────────────────────────────────────────────────────────
    public void CopySelected()
    {
        if (State.SelectedElements.Count == 0) return;
        _clipboard = State.SelectedElements.Select(el => el.Clone()).ToList();
    }

    public void CutSelected()
    {
        if (State.SelectedElements.Count == 0) return;
        CopySelected();
        DeleteSelected();
    }

    public void SelectAll()
    {
        if (State.ActiveSection is null) return;
        State.SelectedElements.Clear();
        State.SelectedElements.AddRange(State.ActiveSection.Elements);
    }

    public void PasteClipboard()
    {
        if (_clipboard.Count == 0) return;
        if (State.ActiveSection is null)
        {
            PopupDisplayer.Info("Nejprve vyberte sekci.");
            return;
        }

        // Mapování starý GroupId → nový GroupId (klony nedědí skupinu originálu)
        var groupIdMap = new Dictionary<string, string>();
        var newElements = _clipboard.Select(el =>
        {
            var clone = el.Clone();
            clone.X = Math.Min(clone.X + 5f,
                State.CurrentTemplate.PageSize.PrintableWidth - clone.Width);
            clone.Y = Math.Min(clone.Y + 5f,
                State.ActiveSection.EffectiveDesignerHeight - clone.EffectiveDesignerHeight);
            RemapGroupId(clone, groupIdMap);
            return clone;
        }).ToList();

        var commands = newElements
            .Select(el => (IDesignerCommand)new AddElementCommand(State.ActiveSection, el))
            .ToList();

        if (commands.Count == 1) Execute(commands[0]);
        else Execute(new CompositeCommand("Vložit elementy", commands));

        State.SelectedElements.Clear();
        State.SelectedElements.AddRange(newElements);
        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    // ── Style clipboard ──────────────────────────────────────────────────────

    public void CopyStyle()
    {
        var el = State.SelectedElements.FirstOrDefault();
        if (el is null) return;
        var textStyle = el is TextElement te ? te.Style.Clone() : null;
        State.StyleClipboard = new StyleClipboard(textStyle, el.BorderColor, el.BorderWidth, el.BackgroundColor);
    }

    /// <summary>
    /// Smaže font nastavený u jednotlivých prvků, aby převzaly výchozí font dokumentu (`B153`).
    /// </summary>
    /// <remarks>
    /// Šablony založené do 30. 6. 2026 - a jejich kopie - mají font zapsaný u každého prvku, takže
    /// v nich volba „Výchozí font" nic nedělá. Tohle je jediná cesta, jak je narovnat; dodávané
    /// šablony jsou uklizené od 14. 9. Vratné přes Ctrl+Z, proto se na nic neptá.
    /// </remarks>
    public void PrevzitVychoziFont()
    {
        var prvky = State.CurrentTemplate.Sections.SelectMany(s => s.Elements).ToList();
        var prikaz = new PrevzitVychoziFontCommand(prvky);
        Execute(prikaz);

        StatusText = prikaz.PocetZmenenych == 0
            ? "Žádný prvek neměl vlastní font - výchozí font dokumentu platí všude."
            : $"Výchozí font dokumentu převzalo {prikaz.PocetZmenenych}× nastavení u prvků.";
        RebuildSections();
        InvalidateAll();
    }

    public void PasteStyle()
    {
        var clip = State.StyleClipboard;
        if (clip is null || State.SelectedElements.Count == 0) return;

        var commands = new List<IDesignerCommand>();
        foreach (var el in State.SelectedElements)
        {
            if (el.BorderColor != clip.BorderColor)
                commands.Add(new SetPropertyCommand("Barva rámečku", el.BorderColor, clip.BorderColor,
                    v => { el.BorderColor = (string)v!; }));
            if (MathF.Abs(el.BorderWidth - clip.BorderWidth) > 0.001f)
                commands.Add(new SetPropertyCommand("Tloušťka rámečku", el.BorderWidth, clip.BorderWidth,
                    v => { el.BorderWidth = (float)v!; }));
            if (el.BackgroundColor != clip.BackgroundColor)
                commands.Add(new SetPropertyCommand("Barva pozadí", el.BackgroundColor, clip.BackgroundColor,
                    v => { el.BackgroundColor = (string)v!; }));

            if (clip.TextStyle is not null && el is TextElement te)
            {
                var oldStyle = te.Style;
                var newStyle = clip.TextStyle.Clone();
                commands.Add(new SetPropertyCommand("Styl textu", oldStyle, newStyle,
                    v => { te.Style = (TextStyle)v!; }));
            }
        }

        if (commands.Count == 0) return;
        Execute(new CompositeCommand("Vložit styl", commands));
        RequestCanvasRefresh?.Invoke();
    }

    public void DuplicateSelected() => DuplicateSelected(direction: null);

    public void DuplicateSelected(DuplicateDirection? direction)
    {
        if (State.SelectedElements.Count == 0 || State.ActiveSection is null) return;

        float sectionW = State.CurrentTemplate.PageSize.PrintableWidth;
        float sectionH = State.ActiveSection.EffectiveDesignerHeight;
        float gap = State.SnapGapMm;

        var newElements = new List<TemplateElement>();
        var commands = new List<IDesignerCommand>();
        var groupIdMap = new Dictionary<string, string>();

        foreach (var orig in State.SelectedElements)
        {
            var clone = orig.Clone();
            float origH = orig.Height ?? orig.EffectiveDesignerHeight;
            float cloneH = clone.Height ?? clone.EffectiveDesignerHeight;

            if (direction is null)
            {
                clone.X = Math.Clamp(orig.X + 5f, 0f, Math.Max(0f, sectionW - clone.Width));
                clone.Y = Math.Clamp(orig.Y + 5f, 0f, Math.Max(0f, sectionH - cloneH));
            }
            else
            {
                (clone.X, clone.Y) = direction.Value switch
                {
                    DuplicateDirection.Down  => (orig.X, orig.Y + origH + gap),
                    DuplicateDirection.Up    => (orig.X, orig.Y - cloneH - gap),
                    DuplicateDirection.Right => (orig.X + orig.Width + gap, orig.Y),
                    DuplicateDirection.Left  => (orig.X - clone.Width - gap, orig.Y),
                    _ => (orig.X + 5f, orig.Y + 5f)
                };
                clone.X = Math.Clamp(clone.X, 0f, Math.Max(0f, sectionW - clone.Width));
                clone.Y = Math.Clamp(clone.Y, 0f, Math.Max(0f, sectionH - cloneH));
            }

            // Přemapovat GroupId - klony dostanou nový společný GroupId
            RemapGroupId(clone, groupIdMap);

            newElements.Add(clone);
            commands.Add(new AddElementCommand(State.ActiveSection, clone));
        }

        if (commands.Count == 1) Execute(commands[0]);
        else Execute(new CompositeCommand("Duplikovat elementy", commands));

        State.SelectedElements.Clear();
        State.SelectedElements.AddRange(newElements);
        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    // ── Split element ─────────────────────────────────────────────────────────

    /// <summary>
    /// Rozdělí vybrané elementy na poloviny. Horizontal = nad/pod, Vertical = vedle sebe.
    /// Originál se zmenší na polovinu, klon se umístí do druhé poloviny.
    /// </summary>
    public void SplitSelected(bool horizontal)
    {
        if (State.SelectedElements.Count == 0 || State.ActiveSection is null) return;

        var commands = new List<IDesignerCommand>();
        var newSelection = new List<TemplateElement>();

        foreach (var orig in State.SelectedElements.ToList())
        {
            float origH = orig.Height ?? orig.EffectiveDesignerHeight;
            var clone = orig.Clone();

            if (horizontal)
            {
                // Rozdělit výšku na dvě poloviny
                float halfH = MathF.Round(origH / 2f, 2);
                commands.Add(new ResizeElementCommand(orig,
                    orig.X, orig.Y, orig.Width, orig.Height,
                    orig.X, orig.Y, orig.Width, halfH));
                clone.Y = orig.Y + halfH;
                clone.Height = MathF.Round(origH - halfH, 2);
            }
            else
            {
                // Rozdělit šířku na dvě poloviny
                float halfW = MathF.Round(orig.Width / 2f, 2);
                commands.Add(new ResizeElementCommand(orig,
                    orig.X, orig.Y, orig.Width, orig.Height,
                    orig.X, orig.Y, halfW, orig.Height));
                clone.X = orig.X + halfW;
                clone.Width = MathF.Round(orig.Width - halfW, 2);
            }

            commands.Add(new AddElementCommand(State.ActiveSection, clone));
            newSelection.Add(orig);
            newSelection.Add(clone);
        }

        if (commands.Count > 0)
        {
            Execute(new CompositeCommand(
                horizontal ? "Rozdělit horizontálně" : "Rozdělit vertikálně", commands));
            State.SelectedElements.Clear();
            State.SelectedElements.AddRange(newSelection);
            RequestPropertiesPanelUpdate?.Invoke();
            RequestCanvasRefresh?.Invoke();
        }
    }

    // ── Element factory (shared by palette drag + toolbar menu) ────────────────

    public static (float Width, float Height) GetDefaultElementSize(string type) => type switch
    {
        ElementTypes.Text          => (80f, 8f),
        ElementTypes.Rectangle     => (60f, 20f),
        ElementTypes.Line          => (190f, 1f),
        ElementTypes.Image         => (40f, 30f),
        ElementTypes.Table         => (90f, 18f),   // 3 řádky po 6 mm - viz CreateGrid
        ElementTypes.DynamicTable  => (190f, 30f),
        ElementTypes.Barcode       => (30f, 30f),
        ElementTypes.StopList      => (60f, 60f),
        ElementTypes.DepartureGrid => (120f, 60f),
        ElementTypes.NotesLegend   => (190f, 25f),
        ElementTypes.LineTable     => (190f, 100f),
        _                          => (80f, 8f)
    };

    public static TemplateElement CreateElementByType(string type, float x, float y)
    {
        var (w, h) = GetDefaultElementSize(type);
        var el = type switch
        {
            ElementTypes.Text          => (TemplateElement)new TextElement      { X = x, Y = y, Width = w, Height = h },
            ElementTypes.Rectangle     => new RectangleElement { X = x, Y = y, Width = w, Height = h, StrokeWidth = 0.5f },
            ElementTypes.Line          => new LineElement      { X = x, Y = y, Width = w, Height = h },
            ElementTypes.Image         => new ImageElement     { X = x, Y = y, Width = w, Height = h },
            ElementTypes.Table         => NewFixedTable(x, y, w, h),
            ElementTypes.DynamicTable  => new DynamicTableElement { X = x, Y = y, Width = w, Height = h },
            ElementTypes.Barcode       => new BarcodeElement   { X = x, Y = y, Width = w, Height = h },
            ElementTypes.StopList      => new StopListElement      { X = x, Y = y, Width = w, Height = h },
            ElementTypes.DepartureGrid => new DepartureGridElement { X = x, Y = y, Width = w, Height = h },
            // Height null = dynamická. Legenda i tabulka rostou z dat, takže pevná výška by jim
            // obsah **zmenšila do fitu** (písmo na 46 %), ne oříznula - a uživatel na to přišel
            // až z papíru (nález Jan 7. 9.). Zástupnou výšku (25 / 60 mm) drží prvek sám pro
            // rámeček výběru v návrháři.
            ElementTypes.NotesLegend   => new NotesLegendElement   { X = x, Y = y, Width = w },
            ElementTypes.LineTable     => new LineTableElement     { X = x, Y = y, Width = w },
            _                          => (TemplateElement)new TextElement      { X = x, Y = y, Width = w, Height = h }
        };
        // Výchozí rámeček pro nové elementy (na modelu je default bez rámečku kvůli zpětné kompatibilitě)
        el.BorderColor = "#FF000000";
        el.BorderWidth = 0.1f;
        return el;
    }

    /// <summary>
    /// Vytvoří prvek k vložení; u pevné tabulky se **nejdřív zeptá na rozměr** (rychlý formulář,
    /// předvyplněný 3 × 3). Vrací null, když uživatel vkládání zrušil - dřív se vždycky vložila
    /// mřížka 3 × 3 a na 12 × 10 se muselo proklikat devatenáct vložení sloupce a řádku.
    /// Bez dialogové služby (testy) se chová jako dřív, takže se nic nezablokuje.
    /// </summary>
    public TemplateElement? CreateElementInteractive(string type, float x, float y)
    {
        if (type != ElementTypes.Table || Dialogs is null) return CreateElementByType(type, x, y);

        var size = new TableGridSize();
        if (!Dialogs.ShowQuickForm(size, "ALVA.TemplateDesigner.EDInputForm.TableInsertEdit", "Vložit tabulku"))
            return null;

        var (w, h) = GetDefaultElementSize(type);
        return NewFixedTable(x, y, w, h, size.Columns, size.Rows);
    }

    /// <summary>
    /// Nová pevná tabulka - mřížka 3 × 3 s **pevnou výškou**. Bez výšky (Height null) by prvek
    /// platil za dynamický a canvas by mu nabídl jen boční úchyty, takže by za něj nešlo táhnout
    /// do výšky - a to uživatel od ručně kreslené tabulky čeká (nahlášeno 12. 8.).
    /// </summary>
    private static TableElement NewFixedTable(float x, float y, float w, float? h,
        int columns = 3, int rows = 3)
    {
        var table = TableElement.CreateGrid(columns, rows);
        table.X = x;
        table.Y = y;
        table.Width = w;
        // Výška roste s počtem řádků - u desetiřádkové tabulky by výchozích 18 mm znamenalo,
        // že se obsah hned po vložení zmenší, aby se vešel.
        table.Height = h is null ? null : rows * table.RowHeight;
        return table;
    }

    // ── Delete ──────────────────────────────────────────────────────────────────

    public void DeleteSelected()
    {
        if (State.SelectedElements.Count == 0) return;

        var commands = new List<IDesignerCommand>();
        foreach (var el in State.SelectedElements.ToList())
        {
            var section = State.CurrentTemplate.Sections.FirstOrDefault(s => s.Elements.Contains(el));
            if (section is null || el.IsLocked) continue;
            commands.Add(new DeleteElementCommand(section, el));
        }

        if (commands.Count == 0) return;

        State.SelectedElements.Clear();

        if (commands.Count == 1) Execute(commands[0]);
        else Execute(new CompositeCommand("Smazat elementy", commands));

        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    // ── Group / Ungroup ──────────────────────────────────────────────────────

    /// <summary>Přemapuje GroupId klonu na nový sdílený GroupId (pro paste/duplicate).</summary>
    private static void RemapGroupId(TemplateElement clone, Dictionary<string, string> groupIdMap)
    {
        if (clone.GroupId is null) return;
        if (!groupIdMap.TryGetValue(clone.GroupId, out var newGid))
            groupIdMap[clone.GroupId] = newGid = ElementGroupHelper.NewGroupId();
        clone.GroupId = newGid;
    }

    public void GroupSelected()
    {
        if (State.SelectedElements.Count < 2) return;
        var groupId = ElementGroupHelper.NewGroupId();
        Execute(new GroupCommand(State.SelectedElements, groupId));
        RequestCanvasRefresh?.Invoke();
        RequestPropertiesPanelUpdate?.Invoke();
    }

    public void UngroupSelected()
    {
        var grouped = State.SelectedElements.Where(e => e.GroupId is not null).ToList();
        if (grouped.Count == 0) return;
        Execute(new UngroupCommand(grouped));
        RequestCanvasRefresh?.Invoke();
        RequestPropertiesPanelUpdate?.Invoke();
    }

    // ── ZIndex ────────────────────────────────────────────────────────────────
    public void ChangeZIndex(int sign, bool toExtreme)
    {
        if (State.SelectedElements.Count == 0 || State.ActiveSection is null) return;

        var section  = State.ActiveSection;
        var selected = State.SelectedElements
            .Where(el => section.Elements.Contains(el))
            .ToList();
        if (selected.Count == 0) return;

        var commands = new List<IDesignerCommand>();

        if (toExtreme && sign > 0)
        {
            int baseZ = section.Elements
                .Where(el => !selected.Contains(el))
                .Select(el => el.ZIndex)
                .DefaultIfEmpty(-1).Max() + 1;
            foreach (var el in selected.OrderBy(e => e.ZIndex))
            {
                int oldZ = el.ZIndex, newZ = baseZ++;
                if (oldZ != newZ) commands.Add(new SetPropertyCommand("ZIndex", oldZ, newZ, v => el.ZIndex = (int)v!));
            }
        }
        else if (toExtreme && sign < 0)
        {
            int shift = selected.Count;
            foreach (var el in section.Elements.Where(e => !selected.Contains(e)))
            {
                int oldZ = el.ZIndex, newZ = oldZ + shift;
                commands.Add(new SetPropertyCommand("ZIndex", oldZ, newZ, v => el.ZIndex = (int)v!));
            }
            int z = 0;
            foreach (var el in selected.OrderBy(e => e.ZIndex))
            {
                int oldZ = el.ZIndex, newZ = z++;
                if (oldZ != newZ) commands.Add(new SetPropertyCommand("ZIndex", oldZ, newZ, v => el.ZIndex = (int)v!));
            }
        }
        else
        {
            foreach (var el in selected)
            {
                int oldZ = el.ZIndex, newZ = Math.Max(0, oldZ + sign);
                if (oldZ != newZ) commands.Add(new SetPropertyCommand("ZIndex", oldZ, newZ, v => el.ZIndex = (int)v!));
            }
        }

        if (commands.Count == 0) return;

        string desc = (sign > 0, toExtreme) switch
        {
            (true,  true)  => "Přenést zcela dopředu",
            (true,  false) => "Přenést dopředu",
            (false, true)  => "Odeslat zcela dozadu",
            _              => "Odeslat dozadu"
        };

        Execute(commands.Count == 1 ? commands[0] : new CompositeCommand(desc, commands));
        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    // ── Alignment ─────────────────────────────────────────────────────────────
    public void AlignElements(AlignMode mode)
    {
        // X-align pracuje napříč sekcemi (sekce sdílejí levý margin → X je přímo porovnatelné).
        // Y-align zůstává per-aktivní-sekce (Y je relativní k hornímu okraji sekce).
        bool isXMode = mode is AlignMode.Left or AlignMode.Right or AlignMode.CenterH or AlignMode.DistributeH;

        List<TemplateElement> els;
        if (isXMode)
        {
            els = State.SelectedElements.ToList();
        }
        else
        {
            if (State.ActiveSection is null) return;
            els = State.SelectedElements
                .Where(el => State.ActiveSection.Elements.Contains(el))
                .ToList();
        }

        int minCount = mode is AlignMode.DistributeH or AlignMode.DistributeV ? 3 : 2;
        if (els.Count < minCount) return;

        var commands = new List<IDesignerCommand>();

        // Zaokrouhlení na celé mm - align cílí na "čistou" hodnotu konzistentní se soft-snapem při dragu.
        static float Rnd(float v) => MathF.Round(v);

        switch (mode)
        {
            case AlignMode.Left:
            {
                float t = Rnd(els.Min(e => e.X));
                foreach (var el in els)
                { if (Math.Abs(el.X - t) > 0.001f) commands.Add(MakeXCmd(el, t)); }
                break;
            }
            case AlignMode.Right:
            {
                float t = Rnd(els.Max(e => e.X + e.Width));
                foreach (var el in els)
                { float nx = t - el.Width; if (Math.Abs(el.X - nx) > 0.001f) commands.Add(MakeXCmd(el, nx)); }
                break;
            }
            case AlignMode.CenterH:
            {
                float cx = Rnd((els.Min(e => e.X) + els.Max(e => e.X + e.Width)) / 2f);
                foreach (var el in els)
                { float nx = cx - el.Width / 2f; if (Math.Abs(el.X - nx) > 0.001f) commands.Add(MakeXCmd(el, nx)); }
                break;
            }
            case AlignMode.Top:
            {
                float t = Rnd(els.Min(e => e.Y));
                foreach (var el in els)
                { if (Math.Abs(el.Y - t) > 0.001f) commands.Add(MakeYCmd(el, t)); }
                break;
            }
            case AlignMode.Bottom:
            {
                float t = Rnd(els.Max(e => e.Y + e.EffectiveDesignerHeight));
                foreach (var el in els)
                { float ny = t - el.EffectiveDesignerHeight; if (Math.Abs(el.Y - ny) > 0.001f) commands.Add(MakeYCmd(el, ny)); }
                break;
            }
            case AlignMode.MiddleV:
            {
                float cy = Rnd((els.Min(e => e.Y) + els.Max(e => e.Y + e.EffectiveDesignerHeight)) / 2f);
                foreach (var el in els)
                { float ny = cy - el.EffectiveDesignerHeight / 2f; if (Math.Abs(el.Y - ny) > 0.001f) commands.Add(MakeYCmd(el, ny)); }
                break;
            }
            case AlignMode.DistributeH:
            {
                var sorted = els.OrderBy(e => e.X).ToList();
                float totalW = sorted.Sum(e => e.Width);
                float span   = sorted.Last().X + sorted.Last().Width - sorted.First().X;
                float gap    = MmMath.Round((span - totalW) / (sorted.Count - 1));
                float cur    = sorted.First().X;
                for (int i = 0; i < sorted.Count - 1; i++)
                {
                    var el = sorted[i];
                    float x = MmMath.Round(cur);
                    if (Math.Abs(el.X - x) > 0.01f) commands.Add(MakeXCmd(el, x));
                    cur += el.Width + gap;
                }
                break;
            }
            case AlignMode.DistributeV:
            {
                var sorted = els.OrderBy(e => e.Y).ToList();
                float totalH = sorted.Sum(e => e.EffectiveDesignerHeight);
                float span   = sorted.Last().Y + sorted.Last().EffectiveDesignerHeight - sorted.First().Y;
                float gap    = MmMath.Round((span - totalH) / (sorted.Count - 1));
                float cur    = sorted.First().Y;
                for (int i = 0; i < sorted.Count - 1; i++)
                {
                    var el = sorted[i];
                    float y = MmMath.Round(cur);
                    if (Math.Abs(el.Y - y) > 0.01f) commands.Add(MakeYCmd(el, y));
                    cur += el.EffectiveDesignerHeight + gap;
                }
                break;
            }
        }

        if (commands.Count == 0) return;

        string desc = mode switch
        {
            AlignMode.Left        => "Zarovnat vlevo",
            AlignMode.Right       => "Zarovnat vpravo",
            AlignMode.CenterH     => "Zarovnat na střed (vodorovně)",
            AlignMode.Top         => "Zarovnat nahoru",
            AlignMode.Bottom      => "Zarovnat dolů",
            AlignMode.MiddleV     => "Zarovnat na střed (svisle)",
            AlignMode.DistributeH => "Rozložit vodorovně",
            AlignMode.DistributeV => "Rozložit svisle",
            _                     => "Zarovnat"
        };

        Execute(commands.Count == 1 ? commands[0] : new CompositeCommand(desc, commands));
        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    private static IDesignerCommand MakeXCmd(TemplateElement el, float nx)
        => new SetPropertyCommand("X", el.X, nx, v => el.X = (float)v!);
    private static IDesignerCommand MakeYCmd(TemplateElement el, float ny)
        => new SetPropertyCommand("Y", el.Y, ny, v => el.Y = (float)v!);

    // ── Normalize positions ───────────────────────────────────────────────────
    public void NormalizePositions(IReadOnlyList<TemplateElement> elements, float step)
    {
        if (elements.Count == 0) return;

        var commands = new List<IDesignerCommand>();
        foreach (var el in elements)
        {
            float nx = MmMath.Snap(el.X, step), ny = MmMath.Snap(el.Y, step);
            float nw = MmMath.Snap(el.Width, step);
            float? nh = el.Height is not null ? MmMath.Snap(el.Height.Value, step) : null;

            bool posChanged  = Math.Abs(nx - el.X) > 0.001f || Math.Abs(ny - el.Y) > 0.001f;
            bool sizeChanged = Math.Abs(nw - el.Width) > 0.001f
                            || (nh is not null && Math.Abs(nh.Value - el.Height!.Value) > 0.001f);

            if (posChanged || sizeChanged)
                commands.Add(new ResizeElementCommand(el, el.X, el.Y, el.Width, el.Height, nx, ny, nw, nh));
        }

        if (commands.Count == 0) return;
        Execute(new CompositeCommand("Normalizovat pozice", commands));
        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    // ── Template scaling ─────────────────────────────────────────────────────
    public void ScaleTemplate(float scale)
    {
        if (Math.Abs(scale - 1f) < 0.001f) return;
        var cmd = new ScaleTemplateCommand(State.CurrentTemplate, scale);
        Execute(cmd);
        RebuildSections();
        RequestCanvasSizeUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    // ── Key movement ──────────────────────────────────────────────────────────
    /// <summary>
    /// Arrow key nudging. Kroky:
    ///   fineStep=true (Ctrl)   → 0.5 mm (jemné doladění)
    ///   largeStep=true (Shift) → GridSize (rychlý skok)
    ///   bez modifikátorů       → 1 mm (default)
    /// </summary>
    public void MoveByKey(Key key, bool gridStep)
    {
        // Krok mřížky drží Ctrl (dřív Shift, a na Ctrl byl jemný krok 0,5 mm) - Shift se uvolnil
        // pro změnu velikosti, jak ji uživatel zná ze starého návrháře (rozhodnutí Jan 1. 9.).
        float step = gridStep ? State.GridSize : 1f;
        float dx   = key switch { Key.Left => -step, Key.Right => step, _ => 0f };
        float dy   = key switch { Key.Up   => -step, Key.Down  => step, _ => 0f };

        var initPositions = State.SelectedElements.ToDictionary(el => el, el => (el.X, el.Y));
        float sectionW = State.CurrentTemplate.PageSize.PrintableWidth;
        float clampedDx = dx, clampedDy = dy;

        foreach (var kvp in initPositions)
        {
            var el      = kvp.Key;
            float elH   = el.EffectiveDesignerHeight;
            var section = State.CurrentTemplate.Sections.FirstOrDefault(s => s.Elements.Contains(el));
            float sectionH = section?.EffectiveDesignerHeight ?? float.MaxValue;
            float maxDx = sectionW - el.Width - kvp.Value.X;
            float maxDy = sectionH - elH    - kvp.Value.Y;
            // Element může být širší/vyšší než sekce (např. background image) - přeskočit clamp
            if (maxDx >= -kvp.Value.X)
                clampedDx = Math.Clamp(clampedDx, -kvp.Value.X, maxDx);
            if (maxDy >= -kvp.Value.Y)
                clampedDy = Math.Clamp(clampedDy, -kvp.Value.Y, maxDy);
        }

        if (clampedDx == 0f && clampedDy == 0f) return;

        foreach (var kvp in initPositions)
        { kvp.Key.X = kvp.Value.X + clampedDx; kvp.Key.Y = kvp.Value.Y + clampedDy; }

        var moves = initPositions
            .Where(kvp => kvp.Key.X != kvp.Value.X || kvp.Key.Y != kvp.Value.Y)
            .Select(kvp => (IDesignerCommand)new MoveElementCommand(
                kvp.Key, kvp.Value.X, kvp.Value.Y, kvp.Key.X, kvp.Key.Y))
            .ToList();

        if (moves.Count == 1) Execute(moves[0]);
        else if (moves.Count > 1) Execute(new CompositeCommand("Přesun šipkou", moves));

        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    /// <summary>
    /// Co udělají šipky nad vybraným prvkem. Rozhodnutí je tady, a ne rozeseté v obsluze kláves,
    /// aby se dalo ohlídat testem - mutace 1. 9. ukázala, že samotné volání
    /// <see cref="ResizeByKey"/> testy pokryjí, ale odpojení od Shiftu už ne.
    /// </summary>
    /// <remarks>
    /// Ctrl+Shift+↑/↓ řeší obsluha kláves dřív (přesun sekce), takže sem ta kombinace nedojde.
    /// </remarks>
    public static ArrowKeyAction ResolveArrowAction(bool ctrl, bool shift) => (ctrl, shift) switch
    {
        (_, true)     => ArrowKeyAction.Resize,      // Shift = velikost, jako ve starém návrháři
        (true, false) => ArrowKeyAction.MoveByGrid,  // Ctrl = posun po mřížce
        _             => ArrowKeyAction.Move,        // samotné šipky = posun o milimetr
    };

    /// <summary>Nejmenší rozměr, na který jde prvek stáhnout - shodně s tažením úchytu myší.</summary>
    private const float MinElementWidthMm = 3f;
    private const float MinElementHeightMm = 2f;

    /// <summary>
    /// Změní velikost vybraných prvků o krok. Mění se <b>pravá a dolní hrana</b>, levý horní roh
    /// zůstává - stejně jako když uživatel táhne pravý dolní úchyt.
    /// </summary>
    /// <remarks>
    /// ⚠️ Prvku s dynamickou výškou se svisle nic nestane a řekne se to ve stavovém řádku. Fixovat
    /// mu výšku by znamenalo, že o dynamiku přijde bez varování a dostane se z toho jen zpětným
    /// krokem; myš mu ze stejného důvodu svislé úchyty vůbec nenabízí (rozhodnutí Jan 1. 9.).
    /// </remarks>
    public void ResizeByKey(Key key, bool gridStep)
    {
        float step = gridStep ? State.GridSize : 1f;
        float dw = key switch { Key.Left => -step, Key.Right => step, _ => 0f };
        float dh = key switch { Key.Up   => -step, Key.Down  => step, _ => 0f };

        var cile = State.SelectedElements.Where(el => !el.IsLocked).ToList();
        if (cile.Count == 0) return;

        // Svislá změna se týká jen prvků s pevnou výškou; když mají všechny vybrané výšku z dat,
        // není co měnit a je lepší to říct, než tiše nedělat nic.
        if (dh != 0f && cile.All(el => el.Height is null))
        {
            StatusText = cile.Count == 1
                ? "Výšku tohoto prvku počítá engine z dat - zadejte ji v jeho vlastnostech, nebo měňte šířku."
                : "Vybrané prvky mají výšku z dat - svisle je zvětšit nejde.";
            return;
        }

        float sectionW = State.CurrentTemplate.PageSize.PrintableWidth;
        var commands = new List<IDesignerCommand>();

        foreach (var el in cile)
        {
            float oldW = el.Width;
            float? oldH = el.Height;

            float newW = MathF.Max(MinElementWidthMm, MathF.Min(oldW + dw, sectionW - el.X));
            float? newH = oldH;
            if (dh != 0f && oldH is { } h)
            {
                var section = State.CurrentTemplate.Sections.FirstOrDefault(sec => sec.Elements.Contains(el));
                float sectionH = section?.EffectiveDesignerHeight ?? float.MaxValue;
                newH = MathF.Max(MinElementHeightMm, MathF.Min(h + dh, sectionH - el.Y));
            }

            if (newW == oldW && newH == oldH) continue;
            el.Width = newW;
            el.Height = newH;
            commands.Add(new ResizeElementCommand(el, el.X, el.Y, oldW, oldH, el.X, el.Y, newW, newH));
        }

        if (commands.Count == 0) return;
        Execute(commands.Count == 1 ? commands[0] : new CompositeCommand("Změna velikosti klávesnicí", commands));

        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    // ── Schema / parameters ───────────────────────────────────────────────────
    /// <summary>Vrací Task, který doběhne až po naplnění <c>_agendaParameters</c> (i na cold cestě,
    /// kde se schéma načítá async). Otevírací flow ho awaituje, aby se hodnoty parametrů ze šablony
    /// / per-agenda aplikovaly až když jsou parametry agendy k dispozici (jinak by je následné
    /// doběhnutí schématu přepsalo defaulty). Ostatní volající mohou volat fire-and-forget.</summary>
    public Task RefreshFieldsList()
    {
        var agendaCode = State.CurrentTemplate.AgendaCode;
        NotifyInstanceLabelsChanged();   // agenda se mohla změnit → přepočítat „linka/faktura" v chipu + tooltipech

        if (string.IsNullOrWhiteSpace(agendaCode))
        {
            AgendaNameText = "Agenda: - (šablona bez agendy)";
            UpdateCacheStatus(null, null);
            LoadAgendaParameters(agendaCode);
            RebuildSchemaTree(null);
            return Task.CompletedTask;
        }

        // R1: první (uncached) GetSchema je pomalý HTTP dotaz - na cold cestě odbav na pozadí
        // a strom polí doplň po návratu; jinak zamrzne UI při otevření šablony cizí agendy na
        // pomalé síti. Opakované (cachované) volání jde rovnou synchronně (rychlé).
        if (CacheProvider is not null && !CacheProvider.IsSchemaCached(agendaCode))
        {
            var code = agendaCode;
            AgendaNameText = $"Agenda: {code} (načítám schéma…)";
            return Task.Run(() => SchemaProvider.GetSchema(code))
                .ContinueWith(_ => RefreshFieldsListCore(code),
                              TaskScheduler.FromCurrentSynchronizationContext());
        }

        RefreshFieldsListCore(agendaCode);
        return Task.CompletedTask;
    }

    private void RefreshFieldsListCore(string agendaCode)
    {
        var schema = SchemaProvider.GetSchema(agendaCode);   // po prewarmu už cached → rychlé

        if (schema is null)
        {
            AgendaNameText = $"Agenda: {agendaCode} (schéma nenalezeno)";
            UpdateCacheStatus(agendaCode, null);
            LoadAgendaParameters(agendaCode);
            RebuildSchemaTree(null);
            return;
        }

        // Explicitní dvouosý formát: „Agenda: Zastávkový JŘ · Varianta: AD" (učí nové názvosloví);
        // bez varianty jen „Agenda: Faktury". Fallback = jméno ze schématu (registr nezná kód).
        var entry = AgendaEntry(agendaCode);
        AgendaNameText = entry is { Variant.Length: > 0 }
            ? $"Agenda: {entry.EffectiveAgendaLabel} · Varianta: {entry.Variant}"
            : $"Agenda: {entry?.EffectiveAgendaLabel ?? schema.AgendaName}";
        var cacheSchema = CacheProvider?.GetSchema(agendaCode);
        UpdateCacheStatus(agendaCode, cacheSchema);
        LoadAgendaParameters(agendaCode);
        RebuildSchemaTree(schema);
        _ = PrefetchMockDataAsync(agendaCode);
    }

    /// <summary>Načte mock data agendy na pozadí do cache a po dokončení překreslí náhled (aby
    /// první vykreslení nešlo přes synchronní HTTP). Když jsou už v cache, hned se vrátí.</summary>
    private async Task PrefetchMockDataAsync(string agendaCode)
    {
        if (CacheProvider is null || string.IsNullOrEmpty(agendaCode)) return;
        // Definice sloupců jde i do ukázkových dat - jinak náhled bez vybrané instance ukazuje
        // rozvržení podle linky a vlastní sloupce se v něm neprojeví (#13158).
        var columnsDefJson = CurrentColumnsDefinitionJson();
        var jazyk = JazykSablony;
        if (CacheProvider.TryGetCachedMockData(agendaCode, columnsDefJson, jazyk, out _)) return;   // už načteno (i negativně)
        await Task.Run(() => CacheProvider.GetMockData(agendaCode, columnsDefJson, jazyk));
        // Náhled se mezitím nakreslil ze syntetických dat - překresli s reálnými mock daty.
        if (State.CurrentTemplate.AgendaCode == agendaCode) RequestPreviewRefresh?.Invoke();
    }

    public void RebuildSchemaTree(DataSchema? schema)
    {
        SchemaGroups.Clear();

        // Paleta zobrazuje JEN skalární pole (draggable jako {POLE}). Kolekce (Zastávky,
        // Odjezdy, …) konzumují dedikované elementy (StopList/DepartureGrid/NotesLegend),
        // ne ad-hoc tabulky - jako draggable pole jen matou. Parametry se nastavují
        // v preview liště / tiskovém dialogu, ne přetažením. (Rozhodnuto 2026-06-16.)
        var fields = schema?.Fields ?? [];

        // Barevná pole se do palety nedávají - přetažením by vznikl text s hexem, což nikdo nechce.
        // Zadávají se do barevného pole jako {POLE} (nápověda je u těch polí ve formuláři).
        var textFields = fields.Where(f => f.DataType != FieldDataType.Color).ToList();
        if (textFields.Count > 0)
            SchemaGroups.Add(new SchemaGroupVM(
                $"📋 Pole agendy ({textFields.Count})",
                textFields.Select(f => new SchemaFieldVM(f.FieldName, f.Label))));

        // Hodnoty, které dosadí až sazba podle stránky (`PageLayoutEngine.HodnotyStranky`) - v datech
        // agendy nejsou, a číslo stránky proto do 23. 9. znala jen nápověda. Vloží se jen do sekce
        // tištěné na každé stránce - v sekci obsahu by ukázala hodnotu první strany.
        SchemaGroups.Add(new SchemaGroupVM($"📄 Stránka ({PoleStranky.Length})",
            PoleStranky.Select(p => new SchemaFieldVM(p.Pole, p.Popis))));

        ShowFieldsEmptyState = textFields.Count == 0;
    }

    public void LoadAgendaParameters(string? agendaCode)
    {
        var code = string.IsNullOrEmpty(agendaCode) ? null : agendaCode;
        if (code == _lastParamsAgendaCode) return;
        _lastParamsAgendaCode = code;

        _agendaParameters = code is not null && CacheProvider is not null
            ? [.. CacheProvider.GetParameters(code)]
            : [.. State.CurrentTemplate.Parameters ?? []];

        _previewParams.Clear();
        _cachedParamValues = null;
        foreach (var p in _agendaParameters)
            _previewParams[p.Name] = p.GetDefaultTyped();

        RebuildParametersPanel();
        // Reálná data řídí výhradně Instance Picker (RefreshDataForCurrentInstanceAsync).
        // Bez vybrané instance preview používá mock data - parametry sestavy už linku neurčují.
        RequestPreviewRefresh?.Invoke();
    }

    /// <summary>
    /// Načte uložené PrintParams pro aktuální agendu a aplikuje je nad <see cref="_previewParams"/>.
    /// Volá se po opening šablony (po LoadAgendaParameters) a z OnPreferencesLoaded (cold-start fallback).
    /// Idempotentní - pokud žádná saved data, nic nemění. Per-agenda granularita: nová šablona
    /// v rámci stejné agendy zdědí naposledy použité hodnoty.
    /// </summary>
    public async Task LoadPrintParamsAndApplyAsync()
    {
        if (Preferences is null) return;
        var agendaCode = State.CurrentTemplate.AgendaCode;
        if (string.IsNullOrEmpty(agendaCode)) return;

        var saved = await Preferences.LoadPrintParamsAsync(agendaCode);
        if (saved.Count == 0) return;

        bool changed = false;
        foreach (var p in _agendaParameters)
        {
            if (!saved.TryGetValue(p.Name, out var node) || node is null) continue;
            try
            {
                object? newValue = p.Type switch
                {
                    "bool"   => node.GetValue<bool>(),
                    "number" => node.GetValue<float>(),
                    _        => node.GetValue<string>()
                };
                if (!Equals(_previewParams.GetValueOrDefault(p.Name), newValue))
                {
                    _previewParams[p.Name] = newValue;
                    changed = true;
                }
            }
            catch { /* malformed value pro tento param - ponecháme default */ }
        }

        // Export pole (členění/název souboru) nejsou agenda parametry → obnovit zvlášť (string).
        // Bez tohoto by se uložená volba po reopenu šablony ztratila (write-only).
        foreach (var key in PrintDialogViewModel.ExportKeys)
        {
            if (!saved.TryGetValue(key, out var node) || node is null) continue;
            try { _previewParams[key] = node.GetValue<string>(); }
            catch { /* malformed - ponecháme default */ }
        }

        if (changed) OnExternalParamsChanged();
    }

    /// <summary>Aplikuje hodnoty obsahových parametrů uložené v šabloně (nejvyšší priorita - nad
    /// per-agenda i defaulty). Volá se po <see cref="LoadPrintParamsAndApplyAsync"/> při otevření
    /// šablony. Nenastavuje dirty (načtení, ne uživatelská změna).</summary>
    private void ApplyTemplatePrintParamsToPreview()
    {
        var tp = State.CurrentTemplate.PrintParameters;
        if (tp is null || tp.Count == 0) return;
        foreach (var p in _agendaParameters)
        {
            if (!tp.TryGetValue(p.Name, out var s) || s is null) continue;
            _previewParams[p.Name] = p.Type switch
            {
                "bool"   => s == "1" || string.Equals(s, "true", StringComparison.OrdinalIgnoreCase),
                "number" => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : null,
                _        => (object)s
            };
        }
        // Přenačtení panelu/tabu z finálních hodnot dělá otevírací flow (RefreshParamsUiAfterLoad),
        // bezpodmínečně - per-agenda i šablona; sem to nedávat (nespustilo by se, když šablona
        // nic nezměnila, protože per-agenda už tytéž hodnoty nastavila → tab by zůstal na defaultech).
    }

    /// <summary>
    /// Jediná cesta k aplikaci persistovaných hodnot parametrů: per-agenda → šablona → přenačtení UI.
    /// **Volat vždy tuhle metodu, nikdy ty tři kroky zvlášť.** Když se jeden krok vynechá (dřív to
    /// dělal cold-start fallback po dokončení načtení preferencí), hodnoty jdou do `GetData`, ale
    /// ovladače v UI zůstanou na defaultech - uživatel pak tiskne něco jiného, než co v nastavení vidí
    /// a nemá jak na to přijít. Přesně tak tiše rozdělovalo vývěsky `ROZDELIT_PER_OZNACNIK=true`.
    /// </summary>
    public async Task ApplyPersistedPrintParamsAsync()
    {
        await LoadPrintParamsAndApplyAsync();
        ApplyTemplatePrintParamsToPreview();
        RefreshParamsUiAfterLoad();
    }

    /// <summary>Po načtení všech persistovaných hodnot parametrů (per-agenda + šablona) přenačte
    /// lištu i EDInputForm tab z finálního <see cref="_previewParams"/>. Bezpodmínečně - hodnoty
    /// do dictu naskočí i bez „změny" v ApplyTemplate (per-agenda), ale tab se jinak drží defaultů,
    /// se kterými se inicializoval na začátku otevření.</summary>
    private void RefreshParamsUiAfterLoad()
    {
        RebuildParametersPanel();            // lišta + RequestParamsFormReload (tab)
        RefreshPreviewAfterParamChange();    // živý náhled; u server-side hodnot i nová data
    }

    /// <summary>
    /// Externí konzument (typicky PrintDialog) změnil hodnotu v sdíleném <see cref="_previewParams"/>.
    /// Cross-notify preview lišta VMs + invalidate cache + refresh preview canvas. Bez tohoto
    /// volání preview lišta neukáže změnu, protože její <c>ParameterControlVM</c> jsou jiné instance
    /// než PrintDialogovy a vlastní PropertyChanged events se nepropagují automaticky.
    ///
    /// ⚠️ Neobnovuje EDInputForm tab (jeho `Init()` je synchronní Caché round-trip, nechceme ho na
    /// každou změnu). Tab přenačítá volající: načtení persistovaných hodnot přes
    /// <see cref="ApplyPersistedPrintParamsAsync"/>, PrintDialog po zavření. Kdo přidá další cestu,
    /// která mění hodnoty, musí tab obnovit taky - jinak UI ukazuje jiné nastavení, než se tiskne.
    /// </summary>
    public void OnExternalParamsChanged()
    {
        _cachedParamValues = null;
        foreach (var vm in PreviewParameters) vm.NotifyValueChanged();
        NotifyParamsSummaryChanged();
        RefreshPreviewAfterParamChange();
    }

    /// <summary>Uloží aktuální <see cref="_previewParams"/> pro tuto agendu (fire-and-forget).</summary>
    public Task SavePrintParamsAsync()
    {
        if (Preferences is null) return Task.CompletedTask;
        var agendaCode = State.CurrentTemplate.AgendaCode;
        if (string.IsNullOrEmpty(agendaCode)) return Task.CompletedTask;
        // Rozvržení sloupců je vlastnost ŠABLONY (odvozuje se z definice sloupců prvku), ne
        // předvolba agendy - uložené napříč šablonami by u každé další lhalo.
        var perAgenda = _previewParams.Where(kv => kv.Key != LayoutParamName)
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        return Preferences.SavePrintParamsAsync(agendaCode, perAgenda);
    }

    /// <summary>Callback do code-behindu: přenačti EDInputForm parametrů v levém tabu
    /// (nové agenda schéma / hodnoty). Nastaví se v MainWindow konstruktoru.</summary>
    public Action? RequestParamsFormReload { get; set; }

    /// <summary>FQN Caché agenda třídy aktuální šablony (pro AgendaParams EDInputForm), null = bez agendy.</summary>
    public string? CurrentAgendaCacheClass =>
        CacheProvider?.GetCacheClass(State.CurrentTemplate.AgendaCode ?? "");

    public void RebuildParametersPanel()
    {
        // ParameterControlVM.Init seeduje _previewParams defaulty + drží HasParameters/summary;
        // samotný render parametrů dělá EDInputForm v tabu (AgendaParams), ne PreviewParameters.
        ParameterControlVM.Init(
            target:     PreviewParameters,
            parameters: _agendaParameters,
            values:     _previewParams,
            onChanged:  OnAgendaParamChanged,
            commitFactory: _ => null);

        // Výchozí snapshot server-paramů, ať první změna client-paramu nespustí falešný re-fetch.
        _lastServerParamsSnapshot = ServerParamsSnapshot();
        HasParameters = _agendaParameters.Count > 0;
        NotifyParamsSummaryChanged();
        RequestParamsFormReload?.Invoke();
    }

    private string? _lastServerParamsSnapshot;

    /// <summary>
    /// Otisk hodnot parametrů, které zpracovává až server v <c>GetData</c> - jejich změna musí
    /// spustit načtení dat, ne jen překreslení náhledu. Které to jsou, říká **sama agenda**
    /// příznakem u definice parametru (<see cref="TemplateParameter.ServerSide"/>).
    /// <para>
    /// ⚠️ **Chybějící příznak je tichá vada:** přepnutí volby v UI nedělá nic a tisk pak vyjede
    /// jinak než náhled (tiskový dialog načítá čerstvě). Stalo se to čtyřikrát, dokud si ten seznam
    /// držel klient natvrdo; hlídá to <c>ServerSideParamsDriftTests</c> - parametr čtený
    /// v <c>GetData</c> musí mít příznak v <c>GetParameters</c>.
    /// </para>
    /// </summary>
    private string ServerParamsSnapshot() => string.Join(
        "",
        _agendaParameters.Where(p => p.ServerSide)
            .Select(p => _previewParams.TryGetValue(p.Name, out var v) ? v?.ToString() : null));

    /// <summary>Reakce na změnu parametru (z EDInputform tabu i staré cesty): invaliduje cache,
    /// server-side parametry → re-fetch dat, ostatní → jen překreslení náhledu.</summary>
    private void OnAgendaParamChanged()
    {
        // Uživatelská změna parametru = změna šablony (parametry patří šabloně) → dirty, ať Ctrl+S uloží.
        // Volá se JEN z user cest (SetAgendaParamValue / preview lišta setter); načtení jde přes
        // OnExternalParamsChanged, takže tady dirty falešně nenaskočí.
        IsDirty = true;
        _cachedParamValues = null;
        NotifyParamsSummaryChanged();
        ApplyLayoutParamPick();   // rozvržení sloupců drží šablona - parametr ho do ní promítne
        RefreshPreviewAfterParamChange();
    }

    /// <summary>
    /// Hodnoty parametrů se změnily → server-side parametr znovu načte data, ostatní jen překreslí
    /// náhled. **Jediné místo, kde se to rozhoduje** - musí ji volat každá cesta, která hodnotami hne.
    /// <para>
    /// Když jedna cesta jen překreslí, náhled zůstane na datech předchozího nastavení, zatímco
    /// ovladače i tisk už jedou podle nového - a uživatel nemá jak poznat, které z toho platí.
    /// Potkávalo to dvě cesty: dialog Tisk a hlavně **otevření šablony** (data se natáhnou při
    /// obnovení instance, tedy dřív, než se aplikují uložené hodnoty parametrů).
    /// </para>
    /// </summary>
    private void RefreshPreviewAfterParamChange()
    {
        var snapshot = ServerParamsSnapshot();
        if (CurrentActiveInstance is not null && snapshot != _lastServerParamsSnapshot)
        {
            _lastServerParamsSnapshot = snapshot;
            _ = RefreshDataForCurrentInstanceAsync(keepPage: true);
        }
        else
        {
            RequestPreviewRefresh?.Invoke();
        }
    }

    // ── EDInputForm parametrů v levém tabu (AgendaParams, includeExport=0) ──────

    /// <summary>IN params pro AgendaParams.Init - jen parametry sestavy (bez export skupiny).
    /// Zrcadlí PrintDialog.BuildInitParams, ale includeExport=0 a bez export klíčů.</summary>
    public CVar BuildAgendaParamsInitParams()
    {
        var init = new CVar();
        init["agendaClass"]   = new CVar(CurrentAgendaCacheClass ?? "");
        init["includeExport"] = new CVar("0");

        var vals = new Dictionary<string, string>();
        foreach (var p in _agendaParameters)
        {
            var v = _previewParams.TryGetValue(p.Name, out var o) ? o : p.GetDefaultTyped();
            vals[p.Name] = ParamToFieldString(v, p.Type);
        }
        init["values"] = new CVar(JsonSerializer.Serialize(vals));
        return init;
    }

    /// <summary>Zapíše hodnotu pole z EDInputformu do sdíleného dict (typová konverze) + živý náhled.
    /// Sdílený dict = tentýž jako PrintDialog → hodnoty tab ↔ tisk konzistentní.</summary>
    public void SetAgendaParamValue(string name, string fieldValue)
    {
        var type = _agendaParameters.FirstOrDefault(p => p.Name == name)?.Type ?? "string";
        object? newValue = type switch
        {
            "bool"   => fieldValue == "1" || string.Equals(fieldValue, "true", StringComparison.OrdinalIgnoreCase),
            "number" => float.TryParse(fieldValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : null,
            _        => (object)fieldValue
        };
        // Beze změny → nic (chrání proti falešnému dirty i re-fetchi při reloadu EDInputformu tabu,
        // který přepošle stávající hodnoty přes FieldChanged).
        if (Equals(_previewParams.GetValueOrDefault(name), newValue)) return;
        _previewParams[name] = newValue;
        OnAgendaParamChanged();
    }

    /// <summary>visibleWhen parametru proti aktuálním hodnotám (jeden zdroj - TemplateParameter.IsVisible).</summary>
    public bool IsAgendaParamVisible(string name) =>
        _agendaParameters.FirstOrDefault(x => x.Name == name)?.IsVisible(_previewParams) ?? true;

    private static string ParamToFieldString(object? v, string type) => type switch
    {
        "bool"   => v is true ? "true" : "false",
        "number" => v is float f ? f.ToString(CultureInfo.InvariantCulture) : (v?.ToString() ?? ""),
        _        => v?.ToString() ?? ""
    };

    private void UpdateCacheStatus(string? agendaCode, DataSchema? cacheSchema)
    {
        if (CacheProvider is null)
        { StatusText = "Databáze: není nakonfigurována - pole načtena z JSON"; return; }

        // Stav připojení se nehlásí vůbec (rozhodnutí Jan 24. 9.): „pro agendu nejsou databázová
        // pole" se ukázalo na okamžik při každém otevření, než se schéma donačetlo. Skutečný
        // problém s daty ukáže „Žádná data" nebo prázdný náhled.
    }

    public CultureInfo GetTemplateCulture()
    {
        try { return CultureInfo.GetCultureInfo(State.CurrentTemplate.CultureCode ?? "cs-CZ"); }
        catch (CultureNotFoundException) { return CultureInfo.GetCultureInfo("cs-CZ"); }
    }

    // ── Preview mock data (called by code-behind preview renderer) ────────────

    /// <summary>
    /// Vrátí všechny dostupné DataContexty pro export - všechny reálné stránky pokud jsou
    /// načtené, jinak jeden mock context. Použito pro PDF/PNG export, aby se exportovaly
    /// všechny zastávky/směry, ne jen aktuálně zobrazená. Fetch (N× GetData přes MLRest)
    /// běží na threadpoolu (R1 - u velkých batchů jednotky sekund).
    /// </summary>
    public async Task<IReadOnlyList<DataContext>> BuildAllPreviewDataPagesAsync(IProgress<string>? progress = null)
    {
        // VEŠKERÝ UI stav zachytit na UI threadu před přechodem na threadpool - uživatel může
        // během exportu dál pracovat (měnit instance v pickeru, parametry, přepnout preview data),
        // background smyčka proto nesmí číst živé VM kolekce/pole.
        var culture         = GetTemplateCulture();
        var agendaCode      = State.CurrentTemplate.AgendaCode;
        var instances       = _activeInstances.ToList();
        var skupinaId       = _activeSkupinaId;
        var printParams     = new Dictionary<string, object?>(_previewParams, StringComparer.OrdinalIgnoreCase);
        var realPreviewData = _realPreviewData;   // reference swap-only pole - lokální kopie reference stačí
        var suppression     = CurrentNoteSuppression();
        var hideNaZnameni   = CurrentHideNaZnameniLegend();
        var hidePrestup     = CurrentHidePrestupLegend();
        var hiddenContent   = CurrentHiddenContent();
        var columnsDefJson  = CurrentColumnsDefinitionJson();
        var turnusy         = CurrentTurnusyKeSpojum();

        return await Task.Run(() => BuildAllPreviewDataPagesCore(
            culture, agendaCode, instances, skupinaId, printParams, realPreviewData, suppression, false, hideNaZnameni, hidePrestup, progress, hiddenContent, columnsDefJson, turnusy));
    }

    /// <summary>Dvoumístný kód jazyka šablony pro server (`cs` / `sk`) - z kultury, kterou uživatel
    /// vybírá ve vlastnostech stránky. Tou se řídí i symbolový font, takže jazyk textů a piktogramů
    /// má jeden zdroj.</summary>
    internal string JazykSablony => GetTemplateCulture().TwoLetterISOLanguageName.ToLowerInvariant();

    /// <summary>
    /// Parametry pro `GetData` jedné instance = parametry sestavy + klíč instance + skupina linek.
    /// **Jediné místo, kde se tahle mapa staví.** Náhled, export i tisk musí serveru poslat totéž,
    /// jinak vyjede jiný výstup, než ukazuje náhled - a nikdo si toho nevšimne. Přesně tak vznikly
    /// dvě vady naráz: export z lišty neposílal parametry sestavy, tisk zapomínal skupinu linek
    /// (u AD je linka klíčovaná dvojicí skupina+linka, takže se fetchla z default skupiny).
    /// </summary>
    internal static Dictionary<string, object?> ComposeGetDataParams(
        IReadOnlyDictionary<string, object?> printParams, string instanceParamName,
        string instanceValue, string? skupinaId, string? columnsDefinitionJson,
        bool turnusyKeSpojum, string? jazyk)
    {
        var p = new Dictionary<string, object?>(printParams, StringComparer.OrdinalIgnoreCase)
        {
            [instanceParamName] = instanceValue   // instance přebíjí případný klíč linky z parametrů
        };
        if (!string.IsNullOrEmpty(skupinaId)) p["SKUPINA_LINEK_ID"] = skupinaId;
        // Definice vlastních sloupců jde VŽDY čerstvě ze šablony (parametr metody), nikdy přes
        // _previewParams - tam by ji ApplyPersistedPrintParams mohl přepsat starou persistovanou
        // hodnotou a tisklo by se jiné rozvržení, než šablona říká (třída chyb B24).
        // ⚠️ Prázdná hodnota ze šablony musí klíč **odebrat**, ne jen nepřidat - jinak v mapě
        // zůstane persistovaná hodnota z `printParams` a serveru dojde rozvržení, které šablona
        // nemá. Do 4. 9. tu byl jen `if`, takže komentář výše popisoval něco jiného, než kód dělal:
        // uživatel, který si někdy vlastní sloupce nastavil, je dostával i do šablon bez nich -
        // a spoj, který do žádného z nich nesedne, z tabulky tiše zmizí.
        if (!string.IsNullOrEmpty(columnsDefinitionJson)) p["DEFINICE_SLOUPCU"] = columnsDefinitionJson;
        else p.Remove("DEFINICE_SLOUPCU");
        // Jazyk textů značek (#12843) - jde ze šablony, ne z parametrů sestavy, ať ho persistovaná
        // hodnota nemůže přepsat (třída chyb B24). Prázdný = server zůstane na češtině.
        if (!string.IsNullOrEmpty(jazyk)) p["JAZYK"] = jazyk;
        else p.Remove("JAZYK");
        // Turnusy ke spojům jsou volba **prvku**, ne parametr sestavy (turnus je provozní věc
        // dopravce). Serveru to ale dojít musí, protože turnusy nejsou v datech linky a jejich
        // dotaz stojí 40-550 ms - proto touž cestou jako DEFINICE_SLOUPCU: vždy čerstvě ze
        // šablony, mimo persistované hodnoty parametrů (třída chyb B24).
        p["TURNUSY_KE_SPOJUM"] = turnusyKeSpojum ? "true" : "false";
        return p;
    }

    /// <summary>Výchozí sloupce nového prvku odjezdů - vzor „Pracovní / Sobota / Neděle+svátky"
    /// tak, jak ho poslal server pro aktuální linku. Bez načtených dat null = prvek zůstane
    /// v režimu „Automaticky (dle linky)"; vzor složený naslepo by u téhle linky nemusel sedět.</summary>
    private List<DepartureColumnDefinition>? DefaultDepartureColumns()
    {
        var presety = DepartureColumnPresets.Parse(State.CurrentPreviewData?.GetValue("PRESETY_SLOUPCU")?.ToString());
        return presety.TryGetValue("PRAC_SO_NE", out var vzor) && vzor.Count > 0
            ? [.. vzor.Select(c => c.Clone())]
            : null;
    }

    /// <summary>Vzor sloupců předvyplnit jen prvnímu prvku odjezdů v šabloně a jen u zastávkového
    /// JŘ AD (jediná agenda, jejíž server DEFINICE_SLOUPCU čte). Kód agendy je stabilní
    /// persistovaný klíč.</summary>
    private bool ShouldPrefillDepartureColumns() =>
        string.Equals(State.CurrentTemplate.AgendaCode, "ZASTAVKOVY_JR_AD", StringComparison.OrdinalIgnoreCase)
        && !State.CurrentTemplate.Sections.SelectMany(s => s.Elements).OfType<DepartureGridElement>().Any();

    /// <summary>Chce **některý** prvek šablony turnusy ke spojům? Podle toho se posílá serveru
    /// (dotaz do modulu turnusů je drahý, takže se nesmí spustit jinému než tomu, kdo je tiskne).</summary>
    internal bool CurrentTurnusyKeSpojum() =>
        State.CurrentTemplate.Sections.SelectMany(s => s.Elements)
            .OfType<LineTableElement>().Any(e => e.TurnusyKeSpojum);

    /// <summary>Definice vlastních sloupců platná pro celou šablonu - z prvního prvku odjezdů,
    /// který ji má. Null = režim „Automaticky (dle linky)".</summary>
    internal string? CurrentColumnsDefinitionJson() =>
        State.CurrentTemplate.Sections.SelectMany(s => s.Elements)
            .OfType<IColumnLayoutOwner>()
            .Select(e => e.BuildColumnsDefinitionWireJson())
            .FirstOrDefault(j => j is not null);

    // ── Rozvržení sloupců vývěsky: parametr je POHLED na stav šablony, ne druhé nastavení ────────
    //
    // Na otázku „jaké sloupce vzniknou" odpovídala dvě místa naráz - parametr sestavy a vlastní
    // sloupce prvku - a vlastní sloupce parametr tiše přebíjely. Kdo je jednou měl (a nové prvky
    // odjezdů je dostávají předvyplněné), měl z parametru navždy mrtvé tlačítko.
    //
    // Stav drží **jen šablona** (definice sloupců prvku); parametr ji zrcadlí a umí ji přepnout:
    //   sloupce = některý vzor  → popisek toho vzoru      (vzory jsou 1:1 s volbami parametru)
    //   žádné sloupce           → „Automaticky (dle linky)"
    //   ručně upravené          → „Vlastní rozložení" (jen ukazatel, ručně vybrat nejde)

    private const string LayoutParamVyveska = "ROZDELENI_SLOUPCU";
    private const string LayoutParamLinka   = "DELENI_SLOUPCU";
    private const string LayoutAutoOption   = "Automaticky (dle linky)";
    private const string LayoutCustomOption = "Vlastní rozložení";

    /// <summary>Který parametr zrcadlí definici sloupců - liší se podle agendy. Vývěska i linka
    /// mají tutéž mechaniku, jen jinak pojmenovanou volbu.</summary>
    private string LayoutParamName =>
        _agendaParameters.Any(p => p.Name == LayoutParamVyveska) ? LayoutParamVyveska : LayoutParamLinka;

    /// <summary>Popisek, kterým se hlásí „prvek žádnou definici nemá".
    /// ⚠️ **U linky žádný takový popisek není** - její volba dělení má tři konkrétní hodnoty a je
    /// to volba uživatele, ne stav prvku. Zrcadlo jí proto do ní nesahá (null) a hlásí jen to,
    /// když definice existuje. Přepsat uživateli jeho volbu na „automaticky" by byla ta stará
    /// chyba naruby.</summary>
    private string? LayoutAutoOptionProAgendu =>
        LayoutParamName == LayoutParamVyveska ? LayoutAutoOption : null;

    /// <summary>Vzory rozložení nabízené v parametru - klíče musí odpovídat tomu, co posílá server
    /// v poli vzorů, popisky volbám parametru.</summary>
    private IReadOnlyList<DepartureColumnPresets.Preset> LayoutPresety =>
        LayoutParamName == LayoutParamVyveska ? DepartureColumnPresets.All : DepartureColumnPresets.Linkove;

    private bool    _layoutParamSyncing;
    private string? _lastLayoutParamValue;

    /// <summary>Callback do code-behindu: sesyncuj hodnotu jednoho pole v tabu parametrů (bez
    /// `Init()`, který je synchronní round-trip na Caché).</summary>
    public Action<string, string>? RequestParamsFieldSync { get; set; }

    private bool HasLayoutParam => _agendaParameters.Any(p => p.Name == LayoutParamName);

    private string? LayoutParamValue() => _previewParams.GetValueOrDefault(LayoutParamName) as string;

    /// <summary>Prvek odjezdů, jehož definice sloupců platí pro celou šablonu - stejná volba jako
    /// <see cref="CurrentColumnsDefinitionJson"/> (první s definicí, jinak první vůbec).</summary>
    private IColumnLayoutOwner? LayoutOwner()
    {
        var grids = State.CurrentTemplate.Sections.SelectMany(s => s.Elements)
            .OfType<IColumnLayoutOwner>().ToList();
        return grids.FirstOrDefault(e => e.CustomColumns is { Count: > 0 }) ?? grids.FirstOrDefault();
    }

    private IReadOnlyDictionary<string, List<DepartureColumnDefinition>> LoadedColumnPresets() =>
        DepartureColumnPresets.Parse(State.CurrentPreviewData?.GetValue("PRESETY_SLOUPCU")?.ToString());

    /// <summary>Srovná parametr podle toho, co je v šabloně. Tichá cesta - šablonu nemění, jen
    /// hlásí stav. Volat po každé změně definice sloupců i po dotažení dat (vzory chodí s nimi).</summary>
    private void SyncLayoutParamFromTemplate()
    {
        if (!HasLayoutParam || _layoutParamSyncing) return;

        var cols = LayoutOwner()?.CustomColumns;
        var presety = LoadedColumnPresets();
        // Bez načtených vzorů nejde poznat, jestli sloupce některému odpovídají. Radši nesahat,
        // než chvíli hlásit „Vlastní rozložení" a po dotažení dat to zase přepsat.
        if (cols is { Count: > 0 } && presety.Count == 0) return;

        var odvozene = cols is { Count: > 0 }
            ? DepartureColumnPresets.MatchLabel(cols, presety, LayoutPresety) ?? LayoutCustomOption
            : LayoutAutoOptionProAgendu;
        if (odvozene is null) return;   // linka: bez definice se uživatelova volba nechává být
        _lastLayoutParamValue = odvozene;
        if (Equals(LayoutParamValue(), odvozene)) return;

        _layoutParamSyncing = true;
        try
        {
            _previewParams[LayoutParamName] = odvozene;
            foreach (var vm in PreviewParameters) vm.NotifyValueChanged();
            RequestParamsFieldSync?.Invoke(LayoutParamName, odvozene);
            NotifyParamsSummaryChanged();
        }
        finally { _layoutParamSyncing = false; }
    }

    /// <summary>Uživatel vybral rozvržení v parametru → promítnout do šablony jako vratný krok.
    /// Vlastní sloupce se tím přepíšou vzorem (Ctrl+Z je vrátí) - mazání prvku máme taky bez
    /// potvrzení, protože změna je hned vidět a vratná.</summary>
    private void ApplyLayoutParamPick()
    {
        if (!HasLayoutParam || _layoutParamSyncing) return;
        var value = LayoutParamValue();
        if (Equals(value, _lastLayoutParamValue)) return;
        _lastLayoutParamValue = value;

        var owner = LayoutOwner();
        if (owner is null) return;

        // „Vlastní rozložení" je ukazatel, ne volba - vrátí se hodnota odpovídající šabloně.
        if (value == LayoutCustomOption) { SyncLayoutParamFromTemplate(); return; }

        List<DepartureColumnDefinition>? nove = null;
        if (value != LayoutAutoOptionProAgendu)
        {
            var klic = LayoutPresety.FirstOrDefault(p => p.Label == value)?.Key;
            var presety = LoadedColumnPresets();
            if (klic is null || !presety.TryGetValue(klic, out var vzor) || vzor.Count == 0)
            {
                // Vzory chodí až s daty linky - ať to není tichý no-op jako dřív.
                StatusText = "Rozvržení sloupců nejde nastavit, dokud nejsou načtená data linky.";
                SyncLayoutParamFromTemplate();
                return;
            }
            nove = [.. vzor.Select(c => c.Clone())];
        }

        if (DepartureColumnPresets.SameColumns(owner.CustomColumns, nove)) return;
        Execute(new SetPropertyCommand("Rozvržení sloupců", owner.CustomColumns, nove,
            v => owner.CustomColumns = (List<DepartureColumnDefinition>?)v));
    }

    /// <summary>
    /// Přimíchá hodnoty parametrů sestavy do každé stránky - část parametrů vyhodnocuje až C#
    /// renderování a čte je z dat (`HODINY_OD`, `CASOVA_OSA`, `TISK_PROJETYCH_ZAST`…). Musí to udělat
    /// **každá** cesta, která staví stránky pro render, jinak se nakreslí něco jiného než v náhledu.
    /// </summary>
    internal static IReadOnlyList<DataContext> MergeParamsIntoPages(
        IReadOnlyList<DataContext> pages, IReadOnlyDictionary<string, object?> printParams)
    {
        var stringParams = printParams
            .Where(kv => kv.Value is not null)
            .ToDictionary(kv => kv.Key, kv => (object?)kv.Value!.ToString(), StringComparer.OrdinalIgnoreCase);
        return stringParams.Count == 0 ? pages : pages.Select(p => p.WithExtraValues(stringParams)).ToList();
    }

    private IReadOnlyList<DataContext> BuildAllPreviewDataPagesCore(
        CultureInfo culture, string? agendaCode, List<AgendaInstance> instances, string? skupinaId,
        IReadOnlyDictionary<string, object?> printParams,
        IReadOnlyList<DataContext?>? realPreviewData,
        NoteRangeSuppression suppression, bool hideAgg, bool hideNaZnameni, bool hidePrestup,
        IProgress<string>? progress, IReadOnlySet<string>? hiddenContent = null,
        string? columnsDefinitionJson = null, bool turnusyKeSpojum = false)
    {
        List<DataContext>? pages = null;

        // Multi-instance batch: pro každou aktivní instanci fetch GetData a sloučit do flat seznamu.
        // PDF export tak vygeneruje N×K stránek (N instancí × K stránek per instance, např. zastávka×směr).
        if (instances.Count > 0 && CacheProvider is not null && !string.IsNullOrEmpty(agendaCode))
        {
            var paramName = InstanceParamName(agendaCode);
            var all = new List<DataContext>(instances.Count * 4);
            for (int i = 0; i < instances.Count; i++)
            {
                var inst = instances[i];
                progress?.Report($"Načítám data {i + 1}/{instances.Count}: {inst.Display}…");
                var fetched = CacheProvider.GetData(
                    agendaCode, ComposeGetDataParams(printParams, paramName, inst.Value, skupinaId,
                        columnsDefinitionJson, turnusyKeSpojum,
                        culture.TwoLetterISOLanguageName.ToLowerInvariant()));
                if (fetched is null) continue;
                foreach (var p in fetched) all.Add(p.WithCulture(culture));
            }
            if (all.Count > 0) pages = all;
        }

        pages ??= realPreviewData is { Count: > 0 }
            ? realPreviewData.Select(d => d?.WithCulture(culture) ?? new DataContext()).ToList()
            : null;

        // Pro šablony bez DB vazby (GENERAL / nová bez agendy) BuildPreviewMockData vrátí null;
        // export musí přesto projít - vrátíme prázdný DataContext, aby se vyrenderovala
        // jedna stránka s placeholdery beze změny. (Parametry si domíchá sám.)
        if (pages is null) return [BuildPreviewMockData() ?? new DataContext()];

        return NoteVisibilityFilter.Apply(MergeParamsIntoPages(pages, printParams),
            suppression, hideAgg, hideNaZnameni, hidePrestupLegend: hidePrestup, hiddenContent: hiddenContent);
    }

    /// <summary>
    /// Datová pole aktuální agendy pro nabídku „vložit pole" u textových vlastností - skaláry
    /// i pole kolekcí v jednom seznamu, u kolekce se do popisku připíše, odkud pole je.
    /// Uživatel hledá název, nepřemýšlí v kategoriích (rozhodnutí Jan 4. 8.).
    /// </summary>
    public IReadOnlyList<EDControls.ListItem> PlaceholderFieldItems()
    {
        var schema = SchemaProvider.GetSchema(State.CurrentTemplate.AgendaCode ?? "");
        if (schema is null) return [];

        var items = schema.Fields.Select(f => new EDControls.ListItem(f.FieldName, f.Label ?? f.FieldName));
        var fromCollections = (schema.FieldCollections ?? []).SelectMany(c =>
            c.Fields.Select(f => new EDControls.ListItem(f.FieldName, $"{f.Label ?? f.FieldName} ({c.Name})")));

        var zeStranky = PoleStranky.Select(p => new EDControls.ListItem(p.Pole, $"{p.Popis} (stránka)"));
        return [.. items.Concat(fromCollections).Concat(zeStranky).DistinctBy(i => i.Key)];
    }

    /// <summary>Pole, která dosadí až sazba podle stránky (`PageLayoutEngine.HodnotyStranky`).</summary>
    internal static readonly (string Pole, string Popis)[] PoleStranky =
    [
        ("STRANA", "Číslo stránky"),
        ("POCET_STRAN", "Počet stran"),
        ("POKRACOVANI", "Pokračování / opačný směr"),
    ];

    /// <summary>
    /// Sestaví DataContext pro preview. Priorita:
    /// 1. Reálná data z Caché pro vybranou instanci (Instance Picker → _realPreviewData)
    /// 2. Mock data z Caché GetMockData
    /// 3. Syntetická mock data ze schématu
    /// Volitelný parametr overrideParams nahrazuje interní _previewParams (pro PrintDialog).
    /// </summary>
    public DataContext? BuildPreviewMockData(Dictionary<string, object?>? overrideParams = null)
    {
        var agendaCode = State.CurrentTemplate.AgendaCode;
        var culture = GetTemplateCulture();
        DataContext? mockData = null;

        // Priorita 1: reálná data (pokud jsou k dispozici)
        if (_realPreviewData is { Count: > 0 })
        {
            var idx = Math.Clamp(_realPreviewIndex, 0, _realPreviewData.Count - 1);
            mockData = _realPreviewData[idx]?.WithCulture(culture);
        }

        // Priorita 2: mock data z Caché - JEN z cache (nikdy HTTP z kreslicí cesty). Když ještě
        // nejsou načtená, spadne to na syntetická data ze schématu (priorita 3) a prefetch
        // spuštěný při změně agendy je po dokončení doplní + překreslí náhled.
        if (mockData is null && agendaCode is not null && CacheProvider is not null
            && CacheProvider.TryGetCachedMockData(agendaCode, CurrentColumnsDefinitionJson(), JazykSablony, out var cachedMock))
            mockData = cachedMock?.WithCulture(culture);

        // Priorita 3: syntetická mock data ze schématu
        if (mockData is null)
        {
            var schema = SchemaProvider.GetSchema(agendaCode ?? "");
            if (schema is not null)
            {
                var collections = schema.FieldCollections ?? [];
                var values = schema.Fields
                    .Concat(collections.SelectMany(c => c.Fields))
                    .DistinctBy(f => f.FieldName)
                    .ToDictionary(f => f.FieldName, f => (object?)(f.Example ?? MockDefault(f)));

                var rowsDict = new Dictionary<string, IReadOnlyList<DataContext>>();
                foreach (var collection in collections)
                {
                    var mockRows = Enumerable.Range(1, 3).Select(i =>
                    {
                        var rowData = collection.Fields.ToDictionary(
                            f => f.FieldName,
                            f => (object?)MakeMockValue(f.Example ?? MockDefault(f), f.DataType, i));
                        return new DataContext(rowData, culture: culture);
                    }).ToList();
                    rowsDict[collection.CollectionKey] = mockRows;
                }

                mockData = new DataContext(values, rowsDict, culture);
            }
        }

        // Použij overrideParams (z PrintDialogu) nebo interní _previewParams
        var paramValues = overrideParams;
        if (paramValues is null && _previewParams.Count > 0)
        {
            _cachedParamValues ??= _previewParams.ToDictionary(
                kv => kv.Key,
                kv => (object?)kv.Value?.ToString(),
                StringComparer.OrdinalIgnoreCase);
            paramValues = _cachedParamValues;
        }

        if (paramValues is { Count: > 0 })
        {
            var stringParams = paramValues.ToDictionary(
                kv => kv.Key,
                kv => (object?)kv.Value?.ToString(),
                StringComparer.OrdinalIgnoreCase);

            mockData = mockData is not null
                ? mockData.WithExtraValues(stringParams)
                : new DataContext(stringParams, culture: culture);
        }

        var src = overrideParams ?? _previewParams;
        var suppressionMode = NoteVisibilityFilter.ParseMode(src.GetValueOrDefault(NoteVisibilityFilter.ParamName));
        if (mockData is null) return null;

        return NoteVisibilityFilter.Apply(mockData, suppressionMode, false, CurrentHideNaZnameniLegend(),
            CurrentHidePrestupLegend(), CurrentHiddenContent());
    }

    /// <summary>Režim schování poznámek v rozsahu „nepoznámkovat" z aktuálního parametru sestavy.</summary>
    private NoteRangeSuppression CurrentNoteSuppression() =>
        NoteVisibilityFilter.ParseMode(_previewParams.GetValueOrDefault(NoteVisibilityFilter.ParamName));

    /// <summary>Kategorie obsahu vypnuté v šabloně (kola, místenky, souhrn bezbariér) - dřív tři
    /// parametry sestavy. Zapíná se to na prvku, který obsah kreslí, aby to bylo na jednom místě.</summary>
    private IReadOnlySet<string> CurrentHiddenContent() =>
        NoteVisibilityFilter.HiddenContentCategories(State.CurrentTemplate);

    /// <summary>Skrýt legendu „× na znamení" když StopList netiskne značku (#7161 D5) - ze šablony.</summary>
    private bool CurrentHideNaZnameniLegend() =>
        NoteVisibilityFilter.ShouldHideNaZnameniLegend(State.CurrentTemplate);

    /// <summary>Skrýt legendu přestupů, když je žádný StopList netiskne - ze šablony (#13048 bod 1).</summary>
    private bool CurrentHidePrestupLegend() =>
        NoteVisibilityFilter.ShouldHidePrestupLegend(State.CurrentTemplate);

    /// <summary>Výchozí mock hodnota podle typu pole - použije se když Example chybí.</summary>
    private static string MockDefault(DataField f) => f.DataType switch
    {
        FieldDataType.Number   => "100",
        FieldDataType.Date     => "01.01.2024",
        FieldDataType.Time     => "08:00",
        FieldDataType.DateTime => "01.01.2024 08:00",
        FieldDataType.Boolean  => "true",
        _                      => f.Label  // text - fallback na label
    };

    private static string MakeMockValue(string baseValue, FieldDataType type, int rowIndex)
    {
        if (rowIndex <= 1) return baseValue;
        return type switch
        {
            FieldDataType.Number => int.TryParse(baseValue, out var n) ? (n + rowIndex - 1).ToString() : baseValue,
            FieldDataType.Time   => TimeSpan.TryParse(baseValue, out var t)
                ? (t + TimeSpan.FromMinutes(10 * (rowIndex - 1))).ToString(@"hh\:mm") : baseValue,
            FieldDataType.Date   => DateTime.TryParse(baseValue, out var d)
                ? d.AddDays(rowIndex - 1).ToString("dd.MM.yyyy") : baseValue,
            _ => string.IsNullOrEmpty(baseValue) ? baseValue : $"{baseValue} #{rowIndex}"
        };
    }

    // ── Available fields (for ElementPropertiesWindow) ────────────────────────
    public IReadOnlyList<DataField>? GetAvailableFields()
    {
        var schemaFields = SchemaProvider.GetSchema(State.CurrentTemplate.AgendaCode ?? "")?.Fields;
        if (schemaFields is null && _agendaParameters.Count == 0) return null;
        var paramFields  = _agendaParameters.Select(p => new DataField
            { FieldName = p.Name, Label = p.Label + " (parametr)", DataType = FieldDataType.Text });
        return [.. (schemaFields ?? []), .. paramFields];
    }

    public IReadOnlyList<DataFieldCollection>? GetAvailableGroups()
        => SchemaProvider.GetSchema(State.CurrentTemplate.AgendaCode ?? "")?.FieldCollections;

    // ── Status text for selection ──────────────────────────────────────────────

    /// <summary>Aktualizuje SelectionStatusText podle aktuálního výběru (klidová zóna status baru).</summary>
    // Jen kontextová informace o výběru - pevné nápovědy („2× klik = vlastnosti") sem nepatří,
    // ovládání je v nápovědě F1 (rozhodnutí Jan 24. 9.). Názvy prvků jako v paletě.
    public void UpdateStatusForSelection()
    {
        if (State.SelectedElements.Count == 1)
        {
            var el = State.SelectedElements[0];
            string vyska = el.Height is { } h ? $"{h:0.#} mm" : "výška podle obsahu";
            SelectionStatusText = $"{ElementNames.SPopisem(el)}  ·  X {el.X:0.#}  Y {el.Y:0.#} mm  ·  {el.Width:0.#} mm × {vyska}"
                + (el.IsLocked ? "  ·  Uzamčeno" : "");
        }
        else if (State.SelectedElements.Count > 1)
        {
            int n = State.SelectedElements.Count;
            SelectionStatusText = $"Vybráno {n} {(n <= 4 ? "prvky" : "prvků")}";
        }
        else if (State.ActiveSection is { } sekce)
        {
            string typ = sekce.Typ switch
            {
                TypSekce.HlavickaStranky   => "hlavička stránky",
                TypSekce.PodObsahemStranky => "pod obsahem stránky",
                TypSekce.PatickaStranky    => "patička stránky",
                _                          => "obsah",
            };
            string vyska = sekce.DesignerHeight is { } h ? $"výška {h:0.#} mm" : "výška podle obsahu";
            int n = sekce.Elements.Count;
            SelectionStatusText = $"Sekce „{sekce.Name}“  ·  {typ}  ·  {vyska}  ·  {n} "
                + (n == 1 ? "prvek" : n is >= 2 and <= 4 ? "prvky" : "prvků");
        }
        else
        {
            SelectionStatusText = "";
        }
    }

    // ── Tree selection ──────────────────────────────────────────────────────────

    /// <summary>Zpracuje výběr z template tree - element, skupina nebo sekce (dříve v code-behind).</summary>
    public void SelectFromTree(object? tag)
    {
        if (tag is TemplateElement element)
        {
            var section = State.CurrentTemplate.Sections.FirstOrDefault(s => s.Elements.Contains(element));
            if (section is not null)
            {
                ActivateSection(section);
                State.SelectedElements.Clear();
                State.SelectedElements.Add(element);
            }
        }
        else if (tag is string groupId)
        {
            var section = State.CurrentTemplate.Sections.FirstOrDefault(s => s.Elements.Any(el => el.GroupId == groupId));
            if (section is not null)
            {
                ActivateSection(section);
                State.SelectedElements.Clear();
                State.SelectedElements.AddRange(section.Elements.Where(el => el.GroupId == groupId));
            }
        }
        else if (tag is Section section)
        {
            ActivateSection(section);
            State.SelectedElements.Clear();
        }
        else return;

        UpdateStatusForSelection();
        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    // ── Schema field insert ─────────────────────────────────────────────────────

    /// <summary>Vloží placeholder do vybraného TextElementu nebo zkopíruje do schránky (dříve v code-behind).</summary>
    public void InsertOrCopyPlaceholder(string placeholder)
    {
        if (State.SelectedElements.Count == 1 && State.SelectedElements[0] is TextElement textEl)
        {
            string oldText = textEl.Text;
            string newText = oldText + placeholder;
            Execute(new SetPropertyCommand(
                $"Vložit {placeholder}",
                oldText, newText,
                v => textEl.Text = (string)v!));
            RequestCanvasRefresh?.Invoke();
            RequestPropertiesPanelUpdate?.Invoke();
        }
        else
        {
            Dialogs?.SetClipboardText(placeholder);
            StatusText = $"Zkopírováno do schránky: {placeholder}";
        }
    }

    // ── Element drop (adding) ───────────────────────────────────────────────────

    /// <summary>Připojí placeholder k existujícímu TextElementu (field drag na element).</summary>
    public void AppendPlaceholderToElement(TextElement textEl, string fieldName)
    {
        string placeholder = $"{{{fieldName}}}";
        string oldText = textEl.Text;
        string newText = oldText + placeholder;
        Execute(new SetPropertyCommand(
            $"Vložit {placeholder}",
            oldText, newText,
            v => textEl.Text = (string)v!));

        var section = State.CurrentTemplate.Sections.FirstOrDefault(s => s.Elements.Contains(textEl));
        if (section is not null) ActivateSection(section);
        State.SelectedElements.Clear();
        State.SelectedElements.Add(textEl);
        UpdateStatusForSelection();
        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    /// <summary>Vytvoří a umístí nový element na danou pozici v sekci (dříve v EndAddingElement code-behind).</summary>
    public void AddElementAtPosition(Section targetSection, float mmX, float mmY, string? elementType, string? fieldName)
    {
        var (defW, defH) = GetDefaultElementSize(elementType ?? ElementTypes.Text);

        // Centrovat element na pozici kurzoru
        mmX -= defW / 2f;
        mmY -= defH / 2f;

        // Clamp do hranic sekce
        float sectionW = State.CurrentTemplate.PageSize.PrintableWidth;
        float sectionH = targetSection.EffectiveDesignerHeight;
        mmX = Math.Clamp(mmX, 0, Math.Max(0, sectionW - defW));
        mmY = Math.Clamp(mmY, 0, Math.Max(0, sectionH - defH));

        // Snap na mřížku
        if (State.SnapToGrid)
        {
            float g = State.GridSize;
            mmX = MathF.Round(mmX / g) * g;
            mmY = MathF.Round(mmY / g) * g;
        }

        // Vytvoření elementu
        TemplateElement element;
        if (fieldName is not null)
        {
            // Přes CreateElementByType, ať DB-pole dostane stejné defaulty jako element z palety
            // (výchozí rámeček, rozměry) - dřív se tvořil holý TextElement bez rámečku.
            var textEl = (TextElement)CreateElementByType(ElementTypes.Text, mmX, mmY);
            textEl.Text = $"{{{fieldName}}}";
            element = textEl;
        }
        else
        {
            // Pevná tabulka se cestou z palety zeptá na rozměr; zrušení = nevkládat nic.
            if (CreateElementInteractive(elementType!, mmX, mmY) is not { } created) return;
            element = created;
            // Výchozí vzor sloupců (rozhodnutí Jan 10. 8.: nové jedou z návrháře, linka je backup)
            // dostane jen PRVNÍ prvek odjezdů v šabloně a jen u agendy, jejíž server definici čte -
            // druhý prvek by definicí tiše přepnul data celé šablony a MHD by v dialogu tvrdila
            // vlastní sloupce, které její server ignoruje. PDF import vzor nedostává (zrcadlí
            // rozvržení importované vývěsky).
            if (element is DepartureGridElement grid && ShouldPrefillDepartureColumns())
                grid.CustomColumns = DefaultDepartureColumns();
        }

        // Snap i rozměrů - výchozí velikosti (80×8, 190×25…) nejsou násobky kroku mřížky,
        // takže bez tohoto spodní/pravá hrana nikdy neseděla na mřížce a element působil
        // „mimo grid" hned po přidání. Zaokrouhlení na 0 (element menší než krok) → původní rozměr.
        if (State.SnapToGrid)
        {
            float g = State.GridSize;
            static float SnapSize(float v, float step)
            {
                float snapped = MathF.Round(v / step) * step;
                return snapped > 0 ? snapped : v;
            }
            element.Width = SnapSize(element.Width, g);
            if (element.Height is { } h) element.Height = SnapSize(h, g);
        }

        Execute(new AddElementCommand(targetSection, element));
        ActivateSection(targetSection);
        State.SelectedElements.Clear();
        State.SelectedElements.Add(element);
        UpdateStatusForSelection();
        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    // ── Hit handling (element selection logic from mouse down) ────────────────

    /// <summary>Zpracuje klik na element - výběr/toggle s Ctrl/Alt/Group logikou (dříve v OnCanvasMouseDown).</summary>
    public void HandleElementHit(Section section, TemplateElement element, bool ctrl, bool alt)
    {
        if (ctrl)
        {
            // Ctrl+klik: toggle element (nebo celou skupinu)
            if (element.GroupId is not null && !alt)
            {
                var groupMembers = section.Elements.Where(e => e.GroupId == element.GroupId).ToList();
                if (State.SelectedElements.Contains(element))
                    foreach (var m in groupMembers) State.SelectedElements.Remove(m);
                else
                    foreach (var m in groupMembers.Where(m2 => !State.SelectedElements.Contains(m2)))
                        State.SelectedElements.Add(m);
            }
            else
            {
                if (State.SelectedElements.Contains(element))
                    State.SelectedElements.Remove(element);
                else
                    State.SelectedElements.Add(element);
            }
        }
        else if (!State.SelectedElements.Contains(element))
        {
            State.SelectedElements.Clear();
            // Klik na člen skupiny → vyber celou skupinu (Alt = jen jeden element)
            if (element.GroupId is not null && !alt)
            {
                var groupMembers = section.Elements.Where(e => e.GroupId == element.GroupId).ToList();
                State.SelectedElements.AddRange(groupMembers);
            }
            else
            {
                State.SelectedElements.Add(element);
            }
        }

        ActivateSection(section);
    }

    /// <summary>Zpracuje klik do prázdné oblasti - zruší výběr (pokud ne Ctrl), aktivuje sekci.</summary>
    public void HandleEmptyAreaHit(Section? section, bool ctrl)
    {
        if (!ctrl)
            State.SelectedElements.Clear();
        ActivateSection(section);
        UpdateStatusForSelection();
    }

    // ── Sample template ───────────────────────────────────────────────────────
    /// <summary>Fallback startovní šablona - agendu typicky přepíše EDISON kontrakt nebo
    /// poslední použitá agenda uživatele (viz ctor).</summary>
    /// <remarks>
    /// Výchozí je <b>linkový JŘ (AD)</b> - pracuje se na něm (vráceno 3. 9., rozhodnutí Jan; od 1. 9.
    /// tu byl kvůli vydání zastávkový).
    ///
    /// ⚠️ **Musí to být zveřejněná agenda.** Když se agenda skryje před vydáním, tahle konstanta
    /// skrytí obchází: nová šablona nastartuje na skryté agendě, ta se stane aktuální agendou
    /// a výběr ji pak ukáže, protože aktuální agendu zobrazuje vždy (aby šly otevřít hotové
    /// šablony). Přesně takhle uživatel linkové JŘ viděl dál, i když byly skryté (nález Jan 1. 9.).
    /// **Skrývá-li se agenda, musí se změnit i tady** - hlídá `SkrytaAgendaTests`.
    ///
    /// ⚠️ Uplatní se jen tam, kde není co obnovit: **poslední použitá agenda uživatele má přednost**
    /// (viz ctor), takže kdo si už jednou vybral jinou, dostane po startu tu svoji.
    /// </remarks>
    public static Template CreateSampleTemplate()
    {
        var t = new Template { Name = "Nová šablona", AgendaCode = "LINKOVY_JR_AD" };
        t.Sections.AddRange(CreateDefaultSections(t.PageSize));
        return t;
    }

    /// <summary>Výška výchozí sekce záhlaví (mm). Sekce roste s obsahem, tohle je jen startovní
    /// plocha, aby do ní šlo něco položit.</summary>
    private const float VychoziVyskaZahlavi = 30f;

    /// <summary>
    /// Výchozí sekce nové šablony: <b>záhlaví a obsah zvlášť</b> - sdílené novou i sample šablonou.
    /// </summary>
    /// <remarks>
    /// ⚠️ **Proč dvě a ne jedna.** Prvky uvnitř sekce se navzájem <b>neposouvají</b>, takže text,
    /// který roste s daty (výčet dopravců), přetiskne tabulku pod sebou. Změřeno 25. 8.: záhlaví
    /// s 26 dopravci leželo přes řádek se zastávkami; přesně na to si stěžuje KORID v #9417.
    /// Sekce se naopak posouvají správně - ta pod rostoucí se odsune o její výšku - takže rozdělení
    /// na dvě sekce ten překryv řeší samo, bez zásahu do stránkování.
    /// </remarks>
    private static List<Section> CreateDefaultSections(PageSize pageSize) =>
    [
        new()
        {
            Name = "Záhlaví",
            PrintBehavior = SectionPrintBehavior.Once,
            OverflowBehavior = SectionOverflowBehavior.SplitAcrossPages,
            DesignerHeight = VychoziVyskaZahlavi
        },
        new()
        {
            Name = "Obsah",
            PrintBehavior = SectionPrintBehavior.Once,
            OverflowBehavior = SectionOverflowBehavior.SplitAcrossPages,
            DesignerHeight = MathF.Max(20f, pageSize.PrintableHeight - VychoziVyskaZahlavi)
        }
    ];
}

// ─────────────────────────────────────────────────────────────────────────────
// SectionVM
// ─────────────────────────────────────────────────────────────────────────────

public class SectionVM : INotifyPropertyChanged
{
    private readonly TemplateDesignerViewModel _vm;
    public Section Section { get; }

    /// <summary>Global callback for showing section properties dialog (wired by code-behind).</summary>
    public static Action<Section>? ShowPropertiesGlobal   { get; set; }
    public static Action<Section>? SaveTemplateGlobal     { get; set; }

    public SectionVM(Section section, TemplateDesignerViewModel vm)
    {
        Section = section;
        _vm     = vm;
    }

    public string  Name      => Section.Name;
    /// <summary>„Strana 2" (nebo „Na každé stránce") nad první sekcí oddílu; jinak null.</summary>
    public string? ZahlaviStranky { get; init; }
    public bool    MaZahlaviStranky => ZahlaviStranky is not null;
    /// <summary>Stránka tohoto nadpisu je aktivní - sem jde „Přidat sekci".</summary>
    public bool    ZahlaviAktivni { get; init; }
    /// <summary>Pod poslední sekcí stránky: hlavička a patička, které se na ni tisknou navíc.</summary>
    public string? SpolecneNaStrance { get; init; }
    public bool    MaSpolecneNaStrance => SpolecneNaStrance is not null;
    public bool    IsDynamic => Section.DesignerHeight is null;
    public bool    IsActive  => _vm.State.ActiveSection == Section;
    public bool    HasSubtitle => Subtitle is not null;

    public string? Subtitle
    {
        get
        {
            string? sub = null;
            if (Section.PrintBehavior != SectionPrintBehavior.Once)
                sub = Section.PrintBehavior switch
                {
                    SectionPrintBehavior.OnEveryPage            => "každá stránka",
                    SectionPrintBehavior.OnFirstPageOnly        => "jen 1. stránka",
                    SectionPrintBehavior.OnLastPageOnly         => "jen posl. stránka",
                    SectionPrintBehavior.OnEveryPageExceptFirst => "kromě 1. stránky",
                    _ => null
                };
            if (Section.PrintBehavior == SectionPrintBehavior.PerDataRow && !string.IsNullOrEmpty(Section.DataSource))
                sub = (sub is null ? "" : sub + " · ") + Section.DataSource;
            return sub;
        }
    }

    public bool CanMoveUp   => _vm.State.CurrentTemplate.Sections.IndexOf(Section) > 0;
    public bool CanMoveDown => _vm.State.CurrentTemplate.Sections.IndexOf(Section)
                               < _vm.State.CurrentTemplate.Sections.Count - 1;

    private RelayCommand? _moveUp, _moveDown, _delete, _saveTemplate, _activate, _showProps;

    public RelayCommand MoveUpCommand       => _moveUp       ??= new(() => _vm.MoveSection(Section, -1),   () => CanMoveUp);
    public RelayCommand MoveDownCommand     => _moveDown     ??= new(() => _vm.MoveSection(Section, +1),   () => CanMoveDown);
    public RelayCommand DeleteCommand       => _delete       ??= new(() => _vm.DeleteSection(Section));
    public RelayCommand SaveTemplateCommand => _saveTemplate ??= new(() => SaveTemplateGlobal?.Invoke(Section));
    public RelayCommand ActivateCommand     => _activate     ??= new(() => _vm.ActivateSection(Section));
    public RelayCommand ShowPropertiesCommand => _showProps  ??= new(() => ShowPropertiesGlobal?.Invoke(Section));

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// ─────────────────────────────────────────────────────────────────────────────
// Schema ViewModels
// ─────────────────────────────────────────────────────────────────────────────

public class SchemaGroupVM
{
    public string                     Header { get; }
    public IReadOnlyList<SchemaFieldVM> Fields { get; }

    public SchemaGroupVM(string header, IEnumerable<SchemaFieldVM> fields)
    {
        Header = header;
        Fields = [.. fields];
    }
}

public class SchemaFieldVM
{
    public string FieldName   { get; }
    public string Label       { get; }
    public string Placeholder => $"{{{FieldName}}}";
    public string LabelDisplay => string.IsNullOrEmpty(Label) ? "" : "  " + Label;

    public SchemaFieldVM(string fieldName, string label)
    {
        FieldName = fieldName;
        Label     = label;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// ParameterControlVM
// ─────────────────────────────────────────────────────────────────────────────

public class ParameterControlVM : INotifyPropertyChanged
{
    private readonly TemplateParameter           _param;
    private readonly Dictionary<string, object?> _values;
    private readonly Action?                     _onChanged;
    private readonly Action?                     _onCommit;
    private bool                                 _isVisible = true;

    public ParameterControlVM(TemplateParameter param, Dictionary<string,object?> values,
        Action? onChanged, Action? onCommit = null)
    {
        _param     = param;
        _values    = values;
        _onChanged = onChanged;
        _onCommit  = onCommit;
    }

    /// <summary>
    /// Naplní kolekci VM pro daný seznam parametrů + sdílený <paramref name="values"/> slovník.
    /// Auto-zapojuje re-vyhodnocení viditelnosti všech VM po každé změně (volá se PŘED <paramref name="onChanged"/>)
    /// a provede initial visibility check po naplnění. Sjednocuje wiring mezi preview lištou (MainWindow) a PrintDialog.
    /// </summary>
    /// <param name="target">Cílová kolekce (typicky ObservableCollection napojená na ItemsControl).</param>
    /// <param name="parameters">Definice parametrů agendy/šablony.</param>
    /// <param name="values">Slovník hodnot - sdílený mezi VM (každá změna ho aktualizuje).</param>
    /// <param name="onChanged">Volitelný callback po změně hodnoty (např. invalidace cache + překreslení).</param>
    /// <param name="commitFactory">Volitelný factory - pro každý parametr může vrátit commit akci (Načíst tlačítko).</param>
    public static void Init(
        ICollection<ParameterControlVM>      target,
        IEnumerable<TemplateParameter>       parameters,
        Dictionary<string, object?>          values,
        Action?                              onChanged     = null,
        Func<TemplateParameter, Action?>?    commitFactory = null)
    {
        target.Clear();
        foreach (var p in parameters)
        {
            // Auto-refresh visibility všech VM před externím callbackem - VisibleWhen rule ve VisualWhen
            // se musí přepočítat dřív, než UI dostane signál k překreslení.
            Action vmOnChanged = () =>
            {
                foreach (var vm in target) vm.UpdateVisibility();
                onChanged?.Invoke();
            };
            target.Add(new ParameterControlVM(p, values, vmOnChanged, commitFactory?.Invoke(p)));
        }

        // Initial visibility check - proběhne až po naplnění kolekce, aby VisibleWhen vidělo všechny VM.
        foreach (var vm in target) vm.UpdateVisibility();
    }

    public string                    Label         => _param.Label;
    public string                    ParameterType => _param.Type;
    public IReadOnlyList<string>     Options       => _param.Options ?? [];
    /// <summary>Skupina pro CollectionViewSource grouping v liště parametrů (prázdné = bez skupiny).</summary>
    public string                    Group         => _param.Group ?? "";

    /// <summary>Hodnota parametru - setter volá onChanged (live refresh) při každé změně.</summary>
    public object? Value
    {
        get => _values.TryGetValue(_param.Name, out var v) ? v : _param.GetDefaultTyped();
        set { _values[_param.Name] = value; OnPropertyChanged(); _onChanged?.Invoke(); }
    }

    /// <summary>
    /// Notifikuje UI binding po externí změně <c>_values[Name]</c> (např. po loaded PrintParams).
    /// Bez volání setteru se neaktivuje OnPropertyChanged - proto explicit notifikace.
    /// </summary>
    public void NotifyValueChanged() => OnPropertyChanged(nameof(Value));

    /// <summary>Viditelnost parametru - řízena dependency rule VisibleWhen z TemplateParameter.</summary>
    public bool IsVisible
    {
        get => _isVisible;
        private set { if (_isVisible != value) { _isVisible = value; OnPropertyChanged(); } }
    }

    /// <summary>Vyhodnotí VisibleWhen rule proti aktuálním hodnotám. Volá se po každé změně libovolného parametru.</summary>
    public void UpdateVisibility() => IsVisible = _param.IsVisible(_values);

    /// <summary>Má se volat při potvrzení hodnoty (Enter/LostFocus) - spustí těžší operace (reload dat).</summary>
    public bool HasCommit => _onCommit is not null;

    private RelayCommand? _commitCmd;
    public RelayCommand CommitCommand => _commitCmd ??= new(() => _onCommit?.Invoke());

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// Selector přesunut do Controls/ParameterTemplateSelector.cs (sdílený mezi MainWindow a PrintDialog).
