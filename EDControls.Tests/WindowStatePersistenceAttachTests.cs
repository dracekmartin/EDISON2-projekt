using System.Windows;
using EDControls;
using EDTesting;
using NSubstitute;

namespace EDControls.Tests;

/// <summary>
/// Celá cesta Attach → restore při SourceInitialized → save při Closing, přes seam
/// <see cref="WindowStatePersistence.MLRestProvider"/>. Statický provider → kolekce
/// bez paralelizace.
/// </summary>
[Collection(SharedStaticStateCollection.Name)]
public class WindowStatePersistenceAttachTests
{
    private const char D = '\0';

    private static IMLRest MockWithSavedState(params string[] rows)
    {
        var mlRest = MLRestMocks.Create();
        mlRest.QueryOrThrow("ALVA.LIBRARY.Form", "LoadParams", Arg.Any<object[]>())
            .Returns(MLRestMocks.Output(rows.Cast<object>().ToArray()));
        return mlRest;
    }

    private static void WithProvider(IMLRest mlRest, Action action)
    {
        var original = WindowStatePersistence.MLRestProvider;
        WindowStatePersistence.MLRestProvider = () => mlRest;
        try { action(); }
        finally { WindowStatePersistence.MLRestProvider = original; }
    }

    [Fact]
    public void Attach_RestoresSavedBoundsOnShow_SavesOnClose()
    {
        var mlRest = MockWithSavedState(
            $"param{D}location{D}100;120",
            $"param{D}size{D}400;300",
            $"param{D}state{D}0");

        Sta.RunOnAppThread(() => WithProvider(mlRest, () =>
        {
            var window = new Window { ShowInTaskbar = false, ShowActivated = false };
            WindowStatePersistence.Attach(window, "EDISON2.TestWindow");

            window.Show();   // SourceInitialized → restore ještě před prvním paintem

            Assert.Equal(100, window.Left);
            Assert.Equal(120, window.Top);
            Assert.Equal(400, window.Width);
            Assert.Equal(300, window.Height);
            Assert.Equal(WindowState.Normal, window.WindowState);

            window.Close();

            mlRest.Received(1).QueryOrThrow("ALVA.LIBRARY.Form", "SaveParams", Arg.Is<object[]>(a =>
                a.Length == 5 && (string)a[0] == $"formID{D}EDISON2.TestWindow"));
        }));
    }

    [Fact]
    public void Attach_ServerFails_WindowKeepsDefaultsAndOpens()
    {
        var mlRest = MLRestMocks.Create();
        mlRest.QueryOrThrow("ALVA.LIBRARY.Form", "LoadParams", Arg.Any<object[]>())
            .Returns(_ => throw new InvalidOperationException("server down"));

        Sta.RunOnAppThread(() => WithProvider(mlRest, () =>
        {
            var window = new Window
            {
                ShowInTaskbar = false, ShowActivated = false,
                Width = 222, Height = 111,
            };
            WindowStatePersistence.Attach(window, "EDISON2.TestWindow");

            window.Show();   // best-effort: chyba nesmí otevření zablokovat

            Assert.Equal(222, window.Width);
            Assert.Equal(111, window.Height);
            window.Close();
        }));
    }

    [Fact]
    public void Attach_MaximizedInXaml_RestoredWithoutSavedState()
    {
        // Attach sráží XAML Maximized na Normal kvůli HWND stylu; bez uloženého stavu
        // ho restore musí vrátit - jinak by okno zůstalo omylem odmaximalizované.
        var mlRest = MockWithSavedState();   // nic uloženého

        Sta.RunOnAppThread(() => WithProvider(mlRest, () =>
        {
            var window = new Window
            {
                ShowInTaskbar = false, ShowActivated = false,
                WindowState = WindowState.Maximized,
            };
            WindowStatePersistence.Attach(window, "EDISON2.TestWindow");
            Assert.Equal(WindowState.Normal, window.WindowState);   // sraženo před HWND

            window.Show();

            Assert.Equal(WindowState.Maximized, window.WindowState);
            window.Close();
        }));
    }
}
