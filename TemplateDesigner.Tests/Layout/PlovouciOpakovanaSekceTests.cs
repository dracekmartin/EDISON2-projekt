using TemplateDesigner.Models;
using TemplateDesigner.Models.Layout;

namespace TemplateDesigner.Tests.Layout;

/// <summary>
/// Opakovaná sekce s Umístěním „Obsah" se tiskne na každé stránce hned za obsahem (`B134`).
/// </summary>
/// <remarks>
/// Požadavek Jan 7. 9.: „poznámky musí být na každé stránce a hned pod tabulkou". Do 10. 9. šlo
/// jen jedno z toho - „Jednou" plavalo za obsahem, ale bylo jen na poslední stránce; „Každá
/// stránka" sedělo na spodní hraně papíru a mezi tabulkou a poznámkami zůstalo prázdno.
/// Kombinace nešla nastavit vůbec: Umístění „Obsah" u opakované sekce nic vlastního neznamenalo
/// a engine ji zařadil mezi hlavičky nebo patičky podle pozice v šabloně.
/// </remarks>
public class PlovouciOpakovanaSekceTests
{
    private const float TiskovaPlochaMm = 277f;

    private static Template Sablona(params Section[] sekce)
    {
        var t = new Template
        {
            PageSize = new PageSize
            {
                Width = 210, Height = 297,
                MarginTop = 10, MarginBottom = 10, MarginLeft = 10, MarginRight = 10,
            }
        };
        t.Sections.Clear();
        t.Sections.AddRange(sekce);
        return t;
    }

    private static Section Obsah(float vyska) => new()
    {
        Name = "Tabulka",
        PrintBehavior = SectionPrintBehavior.Once,
        OverflowBehavior = SectionOverflowBehavior.SplitAcrossPages,
        DesignerHeight = vyska
    };

    private static Section Opakovana(string jmeno, float vyska, SectionRole umisteni) => new()
    {
        Name = jmeno,
        PrintBehavior = SectionPrintBehavior.OnEveryPage,
        OverflowBehavior = SectionOverflowBehavior.KeepTogether,
        Role = umisteni,
        DesignerHeight = vyska
    };

    private static LayoutResult Sazba(Template t) =>
        new PageLayoutEngine(new SkiaHeightCalculator()).Layout(t, null);

    private static PlacedSection NaStrance(LayoutResult r, int strana, string jmeno) =>
        r.Pages[strana - 1].Sections.Single(s => s.Source.Name == jmeno);

    [Fact]
    public void PoznamkySeTisknouNaKazdeStrance()
    {
        var vysledek = Sazba(Sablona(Obsah(600f), Opakovana("Poznámky", 20f, SectionRole.BelowContent)));

        Assert.True(vysledek.TotalPages > 1);
        Assert.All(vysledek.Pages,
            p => Assert.Contains(p.Sections, s => s.Source.Name == "Poznámky"));
    }

    /// <summary>
    /// Poznámky začínají hned za obsahem, ne na spodní hraně papíru.
    /// </summary>
    /// <remarks>
    /// ⚠️ **Měří se na krátkém obsahu, a to je podstatné.** Na **plné** stránce obojí splývá:
    /// plovoucí sekce i patička si ukrojí místo předem, takže obsah končí přesně tam, kde by
    /// patička začínala. Rozdíl je vidět jen tam, kde obsah stránku nevyplní - jinak by test
    /// prošel i se starým chováním (zjištěno mutací 10. 9.).
    /// </remarks>
    [Fact]
    public void PoznamkyZacinajiHnedZaObsahem_NeNaSpodniHrane()
    {
        var vysledek = Sazba(Sablona(Obsah(100f), Opakovana("Poznámky", 20f, SectionRole.BelowContent)));

        var tabulka  = NaStrance(vysledek, 1, "Tabulka");
        var poznamky = NaStrance(vysledek, 1, "Poznámky");

        Assert.Equal(1, vysledek.TotalPages);
        Assert.Equal(tabulka.Y + tabulka.Height, poznamky.Y, precision: 1);
        Assert.True(poznamky.Y < TiskovaPlochaMm - 100f,
            $"poznámky na {poznamky.Y:0.#} mm - u krátkého obsahu nemají klesat ke spodní hraně");
    }

    [Fact]
    public void MistoSiUkrojiPredem_TakzeSeNaStrankuVejde()
    {
        // Plovoucí sekce se kreslí až za obsahem, ale místo musí být rezervované předem - jinak
        // by ji na plné stránce obsah vytlačil za papír.
        var vysledek = Sazba(Sablona(Obsah(600f), Opakovana("Poznámky", 20f, SectionRole.BelowContent)));

        Assert.All(vysledek.Pages, p => Assert.All(p.Sections,
            s => Assert.True(s.Y + s.Height <= TiskovaPlochaMm + SectionLayoutMath.TolerancePrekryvuMm,
                $"sekce {s.Source.Name} na straně {p.PageNumber} sahá do {s.Y + s.Height:0.#} mm")));
    }

    [Fact]
    public void NaPosledniStrance_PlavouTesnePodKratsimObsahem()
    {
        // Poslední strana má obsahu málo - poznámky mají jít hned pod něj, ne dolů na papír.
        var vysledek = Sazba(Sablona(Obsah(300f), Opakovana("Poznámky", 20f, SectionRole.BelowContent)));

        int posledni = vysledek.TotalPages;
        var tabulka  = NaStrance(vysledek, posledni, "Tabulka");
        var poznamky = NaStrance(vysledek, posledni, "Poznámky");

        Assert.Equal(tabulka.Y + tabulka.Height, poznamky.Y, precision: 1);
        Assert.True(poznamky.Y < TiskovaPlochaMm - 40f,
            $"poznámky na {poznamky.Y:0.#} mm - na krátké stránce nemají klesat ke spodní hraně");
    }

    // ── Umístění Patička a Hlavička se nezměnily ─────────────────────────────

    [Fact]
    public void Paticka_ZustavaNaSpodniHrane()
    {
        var vysledek = Sazba(Sablona(Obsah(300f), Opakovana("Patička", 20f, SectionRole.Footer)));

        var paticka = NaStrance(vysledek, 1, "Patička");
        Assert.Equal(TiskovaPlochaMm - 20f, paticka.Y, precision: 1);
    }

    [Fact]
    public void Hlavicka_ZustavaNahore()
    {
        var vysledek = Sazba(Sablona(
            Opakovana("Hlavička", 25f, SectionRole.Header), Obsah(300f)));

        Assert.Equal(0f, NaStrance(vysledek, 1, "Hlavička").Y, precision: 1);
        Assert.Equal(25f, NaStrance(vysledek, 1, "Tabulka").Y, precision: 1);
    }

    /// <summary>
    /// U „Pod obsahem" pozice v šabloně nerozhoduje.
    /// </summary>
    /// <remarks>
    /// U Umístění „Obsah" rozhoduje dál a musí - je to výchozí hodnota, viz
    /// <see cref="UmisteniObsah_SeRidiDalPozici"/>.
    /// </remarks>
    [Fact]
    public void PoziceVSablone_UzNerozhoduje()
    {
        var pred = Sazba(Sablona(Opakovana("Poznámky", 20f, SectionRole.BelowContent), Obsah(100f)));
        var za   = Sazba(Sablona(Obsah(100f), Opakovana("Poznámky", 20f, SectionRole.BelowContent)));

        var tabulkaPred = NaStrance(pred, 1, "Tabulka");
        var tabulkaZa   = NaStrance(za, 1, "Tabulka");

        Assert.Equal(tabulkaPred.Y + tabulkaPred.Height, NaStrance(pred, 1, "Poznámky").Y, precision: 1);
        Assert.Equal(tabulkaZa.Y + tabulkaZa.Height, NaStrance(za, 1, "Poznámky").Y, precision: 1);
    }

    /// <summary>
    /// ⚠️ Umístění „Obsah" se dál řídí pozicí v šabloně - a musí.
    /// </summary>
    /// <remarks>
    /// Je to **výchozí** hodnota, takže ji má každá opakovaná sekce, na jejíž Umístění nikdo
    /// nesáhl. První verze `B134` jí dala nový význam („plav za obsahem") a hlavičky existujících
    /// šablon spadly pod tabulku (nález Jan 10. 9. na `linkovy_hlavickapod_mutace`, kde je
    /// hlavička sekce první a má Umístění Obsah). Nové chování proto dostalo vlastní hodnotu.
    /// </remarks>
    [Fact]
    public void UmisteniObsah_SeRidiDalPozici()
    {
        var sekcePred = Opakovana("Hlavička", 17f, SectionRole.Body);
        var pred = Sazba(Sablona(sekcePred, Obsah(100f)));
        Assert.Equal(0f, NaStrance(pred, 1, "Hlavička").Y, precision: 1);

        var za = Sazba(Sablona(Obsah(100f), Opakovana("Patička", 17f, SectionRole.Body)));
        Assert.Equal(TiskovaPlochaMm - 17f, NaStrance(za, 1, "Patička").Y, precision: 1);
    }

    [Fact]
    public void HlavickaPaticiIPoznamky_NaJedneStrance()
    {
        var vysledek = Sazba(Sablona(
            Opakovana("Hlavička", 25f, SectionRole.Header),
            Obsah(600f),
            Opakovana("Poznámky", 20f, SectionRole.BelowContent),
            Opakovana("Patička", 15f, SectionRole.Footer)));

        var hlavicka = NaStrance(vysledek, 1, "Hlavička");
        var tabulka  = NaStrance(vysledek, 1, "Tabulka");
        var poznamky = NaStrance(vysledek, 1, "Poznámky");
        var paticka  = NaStrance(vysledek, 1, "Patička");

        Assert.Equal(0f, hlavicka.Y, precision: 1);
        Assert.Equal(25f, tabulka.Y, precision: 1);
        Assert.Equal(tabulka.Y + tabulka.Height, poznamky.Y, precision: 1);
        Assert.Equal(TiskovaPlochaMm - 15f, paticka.Y, precision: 1);

        // Na poslední (kratší) straně se poznámky vytáhnou nahoru za obsah - tam je vidět
        // rozdíl proti patičce.
        int posledni = vysledek.TotalPages;
        var tabulkaKonec  = NaStrance(vysledek, posledni, "Tabulka");
        var poznamkyKonec = NaStrance(vysledek, posledni, "Poznámky");
        Assert.Equal(tabulkaKonec.Y + tabulkaKonec.Height, poznamkyKonec.Y, precision: 1);

        // Mezi poznámkami a patičkou nesmí být překryv - obojí si ukrojilo místo předem.
        Assert.True(poznamky.Y + poznamky.Height <= paticka.Y + SectionLayoutMath.TolerancePrekryvuMm,
            $"poznámky končí na {poznamky.Y + poznamky.Height:0.#} mm, patička začíná na {paticka.Y:0.#} mm");
    }
}
