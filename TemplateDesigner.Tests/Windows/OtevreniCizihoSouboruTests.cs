using System.IO;
using EDControls;
using EDControls.EDViewModel;
using NSubstitute;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Serialization;

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

    // Fiktivní kód jako v SkrytaAgendaTests - test nezávisí na tom, které agendy jsou na serveru.
    // ⚠️ Načtená šablona nesmí mít agendu, pro kterou návrhář stahuje nabídku dat: na stroji
    // s lokálním připojením (templatedesigner.appsettings.local.json) by test sahal na server.
    private static readonly AgendaRegistryEntry Obecna = new("GENERAL", "Obecná (bez dat)", "");
    private static readonly AgendaRegistryEntry Zastavkova =
        new("TEST_ZASTAVKOVY", "Zastávkový (test)", "ALVA.Test.Zastavkovy", ElementKind: "STOP");

    private static TemplateDesignerViewModel Navrhar(AgendaRegistryEntry agenda) =>
        new(null, null, [Obecna, Zastavkova], agenda.Code);

    private Task UlozSablonu(string? agenda, TemplateElement prvek)
    {
        var sekce = new Section { Name = "Obsah" };
        sekce.Elements.Add(prvek);
        var sablona = new Template { Name = "Ze souboru", AgendaCode = agenda };
        sablona.Sections.Add(sekce);
        return File.WriteAllTextAsync(_soubor, TemplateSerializer.Serialize(sablona));
    }

    /// <summary>Šablona s prvkem, který do její agendy nepatří, se nenačte (do 30. 10. se načetla
    /// a zůstalo jen upozornění ve stavovém řádku).</summary>
    [Fact]
    public async Task PrvekJineAgendyJeInformaceARozdelanaPraceZustane()
    {
        await UlozSablonu(Zastavkova.Code, new LineTableElement());
        var vm = Navrhar(Obecna);
        var sablona = vm.State.CurrentTemplate;

        await vm.OpenTemplateFromFileAsync(_soubor);

        _popup.Received(1).Info(Arg.Is<string>(t => t.Contains(ElementNames.ForUser(new LineTableElement()))),
            Arg.Any<System.Windows.Window?>());
        Assert.Same(sablona, vm.State.CurrentTemplate);
    }

    /// <summary>Import nahradí celou šablonu i s agendou, stejně jako otevření z databáze - prvek
    /// se nikdy nevloží do šablony jiné agendy.</summary>
    [Fact]
    public async Task SablonaJineAgendyPrepneAgendu()
    {
        await UlozSablonu(null, new TextElement { Text = "Obecná šablona" });
        var vm = Navrhar(Zastavkova);
        Assert.Equal(Zastavkova.Code, vm.State.CurrentTemplate.AgendaCode);

        await vm.OpenTemplateFromFileAsync(_soubor);

        _popup.DidNotReceive().Info(Arg.Any<string>(), Arg.Any<System.Windows.Window?>());
        Assert.True(string.IsNullOrEmpty(vm.State.CurrentTemplate.AgendaCode));
        Assert.Contains(vm.State.CurrentTemplate.Sections.SelectMany(s => s.Elements),
            p => p is TextElement { Text: "Obecná šablona" });
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
