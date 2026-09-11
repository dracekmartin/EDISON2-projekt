---
name: theme-render
description: Render a XAML fragment offscreen in light+dark to PNGs (tools/ThemeRender) and inspect them — ground truth for theming bugs static audit can't see (stock templates, CalendarStyle trap).
---

# theme-render

When /audit-theme is clean but dark mode still looks wrong (stock WPF templates hardcode white in
VisualStates; `DatePicker.CalendarStyle` null suppresses implicit style) — render and LOOK. Applies
real theme dicts like ThemeService (base = light; + EDThemeDark = dark).

1. Loose XAML fragment (no x:Class): existing file, or write a small wrapper (see
   `tools/ThemeRender/samples/sample.xaml`; custom controls via `xmlns:ed=…;assembly=EDControls`).
   Controls needing EDServices/MLRest won't load — extract the visual/style, stub the DataContext.
2. `cd tools/ThemeRender && dotnet run -- <fragment.xaml> <outDir> [--width 360]` → prints
   `<name>.light.png` + `<name>.dark.png`.
3. Read both PNGs: dark bg / light text, contrast, no white-on-white, no Transparent bleed, popups themed.
4. Fix (usually BasedOn/token — /audit-theme rules), re-render to confirm.
