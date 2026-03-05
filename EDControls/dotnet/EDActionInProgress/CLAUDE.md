# EDActionInProgress — developer notes

Full-screen overlay shown while a long operation is running. Contains a pixel-art capybara
with an orange on its back that slides back and forth, plus a "Akce probíhá" label.

## File map

| File | Role |
|---|---|
| `EDActionInProgress.xaml` | Dark semi-transparent overlay, white card, sliding pixel-art capybara, "Akce probíhá" text. |
| `EDActionInProgress.xaml.cs` | `IsActive` DependencyProperty; auto-binds to `EDViewModel.IsActionInProgress` on `Loaded`. |

## Dependency properties

| Property | Type | Default | Description |
|---|---|---|---|
| `IsActive` | `bool` | `false` | Shows (`Visible`) or hides (`Collapsed`) the overlay. |

## Auto-binding (no explicit XAML binding needed)

On `Loaded`, `EDActionInProgress` walks up to the parent `Window` and:
1. Subscribes to `Window.DataContextChanged`.
2. If DataContext is an `EDViewModel`, binds `IsActive` → `EDViewModel.IsActionInProgress`.
3. If the DataContext is not an `EDViewModel`, clears the binding.

This means you **do not** write `IsActive="{Binding IsActionInProgress}"` in XAML.
Just drop the control in and it wires itself up.

## Usage in XAML

The overlay must be a sibling of the window content (not inside a `DockPanel`) so it can
cover the whole window. Use a `Grid` as the window root:

```xaml
<Window xmlns:edaip="clr-namespace:EDControls.EDActionInProgress;assembly=EDControls">
    <Grid>
        <!-- normal window content -->
        <DockPanel>
            ...
        </DockPanel>

        <!-- overlay — placed last so it renders on top -->
        <edaip:EDActionInProgress/>
    </Grid>
</Window>
```

## Triggering the overlay from the ViewModel

Use `ActionInProgress()` from `EDViewModel` — it returns an `IDisposable` that
automatically resets the flag on disposal:

```csharp
// Synchronous
using var _ = ActionInProgress();
DoHeavyWork();

// Async
using var _ = ActionInProgress();
await LoadDataAsync();
```

See `EDViewModel/CLAUDE.md` for full details on `ActionInProgress()`.

## Pixel art internals

- 10 cols × 8 rows, each pixel = 6×6 WPF units → 60×48 canvas
- Slides inside a 160×48 clipping `Border` (`ClipToBounds="True"`)
- `TransformGroup`: `ScaleTransform` (flip) + `TranslateTransform` (slide)
- `DoubleAnimation`: `From="-60" To="160"`, 2.5 s, `AutoReverse=True`, `RepeatBehavior=Forever`
- `DoubleAnimationUsingKeyFrames`: `ScaleX` snaps 1→−1 at t=2.5 s (facing left on return)
- `RenderOptions.EdgeMode="Aliased"` + `SnapsToDevicePixels="True"` prevent sub-pixel gaps
