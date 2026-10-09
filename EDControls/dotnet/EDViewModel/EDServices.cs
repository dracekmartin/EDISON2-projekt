using System.Reflection;
using ALVA.LICENCE.EDLicence;
using EDControls.SessionState;
using EDControls.UserPrefs;

namespace EDControls.EDViewModel;

/// <summary>
/// Sdílené služby pro každý <see cref="EDViewModel"/>. Vytváří se jednou při startu aplikace,
/// testy si dělají vlastní instanci. <see cref="MLRest"/> i <see cref="ActiveLicence"/> jsou
/// mutable - shell je nastaví po výběru firmy / po loginu a aktualizuje při přepnutí, takže
/// změnu vidí všechny VM sdílející tuhle instanci.
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
    /// MLRest - bez stale captures z ctoru.
    /// </summary>
    public Func<Lic?>? LicenceLoader { get; set; }
    /// <summary>Aplikační přístup uživatele (per-IdFa). Načteno post-login, refresh při switch firmy.</summary>
    public AccessTable? AccessTable { get; set; }
    public bool IsAdmin { get; set; }
    /// <summary>Právo aktualizovat serverovou instalaci EDISONu (USERS „UpdateEdison"); propaguje se legacy formám jako MLProps.UserMayUpdate.</summary>
    public bool MayUpdate { get; set; }
    /// <summary>Osobní (export) adresář uživatele z loginu (USERS feature 7, surově - příznak „použít" se
    /// neaplikuje, parita s legacy shellem); legacy formám jde jako MLProps.UserDirectory. Prázdný = nenastaven.</summary>
    public string UserDirectory { get; set; } = "";
    /// <summary>E-mail uživatele z loginu; legacy formám jde jako MLProps.UserEmail (výchozí odesílatel).</summary>
    public string UserEmail { get; set; } = "";

    /// <summary>
    /// Jméno a verze aplikace pro „Informace o verzi" (panel Podpora, menu Nápověda), feedback
    /// a hlášení chyb. Nastavuje composition-root; EDControls na verzi nedosáhne sama (<c>VERZE.INF</c>
    /// čte shell). Nenastaveno → <see cref="AppIdentity"/> vezme jméno a verzi entry assembly.
    /// </summary>
    public string? AppDisplayName { get; set; }
    public string? AppVersion { get; set; }

    /// <summary>
    /// Prefix form ID (persistence stavu okna, usage telemetrie) a <c>source</c> hlášení chyb -
    /// odděluje okna jednotlivých m-line aplikací od sebe i od legacy numerických ID z
    /// <c>EDISFormEnum</c>. Nastavuje composition-root (TemplateDesigner <c>"TD"</c>), default
    /// patří shellu a je součástí uložených formID, proto se nemění.
    /// </summary>
    public string FormIdPrefix { get; set; } = AppIdentity.DefaultPrefix;

    /// <summary>
    /// Persistent per-user state (column layouty, hidden položky, ...). Default je
    /// no-op <see cref="NullUserPrefsService"/>; composition-root nastaví
    /// <see cref="CacheUserPrefsService"/> po vytvoření <see cref="MLRest"/>.
    /// </summary>
    public IUserPrefsService UserPrefs { get; set; } = new NullUserPrefsService();

    /// <summary>
    /// Přechodný stav sdílený controly po dobu běhu procesu (naposledy hledaný výraz, ...).
    /// Nic neukládá - co má přežít restart, patří do <see cref="UserPrefs"/>.
    /// </summary>
    public ISessionState SessionState { get; set; } = new InMemorySessionState();

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
    /// a nesmí kvůli pořadí startupu nic shodit (panel Podpora) - chybějící služby = neděláme nic.
    /// </summary>
    internal static EDServices? CurrentOrNull => _current;

    /// <summary>Volat jednou z <c>App.xaml.cs</c> při startu. Z testů se nevolá.</summary>
    public static void Initialize(EDServices services) =>
        _current = services;

    /// <summary>Smaže static singleton. Pouze pro testy - zabraňuje úniku stavu mezi testy.</summary>
    internal static void Reset() => _current = null;
}

/// <summary>
/// Prefix, jméno a verze aplikace, jak je vidí hlášení chyb, „Informace o verzi" a feedback panelu
/// Podpora. Všechna tři místa doplňují chybějící hodnotu stejně: hodnota z <see cref="EDServices"/>,
/// pak entry assembly, pak konstanta. Entry assembly kryje aplikace, které hodnotu nenastaví (TD
/// verzi), a pád před <see cref="EDServices.Initialize"/>.
/// </summary>
internal sealed record AppIdentity(string Prefix, string DisplayName, string Version)
{
    internal const string DefaultPrefix = "EDISON2";
    internal const string DefaultDisplayName = "M-line";
    internal const string UnknownVersion = "nezjištěna";

    // SDK přidá za "+" celý hash commitu, na dohledání buildu stačí začátek.
    private const int BuildMetadataLength = 8;

    internal static AppIdentity Resolve(EDServices? services, Assembly? entry) =>
        Resolve(services?.FormIdPrefix, services?.AppDisplayName, services?.AppVersion, entry);

    internal static AppIdentity Resolve(string? prefix, string? displayName, string? version, Assembly? entry)
    {
        var entryName = Blank(entry?.GetName().Name);
        var entryVersion = Blank(entry?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

        return new AppIdentity(
            Blank(prefix) ?? entryName ?? DefaultPrefix,
            Blank(displayName) ?? entryName ?? DefaultDisplayName,
            Blank(version) ?? (entryVersion is null ? UnknownVersion : ShortenBuildMetadata(entryVersion)));
    }

    /// <summary><c>1.0.0+3a7b99dff455…</c> → <c>1.0.0+3a7b99df</c>, verze bez metadat zůstane.</summary>
    internal static string ShortenBuildMetadata(string version)
    {
        var end = version.IndexOf('+') + 1 + BuildMetadataLength;
        return version.Contains('+') && version.Length > end ? version[..end] : version;
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
