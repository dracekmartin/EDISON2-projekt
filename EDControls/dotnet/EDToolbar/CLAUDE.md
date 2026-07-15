# EDToolbar — poznámky pro vývojáře

Toolbar vykreslující ikonová tlačítka pro `EDViewModel.Actions` okna. Stačí vložit
element — `ItemsControl` binduje na `Actions` z děděného DataContext, žádná per-window
konfigurace. Dědí z WPF `ToolBar` (vlastní chrome tlačítek + overflow chování zdarma).

## Neodvoditelné

- **Klávesové zkratky se neregistrují tady** — dělá to `EDViewModel.AttachToWindow`,
  takže fungují i v okně bez toolbaru.
- Velikost ikon není konstanta — bere se z tokenu `EDTheme.IconSize.Default`
  (`DynamicResource`).
- V design-time je toolbar záměrně prázdný (Actions se plní až za běhu) — očekávané,
  ne bug.
- Tlačítka bindují sdílený `EDAction.Icon` (`ImageSource`) — pro menu existuje
  `IconElement`, protože `UIElement` smí mít jen jednoho rodiče (viz EDViewModel doc).
