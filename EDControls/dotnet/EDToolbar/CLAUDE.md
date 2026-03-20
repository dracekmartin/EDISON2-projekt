# EDToolbar — poznámky pro vývojáře

Toolbar, který vykreslí ikonová tlačítka pro všechny akce definované v `EDViewModel.Actions`
okna. Žádná konfigurace v XAML pro konkrétní okno není potřeba — stačí přidat element jednou
a naplní se sám.

## Mapa souborů

| Soubor | Role |
| --- | --- |
| `EDToolbar.xaml` | `ToolBar` obsahující `ItemsControl` s `DataTemplate` pro ikonová tlačítka. |
| `EDToolbar.xaml.cs` | Pouze `InitializeComponent()`. |

## Jak funguje

`EDToolbar` nemá žádný code-behind kromě `InitializeComponent()`. `ItemsControl` binduje přímo
na `{Binding Actions}` — DataContext se dědí z rodičovského okna. Každá `EDAction` se vykreslí
jako `Button` s:

- `Command` — příkaz akce
- `ToolTip` — `EDAction.ToolTipText`, formátovaný jako `"Label [ShortcutText]"` (např. `"Přidat [Ctrl+N]"`)
- Content — 16×16 `Image` navázaný na `EDAction.Icon`

Klávesové zkratky se **neregistrují** zde. Registruje je `EDViewModel` sám na
`DispatcherPriority.Loaded` — takže klávesové zkratky fungují i když okno nemá `EDToolbar`.

## Použití

```xaml
<Window xmlns:et="clr-namespace:EDControls.EDToolbar;assembly=EDControls">
    <DockPanel>
        <et:EDToolbar DockPanel.Dock="Top"/>
        <!-- ... -->
    </DockPanel>
</Window>
```

Žádný atribut `Actions` není potřeba — toolbar si je přečte z `DataContext` automaticky.

## Podpora designeru

`EDToolbar` je v design time prázdný (žádné akce nejsou načteny). To je očekávané chování.
