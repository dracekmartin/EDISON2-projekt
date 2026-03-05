# EDViewModel — developer notes

Base ViewModel classes for all EDISONMAIN2 windows. Provides `INotifyPropertyChanged`,
`RelayCommand`, and a standardised Ok/close mechanism that integrates with `EDFooter`.

## File map

| File | Role |
|---|---|
| `EDViewModel.cs` | Base class. `INotifyPropertyChanged`, `Set<T>()`, `OkCommand`, `RequestClose`. |
| `EDDataViewModel.cs` | Extends `EDViewModel` with an `MLRest` property. Use when the window calls Caché. |
| `RelayCommand.cs` | `RelayCommand` (no parameter) and `RelayCommand<T>`. |

## Class hierarchy

```
INotifyPropertyChanged
    └── EDViewModel
            └── EDDataViewModel(MLRest)
```

Use `EDViewModel` for local/configuration windows.
Use `EDDataViewModel` for windows that query Caché.

## EDViewModel API

| Member | Description |
|---|---|
| `event Action<bool?>? RequestClose` | Fired to close the window. `true` = confirmed, `false` = cancelled, `null` = just close. `EDFooter` subscribes automatically on `Loaded`. |
| `ICommand OkCommand` | Calls `OnOk()`. Bound to the Ok button by `EDFooter`. |
| `protected virtual void OnOk()` | Override for validation. Call `base.OnOk()` or `CloseDialog(true)` to proceed. Return without calling base to keep the window open. |
| `protected void CloseDialog(bool? result)` | Fires `RequestClose`. Usable from any method, not only `OnOk`. |
| `protected bool Set<T>(ref T field, T value)` | Sets the backing field and fires `PropertyChanged` if the value changed. Returns `true` if it changed. |
| `protected void OnPropertyChanged(string? name)` | Manually fire `PropertyChanged` — used for computed properties that depend on another. |
| `bool IsActionInProgress` | Read-only from outside. Set only via `ActionInProgress()`. Drives `EDActionInProgress` overlay visibility automatically. |
| `IDisposable ActionInProgress()` | Sets `IsActionInProgress = true` and returns a scope that resets it on disposal. Use with `using`. |

## EDDataViewModel API

| Member | Description |
|---|---|
| `MLRest MLRest` | Set in constructor. Pass to `EDInputForm.MLRest`, `EDList.MLRest`, or use directly for custom Caché calls. |

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
class MyViewModel(MLRest mlRest) : EDDataViewModel(mlRest)
{
    public void LoadData()
    {
        var output = MLRest.Query("MY.Class", "GetData", []);
        // ...
    }
}
```

### Dialog with validation (OnOk override)

```csharp
class MyViewModel(MLRest mlRest, Func<bool> save) : EDDataViewModel(mlRest)
{
    private string _name = "";
    public string Name { get => _name; set => Set(ref _name, value); }

    protected override void OnOk()
    {
        if (string.IsNullOrWhiteSpace(Name)) return;    // keep window open
        if (!save()) return;                             // keep window open on save failure
        base.OnOk();                                     // fires RequestClose(true)
    }
}
```

The `save` delegate is injected from the code-behind so the ViewModel has no reference
to the View:

```csharp
// In the window constructor:
DataContext = _vm = new MyViewModel(mlRest, () => TheInputForm.Save());
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
