using System.Text.Json.Nodes;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Vlastní sloupce vývěsky: wire kontrakt DEFINICE_SLOUPCU, vzory (presety),
/// injektáž do parametrů GetData a souhrn nezařazených odjezdů. Serverovou stranu vyhodnocení
/// kryjí Caché testy (ZastavkoveJrAdHelperyTest - parita presetů s dnešními rozvrženími).
/// </summary>
public class DepartureColumnDefinitionTests
{
    private static DepartureColumnDefinition Col(string id, string header, string days) =>
        new() { Id = id, Header = header, Days = days };

    // ── Wire JSON - klíče musí sedět na ParseDefiniceSloupcu v ZastavkoveJrAd.cls ────

    [Fact]
    public void WireJson_NeseKliceKtereCteServer()
    {
        var el = new DepartureGridElement
        {
            CustomColumns =
            [
                new DepartureColumnDefinition
                {
                    Id = "1", Header = "Pracovní dny", Header2 = "mimo prázdniny", Days = "12345",
                    IncludeMarks = "5", ExcludeMarks = "21,32", SuppressedMarks = "52"
                }
            ]
        };

        var root = JsonNode.Parse(el.BuildColumnsDefinitionWireJson()!)!.AsObject();
        var sl = root["sloupce"]!.AsArray()[0]!.AsObject();

        Assert.Equal("1",              sl["id"]!.GetValue<string>());
        Assert.Equal("Pracovní dny",   sl["nadpis"]!.GetValue<string>());
        Assert.Equal("mimo prázdniny", sl["nadpis2"]!.GetValue<string>());
        Assert.Equal("12345",          sl["dny"]!.GetValue<string>());
        Assert.Equal("5",              sl["znackyJen"]!.GetValue<string>());
        Assert.Equal("21,32",          sl["znackyBez"]!.GetValue<string>());
        Assert.Equal("52",             sl["nevypisovatZnacky"]!.GetValue<string>());
    }

    [Fact]
    public void WireJson_BezDefiniceJeNull()
    {
        Assert.Null(new DepartureGridElement().BuildColumnsDefinitionWireJson());
        Assert.Null(new DepartureGridElement { CustomColumns = [] }.BuildColumnsDefinitionWireJson());
    }

    // ── Vzory (presety) - tvar 1:1 s paritními Caché testy ──────────────────────────

    [Fact]
    public void Preset_PracSoNe_TriSloupceDny()
    {
        var cols = DepartureColumnPresets.Build("PRAC_SO_NE", null, null);

        Assert.Equal(["1", "2", "3"], cols.Select(c => c.Id));
        Assert.Equal(["12345", "6", "7+"], cols.Select(c => c.Days));
        Assert.Equal(["Pracovní dny", "Soboty", "Neděle a svátky"], cols.Select(c => c.Header));
        Assert.All(cols, c => Assert.Equal("", c.ExcludeMarks));
    }

    [Fact]
    public void Preset_Prazdniny_VylucujeZnackyZNzKonfigurace()
    {
        // nzCfg "21~22|31~32": 21 = jede o prázdninách, 22 = nejede; poziční seznamy prohozené
        // (legacy) → sloupec „mimo prázdniny" vylučuje 21+32, „o prázdninách" 22+31.
        var cols = DepartureColumnPresets.Build("PRAZDNINY_VIKEND", "21~22|31~32", null);

        Assert.Equal(3, cols.Count);
        Assert.Equal("21,32", cols[0].ExcludeMarks);
        Assert.Equal("22,31", cols[1].ExcludeMarks);
        Assert.Equal("", cols[2].ExcludeMarks);
        Assert.Equal("67+", cols[2].Days);
    }

    [Fact]
    public void Preset_BezNzKonfigurace_MaPrazdnaVylouceni()
    {
        var cols = DepartureColumnPresets.Build("PRAZDNINY_SO_NE", "", "");
        Assert.Equal(4, cols.Count);
        Assert.All(cols, c => Assert.Equal("", c.ExcludeMarks));
    }

    [Fact]
    public void Preset_PozicniNevypisovatZnacky_SePreklopiNaSloupce()
    {
        // Poziční linkový seznam MHD_NEZNZ123 (slot N = sloupec N) vzor přenese do per-sloupcových seznamů
        var cols = DepartureColumnPresets.Build("PRAC_SO_NE", null, "52~52~53,54,55~");

        Assert.Equal("52", cols[0].SuppressedMarks);
        Assert.Equal("52", cols[1].SuppressedMarks);
        Assert.Equal("53,54,55", cols[2].SuppressedMarks);
    }

    // ── Šablona: serializace + klon ──────────────────────────────────────────────────

    [Fact]
    public void Serializace_DefiniceSloupcuPrezijeRoundTrip()
    {
        var template = new Template();
        template.Sections.Add(new Section
        {
            Elements = { new DepartureGridElement { CustomColumns = [Col("1", "Pracovní dny", "12345")] } }
        });

        var json = TemplateSerializer.Serialize(template);
        var back = TemplateSerializer.Deserialize(json);

        var el = Assert.IsType<DepartureGridElement>(back.Sections[0].Elements[0]);
        var col = Assert.Single(el.CustomColumns!);
        Assert.Equal("Pracovní dny", col.Header);
        Assert.Equal("12345", col.Days);
    }

    [Fact]
    public void Clone_KopirujeDefiniciDoHloubky()
    {
        var el = new DepartureGridElement { CustomColumns = [Col("1", "Pracovní dny", "12345")] };
        var clone = (DepartureGridElement)el.Clone();

        clone.CustomColumns![0].Header = "Změněno";
        Assert.Equal("Pracovní dny", el.CustomColumns![0].Header);
    }

    // ── Parametry GetData ────────────────────────────────────────────────────────────

    [Fact]
    public void ComposeGetDataParams_PridaDefiniciJenKdyzExistuje()
    {
        var baseParams = new Dictionary<string, object?> { ["ROZDELENI_SLOUPCU"] = "x" };

        var withDef = TemplateDesignerViewModel.ComposeGetDataParams(baseParams, "LINKA_ID", "42", null, "{\"sloupce\":[]}");
        Assert.Equal("{\"sloupce\":[]}", withDef["DEFINICE_SLOUPCU"]);

        var withoutDef = TemplateDesignerViewModel.ComposeGetDataParams(baseParams, "LINKA_ID", "42", null, null);
        Assert.False(withoutDef.ContainsKey("DEFINICE_SLOUPCU"));
    }

    // ── Souhrn nezařazených odjezdů (status bar) ────────────────────────────────────

    [Fact]
    public void UnassignedWarning_AgregujeStrankyADrziCeskePluraly()
    {
        static DataContext Page(string? nezarazene)
        {
            var values = new Dictionary<string, object?>();
            if (nezarazene is not null) values["NEZARAZENE_SPOJE"] = nezarazene;
            return new DataContext(values);
        }

        Assert.Null(UnassignedDepartures.Summarize([Page(null), Page("")]));

        var jeden = UnassignedDepartures.Summarize([Page("5:32")]);
        Assert.Equal("1 odjezd nespadá do žádného sloupce (5:32)", jeden);

        var dva = UnassignedDepartures.Summarize([Page("5:32"), Page("14:10, 5:32")]);
        Assert.Equal("2 odjezdy nespadají do žádného sloupce (5:32, 14:10)", dva);
    }
}
