using System.Text.Json.Nodes;
using TemplateDesigner.Models;

namespace TemplateDesigner.Tests.Models;

/// <summary>
/// Filtr viditelnosti poznámek zastávkového JŘ: schování kategorie v rozsahu „Spoje, nepoznámkovat"
/// + počitadlo pro viditelný signál („poznámky jsou, ale nastavení je schovalo" vs. „žádné nejsou").
/// </summary>
public class NoteVisibilityFilterTests
{
    /// <summary>Stránka s jedním odjezdem (anotace) a odpovídající legendou.</summary>
    private static DataContext Stranka(string kategorie, string znak, bool vRozsahu)
    {
        var anotace = new JsonArray(new JsonObject
        {
            ["TYP"] = "text",
            ["ZNAK"] = znak,
            ["KATEGORIE"] = kategorie,
            ["V_ROZSAHU_NEPOZNAMKOVAT"] = vRozsahu ? "1" : "0",
        });

        var odjezd = new DataContext(new Dictionary<string, object?> { ["ANOTACE"] = anotace });
        var poznamka = new DataContext(new Dictionary<string, object?>
        {
            ["TYP"] = "text",
            ["ZNAK"] = znak,
            ["TEXT"] = "neobsluhuje zast. Obchodní",
            ["KATEGORIE"] = kategorie,
        });

        return new DataContext(
            new Dictionary<string, object?>(),
            new Dictionary<string, IReadOnlyList<DataContext>>
            {
                ["ODJEZDY"] = [odjezd],
                ["POZNAMKY"] = [poznamka],
            });
    }

    private static int PocetAnotaci(DataContext page) =>
        page.Rows["ODJEZDY"].Sum(o => (o.GetValue("ANOTACE") as JsonArray)?.Count ?? 0);

    [Fact]
    public void Apply_KategorieVRozsahu_SchovaAnotaciILegendu()
    {
        var page = Stranka("rozdil-trasy", "B", vRozsahu: true);

        var result = NoteVisibilityFilter.Apply(page, NoteRangeSuppression.AutoRouteNote);

        Assert.Equal(0, PocetAnotaci(result));
        Assert.Empty(result.Rows["POZNAMKY"]);
    }

    [Fact]
    public void Apply_MimoRozsah_NechaBytIKdyzKategorieSedi()
    {
        var page = Stranka("rozdil-trasy", "B", vRozsahu: false);

        var result = NoteVisibilityFilter.Apply(page, NoteRangeSuppression.AutoRouteNote);

        Assert.Equal(1, PocetAnotaci(result));
        Assert.Single(result.Rows["POZNAMKY"]);
    }

    [Fact]
    public void Apply_JinaKategorie_NechaBytNedotcene()
    {
        var page = Stranka("rucni-komentar", "a", vRozsahu: true);

        var result = NoteVisibilityFilter.Apply(page, NoteRangeSuppression.AutoRouteNote);

        Assert.Equal(1, PocetAnotaci(result));
        Assert.Single(result.Rows["POZNAMKY"]);
    }

    [Fact]
    public void Pocitadlo_PriSchovani_NapocitaAnotaciILegendu()
    {
        var page = Stranka("rozdil-trasy", "B", vRozsahu: true);
        var counter = new NoteSuppressionCounter();

        NoteVisibilityFilter.Apply(page, NoteRangeSuppression.AutoRouteNote, counter: counter);

        Assert.True(counter.AnythingHidden);
        Assert.Equal(1, counter.HiddenAnnotations);
        Assert.Equal(1, counter.HiddenLegendRows);
    }

    [Fact]
    public void Pocitadlo_KdyzNicNeschovano_ZustaneNula()
    {
        // Parametr zapnutý, ale žádná anotace do rozsahu nespadá → signál nesmí svítit
        var page = Stranka("rozdil-trasy", "B", vRozsahu: false);
        var counter = new NoteSuppressionCounter();

        NoteVisibilityFilter.Apply(page, NoteRangeSuppression.AutoRouteNote, counter: counter);

        Assert.False(counter.AnythingHidden);
        Assert.Equal(0, counter.HiddenAnnotations);
        Assert.Equal(0, counter.HiddenLegendRows);
    }

    [Fact]
    public void Pocitadlo_NaZnameniDleStopListu_SeNepocita()
    {
        // Legendu „× na znamení" neschovává parametr, ale nastavení StopListu → do signálu nepatří
        var page = Stranka("atribut-zastavky", "x", vRozsahu: false);
        var counter = new NoteSuppressionCounter();

        var result = NoteVisibilityFilter.Apply(page, NoteRangeSuppression.ManualComment,
            hideNaZnameniLegend: true, counter: counter);

        Assert.Empty(result.Rows["POZNAMKY"]);
        Assert.False(counter.AnythingHidden);
    }

    [Fact]
    public void Pocitadlo_AgregatBezbarier_SeDoSignaluPocita()
    {
        var page = Stranka("bezbarier-agregat", "H", vRozsahu: false);
        var counter = new NoteSuppressionCounter();

        var result = NoteVisibilityFilter.Apply(page, NoteRangeSuppression.ManualComment,
            hideAggregateBezbarier: true, counter: counter);

        Assert.Empty(result.Rows["POZNAMKY"]);
        Assert.True(counter.AnythingHidden);
        Assert.Equal(1, counter.HiddenLegendRows);
    }

    [Fact]
    public void Apply_SdilenyZnakMimoRozsah_LegendaZustane()
    {
        // Dva spoje se stejným znakem: jeden v rozsahu (schová se), druhý mimo → legenda musí zůstat,
        // jinak by u viditelné značky chybělo vysvětlení.
        var vRozsahu = new JsonArray(new JsonObject
        {
            ["TYP"] = "text", ["ZNAK"] = "B", ["KATEGORIE"] = "rozdil-trasy", ["V_ROZSAHU_NEPOZNAMKOVAT"] = "1",
        });
        var mimoRozsah = new JsonArray(new JsonObject
        {
            ["TYP"] = "text", ["ZNAK"] = "B", ["KATEGORIE"] = "rozdil-trasy", ["V_ROZSAHU_NEPOZNAMKOVAT"] = "0",
        });
        var poznamka = new DataContext(new Dictionary<string, object?>
        {
            ["ZNAK"] = "B", ["TEXT"] = "neobsluhuje zast. Obchodní", ["KATEGORIE"] = "rozdil-trasy",
        });
        var page = new DataContext(
            new Dictionary<string, object?>(),
            new Dictionary<string, IReadOnlyList<DataContext>>
            {
                ["ODJEZDY"] =
                [
                    new DataContext(new Dictionary<string, object?> { ["ANOTACE"] = vRozsahu }),
                    new DataContext(new Dictionary<string, object?> { ["ANOTACE"] = mimoRozsah }),
                ],
                ["POZNAMKY"] = [poznamka],
            });

        var counter = new NoteSuppressionCounter();
        var result = NoteVisibilityFilter.Apply(page, NoteRangeSuppression.AutoRouteNote, counter: counter);

        Assert.Equal(1, PocetAnotaci(result));
        Assert.Single(result.Rows["POZNAMKY"]);
        Assert.Equal(1, counter.HiddenAnnotations);
        Assert.Equal(0, counter.HiddenLegendRows);
    }
}
