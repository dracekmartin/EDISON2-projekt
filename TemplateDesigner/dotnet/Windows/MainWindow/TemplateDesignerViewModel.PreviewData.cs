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

/// <summary>Skutečná data náhledu a sbalitelná lišta parametrů nad náhledem.</summary>
public partial class TemplateDesignerViewModel
{
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
        // Data jiné instance můžou ukázat nebo schovat parametry, které se jí týkají (visibleWhenListedIn).
        RequestParamsVisibilityRefresh?.Invoke();
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
}
