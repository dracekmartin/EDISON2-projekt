using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Dohodnuté názvosloví (<c>ux-conventions §8</c>, <see cref="EDInputFormSchemaDriftTests.BannedTerms"/>)
/// platí v každém textu, který uživatel uvidí, nejen v popiscích (<c>B194f</c> G4). Popisky hlídají drift
/// testy formulářů a agend; tady se kontrolují nápovědy, hlášky a legendy agend, texty oken a texty
/// v kódu návrháře. Tudy se dřív vracela „nástupiště" v nápovědě (<c>P7</c>) a „engine" (<c>E7</c>).
/// Za text pro uživatele se bere text v uvozovkách s mezerou nebo diakritikou, technický klíč
/// (<c>ElementKind</c>) jím není. Tvary popisku (zkratky, „Část: Vlastnost") se tu nekontrolují,
/// ve větě nápovědy jsou správně (<see cref="EDInputFormSchemaDriftTests.BannedLabelForms"/>).
/// </summary>
public class NazvosloviTextuTests
{
    /// <summary>Texty, které zakázaný termín nesou vědomě. Klíč = celý text, hodnota = důvod.</summary>
    private static readonly Dictionary<string, string> Vyjimky = new(StringComparer.Ordinal)
    {
        ["cílová zastávka pro tablo"] = "legenda atributu CIS „H\" doslovně podle číselníku starého modulu " +
            "(AtributyZastavkySpoje, JRapiCis), jako „tisk stanoviště\" (ux-conventions §8)",
    };

    /// <summary>Soubory, jejichž texty čte model AI, ne uživatel (zadání pro asistenta a pro import z PDF).</summary>
    private static readonly string[] TextyProModel = ["AiVisionImportService.cs", "AnthropicApiClient.cs", "CapabilityCatalog.cs"];

    private static readonly Regex TextProCloveka = new("[áčďéěíňóřšťúůýžÁČĎÉĚÍŇÓŘŠŤÚŮÝŽ ]", RegexOptions.Compiled);
    private static readonly Regex DosazovanaHodnota = new(@"\{[^{}]*\}", RegexOptions.Compiled);
    private static readonly Regex AtributXaml = new(
        @"\b(?:Title|ToolTip|Text|Content|Header|Description)=""([^""{][^""]*)""", RegexOptions.Compiled);
    private static readonly Regex TextMeziZnackami = new(@">([^<>]+)<", RegexOptions.Compiled);

    [Fact]
    public void TextyCacheTrid_BezZakazanychTerminu() =>
        Over(TextyCacheTrid(), "Caché třídách (nápovědy, hlášky, legendy)");

    [Fact]
    public void TextyOken_BezZakazanychTerminu() =>
        Over(TextyOken(), "oknech návrháře");

    [Fact]
    public void TextyVKoduNavrhare_BezZakazanychTerminu() =>
        Over(TextyVKodu(), "kódu návrháře (hlášky, stavový řádek, protokoly)");

    /// <summary>Hledání musí texty opravdu najít - jinak by kontroly výš procházely nad prázdným seznamem.</summary>
    [Fact]
    public void Hledani_NajdeNapovedyHlaskyATextyOken()
    {
        Assert.Contains(TextyCacheTrid(), t => t.Text.StartsWith("Datová pole vložíte tlačítkem", StringComparison.Ordinal));
        Assert.Contains(TextyCacheTrid(), t => t.Text.Contains("v jízdních řádech není", StringComparison.Ordinal));
        Assert.True(TextyOken().Count > 100, $"v oknech se našlo jen {TextyOken().Count} textů");
        Assert.True(TextyVKodu().Count > 300, $"v kódu se našlo jen {TextyVKodu().Count} textů");
    }

    private static void Over(List<(string Kde, string Text)> texty, string kde)
    {
        var nalezy = texty
            .Where(t => !Vyjimky.ContainsKey(t.Text))
            .SelectMany(t => EDInputFormSchemaDriftTests.BannedTerms
                .Where(z => ObsahujeTermin(DosazovanaHodnota.Replace(t.Text, ""), z))
                .Select(z => $"{t.Kde}: „{t.Text}\" (obsahuje „{z}\")"))
            .Distinct()
            .ToList();

        Assert.True(nalezy.Count == 0,
            $"Zakázaný termín v {kde} - použij dohodnutý (ux-conventions §8), nebo doplň výjimku s důvodem:\n" +
            string.Join("\n", nalezy));
    }

    /// <summary>Texty ze všech Caché tříd návrháře kromě testů: popisky, nápovědy, hlášky agend, legendy.</summary>
    private static List<(string Kde, string Text)> TextyCacheTrid()
    {
        var cache = Path.Combine(Koren(), "TemplateDesigner", "CACHE");
        return Directory.GetFiles(cache, "*.cls", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}Tests{Path.DirectorySeparatorChar}"))
            .SelectMany(f => File.ReadLines(f).Select((radek, i) => (f, radek, i)))
            .Where(x => !JeKomentar(x.radek, "//", "///", ";", "#;"))
            .SelectMany(x => Regex.Matches(BezKomentare(x.radek, zpetneLomitko: false), @"""((?:[^""]|"""")*)""")
                .Select(m => (Kde: $"{Path.GetFileName(x.f)}:{x.i + 1}", Text: m.Groups[1].Value.Replace("\"\"", "\""))))
            .Where(t => TextProCloveka.IsMatch(t.Text))
            .ToList();
    }

    /// <summary>Texty oken návrháře: titulky, bubliny, popisky tlačítek a text mezi značkami.</summary>
    private static List<(string Kde, string Text)> TextyOken() =>
        Directory.GetFiles(Path.Combine(Koren(), "TemplateDesigner", "dotnet"), "*.xaml", SearchOption.AllDirectories)
            .Where(f => !JeVystupBuildu(f))
            .SelectMany(f => File.ReadLines(f).Select((radek, i) => (f, radek, i)))
            .SelectMany(x => AtributXaml.Matches(x.radek).Concat(TextMeziZnackami.Matches(x.radek))
                .Select(m => (Kde: $"{Path.GetFileName(x.f)}:{x.i + 1}", Text: m.Groups[1].Value.Trim())))
            .Where(t => TextProCloveka.IsMatch(t.Text))
            .ToList();

    /// <summary>Texty v C# kódu návrháře: hlášky, stavový řádek, protokol importu, popisy migrací.</summary>
    private static List<(string Kde, string Text)> TextyVKodu() =>
        Directory.GetFiles(Path.Combine(Koren(), "TemplateDesigner", "dotnet"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !JeVystupBuildu(f) && !TextyProModel.Contains(Path.GetFileName(f)))
            .SelectMany(f => File.ReadLines(f).Select((radek, i) => (f, radek, i)))
            .Where(x => !JeKomentar(x.radek, "//", "*") && !LadiciVypis.IsMatch(BezKomentare(x.radek, zpetneLomitko: true)))
            .SelectMany(x => Regex.Matches(BezKomentare(x.radek, zpetneLomitko: true), @"""((?:[^""\\]|\\.)*)""")
                .Select(m => (Kde: $"{Path.GetFileName(x.f)}:{x.i + 1}", Text: m.Groups[1].Value)))
            .Where(t => TextProCloveka.IsMatch(t.Text))
            .ToList();

    /// <summary>Termín, který začíná písmenem, se počítá jen na začátku slova: „RectangleElement" je jméno
    /// třídy, „elementu" je česká věta.</summary>
    private static bool ObsahujeTermin(string text, string termin)
    {
        for (int i = text.IndexOf(termin, StringComparison.Ordinal); i >= 0;
             i = text.IndexOf(termin, i + 1, StringComparison.Ordinal))
        {
            if (!char.IsLetter(termin[0]) || i == 0 || !char.IsLetter(text[i - 1])) return true;
        }
        return false;
    }

    /// <summary>Ladicí výpis (import z PDF, AI) čte vývojář, ne uživatel.</summary>
    private static readonly Regex LadiciVypis = new(@"\b\w*(?:debug|Debug)\w*\b", RegexOptions.Compiled);

    private static bool JeKomentar(string radek, params string[] zacatky)
    {
        var s = radek.TrimStart();
        return zacatky.Any(z => s.StartsWith(z, StringComparison.Ordinal));
    }

    /// <summary>Řádek bez komentáře na konci; „//" uvnitř textu (adresa) komentář nezačíná.</summary>
    private static string BezKomentare(string radek, bool zpetneLomitko)
    {
        bool vTextu = false;
        for (int i = 0; i < radek.Length - 1; i++)
        {
            if (zpetneLomitko && vTextu && radek[i] == '\\') { i++; continue; }
            if (radek[i] == '"') vTextu = !vTextu;
            else if (!vTextu && radek[i] == '/' && radek[i + 1] == '/') return radek[..i];
        }
        return radek;
    }

    private static bool JeVystupBuildu(string f) =>
        f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
        || f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}");

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
