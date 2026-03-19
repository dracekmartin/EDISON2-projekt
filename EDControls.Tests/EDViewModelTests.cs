using System.ComponentModel;
using EDControls.EDViewModel;
using NSubstitute;

namespace EDControls.Tests;

/// <summary>
/// Tests for the EDViewModel base class. These run without a WPF Application
/// context — key bindings and resource lookups (StandardActions) are skipped.
/// </summary>
public class EDViewModelTests : IDisposable
{
    public EDViewModelTests()
    {
        // Reset static state before each test
        EDViewModel.EDViewModel.ConfigureForTesting(
            Substitute.For<IWindowNavigator>(),
            Substitute.For<IPopupDisplayer>());
    }

    public void Dispose()
    {
        // Reset to defaults after each test
        EDViewModel.EDViewModel.ConfigureForTesting(
            Substitute.For<IWindowNavigator>(),
            Substitute.For<IPopupDisplayer>());
    }

    [Fact]
    public void OnOk_FiresRequestCloseWithTrue()
    {
        var vm = new TestViewModel();
        bool? received = null;
        vm.RequestClose += result => received = result;

        vm.OkCommand.Execute(null);

        Assert.True(received);
    }

    [Fact]
    public void CloseDialog_FiresRequestClose()
    {
        var vm = new TestViewModel();
        bool? received = null;
        vm.RequestClose += result => received = result;

        vm.InvokeCloseDialog(false);

        Assert.False(received);
    }

    [Fact]
    public void CloseDialog_Null_FiresRequestCloseWithNull()
    {
        var vm = new TestViewModel();
        bool? received = 42 > 0 ? true : null; // init to non-null
        vm.RequestClose += result => received = result;

        vm.InvokeCloseDialog(null);

        Assert.Null(received);
    }

    [Fact]
    public void ActionInProgress_SetsAndResetsFlag()
    {
        var vm = new TestViewModel();
        Assert.False(vm.IsActionInProgress);

        var scope = vm.ActionInProgress();
        Assert.True(vm.IsActionInProgress);

        scope.Dispose();
        Assert.False(vm.IsActionInProgress);
    }

    [Fact]
    public void ActionInProgress_FiresPropertyChanged()
    {
        var vm = new TestViewModel();
        var changes = new List<string>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);

        using (vm.ActionInProgress()) { }

        Assert.Contains("IsActionInProgress", changes);
        // Should fire twice: once for true, once for false
        Assert.Equal(2, changes.Count(c => c == "IsActionInProgress"));
    }

    [Fact]
    public void Set_FiresPropertyChanged_WhenValueChanges()
    {
        var vm = new TestViewModel();
        var changes = new List<string>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);

        vm.Title = "Hello";

        Assert.Contains("Title", changes);
    }

    [Fact]
    public void Set_DoesNotFirePropertyChanged_WhenValueSame()
    {
        var vm = new TestViewModel { Title = "Hello" };
        var changes = new List<string>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);

        vm.Title = "Hello";

        Assert.DoesNotContain("Title", changes);
    }

    [Fact]
    public void MLRest_IsAccessibleToSubclasses()
    {
        var mockMLRest = Substitute.For<IMLRest>();
        var vm = new TestViewModel();
        vm.SetMLRest(mockMLRest);

        Assert.Same(mockMLRest, vm.MLRest);
    }

    [Fact]
    public void PopupDisplayer_UsesConfiguredInstance()
    {
        var mockPopup = Substitute.For<IPopupDisplayer>();
        EDViewModel.EDViewModel.ConfigureForTesting(popupDisplayer: mockPopup);

        var vm = new TestViewModel();

        Assert.Same(mockPopup, vm.PopupDisplayer);
    }

    /// <summary>
    /// Concrete subclass that exposes protected members for testing.
    /// </summary>
    private class TestViewModel : EDViewModel.EDViewModel
    {
        private string _title = "";
        public string Title
        {
            get => _title;
            set => Set(ref _title, value);
        }

        public void InvokeCloseDialog(bool? result) => CloseDialog(result);

        public void SetMLRest(IMLRest mlRest) => MLRest = mlRest;
    }
}
