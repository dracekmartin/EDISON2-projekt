using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using EDControls;
using EDControls.EDInputForm;
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
/// a to, že otevření a potvrzení formuláře beze změny nezapeče zděděné hodnoty do buňky.
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

    // ── Formulář buňky (panel i okno Alt+Enter jedou na tomtéž schématu) ──────

    /// <summary>Pole formuláře podle schématu buňky, jak ho vydá server (klíč = property path).</summary>
    private static List<Field> CellForm() =>
    [
        new Field { Key = "Format",          Label = "Formát hodnoty", FieldType = FieldType.Text },
        new Field { Key = "Style.FontSize",  Label = "Velikost",       FieldType = FieldType.Decimal },
        new Field { Key = "Style.Bold",      Label = "Tučné",          FieldType = FieldType.Bool },
        new Field { Key = "Style.TextColor", Label = "Barva textu",    FieldType = FieldType.Color },
    ];

    private static (List<Field> Fields, Dictionary<string, string> PreFilled) OpenForm(TableElement table)
    {
        var fields = CellForm();
        ElementFormBinder.ApplyInheritedValues(fields, Formatting(table).InheritedFormValues());
        var preFilled = ElementFormBinder.Prefill(fields, table.EnsureCell(null, 0, 0)!);
        return (fields, preFilled);
    }

    [Fact]
    public void FormularBunky_UkazujeHodnotyZKaskady()
    {
        var table = Grid();
        table.DataStyle = new TextStyle { FontSize = 13f, Bold = true, TextColor = "#FF445566" };

        var (fields, _) = OpenForm(table);

        Assert.Equal("13", fields.Single(f => f.Key == "Style.FontSize").Value);
        Assert.Equal("1", fields.Single(f => f.Key == "Style.Bold").Value);
        Assert.Equal("#FF445566", fields.Single(f => f.Key == "Style.TextColor").Value);
    }

    /// <summary>
    /// Nejdůležitější test celého bloku: formulář ukazuje zděděné hodnoty, ale samo jeho otevření
    /// a potvrzení je **nesmí** zapsat do buňky. Jinak by se buňka odpojila od nastavení tabulky
    /// a pozdější změna písma celé tabulky by ji minula.
    /// </summary>
    [Fact]
    public void PotvrzeniFormulareBezeZmeny_NezapeceZdedeneHodnoty()
    {
        var table = Grid();
        table.DataStyle = new TextStyle { FontSize = 13f, Bold = true, TextColor = "#FF445566" };
        var cell = table.EnsureCell(null, 0, 0)!;

        var (fields, preFilled) = OpenForm(table);
        var commands = ElementFormBinder.ExtractCommands(fields, cell, preFilled);

        Assert.Empty(commands);
        Assert.Null(cell.Style);
    }

    [Fact]
    public void ZmenaJednohoPole_ZapiseJenJi()
    {
        var table = Grid();
        table.DataStyle = new TextStyle { FontSize = 13f };
        var cell = table.EnsureCell(null, 0, 0)!;

        var (fields, preFilled) = OpenForm(table);
        fields.Single(f => f.Key == "Style.Bold").Value = "1";
        foreach (var command in ElementFormBinder.ExtractCommands(fields, cell, preFilled)) command.Execute();

        Assert.True(cell.Style!.Bold);
        Assert.Null(cell.Style.FontSize);                                   // dál se dědí
        Assert.Equal(13f, Formatting(table).ActiveStyle().FontSize);
    }

    /// <summary>Vymazané pole = zpátky dědění, ne prázdná hodnota uložená do šablony.</summary>
    [Fact]
    public void VymazanePole_VratiDedeni()
    {
        var table = Grid();
        var cell = table.EnsureCell(null, 0, 0)!;
        cell.Style = new TextStyle { FontSize = 20f };
        cell.Format = "N2";

        var (fields, preFilled) = OpenForm(table);
        fields.Single(f => f.Key == "Style.FontSize").Value = "";
        fields.Single(f => f.Key == "Format").Value = "";
        foreach (var command in ElementFormBinder.ExtractCommands(fields, cell, preFilled)) command.Execute();

        Assert.Null(cell.Style!.FontSize);
        Assert.Null(cell.Format);
    }

    // ── Vazba na serverové schéma ─────────────────────────────────────────────

    private static string CellSchemaSource() => File.ReadAllText(Path.Combine(
        EDInputFormDir(), "TableCellEdit.cls"));

    private static string EDInputFormDir()
    {
        // Kořen repa: od výstupní složky nahoru, a když tam není (běh s přesměrovaným výstupem),
        // podle cesty k tomuhle souboru. Test tak závisí jen na obsahu repa, ne na tom, kde běží.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EDISON2.sln"))) dir = dir.Parent;
        dir ??= new FileInfo(ThisFile()).Directory!.Parent!.Parent;
        return Path.Combine(dir.FullName, "TemplateDesigner", "CACHE", "ALVA", "TemplateDesigner", "EDInputForm");
    }

    private static string ThisFile([CallerFilePath] string path = "") => path;

    /// <summary>
    /// Formulář buňky je vědomě mimo `EDInputFormSchemaDriftTests` (jeho výchozí hodnoty se nikdy
    /// nezobrazí - klient je přepíše kaskádou), takže vazbu na C# musí hlídat tenhle test: každé
    /// pole schématu musí existovat jako vlastnost buňky.
    /// </summary>
    [Fact]
    public void KazdePoleSchematuBunky_ExistujeNaTableCell()
    {
        var keys = Regex.Matches(CellSchemaSource(), @"form\.Add(?:Text|Bool|Number|Decimal|FieldToLastGroup)\(\s*""([^""]+)""")
            .Select(m => m.Groups[1].Value)
            .Where(k => !k.StartsWith("__"))
            .ToList();

        var cell = new TableCell();
        Assert.NotEmpty(keys);
        Assert.All(keys, k => Assert.True(ElementPropertyAccessor.Exists(cell, k), $"pole {k} není vlastnost buňky"));
    }

    /// <summary>
    /// Pojistka toho, proč je formulář z driftových testů vyjmutý: **žádné** pole stylu nesmí
    /// zůstat bez hodnoty z kaskády, jinak by u něj formulář ukázal výchozí hodnotu ze schématu
    /// (typicky nezaškrtnuté „Tučné" u textu, který tučný je) a uživatel by ji potvrzením zapekl.
    /// </summary>
    [Fact]
    public void ZdedeneHodnoty_PokryvajiVsechnaStylovaPoleSchematu()
    {
        var styleKeys = Regex.Matches(CellSchemaSource(), @"prefix_""\.(\w+)""")
            .Select(m => "Style." + m.Groups[1].Value)
            .ToList();
        // Balíček polí stylu je sdílený - klíče se čtou ze StyleFields.cls, kde je definovaný.
        if (styleKeys.Count == 0)
            styleKeys = Regex.Matches(File.ReadAllText(Path.Combine(EDInputFormDir(), "StyleFields.cls")),
                    @"prefix_""\.(\w+)""")
                .Select(m => "Style." + m.Groups[1].Value)
                .ToList();

        Assert.NotEmpty(styleKeys);
        var inherited = Formatting(Grid()).InheritedFormValues();
        Assert.All(styleKeys, k => Assert.True(inherited.ContainsKey(k), $"pole {k} nemá hodnotu z kaskády"));
    }
}
