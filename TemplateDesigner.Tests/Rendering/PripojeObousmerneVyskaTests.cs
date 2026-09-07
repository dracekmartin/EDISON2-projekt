using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Výška prvku a místa, kudy se smí řezat mezi stránkami, musí počítat s <b>týmiž</b> přípojnými
/// řádky, jaké tabulka opravdu nakreslí.
/// </summary>
/// <remarks>
/// <para>Vzniklo z chyby, kdy se kreslily přípoje jen <b>hlavního</b> směru, ale výška se počítala
/// z <b>obou</b>. Každý blok tím vyšel o pár milimetrů vyšší, než se nakreslil, hranice bloků se
/// posunula pod jeho skutečný začátek a stránka se rozřízla uprostřed pásu se skupinami dnů - na
/// výtisku šedý pruh s nadpisem a pod ním nic (nahlásil Jan 7. 9. na lince `330510`, rozdíl
/// 6,4 mm za tři bloky).</para>
/// <para>⚠️ <b>Testy se 7. 9. přepsaly.</b> Do té doby držely, že přípoj zrcadleného směru místo
/// nerezervuje - to platilo, dokud se nekreslil (`B128`). Dnes se kreslí na obou stranách, takže
/// se hlídá <b>symetrie</b>: táž situace vlevo i vpravo musí dát shodnou výšku i shodné zlomy.
/// Ta podmínka je silnější než původní konstanta - spadne, když se kterákoli strana přestane
/// počítat i když se přestane kreslit.</para>
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

    /// <summary>Přípoj rezervuje místo bez ohledu na stranu - kreslí se na obou (`B128`).</summary>
    [Fact]
    public void PripojKterekoliStrany_MistoRezervuje()
    {
        float bez = Prvek().CalculateHeightFromData(Linka(null));
        float zrcadleny = Prvek().CalculateHeightFromData(Linka("T"));
        float hlavni = Prvek().CalculateHeightFromData(Linka("Z"));

        Assert.True(zrcadleny > bez,
            $"přípoj zrcadleného směru výšku nezvětšil ({bez:0.00} → {zrcadleny:0.00} mm)");
        Assert.True(hlavni > bez,
            $"přípoj hlavního směru výšku nezvětšil ({bez:0.00} → {hlavni:0.00} mm)");
    }

    /// <summary>
    /// Táž situace na obou stranách dá <b>shodnou</b> výšku - jeden přípoj je jeden řádek, ať leží
    /// vlevo, nebo vpravo. Spadne, když se kterákoli strana přestane počítat i kreslit.
    /// </summary>
    [Fact]
    public void ObeStrany_DajiShodnouVysku()
    {
        float zrcadleny = Prvek().CalculateHeightFromData(Linka("T"));
        float hlavni = Prvek().CalculateHeightFromData(Linka("Z"));

        Assert.Equal(hlavni, zrcadleny, 2);
    }

    /// <summary>
    /// Totéž na hranicích bloků: zlom musí ležet tam, kde blok doopravdy končí. Přípoj vlevo
    /// a vpravo proto musí dát <b>shodné</b> silné zlomy - právě ty říkají, kde se stránka rozřízne.
    /// </summary>
    [Fact]
    public void ObeStrany_DajiShodneZlomy()
    {
        var uzky = Prvek();
        uzky.Width = 60f;   // úzký prvek a víc spojů = víc bloků, jinak žádný silný zlom nevznikne

        var zrcadleny = uzky.ZlomyMm(Linka("T", 6)).Where(z => z.Silny).Select(z => z.Mm).ToList();
        var hlavni = uzky.ZlomyMm(Linka("Z", 6)).Where(z => z.Silny).Select(z => z.Mm).ToList();

        Assert.NotEmpty(hlavni);
        Assert.Equal(hlavni.Count, zrcadleny.Count);
        for (int i = 0; i < hlavni.Count; i++)
            Assert.Equal(hlavni[i], zrcadleny[i], 2);
    }

    /// <summary>Pojistka, že testy výše nezelenají proto, že se přípoje nepočítají vůbec: přípoj
    /// zlomy <b>posune</b>, protože přidá řádek.</summary>
    [Fact]
    public void PripojZlomyPosune()
    {
        var uzky = Prvek();
        uzky.Width = 60f;

        var bez = uzky.ZlomyMm(Linka(null, 6)).Where(z => z.Silny).Select(z => z.Mm).ToList();
        var sPripojem = uzky.ZlomyMm(Linka("T", 6)).Where(z => z.Silny).Select(z => z.Mm).ToList();

        Assert.NotEmpty(bez);
        Assert.Equal(bez.Count, sPripojem.Count);
        Assert.True(sPripojem[0] > bez[0] + 0.01f,
            $"první silný zlom se přípojem neposunul ({bez[0]:0.00} → {sPripojem[0]:0.00} mm)");
    }
}
