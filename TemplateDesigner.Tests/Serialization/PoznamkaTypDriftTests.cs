using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Driftová pojistka slovníku <b>typů poznámek</b> na drátě. Typ, který agenda posílá a klient nezná,
/// spadne na „obyčejný text" - a legenda pak značku nakreslí <b>surovým písmenem</b> místo piktogramu.
///
/// Přesně tak se to stalo: linkové agendy posílají <c>"znak"</c>, vývěskové <c>"specialni"</c>,
/// a klient znal jen to druhé. Značky linkového JŘ se proto v legendě vypisovaly jako <c>m</c>, <c>X</c>
/// nebo <c>+</c>, zatímco v tabulce vycházely správně jako trojúhelníky a kladiva. Nic to nehlídalo -
/// ostatní driftové testy kontrolují **jména polí**, ne slovník jejich **hodnot**.
///
/// Offline, bez Caché serveru.
/// </summary>
public class PoznamkaTypDriftTests
{
    // Typ poznámky je druhý argument BuildPoznamka(druh, "typ", ...) u linkových agend
    // a druhý argument BuildPoznamka("typ", ...) / AddAnotace(o, "typ", ...) u vývěskových.
    private static readonly Regex TypRx = new(
        """(?:BuildPoznamka|AddPoznamka|AddAnotace)\s*\(\s*(?:[^,()"]*,\s*)?"([a-zA-Z-]+)"\s*,""",
        RegexOptions.Compiled);

    [Fact]
    public void KazdyTypPoznamkyZAgendy_KlientZna()
    {
        var zname = StopScheduleElementBase.ZnameTypyPoznamky.ToHashSet(StringComparer.Ordinal);
        var nalezy = new List<string>();

        foreach (var file in AgendaFiles())
            foreach (var typ in TypyZTextu(File.ReadAllText(file)))
                if (!zname.Contains(typ))
                    nalezy.Add($"{Path.GetFileName(file)}: \"{typ}\"");

        Assert.True(nalezy.Count == 0,
            "Agenda posílá typ poznámky, který klient nezná → padne na Text a legenda nakreslí "
            + "místo piktogramu surové písmeno:\n"
            + string.Join("\n", nalezy.Distinct())
            + "\nDoplň slovo do ZnameTypyPoznamky a do ParseTypPoznamky (StopScheduleElementBase).");
    }

    [Fact]
    public void ZnackaLinkovehoJr_NeniObycejnyText()
    {
        // Jádro té chyby: „znak" musí skončit jako značka, ne jako text. Kdyby se to vrátilo,
        // legenda linkového JŘ zase vypíše písmena a nikdo si toho nemusí všimnout.
        Assert.Equal(TypPoznamky.SpecialniZnak, Parse("znak"));
        Assert.Equal(TypPoznamky.SpecialniZnak, Parse("specialni"));
        Assert.Equal(TypPoznamky.NegativniZnacka, Parse("nz"));
        Assert.Equal(TypPoznamky.Text, Parse("text"));
    }

    [Fact]
    public void NeznamyTyp_PadaNaText_ANeVyhodiVyjimku()
    {
        // Tolerance je záměr: starší nebo cizí agenda nesmí prvek shodit. Proto to hlídá test výše,
        // a ne výjimka za běhu.
        Assert.Equal(TypPoznamky.Text, Parse("neco-co-neznam"));
        Assert.Equal(TypPoznamky.Text, Parse(null));
        Assert.Equal(TypPoznamky.Text, Parse(""));
    }

    [Fact]
    public void ParserNasel_ZnameTypyVAgendach()
    {
        // Pojistka regexu: kdyby zrezivěl, test výše by prošel na prázdném vstupu.
        var typy = AgendaFiles().SelectMany(f => TypyZTextu(File.ReadAllText(f))).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("znak", typy);       // linkové agendy
        Assert.Contains("specialni", typy);  // vývěskové agendy
        Assert.Contains("nz", typy);
    }

    private static TypPoznamky Parse(string? s) =>
        (TypPoznamky)typeof(StopScheduleElementBase)
            .GetMethod("ParseTypPoznamky", System.Reflection.BindingFlags.NonPublic
                                        | System.Reflection.BindingFlags.Static)!
            .Invoke(null, [s])!;

    private static IEnumerable<string> TypyZTextu(string text) =>
        TypRx.Matches(text).Select(m => m.Groups[1].Value);

    private static string[] AgendaFiles() => Directory.GetFiles(AgendyDir(), "*.cls");

    private static string AgendyDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EDISON2.sln")))
            dir = dir.Parent;

        dir ??= new FileInfo(ThisFile()).Directory!.Parent!.Parent;

        Assert.True(File.Exists(Path.Combine(dir!.FullName, "EDISON2.sln")),
            "Nenalezen root repa (EDISON2.sln) - drift test potřebuje zdrojáky .cls");
        return Path.Combine(dir.FullName,
            "TemplateDesigner", "CACHE", "ALVA", "TemplateDesigner", "Agendy");
    }

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
