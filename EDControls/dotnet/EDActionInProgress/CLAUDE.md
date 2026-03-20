# EDActionInProgress — poznámky pro vývojáře

Celoobrazovkový overlay zobrazovaný během dlouhé operace. Obsahuje pixel-art kapybaru
s pomerančem na zádech, která se pohybuje tam a zpět, plus nápis „Akce probíhá".

## Mapa souborů

| Soubor | Role |
| --- | --- |
| `EDActionInProgress.xaml` | Tmavý poloprůhledný overlay, bílá karta, posuvná pixel-art kapybara, text „Akce probíhá". |
| `EDActionInProgress.xaml.cs` | `IsActive` DependencyProperty; automaticky se naváže na `EDViewModel.IsActionInProgress` při `Loaded`. |

## Dependency properties

| Vlastnost | Typ | Výchozí | Popis |
| --- | --- | --- | --- |
| `IsActive` | `bool` | `false` | Zobrazí (`Visible`) nebo schová (`Collapsed`) overlay. |

## Automatické navázání (žádný explicitní XAML binding není potřeba)

Při `Loaded` se `EDActionInProgress` vyšplhá k rodičovskému `Window` a:

1. Přihlásí se k `Window.DataContextChanged`.
2. Pokud je DataContext `EDViewModel`, naváže `IsActive` → `EDViewModel.IsActionInProgress`.
3. Pokud DataContext není `EDViewModel`, odstraní binding.

To znamená, že **nepíšeš** `IsActive="{Binding IsActionInProgress}"` v XAML.
Stačí vložit control a sám se napojí.

## Použití v XAML

Overlay musí být sourozenec obsahu okna (ne uvnitř `DockPanel`), aby mohl pokrýt celé okno.
Jako kořen okna použij `Grid`:

```xaml
<Window xmlns:edaip="clr-namespace:EDControls.EDActionInProgress;assembly=EDControls">
    <Grid>
        <!-- normální obsah okna -->
        <DockPanel>
            ...
        </DockPanel>

        <!-- overlay — umístěn poslední, aby se vykreslil navrchu -->
        <edaip:EDActionInProgress/>
    </Grid>
</Window>
```

## Spuštění overlaye z ViewModelu

Použij `ActionInProgress()` z `EDViewModel` — vrací `IDisposable`, který automaticky
resetuje flag při disposal:

```csharp
// Synchronní
using var _ = ActionInProgress();
DoHeavyWork();

// Asynchronní
using var _ = ActionInProgress();
await LoadDataAsync();
```

Viz `EDViewModel/CLAUDE.md` pro kompletní popis `ActionInProgress()`.

## Pixel art — interní detaily

- 10 sloupců × 8 řádků, každý pixel = 6×6 WPF jednotek → 60×48 canvas
- Posouvá se uvnitř 160×48 clipping `Border` (`ClipToBounds="True"`)
- `TransformGroup`: `ScaleTransform` (otočení) + `TranslateTransform` (posun)
- `DoubleAnimation`: `From="-60" To="160"`, 2.5 s, `AutoReverse=True`, `RepeatBehavior=Forever`
- `DoubleAnimationUsingKeyFrames`: `ScaleX` přepne 1→−1 v t=2.5 s (čelem doleva při návratu)
- `RenderOptions.EdgeMode="Aliased"` + `SnapsToDevicePixels="True"` zabraňují sub-pixelovým mezerám
