using System.Text.Json.Nodes;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Tests.Rendering;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Prvky zastávkového JŘ nad daty, kde vnořené záznamy nejsou objekty. Linkový JŘ posílá atributy
/// zastávky jako texty značek (`["L","x"]`), zastávkový jako objekty `{ZNAK, POPIS}`. Seznam zastávek
/// v šabloně linkového JŘ pak padal při každém překreslení náhledu (POVED 8. 10.).
/// </summary>
public class ZastavkovePrvkyNadLinkovymiDatyTests
{
    private static DataContext LinkovaData() => RenderSnapshot.Data(
        ("ZASTAVKY",
        [
            RenderSnapshot.Row(("ID", "1"), ("NAZEV_ZASTAVKY", "Kroměříž"), ("PORADI", "1"), ("JE_AKTUALNI", "true"),
                ("ATRIBUTY", new JsonArray("L", "x")), ("POZNAMKY_ZASTAVKY", new JsonArray("a"))),
            RenderSnapshot.Row(("ID", "2"), ("NAZEV_ZASTAVKY", "Hulín"), ("PORADI", "2"),
                ("ATRIBUTY", new JsonArray(new JsonArray("vnořené pole")))),
        ]),
        ("ODJEZDY",
        [
            RenderSnapshot.Row(("ZASTAVKA_ID", "1"), ("SKUPINA_DNU_ID", "1"), ("HODINA", "8"), ("MINUTA", "5"),
                ("ANOTACE", new JsonArray("B", 42))),
        ]));

    [Fact]
    public void SeznamZastavekNespadne()
    {
        var prvek = new StopListElement { X = 0, Y = 0, Width = 60, Height = 80 };

        Assert.Null(Record.Exception(() => RenderSnapshot.Capture(prvek, LinkovaData())));
    }

    [Fact]
    public void MinutyOdjezduNespadnou()
    {
        var prvek = new DepartureGridElement { X = 0, Y = 0, Width = 100, Height = 80 };

        Assert.Null(Record.Exception(() => RenderSnapshot.Capture(prvek, LinkovaData())));
    }
}
