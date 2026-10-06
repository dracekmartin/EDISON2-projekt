# Pruh podpory - poznámky pro vývojáře

Widgety `podpora.m-line.cz` (kanál IT → uživatelé) v hlavním i přihlašovacím okně. Zbytek shellu
v [../../../../CLAUDE.md](../../../../CLAUDE.md), dock miniaplikací v [MiniApps/CLAUDE.md](../../../MiniApps/CLAUDE.md).

- Pruh je v shellu (`Border SupportStrip` v `EdisonMainWindow.xaml`) **mimo dock**, aby ho nešlo zavřít; garanci drží
  podlaha `SupportRow.MinHeight`, plochu naopak `ShellSplitLimits`. `EDISON_SCREENSHOT_MODE=1` nahradí
  widgety neutrálním popiskem (anonymizované screenshoty, WebView2 se vůbec nevytvoří).
- Hostí se přes `FontZoomWebView : WebView2` - **HWND, ne composition**, ta crashuje na dragu tabu docku
  do plovoucího okna. Airspace: WPF veil dimu HWND nepokryje, kartu „Akce probíhá" nad ním kreslí
  owned okno ([EDActionInProgress/CLAUDE.md](../../../../../EDControls/dotnet/EDActionInProgress/CLAUDE.md)).
- Font widgetu: `ZoomFactor = FontService.BodySize / 14` (základní text je 14 CSS px), rodina
  se do HTML nepropaguje. Dark mode widget sám neumí (natvrdo `bg-white`) - injektuje se
  inverzní CSS filtr (`ThemeService.Applied`) + tmavé `DefaultBackgroundColor` proti bílému záblesku.
