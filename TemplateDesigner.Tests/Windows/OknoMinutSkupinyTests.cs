using EDControls;
using EDControls.EDViewModel;
using NSubstitute;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Windows;

/// <summary>Nastavení skupin dnů v okně Minut odjezdů (`B194d` E13f, E13g).</summary>
public class OknoMinutSkupinyTests
{
    public OknoMinutSkupinyTests() =>
        EDServices.Initialize(new EDServices(Substitute.For<IMLRest>(),
            Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()));

    private static SkupinaDnuData Skupina(string id) => new() { Id = id, Nadpis = id };

    private static DepartureGroupEditorDialogViewModel Okno(DepartureGridElement prvek, params string[] skupinyVDatech) =>
        new(prvek, [.. skupinyVDatech.Select(Skupina)], prvek.GroupColumns, prvek.HourColumnStyle, prvek.CustomColumns);

    private static DepartureGroupColumnVM Radek(DepartureGroupEditorDialogViewModel okno, string id) =>
        okno.Columns.Rows.Cast<DepartureGroupColumnVM>().Single(r => r.SkupinaDnuId == id);

    /// <summary>Neplatnou barvu kreslení přečte jako průhlednou: skupina by ztratila střídání a přebila
    /// i barvu prvku. Pole ji proto nepřijme, stejně jako barvy v editoru stylu.</summary>
    [Theory]
    [InlineData("nesmysl", "#FF112233")]
    [InlineData("#FF33{X}", "#FF112233")]
    [InlineData("#FFAABBCC", "#FFAABBCC")]
    [InlineData("{BARVA_LINKY}", "{BARVA_LINKY}")]
    [InlineData("", "")]
    public void BarvaStridaniRadku_NeplatnaSeNeprijme(string zadano, string ocekavano)
    {
        var prvek = new DepartureGridElement
        {
            GroupColumns = [new DepartureGroupColumnStyle { SkupinaDnuId = "PD", AlternatingRowColor = "#FF112233" }],
        };
        var radek = Radek(Okno(prvek, "PD"), "PD");

        radek.AlternatingRowColor = zadano;

        Assert.Equal(ocekavano, radek.AlternatingRowColor);
    }
}
