using System;
using System.IO;
using EDControls.EDTheme;

namespace EDControls.Tests;

/// <summary>
/// theme.json je cross-process kontrakt (shell ↔ TemplateDesigner) - hlídá se přesný camelCase
/// tvar na disku a to, že poškozený soubor drží poslední známou hodnotu místo výjimky
/// (Load běží ze startupu před prvním oknem). Watcher hlídá <see cref="SharedJsonPreferenceTests"/>.
/// </summary>
public class UserThemePreferenceTests
{
    /// <summary>Spustí <paramref name="body"/> s <c>UserThemePreference.FilePath</c> v tempu;
    /// po dokončení obnoví cestu i hodnotu <c>Current</c> (statický stav sdílený s ThemeService).</summary>
    private static void WithTempThemeFile(string? initialContent, Action<string> body)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"edtheme_{Guid.NewGuid():N}");
        var file = Path.Combine(dir, "theme.json");
        var savedPath = UserThemePreference.FilePath;
        var savedDark = UserThemePreference.Current.DarkMode;
        try
        {
            Directory.CreateDirectory(dir);
            if (initialContent != null) File.WriteAllText(file, initialContent);
            UserThemePreference.FilePath = file;
            body(file);
        }
        finally
        {
            UserThemePreference.FilePath = savedPath;
            UserThemePreference.Current.DarkMode = savedDark;
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Load_DeserializesOnDiskCamelCaseShape()
    {
        // Pinned tvar - jméno klíče čte i druhý proces, přejmenování property ho rozbije.
        WithTempThemeFile("""{"darkMode":true}""", _ =>
        {
            UserThemePreference.Load();

            Assert.True(UserThemePreference.Current.DarkMode);
        });
    }

    [Fact]
    public void Save_WritesCamelCaseKey()
    {
        WithTempThemeFile(null, file =>
        {
            new UserThemePreference { DarkMode = true }.Save();

            Assert.Contains("\"darkMode\": true", File.ReadAllText(file));
        });
    }

    [Fact]
    public void SaveLoad_RoundTrip()
    {
        WithTempThemeFile(null, _ =>
        {
            new UserThemePreference { DarkMode = true }.Save();
            UserThemePreference.Load();
            Assert.True(UserThemePreference.Current.DarkMode);

            new UserThemePreference { DarkMode = false }.Save();
            UserThemePreference.Load();
            Assert.False(UserThemePreference.Current.DarkMode);
        });
    }

    [Fact]
    public void Save_CreatesMissingDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"edtheme_{Guid.NewGuid():N}");
        var savedPath = UserThemePreference.FilePath;
        try
        {
            UserThemePreference.FilePath = Path.Combine(dir, "theme.json");

            new UserThemePreference { DarkMode = true }.Save();

            Assert.True(File.Exists(UserThemePreference.FilePath));
        }
        finally
        {
            UserThemePreference.FilePath = savedPath;
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Load_CorruptFile_KeepsLastKnownValue()
    {
        WithTempThemeFile("""{"darkMode":true}""", file =>
        {
            UserThemePreference.Load();
            File.WriteAllText(file, "{ tohle není JSON");

            UserThemePreference.Load();

            // Držíme poslední hodnotu; příští Save soubor přepíše.
            Assert.True(UserThemePreference.Current.DarkMode);
        });
    }

    [Fact]
    public void Load_MissingFile_DoesNotThrow()
    {
        WithTempThemeFile(null, _ => UserThemePreference.Load());
    }
}
