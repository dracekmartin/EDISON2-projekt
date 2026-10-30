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

/// <summary>Nabídka dat a parametry sestavy, formulář parametrů v levém panelu a ukázková data náhledu.</summary>
public partial class TemplateDesignerViewModel
{
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
            AgendaNameText = $"Agenda: {AgendaEntry(code)?.DisplayName ?? code} (načítám…)";
            return Task.Run(() => SchemaProvider.GetSchema(code))
                .ContinueWith(_ =>
                {
                    // Agenda se mezitím změnila: její parametry už nastavilo novější volání a pozdní
                    // výsledek by tam vrátil parametry té předchozí.
                    if (string.Equals(State.CurrentTemplate.AgendaCode, code, StringComparison.OrdinalIgnoreCase))
                        RefreshFieldsListCore(code);
                }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        RefreshFieldsListCore(agendaCode);
        return Task.CompletedTask;
    }

    private void RefreshFieldsListCore(string agendaCode)
    {
        var schema = SchemaProvider.GetSchema(agendaCode);   // po prewarmu už cached → rychlé

        if (schema is null)
        {
            AgendaNameText = AgendaBezSchematuText(AgendaEntry(agendaCode), agendaCode);
            UpdateCacheStatus(agendaCode, null);
            LoadAgendaParameters(agendaCode);
            RebuildSchemaTree(null);
            return;
        }

        // Týž tvar jako titulek okna a Uložit jako: „Agenda: Zastávkový JŘ · AD" (Jan 27. 10.).
        // Fallback = jméno ze schématu (registr nezná kód).
        var entry = AgendaEntry(agendaCode);
        AgendaNameText = $"Agenda: {entry?.DisplayName ?? schema.AgendaName}";
        var cacheSchema = CacheProvider?.GetSchema(agendaCode);
        UpdateCacheStatus(agendaCode, cacheSchema);
        LoadAgendaParameters(agendaCode);
        RebuildSchemaTree(schema);
        _ = PrefetchMockDataAsync(agendaCode);
    }

    /// <summary>Stavový řádek agendy, od které nepřišlo schéma. Agenda bez třídy na serveru (Obecná)
    /// ho nemá z principu - „nedostupná" by u ní hlásilo výpadek, který není.</summary>
    internal static string AgendaBezSchematuText(Serialization.AgendaRegistryEntry? entry, string agendaCode) =>
        entry is { CacheClass: "" }
            ? $"Agenda: {entry.DisplayName}"
            : $"Agenda: {entry?.DisplayName ?? agendaCode} (nedostupná)";

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
            if (p.PerInstance) continue;   // patří vybrané lince, ne předvolbě (starší uložená hodnota se nepoužije)
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
            if (p.PerInstance) continue;
            if (!tp.TryGetValue(p.Name, out var s) || s is null) continue;
            _previewParams[p.Name] = ParseParamValue(p, s);
        }
        // Přenačtení panelu/tabu z finálních hodnot dělá otevírací flow (RefreshParamsUiAfterLoad),
        // bezpodmínečně - per-agenda i šablona; sem to nedávat (nespustilo by se, když šablona
        // nic nezměnila, protože per-agenda už tytéž hodnoty nastavila → tab by zůstal na defaultech).
    }

    /// <summary>Textová hodnota parametru (šablona, spuštění) v typu, se kterým pracuje náhled.</summary>
    private static object? ParseParamValue(TemplateParameter p, string s) => p.Type switch
    {
        "bool"   => s == "1" || string.Equals(s, "true", StringComparison.OrdinalIgnoreCase),
        "number" => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : null,
        _        => s
    };

    /// <summary>
    /// Jediná cesta k aplikaci persistovaných hodnot parametrů: per-agenda → šablona → spuštění
    /// z modulu JŘ → přenačtení UI. **Volat vždy tuhle metodu, nikdy ty kroky zvlášť.** Když se jeden krok vynechá (dřív to
    /// dělal cold-start fallback po dokončení načtení preferencí), hodnoty jdou do `GetData`, ale
    /// ovladače v UI zůstanou na defaultech - uživatel pak tiskne něco jiného, než co v nastavení vidí
    /// a nemá jak na to přijít. Přesně tak tiše rozdělovalo vývěsky `ROZDELIT_PER_OZNACNIK=true`.
    /// </summary>
    public async Task ApplyPersistedPrintParamsAsync()
    {
        await LoadPrintParamsAndApplyAsync();
        ApplyTemplatePrintParamsToPreview();
        ApplyLaunchParamsToPreview();
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

    /// <summary>Parametry vázané na vybranou instanci (<see cref="TemplateParameter.PerInstance"/>) vrátí
    /// na výchozí - jinak by srovnávací linka z minula ukazovala na jinou trasu (rozdílový JŘ).</summary>
    internal bool VynulujParametryInstance()
    {
        bool zmena = false;
        foreach (var p in _agendaParameters.Where(p => p.PerInstance))
        {
            var vychozi = p.GetDefaultTyped();
            if (Equals(_previewParams.GetValueOrDefault(p.Name), vychozi)) continue;
            _previewParams[p.Name] = vychozi;
            zmena = true;
        }
        if (zmena) _cachedParamValues = null;
        return zmena;
    }

    /// <summary>Uloží aktuální <see cref="_previewParams"/> pro tuto agendu (fire-and-forget).</summary>
    public Task SavePrintParamsAsync()
    {
        if (Preferences is null) return Task.CompletedTask;
        var agendaCode = State.CurrentTemplate.AgendaCode;
        if (string.IsNullOrEmpty(agendaCode)) return Task.CompletedTask;
        // Rozvržení sloupců je vlastnost ŠABLONY (odvozuje se z definice sloupců prvku), ne
        // předvolba agendy - uložené napříč šablonami by u každé další lhalo.
        var perInstance = _agendaParameters.Where(p => p.PerInstance).Select(p => p.Name).ToHashSet();
        var perAgenda = _previewParams.Where(kv => kv.Key != LayoutParamName && !perInstance.Contains(kv.Key))
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
            commitFactory: _ => null,
            template:   () => State.CurrentTemplate);

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
        _launchParams = null;     // od teď platí, co nastavil uživatel
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

    /// <summary>visibleWhen parametru proti aktuálním hodnotám, šabloně a načteným datům instance
    /// (jeden zdroj - TemplateParameter.IsVisible).</summary>
    public bool IsAgendaParamVisible(string name) =>
        _agendaParameters.FirstOrDefault(x => x.Name == name)?.IsVisible(_previewParams, State.CurrentTemplate, _realPreviewData) ?? true;

    /// <summary>Callback do code-behindu: přepočítej viditelnost polí v tabu parametrů. Volá se po
    /// každé změně šablony - zapnutí sloupce v prvku může ukázat nebo schovat parametr.</summary>
    public Action? RequestParamsVisibilityRefresh { get; set; }

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

    /// <summary>Vzor sloupců předvyplnit jen prvnímu prvku odjezdů v šabloně a jen u agendy, jejíž
    /// server DEFINICE_SLOUPCU čte (hlásí to registr agend).</summary>
    private bool ShouldPrefillDepartureColumns() =>
        AgendaEntry(State.CurrentTemplate.AgendaCode)?.CustomColumns == true
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
}
