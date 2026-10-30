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

/// <summary>Sloupce tabulkových prvků: použití výsledků editorů sloupců a rozvržení sloupců vývěsky (parametr jako pohled na stav šablony).</summary>
public partial class TemplateDesignerViewModel
{
    // ── Aplikace výsledků editorů sloupců (dialog otevírá code-behind; sestavení
    //    a vykonání příkazů = business logika, patří sem do VM). Metody neznají WPF. ──

    /// <summary>Odjezdová mřížka: tab „Obecné" (EDInputForm) + per-column styly + styl sloupce hodin
    /// jako jeden undo krok.</summary>
    public void ApplyDepartureGridEdit(DepartureGridElement element, IDesignerCommand? generalCommand,
        List<DepartureGroupColumnStyle> resultGroupColumns,
        bool hasGroups, ColumnVisualStyle? resultHourColumnStyle,
        string hourColumnHeader, float hourColumnWidth,
        List<DepartureColumnDefinition>? resultCustomColumns)
    {
        var cmds = new List<IDesignerCommand>();
        if (generalCommand is not null) cmds.Add(generalCommand);
        AddIfChanged(cmds, "Záhlaví hodin", element.HourColumnHeader, hourColumnHeader,
            v => element.HourColumnHeader = (string)v!);
        AddIfChanged(cmds, "Šířka hodin", element.HourColumnWidth, hourColumnWidth,
            v => element.HourColumnWidth = (float)v!);
        if (hasGroups)
            AddIfChanged(cmds, "Skupiny dnů", element.GroupColumns, resultGroupColumns,
                v => element.GroupColumns = (List<DepartureGroupColumnStyle>)v!);
        AddIfChanged(cmds, "Styl sloupce hodin", element.HourColumnStyle, resultHourColumnStyle,
            v => element.HourColumnStyle = (ColumnVisualStyle?)v);
        AddIfChanged(cmds, "Vlastní sloupce", element.CustomColumns, resultCustomColumns,
            v => element.CustomColumns = (List<DepartureColumnDefinition>?)v);
        // Re-fetch i srovnání parametru rozvržení řeší Execute (RefetchIfColumnsDefinitionChanged).
        ExecuteAsComposite("Nastavení odjezdů", cmds);
    }

    /// <summary>Zvýraznění řádků (seznam pravidel) - jeden undoable krok. Prázdný seznam → null (čistý JSON).</summary>
    public void ApplyHighlightRulesEdit(IRowHighlightable element, List<HighlightRule> resultRules)
    {
        var cmds = new List<IDesignerCommand>();
        AddHighlightRules(cmds, element, resultRules);
        ExecuteAsComposite("Zvýraznění řádků", cmds);
    }

    /// <summary>Pravidla zvýraznění z okna tabulky do téhož vratného kroku jako zbytek okna (`B194d` E13e).
    /// null = okno pravidla neupravilo; prázdný seznam → null (čistý JSON).</summary>
    private static void AddHighlightRules(List<IDesignerCommand> cmds, IRowHighlightable element,
        List<HighlightRule>? resultRules)
    {
        if (resultRules is null) return;
        List<HighlightRule>? newVal = resultRules.Count > 0 ? resultRules : null;
        AddIfChanged(cmds, "Zvýraznění řádků", element.HighlightRules, newVal,
            v => element.HighlightRules = (List<HighlightRule>?)v);
    }

    /// <summary>Okno „Druhy poznámek" jako jeden vratný krok (`#7487`).</summary>
    public static IDesignerCommand SkupinyPoznamekCommand(NotesLegendElement element, List<NastaveniSkupinyPoznamek> skupiny) =>
        new SetPropertyCommand("Druhy poznámek", element.Skupiny, skupiny,
            v => element.Skupiny = (List<NastaveniSkupinyPoznamek>?)v);

    /// <summary>Zastávkový StopList: tab „Obecné" + sloupce jako jeden undo krok.</summary>
    public void ApplyStopColumnsEdit(StopListElement element, IDesignerCommand? generalCommand, List<StopColumn> resultColumns,
        List<HighlightRule>? resultHighlightRules = null)
    {
        var cmds = new List<IDesignerCommand>();
        if (generalCommand is not null) cmds.Add(generalCommand);
        AddIfChanged(cmds, "Sloupce zastávek", element.Columns, resultColumns,
            v => element.Columns = (List<StopColumn>)v!);
        AddHighlightRules(cmds, element, resultHighlightRules);
        ExecuteAsComposite("Nastavení zastávek", cmds);
    }

    /// <summary>Linkový JŘ LineTable: tab „Obecné" + sloupce jako jeden undo krok.</summary>
    public void ApplyLineTableColumnsEdit(LineTableElement element, IDesignerCommand? generalCommand, List<StopColumn> resultColumns,
        List<HighlightRule>? resultHighlightRules = null)
    {
        var cmds = new List<IDesignerCommand>();
        if (generalCommand is not null) cmds.Add(generalCommand);
        AddIfChanged(cmds, "Sloupce linkového JŘ", element.Columns, resultColumns,
            v => element.Columns = (List<StopColumn>)v!);
        AddHighlightRules(cmds, element, resultHighlightRules);
        ExecuteAsComposite("Nastavení linkového JŘ", cmds);
    }

    /// <summary>Dynamická tabulka: tab „Obecné" + sloupce jako jeden undo krok.</summary>
    public void ApplyDynamicTableEdit(DynamicTableElement element, IDesignerCommand? generalCommand, List<TableColumn> resultColumns,
        List<HighlightRule>? resultHighlightRules = null)
    {
        var cmds = new List<IDesignerCommand>();
        if (generalCommand is not null) cmds.Add(generalCommand);
        AddIfChanged(cmds, "Sloupce tabulky", element.Columns, resultColumns,
            v => element.Columns = (List<TableColumn>)v!);
        AddHighlightRules(cmds, element, resultHighlightRules);
        ExecuteAsComposite("Nastavení tabulky", cmds);
    }

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
        RequestParamsVisibilityRefresh?.Invoke();
        ZkontrolujPrvkyAgendy();
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
        RequestParamsVisibilityRefresh?.Invoke();
        ZkontrolujPrvkyAgendy();
    }

    /// <summary>Údaje ze šablony, se kterými se načetla data náhledu. Po každé změně, která je mohla
    /// pohnout (dialog sloupců, kultura, undo/redo), se musí data načíst znovu, jinak náhled ukazuje
    /// sloupce staré definice nebo starý jazyk.</summary>
    private TemplateDataInputs? _lastDataInputs;

    private void RefetchIfColumnsDefinitionChanged()
    {
        // Definice se mohla pohnout jakkoli - dialogem, přidáním či smazáním prvku, vložením ze
        // schránky, undo/redo. Parametr rozvržení ji zrcadlí, takže se srovnává tady, na jediném
        // místě, které tyhle změny vidí všechny. (Bez toho zůstal po přidání prvku odjezdů na
        // „Automaticky", i když prvek dostal sloupce předvyplněné.)
        SyncLayoutParamFromTemplate();

        // Snapshot zapisuje až ÚSPĚŠNÝ fetch (RefreshDataForCurrentInstanceAsync) - kdyby se
        // zapsal tady a fetch spadl, další kontroly by se tvářily, že data definici odpovídají.
        if (CurrentDataInputs() == _lastDataInputs) return;
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
    /// <see cref="TemplateDataInputs.From"/> (první s definicí, jinak první vůbec).</summary>
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
            && CacheProvider.TryGetCachedMockData(agendaCode, CurrentDataInputs(), out var cachedMock))
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
}
