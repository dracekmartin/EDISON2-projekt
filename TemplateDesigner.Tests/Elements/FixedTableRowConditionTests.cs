using EDControls.EDInputForm;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Podmíněný řádek pevné tabulky - „vytiskni ho, jen když podmínka platí". Tisk to uměl odjakživa
/// (výraz se vyhodnocuje toutéž funkcí jako u prvků), ale od zrušení pětizáložkového dialogu nebylo
/// odkud podmínku zadat. Typický případ: řádek „Variabilní symbol: {VS}" u dokladu, který VS nemá.
/// </summary>
public class FixedTableRowConditionTests
{
    private static TableElement Grid(int cols = 2, int rows = 3)
    {
        var t = TableElement.CreateGrid(cols, rows);
        t.Width = 30f * cols;
        return t;
    }

    private static DataContext Data(params (string Key, string Value)[] values) =>
        new(values.ToDictionary(v => v.Key, v => (object?)v.Value));

    // ── Chování při tisku ─────────────────────────────────────────────────────

    [Fact]
    public void RadekSNesplnenouPodminkou_SeNevytiskne()
    {
        var table = Grid();
        table.Rows![1].VisibilityExpression = "{VS}";   // prázdná hodnota = nepravda

        Assert.Equal(2, table.VisibleRows(Data(("VS", ""))).Count);
        Assert.Equal(3, table.VisibleRows(Data(("VS", "1234"))).Count);
    }

    /// <summary>Skrytý řádek nesmí zabírat výšku - jinak by po něm v sestavě zůstala mezera.</summary>
    [Fact]
    public void SkrytyRadek_NezabiraVysku()
    {
        var table = Grid();
        float plna = table.CalculateHeightFromData(Data(("VS", "1234")));

        table.Rows![1].VisibilityExpression = "{VS}";   // prázdná hodnota = nepravda
        float bezRadku = table.CalculateHeightFromData(Data(("VS", "")));

        Assert.True(bezRadku < plna, $"skrytý řádek pořád zabírá místo ({bezRadku} vs {plna} mm)");
    }

    // ── Označení v návrháři ───────────────────────────────────────────────────

    /// <summary>V návrháři se podmíněný řádek kreslí normálně, takže musí být jinak poznat -
    /// jinak uživatel nemá šanci zjistit, proč mu v tisku zmizel.</summary>
    [Fact]
    public void PodmineneRadky_JdouPoznat()
    {
        var table = Grid();
        table.Rows![1].VisibilityExpression = "{VS}";   // prázdná hodnota = nepravda

        var conditional = table.ConditionalRows(null).ToList();

        Assert.Single(conditional);
        Assert.Equal(1, conditional[0].Row);
        Assert.Equal("{VS}", conditional[0].Expression);
        Assert.NotNull(table.RowRectMm(null, 1));
    }

    [Fact]
    public void RadkyBezPodminky_ZadnyOdznakNemaji()
    {
        Assert.Empty(Grid().ConditionalRows(null));
    }

    /// <summary>
    /// V návrháři ještě data nejsou, takže se podmínka **nesmí** vyhodnocovat - jinak by řádek
    /// zmizel z plátna hned, jak mu uživatel podmínku dal, a nešel by vybrat ani té podmínky zbavit.
    /// Schovává se až v náhledu a při tisku.
    /// </summary>
    [Fact]
    public void VNavrhariBezDat_PodmineneRadkyZustavajiVidet()
    {
        var table = Grid();
        table.Rows![1].VisibilityExpression = "{VS}";

        Assert.Equal(3, table.VisibleRows(null).Count);
        Assert.Equal(2, table.VisibleRows(Data(("VS", ""))).Count);
    }

    // ── Formulář řádku ────────────────────────────────────────────────────────

    /// <summary>Formulář jede na týchž property paths jako u prvků (reflexe přes ElementFormBinder),
    /// takže se podmínka zadává všude stejně a nevzniká druhá cesta.</summary>
    [Fact]
    public void FormularRadku_CteAZapisujeStejnouCestouJakoUPrvku()
    {
        var row = new TableRow { VisibilityExpression = "{VS}", BackgroundColor = "#FFEEEEEE" };
        List<Field> fields =
        [
            new Field { Key = "VisibilityExpression", Label = "Podmínka viditelnosti", FieldType = FieldType.Text },
            new Field { Key = "BackgroundColor",      Label = "Barva pozadí",          FieldType = FieldType.Color },
        ];

        var preFilled = ElementFormBinder.Prefill(fields, row);
        Assert.Equal("{VS}", fields[0].Value);
        Assert.Equal("#FFEEEEEE", fields[1].Value);

        fields[0].Value = "";
        foreach (var command in ElementFormBinder.ExtractCommands(fields, row, preFilled)) command.Execute();

        // Vymazaná podmínka = řádek se tiskne vždy, ne prázdný výraz uložený v šabloně.
        Assert.Null(row.VisibilityExpression);
    }
}
