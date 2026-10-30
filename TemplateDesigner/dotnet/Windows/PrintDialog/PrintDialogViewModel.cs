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
using TemplateDesigner.Serialization;
using TemplateDesigner.Services;

namespace TemplateDesigner;

public class PrintDialogViewModel : EDViewModel
{
    private readonly Template _template;
    /// <summary>Název instance agendy pro hlášky („Žádná vybraná linka nemá data").</summary>
    private readonly NazevInstance _nazev;
    public NazevInstance Nazev => _nazev;
    private readonly Dictionary<string, object?> _paramValues;
    private readonly IReadOnlyList<TemplateParameter> _parameters;
    private readonly string? _agendaClass;
    private readonly Action? _onParamsChanged;
    private readonly Func<Dictionary<string, object?>, DataContext?> _buildMockData;
    private readonly Func<Dictionary<string, object?>, IProgress<(int Hotovo, int Celkem, string Linka)>?, List<DataContext>?>? _buildRealData;
    private readonly TdPreferencesService? _preferences;

    private string _statusText = "";

    /// <summary>Do 24. 10. výchozí název pro všechny agendy. Linkové JŘ aktuální zastávku nemají,
    /// takže v názvu stálo doslova „POPIS_ZASTAVKA"; uložený beze změny se bere jako nezadaný.</summary>
    private const string PuvodniVychoziNazev = "{CISLO_LINKY}_{POPIS_ZASTAVKA}_{KONECNA_ZASTAVKA}";

    private readonly string? _elementKind;

    /// <summary>Klíče universálních export polí v EDInputformu (AgendaParams) - NEjsou agenda
    /// parametry, jsou společné všem agendám. Hodnoty tečou přes sdílený <c>_paramValues</c>
    /// (→ persistence per-agenda zadarmo).</summary>
    public const string ExportSplitKey = "__EXPORT_SPLIT";
    public const string ExportFileNameKey = "__EXPORT_FILENAME";
    /// <summary>Formát výstupu „PDF" / „PNG" - volí se v patičce okna, ne ve formuláři z Caché
    /// (ten u šablony bez agendy není, a PNG musí jít i tam).</summary>
    public const string ExportFormatKey = "__EXPORT_FORMAT";

    /// <summary>Klíče export polí - pro restore v <c>LoadPrintParamsAndApplyAsync</c> (nejsou
    /// v <c>_agendaParameters</c>, takže by se jinak nenačetly).</summary>
    public static readonly string[] ExportKeys =
        [ExportSplitKey, ExportFileNameKey, ExportFormatKey, .. SheetLayoutKeys.All];

    /// <summary>FQN Caché agenda třídy pro EDInputForm AgendaParams (null = bez agendy / GENERAL).</summary>
    public string? AgendaClass => _agendaClass;

    /// <summary>True = je agenda (Caché class) → staví se AgendaParams formulář (vč. export polí).
    /// I agenda bez GetParameters má formulář (jen export skupinu).</summary>
    public bool HasForm => !string.IsNullOrEmpty(_agendaClass);

    public bool CanExportRealData => _buildRealData is not null;
    public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

    /// <summary>
    /// Jediné tlačítko výstupu (rozhodnutí Jan 28. 9.): z vybraných linek, bez vybrané linky
    /// z ukázkových dat. Dřív to byly čtyři cesty (PDF a PNG na liště, „Tisk všech vybraných",
    /// „Export náhledu"), které se lišily v tom, co umí, a nebylo poznat, kterou použít.
    /// </summary>
    public AsyncRelayCommand VytvoritCommand { get; }

    public IReadOnlyList<string> Formaty { get; } = ["PDF", "PNG"];

    public string Format
    {
        get => _paramValues.GetValueOrDefault(ExportFormatKey) as string == "PNG" ? "PNG" : "PDF";
        set
        {
            if (value == Format) return;
            _paramValues[ExportFormatKey] = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(JePng));
            OnPropertyChanged(nameof(UkazatUsporadani));
            OnPropertyChanged(nameof(TlacitkoVytvorit));
            RequestApplyVisibility?.Invoke();
        }
    }

    /// <summary>PNG je obrázek za každou stránku - neskládá se na list, nečlení do souborů a nemá měřítko.</summary>
    public bool JePng => Format == "PNG";

    public bool UkazatUsporadani => !JePng && Usporadani.UkazatPanel;

    public string TlacitkoVytvorit => $"Vytvořit {Format}";

    /// <summary>Code-behind po změně formátu znovu vyhodnotí viditelnost polí formuláře.</summary>
    public Action? RequestApplyVisibility { get; set; }

    /// <summary>Code-behind napojí na PreviewCanvas.RenderMultiDataToPng; vrací počet souborů.</summary>
    public Func<string, IReadOnlyList<DataContext>, int>? RequestRenderPng { get; set; }

    /// <summary>Code-behind napojí na PreviewCanvas.RenderToPdf. Třetí parametr = imposice (null = vypnutá).</summary>
    public Action<string, DataContext?, SheetLayoutOptions?, float>? RequestRenderPdf { get; set; }

    /// <summary>Code-behind napojí na PreviewCanvas.RenderMultiDataToPdf. Třetí parametr = imposice (null = vypnutá).</summary>
    public Action<string, IReadOnlyList<DataContext>, SheetLayoutOptions?, float>? RequestRenderMultiPdf { get; set; }

    /// <summary>Code-behind napojí na PreviewCanvas.CountOverflowingPages - layout-only kontrola přetečení
    /// napříč všemi instancemi (data už fetchnutá → levné). Vrací nálezy (co a kde se nevejde)
    /// a celkový počet stránek.</summary>
    public Func<IReadOnlyList<DataContext>, (IReadOnlyList<PreteceniNalez> Nalezy, int Total)>? RequestOverflowCheck { get; set; }

    /// <summary>Hlášky jedné linky (`KontrolaVystupu.ZLinky`) - napojí code-behind, protože sazbu
    /// počítá náhled.</summary>
    public Func<IReadOnlyList<DataContext>, IReadOnlyList<Hlaseni>>? RequestKontrolaLinky { get; set; }

    /// <summary>Otevře okno s výsledkem kontroly (nadpis, hlášky) - hlášky nezabírají formulář.</summary>
    public Action<string, IReadOnlyList<Hlaseni>>? RequestShowHlaseni { get; set; }

    /// <summary>Linky, které při posledním načtení nedaly data, i s důvodem od agendy („Spoje linky
    /// nemají spočítané časy…"). Tisk je přeskočí, ale musí to říct - hláškou (`B193g`).</summary>
    public Func<IReadOnlyList<(string Linka, string? Duvod)>>? VynechaneLinky { get; set; }

    /// <summary>Stav, když nemá data žádná linka: s výčtem linek (důvody jsou v hláškách), jinak obecné navedení.</summary>
    /// <summary>Bubliny tlačítek Zkontrolovat a Vytvořit s názvem instance agendy.</summary>
    public string KontrolaTooltip =>
        $"Projde {_nazev.Shoda("všechny", "všechny", "všechny", "všechna")} {_nazev.VybraneAkuzativ} bez tisku a ukáže, co se kde nevejde nebo vytiskne jinak";
    public string VytvoritTooltip =>
        $"Z vybraných {_nazev.GenitivMnozne}; když {_nazev.Shoda("žádná vybraná", "žádný vybraný", "žádný vybraný", "žádné vybrané")} není, z ukázkových dat";

    internal static string ZadnaData(IReadOnlyList<(string Linka, string? Duvod)>? vynechane, string bezDuvodu, NazevInstance nazev) =>
        vynechane is { Count: > 0 } ? $"{nazev.ZadnaVybrana} nemá data: {string.Join(", ", vynechane.Select(v => v.Linka))}" : bezDuvodu;

    /// <summary>Dovětek k hotovému výstupu o linkách, které se vynechaly; prázdný, když žádná.</summary>
    internal static string DovetekVynechanych(IReadOnlyList<(string Linka, string? Duvod)>? vynechane) =>
        vynechane is { Count: > 0 } ? $" · bez dat vynecháno: {string.Join(", ", vynechane.Select(v => v.Linka))}" : "";

    /// <summary>Hlášky linek bez dat do seznamu; okno se otevře jen na <paramref name="otevrit"/> -
    /// po vytvoření PDF by překáželo, stačí počet ve stavovém řádku.</summary>
    private void NastavHlaseniBezDat(string nadpis, bool otevrit)
    {
        var bezDat = KontrolaVystupu.SBezDat(VynechaneLinky?.Invoke(), [], _nazev);
        if (bezDat.Count == 0) return;
        if (otevrit) NastavHlaseni(bezDat, nadpis);
        else
        {
            Hlaseni = bezDat;
            _nadpisHlaseni = nadpis;
            OnPropertyChanged(nameof(Hlaseni));
            OnPropertyChanged(nameof(HasHlaseni));
            OnPropertyChanged(nameof(SouhrnHlaseni));
        }
    }

    /// <summary>Hlášky poslední kontroly, nejvážnější první.</summary>
    public IReadOnlyList<Hlaseni> Hlaseni { get; private set; } = [];
    public bool HasHlaseni => Hlaseni.Count > 0;
    private string _nadpisHlaseni = "";

    /// <summary>„2 chyby, 1 upozornění" - konec řádku stavu a nadpis okna s výsledkem.</summary>
    public string SouhrnHlaseni
    {
        get
        {
            var casti = new List<string>();
            int ch = Hlaseni.Count(h => h.Zavaznost == ZavaznostHlaseni.Chyba);
            int up = Hlaseni.Count(h => h.Zavaznost == ZavaznostHlaseni.Upozorneni);
            int inf = Hlaseni.Count(h => h.Zavaznost == ZavaznostHlaseni.Informace);
            if (ch > 0) casti.Add($"{ch} {(ch == 1 ? "problém" : ch <= 4 ? "problémy" : "problémů")}");
            if (up > 0) casti.Add($"{up} upozornění");
            if (inf > 0) casti.Add($"{inf} {(inf <= 4 ? "informace" : "informací")}");
            return string.Join(", ", casti);
        }
    }

    /// <summary>Chyba tisku do stavového řádku. Chybu v datech linky (`B194a` C11) i mezi hlášky - říká, co
    /// opravit, a klikem přejde na linku; chyba programu zůstává jen ve stavu.</summary>
    private void UkazChybu(Exception ex)
    {
        if (ex is CacheAgendaDataException data)
        {
            var linka = data.Linka ?? "";
            StatusText = $"⚠ Tisk se zastavil u {_nazev.Genitiv} {linka}";
            NastavHlaseni([KontrolaVystupu.LinkaChybaDat(linka, data.Message)], $"{NazevInstance.SVelkym(_nazev.Akuzativ)} nejde vytisknout");
            return;
        }
        StatusText = $"⚠ Chyba: {ex.Message}";
    }

    private void NastavHlaseni(IReadOnlyList<Hlaseni> hlaseni, string nadpis)
    {
        Hlaseni = hlaseni;
        _nadpisHlaseni = nadpis;
        OnPropertyChanged(nameof(Hlaseni));
        OnPropertyChanged(nameof(HasHlaseni));
        OnPropertyChanged(nameof(SouhrnHlaseni));
        if (HasHlaseni) RequestShowHlaseni?.Invoke(nadpis, hlaseni);
    }

    public RelayCommand ZobrazitHlaseniCommand { get; private set; } = null!;

    // ── Průběh (načítání dat a kontrola po linkách) ──

    private bool _probiha;
    /// <summary>Běží načítání nebo kontrola - pruh průběhu ve stavovém řádku.</summary>
    public bool Probiha { get => _probiha; private set => Set(ref _probiha, value); }

    private double _prubeh;
    /// <summary>Hotová část 0-1.</summary>
    public double Prubeh { get => _prubeh; private set => Set(ref _prubeh, value); }

    /// <summary>Hlášení průběhu načítání - vzniká na UI vlákně, takže hodnoty chodí zpátky na něj.
    /// <paramref name="podil"/> = kolik pruhu načítání zabere (u kontroly polovinu, druhou kontrola).</summary>
    private IProgress<(int Hotovo, int Celkem, string Linka)> PrubehNacitani(double podil = 1)
    {
        Probiha = true;
        Prubeh = 0;
        return new Progress<(int Hotovo, int Celkem, string Linka)>(p =>
        {
            Prubeh = p.Celkem > 0 ? podil * p.Hotovo / p.Celkem : 0;
            StatusText = $"Načítám data {p.Hotovo} z {p.Celkem} · {p.Linka}";
        });
    }

    /// <summary>
    /// Hlášky všech linek s průběhem. Sazba běží na UI vlákně (sdílí cache prvků s náhledem),
    /// proto se mezi linkami pustí kreslení - jinak by pruh průběhu stál.
    /// </summary>
    private Task<List<Hlaseni>> ZkontrolujLinkyAsync(IReadOnlyList<DataContext> dataPages) =>
        KontrolaVystupu.ZkontrolujAsync(_template, dataPages, d => RequestKontrolaLinky?.Invoke(d) ?? [], _nazev,
            (hotovo, celkem, linka) =>
            {
                StatusText = hotovo < celkem ? $"Kontroluji {hotovo + 1} z {celkem} · {linka}" : "Kontrola hotová";
                Prubeh = celkem > 0 ? 0.5 + 0.5 * hotovo / celkem : 1;
            });


    public AsyncRelayCommand KontrolaCommand { get; private set; } = null!;

    /// <summary>
    /// Zkontroluje všechny vybrané linky bez tisku - uživatel se dozví „vadí jen linka X" dřív,
    /// než pošle PDF dál (nápad Jan 24. 9.).
    /// </summary>
    private async Task ZkontrolujAsync()
    {
        if (_buildRealData is null) return;
        try
        {
            PersistParams();
            StatusText = "Načítání dat z databáze…";
            var paramsSnapshot = new Dictionary<string, object?>(_paramValues);
            var dataPages = await Task.Run(() => _buildRealData(paramsSnapshot, PrubehNacitani(podil: 0.5)));
            if (dataPages is null or { Count: 0 })
            {
                StatusText = ZadnaData(VynechaneLinky?.Invoke(),
                    $"Databáze nevrátila žádná data. Zkontrolujte, že je v hlavním okně {_nazev.Shoda("vybraná", "vybraný", "vybraný", "vybrané")} {_nazev.Jednotne} (štítek vedle agendy).", _nazev);
                NastavHlaseniBezDat($"{_nazev.ZadnaVybrana} nemá data", otevrit: true);
                return;
            }
            var hlaseni = KontrolaVystupu.SBezDat(VynechaneLinky?.Invoke(), await ZkontrolujLinkyAsync(dataPages), _nazev);
            int linek = KontrolaVystupu.PoLinkach(dataPages).Count + (VynechaneLinky?.Invoke().Count ?? 0);
            // Linky bez dat jsou mezi hláškami, dovětek ve stavu by je jen opakoval.
            StatusText = hlaseni.Count > 0
                ? $"Zkontrolováno {_nazev.Pocet(linek)}"
                : $"Zkontrolováno {_nazev.Pocet(linek)} - bez hlášek";
            NastavHlaseni(hlaseni, $"Zkontrolováno {_nazev.Pocet(linek)}: {Souhrn(hlaseni)}");
        }
        catch (Exception ex)
        {
            UkazChybu(ex);
        }
        finally
        {
            Probiha = false;
        }
    }

    private string Souhrn(IReadOnlyList<Hlaseni> hlaseni)
    {
        var predtim = Hlaseni;
        Hlaseni = hlaseni;
        var s = SouhrnHlaseni;
        Hlaseni = predtim;
        return s;
    }

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
        Func<Dictionary<string, object?>, IProgress<(int Hotovo, int Celkem, string Linka)>?, List<DataContext>?>? buildRealData = null,
        TdPreferencesService? preferences = null,
        Action? onParamsChanged = null,
        string? agendaClass = null,
        Func<bool>? maVybraneLinky = null,
        string? elementKind = null,
        NazevInstance? nazev = null)
    {
        _nazev = nazev ?? NazevInstance.Obecny;
        _maVybraneLinky = maVybraneLinky ?? (() => false);
        _elementKind = elementKind;
        _template = template;
        _paramValues = sharedParamValues;
        _parameters = parameters;
        _agendaClass = agendaClass;
        _onParamsChanged = onParamsChanged;
        _buildMockData = buildMockData;
        _buildRealData = buildRealData;
        _preferences = preferences;

        VytvoritCommand = CommandAsync(VytvoritAsync);
        KontrolaCommand = CommandAsync(ZkontrolujAsync, () => _buildRealData is not null);
        ZobrazitHlaseniCommand = Command(() => RequestShowHlaseni?.Invoke(_nadpisHlaseni, Hlaseni), () => HasHlaseni);

        // _paramValues přichází už naplněný (preview lišta defaults + případně saved PrintParams
        // aplikované při openu šablony). Pro chybějící klíče (nový parametr přidaný po předchozím
        // savu) doplníme defaults - nepřepisuje existing values.
        // Parametry + export pole renderuje EDInputForm (CacheClass=AgendaParams) v code-behindu;
        // VM jen drží definice + sdílený dict a poskytuje konverzní/visibility helpery (viz níže).
        foreach (var p in parameters)
            _paramValues.TryAdd(p.Name, p.GetDefaultTyped());

        Usporadani = new UsporadaniListuViewModel(_paramValues, DesignerPages.ObsahStranek(template).Count,
            template.PageSize, Meritko);
    }

    private readonly Func<bool> _maVybraneLinky;

    /// <summary>Z vybraných linek, jinak z ukázkových dat (šablona bez agendy, žádná vybraná linka).</summary>
    private bool ZVybranychLinek => _buildRealData is not null && _maVybraneLinky();

    private async Task VytvoritAsync()
    {
        if (JePng) await VytvoritPngAsync();
        else if (ZVybranychLinek) await ExportRealDataPdfAsync();
        else ExportPdf();
    }

    private async Task VytvoritPngAsync()
    {
        var path = ShowSaveDialog("Vytvořit PNG", "png");
        if (path is null) return;
        try
        {
            PersistParams();
            IReadOnlyList<DataContext> stranky;
            if (ZVybranychLinek)
            {
                var paramsSnapshot = new Dictionary<string, object?>(_paramValues);
                stranky = await Task.Run(() => _buildRealData!(paramsSnapshot, PrubehNacitani())) ?? [];
                Probiha = false;
                if (stranky.Count == 0)
                {
                    StatusText = ZadnaData(VynechaneLinky?.Invoke(), "Databáze nevrátila žádná data.", _nazev);
                    NastavHlaseniBezDat($"{_nazev.ZadnaVybrana} nemá data", otevrit: true);
                    return;
                }
            }
            else stranky = [_buildMockData(_paramValues) ?? new DataContext()];

            StatusText = "Generuji PNG…";
            int pocet = await Task.Run(() => RequestRenderPng?.Invoke(path, stranky) ?? 0);
            if (pocet == 1) OpenPdf(path);
            StatusText = (pocet == 1
                ? $"PNG uloženo: {Path.GetFileName(path)}"
                : $"PNG uloženo: {pocet} souborů ({Path.GetFileNameWithoutExtension(path)}_001.png …)")
                + (ZVybranychLinek ? DovetekVynechanych(VynechaneLinky?.Invoke()) : "");
            if (ZVybranychLinek) NastavHlaseniBezDat($"{_nazev.Shoda("Některé", "Některé", "Někteří", "Některá")} {_nazev.Mnozne} nemají data a {_nazev.Shoda("nevytiskly", "nevytiskly", "nevytiskli", "nevytiskla")} se", otevrit: false);
        }
        catch (Exception ex)
        {
            Probiha = false;
            UkazChybu(ex);
        }
    }

    // ── Uspořádání na list ────────────────────────────────────────────────────

    /// <summary>Panel uspořádání na list pod formulářem.</summary>
    public UsporadaniListuViewModel Usporadani { get; }

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
        // Uspořádání na list už není ve formuláři - kreslí se v panelu pod ním (`B186`).

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
            "number" => float.TryParse(fieldValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : null,
            _        => (object)fieldValue
        };

        if (name == SheetLayoutKeys.Meritko) Usporadani.NastavMeritko(Meritko);

        // Export pole (split/název/imposice) neovlivňují obsah sestavy → zbytečně neobnovovat náhled.
        if (!name.StartsWith("__EXPORT", StringComparison.Ordinal))
            _onParamsChanged?.Invoke();
    }

    /// <summary>Vyhodnotí <c>visibleWhen</c> parametru proti aktuálním hodnotám (sdílený dict).
    /// Logika v <see cref="TemplateParameter.IsVisible"/> - jeden zdroj pro preview lištu i tisk.</summary>
    public bool IsParamVisible(string name) =>
        _parameters.FirstOrDefault(x => x.Name == name)?.IsVisible(_paramValues, _template) ?? true;

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

    /// <summary>
    /// Export náhledu (ukázková data) - **záměrně synchronní**, dokud jde o jednu datovou sadu.
    /// ⚠️ Kdo to bude převádět na pozadí: nestačí `Task.Run`. Tahle cesta renderuje nad ŽIVOU
    /// šablonou (`RenderToPdf` → template null → `State.CurrentTemplate`) a dnes to nevadí právě
    /// proto, že synchronní běh uživateli nedovolí mezitím editovat. Async bez `DeepClone`
    /// snapshotu by zavedl závodu s preview renderem - viz `ExportRealDataPdfAsync` níže, které
    /// to dělá správně.
    /// </summary>
    private void ExportPdf()
    {
        SheetLayoutOptions? sheets;
        try { sheets = BuildSheetOptions(); }
        catch (ArgumentException ex) { StatusText = $"⚠ {ex.Message}"; return; }

        var filePath = ShowSaveDialog("Vytvořit PDF z ukázkových dat");
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
            UkazChybu(ex);
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
            ? ShowSaveDialog("Vytvořit PDF")
            : ShowFolderDialog("Vytvořit PDF - vyberte složku");
        if (target is null) return;

        try
        {
            PersistParams();
            StatusText = "Načítání dat z databáze…";

            // Fetch na threadpoolu - batch přes všechny instance jsou HTTP volání per linka;
            // sync by zamrazil dialog na desítky sekund (R1). Snapshot parametrů: uživatel je
            // může během fetche v dialogu měnit (sdílený dict s preview lištou).
            var paramsSnapshot = new Dictionary<string, object?>(_paramValues);
            var dataPages = await Task.Run(() => _buildRealData(paramsSnapshot, PrubehNacitani()));

            if (dataPages is null or { Count: 0 })
            {
                // Nejčastější příčina není výpadek DB, ale žádná vybraná linka - navést uživatele.
                StatusText = ZadnaData(VynechaneLinky?.Invoke(),
                    $"Databáze nevrátila žádná data. Zkontrolujte, že je v hlavním okně {_nazev.Shoda("vybraná", "vybraný", "vybraný", "vybrané")} {_nazev.Jednotne} (štítek vedle agendy).", _nazev);
                NastavHlaseniBezDat($"{_nazev.ZadnaVybrana} nemá data", otevrit: true);
                return;
            }
            string vynechane = DovetekVynechanych(VynechaneLinky?.Invoke());
            // Vynechané linky jako hlášky - okno se po PDF neotvírá, počet je ve stavovém řádku (`B193g`).
            NastavHlaseniBezDat($"{_nazev.Shoda("Některé", "Některé", "Někteří", "Některá")} {_nazev.Mnozne} nemají data a {_nazev.Shoda("nevytiskly", "nevytiskly", "nevytiskli", "nevytiskla")} se", otevrit: false);

            // Kontrolu všech linek tisk sám nespouští (rozhodnutí Jan 5. 10.): PDF vznikne tak jako
            // tak a kontrola by ho jen zdržela. Kdo chce hlášky za všechny linky, má „Zkontrolovat".
            Probiha = false;

            StatusText = "Generuji PDF…";
            // Skládání 1×1 papír jen zvětší (A5 šablona + list „A4 na výšku" = A4 s jednou vývěskou
            // uprostřed). Volba se persistuje per agenda, takže uživatel nemá jak zjistit proč -
            // zákazník to hlásil jako chybu tisku (#13158). Proto varování, ne neutrální popis.
            string sheetInfo = PopisUsporadani(sheets) + PopisMeritka();

            if (splitMode == ExportSplitMode.Single)
            {
                // Render na threadpoolu - callback rendruje nad snapshotem šablony (DeepClone
                // ve wiring), takže nekoliduje s preview renderem UI threadu.
                await Task.Run(() => RequestRenderMultiPdf?.Invoke(target, dataPages, sheets, Meritko));
                OpenPdf(target);
                StatusText = $"PDF uloženo: {Path.GetFileName(target)}{sheetInfo}{vynechane}";
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
            StatusText = $"{fileCount} PDF uloženo do složky{sheetInfo}{vynechane}";
        }
        catch (Exception ex)
        {
            UkazChybu(ex);
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

    private string CurrentFileNamePattern() =>
        NazevSouboruVzor(_paramValues.GetValueOrDefault(ExportFileNameKey) as string, _elementKind);

    /// <summary>Vzor názvu souboru: uložený vlastní vzor uživatele, jinak výchozí podle druhu agendy.</summary>
    internal static string NazevSouboruVzor(string? ulozeny, string? elementKind) =>
        string.IsNullOrWhiteSpace(ulozeny) || ulozeny == PuvodniVychoziNazev
            ? AgendaCapabilities.VychoziNazevSouboru(elementKind)
            : ulozeny;

    /// <summary>Imposice z exportních polí (sdílený dict) + rozměru stránky šablony.
    /// Null = vypnutá. ArgumentException = mřížka se na list nevejde (hláška pro uživatele).</summary>
    /// <remarks>Bez panelu (jednostránková šablona) se neskládá - uložená volba je za agendu, takže
    /// by jinak platila i tam, kde ji uživatel nevidí ani nemůže vypnout.</remarks>
    private SheetLayoutOptions? BuildSheetOptions() => Usporadani.UkazatPanel
        ? SheetComposer.FromParamValues(_paramValues, _template.PageSize)
        : null;

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
    /// <remarks>Po linkách = podle linky vybrané v seznamu, ne podle pole z dat. Do 24. 10. se dělilo
    /// podle <c>CISLO_LINKY</c>: u MHD je to licenční číslo, linky se stejným nebo prázdným skončily
    /// v jednom PDF a agenda bez linek (Faktury) dala jedno PDF vždy.</remarks>
    internal static IEnumerable<List<DataContext>> GroupForExport(IReadOnlyList<DataContext> pages, ExportSplitMode mode) =>
        mode switch
        {
            ExportSplitMode.PerLine => KontrolaVystupu.PoLinkach(pages).Select(g => g.Stranky.ToList()),
            ExportSplitMode.PerStop => pages.Select(p => new List<DataContext> { p }),
            _                       => [pages.ToList()],
        };

    /// <summary>Vyhodnotí pattern nad daty, ošetří neplatné znaky a kolize (přípona _2, _3, …).
    /// Prázdný pattern = jméno vybrané instance.</summary>
    internal static string BuildFileName(DataContext page, string pattern, HashSet<string> used)
    {
        var resolved = string.IsNullOrWhiteSpace(pattern)
            ? page.GetString(KontrolaVystupu.LinkaKlic)
            : page.ResolvePlaceholders(pattern);
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

    private string? ShowSaveDialog(string title, string pripona = "pdf")
    {
        var dlg = new SaveFileDialog
        {
            Title = title,
            Filter = $"{pripona.ToUpperInvariant()} soubory (*.{pripona})|*.{pripona}",
            DefaultExt = pripona,
            FileName = (string.IsNullOrWhiteSpace(_template.Name)
                ? "sablona" : _template.Name) + "." + pripona
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
