using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Volby zarovnání ve formulářích v Caché (sada stylu <c>StyleFields.cls</c>, formulář textu) musí mít tytéž
/// kódy a popisky jako <see cref="AlignmentLabels"/>, ze kterých čte panel, editor stylu sloupce i lišta formátu.
/// Formuláře zůstávají v Caché, protože se opravují na serveru bez nového exe (rozhodnutí Jan 30. 10.).
/// </summary>
public class VolbyZarovnaniTests
{
    private static readonly Regex PoleZarovnani = new(@"AddFieldToLastGroup\([^)]*?\b(HAlign|VAlign)""", RegexOptions.Compiled);
    private static readonly Regex Volba = new(@"AddListItem\(\s*""(\w+)"",\s*""([^""]+)""\)", RegexOptions.Compiled);

    /// <summary>Každé pole zarovnání v každém formuláři: (soubor, HAlign/VAlign, volby v pořadí).</summary>
    private static List<(string Soubor, string Pole, List<(string Kod, string Popisek)> Volby)> PoleVeFormularich()
    {
        var vysledek = new List<(string, string, List<(string, string)>)>();
        foreach (var soubor in Directory.GetFiles(FormulareDir(), "*.cls"))
        {
            var radky = File.ReadAllLines(soubor);
            for (int i = 0; i < radky.Length; i++)
            {
                var pole = PoleZarovnani.Match(radky[i]);
                if (!pole.Success) continue;
                var volby = new List<(string, string)>();
                for (int j = i + 1; j < radky.Length && radky[j].Contains("AddListItem", StringComparison.Ordinal); j++)
                    volby.AddRange(Volba.Matches(radky[j]).Select(m => (m.Groups[1].Value, m.Groups[2].Value)));
                vysledek.Add((Path.GetFileName(soubor), pole.Groups[1].Value, volby));
            }
        }
        return vysledek;
    }

    [Fact]
    public void FormulareNabizejiZarovnaniJakoKod()
    {
        var pole = PoleVeFormularich();
        Assert.True(pole.Count(p => p.Pole == "HAlign") >= 2 && pole.Count(p => p.Pole == "VAlign") >= 2,
            $"ve formulářích se našlo jen {pole.Count} polí zarovnání - hledání je rozbité");

        var vodorovne = AlignmentLabels.Horizontal.Select(o => (o.Value.ToString(), o.Label)).ToList();
        var svisle = AlignmentLabels.Vertical.Select(o => (o.Value.ToString(), o.Label)).ToList();
        var doBloku = (AlignmentLabels.Justify.Value.ToString(), AlignmentLabels.Justify.Label);

        foreach (var (soubor, nazev, volby) in pole)
        {
            // Do bloku nabízí jen text (buňka tabulky ho nekreslí) - smí být navíc na konci.
            var ocekavane = nazev == "VAlign" ? svisle
                : volby.Contains(doBloku) ? [.. vodorovne, doBloku] : vodorovne;
            Assert.True(ocekavane.SequenceEqual(volby),
                $"{soubor} {nazev}: {string.Join(", ", volby)} - čekám {string.Join(", ", ocekavane)} (AlignmentLabels)");
        }
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
