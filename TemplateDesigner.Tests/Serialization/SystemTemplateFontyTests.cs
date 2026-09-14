using System.IO;
using System.Text.Json.Nodes;
using Xunit;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Dodávané systémové šablony nesmí mít font zapečený u prvků - jinak v nich nejde změnit
/// výchozí font dokumentu a uživatel má dojem, že ta volba nic nedělá (nález Jan 14. 9. na
/// šabloně „Linkový JŘ (AD) - postskriptový vzhled A5", která ho měla na dvanácti místech).
/// </summary>
/// <remarks>
/// Font patří **jednou** do <c>defaultTextStyle</c> šablony; prvky ho zdědí kaskádou
/// (`TextStyle.Resolve` → `ResolvedTextStyle.Default` → `DocumentDefaults.Apply`). Explicitní
/// hodnota u prvku je legitimní jen tam, kde se má lišit od zbytku dokumentu - v dodávaných
/// šablonách takový případ zatím není. Kdyby vznikl, přidej ho sem s odůvodněním.
/// <para>Vzniklo to tím, že `TextStyle.CreateDefault()` kdysi font nastavoval každému novému
/// prvku. Dnes už ne, ale co bylo uložené, to zůstalo.</para>
/// </remarks>
public class SystemTemplateFontyTests
{
    [Fact]
    public void SystemoveSablony_NemajiFontZapecenyUPrvku()
    {
        var slozka = SlozkaSystemovychSablon();
        var soubory = Directory.GetFiles(slozka, "*.json");
        Assert.NotEmpty(soubory);

        var provinilci = new List<string>();
        foreach (var soubor in soubory)
        {
            var koren = JsonNode.Parse(File.ReadAllText(soubor))!.AsObject();
            int pocet = PocetFontu(koren["sections"]);
            if (pocet > 0) provinilci.Add($"{Path.GetFileName(soubor)}: {pocet}×");
        }

        Assert.True(provinilci.Count == 0,
            "Font patří do defaultTextStyle šablony, ne k jednotlivým prvkům - jinak v ní nejde "
            + "změnit výchozí font. Zapečený font mají: " + string.Join(", ", provinilci));
    }

    private static int PocetFontu(JsonNode? uzel) => uzel switch
    {
        JsonObject o => (o.ContainsKey("fontFamily") ? 1 : 0) + o.Sum(p => PocetFontu(p.Value)),
        JsonArray a => a.Sum(PocetFontu),
        _ => 0,
    };

    private static string SlozkaSystemovychSablon()
    {
        // Od bin/Debug/netX/ zpět do repozitáře - test závisí jen na obsahu repa, ne na stroji.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "TemplateDesigner", "dotnet")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "TemplateDesigner", "dotnet", "SystemTemplates");
    }
}
