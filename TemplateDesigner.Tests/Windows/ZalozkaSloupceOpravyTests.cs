using EDControls;
using EDControls.EDInputForm;
using EDControls.EDViewModel;
using NSubstitute;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Chyby záložky Sloupce nalezené při sjednocování oken (`B194` E13c): OK v okně tabulky z dat
/// zahodilo zarovnání sloupce a šířku v procentech, okno Minut zapisovalo záhlaví a šířku hodin
/// rovnou do prvku mimo zpětný krok.
/// </summary>
public class ZalozkaSloupceOpravyTests
{
    public ZalozkaSloupceOpravyTests() =>
        EDServices.Initialize(new EDServices(Substitute.For<IMLRest>(),
            Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()));

    /// <summary>Okno zarovnání ani režim šířky nenabízí, takže je OK musí vrátit beze změny. Do 28. 10.
    /// spadlo zarovnání na Vlevo a 40 % se změnilo na 40 mm - i u tabulky, na kterou nikdo nesáhl.</summary>
    [Fact]
    public void TabulkaZDat_OkBezeZmeny_ZachovaZarovnaniASirkuVProcentech()
    {
        var prvek = new DynamicTableElement
        {
            Columns = [new TableColumn { Header = "Cena", Field = "CENA", Width = 40f,
                WidthMode = ColumnWidthMode.Percent, Alignment = TextHorizontalAlignment.Right }],
        };
        var vm = new DynamicTableEditorDialogViewModel(prvek, []);

        vm.OkCommand.Execute(null);

        var sloupec = Assert.Single(vm.ResultColumns);
        Assert.Equal(TextHorizontalAlignment.Right, sloupec.Alignment);
        Assert.Equal(ColumnWidthMode.Percent, sloupec.WidthMode);
        Assert.Equal(40f, sloupec.Width);
    }

    /// <summary>OK okna prvek nemění - změny se provedou až jako jeden vratný krok. Do 28. 10. se záhlaví
    /// a šířka hodin zapsaly rovnou, takže je Ctrl+Z nevrátil a Storno okna nadřazeného kroku nezrušilo.</summary>
    [Fact]
    public void Minuty_OkNezapiseHodinyRovnouDoPrvku()
    {
        var prvek = new DepartureGridElement { HourColumnHeader = "Hod", HourColumnWidth = 8f };
        var vm = new DepartureGroupEditorDialogViewModel(prvek, [], prvek.GroupColumns, prvek.HourColumnStyle,
            prvek.CustomColumns);
        var hodiny = vm.Columns.Rows[0];
        hodiny.Header = "Hodina";
        hodiny.WidthText = "12";

        vm.OkCommand.Execute(null);

        Assert.Equal("Hod", prvek.HourColumnHeader);
        Assert.Equal(8f, prvek.HourColumnWidth);
        Assert.Equal("Hodina", vm.ResultHourColumnHeader);
        Assert.Equal(12f, vm.ResultHourColumnWidth);
    }

    /// <summary>Šedá zděděná hodnota zarovnání ukazuje to, co se vytiskne: sloupec Tabulky spojů se
    /// záložním zarovnáním na střed (zóna) zdědí na střed, ne Vlevo z výchozího stylu buňky.</summary>
    [Fact]
    public void TabulkaSpoju_ZdedeneZarovnani_OdpovidaTisku()
    {
        var prvek = new LineTableElement();
        var vm = new LineTableColumnsEditorDialogViewModel(prvek, LineTableElement.GetDefaultColumns());
        var zona = vm.Columns.Rows.Cast<LineTableColumnVM>().First(c => c.Key == ColumnKeys.Zone);

        Assert.Equal(TextHorizontalAlignment.Center, zona.Alignment);
        Assert.Equal(TextHorizontalAlignment.Center, zona.DataStyle.Inherited.HAlign);
        Assert.Equal(TextHorizontalAlignment.Center, zona.HeaderStyle.Inherited.HAlign);
    }

    /// <summary>Záhlaví a šířka hodin jdou do téhož vratného kroku jako zbytek okna - Ctrl+Z je vrátí.</summary>
    [Fact]
    public void Minuty_ZpetnyKrokVratiZahlaviASirkuHodin()
    {
        var prvek = new DepartureGridElement { HourColumnHeader = "Hod", HourColumnWidth = 8f };
        var navrhar = new TemplateDesignerViewModel();

        navrhar.ApplyDepartureGridEdit(prvek, null, prvek.GroupColumns, false, prvek.HourColumnStyle,
            "Hodina", 12f, prvek.CustomColumns);
        Assert.Equal("Hodina", prvek.HourColumnHeader);
        Assert.Equal(12f, prvek.HourColumnWidth);

        navrhar.UndoCommand.Execute(null);

        Assert.Equal("Hod", prvek.HourColumnHeader);
        Assert.Equal(8f, prvek.HourColumnWidth);
    }
    /// <summary>Tučné záhlaví zapnuté na záložce Obecné a v téže chvíli vypnuté u jednoho sloupce: sloupec
    /// se nesmí vytisknout tučně (`B194h` H2). Zaškrtávátko dřív porovnávalo se stavem prvku z otevření
    /// okna, takže odškrtnutí uložilo dědění a sloupec po OK zdědil nové tučné.</summary>
    [Fact]
    public void TucneZeZalozkyObecne_SloupecOdlisenyOdskrtnutim_ZustaneNetucny()
    {
        var prvek = new StopListElement();
        prvek.HeaderStyle.Bold = false;
        var okno = new StopColumnsEditorDialogViewModel(prvek, prvek.Columns);
        var pole = new List<Field> { new() { Key = "HeaderStyle.Bold", Label = "Tučné", FieldType = FieldType.Bool } };
        var preFilled = ElementFormBinder.Prefill(pole, prvek);

        pole[0].Value = "1";                                                      // Obecné: tučné záhlaví
        okno.ObnovZdedene(ElementFormBinder.Nahled(pole, prvek, preFilled));       // přechod na záložku Sloupce
        var sloupec = okno.Columns.Rows[0].HeaderStyle;
        Assert.True(sloupec.EffectiveBold);
        sloupec.EffectiveBold = false;

        okno.OkCommand.Execute(null);
        foreach (var prikaz in ElementFormBinder.ExtractCommands(pole, prvek, preFilled)) prikaz.Execute();

        Assert.True(prvek.HeaderStyle.Bold);
        Assert.False(okno.ResultColumns[0].Style?.Header?.Bold);
    }

    /// <summary>Náhled prvku pro záložku Sloupce nesmí změnit prvek - změny ze záložky Obecné zapíše až OK.</summary>
    [Fact]
    public void NahledProZalozkuSloupce_PrvekNemeni()
    {
        var prvek = new StopListElement();
        prvek.HeaderStyle.Bold = false;
        var pole = new List<Field> { new() { Key = "HeaderStyle.Bold", Label = "Tučné", FieldType = FieldType.Bool } };
        var preFilled = ElementFormBinder.Prefill(pole, prvek);

        pole[0].Value = "1";
        var nahled = ElementFormBinder.Nahled(pole, prvek, preFilled);

        Assert.True(nahled.HeaderStyle.Bold);
        Assert.False(prvek.HeaderStyle.Bold);
    }
}
