# EDISON2 — developer notes

Two-tier WPF application: .NET UI ↔ Caché (InterSystems) back-end via HTTP (MLRest).

## Repository layout

| Folder | Role |
| --- | --- |
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

### Calling a Caché method — use CVar (strongly preferred)

`CVar` is the idiomatic Caché structured type. **Always use `CVar` unless you have a specific
reason not to.** Avoid `object[]` — it uses a flat numeric-subscript wire format that is
harder to extend and less expressive than CVar.

```csharp
using EDControls;   // for QueryOrThrow

// Empty input
CVar output = mlRest.QueryOrThrow("ALVA.MY.ClassName", "MyMethod", new CVar());

// Structured input
var input = new CVar();
input["key"] = new CVar("value");
input["count"] = new CVar(42);
CVar output = mlRest.QueryOrThrow("ALVA.MY.ClassName", "MyMethod", input);

// CList input (for ordered tuples — maps to $lb() in Caché)
input["item"] = new CList("TYPE", "field1", "field2");
```

Reading output:

```csharp
// Single named value
string val = output["key"].Value?.ToString() ?? "";

// Single named CList
CList row = output["key"];

// Iterate indexed rows ("1", "2", ...)
// Skip "ERR" — it's MLRest metadata, not application data.
foreach (KeyValuePair<string, CVar> kvp in output)
{
    if (kvp.Key == "ERR") continue;
    CList row = kvp.Value;            // implicit CVar→CList
    string type = row.GetString(1);   // 1-based indexing
    string name = row.GetString(2);
}
```

`QueryOrThrow` (extension in `EDControls`) calls `Query` + `RestStatus.ThrowIfError()`
atomically. Only call them separately when you need graceful degradation (`continue` in a
loop, `return null`).

### Caché wire format for CVar

CVar maps to string-subscripted process-private globals:

```text
.NET cVar["key"]          → ^||ML("IN","key")
.NET cVar["1"]            → ^||ML("IN","1")    ← string "1", NOT numeric 1
.NET CList("A","B","C")   → $lb("A","B","C")  in Caché
```

Caché side reads with `$li(^||ML("IN","i"), n)` and writes:

```objectscript
s ^||ML("OUT","1") = $lb("TYPE","value1","value2")
s ^||ML("OUT","2") = $lb("OTHER","value3")
```

### When object[] is acceptable

Use `object[]` only in legacy code or when integrating with existing Caché methods that
already use numeric subscripts (`^||ML("IN",1)`, `^||ML("OUT",1)`). Document the reason.
Do **not** use `object[]` in new code.

### Binary data

```csharp
string guid = mlRest.BinarySave(bytes);    // upload byte[] → GUID
byte[] data = mlRest.BinaryLoad(guid);     // download GUID → byte[]
```

Stored in `$$$SYSBinary(GUID)` on the Caché side; cleaned up automatically.

### Key properties

| Property | Description |
| --- | --- |
| `Delimiter` | Field separator (`char`). Only needed when working with legacy delimiter-based protocols; not required with CVar/CList. |
| `Idfa` | Current company identifier (e.g. `"CB_DP"`). Changes when the user switches company. |
| `Mlp` | User ID string. Passed in `sys("U","MLP")` on the Caché side. |
| `DataNamespace` | Active data namespace. Updated on company switch. |
| `RestStatus` | Only needed when NOT using `QueryOrThrow` (graceful-degradation pattern). |

---

## Caché method skeleton

See `MLRest.md §2.2` for the full reference. Minimal skeleton using CVar format:

```objectscript
Include GENERAL

ClassMethod MyMethod(ByRef sys As %String) As %Status
{
    $$$Init
    try {
        $$$FillParams

        // Read named input values
        s name = ^||ML("IN","name")
        s count = ^||ML("IN","count")

        // Write named output values (string key → readable on .NET as output["result"])
        s ^||ML("OUT","result") = "ok"
        s ^||ML("OUT","info") = $lb("TypeA","value1","value2")

        // Write indexed output rows (iterated on .NET with foreach)
        s i = 0
        s ^||ML("OUT",$i(i)) = $lb("ROW","a","b")
        s ^||ML("OUT",$i(i)) = $lb("ROW","c","d")
    }
    catch (ex) { $$$CatchErrRest }
    ret ret
}
```

Corresponding .NET read:

```csharp
CVar output = mlRest.QueryOrThrow("ALVA.MY.Class", "MyMethod", input);

string result = output["result"].Value?.ToString() ?? "";  // named string value
CList info   = output["info"];                             // named CList

foreach (KeyValuePair<string, CVar> kvp in output)
{
    if (kvp.Key == "ERR") continue;
    if (kvp.Key == "result" || kvp.Key == "info") continue; // skip named keys if mixed
    CList row = kvp.Value;
    // row.GetString(1) == "ROW", row.GetString(2) == "a", etc.
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

---

## EDIcons — shared icon library

Icons live in `EDControls/dotnet/EDIcons/`. Only icons actually used by the application are
copied there from the source set. The `EDIcons.xaml` resource dictionary maps each file to
a semantic key.

### Adding a new icon

1. Copy the PNG from the icon set into `EDControls/dotnet/EDIcons/png/`.
2. Register it in `EDControls/dotnet/EDControls.csproj`:

   ```xml
   <Resource Include="EDIcons\png\myicon.png"/>
   ```

   Only PNG files need explicit registration. The SDK auto-includes all `.xaml` files as
   `Page` items — adding a `<Page>` entry manually causes a duplicate build error.

3. Add a `BitmapImage` entry in `EDControls/dotnet/EDIcons/EDIcons.xaml`:

   ```xml
   <BitmapImage x:Key="EDIcons.MyName"
                UriSource="pack://application:,,,/EDControls;component/EDIcons/png/myicon.png"/>
   ```

   Use a **semantic key** (`EDIcons.MyName`, not `EDIcons.Myicon`) so swapping the PNG only
   touches `EDIcons.xaml`.

### App.xaml — required merge

Every application that uses icons must merge `EDIcons.xaml` once in its `App.xaml`.
Without this, `{StaticResource EDIcons.X}` will throw at runtime.

```xml
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="pack://application:,,,/EDControls;component/EDIcons/EDIcons.xaml"/>
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>
```

Already done in `EDISONMAIN2/dotnet/App.xaml` and `TemplateDesigner/dotnet/App.xaml`.
Any new application project must add the same merge.

### Using an icon in XAML

With the merge in place, `{StaticResource EDIcons.X}` is available in every window across
every project:

```xml
<Button ToolTip="Přidat">
    <Image Source="{StaticResource EDIcons.Add}" Width="16" Height="16"/>
</Button>
```

### Current icons

| Key              | File          | Meaning        |
|------------------|---------------|----------------|
| `EDIcons.Add`    | `add2.png`    | Add a new item |
| `EDIcons.Edit`   | `edit.png`    | Edit an item   |
| `EDIcons.Delete` | `delete2.png` | Delete an item |
