using System.Windows;
using EDControls;
using EDTesting;
using NSubstitute;

namespace EDControls.Tests;

/// <summary>
/// Celá cesta Attach → restore při SourceInitialized → save při Closing, jednou přes fake
/// úložiště a jednou přes legacy Caché úložiště s podstrčeným MLRest.
/// </summary>
public class WindowStatePersistenceAttachTests
{
    private const char D = '\0';

    private sealed class FakeStore(WindowGeometry geometry) : IWindowStateStore
    {
        public FakeStore() : this(WindowGeometry.None) { }

        public bool LoadThrows { get; set; }
        public int SaveCount { get; private set; }
        public Rect SavedBounds { get; private set; }
        public bool SavedMaximized { get; private set; }

        public WindowGeometry Load() =>
            LoadThrows ? throw new InvalidOperationException("úložiště nedostupné") : geometry;

        public void Save(Rect bounds, bool maximized)
        {
            SaveCount++;
            SavedBounds = bounds;
            SavedMaximized = maximized;
        }
    }

    /// <summary>Tolerance jeden fyzický pixel: Show přichytí geometrii na celé pixely (při 150 %
    /// je 111 DIP = 166,5 px → 111,33).</summary>
    private static void AssertBounds(Rect expected, Rect actual)
    {
        Assert.Equal(expected.Left, actual.Left, tolerance: 1.0);
        Assert.Equal(expected.Top, actual.Top, tolerance: 1.0);
        Assert.Equal(expected.Width, actual.Width, tolerance: 1.0);
        Assert.Equal(expected.Height, actual.Height, tolerance: 1.0);
    }

    private static IMLRest MockWithSavedState(params string[] rows)
    {
        var mlRest = MLRestMocks.Create();
        mlRest.QueryOrThrow("ALVA.LIBRARY.Form", "LoadParams", Arg.Any<object[]>())
            .Returns(MLRestMocks.Output(rows.Cast<object>().ToArray()));
        return mlRest;
    }

    private static CacheWindowStateStore CacheStore(IMLRest mlRest) => new("EDISON2.TestWindow", () => mlRest);

    [WindowFact]
    public void Attach_Store_RestoresSavedBoundsOnShow_SavesOnClose()
    {
        var store = new FakeStore(new(100, 120, 400, 300, false));

        Sta.RunOnAppThread(() =>
        {
            var window = new Window { ShowInTaskbar = false, ShowActivated = false };
            WindowStatePersistence.Attach(window, store);

            window.Show();   // SourceInitialized → restore ještě před prvním paintem

            Assert.Equal(100, window.Left);
            Assert.Equal(120, window.Top);
            Assert.Equal(400, window.Width);
            Assert.Equal(300, window.Height);
            Assert.Equal(WindowState.Normal, window.WindowState);

            window.Close();

            Assert.Equal(1, store.SaveCount);
            AssertBounds(new Rect(100, 120, 400, 300), store.SavedBounds);
            Assert.False(store.SavedMaximized);
        });
    }

    [WindowFact]
    public void Attach_Store_CancelledClosingDoesNotSave()
    {
        var store = new FakeStore();

        Sta.RunOnAppThread(() =>
        {
            var window = new Window { ShowInTaskbar = false, ShowActivated = false, Width = 300, Height = 200 };
            // Veto zavření (potvrzovací dialog shellu) - geometrie se ukládat nesmí.
            window.Closing += (_, e) => e.Cancel = true;
            WindowStatePersistence.Attach(window, store);

            window.Show();
            window.Close();

            Assert.Equal(0, store.SaveCount);

            window.Closing += (_, e) => e.Cancel = false;
            window.Close();
        });
    }

    [WindowFact]
    public void Attach_Store_FailingLoadKeepsDefaultsAndOpens()
    {
        var store = new FakeStore { LoadThrows = true };

        Sta.RunOnAppThread(() =>
        {
            var window = new Window
            {
                ShowInTaskbar = false, ShowActivated = false,
                Width = 222, Height = 111,
                WindowState = WindowState.Maximized,
            };
            WindowStatePersistence.Attach(window, store);

            window.Show();   // best-effort: chyba nesmí otevření zablokovat

            // XAML stav se musí vrátit i když úložiště spadlo.
            Assert.Equal(WindowState.Maximized, window.WindowState);
            window.Close();
        });
    }

    [WindowFact]
    public void Attach_ReadOnlyStore_RestoresButNeverSaves()
    {
        // Přihlašovací okno sdílený záznam jen přebírá - zapsat ho smí shell, ne přetažený dialog.
        var store = new FakeStore(new(100, 120, 400, 300, false));

        Sta.RunOnAppThread(() =>
        {
            var window = new Window { ShowInTaskbar = false, ShowActivated = false };
            WindowStatePersistence.Attach(window, new ReadOnlyWindowStateStore(store));

            window.Show();

            Assert.Equal(400, window.Width);
            Assert.Equal(300, window.Height);

            window.Close();

            Assert.Equal(0, store.SaveCount);
        });
    }

    [WindowFact]
    public void Save_WritesGeometryWithoutClosing()
    {
        // Login okno ukládá pozici hned po přihlášení - shell si ji čte dřív, než se zavře.
        var store = new FakeStore();

        Sta.RunOnAppThread(() =>
        {
            var window = new Window
            {
                ShowInTaskbar = false, ShowActivated = false,
                Left = 40, Top = 50, Width = 300, Height = 200,
            };
            window.Show();

            WindowStatePersistence.Save(window, store);

            Assert.Equal(1, store.SaveCount);
            AssertBounds(new Rect(40, 50, 300, 200), store.SavedBounds);
            window.Close();
        });
    }

    [WindowFact]
    public void Attach_CacheStore_RestoresSavedBoundsOnShow_SavesOnClose()
    {
        var mlRest = MockWithSavedState(
            $"param{D}location{D}100;120",
            $"param{D}size{D}400;300",
            $"param{D}state{D}0");

        Sta.RunOnAppThread(() =>
        {
            var window = new Window { ShowInTaskbar = false, ShowActivated = false };
            WindowStatePersistence.Attach(window, CacheStore(mlRest));

            window.Show();

            Assert.Equal(100, window.Left);
            Assert.Equal(120, window.Top);
            Assert.Equal(400, window.Width);
            Assert.Equal(300, window.Height);
            Assert.Equal(WindowState.Normal, window.WindowState);

            window.Close();

            mlRest.Received(1).QueryOrThrow("ALVA.LIBRARY.Form", "SaveParams", Arg.Is<object[]>(a =>
                a.Length == 5 && (string)a[0] == $"formID{D}EDISON2.TestWindow"));
        });
    }

    [WindowFact]
    public void Attach_ServerFails_WindowKeepsDefaultsAndOpens()
    {
        var mlRest = MLRestMocks.Create();
        mlRest.QueryOrThrow("ALVA.LIBRARY.Form", "LoadParams", Arg.Any<object[]>())
            .Returns(_ => throw new InvalidOperationException("server down"));

        Sta.RunOnAppThread(() =>
        {
            var window = new Window
            {
                ShowInTaskbar = false, ShowActivated = false,
                Width = 222, Height = 111,
            };
            WindowStatePersistence.Attach(window, CacheStore(mlRest));

            window.Show();   // best-effort: chyba nesmí otevření zablokovat

            // Show přichytí rozměr na celé fyzické pixely: při 150 % je 111 DIP = 166,5 px → 111,33.
            // Tolerance = jeden fyzický pixel při libovolném běžném škálování.
            Assert.Equal(222, window.Width, tolerance: 1.0);
            Assert.Equal(111, window.Height, tolerance: 1.0);
            window.Close();
        });
    }

    [WindowFact]
    public void Attach_MaximizedInXaml_RestoredWithoutSavedState()
    {
        // Attach sráží XAML Maximized na Normal kvůli HWND stylu; bez uloženého stavu
        // ho restore musí vrátit - jinak by okno zůstalo omylem odmaximalizované.
        var mlRest = MockWithSavedState();   // nic uloženého

        Sta.RunOnAppThread(() =>
        {
            var window = new Window
            {
                ShowInTaskbar = false, ShowActivated = false,
                WindowState = WindowState.Maximized,
            };
            WindowStatePersistence.Attach(window, CacheStore(mlRest));
            Assert.Equal(WindowState.Normal, window.WindowState);   // sraženo před HWND

            window.Show();

            Assert.Equal(WindowState.Maximized, window.WindowState);
            window.Close();
        });
    }
}
