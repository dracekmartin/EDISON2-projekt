namespace TemplateDesigner.Models.Layout;

/// <summary>
/// Interface pro měření výšky dynamických elementů (Height=null).
/// Odděluje výpočet výšky od SkiaSharp - engine je čistá logika.
/// </summary>
public interface IElementHeightCalculator
{
    /// <summary>
    /// Vrátí výšku elementu v mm. Pro statické elementy (Height != null) vrátí Height.
    /// Pro dynamické elementy vypočítá z obsahu (text, tabulka).
    /// </summary>
    float CalculateHeight(TemplateElement element, float availableWidthMm, DataContext? data);
}
