using System.IO;
using EDControls;
using TemplateDesigner.Designer;
using TemplateDesigner.Tests.Serialization;

namespace TemplateDesigner.Tests.Designer;

/// <summary>
/// Synonyma hledání návrháře (<see cref="SlovnikHledani"/>) míří na popisky a skupiny formulářů. Synonymum, jehož cíl
/// se přejmenoval, by tiše nic nenašlo - uživatel by si myslel, že taková vlastnost není.
/// </summary>
public class SlovnikHledaniTests
{
    [Fact]
    public void KazdaHodnota_MaVeFormularichCil()
    {
        var texty = Directory.GetFiles(FormularePrvku.Slozka, "*.cls")
            .SelectMany(f => FormularePrvku.Pole(Path.GetFileNameWithoutExtension(f)))
            .SelectMany(p => new[] { p.Popisek, p.Skupina })
            .Select(SearchNormalization.NormalizeForSearch)
            .Distinct()
            .ToList();
        Assert.True(texty.Count > 100, $"načetlo se jen {texty.Count} popisků a skupin - čtení formulářů je rozbité");

        var bezCile = SlovnikHledani.Slova
            .SelectMany(kv => kv.Value.Select(h => (kv.Key, Hodnota: h)))
            .Where(x => !texty.Any(t => t.Contains(SearchNormalization.NormalizeForSearch(x.Hodnota))))
            .Select(x => $"{x.Key} → {x.Hodnota}")
            .ToList();

        Assert.True(bezCile.Count == 0,
            "Synonymum hledání míří na text, který žádný formulář nemá (přejmenovaný popisek?):\n" + string.Join("\n", bezCile));
    }

    /// <summary>Synonymum, které je samo v popisku, nic nepřidá - hledání ho najde i bez slovníku.</summary>
    [Fact]
    public void Slovo_NeniSynonymemSamoSebe()
    {
        var zbytecna = SlovnikHledani.Slova
            .Where(kv => kv.Value.Any(h => SearchNormalization.NormalizeForSearch(h)
                .Contains(SearchNormalization.NormalizeForSearch(kv.Key))))
            .Select(kv => kv.Key)
            .ToList();

        Assert.Empty(zbytecna);
    }
}
