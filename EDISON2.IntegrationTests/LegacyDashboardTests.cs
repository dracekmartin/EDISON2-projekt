using System.Text.RegularExpressions;
using ALWA.SYS;
using EDControls;
using EDISONMAIN2.LegacyMiniApps;

namespace EDISON2.IntegrationTests;

/// <summary>
/// Legacy Přehledy: skutečný <c>ALVA.EDISON.MiniAplikaceAPI.LoadData</c> do produkčního
/// <see cref="LegacyDashboardLoader"/> a <see cref="DashboardData"/>. Jen <c>refresh=0</c>: refresh=1
/// přepočítá data miniaplikací na serveru.
/// </summary>
[Trait("Category", "Integration")]
public class LegacyDashboardTests
{
    private const string CacheClass = "ALVA.EDISON.MiniAplikaceAPI";
    // DEV má funkční omezení Přehledů (data per uživatel) a uživatel 1 žádné nemá: test si jeden zapíše pod testovací firmu.
    private const string Fixture = "ALVA.EDTesting.ITDashboard";
    private const string SeededAppType = "52001";

    private sealed class RawBlock
    {
        public int Labels, Tables, Rows;
    }

    [DevServerFact]
    public async Task LoadData_SeededDashboardInShellBatch_ParsesSectionsAtProductionPositions()
    {
        var rest = DevServer.Connect(DevServer.WriteIdfa, DevServer.WriteMlp);
        var fixture = new CVar();
        fixture["appType"] = new CVar(SeededAppType);
        // Mimo try: úklid by chybu zápisu zakryl. Seed před zápisem uzel maže, opakovaný běh nic nehromadí.
        rest.QueryOrThrow(Fixture, "Seed", fixture);
        try
        {
            Action? flush = null;
            var loader = new LegacyDashboardLoader(new RestCallRunner(() => rest), action => flush = action);
            var appTypes = new LegacyDashboardMiniAppProvider(loader).GetMiniApps()
                .Select(descriptor => descriptor.Id["Legacy.".Length..]).ToArray();
            Assert.Contains(SeededAppType, appTypes);
            var loads = appTypes.ToDictionary(appType => appType, appType => loader.LoadAsync(appType, refresh: false));
            Assert.NotNull(flush);
            flush();
            await Task.WhenAll(loads.Values);

            var parsed = await loads[SeededAppType];
            Assert.Matches(new Regex(@"^\d{2}\.\d{2}\.\d{4}"), parsed.LastUpdated);
            Assert.Collection(parsed.Sections,
                section =>
                {
                    var table = Assert.IsType<DashboardTable>(section);
                    Assert.Equal("IT nadpis", table.Title);
                    Assert.Equal(new[] { new DashboardColumn("Nazev", 'T', ""), new DashboardColumn("Hodnota", 'N', "#,##0.00") }, table.Columns);
                    Assert.Collection(table.Rows,
                        row =>
                        {
                            Assert.Equal(new[] { "prvni", "1" }, row.Cells);
                            Assert.Null(row.ForeColorHtml);
                            Assert.Equal(0, row.IconType);
                        },
                        row =>
                        {
                            Assert.Equal(new[] { "druhy", "2" }, row.Cells);
                            Assert.Equal("#FF0000", row.ForeColorHtml);
                            Assert.Equal(1, row.IconType);
                        });
                },
                section => Assert.Equal(new DashboardLabel("IT poznamka"), section));

            var raw = RawBlocks(rest, appTypes);
            Assert.Equal(new[] { SeededAppType }, raw.Keys);
            foreach (var appType in appTypes)
            {
                var data = await loads[appType];
                var tables = data.Sections.OfType<DashboardTable>().ToList();
                var block = raw.GetValueOrDefault(appType) ?? new RawBlock();
                Assert.True(block.Tables == tables.Count, $"{appType}: tabulek surově {block.Tables}, parser {tables.Count}.");
                Assert.True(block.Rows == tables.Sum(table => table.Rows.Count),
                    $"{appType}: řádků tabulek surově {block.Rows}, parser {tables.Sum(table => table.Rows.Count)}.");
                // Label těsně nad tabulkou parser převede na její nadpis.
                int labels = data.Sections.OfType<DashboardLabel>().Count() + tables.Count(table => table.Title is not null);
                Assert.True(block.Labels == labels, $"{appType}: labelů surově {block.Labels}, parser {labels}.");
            }
        }
        finally
        {
            rest.QueryOrThrow(Fixture, "Clean", fixture);
        }
    }

    // Stejný vstup jako LegacyDashboardLoader. Do zpráv jen tag a čísla polí, ne hodnoty.
    private static Dictionary<string, RawBlock> RawBlocks(IMLRest rest, string[] appTypes)
    {
        char d = rest.Delimiter;
        var input = new object[] { $"refresh{d}0" }.Concat(appTypes.Select(appType => (object)$"appType{d}{appType}")).ToArray();
        var output = rest.QueryOrThrow(CacheClass, "LoadData", input);

        var blocks = new Dictionary<string, RawBlock>();
        RawBlock? current = null;
        foreach (var line in LegacyOutput.Values(output))
        {
            var t = line.Split(d);
            string head = string.Join("|", t.Take(t[0] == "data" ? 4 : 2));
            switch (t[0])
            {
                case "appType":
                    Assert.True(t.Length == 2 && appTypes.Contains(t[1]), $"Řádek appType nemá 2 pole s vyžádaným typem: {head}");
                    blocks[t[1]] = current = new RawBlock();
                    break;
                case "param":
                    Assert.True(current is not null && t.Length == 3 && int.TryParse(t[1], out _),
                        $"Řádek param není v bloku nebo nemá 3 pole s číselným indexem: {head}");
                    break;
                case "data":
                    Assert.True(current is not null && t.Length >= 5 && int.TryParse(t[1], out _),
                        $"Řádek data není v bloku nebo nemá aspoň 5 polí s číselným typem objektu: {head}");
                    Assert.True(int.TryParse(t[3], out var section) && section is >= 1 and <= 5, $"Sekce na pozici 3 není 1-5: {head}");
                    if (section == 1) current!.Labels++;
                    if (section == 2) current!.Tables++;
                    if (section == 4) current!.Rows++;
                    break;
                default:
                    Assert.Fail($"Neznámý řádek, parser by ho zahodil: {head}");
                    break;
            }
        }
        return blocks;
    }
}
