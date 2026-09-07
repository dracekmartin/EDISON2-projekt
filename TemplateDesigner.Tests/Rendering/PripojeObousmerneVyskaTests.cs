using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Výška prvku a místa, kudy se smí řezat mezi stránkami, musí počítat s <b>týmiž</b> přípojnými
/// řádky, jaké tabulka opravdu nakreslí.
/// </summary>
/// <remarks>
/// <para>Kreslí se přípoje spojů <b>hlavního</b> směru (zrcadlená strana má v přípojném řádku
/// prázdné buňky), ale výška je počítala z <b>obou</b> směrů. Každý blok tím vyšel o pár milimetrů
/// vyšší, než se nakreslil, a hranice bloků se posunula pod to, kde blok doopravdy začíná -
/// stránka se pak rozřízla uprostřed pásu se skupinami dnů. Na výtisku to vypadalo jako šedý pruh
/// s nadpisem a pod ním nic (nahlásil Jan 7. 9. na lince `330510`, kde se rozdíl nasčítal na
/// 6,4 mm za tři bloky).</para>
/// <para>⚠️ Že se přípoje zrcadleného směru netisknou, je <b>samostatná</b> mezera - tenhle test
/// hlídá jen to, že se za ně nerezervuje místo.</para>
/// </remarks>
public class PripojeObousmerneVyskaTests
{
    private const float VyskaRadku = 5f;

    /// <summary>Obousměrná stránka: směr `T` je ten zrcadlený (podle prvního spoje), `Z` hlavní.
    /// <paramref name="kdeJePripoj"/> říká, který směr nese přípoj u prostřední zastávky;
    /// <paramref name="slotu"/> je počet spojů v každém směru.</summary>
    private static DataContext Linka(string? kdeJePripoj, int slotu = 1)
    {
        static (string, object?) Pripoj() => ("PRIPOJE", RenderSnapshot.Nested(
            [[("TYP", "vycka"), ("PORADI", "2"), ("RADEK", "1"),
              ("CAS", "10:25"), ("LINKA", "330520"), ("TEXT", "Kaplice")]]));

        var spoje = new List<DataContext>();
        foreach (var smer in new[] { "T", "Z" })
            for (int i = 0; i < slotu; i++)
            {
                var spoj = new List<(string, object?)>
                {
                    ("CISLO_SPOJE", $"{smer}{i}"), ("SKUPINA", "1"), ("SMER", smer),
                };
                // Přípoj nese jen první spoj směru - ať vznikne jeden řádek, ne jeden na spoj.
                if (i == 0 && kdeJePripoj == smer) spoj.Add(Pripoj());
                spoje.Add(RenderSnapshot.Row([.. spoj]));
            }

        return RenderSnapshot.Data(
            ("ZASTAVKY",
            [
                RenderSnapshot.Row(("ID", "1"), ("NAZEV_ZASTAVKY", "Český Krumlov"), ("PORADI", "1")),
                RenderSnapshot.Row(("ID", "2"), ("NAZEV_ZASTAVKY", "Kaplice, rozcestí"), ("PORADI", "2")),
                RenderSnapshot.Row(("ID", "3"), ("NAZEV_ZASTAVKY", "České Budějovice"), ("PORADI", "3")),
            ]),
            ("SPOJE", [.. spoje]),
            ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", "Pracovní dny"), ("PORADI", "1"))]));
    }

    private static LineTableElement Prvek() => new()
    {
        X = 0, Y = 0, Width = 190,
        DataRowHeight = VyskaRadku,
        ShowPripojeRows = true,
        ShowNzRow = false,
        ShowPlatnostRow = false,
        ShowGroupHeaders = false,
    };

    [Fact]
    public void PripojZrcadlenehoSmeru_NerezervujeMisto()
    {
        float bez = Prvek().CalculateHeightFromData(Linka(null));
        float zrcadleny = Prvek().CalculateHeightFromData(Linka("T"));

        Assert.Equal(bez, zrcadleny, 2);
    }

    /// <summary>Pojistka, že test výše nezelená proto, že se přípoje nepočítají vůbec.</summary>
    [Fact]
    public void PripojHlavnihoSmeru_MistoRezervuje()
    {
        float bez = Prvek().CalculateHeightFromData(Linka(null));
        float hlavni = Prvek().CalculateHeightFromData(Linka("Z"));

        Assert.True(hlavni > bez,
            $"přípoj hlavního směru výšku nezvětšil ({bez:0.00} → {hlavni:0.00} mm)");
    }

    /// <summary>
    /// Totéž na hranicích bloků: zlom musí ležet tam, kde blok doopravdy končí. Měří se rozestup
    /// prvního silného zlomu, protože právě ten říká, kde se stránka rozřízne.
    /// </summary>
    [Fact]
    public void ZlomySeNeposunouPripojemZrcadlenehoSmeru()
    {
        var uzky = Prvek();
        uzky.Width = 60f;   // úzký prvek a víc spojů = víc bloků, jinak žádný silný zlom nevznikne

        var bez = uzky.ZlomyMm(Linka(null, 6)).Where(z => z.Silny).Select(z => z.Mm).ToList();
        var zrcadleny = uzky.ZlomyMm(Linka("T", 6)).Where(z => z.Silny).Select(z => z.Mm).ToList();

        Assert.NotEmpty(bez);
        Assert.Equal(bez.Count, zrcadleny.Count);
        for (int i = 0; i < bez.Count; i++)
            Assert.Equal(bez[i], zrcadleny[i], 2);
    }
}
