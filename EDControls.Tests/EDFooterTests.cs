using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using EDControls.EDFooter;
using EDControls.EDViewModel;
using EDTesting;
using NSubstitute;
using Footer = EDControls.EDFooter.EDFooter;
using Vm = EDControls.EDViewModel.EDViewModel;

namespace EDControls.Tests;

/// <summary>Viditelnost tlačítek dle FooterMode, propsání LeftContent, odběry okna a zavření Esc.</summary>
public class EDFooterTests
{
    private const string Comp = "pack://application:,,,/EDControls;component/";

    [Fact]
    public void Default_ShowsCloseOnly()
    {
        RunSta(() =>
        {
            var footer = new Footer();

            Assert.Equal(Visibility.Collapsed, footer.OkBtn.Visibility);
            Assert.Equal(Visibility.Collapsed, footer.StornoBtn.Visibility);
            Assert.Equal(Visibility.Visible, footer.CloseBtn.Visibility);
        });
    }

    [Fact]
    public void OkCancelMode_SwapsButtons_AndBack()
    {
        RunSta(() =>
        {
            var footer = new Footer { FooterMode = FooterMode.OkCancel };

            Assert.Equal(Visibility.Visible, footer.OkBtn.Visibility);
            Assert.Equal(Visibility.Visible, footer.StornoBtn.Visibility);
            Assert.Equal(Visibility.Collapsed, footer.CloseBtn.Visibility);

            footer.FooterMode = FooterMode.Close;

            Assert.Equal(Visibility.Collapsed, footer.OkBtn.Visibility);
            Assert.Equal(Visibility.Collapsed, footer.StornoBtn.Visibility);
            Assert.Equal(Visibility.Visible, footer.CloseBtn.Visibility);
        });
    }

    [Fact]
    public void LeftContent_LandsInPresenter()
    {
        RunSta(() =>
        {
            var footer = new Footer();
            var content = new TextBlock { Text = "3 záznamy" };

            footer.LeftContent = content;

            Assert.Same(content, footer.LeftContentPresenter.Content);
        });
    }

    [Fact]
    public void RepeatedLoaded_DoesNotAccumulateHandlers()
    {
        // Regrese: OnLoaded se dřív neodhlašoval - každý návrat do stromu (tab, re-template)
        // přidal druhý RequestClose odběr a okno se pak zavíralo/nastavovalo DialogResult
        // vícekrát. Loaded/Unloaded se vyvolávají synchronně (broadcast přes dispatcher
        // by v testu bez message loopu nedoběhl deterministicky).
        RunSta(() =>
        {
            var vm = NewVm();
            var footer = new Footer();
            var window = WindowWith(footer, vm);
            try
            {
                RaiseLoaded(footer);
                Assert.Equal(1, RequestCloseHandlerCount(vm));

                RaiseLoaded(footer);   // druhý Loaded bez Unloaded (re-template) → pořád jeden odběr
                Assert.Equal(1, RequestCloseHandlerCount(vm));

                footer.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent, footer));
                Assert.Equal(0, RequestCloseHandlerCount(vm));

                RaiseLoaded(footer);   // návrat do stromu → znovu právě jeden
                Assert.Equal(1, RequestCloseHandlerCount(vm));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void DataContextSwap_MovesSubscriptionToNewVm()
    {
        RunSta(() =>
        {
            var oldVm = NewVm();
            var newVm = NewVm();
            var footer = new Footer();
            var window = WindowWith(footer, oldVm);
            try
            {
                RaiseLoaded(footer);

                window.DataContext = newVm;   // DataContextChanged jde synchronně

                Assert.Equal(0, RequestCloseHandlerCount(oldVm));
                Assert.Equal(1, RequestCloseHandlerCount(newVm));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [WindowFact]
    public void Esc_ClosesWindow()
    {
        RunSta(() =>
        {
            var box = new TextBox();
            var window = ShownWindowWith(NewVm(), box);
            var closed = false;
            window.Closed += (_, _) => closed = true;
            try
            {
                PressEsc(box);

                Assert.True(closed);
            }
            finally
            {
                if (!closed) window.Close();
            }
        });
    }

    [WindowFact]
    public void HeldEsc_RepeatDoesNotClose()
    {
        // Podržený Esc: první stisk zavřel dialog nad oknem, opakování nesmí zavřít i to pod ním.
        RunSta(() =>
        {
            var box = new TextBox();
            var window = ShownWindowWith(NewVm(), box);
            try
            {
                PressEsc(box, repeat: true);

                Assert.True(window.IsVisible);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [WindowFact]
    public void Esc_FromPopup_DoesNotClose()
    {
        // Kalendář DatePickeru se na Esc zavře, ale Handled nenastaví - Esc pak probublá z popupu až do okna.
        // Popup zůstane zavřený: otevřený na sdíleném app vlákně nechal žít okno souběžného Form_InClosedWindow_IsCollected.
        // Obsah zavřeného popupu vede k oknu jen logický strom, mimo vizuální strom okna je stejně jako otevřený.
        RunSta(() =>
        {
            var inPopup = new Button();
            var window = ShownWindowWith(NewVm(), new Popup { Child = inPopup });
            try
            {
                PressEsc(inPopup, PresentationSource.FromVisual(window));

                Assert.True(window.IsVisible);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [WindowFact]
    public void Esc_DeclinedConfirm_KeepsWindowOpen()
    {
        RunSta(() =>
        {
            var popups = Substitute.For<IPopupDisplayer>();   // ConfirmClose vrací false
            var box = new TextBox();
            var window = ShownWindowWith(new ConfirmingVm(popups), box);
            try
            {
                PressEsc(box);

                popups.Received(1).ConfirmClose(Arg.Any<Window?>());
                Assert.True(window.IsVisible);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [WindowFact]
    public void Esc_InSearchWithText_ClearsFilterFirst()
    {
        RunSta(() =>
        {
            var search = new EDSearchTextBox.EDSearchTextBox { Text = "abc" };
            var window = ShownWindowWith(NewVm(), search);
            var closed = false;
            window.Closed += (_, _) => closed = true;
            try
            {
                PressEsc(search);
                Assert.Equal("", search.Text);
                Assert.False(closed);

                PressEsc(search);
                Assert.True(closed);
            }
            finally
            {
                if (!closed) window.Close();
            }
        });
    }

    private sealed class ConfirmingVm : Vm
    {
        public ConfirmingVm(IPopupDisplayer popups)
            : base(new EDServices(null, Substitute.For<IWindowNavigator>(), popups)) => ConfirmOnCancel = true;
    }

    private static Vm NewVm() =>
        new(new EDServices(null, Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()));

    // Mimo viditelnou plochu a bez chrome - test nesmí blikat přes obrazovku.
    private static Window ShownWindowWith(Vm vm, UIElement content)
    {
        var footer = new Footer();
        DockPanel.SetDock(footer, Dock.Bottom);
        var window = new Window
        {
            Content = new DockPanel { Children = { footer, content } },
            DataContext = vm, WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -3000, Top = 0, Width = 400, Height = 300,
            WindowStyle = WindowStyle.None, ShowInTaskbar = false, ShowActivated = false,
        };
        window.Show();
        RaiseLoaded(footer);   // Loaded po Show čeká ve frontě dispatcheru až za tělem testu
        return window;
    }

    // Jako InputManager: Preview a bublající KeyDown sdílí args, Handled z Preview zastaví i KeyDown.
    private static void PressEsc(UIElement target, PresentationSource? source = null, bool repeat = false)
    {
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, (source ?? PresentationSource.FromVisual(target))!, 0, Key.Escape)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        // IsRepeat nastavuje jen InputManager - veřejný setter není.
        if (repeat)
            typeof(KeyEventArgs).GetMethod("SetRepeat", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(args, [true]);
        target.RaiseEvent(args);
        args.RoutedEvent = Keyboard.KeyDownEvent;
        target.RaiseEvent(args);
    }

    // Bez Show - footer je v logickém stromu okna, Window.GetWindow ho najde.
    private static Window WindowWith(Footer footer, Vm vm) => new()
    {
        Content = footer,
        DataContext = vm,
    };

    private static void RaiseLoaded(Footer footer) =>
        footer.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, footer));

    private static int RequestCloseHandlerCount(Vm vm)
    {
        var field = typeof(Vm).GetField(nameof(Vm.RequestClose), BindingFlags.Instance | BindingFlags.NonPublic);
        return (field?.GetValue(vm) as Delegate)?.GetInvocationList().Length ?? 0;
    }

    // Footer čte StaticResource EDStrings.* - potřebuje Application se slovníky reálné appky.
    // OnExplicitShutdown: testy vytvářejí a zavírají okna a default OnLastWindowClose by
    // sdílenou Application shodil - druhá se v AppDomain vytvořit nedá.
    private static void EnsureApplication()
    {
        if (Application.Current is not null) return;

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        foreach (var dict in new[] { "EDTheme/EDTheme.xaml", "EDControlStyles/EDControlStyles.xaml",
                                     "EDIcons/EDIcons.xaml", "EDStrings/EDStrings.xaml" })
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(Comp + dict) });
    }

    // Trvalé app vlákno - viz EDComponentToolbarTests.RunSta (týž důvod).
    private static void RunSta(Action action) =>
        EDTesting.Sta.RunOnAppThread(() => { EnsureApplication(); action(); });
}
