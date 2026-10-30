using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using SkiaSharp;
using EDControls.EDInputForm;
using EDControls.EDTheme;
using TemplateDesigner.Commands;
using TemplateDesigner.Commands.Implementations;
using TemplateDesigner.Designer;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Config;
using TemplateDesigner.Serialization;
using ALWA.SYS;

namespace TemplateDesigner;

/// <summary>Klávesnice: zkratky okna a plátna.</summary>
public partial class MainWindow
{
    // ── Duplicate pending flag (Ctrl+D chord) ─────────────────────────────────
    private bool _duplicatePending;

    // ─────────────────────────────────────────────────────────────────────────
    // Keyboard handler
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Leží pole uvnitř panelu Pomocníka? Ten si klávesy obsluhuje sám.</summary>
    /// <remarks>Hledá se po vizuálním stromu, ne podle jména pole - jméno by se dalo přejmenovat,
    /// aniž by to někdo spojil s touhle výjimkou.</remarks>
    private static bool JePolePomocnika(DependencyObject? prvek)
    {
        while (prvek is not null)
        {
            if (prvek is Windows.AiGuideDialog.AiGuidePanel) return true;
            prvek = System.Windows.Media.VisualTreeHelper.GetParent(prvek);
        }
        return false;
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (_vm is null) return;
        // V inline edit modu nereagujeme na globální zkratky - TextBox si Enter/Esc obslouží sám.
        // Bez tohoto by PreviewKeyDown odchytil Enter dřív než TextBox a otevřel dialog místo commitu.
        if (_inlineEditElement is not null) return;

        // Úvodní obrazovka jde zavřít Esc, ať se dá rychle odklidit (Jan 30. 10.).
        if (e.Key == Key.Escape && _vm.StartPanel.IsVisible)
        {
            _vm.StartPanel.Hide();
            e.Handled = true;
            return;
        }

        // Uvnitř tabulky patří klávesy buňce, ne prvku (jinak by Delete smazal celou tabulku).
        // ⚠️ Jen když si je nenárokuje pole, kam se píše (`CellKeysActive`): režim buňky zůstává
        // zapnutý i po potvrzení textu, takže bez téhle podmínky by po kliknutí do pole v panelu
        // vlastností Delete mazal obsah buňky místo znaku v poli (nález revize 12. 8.).
        if (InTableCells && (_cellEditing || CellKeysActive))
        {
            if (HandleCellFormatKey(e) || HandleCellEditKey(e) || HandleTableCellsKey(e))
            { e.Handled = true; return; }
            if (_cellEditing) return;   // zbytek psaní obslouží textové pole samo
        }
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

        // Psaní v textovém poli (inline panel EDInputForm, filtr stromu, …): Enter potvrdí pole
        // (push LostFocus binding → živý write-back), ne otevře dialog vlastností. Ostatní bare
        // klávesy (Delete/šipky/písmena/F2) patří poli, ne globálním zkratkám - jinak by Delete
        // v poli smazal vybraný element. Ctrl/Alt zkratky (Ctrl+Z/S/…) procházejí dál.
        if (Keyboard.FocusedElement is System.Windows.Controls.TextBox focusedField)
        {
            // ⚠️ Pomocník si Enter obsluhuje sám (odeslat dotaz) a bez téhle výjimky by se k němu
            // nedostal: okno tuneluje `PreviewKeyDown` dřív než TextBox a Enter by tu skončil jako
            // potvrzení bindingu (nález Jan 16. 9. - v Pomocníkovi se muselo klikat na Odeslat).
            if (e.Key == Key.Enter && JePolePomocnika(focusedField)) return;
            if (e.Key == Key.Enter)
            {
                focusedField.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
                e.Handled = true;
                return;
            }
            if (!ctrl && !Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) return;
        }

        if (e.Key == Key.Z && ctrl)
        {
            _vm.UndoCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Y && ctrl)
        {
            _vm.RedoCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.S && ctrl)
        {
            _vm.SaveCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.N && ctrl && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            _vm.NewCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.O && ctrl)
        {
            MenuOpen_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.P && ctrl && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            _ = OpenPropertiesForPage(_vm.State.CurrentTemplate);
            e.Handled = true;
        }
        else if (e.Key == Key.P && ctrl)
        {
            MenuPrint_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.A && ctrl && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            _vm.ChangeAgendaCommand.Execute(null);
            e.Handled = true;
        }
        // Výběr instancí z klávesnice - vedle změny agendy, protože je to táž dvojice voleb
        // „nad čím pracuju". Písmeno I jako instance: agenda si sama říká linka nebo faktura,
        // takže doménové písmeno by platilo jen pro jízdní řády.
        else if (e.Key == Key.I && ctrl && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            _vm.OpenInstancePickerCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.PageUp && ctrl)
        {
            _vm.ChangeZIndex(+1, toExtreme: Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
            e.Handled = true;
        }
        else if (e.Key == Key.PageDown && ctrl)
        {
            _vm.ChangeZIndex(-1, toExtreme: Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
            e.Handled = true;
        }
        else if (e.Key == Key.A && ctrl && _vm.State.ActiveSection is not null)
        {
            _vm.State.SelectedElements.Clear();
            _vm.State.SelectedElements.AddRange(_vm.State.ActiveSection.Elements);
            UpdatePropertiesPanel();
            InvalidateCanvas();
            e.Handled = true;
        }
        else if (e.Key == Key.G && ctrl && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            _vm.UngroupSelected();
            InvalidateCanvas();
            e.Handled = true;
        }
        else if (e.Key == Key.G && ctrl)
        {
            _vm.GroupSelected();
            InvalidateCanvas();
            e.Handled = true;
        }
        else if (e.Key == Key.H && ctrl && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            _vm.SplitSelected(horizontal: true);
            e.Handled = true;
        }
        else if (e.Key == Key.J && ctrl && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            _vm.SplitSelected(horizontal: false);
            e.Handled = true;
        }
        else if (e.Key == Key.C && ctrl && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            _vm.CopyStyle();
            e.Handled = true;
        }
        else if (e.Key == Key.V && ctrl && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            _vm.PasteStyle();
            e.Handled = true;
        }
        else if (e.Key == Key.C && ctrl)
        {
            _vm.CopySelected();
            e.Handled = true;
        }
        else if (e.Key == Key.X && ctrl)
        {
            _vm.CutSelected();
            InvalidateCanvas();
            e.Handled = true;
        }
        else if (e.Key == Key.V && ctrl)
        {
            _vm.PasteClipboard();
            e.Handled = true;
        }
        else if (e.Key == Key.D && ctrl)
        {
            _duplicatePending = true;
            e.Handled = true;
        }
        else if (e.Key == Key.Delete)
        {
            _vm.DeleteSelected();
            e.Handled = true;
        }
        else if ((e.Key == Key.Up || e.Key == Key.Down)
                 && ctrl && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)
                 && _vm.State.ActiveSection is not null)
        {
            _vm.MoveSection(_vm.State.ActiveSection, e.Key == Key.Up ? -1 : +1);
            e.Handled = true;
        }
        else if (_duplicatePending && e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            _duplicatePending = false;
            var dir = e.Key switch
            {
                Key.Up    => DuplicateDirection.Up,
                Key.Down  => DuplicateDirection.Down,
                Key.Left  => DuplicateDirection.Left,
                Key.Right => DuplicateDirection.Right,
                _ => throw new InvalidOperationException()
            };
            _vm.DuplicateSelected(dir);
            e.Handled = true;
        }
        // Shift = velikost, Ctrl = posun po mřížce, samotné šipky = posun o milimetr. Shift na
        // velikosti je to, co uživatel zná ze starého návrháře; velký krok posunu tím zanikl,
        // protože při výchozí mřížce 1 mm dělal totéž jako šipka samotná (rozhodnutí Jan 1. 9.).
        else if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down
                 && _vm.State.SelectedElements.Count > 0)
        {
            switch (TemplateDesignerViewModel.ResolveArrowAction(ctrl, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)))
            {
                case ArrowKeyAction.Resize: _vm.ResizeByKey(e.Key, gridStep: false); break;
                case ArrowKeyAction.MoveByGrid: _vm.MoveByKey(e.Key, gridStep: true); break;
                default: _vm.MoveByKey(e.Key, gridStep: false); break;
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && _vm.State.SelectedElements.Count == 1)
        {
            OpenPropertiesForElement(_vm.State.SelectedElements[0]);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && _vm.State.SelectedElements.Count > 1)
        {
            OpenPropertiesForMultipleElements(_vm.State.SelectedElements);
            e.Handled = true;
        }
        else if (e.Key == Key.F2 && _vm.State.SelectedElements.Count == 1
                 && _vm.State.SelectedElements[0] is TextElement te
                 && te.Rotation == 0f && !te.IsLocked)
        {
            EnterInlineEdit(te);
            e.Handled = true;
        }
        // F2 = „uprav obsah na místě" i u pevné tabulky - dvojklik zůstává na nastavení, aby se
        // tabulka nechovala jinak než ostatní prvky (námitka Jana 12. 8.).
        else if (e.Key == Key.F2 && _vm.State.SelectedElements.Count == 1
                 && TryEnterTableCells(_vm.State.SelectedElements[0]))
        {
            e.Handled = true;
        }
        else if (e.Key == Key.F1)
        {
            ShowHelp();
            e.Handled = true;
        }
        else if (e.Key == Key.F5)
        {
            if (_vm.RefreshDataCommand.CanExecute(null)) _vm.RefreshDataCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.D0 && ctrl)
        {
            FitToPage();       // Ctrl+0 = Přizpůsobit stránce (== tlačítko „Fit")
            e.Handled = true;
        }
        else if (e.Key == Key.D1 && ctrl)
        {
            ResetZoomTo100();  // Ctrl+1 = Skutečná velikost 100 % (== tlačítko „1:1")
            e.Handled = true;
        }
        else if (e.Key == Key.B && ctrl)
        {
            BtnToggleLeftPanel.IsChecked = !(BtnToggleLeftPanel.IsChecked == true);
            e.Handled = true;
        }
        else if (e.Key == Key.N && ctrl && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            OpenNormalizePositionsDialog();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            // Escape ruší probíhající operaci: drag/resize vrátí pozice na začátek (commit: false),
            // rubber band se zahodí bez výběru. Bez operace = zrušit výběr.
            if (_mouseMode == MouseMode.AddingElement)   CancelAddingElement();
            else if (_mouseMode == MouseMode.Dragging)   EndDrag(commit: false);
            else if (_mouseMode == MouseMode.Resizing)   EndResize(commit: false);
            else if (_mouseMode == MouseMode.SectionResizing) EndSectionResize(commit: false);
            else if (_mouseMode == MouseMode.RubberBand)
            {
                _rubberBandRect = null;
                _mouseMode = MouseMode.Idle;
                skCanvas.ReleaseMouseCapture();
                InvalidateCanvas();
            }
            else
            {
                _vm.State.SelectedElements.Clear();
                UpdatePropertiesPanel();
                InvalidateCanvas();
            }
            e.Handled = true;
        }

        // Fallback: any key (except D itself) while duplicate is pending → clear flag + plain duplicate
        if (_duplicatePending && e.Key != Key.D)
        {
            _duplicatePending = false;
            if (!e.Handled) _vm.DuplicateSelected();
        }
    }
}
