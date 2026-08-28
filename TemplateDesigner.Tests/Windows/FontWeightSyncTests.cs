using System.Linq;
using EDControls;
using EDControls.EDInputForm;
using TemplateDesigner;
using TemplateDesigner.Models;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Nabídka „Řez" se plnila jen při stavbě formuláře, takže po změně rodiny písma nabízela řezy
/// té předchozí (srovnalo se to až znovuotevřením dialogu) a zvolený řez, který nová rodina nemá,
/// zůstal viset - renderer pak kreslil nejbližší dostupný, tedy něco jiného, než co bylo napsané
/// ve formuláři. Testy hlídají přepočet nabídky i zahození neplatné hodnoty a hlavně ty dva
/// případy, kdy se hodnota zahodit NESMÍ (programové plnění, neznámá rodina).
/// </summary>
public class FontWeightSyncTests
{
    private const string FamilyKey = "Style.FontFamily";
    private const string WeightKey = "Style.FontWeight";

    private static List<Field> Fields(string family, string weight) =>
    [
        new Field { Key = FamilyKey, FieldType = FieldType.ComboBox, Value = family },
        new Field { Key = WeightKey, FieldType = FieldType.List,     Value = weight },
    ];

    private static Field Weight(List<Field> fields) => fields.First(f => f.Key == WeightKey);

    /// <summary>Rodina, která na tomto stroji reálně existuje (test nesmí záviset na konkrétním jménu).</summary>
    private static string InstalledFamily() => FontResolver.InstalledFamilies.First();

    [Fact]
    public void ZmenaRodiny_PrepocitaNabidkuRezu()
    {
        var family = InstalledFamily();
        var fields = Fields(family, "");

        ElementFormBinder.SyncFontWeightAfterFamilyChange(fields, FamilyKey, allowValueReset: true);

        var expected = FontWeightCatalog.Items(family).Select(x => x.Weight.ToString()).ToList();
        Assert.Equal(expected, Weight(fields).ListItems.Select(i => i.Key).ToList());
    }

    [Fact]
    public void RezKteryRodinaNema_SeZahodiNaAutomaticky()
    {
        // 137 není reálná váha žádného fontu → v nabídce nainstalované rodiny být nemůže.
        var fields = Fields(InstalledFamily(), "137");

        var reset = ElementFormBinder.SyncFontWeightAfterFamilyChange(fields, FamilyKey, allowValueReset: true);

        Assert.True(reset);
        Assert.Equal("", Weight(fields).Value);
    }

    [Fact]
    public void RezKteryRodinaMa_Zustane()
    {
        var family = InstalledFamily();
        var real = FontWeightCatalog.InstalledWeights(family).First().ToString();
        var fields = Fields(family, real);

        var reset = ElementFormBinder.SyncFontWeightAfterFamilyChange(fields, FamilyKey, allowValueReset: true);

        Assert.False(reset);
        Assert.Equal(real, Weight(fields).Value);
    }

    [Fact]
    public void ProgramovePlneni_HodnotuNezahodi()
    {
        var fields = Fields(InstalledFamily(), "137");

        var reset = ElementFormBinder.SyncFontWeightAfterFamilyChange(fields, FamilyKey, allowValueReset: false);

        Assert.False(reset);
        Assert.Equal("137", Weight(fields).Value);
    }

    [Fact]
    public void NeznamaRodina_HodnotuNezahodi()
    {
        // Překlep i dědění z šablony (prázdno) = řezy neznáme → nabídka je jen odhad, hodnota se nechává.
        foreach (var family in new[] { "ZadnyTakovyFontXyz", "" })
        {
            var fields = Fields(family, "300");

            var reset = ElementFormBinder.SyncFontWeightAfterFamilyChange(fields, FamilyKey, allowValueReset: true);

            Assert.False(reset);
            Assert.Equal("300", Weight(fields).Value);
        }
    }

    [Fact]
    public void ZmenaJinehoPole_SeIgnoruje()
    {
        var fields = Fields(InstalledFamily(), "137");

        var reset = ElementFormBinder.SyncFontWeightAfterFamilyChange(fields, "Style.FontSize", allowValueReset: true);

        Assert.False(reset);
        Assert.Equal("137", Weight(fields).Value);
        Assert.Empty(Weight(fields).ListItems);
    }

    [Fact]
    public void VolbyPoleHlasiZmenu()
    {
        // Bez ohlášení by se vyměněná nabídka nepromítla do už vykresleného dropdownu (EDControls).
        var field = new Field { Key = WeightKey, FieldType = FieldType.List };
        var changed = new List<string>();
        field.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? "");

        field.ListItems = [new ListItem("400", "Normal")];

        Assert.Contains(nameof(Field.ListItems), changed);
        Assert.Contains(nameof(Field.ListItemsForDisplay), changed);
    }
}
