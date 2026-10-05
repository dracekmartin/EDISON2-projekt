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

    private static readonly SharedJsonPreference<UserThemePreference> Store = CreateStore(MlineCommonPaths.File("theme.json"));

    /// <summary>Úložiště s konfigurací <c>theme.json</c>; testy si ho skládají nad dočasným souborem.</summary>
    internal static SharedJsonPreference<UserThemePreference> CreateStore(string filePath) => new(filePath);

    public static UserThemePreference Current => Store.Current;

    /// <summary>Vyvolá se po změně <c>theme.json</c> z jiného procesu. Marshal na UI thread řeší subscriber (ThemeService).</summary>
    internal static event Action? Changed
    {
        add => Store.Changed += value;
        remove => Store.Changed -= value;
    }

    internal static void Load() => Store.Load();

    public void Save() => SaveTo(Store);

    internal void SaveTo(SharedJsonPreference<UserThemePreference> store) => store.Save(this);

    /// <summary>Spustí watcher na <c>theme.json</c>; při změně načte a vyvolá <see cref="Changed"/>. Idempotentní.</summary>
    internal static void BeginListening() => Store.BeginListening();
}
