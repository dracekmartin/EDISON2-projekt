using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Serialization;
using TemplateDesigner.Services;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Pojistka distribučního kanálu systémových (M-line) výchozích šablon.
///
/// <para><see cref="SystemTemplateSeeder"/> nalévá do MLINE větve to, co je zabalené v <c>.exe</c>
/// jako embedded resource pod <c>SystemTemplates/</c>. Soubor, který ve složce leží, ale zabalený
/// není, se **nikam nedostane a nic o tom neřekne** - přesně tak do 17. 8. mimo kanál ležela výchozí
/// linková šablona (`B92`). Proto se tady netestuje jen obsah souborů, ale i to, že je assembly
/// skutečně nese.</para>
///
/// <para>Metadata jsou kontrolovaná, protože chyba v nich je neviditelná stejným způsobem: prázdný
/// <c>agendaCode</c> nebo překlep v něm znamená šablonu, kterou uživatel nikdy nenajde (filtr
/// v pickeru jde přes agendu), a duplicitní <c>Id</c> znamená, že jedna šablona přepíše druhou -
/// seeder je idempotentní právě přes stabilní Id.</para>
/// </summary>
public class SystemTemplateSeedTests
{
    /// <summary>Nejmenší počet šablon, který smí projít. Chrání proti tiše zelenému testu, kdyby se
    /// složka přejmenovala nebo cesta k repu přestala platit.</summary>
    private const int MinimumTemplates = 3;

    private static IReadOnlyList<string> TemplateFiles() =>
        [.. Directory.GetFiles(SystemTemplatesDir(), "*.json").OrderBy(p => p)];

    public static TheoryData<string> AllTemplates
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var f in TemplateFiles()) data.Add(Path.GetFileName(f));
            return data;
        }
    }

    [Fact]
    public void SlozkaNeseVsechnyOcekavaneSablony()
    {
        var names = TemplateFiles().Select(Path.GetFileName).ToList();

        Assert.True(names.Count >= MinimumTemplates,
            $"V SystemTemplates/ je {names.Count} šablon, čekalo se aspoň {MinimumTemplates} - " +
            "buď nějaká zmizela, nebo test hledá špatnou složku a přestal hlídat.");

        // Každá vydaná agenda má mít výchozí šablonu; linková je zabalená i před vydáním agendy
        // (picker ji nenabídne, dokud Released = 0), aby se kanál nedodělával až s vydáním.
        Assert.Contains("zastavkovy-jr-ad.json", names);
        Assert.Contains("zastavkovy-jr-mhd.json", names);
        Assert.Contains("linkovy-jr-ad.json", names);
    }

    /// <summary>Tohle je vlastní jádro `B92`: soubor ve složce ještě neznamená soubor v .exe.</summary>
    [Theory]
    [MemberData(nameof(AllTemplates))]
    public void SablonaJeZabalenaVAssembly(string fileName)
    {
        var asm = typeof(SystemTemplateSeeder).Assembly;
        var embedded = asm.GetManifestResourceNames()
            .Where(n => n.Contains(".SystemTemplates.", StringComparison.Ordinal))
            .ToList();

        Assert.Contains(fileName, embedded.Select(n => n[(n.IndexOf(".SystemTemplates.",
            StringComparison.Ordinal) + ".SystemTemplates.".Length)..]));
    }

    [Theory]
    [MemberData(nameof(AllTemplates))]
    public void SablonaSeNacteAZmigrujeNaAktualniVerzi(string fileName)
    {
        var json = File.ReadAllText(Path.Combine(SystemTemplatesDir(), fileName));

        // Tatáž cesta jako v seederu - Deserialize spustí TemplateMigrator.
        var template = TemplateSerializer.Deserialize(json);

        Assert.Equal(TemplateMigrator.CurrentVersion, template.Version);
        Assert.NotEmpty(template.Sections);
    }

    [Theory]
    [MemberData(nameof(AllTemplates))]
    public void SablonaMaKompletniMetadata(string fileName)
    {
        var t = TemplateSerializer.Deserialize(
            File.ReadAllText(Path.Combine(SystemTemplatesDir(), fileName)));

        Assert.NotEqual(Guid.Empty, t.Id);
        Assert.False(string.IsNullOrWhiteSpace(t.Name), $"{fileName}: chybí název");
        Assert.False(string.IsNullOrWhiteSpace(t.Description),
            $"{fileName}: chybí popis - picker ho ukazuje uživateli jako jediné vysvětlení, co šablona je");
        Assert.False(string.IsNullOrWhiteSpace(t.AgendaCode),
            $"{fileName}: chybí agendaCode - filtr pickeru jde přes agendu, bez něj šablonu nikdo nenajde");
    }

    [Theory]
    [MemberData(nameof(AllTemplates))]
    public void NazevAniPopisNemajiDlouhouPomlcku(string fileName)
    {
        var t = TemplateSerializer.Deserialize(
            File.ReadAllText(Path.Combine(SystemTemplatesDir(), fileName)));

        // Rozhodnutí Jan 2026-07-28: v UI textech vždy běžná pomlcka. Název šablony uživatel vidí
        // v pickeru, takže sem to pravidlo platí stejně jako na popisky polí.
        foreach (var (label, text) in new[] { ("název", t.Name), ("popis", t.Description) })
        {
            Assert.False(text.Contains('\u2014'), $"{fileName}: {label} obsahuje dlouhou pomlčku (em dash)");
            Assert.False(text.Contains('\u2013'), $"{fileName}: {label} obsahuje krátkou pomlčku (en dash)");
        }
    }

    [Fact]
    public void KazdaSablonaMaVlastniId()
    {
        var byId = new Dictionary<Guid, string>();
        foreach (var f in TemplateFiles())
        {
            var t = TemplateSerializer.Deserialize(File.ReadAllText(f));
            Assert.False(byId.TryGetValue(t.Id, out var other),
                $"{Path.GetFileName(f)} má stejné Id jako {other} - seeder je idempotentní přes Id, " +
                "takže by jedna šablona v MLINE přepsala druhou.");
            byId[t.Id] = Path.GetFileName(f);
        }
    }

    /// <summary>
    /// Překlep v <c>agendaCode</c> se nijak neprojeví - šablona se nalije a jen ji nikdo nenajde.
    /// Proto se kód porovnává s tím, co reálně deklarují Caché agendy (offline, ze zdrojáků v repu).
    /// </summary>
    [Theory]
    [MemberData(nameof(AllTemplates))]
    public void AgendaCodeOdpovidaExistujiciAgende(string fileName)
    {
        var known = DeclaredAgendaCodes();
        Assert.True(known.Count > 0, "Nenačetly se kódy agend z Agendy/*.cls - test přestal hlídat");

        var t = TemplateSerializer.Deserialize(
            File.ReadAllText(Path.Combine(SystemTemplatesDir(), fileName)));

        Assert.Contains(t.AgendaCode!, known);
    }

    private static readonly Regex AgendaCodeRx = new(
        "Parameter\\s+AgendaVariantCode[^=]*=\\s*\"([A-Z_0-9]+)\"",
        RegexOptions.Compiled);

    private static HashSet<string> DeclaredAgendaCodes()
    {
        var codes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in Directory.GetFiles(Path.Combine(RepoRoot(),
            "TemplateDesigner", "CACHE", "ALVA", "TemplateDesigner", "Agendy"), "*.cls"))
            foreach (Match m in AgendaCodeRx.Matches(File.ReadAllText(f)))
                if (m.Groups[1].Value != "") codes.Add(m.Groups[1].Value);
        return codes;
    }

    private static string SystemTemplatesDir() =>
        Path.Combine(RepoRoot(), "TemplateDesigner", "dotnet", "SystemTemplates");

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EDISON2.sln")))
            dir = dir.Parent;

        // Běh z izolovaného výstupu (dotnet test -o mimo repo) → root odvodit z cesty zdrojáku
        dir ??= new FileInfo(ThisFile()).Directory!.Parent!.Parent;

        Assert.True(File.Exists(Path.Combine(dir!.FullName, "EDISON2.sln")),
            "Nenalezen root repa (EDISON2.sln) - test potřebuje zdrojáky šablon a agend");
        return dir.FullName;
    }

    private static string ThisFile([CallerFilePath] string path = "") => path;

    // ── Kdy má seed vůbec zapisovat ───────────────────────────────────────────

    private static Template Vzor() => new()
    {
        Id = Guid.Parse("5b1e0000-0000-4000-8000-000000000001"),
        Name = "Systémová",
        Sections = [new Section { Name = "Tělo", Elements = [new TextElement { Text = "{CISLO_LINKY}" }] }]
    };

    /// <summary>Kopie téže šablony - přes serializaci, aby měla i shodné identifikátory prvků.</summary>
    private static Template Kopie(Template t) => TemplateSerializer.Deserialize(TemplateSerializer.Serialize(t));

    /// <summary>
    /// Táž šablona, jen jiné časy → není to změna obsahu a zapisovat se nemá. Seed přepisoval při
    /// každém startu, takže „změněno" v přehledu ukazovalo čas posledního spuštění programu, ne
    /// skutečnou změnu (nález Jan 19. 8.) - a nešlo poznat, co se doopravdy měnilo.
    /// </summary>
    [Fact]
    public void JinyCasZmenyNeniZmenaObsahu()
    {
        var zExe = Vzor();
        var vDatabazi = Kopie(zExe);
        vDatabazi.ModifiedAt = zExe.ModifiedAt.AddDays(3);
        vDatabazi.CreatedAt = zExe.CreatedAt.AddDays(-30);

        Assert.False(SystemTemplateSeeder.SeLisiObsah(zExe, vDatabazi));
    }

    [Fact]
    public void ZmenaNazvuJeZmenaObsahu()
    {
        var zExe = Vzor();
        var vDatabazi = Kopie(zExe);
        vDatabazi.Name = "Stará";

        Assert.True(SystemTemplateSeeder.SeLisiObsah(zExe, vDatabazi));
    }

    /// <summary>Změna hluboko uvnitř (text prvku) se pozná taky - proto se porovnává celý tvar,
    /// ne vyjmenované vlastnosti, které by při další vlastnosti zestaraly.</summary>
    [Fact]
    public void ZmenaUvnitrPrvkuSePozna()
    {
        var zExe = Vzor();
        var vDatabazi = Kopie(zExe);
        ((TextElement)vDatabazi.Sections[0].Elements[0]).Text = "{POPIS_LINKY}";

        Assert.True(SystemTemplateSeeder.SeLisiObsah(zExe, vDatabazi));
    }

    /// <summary>I změna fontu (přesně ta, co se 19. 8. opravovala) musí projít jako změna.</summary>
    [Fact]
    public void ZmenaFontuSePozna()
    {
        var zExe = Vzor();
        var vDatabazi = Kopie(zExe);
        ((TextElement)vDatabazi.Sections[0].Elements[0]).Style = new TextStyle { FontFamily = "Arial," };

        Assert.True(SystemTemplateSeeder.SeLisiObsah(zExe, vDatabazi));
    }
}
