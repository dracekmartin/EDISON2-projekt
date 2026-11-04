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

/// <summary>Spuštění z jízdních řádů s vybranou linkou (porovnání linek v modulu JŘ).</summary>
public partial class TemplateDesignerViewModel
{
    // ── Spuštění s vybranou linkou (porovnání linek v modulu JŘ) ─────────────

    /// <summary>
    /// Hodnoty parametrů ze spuštění a šablona, ke které patří. Mají přednost před uloženými
    /// předvolbami i hodnotami v šabloně - jinak by předvolba načtená až po startu (pomalé spojení)
    /// přepsala srovnávací skupinu, kterou uživatel vybral v modulu JŘ. Platí, dokud uživatel
    /// nezmění parametr nebo nevybere jinou linku.
    /// </summary>
    private (Guid TemplateId, IReadOnlyDictionary<string, string> Values)? _launchParams;

    internal void UseLaunchParams(Guid templateId, IReadOnlyDictionary<string, string> values) =>
        _launchParams = (templateId, values);

    /// <summary>Nese spuštění linku k otevření (ne jen agendu)?</summary>
    public bool ShouldOpenFromLaunch(Config.LaunchInput input) =>
        CacheProvider is not null && TemplateStorage is not null && input.AgendaCode is not null
        && input.Instances(InstanceParamName(input.AgendaCode)).Count > 0;

    /// <summary>
    /// Otevře šablonu agendy ze spuštění, vybere v ní linku a nastaví parametry ze spuštění.
    /// Šablona: nejnověji upravená vlastní šablona firmy v agendě, jinak systémová.
    /// </summary>
    public async Task OpenFromLaunchAsync(Config.LaunchInput input)
    {
        if (!ShouldOpenFromLaunch(input)) return;
        var agenda = State.CurrentTemplate.AgendaCode;
        // Neznámý kód agendy konstruktor přeskočil - pak tu stojí jiná agenda a linka by k ní nepatřila.
        if (!string.Equals(agenda, input.AgendaCode, StringComparison.OrdinalIgnoreCase))
        {
            PopupDisplayer.Info($"Agenda {input.AgendaCode} není na serveru k dispozici, návrhář ji nemůže otevřít.");
            return;
        }

        try
        {
            var (template, isSystem) = await FindLaunchTemplateAsync(agenda!);
            UseLaunchParams(template?.Id ?? State.CurrentTemplate.Id, input.Values);
            if (template is not null)
                await ShowTemplateFromDatabaseAsync(template, isSystem, loadInstanceDefaults: false);
            else
            {
                await RefreshFieldsList();
                await ApplyPersistedPrintParamsAsync();
                StatusText = "Agenda nemá uloženou ani systémovou šablonu - otevřena prázdná šablona.";
            }

            var skupina = input.Get(Config.LaunchInput.SkupinaKey);
            await SelectLaunchInstancesAsync(agenda!, skupina, input.Instances(InstanceParamName(agenda!)));
        }
        catch (Exception ex) { PopupDisplayer.Error(ex); }
    }

    private async Task<(Template? Template, bool IsSystem)> FindLaunchTemplateAsync(string agenda)
    {
        var storage = TemplateStorage!;
        var own = NewestTemplate(await storage.ListAsync(agenda));
        if (own is not null)
            return ((await storage.LoadAsync(own.TemplateId)).Template, false);
        return (await LoadSystemTemplateAsync(agenda), true);
    }

    internal static CacheTemplateMetadata? NewestTemplate(IEnumerable<CacheTemplateMetadata> templates) =>
        templates.OrderByDescending(m => m.ModifiedAt ?? m.CreatedAt ?? DateTime.MinValue).FirstOrDefault();

    /// <param name="loadPreview">false = PDF bez okna: data načte tisková cesta, náhled by je tahal podruhé.</param>
    private async Task SelectLaunchInstancesAsync(string agenda, string? skupina, IReadOnlyList<string> values, bool loadPreview = true)
    {
        var provider = CacheProvider!;
        var (instances, skupinaLabel) = await Task.Run(() =>
        {
            // Popis linky („číslo - název") dá jen server; hledá se podle čísla ve zvolené skupině.
            // ⚠️ Číslo nese klíč linky jen v AD. MHD má klíč vnitřní ID, popis by se nenašel a ukázalo
            // by se holé ID - dnes MHD linku ve spuštění neposílá; kdo to zavede, musí hledat podle klíče.
            // Hromadný tisk popis nehledá: server by se ptal za každou linku a v PDF ani v hláškách ho není potřeba.
            List<AgendaInstance> found = values.Count == 1
                ? [MatchLaunchInstance(provider.GetAvailableInstances(agenda, values[0].TrimStart('0'), skupinaId: skupina), values[0])]
                : [.. values.Select(v => MatchLaunchInstance([], v))];
            var label = string.IsNullOrEmpty(skupina) ? null
                : provider.GetAvailableSkupiny(agenda).FirstOrDefault(s => s.Value == skupina)?.Display;
            return (found, label);
        });

        _activeSkupinaId = string.IsNullOrEmpty(skupina) ? null : skupina;
        _activeSkupinaLabel = string.IsNullOrEmpty(skupina) ? null : skupinaLabel ?? skupina;
        _activeInstances = instances;
        _activeInstanceIndex = 0;
        OnActiveInstancesChanged(reloadData: loadPreview);
        OnPropertyChanged(nameof(ActiveSkupinaText));
        OnPropertyChanged(nameof(HasActiveSkupina));
    }

    /// <summary>
    /// Šablona pro PDF ze spuštění podle pravidla na serveru (`API.SablonyProTisk`, `B190`): šablona ze spuštění
    /// (<see cref="LaunchInput.TemplateKey"/>), jinak výchozí šablona firmy, jinak M-line. Vrátí šablonu, nebo důvod
    /// pro uživatele, proč žádná není.
    /// </summary>
    internal async Task<(Template? Template, string? Duvod)> ResolvePrintTemplateAsync(string agenda, string? templateId)
    {
        if (TemplateStorage is null) return (null, "Návrhář není připojený k databázi.");
        var volba = await TemplateStorage.GetTemplateForPrintAsync(agenda, templateId);
        if (volba.TemplateId is { } id)
            return ((await TemplateStorage.LoadAsync(id, volba.SourceIdFa)).Template, null);

        // Šablonu M-line nalévá do databáze až návrhář spuštěný s oknem; do té doby ji má jen .exe.
        if (string.IsNullOrEmpty(templateId) && Guid.TryParse(AgendaEntry(agenda)?.VychoziSablonaMline, out var mline)
            && await Task.Run(() => EmbeddedSystemTemplate(agenda)) is { } zabalena && zabalena.Id == mline)
            return (zabalena, null);
        return (null, volba.Duvod);
    }

    /// <summary>
    /// PDF ze spuštění bez hlavního okna (rozdílový JŘ z porovnání linek, hromadný tisk z okna JŘ). Šablona,
    /// linky a parametry jdou toutéž cestou jako otevření v návrháři, data a kreslení touž cestou jako tisk
    /// do PDF z tiskového okna. Všechny linky jdou do jednoho PDF v pořadí ze spuštění (rozhodnutí Jan 2. 11.).
    /// </summary>
    /// <param name="folder">Složka pro PDF; soubor se jmenuje podle šablony a linky, u víc linek podle jejich počtu.</param>
    /// <param name="stav">Text průběhu pro okno (načítání linek, kreslení).</param>
    public async Task<LaunchPdfResult> ExportLaunchPdfAsync(LaunchInput input, Template template, string folder,
                                                            IProgress<string>? stav = null)
    {
        var agenda = input.AgendaCode;
        if (CacheProvider is null || agenda is null) return LaunchPdfResult.Nevzniklo("Návrhář není připojený k databázi.");
        var nazev = AgendaEntry(agenda)?.Nazev ?? NazevInstance.Obecny;
        var hodnoty = input.Instances(InstanceParamName(agenda));
        if (hodnoty.Count == 0) return LaunchPdfResult.Nevzniklo($"Spuštění neurčuje {nazev.Akuzativ}.");

        State.CurrentTemplate = template;
        await RefreshFieldsList();
        UseLaunchParams(template.Id, input.Values);
        await ApplyPersistedPrintParamsAsync();
        await SelectLaunchInstancesAsync(agenda, input.Get(LaunchInput.SkupinaKey), hodnoty, loadPreview: false);
        var linka = CurrentActiveInstance!.Display;

        var load = NacitaniVsechLinek(out var vynechane);
        if (load is null) return LaunchPdfResult.Nevzniklo("Návrhář není připojený k databázi.");
        var paramsSnapshot = new Dictionary<string, object?>(_previewParams);
        IProgress<(int Hotovo, int Celkem, string Linka)>? prubeh = stav is null ? null
            : new Progress<(int Hotovo, int Celkem, string Linka)>(p => stav.Report(p.Celkem > 1
                ? $"Načítám data: {nazev.Jednotne} {p.Linka} ({p.Hotovo + 1} z {p.Celkem})…"
                : $"Načítám data: {nazev.Jednotne} {p.Linka}…"));
        var pages = await Task.Run(() => load(paramsSnapshot, prubeh));
        if (pages is null or { Count: 0 })
        {
            if (hodnoty.Count > 1)
                return LaunchPdfResult.Nevzniklo($"{nazev.ZadnaVybrana} nemá data, PDF nevzniklo.\n\n{SeznamVynechanych(vynechane)}");
            return LaunchPdfResult.Nevzniklo(vynechane.FirstOrDefault().Duvod is { Length: > 0 } duvod
                ? $"{NazevInstance.SVelkym(nazev.Jednotne)} {linka} nemá data: {duvod}"
                : $"{NazevInstance.SVelkym(nazev.Jednotne)} {linka} nemá data.");
        }

        var snapshot = TemplateSerializer.DeepClone(template);
        var upozorneni = vynechane.Count > 0
            ? $"V PDF chybí {nazev.Pocet(vynechane.Count)} bez dat:\n{SeznamVynechanych(vynechane)}"
            : null;
        var nazevSouboru = input.Get(LaunchInput.FileNameKey)?.Trim();

        if (input.Get(LaunchInput.SplitKey) != "PER_LINE")
        {
            stav?.Report("Kreslím PDF…");
            var path = LaunchPdfPath(folder, nazevSouboru is { Length: > 0 } ? nazevSouboru
                : hodnoty.Count == 1 ? $"{template.Name} - {linka}" : $"{template.Name} - {nazev.Pocet(hodnoty.Count)}");
            await Task.Run(() => new PreviewCanvas(State).RenderMultiDataToPdf(path, pages, template: snapshot));
            return new LaunchPdfResult(path, null, upozorneni);
        }

        // Soubor pro každou linku se jménem ze vzoru, týmiž funkcemi jako tiskové okno návrháře.
        var vzor = PrintDialogViewModel.NazevSouboruVzor(nazevSouboru, AgendaEntry(agenda)?.ElementKind);
        var skupiny = PrintDialogViewModel.GroupForExport(pages, ExportSplitMode.PerLine).ToList();
        var pouzite = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var soubory = new List<string>();
        foreach (var skupina in skupiny)
        {
            stav?.Report($"Kreslím PDF {soubory.Count + 1} z {skupiny.Count}…");
            var cesta = LaunchPdfPath(folder, PrintDialogViewModel.BuildFileName(skupina[0], vzor, pouzite));
            await Task.Run(() => new PreviewCanvas(State).RenderMultiDataToPdf(cesta, skupina, template: snapshot));
            soubory.Add(cesta);
        }
        return soubory.Count == 1
            ? new LaunchPdfResult(soubory[0], null, upozorneni)
            : new LaunchPdfResult(folder, null, upozorneni, soubory.Count);
    }

    /// <summary>Instance bez dat po řádcích s důvodem od agendy; dlouhý seznam se zkrátí, okno s hláškou by přeteklo.</summary>
    internal static string SeznamVynechanych(IReadOnlyList<(string Linka, string? Duvod)> vynechane, int nejvys = 10)
    {
        var radky = vynechane.Take(nejvys).Select(v => $"{v.Linka}: {(string.IsNullOrEmpty(v.Duvod) ? "nemá data." : v.Duvod)}");
        var zbytek = vynechane.Count > nejvys ? $"\n… a dalších {vynechane.Count - nejvys}" : "";
        return string.Join("\n", radky) + zbytek;
    }

    /// <summary>
    /// Cesta k PDF ze spuštění. Stejné jméno se přepisuje, aby ve složce nepřibývaly soubory; když starý soubor drží
    /// otevřený prohlížeč nebo prohlížeč PDF, dostane nový soubor k jménu čas.
    /// </summary>
    internal static string LaunchPdfPath(string folder, string name)
    {
        Directory.CreateDirectory(folder);
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string([.. name.Select(c => invalid.Contains(c) ? '_' : c)]).Trim();
        if (clean.Length > 120) clean = clean[..120].Trim();
        var path = Path.Combine(folder, clean + ".pdf");
        try
        {
            if (File.Exists(path)) File.Delete(path);
            return path;
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return Path.Combine(folder, $"{clean} {DateTime.Now:HHmmss}.pdf");
    }

    /// <summary>
    /// Načtení dat všech vybraných linek pro tisk (tiskové okno, kontrola z návrháře, PDF ze spuštění).
    /// Null = bez databáze nebo agendy. Stránky dat nesou popis linky (<c>KontrolaVystupu.LinkaKlic</c>).
    /// <paramref name="vynechane"/> = linky bez dat z posledního načtení, s důvodem od agendy.
    /// </summary>
    public Func<Dictionary<string, object?>, IProgress<(int Hotovo, int Celkem, string Linka)>?, List<DataContext>?>? NacitaniVsechLinek(out List<(string Linka, string? Duvod)> vynechane)
    {
        var bezDat = vynechane = [];
        Func<Dictionary<string, object?>, IProgress<(int Hotovo, int Celkem, string Linka)>?, List<DataContext>?>? buildRealData = null;
        if (CacheProvider is not null && !string.IsNullOrEmpty(State.CurrentTemplate.AgendaCode))
        {
            var provider = CacheProvider;
            var agendaCode = State.CurrentTemplate.AgendaCode!;
            // Snapshot na UI threadu - callback běží na threadpoolu a nesmí sahat na živou šablonu
            var dataInputs = CurrentDataInputs();
            // Kultura šablony (formát dat, čísel) - bez ní by tisk formátoval podle nastavení
            // Windows, zatímco náhled podle šablony, a slovenská šablona by vyšla jinak.
            var kultura = GetTemplateCulture();
            buildRealData = (paramValues, prubeh) =>
            {
                List<DataContext>? pages;
                bezDat.Clear();

                // Pokud má uživatel aktivní instance (vybrané přes Instance Picker), iteruj je
                // a vygeneruj batch napříč všemi - ignoruje LINKA_ID z parametrů dialogu.
                if (ActiveInstances.Count > 0)
                {
                    var paramName = CurrentInstanceParamName;
                    var all = new List<DataContext>(ActiveInstances.Count * 4);
                    int hotovo = 0;
                    foreach (var inst in ActiveInstances)
                    {
                        prubeh?.Report((hotovo++, ActiveInstances.Count, inst.Display));
                        List<DataContext>? instPages;
                        string? duvod;
                        try
                        {
                            (instPages, duvod) = provider.GetDataSDuvodem(agendaCode, ComposeGetDataParams(
                                paramValues, paramName, inst.Value, ActiveSkupinaId, dataInputs));
                        }
                        // Chyba v datech tisk zastaví (rozhodnutí Jan 26. 10.), ale musí říct, u které linky.
                        catch (CacheAgendaDataException ex) when (ex.Linka is null)
                        {
                            throw new CacheAgendaDataException(ex.Message, inst.Display);
                        }
                        if (instPages is null)
                        {
                            // Linka bez dat nezastaví ostatní, ale nesmí zmizet potichu.
                            bezDat.Add((inst.Display, duvod));
                            continue;
                        }
                        // Linka ke každé stránce - kontrola v tiskovém okně z ní řekne, které lince
                        // hláška patří (`B178`).
                        var linka = new Dictionary<string, object?> { [KontrolaVystupu.LinkaKlic] = inst.Display };
                        all.AddRange(instPages.Select(p => p.WithCulture(kultura).WithExtraValues(linka)));
                    }
                    pages = all.Count > 0 ? all : null;
                }
                else
                {
                    // Bez vybrané instance nelze tisknout reálná data - linku určuje výhradně
                    // Instance Picker (parametry sestavy už LINKA_ID neobsahují). Dialog na null
                    // ukáže hlášku s navedením na výběr linky.
                    pages = null;
                }

                if (pages is null) return null;
                // Merge parametrů do každého DataContextu - C# rendering je potřebuje
                // (HODINY_OD, HODINY_DO, CASOVA_OSA, TISK_PROJETYCH_ZAST atd.)
                pages = MergeParamsIntoPages(pages, paramValues).ToList();

                return NoteVisibilityFilter.Apply(
                    pages,
                    NoteVisibilityFilter.ParseMode(paramValues.GetValueOrDefault(NoteVisibilityFilter.ParamName)),
                    false,
                    NoteVisibilityFilter.ShouldHideNaZnameniLegend(State.CurrentTemplate),
                    hidePrestupLegend: NoteVisibilityFilter.ShouldHidePrestupLegend(State.CurrentTemplate),
                    hiddenContent: NoteVisibilityFilter.HiddenContentCategories(State.CurrentTemplate));
            };
        }

        return buildRealData;
    }

    /// <summary>
    /// Linka ze spuštění mezi nalezenými. Modul JŘ posílá klíč linky s vedoucími nulami („0380760"),
    /// shoda se proto bere i bez nich. Nenalezenou linku nechá s jejím klíčem - data načte server
    /// i tak, jen popis linky bude holé číslo.
    /// </summary>
    internal static AgendaInstance MatchLaunchInstance(IEnumerable<AgendaInstance> found, string value)
    {
        static string Bare(string s) => s.TrimStart('0') is { Length: > 0 } t ? t : s;
        return found.FirstOrDefault(i => i.Value == value || Bare(i.Value) == Bare(value))
               ?? new AgendaInstance(value, Bare(value), null);
    }

    /// <summary>Hodnoty parametrů ze spuštění nad <see cref="_previewParams"/>, jen u šablony, ke které patří.</summary>
    private void ApplyLaunchParamsToPreview()
    {
        if (_launchParams is not { } launch || launch.TemplateId != State.CurrentTemplate.Id) return;
        foreach (var p in _agendaParameters)
            if (launch.Values.TryGetValue(p.Name, out var s))
                _previewParams[p.Name] = ParseParamValue(p, s);
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
            RememberTemplate(State.CurrentTemplate.Id);
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
            State.CurrentTemplate.AgendaCode,
            CurrentAgendaDisplayName);
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

        SyncParametersToTemplate();
        CaptureEditorPreferencesToTemplate();
        try
        {
            await TemplateStorage.SaveAsync(State.CurrentTemplate);
            RememberTemplate(State.CurrentTemplate.Id);
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
            if (!p.PerInstance && _previewParams.TryGetValue(p.Name, out var v))
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

    /// <summary>View vrstva (code-behind) napojí WPF implementaci - modální/souborové dialogy + schránka.
    /// Přes tuto abstrakci VM nereferencuje WPF (R4). Null v testech → dialogové akce se tiše přeskočí.</summary>
    public IDesignerDialogService? Dialogs { get; set; }

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
}

/// <summary>
/// Výsledek PDF ze spuštění: cesta k PDF, nebo důvod pro uživatele, proč nevzniklo. <see cref="Upozorneni"/> = PDF
/// vzniklo, ale některé instance v něm chybějí (nemají data) - uživatel to má vědět, ne hledat v PDF.
/// <see cref="Souboru"/> větší než 1 = soubor pro každou linku, <see cref="Path"/> je pak složka.
/// </summary>
public record LaunchPdfResult(string? Path, string? Duvod, string? Upozorneni = null, int Souboru = 1)
{
    public static LaunchPdfResult Nevzniklo(string duvod) => new(null, duvod);
}
