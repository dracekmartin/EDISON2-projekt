using System.Windows;
using System.Windows.Input;
using EDControls;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
using EDISONMAIN2.Windows.EdisonMainWindow.SupportStrip;
using EDTesting;
using NSubstitute;
using static EDISONMAIN2.Tests.EdisonMainViewModelHarness;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Enter v hledání skočí na první nález ve stromu aktivní záložky. Strom k záložce dohledává okno,
/// takže nová záložka bez stromu v kartě by Enter tiše vypnula.
/// </summary>
[Collection(SharedStaticStateCollection.Name)]
public class EdisonMainWindowTreeSearchTests
{
    private const string Comp = "pack://application:,,,/EDControls;component/";

    private static ExecutableModule Exe(string id, string name) =>
        new() { Name = name, ElementId = id, ExecutablePath = id + ".exe", FormName = "ALVA.X.frm" };

    // Každý strom ze serveru má vlastní modul, Oblíbené dostanou modul hlavní nabídky.
    private static IModuleTreeLoader Trees()
    {
        var loader = Substitute.For<IModuleTreeLoader>();
        loader.GetModuleTree(Arg.Any<IMLRest>(), "main").Returns([Exe("1", "Jízdenky")]);
        loader.GetModuleTree(Arg.Any<IMLRest>(), "system").Returns([Exe("2", "Uživatelé")]);
        loader.GetModuleTree(Arg.Any<IMLRest>(), "partners").Returns([Exe("3", "Partner")]);
        return loader;
    }

    // Application je jedna na proces a mohl ji založit jiný test: chybějící slovníky se doplní i do ní.
    private static void EnsureApplication()
    {
        var app = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        foreach (var dict in new[] { "EDTheme/EDTheme.xaml", "EDControlStyles/EDControlStyles.xaml",
                                     "EDIcons/EDIcons.xaml", "EDStrings/EDStrings.xaml" })
        {
            var source = new Uri(Comp + dict);
            if (!app.Resources.MergedDictionaries.Any(d => d.Source == source))
                app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = source });
        }
    }

    private static int VisibleInCard(EdisonMainWindow window) =>
        window.TreeCard.Children.OfType<UIElement>().Count(c => c.Visibility == Visibility.Visible);

    private static void PressEnter(UIElement target) =>
        target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target)!, 0, Key.Enter)
            { RoutedEvent = Keyboard.KeyDownEvent });

    [WindowFact]
    public void EnterInSearch_SelectsHitInTreeOfEveryTab()
    {
        var screenshotMode = Environment.GetEnvironmentVariable(SupportWidgetHost.ScreenshotModeVariable);
        // Pruh podpory pak nestaví WebView2, které by v testu sahalo na síť.
        Environment.SetEnvironmentVariable(SupportWidgetHost.ScreenshotModeVariable, "1");
        try
        {
            Sta.RunOnAppThread(() =>
            {
                EnsureApplication();
                var vm = CreateVM(treeLoader: Trees());
                vm.ToggleFavoriteCommand.Execute(vm.MainMenuTree.Single());
                // Mimo viditelnou plochu a bez chrome - test nesmí blikat přes obrazovku.
                var window = new EdisonMainWindow
                {
                    DataContext = vm, WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = -3000, Top = 0, Width = 1200, Height = 800,
                    WindowStyle = WindowStyle.None, ShowInTaskbar = false,
                };
                window.Show();
                try
                {
                    var tabsWithTree = vm.TreeTabs.Where(t => t.Tree is not null).ToList();
                    Assert.Equal(4, tabsWithTree.Count);   // předpoklad: Hlavní, Správa, Partnerská, Oblíbené
                    foreach (var tab in tabsWithTree)
                    {
                        var hit = tab.Tree!.OfType<ExecutableModule>().Single();
                        vm.SelectedTab = tab;
                        vm.TreeSearchText = hit.Name;

                        PressEnter(window.TreeSearchBox);

                        var tree = window.ActiveTreeView();
                        Assert.True(tree is not null, $"záložka {tab.Key} nemá strom v kartě");
                        Assert.Same(tab.Tree, tree.ItemsSource);
                        Assert.Same(hit, tree.SelectedItem);
                        // Strom bez vazby na záložku by zůstal vidět a překryl aktivní.
                        Assert.Equal(1, VisibleInCard(window));
                    }

                    vm.SelectedTab = vm.TreeTabs.Single(t => t.Tree is null);   // Používané
                    Assert.Null(window.ActiveTreeView());
                    Assert.Equal(1, VisibleInCard(window));
                }
                finally
                {
                    vm.SuppressCloseConfirm();
                    window.Close();
                }
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable(SupportWidgetHost.ScreenshotModeVariable, screenshotMode);
        }
    }
}
