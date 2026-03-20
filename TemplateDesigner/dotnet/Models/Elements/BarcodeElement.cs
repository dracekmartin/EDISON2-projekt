using SkiaSharp;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using ZXing;
using ZXing.Common;

namespace TemplateDesigner.Models.Elements;

public class BarcodeElement : TemplateElement
{
    /// <summary>
    /// Obsah kódu. Může obsahovat placeholdery ve tvaru {NAZEV_POLE}.
    /// Print engine je za běhu nahradí reálnými daty.
    /// </summary>
    public string Content { get; set; } = "";

    public BarcodeElementType BarcodeType { get; set; } = BarcodeElementType.QR;

    /// <summary>Barva modulů (popředí) ve formátu #AARRGGBB.</summary>
    public string ForeColor { get; set; } = "#FF000000";

    /// <summary>Barva pozadí ve formátu #AARRGGBB.</summary>
    public string BackColor { get; set; } = "#FFFFFFFF";

    [JsonIgnore] public SKColor SkForeColor => SKColor.Parse(ForeColor);
    [JsonIgnore] public SKColor SkBackColor => SKColor.Parse(BackColor);

    public override void Render(RenderContext ctx)
    {
        if (Width <= 0) return;

        var rect = ctx.ToCanvasRect(X, Y, Width, Height ?? EffectiveDesignerHeight);
        var content = ctx.Data?.ResolvePlaceholders(Content) ?? Content;

        // Pozadí
        if (SkBackColor.Alpha > 0)
        {
            using var bgPaint = new SKPaint { Color = SkBackColor, Style = SKPaintStyle.Fill };
            ctx.Canvas.DrawRect(rect, bgPaint);
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            DrawPlaceholder(ctx.Canvas, rect);
            return;
        }

        try
        {
            RenderBarcode(ctx.Canvas, rect, content);
        }
        catch
        {
            DrawPlaceholder(ctx.Canvas, rect);
        }

    }

    private void RenderBarcode(SKCanvas canvas, SKRect rect, string content)
    {
        int widthPx = Math.Max(1, (int)rect.Width);
        int heightPx = Math.Max(1, (int)rect.Height);

        var format = BarcodeType == BarcodeElementType.QR ? BarcodeFormat.QR_CODE : BarcodeFormat.CODE_128;
        var hints = new Dictionary<EncodeHintType, object>
        {
            [EncodeHintType.MARGIN] = 0,
            [EncodeHintType.CHARACTER_SET] = "UTF-8"
        };

        var writer = new MultiFormatWriter();
        BitMatrix matrix = writer.encode(content, format, widthPx, heightPx, hints);

        float cellW = rect.Width / matrix.Width;
        float cellH = rect.Height / matrix.Height;

        using var fgPaint = new SKPaint { Color = SkForeColor, Style = SKPaintStyle.Fill };

        for (int row = 0; row < matrix.Height; row++)
        {
            for (int col = 0; col < matrix.Width; col++)
            {
                if (matrix[col, row])
                {
                    canvas.DrawRect(
                        rect.Left + col * cellW,
                        rect.Top + row * cellH,
                        cellW + 0.5f,   // +0.5 zamezí mezerám mezi buňkami při subpixelovém zarovnání
                        cellH + 0.5f,
                        fgPaint);
                }
            }
        }
    }

    private void DrawPlaceholder(SKCanvas canvas, SKRect rect)
    {
        using var borderPaint = new SKPaint
        {
            Color = SKColors.LightGray,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            PathEffect = SKPathEffect.CreateDash([4f, 3f], 0f)
        };
        canvas.DrawRect(rect, borderPaint);

        var label = BarcodeType == BarcodeElementType.QR ? "QR" : "CODE128";
        using var textPaint = new SKPaint { Color = SKColors.Gray, IsAntialias = true };
        using var font = new SKFont(SKTypeface.Default, rect.Height * 0.3f);
        float textX = rect.MidX - font.MeasureText(label) / 2f;
        float textY = rect.MidY + rect.Height * 0.1f;
        canvas.DrawText(label, textX, textY, font, textPaint);
    }

    public override IReadOnlyList<PropertyDescriptor> GetPropertyDescriptors() =>
    [
        new StringPropertyDescriptor
        {
            Label = "Obsah", Group = "Kód",
            Get = () => Content, Set = v => Content = v,
            MultiLine = false
        },
        new EnumPropertyDescriptor
        {
            Label = "Typ kódu", Group = "Kód",
            Get = () => BarcodeType, Set = v => BarcodeType = (BarcodeElementType)v,
            Options =
            [
                ("QR kód",   (object)BarcodeElementType.QR),
                ("Code 128", (object)BarcodeElementType.Code128)
            ]
        },
        new ColorPropertyDescriptor { Label = "Barva kódu",   Group = "Vzhled", Get = () => ForeColor, Set = v => ForeColor = v },
        new ColorPropertyDescriptor { Label = "Barva pozadí", Group = "Vzhled", Get = () => BackColor,  Set = v => BackColor  = v },

        .. GetPositionDescriptors()
    ];

    public override TemplateElement Clone() => new BarcodeElement
    {
        Id = Guid.NewGuid(),
        X = X, Y = Y, Width = Width, Height = Height,
        ZIndex = ZIndex, IsLocked = IsLocked,
        VisibilityExpression = VisibilityExpression,
        CustomProperties = CustomProperties is null ? null : new(CustomProperties),
        GroupId = GroupId,
        BorderColor = BorderColor, BorderWidth = BorderWidth,
        Content = Content,
        BarcodeType = BarcodeType,
        ForeColor = ForeColor,
        BackColor = BackColor
    };
}

public enum BarcodeElementType { QR, Code128 }
