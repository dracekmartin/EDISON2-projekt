using System.IO;

namespace TemplateDesigner.Tests.Conventions;

/// <summary>
/// Hlídá, že žádný test nezávisí na **stroji, kde běží** - konkrétně na souboru mimo repozitář,
/// který si test sám nevytvoří (nebo si ho vytvoří pod pevným jménem, takže mezi běhy stárne).
///
/// Proč to má vlastní hlídače: `pre-push` pouští sadu **na stroji autora**, kde takový soubor
/// existuje a je svěží, takže je tam vždy zeleno. CI, které by sadu pustilo na čistém stroji,
/// nemáme - chyba je tedy principiálně nevidět, dokud na ni nenarazí kolega. Přesně to se stalo
/// 10. 8.: odtiskový test si při prvním běhu sejmul baseline do `%TEMP%` a po první **zamýšlené**
/// změně kreslení začal kolegovi hlásit „kreslení se rozešlo" o ničem reálném.
///
/// Pravidlo, které z toho platí: test smí záviset jen na obsahu repozitáře a na tom, co si sám
/// v běhu vytvoří (pod unikátním jménem). Cokoli lokálního = **vypnuté defaultně** a zapnuté
/// proměnnou prostředí, ať to nemůže spadnout někomu, kdo o tom neví.
///
/// Co hlídač **nezachytí**: závislost na fontech z OS, na síti / dev serveru a na obsahu proměnných
/// prostředí. Na tu třídu je jediná skutečná pojistka běh na čistém stroji, tedy CI.
/// </summary>
public class MachineLocalStateGuardTests
{
    /// <summary>Soubory s vědomou výjimkou - důvod je součástí záznamu, ne jen jméno.</summary>
    private static readonly Dictionary<string, string> Vyjimky = new()
    {
        ["ElementRenderSnapshotTests.cs"] =
            "baseline odtisků kreslení je nástroj na vyžádání - porovnání běží jen s TD_RENDER_BASELINE=1, "
            + "bez proměnné se test souboru vůbec nedotkne",
    };

    [Fact]
    public void ZadnyTest_NezavisiNaSouboruMimoRepozitar()
    {
        var problems = new List<string>();

        foreach (var file in TestSources())
        {
            string name = Path.GetFileName(file);
            if (Vyjimky.ContainsKey(name)) continue;

            foreach (var (line, text) in FindViolations(File.ReadAllLines(file)))
                problems.Add($"{name}:{line}: {text}");
        }

        Assert.True(problems.Count == 0,
            "Test závisí na souboru mimo repozitář se stálým jménem (nebo na uživatelské složce). "
            + "Na stroji autora je takový test zelený vždy, kolegovi spadne, jakmile jeho kopie "
            + "toho souboru zestará. Pracovní soubor pojmenuj unikátně (Guid.NewGuid()), nebo celou "
            + "kontrolu zapni jen proměnnou prostředí a doplň výjimku s důvodem:\n"
            + string.Join("\n", problems));
    }

    /// <summary>Výjimka, která přestala platit, je horší než žádná - mlčky by krylo nový prohřešek
    /// ve stejném souboru.</summary>
    [Fact]
    public void Vyjimky_JsouStalePotreba()
    {
        var sources = TestSources().ToDictionary(Path.GetFileName, f => f);

        foreach (var (name, reason) in Vyjimky)
        {
            Assert.True(sources.ContainsKey(name), $"Výjimka na neexistující soubor: {name}");
            Assert.True(FindViolations(File.ReadAllLines(sources[name])).Count > 0,
                $"Soubor {name} už na pevný soubor mimo repozitář nesahá - vyhoď ho z výjimek "
                + $"(důvod byl: {reason}).");
        }
    }

    // ── Detekce ──────────────────────────────────────────────────────────────

    /// <summary>Uživatelské složky - do reálného profilu uživatele test sahat nemá vůbec.</summary>
    private static readonly string[] UzivatelskeSlozky =
    [
        "GetFolderPath(", "GetEnvironmentVariable(\"APPDATA\")", "GetEnvironmentVariable(\"LOCALAPPDATA\")",
        "GetEnvironmentVariable(\"USERPROFILE\")", "GetEnvironmentVariable(\"TEMP\")",
        "GetEnvironmentVariable(\"TMP\")",
    ];

    /// <summary>Co dělá jméno unikátní na běh - takový soubor mezi běhy nezůstane a nezestárne.</summary>
    private static readonly string[] UnikatniJmeno =
        ["Guid.NewGuid", "GetRandomFileName", "GetTempFileName", "ProcessId", "Ticks"];

    internal static List<(int Line, string Text)> FindViolations(string[] lines)
    {
        var hits = new List<(int, string)>();

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].TrimStart();
            if (line.StartsWith("//") || line.StartsWith("*")) continue;   // komentář nic nedělá

            if (UzivatelskeSlozky.Any(line.Contains))
            {
                hits.Add((i + 1, lines[i].Trim()));
            }
            else if (line.Contains("GetTempPath"))
            {
                // Unikátní jméno se hledá v celém příkazu, ne jen na řádku - Path.Combine(...) se
                // běžně láme a Guid.NewGuid() pak leží až pod GetTempPath().
                if (!UnikatniJmeno.Any(Statement(lines, i).Contains))
                    hits.Add((i + 1, lines[i].Trim()));
            }
        }

        return hits;
    }

    /// <summary>Řádek plus pokračování až po středník (nejvýš 3 řádky - delší řetěz nemá smysl).</summary>
    private static string Statement(string[] lines, int start)
    {
        string text = lines[start];
        for (int i = start + 1; i < lines.Length && i <= start + 2 && !text.Contains(';'); i++)
            text += " " + lines[i].Trim();
        return text;
    }

    // ── Samotná detekce musí být ověřená, jinak by mlčela ────────────────────

    [Theory]
    // ── v pořádku ────────────────────────────────────────────────────────────
    [InlineData("var p = Path.Combine(Path.GetTempPath(), $\"td-{Guid.NewGuid():N}.pdf\");", false)]
    [InlineData("var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());", false)]
    [InlineData("var p = Path.Combine(RepoRoot(), \"schemas\", \"mhd.schema.json\");", false)]
    [InlineData("// baseline v Path.GetTempPath() - jen popis, ne přístup", false)]
    // ── prohřešky ────────────────────────────────────────────────────────────
    [InlineData("var p = Path.Combine(Path.GetTempPath(), \"td-render-baseline.txt\");", true)]
    [InlineData("var p = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);", true)]
    [InlineData("var p = Environment.GetEnvironmentVariable(\"APPDATA\") + \"/m-line/local.json\";", true)]
    public void Detekce_PoznaProhresek(string line, bool ceka)
    {
        Assert.Equal(ceka, FindViolations([line]).Count > 0);
    }

    [Fact]
    public void Detekce_CteCelyPrikazNejenRadek()
    {
        // Unikátní jméno až na druhém řádku - kontrola po jednotlivých řádcích by tohle nahlásila
        // jako prohřešek, i když v pořádku je.
        string[] rozlomenyOk = ["var p = Path.Combine(Path.GetTempPath(),", "    $\"td-{Guid.NewGuid():N}.txt\");"];
        string[] rozlomeny = ["var p = Path.Combine(Path.GetTempPath(),", "    \"td-render-baseline.txt\");"];

        Assert.Empty(FindViolations(rozlomenyOk));
        Assert.NotEmpty(FindViolations(rozlomeny));
    }

    [Fact]
    public void Detekce_HlasiJenRadekSeSpouctecem()
    {
        // Okno příkazu se nesmí otevírat na řádku bez spouštěče, jinak hlášku zaplní `{` a hlavičky.
        string[] blok = ["{", "    var path = Path.Combine(", "        Path.GetTempPath(), \"pevne.txt\");", "}"];

        var hits = FindViolations(blok);

        Assert.Equal(3, Assert.Single(hits).Line);
    }

    // ── Zdrojáky testů ───────────────────────────────────────────────────────

    /// <summary>Všechny testovací projekty v repu, ne jen TemplateDesigner - kolegovi je jedno,
    /// který projekt mu sadu shodí. Vlastní zdroják vynechán: nese ukázky prohřešků v `InlineData`.</summary>
    private static IEnumerable<string> TestSources()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "EDISON2.sln")))
            root = root.Parent;
        Assert.NotNull(root);

        var files = root!.GetDirectories("*.Tests")
            .SelectMany(d => d.GetFiles("*.cs", SearchOption.AllDirectories))
            .Where(f => !f.FullName.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.FullName.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && f.Name != nameof(MachineLocalStateGuardTests) + ".cs")
            .Select(f => f.FullName)
            .ToList();

        Assert.NotEmpty(files);   // prázdno = špatná cesta, ne že je vše v pořádku
        return files;
    }
}
