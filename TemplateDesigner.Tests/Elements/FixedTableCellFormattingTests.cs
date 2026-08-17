using EDControls;
using EDControls.EDViewModel;
using NSubstitute;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Formátování jednotlivé buňky pevné tabulky. Do 17. 8. šla tabulka formátovat jen jako celek,
/// takže nefungoval ani způsob práce, na kterém prvek stojí („záhlaví si udělám z prvního řádku
/// tím, že ho ztuční"). Testy hlídají tři věci, které se dají pokazit tiše:
/// kaskádu (buňka je překryv nad stylem prvku), vratnost (zpětný krok podle souřadnic)
/// a to, že potvrzení dialogu beze změny nezapeče zděděné hodnoty do buňky.
/// </summary>
public class FixedTableCellFormattingTests
{
    public FixedTableCellFormattingTests() =>
        EDServices.Initialize(new EDServices(Substitute.For<IMLRest>(),
            Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()));

    private static TableElement Grid()
    {
        var t = TableElement.CreateGrid(3, 3);
        t.Width = 90f;
        return t;
    }

    private static TableCellFormatting Formatting(TableElement t, int row = 0, int col = 0) =>
        new(t, () => null, [(row, col)]);

    // ── Kaskáda ───────────────────────────────────────────────────────────────

    [Fact]
    public void BunkaBezVlastnihoStylu_UkazujeHodnotyPrvku()
    {
        var table = Grid();
        table.DataStyle = new TextStyle { Bold = true, FontSize = 14f };

        var style = Formatting(table).ActiveStyle();

        Assert.True(style.Bold);
        Assert.Equal(14f, style.FontSize);
    }

    [Fact]
    public void VlastniHodnotaBunky_PrebijiPrvek()
    {
        var table = Grid();
        table.DataStyle = new TextStyle { Bold = true };
        table.EnsureCell(null, 0, 0)!.Style = new TextStyle { Bold = false };

        Assert.False(Formatting(table).ActiveStyle().Bold);
    }

    /// <summary>Zapisuje se **jen** změněná vlastnost - zbytek musí zůstat zděděný, jinak by se
    /// buňka odpojila od stylu prvku a pozdější změna písma tabulky by se jí netýkala.</summary>
    [Fact]
    public void ZtucneniBunky_NezapecePrevzateHodnoty()
    {
        var table = Grid();
        table.DataStyle = new TextStyle { FontSize = 12f, TextColor = "#FF112233" };

        Formatting(table).Apply("Tučné", s => s.Bold = true);

        var cellStyle = table.EnsureCell(null, 0, 0)!.Style!;
        Assert.True(cellStyle.Bold);
        Assert.Null(cellStyle.FontSize);
        Assert.Null(cellStyle.TextColor);
        Assert.Equal(12f, Formatting(table).ActiveStyle().FontSize);   // dál se dědí
    }

    // ── Vratnost ──────────────────────────────────────────────────────────────

    [Fact]
    public void ZpetnyKrok_VratiBunceJejiPuvodniStav()
    {
        var table = Grid();
        var command = Formatting(table).Apply("Tučné", s => s.Bold = true)!;

        Assert.True(table.EnsureCell(null, 0, 0)!.Style!.Bold);

        command.Undo();
        Assert.Null(table.EnsureCell(null, 0, 0)!.Style);

        command.Execute();
        Assert.True(table.EnsureCell(null, 0, 0)!.Style!.Bold);
    }

    /// <summary>
    /// U výběru víc buněk musí zpětný krok vrátit **každé její vlastní** hodnotu. Kdyby se vracela
    /// jedna společná hodnota z lišty, ztučnění dvojice „tučná + obyčejná" by po Ctrl+Z nechalo
    /// obě stejné.
    /// </summary>
    [Fact]
    public void ZpetnyKrokNadSkupinou_NesrovnaBunkyNaJednuHodnotu()
    {
        var table = Grid();
        table.EnsureCell(null, 0, 0)!.Style = new TextStyle { Bold = true };

        var formatting = new TableCellFormatting(table, () => null, [(0, 0), (0, 1)]);
        var command = formatting.Apply("Tučné", s => s.Bold = false)!;

        Assert.False(table.EnsureCell(null, 0, 0)!.Style!.Bold);
        Assert.False(table.EnsureCell(null, 0, 1)!.Style!.Bold);

        command.Undo();
        Assert.True(table.EnsureCell(null, 0, 0)!.Style!.Bold);   // tahle byla tučná
        Assert.Null(table.EnsureCell(null, 0, 1)!.Style);         // tahle neměla nic
    }

    /// <summary>
    /// Zpětný krok adresuje buňku souřadnicí, ne odkazem. Každá změna struktury (vložení řádku,
    /// tažení hranice, vložení ze schránky) mřížku naklonuje - příkaz držící instanci by pak psal
    /// do odpojeného objektu a tiše by nedělal nic.
    /// </summary>
    [Fact]
    public void ZpetnyKrok_FungujeIKdyzSeMrizkaMeziTimNaklonovala()
    {
        var table = Grid();
        var command = Formatting(table, row: 1).Apply("Tučné", s => s.Bold = true)!;

        table.RestoreStructure(table.CaptureStructure());   // klon všech řádků i buněk

        command.Undo();
        Assert.Null(table.EnsureCell(null, 1, 0)!.Style);
    }

    // ── Formátovací lišta ─────────────────────────────────────────────────────

    [Fact]
    public void ListaUkazujeStavAktivniBunky_VcetneZdedenych()
    {
        var table = Grid();
        table.DataStyle = new TextStyle { Bold = true, HAlign = TextHorizontalAlignment.Right };

        var descriptors = Formatting(table).Descriptors();
        var bold  = Assert.IsType<BoolPropertyDescriptor>(descriptors.Single(d => d.Label == "Tučné"));
        var align = Assert.IsType<EnumPropertyDescriptor>(descriptors.Single(d => d.Label == "Vodorovně"));

        Assert.True(bold.Get());
        Assert.Equal(TextHorizontalAlignment.Right, align.Get());
    }

    /// <summary>Deskriptor si nese vlastní vratný příkaz - lišta ho nesmí obalit svým, jinak by
    /// zpětný krok u skupiny buněk srovnal všechny na jednu hodnotu.</summary>
    [Fact]
    public void DeskriptoryListy_NesouVlastniVratnyPrikaz()
    {
        var descriptors = Formatting(Grid()).Descriptors();

        Assert.All(descriptors, d => Assert.NotNull(d.MakeCommand));
    }

    [Fact]
    public void ZarovnaniZListy_SeZapiseDoBunky()
    {
        var table = Grid();
        var align = (EnumPropertyDescriptor)Formatting(table).Descriptors().Single(d => d.Label == "Svisle");

        var command = align.MakeCommand!(TextVerticalAlignment.Bottom)!;

        Assert.Equal(TextVerticalAlignment.Bottom, table.EnsureCell(null, 0, 0)!.Style!.VAlign);
        command.Undo();
        Assert.Null(table.EnsureCell(null, 0, 0)!.Style);
    }

    // ── Dialog vlastností buňky ───────────────────────────────────────────────

    private static StyleEditDialogViewModel CellDialog(TableElement table, int row = 0, int col = 0) =>
        new(table.EnsureCell(null, row, col)!, nameof(TableCell.Style),
            table.ResolveCellParent(null, col),
            extraTextPath: nameof(TableCell.Format),
            extraTextLabel: "Formát hodnoty");

    /// <summary>
    /// Nejdůležitější test celého bloku: dialog ukazuje zděděné hodnoty, ale potvrzení beze změny
    /// je **nesmí** zapsat do buňky. Jinak by se buňka odpojila od „Stylu buněk" prvku a uživatel
    /// by pak marně měnil písmo celé tabulky.
    /// </summary>
    [Fact]
    public void PotvrzeniDialoguBezeZmeny_NezapeceZdedeneHodnoty()
    {
        var table = Grid();
        table.DataStyle = new TextStyle { Bold = true, FontSize = 13f, TextColor = "#FF445566" };

        var vm = CellDialog(table);
        vm.OkCommand.Execute(null);

        Assert.Null(vm.ResultCommand);
        Assert.Null(table.EnsureCell(null, 0, 0)!.Style);
    }

    [Fact]
    public void ZmenaVDialogu_SeZapiseAJdeVratit()
    {
        var table = Grid();
        var vm = CellDialog(table);
        vm.Style.Bold = true;
        vm.ExtraText  = "N2";
        vm.OkCommand.Execute(null);

        var command = Assert.IsAssignableFrom<TemplateDesigner.Commands.IDesignerCommand>(vm.ResultCommand);
        command.Execute();

        var cell = table.EnsureCell(null, 0, 0)!;
        Assert.True(cell.Style!.Bold);
        Assert.Equal("N2", cell.Format);

        command.Undo();
        Assert.Equal(new TextStyle().IsEmpty, cell.Style?.IsEmpty ?? true);
        Assert.Null(cell.Format);
    }

    /// <summary>Prázdný formát znamená „podle sloupce", ne prázdný řetězec uložený v šabloně.</summary>
    [Fact]
    public void VymazanyFormat_SeUlozeJakoNenastaveno()
    {
        var table = Grid();
        table.EnsureCell(null, 0, 0)!.Format = "N2";

        var vm = CellDialog(table);
        vm.ExtraText = "   ";
        vm.OkCommand.Execute(null);
        vm.ResultCommand!.Execute();

        Assert.Null(table.EnsureCell(null, 0, 0)!.Format);
    }
}
