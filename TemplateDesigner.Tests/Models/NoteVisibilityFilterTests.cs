using System.Text.Json.Nodes;
using TemplateDesigner.Models;

namespace TemplateDesigner.Tests.Models;

/// <summary>
/// Filtr viditelnosti poznámek zastávkového JŘ: schování kategorie v rozsahu „Spoje, nepoznámkovat"
/// a podle toho, co je vypnuté v prvku poznámek. (Počitadlo pro signál ve stavovém řádku zrušeno
/// 5. 8. spolu s tou hláškou - rozhodnutí Jan: byl to obklad na chybu, ne oprava.)
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
    public void Apply_NegativniZnackaVRozsahu_NezmiziAniVRezimuOboji()
    {
        // #7955 D3 - zákazník si stěžoval, že „Spoje, nepoznámkovat" ruší i inverzní znaménko.
        // Datová platnost spoje (`omezeni`) není nikdy mezi schovávanými kategoriemi, takže zůstane
        // i u spoje, který v rozsahu JE a i v nejagresivnějším režimu.
        var page = Stranka("omezeni", "43", vRozsahu: true);

        var result = NoteVisibilityFilter.Apply(page, NoteRangeSuppression.Both);

        Assert.Equal(1, PocetAnotaci(result));
        Assert.Single(result.Rows["POZNAMKY"]);
    }

    /// <summary>Kategorie vypnutá v prvku poznámek odebere značku u minuty **i** její vysvětlivku -
    /// nemůže vzniknout značka, kterou legenda nevysvětlí, ani vysvětlivka k ničemu.</summary>
    [Fact]
    public void VypnutaKategorieVPrvku_OdebereZnackuIVysvetlivku()
    {
        var page = Stranka("kola", "b", vRozsahu: false);

        var result = NoteVisibilityFilter.Apply(page, NoteRangeSuppression.ManualComment,
            hiddenContent: new HashSet<string> { "kola" });

        Assert.Empty((JsonArray)result.Rows["ODJEZDY"][0].GetValue("ANOTACE")!);
        Assert.Empty(result.Rows["POZNAMKY"]);
    }

    [Fact]
    public void NaZnameniDleStopListu_SchovaLegendu()
    {
        var page = Stranka("atribut-zastavky", "x", vRozsahu: false);

        var result = NoteVisibilityFilter.Apply(page, NoteRangeSuppression.ManualComment,
            hideNaZnameniLegend: true);

        Assert.Empty(result.Rows["POZNAMKY"]);
    }

    /// <summary>Rozhodnutí, jestli legendu přestupů schovat, se bere ze šablony: schová se, jen když
    /// je žádný seznam zastávek netiskne. Bez seznamu zastávek neznáme záměr, takže se neschovává.</summary>
    [Fact]
    public void ShouldHidePrestup_SledujeNastaveniStopListu()
    {
        var bezPrestupu = new TemplateDesigner.Models.Elements.StopListElement();   // default = přestupy vyp
        var sPrestupy   = new TemplateDesigner.Models.Elements.StopListElement { ShowTransferAttributes = true };

        Assert.True(NoteVisibilityFilter.ShouldHidePrestupLegend(SablonaS(bezPrestupu)));
        Assert.False(NoteVisibilityFilter.ShouldHidePrestupLegend(SablonaS(sPrestupy)));
        Assert.False(NoteVisibilityFilter.ShouldHidePrestupLegend(new Template()));   // bez StopListu → neskrývat
    }

    private static Template SablonaS(TemplateElement element)
    {
        var t = new Template();
        var s = new Section();
        s.Elements.Add(element);
        t.Sections.Add(s);
        return t;
    }

    /// <summary>Piktogram přestupu bez vysvětlení je nečitelný, vysvětlení bez piktogramu matoucí -
    /// legenda přestupů proto následuje viditelnost značky, ne parametr sestavy (#13048 bod 1).</summary>
    [Fact]
    public void PrestupLegenda_SeSkryjeKdyzStopListZnackuNetiskne()
    {
        var page = Stranka("prestup", "A", vRozsahu: false);

        var result = NoteVisibilityFilter.Apply(page, NoteRangeSuppression.ManualComment,
            hidePrestupLegend: true);

        Assert.Empty(result.Rows["POZNAMKY"]);
    }

    [Fact]
    public void PrestupLegenda_ZustaneKdyzStopListZnackuTiskne()
    {
        var page = Stranka("prestup", "A", vRozsahu: false);

        var result = NoteVisibilityFilter.Apply(page, NoteRangeSuppression.ManualComment);

        Assert.Single(result.Rows["POZNAMKY"]);
    }

    [Fact]
    public void AgregatBezbarier_SeSchovaKdyzMaByt()
    {
        var page = Stranka("bezbarier-agregat", "H", vRozsahu: false);

        var result = NoteVisibilityFilter.Apply(page, NoteRangeSuppression.ManualComment,
            hideAggregateBezbarier: true);

        Assert.Empty(result.Rows["POZNAMKY"]);
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

        var result = NoteVisibilityFilter.Apply(page, NoteRangeSuppression.AutoRouteNote);

        // Schová se jen anotace v rozsahu; legenda zůstane, protože znak nese i spoj mimo rozsah.
        Assert.Equal(1, PocetAnotaci(result));
        Assert.Single(result.Rows["POZNAMKY"]);
    }
}
