# EDISON2 — poznámky pro vývojáře

Dvouvrstvá WPF aplikace: .NET UI ↔ Caché (InterSystems) back-end přes HTTP (MLRest).

## Struktura repozitáře

| Složka | Role |
| --- | --- |
| `EDISONMAIN2/dotnet/` | Hlavní WPF aplikace (vstupní bod, okna, služby). |
| `EDControls/dotnet/` | Sdílená WPF knihovna — ovládací prvky (EDFooter, EDInputForm, EDList, EDActionInProgress) a základní třída pro ViewModely (EDViewModel). |
| `MLRest.md` | Kompletní referenční dokumentace Caché architektury (namespace, makra, komunikační protokol). |

---

## MLRest — použití v .NET

`MLRest` je HTTP klient, který serializuje .NET hodnoty do Caché process-private globals
(`^||ML("IN",...)`) a deserializuje odpověď (`^||ML("OUT",...)`) zpět do .NET.

DLL je odkazována přímo: `C:\Edison\MLRest.dll`.

Instance `MLRest` se nikdy nevytváří přímo ve windows nebo ovládacích prvcích — bere se
z `EDViewModel.MLRest` (statická vlastnost dostupná všem potomkům).

### Volání Caché metody — používej CVar (silně doporučeno)

`CVar` je idiomatický Caché strukturovaný typ. **Vždy používej `CVar`, pokud nemáš konkrétní
důvod nepoužívat ho.** Vyhni se `object[]` — používá plochý numerický formát subscriptů, který
je hůře rozšiřitelný a méně výrazový než CVar.

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

Čtení výstupu:

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

`QueryOrThrow` (extension v `EDControls`) volá `Query` + `RestStatus.ThrowIfError()`
atomicky. Volej je zvlášť pouze tehdy, kdy potřebuješ graceful degradation (`continue` ve smyčce,
`return null`).

### Caché wire format pro CVar

CVar se mapuje na string-subscriptované process-private globals:

```text
.NET cVar["key"]          → ^||ML("IN","key")
.NET cVar["1"]            → ^||ML("IN","1")    ← string "1", NOT numeric 1
.NET CList("A","B","C")   → $lb("A","B","C")  in Caché
```

Caché strana čte přes `$li(^||ML("IN","i"), n)` a zapisuje:

```objectscript
s ^||ML("OUT","1") = $lb("TYPE","value1","value2")
s ^||ML("OUT","2") = $lb("OTHER","value3")
```

### Kdy je object[] přijatelný

Používej `object[]` pouze v legacy kódu nebo při integraci s existujícími Caché metodami,
které již používají numerické subscripty (`^||ML("IN",1)`, `^||ML("OUT",1)`). Důvod zdokumentuj.
V novém kódu `object[]` **nepoužívej**.

### Binární data

```csharp
string guid = mlRest.BinarySave(bytes);    // upload byte[] → GUID
byte[] data = mlRest.BinaryLoad(guid);     // download GUID → byte[]
```

Na Caché straně uloženo v `$$$SYSBinary(GUID)`; automaticky se uvolní.

### Klíčové vlastnosti

| Vlastnost | Popis |
| --- | --- |
| `Delimiter` | Oddělovač polí (`char`). Potřebný pouze při práci s legacy protokoly; s CVar/CList není třeba. |
| `Idfa` | Identifikátor aktuální firmy (např. `"CB_DP"`). Mění se při přepnutí firmy uživatelem. |
| `Mlp` | Řetězec ID uživatele. Na Caché straně předáván přes `sys("U","MLP")`. |
| `DataNamespace` | Aktivní datový namespace. Aktualizuje se při přepnutí firmy. |
| `RestStatus` | Potřebný pouze tehdy, kdy se NEPOUŽÍVÁ `QueryOrThrow` (vzor graceful degradation). |

---

## Kostra Caché metody

Viz `MLRest.md §2.2` pro kompletní referenci. Minimální kostra ve formátu CVar:

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

Odpovídající čtení v .NET:

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

- `$$$Init` — nastaví `ret=0`, `err=""`
- `$$$FillParams` — naplní `IdFa`, `del`, `dataDB`, `userID`, `GUID`, atd.
- `$$$CatchErrRest` — nastaví `ret="1~message"`, což `ThrowIfError()` vyhodí jako výjimku v .NET

---

## Přidání nového okna

1. Vytvoř složku pod `EDISONMAIN2/dotnet/Windows/MyWindow/`.
2. Přidej `MyWindow.xaml` (Window), `MyWindow.xaml.cs`, `MyWindowViewModel.cs`.
3. ViewModel dědí `EDViewModel`. Caché volání jdou přes statickou `EDViewModel.MLRest` —
   v okně ani ViewModelu se `MLRest` nevytváří ani nepředává.
4. V code-behind nastav `DataContext` **před** `InitializeComponent()`:

   ```csharp
   public MyWindow()
   {
       DataContext = _vm = new MyWindowViewModel();
       InitializeComponent();
   }
   ```

5. Zaregistruj `RelayCommand` v `EdisonMainViewModel` přes `WindowNavigator.Show` (modeless)
   nebo `WindowNavigator.ShowDialog` (modální) s názvem třídy okna jako string:

   ```csharp
   OpenMyWindowCommand = new RelayCommand(() => WindowNavigator.Show("MyWindow"));
   ```

   Pokud okno potřebuje vstupní data, předej `EDWindowInput` a přidej konstruktor
   `public MyWindow(EDWindowInput input)`.

6. Přidej tlačítko na toolbar v `EdisonMainWindow.xaml` navázané na příkaz.

Viz `EDFooter/CLAUDE.md` pro kompletní šablonu okna s footerem.
Viz `EDComponentAndViewModelTestWindow` pro funkční referenční implementaci.

---

## EDIcons — sdílená knihovna ikon

Ikony jsou uloženy v `EDControls/dotnet/EDIcons/`. Do složky se kopírují pouze ikony, které
aplikace skutečně používá. Resource dictionary `EDIcons.xaml` mapuje každý soubor na sémantický klíč.

### Přidání nové ikony

1. Zkopíruj PNG ze sady ikon do `EDControls/dotnet/EDIcons/png/`.
2. Zaregistruj ho v `EDControls/dotnet/EDControls.csproj`:

   ```xml
   <Resource Include="EDIcons\png\myicon.png"/>
   ```

   Explicitní registrace je potřebná pouze pro PNG soubory. SDK automaticky zahrnuje všechny
   `.xaml` soubory jako `Page` položky — ruční přidání `<Page>` záznamu způsobí chybu duplikátu.

3. Přidej `BitmapImage` záznam do `EDControls/dotnet/EDIcons/EDIcons.xaml`:

   ```xml
   <BitmapImage x:Key="EDIcons.MyName"
                UriSource="pack://application:,,,/EDControls;component/EDIcons/png/myicon.png"/>
   ```

   Používej **sémantický klíč** (`EDIcons.MyName`, ne `EDIcons.Myicon`) — výměna PNG souboru
   pak vyžaduje změnu pouze v `EDIcons.xaml`.

### App.xaml — povinné přidání

Každá aplikace, která používá ikony, musí jednou v `App.xaml` mergovat `EDIcons.xaml`.
Bez toho `{StaticResource EDIcons.X}` za běhu hodí výjimku.

```xml
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="pack://application:,,,/EDControls;component/EDIcons/EDIcons.xaml"/>
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>
```

Již hotovo v `EDISONMAIN2/dotnet/App.xaml` a `TemplateDesigner/dotnet/App.xaml`.
Každý nový aplikační projekt musí stejný merge přidat.

### Použití ikony v XAML

Po přidání merge je `{StaticResource EDIcons.X}` dostupný v každém okně napříč všemi projekty:

```xml
<Button ToolTip="Přidat">
    <Image Source="{StaticResource EDIcons.Add}" Width="16" Height="16"/>
</Button>
```

### Aktuální ikony

| Klíč               | Soubor              | Význam                            |
|--------------------|---------------------|-----------------------------------|
| `EDIcons.Molecule` | `cube_molecule.png` | Ikona aplikace (EdisonMainWindow) |
| `EDIcons.Add`      | `add2.png`          | Přidat položku                    |
| `EDIcons.Edit`     | `edit.png`          | Upravit položku                   |
| `EDIcons.Delete`   | `delete2.png`       | Smazat položku                    |
