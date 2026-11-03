using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace TemplateDesigner;

/// <summary>Odkazy úvodní obrazovky, které otevírají okno, záložku nebo prohlížeč.</summary>
public partial class MainWindow
{
    // Veřejné články nápovědy na podpoře M-line (seznam v .claude/wiki-map.json).
    private const string PriruckaUrl = "https://podpora.m-line.cz/support/wiki/90511fc5-10e5-476d-b7c1-3cd70672f26d";
    private const string NovinkyUrl  = "https://podpora.m-line.cz/support/wiki/6d48ca01-ce81-45d8-a396-76fcf0f982db";

    private void StartPanelManual_Click(object sender, RoutedEventArgs e) => OpenInBrowser(PriruckaUrl);

    private void StartPanelNews_Click(object sender, RoutedEventArgs e) => OpenInBrowser(NovinkyUrl);

    private void StartPanelShortcuts_Click(object sender, RoutedEventArgs e) => ShowHelp();

    /// <summary>Přepne levý panel na Pomocníka (a rozbalí ho, je-li sbalený). Úvodní obrazovka leží přes celé
    /// okno, panel by zůstal pod ní - proto se zavře.</summary>
    private void StartPanelGuide_Click(object sender, RoutedEventArgs e)
    {
        _vm.StartPanel.Hide();
        if (BtnToggleLeftPanel.IsChecked != true) BtnToggleLeftPanel.IsChecked = true;
        LeftTabControl.SelectedItem = TabAiGuide;
    }

    /// <summary>Klik vedle karty úvodní obrazovky ji zavře, jako Esc.</summary>
    private void StartPanelScrim_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => _vm.StartPanel.Hide();

    private void OpenInBrowser(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { _vm?.PopupDisplayer.Error(ex); }
    }
}
