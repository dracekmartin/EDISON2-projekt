using SkiaSharp;
using System.Text.Json.Serialization;

namespace TemplateDesigner.Models.Elements;

public class TextElement : TemplateElement
{
    /// <summary>
    /// Text pro zobrazení. Může obsahovat placeholdery ve tvaru {NAZEV_POLE}.
    /// Print engine je za běhu nahradí reálnými daty.
    /// </summary>
    public string Text { get; set; } = "";

    public string FontFamily { get; set; } = "Arial";
    public float FontSize { get; set; } = 10f;
    public TextFontStyle FontStyle { get; set; } = TextFontStyle.Normal;
    public TextFontWeight FontWeight { get; set; } = TextFontWeight.Normal;
    public TextHorizontalAlignment HorizontalAlignment { get; set; } = TextHorizontalAlignment.Left;
    public TextVerticalAlignment VerticalAlignment { get; set; } = TextVerticalAlignment.Top;

    /// <summary>Barva textu ve formátu #AARRGGBB, např. "#FF000000" = černá.</summary>
    public string TextColor { get; set; } = "#FF000000";

    public bool WordWrap { get; set; } = true;

    [JsonIgnore]
    public SKColor SkTextColor => SKColor.Parse(TextColor);

    public override void Render(RenderContext ctx)
    {
        // TODO: implementovat renderování textu
    }

    public override TemplateElement Clone() => new TextElement
    {
        Id = Guid.NewGuid(),
        X = X, Y = Y, Width = Width, Height = Height,
        ZIndex = ZIndex, IsLocked = IsLocked,
        VisibilityExpression = VisibilityExpression,
        CustomProperties = CustomProperties is null ? null : new(CustomProperties),
        Text = Text,
        FontFamily = FontFamily,
        FontSize = FontSize,
        FontStyle = FontStyle,
        FontWeight = FontWeight,
        HorizontalAlignment = HorizontalAlignment,
        VerticalAlignment = VerticalAlignment,
        TextColor = TextColor,
        WordWrap = WordWrap
    };
}

public enum TextFontStyle { Normal, Italic }
public enum TextFontWeight { Normal, Bold }
public enum TextHorizontalAlignment { Left, Center, Right, Justify }
public enum TextVerticalAlignment { Top, Middle, Bottom }
