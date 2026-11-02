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

/// <summary>Úprava textu přímo na plátně (F2).</summary>
public partial class MainWindow
{
    // ── Inline text edit (V0 - TextElement only, no rotation, no lock) ─────────

    private TextElement? _inlineEditElement;
    private string? _inlineEditOriginalText;

    private void EnterInlineEdit(TextElement el)
    {
        if (_canvas is null || _vm is null) return;
        var rect = _canvas.GetElementCanvasRect(el, (float)skCanvas.ActualWidth);
        if (rect is null) return;

        _inlineEditElement = el;
        _inlineEditOriginalText = el.Text;

        // Pozice + rozměr v canvas px (Canvas overlay má stejné souřadnice jako SKElement)
        Canvas.SetLeft(InlineEditBox, rect.Value.Left);
        Canvas.SetTop (InlineEditBox, rect.Value.Top);
        InlineEditBox.Width  = rect.Value.Width;
        InlineEditBox.Height = rect.Value.Height;

        // WPF FontSize v DIP; Skia kreslí FontSize_pt * MmToPx_per_inch * zoom px.
        // Pro vizuální shodu při zoom=1, 96dpi: FontSize_DIP = FontSize_pt * 96/72 * Zoom.
        float fontPt = el.Style.FontSize ?? 10f;
        InlineEditBox.FontSize   = fontPt * (96f / 72f) * _vm.State.Zoom;
        InlineEditBox.FontFamily = new FontFamily(el.Style.FontFamily ?? "Arial");
        InlineEditBox.Text       = el.Text ?? "";
        InlineEditBox.Visibility = Visibility.Visible;

        InlineEditBox.Focus();
        InlineEditBox.SelectAll();
    }

    private void ExitInlineEdit(bool commit)
    {
        if (_inlineEditElement is null || _vm is null) return;

        var el = _inlineEditElement;
        var originalText = _inlineEditOriginalText ?? "";
        var newText = InlineEditBox.Text ?? "";

        _inlineEditElement = null;
        _inlineEditOriginalText = null;
        InlineEditBox.Visibility = Visibility.Collapsed;

        if (commit && newText != originalText)
        {
            var cmd = new SetPropertyCommand(
                "Text", originalText, newText,
                v => el.Text = (string)(v ?? ""));
            _vm.Execute(cmd);
        }

        skCanvas.InvalidateVisual();
        skCanvas.Focus(); // vrátit focus na canvas pro klávesové zkratky
    }

    private void InlineEditBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Buňka tabulky má vlastní pravidla (Enter a Tab potvrdí a posunou se dál).
        if (_cellEditing)
        {
            if (HandleCellEditKey(e)) e.Handled = true;
            return;
        }

        // PreviewKeyDown - musí zachytit Enter dřív než TextBox.AcceptsReturn vloží newline.
        if (e.Key == Key.Escape)
        {
            ExitInlineEdit(commit: false);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            // Enter sám = commit; Shift+Enter = nový řádek (default WPF chování projde)
            ExitInlineEdit(commit: true);
            e.Handled = true;
        }
    }

    private void InlineEditBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_cellEditing)
        {
            // Klik do nabídky polí přebere fokus - to není odchod z buňky, ale výběr položky.
            if (CellFieldSuggestBox.IsMouseOver) return;
            EndCellEdit(commit: true);
            return;
        }
        if (_inlineEditElement is not null)
            ExitInlineEdit(commit: true);
    }
}
