using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using ALWA.SYS;
using EDControls;
using TemplateDesigner.Config;
using Xunit.Abstractions;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Snímek výstupu všech agend před sloučením kódu agend (<c>B56</c>, <c>legacy-nahrada.md §4</c>):
/// schéma, parametry a ukázka každé agendy z registru a data linek z testovacích sad, s výchozími parametry
/// (seznam drží sonda <c>ALVA.TemplateDesigner.Tests.SnimekAgendProbe</c>). První běh uloží snímek, každý
/// další porovná obsah stránek a vypíše, co se liší - změna tisku se tak ukáže u kroku, který ji
/// způsobil. Snímek se nepřepisuje; nový vznikne jen po přejmenování nebo smazání podsložky <c>snimek</c>.
/// Stránky se porovnávají jako JSON: jiná hodnota nebo jiný typ (číslo × text) je rozdíl, jiné pořadí
/// položek ne - návrhář je čte podle jména. Pořadí se jen vypíše; do 30. 10. se porovnávalo bajt po bajtu
/// a každé přeskládání hlavičky (<c>B56</c> S7, S12) se muselo ověřovat skriptem mimo repo.
/// </summary>
/// <remarks>
/// Bez proměnné <c>TD_SNIMEK_AGEND</c> (složka snímku) nic nedělá - výstup závisí na datech dev serveru.
/// Připojení z <c>templatedesigner.appsettings.local.json</c> (VPN):
/// <code>TD_SNIMEK_AGEND=C:\snimek-agend dotnet test TemplateDesigner.Tests --filter SnimekAgendNaostro</code>
/// </remarks>
public class SnimekAgendNaostro(ITestOutputHelper output)
{
    private const string Sonda = "ALVA.TemplateDesigner.Tests.SnimekAgendProbe";

    [Fact]
    public void Snimek()
    {
        string? slozka = Environment.GetEnvironmentVariable("TD_SNIMEK_AGEND");
        if (string.IsNullOrEmpty(slozka)) return;

        var mlRest = CacheSchemaWiring.TryCreateMlRest(AppSettings.Load())
                     ?? throw new Exception("Caché není nakonfigurovaná");
        string snimek = Path.Combine(slozka, "snimek");
        bool prvni = !Directory.Exists(snimek);
        string cil = prvni ? snimek : Path.Combine(slozka, "aktualni");
        if (!prvni && Directory.Exists(cil)) Directory.Delete(cil, recursive: true);
        Directory.CreateDirectory(cil);

        // Jen očíslovaná volání - výstup nese i jiné uzly než seznam.
        var volani = Serad(mlRest.QueryOrThrow(Sonda, "Seznam", new CVar()))
            .Where(kv => int.TryParse(kv.Klic, out _)).Select(kv => kv.Hodnota).ToList();
        foreach (var radek in volani)
        {
            var c = radek.Split('~');
            var vstup = new CVar();
            vstup["trida"] = new CVar(c[1]);
            vstup["metoda"] = new CVar(c[2]);
            vstup["idfa"] = new CVar(c[3]);
            vstup["db"] = new CVar(c[4]);
            vstup["vstup"] = new CVar(c[5]);
            var sb = new StringBuilder();
            foreach (var (klic, hodnota) in Serad(mlRest.QueryOrThrow(Sonda, "Vystup", vstup)))
                sb.Append("== ").Append(klic).Append(" ==\n").Append(hodnota).Append('\n');
            File.WriteAllText(Path.Combine(cil, c[0] + ".txt"), sb.ToString(), new UTF8Encoding(false));
        }

        if (prvni)
        {
            output.WriteLine($"Snímek uložen: {volani.Count} souborů v {snimek}");
            return;
        }

        var (rozdily, jenPoradi) = Porovnej(snimek, cil);
        if (jenPoradi.Count > 0)
            output.WriteLine($"Jen pořadí položek se liší v {jenPoradi.Count} souborech (obsah shodný): " + string.Join(", ", jenPoradi));
        output.WriteLine(rozdily.Count == 0
            ? $"Obsah výstupu všech agend beze změny ({volani.Count} souborů)."
            : string.Join("\n", rozdily));
        Assert.True(rozdily.Count == 0, $"Výstup agend se liší od snímku v {rozdily.Count} souborech:\n"
            + string.Join("\n", rozdily));
    }

    /// <summary>Porovnání musí poznat změněný, chybějící i přibylý soubor - jinak by snímek tiše nehlídal.</summary>
    [Fact]
    public void Porovnani_PoznaKazdyRozdil()
    {
        string koren = Path.Combine(Path.GetTempPath(), "snimek-" + Guid.NewGuid().ToString("N"));
        string a = Path.Combine(koren, "a"), b = Path.Combine(koren, "b");
        Directory.CreateDirectory(a);
        Directory.CreateDirectory(b);
        try
        {
            File.WriteAllText(Path.Combine(a, "stejny.txt"), "== 1 ==\n{}\n");
            File.WriteAllText(Path.Combine(b, "stejny.txt"), "== 1 ==\n{}\n");
            File.WriteAllText(Path.Combine(a, "zmeneny.txt"), "== 1 ==\n{}\n== 2 ==\n{\"A\":1}\n");
            File.WriteAllText(Path.Combine(b, "zmeneny.txt"), "== 1 ==\n{}\n== 2 ==\n{\"A\":2}\n");
            File.WriteAllText(Path.Combine(a, "chybejici.txt"), "x");
            File.WriteAllText(Path.Combine(b, "novy.txt"), "x");
            File.WriteAllText(Path.Combine(a, "typ.txt"), "== 1 ==\n{\"CISLO\":\"1\"}\n");
            File.WriteAllText(Path.Combine(b, "typ.txt"), "== 1 ==\n{\"CISLO\":1}\n");
            File.WriteAllText(Path.Combine(a, "stranky.txt"), "== 1 ==\n{}\n");
            File.WriteAllText(Path.Combine(b, "stranky.txt"), "== 1 ==\n{}\n== 2 ==\n{}\n");
            File.WriteAllText(Path.Combine(a, "text.txt"), "== CHYBA ==\nstará hláška\n");
            File.WriteAllText(Path.Combine(b, "text.txt"), "== CHYBA ==\nnová hláška\n");

            var (rozdily, jenPoradi) = Porovnej(a, b);

            Assert.Equal([
                "chybejici.txt: chybí", "novy.txt: přibyl",
                "stranky.txt: jiné stránky (1 → 1, 2)",
                "text.txt: liší se == CHYBA == (text)",
                "typ.txt: liší se == 1 == ($.CISLO: \"1\" → 1)",
                "zmeneny.txt: liší se == 2 == ($.A: 1 → 2)"], rozdily);
            Assert.Empty(jenPoradi);
        }
        finally { Directory.Delete(koren, recursive: true); }
    }

    /// <summary>Jiné pořadí položek se stejným obsahem není rozdíl - návrhář je čte podle jména - jen se vypíše.
    /// Pořadí v poli rozdíl je (řádky, stránky).</summary>
    [Fact]
    public void Porovnani_PoradiPolozekNeniRozdil()
    {
        string koren = Path.Combine(Path.GetTempPath(), "snimek-" + Guid.NewGuid().ToString("N"));
        string a = Path.Combine(koren, "a"), b = Path.Combine(koren, "b");
        Directory.CreateDirectory(a);
        Directory.CreateDirectory(b);
        try
        {
            File.WriteAllText(Path.Combine(a, "poradi.txt"), "== 1 ==\n{\"values\":{\"A\":1,\"B\":\"x\"},\"rows\":{\"R\":[{\"P\":1,\"Q\":2}]}}\n");
            File.WriteAllText(Path.Combine(b, "poradi.txt"), "== 1 ==\n{\"rows\":{\"R\":[{\"Q\":2,\"P\":1}]},\"values\":{\"B\":\"x\",\"A\":1}}\n");
            File.WriteAllText(Path.Combine(a, "radky.txt"), "== 1 ==\n{\"R\":[1,2]}\n");
            File.WriteAllText(Path.Combine(b, "radky.txt"), "== 1 ==\n{\"R\":[2,1]}\n");

            var (rozdily, jenPoradi) = Porovnej(a, b);

            Assert.Equal(["radky.txt: liší se == 1 == ($.R[0]: 1 → 2)"], rozdily);
            Assert.Equal(["poradi.txt"], jenPoradi);
        }
        finally { Directory.Delete(koren, recursive: true); }
    }

    /// <summary>Uzly výstupu: číselné (stránky) podle čísla, pojmenované (DUVOD_PRAZDNE, CHYBA) za nimi.</summary>
    private static IEnumerable<(string Klic, string Hodnota)> Serad(CVar vysledek) =>
        vysledek.Select(kv => (Klic: kv.Key, Hodnota: kv.Value?.Value?.ToString() ?? ""))
            .OrderBy(kv => int.TryParse(kv.Klic, out var n) ? n : int.MaxValue)
            .ThenBy(kv => kv.Klic, StringComparer.Ordinal);

    /// <summary>Soubory, které chybí, přibyly, nebo se liší obsahem (u rozdílu první odlišné místo stránky),
    /// a zvlášť soubory, ve kterých se liší jen pořadí položek.</summary>
    internal static (List<string> Rozdily, List<string> JenPoradi) Porovnej(string snimek, string aktualni)
    {
        var rozdily = new List<string>();
        var jenPoradi = new List<string>();
        var stare = Directory.GetFiles(snimek).Select(Path.GetFileName).ToHashSet();
        var nove = Directory.GetFiles(aktualni).Select(Path.GetFileName).ToHashSet();
        foreach (var f in stare.Except(nove).Order()) rozdily.Add($"{f}: chybí");
        foreach (var f in nove.Except(stare).Order()) rozdily.Add($"{f}: přibyl");
        foreach (var f in stare.Intersect(nove).Order())
        {
            string ta = File.ReadAllText(Path.Combine(snimek, f!), Encoding.UTF8);
            string tb = File.ReadAllText(Path.Combine(aktualni, f!), Encoding.UTF8);
            if (ta == tb) continue;
            string? rozdil = RozdilSouboru(Uzly(ta), Uzly(tb));
            if (rozdil is null) jenPoradi.Add(f!);
            else rozdily.Add($"{f}: {rozdil}");
        }
        return (rozdily, jenPoradi);
    }

    /// <summary>Uzly souboru snímku v pořadí: hlavička „== klíč ==" a text pod ní.</summary>
    private static List<(string Klic, string Text)> Uzly(string text)
    {
        var uzly = new List<(string Klic, string Text)>();
        foreach (var radek in text.Split('\n'))
        {
            if (radek.StartsWith("== ", StringComparison.Ordinal) && radek.EndsWith(" ==", StringComparison.Ordinal))
                uzly.Add((radek[3..^3], ""));
            else if (uzly.Count > 0)
                uzly[^1] = (uzly[^1].Klic, uzly[^1].Text.Length == 0 ? radek : uzly[^1].Text + "\n" + radek);
        }
        return uzly.Select(u => (u.Klic, u.Text.TrimEnd('\n'))).ToList();
    }

    private static string? RozdilSouboru(List<(string Klic, string Text)> a, List<(string Klic, string Text)> b)
    {
        if (!a.Select(u => u.Klic).SequenceEqual(b.Select(u => u.Klic)))
            return $"jiné stránky ({string.Join(", ", a.Select(u => u.Klic))} → {string.Join(", ", b.Select(u => u.Klic))})";
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i].Text == b[i].Text) continue;
            string? rozdil;
            try { rozdil = RozdilObsahu(JsonNode.Parse(a[i].Text), JsonNode.Parse(b[i].Text), "$"); }
            catch (JsonException) { rozdil = "text"; }
            if (rozdil is not null) return $"liší se == {a[i].Klic} == ({rozdil})";
        }
        return null;
    }

    private static readonly JsonSerializerOptions Citelne = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>První místo, kde se dva JSON liší (cesta a hodnoty), nebo null. Položky objektu podle jména,
    /// prvky pole podle pořadí; hodnota včetně typu (číslo × text).</summary>
    private static string? RozdilObsahu(JsonNode? a, JsonNode? b, string cesta)
    {
        switch (a, b)
        {
            case (JsonObject oa, JsonObject ob):
                foreach (var (klic, hodnota) in oa)
                {
                    if (!ob.ContainsKey(klic)) return $"{cesta}.{klic}: chybí";
                    if (RozdilObsahu(hodnota, ob[klic], $"{cesta}.{klic}") is { } r) return r;
                }
                return ob.Select(kv => kv.Key).FirstOrDefault(k => !oa.ContainsKey(k)) is { } novy ? $"{cesta}.{novy}: přibyl" : null;
            case (JsonArray pa, JsonArray pb):
                if (pa.Count != pb.Count) return $"{cesta}: {pa.Count} → {pb.Count} prvků";
                for (int i = 0; i < pa.Count; i++)
                    if (RozdilObsahu(pa[i], pb[i], $"{cesta}[{i}]") is { } r) return r;
                return null;
            default:
                string sa = a?.ToJsonString(Citelne) ?? "null", sb = b?.ToJsonString(Citelne) ?? "null";
                return sa == sb ? null : $"{cesta}: {Zkrat(sa)} → {Zkrat(sb)}";
        }
    }

    private static string Zkrat(string s) => s.Length <= 80 ? s : s[..77] + "...";
}
