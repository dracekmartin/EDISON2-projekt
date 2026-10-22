# EDFooter - poznámky pro vývojáře

Standardizovaný footer okna (`UserControl`, plně v XAML → viditelný v designeru).
`FooterMode`: `Close` (default) / `OkCancel`. Volitelný `LeftContent` (přiřazuje se
přímo do presenteru, ne bindingem). Když má `EDViewModel` okna neprázdné `Actions`,
footer automaticky zobrazí rozbalovací menu „Akce" vlevo - vše řízeno děděným
DataContext, žádné atributy.

## Zavírání okna (neodvoditelné chování)

- Na `Loaded` se footer přihlásí k `EDViewModel.RequestClose`. Non-null výsledek zkusí
  `window.DialogResult = result` (`ShowDialog()` okno se tím zavře); při
  `InvalidOperationException` (= `Show()` okno) fallback na `window.Close()`.
  Null → rovnou `Close()`. Host okno nepotřebuje žádný flag.
- Před `Close()` se volá `Owner?.Activate()` - vrací fokus vlastníkovi.
- **Zavřít i Storno sdílí handler**: pokud VM má `ConfirmOnCancel = true`, nejdřív se
  ptá přes `PopupDisplayer.ConfirmClose()` - okna s neuloženými daty tím dostanou
  potvrzení zdarma.
- **Esc = Zavřít/Storno** (týž handler). Footer ho bere z `KeyDown` okna, takže dostane jen Esc,
  který nezpracoval control s fokusem (otevřené combo, hledání s textem). Esc z popupu a opakování
  podržené klávesy ignoruje: `DatePicker` kalendář zavře, ale Esc neoznačí. `IsCancel` na tlačítku
  nejde: `Button` po `Click` zavře dialog sám a obešel by odmítnuté `ConfirmClose`.
- OK tlačítko: s VM volá `OkCommand` a spoléhá, že VM vyvolá `RequestClose`
  (validace v `OnOk` může zavření odmítnout); bez VM zavře rovnou.

## Umístění

Přidání okna viz root [CLAUDE.md](../../../CLAUDE.md) („Přidání nového okna"); footer na
spodek kořenového `DockPanel`: `<ef:EDFooter DockPanel.Dock="Bottom" FooterMode="OkCancel"/>`.
Potvrzení klávesou (Ctrl+Enter, OK bez `IsDefault`) řeší `EDViewModel.AttachToWindow` -
viz [EDViewModel/CLAUDE.md](../EDViewModel/CLAUDE.md).
