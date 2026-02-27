namespace TemplateDesigner.Models.Sections;

/// <summary>
/// Sekce která se vertikálně roztahuje podle obsahu textu.
/// Používá se pro víceřádkové texty (poznámky, popisy) kde výška není předem známá.
/// DesignerHeight je null — dynamická sekce.
/// </summary>
public class SectionInfiniteText : Section
{
    public SectionInfiniteText()
    {
        Name = "Nekonečný text";
        OverflowBehavior = SectionOverflowBehavior.AllowSplit;
    }

    protected override float DefaultPlaceholderHeight => 40f;
}
