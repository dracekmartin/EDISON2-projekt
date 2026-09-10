using System;
using System.Threading;
using System.Windows;
using EDControls.EDViewModel;
using NSubstitute;

namespace EDControls.Tests;

/// <summary>
/// Resolve order owneru popupu: explicitní per-call owner → scoped owner (okno VM) →
/// (dál už řeší globální displayer svým DefaultOwner). Regrese, kterou to hlídá: popup
/// z VM bez explicitního owneru by se centroval na hlavní okno, ne na svoje.
/// </summary>
public class WindowScopedPopupDisplayerTests
{
    [Fact]
    public void NoExplicitOwner_UsesScopedWindow()
    {
        EDTesting.Sta.Run(() =>
        {
            var inner = Substitute.For<IPopupDisplayer>();
            var scoped = new Window();
            var sut = new WindowScopedPopupDisplayer(inner, scoped);

            sut.Info("hello");
            sut.Confirm("really?");
            sut.ConfirmClose();
            sut.ConfirmYesNoCancel("save?", "Neuložené změny");
            sut.Error(new InvalidOperationException("boom"));

            inner.Received(1).Info("hello", scoped);
            inner.Received(1).Confirm("really?", scoped);
            inner.Received(1).ConfirmClose(scoped);
            inner.Received(1).ConfirmYesNoCancel("save?", "Neuložené změny", scoped);
            inner.Received(1).Error(Arg.Any<InvalidOperationException>(), scoped);
        });
    }

    [Fact]
    public void ExplicitOwner_WinsOverScopedWindow()
    {
        EDTesting.Sta.Run(() =>
        {
            var inner = Substitute.For<IPopupDisplayer>();
            var scoped = new Window();
            var explicitOwner = new Window();
            var sut = new WindowScopedPopupDisplayer(inner, scoped);

            sut.Info("hello", explicitOwner);
            sut.Confirm("really?", explicitOwner);
            sut.ConfirmClose(explicitOwner);
            sut.ConfirmYesNoCancel("save?", owner: explicitOwner);
            sut.Error(new InvalidOperationException("boom"), explicitOwner);

            inner.Received(1).Info("hello", explicitOwner);
            inner.Received(1).Confirm("really?", explicitOwner);
            inner.Received(1).ConfirmClose(explicitOwner);
            inner.Received(1).ConfirmYesNoCancel("save?", null, explicitOwner);
            inner.Received(1).Error(Arg.Any<InvalidOperationException>(), explicitOwner);
            inner.DidNotReceive().Info(Arg.Any<string>(), scoped);
        });
    }

    [Fact]
    public void ReturnValues_PassThroughFromInner()
    {
        EDTesting.Sta.Run(() =>
        {
            var inner = Substitute.For<IPopupDisplayer>();
            var scoped = new Window();
            inner.Confirm(Arg.Any<string>(), Arg.Any<Window>()).Returns(true);
            inner.ConfirmClose(Arg.Any<Window>()).Returns(true);
            inner.ConfirmYesNoCancel(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Window>())
                 .Returns((bool?)null);
            var sut = new WindowScopedPopupDisplayer(inner, scoped);

            Assert.True(sut.Confirm("really?"));
            Assert.True(sut.ConfirmClose());
            Assert.Null(sut.ConfirmYesNoCancel("save?"));
        });
    }

}
