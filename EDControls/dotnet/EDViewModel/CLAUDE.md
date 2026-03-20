# EDViewModel — poznámky pro vývojáře

Základní třídy ViewModelů pro všechna okna v EDISON2. Poskytuje `INotifyPropertyChanged`,
`RelayCommand`, přístup ke službám (`EDServices`) a standardizovaný Ok/close mechanismus
integrovaný s `EDFooter`.

## Mapa souborů

| Soubor | Role |
| --- | --- |
| `EDViewModel.cs` | Základní třída. `INotifyPropertyChanged`, `Set<T>()`, `Command()`, `OkCommand`, `RequestClose`, `Actions`, `MLRest`, `StandardActionsAccessor`. |
| `RelayCommand.cs` | `RelayCommand` (bez parametru) a `RelayCommand<T>`. Zachytávání výjimek přes `IPopupDisplayer`. |
| `EDAction.cs` | Model jedné akce pro toolbar/menu/klávesovou zkratku (label, ikona, klávesa, příkaz). |
| `EDServices.cs` | DI kontejner: `IMLRest`, `IWindowNavigator`, `IPopupDisplayer`. Singleton `EDServices.Current`. |
| `IWindowNavigator.cs` | Interface pro otevírání oken (Show/ShowDialog s názvem třídy). |
| `ReflectionWindowNavigator.cs` | Implementace — hledá typ okna reflexí, vytváří přes `Activator.CreateInstance`. |
| `IPopupDisplayer.cs` | Interface pro dialogy (`Confirm`, `Info`, `ConfirmClose`, `Error`). |
| `PopupDisplayer.cs` | Implementace — zobrazí `EDPopup` / `EDErrorPopup` s aktivním oknem jako owner. |
| `EDWindowInput.cs` | Bázová třída pro vstupní data předávaná do modálních oken. |
| `EDWindowOutput.cs` | Bázová třída pro výstupní data z dialogů. |

## EDViewModel API

| Člen | Popis |
| --- | --- |
| `IMLRest? MLRest` | Property — čte z `_services.MLRest` (sdíleno přes `EDServices`). Použij přímo pro Caché volání: `MLRest!.QueryOrThrow(...)`. |
| `IWindowNavigator WindowNavigator` | Protected — pro otevírání dalších oken: `WindowNavigator.Show("MyWindow")`. |
| `IPopupDisplayer PopupDisplayer` | Public — pro dialogy: `PopupDisplayer.Confirm("Opravdu?")`. |
| `bool ConfirmOnCancel` | Protected set. Pokud `true`, EDFooter zobrazí potvrzení před zavřením. |
| `event Action<bool?>? RequestClose` | Vyvolán pro zavření okna. `true` = potvrzeno, `false` = zrušeno, `null` = jen zavřít. `EDFooter` se přihlásí automaticky na `Loaded`. |
| `ICommand OkCommand` | Volá `OnOk()`. Navázáno na tlačítko Ok přes `EDFooter`. |
| `ICommand AddCommand / EditCommand / DeleteCommand` | Volají `OnAdd/OnEdit/OnDelete()`. Automaticky použito v `StandardActions.Add/Edit/Delete()`. |
| `IReadOnlyList<EDAction>? Actions` | Akce tohoto okna. Nastavit v konstruktoru podtřídy. `EDToolbar` je čte na `Loaded`; `EDFooter` binduje přes DataContext. Klávesové zkratky se registrují automaticky v `EDViewModel` (viz níže). |
| `protected RelayCommand Command(Action, canExecute?)` | **Preferovaný způsob vytváření příkazů.** Vytvoří `RelayCommand` s automatickým error handlingem přes `PopupDisplayer`. Používej místo `new RelayCommand(...)`. |
| `protected RelayCommand<T> Command<T>(Action<T>, canExecute?)` | Generická varianta `Command()`. |
| `protected virtual void OnOk()` | Přepiš pro validaci. Zavolej `base.OnOk()` nebo `CloseDialog(true)` pro potvrzení. Bez volání base okno zůstane otevřené. |
| `protected virtual void OnAdd/OnEdit/OnDelete()` | Přepiš pro obsluhu standardních CRUD akcí. Ve výchozím stavu nedělají nic. |
| `protected void CloseDialog(bool? result)` | Vyvolá `RequestClose`. Použitelné z jakékoliv metody, nejen z `OnOk`. |
| `protected bool Set<T>(ref T field, T value)` | Nastaví backing field a vyvolá `PropertyChanged` pokud se hodnota změnila. Vrací `true` pokud ke změně došlo. |
| `protected void OnPropertyChanged(string? name)` | Ruční vyvolání `PropertyChanged` — pro computed properties závislé na jiné. |
| `bool IsActionInProgress` | Zvenku read-only. Nastavitelné pouze přes `ActionInProgress()`. Automaticky řídí viditelnost `EDActionInProgress` overlaye. |
| `IDisposable ActionInProgress()` | Nastaví `IsActionInProgress = true` a vrátí scope, který ho resetuje při disposal. Používej s `using`. |

### Konstruktory

```csharp
// Bezparametrický — čte z EDServices.Current (pro okna vytvářená přes ReflectionWindowNavigator)
public EDViewModel() : this(EDServices.Current) { }

// Explicitní — pro DI v testech nebo composition-root ViewModelech
public EDViewModel(EDServices services) { ... }
```

### Automatická registrace klávesových zkratek

`EDViewModel` naplánuje `RegisterKeyBindings()` přes `Dispatcher.BeginInvoke` na
`DispatcherPriority.Loaded`. V tom okamžiku je celý řetězec konstruktorů dokončen a okno
je v `Application.Current.Windows`. Metoda najde okno, jehož `DataContext == this`, a přidá
`KeyBinding` pro každou akci. **V žádném view není potřeba nic nastavovat** — akce deklarované
ve ViewModelu jsou automaticky dostupné z klávesnice, i když okno nemá `EDToolbar` ani `EDFooter`.

## StandardActions — továrna standardních akcí

`StandardActions` je protected property na `EDViewModel` vracející `StandardActionsAccessor`.
Předvyplní label (`EDStrings`), ikonu (`EDIcons`) a klávesovou zkratku pro tři standardní
CRUD akce.

```csharp
// Nejjednodušší — přepiš OnAdd/OnEdit/OnDelete ve ViewModelu:
Actions = [StandardActions.Add(), StandardActions.Edit(), StandardActions.Delete()];

protected override void OnAdd() { /* ... */ }
protected override void OnEdit() { /* ... */ }
protected override void OnDelete() { /* ... */ }

// S explicitním příkazem (např. pro CanExecute):
Actions = [StandardActions.Add(new RelayCommand(OnAdd, CanAdd)), StandardActions.Edit(), StandardActions.Delete()];
```

Bezparametrové varianty se automaticky navážou na `EDViewModel.AddCommand/EditCommand/DeleteCommand`,
které volají odpovídající virtuální metody. Varianty s `ICommand` jsou pro vlastní `CanExecute`.
`new EDAction(...)` používej přímo pouze pro nestandardní akce.

| Metoda | Zkratka | EDStrings klíč | EDIcons klíč |
| --- | --- | --- | --- |
| `Add()` / `Add(ICommand)` | Ctrl+N | `EDStrings.Add` | `EDIcons.Add` |
| `Edit()` / `Edit(ICommand)` | Ctrl+E | `EDStrings.Edit` | `EDIcons.Edit` |
| `Delete()` / `Delete(ICommand)` | Ctrl+D | `EDStrings.Delete` | `EDIcons.Delete` |

## EDAction

```csharp
// Plná varianta — label z EDStrings, ikona z EDIcons, klávesová zkratka:
new EDAction(
    (string)Application.Current.FindResource("EDStrings.Add"),
    new RelayCommand(OnAdd),
    (ImageSource)Application.Current.FindResource("EDIcons.Add"),
    Key.Insert)

// Se zkratkou, bez ikony:
new EDAction((string)Application.Current.FindResource("EDStrings.Add"), new RelayCommand(OnAdd), key: Key.Insert)

// Pouze label + příkaz:
new EDAction((string)Application.Current.FindResource("EDStrings.Add"), new RelayCommand(OnAdd))
```

`icon`, `key` a `modifiers` jsou volitelné (`null` / `Key.None` / `ModifierKeys.None`).
Používej `EDStrings` klíče pro labely a `EDIcons` klíče pro ikony, aby texty a grafika
byly konzistentní napříč okny.

| Vlastnost | Typ | Popis |
| --- | --- | --- |
| `Label` | `string` | Zobrazovaný název (hlavička menu, prefix tooltipu). |
| `Command` | `ICommand` | Příkaz k vykonání. |
| `Icon` | `ImageSource?` | Sdílený zdroj obrázku z resource dictionary. Volitelné. |
| `IconElement` | `Image?` | Vrací **nový** `Image` obalující `Icon` při každém přístupu — potřebné pro `MenuItem.Icon` binding přes `ItemContainerStyle` (viz komentář ve zdrojáku). |
| `Key` / `Modifiers` | `Key` / `ModifierKeys` | Klávesová zkratka. Volitelné — vynech nebo předej `Key.None` pro žádnou zkratku. |
| `ShortcutText` | `string` | Čitelný text zkratky, např. `"Ctrl+Shift+S"`, `"F2"`. Prázdný string bez zkratky. |
| `ToolTipText` | `string` | `"{Label} [{ShortcutText}]"` když je zkratka, jinak jen `"{Label}"`. |

## RelayCommand

**Preferovaný způsob** — přes `Command()` helper (automatický error handling):

```csharp
// V EDViewModel subclass:
SomeCommand = Command(DoSomething);
SomeCommand = Command(DoSomething, () => CanDoSomething);

// Generický:
OpenCommand = Command<string>(id => OpenItem(id));
OpenCommand = Command<string>(id => OpenItem(id), id => id != null);
```

Přímá konstrukce (mimo EDViewModel nebo pro speciální potřeby):

```csharp
SomeCommand = new RelayCommand(DoSomething);
SomeCommand = new RelayCommand(DoSomething, () => CanDoSomething, popupDisplayer);
```

`RelayCommand` zachytí výjimky v `Execute` a zobrazí je přes `IPopupDisplayer.Error()`.
`CanExecuteChanged` se napojí na `CommandManager.RequerySuggested` — WPF automaticky
přehodnotí `CanExecute` po UI interakcích.

## Vzory použití

### Okno bez Caché (read-only)

```csharp
class MyViewModel : EDViewModel
{
    private string _title = "";
    public string Title { get => _title; set => Set(ref _title, value); }
}
```

### Okno s Caché přístupem

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

`MLRest` čte z `EDServices` — žádná ruční injekce není potřeba.

### Dialog s validací (přepsání OnOk)

```csharp
class MyViewModel : EDViewModel
{
    private readonly Func<bool> _save;

    public MyViewModel(Func<bool> save) { _save = save; }

    private string _name = "";
    public string Name { get => _name; set => Set(ref _name, value); }

    protected override void OnOk()
    {
        if (string.IsNullOrWhiteSpace(Name)) return;    // okno zůstane otevřené
        if (!_save()) return;                            // okno zůstane otevřené při chybě uložení
        base.OnOk();                                     // vyvolá RequestClose(true)
    }
}
```

Delegát `save` se injektuje z code-behind, takže ViewModel nemá referenci na View:

```csharp
// V konstruktoru okna:
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
            OnPropertyChanged(nameof(SelectedItem));    // závislá property
            OnPropertyChanged(nameof(HasSelection));
        }
    }
}
public CompanyItem SelectedItem => Items[_selectedIndex];
public bool HasSelection => _selectedIndex >= 0;
```

### Progress overlay

```csharp
// Synchronní
using var _ = ActionInProgress();
DoHeavyWork();

// Asynchronní — disposal proběhne při opuštění using bloku, i při výjimce
using var _ = ActionInProgress();
await LoadDataAsync();
```

`IsActionInProgress` má `private set` — `ActionInProgress()` je jediný způsob aktivace.
Pokud je v okně `EDActionInProgress`, overlay se zobrazí a schová automaticky.

## Návrhové poznámky

- `Set<T>` používá `EqualityComparer<T>.Default` — bezpečné pro hodnotové typy, stringy
  i referenční typy. U referenčních typů porovnává referencí, což je normálně správně.
- `OkCommand` se vždy vytvoří v konstruktoru. U oken kde `FooterMode = Close` se nikdy
  nenavážou ani nevolají — žádný overhead.
- Neduplikuj `RelayCommand` v projektech. Odkazuj `EDControls` a importuj
  `using EDControls.EDViewModel;`.
- `EDServices.MLRest` je mutable — mění se při přepnutí firmy. Všechny ViewModely sdílející
  stejnou `EDServices` instanci vidí změnu automaticky.
- Testy vytvářejí vlastní `EDServices` s mock `IMLRest` (NSubstitute) — žádný singleton.
