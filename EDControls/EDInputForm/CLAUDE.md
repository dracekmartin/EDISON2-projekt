# EDInputForm — developer notes

## Purpose

`EDInputForm` is a self-contained WPF `UserControl` that loads, renders, validates, and saves a
dynamic data-entry form via a Caché (InterSystems) back-end. The host window only needs to
supply `MLRest` and `CacheClass`, then call `Init()` / `Validate()` / `Save()` from button
handlers — all Caché communication and serialization is handled internally.

## File map

| File | Role |
| --- | --- |
| `EDInputForm.xaml / .xaml.cs` | The UserControl. Owns all Caché calls, serialization helpers, and the Items DP. |
| `FormItem.cs` | Abstract base for all form items. |
| `FormGroup.cs` | Section header: `GroupName`. |
| `FormField.cs` | Editable field: `Key`, `Label`, `FieldType`, `Value`, `ValidationError`, `Required`, `ValidateOnChange`. Implements `INotifyPropertyChanged`. |
| `FieldType.cs` | Enum: `Text`, `Number`. |
| `EValidationTrigger.cs` | Enum: `Init`, `Change`, `BeforeSave`. |
| `ValidationContext.cs` | Carries trigger + `ChangedKey` — reserved for future use (see note below). |
| `FormInputDesignData.cs` | Design-time sample data only; never instantiated at runtime. |

## Host integration (minimal)

```csharp
// In the host window constructor:
TheInputForm.MLRest     = mlRest;
TheInputForm.CacheClass = "NAMESPACE.MyClass";

// Button handlers — pure UI:
void Init_Click(...)     { TheInputForm.Init();              /* update StatusText */ }
void Validate_Click(...) { bool ok = TheInputForm.Validate(); /* update StatusText */ }
void Save_Click(...)     { bool ok = TheInputForm.Save();     /* update StatusText */ }
```

No event subscriptions, no callbacks, no serialization in the host.

## Public API

| Member | Description |
| --- | --- |
| `MLRest MLRest` | Must be set before any call. |
| `string CacheClass` | Must be set before any call. |
| `void Init()` | Calls Caché `Init`, populates Items, runs `ExecuteValidate(Init)`. |
| `bool Validate()` | Calls Caché `Validate` with Change trigger; returns true if no errors. |
| `bool Save()` | Validates with BeforeSave, calls Caché `Save` only if clean; returns true on success. |
| `IEnumerable<FormItem>? Items` | DP — current form items; updated after every Caché call. |
| `static object[] SerializeItems(items, delimiter)` | Converts Items → Caché IN array (no trigger prefix). |
| `static List<FormItem> ParseItems(output, delimiter)` | Converts Caché OUT array → FormItem list; skips `output[0]`. |

## Caché wire protocol

### Shared delimiter

`MLRest.Delimiter` (typically `\x1C`) is used as the field separator in all entries.

### Form items (both directions)

```text
GROUP <del> groupName
FIELD <del> key <del> label <del> TEXT|NUMBER <del> value <del> 0|1(validateOnChange) <del> 0|1(required)
```

Caché OUT may append an extra field on FIELD entries:

```text
FIELD <del> ... <del> validationError
```

### Validate call structure

```text
IN[1]   = trigger string ("Init" | "Change" | "BeforeSave")
IN[2..] = serialised form items
```

The Caché class reads `IN[1]` as a plain string. `ValidationContext.Serialize` produces a
future CTRL-format string (`"CTRL|trigger|changedKey"`) not yet used on the Caché side.

## Auto-validate on field change

When a `FormField` with `ValidateOnChange == true` has its `Value` changed, EDInputForm calls
`ExecuteValidate(Change)` automatically via `Dispatcher.BeginInvoke` — no host involvement.

## Design notes

- `EDControls.csproj` references `C:\Edison\MLRest.dll` directly (same HintPath convention as EDISONMAIN2).
- `ExecuteValidate` is private; hosts use `Validate()` and `Save()` instead.
- `ValidationRequested` event has been removed — all validation is driven internally.
- `OnItemsChanged` subscribes/unsubscribes `PropertyChanged` on each `FormField` to avoid leaks.
- `FormInputDesignData` is referenced only from `d:DataContext` in the XAML; never instantiate at runtime.
