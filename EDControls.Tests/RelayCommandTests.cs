using EDControls.EDViewModel;
using NSubstitute;

namespace EDControls.Tests;

// Sdílí serializovanou kolekci s ostatními třídami sahajícími na EDServices.Current
// (Initialize/Reset ve *_FallsBackToEDServicesCurrent testech) — viz SharedStaticStateCollection.
[Collection(SharedStaticStateCollection.Name)]
public class RelayCommandTests
{
    private static EDServices ServicesWith(IPopupDisplayer popup) =>
        new(mlRest: null,
            windowNavigator: Substitute.For<IWindowNavigator>(),
            popupDisplayer: popup);

    // ---------- Non-generic ----------

    [Fact]
    public void Execute_InvokesAction()
    {
        var invoked = false;
        var cmd = new RelayCommand(() => invoked = true);

        cmd.Execute(null);

        Assert.True(invoked);
    }

    [Fact]
    public void CanExecute_NullPredicate_AlwaysTrue()
    {
        var cmd = new RelayCommand(() => { });

        Assert.True(cmd.CanExecute(null));
    }

    [Fact]
    public void CanExecute_PredicateRespected()
    {
        var allowed = false;
        var cmd = new RelayCommand(() => { }, () => allowed);

        Assert.False(cmd.CanExecute(null));
        allowed = true;
        Assert.True(cmd.CanExecute(null));
    }

    [Fact]
    public void Execute_Exception_RoutedToInjectedPopup()
    {
        var popup = Substitute.For<IPopupDisplayer>();
        var boom = new InvalidOperationException("boom");
        var cmd = new RelayCommand(() => throw boom, canExecute: null, popupDisplayer: popup);

        cmd.Execute(null);

        popup.Received(1).Error(boom);
    }

    [Fact]
    public void Execute_Exception_NullPopup_FallsBackToEDServicesCurrent()
    {
        // Fallback z `new PopupDisplayer()` (orphan) na composition root přes
        // EDServices.Current.PopupDisplayer — ověřujeme, že sahá na Current, ne na vlastní instanci.
        var popup = Substitute.For<IPopupDisplayer>();
        EDServices.Initialize(ServicesWith(popup));
        try
        {
            var boom = new InvalidOperationException("boom");
            var cmd = new RelayCommand(() => throw boom);

            cmd.Execute(null);

            popup.Received(1).Error(boom);
        }
        finally
        {
            EDServices.Reset();
        }
    }

    // ---------- Generic ----------

    [Fact]
    public void Generic_Execute_PassesParameter()
    {
        string? received = null;
        var cmd = new RelayCommand<string>(x => received = x);

        cmd.Execute("hello");

        Assert.Equal("hello", received);
    }

    [Fact]
    public void Generic_Execute_WrongParameterType_NoOp()
    {
        // parameter is T t je false → execute se neprovolá. Žádná výjimka, žádný popup.
        var invoked = false;
        var popup = Substitute.For<IPopupDisplayer>();
        var cmd = new RelayCommand<string>(_ => invoked = true, canExecute: null, popupDisplayer: popup);

        cmd.Execute(42);

        Assert.False(invoked);
        popup.DidNotReceive().Error(Arg.Any<Exception>());
    }

    [Fact]
    public void Generic_CanExecute_WrongParameterType_False()
    {
        var cmd = new RelayCommand<string>(_ => { }, _ => true);

        Assert.False(cmd.CanExecute(42));
    }

    [Fact]
    public void Generic_NullPredicate_CanExecuteTrueForWrongType_ButExecuteNoOps()
    {
        // Kvórk: bez canExecute se typová kontrola dělá až uvnitř Execute, ne v CanExecute.
        // CanExecute proto vrátí true i pro špatný typ → tlačítko je povolené, ale klik nic neudělá.
        var invoked = false;
        var cmd = new RelayCommand<string>(_ => invoked = true);

        Assert.True(cmd.CanExecute(42));
        cmd.Execute(42);
        Assert.False(invoked);
    }

    [Fact]
    public void Generic_Predicate_CanExecuteNull_False()
    {
        // null není T (string) → parameter is T t je false, predikát se ani nevolá.
        var cmd = new RelayCommand<string>(_ => { }, _ => true);

        Assert.False(cmd.CanExecute(null));
    }

    [Fact]
    public void Generic_Execute_Exception_RoutedToInjectedPopup()
    {
        var popup = Substitute.For<IPopupDisplayer>();
        var boom = new InvalidOperationException("boom");
        var cmd = new RelayCommand<string>(_ => throw boom, canExecute: null, popupDisplayer: popup);

        cmd.Execute("any");

        popup.Received(1).Error(boom);
    }

    [Fact]
    public void Generic_Execute_Exception_NullPopup_FallsBackToEDServicesCurrent()
    {
        var popup = Substitute.For<IPopupDisplayer>();
        EDServices.Initialize(ServicesWith(popup));
        try
        {
            var boom = new InvalidOperationException("boom");
            var cmd = new RelayCommand<string>(_ => throw boom);

            cmd.Execute("any");

            popup.Received(1).Error(boom);
        }
        finally
        {
            EDServices.Reset();
        }
    }
}
