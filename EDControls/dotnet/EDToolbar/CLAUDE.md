# EDToolbar — developer notes

Toolbar that renders icon buttons for all actions defined in the window's `EDViewModel.Actions`.
No per-window XAML configuration needed — add the element once and it populates itself.

## File map

| File | Role |
| --- | --- |
| `EDToolbar.xaml` | `ToolBar` containing an `ItemsControl` with a `DataTemplate` for icon buttons. |
| `EDToolbar.xaml.cs` | `Loaded` handler that reads `Actions` from the parent window's `EDViewModel` DataContext. |

## How it works

`EDToolbar` has no code-behind beyond `InitializeComponent()`. `ItemsControl` binds directly
to `{Binding Actions}` — DataContext is inherited from the parent window. Each `EDAction`
is rendered as a `Button` with:

- `Command` — the action's command
- `ToolTip` — `EDAction.ToolTipText`, formatted as `"Label [ShortcutText]"` (e.g. `"Přidat [Ins]"`)
- Content — a 16×16 `Image` bound to `EDAction.Icon`

Key bindings are **not** registered here. They are registered by `EDViewModel` itself on
`DispatcherPriority.Loaded` — so keyboard shortcuts work even if the window has no `EDToolbar`.

## Usage

```xaml
<Window xmlns:et="clr-namespace:EDControls.EDToolbar;assembly=EDControls">
    <DockPanel>
        <et:EDToolbar DockPanel.Dock="Top"/>
        <!-- ... -->
    </DockPanel>
</Window>
```

No `Actions` attribute needed — the toolbar reads it from `DataContext` automatically.

## Designer support

`EDToolbar` is empty at design time (no actions are loaded). This is expected.
