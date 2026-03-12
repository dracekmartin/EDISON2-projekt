# EDViewModel — developer notes

Base ViewModel classes for all EDISONMAIN2 windows. Provides `INotifyPropertyChanged`,
`RelayCommand`, and a standardised Ok/close mechanism that integrates with `EDFooter`.

## File map

| File | Role |
|---|---|
| `EDViewModel.cs` | Base class. `INotifyPropertyChanged`, `Set<T>()`, `OkCommand`, `RequestClose`, `Actions`, `MLRest`. |
| `RelayCommand.cs` | `RelayCommand` (no parameter) and `RelayCommand<T>`. |
| `EDAction.cs` | Model for a single toolbar/menu/shortcut action (label, icon, key, command). |

## EDViewModel API

| Member | Description |
|---|---|
| `event Action<bool?>? RequestClose` | Fired to close the window. `true` = confirmed, `false` = cancelled, `null` = just close. `EDFooter` subscribes automatically on `Loaded`. |
| `ICommand OkCommand` | Calls `OnOk()`. Bound to the Ok button by `EDFooter`. |
| `IReadOnlyList<EDAction>? Actions` | Actions for this window. Set in the subclass constructor. `EDToolbar` reads this on `Loaded`; `EDFooter` binds to it via DataContext. Key bindings are registered automatically by `EDViewModel` itself (see below). |
| `protected virtual void OnOk()` | Override for validation. Call `base.OnOk()` or `CloseDialog(true)` to proceed. Return without calling base to keep the window open. |
| `protected void CloseDialog(bool? result)` | Fires `RequestClose`. Usable from any method, not only `OnOk`. |
| `protected bool Set<T>(ref T field, T value)` | Sets the backing field and fires `PropertyChanged` if the value changed. Returns `true` if it changed. |
| `protected void OnPropertyChanged(string? name)` | Manually fire `PropertyChanged` — used for computed properties that depend on another. |
| `bool IsActionInProgress` | Read-only from outside. Set only via `ActionInProgress()`. Drives `EDActionInProgress` overlay visibility automatically. |
| `IDisposable ActionInProgress()` | Sets `IsActionInProgress = true` and returns a scope that resets it on disposal. Use with `using`. |
| `MLRest? MLRest` | Static — shared across all ViewModels. Set once at application startup. Use directly for Caché calls. |

### Key binding auto-registration

`EDViewModel` schedules `RegisterKeyBindings()` via `Dispatcher.BeginInvoke` at `DispatcherPriority.Loaded`. At that point the full constructor chain has completed and the window is in `Application.Current.Windows`. The method finds the window whose `DataContext == this` and adds a `KeyBinding` for every action. **No work is needed in any view** — actions declared in the ViewModel are keyboard-accessible automatically, even if the window has no `EDToolbar` or `EDFooter`.

## EDAction

```csharp
new EDAction(label, icon, key, modifiers, command)
```

| Property | Type | Description |
| --- | --- | --- |
| `Label` | `string` | Display name (menu header, tooltip prefix). |
| `Icon` | `ImageSource?` | Shared image source from resource dictionary. |
| `IconElement` | `Image?` | Returns a **new** `Image` wrapping `Icon` on every access — required for `MenuItem.Icon` binding via `ItemContainerStyle` (see source comment). |
| `Key` / `Modifiers` | `Key` / `ModifierKeys` | Keyboard shortcut registered by `EDViewModel`. |
| `Command` | `ICommand` | The command to execute. |
| `ShortcutText` | `string` | Human-readable shortcut, e.g. `"Ctrl+Shift+S"`, `"Ins"`, `"F2"`. Auto-generated from `Key`/`Modifiers`. |
| `ToolTipText` | `string` | `"{Label} [{ShortcutText}]"` — used directly as `ToolTip` on toolbar buttons. |

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
