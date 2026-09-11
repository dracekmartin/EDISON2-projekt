---
name: audit-theme
description: Static XAML theming audit of the given projects (args = project list, e.g. EDISONMAIN2 EDControls) — Style without BasedOn, hardcoded colors/FontSize, tokens missing in EDThemeDark, StaticResource on colors. Reports file:line, applies fixes.
---

# audit-theme

Args = projects to audit (`EDISONMAIN2`, `EDControls`, `TemplateDesigner`, …); none given → ask.
Project scope = top-level dirs `X/` + `X.*/` (Tests, LegacyMiniApps, …). Scan all `.xaml` there
(skip `bin/obj`). Dicts:
`EDControls/dotnet/EDTheme/EDTheme.xaml` (light + neutral FontSize/IconSize/FontFamily),
`EDThemeDark.xaml` (dark **color** overrides only); shared styles `EDControls/dotnet/EDControlStyles/EDControlStyles.xaml`.
WHY: WPF picks ONE style (no merge) — local Style without BasedOn = default template = white-on-white
in dark + lost font scaling; StaticResource/literal colors ignore the runtime theme swap.

Checks (`rg -n`; report `path:line` + fix):
1. `<Style(?![^>]*BasedOn)[^>]*TargetType` — plus `ItemContainerStyle`/`ContextMenu`/`Popup`/`CellStyle`
   (own visual tree = worst offenders). Fix: `BasedOn="{StaticResource {x:Type X}}"` or move to EDControlStyles.
2. `(Background|Foreground|BorderBrush|Fill|Stroke)="#` + named brushes (`White|Black|Gray…|Transparent`
   — Transparent often bleeds the wrong surface). Data/decorative/external (WebView2, WinForms) OK.
3. `FontSize="[0-9]` → `EDTheme.FontSize.Small|Caption|Body|Header|Large` (+ FontFamily, IconSize.*).
4. `StaticResource EDTheme\.(Bg|Text|Border|Field|Accent|Icon)` on a brush property → DynamicResource.
   (Neutral FontSize/IconSize/FontFamily via StaticResource is fine.)
5. Each brush key used must exist in BOTH dicts; light-only → add dark override (neutral tokens
   light-only OK). Token missing entirely → add to both.

Severity: missing BasedOn / Transparent bleed first. Apply the fixes. Static scan misses stock
templates hardcoding white (DatePicker/Calendar/ComboBox popup; CalendarStyle null trap) → escalate `/theme-render`.

## Record
Write-tool update `<repo>/.claude/last-audit.json`, key `"audit-theme"`: `{last: <date -Iseconds>,
scope: <projects>, issues, session}`; keep other keys.
