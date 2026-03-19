using System.ComponentModel;
using EDControls;
using EDControls.EDViewModel;
using NSubstitute;

namespace EDControls.Tests;

/// <summary>
/// Tests for the EDViewModel base class. Each test creates its own EDServices
/// with mocks — no shared static state, no ConfigureForTesting.
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
        bool? received = 42 > 0 ? true : null; // init to non-null
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
    public void MLRest_ComesFromServices()
    {
        var mockMLRest = Substitute.For<IMLRest>();
        var vm = new TestViewModel(MockServices(mlRest: mockMLRest));

        Assert.Same(mockMLRest, vm.MLRest);
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

    /// <summary>
    /// Concrete subclass that exposes protected members for testing.
    /// </summary>
    private class TestViewModel(EDServices services) : EDViewModel.EDViewModel(services)
    {
        private string _title = "";
        public string Title
        {
            get => _title;
            set => Set(ref _title, value);
        }

        public void InvokeCloseDialog(bool? result) => CloseDialog(result);
    }
}
