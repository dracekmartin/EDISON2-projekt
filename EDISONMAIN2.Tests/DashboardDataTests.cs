using EDISONMAIN2.LegacyMiniApps;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Testy parseru delimited výstupu legacy ALVA.EDISON.MiniAplikaceAPI.LoadData → DashboardData.
/// Primární oddělovač volíme '\0' (jako reálný MLRest.Delimiter), sekundární '~', styl ':'.
/// </summary>
public class DashboardDataTests
{
    private const char D = '\0';

    // Postaví řádky výstupu pro jednu miniaplikaci: timestamp + 1 label + tabulka (2 sloupce, 2 řádky,
    // 2. řádek obarvený). Kódy: objType 1=Label/2=Table; section 1=Label,2=Table,3=Cols,4=Rows,5=Styles.
    private static object[] SampleOutput(string appType) =>
    [
        "",                                                   // [0] status (přeskakuje se)
        $"appType{D}{appType}",
        $"param{D}1{D}21.06.2026 09:00",                      // TimeStamp
        $"data{D}1{D}C1{D}1{D}Celkem 3 vozidla",              // Label
        $"data{D}2{D}C2{D}2{D}T:T",                           // Table start (deskriptor typů sloupců)
        $"data{D}2{D}C2{D}3{D}Linka~Řidič",                   // Cols
        $"data{D}2{D}C2{D}4{D}1~Novák",                       // Row 1
        $"data{D}2{D}C2{D}4{D}2~Svoboda",                     // Row 2
        $"data{D}2{D}C2{D}5{D}3:#FF0000",                     // Row 2 style (icon 3, červená)
    ];

    [Fact]
    public void Parse_LabelTableTimestamp()
    {
        var data = DashboardData.Parse(SampleOutput("52005"), D, "52005");

        Assert.Equal("21.06.2026 09:00", data.LastUpdated);

        // Label těsně nad tabulkou už není samostatná sekce - je z něj titulek pruhu EDListu.
        var table = Assert.IsType<DashboardTable>(Assert.Single(data.Sections));
        Assert.Equal("Celkem 3 vozidla", table.Title);
        Assert.Equal(["Linka", "Řidič"], table.Columns.Select(c => c.Name));
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(["1", "Novák"], table.Rows[0].Cells);
        Assert.Null(table.Rows[0].ForeColorHtml);
        Assert.Equal(["2", "Svoboda"], table.Rows[1].Cells);
        Assert.Equal("#FF0000", table.Rows[1].ForeColorHtml);
        Assert.Equal(3, table.Rows[1].IconType);
    }

    [Fact]
    public void Parse_FiltersToRequestedAppType()
    {
        object[] output =
        [
            "",
            $"appType{D}52005",
            $"data{D}1{D}C1{D}1{D}Patří 52005",
            $"appType{D}52006",
            $"data{D}1{D}C1{D}1{D}Patří 52006",
        ];

        var data = DashboardData.Parse(output, D, "52006");

        var label = Assert.IsType<DashboardLabel>(Assert.Single(data.Sections));
        Assert.Equal("Patří 52006", label.Text);
    }

    [Fact]
    public void Parse_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Empty(DashboardData.Parse(null, D, "52005").Sections);
        Assert.Empty(DashboardData.Parse([], D, "52005").Sections);
        Assert.Empty(DashboardData.Parse(["", "garbage line"], D, "52005").Sections);
    }

    [Fact]
    public void ParseAll_SplitsBatchIntoBlocksPerAppType()
    {
        object[] output =
        [
            "",
            $"appType{D}52005",
            $"param{D}1{D}21.06.2026 09:00",
            $"data{D}1{D}C1{D}1{D}Patří 52005",
            $"appType{D}52006",
            $"data{D}1{D}C1{D}1{D}Patří 52006",
        ];

        var all = DashboardData.ParseAll(output, D);

        Assert.Equal(["52005", "52006"], all.Keys.Order());
        Assert.Equal("21.06.2026 09:00", all["52005"].LastUpdated);
        Assert.Equal("Patří 52005", Assert.IsType<DashboardLabel>(Assert.Single(all["52005"].Sections)).Text);
        // Časové razítko prvního bloku nesmí přetéct do druhého.
        Assert.Null(all["52006"].LastUpdated);
        Assert.Equal("Patří 52006", Assert.IsType<DashboardLabel>(Assert.Single(all["52006"].Sections)).Text);
    }

    [Fact]
    public void ParseAll_AppTypeWithoutBlock_IsAbsent()
    {
        var all = DashboardData.ParseAll(["", $"appType{D}52005"], D);

        Assert.False(all.ContainsKey("52014"));
    }

    // Deskriptor typů z legacy (ObjectSectionTable) - bez něj se N sloupce neformátují,
    // nezarovnají vpravo a řadí se lexikograficky. Motohodiny vozidel (52015) posílají "T:N2#".
    private static DashboardTable TableWithTypes(string types, string columns) =>
        Assert.IsType<DashboardTable>(Assert.Single(DashboardData.Parse(
        [
            "",
            $"appType{D}52015",
            $"data{D}2{D}C1{D}2{D}{types}",
            $"data{D}2{D}C1{D}3{D}{columns}",
        ], D, "52015").Sections));

    [Fact]
    public void Parse_ColumnTypeDescriptor_MapsTypeAndNumberFormat()
    {
        var table = TableWithTypes("T:N2#", "Rz~Celkové MTH");

        Assert.Equal(new DashboardColumn("Rz", 'T', ""), table.Columns[0]);
        Assert.Equal(new DashboardColumn("Celkové MTH", 'N', "#,##0.00"), table.Columns[1]);
    }

    [Theory]
    [InlineData("N0#", "#,##0")]      // celá čísla s oddělovačem tisíců
    [InlineData("N2", "###0.00")]     // desetinná bez oddělovače
    [InlineData("N", "")]             // holé N = bez formátování, hodnota zůstane raw (parita s legacy)
    public void Parse_NumberFormatVariants(string def, string expected) =>
        Assert.Equal(expected, TableWithTypes(def, "X").Columns[0].Format);

    [Fact]
    public void Parse_MissingOrShortDescriptor_FallsBackToText()
    {
        // Kratší deskriptor než počet sloupců - zbytek je TEXT, ne výjimka.
        Assert.Equal(['N', 'T'], TableWithTypes("N0", "A~B").Columns.Select(c => c.Type));
        Assert.Equal(['T', 'T'], TableWithTypes("", "A~B").Columns.Select(c => c.Type));
    }

    [Fact]
    public void Parse_TablePrefsKey_IsUniquePerAppAndTableIndex()
    {
        object[] output =
        [
            "",
            $"appType{D}52015",
            $"data{D}2{D}C1{D}2{D}T",
            $"data{D}2{D}C1{D}3{D}A",
            $"data{D}1{D}C2{D}1{D}mezi tabulkami",
            $"data{D}2{D}C3{D}2{D}T",
            $"data{D}2{D}C3{D}3{D}B",
        ];

        var tables = DashboardData.Parse(output, D, "52015").Sections.OfType<DashboardTable>().ToList();

        Assert.Equal(
            ["EDISONMAIN2.LegacyMiniApp.52015.0", "EDISONMAIN2.LegacyMiniApp.52015.1"],
            tables.Select(t => t.PrefsKey));
    }

    /// <summary>Label, po kterém tabulka nenásleduje, zůstává samostatnou sekcí.</summary>
    [Fact]
    public void Parse_MultipleTablesAndLabels_PreserveOrder()
    {
        object[] output =
        [
            "",
            $"appType{D}52005",
            $"data{D}1{D}C1{D}1{D}L1",
            $"data{D}2{D}C2{D}2{D}text",
            $"data{D}2{D}C2{D}3{D}A",
            $"data{D}2{D}C2{D}4{D}x",
            $"data{D}1{D}C3{D}1{D}L2",
        ];

        var data = DashboardData.Parse(output, D, "52005");

        Assert.Collection(data.Sections,
            s => Assert.Equal("L1", Assert.IsType<DashboardTable>(s).Title),
            s => Assert.Equal("L2", Assert.IsType<DashboardLabel>(s).Text));
    }

    /// <summary>Tabulka bez předchozího labelu titulek nemá - pruh ukáže jen search box.</summary>
    [Fact]
    public void Parse_TableWithoutLabel_HasNoTitle()
    {
        object[] output =
        [
            "",
            $"appType{D}52005",
            $"data{D}2{D}C1{D}2{D}T",
            $"data{D}2{D}C1{D}3{D}A",
        ];

        Assert.Null(Assert.IsType<DashboardTable>(
            Assert.Single(DashboardData.Parse(output, D, "52005").Sections)).Title);
    }

    /// <summary>Titulek si bere jen tabulka, která za labelem opravdu stojí - ne každá další.</summary>
    [Fact]
    public void Parse_LabelTitlesOnlyTheTableBelowIt()
    {
        object[] output =
        [
            "",
            $"appType{D}52005",
            $"data{D}1{D}C1{D}1{D}L1",
            $"data{D}2{D}C2{D}2{D}T",
            $"data{D}2{D}C2{D}3{D}A",
            $"data{D}2{D}C3{D}2{D}T",
            $"data{D}2{D}C3{D}3{D}B",
        ];

        var tables = DashboardData.Parse(output, D, "52005").Sections.OfType<DashboardTable>().ToList();

        Assert.Equal(["L1", null], tables.Select(t => t.Title));
    }
}
