using System.ComponentModel;
using System.Runtime.CompilerServices;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Layout;

namespace TemplateDesigner;

/// <summary>Položka nabídky papíru (wire klíč + popis s počtem míst).</summary>
public sealed record VolbaListu(string Klic, string Popis);

/// <summary>Co se na list skládá.</summary>
public enum RezimListu
{
    /// <summary>Stránky jdou na místa po řadě (1, 2, 3…).</summary>
    ZaSebou,
    /// <summary>Každá stránka vyplní celý list kopiemi.</summary>
    Kopie,
    /// <summary>První stránky dokladů na přední stranu listu, druhé na zadní.</summary>
    Oboustranne,
}

/// <summary>Místo na listu v nákresu.</summary>
public sealed record MistoListuVM(string Popis, bool JePrazdne);

/// <summary>
/// Panel „Uspořádání na list" tiskového okna: uživatel volí jen papír a co se na něj skládá,
/// počet míst se spočítá z velikosti stránky (rozhodnutí Jan 28. 9.). Nákres pod volbami jen
/// ukazuje, co kam padne.
/// </summary>
/// <remarks>
/// Hodnoty jdou do týchž klíčů jako dřív (<see cref="SheetLayoutKeys"/>) - staré uložené volby
/// (za sebou / každá stránka na celý list / oboustranně) tak platí beze změny.
/// </remarks>
public class UsporadaniListuViewModel : INotifyPropertyChanged
{
    private const float SirkaListuPx = 200f;

    private static readonly (string Klic, string Popis)[] Papiry =
    [
        ("A4_PORTRAIT", "A4 na výšku"), ("A4_LANDSCAPE", "A4 na šířku"),
        ("A3_PORTRAIT", "A3 na výšku"), ("A3_LANDSCAPE", "A3 na šířku"),
    ];

    private readonly Dictionary<string, object?> _hodnoty;
    private readonly PageSize _stranka;
    private float _meritko = 1f;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <param name="hodnoty">Sdílené hodnoty tisku - panel z nich čte i do nich zapisuje.</param>
    /// <param name="pocetStranek">Kolik stránek má doklad (stránek šablony).</param>
    public UsporadaniListuViewModel(Dictionary<string, object?> hodnoty, int pocetStranek, PageSize stranka, float meritko = 1f)
    {
        _hodnoty = hodnoty;
        _stranka = stranka;
        _meritko = meritko;
        PocetStranek = Math.Max(1, pocetStranek);
        // Mřížka se už nevolí - uložená menší by jinak tiše platila dál.
        if (JeZapnuto) _hodnoty[SheetLayoutKeys.Grid] = SheetLayoutKeys.GridAuto;
        if (Rezim == RezimListu.Oboustranne && !NabiziOboustranne) Rezim = RezimListu.ZaSebou;
        NabiziKopie = Rezim == RezimListu.Kopie;
        NabiziOrezoveZnacky = OrezoveZnacky;
        UkazatPanel = PocetStranek >= 2 || JeZapnuto;
    }

    public int PocetStranek { get; }

    /// <summary>
    /// Panel se ukáže jen u šablony o víc stránkách (rozhodnutí Jan 28. 9.) - u jednostránkové
    /// jen mate. Výjimka je už zapnuté uspořádání (dvě vývěsky na A4 z dřívějška): bez panelu by
    /// platilo dál a nešlo by ho vidět ani vypnout.
    /// </summary>
    public bool UkazatPanel { get; }

    /// <summary>Oboustranný tisk dává smysl jen u dokladu o víc stránkách (líc a rub).</summary>
    public bool NabiziOboustranne => PocetStranek >= 2;

    public bool JednaStranka => PocetStranek < 2;

    /// <summary>
    /// Kopie se nová volba nenabízí (rozhodnutí Jan 28. 9. - nebylo jasné, k čemu je). Zůstává
    /// vidět jen u toho, kdo ji má uloženou z dřívějška („Každá stránka na celý list"), aby se mu
    /// tisk tiše nezměnil.
    /// </summary>
    public bool NabiziKopie { get; }

    /// <summary>Ořezové značky se nově nenabízejí (rozhodnutí Jan 28. 9. - panel má být hlavně
    /// přehledný); vidí je jen ten, kdo je má zapnuté z dřívějška, aby o ně tiše nepřišel.</summary>
    public bool NabiziOrezoveZnacky { get; }

    // ── Papír ────────────────────────────────────────────────────────────────

    private string Format => _hodnoty.GetValueOrDefault(SheetLayoutKeys.Sheet) as string is { Length: > 0 } f
        ? f : SheetLayoutKeys.SheetNone;

    public bool JeZapnuto => Format != SheetLayoutKeys.SheetNone && SheetComposer.ParseSheetFormat(Format) is not null;

    /// <summary>Papíry. Papír, na který se vejde jen jedna stránka, se nenabízí - nic by neskládal
    /// (#13158); zůstane jen, když je už zvolený.</summary>
    public IReadOnlyList<VolbaListu> Formaty =>
    [
        new(SheetLayoutKeys.SheetNone, "Vypnuto (každá stránka na vlastní list)"),
        .. Papiry.Select(p => (p, Mist: Mist(p.Klic)))
            .Where(x => x.Mist > 1 || x.p.Klic == Format)
            .Select(x => new VolbaListu(x.p.Klic, x.p.Popis)),
    ];

    public VolbaListu VybranyFormat
    {
        get => Formaty.FirstOrDefault(f => f.Klic == Format) ?? Formaty[0];
        set
        {
            if (value is null || value.Klic == Format) return;
            _hodnoty[SheetLayoutKeys.Sheet] = value.Klic;
            _hodnoty[SheetLayoutKeys.Grid] = SheetLayoutKeys.GridAuto;
            Obnov();
        }
    }

    // ── Co se skládá ─────────────────────────────────────────────────────────

    public RezimListu Rezim
    {
        get => Zapnuto(SheetLayoutKeys.Repeat) ? RezimListu.Kopie
             : Zapnuto(SheetLayoutKeys.Oboustranne) ? RezimListu.Oboustranne
             : RezimListu.ZaSebou;
        set
        {
            _hodnoty[SheetLayoutKeys.Repeat] = value == RezimListu.Kopie ? "true" : "false";
            _hodnoty[SheetLayoutKeys.Oboustranne] = value == RezimListu.Oboustranne ? "true" : "false";
            Obnov();
        }
    }

    public bool ZaSebou     { get => Rezim == RezimListu.ZaSebou;     set { if (value) Rezim = RezimListu.ZaSebou; } }
    public bool Kopie       { get => Rezim == RezimListu.Kopie;       set { if (value) Rezim = RezimListu.Kopie; } }
    public bool Oboustranne { get => Rezim == RezimListu.Oboustranne; set { if (value) Rezim = RezimListu.Oboustranne; } }

    public bool OrezoveZnacky
    {
        get => Zapnuto(SheetLayoutKeys.Marks);
        set { _hodnoty[SheetLayoutKeys.Marks] = value ? "true" : "false"; Obnov(); }
    }

    /// <summary>Měřítko tisku se změnilo ve formuláři - počet míst se tím mění.</summary>
    public void NastavMeritko(float meritko)
    {
        if (Math.Abs(meritko - _meritko) < 0.0001f) return;
        _meritko = meritko;
        Obnov();
    }

    // ── Nákres ───────────────────────────────────────────────────────────────

    private SheetLayoutOptions? Volby
    {
        get
        {
            try { return SheetComposer.FromParamValues(new Dictionary<string, object?>(_hodnoty) { [SheetLayoutKeys.Meritko] = _meritko }, _stranka); }
            catch (ArgumentException) { return null; }
        }
    }

    public bool MaNakres => JeZapnuto && Volby is not null;
    public int Radku => Volby?.Rows ?? 1;
    public int Sloupcu => Volby?.Cols ?? 1;

    private (float W, float H) List => SheetComposer.ParseSheetFormat(Format) ?? (297f, 210f);
    private float PxNaMm => SirkaListuPx / List.W;
    public double ListSirkaPx => SirkaListuPx;
    public double ListVyskaPx => List.H * PxNaMm;
    public double MistoSirkaPx => _stranka.Width * _meritko * PxNaMm;
    public double MistoVyskaPx => _stranka.Height * _meritko * PxNaMm;

    /// <summary>Co padne na přední stranu prvního listu.</summary>
    public IReadOnlyList<MistoListuVM> PredniMista => Mista(zadni: false);

    /// <summary>Co padne na zadní stranu (jen oboustranně) - už zrcadlené podle převracení listu.</summary>
    public IReadOnlyList<MistoListuVM> ZadniMista => Mista(zadni: true);

    private List<MistoListuVM> Mista(bool zadni)
    {
        if (Volby is not { } o) return [];
        int n = o.SlotsPerSheet;
        var mista = new MistoListuVM[n];
        for (int i = 0; i < n; i++)
        {
            string popis = Rezim switch
            {
                RezimListu.Kopie => "stránka 1",
                RezimListu.Oboustranne => $"{i + 1}. doklad · {(zadni ? "rub" : "líc")}",
                _ when PocetStranek == 1 => $"{i + 1}.",
                _ => $"{i / PocetStranek + 1}. doklad · str. {i % PocetStranek + 1}",
            };
            mista[zadni ? SheetComposer.ZadniPozice(i, o) : i] = new MistoListuVM(popis, false);
        }
        return [.. mista];
    }


    // ── Pomocné ──────────────────────────────────────────────────────────────

    private int Mist(string klic)
    {
        if (SheetComposer.ParseSheetFormat(klic) is not { } list) return 0;
        float w = _stranka.Width * _meritko, h = _stranka.Height * _meritko;
        return Math.Max(0, (int)(list.W / w)) * Math.Max(0, (int)(list.H / h));
    }


    private bool Zapnuto(string klic) => _hodnoty.GetValueOrDefault(klic) is var v
        && (v is true || (v is string s && (s == "1" || string.Equals(s, "true", StringComparison.OrdinalIgnoreCase))));

    private void Obnov()
    {
        foreach (var n in new[] { nameof(Formaty), nameof(VybranyFormat), nameof(JeZapnuto), nameof(MaNakres), nameof(Rezim),
                     nameof(ZaSebou), nameof(Kopie), nameof(Oboustranne), nameof(OrezoveZnacky), nameof(Radku), nameof(Sloupcu),
                     nameof(ListSirkaPx), nameof(ListVyskaPx), nameof(MistoSirkaPx), nameof(MistoVyskaPx),
                     nameof(PredniMista), nameof(ZadniMista) })
            OnPropertyChanged(n);
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
