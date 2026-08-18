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

    // ── Vzory (presety) ─────────────────────────────────────────────────────────────
    // Obsah vzorů staví server (PresetySloupcu) a měří ho živá parita v Caché - klient je jen
    // čte, takže tady se testuje čtení, ne pravidla rozvržení.
    // ⚠️ Tvar dnů musí odpovídat `PresetySloupcu` v `ZastavkoveJrAd.cls`. Když se rozešel (vzory
    // přešly při `B82` z „12345"/„7+" na „X"/„+", fixture zůstala stará), testy dál potvrzovaly
    // chování, které server už neposílá - na server se odtud nedosáhne, hlídá to jen tahle poznámka.

    internal const string PresetyJson = """
        {"PRAC_SO_NE":[
            {"id":"1","nadpis":"Pracovní dny","dny":"X","znackyBez":"","nevypisovatZnacky":"52"},
            {"id":"2","nadpis":"Soboty","dny":"6","znackyBez":"","nevypisovatZnacky":""},
            {"id":"3","nadpis":"Neděle a svátky","dny":"+","znackyBez":"","nevypisovatZnacky":"53,54"}],
         "PRAC_VIKEND":[
            {"id":"1","nadpis":"Pracovní dny","dny":"X","znackyBez":"21,32","nevypisovatZnacky":""},
            {"id":"2","nadpis":"Soboty, neděle a svátky","dny":"6+","znackyBez":"","nevypisovatZnacky":""}]}
        """;

    [Fact]
    public void Presety_PrectouSeZeServeru()
    {
        var presety = DepartureColumnPresets.Parse(PresetyJson);

        Assert.Equal(2, presety.Count);
        var cols = presety["PRAC_SO_NE"];
        Assert.Equal(["1", "2", "3"], cols.Select(c => c.Id));
        Assert.Equal(["X", "6", "+"], cols.Select(c => c.Days));
        Assert.Equal(["Pracovní dny", "Soboty", "Neděle a svátky"], cols.Select(c => c.Header));
        Assert.Equal("53,54", cols[2].SuppressedMarks);
        Assert.Equal("21,32", presety["PRAC_VIKEND"][0].ExcludeMarks);
    }

    /// <summary>Bez dat (standalone běh, agenda bez vzorů) se vzory prostě nenabídnou - klient
    /// si je nesmí složit sám, jinak by se rozešel se serverem a parita by hlídala kopii.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("tohle není JSON")]
    [InlineData("[1,2,3]")]
    public void Presety_BezPodkladuJsouPrazdne(string? json)
        => Assert.Empty(DepartureColumnPresets.Parse(json));

    /// <summary>Server smí kontrakt rozšířit (aditivní evoluce) - chybějící klíč nesmí shodit čtení.</summary>
    [Fact]
    public void Presety_ChybejiciKliceJsouPrazdneRetezce()
    {
        var presety = DepartureColumnPresets.Parse("""{"X":[{"id":"1"}]}""");
        var col = presety["X"][0];
        Assert.Equal("1", col.Id);
        Assert.Equal("", col.Header);
        Assert.Equal("", col.Days);
        Assert.Equal("", col.Period);
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

        var withDef = TemplateDesignerViewModel.ComposeGetDataParams(baseParams, "LINKA_ID", "42", null, "{\"sloupce\":[]}", null);
        Assert.Equal("{\"sloupce\":[]}", withDef["DEFINICE_SLOUPCU"]);

        var withoutDef = TemplateDesignerViewModel.ComposeGetDataParams(baseParams, "LINKA_ID", "42", null, null, null);
        Assert.False(withoutDef.ContainsKey("DEFINICE_SLOUPCU"));
    }

    // ── Souhrn toho, co sloupce nevzaly (status bar) ────────────────────────────────

    private static DataContext Page(string? nezarazene, string? mimoPlatnost = null, string? cisla = null)
    {
        var values = new Dictionary<string, object?>();
        if (nezarazene is not null) values["NEZARAZENE_SPOJE"] = nezarazene;
        if (mimoPlatnost is not null) values["OBDOBI_MIMO_PLATNOST"] = mimoPlatnost;
        if (cisla is not null) values["NEZARAZENE_SPOJE_CISLA"] = cisla;
        return new DataContext(values);
    }

    /// <summary>Tentýž spoj vypadne na každé zastávce linky zvlášť, takže počítat časy znamená
    /// hlásit desítky problémů místo tří (KOVED 802192: 31 odjezdů = 3 spoje).</summary>
    [Fact]
    public void ColumnWarning_PocitaSpojeNeOdjezdy()
    {
        var pages = new[]
        {
            Page("5:32", cisla: "10"),
            Page("5:40, 14:10", cisla: "10,23"),
            Page("5:48", cisla: "10"),
        };

        var text = DepartureColumnWarnings.Summarize(pages);

        Assert.StartsWith("2 spoje se nevejdou do žádného sloupce", text);
        Assert.Contains("č. 10, 23", text);
        // pořadí je vzestupné, ne v jakém je potkaly stránky - jinak to vypadá jako jiná čísla
        var prehazene = DepartureColumnWarnings.Summarize([Page("5:32", cisla: "21,3"), Page("5:40", cisla: "14")]);
        Assert.Contains("č. 3, 14, 21", prehazene);
        // časy odjezdů v závorce být nesmí - je jich víc než spojů a hláška by se rozešla s počtem
        Assert.DoesNotContain("5:32", text);
    }

    [Fact]
    public void ColumnWarning_AgregujeStrankyADrziCeskePluraly()
    {
        Assert.Null(DepartureColumnWarnings.Summarize([Page(null), Page("")]));

        var jeden = DepartureColumnWarnings.Summarize([Page("5:32", cisla: "10")]);
        Assert.Equal("1 spoj se nevejde do žádného sloupce a na vývěsce nebude (č. 10)", jeden);

        var pet = DepartureColumnWarnings.Summarize([Page("5:32", cisla: "1,2,3,4,5")]);
        Assert.StartsWith("5 spojů se nevejde", pet);
    }

    /// <summary>Bez čísel spojů (data staršího serveru) se počítají časy - hláška nesmí zmizet.</summary>
    [Fact]
    public void ColumnWarning_BezCiselSpojuPocitaOdjezdy()
    {
        var text = DepartureColumnWarnings.Summarize([Page("5:32"), Page("14:10, 5:32")]);
        Assert.StartsWith("2 odjezdy se nevejdou do žádného sloupce", text);
        Assert.Contains("5:32", text);
    }

    /// <summary>Sloupec s obdobím mimo platnost jízdního řádku zůstane prázdný - bez hlášky by
    /// uživatel neměl jak zjistit proč (typicky šablona, která přežila překlopení jízdního řádku).</summary>
    [Fact]
    public void ColumnWarning_HlasiObdobiMimoPlatnostAJmenujeSloupce()
    {
        var jeden = DepartureColumnWarnings.Summarize([Page(null, "Vánoční provoz")]);
        Assert.Equal("období sloupce „Vánoční provoz“ leží mimo platnost jízdního řádku - sloupec zůstane prázdný", jeden);

        var dva = DepartureColumnWarnings.Summarize([Page(null, "Vánoce"), Page(null, "Prázdniny, Vánoce")]);
        Assert.Equal("období sloupců „Vánoce“, „Prázdniny“ leží mimo platnost jízdního řádku - zůstanou prázdné", dva);
    }

    /// <summary>Obě vady naráz se spojí do jedné hlášky - badge ve status baru je jeden.</summary>
    [Fact]
    public void ColumnWarning_SpojiObeVadyDoJedneHlasky()
    {
        var obe = DepartureColumnWarnings.Summarize([Page("5:32", "Vánoce")]);
        Assert.NotNull(obe);
        Assert.Contains("1 odjezd se nevejde", obe);
        Assert.Contains("mimo platnost jízdního řádku", obe);
        Assert.Contains(";", obe);
    }

    // ── Období sloupce - validace vstupu ────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("1.7.-31.8.")]
    [InlineData("14.2.-18.2., 1.7.-31.8., 29.10.-30.10.")]
    [InlineData("27.12.2026-29.12.2026")]
    [InlineData("1.7.2026-31.8.")]
    [InlineData("24.12.")]
    public void Obdobi_PlatneTvaryProjdou(string obdobi)
        => Assert.True(DepartureGroupEditorDialogViewModel.JePlatneObdobi(obdobi));

    /// <summary>Server nerozpoznaný rozsah tiše zahodí - sloupec by pak mlčky bral všechny spoje,
    /// takže neplatný tvar musí zastavit OK v dialogu.</summary>
    [Theory]
    [InlineData("od cervence do srpna")]
    [InlineData("1.7.-31.8.-30.9.")]
    [InlineData("32.1.-5.2.")]
    [InlineData("1.13.-5.2.")]
    [InlineData("2026-07-01")]
    [InlineData("1.7.1800-31.8.1800")]
    public void Obdobi_NeplatneTvaryNeprojdou(string obdobi)
        => Assert.False(DepartureGroupEditorDialogViewModel.JePlatneObdobi(obdobi));
}
