using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace EDControls;

/// <summary>
/// Jednotné ztmavení okna (#1A000000) — stejný závoj jako EDActionInProgress. Adorner spolkne
/// myš (klik ale pořád aktivuje okno → bounce handlery fungují), Preview handlery blokují
/// klávesnici včetně window-level KeyBindings. Refcount per okno: vnořené dimy nesčítají tint.
/// </summary>
public static class WindowDimmer
{
    /// <summary>Single source of truth pro závoj — sdílí ho EDActionInProgress přes x:Static.</summary>
    public static readonly SolidColorBrush TintBrush = CreateTint();

    /// <summary>Přepnutí dim stavu okna (true = první dim, false = poslední undim). HWND obsah,
    /// který WPF veil kvůli airspace nepřekryje (WebView2), se na to napojí a po dobu dimu se
    /// schová — jinak by přes ztmavený shell zůstal viditelný i klikatelný.</summary>
    public static event Action<Window, bool>? DimStateChanged;

    private static SolidColorBrush CreateTint()
    {
        var brush = new SolidColorBrush(Color.FromArgb(0x1A, 0x00, 0x00, 0x00));
        brush.Freeze();
        return brush;
    }

    private sealed record State(AdornerLayer? Layer, DimAdorner? Adorner,
        KeyEventHandler Keys, TextCompositionEventHandler Text);

    private static readonly RefCountRegistry _refs = new();
    private static readonly Dictionary<Window, State> _states = [];

    /// <summary>Ztmaví okno do disposal vráceného scope. Pouze UI thread.</summary>
    public static IDisposable Dim(Window? owner)
    {
        if (owner is null) return EmptyScope.Instance;
        return _refs.Acquire(owner, onFirst: () => Attach(owner), onLast: () => Detach(owner));
    }

    private static void Attach(Window owner)
    {
        // Default Window template obsahuje AdornerDecorator; layer chybí jen u nezobrazeného
        // okna — pak se přeskočí vizuál, blokace vstupu platí dál.
        AdornerLayer? layer = null;
        DimAdorner? adorner = null;
        if (owner.Content is UIElement content && AdornerLayer.GetAdornerLayer(content) is { } l)
        {
            layer = l;
            adorner = new DimAdorner(content);
            layer.Add(adorner);
        }

        KeyEventHandler keys = (_, e) => e.Handled = true;
        TextCompositionEventHandler text = (_, e) => e.Handled = true;
        owner.PreviewKeyDown += keys;
        owner.PreviewTextInput += text;
        owner.Closed += OnOwnerClosed;
        _states[owner] = new State(layer, adorner, keys, text);
        DimStateChanged?.Invoke(owner, true);
    }

    private static void Detach(Window owner)
    {
        if (!_states.Remove(owner, out var st)) return;
        if (st.Layer is not null && st.Adorner is not null) st.Layer.Remove(st.Adorner);
        owner.PreviewKeyDown -= st.Keys;
        owner.PreviewTextInput -= st.Text;
        owner.Closed -= OnOwnerClosed;
        DimStateChanged?.Invoke(owner, false);
    }

    private static void OnOwnerClosed(object? sender, EventArgs e) =>
        _refs.ForceRelease((Window)sender!);

    private sealed class DimAdorner(UIElement adornedElement) : Adorner(adornedElement)
    {
        protected override void OnRender(DrawingContext dc) =>
            dc.DrawRectangle(TintBrush, null, new Rect(AdornedElement.RenderSize));
    }

    private sealed class EmptyScope : IDisposable
    {
        public static readonly EmptyScope Instance = new();
        public void Dispose() { }
    }
}

/// <summary>Refcount izolovaný od WPF (unit-testovatelný). Není thread-safe — UI thread only.</summary>
internal sealed class RefCountRegistry
{
    private sealed class Entry(Action onLast)
    {
        public int Count;
        public bool Released;
        public readonly Action OnLast = onLast;
    }

    private readonly Dictionary<object, Entry> _entries = [];

    public IDisposable Acquire(object key, Action onFirst, Action onLast)
    {
        if (!_entries.TryGetValue(key, out var entry))
        {
            _entries[key] = entry = new Entry(onLast);
            onFirst();
        }
        entry.Count++;
        return new Scope(this, key, entry);
    }

    /// <summary>Okamžité uvolnění (zavření okna) — pozdější Dispose přeživších scope je no-op.</summary>
    public void ForceRelease(object key)
    {
        if (!_entries.Remove(key, out var entry) || entry.Released) return;
        entry.Released = true;
        entry.OnLast();
    }

    private void Release(object key, Entry entry)
    {
        if (entry.Released || --entry.Count > 0) return;
        entry.Released = true;
        _entries.Remove(key);
        entry.OnLast();
    }

    private sealed class Scope(RefCountRegistry owner, object key, Entry entry) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            owner.Release(key, entry);
        }
    }
}
