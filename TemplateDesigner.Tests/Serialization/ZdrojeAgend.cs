using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Zdrojáky agend pro testy, které čtou <c>.cls</c> z repa. Rozlišuje <b>agendu</b> (přímý potomek
/// <c>BaseAgenda</c> - totéž pravidlo jako registr <c>AgendyRegistr.ListAgendy</c>) a <b>kód agend</b>
/// (agendy, základ a pomocné třídy včetně podsložek, <c>Agendy/Jr/</c> podle R8).
/// </summary>
/// <remarks>
/// Kontroly kódu a textů čtou kód agend, kontroly vlastností agendy (zveřejnění, kód, příznaky
/// registru) jen agendy. Do 30. 10. četl každý test jen soubory přímo ve složce agend a bral je
/// všechny jako agendy, takže kód přesunutý v <c>B56</c> do pomocné třídy by testy tiše přestaly
/// hlídat (<c>B56</c> S1).
/// </remarks>
internal static class ZdrojeAgend
{
    private static readonly Regex PrimyPotomek =
        new(@"^Class \S+ Extends ALVA\.TemplateDesigner\.Agendy\.BaseAgenda\b", RegexOptions.Multiline);

    public static string Slozka => Path.Combine(Koren(), "TemplateDesigner", "CACHE", "ALVA", "TemplateDesigner", "Agendy");

    /// <summary>Třídy agend, které registr nabízí - bez základu a pomocných tříd.</summary>
    public static string[] Agendy(string? slozka = null) =>
        [.. KodAgend(slozka).Where(f => PrimyPotomek.IsMatch(File.ReadAllText(f)))];

    /// <summary>Všechen kód agend: agendy, <c>BaseAgenda</c> a pomocné třídy v podsložkách.</summary>
    public static string[] KodAgend(string? slozka = null) =>
        [.. Directory.GetFiles(slozka ?? Slozka, "*.cls", SearchOption.AllDirectories).Order(StringComparer.Ordinal)];

    public static string Koren()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EDISON2.sln")))
            dir = dir.Parent;
        dir ??= new FileInfo(ThisFile()).Directory!.Parent!.Parent;
        Assert.True(File.Exists(Path.Combine(dir!.FullName, "EDISON2.sln")),
            "Nenalezen kořen repa (EDISON2.sln) - test potřebuje zdrojáky .cls");
        return dir.FullName;
    }

    private static string ThisFile([CallerFilePath] string path = "") => path;
}

public class ZdrojeAgendTests
{
    /// <summary>Pomocná třída v podsložce patří do kódu agend, ale není agenda; agendu pozná test
    /// stejně jako registr, ne podle složky.</summary>
    [Fact]
    public void PomocnaTridaVPodslozce_JeKodAleNeAgenda()
    {
        var slozka = Path.Combine(Path.GetTempPath(), "TdZdrojeAgend-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(slozka, "Jr"));
            File.WriteAllText(Path.Combine(slozka, "BaseAgenda.cls"),
                "Class ALVA.TemplateDesigner.Agendy.BaseAgenda Extends %RegisteredObject [ Abstract ]\r\n{\r\n}\r\n");
            File.WriteAllText(Path.Combine(slozka, "Vyveska.cls"),
                "Include GENERAL\r\n\r\nClass ALVA.TemplateDesigner.Agendy.Vyveska Extends ALVA.TemplateDesigner.Agendy.BaseAgenda\r\n{\r\n}\r\n");
            File.WriteAllText(Path.Combine(slozka, "Jr", "DataAd.cls"),
                "Class ALVA.TemplateDesigner.Agendy.Jr.DataAd Extends %RegisteredObject\r\n{\r\n}\r\n");
            File.WriteAllText(Path.Combine(slozka, "Odvozena.cls"),
                "Class ALVA.TemplateDesigner.Agendy.Odvozena Extends ALVA.TemplateDesigner.Agendy.Vyveska\r\n{\r\n}\r\n");

            Assert.Equal(["BaseAgenda.cls", "DataAd.cls", "Odvozena.cls", "Vyveska.cls"],
                ZdrojeAgend.KodAgend(slozka).Select(Path.GetFileName).Order(StringComparer.Ordinal));
            Assert.Equal(["Vyveska.cls"], ZdrojeAgend.Agendy(slozka).Select(Path.GetFileName));
        }
        finally
        {
            Directory.Delete(slozka, recursive: true);
        }
    }

    /// <summary>Agenda je právě třída s kódem agendy - kdyby se změnil zápis třídy a filtr agendy
    /// přestal poznávat, testy vlastností agend by běžely na prázdném seznamu.</summary>
    [Fact]
    public void AgendyVRepu_JsouTridySKodemAgendy()
    {
        var sKodem = ZdrojeAgend.KodAgend()
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"^Parameter AgendaVariantCode\b[^=]*=\s*""[A-Z]", RegexOptions.Multiline));

        Assert.True(ZdrojeAgend.Agendy().Length >= 6);
        Assert.Equal(sKodem, ZdrojeAgend.Agendy());
    }
}
