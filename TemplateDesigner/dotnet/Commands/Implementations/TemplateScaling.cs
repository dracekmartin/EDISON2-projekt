using TemplateDesigner.Models;
using static TemplateDesigner.Models.MmMath;

namespace TemplateDesigner.Commands.Implementations;

/// <summary>
/// Sdílené přepočty geometrie šablony. Používá je ruční škálování
/// (<see cref="ScaleTemplateCommand"/>) i změna formátu stránky
/// (<see cref="ChangePageFormatCommand"/>).
/// </summary>
/// <remarks>
/// Stránka a obsah se škálují <b>zvlášť</b> schválně: ruční škálování zvětšuje i papír, kdežto při
/// změně formátu je cílový rozměr daný dialogem a přepočítat se má jen to, co je na něm.
/// </remarks>
public static class TemplateScaling
{
    /// <summary>Přeškáluje rozměr stránky včetně okrajů.</summary>
    public static void ScalePage(PageSize ps, float s)
    {
        ps.Width  = Round(ps.Width  * s);
        ps.Height = Round(ps.Height * s);
        ps.MarginTop    = Round(ps.MarginTop    * s);
        ps.MarginBottom = Round(ps.MarginBottom * s);
        ps.MarginLeft   = Round(ps.MarginLeft   * s);
        ps.MarginRight  = Round(ps.MarginRight  * s);
    }

    /// <summary>Přeškáluje obsah - sekce a všechny prvky včetně jejich písma a odsazení.</summary>
    public static void ScaleContent(Template template, float s)
    {
        foreach (var section in template.Sections)
        {
            if (section.DesignerHeight is not null)
                section.DesignerHeight = Round(section.DesignerHeight.Value * s);
            if (section.RowHeight is not null)
                section.RowHeight = Round(section.RowHeight.Value * s);
            if (section.BorderWidth > 0)
                section.BorderWidth = Round(section.BorderWidth * s);

            foreach (var element in section.Elements)
                element.ApplyScale(s);
        }
    }

    /// <summary>Posune všechny prvky vodorovně - používá se na vystředění po změně orientace.</summary>
    public static void OffsetElementsX(Template template, float dxMm)
    {
        foreach (var section in template.Sections)
            foreach (var element in section.Elements)
                element.X = Round(element.X + dxMm);
    }
}
