# EDInputForm — poznámky pro vývojáře

## Účel

`EDInputForm` je samostatný WPF `UserControl`, který načte, vykreslí, validuje a uloží
dynamický formulář přes Caché (InterSystems) back-end. Hostitelské okno potřebuje pouze
nastavit `CacheClass` — `MLRest` se čte automaticky z `EDViewModel` DataContext rodičovského
okna. Veškerá Caché komunikace a serializace je řešena interně.

## Mapa souborů

| Soubor | Role |
| --- | --- |
| `EDInputForm.xaml / .xaml.cs` | UserControl. Vlastní všechna Caché volání, serializační helpery a Items DP. |
| `EDInputFormController.cs` | Testovatelná orchestrace (Init/Validate/Save) bez WPF závislosti. Přijímá `IMLRest` a `CacheClass` jako parametry konstruktoru. |
| `Item.cs` | Abstraktní báze pro všechny položky formuláře. Property `Number` (1-based sekvenční). |
| `Group.cs` | Hlavička sekce: `GroupName`, `IsVisible` (řízené serverem), `IsExpanded` (řízené uživatelem). Implementuje `INotifyPropertyChanged`. |
| `Field.cs` | Editovatelné pole: `Key`, `Label`, `FieldType`, `Value`, `ValidationError`, `Required`, `ValidateOnChange`, `ParentGroup`. Implementuje `INotifyPropertyChanged`. |
| `EFieldType.cs` | Enum: `Text`, `Number`. |
| `EValidationTrigger.cs` | Enum: `Init`, `Change`, `BeforeSave`. |
| `ValidationContext.cs` | Nese trigger + `ChangedKey` — serializuje se do CTRL záznamu v IN. |
| `DesignTimeData/EDInputFormDesignTimeData.cs` | Design-time ukázková data; nikdy se neinstanciuje za běhu. |

## Integrace s hostem (minimální)

```csharp
// V XAML:
<eif:EDInputForm x:Name="TheInputForm" CacheClass="NAMESPACE.MyClass"/>

// MLRest se čte automaticky z DataContext rodičovského okna (EDViewModel).
// Init() se volá automaticky při Loaded, pokud je CacheClass nastaveno.

// Handlery tlačítek (pokud je potřeba ruční řízení):
void Validate_Click(...) { bool ok = TheInputForm.Validate(); }
void Save_Click(...)     { bool ok = TheInputForm.Save();     }
```

Žádné nastavování MLRest není potřeba — control ho čte z DataContext rodičovského okna.

## Veřejné API

| Člen | Popis |
| --- | --- |
| `string CacheClass` | Musí být nastaveno před jakýmkoliv voláním. Caché třída implementující Init/Validate/Save. |
| `void Init()` | Volá Caché `Init`, naplní Items, spustí `ExecuteValidate(Init)`. Volá se automaticky při `Loaded` pokud je `CacheClass` nastaveno. |
| `bool Validate()` | Volá Caché `Validate` s Change triggerem; vrací true pokud nejsou chyby. |
| `bool Save()` | Validuje s BeforeSave triggerem, volá Caché `Save` pouze pokud je čistý stav; vrací true při úspěchu. |
| `IEnumerable<Item>? Items` | DP — aktuální položky formuláře; aktualizuje se po každém Caché volání. |
| `static CVar SerializeItemsCVar(items)` | Převede Items → CVar pro odeslání do Caché. |
| `static List<Item> ParseItems(CVar output)` | Převede Caché CVar výstup → seznam Item; přeskočí klíč "ERR". |

### EDInputFormController (testovatelný, bez WPF)

```csharp
// Pro unit testy nebo non-WPF scénáře:
var controller = new EDInputFormController(mockMLRest, "NAMESPACE.MyClass");
controller.Init();
bool ok = controller.Save();
List<Item> items = controller.Items;
```

## Caché wire protokol (CVar/CList)

### Položky formuláře (oba směry)

Položky se serializují jako CList záznamy se string-indexovanými klíči:

```text
CVar["1"] = CList("GROUP", number, groupName, "1"|"0"(visible))
CVar["2"] = CList("FIELD", number, key, label, "TEXT"|"NUMBER", value, "0"|"1"(validateOnChange), "0"|"1"(required))
```

Caché OUT může přidat 9. element u FIELD záznamů pro validační chyby:

```text
CList("FIELD", number, key, label, "TEXT", value, "0", "1", "Chyba: pole je povinné")
```

### Struktura volání Validate

```text
IN["trigger"] = "Init" | "Change" | "BeforeSave"
IN["1"..]     = serializované položky formuláře (CList záznamy)
```

## Automatická inicializace při Loaded

Pokud je `CacheClass` neprázdný, `Init()` se zavolá automaticky při události `Loaded`.
Žádné ruční volání `Init()` v hostitelském okně není potřeba.

## Automatická validace při změně pole

Když `Field` s `ValidateOnChange == true` změní svou `Value`, EDInputForm zavolá
`ExecuteValidate(Change)` automaticky přes `Dispatcher.BeginInvoke` — bez zásahu hostitele.

## Klávesová navigace

`Ctrl+číslice` naviguje na položku, jejíž `Number` odpovídá akumulované hodnotě:

- Číslice se bufferují 200 ms (např. Ctrl+1, Ctrl+2 → položka 12)
- Pokud je cíl `Field`, zaostří jeho TextBox
- Pokud je cíl `Group`, přepne `IsExpanded`

## Návrhové poznámky

- `MLRest` se čte z `EDViewModel` DataContext rodičovského okna — žádný setter na controlu.
- `EDControls.csproj` odkazuje `C:\Edison\MLRest.dll` přímo (stejná HintPath konvence jako EDISONMAIN2).
- `ExecuteValidate` je private; host používá `Validate()` a `Save()`.
- `OnItemsChanged` přihlašuje/odhlašuje `PropertyChanged` na každém `Field` proti únikům paměti.
- `EDInputFormDesignTimeData` je odkazován pouze z `d:DataContext` v XAML; nikdy neinstanciuj za běhu.
- `PreserveExpansionState` zachovává uživatelem přepnuté rozbalení skupin napříč překreslením.
