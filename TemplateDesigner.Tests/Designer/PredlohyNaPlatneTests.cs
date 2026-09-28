using TemplateDesigner.Designer;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Layout;

namespace TemplateDesigner.Tests.Designer;

/// <summary>
/// Plátno návrháře ukazuje jednu předlohu stránky - tu otevřenou v přepínači (`B184`).
/// </summary>
/// <remarks>
/// Listy pod sebou (první pokus 25. 9.) míchaly výběr sekce a stránky; s přepínačem je otevřená
/// vždy ta předloha, kterou uživatel vidí (rozhodnutí Jan 25. 9.).
/// </remarks>
public class PredlohyNaPlatneTests
{
    private const float CanvasWidth = 1200f;

    private static (DesignerCanvas Canvas, DesignerState State, Section Hlavicka, Section Lic, Section Rub) Scena()
    {
        var hlavicka = new Section
        {
            Name = "Hlavička", DesignerHeight = 20, Role = SectionRole.Header,
            PrintBehavior = SectionPrintBehavior.OnEveryPage,
        };
        var lic = new Section { Name = "Líc", DesignerHeight = 100 };
        var rub = new Section { Name = "Rub", DesignerHeight = 100, ZacitNaNoveStrance = true };
        var template = new Template
        {
            PageSize = new PageSize { Width = 148, Height = 210, MarginTop = 5, MarginBottom = 5, MarginLeft = 5, MarginRight = 5 },
        };
        template.Sections.Clear();
        template.Sections.AddRange([hlavicka, lic, rub]);
        var state = new DesignerState { CurrentTemplate = template };
        return (new DesignerCanvas(state), state, hlavicka, lic, rub);
    }

    [Fact]
    public void PlatnoUkazujeJenOtevrenouPredlohu()
    {
        var (canvas, state, hlavicka, lic, rub) = Scena();

        state.Predloha = 1;
        var sekce = canvas.GetSectionOrigins(CanvasWidth).Select(t => t.Section).ToList();

        Assert.Contains(rub, sekce);
        Assert.DoesNotContain(lic, sekce);
        Assert.Contains(hlavicka, sekce);   // společná sekce je na každé předloze a jde upravovat
    }

    /// <summary>Rub leží pod hlavičkou na horní hraně svého listu, ne pod lícem.</summary>
    [Fact]
    public void RubZacinaNahoreSvehoListu()
    {
        var (canvas, state, hlavicka, _, rub) = Scena();

        state.Predloha = 1;
        var origins = canvas.GetSectionOrigins(CanvasWidth).ToDictionary(t => t.Section, t => t.OriginY);

        Assert.Equal(origins[hlavicka] + canvas.SectionHeightPx(hlavicka), origins[rub], precision: 2);
    }

    [Fact]
    public void VyskaPlatna_JenOtevrenyList()
    {
        var (canvas, state, _, _, _) = Scena();

        state.Predloha = 1;

        Assert.Equal(210f * canvas.MmToPx, canvas.TotalPaperHeightPx(), precision: 1);
    }

    [Fact]
    public void PredlohaMimoRozsah_SeSrovnaNaPosledni()
    {
        var (canvas, state, _, _, rub) = Scena();

        state.Predloha = 7;

        Assert.Contains(rub, canvas.GetSectionOrigins(CanvasWidth).Select(t => t.Section));
    }

    /// <summary>
    /// Náhled u šablony o víc stránkách ukazuje všechny stránky; po vypnutí „Náhled všech stránek"
    /// jen stránky z otevřené stránky šablony (rozhodnutí Jan 28. 9.).
    /// </summary>
    [Fact]
    public void Nahled_VsechnyStrankyVychozi_NeboJenOtevrena()
    {
        var (_, state, _, _, _) = Scena();
        var nahled = new PreviewCanvas(state);

        state.Predloha = 1;
        Assert.True(state.NahledVsechStranek);
        float vsechny = nahled.CalculatePageHeightPx(1f);
        state.NahledVsechStranek = false;
        float jenRub = nahled.CalculatePageHeightPx(1f);

        float strankaPx = 210f * 96f / 25.4f;
        const float mezeraMeziStrankamiPx = 16f;   // PreviewCanvas.PageGapPx
        Assert.Equal(strankaPx + mezeraMeziStrankamiPx, vsechny - jenRub, precision: 0);   // o jednu stránku víc
    }

    [Theory]
    [InlineData(100f, SectionOverflowBehavior.KeepTogether, false)]
    [InlineData(null, SectionOverflowBehavior.KeepTogether, true)]    // výška podle obsahu
    [InlineData(100f, SectionOverflowBehavior.SplitAcrossPages, true)]
    public void PodleDat_OdvozeneZObsahu(float? vyska, SectionOverflowBehavior preteceni, bool podleDat)
    {
        var sekce = new Section { DesignerHeight = vyska, OverflowBehavior = preteceni };

        Assert.Equal(podleDat, DesignerPages.JePodleDat([sekce]));
    }
}
