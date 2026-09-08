using EDControls;
using EDControls.EDViewModel;
using NSubstitute;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Layout;


namespace TemplateDesigner.Tests.Layout;

/// <summary>
/// Sekce, které se sečtou o **setinu milimetru** nad tiskovou plochu, nesmí vyrobit stránku navíc.
/// </summary>
/// <remarks>
/// <para>Nález Jan 7. 9. při stavbě šablony na školení (`templates/bug_ukazka.json`): tři prázdné
/// sekce 20 + 106,1203 + 21,9 mm na A5 na šířku bez okrajů, tedy 148,0203 proti tiskové ploše
/// 148 mm. Poslední sekce se „nevešla" o 0,02 mm, odešla na druhou stránku a uživatel viděl
/// zdvojený výstup, který si ničím nevysvětlil.</para>
/// <para>⚠️ **Ten zbytek tam vzniká sám.** Přidání sekce krátí ostatní poměrem (`B129`), takže
/// z kulatých výšek udělá 21,8797; tažení hrany se pak zarazí přesně na zbytku stránky
/// (106,1203) a dialog výšky sekce dorovnává jen rozdíly nad 0,05 mm - pod tou mezí smetí projde.
/// Proto je mez **jedna konstanta** (<see cref="SectionLayoutMath.TolerancePrekryvuMm"/>): co pustí
/// dialog, to musí engine unést.</para>
/// </remarks>
public class VyskySekciNaHranuStrankyTests
{
    public VyskySekciNaHranuStrankyTests() =>
        EDServices.Initialize(new EDServices(Substitute.For<IMLRest>(),
            Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()));

    /// <summary>A5 na šířku bez okrajů = tisková plocha 148 mm; sekce bez prvků, výšky dle zadání.</summary>
    private static Template Sablona(params float[] vysky)
    {
        var t = new Template
        {
            PageSize = new PageSize { Width = 210, Height = 148 },
        };
        t.Sections.Clear();
        for (int i = 0; i < vysky.Length; i++)
            t.Sections.Add(new Section
            {
                Name = $"Sekce {i + 1}",
                PrintBehavior = SectionPrintBehavior.Once,
                OverflowBehavior = SectionOverflowBehavior.KeepTogether,
                DesignerHeight = vysky[i],
            });
        return t;
    }

    private static int PocetStranek(Template t) =>
        new PageLayoutEngine(new FixedHeightCalculator(null)).Layout(t, null).TotalPages;

    /// <summary>Jádro nálezu: 148,0203 mm obsahu na 148 mm papíru je pořád jedna stránka.</summary>
    [Fact]
    public void SoucetOSetinuMilimetruNadPlochu_ZustaneJednaStranka()
    {
        Assert.Equal(1, PocetStranek(Sablona(20f, 106.1203f, 21.9f)));
    }

    /// <summary>Kulatý součet přesně na plochu - kontrola, že se to neláme ani na float smetí.</summary>
    [Fact]
    public void SoucetPresneNaPlochu_ZustaneJednaStranka()
    {
        Assert.Equal(1, PocetStranek(Sablona(20f, 106.1f, 21.9f)));
    }

    /// <summary>⚠️ Tolerance nesmí spolknout skutečné přetečení - milimetr navíc je druhá stránka.</summary>
    [Fact]
    public void SoucetOMilimetrNadPlochu_JeDruhaStranka()
    {
        Assert.Equal(2, PocetStranek(Sablona(20f, 107f, 21.9f)));
    }

    /// <summary>
    /// Dialog výšky sekce **vždy** srazí hodnotu nad strop, i když je rozdíl pod hlásicí mezí -
    /// jinak se smetí drží v šabloně a engine ho musí tolerovat i po ručním zadání.
    /// </summary>
    [Fact]
    public void DialogSraziIRozdilPodHlasiciMezi()
    {
        var vm = new TemplateDesignerViewModel();
        vm.State.CurrentTemplate = Sablona(20f, 106.1203f, 21.9f);
        var posledni = vm.State.CurrentTemplate.Sections[^1];

        vm.SrazVyskuSekceDoMezi(posledni);

        float strop = SectionLayoutMath.ZbyvajiciMistoMm(vm.State.CurrentTemplate, posledni);
        Assert.Equal(strop, posledni.DesignerHeight!.Value, precision: 3);
    }
}
