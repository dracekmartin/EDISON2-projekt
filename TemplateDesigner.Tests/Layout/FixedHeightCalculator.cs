using TemplateDesigner.Models;
using TemplateDesigner.Models.Layout;

namespace TemplateDesigner.Tests.Layout;

/// <summary>
/// Test double pro IElementHeightCalculator — bez SkiaSharp.
/// Statické elementy (Height != null) vrací svou výšku; dynamické (Height == null)
/// dostanou pevnou hodnotu z konstruktoru (default = EffectiveDesignerHeight).
/// </summary>
public class FixedHeightCalculator(float? dynamicHeight = null) : IElementHeightCalculator
{
    public float CalculateHeight(TemplateElement element, float availableWidthMm, DataContext? data) =>
        element.Height ?? dynamicHeight ?? element.EffectiveDesignerHeight;
}
