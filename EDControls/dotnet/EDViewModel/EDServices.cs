using ALVA.LICENCE.EDLicence;

namespace EDControls.EDViewModel;

/// <summary>
/// Sdílené služby pro každý <see cref="EDViewModel"/>. Vytváří se jednou při startu aplikace,
/// testy si dělají vlastní instanci.
/// <para>
/// <see cref="MLRest"/> je mutable — composition-root VM ji nastaví po výběru firmy a aktualizuje
/// při přepnutí. Všechny VM sdílející stejnou <see cref="EDServices"/> vidí změnu.
/// </para>
/// <para>
/// <see cref="ActiveLicence"/> je mutable — shell ji načte po loginu, případně refreshuje
/// (admin „Refresh licence"). VM ji čtou pro per-window licence checky.
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
    /// Globální default. Nastav jednou v <c>App.xaml.cs</c> přes <see cref="Initialize"/>.
    /// Vyhazuje výjimku pokud někdo přistoupí před inicializací (chytí misuse při startu).
    /// </summary>
    public static EDServices Current =>
        _current ?? throw new InvalidOperationException(
            "EDServices.Initialize must be called before any EDViewModel is created.");

    /// <summary>Volat jednou z <c>App.xaml.cs</c> při startu. Z testů se nevolá.</summary>
    public static void Initialize(EDServices services) =>
        _current = services;

    /// <summary>Smaže static singleton. Pouze pro testy — zabraňuje úniku stavu mezi testy.</summary>
    internal static void Reset() => _current = null;
}
