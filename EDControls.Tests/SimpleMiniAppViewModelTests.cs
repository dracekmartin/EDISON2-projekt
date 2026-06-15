using System.Windows;
using EDControls.EDViewModel;
using EDControls.MiniApps;
using NSubstitute;

namespace EDControls.Tests;

public class SimpleMiniAppViewModelTests
{
    private static EDServices MockServices() =>
        new(null, Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>());

    [Fact]
    public void Title_ReturnsProvidedTitle()
    {
        var vm = new SimpleMiniAppViewModel(MockServices(), "Nástěnka", DummyFactory);

        Assert.Equal("Nástěnka", vm.Title);
    }

    [Fact]
    public void ContentFactory_StoresProvidedFactoryUninvoked()
    {
        // VM továrnu jen drží — invokuje ji až view. (Vlastní instanci FrameworkElement nelze
        // vytvořit mimo STA vlákno, ale tady o to nejde: kontrolujeme identitu delegátu.)
        Func<FrameworkElement> factory = DummyFactory;
        var vm = new SimpleMiniAppViewModel(MockServices(), "x", factory);

        Assert.Same(factory, vm.ContentFactory);
    }

    private static FrameworkElement DummyFactory() => throw new InvalidOperationException("nemá se volat");

    [Fact]
    public void PersistedFormId_IsNull()
    {
        // Mini-app: geometrii drží layout docku, ne WindowStatePersistence.
        var vm = new ExposingMiniApp(MockServices());

        Assert.Null(vm.PersistedFormIdForTest);
    }

    private sealed class ExposingMiniApp(EDServices services) : EDMiniAppViewModel(services)
    {
        public override string Title => "x";
        public string? PersistedFormIdForTest => PersistedFormId;
    }
}
