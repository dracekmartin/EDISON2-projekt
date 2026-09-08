using EDControls;
using EDControls.EDViewModel;
using NSubstitute;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Layout;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Ručně zadaná výška sekce musí držet stejné meze jako tažení za hranu (`B118`).
/// </summary>
/// <remarks>
/// Formulář sekce přichází z Caché a **nezná ostatní sekce šablony**, takže má statický rozsah
/// 5-9999 mm. Bez sražení na klientu šla mez zavedná pro tažení obejít číslem (požadavek Jan 4. 9.:
/// „at to je kompletní a nedá se to přehnat").
/// </remarks>
public class VyskaSekceZFormulareTests
{
    public VyskaSekceZFormulareTests() =>
        EDServices.Initialize(new EDServices(Substitute.For<IMLRest>(),
            Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()));

    /// <summary>A4 s okraji 10 mm (tisková plocha 277 mm) a zadaným počtem sekcí.</summary>
    private static TemplateDesignerViewModel Scena(params float[] vysky)
    {
        var vm = new TemplateDesignerViewModel();
        var t  = vm.State.CurrentTemplate;
        t.PageSize = new PageSize
        {
            Width = 210, Height = 297,
            MarginTop = 10, MarginBottom = 10, MarginLeft = 10, MarginRight = 10,
        };
        t.Sections.Clear();
        for (int i = 0; i < vysky.Length; i++)
            t.Sections.Add(new Section { Name = $"Sekce {i + 1}", DesignerHeight = vysky[i] });
        return vm;
    }

    [Fact]
    public void PodObsah_SeSraziNaObsah()
    {
        // Prvek koncí na 60 mm, uživatel zapíše 20 mm. V návrháři by se oříznul, ale vytiskl by se
        // celý - engine dává sekci max(nastavená výška, obsah).
        var vm = Scena(80f, 80f);
        var s  = vm.State.CurrentTemplate.Sections[0];
        s.Elements.Add(new TextElement { X = 5f, Y = 40f, Width = 40f, Height = 20f });

        s.DesignerHeight = 20f;
        Assert.True(vm.SrazVyskuSekceDoMezi(s));
        Assert.Equal(60f, s.DesignerHeight);
    }

    [Fact]
    public void NadZbytekStranky_SeSraziNaZbytek()
    {
        // Dvě sekce po 80 mm; do první se zapíše 9999 mm. Zbývá 277 - 80 = 197 mm.
        var vm = Scena(80f, 80f);
        var s  = vm.State.CurrentTemplate.Sections[0];

        s.DesignerHeight = 9999f;
        Assert.True(vm.SrazVyskuSekceDoMezi(s));
        Assert.Equal(197f, s.DesignerHeight!.Value, precision: 1);
    }

    [Fact]
    public void HodnotaVMezich_ZustaneNedotcena()
    {
        var vm = Scena(80f, 80f);
        var s  = vm.State.CurrentTemplate.Sections[0];

        s.DesignerHeight = 100f;
        Assert.False(vm.SrazVyskuSekceDoMezi(s));
        Assert.Equal(100f, s.DesignerHeight);
    }

    [Fact]
    public void DynamickaVyska_ZustaneDynamicka()
    {
        // Prázdná výška = počítá ji engine z dat; sražením na číslo by se ta dynamika tiše zrušila.
        var vm = Scena(80f, 80f);
        var s  = vm.State.CurrentTemplate.Sections[0];

        s.DesignerHeight = null;
        Assert.False(vm.SrazVyskuSekceDoMezi(s));
        Assert.Null(s.DesignerHeight);
    }

    [Fact]
    public void SrazeniSeRekneVeStavovemRadku()
    {
        // ⚠ Bez hlášky by uživatel viděl v poli jinou hodnotu, než zapsal, a nevěděl by proč.
        var vm = Scena(80f, 80f);
        var s  = vm.State.CurrentTemplate.Sections[0];

        s.DesignerHeight = 9999f;
        vm.SrazVyskuSekceDoMezi(s);

        Assert.Contains(s.Name, vm.StatusText);
        Assert.Contains("197", vm.StatusText);
    }

    [Fact]
    public void MezJeTaSamaJakoUTazeni()
    {
        // ⚠ Dvě různé meze by znamenaly, že číslo dovolí to, co tažení ne (nebo naopak) - a přesně
        // to byl ten stav do 4. 9. Test drží, že sražení kopíruje `SectionLayoutMath`, ne vlastní čísla.
        var vm = Scena(80f, 80f);
        var t  = vm.State.CurrentTemplate;
        var s  = t.Sections[0];
        s.Elements.Add(new TextElement { X = 5f, Y = 30f, Width = 40f, Height = 15f });

        s.DesignerHeight = 1f;
        vm.SrazVyskuSekceDoMezi(s);
        Assert.Equal(SectionLayoutMath.MinimalniVyskaProSekci(s), s.DesignerHeight!.Value, precision: 2);

        float strop = SectionLayoutMath.ZbyvajiciMistoMm(t, s);
        s.DesignerHeight = 9999f;
        vm.SrazVyskuSekceDoMezi(s);
        Assert.Equal(strop, s.DesignerHeight!.Value, precision: 2);
    }
}
