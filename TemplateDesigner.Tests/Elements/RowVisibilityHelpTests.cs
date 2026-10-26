using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using EDControls.EDInputForm;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Expressions;
using TemplateDesigner.Models.Schema;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Nápověda „Viditelnost řádku" (Seznam zastávek, Tabulka spojů) smí jmenovat jen pole, která řádek
/// v agendě šablony opravdu nese. Do 23. 10. ji formulář vedl napevno a Tabulka spojů radila
/// <c>POPIS</c> a <c>ZONA</c>, které žádná agenda nemá - podmínka podle příkladu nic neskryla (`B194a` C7).
/// Seznam teď doplní návrhář ze schématu agendy; formulář nese jen úvod a příklad.
/// </summary>
public class RowVisibilityHelpTests
{
    private static Field PoleViditelnosti(string napoveda) =>
        new() { Key = ElementFormBinder.ViditelnostRadkuKlic, HelpText = napoveda };

    private static DataSchema Schema(params string[] poleZastavky) => new()
    {
        FieldCollections =
        [
            new DataFieldCollection { CollectionKey = "ZASTAVKY", Fields = [.. poleZastavky.Select(p => new DataField { FieldName = p })] },
            new DataFieldCollection { CollectionKey = "SPOJE", Fields = [new DataField { FieldName = "CISLO_SPOJE" }] },
        ],
    };

    [Fact]
    public void Napoveda_JmenujePoleRadkuZAgendy_OpakovaneJednou()
    {
        var pole = PoleViditelnosti("Zastávka, kde výraz neplatí, se nezobrazí.");
        var schema = Schema("NAZEV_ZASTAVKY", "ZONY");

        ElementFormBinder.DoplnPoleRadku([pole], schema, "ZASTAVKY");
        ElementFormBinder.DoplnPoleRadku([pole], schema, "ZASTAVKY");   // re-Prefill panelu

        Assert.Equal("Zastávka, kde výraz neplatí, se nezobrazí. Pole řádku: NAZEV_ZASTAVKY, ZONY.", pole.HelpText);
    }

    [Fact]
    public void BezSchematu_ZustaneObecnyText()
    {
        var pole = PoleViditelnosti("Zastávka, kde výraz neplatí, se nezobrazí.");

        ElementFormBinder.DoplnPoleRadku([pole], null, "ZASTAVKY");

        Assert.Equal("Zastávka, kde výraz neplatí, se nezobrazí.", pole.HelpText);
    }

    /// <summary>
    /// Formulář je společný všem agendám, které prvek nabízejí, takže pole v jeho textu (příklad) musí
    /// mít řádek zastávky v každé z nich - a pevný výčet polí v něm být nesmí.
    /// </summary>
    [Theory]
    [InlineData("LineTableElementEdit.cls", new[] { "LinkovyJrAd.cls", "LinkovyJrMhd.cls" })]
    [InlineData("StopListElementEdit.cls", new[] { "ZastavkoveJrAd.cls", "ZastavkoveJrMhd.cls" })]
    public void TextFormulare_JenPoleKazdeAgendy(string formular, string[] agendy)
    {
        string napoveda = NapovedaViditelnosti(formular);
        var pouzita = Regex.Matches(napoveda, @"\{([A-Z_0-9]+)\}").Select(m => m.Groups[1].Value).ToList();

        Assert.DoesNotContain("Pole řádku", napoveda);
        Assert.NotEmpty(pouzita);
        foreach (var agenda in agendy)
        {
            var poleZastavky = PoleZastavek(agenda);
            Assert.NotEmpty(poleZastavky);
            Assert.All(pouzita, p => Assert.True(poleZastavky.Contains(p), $"{formular}: {agenda} nemá v řádku zastávky pole {p}"));
        }
    }

    [Fact]
    public void PrikladZNapovedy_SkryjeJmenovanouZastavku()
    {
        var vyraz = Regex.Match(NapovedaViditelnosti("LineTableElementEdit.cls"), @"Např\. (.+?)\.?$").Groups[1].Value;
        DataContext Radek(string nazev) => new(new Dictionary<string, object?> { ["NAZEV_ZASTAVKY"] = nazev });

        Assert.False(RowConditions.IsVisible(vyraz, Radek("Nádraží")));
        Assert.True(RowConditions.IsVisible(vyraz, Radek("Poliklinika Sever")));
    }

    /// <summary>Text nápovědy pole viditelnosti řádku ze zdrojáku formuláře (zdvojené uvozovky rozbalené).</summary>
    private static string NapovedaViditelnosti(string formular)
    {
        var radky = File.ReadAllLines(Path.Combine(CacheDir(), "EDInputForm", formular));
        int od = Array.FindIndex(radky, r => r.Contains($"\"{ElementFormBinder.ViditelnostRadkuKlic}\""));
        Assert.True(od >= 0, $"{formular}: pole viditelnosti řádku nenalezeno");
        var napoveda = radky.Skip(od).Select(r => Regex.Match(r, "helpText = \"(.*)\"\\s*$")).First(m => m.Success);
        return napoveda.Groups[1].Value.Replace("\"\"", "\"");
    }

    /// <summary>Pole kolekce ZASTAVKY ve schématu agendy (čte se sekvenčně jako v <c>AgendaSchemaDriftTests</c>).</summary>
    private static HashSet<string> PoleZastavek(string agenda)
    {
        var vysledek = new HashSet<string>(StringComparer.Ordinal);
        string? kolekce = null;
        foreach (var radek in File.ReadAllLines(Path.Combine(CacheDir(), "Agendy", agenda)))
        {
            if (Regex.Match(radek, "collectionKey\\s*=\\s*\"([A-Z_0-9]+)\"") is { Success: true } k) kolekce = k.Groups[1].Value;
            if (kolekce != "ZASTAVKY") continue;
            foreach (Match m in Regex.Matches(radek, "AddField\\(\\s*\\w+\\s*,\\s*\"([A-Z_0-9]+)\""))
                vysledek.Add(m.Groups[1].Value);
        }
        return vysledek;
    }

    private static string CacheDir()
    {
        // Kořen repa od výstupní složky nahoru, jinak podle cesty k tomuhle souboru (test závisí jen na repu).
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EDISON2.sln"))) dir = dir.Parent;
        dir ??= new FileInfo(ThisFile()).Directory!.Parent!.Parent!;
        return Path.Combine(dir.FullName, "TemplateDesigner", "CACHE", "ALVA", "TemplateDesigner");
    }

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
