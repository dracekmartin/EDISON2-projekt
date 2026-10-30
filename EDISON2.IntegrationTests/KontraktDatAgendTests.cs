using System.Text.Json.Nodes;
using ALWA.SYS;
using EDControls;
using TemplateDesigner.Models;

namespace EDISON2.IntegrationTests;

/// <summary>
/// Agendy návrháře posílají položky, které čtou prvky, a ve tvaru, který prvky čekají - kontrakt
/// <see cref="KontraktDat"/> (<c>B194g</c> R3) proti skutečným datům na dev serveru. Chybějící položka se
/// dřív projevila jen prázdným prvkem a jiný tvar shodil náhled (atributy zastávky, 8. 10.); nová agenda
/// téhož druhu tu musí projít celým kontraktem, ne jen zlatým odtiskem, který pro ni neexistuje.
/// Linky bere ze sond návrháře (<c>SnimekAgendProbe</c>: sady testů a zlaté odtisky, výchozí parametry).
/// </summary>
[Trait("Category", "Integration")]
public class KontraktDatAgendTests
{
    private const string Sonda = "ALVA.TemplateDesigner.Tests.SnimekAgendProbe";
    private const string Registr = "ALVA.TemplateDesigner.API.AgendyRegistr";

    private const string AdSloupky = "sloupky jsou číselník AD, MHD má jen označník (ux-conventions §8)";

    /// <summary>Položky, které konkrétní agenda vědomě neposílá, s důvodem. Výjimka, která přestala platit
    /// (agenda položku posílá), test taky zastaví - seznam nesmí lhát. Položky vázané na příznak registru
    /// (varianty tras, vzory sloupců - <see cref="KontraktDat.Polozka.Priznak"/>) tu nejsou: výjimku odvodí test
    /// z registru; zbyly ty, pro které příznak neexistuje.</summary>
    private static readonly Dictionary<(string Agenda, string Kolekce, string Klic), string> Bez = new()
    {
        [("ZASTAVKOVY_JR_MHD", KontraktDat.Udaje, "ZDROJ_STANOVISTE")] = "volba označník / sloupek je jen v AD, " + AdSloupky,
        [("ZASTAVKOVY_JR_MHD", "ZASTAVKY", StopFields.Sloupek)] = AdSloupky,
        [("ZASTAVKOVY_JR_MHD", "ZASTAVKY", StopFields.SloupekSpoju)] = AdSloupky,
        [("ZASTAVKOVY_JR_MHD", "ZASTAVKY", StopFields.SloupekPopis)] = AdSloupky,
        [("ZASTAVKOVY_JR_MHD", "ZASTAVKY", StopFields.NastupisteSpoju)] = "označníky podle spojů bere AD z tras spojů, MHD je nemá",
        [("LINKOVY_JR_MHD", "ZASTAVKY", "TC")] = "MHD nemá tarifní čísla",
        [("LINKOVY_JR_MHD", "ZASTAVKY", "MHD")] = "sloupec MHD (přestup na městskou dopravu) je pojem AD",
        [("LINKOVY_JR_MHD", "SPOJE", "SMER")] = "směr spoje slouží jen obousměrnému tisku, který linkový JŘ MHD nemá",
        [("LINKOVY_JR_MHD", "SPOJE", "DOPRAVCI")] = "dopravce do hlavičky bere linkový JŘ MHD z vývěsky MHD, ne ze spojů",
    };

    [DevServerFact]
    public void AgendyPosilajiPolozkyKontraktuVeSpravnemTvaru()
    {
        var mlRest = DevServer.Connect();
        var druhTridy = DruhyAgend(mlRest);
        var stranky = StrankyAgend(mlRest, druhTridy, out var chyby);
        var nalezy = new List<string>(chyby);

        foreach (var (agenda, (druh, strankyAgendy)) in stranky)
        {
            var priznaky = druhTridy.Values.First(a => a.Kod == agenda).Priznaky;
            foreach (var polozka in KontraktDat.Polozky.Where(p => p.Druh == druh || p.Druh == KontraktDat.Kazda))
            {
                var hodnoty = Hodnoty(strankyAgendy, polozka);
                int chybi = hodnoty.Count(h => h is null);
                bool vyjimka = Bez.ContainsKey((agenda, polozka.Kolekce, polozka.Klic))
                               || (polozka.Priznak is { } priznak && !priznaky.Contains(priznak));

                if (vyjimka && chybi < hodnoty.Count)
                    nalezy.Add($"{agenda}: {polozka.Kolekce} {polozka.Klic} posílá, výjimka v Bez nebo příznak {polozka.Priznak} v registru už neplatí");
                else if (polozka.Volitelna is null && !vyjimka && hodnoty.Count == 0)
                    nalezy.Add($"{agenda}: kolekce {polozka.Kolekce} je na všech linkách prázdná, {polozka.Klic} nejde ověřit");
                else if (polozka.Volitelna is null && !vyjimka && chybi > 0)
                    nalezy.Add($"{agenda}: {polozka.Kolekce} {polozka.Klic} chybí v {chybi} z {hodnoty.Count}");

                int spatne = hodnoty.Count(h => h is not null && !MaTvar(h, polozka.Tvar));
                if (spatne > 0)
                    nalezy.Add($"{agenda}: {polozka.Kolekce} {polozka.Klic} nemá tvar {polozka.Tvar} v {spatne} z {hodnoty.Count}");
            }
        }

        Assert.True(stranky.Count >= 5, $"ověřeno jen {stranky.Count} agend - sonda nebo registr nevrátily data");
        Assert.True(nalezy.Count == 0, "Data agend neodpovídají kontraktu prvků (KontraktDat):\n" + string.Join("\n", nalezy));
    }

    /// <summary>Hodnoty položky ze všech stránek (údaje) nebo všech řádků kolekce; null = položka chybí.</summary>
    private static List<JsonNode?> Hodnoty(List<JsonObject> stranky, KontraktDat.Polozka polozka) =>
        polozka.Kolekce == KontraktDat.Udaje
            ? [.. stranky.Select(s => s["values"] is JsonObject v && v.TryGetPropertyValue(polozka.Klic, out var h) ? h ?? JsonValue.Create("") : null)]
            : [.. stranky.SelectMany(s => s["rows"]?[polozka.Kolekce] as JsonArray ?? [])
                  .OfType<JsonObject>()
                  .Select(r => r.TryGetPropertyValue(polozka.Klic, out var h) ? h ?? JsonValue.Create("") : null)];

    private static bool MaTvar(JsonNode hodnota, KontraktDat.Tvar tvar) => tvar switch
    {
        KontraktDat.Tvar.Hodnota => hodnota is JsonValue,
        KontraktDat.Tvar.PoleHodnot => hodnota is JsonArray a && a.All(x => x is JsonValue),
        KontraktDat.Tvar.PoleObjektu => hodnota is JsonArray a && a.All(x => x is JsonObject),
        _ => false,
    };

    /// <summary>Třída agendy → (kód, druh prvků, zapnuté příznaky) z registru, jak je dostane návrhář.</summary>
    private static Dictionary<string, (string Kod, string Druh, HashSet<string> Priznaky)> DruhyAgend(IMLRest mlRest)
    {
        var json = mlRest.QueryOrThrow(Registr, "ListAgendy", new CVar())["1"]?.Value?.ToString() ?? "[]";
        return JsonNode.Parse(json)!.AsArray().OfType<JsonObject>()
            .ToDictionary(a => a["cacheClass"]!.ToString(), a => (a["code"]!.ToString(), a["elementKind"]?.ToString() ?? "",
                a.Where(kv => kv.Value is JsonValue v && v.TryGetValue<bool>(out var b) && b).Select(kv => kv.Key).ToHashSet()));
    }

    /// <summary>Stránky dat agend zastávkového a linkového druhu z linek sondy: kód → (druh, stránky).</summary>
    private static Dictionary<string, (string Druh, List<JsonObject> Stranky)> StrankyAgend(
        IMLRest mlRest, Dictionary<string, (string Kod, string Druh, HashSet<string> Priznaky)> druhTridy, out List<string> chyby)
    {
        chyby = [];
        var vysledek = new Dictionary<string, (string, List<JsonObject>)>();
        var seznam = mlRest.QueryOrThrow(Sonda, "Seznam", new CVar());
        foreach (var kv in seznam.Where(kv => int.TryParse(kv.Key, out _)))
        {
            var c = (kv.Value?.Value?.ToString() ?? "").Split('~');
            if (c.Length < 6 || c[2] != "GetData") continue;
            if (!druhTridy.TryGetValue(c[1], out var agenda) || agenda.Druh is not (KontraktDat.Zastavkove or KontraktDat.Linkove)) continue;

            var vstup = new CVar();
            vstup["trida"] = new CVar(c[1]);
            vstup["metoda"] = new CVar(c[2]);
            vstup["idfa"] = new CVar(c[3]);
            vstup["db"] = new CVar(c[4]);
            vstup["vstup"] = new CVar(c[5]);
            var vystup = mlRest.QueryOrThrow(Sonda, "Vystup", vstup);
            if (vystup.Any(x => x.Key == "PRESKOCENO")) continue;
            if (vystup.FirstOrDefault(x => x.Key == "CHYBA").Value?.Value?.ToString() is { Length: > 0 } chyba)
            {
                chyby.Add($"{c[0]}: agenda spadla - {chyba}");
                continue;
            }

            if (!vysledek.TryGetValue(agenda.Kod, out var zaznam))
                vysledek[agenda.Kod] = zaznam = (agenda.Druh, []);
            zaznam.Item2.AddRange(vystup
                .Where(x => int.TryParse(x.Key, out _))
                .Select(x => JsonNode.Parse(x.Value?.Value?.ToString() ?? "{}") as JsonObject)
                .OfType<JsonObject>());
        }
        return vysledek;
    }
}
