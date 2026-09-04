using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Layout;

namespace TemplateDesigner.Tests.Layout;

/// <summary>
/// Kolik místa na stránce zbývá pro sekci - podklad pro „Vyplnit zbytek stránky" (`B118`).
/// </summary>
/// <remarks>
/// Výšku sekce šlo do 3. 9. zadat jen číslem, takže si uživatel musel od tiskové plochy odečítat
/// ostatní sekce (podnět Jan 3. 9.).
///
/// <para>⚠️ Počítá se podle **papíru**. Původní výpočet odečítal 5 mm za každou sekci jako místo
/// pro její modré záhlaví na plátně - u tří sekcí by tím zůstalo 15 mm papíru nevyužitých
/// a tlačítko by stránku nevyplnilo.</para>
/// </remarks>
public class SectionLayoutMathTests
{
    /// <summary>A4 na výšku s okraji 10 mm → tisková plocha 277 mm.</summary>
    private static Template Sablona(params float[] vysky)
    {
        // Okraje 10 mm se musí nastavit explicitně - PageSize je má výchozí nulové, takže bez
        // nich by tisková plocha byla celých 297 mm a test by měřil jiný papír než šablony.
        var t = new Template
        {
            PageSize = new PageSize
            {
                Width = 210, Height = 297,
                MarginTop = 10, MarginBottom = 10, MarginLeft = 10, MarginRight = 10,
            }
        };
        t.Sections.Clear();
        for (int i = 0; i < vysky.Length; i++)
            t.Sections.Add(new Section { Name = $"Sekce {i + 1}", DesignerHeight = vysky[i] });
        return t;
    }

    [Fact]
    public void PrikladZeZadani_TriSekcePoDesetiProcentech()
    {
        // Zadání Jan 3. 9.: tři sekce zabírají 0-10 %, 10-20 % a 20-30 % stránky. Po vyplnění
        // prostřední má vyjít 0-10 %, 10-90 % a 90-100 % - tedy stránka je plná a poslední
        // sekce si drží svých 10 %.
        float desetProcent = 277f * 0.1f;
        var t = Sablona(desetProcent, desetProcent, desetProcent);

        float zbyva = SectionLayoutMath.ZbyvajiciMistoMm(t, t.Sections[1]);

        // Prostřední dostane 80 % stránky; první a třetí si drží svých 10 %.
        Assert.Equal(277f * 0.8f, zbyva, precision: 1);

        // A dohromady vyplní přesně tiskovou plochu - nic se nevytlačí ani nezbyde.
        t.Sections[1].DesignerHeight = zbyva;
        Assert.Equal(277f, t.Sections.Sum(s => s.DesignerHeight ?? 0f), precision: 1);
    }

    [Fact]
    public void SekcePodSePosune_AlePodrziSvouVysku()
    {
        // Chování rozhodnuté 3. 9.: vyplnění nemění výšku ostatních sekcí. Sekce pod se jen posune,
        // a protože prvky mají souřadnice relativní ke své sekci, jdou s ní.
        var t = Sablona(30f, 30f, 30f);
        float pred = t.Sections[2].DesignerHeight ?? 0f;

        t.Sections[1].DesignerHeight = SectionLayoutMath.ZbyvajiciMistoMm(t, t.Sections[1]);

        Assert.Equal(pred, t.Sections[2].DesignerHeight);
        Assert.Equal(30f, t.Sections[0].DesignerHeight);
    }

    [Fact]
    public void JednaSekce_DostaneCelouTiskovouPlochu()
    {
        var t = Sablona(50f);

        Assert.Equal(277f, SectionLayoutMath.ZbyvajiciMistoMm(t, t.Sections[0]), precision: 1);
    }

    [Fact]
    public void PlnaStranka_NabidneNejmensiVysku_NeZapornou()
    {
        // Ostatní sekce už zaberou celou plochu; výsledek nesmí být nula ani záporné číslo,
        // protože do takové sekce by se nedalo nic položit.
        var t = Sablona(200f, 10f, 200f);

        Assert.Equal(SectionLayoutMath.MinimalniVyskaMm,
            SectionLayoutMath.ZbyvajiciMistoMm(t, t.Sections[1]));
    }

    [Fact]
    public void NepocitaMistoProZahlaviSekceNaPlatne()
    {
        // Pojistka proti návratu původního výpočtu: ten odečítal 5 mm za každou sekci, takže
        // u tří sekcí chybělo 15 mm papíru a stránka se nevyplnila.
        var t = Sablona(40f, 40f, 40f);

        Assert.Equal(277f - 80f, SectionLayoutMath.ZbyvajiciMistoMm(t, t.Sections[1]), precision: 1);
    }

    [Fact]
    public void SekceKteraJizVyplnuje_SePozna()
    {
        // Slouží k tomu, aby tlačítko nenabízelo akci, po které se nic nezmění.
        var t = Sablona(40f, 40f);

        // Na půl prázdné stránce nevyplňuje ani jedna.
        Assert.False(SectionLayoutMath.VyplnujeCeleMisto(t, t.Sections[0]));
        Assert.False(SectionLayoutMath.VyplnujeCeleMisto(t, t.Sections[1]));

        t.Sections[1].DesignerHeight = SectionLayoutMath.ZbyvajiciMistoMm(t, t.Sections[1]);

        // ⚠️ Po vyplnění je stránka plná, takže „vyplňuje" platí pro OBĚ - i pro tu, které se
        // nikdo nedotkl. Je to správně: zbývající místo pro ni je právě její dnešní výška.
        Assert.True(SectionLayoutMath.VyplnujeCeleMisto(t, t.Sections[1]));
        Assert.True(SectionLayoutMath.VyplnujeCeleMisto(t, t.Sections[0]));
    }

    // ── Dno: sekce nesmí spadnout pod svůj obsah ────────────────────────────
    // Nález Jan 4. 9.: po zmenšení sekce se prvek u dolního okraje v návrháři **ořízl**, ale při
    // tisku vyšel celý - engine dává sekci max(nastavená výška, obsah), takže ji nafoukne zpátky.
    // Návrhář a tisk se tím rozešly a šablona vypadala rozbitě, přestože nebyla.

    [Fact]
    public void PrvekDoleVSekci_DrziJejiMinimalniVysku()
    {
        var t = Sablona(100f);
        t.Sections[0].Elements.Add(new TextElement { X = 5f, Y = 60f, Width = 40f, Height = 30f });

        // Prvek sahá do 90 mm, takže pod 90 mm sekci zmenšit nejde.
        Assert.Equal(90f, SectionLayoutMath.MinimalniVyskaProSekci(t.Sections[0]), precision: 1);
    }

    [Fact]
    public void PrazdnaSekce_MaDnoNaKonstante()
    {
        var t = Sablona(100f);

        Assert.Equal(SectionLayoutMath.MinimalniVyskaMm,
            SectionLayoutMath.MinimalniVyskaProSekci(t.Sections[0]));
    }

    [Fact]
    public void DnoBerePrvekSNejnizsiHranou_NeNejvyssi()
    {
        var t = Sablona(100f);
        t.Sections[0].Elements.Add(new TextElement { X = 5f, Y = 70f, Width = 40f, Height = 10f });
        t.Sections[0].Elements.Add(new TextElement { X = 5f, Y = 10f, Width = 40f, Height = 10f });

        Assert.Equal(80f, SectionLayoutMath.MinimalniVyskaProSekci(t.Sections[0]), precision: 1);
    }

    [Fact]
    public void NaPlneStranceNabidkaNespadnePodObsah()
    {
        // Ostatní sekce zaberou celou plochu, ale tahle drží prvek - nabídnutá výška nesmí být
        // menší než on, jinak by se v návrháři ořízl a v tisku vyšel celý.
        var t = Sablona(200f, 50f, 100f);
        t.Sections[1].Elements.Add(new TextElement { X = 5f, Y = 20f, Width = 40f, Height = 25f });

        Assert.Equal(45f, SectionLayoutMath.ZbyvajiciMistoMm(t, t.Sections[1]), precision: 1);
    }

    [Fact]
    public void Paticka_SeVyplnujeStejneJakoOstatni()
    {
        // Patička se na plátně tahá za horní hranu, ale výpočet zbývajícího místa je pro ni týž
        // jako pro sekce v toku - jinak by položka v místní nabídce dala u patičky jiné číslo.
        var t = Sablona(50f, 30f);
        t.Sections[1].Role = SectionRole.Footer;

        Assert.Equal(277f - 50f, SectionLayoutMath.ZbyvajiciMistoMm(t, t.Sections[1]), precision: 1);
    }

    [Fact]
    public void DynamickaVyska_SeNepocita()
    {
        // Prázdná výška = engine ji dopočítá z obsahu; vyplňovat ji nemá smysl.
        var t = Sablona(40f, 40f);
        t.Sections[1].DesignerHeight = null;

        Assert.True(SectionLayoutMath.VyplnujeCeleMisto(t, t.Sections[1]));
    }
}
