# EDTheme — sdílené design tokens

> **Stav:** v1 (2026-05-19) — baseline tokens. Hodnoty odpovídají dnešnímu vizuálnímu stavu TemplateDesigneru; cílem v1 je infrastruktura, ne redesign.

`EDTheme.xaml` je `ResourceDictionary` v EDControls knihovně se sjednocenými barvami, typografií a spacing tokens. Konzumovat přes `DynamicResource` (umožní runtime swap, např. dark mode v budoucnu).

## Konzumace

```xml
<!-- App.xaml: merge na začátku, před ostatními resource dictionaries -->
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="pack://application:,,,/EDControls;component/EDTheme/EDTheme.xaml"/>
            <!-- ostatní -->
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>
```

Pak kdekoliv v XAML:
```xml
<Border BorderBrush="{DynamicResource EDTheme.Border.Light}"
        Background="{DynamicResource EDTheme.Bg.Panel}">
    <TextBlock Text="..."
               FontSize="{DynamicResource EDTheme.FontSize.Body}"
               Foreground="{DynamicResource EDTheme.Text.Primary}"/>
</Border>
```

## Naming convention

`EDTheme.<Group>.<Variant>` — vždy s sémantickým názvem podle účelu, ne podle hodnoty.

Bonus tokens: pro každou barvu existují **dva** klíče:
- `EDTheme.X.Color` — `Color` value (pro místa kde stačí Color, např. gradient stops)
- `EDTheme.X` — `SolidColorBrush` postavený na Color (pro Background/Foreground/BorderBrush)

## Color tokens

### Borders
| Token | Default | Použití |
|---|---|---|
| `EDTheme.Border.Light` | `#DDDDDD` | Lehké okraje panelů, sekcí, oddělovače |
| `EDTheme.Border.Medium` | `#CCCCCC` | Stránka v designeru, dividery |
| `EDTheme.Border.Strong` | `#999999` | Toolbar separátory, větší kontrast |

### Backgrounds
| Token | Default | Použití |
|---|---|---|
| `EDTheme.Bg.Panel` | `#F0F0F0` | Panel headers, status bar |
| `EDTheme.Bg.PanelAlt` | `#F5F5F5` | Section item ve výchozím stavu |
| `EDTheme.Bg.Footer` | `#FAFAFA` | Tip area, button footer |
| `EDTheme.Bg.Hover` | `#E8E8E8` | Toolbar button hover, list item hover |
| `EDTheme.Bg.Pressed` | `#D0D0D0` | Toolbar button pressed, separators |
| `EDTheme.Bg.Checked` | `#D0E0F0` | Toggle button checked stav |
| `EDTheme.Bg.Canvas` | `#E4E4E4` | Canvas background v designeru (TD-specific) |

### Text
| Token | Default | Použití |
|---|---|---|
| `EDTheme.Text.Primary` | `#333333` | Hlavní text, body |
| `EDTheme.Text.Secondary` | `#555555` | Popisky polí, sekundární text |
| `EDTheme.Text.Muted` | `#888888` | Tipy, hints, sekundární info |
| `EDTheme.Text.Header` | `#444444` | Nadpisy panelů |
| `EDTheme.Text.Disabled` | `#AAAAAA` | Disabled stav |

### Accent — Blue (primary action color)
| Token | Default | Použití |
|---|---|---|
| `EDTheme.Accent.Blue` | `#2196F3` | Focus, active section border, selection |
| `EDTheme.Accent.BlueDark` | `#0D47A1` | Text na světle modrém pozadí |
| `EDTheme.Accent.BlueBg` | `#E3F2FD` | Instance chip, info badges |
| `EDTheme.Accent.BlueSelected` | `#D2E6FF` | Vybraná sekce v listu |

### Accent — Green (agenda status, success)
| Token | Default | Použití |
|---|---|---|
| `EDTheme.Accent.GreenDark` | `#1B5E20` | Agenda chip text |
| `EDTheme.Accent.GreenBg` | `#E8F5E9` | Agenda chip background |

### Brand
| Token | Default | Použití |
|---|---|---|
| `EDTheme.Brand` | `#A07840` | M-line firemní hnědá (Molecule ikona, EDFooter) |

### Semantic
| Token | Default | Použití |
|---|---|---|
| `EDTheme.Semantic.Danger` | `#C62828` | Errors, delete actions |
| `EDTheme.Semantic.Warning` | `#FF8C00` | Warnings, locked element, dirty marker |
| `EDTheme.Semantic.Success` | `#2E7D32` | Pozitivní feedback |
| `EDTheme.Semantic.Condition` | `#7B1FA2` | VisibilityExpression badges (TD specific) |

## Typography tokens

| Token | Default | Použití |
|---|---|---|
| `EDTheme.FontSize.Small` | `9` | Ruler ticks, badge texts, group labels v toolbaru |
| `EDTheme.FontSize.Caption` | `10` | Tipy, hints |
| `EDTheme.FontSize.Body` | `11` | Defaultní velikost UI |
| `EDTheme.FontSize.Header` | `12` | Nadpisy panelů, dialog titulky |
| `EDTheme.FontSize.Large` | `14` | Emphasis (rare) |

## Spacing tokens

| Token | Default | Použití |
|---|---|---|
| `EDTheme.Spacing.XS` | `2` | Velmi těsné padding |
| `EDTheme.Spacing.S` | `4` | Padding tlačítek |
| `EDTheme.Spacing.M` | `6` | Defaultní inner padding |
| `EDTheme.Spacing.L` | `8` | Panel padding |
| `EDTheme.Spacing.XL` | `12` | Větší okraje |

## Per-aplikační override

App si může přepsat libovolný token v `App.xaml` po merge EDTheme:

```xml
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="pack://application:,,,/EDControls;component/EDTheme/EDTheme.xaml"/>
        </ResourceDictionary.MergedDictionaries>
        <!-- Per-app override -->
        <SolidColorBrush x:Key="EDTheme.Accent.Blue" Color="#FF7B1FA2"/>
    </ResourceDictionary>
</Application.Resources>
```

Po této změně všechny komponenty (EDList, EDFilter, ...) i hostitelská aplikace, které používají `{DynamicResource EDTheme.Accent.Blue}`, dostanou fialovou místo modré.

## Refactor existujícího kódu

**Princip:** Nahrazuj hex hodnoty za `{DynamicResource EDTheme.X}` tam, kde to dává sémantický smysl. **Nech intentional accents** (specifické barvy v ikonách, brand expressions, designové detaily) jako hex.

Postup:
1. Identifikuj barvu — co reprezentuje? (border? text primary? accent?)
2. Najdi odpovídající token v tabulce výše
3. Nahraď `Color="#XXX"` → `Color="{DynamicResource EDTheme.X}"`
4. Build + visual check (DynamicResource missing → render selže elegantně, ale visual může vypadat divně)

## Dark mode (budoucnost)

Tokens v `DynamicResource` = připraveno pro runtime swap. Postup:
1. Vytvořit `EDTheme.Dark.xaml` se stejnými klíči, jen dark color values
2. V `App.xaml`/`MainWindow` runtime swap merged dictionaries (Dark vs Light)
3. Aplikace překreslí automaticky

V1 (today): jen light theme; infra pro dark hotová.
