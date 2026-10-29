using System.IO;
using System.Text;
using ALWA.SYS;
using EDControls;
using TemplateDesigner.Config;
using Xunit.Abstractions;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Snímek výstupu všech agend před sloučením kódu agend (<c>B56</c>, <c>konzistence-agend-jr.md §7</c>):
/// schéma, parametry a ukázka každé agendy z registru a data linek z testovacích sad, s výchozími parametry
/// (seznam drží sonda <c>ALVA.TemplateDesigner.Tests.SnimekAgendProbe</c>). První běh uloží snímek, každý
/// další porovná výstup bajt po bajtu a vypíše, co se liší - změna tisku se tak ukáže u kroku, který ji
/// způsobil. Snímek se v <c>B56</c> nepřepisuje; nový vznikne jen po smazání podsložky <c>snimek</c>.
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

        var rozdily = Porovnej(snimek, cil);
        output.WriteLine(rozdily.Count == 0
            ? $"Výstup všech agend beze změny ({volani.Count} souborů)."
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

            var rozdily = Porovnej(a, b);

            Assert.Equal(["chybejici.txt: chybí", "novy.txt: přibyl", "zmeneny.txt: liší se == 2 == (řádek 4)"], rozdily);
        }
        finally { Directory.Delete(koren, recursive: true); }
    }

    /// <summary>Uzly výstupu: číselné (stránky) podle čísla, pojmenované (DUVOD_PRAZDNE, CHYBA) za nimi.</summary>
    private static IEnumerable<(string Klic, string Hodnota)> Serad(CVar vysledek) =>
        vysledek.Select(kv => (Klic: kv.Key, Hodnota: kv.Value?.Value?.ToString() ?? ""))
            .OrderBy(kv => int.TryParse(kv.Klic, out var n) ? n : int.MaxValue)
            .ThenBy(kv => kv.Klic, StringComparer.Ordinal);

    /// <summary>Soubory, které chybí, přibyly, nebo se liší - u rozdílu první odlišný řádek uzlu.</summary>
    internal static List<string> Porovnej(string snimek, string aktualni)
    {
        var rozdily = new List<string>();
        var stare = Directory.GetFiles(snimek).Select(Path.GetFileName).ToHashSet();
        var nove = Directory.GetFiles(aktualni).Select(Path.GetFileName).ToHashSet();
        foreach (var f in stare.Except(nove).Order()) rozdily.Add($"{f}: chybí");
        foreach (var f in nove.Except(stare).Order()) rozdily.Add($"{f}: přibyl");
        foreach (var f in stare.Intersect(nove).Order())
        {
            var a = File.ReadAllBytes(Path.Combine(snimek, f!));
            var b = File.ReadAllBytes(Path.Combine(aktualni, f!));
            if (a.AsSpan().SequenceEqual(b)) continue;
            var ra = Encoding.UTF8.GetString(a).Split('\n');
            var rb = Encoding.UTF8.GetString(b).Split('\n');
            int i = 0;
            while (i < ra.Length && i < rb.Length && ra[i] == rb[i]) i++;
            string uzel = ra.Take(i + 1).LastOrDefault(r => r.StartsWith("== ", StringComparison.Ordinal)) ?? "";
            rozdily.Add($"{f}: liší se {uzel} (řádek {i + 1})");
        }
        return rozdily;
    }
}
