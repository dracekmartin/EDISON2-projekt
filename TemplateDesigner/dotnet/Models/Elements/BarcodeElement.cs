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

    [JsonIgnore] public SKColor SkForeColor => ColorHex.ParseOrBlack(ForeColor);

    private static readonly Dictionary<EncodeHintType, object> _hints = new()
    {
        [EncodeHintType.MARGIN] = 0,
        [EncodeHintType.CHARACTER_SET] = "UTF-8"
    };
    private static readonly MultiFormatWriter _writer = new();
    private static readonly SKPathEffect _dashEffect = SKPathEffect.CreateDash([4f, 3f], 0f);

    public BarcodeElement()
    {
        // Čárový kód potřebuje bílé pozadí (ne průhledné jako ostatní elementy)
        BackgroundColor = "#FFFFFFFF";
    }

    // ─── Cache vykresleného kódu ────────────────────────────────────────────
    // Bez cache se při každém framu znovu zakóduje matice a nakreslí modul po modulu -
    // u velkého QR jsou to stovky tisíc kreslicích příkazů na jedno překreslení plátna.
    // Cachuje se hotová nahrávka kreslení (SKPicture) v lokálních souřadnicích; per frame
    // zbyde jeden DrawPicture s posunem. Klíč = vše, co tvar nebo barvu ovlivňuje.

    [JsonIgnore] private SKPicture? _cachedPicture;
    [JsonIgnore] private string? _cachedContent;
    [JsonIgnore] private int _cachedWidthPx;
    [JsonIgnore] private int _cachedHeightPx;
    [JsonIgnore] private BarcodeElementType _cachedType;
    [JsonIgnore] private SKColor _cachedForeColor;
    [JsonIgnore] private bool _cacheFilled;

    /// <summary>Počet skutečných zakódování - testovací sonda, že cache drží.</summary>
    [JsonIgnore] internal int EncodeCount { get; private set; }

    public override void Render(RenderContext ctx)
    {
        if (Width <= 0) return;

        var rect = ctx.ToCanvasRect(X, Y, Width, Height ?? EffectiveDesignerHeight);
        var content = ctx.Data?.ResolvePlaceholders(Content) ?? Content;

        // Pozadí elementu kreslí base RenderBackgroundPass (respektuje rohy) před obsahem.

        if (string.IsNullOrWhiteSpace(content))
        {
            DrawPlaceholder(ctx.Canvas, rect);
            return;
        }

        var picture = GetBarcodePicture(content, rect.Width, rect.Height);
        if (picture is null)
        {
            DrawPlaceholder(ctx.Canvas, rect);
            return;
        }

        // Nahrávka je v lokálních souřadnicích (0,0) - posun na místo elementu dělá matice.
        ctx.Canvas.Save();
        ctx.Canvas.Translate(rect.Left, rect.Top);
        ctx.Canvas.DrawPicture(picture);
        ctx.Canvas.Restore();
    }

    /// <summary>
    /// Nahrávka kreslení kódu, nebo null když se obsah nedá zakódovat (např. EAN-13 s jiným
    /// počtem číslic). Neúspěch se cachuje taky - jinak by ZXing při každém framu vyhazoval výjimku.
    /// </summary>
    private SKPicture? GetBarcodePicture(string content, float rectW, float rectH)
    {
        int widthPx  = Math.Max(1, (int)rectW);
        int heightPx = Math.Max(1, (int)rectH);
        var foreColor = SkForeColor;

        if (_cacheFilled
            && _cachedContent == content
            && _cachedWidthPx == widthPx && _cachedHeightPx == heightPx
            && _cachedType == BarcodeType && _cachedForeColor == foreColor)
            return _cachedPicture;

        _cachedPicture?.Dispose();
        _cachedPicture   = null;
        _cachedContent   = content;
        _cachedWidthPx   = widthPx;
        _cachedHeightPx  = heightPx;
        _cachedType      = BarcodeType;
        _cachedForeColor = foreColor;
        _cacheFilled     = true;

        EncodeCount++;
        try
        {
            _cachedPicture = RecordBarcode(content, widthPx, heightPx, rectW, rectH, foreColor);
        }
        catch
        {
            _cachedPicture = null;
        }
        return _cachedPicture;
    }

    private SKPicture RecordBarcode(string content, int widthPx, int heightPx,
        float rectW, float rectH, SKColor foreColor)
    {
        var format = BarcodeType switch
        {
            BarcodeElementType.QR         => BarcodeFormat.QR_CODE,
            BarcodeElementType.Code128    => BarcodeFormat.CODE_128,
            BarcodeElementType.Ean13      => BarcodeFormat.EAN_13,
            BarcodeElementType.Ean8       => BarcodeFormat.EAN_8,
            BarcodeElementType.Pdf417     => BarcodeFormat.PDF_417,
            BarcodeElementType.DataMatrix => BarcodeFormat.DATA_MATRIX,
            _ => throw new ArgumentOutOfRangeException(nameof(BarcodeType), BarcodeType, null),
        };
        BitMatrix matrix = _writer.encode(content, format, widthPx, heightPx, _hints);

        float cellW = rectW / matrix.Width;
        float cellH = rectH / matrix.Height;

        using var recorder = new SKPictureRecorder();
        // +1 v ohraničení nahrávky: krajní modul přesahuje o +0.5 (viz níže) a nesmí se odříznout.
        var canvas = recorder.BeginRecording(new SKRect(0, 0, rectW + 1f, rectH + 1f));

        using (var fgPaint = new SKPaint { Color = foreColor, Style = SKPaintStyle.Fill })
        {
            // ZXing vrací matici v pixelech cílové velikosti, ne v modulech kódu - jeden modul je
            // tak desítky sousedních buněk. Souvislý úsek se kreslí jako jeden obdélník: buňky se
            // kvůli +0.5 překrývají, takže sjednocení úseku je přesně tentýž obrazec (bez vyhlazování
            // rozhoduje jen to, jestli střed pixelu leží uvnitř) - u velkého QR ubude řádově práce.
            for (int row = 0; row < matrix.Height; row++)
            {
                int col = 0;
                while (col < matrix.Width)
                {
                    if (!matrix[col, row]) { col++; continue; }

                    int start = col;
                    while (col < matrix.Width && matrix[col, row]) col++;

                    canvas.DrawRect(
                        start * cellW,
                        row * cellH,
                        (col - start) * cellW + 0.5f,   // +0.5 zamezí mezerám při subpixelovém zarovnání
                        cellH + 0.5f,
                        fgPaint);
                }
            }
        }

        return recorder.EndRecording();
    }

    private void DrawPlaceholder(SKCanvas canvas, SKRect rect)
    {
        using var borderPaint = new SKPaint
        {
            Color = SKColors.LightGray,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            PathEffect = _dashEffect
        };
        canvas.DrawRect(rect, borderPaint);

        var label = BarcodeType switch
        {
            BarcodeElementType.QR         => "QR",
            BarcodeElementType.Code128    => "CODE 128",
            BarcodeElementType.Ean13      => "EAN-13",
            BarcodeElementType.Ean8       => "EAN-8",
            BarcodeElementType.Pdf417     => "PDF417",
            BarcodeElementType.DataMatrix => "DataMatrix",
            _                             => BarcodeType.ToString(),
        };
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
                ("QR kód",     (object)BarcodeElementType.QR),
                ("Code 128",   (object)BarcodeElementType.Code128),
                ("EAN-13",     (object)BarcodeElementType.Ean13),
                ("EAN-8",      (object)BarcodeElementType.Ean8),
                ("PDF417",     (object)BarcodeElementType.Pdf417),
                ("DataMatrix", (object)BarcodeElementType.DataMatrix),
            ]
        },
        new ColorPropertyDescriptor { Label = "Barva kódu",   Group = "Vzhled", Get = () => ForeColor,       Set = v => ForeColor = v },
        new ColorPropertyDescriptor { Label = "Barva pozadí", Group = "Vzhled", Get = () => BackgroundColor, Set = v => BackgroundColor = v },

        .. GetPositionDescriptors()
    ];

    public override TemplateElement Clone()
    {
        var clone = new BarcodeElement
        {
            Content = Content,
            BarcodeType = BarcodeType,
            ForeColor = ForeColor,
        };
        CopyBaseTo(clone);
        return clone;
    }
}

public enum BarcodeElementType { QR, Code128, Ean13, Ean8, Pdf417, DataMatrix }
