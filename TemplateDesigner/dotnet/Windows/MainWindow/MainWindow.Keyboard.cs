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
        if (KeyboardShortcuts.Cancel.Matches(e) && _vm.StartPanel.IsVisible)
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

        if (KeyboardShortcuts.Undo.Matches(e))
        {
            _vm.UndoCommand.Execute(null);
            e.Handled = true;
        }
        else if (KeyboardShortcuts.Redo.Matches(e))
        {
            _vm.RedoCommand.Execute(null);
            e.Handled = true;
        }
        else if (KeyboardShortcuts.Save.Matches(e))
        {
            _vm.SaveCommand.Execute(null);
            e.Handled = true;
        }
        else if (KeyboardShortcuts.New.Matches(e))
        {
            _vm.NewCommand.Execute(null);
            e.Handled = true;
        }
        else if (KeyboardShortcuts.Open.Matches(e))
        {
            MenuOpen_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (KeyboardShortcuts.PageProperties.Matches(e))
        {
            _ = OpenPropertiesForPage(_vm.State.CurrentTemplate);
            e.Handled = true;
        }
        else if (KeyboardShortcuts.Print.Matches(e))
        {
            MenuPrint_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (KeyboardShortcuts.ChangeAgenda.Matches(e))
        {
            _vm.ChangeAgendaCommand.Execute(null);
            e.Handled = true;
        }
        // Výběr instancí z klávesnice - vedle změny agendy, protože je to táž dvojice voleb
        // „nad čím pracuju". Písmeno I jako instance: agenda si sama říká linka nebo faktura,
        // takže doménové písmeno by platilo jen pro jízdní řády.
        else if (KeyboardShortcuts.SelectInstances.Matches(e))
        {
            _vm.OpenInstancePickerCommand.Execute(null);
            e.Handled = true;
        }
        else if (KeyboardShortcuts.BringForward.Matches(e) || KeyboardShortcuts.BringToFront.Matches(e))
        {
            _vm.ChangeZIndex(+1, toExtreme: KeyboardShortcuts.BringToFront.Matches(e));
            e.Handled = true;
        }
        else if (KeyboardShortcuts.SendBackward.Matches(e) || KeyboardShortcuts.SendToBack.Matches(e))
        {
            _vm.ChangeZIndex(-1, toExtreme: KeyboardShortcuts.SendToBack.Matches(e));
            e.Handled = true;
        }
        else if (KeyboardShortcuts.SelectAll.Matches(e) && _vm.State.ActiveSection is not null)
        {
            _vm.State.SelectedElements.Clear();
            _vm.State.SelectedElements.AddRange(_vm.State.ActiveSection.Elements);
            UpdatePropertiesPanel();
            InvalidateCanvas();
            e.Handled = true;
        }
        else if (KeyboardShortcuts.Ungroup.Matches(e))
        {
            _vm.UngroupSelected();
            InvalidateCanvas();
            e.Handled = true;
        }
        else if (KeyboardShortcuts.Group.Matches(e))
        {
            _vm.GroupSelected();
            InvalidateCanvas();
            e.Handled = true;
        }
        else if (KeyboardShortcuts.SplitHorizontally.Matches(e))
        {
            _vm.SplitSelected(horizontal: true);
            e.Handled = true;
        }
        else if (KeyboardShortcuts.SplitVertically.Matches(e))
        {
            _vm.SplitSelected(horizontal: false);
            e.Handled = true;
        }
        else if (KeyboardShortcuts.CopyStyle.Matches(e))
        {
            _vm.CopyStyle();
            e.Handled = true;
        }
        else if (KeyboardShortcuts.PasteStyle.Matches(e))
        {
            _vm.PasteStyle();
            e.Handled = true;
        }
        else if (KeyboardShortcuts.Copy.Matches(e))
        {
            _vm.CopySelected();
            e.Handled = true;
        }
        else if (KeyboardShortcuts.Cut.Matches(e))
        {
            _vm.CutSelected();
            InvalidateCanvas();
            e.Handled = true;
        }
        else if (KeyboardShortcuts.Paste.Matches(e))
        {
            _vm.PasteClipboard();
            e.Handled = true;
        }
        else if (KeyboardShortcuts.Duplicate.Matches(e))
        {
            _duplicatePending = true;
            e.Handled = true;
        }
        else if (KeyboardShortcuts.Delete.Matches(e))
        {
            _vm.DeleteSelected();
            e.Handled = true;
        }
        else if ((KeyboardShortcuts.MoveSectionUp.Matches(e) || KeyboardShortcuts.MoveSectionDown.Matches(e))
                 && _vm.State.ActiveSection is not null)
        {
            _vm.MoveSection(_vm.State.ActiveSection, KeyboardShortcuts.MoveSectionUp.Matches(e) ? -1 : +1);
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
        else if (KeyboardShortcuts.Properties.Matches(e) && _vm.State.SelectedElements.Count == 1)
        {
            OpenPropertiesForElement(_vm.State.SelectedElements[0]);
            e.Handled = true;
        }
        else if (KeyboardShortcuts.Properties.Matches(e) && _vm.State.SelectedElements.Count > 1)
        {
            OpenPropertiesForMultipleElements(_vm.State.SelectedElements);
            e.Handled = true;
        }
        else if (KeyboardShortcuts.EditContent.Matches(e) && _vm.State.SelectedElements.Count == 1
                 && _vm.State.SelectedElements[0] is TextElement te
                 && te.Rotation == 0f && !te.IsLocked)
        {
            EnterInlineEdit(te);
            e.Handled = true;
        }
        // F2 = „uprav obsah na místě" i u pevné tabulky - dvojklik zůstává na nastavení, aby se
        // tabulka nechovala jinak než ostatní prvky (námitka Jana 12. 8.).
        else if (KeyboardShortcuts.EditContent.Matches(e) && _vm.State.SelectedElements.Count == 1
                 && TryEnterTableCells(_vm.State.SelectedElements[0]))
        {
            e.Handled = true;
        }
        else if (KeyboardShortcuts.Help.Matches(e))
        {
            ShowHelp();
            e.Handled = true;
        }
        else if (KeyboardShortcuts.RefreshData.Matches(e))
        {
            if (_vm.RefreshDataCommand.CanExecute(null)) _vm.RefreshDataCommand.Execute(null);
            e.Handled = true;
        }
        else if (KeyboardShortcuts.FitToPage.Matches(e))
        {
            FitToPage();
            e.Handled = true;
        }
        else if (KeyboardShortcuts.ActualSize.Matches(e))
        {
            ResetZoomTo100();
            e.Handled = true;
        }
        else if (KeyboardShortcuts.ToggleLeftPanel.Matches(e))
        {
            BtnToggleLeftPanel.IsChecked = !(BtnToggleLeftPanel.IsChecked == true);
            e.Handled = true;
        }
        else if (KeyboardShortcuts.NormalizePositions.Matches(e))
        {
            OpenNormalizePositionsDialog();
            e.Handled = true;
        }
        else if (KeyboardShortcuts.Cancel.Matches(e))
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
