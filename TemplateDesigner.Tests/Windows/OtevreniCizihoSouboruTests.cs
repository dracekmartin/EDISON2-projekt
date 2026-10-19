using System.IO;
using EDControls;
using EDControls.EDViewModel;
using NSubstitute;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Otevření souboru, který vůbec není šablona (telemetrie 19. 10.: uživatel CB_CSADAD vybral
/// v importu PDF a dostal „Neplatný formát JSON: '%' is an invalid start of a value").
/// Je to chyba výběru, ne aplikace: srozumitelná informace a rozdělaná šablona zůstane v okně.
/// </summary>
public class OtevreniCizihoSouboruTests : IDisposable
{
    private readonly IPopupDisplayer _popup = Substitute.For<IPopupDisplayer>();
    private readonly string _soubor = Path.Combine(Path.GetTempPath(), $"td-ciziho-{Guid.NewGuid():N}.pdf");

    public OtevreniCizihoSouboruTests() =>
        EDServices.Initialize(new EDServices(Substitute.For<IMLRest>(), Substitute.For<IWindowNavigator>(), _popup));

    public void Dispose() => File.Delete(_soubor);

    /// <summary>Návrhář s neuloženým prvkem - přidaným tak, jak ho přidá uživatel.</summary>
    private static TemplateDesignerViewModel RozdelanaPrace()
    {
        var vm = new TemplateDesignerViewModel();
        vm.Execute(new TemplateDesigner.Commands.Implementations.AddElementCommand(
            vm.State.CurrentTemplate.Sections[0], new TextElement { Text = "Rozdělaná práce" }));
        Assert.True(vm.IsDirty);
        return vm;
    }

    [Fact]
    public async Task PdfJeInformaceARozdelanaPraceZustane()
    {
        await File.WriteAllTextAsync(_soubor, "%PDF-1.7\n%âãÏÓ\n1 0 obj");
        var vm = RozdelanaPrace();
        var sablona = vm.State.CurrentTemplate;

        await vm.OpenTemplateFromFileAsync(_soubor);

        _popup.Received(1).Info(Arg.Is<string>(t => t.Contains("je PDF, ne šablona")), Arg.Any<System.Windows.Window?>());
        _popup.DidNotReceive().Error(Arg.Any<Exception>(), Arg.Any<System.Windows.Window?>());
        Assert.Same(sablona, vm.State.CurrentTemplate);
        Assert.True(vm.IsDirty, "rozdělaná práce přestala být neuložená");
    }

    [Fact]
    public async Task JinySouborNezJsonJeTakyInformace()
    {
        await File.WriteAllTextAsync(_soubor, "Linka;Spoj;Cas\n330510;1;6:00");
        var vm = RozdelanaPrace();

        await vm.OpenTemplateFromFileAsync(_soubor);

        _popup.Received(1).Info(Arg.Is<string>(t => t.Contains("není šablona návrháře")), Arg.Any<System.Windows.Window?>());
        _popup.DidNotReceive().Error(Arg.Any<Exception>(), Arg.Any<System.Windows.Window?>());
    }

    /// <summary>Soubor, který vypadá jako šablona, ale nejde načíst, je dál chyba - ta nás zajímá.</summary>
    [Fact]
    public async Task PoskozenaSablonaZustavaChybou()
    {
        await File.WriteAllTextAsync(_soubor, "{ \"version\": \"1.15.0\", \"sections\": [ ");
        var vm = RozdelanaPrace();
        var sablona = vm.State.CurrentTemplate;

        await vm.OpenTemplateFromFileAsync(_soubor);

        _popup.Received(1).Error(Arg.Any<Exception>(), Arg.Any<System.Windows.Window?>());
        Assert.Same(sablona, vm.State.CurrentTemplate);
    }
}
