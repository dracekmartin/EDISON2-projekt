using System.IO;
using System.Text.RegularExpressions;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Věci, na kterých se zasekl nováček (review 4. 8., blok `B58`): náhled s ukázkovými daty nebyl
/// nijak označený a nápověda F1 neznala featury posledního měsíce. Testy drží to, co jde ověřit
/// strojově - hlavně **aby nápověda nezačala lhát** o tom, kde se co zapíná.
/// (Popis místo interního kódu ve výběru agendy se 5. 8. **vrátil** - rozhodnutí Jan, kód je
/// pochopitelný; testy na popis proto zrušené.)
/// </summary>
public class NovackovaOrientaceTests
{
    // ── Štítek „ukázková data" u náhledu ─────────────────────────────────────

    [Fact]
    public void UkazkovaData_BezVybraneInstanceSeHlasi()
    {
        Assert.True(TemplateDesignerViewModel.ShouldReportSampleData(
            realPageCount: 0, agendaCode: "ZASTAVKOVY_JR_AD", agendaHasData: true));
    }

    [Fact]
    public void UkazkovaData_SReálnymiDatySeNeoznacuji()
    {
        Assert.False(TemplateDesignerViewModel.ShouldReportSampleData(
            realPageCount: 3, agendaCode: "ZASTAVKOVY_JR_AD", agendaHasData: true));
    }

    /// <summary>U šablony bez vazby na databázi by štítek lhal - žádná reálná data se tam nečekají.</summary>
    [Fact]
    public void UkazkovaData_SablonaBezVazbyNaDatabaziSeNeoznacuje()
    {
        Assert.False(TemplateDesignerViewModel.ShouldReportSampleData(
            realPageCount: 0, agendaCode: "GENERAL", agendaHasData: false));
        Assert.False(TemplateDesignerViewModel.ShouldReportSampleData(
            realPageCount: 0, agendaCode: "", agendaHasData: false));
        Assert.False(TemplateDesignerViewModel.ShouldReportSampleData(
            realPageCount: 0, agendaCode: null, agendaHasData: false));
    }

    // ── Nápověda F1 nesmí lhát o tom, kde se co zapíná ───────────────────────

    /// <summary>
    /// Nápověda tvrdí, že se značky u odjezdů a souhrn bezbariérovosti zapínají v prvku
    /// Legenda poznámek ve skupině „Značky u odjezdů" (dřív to byly parametry tisku). Kdyby se ty
    /// přepínače přesunuly nebo přejmenovaly, nápověda by posílala uživatele na místo, kde nic
    /// není - a to je přesně ta situace, kterou má `B58` řešit, ne vyrobit.
    /// </summary>
    [Fact]
    public void Napoveda_PrepinaceZnacekOpravduJsouVeFiltrovaniLegendy()
    {
        var znacky = FieldsInGroup("NotesLegendElementEdit.cls", "Značky u odjezdů");

        Assert.Contains("ShowZnackaKola", znacky);            // přeprava kol
        Assert.Contains("ShowZnackaMistenky", znacky);        // místenky
        Assert.Contains("ShowPokracujiciSpoje", znacky);      // pokračující spoje
        Assert.Contains("ShowSouhrnBezbarier", znacky);       // souhrn bezbariérových spojů
    }

    /// <summary>Nápověda posílá k rozdělení dlouhé trasy do prvku Zastávky JŘ - ať tam ta volba je.</summary>
    [Fact]
    public void Napoveda_RozdeleniNaOddilyOpravduJeUZastavek()
    {
        var text = ClsText("StopListElementEdit.cls");

        Assert.Contains("\"PartCount\", \"Rozdělit na více oddílů\"", text);
        Assert.Contains("\"PartGap\", \"Mezera mezi oddíly\"", text);
    }

    /// <summary>Nápověda i AI pomocník slibují řazení řádků dynamické tabulky.</summary>
    [Fact]
    public void Napoveda_RazeniDynamickeTabulkyOpravduExistuje()
    {
        var text = ClsText("DynamicTableElementEdit.cls");

        Assert.Contains("\"SortField\", \"Řadit podle\"", text);
        Assert.Contains("\"SortDescending\", \"Směr\"", text);
    }

    /// <summary>
    /// Nápověda slibuje u pevné tabulky vlastnosti buňky (Alt+Enter) a vlastnosti řádku s podmínkou
    /// viditelnosti - obojí jede na serverovém schématu, takže kdyby se pole přejmenovala, posílala
    /// by nápověda uživatele na místo, kde nic není.
    /// </summary>
    [Fact]
    public void Napoveda_VlastnostiBunkyARadkuOpravduExistuji()
    {
        Assert.Contains("\"Format\", \"Formát hodnoty\"", ClsText("TableCellEdit.cls"));
        Assert.Contains("\"Style\", \"cellstyle\"", ClsText("TableCellEdit.cls"));

        var radek = ClsText("TableRowEdit.cls");
        Assert.Contains("\"BackgroundColor\", \"Barva pozadí\"", radek);
        Assert.Contains("AddConditions(form, \"řádek\")", radek);
    }

    /// <summary>Nápověda i příručka tvrdí, že se rozměr tabulky zadává při vložení.</summary>
    [Fact]
    public void Napoveda_RozmerTabulkySeOpravduZadavaPriVlozeni()
    {
        var text = ClsText("TableInsertEdit.cls");

        Assert.Contains("\"Columns\", \"Počet sloupců\"", text);
        Assert.Contains("\"Rows\", \"Počet řádků\"", text);
    }

    // ── Parsování .cls (stejný zdroj pravdy jako drift testy schémat) ─────────

    private static readonly Regex GroupRx = new(
        @"AddGroup\(\s*""[^""]*"",\s*""([^""]+)""", RegexOptions.Compiled);

    private static readonly Regex FieldKeyRx = new(
        @"form\.Add(?:Text|Bool|Number|Decimal|FieldToLastGroup)\(\s*""([^""]+)""", RegexOptions.Compiled);

    /// <summary>Klíče polí, která leží ve zadané skupině formuláře (od jejího AddGroup po další).</summary>
    private static List<string> FieldsInGroup(string clsFile, string groupLabel)
    {
        var keys = new List<string>();
        bool inGroup = false;

        foreach (var line in File.ReadAllLines(ClsPath(clsFile)))
        {
            if (GroupRx.Match(line) is { Success: true } g)
            {
                inGroup = g.Groups[1].Value == groupLabel;
                continue;
            }
            if (inGroup && FieldKeyRx.Match(line) is { Success: true } f)
                keys.Add(f.Groups[1].Value);
        }

        Assert.NotEmpty(keys);   // prázdno = skupina se přejmenovala, ne že je prázdná
        return keys;
    }

    private static string ClsText(string clsFile) => File.ReadAllText(ClsPath(clsFile));

    private static string ClsPath(string clsFile)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EDISON2.sln")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        string path = Path.Combine(dir!.FullName, "TemplateDesigner", "CACHE", "ALVA",
            "TemplateDesigner", "EDInputForm", clsFile);
        Assert.True(File.Exists(path), $"Nenalezen formulář: {path}");
        return path;
    }
}
