using EDControls;
using EDControls.EDViewModel;
using NSubstitute;
using TemplateDesigner.Commands.Implementations;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Services;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Úvodní nabídka přes prázdné plátno po spuštění (zkušební verze, nápad Jan 29. 10.): co nabídne
/// a že zmizí, jakmile uživatel začne pracovat - stálého uživatele nesmí zdržovat.
/// </summary>
public class UvodniNabidkaTests
{
    public UvodniNabidkaTests() =>
        EDServices.Initialize(new EDServices(Substitute.For<IMLRest>(),
            Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()));

    /// <summary>Návrhář bez serveru se startovní agendou, která má hotovou šablonu M-line.</summary>
    private static TemplateDesignerViewModel Vm(string agenda = "LINKOVY_JR_AD")
    {
        var vm = new TemplateDesignerViewModel();
        vm.State.CurrentTemplate.AgendaCode = agenda;
        return vm;
    }

    [Fact]
    public async Task Nabidne_HotovouSablonuAgendy()
    {
        var vm = Vm();
        await vm.ShowStartPanelAsync();

        Assert.True(vm.StartPanel.IsVisible);
        Assert.True(vm.StartPanel.HasSystemTemplate);
        Assert.Equal(EmbeddedTemplates.ForAgenda(EmbeddedTemplates.SystemFolder, "LINKOVY_JR_AD")!.Name,
            vm.StartPanel.SystemTemplateName);
    }

    /// <summary>Panel přímo: návrhář v testu si spojení vytvoří z nastavení v programu, takže
    /// „bez serveru" přes něj nastavit nejde.</summary>
    [Fact]
    public void BezServeru_NenabidneDatabazi()
    {
        var panel = new StartPanelViewModel(() => Task.CompletedTask, () => Task.CompletedTask, () => Task.CompletedTask);
        panel.Show("Obecná (bez dat)", canOpenFromDatabase: false, systemTemplateName: null);

        Assert.False(panel.OpenFromDatabaseCommand.CanExecute(null));
        Assert.False(panel.StartFromSystemTemplateCommand.CanExecute(null));
        Assert.True(panel.StartBlankCommand.CanExecute(null));
    }

    [Fact]
    public async Task AgendaBezHotoveSablony_NabidneJenPrazdnou()
    {
        var vm = Vm("GENERAL");
        await vm.ShowStartPanelAsync();

        Assert.True(vm.StartPanel.IsVisible);
        Assert.False(vm.StartPanel.HasSystemTemplate);
    }

    [Fact]
    public async Task PrazdnaSablona_NabidkuZavre()
    {
        var vm = Vm();
        await vm.ShowStartPanelAsync();

        vm.StartPanel.StartBlankCommand.Execute(null);

        Assert.False(vm.StartPanel.IsVisible);
    }

    [Fact]
    public async Task VymenaSablony_NabidkuZavre()
    {
        var vm = Vm();
        await vm.ShowStartPanelAsync();

        vm.State.CurrentTemplate = new Template();

        Assert.False(vm.StartPanel.IsVisible);
    }

    [Fact]
    public async Task PrvniUprava_NabidkuZavre()
    {
        var vm = Vm();
        await vm.ShowStartPanelAsync();

        vm.Execute(new AddElementCommand(vm.State.CurrentTemplate.Sections[0], new TextElement { Text = "A" }));

        Assert.False(vm.StartPanel.IsVisible);
    }

    [Fact]
    public async Task HotovaSablona_SeOtevreJakoKopie()
    {
        var vm = Vm();
        await vm.ShowStartPanelAsync();

        await vm.StartFromSystemTemplateAsync();

        // Bez spojení přijde šablona z programu, se spojením z databáze M-line (může být novější).
        Assert.Contains(vm.State.CurrentTemplate.Sections, s => s.Elements.Count > 0);
        Assert.Equal("LINKOVY_JR_AD", vm.State.CurrentTemplate.AgendaCode);
        // Systémová šablona je jen pro čtení: Ctrl+S ji musí uložit jako novou šablonu firmy.
        Assert.False(vm.IsCacheBacked);
        Assert.False(vm.StartPanel.IsVisible);
    }
}
