# EDISON2 — developer notes

Two-tier WPF application: .NET UI ↔ Caché (InterSystems) back-end via HTTP (MLRest).

## Repository layout

| Folder | Role |
|---|---|
| `EDISONMAIN2/dotnet/` | Main WPF application (entry point, windows, services). |
| `EDControls/dotnet/` | Shared WPF control library (EDFooter, EDInputForm, EDList, EDViewModel, EDActionInProgress). |
| `MLRest.md` | Full Caché-side architecture reference (namespaces, macros, wire protocol). |

---

## MLRest — .NET usage

`MLRest` is an HTTP client that serialises .NET values into Caché process-private globals
(`^||ML("IN",...)`) and deserialises the response (`^||ML("OUT",...)`) back to .NET.

The DLL is referenced directly: `C:\Edison\MLRest.dll`.

### Construction

```csharp
// Typical: server + port + data namespace + IdFa (company identifier)
var mlRest = new MLRest(server, restPort, dataNamespace, idfa)
{
    LoginName = connParams.UserName,
    FullName  = connParams.FullName,
    Mlp       = connParams.Mlp.ToString()
};
```

`MLRest` is constructed once per session in `EdisonMainViewModel` and passed down into
every window and control that needs Caché access.

### Calling a Caché method

```csharp
using EDControls;   // for QueryOrThrow

object[] output = mlRest.QueryOrThrow("ALVA.MY.ClassName", "MyMethod", [input1, input2]);
string result = (string)output[1];  // output[0] is reserved; data starts at [1]
```

`QueryOrThrow` is an extension method in `EDControls` that calls `Query` and immediately
calls `RestStatus.ThrowIfError()` — use it everywhere you want to throw on Caché error.
Only call `Query` + `RestStatus` separately when you need to handle errors gracefully
(e.g. `continue` in a loop or `return null`).

### Delimiter

Fields within a single output slot are separated by `MLRest.Delimiter` (a `char`, typically
`\x1C`). Split with:

```csharp
string[] parts = output[1].ToString()!.Split(mlRest.Delimiter);
```

### Error handling

```csharp
mlRest.RestStatus.ThrowIfError();
```

Always call immediately after `Query`. Throws `InvalidOperationException` with the Caché
error message if `ret` was non-zero. Never skip this call.

### Binary data

```csharp
string guid = mlRest.BinarySave(bytes);    // upload byte[] → GUID
byte[] data = mlRest.BinaryLoad(guid);     // download GUID → byte[]
```

Stored in `$$$SYSBinary(GUID)` on the Caché side; cleaned up automatically.

### Key properties

| Property | Description |
|---|---|
| `Delimiter` | Field separator (`char`). Use everywhere you parse delimited output. |
| `Idfa` | Current company identifier (e.g. `"CB_DP"`). Changes when the user switches company. |
| `Mlp` | User ID string. Passed in `sys("U","MLP")` on the Caché side. |
| `DataNamespace` | Active data namespace. Updated on company switch. |
| `RestStatus` | Only needed when NOT using `QueryOrThrow` (graceful-degradation pattern). |

---

## Caché method skeleton

See `MLRest.md §2.2` for the full reference. Minimal skeleton:

```objectscript
Include GENERAL

ClassMethod MyMethod(ByRef sys As %String) As %Status
{
    $$$Init
    try {
        $$$FillParams
        s vstup = ^||ML("IN",1)
        // ... business logic ...
        s ^||ML("OUT",1) = "result"
    }
    catch (ex) { $$$CatchErrRest }
    ret ret
}
```

- `$$$Init` — sets `ret=0`, `err=""`
- `$$$FillParams` — populates `IdFa`, `del`, `dataDB`, `userID`, `GUID`, etc.
- `$$$CatchErrRest` — sets `ret="1~message"`, which `ThrowIfError()` surfaces in .NET

---

## Adding a new window

1. Create a folder under `EDISONMAIN2/dotnet/Windows/MyWindow/`.
2. Add `MyWindow.xaml` (Window), `MyWindow.xaml.cs`, `MyWindowViewModel.cs`.
3. ViewModel inherits `EDViewModel` (no Caché) or passes `MLRest` directly.
4. Set `DataContext` before `InitializeComponent()` in the runtime constructor.
5. Add a parameterless constructor for the WPF designer.
6. Register a `RelayCommand` in `EdisonMainViewModel` using `Navigator.Show` /
   `Navigator.ShowDialog` with the window's class name as a string key.
7. Add a toolbar button in `EdisonMainWindow.xaml` bound to the command.

See `EDFooter/CLAUDE.md` for the full window + footer template.
See `EDComponentAndViewModelTestWindow` for a working reference implementation.
