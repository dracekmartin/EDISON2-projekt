using System.Text.Json;
using System.Text.Json.Serialization;

namespace TemplateDesigner.Models.Layout;

/// <summary>
/// Uspořádání stránek šablony na list, jak ho uživatel nakreslí v tiskovém okně (`B186`): do polí
/// 1. a případně 2. strany PDF dá stránky dokladu (Stránka 1, Stránka 2…).
/// </summary>
/// <remarks>
/// <para><b>Vyrábí se jen PDF ve správném pořadí.</b> Oboustranný tisk řeší tiskárna - 2. strana
/// PDF jí padne na zadní stranu papíru. Panel proto nemluví o lící a rubu papíru ani o převracení
/// (rozhodnutí Jan 25. 9.); kdo potřebuje stránky na 2. straně jinak, přetáhne je tam.</para>
/// <para><b>Dva režimy</b> (<see cref="JedenDoklad"/>): jeden doklad na list - stejná stránka ve
/// dvou polích je kopie (pro řezání); víc dokladů na list - každý další výskyt téže stránky na
/// straně je další doklad („Stránka 1 · Stránka 1" = dvě stazky vedle sebe, #12854).</para>
/// </remarks>
public sealed class UsporadaniListu
{
    /// <summary>Formát listu - wire klíč jako u <see cref="SheetComposer.ParseSheetFormat"/>.</summary>
    public string Format { get; set; } = "A4_LANDSCAPE";
    public int Radku { get; set; } = 1;
    public int Sloupcu { get; set; } = 2;

    /// <summary>Pole 1. strany PDF po řádcích; hodnota = pořadí stránky dokladu, null = prázdné.</summary>
    public List<int?> Predni { get; set; } = [];

    /// <summary>Pole 2. strany PDF; prázdná = každý list je jen jedna strana PDF.</summary>
    public List<int?> Zadni { get; set; } = [];

    /// <summary>
    /// Na list jde jeden doklad: všechna pole nesou týž doklad a stejná stránka ve dvou polích je
    /// kopie. False = víc dokladů na list, další výskyt stránky = další doklad.
    /// </summary>
    public bool JedenDoklad { get; set; } = true;

    public bool OrezoveZnacky { get; set; }

    [JsonIgnore] public int PoliNaStranu => Math.Max(1, Radku) * Math.Max(1, Sloupcu);

    /// <summary>Má list dvě strany PDF? Ano, jakmile má 2. strana aspoň jedno pole.</summary>
    [JsonIgnore] public bool DveStrany => Zadni.Any(p => p is not null);

    /// <summary>Dorovná seznamy polí na velikost mřížky (po změně mřížky, po načtení).</summary>
    public UsporadaniListu Normalizuj()
    {
        Radku = Math.Max(1, Radku);
        Sloupcu = Math.Max(1, Sloupcu);
        Predni = Dorovnej(Predni);
        Zadni = Dorovnej(Zadni);
        return this;

        List<int?> Dorovnej(List<int?> pole) =>
            [.. pole.Take(PoliNaStranu), .. Enumerable.Repeat<int?>(null, Math.Max(0, PoliNaStranu - pole.Count))];
    }

    /// <summary>Kolik dokladů spotřebuje jeden list: nejvíc výskytů jedné stránky na jedné straně.</summary>
    [JsonIgnore]
    public int DokladuNaList
    {
        get
        {
            if (JedenDoklad) return 1;
            int Nejvic(List<int?> pole) => pole.Where(p => p is not null).GroupBy(p => p)
                .Select(g => g.Count()).DefaultIfEmpty(0).Max();
            return Math.Max(1, Math.Max(Nejvic(Predni), Nejvic(Zadni)));
        }
    }

    /// <summary>
    /// Co se tiskne do pole: kolikátý doklad listu (0 = první) a která stránka šablony. Null =
    /// prázdné pole.
    /// </summary>
    public (int Doklad, int Stranka)? Pole(bool zadni, int index)
    {
        var strana = zadni ? Zadni : Predni;
        if (index < 0 || index >= strana.Count || strana[index] is not int stranka) return null;
        int doklad = JedenDoklad ? 0 : strana.Take(index).Count(p => p == stranka);
        return (doklad, stranka);
    }

    /// <summary>Popis pole pro nákres: „Stránka 1", „Stránka 1 · 2. doklad", „prázdné".</summary>
    public string PopisPole(bool zadni, int index)
    {
        if (Pole(zadni, index) is not { } p) return "prázdné";
        return JedenDoklad ? $"Stránka {p.Stranka + 1}" : $"Stránka {p.Stranka + 1} · {p.Doklad + 1}. doklad";
    }

    /// <summary>Výchozí režim: šablona o jedné stránce skládá víc dokladů (vývěsky na A4), víc
    /// stránek dá celý doklad na jeden list.</summary>
    public static bool VychoziJedenDoklad(int pocetStranek) => pocetStranek > 1;

    /// <summary>
    /// Nejčastější tvar pro režim: jeden doklad = stránky dokladu vedle sebe (1 | 2); víc dokladů =
    /// na 1. straně PDF stránka 1 všude, u dvoustránkového dokladu na 2. straně stránka 2 (#12854).
    /// </summary>
    public static UsporadaniListu Vychozi(int pocetStranek, bool jedenDoklad, string format, int radku, int sloupcu)
    {
        var u = new UsporadaniListu { Format = format, Radku = radku, Sloupcu = sloupcu, JedenDoklad = jedenDoklad }.Normalizuj();
        int stranek = Math.Max(1, pocetStranek);
        for (int i = 0; i < u.PoliNaStranu; i++)
        {
            if (jedenDoklad) u.Predni[i] = i < stranek ? i : null;
            else
            {
                u.Predni[i] = 0;
                if (stranek >= 2) u.Zadni[i] = 1;
            }
        }
        return u;
    }

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    /// <summary>Načte uložené uspořádání; nesmysl nebo prázdno = null (volá se nad uloženou volbou).</summary>
    public static UsporadaniListu? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<UsporadaniListu>(json, Json)?.Normalizuj(); }
        catch (JsonException) { return null; }
    }
}
