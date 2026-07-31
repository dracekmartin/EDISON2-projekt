using ALVA.LICENCE.EDLicence;
using EDControls.UserPrefs;

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

    /// <summary>
    /// <see cref="MLRest"/> pro post-login konzumenty, kteří bez session nemají co dělat -
    /// místo `MLRest!` a NRE dá čitelnou výjimku. Controls (<c>EDList</c>, <c>EDInputForm</c>)
    /// tohle nepoužívají, mají vlastní hlášku pro hosta v <c>EnsureConfigured</c>.
    /// </summary>
    public IMLRest RequireMLRest => MLRest ?? throw new InvalidOperationException(
        "MLRest není k dispozici - přístup před loginem (EDServices.MLRest je null).");

    public IWindowNavigator WindowNavigator { get; } = windowNavigator;
    public IPopupDisplayer PopupDisplayer { get; } = popupDisplayer;
    public Lic? ActiveLicence { get; set; }
    /// <summary>
    /// Loader aktuální licence ze serveru. Composition-root nastavuje při <see cref="MLRest"/>
    /// swapu (initial bootstrap, S2 user switch), takže consumeři čtou loader vázaný na current
    /// MLRest — bez stale captures z ctoru.
    /// </summary>
    public Func<Lic?>? LicenceLoader { get; set; }
    /// <summary>Aplikační přístup uživatele (per-IdFa). Načteno post-login, refresh při switch firmy.</summary>
    public AccessTable? AccessTable { get; set; }
    public bool IsAdmin { get; set; }

    /// <summary>
    /// Jméno a verze aplikace pro „Informace o verzi" (panel Podpora, menu Nápověda).
    /// Nastavuje composition-root; EDControls na verzi nedosáhne sama (<c>VERZE.INF</c>
    /// čte shell). Nenastaveno → fallback na entry assembly, tedy nepravdivá 1.0.0.0.
    /// </summary>
    public string? AppDisplayName { get; set; }
    public string? AppVersion { get; set; }

    /// <summary>
    /// Persistent per-user state (column layouty, hidden položky, ...). Default je
    /// no-op <see cref="NullUserPrefsService"/>; composition-root nastaví
    /// <see cref="CacheUserPrefsService"/> po vytvoření <see cref="MLRest"/>.
    /// </summary>
    public IUserPrefsService UserPrefs { get; set; } = new NullUserPrefsService();

    private static EDServices? _current;

    /// <summary>
    /// Globální default. Nastav jednou v <c>App.xaml.cs</c> přes <see cref="Initialize"/>.
    /// Vyhazuje výjimku pokud někdo přistoupí před inicializací (chytí misuse při startu).
    /// </summary>
    public static EDServices Current =>
        _current ?? throw new InvalidOperationException(
            "EDServices.Initialize must be called before any EDViewModel is created.");

    /// <summary>
    /// Jako <see cref="Current"/>, ale bez výjimky. Pro infrastrukturu, která běží na každé okno
    /// a nesmí kvůli pořadí startupu nic shodit (panel Podpora) — chybějící služby = neděláme nic.
    /// </summary>
    internal static EDServices? CurrentOrNull => _current;

    /// <summary>Volat jednou z <c>App.xaml.cs</c> při startu. Z testů se nevolá.</summary>
    public static void Initialize(EDServices services) =>
        _current = services;

    /// <summary>Smaže static singleton. Pouze pro testy — zabraňuje úniku stavu mezi testy.</summary>
    internal static void Reset() => _current = null;
}
