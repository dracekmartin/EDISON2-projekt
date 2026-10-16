using SkiaSharp;
using TemplateDesigner.Config;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Rozdílový linkový JŘ (`B192`): server pošle změněný spoj dvakrát (původní a nový) a u každého
/// řekne, které buňky se liší. Tabulka podbarví jen ty buňky - barvou podle verze ze šablony.
/// </summary>
public class RozdilovyJrTests
{
    private const float MmToPx = 96f / 25.4f;

    private static DataContext Linka(bool sVerzemi, string rozdily = "[0,1]", string? platnost = null) => RenderSnapshot.Data(
        ("ZASTAVKY",
        [
            RenderSnapshot.Row(("ID", "1"), ("NAZEV_ZASTAVKY", "Strakonice"), ("PORADI", "1")),
            RenderSnapshot.Row(("ID", "2"), ("NAZEV_ZASTAVKY", "Blatná"), ("PORADI", "2")),
        ]),
        ("SPOJE",
        [
            Spoj("21", ["14:15", "14:20"], sVerzemi ? "puvodni" : null, rozdily, platnost),
            Spoj("21", ["14:15", "14:22"], sVerzemi ? "nova" : null, rozdily, platnost),
        ]),
        ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", "Pracovní dny"), ("PORADI", "1"))]));

    private static DataContext Spoj(string cislo, string[] casy, string? verze, string rozdily, string? platnost)
    {
        var pole = new List<(string, object?)>
        {
            ("CISLO_SPOJE", cislo), ("SKUPINA", "1"), ("SMER", "T"),
            ("CASY", new System.Text.Json.Nodes.JsonArray([.. casy.Select(c => (System.Text.Json.Nodes.JsonNode?)c)])),
        };
        if (platnost is not null)
            pole.Add(("PLATNOST", new System.Text.Json.Nodes.JsonArray((System.Text.Json.Nodes.JsonNode?)platnost)));
        if (verze is not null)
        {
            pole.Add(("VERZE", verze));
            pole.Add(("ROZDILY", System.Text.Json.Nodes.JsonNode.Parse(rozdily)));
        }
        return RenderSnapshot.Row([.. pole]);
    }

    private static LineTableElement Prvek() => new()
    {
        X = 0, Y = 0, Width = 120,
        DataRowHeight = 5f,
        ShowNzRow = false,
        ShowPlatnostRow = false,
        ShowGroupHeaders = false,
    };

    /// <summary>Dvojice stojí těsně vedle sebe: prázdný sloupec dorovnání, který by padl mezi původní
    /// a novou podobu spoje, jde až za ni (rozhodnutí Jan 16. 10., vzor z CIS JŘ).</summary>
    [Fact]
    public void PrazdneSloupceNestojiUvnitrDvojice()
    {
        var prvek = Prvek();
        prvek.PadEmptyColumns = true;
        var bitmapa = Vykresli(prvek, Linka(sVerzemi: true));
        int cervenaVpravo = KrajBarvy(bitmapa, prvek.RozdilPuvodniBackColor, vpravo: true);
        int zelenaVlevo = KrajBarvy(bitmapa, prvek.RozdilNovaBackColor, vpravo: false);

        Assert.True(zelenaVlevo - cervenaVpravo < MmToPx,
            $"původní končí {cervenaVpravo} px, nová začíná {zelenaVlevo} px - mezi nimi je prázdný sloupec");
    }

    [Theory]
    [InlineData(false, new[] { 1, 0, 0, 1 }, new[] { 0, 1, 0, 1 })]   // za spojem: z původní za novou
    [InlineData(true, new[] { 0, 1, 0, 0 }, new[] { 1, 0, 0, 0 })]    // zrcadlená strana: před spojem
    public void PrazdnyVeDvojiciJdeZaNi(bool predSpojem, int[] pocty, int[] cekane)
    {
        var spoje = new List<LineTableElement.LineTripData>
        {
            new() { CisloSpoje = "21", Verze = "puvodni" },
            new() { CisloSpoje = "21", Verze = "nova" },
            new() { CisloSpoje = "23" },
            new() { CisloSpoje = "25" },
        };
        Assert.Equal(cekane, LineTableElement.BezPrazdnychVeDvojici(pocty, spoje, predSpojem));
    }

    private static SKBitmap Vykresli(LineTableElement prvek, DataContext data)
    {
        float vyska = prvek.CalculateHeightFromData(data);
        var bitmap = new SKBitmap((int)(120 * MmToPx), (int)((vyska + 2) * MmToPx), SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        prvek.Render(new RenderContext(canvas, MmToPx, data));
        return bitmap;
    }

    /// <summary>Nejpravější (nebo nejlevější) sloupec pixelů s barvou <paramref name="hex"/>.</summary>
    private static int KrajBarvy(SKBitmap bitmap, string hex, bool vpravo)
    {
        var barva = SKColor.Parse(hex);
        int kraj = vpravo ? -1 : int.MaxValue;
        for (int x = 0; x < bitmap.Width; x++)
            for (int y = 0; y < bitmap.Height; y++)
            {
                var p = bitmap.GetPixel(x, y);
                if (Math.Abs(p.Red - barva.Red) < 8 && Math.Abs(p.Green - barva.Green) < 8 && Math.Abs(p.Blue - barva.Blue) < 8)
                    kraj = vpravo ? Math.Max(kraj, x) : Math.Min(kraj, x);
            }
        return kraj;
    }

    /// <summary>Linka s jedním spojem, jehož pole jsou doplněná o <paramref name="pole"/> - značky
    /// spoje se stejnými časy, jak je pošle server (`B192c`).</summary>
    private static DataContext SpojSeZnackami(params (string, object?)[] pole) => RenderSnapshot.Data(
        ("ZASTAVKY",
        [
            RenderSnapshot.Row(("ID", "1"), ("NAZEV_ZASTAVKY", "Strakonice"), ("PORADI", "1")),
            RenderSnapshot.Row(("ID", "2"), ("NAZEV_ZASTAVKY", "Blatná"), ("PORADI", "2")),
        ]),
        ("SPOJE",
        [
            RenderSnapshot.Row([("CISLO_SPOJE", "21"), ("SKUPINA", "1"), ("SMER", "T"),
                ("CASY", System.Text.Json.Nodes.JsonNode.Parse("[\"14:15\",\"14:20\"]")), .. pole]),
        ]),
        ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", "Pracovní dny"), ("PORADI", "1"))]));

    private static LineTableElement PrvekSeZnackami()
    {
        var prvek = Prvek();
        prvek.ShowNzRow = true;
        prvek.ShowPlatnostRow = true;
        return prvek;
    }

    /// <summary>Negativní značka, kterou spoj měl jen v původní verzi, je ve sloupci v barvě původní verze.</summary>
    [Fact]
    public void NegativniZnackaPuvodniVerzeMaSvouBarvu()
    {
        var prvek = PrvekSeZnackami();
        var data = SpojSeZnackami(("NZ", ""), ("NZ_PUVODNI", "31"));

        Assert.True(Pixelu(prvek, data, prvek.RozdilPuvodniZnackaColor) > 30, "značka původní verze nemá barvu");
        Assert.Equal(0, Pixelu(prvek, data, prvek.RozdilNovaZnackaColor));
    }

    /// <summary>Změněná negativní značka (38 → 37) je ve sloupci dvakrát pod sebou, původní nad novou -
    /// sloupec se kvůli ní nerozšíří, přibude řádek.</summary>
    [Fact]
    public void ZmenenaNegativniZnackaJePodSebou()
    {
        var prvek = PrvekSeZnackami();
        var obe = SpojSeZnackami(("NZ", "37"), ("NZ_VERZE", "nova"), ("NZ_PUVODNI", "38"));
        var jen = SpojSeZnackami(("NZ", "37"));

        Assert.True(Pixelu(prvek, obe, prvek.RozdilPuvodniZnackaColor) > 30, "původní značka chybí");
        Assert.True(Pixelu(prvek, obe, prvek.RozdilNovaZnackaColor) > 30, "nová značka nemá barvu nové verze");
        Assert.True(prvek.CalculateHeightFromData(obe) > prvek.CalculateHeightFromData(jen) + 1f,
            "druhá značka nepřidala řádek");
    }

    /// <summary>Ve dvou řádcích negativních značek je nahoře původní a dole aktuální značka - i u spoje,
    /// který má jen tu aktuální, takže aktuální značky všech spojů stojí v jedné výšce (Jan 16. 10.).</summary>
    [Fact]
    public void AktualniNegativniZnackyJsouVJedneVysce()
    {
        var obe = new LineTableElement.LineTripData { Nz = "37", NzVerze = "nova", NzPuvodni = "38" };
        var jenNova = new LineTableElement.LineTripData { Nz = "21", NzVerze = "nova" };
        var beze = new LineTableElement.LineTripData { Nz = "46" };
        var jenPuvodni = new LineTableElement.LineTripData { NzPuvodni = "23" };

        Assert.Equal(("38", "puvodni"), LineTableElement.NzVRadku(obe, 0, 2));
        Assert.Equal(("37", "nova"), LineTableElement.NzVRadku(obe, 1, 2));
        Assert.Null(LineTableElement.NzVRadku(jenNova, 0, 2));
        Assert.Equal(("21", "nova"), LineTableElement.NzVRadku(jenNova, 1, 2));
        Assert.Equal(("46", ""), LineTableElement.NzVRadku(beze, 1, 2));
        Assert.Equal(("23", "puvodni"), LineTableElement.NzVRadku(jenPuvodni, 0, 2));
        // Jeden řádek: spoj má jedinou značku a ta jde do něj, ať je která chce.
        Assert.Equal(("23", "puvodni"), LineTableElement.NzVRadku(jenPuvodni, 0, 1));
        Assert.Equal(("46", ""), LineTableElement.NzVRadku(beze, 0, 1));
    }

    /// <summary>Značka platnosti jen v nové verzi má ve sloupci barvu nové verze, ostatní zůstávají.</summary>
    [Fact]
    public void ZnackaPlatnostiNoveVerzeMaSvouBarvu()
    {
        var prvek = PrvekSeZnackami();
        var data = SpojSeZnackami(
            ("PLATNOST", System.Text.Json.Nodes.JsonNode.Parse("[\"X\",\"R\"]")),
            ("PLATNOST_VERZE", System.Text.Json.Nodes.JsonNode.Parse("[\"\",\"nova\"]")));

        Assert.True(Pixelu(prvek, data, prvek.RozdilNovaZnackaColor) > 10, "značka nové verze nemá barvu");
        Assert.Equal(0, Pixelu(prvek, data, prvek.RozdilPuvodniZnackaColor));
    }

    [Theory]
    [InlineData(nameof(LineTableElement.RozdilPuvodniZnackaColor))]
    [InlineData(nameof(LineTableElement.RozdilNovaZnackaColor))]
    public void BarvyZnacekJenVRozdiloveAgende(string klic)
    {
        Assert.True(LineTableElement.PoleFormulareViditelne(klic, true));
        Assert.False(LineTableElement.PoleFormulareViditelne(klic, false));
    }

    [Fact]
    public void KopiePrvkuNeseBarvyZnacek()
    {
        var prvek = Prvek();
        prvek.RozdilPuvodniZnackaColor = "#FF112233";
        prvek.RozdilNovaZnackaColor = "#FF445566";
        var kopie = (LineTableElement)prvek.Clone();

        Assert.Equal("#FF112233", kopie.RozdilPuvodniZnackaColor);
        Assert.Equal("#FF445566", kopie.RozdilNovaZnackaColor);
    }

    /// <summary>Zastávka, kterou má jen nová verze trasy, má barvu nové verze celý řádek - i přes
    /// zvýraznění lichého Tč (`B192d`, vzor ARRIVA Ludvíkov,pošta).</summary>
    [Fact]
    public void ZastavkaJenVNoveVerziMaBarevnyRadek()
    {
        static DataContext Trasa(string? verze) => RenderSnapshot.Data(
            ("ZASTAVKY",
            [
                RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Strakonice"), ("PORADI", "1"), ("TC", "2")),
                RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Kejnice,rozc."), ("PORADI", "2"), ("TC", "3"), ("VERZE", verze)),
                RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Blatná"), ("PORADI", "3"), ("TC", "4")),
            ]),
            ("SPOJE",
            [
                RenderSnapshot.Row(("CISLO_SPOJE", "21"), ("SKUPINA", "1"), ("SMER", "T"),
                    ("CASY", System.Text.Json.Nodes.JsonNode.Parse("[\"14:15\",\"14:18\",\"14:20\"]"))),
            ]),
            ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", "Pracovní dny"), ("PORADI", "1"))]));

        var prvek = Prvek();
        prvek.HighlightOddTc = true;
        // Pruh aspoň 40 mm široký a 4 mm vysoký - víc, než kolik zabere jedna buňka.
        int radekPx = (int)(40 * MmToPx * 4f * MmToPx);

        int nova = Pixelu(prvek, Trasa("nova"), prvek.RozdilNovaBackColor);
        Assert.True(nova > radekPx, $"řádek nové zastávky nemá barvu verze ({nova} px, čekáno přes {radekPx})");
        Assert.True(Pixelu(prvek, Trasa("puvodni"), prvek.RozdilPuvodniBackColor) > radekPx, "řádek zrušené zastávky nemá barvu verze");
        Assert.Equal(0, Pixelu(prvek, Trasa(null), prvek.RozdilNovaBackColor));
    }

    /// <summary>Dvě zastávky obou verzí; u druhé se hodnota (Tč nebo pásmo) změnila ze 2 na 3.
    /// Bez <paramref name="sPuvodnimi"/> je server nepošle, jako když se verze neliší.</summary>
    private static DataContext PuvodniHodnoty(string pole, bool sPuvodnimi, string druhaPuvodni = "2") => RenderSnapshot.Data(
        ("ZASTAVKY",
        [
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Strakonice"), ("PORADI", "1"), (pole, "1"),
                (pole + "_PUVODNI", sPuvodnimi ? "1" : null)),
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Blatná"), ("PORADI", "2"), (pole, "3"),
                (pole + "_PUVODNI", sPuvodnimi ? druhaPuvodni : null)),
        ]),
        ("SPOJE",
        [
            RenderSnapshot.Row(("CISLO_SPOJE", "21"), ("SKUPINA", "1"), ("SMER", "T"),
                ("CASY", System.Text.Json.Nodes.JsonNode.Parse("[\"14:15\",\"14:20\"]"))),
        ]),
        ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", "Pracovní dny"), ("PORADI", "1"))]));

    private static LineTableElement PrvekSeSloupci(params string[] klice)
    {
        var prvek = Prvek();
        prvek.Columns = [.. klice.Select(k => new StopColumn { Key = k, Width = 6f }),
            new StopColumn { Key = ColumnKeys.StopName, Width = 30f },
            new StopColumn { Key = LineTableElement.TripKey }];
        return prvek;
    }

    /// <summary>Původní Tč a pásmo mají vlastní sloupec a buňka, ve které se verze liší, má barvu
    /// své verze - jako odlišný čas spoje (`B192e`, vzor ARRIVA: Tč za přidanou zastávkou).</summary>
    [Theory]
    [InlineData(ColumnKeys.OriginalTc, ColumnKeys.Tc, "TC")]
    [InlineData(ColumnKeys.OriginalZone, ColumnKeys.Zone, "ZONY")]
    public void OdlisnaHodnotaZastavkyMaBarvuSveVerze(string puvodniKlic, string novyKlic, string pole)
    {
        var prvek = PrvekSeSloupci(puvodniKlic, novyKlic);
        var data = PuvodniHodnoty(pole, sPuvodnimi: true);

        // Buňka 6 × 5 mm je přes 400 px; podbarvená má být jen ta jedna v každém sloupci.
        int cervena = Pixelu(prvek, data, prvek.RozdilPuvodniBackColor);
        int zelena = Pixelu(prvek, data, prvek.RozdilNovaBackColor);
        Assert.InRange(cervena, 200, 700);
        Assert.InRange(zelena, 200, 700);
    }

    [Fact]
    public void StejnaHodnotaZastavkySeNepodbarvi()
    {
        var prvek = PrvekSeSloupci(ColumnKeys.OriginalTc, ColumnKeys.Tc);
        var data = PuvodniHodnoty("TC", sPuvodnimi: true, druhaPuvodni: "3");

        Assert.Equal(0, Pixelu(prvek, data, prvek.RozdilPuvodniBackColor));
        Assert.Equal(0, Pixelu(prvek, data, prvek.RozdilNovaBackColor));
    }

    /// <summary>Když se verze v Tč neliší, server původní Tč nepošle a sloupec se netiskne -
    /// tabulka vypadá stejně, jako by ho šablona neměla.</summary>
    [Fact]
    public void BezPuvodnichHodnotSloupecNeni()
    {
        var data = PuvodniHodnoty("TC", sPuvodnimi: false);
        using var sPuvodnim = Vykresli(PrvekSeSloupci(ColumnKeys.OriginalTc, ColumnKeys.Tc), data);
        using var bez = Vykresli(PrvekSeSloupci(ColumnKeys.Tc), data);

        Assert.Equal(bez.Bytes, sPuvodnim.Bytes);
    }

    /// <summary>Kolik pixelů má (zhruba) barvu <paramref name="hex"/>.</summary>
    private static int Pixelu(LineTableElement prvek, DataContext data, string hex)
    {
        var barva = SKColor.Parse(hex);
        float vyska = prvek.CalculateHeightFromData(data);
        using var bitmap = new SKBitmap((int)(120 * MmToPx), (int)((vyska + 2) * MmToPx), SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            prvek.Render(new RenderContext(canvas, MmToPx, data));
        }
        int n = 0;
        for (int x = 0; x < bitmap.Width; x++)
            for (int y = 0; y < bitmap.Height; y++)
            {
                var p = bitmap.GetPixel(x, y);
                if (Math.Abs(p.Red - barva.Red) < 8 && Math.Abs(p.Green - barva.Green) < 8 && Math.Abs(p.Blue - barva.Blue) < 8) n++;
            }
        return n;
    }

    [Fact]
    public void OdlisnaBunkaMaBarvuSveVerze()
    {
        var prvek = Prvek();
        var data = Linka(sVerzemi: true);

        // Jedna buňka (sloupec spoje × řádek 5 mm) je stovky pixelů; obě verze ji mají.
        Assert.True(Pixelu(prvek, data, prvek.RozdilPuvodniBackColor) > 200, "původní verze není podbarvená");
        Assert.True(Pixelu(prvek, data, prvek.RozdilNovaBackColor) > 200, "nová verze není podbarvená");
    }

    /// <summary>Číslo spoje má barvu své verze, i když se žádný čas neliší - u dvojice musí být
    /// vidět, která je původní a která nová (vzor z CIS JŘ, `B192a`).</summary>
    [Fact]
    public void CisloSpojeMaBarvuSveVerze()
    {
        var prvek = Prvek();
        var data = Linka(sVerzemi: true, rozdily: "[0,0]");

        Assert.True(Pixelu(prvek, data, prvek.RozdilPuvodniBackColor) > 200, "číslo původní verze není podbarvené");
        Assert.True(Pixelu(prvek, data, prvek.RozdilNovaBackColor) > 200, "číslo nové verze není podbarvené");
    }

    /// <summary>Barvu verze má celé záhlaví sloupce, tedy i řádek značek platnosti pod číslem spoje.</summary>
    [Fact]
    public void ZnackyPlatnostiMajiBarvuVerze()
    {
        var bezZnacek = Prvek();
        var seZnackami = Prvek();
        seZnackami.ShowPlatnostRow = true;
        var data = Linka(sVerzemi: true, rozdily: "[0,0]", platnost: "X");

        Assert.True(Pixelu(seZnackami, data, seZnackami.RozdilNovaBackColor)
            > Pixelu(bezZnacek, data, bezZnacek.RozdilNovaBackColor) + 100,
            "řádek značek platnosti nemá barvu verze");
    }

    /// <summary>Prázdný sloupec dorovnání za spojem nepatří spoji - podbarvení rozdílu nepřebírá
    /// (nález Jan 9. 10.: zelený prázdný sloupec za novou verzí spoje).</summary>
    [Fact]
    public void PrazdnySloupecDorovnaniSeNepodbarvi()
    {
        // Nová verze spoje je poslední sloupec a za ní jsou prázdné sloupce až k okraji tabulky:
        // zelená musí skončit u spoje, ne u okraje.
        var prvek = Prvek();
        prvek.PadEmptyColumns = true;
        var (zelenaVpravo, tabulkaVpravo) = PraveOkraje(prvek, Linka(sVerzemi: true), prvek.RozdilNovaBackColor);

        Assert.True(tabulkaVpravo - zelenaVpravo > 3 * MmToPx,
            $"zelená končí {zelenaVpravo} px, tabulka {tabulkaVpravo} px - prázdný sloupec je podbarvený");
    }

    /// <summary>Nejpravější pixel barvy <paramref name="hex"/> a nejpravější tmavý pixel (čáry, text).</summary>
    private static (int Barva, int Tabulka) PraveOkraje(LineTableElement prvek, DataContext data, string hex)
    {
        var barva = SKColor.Parse(hex);
        float vyska = prvek.CalculateHeightFromData(data);
        using var bitmap = new SKBitmap((int)(120 * MmToPx), (int)((vyska + 2) * MmToPx), SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            prvek.Render(new RenderContext(canvas, MmToPx, data));
        }
        int barvaX = -1, tabulkaX = -1;
        for (int x = 0; x < bitmap.Width; x++)
            for (int y = 0; y < bitmap.Height; y++)
            {
                var p = bitmap.GetPixel(x, y);
                if (Math.Abs(p.Red - barva.Red) < 8 && Math.Abs(p.Green - barva.Green) < 8 && Math.Abs(p.Blue - barva.Blue) < 8) barvaX = x;
                else if (p.Red < 230 && p.Green < 230 && p.Blue < 230) tabulkaX = Math.Max(tabulkaX, x);
            }
        return (barvaX, tabulkaX);
    }

    [Fact]
    public void BezVerziSeNicNepodbarvi()
    {
        var prvek = Prvek();
        var data = Linka(sVerzemi: false);

        Assert.Equal(0, Pixelu(prvek, data, prvek.RozdilPuvodniBackColor));
        Assert.Equal(0, Pixelu(prvek, data, prvek.RozdilNovaBackColor));
    }

    [Fact]
    public void BarvaJeZeSablony()
    {
        var prvek = Prvek();
        prvek.RozdilNovaBackColor = "#FF4060FF";

        Assert.True(Pixelu(prvek, Linka(sVerzemi: true), "#FF4060FF") > 200);
    }

    [Theory]
    [InlineData(true, nameof(LineTableElement.RozdilNovaBackColor), true)]
    [InlineData(false, nameof(LineTableElement.RozdilNovaBackColor), false)]
    [InlineData(false, nameof(LineTableElement.RozdilPuvodniBackColor), false)]
    [InlineData(false, nameof(LineTableElement.OddTcBackColor), true)]
    public void BarvyRozdiluJenVRozdiloveAgende(bool rozdilovy, string klic, bool viditelne) =>
        Assert.Equal(viditelne, LineTableElement.PoleFormulareViditelne(klic, rozdilovy));

    [Fact]
    public void KopiePrvkuNeseBarvy()
    {
        var prvek = Prvek();
        prvek.RozdilPuvodniBackColor = "#FF112233";

        Assert.Equal("#FF112233", ((LineTableElement)prvek.Clone()).RozdilPuvodniBackColor);
    }

    /// <summary>Nezveřejněné agendy ukazuje výběr jen vývojáři - klíčem v lokálním configu.</summary>
    [Fact]
    public void SkryteAgendyJenLokalnimKlicem()
    {
        var target = new AppSettings();
        var local = new AppSettings();
        AppSettings.MergeLocal(target, local);
        Assert.False(target.Dev.HiddenAgendas);

        local.Dev.HiddenAgendas = true;
        AppSettings.MergeLocal(target, local);
        Assert.True(target.Dev.HiddenAgendas);
    }
}
