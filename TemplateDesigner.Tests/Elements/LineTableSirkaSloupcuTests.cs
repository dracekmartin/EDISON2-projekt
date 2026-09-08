using System.Text.Json.Nodes;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Tests.Rendering;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Dno a strop šířky sloupce spoje u tabulky linkového JŘ.
/// </summary>
/// <remarks>
/// <para>Požadavek Jan 8. 9.: šířka měla dno (z písma a vnitřního okraje), ale <b>žádný strop</b>,
/// takže zbytek prvku se vždy rozdělil mezi sloupce. U linky s pár spoji vyšel sloupec extrémně
/// široký a čas stál uprostřed prázdného pole - lidem, kteří dnes tisknou postskriptem, se to
/// nelíbí. Starý tisk místo roztahování drží sloupce úzké a zbytek vyplní prázdnými sloupci.</para>
/// <para>Doplněním stropu vzniknou tři chování z jednoho pravidla, aniž si mezi nimi kdokoli volí:
/// hodně spojů → šířka na dně a řeže se do bloků (fungovalo už dřív); málo spojů → šířka na stropu
/// a zbytek se dorovná; mezi tím → šířka plave a vyplní prvek.</para>
/// <para>Dno jde nově zadat i <b>napřímo</b>. Přes vnitřní okraj to byla oklika - uživatel musel
/// znát vzorec „šířka času + okraj vlevo + okraj vpravo", aby výsledek odhadl.</para>
/// </remarks>
public class LineTableSirkaSloupcuTests
{
    private static PropertyDescriptor Descriptor(LineTableElement prvek, string label) =>
        prvek.GetPropertyDescriptors().Single(d => d.Label == label);

    private static float SirkaSloupce(LineTableElement prvek, DataContext data) =>
        prvek.Rozvrzeni(data)[0].SloupecSpojeMm;

    // ── Strop ───────────────────────────────────────────────────────────────

    /// <summary>
    /// ⚠️ Jádro požadavku: u linky, která se do prvku vejde, se sloupce bez stropu roztáhnou přes
    /// celý prvek. Strop je má zastavit.
    /// </summary>
    [Fact]
    public void StropZastaviRoztahovaniUMaleLinky()
    {
        var data = Data(spoju: 5);
        float bez = SirkaSloupce(Prvek(), data);
        float dno = Dno(data);
        float strop = (dno + bez) / 2f;   // mezi dnem a přirozenou šířkou, ať se opravdu uplatní

        var seStropem = Prvek();
        seStropem.NejvetsiSirkaSpojeMm = strop;

        Assert.True(bez > dno + 1f,
            $"sloupec bez stropu vyšel {bez:0.00} mm při dnu {dno:0.00} mm - fixtura neroztahuje, "
            + "test by nic neověřil");
        Assert.Equal(strop, SirkaSloupce(seStropem, data), precision: 2);
    }

    /// <summary>Prázdný strop = chování jako dosud. Existující šablony tisknou totéž.</summary>
    [Fact]
    public void BezStropuSeNicNemeni()
    {
        var data = Data(spoju: 5);

        Assert.Equal(SirkaSloupce(Prvek(), data), SirkaSloupce(
            new LineTableElement { X = 0, Y = 0, Width = 200, NejvetsiSirkaSpojeMm = null }, data),
            precision: 3);
    }

    /// <summary>
    /// Strop nesmí podlézt dno - kdo zadá nesmysl, dostane čitelný sloupec, ne oříznutý.
    /// </summary>
    [Fact]
    public void StropPodDnemSeNeuplatni()
    {
        var data = Data(spoju: 5);
        var prvek = Prvek();
        prvek.NejvetsiSirkaSpojeMm = 0.5f;

        Assert.True(SirkaSloupce(prvek, data) >= 3f,
            $"sloupec vyšel {SirkaSloupce(prvek, data):0.00} mm - strop podlezl tvrdé dno 3 mm");
    }

    /// <summary>
    /// U linky, která se dnes řeže, strop nezasáhne - sloupec tam sedí na dně a strop je nad ním.
    /// Tím je doložené, že nová volba nemění to, co dnes funguje.
    /// </summary>
    [Fact]
    public void UVelkeLinkyStropNezasahne()
    {
        var data = Data(spoju: 40);
        var uzky = new LineTableElement { X = 0, Y = 0, Width = 120 };
        float prirozena = SirkaSloupce(uzky, data);
        var uzkySeStropem = new LineTableElement
        {
            X = 0, Y = 0, Width = 120,
            // Strop nad přirozenou šířkou - u řezané tabulky je sloupec u dna, takže nemá co dělat.
            NejvetsiSirkaSpojeMm = prirozena + 10f,
        };

        Assert.Equal(prirozena, SirkaSloupce(uzkySeStropem, data), precision: 3);
        Assert.Equal(uzky.Rozvrzeni(data).Count, uzkySeStropem.Rozvrzeni(data).Count);
    }

    // ── Dorovnání volného místa ─────────────────────────────────────────────

    /// <summary>
    /// Se stropem zbyde vpravo volno i u tabulky, která se vůbec neřeže. Zapnuté dorovnání ho má
    /// rozkreslit prázdnými sloupci - to je ten postskriptový vzhled.
    /// </summary>
    [Fact]
    public void SeStropemSeVolneMistoDorovna()
    {
        var data = Data(spoju: 5);
        var prvek = Prvek();
        prvek.NejvetsiSirkaSpojeMm = 6f;
        prvek.PadEmptyColumns = true;

        // Prázdné sloupce nese kreslení, ne rozvržení - ověřuje se, že po nich zbylo místo:
        // blok je užší než prvek, takže je co dorovnávat.
        var blok = prvek.Rozvrzeni(data)[0];
        Assert.True(blok.SirkaMm < prvek.Width - 6f,
            $"blok měří {blok.SirkaMm:0.0} mm z {prvek.Width:0.0} mm - strop nenechal volné místo, "
            + "takže dorovnání nemá co vyplnit");
    }

    // ── Dno napřímo ─────────────────────────────────────────────────────────

    /// <summary>Zadané dno nahradí výpočet z písma a okraje, takže okraj na šířku přestane sahat.</summary>
    [Fact]
    public void ZadaneDnoPrebijeVypocetZOkraje()
    {
        var data = Data(spoju: 40);
        // Dno musí být nad přirozenou šířkou, jinak by rozhodoval zbytek prvku a ne ono.
        float dno = SirkaSloupce(new LineTableElement { X = 0, Y = 0, Width = 120 }, data) + 5f;
        var sOkrajem = new LineTableElement { X = 0, Y = 0, Width = 120, NejmensiSirkaSpojeMm = dno };
        sOkrajem.DataStyle.PaddingLeft = 2f;
        sOkrajem.DataStyle.PaddingRight = 2f;
        var bezOkraje = new LineTableElement { X = 0, Y = 0, Width = 120, NejmensiSirkaSpojeMm = dno };

        // Dno je spodní hranice, ne přesná šířka: zvednutím dna se do bloku vejde méně spojů,
        // takže na každý zbyde o něco víc místa a sloupec vyjde širší než zadané minimum.
        Assert.True(SirkaSloupce(bezOkraje, data) >= dno,
            $"sloupec vyšel {SirkaSloupce(bezOkraje, data):0.00} mm pod zadaným dnem {dno:0.00} mm");
        // Vlastní pointa: s okrajem i bez něj totéž - okraj na šířku přestal sahat.
        Assert.Equal(SirkaSloupce(bezOkraje, data), SirkaSloupce(sOkrajem, data), precision: 3);
    }

    /// <summary>Ani zadané dno nepustí sloupec pod tvrdou hranici čitelnosti.</summary>
    [Fact]
    public void ZadaneDnoNepodlezeTvrdouHranici()
    {
        var prvek = new LineTableElement { X = 0, Y = 0, Width = 120, NejmensiSirkaSpojeMm = 0.5f };

        Assert.True(SirkaSloupce(prvek, Data(spoju: 40)) >= 3f, "zadané dno podlezlo tvrdou hranici 3 mm");
    }

    // ── Panel ───────────────────────────────────────────────────────────────

    /// <summary>Obě nová pole jsou v panelu a v jedné skupině se zbytkem nastavení šířky.</summary>
    [Theory]
    [InlineData("Nejmenší šířka sloupce spoje")]
    [InlineData("Největší šířka sloupce spoje")]
    [InlineData("Dorovnat prázdnými sloupci")]
    [InlineData("Stejně široké sloupce spojů")]
    [InlineData("Mezera mezi skupinami sloupců")]
    public void NastaveniSirkyJsouVJedneSkupine(string label)
        => Assert.Equal("Šířka sloupců", Descriptor(Prvek(), label).Group);

    /// <summary>Prázdná hodnota se v panelu zadává nulou - jinak by z něj nešlo omezení zrušit.</summary>
    [Fact]
    public void NulaVPanelaZnamenaBezOmezeni()
    {
        var prvek = Prvek();
        var d = (FloatPropertyDescriptor)Descriptor(prvek, "Největší šířka sloupce spoje");

        d.Set(6f);
        Assert.Equal(6f, prvek.NejvetsiSirkaSpojeMm);
        d.Set(0f);
        Assert.Null(prvek.NejvetsiSirkaSpojeMm);
    }

    /// <summary>Obě nová pole musí přežít kopii prvku, jinak by se duplikát tiskl jinak.</summary>
    [Fact]
    public void KopiePrvkuNesePodleObouMezi()
    {
        var prvek = Prvek();
        prvek.NejmensiSirkaSpojeMm = 3.5f;
        prvek.NejvetsiSirkaSpojeMm = 7f;

        var kopie = (LineTableElement)prvek.Clone();

        Assert.Equal(3.5f, kopie.NejmensiSirkaSpojeMm);
        Assert.Equal(7f, kopie.NejvetsiSirkaSpojeMm);
    }

    private static LineTableElement Prvek() => new() { X = 0, Y = 0, Width = 200 };

    /// <summary>Dno fixtury - prvek tak úzký, že o šířce sloupce rozhoduje jen spodní hranice.
    /// Měří se, ne hardcoduje: závisí na písmu, a to se může změnit.</summary>
    private static float Dno(DataContext data) =>
        new LineTableElement { X = 0, Y = 0, Width = 1 }.Rozvrzeni(data)[0].SloupecSpojeMm;

    private static DataContext Data(int spoju) => RenderSnapshot.Data(
        ("ZASTAVKY",
        [
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Kroměříž"), ("PORADI", "1")),
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Hulín"), ("PORADI", "2")),
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Přerov"), ("PORADI", "3")),
        ]),
        ("SPOJE",
        [.. Enumerable.Range(1, spoju).Select(i => RenderSnapshot.Row(
            ("CISLO_SPOJE", i.ToString()), ("SKUPINA", "1"), ("SMER", "T"),
            ("CASY", new JsonArray("8:00", "8:20", "8:40"))))]),
        ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", ""), ("PORADI", "1"))]));
}
