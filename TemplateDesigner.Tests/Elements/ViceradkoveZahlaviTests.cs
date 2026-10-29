using EDControls;
using EDControls.EDViewModel;
using NSubstitute;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Tests.Rendering;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Záhlaví sloupce na víc řádků (Enter nebo \n) v Tabulce spojů a Tabulce z dat (`B194d`). Kreslení
/// buněk víc řádků umělo vždycky, ale výška záhlaví počítala s jedním řádkem, takže druhý přetekl do
/// dat - proto ho okno sloupců u těch dvou tabulek nenabízelo. Seznam zastávek a Minuty to uměly.
/// </summary>
public class ViceradkoveZahlaviTests
{
    public ViceradkoveZahlaviTests() =>
        EDServices.Initialize(new EDServices(Substitute.For<IMLRest>(),
            Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()));

    /// <summary>Výška jednoho řádku záhlaví stejně jako u ostatních tabulek (písmo × 1,8).</summary>
    private static float RadekZahlavi(float pismoPt) => pismoPt * ResolvedTextStyle.PtToMm * 1.8f;

    // ── Tabulka spojů ─────────────────────────────────────────────────────────

    private static DataContext DataLinky() => RenderSnapshot.Data(
        ("ZASTAVKY",
        [
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Kroměříž"), ("PORADI", "1"), ("TC", "1")),
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Hulín"), ("PORADI", "2"), ("TC", "2")),
        ]),
        ("SPOJE",
        [.. Enumerable.Range(0, 3).Select(i => RenderSnapshot.Row(
            ("CISLO_SPOJE", (i + 1).ToString()), ("SKUPINA", "1"), ("SMER", "T"),
            ("CASY", new System.Text.Json.Nodes.JsonArray("8:00", "8:20"))))]),
        ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", ""), ("PORADI", "1"))]));

    private static LineTableElement TabulkaSpoju(string zahlaviZastavky)
    {
        var prvek = new LineTableElement { X = 0, Y = 0, Width = 120, Height = null };
        prvek.Columns.Single(c => c.Key == ColumnKeys.StopName).Header = zahlaviZastavky;
        return prvek;
    }

    [Theory]
    [InlineData("Zastávka\nStop")]
    [InlineData("Zastávka\\nStop")]
    [InlineData("Zastávka\r\nStop")]
    public void TabulkaSpoju_DvaRadkyZahlavi_VyskaVzrosteOJedenRadek(string zahlavi)
    {
        var data = DataLinky();
        float jeden = TabulkaSpoju("Zastávka").CalculateHeightFromData(data);
        float dva = TabulkaSpoju(zahlavi).CalculateHeightFromData(data);

        Assert.Equal(RadekZahlavi(9f), dva - jeden, 3);
    }

    /// <summary>Opakovaná hlavička na pokračovací straně musí mít záhlaví stejně vysoké jako první strana.</summary>
    [Fact]
    public void TabulkaSpoju_OpakovanaHlavickaPocitaSeVsemiRadky()
    {
        var data = DataLinky();
        float jeden = TabulkaSpoju("Zastávka").OpakovanaHlavickaMm(data);
        float tri = TabulkaSpoju("Zastávka\nStop\nHaltestelle").OpakovanaHlavickaMm(data);

        Assert.Equal(2 * RadekZahlavi(9f), tri - jeden, 3);
    }

    [Fact]
    public void OknoTabulkySpoju_NabiziViceradkoveZahlavi()
    {
        var prvek = new LineTableElement();
        Assert.True(new LineTableColumnsEditorDialogViewModel(prvek, prvek.Columns).Columns.MultilineHeader);
    }

    // ── Tabulka z dat ─────────────────────────────────────────────────────────

    private static DynamicTableElement TabulkaZDat(string zahlavi, float vyskaZahlavi = 8f) => new()
    {
        Height = null,
        HeaderHeight = vyskaZahlavi,
        Columns = [new TableColumn { Header = zahlavi, Field = "CENA" }, new TableColumn { Header = "Kód", Field = "KOD" }],
    };

    /// <summary>Jednořádkové záhlaví má nastavenou výšku beze změny - existující šablony se nezmění.</summary>
    [Fact]
    public void TabulkaZDat_JedenRadek_NastavenaVyska()
    {
        float bez = TabulkaZDat("Cena").CalculateHeightFromData(null);
        float bezZahlavi = new DynamicTableElement { Height = null, ShowHeader = false,
            Columns = [new TableColumn { Header = "Cena", Field = "CENA" }] }.CalculateHeightFromData(null);

        Assert.Equal(8f, bez - bezZahlavi, 3);
    }

    /// <summary>Víc řádků, než se vejde do nastavené výšky: záhlaví se zvětší, aby se řádky vešly.</summary>
    [Fact]
    public void TabulkaZDat_DvaRadky_ZahlaviSeZvetsi()
    {
        float jeden = TabulkaZDat("Cena").CalculateHeightFromData(null);
        float dva = TabulkaZDat("Cena\nbez DPH").CalculateHeightFromData(null);

        Assert.Equal(2 * RadekZahlavi(9f) - 8f, dva - jeden, 3);
    }

    /// <summary>Nastavená výška, do které se řádky vejdou, zůstává - je to nejmenší výška záhlaví.</summary>
    [Fact]
    public void TabulkaZDat_DvaRadkyVeVysokemZahlavi_VyskaZustane()
    {
        float jeden = TabulkaZDat("Cena", vyskaZahlavi: 20f).CalculateHeightFromData(null);
        float dva = TabulkaZDat("Cena\nbez DPH", vyskaZahlavi: 20f).CalculateHeightFromData(null);

        Assert.Equal(jeden, dva, 3);
    }

    [Fact]
    public void OknoTabulkyZDat_NabiziViceradkoveZahlavi()
    {
        Assert.True(new DynamicTableEditorDialogViewModel(TabulkaZDat("Cena"), []).Columns.MultilineHeader);
    }
}
