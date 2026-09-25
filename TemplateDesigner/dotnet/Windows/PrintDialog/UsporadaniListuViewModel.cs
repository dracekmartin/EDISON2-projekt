using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Layout;

namespace TemplateDesigner;

/// <summary>Položka výběru (wire klíč + popis).</summary>
public sealed record VolbaListu(string Klic, string Popis);

/// <summary>Stránka dokladu v paletě - přetahuje se do polí listu.</summary>
public sealed record StrankaSablonyVM(int Index, string Nazev);

/// <summary>Pole listu v nákresu.</summary>
public sealed record PoleListuVM(bool Zadni, int Index, string Popis, bool JePrazdne);

/// <summary>
/// Panel „Uspořádání na list" tiskového okna (`B186`): formát listu, mřížka, režim (jeden / víc
/// dokladů na list) a nákres 1. a 2. strany PDF, do jejichž polí se přetahují stránky dokladu.
/// </summary>
/// <remarks>
/// Mluví jen o stranách PDF, ne o lící a rubu papíru - vyrábí se PDF ve správném pořadí a
/// oboustranný tisk si zapne tiskárna (rozhodnutí Jan 25. 9.).
/// </remarks>
public class UsporadaniListuViewModel : INotifyPropertyChanged
{
    private const float SirkaListuPx = 200f;

    private readonly PageSize _stranka;
    private readonly Action<UsporadaniListu> _zmena;
    private readonly int _pocetStranek;
    private UsporadaniListu _plan;

    public event PropertyChangedEventHandler? PropertyChanged;

    public static IReadOnlyList<VolbaListu> Formaty { get; } =
    [
        new(SheetLayoutKeys.SheetNone, "Vypnuto (1 stránka = 1 list)"),
        new("A4_PORTRAIT", "A4 na výšku"),
        new("A4_LANDSCAPE", "A4 na šířku"),
        new("A3_PORTRAIT", "A3 na výšku"),
        new("A3_LANDSCAPE", "A3 na šířku"),
    ];

    public IReadOnlyList<StrankaSablonyVM> Stranky { get; }
    public ObservableCollection<PoleListuVM> PredniPole { get; } = [];
    public ObservableCollection<PoleListuVM> ZadniPole { get; } = [];
    public ObservableCollection<VolbaListu> Mrizky { get; } = [];

    /// <param name="plan">Uložené uspořádání; null = vypnuto.</param>
    /// <param name="pocetStranek">Kolik stránek má doklad (stránek šablony).</param>
    /// <param name="zmena">Každá změna - uspořádání s formátem „Vypnuto", když je vypnuté.</param>
    public UsporadaniListuViewModel(UsporadaniListu? plan, int pocetStranek, PageSize stranka,
        Action<UsporadaniListu> zmena)
    {
        _stranka = stranka;
        _zmena = zmena;
        _pocetStranek = Math.Max(1, pocetStranek);
        Stranky = [.. Enumerable.Range(0, _pocetStranek).Select(i => new StrankaSablonyVM(i, $"Stránka {i + 1}"))];
        _plan = plan?.Normalizuj() ?? new UsporadaniListu
        {
            Format = SheetLayoutKeys.SheetNone, JedenDoklad = UsporadaniListu.VychoziJedenDoklad(_pocetStranek),
        };
        Obnov();
    }

    public UsporadaniListu Plan => _plan;

    public bool JeZapnuto => _plan.Format is not ("" or SheetLayoutKeys.SheetNone);

    public VolbaListu VybranyFormat
    {
        get => Formaty.FirstOrDefault(f => f.Klic == _plan.Format) ?? Formaty[0];
        set
        {
            if (value is null || value.Klic == _plan.Format) return;
            bool bylVypnuty = !JeZapnuto;
            _plan.Format = value.Klic;
            if (JeZapnuto)
            {
                var (r, c) = NejvetsiMrizka();
                // Poprvé zapnuto, nebo se dosavadní mřížka na nový list nevejde: nejčastější tvar
                // pro zvolený režim.
                if (bylVypnuty || !Vejde(_plan.Radku, _plan.Sloupcu))
                    _plan = Vychozi(_plan.JedenDoklad, value.Klic, r, c);
            }
            Zmeneno();
        }
    }

    public VolbaListu? VybranaMrizka
    {
        get => Mrizky.FirstOrDefault(m => m.Klic == $"{_plan.Radku}x{_plan.Sloupcu}");
        set
        {
            if (value is null || value.Klic == $"{_plan.Radku}x{_plan.Sloupcu}") return;
            var casti = value.Klic.Split('x');
            _plan = Vychozi(_plan.JedenDoklad, _plan.Format, int.Parse(casti[0]), int.Parse(casti[1]));
            Zmeneno();
        }
    }

    /// <summary>Jeden doklad na list (stejná stránka ve dvou polích = kopie).</summary>
    public bool JedenDoklad
    {
        get => _plan.JedenDoklad;
        set
        {
            if (value == _plan.JedenDoklad) return;
            // Přepnutí režimu = nejčastější tvar nového režimu; pole z jiného režimu by v něm
            // znamenala něco jiného (kopie × další doklad).
            _plan = Vychozi(value, _plan.Format, _plan.Radku, _plan.Sloupcu);
            Zmeneno();
        }
    }

    /// <summary>Víc dokladů na list (další výskyt stránky = další doklad).</summary>
    public bool VicDokladu
    {
        get => !JedenDoklad;
        set => JedenDoklad = !value;
    }

    public bool OrezoveZnacky
    {
        get => _plan.OrezoveZnacky;
        set { _plan.OrezoveZnacky = value; Zmeneno(); }
    }

    public int Radku => _plan.Radku;
    public int Sloupcu => _plan.Sloupcu;

    // Rozměry nákresu: list 200 px široký, pole v poměru stránky k listu.
    private (float W, float H) List => SheetComposer.ParseSheetFormat(_plan.Format) ?? (297f, 210f);
    private float Meritko => SirkaListuPx / List.W;
    public double ListSirkaPx => SirkaListuPx;
    public double ListVyskaPx => List.H * Meritko;
    public double PoleSirkaPx => _stranka.Width * Meritko;
    public double PoleVyskaPx => _stranka.Height * Meritko;

    /// <summary>„Na list 2 doklady, 2 strany PDF" - co z nákresu vyjde, jednou větou.</summary>
    public string Souhrn
    {
        get
        {
            if (!JeZapnuto) return "Každá stránka na vlastní list.";
            int n = _plan.DokladuNaList;
            string doklady = n == 1 ? "1 doklad" : n <= 4 ? $"{n} doklady" : $"{n} dokladů";
            return $"Na list {doklady}, {(_plan.DveStrany ? "2 strany PDF" : "1 strana PDF")}.";
        }
    }

    /// <summary>Dá stránku dokladu do pole (null = vyprázdní).</summary>
    public void Umisti(bool zadni, int index, int? stranka)
    {
        var strana = zadni ? _plan.Zadni : _plan.Predni;
        if (index < 0 || index >= strana.Count) return;
        strana[index] = stranka;
        Zmeneno();
    }

    /// <summary>Přesune obsah pole do jiného pole (tažení mezi poli); obsahy se prohodí.</summary>
    public void Presun(bool zZadni, int zIndex, bool naZadni, int naIndex)
    {
        var z = zZadni ? _plan.Zadni : _plan.Predni;
        var na = naZadni ? _plan.Zadni : _plan.Predni;
        if (zIndex < 0 || zIndex >= z.Count || naIndex < 0 || naIndex >= na.Count) return;
        (z[zIndex], na[naIndex]) = (na[naIndex], z[zIndex]);
        Zmeneno();
    }

    public void VycistiStranu(bool zadni)
    {
        var strana = zadni ? _plan.Zadni : _plan.Predni;
        for (int i = 0; i < strana.Count; i++) strana[i] = null;
        Zmeneno();
    }

    private UsporadaniListu Vychozi(bool jedenDoklad, string format, int radku, int sloupcu)
    {
        var u = UsporadaniListu.Vychozi(_pocetStranek, jedenDoklad, format, radku, sloupcu);
        u.OrezoveZnacky = _plan.OrezoveZnacky;
        return u;
    }

    private bool Vejde(int radku, int sloupcu)
    {
        try { SheetComposer.ResolveGrid($"{radku}x{sloupcu}", _stranka.Width, _stranka.Height, List.W, List.H); return true; }
        catch (ArgumentException) { return false; }
    }

    private (int R, int C) NejvetsiMrizka()
    {
        try { return SheetComposer.ResolveGrid(SheetLayoutKeys.GridAuto, _stranka.Width, _stranka.Height, List.W, List.H); }
        catch (ArgumentException) { return (1, 1); }
    }

    private void Zmeneno()
    {
        _plan.Normalizuj();
        Obnov();
        _zmena(_plan);
    }

    private void Obnov()
    {
        Mrizky.Clear();
        if (JeZapnuto)
            foreach (var (r, c) in new[] { (1, 1), (1, 2), (2, 1), (2, 2), (2, 3), (3, 2), (3, 3) })
                if (Vejde(r, c)) Mrizky.Add(new VolbaListu($"{r}x{c}", $"{r} × {c}"));

        PredniPole.Clear();
        ZadniPole.Clear();
        for (int i = 0; i < _plan.PoliNaStranu; i++)
        {
            PredniPole.Add(new PoleListuVM(false, i, _plan.PopisPole(false, i), _plan.Pole(false, i) is null));
            ZadniPole.Add(new PoleListuVM(true, i, _plan.PopisPole(true, i), _plan.Pole(true, i) is null));
        }
        foreach (var n in new[] { nameof(JeZapnuto), nameof(VybranyFormat), nameof(VybranaMrizka), nameof(Radku),
                     nameof(Sloupcu), nameof(ListSirkaPx), nameof(ListVyskaPx), nameof(PoleSirkaPx), nameof(PoleVyskaPx),
                     nameof(JedenDoklad), nameof(VicDokladu), nameof(OrezoveZnacky), nameof(Souhrn) })
            OnPropertyChanged(n);
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
