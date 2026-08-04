using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using FontFamily = System.Windows.Media.FontFamily;

namespace EDControls.UserFonts;

/// <summary>
/// Dialog nastavení UI fontu: rodina z whitelistu (<see cref="FontCatalog"/>) + velikost
/// v pt, s živým náhledem. Aplikace se mění až potvrzením — Storno tedy nemusí nic vracet.
/// Nahrazuje WinForms <c>FontDialog</c>, který nabízel i symbolové fonty a neukazoval,
/// co je vlastně výchozí.
/// </summary>
public partial class FontSettingsWindow : Window
{
    /// <summary>Položka comba velikostí — <see cref="Label"/> nese označení výchozí hodnoty.</summary>
    private sealed record SizeChoice(double Pt, string Label);

    private FontSettingsWindow()
    {
        InitializeComponent();
        // CenterOwner mine maximalizovaný shell (restore bounds) — centrujeme ručně po layoutu.
        Loaded += (_, _) => OwnerWindowCentering.CenterOnOwner(this);

        OkBtn.Content = Str("EDStrings.Ok", "OK");
        CancelBtn.Content = Str("EDStrings.Cancel", "Storno");
        ResetBtn.Content = $"Obnovit výchozí ({DefaultLabel})";

        var prefs = UserFontPreference.Current;
        var currentPt = FontService.OverrideBodyPt(prefs);
        FamilyCombo.ItemsSource = FontCatalog.Families(FontCatalog.InstalledFamilies(), prefs.FontFamily);
        SizeCombo.ItemsSource = FontCatalog.SizesPt(FontService.DefaultBodyPt, currentPt)
            .Select(pt => new SizeChoice(pt, FormatPt(pt) + (IsDefaultPt(pt) ? " (výchozí)" : "")))
            .ToList();

        SelectFamily(prefs.FontApplied ? prefs.FontFamily : null);
        SelectSize(currentPt);
        UpdateCurrentState(prefs);
        UpdatePreview();
    }

    private static string DefaultLabel =>
        $"{FontCatalog.DefaultFamily}, {FormatPt(FontService.DefaultBodyPt)} pt";

    // Necelá výchozí hodnota plyne z px tokenu (14 px = 10,5 pt) — nezaokrouhlovat, jinak
    // by se popisek rozešel s tím, co dialog opravdu nastaví.
    private static string FormatPt(double pt) => pt.ToString("0.##");

    private static bool IsDefaultPt(double pt) => Math.Abs(pt - FontService.DefaultBodyPt) < 0.01;

    // Sdílený string z EDStrings; fallback když slovník není mergnutý (EDControls jako knihovna).
    private static string Str(string key, string fallback)
        => Application.Current?.TryFindResource(key) as string ?? fallback;

    private void SelectFamily(string? family)
    {
        var items = (IReadOnlyList<string>)FamilyCombo.ItemsSource;
        FamilyCombo.SelectedItem = items.FirstOrDefault(f =>
                                       string.Equals(f, family, StringComparison.OrdinalIgnoreCase))
                                ?? items.FirstOrDefault(f => f == FontCatalog.DefaultFamily)
                                ?? items[0];
    }

    private void SelectSize(double? pt)
    {
        var items = (IReadOnlyList<SizeChoice>)SizeCombo.ItemsSource;
        SizeCombo.SelectedItem = items.FirstOrDefault(s => pt is { } p && Math.Abs(s.Pt - p) < 0.01)
                              ?? items.First(s => IsDefaultPt(s.Pt));
    }

    private string SelectedFamily => (string)FamilyCombo.SelectedItem;

    private double SelectedSizePt => ((SizeChoice)SizeCombo.SelectedItem).Pt;

    private void Choice_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        => UpdatePreview();

    private void UpdatePreview()
    {
        if (FamilyCombo.SelectedItem is null || SizeCombo.SelectedItem is null) return;
        var family = new FontFamily(SelectedFamily);
        var sizePx = SelectedSizePt * FontService.PtToPx;
        foreach (var block in new[] { PreviewText, PreviewDigits })
        {
            block.FontFamily = family;
            block.FontSize = sizePx;
        }
    }

    private void UpdateCurrentState(UserFontPreference prefs)
    {
        CurrentStateText.Text = prefs.FontApplied && !string.IsNullOrEmpty(prefs.FontFamily)
            ? $"Aktuálně: vlastní ({prefs.FontFamily}, {FormatPt(FontService.OverrideBodyPt(prefs) ?? FontService.DefaultBodyPt)} pt)"
            : $"Aktuálně: výchozí ({DefaultLabel})";
    }

    // Jen přehodí volbu na výchozí — aplikuje se stejně jako cokoli jiného až přes OK,
    // ať dialog nemá dvě různá pravidla, kdy se zapisuje.
    private void ResetBtn_Click(object sender, RoutedEventArgs e)
    {
        SelectFamily(FontCatalog.DefaultFamily);
        SelectSize(FontService.DefaultBodyPt);
    }

    private void OkBtn_Click(object sender, RoutedEventArgs e)
    {
        // Volba shodná s výchozí → override se rovnou uklidí, ať UI dál sleduje EDTheme tokeny
        // (jinak by změna defaultu v nové verzi uživatele s "vlastním" nastavením minula).
        var isDefault = SelectedFamily == FontCatalog.DefaultFamily && IsDefaultPt(SelectedSizePt);
        Save(applied: !isDefault);
    }

    private void Save(bool applied)
    {
        var prefs = UserFontPreference.Current;
        prefs.FontApplied = applied;
        prefs.FontFamily = SelectedFamily;
        prefs.FontSize = SelectedSizePt;
        prefs.Save();
        FontService.Apply(prefs);
        DialogResult = true;
    }

    /// <summary>Otevře dialog nad <paramref name="owner"/>; potvrzení uloží a aplikuje volbu samo.</summary>
    public static void ShowDialogFor(Window? owner)
        => new FontSettingsWindow { Owner = owner }.ShowDialog();
}
