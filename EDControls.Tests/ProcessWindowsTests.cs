using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using EDControls.EDViewModel;
using EDTesting;

namespace EDControls.Tests;

/// <summary>Hledání a vytažení okna legacy modulu proti skutečným oknům vlastního procesu.</summary>
public class ProcessWindowsTests
{
    [DllImport("user32.dll")]
    private static extern bool EnableWindow(IntPtr hwnd, bool enable);

    private static Window NewWindow() =>
        new() { ShowInTaskbar = false, ShowActivated = false, Width = 200, Height = 100 };

    [WindowFact]
    public void Find_DialogOwnedByHiddenWindow_IsFound()
    {
        // EDMessageBox z načítání agendy: vlastník je skryté okno, Process.MainWindowHandle ho nevidí.
        Sta.RunOnAppThread(() =>
        {
            var hiddenOwner = NewWindow();
            var dialog = NewWindow();
            try
            {
                var ownerHandle = new WindowInteropHelper(hiddenOwner).EnsureHandle();
                new WindowInteropHelper(dialog).Owner = ownerHandle;
                dialog.Show();

                Assert.Equal(new WindowInteropHelper(dialog).Handle, ProcessWindows.Find(Environment.ProcessId));
            }
            finally
            {
                dialog.Close();
                hiddenOwner.Close();
            }
        });
    }

    [WindowFact]
    public void Find_PrefersEnabledWindow_FallsBackToDisabled()
    {
        // Formulář pod modálním dialogem je zablokovaný a fokus musí dostat dialog.
        Sta.RunOnAppThread(() =>
        {
            var enabled = NewWindow();
            var disabledOnTop = NewWindow();
            disabledOnTop.Topmost = true;
            try
            {
                enabled.Show();
                disabledOnTop.Show();
                var disabledHandle = new WindowInteropHelper(disabledOnTop).Handle;
                EnableWindow(disabledHandle, false);

                Assert.Equal(new WindowInteropHelper(enabled).Handle, ProcessWindows.Find(Environment.ProcessId));

                enabled.Hide();
                Assert.Equal(disabledHandle, ProcessWindows.Find(Environment.ProcessId));
            }
            finally
            {
                EnableWindow(new WindowInteropHelper(disabledOnTop).Handle, true);
                disabledOnTop.Close();
                enabled.Close();
            }
        });
    }

    [WindowFact]
    public void Activate_MinimizedWindow_IsRestored()
    {
        Sta.RunOnAppThread(() =>
        {
            var window = NewWindow();
            try
            {
                window.Show();
                window.WindowState = WindowState.Minimized;

                ProcessWindows.Activate(new WindowInteropHelper(window).Handle);

                // Obnova jde frontou zpráv okna (ShowWindowAsync), dispatcher ji musí zpracovat.
                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (window.WindowState != WindowState.Normal && DateTime.UtcNow < deadline)
                {
                    var frame = new DispatcherFrame();
                    window.Dispatcher.BeginInvoke(DispatcherPriority.Background, () => frame.Continue = false);
                    Dispatcher.PushFrame(frame);
                }
                Assert.Equal(WindowState.Normal, window.WindowState);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Find_UnknownPid_ReturnsZero()
    {
        // SystemLegacyProcess.Id vrací 0, když pid zjistit nejde - pid 0 je systémový proces, ne modul.
        Assert.Equal(IntPtr.Zero, ProcessWindows.Find(0));
    }
}
