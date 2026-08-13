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
                if (JeRozbityLiteral(lines[i]))
                    problems.Add($"{Path.GetFileName(file)}:{i + 1}: {lines[i].Trim()}");
        }

        Assert.True(problems.Count == 0,
            "Neuzavřený textový literál v ObjectScriptu (uvozovka uvnitř textu se musí zdvojit "
            + "na \"\"). Takhle rozbitá třída se nasadí a spadne teprve uživateli:\n"
            + string.Join("\n", problems));
    }

    /// <summary>Má řádek rozbitý textový literál? Dvě různé podoby téže chyby - viz
    /// <see cref="EndsInsideString"/> a <see cref="ZavrenoUprostredTextu"/>.</summary>
    internal static bool JeRozbityLiteral(string line) =>
        EndsInsideString(line) || ZavrenoUprostredTextu(line);

    /// <summary>Skončí řádek uprostřed textového literálu? (lichý počet uvozovek)</summary>
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

    /// <summary>Ukončila uvozovka text uprostřed, i když jich je na řádku sudý počet?
    ///
    /// <para><see cref="EndsInsideString"/> tuhle polovinu chyby **neuvidí**: v
    /// <c>"„&lt;" nevzniklo (" _ y _ ")"</c> se první text zavře už po <c>„&lt;</c>, ale zbylé
    /// uvozovky počet dorovnají, takže řádek „končí mimo text" a projde. Třída se pak nezkompiluje
    /// a Caché test spadne **bez hlášky** (na serveru zůstane stará verze) - najeto 13. 8.</para>
    ///
    /// <para>Poznávací znak platí **jen uvnitř závorek** (seznam argumentů): tam po textu smí přijít
    /// jen oddělovač nebo operátor, takže holé slovo za uvozovkou znamená, že text měl pokračovat.
    /// Mimo závorky je totéž **legitimní** - <c>s:group'="" param.group = group</c> je běžný
    /// podmíněný příkaz, kde za textem stojí jeho argument.</para></summary>
    internal static bool ZavrenoUprostredTextu(string line)
    {
        bool inString = false;
        int zavorek = 0;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (inString)
            {
                if (c != '"') continue;
                if (i + 1 < line.Length && line[i + 1] == '"') { i++; continue; }
                inString = false;
                if (zavorek > 0 && SlovoZaUvozovkou(line, i + 1)) return true;
            }
            else if (c == '"') inString = true;
            else if (c == ';') break;                                           // komentář
            else if (c == '/' && i + 1 < line.Length && line[i + 1] == '/') break;
            else if (c == '(') zavorek++;
            else if (c == ')' && zavorek > 0) zavorek--;
        }

        return false;
    }

    /// <summary>Stojí za zavřenou uvozovkou mezera a po ní holé slovo? Makra a proměnné s <c>$</c>
    /// se nepočítají - ty se za textem v argumentu objevit můžou.</summary>
    private static bool SlovoZaUvozovkou(string line, int idx)
    {
        if (idx >= line.Length) return false;
        if (line[idx] != ' ' && line[idx] != '\t') return false;

        string zbytek = line[idx..].TrimStart();
        return zbytek.Length > 0 && char.IsLetter(zbytek[0]);
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

    // ── Druhá podoba: uvozovek je sudě, ale text skončil uprostřed (`B78`) ───

    [Theory]
    // ── v pořádku ────────────────────────────────────────────────────────────
    [InlineData("i ctV = \"\" continue", false)]                             // po textu příkaz
    [InlineData("i lin = \"\" $$$ThrowStatus(chyba)", false)]                // po textu makro
    [InlineData("s x = \"a\" _ \"b\"", false)]                               // po textu operátor
    [InlineData("d f.AddListItem(\"Top\", \"Nahoře\")", false)]              // po textu oddělovač
    [InlineData("i key = \"JR_ZAHLAVI\" {", false)]                          // po textu blok
    [InlineData("s f.helpText = \"Nic zvláštního\"", false)]                 // konec řádku
    [InlineData("s x = \"a\"_\"b\"", false)]                                 // operátor bez mezery
    [InlineData("s x = $p(veta, \":\", 1)", false)]
    [InlineData("s f.helpText = \"… (výchozí \"\"×\"\")\"", false)]          // správně zdvojené
    // Podmíněný příkaz: za textem stojí jeho argument, a to je v pořádku - mimo závorky se pravidlo
    // neuplatní, jinak by test hlásil desítky míst v repu.
    [InlineData("s:group'=\"\" param.group = group", false)]
    [InlineData("q:slovo=\"\" slovo", false)]
    [InlineData("set:smer=\"\" smer = \"T\"", false)]
    [InlineData("i 'BodID continue ; skip \"slouceni\" a další pomocné subscripty", false)]  // komentář
    // ⚠️ Známá mez: mimo závorky se pravidlo neuplatní, takže tenhle tvar projde. Rozšířit nejde
    // bez falešných poplachů - `s:podmínka="" proměnná` vypadá stejně a je správně. V praxi se to
    // stává v argumentu (hlášky testů, helpTexty), kde závorky jsou.
    [InlineData("s zprava = \"chyba (\" pole \")\"", false)]
    // ── rozbité ──────────────────────────────────────────────────────────────
    // Přesně tvar z 13. 8.: uvozovek je sudě, ale „<" text ukončí a zbytek se rozsype.
    [InlineData("do $$$AssertEquals(x, 1, \"„<\" nevzniklo (\" _ y _ \")\")", true)]
    [InlineData("d ..Pridej(x, \"prefix \" a \" suffix\")", true)]
    public void KontrolaPoznaTextUkoncenyUprostred(string line, bool ceka)
    {
        Assert.Equal(ceka, ZavrenoUprostredTextu(line));
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
