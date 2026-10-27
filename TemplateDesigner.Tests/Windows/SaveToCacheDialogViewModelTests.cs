using EDControls;
using EDControls.EDViewModel;
using NSubstitute;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// „Uložit jako" agendu šablony jen ukáže (`B194a` C10): změna tam uložila šablonu s prvky jedné agendy
/// pod jinou. Agenda okna proto nemá setter - kdo by ho vrátil, otevře tu cestu znovu.
/// </summary>
public class SaveToCacheDialogViewModelTests
{
    public SaveToCacheDialogViewModelTests() =>
        EDServices.Initialize(new EDServices(Substitute.For<IMLRest>(),
            Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()));

    [Fact]
    public void Agenda_JeJenKeCteni()
    {
        var vlastnost = typeof(SaveToCacheDialogViewModel).GetProperty(nameof(SaveToCacheDialogViewModel.AgendaCode))!;

        Assert.Null(vlastnost.SetMethod);
    }

    [Fact]
    public void BezNazvu_UkazeKodAgendy()
    {
        var vm = new SaveToCacheDialogViewModel("Šablona", "GENERAL");

        Assert.Equal("GENERAL", vm.AgendaLabel);
        Assert.Equal("Obecná (bez databáze)", new SaveToCacheDialogViewModel("Šablona", "GENERAL", "Obecná (bez databáze)").AgendaLabel);
    }
}
