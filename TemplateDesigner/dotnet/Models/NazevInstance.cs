namespace TemplateDesigner.Models;

/// <summary>Mluvnický rod instance agendy - podle něj se v textech shoduje přídavné jméno a sloveso.</summary>
public enum RodInstance { Zensky, Muzsky, MuzskyZivotny, Stredni }

/// <summary>
/// Jak se jmenuje instance agendy - to, co se tiskne (linka, oběh, zaměstnanec). Tvary a rod hlásí agenda
/// (`InstanceTvary`, `InstanceRod` v `BaseAgenda.cls`) a návrhář z nich skládá texty oken a hlášek.
/// Do 30. 10. byla „linka" v obecných textech napevno (`B194e` A5) a agenda report generátoru by tiskla
/// „Žádná vybraná linka nemá data" i u oběhů.
/// </summary>
/// <remarks>
/// Shodu drží text sám přes <see cref="Shoda"/>: každá věta uvede své tvary pro všechny rody. Obecné
/// skloňování přídavných jmen by bylo složitější než pár vět, které ho potřebují.
/// </remarks>
public sealed record NazevInstance(
    string Jednotne, string Genitiv, string Akuzativ,
    string Mnozne, string GenitivMnozne, string AkuzativMnozne,
    RodInstance Rod)
{
    /// <summary>Agenda bez doménového pojmu, nebo starší server, který tvary neposílá.</summary>
    public static readonly NazevInstance Obecny = new("instance", "instance", "instanci", "instance", "instancí", "instance", RodInstance.Zensky);

    /// <summary>
    /// Z hodnot registru: tvary čárkou v pořadí 1. a 2. a 4. pád jednotného čísla, 1. a 2. pád množného,
    /// volitelně 4. pád množného (jiný jen u mužského životného: zaměstnanci / zaměstnance). Rod F, M,
    /// MZ (mužský životný), N. Neúplné tvary = <see cref="Obecny"/>.
    /// </summary>
    public static NazevInstance Z(string? tvary, string? rod)
    {
        var t = (tvary ?? "").Split(',', StringSplitOptions.TrimEntries);
        if (t.Length < 5 || t.Take(5).Any(string.IsNullOrEmpty)) return Obecny;
        var r = rod?.Trim().ToUpperInvariant() switch
        {
            "M" => RodInstance.Muzsky,
            "MZ" => RodInstance.MuzskyZivotny,
            "N" => RodInstance.Stredni,
            _ => RodInstance.Zensky,
        };
        return new(t[0], t[1], t[2], t[3], t[4], t.Length > 5 && t[5] != "" ? t[5] : t[3], r);
    }

    /// <summary>Tvar slova, které se shoduje s instancí, podle jejího rodu.</summary>
    public string Shoda(string zensky, string muzsky, string zivotny, string stredni) => Rod switch
    {
        RodInstance.Muzsky => muzsky,
        RodInstance.MuzskyZivotny => zivotny,
        RodInstance.Stredni => stredni,
        _ => zensky,
    };

    /// <summary>Počet s tvarem podle čísla: „1 linka", „3 linky", „5 linek".</summary>
    public string Pocet(int n) => $"{n} {(n == 1 ? Jednotne : n is >= 2 and <= 4 ? Mnozne : GenitivMnozne)}";

    /// <summary>„Žádná vybraná linka" - začátek hlášky, že žádná vybraná instance něco nemá.</summary>
    public string ZadnaVybrana => $"{Shoda("Žádná vybraná", "Žádný vybraný", "Žádný vybraný", "Žádné vybrané")} {Jednotne}";

    /// <summary>„vybrané linky" ve 4. pádě (Zkontrolovat vybrané linky).</summary>
    public string VybraneAkuzativ => $"{Shoda("vybrané", "vybrané", "vybrané", "vybraná")} {AkuzativMnozne}";

    /// <summary>První písmeno velké (štítek, začátek věty).</summary>
    public static string SVelkym(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];
}
