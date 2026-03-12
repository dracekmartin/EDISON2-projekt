---
description: Přidej nové WPF okno podle MVVM vzoru (Window + ViewModel : EDViewModel)
argument-hint: [NázevOkna]
allowed-tools: Read, Edit, Write, Bash(ls *), Bash(grep *)
---

Přidej nové WPF okno "$ARGUMENTS" do TemplateDesigneru.

## ⚠️ MVVM — závazná pravidla před zahájením

Toto jsou TVRDÁ pravidla. Pokud si nejsi jistý, raději se zeptej než je porušit.

### Co patří do ViewModelu (VŽDY)
- Veškerá business logika — načítání dat, validace, ukládání, výpočty
- Stav UI který ovlivňuje logiku — vybraná položka, filtr, mode přepínač
- Commands pro všechna tlačítka a akce (`RelayCommand`)
- Bindovatelné properties pro vše co se zobrazuje nebo edituje (`Set(ref ...)`)
- `ObservableCollection<T>` pro seznamy (ListBox, ComboBox, DataGrid)
- Výsledek dialogu (`public SomeType? Result { get; private set; }`)

### Co patří do code-behind (VÝJIMKY — jen pokud nelze přes binding)
- `vm.RequestClose += ...` — standardní zavření okna
- `vm.RequestXxx = x => control.DoSomething(x)` — napojení Action callbacků
- `DataGrid.BeginEdit()` / `CommitEdit()` — WPF interní API bez alternativy
- SkiaSharp `PaintSurface` event — technické omezení knihovny

### Červené vlajky — pokud tě napadne toto, zastav se
- ❌ `private void Button_Click(...)` v code-behind → použij `Command="{Binding ...}"`
- ❌ `_vm.SomeMethod()` volané z event handleru → přesuň logiku do Command
- ❌ `textBox.Text = ...` přímá manipulace kontrolky → použij binding property
- ❌ `if (listBox.SelectedItem != null)` v code-behind → sleduj přes bindovanou property
- ❌ `MessageBox.Show(...)` ve ViewModelu → použij `Action? RequestShowMessage` callback

---

## Kontext — přečti před zahájením

Existující okna: !`ls dotnet/Windows/`
Vzorový ViewModel: !`cat dotnet/Windows/TableEditorDialog/TableEditorDialogViewModel.cs`
Vzorový code-behind: !`cat dotnet/Windows/TableEditorDialog/TableEditorDialog.xaml.cs`

---

## Struktura k vytvoření

```
dotnet/Windows/$ARGUMENTS/
    $ARGUMENTS.xaml
    $ARGUMENTS.xaml.cs
    $ARGUMENTSViewModel.cs
```

---

## Krok 1 — ViewModel (`$ARGUMENTSViewModel.cs`)

```csharp
using EDControls;

namespace TemplateDesigner.Windows.$ARGUMENTS;

public class $ARGUMENTSViewModel : EDViewModel
{
    // -------------------------------------------------------------------------
    // Bindovatelné properties — KAŽDÁ hodnota která jde do UI musí být tady
    // -------------------------------------------------------------------------
    private string _title = "";
    public string Title { get => _title; set => Set(ref _title, value); }

    // Pro seznam položek — ObservableCollection, ne List
    // public ObservableCollection<ItemVM> Items { get; } = [];

    // Pro vybranou položku
    // private ItemVM? _selectedItem;
    // public ItemVM? SelectedItem { get => _selectedItem; set => Set(ref _selectedItem, value); }

    // -------------------------------------------------------------------------
    // Commands — KAŽDÉ tlačítko nebo akce má svůj Command tady
    // -------------------------------------------------------------------------
    // public RelayCommand AddItemCommand { get; }
    // public RelayCommand<ItemVM> DeleteItemCommand { get; }

    // -------------------------------------------------------------------------
    // Callbacks pro View-only operace (ViewModel neví o konkrétních kontrolkách)
    // -------------------------------------------------------------------------
    // public Action? RequestFocusOnInput { get; set; }
    // public Action<string>? RequestShowMessage { get; set; }

    // -------------------------------------------------------------------------
    // Výsledek dialogu (pokud okno vrací data)
    // -------------------------------------------------------------------------
    // public SomeType? Result { get; private set; }

    // -------------------------------------------------------------------------
    // Konstruktor
    // -------------------------------------------------------------------------
    public $ARGUMENTSViewModel(/* závislosti — např. Template template */)
    {
        // Inicializace commands:
        // AddItemCommand = new RelayCommand(AddItem);
        // DeleteItemCommand = new RelayCommand<ItemVM>(DeleteItem, vm => vm != null);

        // OkCommand a CancelCommand jsou zděděny z EDViewModel — nemusíš je přidávat
    }

    // -------------------------------------------------------------------------
    // Business logika — vše privátní, volané z Commands
    // -------------------------------------------------------------------------

    // Příklad: Override OnOk pro validaci před zavřením
    // protected override void OnOk()
    // {
    //     if (string.IsNullOrWhiteSpace(Title))
    //     {
    //         RequestShowMessage?.Invoke("Název nesmí být prázdný.");
    //         return;  // nezavírat
    //     }
    //     Result = new SomeType { Title = Title };
    //     CloseDialog(true);
    // }

    // private void AddItem()
    // {
    //     Items.Add(new ItemVM { ... });
    // }

    // private void DeleteItem(ItemVM? item)
    // {
    //     if (item != null) Items.Remove(item);
    // }
}
```

---

## Krok 2 — Code-behind (`$ARGUMENTS.xaml.cs`)

Code-behind je záměrně minimální — jen wiring, nula logiky.

```csharp
using System.Windows;

namespace TemplateDesigner.Windows.$ARGUMENTS;

public partial class $ARGUMENTS : Window
{
    private readonly $ARGUMENTSViewModel _vm;

    // Runtime konstruktor — ViewModel se předává zvenku (dependency injection)
    public $ARGUMENTS($ARGUMENTSViewModel vm)
    {
        DataContext = _vm = vm;   // DataContext PŘED InitializeComponent!
        InitializeComponent();

        // Napojení callbacků — jedině oprávněný kód v code-behind
        // vm.RequestFocusOnInput = () => InputTextBox.Focus();
        // vm.RequestShowMessage  = msg => MessageBox.Show(msg, Title);

        // Standardní zavření — vždy přítomno
        vm.RequestClose += result => { DialogResult = result; Close(); };
    }

    // Konstruktor pro WPF designer (zobrazí preview bez pádu)
    public $ARGUMENTS()
    {
        DataContext = new $ARGUMENTSViewModel();
        InitializeComponent();
    }
}
```

---

## Krok 3 — XAML (`$ARGUMENTS.xaml`)

Všechny interakce jdou přes `{Binding}` — žádné `Click="..."` handlery.

```xml
<Window x:Class="TemplateDesigner.Windows.$ARGUMENTS.$ARGUMENTS"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="{Binding Title}"
        Width="600" Height="400"
        WindowStartupLocation="CenterOwner">
    <Grid Margin="12">
        <Grid.RowDefinitions>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- Obsah — každý prvek bindovaný na ViewModel property -->
        <StackPanel Grid.Row="0">

            <!-- TextBox — TwoWay binding, aktualizuje VM při každé změně -->
            <!-- <TextBox x:Name="InputTextBox"
                         Text="{Binding SomeText, UpdateSourceTrigger=PropertyChanged}"/> -->

            <!-- ListBox — ItemsSource na ObservableCollection, SelectedItem TwoWay -->
            <!-- <ListBox ItemsSource="{Binding Items}"
                         SelectedItem="{Binding SelectedItem, Mode=TwoWay}"/> -->

            <!-- CheckBox — IsChecked TwoWay -->
            <!-- <CheckBox Content="Možnost" IsChecked="{Binding IsEnabled, Mode=TwoWay}"/> -->

            <!-- ComboBox — ItemsSource + SelectedItem TwoWay -->
            <!-- <ComboBox ItemsSource="{Binding Options}"
                          SelectedItem="{Binding SelectedOption, Mode=TwoWay}"/> -->

            <!-- Button — vždy Command, nikdy Click handler -->
            <!-- <Button Content="Přidat" Command="{Binding AddItemCommand}"/> -->

            <!-- Button s parametrem -->
            <!-- <Button Content="Smazat"
                         Command="{Binding DeleteItemCommand}"
                         CommandParameter="{Binding SelectedItem}"/> -->

        </StackPanel>

        <!-- Tlačítka OK / Storno -->
        <StackPanel Grid.Row="1" Orientation="Horizontal" HorizontalAlignment="Right"
                    Margin="0,12,0,0">
            <Button Content="OK"     Width="80" Margin="0,0,8,0"
                    IsDefault="True" Command="{Binding OkCommand}"/>
            <Button Content="Storno" Width="80"
                    IsCancel="True"/>
            <!-- IsCancel="True" zavírá okno s DialogResult=false automaticky — bez kódu -->
        </StackPanel>
    </Grid>
</Window>
```

---

## Krok 4 — Otevření okna

Otevření patří do **volajícího ViewModelu** (ne do code-behind):

```csharp
// V MainWindowViewModel.cs nebo jiném volajícím ViewModelu:
public RelayCommand Open$ARGUMENTSCommand { get; }

// V konstruktoru:
Open$ARGUMENTSCommand = new RelayCommand(() =>
{
    var vm = new $ARGUMENTSViewModel(/* závislosti */);
    var dlg = new Windows.$ARGUMENTS.$ARGUMENTS(vm)
    {
        Owner = Application.Current.MainWindow
    };
    if (dlg.ShowDialog() == true)
    {
        // Výsledek čti z vm.Result — ne z kontrolek okna
        var result = vm.Result;
        // zpracuj result...
    }
});
```

---

## Pomocné třídy ItemVM (pokud máš seznam)

Položky v `ObservableCollection` by také měly notifikovat změny:

```csharp
// Ve stejném souboru jako ViewModel, nebo ve vlastním souboru
public class ItemVM : EDViewModel
{
    private string _name = "";
    public string Name { get => _name; set => Set(ref _name, value); }

    // další properties...
}
```
