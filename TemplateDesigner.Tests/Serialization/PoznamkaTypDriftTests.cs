using System.IO;
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

    /// <summary>
    /// Druhý slovník na drátě: <b>kategorie</b> značky. Na jejím jménu visí to, co šablona vypíná
    /// (kola, místenky, pokračování, souhrn bezbariér, písmena variant tras) - a když se jméno na
    /// serveru přejmenuje, filtr **tiše přestane fungovat**: značka se tiskne dál, testy klienta
    /// zůstanou zelené (používají tutéž konstantu) a nikdo si toho nevšimne až do vývěsky
    /// u zákazníka. Proto se každá kategorie, kterou klient zná, musí najít i ve zdroji agendy.
    /// </summary>
    [Fact]
    public void KazdaKategorieZnamaKlientovi_ExistujeVeZdrojiAgendy()
    {
        var zdroje = AgendaFiles().Select(File.ReadAllText).ToList();
        var chybi = KategorieKlienta()
            .Where(k => !zdroje.Any(z => z.Contains($"\"{k}\"", StringComparison.Ordinal)))
            .ToList();

        Assert.True(chybi.Count == 0,
            "Klient filtruje podle kategorie, kterou žádná agenda neposílá → filtr tiše nic nedělá "
            + "(značka se tiskne dál):\n"
            + string.Join("\n", chybi)
            + "\nSrovnej NoteCategories s tím, co agenda zapisuje do KATEGORIE.");
    }

    private static IEnumerable<string> KategorieKlienta() =>
        typeof(TemplateDesigner.Models.NoteCategories)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!);

    private static TypPoznamky Parse(string? s) =>
        (TypPoznamky)typeof(StopScheduleElementBase)
            .GetMethod("ParseTypPoznamky", System.Reflection.BindingFlags.NonPublic
                                        | System.Reflection.BindingFlags.Static)!
            .Invoke(null, [s])!;

    private static IEnumerable<string> TypyZTextu(string text) =>
        TypRx.Matches(text).Select(m => m.Groups[1].Value);

    private static string[] AgendaFiles() => ZdrojeAgend.KodAgend();
}
