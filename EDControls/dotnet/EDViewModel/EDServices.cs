using ALVA.LICENCE.EDLicence;

namespace EDControls.EDViewModel;

/// <summary>
/// Bundles the shared services that every EDViewModel needs.
/// Created once at application startup; tests create their own instance.
/// <para>
/// <see cref="MLRest"/> is mutable because the composition-root ViewModel
/// creates it after company selection and updates it on company switch.
/// All ViewModels sharing the same EDServices instance see the change.
/// </para>
/// <para>
/// <see cref="ActiveLicence"/> is mutable because the shell loads the licence
/// after login and may refresh it later (admin "Refresh licence"). ViewModels
/// read it for per-window licence checks (e.g., disabling buttons that lead
/// to unlicensed features).
/// </para>
/// </summary>
public class EDServices(
    IMLRest? mlRest,
    IWindowNavigator windowNavigator,
    IPopupDisplayer popupDisplayer)
{
    public IMLRest? MLRest { get; set; } = mlRest;
    public IWindowNavigator WindowNavigator { get; } = windowNavigator;
    public IPopupDisplayer PopupDisplayer { get; } = popupDisplayer;
    public Lic? ActiveLicence { get; set; }
    public bool IsAdmin { get; set; }

    private static EDServices? _current;

    /// <summary>
    /// Application-wide default. Set once in App.xaml.cs via <see cref="Initialize"/>.
    /// Throws if accessed before initialization (catches misuse at startup, not at test time).
    /// </summary>
    public static EDServices Current =>
        _current ?? throw new InvalidOperationException(
            "EDServices.Initialize must be called before any EDViewModel is created.");

    /// <summary>
    /// Call once from App.xaml.cs at startup. Not called from tests.
    /// </summary>
    public static void Initialize(EDServices services) =>
        _current = services;

    /// <summary>
    /// Clears the static singleton. Test-only — prevents state leaking between tests.
    /// </summary>
    internal static void Reset() => _current = null;
}
