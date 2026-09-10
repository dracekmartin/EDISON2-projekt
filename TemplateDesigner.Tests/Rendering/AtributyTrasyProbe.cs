using System.Text.Json.Nodes;
using ALWA.SYS;
using TemplateDesigner.Models;
using TemplateDesigner.Serialization;
using TemplateDesigner.Services;
using Xunit;
using Xunit.Abstractions;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>Docasna sonda: ktere atributy trasy zastavky se v datech opravdu vyskytuji (B123).</summary>
public class AtributyTrasyProbe(ITestOutputHelper vystup)
{
    /// <summary>Ciselnik 16 „atributy trasy" (JRapiCis.mac:1283).</summary>
    private static readonly Dictionary<string, string> Ciselnik = new()
    {
        ["P"] = "podtrzeno s prijezdem a odjezdem", ["T"] = "tucne",
        ["p"] = "podtrzene",                        ["N"] = "jen nastup",
        ["V"] = "jen vystup",                       ["x"] = "na znameni",
        ["y"] = "na znameni od 20:00",              ["z"] = "na znameni prac. dny od 22:00",
        ["A"] = "autobus",                          ["L"] = "vlak",
        ["e"] = "Metro A",                          ["f"] = "Metro B",
        ["g"] = "Metro C",                          ["O"] = "letadlo",
        ["D"] = "lod",                              ["C"] = "clo",
        ["J"] = "s prijezdem a odjezdem",           ["o"] = "podtrzeno nad i pod",
        ["S"] = "tisk stanoviste",                  ["G"] = "paragraf",
        ["a"] = "paragraf 1",                       ["b"] = "paragraf 2",
        ["c"] = "paragraf 3",                       [">"] = "odjezdova zastavka (souhrn)",
        ["<"] = "prijezdova zastavka (souhrn)",     ["E"] = "nacestna zastavka",
        ["H"] = "cilova zastavka pro tablo",
    };

    [Fact]
    public void ZmerVyskyt()
    {
        if (Environment.GetEnvironmentVariable("TD_ATRIBUTY_PROBE") != "1") return;

        int limit = int.TryParse(Environment.GetEnvironmentVariable("TD_ATRIBUTY_LIMIT"), out var l) ? l : 40;
        string[] firmy = (Environment.GetEnvironmentVariable("TD_ATRIBUTY_FIRMY")
                          ?? "CB_CSADAD,ZL_KOVED,CB_GWBUS").Split(',');
        string user = Environment.GetEnvironmentVariable("CACHE_USER")
                      ?? Environment.GetEnvironmentVariable("USERNAME") ?? "";

        // znak → (kolik radku, prvni tri priklady "firma/linka")
        var vyskyt = new Dictionary<string, (int Pocet, List<string> Priklady)>();
        int linekCelkem = 0, radkuCelkem = 0;

        foreach (var idfa in firmy)
        {
            var mlRest = new MLRest("192.168.16.1", 57770)
            {
                ClassNamespace = "MLINE", DataNamespace = idfa, Idfa = idfa,
                LoginName = user, FullName = user, UserID = user,
                Mlp = user, Locale = "cs_CZ", EdisonVersion = "PROBE-1.0"
            };

            var seznamIn = new CVar();
            seznamIn["limit"] = new CVar(limit.ToString());
            CVar seznam;
            try { seznam = mlRest.Query("ALVA.TemplateDesigner.Agendy.LinkovyJrAd", "GetAvailableInstances", seznamIn); }
            catch (Exception ex) { vystup.WriteLine($"{idfa}: seznam linek selhal - {ex.Message}"); continue; }

            var linky = JsonNode.Parse(seznam["1"]?.Value?.ToString() ?? "[]") as JsonArray ?? [];
            vystup.WriteLine($"{idfa}: {linky.Count} linek k projiti");

            foreach (var l2 in linky)
            {
                string? id = l2?["value"]?.ToString();
                if (string.IsNullOrEmpty(id)) continue;

                var inVar = new CVar();
                inVar["SKUPINA_LINEK_ID"] = new CVar("1");
                inVar["LINKA_ID"] = new CVar(id);
                if (Environment.GetEnvironmentVariable("TD_ATRIBUTY_OBOUSMERNE") == "1")
                    inVar["OBOUSMERNY_TISK"] = new CVar("true");
                CVar result;
                try { result = mlRest.Query("ALVA.TemplateDesigner.Agendy.LinkovyJrAd", "GetData", inVar); }
                catch { continue; }
                if (mlRest.RestStatus.State == MLRStatus.EState.Error) continue;

                var pages = CacheAgendaProvider.ParsePages(
                    result.Select(kvp => (kvp.Key, kvp.Value?.Value?.ToString())));
                if (pages is null) continue;

                linekCelkem++;
                foreach (var page in pages)
                    foreach (var radek in page.GetRows("ZASTAVKY") ?? [])
                    {
                        radkuCelkem++;
                        if (radek.GetValue("ATRIBUTY") is not JsonArray pole) continue;
                        if (Environment.GetEnvironmentVariable("TD_ATRIBUTY_UKAZ") is { Length: > 0 } hledany
                            && pole.Any(a => a?.ToString() == hledany))
                            vystup.WriteLine($"    {idfa}/{id} poradi {radek.GetString("PORADI")}: "
                                + $"nazev={radek.GetString("NAZEV_ZASTAVKY")} "
                                + $"nastupiste={radek.GetString("NASTUPISTE")} "
                                + $"atributy={string.Join("", pole.Select(a => a?.ToString()))}");
                        foreach (var znak in pole.Select(a => a?.ToString()).Where(a => !string.IsNullOrEmpty(a)))
                        {
                            var (pocet, priklady) = vyskyt.GetValueOrDefault(znak!, (0, []));
                            if (priklady.Count < 3 && !priklady.Contains($"{idfa}/{id}"))
                                priklady.Add($"{idfa}/{id}");
                            vyskyt[znak!] = (pocet + 1, priklady);
                        }
                    }
            }
        }

        vystup.WriteLine("");
        vystup.WriteLine($"=== {linekCelkem} linek, {radkuCelkem} zastavkovych radku ===");
        foreach (var (znak, (pocet, priklady)) in vyskyt.OrderByDescending(v => v.Value.Pocet))
            vystup.WriteLine($"  {znak,-3} {Ciselnik.GetValueOrDefault(znak, "?? NENI V CISELNIKU"),-42} "
                           + $"{pocet,6}x   {string.Join(", ", priklady)}");

        vystup.WriteLine("");
        vystup.WriteLine("nevyskytlo se: " + string.Join(" ", Ciselnik.Keys.Where(k => !vyskyt.ContainsKey(k))));
    }
}
