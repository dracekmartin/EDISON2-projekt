using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using EDControls;
using EDControls.EDViewModel;
using EDISONMAIN2.Windows.SetupWindow;
using EDTesting;
using NSubstitute;

namespace EDISONMAIN2.Tests;

/// <summary>Okno wizardu jen binduje VM; překlep v cestě by WPF tiše spolkl a pole by zůstalo mrtvé.</summary>
public class SetupWindowTests
{
    private const string Comp = "pack://application:,,,/EDControls;component/";

    [Fact]
    public void Window_EveryBindingFindsItsViewModelProperty()
    {
        InWindow(probe: true, (_, window) =>
        {
            var bindings = BindingsOf(window).ToList();

            Assert.True(bindings.Count >= 12, $"okno má jen {bindings.Count} bindingů");
            Assert.Empty(bindings.Where(b => b.Status == BindingStatus.PathError).Select(b => b.ParentBinding.Path.Path));
        });
    }

    [Theory]
    [InlineData(true, "EDTheme.Semantic.Success")]
    [InlineData(false, "EDTheme.Semantic.Danger")]
    public void Window_StatusColorFollowsTestResult(bool reachable, string brush)
    {
        // Binding v DataTriggeru stylu lokální hodnoty nevidí; překlep by chybu ukázal zeleně.
        InWindow(reachable, (vm, window) =>
        {
            vm.TestCommand.Execute(null);

            var status = (TextBlock)BindingsOf(window).Single(b => b.ParentBinding.Path.Path == "StatusText").Target;
            Assert.Same(Application.Current.FindResource(brush), status.Foreground);
        });
    }

    // Okno se nezobrazuje, ale konstruktor ho zapíše jako MainWindow sdílené aplikace: zavřít, ať nepřekáží dalším testům.
    private static void InWindow(bool probe, Action<SetupWizardViewModel, SetupWindow> test)
    {
        Sta.RunOnAppThread(() =>
        {
            EnsureApplication();
            var vm = new SetupWizardViewModel(
                new EDServices(null, Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()),
                new ServerSettings { Host = "10.0.0.5", SuperPort = 51972 }, (_, _) => probe, (_, _) => [], _ => { });
            var window = new SetupWindow(vm);
            try
            {
                test(vm, window);
            }
            finally
            {
                window.Close();
            }
        });
    }

    internal static IEnumerable<BindingExpression> BindingsOf(DependencyObject root)
    {
        var local = root.GetLocalValueEnumerator();
        while (local.MoveNext())
            if (BindingOperations.GetBindingExpression(root, local.Current.Property) is { } expression)
                yield return expression;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var expression in BindingsOf(child))
                yield return expression;
    }

    internal static void EnsureApplication()
    {
        if (Application.Current is not null) return;

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        foreach (var dict in new[] { "EDTheme/EDTheme.xaml", "EDControlStyles/EDControlStyles.xaml",
                                     "EDIcons/EDIcons.xaml", "EDStrings/EDStrings.xaml" })
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(Comp + dict) });
    }
}
