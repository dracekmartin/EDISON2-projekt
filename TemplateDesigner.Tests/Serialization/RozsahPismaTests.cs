using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// `B194d` E8f: velikost písma jde všude zadat 3-72 pt (rozhodnutí Jan 27. 10.). Do té doby měla tatáž
/// vlastnost v panelu 3-24 pt a v okně stylu 4-200 pt; pod 3 pt je tisk nečitelný, 72 pt pokryje nadpis
/// na A3. Formulář hodnotu mimo rozsah nepustí uložit, panel ji ořízne - rozsah se proto musí shodovat.
/// </summary>
public class RozsahPismaTests
{
    private const float Nejmene = 3f, Nejvys = 72f;

    [Fact]
    public void FormulareMajiVsudeStejnyRozsah()
    {
        // AddDecimal(klíč, popisek, výchozí, min, max, "pt"…)
        var pole = new Regex(@"AddDecimal\((?<klic>[^,]+),\s*(?<popisek>[^,]+),\s*[^,]*,\s*(?<min>[0-9.]+),\s*(?<max>[0-9.]+),\s*""pt""");
        var vsechna = Directory.GetFiles(FormulareDir(), "*.cls")
            .SelectMany(f => pole.Matches(File.ReadAllText(f)).Select(m => (Soubor: Path.GetFileName(f), m)))
            .ToList();
        Assert.True(vsechna.Count >= 8, $"našlo se jen {vsechna.Count} polí písma - změnil se zápis formulářů?");

        var mimo = vsechna
            .Where(x => x.m.Groups["min"].Value != "3" || x.m.Groups["max"].Value != "72")
            .Select(x => $"{x.Soubor}: {x.m.Groups["popisek"].Value} {x.m.Groups["min"].Value}-{x.m.Groups["max"].Value}")
            .ToList();

        Assert.Empty(mimo);
    }

    /// <summary>Panel: každé číselné pole v pt. Jediná nula smí zůstat u „Symbol: Velikost" (0 = podle minuty).</summary>
    [Fact]
    public void PanelMaVsudeStejnyRozsah()
    {
        var vsechna = typeof(TemplateElement).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(TemplateElement)) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) is not null)
            .SelectMany(t => ((TemplateElement)Activator.CreateInstance(t)!).GetPropertyDescriptors()
                .OfType<FloatPropertyDescriptor>()
                .Where(d => d.Unit == "pt")
                .Select(d => (Prvek: t.Name, d)))
            .ToList();
        Assert.True(vsechna.Count >= 8, $"našlo se jen {vsechna.Count} polí písma v panelu");

        var mimo = vsechna
            .Where(x => x.d.Max != Nejvys || (x.d.Min != Nejmene && !(x.d.Min == 0 && x.d.Label == "Symbol: Velikost")))
            .Select(x => $"{x.Prvek}: {x.d.Label} {x.d.Min}-{x.d.Max}")
            .ToList();

        Assert.Empty(mimo);
    }

    private static string FormulareDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EDISON2.sln")))
            dir = dir.Parent;
        dir ??= new FileInfo(ThisFile()).Directory!.Parent!.Parent;
        return Path.Combine(dir!.FullName, "TemplateDesigner", "CACHE", "ALVA", "TemplateDesigner", "EDInputForm");
    }

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
