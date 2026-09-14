using SkiaSharp;
using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Rozřezání linkové tabulky do bloků na úrovni prvku - tedy s <b>opravdovými</b> šířkami sloupců,
/// ne s hodnotami dosazenými do vzorce.
/// </summary>
/// <remarks>
/// <para>Dřív k tomu existoval i test <b>dělicího vzorce samotného</b> (`SloupcoveBlokyTests` nad
/// `TableLayout.RozdelSpojeDoBloku`) a byl celou dobu zelený i ve chvíli, kdy tabulka v návrháři
/// vycházela nečitelná: vzorec dostával špatné vstupy, protože popisné sloupce se měřily při plném
/// počtu spojů, tedy zmáčknuté na minimum. Ten vzorec už nikdo nevolal a 4. 9. byl odstraněn
/// i s testy - dělí `LineTableElement.SloupcoveBloky`, a to se ověřuje tady, nad daty.</para>
/// <para>Proto se tady netestuje vzorec, ale <b>výsledek</b>: co vyjde z prvku nad daty.</para>
/// </remarks>
public class LineTableBlokyTests
{
    /// <summary>Linka s tolika spoji, že se do šířky nevejdou - právě na téhle se to lámalo.</summary>
    /// <summary>Sdíleno se stránkovacím testem - táž linka, dva pohledy na ni.</summary>
    internal static DataContext SirokaLinkaData(int pocetSpoju, int pocetZastavek = 3) =>
        SirokaLinka(pocetSpoju, pocetZastavek);

    private static DataContext SirokaLinka(int pocetSpoju, int pocetZastavek = 3)
    {
        // ⚠️ Přípojné řádky jen u **některých** spojů: patří ke spoji, takže každý blok jich má
        // jiný počet. Bez nich by fixtura neuměla ukázat rozdíl mezi „počítat ze všech spojů"
        // a „počítat ze spojů bloku" - a přesně na tom se výpočet rozešel s kreslením.
        var spoje = Enumerable.Range(1, pocetSpoju)
            .Select(i =>
            {
                var pole = new List<(string, object?)>
                {
                    ("CISLO_SPOJE", (i * 2).ToString()),
                    ("SKUPINA", i <= pocetSpoju * 2 / 3 ? "1" : "2")
                };
                if (i % 5 == 0)
                    pole.Add(("PRIPOJE", RenderSnapshot.Nested(
                        [[("TYP", "vycka"), ("PORADI", "2"), ("RADEK", "1"),
                          ("CAS", "12:30"), ("LINKA", "330520"), ("SPOJ", "7"), ("TEXT", "Kaplice")]])));
                return RenderSnapshot.Row([.. pole]);
            })
            .ToArray();
        var zastavky = Enumerable.Range(1, pocetZastavek)
            .Select(i => RenderSnapshot.Row(("ID", i.ToString()),
                                            ("NAZEV_ZASTAVKY", $"Zastávka {i}, rozcestí"),
                                            ("PORADI", i.ToString())))
            .ToArray();
        return RenderSnapshot.Data(
            ("ZASTAVKY", zastavky),
            ("SPOJE", spoje),
            ("SKUPINY_DNU",
            [
                RenderSnapshot.Row(("ID", "1"), ("NADPIS", "Pracovní dny"), ("PORADI", "1")),
                RenderSnapshot.Row(("ID", "2"), ("NADPIS", "Sobota, neděle"), ("PORADI", "2")),
            ]));
    }

    /// <summary>
    /// Obousměrná linka: spoje dvou směrů, jak je posílá server - nejdřív celý směr „T", pak „Z".
    /// </summary>
    /// <remarks>
    /// ⚠️ Musí to být **vlastní fixtura**, ne parametr té jednosměrné: u obousměrné sestavy zabere
    /// jeden slot dělení <b>sloupec na každé straně</b> zastávek, takže se do šířky musí vejít
    /// dvojnásobek. Právě tenhle případ šířku bloku nehlídal.
    /// </remarks>
    private static DataContext ObousmernaLinka(int spojuNaSmer, int pocetZastavek = 3)
    {
        var spoje = new List<(string, object?)[]>();
        foreach (var smer in new[] { "T", "Z" })
            for (int i = 1; i <= spojuNaSmer; i++)
                spoje.Add([("CISLO_SPOJE", (smer == "T" ? i * 2 - 1 : i * 2).ToString()),
                           ("SMER", smer),
                           ("SKUPINA", i <= spojuNaSmer * 2 / 3 ? "1" : "2")]);
        var zastavky = Enumerable.Range(1, pocetZastavek)
            .Select(i => RenderSnapshot.Row(("ID", i.ToString()),
                                            ("NAZEV_ZASTAVKY", $"Zastávka {i}, rozcestí"),
                                            ("PORADI", i.ToString())))
            .ToArray();
        return RenderSnapshot.Data(
            ("ZASTAVKY", zastavky),
            ("SPOJE", [.. spoje.Select(r => RenderSnapshot.Row(r))]),
            ("SKUPINY_DNU",
            [
                RenderSnapshot.Row(("ID", "1"), ("NADPIS", "Pracovní dny"), ("PORADI", "1")),
                RenderSnapshot.Row(("ID", "2"), ("NADPIS", "Sobota, neděle"), ("PORADI", "2")),
            ]));
    }

    /// <summary>
    /// ⚠️ Táž podmínka jako u jednosměrné linky, ale pro **obousměrnou** - a ta nebyla hlídaná:
    /// šířka se měřila pro počet slotů, kdežto kreslí se sloupec na každé straně zastávek. Blok
    /// tak mohl přetéct za hranu prvku a v tisku se odříznout, aniž by o tom test věděl
    /// (nález při hledání chybějících spojů na `CB_GWBUS 0320144`, 4. 9.).
    /// </summary>
    [Theory]
    [InlineData(9)]
    [InlineData(43)]
    public void KazdyBlokSeVejdeDoSirky_ObousmernaLinka(int spojuNaSmer)
    {
        var prvek = Prvek();

        var bloky = prvek.Rozvrzeni(ObousmernaLinka(spojuNaSmer));

        Assert.NotEmpty(bloky);
        Assert.All(bloky, b => Assert.True(b.SirkaMm <= prvek.Width + 0.01f,
            $"blok od slotu {b.Od} ({b.Pocet} slotů = {b.Pocet * 2} sloupců) je {b.SirkaMm:0.0} mm, "
            + $"prvek má {prvek.Width} mm"));
    }

    /// <summary>
    /// Kolik spojů se do prvku vejde na jeden blok - největší počet, který se ještě nerozdělí.
    /// </summary>
    /// <remarks>
    /// ⚠️ Nejde to odečíst z prvního bloku rozdělené linky: od 14. 9. se spoje dělí mezi části
    /// rovným dílem, takže části bývají menší než kapacita (60 spojů při kapacitě 17 vyjde
    /// 15 + 15 + 15 + 15, ne 17 + 17 + 17 + 9).
    /// </remarks>
    private static int KapacitaBloku(LineTableElement prvek, Func<int, DataContext> linka)
    {
        int n = 1;
        while (n < 500 && prvek.Rozvrzeni(linka(n + 1)).Count == 1) n++;
        return n;
    }

    /// <summary>
    /// Jednosměrná linka se skupinami dnů, kde oddělení nestojí blok navíc: bloky proto **končí
    /// na hranici skupiny**, ne po plných devíti sloupcích. Bez toho jsme se od starého tisku
    /// lišili rozřezáním u každé linky, kde by oddělení bylo zadarmo.
    /// </summary>
    [Fact]
    public void JednosmernaLinka_BlokyKonciNaHraniciSkupiny()
    {
        var prvek = Prvek();
        // Kolik spojů se vejde na blok, řídí šířka prvku a písmo. Aby test nezávisel na geometrii,
        // zjistí se to nejdřív na lince s jedinou skupinou a skupiny se pak udělají právě tak
        // velké, aby jejich oddělení nestálo blok navíc.
        int naBlok = KapacitaBloku(prvek, n => LinkaSeSkupinami(n));
        // Velikosti `n+2, n, n`: oddělené vyjde na 2+1+1 bloků a úsporné na tolikéž, takže je
        // oddělení zadarmo - a **liší se**, protože řez podle šířky by padl doprostřed skupin.
        // Kdyby se velikosti daly jako násobky bloku, oba řezy by padly na stejná místa a test
        // by neověřoval nic (odhaleno mutací 6. 9.).
        var data = LinkaSeSkupinami(naBlok + 2, naBlok, naBlok);

        var bloky = prvek.Rozvrzeni(data);

        var skupiny = SkupinyPerSlot(data);
        Assert.Equal(4, bloky.Count);
        // Žádný blok nesmí nést dvě skupiny - to je celý rozdíl proti řezu podle šířky, který by
        // při týchž čtyřech blocích rozsekal tyhle velikosti na 14 + 13 + 13 + 13.
        Assert.All(bloky, b => Assert.Single(skupiny.Skip(b.Od).Take(b.Pocet).Distinct()));
    }

    /// <summary>
    /// ⚠️ Obousměrná linka řez podle skupin **nedostane**: slot nese spoj z každého směru a jejich
    /// skupiny se neshodují, takže hranice skupiny není jedno místo. Starý modul tenhle případ
    /// nezná (obousměrnou tabulku, která se má řezat, zahodí a tiskne po směrech) a my ji vědomě
    /// zachováváme (rozhodnutí Jan 6. 9.).
    /// </summary>
    [Fact]
    public void ObousmernaLinka_ZustaneUspornyRez()
    {
        var prvek = Prvek();

        var bloky = prvek.Rozvrzeni(ObousmernaLinka(43));

        // Úsporný řez = všechny bloky kromě posledního plné, tedy stejně velké.
        int prvni = bloky[0].Pocet;
        Assert.True(bloky.Count > 1, "43 slotů se do 190 mm vejít nemá");
        Assert.All(bloky.Take(bloky.Count - 1), b => Assert.Equal(prvni, b.Pocet));
    }

    /// <summary>Bez pásu nadpisů skupin se neoddělují - v tisku by nebylo poznat, že tam skupina
    /// začíná, a kratší blok by vypadal jako náhodná díra v tabulce.</summary>
    [Fact]
    public void BezNadpisuSkupin_ZustaneUspornyRez()
    {
        var prvek = Prvek();
        prvek.ShowGroupHeaders = false;

        var bloky = prvek.Rozvrzeni(LinkaSeSkupinami(25, 9, 9));

        int prvni = bloky[0].Pocet;
        Assert.All(bloky.Take(bloky.Count - 1), b => Assert.Equal(prvni, b.Pocet));
    }

    /// <summary>Jednosměrná linka se zadanými velikostmi skupin dnů - spoje jednoho směru
    /// (bez `SMER`), takže se řeže jedna osa sloupců.</summary>
    private static DataContext LinkaSeSkupinami(params int[] velikostiSkupin)
    {
        var spoje = new List<(string, object?)[]>();
        int cislo = 0;
        for (int g = 0; g < velikostiSkupin.Length; g++)
            for (int i = 0; i < velikostiSkupin[g]; i++)
                spoje.Add([("CISLO_SPOJE", (++cislo * 2).ToString()), ("SKUPINA", (g + 1).ToString())]);

        var zastavky = Enumerable.Range(1, 3)
            .Select(i => RenderSnapshot.Row(("ID", i.ToString()),
                                            ("NAZEV_ZASTAVKY", $"Zastávka {i}, rozcestí"),
                                            ("PORADI", i.ToString())))
            .ToArray();
        var skupiny = Enumerable.Range(1, velikostiSkupin.Length)
            .Select(g => RenderSnapshot.Row(("ID", g.ToString()),
                                            ("NADPIS", $"Skupina {g}"),
                                            ("PORADI", g.ToString())))
            .ToArray();
        return RenderSnapshot.Data(
            ("ZASTAVKY", zastavky),
            ("SPOJE", [.. spoje.Select(r => RenderSnapshot.Row(r))]),
            ("SKUPINY_DNU", skupiny));
    }

    /// <summary>Skupina dnů každého slotu v pořadí, v jakém přišly spoje.</summary>
    private static List<string> SkupinyPerSlot(DataContext data) =>
        [.. data.GetRows("SPOJE").Select(r => r.GetString("SKUPINA"))];

    private static LineTableElement Prvek() => ElementRenderFixtures.LineTable();

    /// <summary>
    /// ⚠️ Podstata celé opravy: blok se musí do šířky prvku <b>vejít</b>. Dokud se počet spojů
    /// odvozoval z popisných sloupců změřených pro jiný počet, první blok šířku překročil
    /// a časy v něm byly přes sebe.
    /// </summary>
    [Theory]
    [InlineData(12)]
    [InlineData(41)]
    [InlineData(120)]
    public void KazdyBlokSeVejdeDoSirky(int spoju)
    {
        var prvek = Prvek();

        var bloky = prvek.Rozvrzeni(SirokaLinka(spoju));

        Assert.NotEmpty(bloky);
        Assert.All(bloky, b => Assert.True(b.SirkaMm <= prvek.Width + 0.01f,
            $"blok od spoje {b.Od} ({b.Pocet} spojů) je {b.SirkaMm:0.0} mm, prvek má {prvek.Width} mm"));
    }

    /// <summary>Bloky vycházejí stejně velké - smějí se lišit nejvýš o jeden sloupec. Chyba se
    /// poznala právě takhle: 36 spojů v prvním bloku a 5 ve druhém.</summary>
    /// <remarks>Od 14. 9. je to podmínka na **všechny** bloky, ne jen na ty kromě posledního:
    /// spoje se mezi části dělí rovným dílem, takže poslední část už nezůstává skoro prázdná.</remarks>
    [Fact]
    public void BlokyJsouStejneVelke()
    {
        var bloky = Prvek().Rozvrzeni(SirokaLinka(41));

        Assert.True(bloky.Count > 1, "41 spojů se do 190 mm vejít nemá");
        Assert.True(bloky.Max(b => b.Pocet) - bloky.Min(b => b.Pocet) <= 1,
            $"části vyšly {string.Join(" + ", bloky.Select(b => b.Pocet))}");
    }

    /// <summary>Žádný spoj se dělením neztratí ani nezopakuje.</summary>
    [Fact]
    public void VsechnySpojeJsouPraveJednou()
    {
        var bloky = Prvek().Rozvrzeni(SirokaLinka(41));

        Assert.Equal(41, bloky.Sum(b => b.Pocet));
        int ocekavany = 0;
        foreach (var b in bloky) { Assert.Equal(ocekavany, b.Od); ocekavany += b.Pocet; }
    }

    /// <summary>Vejde-li se linka celá, blok je jeden - dělení se nesmí plést do běžného případu.</summary>
    [Fact]
    public void UzkaLinka_JedenBlok() => Assert.Single(Prvek().Rozvrzeni(SirokaLinka(4)));

    /// <summary>Výška roste s počtem bloků - stránkování musí vědět, že tabulka je pod sebou
    /// několikrát. Kdyby se počítala jen z jednoho bloku, zbytek by se ořízl.</summary>
    [Fact]
    public void VyskaRosteSPoctemBloku()
    {
        var prvek = Prvek();
        prvek.Height = null;   // dynamická výška, jinak se počítá z pevné hodnoty
        var uzka = SirokaLinka(4);
        var siroka = SirokaLinka(41);

        float vyskaJednoho = prvek.CalculateHeightFromData(uzka);
        int bloku = prvek.Rozvrzeni(siroka).Count;
        float vyskaVice = prvek.CalculateHeightFromData(siroka);

        Assert.True(bloku > 1);
        Assert.True(vyskaVice > vyskaJednoho * (bloku - 1),
            $"{bloku} bloků má být zhruba {bloku}× vyšší než jeden ({vyskaVice:0.0} vs {vyskaJednoho:0.0} mm)");
    }

    /// <summary>
    /// ⚠️ Jádro věci: <b>na sloupec spoje musí zbýt tolik, aby se do něj vešel čas</b>. Tohle je ta
    /// podmínka, která v návrháři chyběla - dno šířky bylo pevné 3 mm a čas se do 3 mm nevejde
    /// v žádném písmu. Dělení se přitom řídilo právě tím dnem, takže do bloku nasypalo tolik spojů,
    /// kolik se jich při nečitelné šířce vešlo, a tabulka vyšla jako slitá šeď (nahlásil Jan 25. 8.).
    /// </summary>
    [Fact]
    public void SloupecSpojeUnesePlnyCas()
    {
        var prvek = Prvek();
        float potreba = SirkaCasuMm(prvek.DataStyle.FontSize ?? 9f);

        var bloky = prvek.Rozvrzeni(SirokaLinka(60));

        Assert.All(bloky, b => Assert.True(b.SloupecSpojeMm >= potreba,
            $"sloupec spoje má {b.SloupecSpojeMm:0.00} mm, čas „00:00\" potřebuje {potreba:0.00} mm"));
    }

    /// <summary>Šířka času v milimetrech - z pohledu toho, kdo se na tisk dívá, ne z výpočtu prvku.</summary>
    private static float SirkaCasuMm(float fontPt)
    {
        using var face = SKTypeface.FromFamilyName("Arial");
        using var font = new SKFont(face, fontPt);            // velikost v pt → míra vyjde v pt
        return font.MeasureText("00:00") * 0.3528f;
    }


    /// <summary>
    /// ⚠️ Sloupce spojů jsou ve <b>všech blocích stejně široké</b>. Podle vlastního počtu spojů
    /// vycházel poslední blok se čtyřmi spoji se sloupci 58,8 mm proti 6,4 mm v prvním (změřeno
    /// 25. 8.) - čas uprostřed šesticentimetrového pole a dva bloky, které vedle sebe nevypadají
    /// jako táž tabulka. Zbylé místo vpravo zůstane prázdné.
    /// </summary>
    [Fact]
    public void SloupceSpojuJsouVeVsechBlocichStejne()
    {
        var prvek = Prvek();
        prvek.Width = 277;

        var bloky = prvek.Rozvrzeni(SirokaLinkaData(90, pocetZastavek: 28));

        Assert.True(bloky.Count > 1, "90 spojů na 277 mm se má rozřezat");
        float prvni = bloky[0].SloupecSpojeMm;
        Assert.All(bloky, b => Assert.Equal(prvni, b.SloupecSpojeMm, 2));
    }

    /// <summary>Neúplný blok proto tabulku nevyplní až k pravému okraji - a je to tak správně.
    /// Dorovnání prázdnými sloupci je samostatné přání zákazníka (#8372), ne dnešní chování.</summary>
    [Fact]
    public void NeuplnyBlokNechaMistoVpravo()
    {
        var prvek = Prvek();
        prvek.Width = 277;
        // ⚠️ Neúplný blok musí vzniknout, jinak test nic neměří - a od 14. 9. se spoje dělí rovným
        // dílem, takže u většiny počtů vyjdou všechny části stejné. Hledá se proto první počet
        // spojů, u kterého poslední část kratší je.
        var bloky = Enumerable.Range(80, 20)
            .Select(n => prvek.Rozvrzeni(SirokaLinkaData(n, pocetZastavek: 28)))
            .First(b => b.Count > 1 && b[^1].Pocet < b.Max(x => x.Pocet));

        Assert.True(bloky[^1].Pocet < bloky[0].Pocet, "poslední blok má být neúplný");
        Assert.True(bloky[^1].SirkaMm < prvek.Width - 1f,
            $"neúplný blok má nechat místo vpravo, zabral {bloky[^1].SirkaMm:0.0} z {prvek.Width} mm");
    }

    /// <summary>
    /// ⚠️ Nejdůležitější podmínka celého dělení: <b>kde výpočet tvrdí, že blok začíná, tam se musí
    /// opravdu nakreslit</b>. Ověřuje se vykreslením - model se porovná s obrázkem, ne sám se sebou.
    /// </summary>
    /// <remarks>
    /// Rozešlo se to na přípojných řádcích: kreslí se jen ze spojů daného bloku, ale výška je
    /// počítala ze všech spojů linky. Bloky se tím ve výpočtu posouvaly dopředu a řez stránky padl
    /// doprostřed hlavičky dalšího bloku - na konci stránky osiřelý pás skupin dnů, na další stránce
    /// hlavička dvakrát (nahlásil Jan 25. 8.). Žádný test to nechytil, protože se výpočet i zlomy
    /// braly z téhož špatného čísla.
    /// </remarks>
    [Fact]
    public void ZacatkyBlokuOdpovidajiVykreslenemu()
    {
        const float MmToPx = 8f;
        const string Znacka = "#FFFF00FF";   // barva pozadí záhlaví, podle níž se blok v obrázku pozná

        var prvek = Prvek();
        prvek.X = 0; prvek.Y = 0; prvek.Height = null; prvek.Width = 277;
        prvek.HeaderStyle.BackgroundColor = Znacka;
        var data = SirokaLinkaData(60, pocetZastavek: 12);

        float celkem = prvek.CalculateHeightFromData(data);
        var tvrzene = new List<float> { 0f };
        tvrzene.AddRange(((INedelitelneDily)prvek).ZlomyMm(data).Where(z => z.Silny).Select(z => z.Mm));
        Assert.True(tvrzene.Count > 1, "linka se má rozřezat na víc bloků");

        var nalezene = ZacatkyZahlaviMm(prvek, data, celkem, MmToPx, SKColor.Parse(Znacka));

        Assert.Equal(tvrzene.Count, nalezene.Count);
        for (int i = 0; i < tvrzene.Count; i++)
            Assert.True(Math.Abs(tvrzene[i] - nalezene[i]) < 0.3f,
                $"blok {i + 1}: výpočet tvrdí {tvrzene[i]:0.00} mm, nakreslený je na {nalezene[i]:0.00} mm");
    }

    /// <summary>Vykreslí prvek a vrátí odsazení, kde v obrázku začínají pruhy se záhlavím - tedy
    /// kde doopravdy začínají bloky.</summary>
    private static List<float> ZacatkyZahlaviMm(LineTableElement prvek, DataContext data,
        float vyskaMm, float mmToPx, SKColor znacka)
    {
        int w = (int)(prvek.Width * mmToPx), h = (int)MathF.Ceiling(vyskaMm * mmToPx);
        using var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.White);
            prvek.Render(new RenderContext(canvas, mmToPx, data));
        }

        var zacatky = new List<float>();
        float posledniZahlaviMm = -1000f;   // ne int.MinValue: rozdíl by přetekl
        for (int y = 0; y < h; y++)
        {
            bool jeZahlavi = false;
            for (int x = 0; x < w && !jeZahlavi; x++)
                if (bmp.GetPixel(x, y) == znacka) jeZahlavi = true;
            if (!jeZahlavi) continue;

            // Hlava bloku je z několika řádků oddělených čarami mřížky, takže se v obrázku jeví
            // jako několik pruhů těsně za sebou - patří k témuž bloku. Nový blok začíná až po
            // řádcích zastávek, tedy po pořádné mezeře.
            float mm = y / mmToPx;
            if (mm - posledniZahlaviMm > 5f) zacatky.Add(mm);
            posledniZahlaviMm = mm;
        }
        return zacatky;
    }

    /// <summary>
    /// Zapnuté dorovnání doplní neúplnému bloku prázdné sloupce, takže je jeho tabulka stejně
    /// široká jako u plných bloků (#8372). Vypnuté = blok končí u posledního spoje, jako dnes.
    /// </summary>
    /// <remarks>
    /// Porovnávají se obě varianty proti sobě, ne proti absolutní šířce: úplně poslední prázdný
    /// sloupec nemá vpravo čáru, takže by měření z obrázku vycházelo o jeden sloupec kratší
    /// a test by hlídal artefakt měření místo chování.
    /// </remarks>
    [Fact]
    public void DorovnaniVyplniNeuplnyBlok()
    {
        var data = SirokaLinkaData(60, pocetZastavek: 12);

        LineTableElement Prvek(bool dorovnat)
        {
            var p = LineTableBlokyTests.Prvek();
            p.Width = 277;
            p.PadEmptyColumns = dorovnat;
            return p;
        }

        var bez = Prvek(dorovnat: false);
        var sDorovnanim = Prvek(dorovnat: true);
        var bloky = bez.Rozvrzeni(data);
        float sloupec = bloky[0].SloupecSpojeMm;
        float zacatekPosledniho = ((INedelitelneDily)bez).ZlomyMm(data)
            .Where(z => z.Silny).Select(z => z.Mm).Max();

        float sirkaBez = SirkaNejsirsihoRadkuMm(bez, data, zacatekPosledniho);
        float sirkaS = SirkaNejsirsihoRadkuMm(sDorovnanim, data, zacatekPosledniho);

        int chybi = bloky[0].Pocet - bloky[^1].Pocet;
        Assert.True(chybi > 0, "poslední blok má být neúplný, jinak test nic neměří");
        Assert.True(sirkaS - sirkaBez > (chybi - 1) * sloupec,
            $"dorovnání má přidat {chybi} sloupců po {sloupec:0.0} mm, přidalo {sirkaS - sirkaBez:0.0} mm");
        Assert.True(sirkaS >= bloky[0].SirkaMm - sloupec - 0.5f,
            $"s dorovnáním má blok sahat k plné šířce ({bloky[0].SirkaMm:0.0} mm), sahá k {sirkaS:0.0} mm");
    }

    /// <summary>Jak daleko doprava sahá nakreslený obsah <b>od zadaného odsazení dolů</b> - měří se
    /// jen poslední blok, jinak by do měření vstoupil plný blok nad ním.</summary>
    private static float SirkaNejsirsihoRadkuMm(LineTableElement prvek, DataContext data, float odMm)
    {
        const float MmToPx = 6f;
        float vyska = prvek.CalculateHeightFromData(data);
        int w = (int)MathF.Ceiling((prvek.X + prvek.Width + 10f) * MmToPx);
        int h = (int)MathF.Ceiling((prvek.Y + vyska + 5f) * MmToPx);
        using var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.White);
            prvek.Render(new RenderContext(canvas, MmToPx, data));
        }

        int yOd = (int)((prvek.Y + odMm) * MmToPx);
        for (int x = w - 1; x >= 0; x--)
            for (int y = yOd; y < h; y++)
                if (bmp.GetPixel(x, y) != SKColors.White)
                    return (x + 1) / MmToPx - prvek.X;
        return 0f;
    }
}
