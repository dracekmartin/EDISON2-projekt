using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Toolbar = EDControls.EDComponentToolbar.EDComponentToolbar;

namespace EDControls.Tests;

/// <summary>
/// Sdílený toolbar komponentových hlaviček. Regrese, které to hlídá: rozejití brandového
/// pruhu (EDList i EDInputForm stojí na témže podkladu) a akumulace Ctrl+F bindingů.
/// </summary>
public class EDComponentToolbarTests
{
    private const string Comp = "pack://application:,,,/EDControls;component/";

    [Fact]
    public void BarBackground_IsBrandHeader()
    {
        RunSta(() =>
        {
            var toolbar = new Toolbar();
            var bar = Assert.IsType<Border>(toolbar.Content);

            // DynamicResource se vyhodnotí až v resource stromu.
            var root = new Grid();
            root.Children.Add(toolbar);
            root.Measure(new Size(800, 100));
            root.Arrange(new Rect(0, 0, 800, 100));
            root.UpdateLayout();

            var expected = (Brush)Application.Current!.Resources["EDTheme.Accent.Header"];
            Assert.Equal(expected.ToString(), bar.Background.ToString());
        });
    }

    /// <summary>Akce nad položkami jsou ikony v pruhu s popiskem a zkratkou v bublině, jako lišta nad seznamem
    /// legacy formulářů; bez akcí v pruhu žádné nejsou (stávající hostitelé se nezmění).</summary>
    [Fact]
    public void Actions_AreIconButtonsWithShortcutTooltip()
    {
        RunSta(() =>
        {
            ICommand prikaz = new RoutedCommand();
            var akce = new[]
            {
                new EDViewModel.EDAction("Nový", prikaz, null, Key.N, ModifierKeys.Control),
                new EDViewModel.EDAction("Odstranit", prikaz, null, Key.D, ModifierKeys.Control),
            };

            Assert.Equal(["Nový [Ctrl+N]", "Odstranit [Ctrl+D]"], TooltipyAkci(new Toolbar { Actions = akce }));
            Assert.Empty(TooltipyAkci(new Toolbar()));
        });
    }

    /// <summary>Oddělovač skupiny akcí je v pruhu svislá čára, ne tlačítko bez ikony.</summary>
    [Fact]
    public void ActionSeparator_IsLineNotButton()
    {
        RunSta(() =>
        {
            ICommand prikaz = new RoutedCommand();
            var toolbar = new Toolbar
            {
                Actions = [new EDViewModel.EDAction("Nový", prikaz), EDViewModel.EDAction.Separator,
                           new EDViewModel.EDAction("Výchozí", prikaz)],
            };

            Assert.Equal(["Nový", "Výchozí"], TooltipyAkci(toolbar));
            Assert.Single(Potomci(toolbar).OfType<Border>(),
                b => b.Name == "Oddelovac" && b.Visibility == Visibility.Visible);
        });
    }

    private static List<string> TooltipyAkci(Toolbar toolbar)
    {
        var root = new Grid();
        root.Children.Add(toolbar);
        root.Measure(new Size(800, 100));
        root.Arrange(new Rect(0, 0, 800, 100));
        root.UpdateLayout();
        return Potomci(toolbar).OfType<Button>()
            .Where(b => b.DataContext is EDViewModel.EDAction && b.Visibility == Visibility.Visible)
            .Select(b => b.ToolTip as string ?? "").ToList();
    }

    private static IEnumerable<DependencyObject> Potomci(DependencyObject rodic)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(rodic); i++)
        {
            var dite = VisualTreeHelper.GetChild(rodic, i);
            yield return dite;
            foreach (var dalsi in Potomci(dite)) yield return dalsi;
        }
    }

    /// <summary>Ikona nápovědy je v XAML `StaticResource` - chybějící klíč shodí toolbar
    /// hned při načtení, ne až při kliku.</summary>
    [Fact]
    public void HelpIcon_ResourceExists()
    {
        RunSta(() => Assert.True(Application.Current!.Resources.Contains("EDIcons.Help")));
    }

    [Fact]
    public void CtrlFBinding_DoesNotAccumulateAcrossReloads()
    {
        // Dokumentovaná past (EDControls/CLAUDE.md): window-level Ctrl+F binding z OnLoaded
        // se musí v OnUnloaded odregistrovat, jinak se při re-loadu akumulují kopie.
        RunSta(() =>
        {
            var toolbar = new Toolbar();
            var window = new Window { Content = toolbar };
            try
            {
                int CtrlFCount() => window.InputBindings.OfType<KeyBinding>()
                    .Count(b => b.Key == Key.F && b.Modifiers == ModifierKeys.Control);

                RaiseOn(toolbar, FrameworkElement.LoadedEvent);
                RaiseOn(toolbar, FrameworkElement.LoadedEvent);   // re-load bez Unloaded
                Assert.Equal(1, CtrlFCount());

                RaiseOn(toolbar, FrameworkElement.UnloadedEvent);
                Assert.Equal(0, CtrlFCount());

                RaiseOn(toolbar, FrameworkElement.LoadedEvent);   // návrat do stromu
                Assert.Equal(1, CtrlFCount());
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>Nápověda „(Ctrl+F)" u každého toolbaru v okně - zkratka vede do hledání komponenty,
    /// ve které uživatel je, takže z kteréhokoli platí. Mimo okno (bez zkratky) se neukazuje.</summary>
    [Fact]
    public void Placeholder_CtrlFHintOnEveryToolbarInWindow()
    {
        RunSta(() =>
        {
            var prvni = new Toolbar();
            var druhy = new Toolbar();
            var window = new Window { Content = new StackPanel { Children = { prvni, druhy } } };
            try
            {
                Assert.Equal("Hledat…", prvni.EffectiveSearchPlaceholder);

                RaiseOn(prvni, FrameworkElement.LoadedEvent);
                RaiseOn(druhy, FrameworkElement.LoadedEvent);
                Assert.Equal("Hledat… (Ctrl+F)", prvni.EffectiveSearchPlaceholder);
                Assert.Equal("Hledat… (Ctrl+F)", druhy.EffectiveSearchPlaceholder);

                RaiseOn(prvni, FrameworkElement.UnloadedEvent);
                Assert.Equal("Hledat…", prvni.EffectiveSearchPlaceholder);
                Assert.Equal("Hledat… (Ctrl+F)", druhy.EffectiveSearchPlaceholder);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>Ctrl+F jde do hledání komponenty s fokusem, jinak prvního viditelného - nikdy do
    /// skrytého (dřív ho držel první načtený toolbar, třeba formulář na neaktivní záložce).</summary>
    [Fact]
    public void CtrlFTarget_FocusedComponentThenFirstVisible()
    {
        RunSta(() =>
        {
            var skryty = new Toolbar();
            var viditelny = new Toolbar();
            var sFokusem = new Toolbar();
            var bezHledani = new Toolbar { ShowSearch = false };
            var vsechny = new[] { skryty, viditelny, sFokusem, bezHledani };
            bool JeVidet(Toolbar t) => t != skryty;

            Assert.Same(sFokusem, Toolbar.CilCtrlF(vsechny, t => t == sFokusem, JeVidet));
            Assert.Same(viditelny, Toolbar.CilCtrlF(vsechny, _ => false, JeVidet));
            Assert.Same(viditelny, Toolbar.CilCtrlF(vsechny, t => t == skryty, JeVidet));
            Assert.Null(Toolbar.CilCtrlF(vsechny, t => t == bezHledani, t => t == bezHledani));
        });
    }

    private static void RaiseOn(Toolbar toolbar, RoutedEvent routedEvent) =>
        toolbar.RaiseEvent(new RoutedEventArgs(routedEvent, toolbar));

    // Toolbar načítá pack:// resource a StaticResource EDIcons.* - potřebuje tedy Application
    // se stejnými slovníky, jaké merguje reálná appka (viz root CLAUDE.md).
    private static void EnsureApplication()
    {
        if (Application.Current is not null) return;

        // OnExplicitShutdown - jiné testy (EDFooter) vytvářejí okna; default OnLastWindowClose
        // by sdílenou Application shodil a druhá se v AppDomain vytvořit nedá.
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        foreach (var dict in new[] { "EDTheme/EDTheme.xaml", "EDControlStyles/EDControlStyles.xaml",
                                     "EDIcons/EDIcons.xaml", "EDStrings/EDStrings.xaml" })
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(Comp + dict) });
    }

    // Trvalé app vlákno: Application vytvořená na jednorázovém STA vlákně občas zanikne
    // se svým dispatcherem a druhá se v AppDomain vytvořit nedá.
    private static void RunSta(Action action) =>
        EDTesting.Sta.RunOnAppThread(() => { EnsureApplication(); action(); });
}
