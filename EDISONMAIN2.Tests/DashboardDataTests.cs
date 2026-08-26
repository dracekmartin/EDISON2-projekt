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
        $"data{D}2{D}C2{D}2{D}text:text",                     // Table start (datové typy sloupců)
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
        Assert.Equal(2, data.Sections.Count);

        var label = Assert.IsType<DashboardLabel>(data.Sections[0]);
        Assert.Equal("Celkem 3 vozidla", label.Text);

        var table = Assert.IsType<DashboardTable>(data.Sections[1]);
        Assert.Equal(["Linka", "Řidič"], table.Columns);
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
            s => Assert.Equal("L1", Assert.IsType<DashboardLabel>(s).Text),
            s => Assert.Single(Assert.IsType<DashboardTable>(s).Rows),
            s => Assert.Equal("L2", Assert.IsType<DashboardLabel>(s).Text));
    }
}
