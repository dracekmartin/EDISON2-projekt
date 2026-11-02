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

/// <summary>Kreslení plátna a náhledu.</summary>
public partial class MainWindow
{
    // ─────────────────────────────────────────────────────────────────────────
    // Render
    // ─────────────────────────────────────────────────────────────────────────

    private void OnPaintSurface(object? sender, object e)
    {
        if (_canvas is null) return;
        var args    = (dynamic)e;
        SKSurface surface = args.Surface;
        // Předat viditelnou oblast pro viewport culling (grid pak nekreslí mimo viewport).
        _canvas.VisibleViewport = new SKRect(
            (float)CanvasScrollViewer.HorizontalOffset,
            (float)CanvasScrollViewer.VerticalOffset,
            (float)(CanvasScrollViewer.HorizontalOffset + CanvasScrollViewer.ViewportWidth),
            (float)(CanvasScrollViewer.VerticalOffset + CanvasScrollViewer.ViewportHeight));
        _canvas.Render(surface.Canvas, (float)skCanvas.ActualWidth, (float)skCanvas.ActualHeight);

        DrawTableCellOverlay(surface.Canvas, (float)skCanvas.ActualWidth);

        if (_mouseMode == MouseMode.RubberBand && _rubberBandRect.HasValue)
        {
            var r = _rubberBandRect.Value;
            using var fill   = new SKPaint { Color = new SKColor(0x222563EB) };
            using var border = new SKPaint { Color = new SKColor(0xFF2563EB), IsStroke = true, StrokeWidth = 1f };
            surface.Canvas.DrawRect(r, fill);
            surface.Canvas.DrawRect(r, border);
        }

        if (_mouseMode == MouseMode.AddingElement
            && (_addingElementType is not null || _addingFieldName is not null)
            && _vm is not null
            && _addingGhostPos != default)
        {
            float cw = (float)skCanvas.ActualWidth;

            // Field drag na TextElement → zvýraznit cílový element
            if (_addingFieldName is not null && _addingTargetElement is not null)
            {
                var elRect = _canvas.GetElementCanvasRect(_addingTargetElement, cw);
                if (elRect is not null)
                {
                    using var highlight = new SKPaint { Color = new SKColor(0x302563EB) };
                    using var border = new SKPaint
                    {
                        Color = new SKColor(0xFF2563EB), IsStroke = true,
                        StrokeWidth = 2f
                    };
                    surface.Canvas.DrawRect(elRect.Value, highlight);
                    surface.Canvas.DrawRect(elRect.Value, border);
                }
            }
            // Zvýrazni cílovou sekci + ghost element
            else if (_addingTargetSection is not null)
            {
                var origin = _canvas.GetSectionOrigins(cw)
                    .FirstOrDefault(s => s.Section == _addingTargetSection);
                if (origin.Section is not null)
                {
                    float sW = _vm.State.CurrentTemplate.PageSize.PrintableWidth * _canvas.MmToPx;
                    float sH = _addingTargetSection.EffectiveDesignerHeight * _canvas.MmToPx;
                    var sRect = new SKRect(origin.OriginX, origin.OriginY, origin.OriginX + sW, origin.OriginY + sH);
                    using var sectionHighlight = new SKPaint { Color = new SKColor(0x182563EB) };
                    surface.Canvas.DrawRect(sRect, sectionHighlight);

                    // Ghost element - čárkovaný obdélník
                    var (defW, defH) = TemplateDesignerViewModel.GetDefaultElementSize(_addingElementType ?? ElementTypes.Text);
                    float gx = _addingGhostPos.X - (defW * _canvas.MmToPx / 2f);
                    float gy = _addingGhostPos.Y - (defH * _canvas.MmToPx / 2f);
                    var ghostRect = new SKRect(gx, gy, gx + defW * _canvas.MmToPx, gy + defH * _canvas.MmToPx);
                    using var ghostFill = new SKPaint { Color = new SKColor(0x152563EB) };
                    using var ghostBorder = new SKPaint
                    {
                        Color = new SKColor(0xFF2563EB),
                        IsStroke = true,
                        StrokeWidth = 1.5f,
                        PathEffect = SKPathEffect.CreateDash([6f, 4f], 0)
                    };
                    surface.Canvas.DrawRect(ghostRect, ghostFill);
                    surface.Canvas.DrawRect(ghostRect, ghostBorder);
                }
            }
        }
    }

    private void UpdateCanvasSize()
    {
        if (_canvas is null || _vm is null) return;
        const float padding   = 80f;  // 40 top + 40 bottom standardní okraj kolem stránky
        const float topBuffer = 26f;  // = DesignerCanvas.TopBufferPx (nad plátnem je 34px pruh stránek jako nad náhledem)
        float pageW   = _vm.State.CurrentTemplate.PageSize.Width * _canvas.MmToPx;
        // Výška plátna = listy stránek pod sebou (i s případným přesahem obsahu).
        float paperH  = _canvas.TotalPaperHeightPx();
        var dpi = VisualTreeHelper.GetDpi(skCanvas);
        skCanvas.Width  = NaCelePixely(pageW + padding + _canvas.ExtraLeftPx, dpi.DpiScaleX);
        skCanvas.Height = NaCelePixely(paperH + padding + topBuffer, dpi.DpiScaleY);
        skCanvas.InvalidateVisual();
        UpdatePreviewSize();
    }

    private void InvalidateCanvas()
    {
        skCanvas.InvalidateVisual();
        InvalidatePreview();
    }

    private void OnCanvasScrollWheel(object sender, MouseWheelEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;
        e.Handled = true;
        int newIdx = Math.Clamp(CmbZoom.SelectedIndex + (e.Delta > 0 ? 1 : -1), 0, ZoomLevels.Length - 1);
        if (newIdx == CmbZoom.SelectedIndex) return;

        // Zoom ke kurzoru: bod obsahu pod myší má po zoomu zůstat pod myší (standard editorů).
        // Offsety se nastaví až po layout passu (BeginInvoke Loaded) - ScrollViewer jinak clampne
        // na starou velikost plátna.
        var mouse = e.GetPosition(CanvasScrollViewer);
        double contentX = CanvasScrollViewer.HorizontalOffset + mouse.X;
        double contentY = CanvasScrollViewer.VerticalOffset + mouse.Y;
        float oldZoom = ZoomLevels[Math.Clamp(CmbZoom.SelectedIndex, 0, ZoomLevels.Length - 1)];
        float scale = ZoomLevels[newIdx] / oldZoom;

        CmbZoom.SelectedIndex = newIdx;   // SelectionChanged přenastaví State.Zoom + UpdateCanvasSize

        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
        {
            CanvasScrollViewer.ScrollToHorizontalOffset(contentX * scale - mouse.X);
            CanvasScrollViewer.ScrollToVerticalOffset(contentY * scale - mouse.Y);
        });
    }

    private void FitToPage()
    {
        if (_vm is null || _canvas is null) return;
        float viewportW = (float)CanvasScrollViewer.ActualWidth - 20f; // margin pro scrollbar
        float pageW     = _vm.State.CurrentTemplate.PageSize.Width * (96f / 25.4f); // mm → px at zoom=1
        if (pageW <= 0 || viewportW <= 0) return;

        float idealZoom = viewportW / pageW;
        // Najdi nejbližší úroveň zoomu (zaokrouhleno dolů)
        int bestIdx = 0;
        for (int i = ZoomLevels.Length - 1; i >= 0; i--)
        {
            if (ZoomLevels[i] <= idealZoom + 0.01f) { bestIdx = i; break; }
        }
        CmbZoom.SelectedIndex = bestIdx;
    }

    // ── Preview ───────────────────────────────────────────────────────────────

    private float PreviewZoom =>
        CmbPreviewZoom is { SelectedIndex: var idx } && idx >= 0 && idx < ZoomLevels.Length
            ? ZoomLevels[idx]
            : 1.0f;

    private void UpdatePreviewSize()
    {
        if (BtnPreview?.IsChecked != true || _previewCanvas is null || _vm is null) return;
        float zoom = PreviewZoom;
        var dpi = VisualTreeHelper.GetDpi(skPreview);
        skPreview.Width  = NaCelePixely(_previewCanvas.ComputePageWidthPx(zoom), dpi.DpiScaleX);
        skPreview.Height = NaCelePixely(
            _previewCanvas.CalculatePageHeightPx(zoom, NahledData()) + 40f, dpi.DpiScaleY);
    }

    // Data náhledu se skládají jednou za `InvalidatePreview` (změna šablony, dat, strany, parametrů).
    // Dřív se skládala při každém výpočtu velikosti i kreslení - a tabulka spojů, která si přečtená
    // data pamatuje podle objektu, je pak četla znovu a sazba se nedala pamatovat.
    private DataContext? _nahledData;
    private bool _nahledDataPlatna;

    private DataContext? NahledData()
    {
        if (!_nahledDataPlatna)
        {
            _nahledData = _vm!.BuildPreviewMockData();
            _nahledDataPlatna = true;
        }
        return _nahledData;
    }

    // Zoom kolečkem: během otáčení se jen zvětšuje hotový obraz (okamžitě), ostrý náhled se
    // nakreslí až po krátké pauze. Každý krok jinak znamenal sazbu a kreslení všech stran
    // (městská linka se 7 stranami: 0,3-0,4 s na krok, nález Jan 12. 10.).
    private static readonly TimeSpan ZoomPauza = TimeSpan.FromMilliseconds(200);
    private System.Windows.Threading.DispatcherTimer? _zoomCasovac;
    private float? _zoomVykresleny;   // zoom, ve kterém je nakreslený zvětšovaný obraz; null = gesto neběží

    private void DokonciZoomGesto()
    {
        _zoomCasovac?.Stop();
        _zoomVykresleny = null;
        skPreview.LayoutTransform = Transform.Identity;
        PrekresliNahled();
    }

    /// <summary>Rozměr plochy Skia zaokrouhlený nahoru na celý pixel obrazovky.</summary>
    /// <remarks>⚠️ <c>SKElement</c> uřízne bitmapu na celé pixely a WPF ji pak roztáhne zpátky na
    /// necelý rozměr - obraz se převzorkuje a čára, která tím padne mezi pixely, se rozmaže do dvou
    /// řad. Při zvětšení pak jedna čára tabulky vypadala silnější, v PDF ne.</remarks>
    private static double NaCelePixely(double dip, double dpiScale) =>
        Math.Ceiling((dip * dpiScale) - 0.001) / dpiScale;

    private void OnPreviewScrollWheel(object sender, MouseWheelEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;
        e.Handled = true;
        int newIdx = Math.Clamp(CmbPreviewZoom.SelectedIndex + (e.Delta > 0 ? 1 : -1), 0, ZoomLevels.Length - 1);
        if (newIdx == CmbPreviewZoom.SelectedIndex) return;

        // Zoom ke kurzoru (stejně jako plátno): bod obsahu pod myší má po zoomu zůstat pod myší.
        // Offsety se nastaví až po layout passu (BeginInvoke Loaded) - skPreview se přepočítá
        // synchronně v CmbPreviewZoom_SelectionChanged → UpdatePreviewSize, ScrollViewer jinak
        // clampne na starou velikost.
        var mouse = e.GetPosition(PreviewScrollViewer);
        double contentX = PreviewScrollViewer.HorizontalOffset + mouse.X;
        double contentY = PreviewScrollViewer.VerticalOffset + mouse.Y;
        float oldZoom = ZoomLevels[Math.Clamp(CmbPreviewZoom.SelectedIndex, 0, ZoomLevels.Length - 1)];
        float scale = ZoomLevels[newIdx] / oldZoom;

        // Gesto: obraz nakreslený ve výchozím zoomu se jen zvětší, SelectionChanged nekreslí.
        _zoomVykresleny ??= oldZoom;
        CmbPreviewZoom.SelectedIndex = newIdx;
        float pomer = ZoomLevels[newIdx] / _zoomVykresleny.Value;
        skPreview.LayoutTransform = new ScaleTransform(pomer, pomer);
        if (_zoomCasovac is null)
        {
            _zoomCasovac = new System.Windows.Threading.DispatcherTimer { Interval = ZoomPauza };
            _zoomCasovac.Tick += (_, _) => DokonciZoomGesto();
        }
        _zoomCasovac.Stop();
        _zoomCasovac.Start();

        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
        {
            PreviewScrollViewer.ScrollToHorizontalOffset(contentX * scale - mouse.X);
            PreviewScrollViewer.ScrollToVerticalOffset(contentY * scale - mouse.Y);
        });
    }

    private void CmbPreviewZoom_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_vm is null || _previewCanvas is null) return;
        PrekresliNahled();   // zoom data ani šablonu nemění; během gesta kolečkem nekreslí
    }

    /// <summary>„1:1" - skutečná velikost 100 % (designer i náhled). Tlačítko i Ctrl+1.</summary>
    private void BtnResetView_Click(object sender, RoutedEventArgs e) => ResetZoomTo100();

    /// <summary>„Fit" - přizpůsobit designer stránce. Tlačítko i Ctrl+0.</summary>
    private void BtnFitPage_Click(object sender, RoutedEventArgs e) => FitToPage();

    private void ResetZoomTo100()
    {
        const int defaultIdx = 2; // 100 %
        if (CmbZoom is not null)        CmbZoom.SelectedIndex        = defaultIdx;
        if (CmbPreviewZoom is not null) CmbPreviewZoom.SelectedIndex = defaultIdx;
    }

    /// <summary>Šablona, data, strana nebo parametry se změnily - složit data a sazbu znovu a překreslit.</summary>
    private void InvalidatePreview()
    {
        _nahledDataPlatna = false;
        _previewCanvas?.ZapomenSazbu();
        PrekresliNahled();
    }

    /// <summary>Překreslit náhled se stejnými daty i sazbou (zoom, velikost plochy).</summary>
    private void PrekresliNahled()
    {
        if (BtnPreview?.IsChecked != true || _zoomVykresleny is not null) return;   // gesto dokreslí po pauze
        UpdatePreviewSize();
        skPreview.InvalidateVisual();
    }

    private void OnPreviewPaintSurface(object? sender, object e)
    {
        if (_previewCanvas is null || _vm is null) return;
        var args    = (dynamic)e;
        SKSurface surface = args.Surface;
        float w = (float)args.Info.Width;
        float h = (float)args.Info.Height;
        var previewData = NahledData();
        _vm.State.CurrentPreviewData = previewData;
        _previewCanvas.Render(surface.Canvas, w, h, previewData);
    }
}
