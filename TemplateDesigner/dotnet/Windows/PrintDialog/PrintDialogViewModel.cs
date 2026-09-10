using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using ALWA.SYS;
using EDControls.EDViewModel;
using Microsoft.Win32;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Layout;
using TemplateDesigner.Services;

namespace TemplateDesigner;

public class PrintDialogViewModel : EDViewModel
{
    private readonly Template _template;
    private readonly Dictionary<string, object?> _paramValues;
    private readonly IReadOnlyList<TemplateParameter> _parameters;
    private readonly string? _agendaClass;
    private readonly Action? _onParamsChanged;
    private readonly Func<Dictionary<string, object?>, DataContext?> _buildMockData;
    private readonly Func<Dictionary<string, object?>, List<DataContext>?>? _buildRealData;
    private readonly TdPreferencesService? _preferences;

    private string _statusText = "";

    /// <summary>Výchozí pattern názvu souboru (tokeny {POLE} z dat) - odpovídá legacy
    /// „NázevZastávky_ČísloLinky_…". Funguje pro AD i MHD (obě emitují tato pole).</summary>
    public const string DefaultFileNamePattern = "{CISLO_LINKY}_{POPIS_ZASTAVKA}_{KONECNA_ZASTAVKA}";

    /// <summary>Klíče universálních export polí v EDInputformu (AgendaParams) - NEjsou agenda
    /// parametry, jsou společné všem agendám. Hodnoty tečou přes sdílený <c>_paramValues</c>
    /// (→ persistence per-agenda zadarmo).</summary>
    public const string ExportSplitKey = "__EXPORT_SPLIT";
    public const string ExportFileNameKey = "__EXPORT_FILENAME";

    /// <summary>Klíče export polí - pro restore v <c>LoadPrintParamsAndApplyAsync</c> (nejsou
    /// v <c>_agendaParameters</c>, takže by se jinak nenačetly).</summary>
    public static readonly string[] ExportKeys =
        [ExportSplitKey, ExportFileNameKey, .. SheetLayoutKeys.All];

    /// <summary>FQN Caché agenda třídy pro EDInputForm AgendaParams (null = bez agendy / GENERAL).</summary>
    public string? AgendaClass => _agendaClass;

    /// <summary>True = je agenda (Caché class) → staví se AgendaParams formulář (vč. export polí).
    /// I agenda bez GetParameters má formulář (jen export skupinu).</summary>
    public bool HasForm => !string.IsNullOrEmpty(_agendaClass);

    public bool CanExportRealData => _buildRealData is not null;
    public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

    public RelayCommand ExportPdfCommand { get; }
    public AsyncRelayCommand ExportRealDataPdfCommand { get; }

    /// <summary>Code-behind napojí na PreviewCanvas.RenderToPdf. Třetí parametr = imposice (null = vypnutá).</summary>
    public Action<string, DataContext?, SheetLayoutOptions?, float>? RequestRenderPdf { get; set; }

    /// <summary>Code-behind napojí na PreviewCanvas.RenderMultiDataToPdf. Třetí parametr = imposice (null = vypnutá).</summary>
    public Action<string, IReadOnlyList<DataContext>, SheetLayoutOptions?, float>? RequestRenderMultiPdf { get; set; }

    /// <summary>Code-behind napojí na PreviewCanvas.CountOverflowingPages - layout-only kontrola přetečení
    /// napříč všemi instancemi (data už fetchnutá → levné). Vrací nálezy (co a kde se nevejde)
    /// a celkový počet stránek.</summary>
    public Func<IReadOnlyList<DataContext>, (IReadOnlyList<PreteceniNalez> Nalezy, int Total)>? RequestOverflowCheck { get; set; }

    /// <summary>
    /// PrintDialog operuje nad **sdíleným** dictionary parametrů (typicky <c>MainWindowViewModel.PreviewParamValues</c>).
    /// Tím je zaručeno že preview lišta a PrintDialog ukazují stejné hodnoty - když user změní
    /// param v PrintDialogu, preview se live aktualizuje přes <paramref name="onParamsChanged"/>
    /// callback (typicky <c>MainWindowViewModel.OnExternalParamsChanged</c>).
    /// </summary>
    public PrintDialogViewModel(
        Template template,
        IReadOnlyList<TemplateParameter> parameters,
        Dictionary<string, object?> sharedParamValues,
        Func<Dictionary<string, object?>, DataContext?> buildMockData,
        Func<Dictionary<string, object?>, List<DataContext>?>? buildRealData = null,
        TdPreferencesService? preferences = null,
        Action? onParamsChanged = null,
        string? agendaClass = null)
    {
        _template = template;
        _paramValues = sharedParamValues;
        _parameters = parameters;
        _agendaClass = agendaClass;
        _onParamsChanged = onParamsChanged;
        _buildMockData = buildMockData;
        _buildRealData = buildRealData;
        _preferences = preferences;

        ExportPdfCommand = Command(ExportPdf);
        ExportRealDataPdfCommand = CommandAsync(ExportRealDataPdfAsync, () => _buildRealData is not null);

        // _paramValues přichází už naplněný (preview lišta defaults + případně saved PrintParams
        // aplikované při openu šablony). Pro chybějící klíče (nový parametr přidaný po předchozím
        // savu) doplníme defaults - nepřepisuje existing values.
        // Parametry + export pole renderuje EDInputForm (CacheClass=AgendaParams) v code-behindu;
        // VM jen drží definice + sdílený dict a poskytuje konverzní/visibility helpery (viz níže).
        foreach (var p in parameters)
            _paramValues.TryAdd(p.Name, p.GetDefaultTyped());
    }

    // ── EDInputForm integrace ─────────────────────────────────────────────────

    /// <summary>IN params pro AgendaParams.Init - agendaClass + JSON aktuálních hodnot pro prefill.</summary>
    public CVar BuildInitParams()
    {
        var init = new CVar();
        init["agendaClass"] = new CVar(_agendaClass ?? "");

        var vals = new Dictionary<string, string>();
        foreach (var p in _parameters)
        {
            var v = _paramValues.TryGetValue(p.Name, out var o) ? o : p.GetDefaultTyped();
            vals[p.Name] = ToFieldString(v, p.Type);
        }

        // Export pole (universální, ne agenda parametry) - prefill posledně použitými / defaulty.
        vals[ExportSplitKey] = CurrentSplitKey();
        vals[ExportFileNameKey] = CurrentFileNamePattern();
        vals[SheetLayoutKeys.Sheet] = (_paramValues.GetValueOrDefault(SheetLayoutKeys.Sheet) as string) is { Length: > 0 } sh ? sh : SheetLayoutKeys.SheetNone;
        vals[SheetLayoutKeys.Grid] = (_paramValues.GetValueOrDefault(SheetLayoutKeys.Grid) as string) is { Length: > 0 } gr ? gr : SheetLayoutKeys.GridAuto;
        vals[SheetLayoutKeys.Repeat] = StoredBool(SheetLayoutKeys.Repeat) ? "true" : "false";
        vals[SheetLayoutKeys.Marks] = StoredBool(SheetLayoutKeys.Marks) ? "true" : "false";

        init["values"] = new CVar(JsonSerializer.Serialize(vals));
        return init;
    }

    /// <summary>Zapíše hodnotu pole (string z EDInputFormu) do sdíleného dict s typovou konverzí
    /// a spustí živý náhled (<c>onParamsChanged</c>). Typování zrcadlí <see cref="TemplateParameter.GetDefaultTyped"/>.</summary>
    public void SetParamValue(string name, string fieldValue)
    {
        var type = _parameters.FirstOrDefault(p => p.Name == name)?.Type ?? "string";
        _paramValues[name] = type switch
        {
            "bool"   => fieldValue == "1" || string.Equals(fieldValue, "true", StringComparison.OrdinalIgnoreCase),
            "number" => float.TryParse(fieldValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f,
            _        => (object)fieldValue
        };

        // Export pole (split/název/imposice) neovlivňují obsah sestavy → zbytečně neobnovovat náhled.
        if (!name.StartsWith("__EXPORT", StringComparison.Ordinal))
            _onParamsChanged?.Invoke();
    }

    /// <summary>Vyhodnotí <c>visibleWhen</c> parametru proti aktuálním hodnotám (sdílený dict).
    /// Logika v <see cref="TemplateParameter.IsVisible"/> - jeden zdroj pro preview lištu i tisk.</summary>
    public bool IsParamVisible(string name) =>
        _parameters.FirstOrDefault(x => x.Name == name)?.IsVisible(_paramValues) ?? true;

    private static string ToFieldString(object? v, string type) => type switch
    {
        "bool"   => v is true ? "true" : "false",
        "number" => v is float f ? f.ToString(CultureInfo.InvariantCulture) : (v?.ToString() ?? ""),
        _        => v?.ToString() ?? ""
    };

    /// <summary>
    /// Uloží aktuální _paramValues (= shared dict s preview lištou) do prefs (fire-and-forget).
    /// Volá se před export - perzistuje uživatelovy hodnoty pro příští session.
    /// MainWindow.OnWindowClosing taky uloží přes <c>SavePrintParamsAsync</c> jako safety.
    /// </summary>
    private void PersistParams()
    {
        if (_preferences is null) return;
        if (string.IsNullOrEmpty(_template.AgendaCode)) return;
        _ = _preferences.SavePrintParamsAsync(_template.AgendaCode, _paramValues);
    }

    /// <summary>
    /// Export náhledu (ukázková data) - **záměrně synchronní**, dokud jde o jednu datovou sadu.
    /// ⚠️ Kdo to bude převádět na pozadí: nestačí `Task.Run`. Tahle cesta renderuje nad ŽIVOU
    /// šablonou (`RenderToPdf` → template null → `State.CurrentTemplate`) a dnes to nevadí právě
    /// proto, že synchronní běh uživateli nedovolí mezitím editovat. Async bez `DeepClone`
    /// snapshotu by zavedl závodu s preview renderem - viz `ExportRealDataPdfAsync` níže, které
    /// to dělá správně.
    /// </summary>
    /// <summary>
    /// Popis uspořádání na list do stavové hlášky: varování, když mřížka vyjde 1×1 (papír se jen
    /// zvětšil), jinak neutrální popis. Prázdné = uspořádání vypnuté.
    /// </summary>
    /// <remarks>
    /// ⚠️ Jedno místo pro **oba** exporty (ukázková data i data z databáze). Když jsem varování
    /// 1. 9. přidal jen do exportu s daty, Jan ho na prázdné šabloně neviděl - a přesně tou cestou
    /// jde každý, kdo si chce chování vyzkoušet bez vybrané linky.
    /// </remarks>
    private static string PopisUsporadani(SheetLayoutOptions? sheets) => sheets is null
        ? ""
        : sheets.VarovaniBezEfektu() is { } bezEfektu ? $" · ⚠ {bezEfektu}" : $" · {sheets.ShortLabel}";

    private void ExportPdf()
    {
        SheetLayoutOptions? sheets;
        try { sheets = BuildSheetOptions(); }
        catch (ArgumentException ex) { StatusText = $"⚠ {ex.Message}"; return; }

        var filePath = ShowSaveDialog("Export náhledu do PDF");
        if (filePath is null) return;

        try
        {
            PersistParams();
            var data = _buildMockData(_paramValues);
            RequestRenderPdf?.Invoke(filePath, data, sheets, Meritko);
            OpenPdf(filePath);
            StatusText = $"PDF uloženo: {Path.GetFileName(filePath)}{PopisUsporadani(sheets)}";
        }
        catch (Exception ex)
        {
            StatusText = $"⚠ Chyba: {ex.Message}";
        }
    }

    private async Task ExportRealDataPdfAsync()
    {
        if (_buildRealData is null) return;

        // Validace imposice před výběrem cíle - chybná mřížka je uživatelská chyba konfigurace.
        SheetLayoutOptions? sheets;
        try { sheets = BuildSheetOptions(); }
        catch (ArgumentException ex) { StatusText = $"⚠ {ex.Message}"; return; }

        var splitMode = CurrentSplitMode();
        var pattern   = CurrentFileNamePattern();

        // Single = vyber soubor (jako dnes); split = vyber složku, vznikne N souborů.
        string? target = splitMode == ExportSplitMode.Single
            ? ShowSaveDialog("Tisk všech vybraných do PDF")
            : ShowFolderDialog("Tisk všech vybraných - vyber složku");
        if (target is null) return;

        try
        {
            PersistParams();
            StatusText = "Načítání dat z databáze…";

            // Fetch na threadpoolu - batch přes všechny instance jsou HTTP volání per linka;
            // sync by zamrazil dialog na desítky sekund (R1). Snapshot parametrů: uživatel je
            // může během fetche v dialogu měnit (sdílený dict s preview lištou).
            var paramsSnapshot = new Dictionary<string, object?>(_paramValues);
            var dataPages = await Task.Run(() => _buildRealData(paramsSnapshot));

            if (dataPages is null or { Count: 0 })
            {
                // Nejčastější příčina není výpadek DB, ale žádná vybraná linka - navést uživatele.
                StatusText = "Databáze nevrátila žádná data. Zkontrolujte, že je v hlavním okně vybraná linka (štítek vedle agendy).";
                return;
            }

            // Kontrola přetečení na UI threadu (layout-only, levná) - sdílí element cache
            // s preview renderem, na pozadí by závodila.
            var (preteceni, totalPages) = RequestOverflowCheck?.Invoke(dataPages)
                                          ?? ([], dataPages.Count);
            int overflow = preteceni.Count;
            // Co konkrétně přetéká, ne jen kolik stránek: uživatel tiskne dávkově a bez toho nemá
            // v desítkách stránek co hledat (podnět kolega 3. 9.).
            string preteceniInfo = OverflowWarnings.Summarize(preteceni, totalPages) is { } popis
                ? $" ({popis})" : "";

            StatusText = $"Generuji PDF ({totalPages} stránek)…";
            // Skládání 1×1 papír jen zvětší (A5 šablona + list „A4 na výšku" = A4 s jednou vývěskou
            // uprostřed). Volba se persistuje per agenda, takže uživatel nemá jak zjistit proč -
            // zákazník to hlásil jako chybu tisku (#13158). Proto varování, ne neutrální popis.
            string sheetInfo = PopisUsporadani(sheets) + PopisMeritka();
            // Vlastní sloupce: co se na vývěsku nedostane (odjezd mimo sloupce, sloupec s obdobím
            // mimo platnost JŘ) říct i tady, ne jen ve status baru návrháře - tisknout může jiná
            // linka než v náhledu.
            var unassigned = DepartureColumnWarnings.Summarize(dataPages);
            string unassignedInfo = unassigned is null ? "" : $" · ⚠ {unassigned}";
            // Chybějící font: tichá záměna za Arial, takže PDF vypadá v pořádku a jen je jiné,
            // než autor zamýšlel. Kontroluje se i tady, ne jen v návrháři - tisknout může
            // někdo, kdo šablonu jen otevřel z databáze.
            var chybejiciFonty = FontWarnings.Summarize(_template);
            string fontInfo = chybejiciFonty is null ? "" : $" · ⚠ {chybejiciFonty}";
            // Zmenšené písmo: prvek se nevešel, takže se tiskne menší, než co je v šabloně
            // nastavené. Bez hlášky to jde poznat až z papíru - a u vývěsek vyjde každá zastávka
            // jinak podle počtu poznámek.
            var zmenseno = ContentShrinkWarnings.Summarize(_template, dataPages);
            string zmensenoInfo = zmenseno is null ? "" : $" · ⚠ {zmenseno}";
            // Malý QR: na papíře se nesejme, ale v PDF vypadá „nakresleně" - bez hlášky to zákazník
            // pozná až u zastávky s mobilem v ruce (#13158).
            var maleKody = BarcodeSizeWarnings.Summarize(_template, dataPages);
            string maleKodyInfo = maleKody is null ? "" : $" · ⚠ {maleKody}";

            if (splitMode == ExportSplitMode.Single)
            {
                // Render na threadpoolu - callback rendruje nad snapshotem šablony (DeepClone
                // ve wiring), takže nekoliduje s preview renderem UI threadu.
                await Task.Run(() => RequestRenderMultiPdf?.Invoke(target, dataPages, sheets, Meritko));
                OpenPdf(target);
                StatusText = (overflow > 0
                    ? $"⚠ PDF uloženo, ale {overflow} z {totalPages} stránek přetéká{preteceniInfo}: {Path.GetFileName(target)}{sheetInfo}"
                    : $"PDF uloženo ({totalPages} stránek): {Path.GetFileName(target)}{sheetInfo}") + unassignedInfo + fontInfo + zmensenoInfo + maleKodyInfo;
                return;
            }

            // Split - seskup instance a vyrenderuj jedno PDF per skupina (existující multi-render
            // per cesta). Název každého souboru = pattern {POLE} vyhodnocený nad první instancí skupiny.
            int fileCount = await Task.Run(() =>
            {
                var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                int files = 0;
                foreach (var group in GroupForExport(dataPages, splitMode))
                {
                    var name = BuildFileName(group[0], pattern, usedNames);
                    RequestRenderMultiPdf?.Invoke(Path.Combine(target, name + ".pdf"), group, sheets, Meritko);
                    files++;
                }
                return files;
            });

            OpenFolder(target);
            StatusText = (overflow > 0
                ? $"⚠ {fileCount} PDF do složky, ale {overflow} z {totalPages} stránek přetéká{preteceniInfo}{sheetInfo}"
                : $"{fileCount} PDF uloženo do složky ({totalPages} stránek celkem){sheetInfo}") + unassignedInfo + fontInfo + zmensenoInfo + maleKodyInfo;
        }
        catch (Exception ex)
        {
            StatusText = $"⚠ Chyba: {ex.Message}";
        }
    }

    /// <summary>Aktuální klíč členění z formuláře (sdílený dict). Default "SINGLE".</summary>
    private string CurrentSplitKey()
    {
        var s = _paramValues.GetValueOrDefault(ExportSplitKey) as string;
        return string.IsNullOrWhiteSpace(s) ? "SINGLE" : s;
    }

    private ExportSplitMode CurrentSplitMode() => CurrentSplitKey() switch
    {
        "PER_LINE" => ExportSplitMode.PerLine,
        "PER_STOP" => ExportSplitMode.PerStop,
        _          => ExportSplitMode.Single,
    };

    private string CurrentFileNamePattern()
    {
        var p = _paramValues.GetValueOrDefault(ExportFileNameKey) as string;
        return string.IsNullOrWhiteSpace(p) ? DefaultFileNamePattern : p;
    }

    /// <summary>Imposice z exportních polí (sdílený dict) + rozměru stránky šablony.
    /// Null = vypnutá. ArgumentException = mřížka se na list nevejde (hláška pro uživatele).</summary>
    private SheetLayoutOptions? BuildSheetOptions() =>
        SheetComposer.FromParamValues(_paramValues, _template.PageSize);

    /// <summary>Měřítko tisku z exportních polí (1 = skutečná velikost).</summary>
    private float Meritko => SheetComposer.MeritkoZParamValues(_paramValues);

    /// <summary>
    /// Co z měřítka vyjde za rozměr papíru, nebo prázdno při 1×.
    /// </summary>
    /// <remarks>
    /// Server tuhle větu složit nemůže - formulář nezná rozměr stránky šablony. Bez ní uživatel
    /// nastaví 1,3 a nemá jak zjistit, na jaký papír to má poslat (`B132`).
    /// </remarks>
    private string PopisMeritka()
    {
        float m = Meritko;
        if (Math.Abs(m - 1f) < 0.001f) return "";
        var ps = _template.PageSize;
        return $" · měřítko {m:0.##}× (stránka {ps.Width:0.#}×{ps.Height:0.#} mm vyjde "
             + $"{ps.Width * m:0.#}×{ps.Height * m:0.#} mm)";
    }

    private bool StoredBool(string key) => _paramValues.GetValueOrDefault(key) is string s
        && (s == "1" || string.Equals(s, "true", StringComparison.OrdinalIgnoreCase));

    /// <summary>Seskupí stránky dat dle režimu členění. Pořadí skupin i stránek v nich
    /// odpovídá pořadí z Caché (LINQ GroupBy zachovává first-seen pořadí).</summary>
    private static IEnumerable<List<DataContext>> GroupForExport(IReadOnlyList<DataContext> pages, ExportSplitMode mode) =>
        mode switch
        {
            ExportSplitMode.PerLine => pages.GroupBy(p => p.GetString("CISLO_LINKY")).Select(g => g.ToList()),
            ExportSplitMode.PerStop => pages.Select(p => new List<DataContext> { p }),
            _                       => [pages.ToList()],
        };

    /// <summary>Vyhodnotí pattern nad daty, ošetří neplatné znaky a kolize (přípona _2, _3, …).</summary>
    private static string BuildFileName(DataContext page, string pattern, HashSet<string> used)
    {
        var resolved = string.IsNullOrWhiteSpace(pattern) ? "export" : page.ResolvePlaceholders(pattern);
        var name = Sanitize(resolved);
        if (string.IsNullOrWhiteSpace(name)) name = "export";

        var unique = name;
        for (int n = 2; !used.Add(unique); n++)
            unique = $"{name}_{n}";
        return unique;
    }

    /// <summary>Odstraní znaky neplatné v názvu souboru (Windows) + zbytky nevyřešených {POLE}.</summary>
    private static string Sanitize(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            s = s.Replace(c, '_');
        return s.Replace("{", "").Replace("}", "").Trim();
    }

    private static void OpenPdf(string filePath)
    {
        try { Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true }); }
        catch { /* PDF viewer nemusí být nainstalován */ }
    }

    private static void OpenFolder(string folderPath)
    {
        try { Process.Start(new ProcessStartInfo(folderPath) { UseShellExecute = true }); }
        catch { /* průzkumník nemusí být dostupný */ }
    }

    private string? ShowSaveDialog(string title)
    {
        var dlg = new SaveFileDialog
        {
            Title = title,
            Filter = "PDF soubory (*.pdf)|*.pdf",
            DefaultExt = "pdf",
            FileName = (string.IsNullOrWhiteSpace(_template.Name)
                ? "sablona" : _template.Name) + ".pdf"
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    private static string? ShowFolderDialog(string title)
    {
        var dlg = new OpenFolderDialog { Title = title };
        return dlg.ShowDialog() == true ? dlg.FolderName : null;
    }
}

/// <summary>Členění výstupu při tisku s daty (více instancí = zastávka × směr).</summary>
public enum ExportSplitMode
{
    /// <summary>Vše do jednoho PDF (dnešní chování).</summary>
    Single,
    /// <summary>Jeden PDF na linku (všechny zastávky linky pohromadě).</summary>
    PerLine,
    /// <summary>Jeden PDF na zastávku × směr (legacy vývěska per zastávka).</summary>
    PerStop,
}
