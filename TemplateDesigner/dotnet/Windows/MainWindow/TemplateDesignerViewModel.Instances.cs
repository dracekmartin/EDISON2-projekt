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

/// <summary>Parametry náhledu a výběr instancí (linek), včetně rozbalovacího seznamu vybraných instancí.</summary>
public partial class TemplateDesignerViewModel
{
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
}
