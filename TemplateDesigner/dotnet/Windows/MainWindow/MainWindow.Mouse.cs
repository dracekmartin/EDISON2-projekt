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

/// <summary>Myš na plátně: výběr, přesun, změna velikosti, tažení hrany sekce, přidávání prvku.</summary>
public partial class MainWindow
{
    // ── Mouse state machine ───────────────────────────────────────────────────
    private enum MouseMode { Idle, Dragging, Resizing, RubberBand, AddingElement, SectionResizing }
    private MouseMode _mouseMode = MouseMode.Idle;

    private TemplateElement? _dragElement;
    private float _dragMouseStartCanvasX, _dragMouseStartCanvasY;
    private readonly Dictionary<TemplateElement, (float X, float Y)> _dragInitPositions = new();
    // Per-axis snap state z posledního mouse move (pro soft-snap v EndDrag)
    private bool _lastSnapActiveX, _lastSnapActiveY;
    // True jakmile myš překročí click-vs-drag práh (4px). Před tím se klik chová jako selekce
    // bez posunu - jinak by jemný cuk myší při kliku vyvolal soft-snap a rozbil pozici.
    private bool _dragHasMoved;
    private const float DragThresholdPxSquared = 16f; // 4px

    private readonly SnapEngine _snapEngine = new();

    private SKPoint _rubberBandStart;
    private SKRect? _rubberBandRect;

    private TemplateElement? _resizeElement;
    private ResizeHandle     _resizeHandle;
    private float  _resizeInitX, _resizeInitY, _resizeInitW;
    private float? _resizeInitH;
    private float  _resizeInitHComputed;
    private float  _resizeMouseStartX, _resizeMouseStartY;
    private float  _resizeOriginX, _resizeOriginY;
    private float  _resizeSectionW, _resizeSectionH; // hranice sekce pro clamp

    // ── Tažení spodní hrany sekce (mění její výšku, sekce pod se posunou) ─────
    private Section? _sectionResizeTarget;
    private float    _sectionResizeStartY;      // px, kde tažení začalo
    private float    _sectionResizeStartH;      // mm, výška před tažením
    private bool     _sectionResizeUpwards;     // patička je kotvená dole, takže roste nahoru

    // ─────────────────────────────────────────────────────────────────────────
    // Mouse state machine
    // ─────────────────────────────────────────────────────────────────────────

    private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_vm is null || _canvas is null) return;

        if (_duplicatePending)
        {
            _duplicatePending = false;
            _vm.DuplicateSelected();
        }

        var pos = e.GetPosition(skCanvas);
        float cx = (float)pos.X, cy = (float)pos.Y, cw = (float)skCanvas.ActualWidth;

        if (e.ClickCount == 2)
        {
            var dblHit = _canvas.HitTestElementForClick(cx, cy, cw);
            if (dblHit is not null)
            {
                var (dblSection, dblElement) = dblHit.Value;
                if (!_vm.State.SelectedElements.Contains(dblElement))
                {
                    _vm.State.SelectedElements.Clear();
                    _vm.State.SelectedElements.Add(dblElement);
                    _vm.ActivateSection(dblSection);
                }
                OpenPropertiesForElement(dblElement);
                e.Handled = true;
                return;
            }
            var dblSection2 = _canvas.HitTestSectionHeader(cx, cy, cw);
            if (dblSection2 is not null)
            {
                OpenPropertiesForSection(dblSection2);
                e.Handled = true;
                return;
            }
            // U4: dvojklik do prázdna = zrušit výběr (ne otevřít vlastnosti stránky - ty jsou
            // na Ctrl+Shift+P / menu Zobrazení). Dialog do prázdna je pro uživatele překvapení.
            _vm.State.SelectedElements.Clear();
            UpdatePropertiesPanel();
            InvalidateCanvas();
            e.Handled = true;
            return;
        }

        // Spodní hrana sekce - tažením se mění její výška a sekce pod ní se posunou.
        // ⚠️ Až PO úchytu prvku: prvek, který se hrany dotýká, musí zůstat roztažitelný.
        if (_canvas.HitTestHandle(cx, cy, cw) is null &&
            _canvas.HitTestSectionEdge(cx, cy, cw) is { } edge &&
            edge.Section.DesignerHeight is { } edgeStartH)
        {
            _sectionResizeTarget  = edge.Section;
            _sectionResizeUpwards = edge.RosteNahoru;
            _sectionResizeStartY = cy;
            _sectionResizeStartH = edgeStartH;
            _vm.State.ActiveSection = edge.Section;

            _mouseMode = MouseMode.SectionResizing;
            skCanvas.CaptureMouse();
            skCanvas.Cursor = System.Windows.Input.Cursors.SizeNS;
            e.Handled = true;
            return;
        }

        var handleHit = _canvas.HitTestHandle(cx, cy, cw);
        if (handleHit.HasValue)
        {
            var (el, handle) = handleHit.Value;
            float? originalH = el.Height;
            if (el.Height is null) el.Height = el.EffectiveDesignerHeight;

            _resizeElement       = el;
            _resizeHandle        = handle;
            _resizeInitX         = el.X;
            _resizeInitY         = el.Y;
            _resizeInitW         = el.Width;
            _resizeInitH         = originalH;
            _resizeInitHComputed = el.Height!.Value;
            var origin = _canvas.GetSectionOrigins(cw)
                                .FirstOrDefault(t => t.Section.Elements.Contains(el));
            _resizeOriginX     = origin.OriginX;
            _resizeOriginY     = origin.OriginY;
            _resizeSectionW    = _vm.State.CurrentTemplate.PageSize.PrintableWidth;
            _resizeSectionH    = origin.Section.EffectiveDesignerHeight;
            _resizeMouseStartX = (cx - _resizeOriginX) / _canvas.MmToPx;
            _resizeMouseStartY = (cy - _resizeOriginY) / _canvas.MmToPx;

            _mouseMode         = MouseMode.Resizing;
            skCanvas.CaptureMouse();
            skCanvas.Cursor = GetResizeCursor(handle);
            e.Handled = true;
            return;
        }

        var hit = _canvas.HitTestElementForClick(cx, cy, cw);

        // Hrana sloupce má přednost před výběrem buňky i před posunem prvku - myš na ní ukazuje
        // kurzor pro změnu šířky, takže uživatel míří na ni.
        if (TryBeginDividerDrag(cx, cy, cw))
        {
            e.Handled = true;
            return;
        }

        // Uvnitř tabulky: klik přepne buňku (Excel), klik jinam nás z tabulky pustí ven.
        ExitTableCellsIfElsewhere(hit?.Element);
        if (InTableCells && TrySelectCellAt(cx, cy, cw))
        {
            e.Handled = true;
            return;
        }

        if (hit is not null)
        {
            var (section, element) = hit.Value;
            bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
            bool alt  = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);

            // Kam se kliklo - F2 pak otevře tuhle buňku, ne pořád levou horní.
            RememberElementClick(element, cx, cy, cw);

            // Alt+klik = rovnou do té buňky. Alt v aplikaci znamená „o úroveň hloub" (vytáhnout
            // prvek ze skupiny), takže obsah prvku je jeho přirozené pokračování - a je to jeden
            // klik bez F-klávesy, což na noteboocích s Fn řadou rozhoduje.
            if (alt && element is TableElement { Mode: TableMode.Fixed })
            {
                _vm.State.SelectedElements.Clear();
                _vm.State.SelectedElements.Add(element);
                _vm.ActivateSection(section);
                if (TryEnterTableCells(element))
                {
                    UpdatePropertiesPanel();
                    e.Handled = true;
                    return;
                }
            }

    
            _vm.HandleElementHit(section, element, ctrl, alt);

            _dragElement           = element;
            _dragMouseStartCanvasX = cx;
            _dragMouseStartCanvasY = cy;
            _dragHasMoved          = false;
            _dragInitPositions.Clear();
            foreach (var el in _vm.State.SelectedElements.Where(el2 => !el2.IsLocked))
                _dragInitPositions[el] = (el.X, el.Y);

            _mouseMode = MouseMode.Dragging;
            skCanvas.CaptureMouse();
            UpdatePropertiesPanel();
            InvalidateCanvas();
            e.Handled = true;
            return;
        }

        var section2 = _canvas.HitTestSection(cx, cy, cw);
        _vm.HandleEmptyAreaHit(section2, Keyboard.Modifiers.HasFlag(ModifierKeys.Control));

        _rubberBandStart = new SKPoint(cx, cy);
        _rubberBandRect  = null;
        _mouseMode       = MouseMode.RubberBand;
        skCanvas.CaptureMouse();
    }

    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        if (_vm is null || _canvas is null) return;
        var pos = e.GetPosition(skCanvas);
        float cx = (float)pos.X, cy = (float)pos.Y, cw = (float)skCanvas.ActualWidth;

        if (InDividerDrag)
        {
            if (e.LeftButton != MouseButtonState.Pressed) { EndDividerDrag(true); return; }
            UpdateDividerDrag(cx, cy, cw);
            return;
        }

        // Tažení výběru buněk uvnitř tabulky - musí být před přesunem prvku, jinak by tah po
        // buňkách hýbal celou tabulkou.
        if (InCellRangeDrag)
        {
            if (e.LeftButton != MouseButtonState.Pressed) { EndCellRangeDrag(); return; }
            UpdateCellRangeDrag(cx, cy, cw);
            return;
        }

        if (_mouseMode == MouseMode.Dragging)
        {
            if (e.LeftButton != MouseButtonState.Pressed) { EndDrag(false); return; }
            if (_dragElement is null || _dragInitPositions.Count == 0) return;

            // Click vs drag - dokud myš nepřekročí threshold, nehýbáme s elementem.
            // Brání tomu, aby pouhý klik (např. pro otevření properties) zaregistroval soft-snap.
            if (!_dragHasMoved)
            {
                float dxPx = cx - _dragMouseStartCanvasX;
                float dyPx = cy - _dragMouseStartCanvasY;
                if (dxPx * dxPx + dyPx * dyPx < DragThresholdPxSquared) return;
                _dragHasMoved = true;
            }

            float deltaMmX = (cx - _dragMouseStartCanvasX) / _canvas.MmToPx;
            float deltaMmY = (cy - _dragMouseStartCanvasY) / _canvas.MmToPx;

            _vm.State.ActiveGapIndicators.Clear();
            _vm.State.SnapTargetElements.Clear();
            _vm.State.ActiveSectionCenterX = null;
            bool snappedX = false, snappedY = false;
            if (_dragInitPositions.TryGetValue(_dragElement!, out var refInit))
            {
                // Ctrl držený = dočasně vypnout snap (free positioning).
                bool ctrlHeld = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
                if (_vm.State.SnapToElements && _vm.State.ActiveSection is { } section && !ctrlHeld)
                {
                    var others = section.Elements.Where(el2 => !_dragInitPositions.ContainsKey(el2));
                    float threshold = 8f / _canvas.MmToPx; // ~8px
                    _snapEngine.GapMm = _vm.State.SnapGapMm;

                    // Edge anchors: hrany sekce/printable area - edge-to-edge i gap snap (mezera od okraje).
                    float sectionW = _vm.State.CurrentTemplate.PageSize.PrintableWidth;
                    float sectionH = section.EffectiveDesignerHeight;
                    var edgeX = new List<float> { 0f, sectionW };
                    var edgeY = new List<float> { 0f, sectionH };

                    // Extra X anchors: X hodnoty elementů z ostatních sekcí
                    // (všechny sekce sdílejí levý margin → X je přímo porovnatelné). Bez gap snap.
                    var extraX = new List<float>();
                    foreach (var otherSection in _vm.State.CurrentTemplate.Sections)
                    {
                        if (otherSection == section) continue;
                        foreach (var el in otherSection.Elements)
                        {
                            extraX.Add(el.X);
                            extraX.Add(el.X + el.Width);
                        }
                    }

                    // Extra Y anchors: vertikální střed. Bez gap snap.
                    float refElH = _dragElement!.Height ?? _dragElement!.EffectiveDesignerHeight;
                    var extraY = new List<float> { (sectionH - refElH) / 2f };

                    // Section center X (center-to-anchor) - pro horizontální guide line.
                    float sectionCenterX = sectionW / 2f;

                    var snap = _snapEngine.Calculate(
                        _dragElement!, (refInit.X, refInit.Y),
                        deltaMmX, deltaMmY, others, threshold,
                        extraXAnchors: extraX, extraYAnchors: extraY,
                        edgeXAnchors: edgeX, edgeYAnchors: edgeY,
                        sectionCenterX: sectionCenterX);

                    if (snap.X.HasSnap) { deltaMmX = snap.X.AdjustedDelta; snappedX = true; }
                    if (snap.Y.HasSnap) { deltaMmY = snap.Y.AdjustedDelta; snappedY = true; }
                    if (snap.X.Gap is { } gx) _vm.State.ActiveGapIndicators.Add(gx);
                    if (snap.Y.Gap is { } gy) _vm.State.ActiveGapIndicators.Add(gy);

                    // Section center guide line - pokud snap právě sedí na sectionCenterX
                    _vm.State.ActiveSectionCenterX = snap.X.IsSectionCenter ? (float?)sectionCenterX : null;

                    // Zvýrazni cílové elementy
                    var targets = (snap.X.MatchedElements ?? []).Concat(snap.Y.MatchedElements ?? []).Distinct();
                    _vm.State.SnapTargetElements.AddRange(targets);
                }

                if (_vm.State.SnapToGrid && !ctrlHeld)
                {
                    float g = _vm.State.GridSize;
                    // Bounds elementu v sekci - aby grid snap nepřekročil okraje (jinak by clamp odsunul element mimo grid line)
                    float boundSectionW = _vm.State.CurrentTemplate.PageSize.PrintableWidth;
                    var gridSection = _vm.State.CurrentTemplate.Sections.FirstOrDefault(s => s.Elements.Contains(_dragElement!));
                    float boundSectionH = gridSection?.EffectiveDesignerHeight ?? float.MaxValue;
                    float boundElW = _dragElement!.Width;
                    float boundElH = _dragElement!.Height ?? _dragElement!.EffectiveDesignerHeight;
                    float maxGridX = MathF.Max(0, MathF.Floor((boundSectionW - boundElW) / g) * g);
                    float maxGridY = MathF.Max(0, MathF.Floor((boundSectionH - boundElH) / g) * g);

                    // Grid snap má prioritu - pokud je zapnutý, zaokrouhlí i výsledek smart snapu,
                    // takže pozice je vždy grid-aligned (očekávání uživatele: "mám grid 1mm → pozice jsou v 1mm krocích").
                    // Bez tohoto smart snap k non-grid pozicím sousedů (typicky po PDF importu) obchází grid.
                    {
                        float sx = MathF.Round((refInit.X + deltaMmX) / g) * g;
                        sx = Math.Clamp(sx, 0f, maxGridX);
                        deltaMmX = sx - refInit.X;
                    }
                    {
                        float sy = MathF.Round((refInit.Y + deltaMmY) / g) * g;
                        sy = Math.Clamp(sy, 0f, maxGridY);
                        deltaMmY = sy - refInit.Y;
                    }
                }
            }
            _lastSnapActiveX = snappedX;
            _lastSnapActiveY = snappedY;

            var (clampedX, clampedY) = ClampDragDelta(deltaMmX, deltaMmY);
            foreach (var kvp in _dragInitPositions)
            { kvp.Key.X = kvp.Value.X + clampedX; kvp.Key.Y = kvp.Value.Y + clampedY; }

            // Měřící badge - aktuální pozice draggovaného elementu
            _vm.State.DragBadgeText = $"X: {_dragElement!.X:0.#} mm   Y: {_dragElement!.Y:0.#} mm";
            _vm.State.DragBadgeX = cx;
            _vm.State.DragBadgeY = cy;

            InvalidateCanvas();
            return;
        }

        if (_mouseMode == MouseMode.SectionResizing)
        {
            if (e.LeftButton != MouseButtonState.Pressed) { EndSectionResize(commit: false); return; }
            if (_sectionResizeTarget is null) return;

            // Patička roste nahoru (je kotvená ke spodku papíru), ostatní sekce dolů.
            float posunMm = (cy - _sectionResizeStartY) / _canvas.MmToPx;
            if (_sectionResizeUpwards) posunMm = -posunMm;
            float novaMm  = _sectionResizeStartH + posunMm;

            // Ctrl vypíná mřížku - stejně jako u prvků.
            if (_vm.State.SnapToGrid && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                novaMm = MathF.Round(novaMm / _vm.State.GridSize) * _vm.State.GridSize;

            // Táž mez jako u „Vyplnit zbytek stránky": sekce nesmí vytlačit ostatní ze stránky.
            // Bez toho by tatáž věc měla dvě chování podle toho, kudy k ní uživatel přišel.
            float strop = Models.Layout.SectionLayoutMath.ZbyvajiciMistoMm(
                _vm.State.CurrentTemplate, _sectionResizeTarget);
            // Dno = obsah sekce. Bez toho by se prvek u dolního okraje v návrháři oříznul,
            // ale při tisku vytiskl celý (engine sekci nafoukne na obsah).
            float dno = Models.Layout.SectionLayoutMath.MinimalniVyskaProSekci(_sectionResizeTarget);
            novaMm = Math.Clamp(novaMm, dno, MathF.Max(dno, strop));

            _sectionResizeTarget.DesignerHeight = novaMm;
            _vm.StatusText = $"Výška sekce {_sectionResizeTarget.Name}: {novaMm:0.#} mm"
                + (novaMm >= strop - 0.05f ? " (na hranici stránky)" : "");

            UpdateCanvasSize();
            InvalidateCanvas();
            return;
        }

        if (_mouseMode == MouseMode.Resizing)
        {
            if (e.LeftButton != MouseButtonState.Pressed) { EndResize(false); return; }
            if (_resizeElement is null) return;

            float mouseMmX = (cx - _resizeOriginX) / _canvas.MmToPx;
            float mouseMmY = (cy - _resizeOriginY) / _canvas.MmToPx;

            // Ctrl držený = dočasně vypnout snap (free positioning/resize)
            bool resizeCtrlHeld = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
            if (_vm.State.SnapToGrid && !resizeCtrlHeld)
            {
                float g = _vm.State.GridSize;
                mouseMmX = MathF.Round(mouseMmX / g) * g;
                mouseMmY = MathF.Round(mouseMmY / g) * g;
            }

            var (newX, newY, newW, newH) = ComputeResize(mouseMmX, mouseMmY);

            bool movL = _resizeHandle is ResizeHandle.TopLeft or ResizeHandle.MiddleLeft or ResizeHandle.BottomLeft;
            bool movR = _resizeHandle is ResizeHandle.TopRight or ResizeHandle.MiddleRight or ResizeHandle.BottomRight;
            bool movT = _resizeHandle is ResizeHandle.TopLeft or ResizeHandle.TopCenter or ResizeHandle.TopRight;
            bool movB = _resizeHandle is ResizeHandle.BottomLeft or ResizeHandle.BottomCenter or ResizeHandle.BottomRight;

            // Smart snap při resize
            _vm.State.ActiveGapIndicators.Clear();
            _vm.State.SnapTargetElements.Clear();
            _vm.State.ActiveSectionCenterX = null;
            if (_vm.State.SnapToElements && _vm.State.ActiveSection is { } resizeSection && !resizeCtrlHeld)
            {
                var others = resizeSection.Elements.Where(e2 => e2 != _resizeElement);
                float threshold = 8f / _canvas.MmToPx;
                _snapEngine.GapMm = _vm.State.SnapGapMm;

                var snap = _snapEngine.CalculateResize(
                    newX, newY, newW, newH,
                    movL, movR, movT, movB,
                    others, _resizeElement, threshold);

                newX = snap.X; newY = snap.Y; newW = snap.W; newH = snap.H;
                foreach (var g in snap.Gaps) _vm.State.ActiveGapIndicators.Add(g);
                _vm.State.SnapTargetElements.AddRange(snap.Targets);
            }

            // Snap na čtverec - u rohového úchytu tvarového elementu dorovná W≈H na přesný
            // čtverec (uživatel nemusí ručně dopočítávat rovné strany). Práh v px → v mm dle zoomu;
            // Ctrl snap vypíná. Protilehlý roh (kotva) zůstává, dorovnaná strana snapuje na grid.
            bool squareSnapped = false;
            if (!resizeCtrlHeld && (movL || movR) && (movT || movB)
                && _resizeElement is RectangleElement or ImageElement or BarcodeElement
                && newH is { } hVal && newW > 0.1f && hVal > 0.1f
                && MathF.Abs(newW - hVal) <= 6f / _canvas.MmToPx)
            {
                float s = (newW + hVal) / 2f;
                if (_vm.State.SnapToGrid)
                {
                    float g = _vm.State.GridSize;
                    s = MathF.Round(s / g) * g;
                }
                s = MathF.Max(s, 0.1f);
                float candX = movL ? newX + newW - s : newX;
                float candY = movT ? newY + hVal - s : newY;
                if (candX >= 0 && candY >= 0
                    && candX + s <= _resizeSectionW && candY + s <= _resizeSectionH)
                {
                    newX = candX; newY = candY; newW = s; newH = s;
                    squareSnapped = true;
                }
            }

            _resizeElement.X = newX; _resizeElement.Y = newY;
            _resizeElement.Width = newW; _resizeElement.Height = newH;

            // Měřící badge - aktuální rozměr a pozice (stejný formát jako u dragu)
            _vm.State.DragBadgeText = squareSnapped
                ? $"{newW:0.#} × {newH:0.#} mm  ◻ čtverec   X: {newX:0.#}   Y: {newY:0.#} mm"
                : $"{newW:0.#} × {newH:0.#} mm   X: {newX:0.#}   Y: {newY:0.#} mm";
            _vm.State.DragBadgeX = cx;
            _vm.State.DragBadgeY = cy;

            InvalidateCanvas();
            return;
        }

        if (_mouseMode == MouseMode.RubberBand)
        {
            if (e.LeftButton != MouseButtonState.Pressed) { EndRubberBand(); return; }
            _rubberBandRect = new SKRect(
                Math.Min(_rubberBandStart.X, cx), Math.Min(_rubberBandStart.Y, cy),
                Math.Max(_rubberBandStart.X, cx), Math.Max(_rubberBandStart.Y, cy));
            InvalidateCanvas();
            return;
        }

        if (_mouseMode == MouseMode.AddingElement)
        {
            if (e.LeftButton != MouseButtonState.Pressed) { CancelAddingElement(); return; }
            _addingGhostPos = new SKPoint(cx, cy);
            _addingTargetSection = _canvas.HitTestSection(cx, cy, cw);

            // Pro field drag: detekce TextElementu pod kurzorem (pro append)
            _addingTargetElement = null;
            if (_addingFieldName is not null)
            {
                var hit = _canvas.HitTestElement(cx, cy, cw);
                if (hit?.Element is TextElement)
                    _addingTargetElement = hit.Value.Element;
            }

            InvalidateCanvas();
            return;
        }

        var handleHit2 = _canvas.HitTestHandle(cx, cy, cw);
        // Hrana sloupce pevné tabulky má přednost před „posunout prvek" - jinak by o možnosti
        // táhnout šířku uživatel nevěděl, protože kurzor by pořád ukazoval posun.
        if (DividerAt(cx, cy, cw) is { } divider)
            skCanvas.Cursor = divider.Kind == DividerKind.Column ? Cursors.SizeWE : Cursors.SizeNS;
        else if (handleHit2.HasValue)
            skCanvas.Cursor = GetResizeCursor(handleHit2.Value.Handle);
        else if (_canvas.HitTestSectionBottomEdge(cx, cy, cw) is not null)
            // Bez kurzoru by o možnosti táhnout výšku sekce nikdo nevěděl - hrana není nijak
            // vyznačená a klik na ni jinak jen vybere sekci.
            skCanvas.Cursor = Cursors.SizeNS;
        else
        {
            // Táž cesta jako klik, aby kurzor předpovídal, co se stane (i u vybraného elementu pod jiným).
            var elHit = _canvas.HitTestElementForClick(cx, cy, cw);
            skCanvas.Cursor = (elHit.HasValue && !elHit.Value.Element.IsLocked)
                ? Cursors.SizeAll : Cursors.Arrow;
        }
    }

    private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (InDividerDrag)                          { EndDividerDrag(true); return; }
        if (InCellRangeDrag)                        EndCellRangeDrag();
        else if (_mouseMode == MouseMode.Dragging)  EndDrag(true);
        else if (_mouseMode == MouseMode.Resizing)  EndResize(true);
        else if (_mouseMode == MouseMode.SectionResizing) EndSectionResize(true);
        else if (_mouseMode == MouseMode.RubberBand) EndRubberBand();
        else if (_mouseMode == MouseMode.AddingElement) EndAddingElement();
    }

    private void OnCanvasRightClick(object sender, MouseButtonEventArgs e)
    {
        if (_vm is null || _canvas is null) return;
        var pos  = e.GetPosition(skCanvas);
        float cx = (float)pos.X, cy = (float)pos.Y;
        float cw = (float)skCanvas.ActualWidth;

        var hit = _canvas.HitTestElementForClick(cx, cy, cw);
        var menu = new ContextMenu();

        // Pravý klik na pevnou tabulku nabízí práci s řádky a sloupci - i když uživatel zrovna
        // není „uvnitř". Jinak by se k vkládání řádku dostal jen ten, kdo tuší Alt+klik.
        if (hit?.Element is TableElement { Mode: TableMode.Fixed } rightClickedTable
            && !ReferenceEquals(rightClickedTable, _cellTable))
        {
            _vm.State.SelectedElements.Clear();
            _vm.State.SelectedElements.Add(rightClickedTable);
            _vm.ActivateSection(hit.Value.Section);
            RememberElementClick(rightClickedTable, cx, cy, cw);
            TryEnterTableCells(rightClickedTable, beginEdit: false);
            UpdatePropertiesPanel();
        }

        if (InTableCells && ReferenceEquals(hit?.Element, _cellTable)
            && TryBuildCellContextMenu(menu, cx, cy, cw))
        {
            menu.PlacementTarget = skCanvas;
            menu.IsOpen = true;
            e.Handled = true;
            return;
        }
        ExitTableCellsIfElsewhere(hit?.Element);

        if (hit.HasValue)
        {
            var el = hit.Value.Element;
            if (!_vm.State.SelectedElements.Contains(el))
            {
                _vm.State.SelectedElements.Clear();
                _vm.State.SelectedElements.Add(el);
                InvalidateCanvas();
            }

            menu.Items.Add(new MenuItem { Header = "Vyjmout (Ctrl+X)", Command = _vm.CutCommand });
            menu.Items.Add(new MenuItem { Header = "Kopírovat (Ctrl+C)", Command = _vm.CopyCommand });
            menu.Items.Add(new MenuItem { Header = "Vložit (Ctrl+V)", Command = _vm.PasteCommand });
            menu.Items.Add(new Separator());
            menu.Items.Add(new MenuItem { Header = "Smazat (Del)", Command = _vm.DeleteCommand });
            menu.Items.Add(new Separator());

            var zMenu = new MenuItem { Header = "Vrstva" };
            zMenu.Items.Add(new MenuItem { Header = "Dopředu", Command = new EDControls.EDViewModel.RelayCommand(() => { _vm.ChangeZIndex(1, false); InvalidateCanvas(); }) });
            zMenu.Items.Add(new MenuItem { Header = "Dozadu", Command = new EDControls.EDViewModel.RelayCommand(() => { _vm.ChangeZIndex(-1, false); InvalidateCanvas(); }) });
            zMenu.Items.Add(new MenuItem { Header = "Úplně dopředu", Command = new EDControls.EDViewModel.RelayCommand(() => { _vm.ChangeZIndex(1, true); InvalidateCanvas(); }) });
            zMenu.Items.Add(new MenuItem { Header = "Úplně dozadu", Command = new EDControls.EDViewModel.RelayCommand(() => { _vm.ChangeZIndex(-1, true); InvalidateCanvas(); }) });
            menu.Items.Add(zMenu);

            // Seskupení / rozdělení / editace textu - dřív jen na zkratkách, teď viditelné v menu.
            menu.Items.Add(new Separator());
            if (_vm.State.SelectedElements.Count >= 2)
                menu.Items.Add(new MenuItem { Header = "Seskupit (Ctrl+G)", Command = new EDControls.EDViewModel.RelayCommand(() => { _vm.GroupSelected(); InvalidateCanvas(); }) });
            if (_vm.State.SelectedElements.Any(x => x.GroupId is not null))
                menu.Items.Add(new MenuItem { Header = "Rozeskupit (Ctrl+Shift+G)", Command = new EDControls.EDViewModel.RelayCommand(() => { _vm.UngroupSelected(); InvalidateCanvas(); }) });

            var splitMenu = new MenuItem { Header = "Rozdělit" };
            splitMenu.Items.Add(new MenuItem { Header = "Vodorovně (Ctrl+Shift+H)", Command = new EDControls.EDViewModel.RelayCommand(() => { _vm.SplitSelected(horizontal: true); InvalidateCanvas(); }) });
            splitMenu.Items.Add(new MenuItem { Header = "Svisle (Ctrl+Shift+J)", Command = new EDControls.EDViewModel.RelayCommand(() => { _vm.SplitSelected(horizontal: false); InvalidateCanvas(); }) });
            menu.Items.Add(splitMenu);

            if (el is TextElement te)
                menu.Items.Add(new MenuItem { Header = "Upravit text (F2)", Command = new EDControls.EDViewModel.RelayCommand(() => EnterInlineEdit(te)) });
            if (el is TableElement { Mode: TableMode.Fixed })
                menu.Items.Add(new MenuItem { Header = "Vyplnit tabulku (Alt+klik, F2)", Command = new EDControls.EDViewModel.RelayCommand(() => TryEnterTableCells(el)) });

            menu.Items.Add(new Separator());
            menu.Items.Add(new MenuItem
            {
                Header = "Vlastnosti prvku…",
                Command = new EDControls.EDViewModel.RelayCommand(() =>
                {
                    // U2: otevřít dialog (jako dvojklik a „Vlastnosti sekce…") - dřív jen přepínalo
                    // inline panel, což u stejně pojmenované akce se sekcí bylo nekonzistentní.
                    _vm.State.SelectedElements.Clear();
                    _vm.State.SelectedElements.Add(el);
                    UpdatePropertiesPanel();
                    InvalidateCanvas();
                    OpenPropertiesForElement(el);
                })
            });
        }
        else
        {
            // Obecné menu - vložit, vybrat vše
            menu.Items.Add(new MenuItem { Header = "Vložit (Ctrl+V)", Command = _vm.PasteCommand });
            menu.Items.Add(new MenuItem
            {
                Header = "Vybrat vše (Ctrl+A)",
                Command = new EDControls.EDViewModel.RelayCommand(() => { _vm.SelectAll(); InvalidateCanvas(); })
            });
        }

        // Sekční položky - pokud klik je na záhlaví nebo pozadí sekce
        var section = _canvas.HitTestSectionHeader(cx, cy, cw)
                   ?? _canvas.HitTestSection(cx, cy, cw);
        if (section is not null)
        {
            _vm.State.ActiveSection = section;
            if (menu.Items.Count > 0) menu.Items.Add(new Separator());

            menu.Items.Add(new MenuItem
            {
                Header = "Vlastnosti sekce…",
                Command = new EDControls.EDViewModel.RelayCommand(() =>
                {
                    var sectionVm = _vm.Sections.FirstOrDefault(s => s.Section == section);
                    sectionVm?.ShowPropertiesCommand.Execute(null);
                })
            });
            // Akce, ne nastavení - proto v místní nabídce a ne ve formuláři sekce (rozhodnutí
            // Jan 3. 9.). Zbývající místo se nedá spočítat z hlavy, takže tohle je jediná cesta,
            // jak sekci roztáhnout bez počítání výšek ostatních sekcí.
            menu.Items.Add(new MenuItem
            {
                Header = "Vyplnit zbytek stránky",
                Command = new EDControls.EDViewModel.RelayCommand(
                    () => { _vm.VyplnitZbytekStranky(section); InvalidateCanvas(); },
                    () => !Models.Layout.SectionLayoutMath.VyplnujeCeleMisto(_vm.State.CurrentTemplate, section))
            });
            menu.Items.Add(new MenuItem
            {
                Header = "Přesunout nahoru",
                Command = new EDControls.EDViewModel.RelayCommand(
                    () => _vm.MoveSection(section, -1),
                    () => _vm.State.CurrentTemplate.Sections.IndexOf(section) > 0)
            });
            menu.Items.Add(new MenuItem
            {
                Header = "Přesunout dolů",
                Command = new EDControls.EDViewModel.RelayCommand(
                    () => _vm.MoveSection(section, +1),
                    () => _vm.State.CurrentTemplate.Sections.IndexOf(section) < _vm.State.CurrentTemplate.Sections.Count - 1)
            });
            menu.Items.Add(new Separator());
            menu.Items.Add(new MenuItem
            {
                Header = "Smazat sekci",
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x8B, 0, 0)),
                Command = new EDControls.EDViewModel.RelayCommand(() => { _vm.DeleteSection(section); InvalidateCanvas(); })
            });
            InvalidateCanvas();
        }

        menu.IsOpen = true;
    }

    private void EndAddingElement()
    {
        if (_vm is null || _canvas is null)
        {
            CancelAddingElement();
            return;
        }

        // ── Field drag: append do existujícího TextElementu ──────────────────
        if (_addingFieldName is not null && _addingTargetElement is TextElement textEl)
        {
            _vm.AppendPlaceholderToElement(textEl, _addingFieldName);
            FinishAddingElement();
            return;
        }

        // ── Potřebujeme sekci (pro vytvoření nového elementu) ────────────────
        if (_addingTargetSection is null || (_addingElementType is null && _addingFieldName is null))
        {
            // U3: prostý klik na paletu (bez tažení na sekci) → vlož element do aktivní sekce
            // na výchozí pozici. Bez tohoto by klik jen zrušil přidávání (drag-only = neobjevitelné).
            if (_addingElementType is not null && _vm.State.ActiveSection is not null)
            {
                _vm.AddElementAtPosition(_vm.State.ActiveSection, 5f, 5f, _addingElementType, null);
                FinishAddingElement();
                return;
            }
            CancelAddingElement();
            return;
        }

        float cw = (float)skCanvas.ActualWidth;
        var origin = _canvas.GetSectionOrigins(cw)
            .FirstOrDefault(s => s.Section == _addingTargetSection);
        if (origin.Section is null) { CancelAddingElement(); return; }

        // Přepočet px → mm relativně k sekci
        float mmX = (_addingGhostPos.X - origin.OriginX) / _canvas.MmToPx;
        float mmY = (_addingGhostPos.Y - origin.OriginY) / _canvas.MmToPx;

        _vm.AddElementAtPosition(_addingTargetSection, mmX, mmY, _addingElementType, _addingFieldName);
        FinishAddingElement();
    }

    /// <summary>Společný cleanup po úspěšném dropu (palette i field).</summary>
    private void FinishAddingElement()
    {
        ResetAddingElementState();
        UpdatePropertiesPanel();
        InvalidateCanvas();
    }

    private void EndRubberBand()
    {
        if (_rubberBandRect.HasValue && _vm is not null && _canvas is not null)
        {
            float cw = (float)skCanvas.ActualWidth;
            var elements = _canvas.HitTestRect(_rubberBandRect.Value, cw);
            if (_vm.State.ActiveSection is not null)
                elements = elements.Where(el => _vm.State.ActiveSection.Elements.Contains(el)).ToList();
            if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                _vm.State.SelectedElements.Clear();
            foreach (var el in elements)
                if (!_vm.State.SelectedElements.Contains(el))
                    _vm.State.SelectedElements.Add(el);
        }
        _rubberBandRect = null;
        _mouseMode      = MouseMode.Idle;
        skCanvas.ReleaseMouseCapture();
        UpdatePropertiesPanel();
        InvalidateCanvas();
    }

    private void EndDrag(bool commit)
    {
        // Bez pohybu (čistý klik) zachovat init pozice - ne commit, ne soft-snap.
        // Bez tohoto by klik na element s GridSize=5mm „strhl" jeho přesnou 0.1mm pozici na 5mm grid.
        if (commit && !_dragHasMoved) commit = false;
        if (commit && _vm is not null)
        {
            // Při vypnutém grid snapu jen očistíme zlomkové mm z draggu (3.247… → 3.2 mm) na osách,
            // kde se neuplatnil smart-snap. Kvantizace na 0.1 mm je nepostřehnutelná, ale drží čisté
            // souřadnice pro zarovnávání klávesami - shodně s EndResize. NEsnapujeme na celý GridSize,
            // jinak by vypnuté tlačítko mřížky reálně grid snapping nevyplo (jen by ho odložilo na puštění).
            bool applySoftX = !_lastSnapActiveX && !_vm.State.SnapToGrid;
            bool applySoftY = !_lastSnapActiveY && !_vm.State.SnapToGrid;
            if ((applySoftX || applySoftY) && _dragInitPositions.Count > 0)
            {
                foreach (var el in _dragInitPositions.Keys)
                {
                    if (applySoftX) el.X = MmMath.Round(el.X);
                    if (applySoftY) el.Y = MmMath.Round(el.Y);
                }
            }

            var moves = new List<IDesignerCommand>();
            foreach (var kvp in _dragInitPositions)
                if (kvp.Key.X != kvp.Value.X || kvp.Key.Y != kvp.Value.Y)
                    moves.Add(new MoveElementCommand(kvp.Key, kvp.Value.X, kvp.Value.Y, kvp.Key.X, kvp.Key.Y));
            if (moves.Count == 1) _vm.Execute(moves[0]);
            else if (moves.Count > 1) _vm.Execute(new CompositeCommand("Přesun prvků", moves));
        }
        else
        {
            foreach (var kvp in _dragInitPositions)
            { kvp.Key.X = kvp.Value.X; kvp.Key.Y = kvp.Value.Y; }
        }
        _dragInitPositions.Clear();
        _vm?.State.ActiveGapIndicators.Clear();
        _vm?.State.SnapTargetElements.Clear();
        if (_vm is not null) _vm.State.DragBadgeText = null;
        _mouseMode   = MouseMode.Idle;
        _dragElement = null;
        skCanvas.ReleaseMouseCapture();
        skCanvas.Cursor = Cursors.Arrow;
        InvalidateCanvas();
        UpdatePropertiesPanel();
    }

    /// <summary>
    /// Dokončí tažení spodní hrany sekce. Do kroku zpět jde **jedna** změna výšky, ne každý pohyb
    /// myší - proto se během tažení hodnota jen zapisuje a příkaz vzniká teprve tady.
    /// </summary>
    private void EndSectionResize(bool commit)
    {
        var section = _sectionResizeTarget;
        if (section is not null && _vm is not null)
        {
            float konecna = MmMath.Round(section.DesignerHeight ?? _sectionResizeStartH);

            if (commit && MathF.Abs(konecna - _sectionResizeStartH) >= 0.05f)
            {
                // Hodnota už je v modelu z průběhu tažení; příkaz ji jen zapíše znovu, aby
                // krok zpět vrátil původní výšku.
                section.DesignerHeight = _sectionResizeStartH;
                _vm.Execute(new SetPropertyCommand("výška sekce", _sectionResizeStartH, konecna,
                    v => section.DesignerHeight = (float?)v));
            }
            else
            {
                section.DesignerHeight = commit ? konecna : _sectionResizeStartH;
            }
        }

        _sectionResizeTarget = null;
        _mouseMode = MouseMode.Idle;
        if (skCanvas.IsMouseCaptured) skCanvas.ReleaseMouseCapture();
        skCanvas.Cursor = System.Windows.Input.Cursors.Arrow;
        UpdateCanvasSize();
        InvalidateCanvas();
        UpdatePropertiesPanel();
    }

    private void EndResize(bool commit)
    {
        if (_resizeElement is not null && _vm is not null)
        {
            if (commit)
            {
                // Zaokrouhlit na 0.1mm před commitem - eliminuje float artefakty z dragu
                _resizeElement.X      = MmMath.Round(_resizeElement.X);
                _resizeElement.Y      = MmMath.Round(_resizeElement.Y);
                _resizeElement.Width  = MmMath.Round(_resizeElement.Width);
                _resizeElement.Height = MmMath.Round(_resizeElement.Height);

                float? finalH = _resizeElement.Height;
                if (_resizeElement.X != _resizeInitX || _resizeElement.Y != _resizeInitY ||
                    _resizeElement.Width != _resizeInitW || finalH != _resizeInitH)
                {
                    _vm.Execute(new ResizeElementCommand(
                        _resizeElement,
                        _resizeInitX, _resizeInitY, _resizeInitW, _resizeInitH,
                        _resizeElement.X, _resizeElement.Y, _resizeElement.Width, finalH));
                }
            }
            else
            {
                _resizeElement.X = _resizeInitX; _resizeElement.Y = _resizeInitY;
                _resizeElement.Width = _resizeInitW; _resizeElement.Height = _resizeInitH;
            }
        }
        _vm?.State.ActiveGapIndicators.Clear();
        _vm?.State.SnapTargetElements.Clear();
        if (_vm is not null) _vm.State.DragBadgeText = null;
        _mouseMode      = MouseMode.Idle;
        _resizeElement  = null;
        skCanvas.ReleaseMouseCapture();
        skCanvas.Cursor = Cursors.Arrow;
        InvalidateCanvas();
        UpdatePropertiesPanel();
    }

    private (float newX, float newY, float newW, float? newH) ComputeResize(float mouseMmX, float mouseMmY)
    {
        float dx = mouseMmX - _resizeMouseStartX;
        float dy = mouseMmY - _resizeMouseStartY;

        const float minW = 3f, minH = 2f;
        float initH  = _resizeInitHComputed;
        float right  = _resizeInitX + _resizeInitW;
        float bottom = _resizeInitY + initH;

        float newX = _resizeInitX, newY = _resizeInitY, newW = _resizeInitW;
        float? newH = _resizeInitH;
        bool setsH = false;

        switch (_resizeHandle)
        {
            case ResizeHandle.TopLeft:
                newX = Math.Min(_resizeInitX + dx, right - minW);
                newY = Math.Min(_resizeInitY + dy, bottom - minH);
                newW = right - newX; newH = bottom - newY; setsH = true; break;
            case ResizeHandle.TopCenter:
                newY = Math.Min(_resizeInitY + dy, bottom - minH);
                newH = bottom - newY; setsH = true; break;
            case ResizeHandle.TopRight:
                newW = Math.Max(minW, _resizeInitW + dx);
                newY = Math.Min(_resizeInitY + dy, bottom - minH);
                newH = bottom - newY; setsH = true; break;
            case ResizeHandle.MiddleLeft:
                newX = Math.Min(_resizeInitX + dx, right - minW);
                newW = right - newX; break;
            case ResizeHandle.MiddleRight:
                newW = Math.Max(minW, _resizeInitW + dx); break;
            case ResizeHandle.BottomLeft:
                newX = Math.Min(_resizeInitX + dx, right - minW);
                newW = right - newX;
                newH = Math.Max(minH, initH + dy); setsH = true; break;
            case ResizeHandle.BottomCenter:
                newH = Math.Max(minH, initH + dy); setsH = true; break;
            case ResizeHandle.BottomRight:
                newW = Math.Max(minW, _resizeInitW + dx);
                newH = Math.Max(minH, initH + dy); setsH = true; break;
        }

        if (!setsH) newH = _resizeInitH;

        // Clamp na hranice sekce
        newX = Math.Max(0f, newX);
        newY = Math.Max(0f, newY);
        newW = Math.Min(newW, _resizeSectionW - newX);
        if (newH.HasValue)
            newH = Math.Min(newH.Value, _resizeSectionH - newY);
        newW = Math.Max(minW, newW);
        if (newH.HasValue)
            newH = Math.Max(minH, newH.Value);

        return (newX, newY, newW, newH);
    }

    private static Cursor GetResizeCursor(ResizeHandle handle) => handle switch
    {
        ResizeHandle.TopLeft     or ResizeHandle.BottomRight  => Cursors.SizeNWSE,
        ResizeHandle.TopRight    or ResizeHandle.BottomLeft   => Cursors.SizeNESW,
        ResizeHandle.TopCenter   or ResizeHandle.BottomCenter => Cursors.SizeNS,
        ResizeHandle.MiddleLeft  or ResizeHandle.MiddleRight  => Cursors.SizeWE,
        _ => Cursors.Arrow
    };

    private (float deltaX, float deltaY) ClampDragDelta(float rawDeltaX, float rawDeltaY)
    {
        if (_vm is null) return (rawDeltaX, rawDeltaY);
        float sectionW = _vm.State.CurrentTemplate.PageSize.PrintableWidth;

        float minDeltaX = float.MinValue, maxDeltaX = float.MaxValue;
        float minDeltaY = float.MinValue, maxDeltaY = float.MaxValue;

        foreach (var kvp in _dragInitPositions)
        {
            var el      = kvp.Key;
            float initX = kvp.Value.X, initY = kvp.Value.Y;
            float elH   = el.EffectiveDesignerHeight;
            var section = _vm.State.CurrentTemplate.Sections.FirstOrDefault(s => s.Elements.Contains(el));
            float sectionH = section?.EffectiveDesignerHeight ?? float.MaxValue;

            minDeltaX = Math.Max(minDeltaX, -initX);
            maxDeltaX = Math.Min(maxDeltaX, sectionW - el.Width - initX);
            minDeltaY = Math.Max(minDeltaY, -initY);
            maxDeltaY = Math.Min(maxDeltaY, sectionH - elH - initY);
        }

        float clampedX = minDeltaX <= maxDeltaX ? Math.Clamp(rawDeltaX, minDeltaX, maxDeltaX) : 0f;
        float clampedY = minDeltaY <= maxDeltaY ? Math.Clamp(rawDeltaY, minDeltaY, maxDeltaY) : 0f;
        return (clampedX, clampedY);
    }
}
