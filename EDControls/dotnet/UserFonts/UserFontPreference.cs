using System;
using System.IO;

namespace EDControls.UserFonts;

/// <summary>
/// User-zvolený font (FontFamily + FontSize + toggle Apply) sdílený napříč všemi
/// m-line WPF nástroji - uložen v <c>%APPDATA%\m-line\common\font.json</c>.
/// Při změně shellem se přes <see cref="FileSystemWatcher"/> propaguje do otevřených
/// procesů (TemplateDesigner atd.) - viz <see cref="Changed"/> event. Mechanismus
/// (Load/Save/watcher) drží <see cref="SharedJsonPreference{T}"/>, tahle třída je jeho veřejný tvar.
///
/// Storage je úmyslně v "common" složce (ne EDISONMAIN2-specific), aby preference
/// nezůstala vázaná na konkrétní aplikaci. Nová m-line apka nic z toho nevolá přímo -
/// stačí <c>EDAppearance.Initialize(app)</c>.
/// </summary>
public class UserFontPreference
{
    /// <summary>Pokud true, <see cref="FontFamily"/> a <see cref="FontSize"/> se aplikují přes <see cref="FontService"/>.</summary>
    public bool FontApplied { get; set; }

    /// <summary>Název rodiny fontu, např. "Calibri". Null nebo prázdný = nepoužít user override.</summary>
    public string? FontFamily { get; set; }

    /// <summary>Velikost základního UI (Body) v pt. Ostatní velikosti (Small/Caption/Header/Large) se škálují proporcionálně.</summary>
    public double? FontSize { get; set; }

    /// <summary>
    /// Verze schématu. Chybí = soubor psaný verzí, která pt hodnotu vykreslovala jako px
    /// (viz <see cref="MigrateLegacy"/>). Novější verze než <see cref="CurrentVersion"/>
    /// se nechává být - starší build cizí data nepřepisuje, dokud user sám neuloží.
    /// </summary>
    public int? Version { get; set; }

    internal const int CurrentVersion = 1;

    private static readonly SharedJsonPreference<UserFontPreference> Store =
        new(MlineCommonPaths.File("font.json"), MigrateLegacy);

    /// <summary>Cesta k <c>font.json</c>. Přepisovatelná (internal seam pro testy) - produkce
    /// používá default v <c>%APPDATA%\m-line\common</c>.</summary>
    internal static string FilePath
    {
        get => Store.FilePath;
        set => Store.FilePath = value;
    }

    public static UserFontPreference Current => Store.Current;

    /// <summary>
    /// Vyvolá se po změně <c>font.json</c> z jiného procesu (typicky shell změnil user font).
    /// Marshal na UI thread je odpovědnost subscriberu - viz <see cref="FontService.Initialize"/>.
    /// </summary>
    internal static event Action? Changed
    {
        add => Store.Changed += value;
        remove => Store.Changed -= value;
    }

    internal static void Load() => Store.Load();

    /// <summary>
    /// Zahodí velikost uloženou verzí, která pt hodnotu vykreslovala jako px: uživatelé si ji
    /// tehdy nastavovali podle výsledku na obrazovce, takže to není jejich zamýšlená velikost
    /// v pt, ale kompenzace chyby - po opravě by dala nesmysl (menší než default, nebo skok
    /// o třetinu nahoru). Rodina se drží (chyba se jí netýkala), pokud je na whitelistu;
    /// symbolový font z původního pickeru se zahodí taky. Běží v paměti při každém načtení,
    /// dokud uživatel neuloží - startup záměrně nic nezapisuje.
    /// </summary>
    internal static UserFontPreference MigrateLegacy(UserFontPreference prefs)
    {
        if (prefs.Version >= CurrentVersion) return prefs;

        prefs.FontSize = null;
        if (!FontCatalog.IsAllowed(prefs.FontFamily)) prefs.FontFamily = null;
        prefs.FontApplied = prefs.FontApplied && prefs.FontFamily is not null;
        prefs.Version = CurrentVersion;
        return prefs;
    }

    public void Save()
    {
        // Co zapisujeme, je podle definice aspoň aktuální schéma - bez razítka by čerstvě
        // uloženou velikost při dalším startu zahodila MigrateLegacy. Max drží razítko
        // novějšího buildu, ať mu tenhle uložení nesrazí soubor na starší verzi.
        Version = Math.Max(Version ?? 0, CurrentVersion);
        Store.Save(this);
    }

    /// <summary>
    /// Spustí watcher na <c>font.json</c>: při změně načte aktuální hodnoty a vyvolá <see cref="Changed"/>.
    /// Idempotentní. Listener je odpovědný za marshal na UI thread (FileSystemWatcher používá thread pool).
    /// </summary>
    internal static void BeginListening() => Store.BeginListening();
}
