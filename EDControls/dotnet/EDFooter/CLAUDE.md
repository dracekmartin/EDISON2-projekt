# EDFooter — poznámky pro vývojáře

Standardizovaný footer okna jako obyčejný `UserControl` — plně definovaný v XAML, takže je
vždy viditelný v designeru.

## Mapa souborů

| Soubor | Role |
| --- | --- |
| `EFooterMode.cs` | Enum: `Close` (výchozí), `OkCancel`. |
| `EDFooter.xaml` | Border s levým `ContentPresenter` + pravý panel tlačítek. Výchozí viditelnost: Zavřít zobrazen, Ok/Storno skryté. |
| `EDFooter.xaml.cs` | DependencyProperties, přepínání viditelnosti tlačítek, napojení na ViewModel. Poznámka: `CloseBtn` (Zavřít) volá `CloseWindow(false)` — stejný handler jako Storno. |

## Dependency properties

| Vlastnost | Typ | Výchozí | Popis |
| --- | --- | --- | --- |
| `FooterMode` | `EFooterMode` | `Close` | Přepíná mezi Zavřít / Ok+Storno. |
| `LeftContent` | `UIElement?` | `null` | Volitelný element v levém sloupci footer gridu. Nastavuje se přes `LeftContent` property (ne binding — přiřazuje se přímo do `LeftContentPresenter.Content`). |

## Menu akcí (automatické)

Když `EDViewModel` okna vystavuje neprázdný `Actions` seznam, `EDFooter` automaticky zobrazí
**rozbalovací menu „Akce"** v levém dolním rohu — žádný XAML atribut není potřeba. Menu je řízeno
výhradně dědičností DataContext:

- `ActionsMenuItem` binduje `ItemsSource="{Binding Actions}"`.
- Viditelnost `Menu` binduje na `ActionsMenuItem.HasItems` přes `BooleanToVisibilityConverter` — menu se zobrazí a schová podle změn kolekce.
- `Header`, `Command`, `InputGestureText` a `Icon` každé položky se nastavují přes `ItemContainerStyle` bindingy na vlastnosti `EDAction`.

Slot `LeftContent` (vlastní tlačítka) je napravo od menu „Akce" ve stejném `StackPanel`.

## Napojení na ViewModel (automatické)

Na `Loaded` se `EDFooter` vyšplhá k rodičovskému `Window` a:

1. Přihlásí se k `Window.DataContextChanged`.
2. Pokud je DataContext `EDViewModel`, přihlásí se k `EDViewModel.RequestClose`.
3. Když `RequestClose` vyvolá non-null výsledek, zkusí `window.DialogResult = result`
   (úspěšné pro `ShowDialog()` okna, ta i zavře). Pokud to vyhodí
   `InvalidOperationException` (tj. `Show()` okno), spadne na `window.Close()`.
   Když `RequestClose` vyvolá `null`, volá `window.Close()` přímo.
   Na hostitelském okně není potřeba žádný flag.

## Potvrzení dialogu klávesou

OK tlačítko **nemá** `IsDefault="True"` — samotný Enter dialog nepotvrdí. Potvrzení jde přes
**Ctrl+Enter**, které `EDViewModel` automaticky registruje jako window-level `InputBinding`
pro `OkCommand`. Toto umožňuje EDInputFormu používat Enter pro navigaci mezi poli.

## Použití v XAML

```xaml
<Window xmlns:ef="clr-namespace:EDControls.EDFooter;assembly=EDControls">
    <DockPanel>
        <ef:EDFooter DockPanel.Dock="Bottom" FooterMode="OkCancel">
            <ef:EDFooter.LeftContent>
                <Button Content="Nápověda" Click="Help_Click" Padding="8,4"/>
            </ef:EDFooter.LeftContent>
        </ef:EDFooter>

        <!-- hlavní obsah vyplní zbývající prostor -->
        <Grid>...</Grid>
    </DockPanel>
</Window>
```

## Podpora designeru

`EDFooter` je standardní UserControl — jeho tlačítka jsou deklarována v XAML a vždy viditelná
v designeru. `FooterMode` přepíná viditelnost tlačítek přes `DependencyProperty`
`PropertyChangedCallback`, který běží i v design time.

---

## Vytvoření nového standardního okna

1. Přidej nové `Window` do EDISONMAIN2 (standardní WPF, žádná speciální bázová třída).
2. Přidej `<ef:EDFooter DockPanel.Dock="Bottom" FooterMode="OkCancel"/>` na spodek
   kořenového `DockPanel`.
3. Vytvoř ViewModel dědící `EDViewModel`. Caché volání jdou přes `MLRest` property
   (čte z `EDServices.Current`).
4. V konstruktoru nastav `DataContext = new MyViewModel(...)` **před** `InitializeComponent()`.

```csharp
public partial class MyWindow : Window
{
    private readonly MyViewModel _vm;

    public MyWindow()
    {
        DataContext = _vm = new MyWindowViewModel();
        InitializeComponent();
    }
}
```

5. Zaregistruj třídu okna ve `WindowNavigator` (děje se automaticky — navigator
   nachází okna podle názvu třídy). Přidej příkaz + tlačítko na toolbar v
   `EdisonMainViewModel` / `EdisonMainWindow.xaml` — viz vzor
   `OpenEDComponentAndViewModelTestCommand` / `EDComponentAndViewModelTestWindow`.

```csharp
// V EdisonMainViewModel:
OpenMyWindowCommand = Command(() => WindowNavigator.ShowDialog(new MyWindow()));
```
