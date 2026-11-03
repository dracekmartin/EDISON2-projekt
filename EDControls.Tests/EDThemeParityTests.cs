using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;

namespace EDControls.Tests;

/// <summary>
/// Drift test parity barevných tokenů: každý brush/color klíč z <c>EDTheme.xaml</c> musí mít
/// protějšek v <c>EDThemeDark.xaml</c>, jinak v dark režimu prosvítí světlá hodnota a přijde
/// se na to až vizuálně (dark dictionary se merguje navrch a přebíjí jen klíče, které zná).
/// Vědomé výjimky jsou ve <see cref="LightOnly"/> - nový token tam nepatří, dokud pro něj
/// není důvod jako u brandového pruhu.
/// </summary>
public class EDThemeParityTests
{
    // Testovací host nevytváří WPF Application → pack:// loading vyžaduje ručně triggernout
    // Application type initializer.
    static EDThemeParityTests() => _ = Application.ResourceAssembly;

    private const string LightUri = "pack://application:,,,/EDControls;component/EDTheme/EDTheme.xaml";
    private const string DarkUri  = "pack://application:,,,/EDControls;component/EDTheme/EDThemeDark.xaml";

    /// <summary>
    /// Tokeny, které v dark režimu záměrně zůstávají světlé. Brandový pruh
    /// (<c>Accent.Header</c> + <c>OnAccent.*</c> obsah na něm) a brandová plocha přihlášení
    /// (<c>BrandPanel.*</c>) drží stejnou barvu v obou režimech; <c>Module.*</c> jsou datové
    /// barvy tekoucí jako hex do HexToBrush.
    /// </summary>
    private static readonly HashSet<string> LightOnly =
    [
        "EDTheme.Accent.Header",
        "EDTheme.Accent.Header.Color",
        "EDTheme.BrandPanel.Bg",
        "EDTheme.BrandPanel.Bg.Color",
        "EDTheme.BrandPanel.TextMuted",
        "EDTheme.BrandPanel.TextMuted.Color",
        "EDTheme.BrandPanel.LinkHover",
        "EDTheme.BrandPanel.LinkHover.Color",
        "EDTheme.OnAccent.Text",
        "EDTheme.OnAccent.Text.Color",
        "EDTheme.OnAccent.HoverOverlay",
        "EDTheme.OnAccent.PressedOverlay",
        "EDTheme.Module.Stub.Color",
        "EDTheme.Module.Broken.Color",
    ];

    [Fact]
    public void EveryLightColorToken_HasDarkCounterpart()
    {
        EDTesting.Sta.Run(() =>
        {
            var light = ColorKeys(Load(LightUri));
            var dark = ColorKeys(Load(DarkUri));

            // Pojistka proti falešně zelenému testu, kdyby se dictionary přestala načítat.
            Assert.True(light.Count > 50, $"EDTheme.xaml dalo jen {light.Count} barevných klíčů");
            Assert.True(dark.Count > 50, $"EDThemeDark.xaml dalo jen {dark.Count} barevných klíčů");

            var missing = light.Except(dark).Except(LightOnly).OrderBy(k => k).ToList();

            Assert.True(missing.Count == 0,
                "Chybí v EDThemeDark.xaml: " + string.Join(", ", missing) +
                " (doplň dark hodnotu, nebo klíč zdůvodni v LightOnly)");
        });
    }

    [Fact]
    public void DarkDictionary_OverridesNothingUnknown()
    {
        // Klíč jen v dark = přejmenovaný/smazaný light token; DynamicResource by v light
        // režimu spadl na UnsetValue.
        EDTesting.Sta.Run(() =>
        {
            var light = ColorKeys(Load(LightUri));
            var extra = ColorKeys(Load(DarkUri)).Except(light).OrderBy(k => k).ToList();

            Assert.True(extra.Count == 0,
                "Klíč je v EDThemeDark.xaml, ale ne v EDTheme.xaml: " + string.Join(", ", extra));
        });
    }

    [Fact]
    public void LightOnlyList_HasNoStaleEntries()
    {
        // Výjimka na neexistující klíč = nikdy nezavolaná; maž ji s tokenem.
        EDTesting.Sta.Run(() =>
        {
            var light = ColorKeys(Load(LightUri));

            Assert.Empty(LightOnly.Except(light).OrderBy(k => k));
        });
    }

    /// <summary>Klíče, jejichž hodnota je barva nebo brush (spacing / typografie se v dark nepřepisují).</summary>
    private static HashSet<string> ColorKeys(ResourceDictionary dict)
    {
        var keys = new HashSet<string>();
        foreach (DictionaryEntry entry in dict)
        {
            if (entry.Key is not string key) continue;
            object? value;
            try { value = entry.Value; }
            catch { continue; }   // odložená hodnota, kterou samostatná dictionary neumí složit
            if (value is Brush or Color) keys.Add(key);
        }
        return keys;
    }

    private static ResourceDictionary Load(string uri) => new() { Source = new System.Uri(uri) };

}
