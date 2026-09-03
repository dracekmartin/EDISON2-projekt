using TemplateDesigner.Commands.Implementations;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Commands;

/// <summary>
/// Změna formátu stránky (`B104`) - obsah se <b>přeškáluje</b>, ne vymaže.
/// </summary>
/// <remarks>
/// <para>Do 31. 8. změna rozměru nebo orientace celou šablonu zahodila a založila prázdné plátno;
/// uživatel ji stavěl znovu. Nově se mění <b>jen geometrie</b> - nastavení prvků (sloupce, styly,
/// volby, podmínky) zůstávají, jak byly.</para>
/// <para><b>Poměr počítá z tiskové plochy, ne z papíru</b> (stránka bez okrajů) - prvky leží uvnitř
/// okrajů, takže jinak by se změna okrajů promítla do škálování dvakrát.</para>
/// <para>Při <b>stejném poměru stran</b> vyjde jedno číslo a výsledek je zvětšenina téhož. Při
/// <b>změně orientace</b> se bere ten menší z obou poměrů (nic nepřeteče) a obsah se vodorovně
/// vystředí - svisle zůstává nahoře, protože sekce jdou odshora jako dokument.</para>
/// </remarks>
public class ZmenaFormatuStrankyTests
{
    private const string A4W = "PageSize.Width", A4H = "PageSize.Height";

    private static Template Sablona(float w, float h, float okraj = 10f)
    {
        var t = new Template
        {
            PageSize = new PageSize
            {
                Width = w, Height = h,
                MarginTop = okraj, MarginBottom = okraj, MarginLeft = okraj, MarginRight = okraj,
            },
        };
        var sekce = new Section { Name = "Hlavička", DesignerHeight = 40f };
        sekce.Elements.Add(new TextElement
        {
            X = 0, Y = 0, Width = 60, Height = 10,
            Text = "Linka {CISLO_LINKY}",
            Style = new TextStyle { FontFamily = "Arial", FontSize = 12f, Bold = true },
        });
        sekce.Elements.Add(new TextElement
        {
            X = t.PageSize.PrintableWidth - 40f, Y = 0, Width = 40, Height = 10,
            Text = "Platí od {DATUM_OD}",
            Style = new TextStyle { FontFamily = "Arial", FontSize = 9f },
        });
        t.Sections.Add(sekce);
        return t;
    }

    /// <summary>Prvek u levého okraje.</summary>
    private static TextElement Prvek(Template t) => (TextElement)t.Sections[0].Elements[0];

    /// <summary>Prvek u pravého okraje - bez něj by nešlo poznat, jestli se středí blok obsahu,
    /// nebo jen jeden prvek.</summary>
    private static TextElement PrvekVpravo(Template t) => (TextElement)t.Sections[0].Elements[1];

    private static Dictionary<string, string> Pole(float w, float h) => new()
    {
        [A4W] = w.ToString(System.Globalization.CultureInfo.InvariantCulture),
        [A4H] = h.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    /// <summary>Poměr z tiskové plochy: menší z obou os, aby se obsah vešel.</summary>
    private static PageFormatPlan Prepocet(Template t, float newW, float newH)
        => PageFormatChange.Plan(t, newW, newH,
            t.PageSize.MarginLeft, t.PageSize.MarginRight,
            t.PageSize.MarginTop, t.PageSize.MarginBottom);

    /// <summary>Provede změnu formátu tak, jak ji provede aplikace.</summary>
    private static void Zmen(Template t, float newW, float newH)
    {
        var plan = Prepocet(t, newW, newH);
        new ChangePageFormatCommand(t, Pole(newW, newH), plan).Execute();
    }

    // ── stejný poměr stran ────────────────────────────────────────────────────

    /// <summary>A5 → A4: jeden poměr na obě osy, obsah se zvětší a nic se nestředí.</summary>
    [Fact]
    public void StejnyPomerStran_ZvetsiObsahBezStredeni()
    {
        var t = Sablona(148f, 210f);
        var plan = Prepocet(t, 210f, 297f);

        Assert.True(plan.Factor > 1.3f && plan.Factor < 1.6f,
            $"poměr vyšel {plan.Factor:0.###}, čekal jsem kolem 1,45");
        // ⚠️ Nula nevyjde ani při stejném poměru papíru: okraje se neškálují, takže tisková
        // plocha svůj poměr o kousek změní. Jednotky milimetrů jsou v pořádku, desítky ne.
        Assert.True(MathF.Abs(plan.NewOffsetX) < 3f,
            $"při stejném poměru stran se má středit jen nepatrně, offset je {plan.NewOffsetX:0.##} mm");
    }

    /// <summary>Zvětšení opravdu přepočítá pozice, rozměry i velikost písma.</summary>
    [Fact]
    public void Zvetseni_PrepocitaGeometriiIPismo()
    {
        var t = Sablona(148f, 210f);
        var plan = Prepocet(t, 210f, 297f);
        var puvodniSirka = Prvek(t).Width;

        new ChangePageFormatCommand(t, Pole(210f, 297f), plan).Execute();

        Assert.Equal(puvodniSirka * plan.Factor, Prvek(t).Width, 0.15f);
        Assert.Equal(12f * plan.Factor, Prvek(t).Style.FontSize!.Value, 0.15f);
        Assert.Equal(210f, t.PageSize.Width, 0.001f);
        Assert.Equal(297f, t.PageSize.Height, 0.001f);
    }

    // ── změna orientace ───────────────────────────────────────────────────────

    /// <summary>Na šířku: bere se menší poměr, takže se obsah zmenší a nic nepřeteče.</summary>
    [Fact]
    public void ZmenaOrientace_BereMensiPomer_NicNepretece()
    {
        var t = Sablona(210f, 297f);
        var plan = Prepocet(t, 297f, 210f);

        Assert.True(plan.Factor < 1f, $"na šířku se má obsah zmenšit, poměr je {plan.Factor:0.###}");

        new ChangePageFormatCommand(t, Pole(297f, 210f), plan).Execute();

        float pravaHrana = PrvekVpravo(t).X + PrvekVpravo(t).Width;
        Assert.True(pravaHrana <= t.PageSize.PrintableWidth + 0.2f,
            $"prvek sahá na {pravaHrana:0.##} mm, tisková plocha končí na {t.PageSize.PrintableWidth:0.##} mm");
    }

    /// <summary>Po otočení sedí obsah vodorovně na střed, ne u levého okraje.</summary>
    [Fact]
    public void ZmenaOrientace_VystrediObsahVodorovne()
    {
        var t = Sablona(210f, 297f);
        var plan = Prepocet(t, 297f, 210f);

        Assert.True(plan.NewOffsetX > 1f,
            $"po otočení se má obsah vystředit, offset je {plan.NewOffsetX:0.##} mm");

        new ChangePageFormatCommand(t, Pole(297f, 210f), plan).Execute();

        float vlevo = Prvek(t).X;
        float vpravo = t.PageSize.PrintableWidth - (PrvekVpravo(t).X + PrvekVpravo(t).Width);
        Assert.Equal(vlevo, vpravo, 1.5f);
    }

    /// <summary>Svisle obsah zůstává nahoře - sekce jdou odshora jako dokument.</summary>
    [Fact]
    public void ZmenaOrientace_SvisleZustavaNahore()
    {
        var t = Sablona(210f, 297f);
        var plan = Prepocet(t, 297f, 210f);

        new ChangePageFormatCommand(t, Pole(297f, 210f), plan).Execute();

        Assert.Equal(0f, Prvek(t).Y, 0.001f);
    }

    // ── co se měnit nesmí ─────────────────────────────────────────────────────

    /// <summary>
    /// Nastavení prvku přežije změnu formátu beze změny - to je celý smysl `B104`.
    /// </summary>
    [Fact]
    public void NastaveniPrvku_ZustavaBezeZmeny()
    {
        var t = Sablona(210f, 297f);
        var plan = Prepocet(t, 297f, 210f);

        new ChangePageFormatCommand(t, Pole(297f, 210f), plan).Execute();

        var p = Prvek(t);
        Assert.Equal("Linka {CISLO_LINKY}", p.Text);
        Assert.Equal("Arial", p.Style.FontFamily);
        Assert.True(p.Style.Bold);
        Assert.Single(t.Sections);
        Assert.Equal(2, t.Sections[0].Elements.Count);
    }

    /// <summary>Okraje se berou <b>z dialogu</b>, ne přeškálované.</summary>
    [Fact]
    public void Okraje_SeBerouZDialogu_NePreskalovane()
    {
        var t = Sablona(148f, 210f, okraj: 10f);
        var plan = Prepocet(t, 210f, 297f);

        var pole = Pole(210f, 297f);
        pole["PageSize.MarginLeft"] = "12";

        new ChangePageFormatCommand(t, pole, plan).Execute();

        Assert.Equal(12f, t.PageSize.MarginLeft, 0.001f);
        Assert.Equal(10f, t.PageSize.MarginTop, 0.001f);   // co dialog neposlal, zůstává
    }

    /// <summary>Volba „nechat velikosti" změní jen stránku, obsahu se nedotkne.</summary>
    [Fact]
    public void BezPreskalovani_ZmeniJenStranku()
    {
        var t = Sablona(210f, 297f);
        float sirka = Prvek(t).Width;
        float font = Prvek(t).Style.FontSize!.Value;

        new ChangePageFormatCommand(t, Pole(297f, 210f), PageFormatChange.KeepSizes(t)).Execute();

        Assert.Equal(sirka, Prvek(t).Width, 0.001f);
        Assert.Equal(font, Prvek(t).Style.FontSize!.Value, 0.001f);
        Assert.Equal(297f, t.PageSize.Width, 0.001f);
    }

    // ── otočení tam a zpátky ──────────────────────────────────────────────────

    /// <summary>
    /// Otočení na šířku a zase zpátky vrátí obsah <b>na původní velikost</b>.
    /// </summary>
    /// <remarks>
    /// ⚠️ Tohle je ta past, kvůli které se poměr počítá od výchozí plochy a ne od aktuálního stavu.
    /// Poměr „aby se to vešlo" vyjde u A4 v obou směrech stejně (0,686), takže se dvě otočení
    /// <b>nevyruší, ale vynásobí</b>: obsah by po návratu seděl na 47 % a písmo 12 pt by z něj
    /// vyšlo jako 5,6 pt. Nález Jan 31. 8. při zkoušení - sekce se po návratu „nenatáhla zpátky".
    /// </remarks>
    [Fact]
    public void OtoceniTamAZpet_VratiPuvodniVelikost()
    {
        var t = Sablona(210f, 297f);
        float sirka = Prvek(t).Width, x = Prvek(t).X, font = Prvek(t).Style.FontSize!.Value;
        float vyskaSekce = t.Sections[0].DesignerHeight!.Value;

        Zmen(t, 297f, 210f);   // na šířku
        Zmen(t, 210f, 297f);   // a zpátky na výšku

        Assert.Equal(sirka, Prvek(t).Width, 0.15f);
        Assert.Equal(x, Prvek(t).X, 0.15f);
        Assert.Equal(font, Prvek(t).Style.FontSize!.Value, 0.15f);
        Assert.Equal(vyskaSekce, t.Sections[0].DesignerHeight!.Value, 0.15f);
    }

    /// <summary>Mezikrok navíc na tom nic nemění - poměr se počítá vždy od výchozí plochy.</summary>
    [Fact]
    public void PresJinyFormatAZpet_VratiPuvodniVelikost()
    {
        var t = Sablona(210f, 297f);
        float sirka = Prvek(t).Width, font = Prvek(t).Style.FontSize!.Value;

        Zmen(t, 297f, 210f);   // na šířku
        Zmen(t, 148f, 210f);   // A5
        Zmen(t, 210f, 297f);   // zpátky na původní

        Assert.Equal(sirka, Prvek(t).Width, 0.15f);
        Assert.Equal(font, Prvek(t).Style.FontSize!.Value, 0.15f);
    }

    // ── undo ──────────────────────────────────────────────────────────────────

    /// <summary>Zpětný krok vrátí rozměr stránky i okraje přesně a obsah na 0,1 mm.</summary>
    /// <remarks>
    /// Geometrie se vrací převráceným poměrem, takže se po cestě zaokrouhluje na desetinu
    /// milimetru - stejně jako u ručního škálování. Stránka a okraje se naopak vrací z odloženého
    /// snímku, tedy přesně.
    /// </remarks>
    [Fact]
    public void Undo_VratiStrankuPresne_AObsahNaDesetinu()
    {
        var t = Sablona(210f, 297f);
        var plan = Prepocet(t, 297f, 210f);
        float sirka = Prvek(t).Width, x = Prvek(t).X, font = Prvek(t).Style.FontSize!.Value;

        var cmd = new ChangePageFormatCommand(t, Pole(297f, 210f), plan);
        cmd.Execute();
        cmd.Undo();

        Assert.Equal(210f, t.PageSize.Width, 0.001f);
        Assert.Equal(297f, t.PageSize.Height, 0.001f);
        Assert.Equal(10f, t.PageSize.MarginLeft, 0.001f);
        Assert.Equal(sirka, Prvek(t).Width, 0.15f);
        Assert.Equal(x, Prvek(t).X, 0.15f);
        Assert.Equal(font, Prvek(t).Style.FontSize!.Value, 0.15f);
    }
}
