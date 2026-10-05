using System;
using System.IO;
using EDControls.EDTheme;

namespace EDControls.Tests;

/// <summary>
/// theme.json je cross-process kontrakt (shell ↔ TemplateDesigner) - hlídá se přesný camelCase
/// tvar na disku a to, že poškozený soubor drží poslední známou hodnotu místo výjimky
/// (Load běží ze startupu před prvním oknem). Watcher hlídá <see cref="SharedJsonPreferenceTests"/>.
/// Vlastní úložiště (<see cref="UserThemePreference.CreateStore"/>) v tempu, statika procesu se nemění.
/// </summary>
public class UserThemePreferenceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"edtheme_{Guid.NewGuid():N}");

    private string FilePath => Path.Combine(_dir, "theme.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private SharedJsonPreference<UserThemePreference> StoreWith(string? initialContent)
    {
        if (initialContent != null)
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(FilePath, initialContent);
        }
        return UserThemePreference.CreateStore(FilePath);
    }

    [Fact]
    public void Load_DeserializesOnDiskCamelCaseShape()
    {
        // Pinned tvar - jméno klíče čte i druhý proces, přejmenování property ho rozbije.
        var store = StoreWith("""{"darkMode":true}""");

        store.Load();

        Assert.True(store.Current.DarkMode);
    }

    [Fact]
    public void Save_WritesCamelCaseKey()
    {
        var store = StoreWith(null);

        new UserThemePreference { DarkMode = true }.SaveTo(store);

        Assert.Contains("\"darkMode\": true", File.ReadAllText(FilePath));
    }

    [Fact]
    public void SaveLoad_RoundTrip()
    {
        var store = StoreWith(null);

        new UserThemePreference { DarkMode = true }.SaveTo(store);
        store.Load();
        Assert.True(store.Current.DarkMode);

        new UserThemePreference { DarkMode = false }.SaveTo(store);
        store.Load();
        Assert.False(store.Current.DarkMode);
    }

    [Fact]
    public void Save_CreatesMissingDirectory()
    {
        var store = StoreWith(null);

        new UserThemePreference { DarkMode = true }.SaveTo(store);

        Assert.True(File.Exists(FilePath));
    }

    [Fact]
    public void Load_CorruptFile_KeepsLastKnownValue()
    {
        var store = StoreWith("""{"darkMode":true}""");
        store.Load();
        File.WriteAllText(FilePath, "{ tohle není JSON");

        store.Load();

        // Držíme poslední hodnotu; příští Save soubor přepíše.
        Assert.True(store.Current.DarkMode);
    }

    [Fact]
    public void Load_MissingFile_DoesNotThrow()
    {
        var store = StoreWith(null);

        Assert.Null(Record.Exception(store.Load));
    }
}
