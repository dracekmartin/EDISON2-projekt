using EDControls;
using EDControls.EDViewModel;
using NSubstitute;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Services;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Rozměr nově vkládané pevné tabulky. Do 18. 8. se vkládala natvrdo mřížka 3 × 3, takže na
/// 12 × 10 se uživatel musel proklikat devatenácti vloženími sloupce a řádku (nález Jan).
/// </summary>
public class FixedTableInsertSizeTests
{
    public FixedTableInsertSizeTests() =>
        EDServices.Initialize(new EDServices(Substitute.For<IMLRest>(),
            Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()));

    /// <summary>Dialogová služba, která formulář „potvrdí" se zadaným rozměrem.</summary>
    private static IDesignerDialogService Dialogs(int? columns, int? rows)
    {
        var dialogs = Substitute.For<IDesignerDialogService>();
        dialogs.ShowQuickForm(Arg.Any<object>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(call =>
            {
                if (columns is null || rows is null) return false;   // zrušeno
                var size = (TableGridSize)call[0];
                size.Columns = columns.Value;
                size.Rows    = rows.Value;
                return true;
            });
        return dialogs;
    }

    private static TemplateDesignerViewModel Vm(IDesignerDialogService? dialogs)
    {
        var vm = new TemplateDesignerViewModel();
        vm.Dialogs = dialogs;
        return vm;
    }

    [Fact]
    public void ZadanyRozmer_SeVlozi()
    {
        var element = Vm(Dialogs(12, 10)).CreateElementInteractive(ElementTypes.Table, 5, 5);

        var table = Assert.IsType<TableElement>(element);
        Assert.Equal(12, table.Columns.Count);
        Assert.Equal(10, table.Rows!.Count);
    }

    /// <summary>Výška musí růst s počtem řádků - jinak by se obsah desetiřádkové tabulky hned
    /// po vložení zmenšil, aby se vešel do výchozích 18 mm.</summary>
    [Fact]
    public void VyskaRosteSPoctemRadku()
    {
        var table = (TableElement)Vm(Dialogs(3, 10)).CreateElementInteractive(ElementTypes.Table, 0, 0)!;

        Assert.Equal(10 * table.RowHeight, table.Height);
    }

    [Fact]
    public void ZruseniFormulare_NevlozeNic()
    {
        Assert.Null(Vm(Dialogs(null, null)).CreateElementInteractive(ElementTypes.Table, 0, 0));
    }

    /// <summary>Ostatní prvky se na nic neptají - dialog patří jen tabulce.</summary>
    [Fact]
    public void JinePrvky_SeNaRozmerNeptaji()
    {
        var dialogs = Dialogs(12, 10);

        var element = Vm(dialogs).CreateElementInteractive(ElementTypes.Text, 0, 0);

        Assert.IsType<TextElement>(element);
        dialogs.DidNotReceive().ShowQuickForm(Arg.Any<object>(), Arg.Any<string>(), Arg.Any<string>());
    }

    /// <summary>Bez dialogové služby (testy, headless) se vloží výchozí mřížka - vkládání se nesmí
    /// zaseknout jen proto, že není kdo se zeptat.</summary>
    [Fact]
    public void BezDialogoveSluzby_SeVloziVychoziMrizka()
    {
        var table = (TableElement)Vm(null).CreateElementInteractive(ElementTypes.Table, 0, 0)!;

        Assert.Equal(3, table.Columns.Count);
        Assert.Equal(3, table.Rows!.Count);
    }

    /// <summary>Výchozí hodnoty formuláře musí sedět s tím, co vznikne bez ptaní - jinak by
    /// pouhé potvrzení dialogu vyrobilo jinou tabulku než dřív.</summary>
    [Fact]
    public void VychoziRozmerFormulare_JeTrikratTri()
    {
        var size = new TableGridSize();

        Assert.Equal(3, size.Columns);
        Assert.Equal(3, size.Rows);
    }
}
