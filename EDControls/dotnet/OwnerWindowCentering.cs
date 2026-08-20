using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace EDControls;

/// <summary>
/// Vycentruje okno na klientskou oblast svého ownera. Přes PointToScreen(0,0) + GetClientRect,
/// protože maximalizovaný owner má v Left/Top restore bounds a WPF CenterOwner ho tam mine
/// (stejný důvod jako u EDActionInProgress). Bez ownera → střed pracovní plochy.
/// </summary>
public static class OwnerWindowCentering
{
    /// <summary>Nastaví Left/Top okna tak, aby leželo na středu klientské oblasti ownera. UI thread only.</summary>
    public static void CenterOnOwner(Window window)
    {
        var owner = window.Owner;
        if (owner is null || owner.WindowState == WindowState.Minimized)
        {
            var wa = SystemParameters.WorkArea;
            window.Left = wa.Left + (wa.Width - window.ActualWidth) / 2;
            window.Top = wa.Top + (wa.Height - window.ActualHeight) / 2;
            return;
        }

        if (PresentationSource.FromVisual(owner)?.CompositionTarget is not { } ct) return;
        if (!GetClientRect(new WindowInteropHelper(owner).Handle, out var rc)) return;

        var toDips = ct.TransformFromDevice;
        var origin = toDips.Transform(owner.PointToScreen(new Point(0, 0)));
        var client = toDips.Transform(new Point(rc.Right, rc.Bottom)); // GetClientRect: Left/Top = 0

        var topLeft = ComputeTopLeft(origin, new Size(client.X, client.Y),
            new Size(window.ActualWidth, window.ActualHeight));
        window.Left = topLeft.X;
        window.Top = topLeft.Y;
    }

    /// <summary>Levý horní roh vycentrovaného okna. Čistá aritmetika (bez WPF/HWND) - testovatelná.</summary>
    internal static Point ComputeTopLeft(Point clientOrigin, Size clientSize, Size windowSize)
        => new(clientOrigin.X + (clientSize.Width - windowSize.Width) / 2,
               clientOrigin.Y + (clientSize.Height - windowSize.Height) / 2);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT rc);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }
}
