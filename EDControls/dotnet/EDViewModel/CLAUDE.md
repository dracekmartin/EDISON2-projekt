# EDViewModel — developer notes

Base ViewModel classes for all EDISONMAIN2 windows. Provides `INotifyPropertyChanged`,
`RelayCommand`, and a standardised Ok/close mechanism that integrates with `EDFooter`.

## File map

| File | Role |
| --- | --- |
| `EDViewModel.cs` | Base class. `INotifyPropertyChanged`, `Set<T>()`, `OkCommand`, `RequestClose`, `Actions`, `MLRest`. |
| `RelayCommand.cs` | `RelayCommand` (no parameter) and `RelayCommand<T>`. |
| `EDAction.cs` | Model for a single toolbar/menu/shortcut action (label, icon, key, command). |
| `EDStandardActions.cs` | Factory methods for standard Add/Edit/Delete actions. |

## EDViewModel API

| Member | Description |
| --- | --- |
| `event Action<bool?>? RequestClose` | Fired to close the window. `true` = confirmed, `false` = cancelled, `null` = just close. `EDFooter` subscribes automatically on `Loaded`. |
| `ICommand OkCommand` | Calls `OnOk()`. Bound to the Ok button by `EDFooter`. |
| `ICommand AddCommand / EditCommand / DeleteCommand` | Call `OnAdd/OnEdit/OnDelete()`. Pass to `EDStandardActions.Add/Edit/Delete(this)`. |
| `IReadOnlyList<EDAction>? Actions` | Actions for this window. Set in the subclass constructor. `EDToolbar` reads this on `Loaded`; `EDFooter` binds to it via DataContext. Key bindings are registered automatically by `EDViewModel` itself (see below). |
| `protected virtual void OnOk()` | Override for validation. Call `base.OnOk()` or `CloseDialog(true)` to proceed. Return without calling base to keep the window open. |
| `protected virtual void OnAdd/OnEdit/OnDelete()` | Override to handle the standard CRUD actions. No-op by default. |
| `protected void CloseDialog(bool? result)` | Fires `RequestClose`. Usable from any method, not only `OnOk`. |
| `protected bool Set<T>(ref T field, T value)` | Sets the backing field and fires `PropertyChanged` if the value changed. Returns `true` if it changed. |
| `protected void OnPropertyChanged(string? name)` | Manually fire `PropertyChanged` — used for computed properties that depend on another. |
| `bool IsActionInProgress` | Read-only from outside. Set only via `ActionInProgress()`. Drives `EDActionInProgress` overlay visibility automatically. |
| `IDisposable ActionInProgress()` | Sets `IsActionInProgress = true` and returns a scope that resets it on disposal. Use with `using`. |
| `MLRest? MLRest` | Static — shared across all ViewModels. Set once at application startup. Use directly for Caché calls. |

### Key binding auto-registration

`EDViewModel` schedules `RegisterKeyBindings()` via `Dispatcher.BeginInvoke` at `DispatcherPriority.Loaded`. At that point the full constructor chain has completed and the window is in `Application.Current.Windows`. The method finds the window whose `DataContext == this` and adds a `KeyBinding` for every action. **No work is needed in any view** — actions declared in the ViewModel are keyboard-accessible automatically, even if the window has no `EDToolbar` or `EDFooter`.

## EDStandardActions — standard action factory

```csharp
// Simplest — override OnAdd/OnEdit/OnDelete in the ViewModel:
Actions = [EDStandardActions.Add(this), EDStandardActions.Edit(this), EDStandardActions.Delete(this)];

protected override void OnAdd() { /* ... */ }
protected override void OnEdit() { /* ... */ }
protected override void OnDelete() { /* ... */ }

// With explicit command (e.g. for CanExecute):
Actions = [EDStandardActions.Add(new RelayCommand(OnAdd, CanAdd)), ...];
```

`EDStandardActions.Add/Edit/Delete` pre-fill label (`EDStrings`), icon (`EDIcons`), and keyboard
shortcut (`Ctrl+N` / `Ctrl+E` / `Ctrl+D`). The `(this)` overload binds to `EDViewModel.AddCommand/EditCommand/DeleteCommand`,
which call the corresponding `OnAdd/OnEdit/OnDelete` virtual methods. Use `new EDAction(...)` directly
only for non-standard actions.

## EDAction

```csharp
// Full — label from EDStrings, icon from EDIcons, keyboard shortcut:
new EDAction(
    (string)Application.Current.FindResource("EDStrings.Add"),
    new RelayCommand(OnAdd),
    (ImageSource)Application.Current.FindResource("EDIcons.Add"),
    Key.Insert)

// With shortcut, no icon:
new EDAction((string)Application.Current.FindResource("EDStrings.Add"), new RelayCommand(OnAdd), key: Key.Insert)

// Label + command only:
new EDAction((string)Application.Current.FindResource("EDStrings.Add"), new RelayCommand(OnAdd))
```

`icon`, `key`, and `modifiers` are all optional (`null` / `Key.None` / `ModifierKeys.None`).
Use `EDStrings` keys for labels and `EDIcons` keys for icons so text and graphics are consistent across windows.

| Property | Type | Description |
| --- | --- | --- |
| `Label` | `string` | Display name (menu header, tooltip prefix). |
| `Command` | `ICommand` | The command to execute. |
| `Icon` | `ImageSource?` | Shared image source from resource dictionary. Optional. |
| `IconElement` | `Image?` | Returns a **new** `Image` wrapping `Icon` on every access — required for `MenuItem.Icon` binding via `ItemContainerStyle` (see source comment). |
| `Key` / `Modifiers` | `Key` / `ModifierKeys` | Keyboard shortcut. Optional — omit or pass `Key.None` for no binding. |
| `ShortcutText` | `string` | Human-readable shortcut, e.g. `"Ctrl+Shift+S"`, `"F2"`. Empty string when no shortcut. |
| `ToolTipText` | `string` | `"{Label} [{ShortcutText}]"` when shortcut present, otherwise just `"{Label}"`. |

## RelayCommand

```csharp
// No parameter — most common:
SomeCommand = new RelayCommand(DoSomething);
SomeCommand = new RelayCommand(DoSomething, () => CanDoSomething);

// With parameter:
OpenCommand = new RelayCommand<string>(id => OpenItem(id));
OpenCommand = new RelayCommand<string>(id => OpenItem(id), id => id != null);
```

`CanExecuteChanged` hooks into `CommandManager.RequerySuggested` — WPF re-evaluates
`CanExecute` automatically after UI interactions.

## Usage patterns

### Read-only window (no Caché)

```csharp
class MyViewModel : EDViewModel
{
    private string _title = "";
    public string Title { get => _title; set => Set(ref _title, value); }
}
```

### Window with Caché access

```csharp
class MyViewModel : EDViewModel
{
    public void LoadData()
    {
        var output = MLRest!.QueryOrThrow("MY.Class", "GetData", new CVar());
        // ...
    }
}
```

`MLRest` is static — no constructor injection needed.

### Dialog with validation (OnOk override)

```csharp
class MyViewModel : EDViewModel
{
    private readonly Func<bool> _save;

    public MyViewModel(Func<bool> save) { _save = save; }

    private string _name = "";
    public string Name { get => _name; set => Set(ref _name, value); }

    protected override void OnOk()
    {
        if (string.IsNullOrWhiteSpace(Name)) return;    // keep window open
        if (!_save()) return;                            // keep window open on save failure
        base.OnOk();                                     // fires RequestClose(true)
    }
}
```

The `save` delegate is injected from the code-behind so the ViewModel has no reference
to the View:

```csharp
// In the window constructor:
DataContext = _vm = new MyViewModel(() => TheInputForm.Save());
```

### Computed properties

```csharp
private int _selectedIndex;
public int SelectedIndex
{
    get => _selectedIndex;
    set
    {
        if (Set(ref _selectedIndex, value))
        {
            OnPropertyChanged(nameof(SelectedItem));    // dependent property
            OnPropertyChanged(nameof(HasSelection));
        }
    }
}
public CompanyItem SelectedItem => Items[_selectedIndex];
public bool HasSelection => _selectedIndex >= 0;
```

### Progress overlay

```csharp
// Synchronous
using var _ = ActionInProgress();
DoHeavyWork();

// Async — disposal fires when the using block exits, even on exception
using var _ = ActionInProgress();
await LoadDataAsync();
```

`IsActionInProgress` has a `private set` — `ActionInProgress()` is the only way to activate it.
If `EDActionInProgress` is present in the window, the overlay appears and disappears automatically.

## Design notes

- `Set<T>` uses `EqualityComparer<T>.Default` — safe for value types, strings, and
  reference types. For reference types it compares by reference, which is normally correct.
- `OkCommand` is always created in the constructor. On windows where `FooterMode = Close`,
  it is simply never bound and never called — no overhead.
- Do not duplicate `RelayCommand` in consuming projects. Reference `EDControls` and import
  `using EDControls.EDViewModel;`.
