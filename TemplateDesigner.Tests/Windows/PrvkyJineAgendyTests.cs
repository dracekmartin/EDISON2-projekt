using System.Windows;
using EDControls;
using EDControls.EDViewModel;
using NSubstitute;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Prvek jízdního řádu patří jen do šablony agendy, která ho podporuje - paleta ho jinde nenabízí,
/// ale vložení ze schránky a vlastní šablona sekce agendu nekontrolovaly. Seznam zastávek vložený
/// do linkového JŘ kreslil náhled nad daty jiného tvaru a shodil ho (POVED 8. 10.).
/// </summary>
public class PrvkyJineAgendyTests
{
    private readonly IPopupDisplayer _popup = Substitute.For<IPopupDisplayer>();

    public PrvkyJineAgendyTests() =>
        EDServices.Initialize(new EDServices(Substitute.For<IMLRest>(),
            Substitute.For<IWindowNavigator>(), _popup));

    // Fiktivní kódy - testy nezávisí na tom, které agendy server zrovna nabízí.
    private static readonly AgendaRegistryEntry Zastavkova =
        new("TEST_STOP", "Zastávková (test)", "ALVA.Test.Stop", ElementKind: "STOP");

    private static readonly AgendaRegistryEntry Linkova =
        new("TEST_LINE", "Linková (test)", "ALVA.Test.Line", ElementKind: "LINE");

    private static TemplateDesignerViewModel Vm() => new(null, null, [Zastavkova, Linkova], Zastavkova.Code);

    private static Section Otevri(TemplateDesignerViewModel vm, string? agenda, params TemplateElement[] prvky)
    {
        var sekce = new Section();
        sekce.Elements.AddRange(prvky);
        vm.State.CurrentTemplate = new Template { AgendaCode = agenda, Sections = [sekce] };
        vm.State.ActiveSection = sekce;
        return sekce;
    }

    [Fact]
    public void ZeSchrankySePrvekJineAgendyNevlozi()
    {
        var vm = Vm();
        var seznam = new StopListElement();
        var text = new TextElement { Text = "A" };
        Otevri(vm, Zastavkova.Code, seznam, text);
        vm.State.SelectedElements.AddRange([seznam, text]);
        vm.CopySelected();

        var cil = Otevri(vm, Linkova.Code);
        vm.PasteClipboard();

        Assert.DoesNotContain(cil.Elements, p => p is StopListElement);
        Assert.Contains(cil.Elements, p => p is TextElement);
        _popup.Received().Info(Arg.Is<string>(s => s.Contains("Seznam zastávek")), Arg.Any<Window?>());
    }

    [Fact]
    public void DoStejneAgendySeVlozi()
    {
        var vm = Vm();
        var seznam = new StopListElement();
        Otevri(vm, Zastavkova.Code, seznam);
        vm.State.SelectedElements.Add(seznam);
        vm.CopySelected();

        var cil = Otevri(vm, Zastavkova.Code);
        vm.PasteClipboard();

        Assert.Contains(cil.Elements, p => p is StopListElement);
        _popup.DidNotReceive().Info(Arg.Any<string>(), Arg.Any<Window?>());
    }

    [Fact]
    public void SablonaSekceVynechaPrvekJineAgendy()
    {
        var vm = Vm();
        Otevri(vm, Linkova.Code);
        var sekce = new Section();
        sekce.Elements.AddRange([new StopListElement(), new DepartureGridElement(), new LineTableElement(), new TextElement()]);

        vm.OdeberPrvkyJineAgendy(sekce);

        Assert.Equal([typeof(LineTableElement), typeof(TextElement)], sekce.Elements.Select(p => p.GetType()));
    }

    /// <summary>Hotová šablona může prvek jiné agendy mít z doby, kdy se nekontrolovalo -
    /// návrhář ho nesmaže sám, ale upozorní na něj a klik ho vybere.</summary>
    [Fact]
    public void ExistujiciPrvekJineAgendyHlasi()
    {
        var vm = Vm();
        var seznam = new StopListElement();
        Otevri(vm, Linkova.Code, seznam, new LineTableElement());

        Assert.Contains(vm.Hlaseni, h => h.PrvekId == seznam.Id);
    }

    /// <summary>Neznámá agenda (registr se nenačetl, server nedostupný) nesmí blokovat nic -
    /// jinak by bez spojení nešel vložit ani Seznam zastávek do zastávkového JŘ.</summary>
    [Fact]
    public void NeznamaAgendaNicNeblokuje()
    {
        var vm = new TemplateDesignerViewModel(null, null, []);
        var seznam = new StopListElement();
        Otevri(vm, "NEZNAMA", seznam);
        vm.State.SelectedElements.Add(seznam);
        vm.CopySelected();

        var cil = Otevri(vm, "NEZNAMA");
        vm.PasteClipboard();

        Assert.Contains(cil.Elements, p => p is StopListElement);
    }
}
