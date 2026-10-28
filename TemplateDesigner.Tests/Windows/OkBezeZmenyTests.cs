using EDControls;
using EDControls.EDViewModel;
using NSubstitute;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// OK v okně tabulky, ve kterém se nic nezměnilo, nesmí přidat krok do Ctrl+Z ani označit šablonu jako
/// změněnou (`B194d` E13d). Do 28. 10. se sloupce, hodiny i skupiny dnů zapsaly vždy, takže uživatel
/// po pouhém nahlédnutí do nastavení dostal dotaz na uložení a Ctrl+Z „nic nevrátil".
/// Okno i předání výsledku jdou stejnou cestou jako z hlavního okna (`MainWindow.Open*Dialog`).
/// </summary>
public class OkBezeZmenyTests
{
    public OkBezeZmenyTests() =>
        EDServices.Initialize(new EDServices(Substitute.For<IMLRest>(),
            Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()));

    private static void AssertBezKroku(TemplateDesignerViewModel navrhar)
    {
        Assert.False(navrhar.CanUndo, "OK beze změny přidalo krok do Ctrl+Z");
        Assert.False(navrhar.IsDirty, "OK beze změny označilo šablonu jako změněnou");
    }

    [Fact]
    public void SeznamZastavek()
    {
        var prvek = new StopListElement();
        var okno = new StopColumnsEditorDialogViewModel(prvek, prvek.Columns);
        var navrhar = new TemplateDesignerViewModel();

        okno.OkCommand.Execute(null);
        navrhar.ApplyStopColumnsEdit(prvek, null, okno.ResultColumns);

        AssertBezKroku(navrhar);
    }

    /// <summary>Agenda bez dat některého sloupce (MHD nemá varianty tras): okno ho nenabízí, ale ze šablony
    /// nesmí zmizet ani se v ní přesunout.</summary>
    [Fact]
    public void SeznamZastavek_AgendaNenabiziVsechnySloupce()
    {
        var prvek = new StopListElement();
        var okno = new StopColumnsEditorDialogViewModel(prvek, prvek.Columns,
            klic => klic != StopFields.TravelTimeVariants);
        var navrhar = new TemplateDesignerViewModel();

        okno.OkCommand.Execute(null);
        navrhar.ApplyStopColumnsEdit(prvek, null, okno.ResultColumns);

        AssertBezKroku(navrhar);
    }

    /// <summary>Starší šablona bez později přidaných sloupců: okno je nabídne vypnuté, ale dokud na ně
    /// uživatel nesáhne, šablona se nemění.</summary>
    [Fact]
    public void SeznamZastavek_StarsiSablonaBezNovychSloupcu()
    {
        var prvek = new StopListElement();
        prvek.Columns = [.. prvek.Columns.Where(c => c.Key != StopFields.Sloupek && c.Key != StopFields.TravelTimeVariants)];
        var okno = new StopColumnsEditorDialogViewModel(prvek, prvek.Columns);
        var navrhar = new TemplateDesignerViewModel();

        okno.OkCommand.Execute(null);
        navrhar.ApplyStopColumnsEdit(prvek, null, okno.ResultColumns);

        AssertBezKroku(navrhar);
    }

    [Fact]
    public void TabulkaSpoju_StarsiSablonaBezNovychSloupcu()
    {
        var prvek = new LineTableElement();
        prvek.Columns = [.. prvek.Columns.Skip(1)];
        var okno = new LineTableColumnsEditorDialogViewModel(prvek, prvek.Columns);
        var navrhar = new TemplateDesignerViewModel();

        okno.OkCommand.Execute(null);
        navrhar.ApplyLineTableColumnsEdit(prvek, null, okno.ResultColumns);

        AssertBezKroku(navrhar);
    }

    [Fact]
    public void MinutyOdjezdu()
    {
        var prvek = new DepartureGridElement();
        var okno = new DepartureGroupEditorDialogViewModel(prvek, [], prvek.GroupColumns, prvek.HourColumnStyle,
            prvek.CustomColumns);
        var navrhar = new TemplateDesignerViewModel();

        okno.OkCommand.Execute(null);
        navrhar.ApplyDepartureGridEdit(prvek, null, okno.ResultGroupColumns, false, okno.ResultHourColumnStyle,
            okno.ResultHourColumnHeader, okno.ResultHourColumnWidth, okno.ResultCustomColumns);

        AssertBezKroku(navrhar);
    }

    [Fact]
    public void TabulkaSpoju()
    {
        var prvek = new LineTableElement();
        var okno = new LineTableColumnsEditorDialogViewModel(prvek, prvek.Columns);
        var navrhar = new TemplateDesignerViewModel();

        okno.OkCommand.Execute(null);
        navrhar.ApplyLineTableColumnsEdit(prvek, null, okno.ResultColumns);

        AssertBezKroku(navrhar);
    }

    [Fact]
    public void TabulkaZDat()
    {
        var prvek = new DynamicTableElement
        {
            Columns = [new TableColumn { Header = "Cena", Field = "CENA", Width = 40f }],
        };
        var okno = new DynamicTableEditorDialogViewModel(prvek, []);
        var navrhar = new TemplateDesignerViewModel();

        okno.OkCommand.Execute(null);
        navrhar.ApplyDynamicTableEdit(prvek, null, okno.ResultColumns);

        AssertBezKroku(navrhar);
    }

    /// <summary>Pojistka opačným směrem: skutečná změna dál vznikne jako jeden vratný krok.</summary>
    [Fact]
    public void SkutecnaZmena_JeJedenKrok()
    {
        var prvek = new StopListElement();
        var okno = new StopColumnsEditorDialogViewModel(prvek, prvek.Columns);
        var navrhar = new TemplateDesignerViewModel();
        var puvodni = prvek.Columns[0].Header;

        okno.Columns.Rows[0].Header = "Jiné záhlaví";
        okno.OkCommand.Execute(null);
        navrhar.ApplyStopColumnsEdit(prvek, null, okno.ResultColumns);

        Assert.True(navrhar.CanUndo);
        Assert.Equal("Jiné záhlaví", prvek.Columns[0].Header);
        navrhar.UndoCommand.Execute(null);
        Assert.Equal(puvodni, prvek.Columns[0].Header);
        Assert.False(navrhar.CanUndo);
    }
}
