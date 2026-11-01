using System.Collections.Generic;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Parametr „Rozdělení sloupců" se odvozuje z obsahu sloupců v šabloně - shodují-li se s některým
/// vzorem, ukáže jeho popisek, jinak „Vlastní rozložení". Když by porovnání bylo příliš benevolentní,
/// hlásil by parametr pevné rozvržení i pro ručně upravené sloupce a jeho přepnutí by pak uživateli
/// tiše přepsalo vlastní práci; kdyby bylo příliš přísné, hlásil by „Vlastní rozložení" i pro
/// nedotčenou předvyplněnou šablonu.
/// </summary>
public class DepartureColumnPresetMatchTests
{
    private const string PracSoNeLabel = "Pracovní / Sobota / Neděle+svátky";

    private static List<DepartureColumnDefinition> PracSoNe() =>
    [
        new() { Id = "1", Header = "Pracovní dny",     Days = "12345" },
        new() { Id = "2", Header = "Soboty",           Days = "6" },
        new() { Id = "3", Header = "Neděle a svátky",  Days = "7+" },
    ];

    /// <summary>Nabídka vzorů tak, jak ji posílá server u voleb parametru (klíč + popisek).</summary>
    private static readonly DepartureColumnPresets.Preset[] Nabidka = [new("PRAC_SO_NE", PracSoNeLabel)];

    private static Dictionary<string, List<DepartureColumnDefinition>> Presets() =>
        new() { ["PRAC_SO_NE"] = PracSoNe() };

    [Fact]
    public void ShodneSloupce_VratiPopisekVzoru()
    {
        Assert.Equal(PracSoNeLabel, DepartureColumnPresets.MatchOption(PracSoNe(), Presets(), Nabidka));
    }

    [Fact]
    public void ZadneSloupce_NevratiNic()
    {
        Assert.Null(DepartureColumnPresets.MatchOption([], Presets(), Nabidka));
        Assert.Null(DepartureColumnPresets.MatchOption(null, Presets(), Nabidka));
    }

    [Fact]
    public void ZmenenyDen_UzToNeniVzor()
    {
        var upravene = PracSoNe();
        upravene[2].Days = "7";   // svátek vyhozen z nedělního sloupce = jiné chování

        Assert.Null(DepartureColumnPresets.MatchOption(upravene, Presets(), Nabidka));
    }

    [Fact]
    public void PrejmenovanyNadpis_UzToNeniVzor()
    {
        // Nadpis jde na vývěsku, takže přejmenovaný sloupec už není ten vzor - jinak by přepnutí
        // parametru uživateli tiše vrátilo původní nadpis.
        var upravene = PracSoNe();
        upravene[1].Header = "So";

        Assert.Null(DepartureColumnPresets.MatchOption(upravene, Presets(), Nabidka));
    }

    [Fact]
    public void UbranySloupec_UzToNeniVzor()
    {
        var upravene = PracSoNe();
        upravene.RemoveAt(2);

        Assert.Null(DepartureColumnPresets.MatchOption(upravene, Presets(), Nabidka));
    }

    [Fact]
    public void PrehozenePoradi_UzToNeniVzor()
    {
        // Pořadí sloupců je vidět na vývěsce, takže přeskládané sloupce nejsou tentýž vzor.
        var upravene = PracSoNe();
        (upravene[0], upravene[1]) = (upravene[1], upravene[0]);

        Assert.Null(DepartureColumnPresets.MatchOption(upravene, Presets(), Nabidka));
    }

    [Fact]
    public void BezNactenychVzoru_NevratiNic()
    {
        // Vzory chodí až s daty linky - do té doby se nesmí tvrdit ani shoda, ani rozdíl.
        Assert.Null(DepartureColumnPresets.MatchOption(PracSoNe(), new Dictionary<string, List<DepartureColumnDefinition>>(), Nabidka));
    }

    /// <summary>`Id` je jen vazba pro per-sloupcové styly, ne obsah - a editor sloupců ho při
    /// rozpoznávání vzoru taky ignoruje. Kdyby ho parametr zohledňoval, obě místa by nad týmiž
    /// sloupci odpovídala jinak.</summary>
    [Fact]
    public void JineIdSteznehoObsahu_PoradToJeVzor()
    {
        var jinaId = PracSoNe();
        foreach (var c in jinaId) c.Id = "x" + c.Id;

        Assert.Equal(PracSoNeLabel, DepartureColumnPresets.MatchOption(jinaId, Presets(), Nabidka));
    }

    [Fact]
    public void SameColumns_PoznaShoduIRozdilVKazdemPoli()
    {
        Assert.True(DepartureColumnPresets.SameColumns(PracSoNe(), PracSoNe()));
        Assert.True(DepartureColumnPresets.SameColumns(null, null));
        Assert.False(DepartureColumnPresets.SameColumns(PracSoNe(), null));

        // Každé pole, které jde na server, musí rozdíl založit - jinak by se „shodou" prohlásily
        // sloupce, které se ve výstupu liší.
        foreach (var uprav in new Action<DepartureColumnDefinition>[]
                 {
                     c => c.Id = "9",
                     c => c.Header = "jiný",
                     c => c.Header2 = "druhý řádek",
                     c => c.Days = "1",
                     c => c.IncludeMarks = "52",
                     c => c.ExcludeMarks = "53",
                     c => c.SuppressedMarks = "54",
                     c => c.Period = "1.7.-31.8.",
                 })
        {
            var upravene = PracSoNe();
            uprav(upravene[0]);
            Assert.False(DepartureColumnPresets.SameColumns(PracSoNe(), upravene));
        }
    }
}
