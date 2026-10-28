using EDControls;
using EDControls.EDViewModel;
using NSubstitute;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Expressions;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Pravidla zvýraznění upravená v okně tabulky patří do téhož vratného kroku jako zbytek okna
/// (`B194d` E13e). Do 28. 10. byla samostatným krokem, takže jedno Ctrl+Z vrátilo jen pravidla
/// a změny sloupců zůstaly.
/// </summary>
public class ZvyrazneniJednimKrokemTests
{
    public ZvyrazneniJednimKrokemTests() =>
        EDServices.Initialize(new EDServices(Substitute.For<IMLRest>(),
            Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()));

    private static List<HighlightRule> Pravidla() =>
        [new HighlightRule { Condition = "{STAV}==\"STORNO\"", BackgroundColor = "#FFFFE0E0" }];

    [Fact]
    public void SeznamZastavek()
    {
        var prvek = new StopListElement();
        var puvodni = prvek.Columns[0].Header;
        var okno = new StopColumnsEditorDialogViewModel(prvek, prvek.Columns);
        okno.Columns.Rows[0].Header = "Jiné záhlaví";
        okno.OkCommand.Execute(null);
        var navrhar = new TemplateDesignerViewModel();

        navrhar.ApplyStopColumnsEdit(prvek, null, okno.ResultColumns, Pravidla());
        Assert.NotNull(prvek.HighlightRules);
        navrhar.UndoCommand.Execute(null);

        Assert.Null(prvek.HighlightRules);
        Assert.Equal(puvodni, prvek.Columns[0].Header);
        Assert.False(navrhar.CanUndo);
    }

    [Fact]
    public void TabulkaSpoju()
    {
        var prvek = new LineTableElement();
        var puvodni = prvek.Columns[0].Header;
        var okno = new LineTableColumnsEditorDialogViewModel(prvek, prvek.Columns);
        okno.Columns.Rows[0].Header = "Jiné záhlaví";
        okno.OkCommand.Execute(null);
        var navrhar = new TemplateDesignerViewModel();

        navrhar.ApplyLineTableColumnsEdit(prvek, null, okno.ResultColumns, Pravidla());
        navrhar.UndoCommand.Execute(null);

        Assert.Null(prvek.HighlightRules);
        Assert.Equal(puvodni, prvek.Columns[0].Header);
        Assert.False(navrhar.CanUndo);
    }

    [Fact]
    public void TabulkaZDat()
    {
        var prvek = new DynamicTableElement { Columns = [new TableColumn { Header = "Cena", Field = "CENA" }] };
        var okno = new DynamicTableEditorDialogViewModel(prvek, []);
        okno.Columns.Rows[0].Header = "Částka";
        okno.OkCommand.Execute(null);
        var navrhar = new TemplateDesignerViewModel();

        navrhar.ApplyDynamicTableEdit(prvek, null, okno.ResultColumns, Pravidla());
        navrhar.UndoCommand.Execute(null);

        Assert.Null(prvek.HighlightRules);
        Assert.Equal("Cena", prvek.Columns[0].Header);
        Assert.False(navrhar.CanUndo);
    }

    /// <summary>Pravidla beze změny (okno je neotevřelo, null) nevzniknou jako krok.</summary>
    [Fact]
    public void BezUpravyPravidel_NicNevznikne()
    {
        var prvek = new StopListElement { HighlightRules = Pravidla() };
        var okno = new StopColumnsEditorDialogViewModel(prvek, prvek.Columns);
        okno.OkCommand.Execute(null);
        var navrhar = new TemplateDesignerViewModel();

        navrhar.ApplyStopColumnsEdit(prvek, null, okno.ResultColumns, null);
        navrhar.ApplyStopColumnsEdit(prvek, null, okno.ResultColumns, Pravidla());

        Assert.False(navrhar.CanUndo);
        Assert.Single(prvek.HighlightRules!);
    }
}
