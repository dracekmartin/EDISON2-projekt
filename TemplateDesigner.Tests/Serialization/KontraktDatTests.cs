using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using TemplateDesigner.Models;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Kód návrháře čte z dat agendy jen položky zapsané v <see cref="KontraktDat"/> a každou z nich opravdu
/// čte (<c>B194g</c> R3). Nová položka, kterou prvek začne číst, tak musí do kontraktu, a ten ji pak
/// integrační <c>KontraktDatAgendTests</c> ověří na datech všech agend daného druhu. Hledají se čtení
/// z dat (<c>GetValue</c>, <c>GetString</c>, <c>GetRows</c>, i přes konstanty <see cref="StopFields"/>)
/// a klíče uvnitř vnořených objektů (<c>PoleZaznamu</c>, <c>o["…"]</c>).
/// </summary>
public class KontraktDatTests
{
    private static readonly Regex CteniLiteralem = new(
        @"\b(?:GetValue|GetString|GetRows)\(""(?<klic>[A-Z][A-Z0-9_]*)""\)", RegexOptions.Compiled);
    private static readonly Regex CteniKonstantou = new(
        @"\b(?:GetValue|GetString)\(StopFields\.(?<jmeno>\w+)\)", RegexOptions.Compiled);
    private static readonly Regex VnorenyKlic = new(
        @"(?:PoleZaznamu\(\w+, ""(?<klic>[A-Z][A-Z0-9_]*)""\)|\b\w+\[""(?<klic>[A-Z][A-Z0-9_]*)""\](?!\s*=[^=]))", RegexOptions.Compiled);

    /// <summary>Čtení, která nejsou z dat agendy. Klíč = soubor|klíč, hodnota = důvod.</summary>
    private static readonly Dictionary<string, string> MimoData = new(StringComparer.Ordinal);

    [Fact]
    public void KodCteJenPolozkyKontraktu()
    {
        var znamo = KlicePolozek().Concat(VnoreneKlice()).Concat(Kolekce()).ToHashSet(StringComparer.Ordinal);
        var mimo = Cteni()
            .Where(c => !znamo.Contains(c.Klic) && !MimoData.ContainsKey($"{c.Soubor}|{c.Klic}"))
            .Select(c => $"{c.Soubor}: {c.Klic}")
            .Distinct()
            .ToList();

        Assert.True(mimo.Count == 0,
            "Kód čte z dat položku, která není v KontraktDat - doplň ji tam (druh, kolekce, tvar):\n" + string.Join("\n", mimo));
    }

    [Fact]
    public void KazdouPolozkuKontraktuKodCte()
    {
        var ctene = Cteni().Select(c => c.Klic).ToHashSet(StringComparer.Ordinal);
        var nectene = KlicePolozek().Concat(VnoreneKlice()).Where(k => !ctene.Contains(k)).Distinct().ToList();

        Assert.True(nectene.Count == 0,
            "Položka kontraktu, kterou kód návrháře nečte - smaž ji z KontraktDat:\n" + string.Join("\n", nectene));
    }

    /// <summary>Hledání musí najít čtení všech druhů zápisu, jinak by testy výš procházely naprázdno.</summary>
    [Fact]
    public void Hledani_NajdeCteniLiteralemKonstantouIVnorene()
    {
        var cteni = Cteni();
        Assert.Contains(cteni, c => c.Klic == "ATRIBUTY");
        Assert.Contains(cteni, c => c.Klic == StopFields.StopName && c.Soubor == "StopScheduleElementBase.cs");
        Assert.Contains(cteni, c => c.Klic == "AVL");
        Assert.Contains(cteni, c => c.Klic == "POPIS");
    }

    private static IEnumerable<string> KlicePolozek() => KontraktDat.Polozky.Select(p => p.Klic);

    private static IEnumerable<string> VnoreneKlice() => KontraktDat.Polozky.SelectMany(p => p.VnoreneKlice ?? []);

    private static IEnumerable<string> Kolekce() =>
        KontraktDat.Polozky.Select(p => p.Kolekce).Where(k => k != KontraktDat.Udaje);

    /// <summary>Všechna čtení z dat v kódu návrháře: (soubor, klíč).</summary>
    private static List<(string Soubor, string Klic)> Cteni()
    {
        var konstanty = typeof(StopFields).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral)
            .ToDictionary(f => f.Name, f => (string)f.GetRawConstantValue()!);
        var vysledek = new List<(string, string)>();
        foreach (var f in Directory.GetFiles(Path.Combine(Koren(), "TemplateDesigner", "dotnet"), "*.cs", SearchOption.AllDirectories))
        {
            if (f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                || f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")) continue;
            var soubor = Path.GetFileName(f);
            if (soubor is "KontraktDat.cs" or "StopFields.cs") continue;
            var kod = File.ReadAllText(f);
            vysledek.AddRange(CteniLiteralem.Matches(kod).Select(m => (soubor, m.Groups["klic"].Value)));
            vysledek.AddRange(CteniKonstantou.Matches(kod)
                .Where(m => konstanty.ContainsKey(m.Groups["jmeno"].Value))
                .Select(m => (soubor, konstanty[m.Groups["jmeno"].Value])));
            vysledek.AddRange(VnorenyKlic.Matches(kod).Select(m => (soubor, m.Groups["klic"].Value)));
        }
        return vysledek;
    }

    private static string Koren()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EDISON2.sln")))
            dir = dir.Parent;
        dir ??= new FileInfo(ThisFile()).Directory!.Parent!.Parent;
        return dir!.FullName;
    }

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
