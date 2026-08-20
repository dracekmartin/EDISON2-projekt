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
    public void Ctor_DoesNotInvokeContentFactory()
    {
        // Kontrakt: VM továrnu jen drží, invokuje ji až view (viz OnContentCreated) - kvůli
        // vícenásobnému otevření a dock/float re-parentingu. Kdyby ji ctor zavolal, throwing
        // factory by vybublala výjimku.
        var ex = Record.Exception(() => new SimpleMiniAppViewModel(MockServices(), "x", DummyFactory));

        Assert.Null(ex);
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
