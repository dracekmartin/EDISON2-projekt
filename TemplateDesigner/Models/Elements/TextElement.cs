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
        if (Width <= 0) return;
        var resolvedText = ctx.Data?.ResolvePlaceholders(Text) ?? Text;
        if (string.IsNullOrEmpty(resolvedText)) return;

        var rect = ctx.ToCanvasRect(X, Y, Width, Height ?? EffectiveDesignerHeight);

        var typeface = SKTypeface.FromFamilyName(
            FontFamily,
            FontWeight == TextFontWeight.Bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            FontStyle == TextFontStyle.Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright)
            ?? SKTypeface.Default;

        // FontSize je v bodech (pt): 1pt = 0.3528mm → mm → px
        float fontSizePx = ctx.Px(FontSize * 0.3528f);
        using var font = new SKFont(typeface, fontSizePx);
        using var paint = new SKPaint { Color = SkTextColor, IsAntialias = true };

        ctx.Canvas.Save();
        ctx.Canvas.ClipRect(rect);
        ctx.Canvas.DrawText(resolvedText, rect.Left + 1f, rect.Top + fontSizePx, font, paint);
        ctx.Canvas.Restore();
    }

    public override IReadOnlyList<PropertyDescriptor> GetPropertyDescriptors() =>
    [
        new StringPropertyDescriptor { Label = "Text",         Group = "Obsah",     Get = () => Text,                Set = v => Text = v,                                   MultiLine = true },

        new StringPropertyDescriptor { Label = "Rodina písma", Group = "Font",      Get = () => FontFamily,          Set = v => FontFamily = v },
        new FloatPropertyDescriptor  { Label = "Velikost",      Group = "Font",      Get = () => FontSize,            Set = v => FontSize = v,                               Min = 4, Max = 200, Unit = "pt" },
        new EnumPropertyDescriptor   { Label = "Tučnost",       Group = "Font",      Get = () => FontWeight,          Set = v => FontWeight = (TextFontWeight)v,
            Options = [("Normální", (object)TextFontWeight.Normal), ("Tučné", (object)TextFontWeight.Bold)] },
        new EnumPropertyDescriptor   { Label = "Styl",          Group = "Font",      Get = () => FontStyle,           Set = v => FontStyle = (TextFontStyle)v,
            Options = [("Normální", (object)TextFontStyle.Normal), ("Kurzíva", (object)TextFontStyle.Italic)] },
        new BoolPropertyDescriptor   { Label = "Zalamování",    Group = "Font",      Get = () => WordWrap,            Set = v => WordWrap = v },

        new EnumPropertyDescriptor   { Label = "Vodorovně",     Group = "Zarovnání", Get = () => HorizontalAlignment, Set = v => HorizontalAlignment = (TextHorizontalAlignment)v,
            Options = [("Vlevo", (object)TextHorizontalAlignment.Left), ("Na střed", (object)TextHorizontalAlignment.Center),
                       ("Vpravo", (object)TextHorizontalAlignment.Right), ("Do bloku", (object)TextHorizontalAlignment.Justify)] },
        new EnumPropertyDescriptor   { Label = "Svisle",        Group = "Zarovnání", Get = () => VerticalAlignment,   Set = v => VerticalAlignment = (TextVerticalAlignment)v,
            Options = [("Nahoře", (object)TextVerticalAlignment.Top), ("Na střed", (object)TextVerticalAlignment.Middle), ("Dole", (object)TextVerticalAlignment.Bottom)] },

        new ColorPropertyDescriptor  { Label = "Barva textu",   Group = "Vzhled",    Get = () => TextColor,           Set = v => TextColor = v },

        .. GetPositionDescriptors()
    ];

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
