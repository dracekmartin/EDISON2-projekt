# EDFooter — developer notes

Standardised window footer as a plain `UserControl` — fully XAML-defined so it is always
visible in the WPF designer.

## File map

| File | Role |
|---|---|
| `EFooterMode.cs` | Enum: `Close` (default), `OkCancel`. |
| `EDFooter.xaml` | Border with left `ContentPresenter` + right button panel. Default visibility: Zavřít shown, Ok/Storno collapsed. |
| `EDFooter.xaml.cs` | DependencyProperties, button visibility switch, ViewModel wiring. Note: `CloseBtn` (Zavřít) also calls `CloseWindow(false)` — same handler as Storno. |

## Dependency properties

| Property | Type | Default | Description |
|---|---|---|---|
| `FooterMode` | `EFooterMode` | `Close` | Switches between Zavřít / Ok+Storno. |
| `LeftContent` | `UIElement?` | `null` | Optional element in the left column of the footer grid. Set via `LeftContent` property (not a binding — assigned directly to `LeftContentPresenter.Content`). |

## Actions menu (automatic)

When the window's `EDViewModel` exposes a non-empty `Actions` list, `EDFooter` automatically shows an **"Akce" dropdown menu** in the bottom-left corner — no XAML attribute needed. The menu is driven entirely by DataContext inheritance:

- `ActionsMenuItem` binds `ItemsSource="{Binding Actions}"`.
- `Menu` visibility binds to `ActionsMenuItem.HasItems` via `BooleanToVisibilityConverter` — the menu appears and disappears as the collection changes.
- Each item's `Header`, `Command`, `InputGestureText`, and `Icon` are set through `ItemContainerStyle` bindings on `EDAction` properties.

The `LeftContent` slot (custom buttons) sits to the right of the "Akce" menu in the same `StackPanel`.

## ViewModel integration (automatic)

On `Loaded`, `EDFooter` walks up to the parent `Window` and:
1. Subscribes to `Window.DataContextChanged`.
2. If DataContext is an `EDViewModel`, subscribes to `EDViewModel.RequestClose`.
3. When `RequestClose` fires with a non-null result, tries `window.DialogResult = result`
   (succeeds for `ShowDialog()` windows, also closes them). If that throws
   `InvalidOperationException` (i.e. a `Show()` window), falls back to `window.Close()`.
   When `RequestClose` fires with `null`, calls `window.Close()` directly.
   No flag needed on the host window.

## Usage in XAML

```xaml
<Window xmlns:ef="clr-namespace:EDControls.EDFooter;assembly=EDControls">
    <DockPanel>
        <ef:EDFooter DockPanel.Dock="Bottom" FooterMode="OkCancel">
            <ef:EDFooter.LeftContent>
                <Button Content="Nápověda" Click="Help_Click" Padding="8,4"/>
            </ef:EDFooter.LeftContent>
        </ef:EDFooter>

        <!-- main content fills remaining space -->
        <Grid>...</Grid>
    </DockPanel>
</Window>
```

## Designer support

`EDFooter` is a standard UserControl — its buttons are declared in XAML and always visible
in the WPF designer. `FooterMode` switches button visibility via a `DependencyProperty`
`PropertyChangedCallback`, which also runs at design time.

---

## Creating a new standard window

1. Add a new `Window` to EDISONMAIN2 (standard WPF, no special base class).
2. Add a `<ef:EDFooter DockPanel.Dock="Bottom" FooterMode="OkCancel"/>` at the bottom of the
   root `DockPanel`.
3. Create a ViewModel that extends `EDViewModel` (or `EDDataViewModel` if MLRest is needed).
4. In the constructor set `DataContext = new MyViewModel(...)` **before** `InitializeComponent()`.
5. Add a parameterless constructor for the WPF designer (just calls `InitializeComponent()`).

```csharp
public partial class MyWindow : Window
{
    private readonly MyViewModel? _vm;

    public MyWindow() => InitializeComponent();          // designer

    public MyWindow(MLRest mlRest)
    {
        DataContext = _vm = new MyViewModel(mlRest);
        InitializeComponent();
    }
}
```

6. Register the window class in `ReflectionWindowNavigator` (handled automatically — the
   navigator resolves windows by class name). Add a command + toolbar button in
   `EdisonMainViewModel` / `EdisonMainWindow.xaml` — follow the
   `OpenEDComponentAndViewModelTestCommand` / `EDComponentAndViewModelTestWindow` pattern.
