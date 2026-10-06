using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using EDControls.EDViewModel;

namespace EDControls;

/// <summary>
/// Uložená geometrie okna. Každá složka je volitelná - chybějící hodnota znamená "neuloženo"
/// a okno si na jejím místě nechá XAML default.
/// </summary>
public readonly record struct WindowGeometry(
    double? Left, double? Top, double? Width, double? Height, bool? Maximized)
{
    /// <summary>Nic uloženého.</summary>
    public static WindowGeometry None => default;

    public bool IsEmpty => Left is null && Top is null && Width is null && Height is null && Maximized is null;
}

/// <summary>
/// Úložiště geometrie jednoho okna. Implementace drží identitu okna (formID, klíč v JSON),
/// <see cref="WindowStatePersistence"/> řeší jen práci se samotným oknem. Load i Save smí házet -
/// persistence je best-effort a výjimku spolkne.
/// </summary>
public interface IWindowStateStore
{
    /// <summary><see cref="WindowGeometry.None"/> = nic uloženo.</summary>
    WindowGeometry Load();

    void Save(Rect bounds, bool maximized);
}

/// <summary>
/// Čte z <paramref name="inner"/>, zápis zahazuje. Pro okna, která uloženou geometrii jen
/// přebírají: přihlašovací okno sdílí záznam se shellem, ale zapisovat ho má shell (a jednou
/// bootstrapper hned po přihlášení) - jinak by do něj zapsal svůj přechodný stav dialog,
/// který uživatel jen přetáhl a zavřel.
/// </summary>
public sealed class ReadOnlyWindowStateStore(IWindowStateStore inner) : IWindowStateStore
{
    public WindowGeometry Load() => inner.Load();

    public void Save(Rect bounds, bool maximized) { }
}

/// <summary>
/// Geometrie okna přes existující Caché endpoint <c>ALVA.LIBRARY.Form.SaveParams</c> /
/// <c>LoadParams</c>. Storage je per-MLP × IDFa (server čte IDFa ze sys kontextu MLRest).
/// Wire je <c>object[]</c>, ne CVar - legacy endpoint posílá IN i OUT jako číslované řádky
/// <c>"klíč{del}hodnota"</c>.
/// </summary>
/// <remarks>
/// FormID se default odvozuje z názvu VM (viz <see cref="EDViewModel.PersistedFormId"/>)
/// a dostává prefix <c>EDServices.FormIdPrefix</c> (shell <c>"EDISON2."</c>) - odděluje od starých
/// numerických IDs z <c>EDISFormEnum</c> (tam VB moduly ukládají vlastní state).
/// </remarks>
public sealed class CacheWindowStateStore(string formId, Func<IMLRest?> mlRestSource) : IWindowStateStore
{
    /// <summary>Session ze sdíleného <c>EDServices.Current</c>; okno s ViewModelem dostává MLRest jeho služeb.</summary>
    public CacheWindowStateStore(string formId) : this(formId, () => MLRestProvider()) { }

    /// <summary>Settable jen kvůli testům - produkce čte sdílené <c>EDServices.Current</c>.</summary>
    internal static Func<IMLRest?> MLRestProvider { get; set; } = () => EDServices.Current.MLRest;

    public WindowGeometry Load()
    {
        var mlRest = mlRestSource();
        if (mlRest is null) return WindowGeometry.None;
        var del = mlRest.Delimiter;

        var input = new object[]
        {
            $"formID{del}{formId}",
            $"ciselnik{del}state",
        };
        return ToGeometry(ParseOutput(mlRest.QueryOrThrow("ALVA.LIBRARY.Form", "LoadParams", input), del));
    }

    public void Save(Rect bounds, bool maximized)
    {
        var mlRest = mlRestSource();
        if (mlRest is null) return;
        var del = mlRest.Delimiter;

        mlRest.QueryOrThrow("ALVA.LIBRARY.Form", "SaveParams",
                            BuildSaveParams(formId, bounds, maximized ? 1 : 0, del));
    }

    internal static object[] BuildSaveParams(string formId, Rect bounds, int stateInt, char del) =>
    [
        $"formID{del}{formId}",
        $"clear{del}1",
        $"param{del}location{del}{bounds.Left:0};{bounds.Top:0}",
        $"param{del}size{del}{bounds.Width:0};{bounds.Height:0}",
        $"param{del}state{del}{stateInt}",
    ];

    internal static Dictionary<string, string> ParseOutput(object[] output, char del)
    {
        var dict = new Dictionary<string, string>();
        foreach (var parts in LegacyOutput.Rows(output, del))
            if (parts.Length >= 3 && parts[0] == "param")
                dict[parts[1]] = parts[2];
        return dict;
    }

    internal static WindowGeometry ToGeometry(Dictionary<string, string> prefs)
    {
        double? left = null, top = null, width = null, height = null;
        bool? maximized = null;

        if (prefs.TryGetValue("location", out var loc) && TryParsePair(loc, out var x, out var y))
            (left, top) = (x, y);
        if (prefs.TryGetValue("size", out var size) && TryParsePair(size, out var w, out var h))
            (width, height) = (w, h);
        if (prefs.TryGetValue("state", out var state))
            maximized = state == "1";

        return new(left, top, width, height, maximized);
    }

    internal static bool TryParsePair(string s, out double a, out double b)
    {
        a = b = 0;
        var parts = s.Split(';');
        return parts.Length == 2
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out a)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out b);
    }
}
