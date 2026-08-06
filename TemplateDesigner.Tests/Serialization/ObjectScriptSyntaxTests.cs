using System.IO;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Statická kontrola zdrojáků Caché: **neuzavřený textový literál**. V ObjectScriptu se uvozovka
/// uvnitř textu musí zdvojit (<c>""</c>); obyčejná <c>"</c> text ukončí a zbytek řádku se rozsype
/// na <c>&lt;SYNTAX&gt;</c>.
///
/// Proč to má vlastní test: tuhle chybu **nechytí nic ostatního**. Rozbitá třída se nasadí,
/// .NET testy o Caché zdrojácích nic nevědí, `%UnitTest` kompiluje jen testovací package - a projeví
/// se to teprve tím, že uživateli spadne otevření formuláře prvku (stalo se 5. 8. při doplňování
/// vysvětlivek: „(výchozí „×")" ukončilo string uprostřed).
///
/// Kontrola projde znak po znaku a hlídá, že řádek nekončí uprostřed textu. Komentáře řeší
/// implicitně - <c>//</c> mimo text ukončí zpracování řádku, uvnitř textu se ignoruje.
/// </summary>
public class ObjectScriptSyntaxTests
{
    [Fact]
    public void ZadnyClsSoubor_NemaNeuzavrenyTextovyLiteral()
    {
        var problems = new List<string>();
        var files = Directory.GetFiles(CacheRoot(), "*.cls", SearchOption.AllDirectories);

        Assert.NotEmpty(files);   // prázdno = špatná cesta, ne že je vše v pořádku

        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
                if (EndsInsideString(lines[i]))
                    problems.Add($"{Path.GetFileName(file)}:{i + 1}: {lines[i].Trim()}");
        }

        Assert.True(problems.Count == 0,
            "Neuzavřený textový literál v ObjectScriptu (uvozovka uvnitř textu se musí zdvojit "
            + "na \"\"). Takhle rozbitá třída se nasadí a spadne teprve uživateli:\n"
            + string.Join("\n", problems));
    }

    /// <summary>Skončí řádek uprostřed textového literálu?</summary>
    internal static bool EndsInsideString(string line)
    {
        bool inString = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (inString)
            {
                if (c != '"') continue;
                // Zdvojená uvozovka = escapovaná, text pokračuje dál.
                if (i + 1 < line.Length && line[i + 1] == '"') { i++; continue; }
                inString = false;
            }
            else if (c == '"')
            {
                inString = true;
            }
            else if (c == '/' && i + 1 < line.Length && line[i + 1] == '/')
            {
                break;   // komentář - zbytek řádku syntaxi neovlivní
            }
        }

        return inString;
    }

    // ── Samotná kontrola musí být ověřená, jinak by mlčela ───────────────────

    [Theory]
    // ── v pořádku ────────────────────────────────────────────────────────────
    [InlineData("s f.helpText = \"Nic zvláštního\"", false)]
    [InlineData("s x = \"Text se \"\"zdvojenou\"\" uvozovkou\"", false)]   // správné escapování
    [InlineData("// komentář s jednou \" uvozovkou", false)]
    [InlineData("s x = \"a\" _ \"b\"", false)]
    [InlineData("d f.AddListItem(\"Top\", \"Nahoře\")", false)]
    // ── rozbité ──────────────────────────────────────────────────────────────
    // Přesně ta chyba z 5. 8.: „×" zavřené obyčejnou uvozovkou uprostřed textu, takže
    // závěrečná uvozovka řádku otevře nový (neuzavřený) literál.
    [InlineData("s f.helpText = \"dostane značku (výchozí „×\")\"", true)]
    [InlineData("s x = \"neuzavřeno", true)]
    [InlineData("s x = \"a\" _ \"b", true)]
    public void KontrolaPoznaNeuzavrenyLiteral(string line, bool ceka)
    {
        Assert.Equal(ceka, EndsInsideString(line));
    }

    private static string CacheRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EDISON2.sln")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        string path = Path.Combine(dir!.FullName, "TemplateDesigner", "CACHE");
        Assert.True(Directory.Exists(path), $"Nenalezena složka Caché zdrojáků: {path}");
        return path;
    }
}
