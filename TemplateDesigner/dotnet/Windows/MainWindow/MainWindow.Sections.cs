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

/// <summary>Seznam sekcí v levém panelu: výběr, přetahování pořadí, uložení sekce jako šablony.</summary>
public partial class MainWindow
{
    // ── Section list selection helper ─────────────────────────────────────────
    private bool _suppressSectionSelection;

    // ── Section drag reorder ────────────────────────────────────────────────────
    private SectionVM? _sectionDragCandidate;
    private System.Windows.Point _sectionDragStartPoint;
    private bool _sectionDragActive;
    private int _sectionDropIndex = -1;
    private int _sectionDropIndicatorAt = -1; // index itemu s upraveným Margin

    // ─────────────────────────────────────────────────────────────────────────
    // Section list - selection helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void RestoreSectionSelection()
    {
        _suppressSectionSelection = true;
        try { SectionsList.SelectedItem = _vm?.Sections.FirstOrDefault(s => s.Section == _vm?.State.ActiveSection); }
        finally { _suppressSectionSelection = false; }
    }

    private void SectionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSectionSelection || _vm is null) return;
        if (SectionsList.SelectedItem is SectionVM svm)
        {
            _suppressSectionSelection = true;
            try { _vm.ActivateSection(svm.Section); }
            finally { _suppressSectionSelection = false; }
        }
    }

    private void SectionsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SectionsList.SelectedItem is SectionVM svm)
            OpenPropertiesForSection(svm.Section);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Section drag reorder
    // ─────────────────────────────────────────────────────────────────────────

    private void SectionsList_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        _sectionDragCandidate = null;
        _sectionDragActive = false;
        if (GetSectionVMFromEvent(e) is { } svm)
        {
            _sectionDragStartPoint = e.GetPosition(SectionsList);
            _sectionDragCandidate = svm;
        }
    }

    private void SectionsList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            if (_sectionDragActive) SectionsList_DragFinish();
            return;
        }

        if (_sectionDragCandidate is not null && !_sectionDragActive)
        {
            var pos = e.GetPosition(SectionsList);
            if (Math.Abs(pos.X - _sectionDragStartPoint.X) < 5 && Math.Abs(pos.Y - _sectionDragStartPoint.Y) < 5)
                return;

            _sectionDragActive = true;
            SectionsList.CaptureMouse();
            SectionsList.Cursor = Cursors.Hand;
        }

        if (_sectionDragActive)
        {
            var pos = e.GetPosition(SectionsList);
            _sectionDropIndex = GetSectionDropIndex(pos);
            UpdateSectionDropIndicator();
        }
    }

    private void SectionsList_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_sectionDragActive)
        {
            SectionsList_DragFinish();
            e.Handled = true;
        }
        else
        {
            _sectionDragCandidate = null;
        }
    }

    private void SectionsList_DragCancel(object sender, MouseEventArgs e)
    {
        if (!_sectionDragActive) { _sectionDragCandidate = null; return; }
        _sectionDragCandidate = null;
        _sectionDragActive = false;
        _sectionDropIndex = -1;
        SectionsList.ReleaseMouseCapture();
        SectionsList.ClearValue(CursorProperty);
        ClearSectionDropIndicator();
    }

    private void SectionsList_DragFinish()
    {
        if (_sectionDragCandidate is not null && _sectionDropIndex >= 0 && _vm is not null)
        {
            int fromIndex = _vm.State.CurrentTemplate.Sections.IndexOf(_sectionDragCandidate.Section);
            int toIndex = _sectionDropIndex;
            // Adjust: pokud drop je za dragged item, odečti 1 (protože se item nejdřív odebere)
            if (toIndex > fromIndex) toIndex--;
            if (fromIndex >= 0 && toIndex >= 0 && fromIndex != toIndex)
            {
                _vm.ReorderSection(_sectionDragCandidate.Section, fromIndex, toIndex);
                // ActivateSection nevoláme - ReorderSection už volá RebuildSections
                _vm.State.ActiveSection = _sectionDragCandidate.Section;
            }
        }

        _sectionDragCandidate = null;
        _sectionDragActive = false;
        _sectionDropIndex = -1;
        SectionsList.ReleaseMouseCapture();
        SectionsList.ClearValue(CursorProperty);
        ClearSectionDropIndicator();
    }

    /// <summary>Vrátí index kam se má sekce vložit (0 = před první, Count = za poslední).</summary>
    private int GetSectionDropIndex(System.Windows.Point posInListBox)
    {
        if (_vm is null) return -1;
        int count = _vm.Sections.Count;

        for (int i = 0; i < count; i++)
        {
            if (SectionsList.ItemContainerGenerator.ContainerFromIndex(i) is not ListBoxItem container)
                continue;
            var itemPos = container.TranslatePoint(new System.Windows.Point(0, 0), SectionsList);
            float midY = (float)(itemPos.Y + container.ActualHeight / 2);
            if (posInListBox.Y < midY)
                return i;
        }
        return count;
    }

    private void UpdateSectionDropIndicator()
    {
        ClearSectionDropIndicator();
        if (_sectionDropIndex < 0 || _vm is null) return;

        int count = _vm.Sections.Count;
        int indicatorAt = _sectionDropIndex < count ? _sectionDropIndex : count - 1;
        bool atBottom = _sectionDropIndex >= count;

        var border = GetSectionItemBorder(indicatorAt);
        if (border is null) return;

        // Zvětšený Margin na vrchu/spodu funguje jako vizuální indikátor pozice dropu
        border.Margin = atBottom
            ? new Thickness(0, 2, 0, 4)
            : new Thickness(0, 6, 0, 0);
        _sectionDropIndicatorAt = indicatorAt;
    }

    private void ClearSectionDropIndicator()
    {
        if (_sectionDropIndicatorAt >= 0)
        {
            var border = GetSectionItemBorder(_sectionDropIndicatorAt);
            if (border is not null)
                border.Margin = new Thickness(0, 2, 0, 0); // obnovit výchozí
            _sectionDropIndicatorAt = -1;
        }
    }

    /// <summary>Najde vnitřní Border element ze SectionItemTemplate pro daný index.</summary>
    private Border? GetSectionItemBorder(int index)
    {
        if (SectionsList.ItemContainerGenerator.ContainerFromIndex(index) is not ListBoxItem container)
            return null;
        // ListBoxItem template = ContentPresenter → Border (z DataTemplate)
        container.ApplyTemplate();
        if (VisualTreeHelper.GetChildrenCount(container) == 0) return null;
        var presenter = VisualTreeHelper.GetChild(container, 0);
        if (VisualTreeHelper.GetChildrenCount(presenter) == 0) return null;
        return VisualTreeHelper.GetChild(presenter, 0) as Border;
    }

    private static SectionVM? GetSectionVMFromEvent(MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source)
        {
            var current = source;
            while (current is not null)
            {
                if (current is FrameworkElement { DataContext: SectionVM svm })
                    return svm;
                current = VisualTreeHelper.GetParent(current);
            }
        }
        return null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Save section as template (UI dialog - stays in code-behind)
    // ─────────────────────────────────────────────────────────────────────────

    private void SaveSectionAsTemplate(Section section)
    {
        if (_vm is null) return;
        var dlgVm = new SaveSectionTemplateDialogViewModel(section.Name);
        var dlg   = new SaveSectionTemplateDialog(dlgVm) { Owner = this };
        if (dlg.ShowDialog() == true)
            _vm.SaveSectionTemplate(section, dlgVm.Name);
    }
}
