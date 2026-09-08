using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using EDControls.EDFooter;
using EDControls.EDViewModel;
using NSubstitute;
using Footer = EDControls.EDFooter.EDFooter;
using Vm = EDControls.EDViewModel.EDViewModel;

namespace EDControls.Tests;

/// <summary>Viditelnost tlačítek dle FooterMode a propsání LeftContent (bez plného okna).</summary>
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

    private static Vm NewVm() =>
        new(new EDServices(null, Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()));

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

    // Trvalé app vlákno - viz EDComponentToolbarFlatTests.RunSta (týž důvod).
    private static void RunSta(Action action) =>
        EDTesting.Sta.RunOnAppThread(() => { EnsureApplication(); action(); });
}
