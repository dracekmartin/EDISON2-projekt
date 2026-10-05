using System;
using System.IO;

namespace EDControls.EDTheme;

/// <summary>
/// Sdílená volba vzhledu (zatím tmavý režim) napříč všemi m-line WPF nástroji -
/// uložená v <c>%APPDATA%\m-line\common\theme.json</c> (stejný mechanismus
/// <see cref="SharedJsonPreference{T}"/> jako <see cref="EDControls.UserFonts.UserFontPreference"/>).
/// Při změně jedním procesem se přes <see cref="FileSystemWatcher"/> propaguje do ostatních
/// (shell ↔ TemplateDesigner) - viz <see cref="Changed"/>.
///
/// Storage je úmyslně v "common" (ne app-specific), aby dark mode nebyl vázaný na jednu aplikaci.
/// Registruje se přes <see cref="ThemeService.Initialize"/>.
/// </summary>
public class UserThemePreference
{
    /// <summary>Tmavý režim UI. Default false = světlý.</summary>
    public bool DarkMode { get; set; }

    private static readonly SharedJsonPreference<UserThemePreference> Store =
        new(MlineCommonPaths.File("theme.json"));

    /// <summary>Cíl persistence. Settable jen kvůli testům (jinak by šly proti reálnému %APPDATA%).</summary>
    internal static string FilePath
    {
        get => Store.FilePath;
        set => Store.FilePath = value;
    }

    public static UserThemePreference Current => Store.Current;

    /// <summary>Vyvolá se po změně <c>theme.json</c> z jiného procesu. Marshal na UI thread řeší subscriber (ThemeService).</summary>
    internal static event Action? Changed
    {
        add => Store.Changed += value;
        remove => Store.Changed -= value;
    }

    internal static void Load() => Store.Load();

    public void Save() => Store.Save(this);

    /// <summary>Spustí watcher na <c>theme.json</c>; při změně načte a vyvolá <see cref="Changed"/>. Idempotentní.</summary>
    internal static void BeginListening() => Store.BeginListening();
}
