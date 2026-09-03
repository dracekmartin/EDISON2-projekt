using System.Windows.Input;
using EDControls;
using EDControls.EDViewModel;
using NSubstitute;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Změna velikosti prvku klávesnicí (Shift+šipky) a posun (šipky, Ctrl = po mřížce).
/// </summary>
/// <remarks>
/// Rozdělení kláves je z rozhodnutí Jana 1. 9.: Shift na velikosti je to, co uživatel zná ze starého
/// návrháře, a velký krok posunu tím zanikl bez ztráty - při výchozí mřížce 1 mm dělal totéž jako
/// šipka samotná, takže ho nikdo nemohl poznat. Myší se rozměr nedoladí, proto o to Pavel žádal.
/// </remarks>
public class VelikostKlavesniciTests
{
    public VelikostKlavesniciTests() =>
        EDServices.Initialize(new EDServices(Substitute.For<IMLRest>(),
            Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()));

    private static (TemplateDesignerViewModel Vm, TextElement El) Scena(
        float w = 40f, float? h = 20f, float x = 5f, float y = 5f)
    {
        var vm = new TemplateDesignerViewModel();
        var section = vm.State.CurrentTemplate.Sections[0];
        var el = new TextElement { X = x, Y = y, Width = w, Height = h };
        section.Elements.Add(el);
        vm.State.ActiveSection = section;
        vm.State.SelectedElements.Add(el);
        return (vm, el);
    }

    // ── Velikost ────────────────────────────────────────────────────────────

    [Fact]
    public void ShiftVpravo_RozsiriPrvekAPonechaLevyHorniRoh()
    {
        var (vm, el) = Scena();
        vm.ResizeByKey(Key.Right, gridStep: false);

        Assert.Equal(41f, el.Width);
        Assert.Equal(20f, el.Height);
        Assert.Equal(5f, el.X);   // levý horní roh se nehýbe
        Assert.Equal(5f, el.Y);
    }

    [Fact]
    public void ShiftDolu_ZvysiPrvek()
    {
        var (vm, el) = Scena();
        vm.ResizeByKey(Key.Down, gridStep: false);
        Assert.Equal(21f, el.Height);
    }

    [Fact]
    public void ShiftVlevoANahoru_Zmensuji()
    {
        var (vm, el) = Scena();
        vm.ResizeByKey(Key.Left, gridStep: false);
        vm.ResizeByKey(Key.Up, gridStep: false);
        Assert.Equal(39f, el.Width);
        Assert.Equal(19f, el.Height);
    }

    [Fact]
    public void PrvekSeNezmensiPodMinimum()
    {
        var (vm, el) = Scena(w: 3.5f, h: 2.5f);
        for (int i = 0; i < 5; i++)
        {
            vm.ResizeByKey(Key.Left, gridStep: false);
            vm.ResizeByKey(Key.Up, gridStep: false);
        }
        Assert.Equal(3f, el.Width);
        Assert.Equal(2f, el.Height);
    }

    [Fact]
    public void PrvekNepresahneSekci()
    {
        var (vm, el) = Scena(w: 40f, x: 150f);
        float sirkaStranky = vm.State.CurrentTemplate.PageSize.PrintableWidth;
        for (int i = 0; i < 100; i++) vm.ResizeByKey(Key.Right, gridStep: false);
        Assert.Equal(sirkaStranky - 150f, el.Width, 2);
    }

    [Fact]
    public void ZmenaVelikostiSeVratiZpetnymKrokem()
    {
        var (vm, el) = Scena();
        vm.ResizeByKey(Key.Right, gridStep: false);
        Assert.Equal(41f, el.Width);

        vm.State.Undo();
        Assert.Equal(40f, el.Width);
    }

    [Fact]
    public void ZamcenyPrvekSeNezmeni()
    {
        var (vm, el) = Scena();
        el.IsLocked = true;
        vm.ResizeByKey(Key.Right, gridStep: false);
        Assert.Equal(40f, el.Width);
    }

    [Fact]
    public void PrvekSVyskouZDat_SeSvisleNezmeniAReknesSeTo()
    {
        // Výšku počítá engine, takže fixovat ji tichou cestou by znamenalo přijít o dynamiku
        // bez varování - myš mu ze stejného důvodu svislé úchyty nenabízí.
        var (vm, el) = Scena(h: null);
        vm.ResizeByKey(Key.Down, gridStep: false);

        Assert.Null(el.Height);
        Assert.Contains("z dat", vm.StatusText);
    }

    [Fact]
    public void PrvekSVyskouZDat_SirkuMenitJde()
    {
        var (vm, el) = Scena(h: null);
        vm.ResizeByKey(Key.Right, gridStep: false);
        Assert.Equal(41f, el.Width);
        Assert.Null(el.Height);
    }

    // ── Které klávesy co dělají ─────────────────────────────────────────────
    // Bez téhle brány projde odpojení Shiftu od velikosti zeleně: ostatní testy volají
    // ResizeByKey přímo, takže na navázání kláves nikdy nesáhnou (ověřeno mutací 1. 9.).

    [Theory]
    [InlineData(false, false, ArrowKeyAction.Move)]
    [InlineData(true,  false, ArrowKeyAction.MoveByGrid)]
    [InlineData(false, true,  ArrowKeyAction.Resize)]
    public void SipkyMajiPrirazenouAkci(bool ctrl, bool shift, ArrowKeyAction ocekavano) =>
        Assert.Equal(ocekavano, TemplateDesignerViewModel.ResolveArrowAction(ctrl, shift));

    // ── Posun ───────────────────────────────────────────────────────────────

    [Fact]
    public void SipkaBezModifikatoru_PosuneOMilimetr()
    {
        var (vm, el) = Scena();
        vm.MoveByKey(Key.Right, gridStep: false);
        Assert.Equal(6f, el.X);
        Assert.Equal(40f, el.Width);   // posun velikost nemění
    }

    [Fact]
    public void CtrlSipka_PosunePoMrizce()
    {
        var (vm, el) = Scena();
        vm.GridSize = 5f;
        vm.MoveByKey(Key.Down, gridStep: true);
        Assert.Equal(10f, el.Y);
    }
}
