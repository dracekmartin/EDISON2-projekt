using System.ComponentModel;
using EDControls;
using EDControls.EDViewModel;
using NSubstitute;

namespace EDControls.Tests;

/// <summary>
/// Testy základní třídy <see cref="EDViewModel"/>. Každý test si vytváří vlastní
/// <c>EDServices</c> s mocky — žádný sdílený static state.
/// </summary>
public class EDViewModelTests
{
    private static EDServices MockServices(
        IMLRest? mlRest = null,
        IWindowNavigator? nav = null,
        IPopupDisplayer? popup = null) =>
        new(mlRest,
            nav ?? Substitute.For<IWindowNavigator>(),
            popup ?? Substitute.For<IPopupDisplayer>());

    [Fact]
    public void OnOk_FiresRequestCloseWithTrue()
    {
        var vm = new TestViewModel(MockServices());
        bool? received = null;
        vm.RequestClose += result => received = result;

        vm.OkCommand.Execute(null);

        Assert.True(received);
    }

    [Fact]
    public void CloseDialog_FiresRequestClose()
    {
        var vm = new TestViewModel(MockServices());
        bool? received = null;
        vm.RequestClose += result => received = result;

        vm.InvokeCloseDialog(false);

        Assert.False(received);
    }

    [Fact]
    public void CloseDialog_Null_FiresRequestCloseWithNull()
    {
        var vm = new TestViewModel(MockServices());
        bool? received = true;
        vm.RequestClose += result => received = result;

        vm.InvokeCloseDialog(null);

        Assert.Null(received);
    }

    [Fact]
    public void ActionInProgress_SetsAndResetsFlag()
    {
        var vm = new TestViewModel(MockServices());
        Assert.False(vm.IsActionInProgress);

        var scope = vm.ActionInProgress();
        Assert.True(vm.IsActionInProgress);

        scope.Dispose();
        Assert.False(vm.IsActionInProgress);
    }

    [Fact]
    public void ActionInProgress_FiresPropertyChanged()
    {
        var vm = new TestViewModel(MockServices());
        var changes = new List<string>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);

        using (vm.ActionInProgress()) { }

        Assert.Contains("IsActionInProgress", changes);
        Assert.Equal(2, changes.Count(c => c == "IsActionInProgress"));
    }

    [Fact]
    public void Set_FiresPropertyChanged_WhenValueChanges()
    {
        var vm = new TestViewModel(MockServices());
        var changes = new List<string>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);

        vm.Title = "Hello";

        Assert.Contains("Title", changes);
    }

    [Fact]
    public void Set_DoesNotFirePropertyChanged_WhenValueSame()
    {
        var vm = new TestViewModel(MockServices()) { Title = "Hello" };
        var changes = new List<string>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);

        vm.Title = "Hello";

        Assert.DoesNotContain("Title", changes);
    }

    [Fact]
    public void MLRest_WrittenOnServicesVisibleToVM()
    {
        var services = MockServices();
        var vm = new TestViewModel(services);
        var newMLRest = Substitute.For<IMLRest>();

        services.MLRest = newMLRest;

        Assert.Same(newMLRest, vm.MLRest);
    }

    [Fact]
    public void PopupDisplayer_ComesFromServices()
    {
        var mockPopup = Substitute.For<IPopupDisplayer>();
        var vm = new TestViewModel(MockServices(popup: mockPopup));

        Assert.Same(mockPopup, vm.PopupDisplayer);
    }

    [Fact]
    public void Command_ExceptionRoutedToMockPopupDisplayer()
    {
        var mockPopup = Substitute.For<IPopupDisplayer>();
        var vm = new TestViewModel(MockServices(popup: mockPopup));
        var ex = new InvalidOperationException("test error");

        var cmd = vm.CreateTestCommand(() => throw ex);
        cmd.Execute(null);

        mockPopup.Received(1).Error(ex);
    }

    [Fact]
    public void Command_NoExceptionDoesNotCallPopupDisplayer()
    {
        var mockPopup = Substitute.For<IPopupDisplayer>();
        var vm = new TestViewModel(MockServices(popup: mockPopup));

        var cmd = vm.CreateTestCommand(() => { });
        cmd.Execute(null);

        mockPopup.DidNotReceive().Error(Arg.Any<Exception>());
    }

    [Fact]
    public void StandardActions_WorksWithoutWpfApplication()
    {
        var vm = new TestViewModelWithActions(MockServices());
        vm.SetupActions();

        Assert.NotNull(vm.Actions);
        Assert.Equal(3, vm.Actions!.Count);
        Assert.Equal("Přidat", vm.Actions[0].Label);
        Assert.Equal("Upravit", vm.Actions[1].Label);
        Assert.Equal("Smazat", vm.Actions[2].Label);
    }

    [Fact]
    public void StandardActions_CommandsAreWiredToViewModel()
    {
        var vm = new TestViewModelWithActions(MockServices());
        vm.SetupActions();

        Assert.Same(vm.AddCommand, vm.Actions![0].Command);
        Assert.Same(vm.EditCommand, vm.Actions[1].Command);
        Assert.Same(vm.DeleteCommand, vm.Actions[2].Command);
    }

    [Fact]
    public void HasAccess_NullAccessTable_ReturnsTrue()
    {
        // Graceful degradation: bez načteného AccessTable (testy, pre-login) je vše povoleno.
        var vm = new TestViewModel(MockServices());

        Assert.True(vm.CheckAccess("COKOLIV"));
    }

    [Fact]
    public void HasAccess_PopulatedAccessTable_GatesByContains()
    {
        var services = MockServices();
        services.AccessTable = new AccessTable(["FORM_A"], isAllAccess: false);
        var vm = new TestViewModel(services);

        Assert.True(vm.CheckAccess("FORM_A"));
        Assert.False(vm.CheckAccess("FORM_B"));
    }

    [Fact]
    public void PersistedFormId_ViewModelSuffix_SwappedToWindow()
    {
        var vm = new FooViewModel(MockServices());

        Assert.Equal("EDISON2.FooWindow", vm.PublicPersistedFormId);
    }

    [Fact]
    public void PersistedFormId_NoViewModelSuffix_NoSwap()
    {
        // Název nekončí na "ViewModel" → žádná záměna suffixu, jen prefix EDISON2.
        var vm = new PlainWidget(MockServices());

        Assert.Equal("EDISON2.PlainWidget", vm.PublicPersistedFormId);
    }

    [Fact]
    public void Set_ReturnsTrue_OnChange_False_OnSame()
    {
        var vm = new TestViewModel(MockServices());

        Assert.True(vm.SetTitleReturning("A"));    // změna
        Assert.False(vm.SetTitleReturning("A"));   // stejná hodnota
        Assert.True(vm.SetTitleReturning("B"));    // znovu změna
    }

    /// <summary>Konkrétní subclass která vystavuje protected členy pro testy.</summary>
    private class TestViewModel(EDServices services) : EDViewModel.EDViewModel(services)
    {
        private string _title = "";
        public string Title
        {
            get => _title;
            set => Set(ref _title, value);
        }

        public void InvokeCloseDialog(bool? result) => CloseDialog(result);

        public RelayCommand CreateTestCommand(Action action) => Command(action);

        public bool CheckAccess(string elementId) => HasAccess(elementId);

        public bool SetTitleReturning(string value) => Set(ref _title, value);
    }

    private class TestViewModelWithActions(EDServices services) : EDViewModel.EDViewModel(services)
    {
        public void SetupActions() =>
            Actions = [StandardActions.Add(), StandardActions.Edit(), StandardActions.Delete()];
    }

    /// <summary>Název končí na "ViewModel" → PersistedFormId zamění suffix za "Window".</summary>
    private class FooViewModel(EDServices services) : EDViewModel.EDViewModel(services)
    {
        public string? PublicPersistedFormId => PersistedFormId;
    }

    /// <summary>Název nekončí na "ViewModel" → PersistedFormId nechá jméno beze změny.</summary>
    private class PlainWidget(EDServices services) : EDViewModel.EDViewModel(services)
    {
        public string? PublicPersistedFormId => PersistedFormId;
    }
}
