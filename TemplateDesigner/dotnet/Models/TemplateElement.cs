using System.Text.Json.Serialization;
using SkiaSharp;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextElement),      "TextElement")]
[JsonDerivedType(typeof(RectangleElement), "RectangleElement")]
[JsonDerivedType(typeof(LineElement),      "LineElement")]
[JsonDerivedType(typeof(ImageElement),     "ImageElement")]
[JsonDerivedType(typeof(TableElement),     "TableElement")]
[JsonDerivedType(typeof(BarcodeElement),  "BarcodeElement")]
[JsonDerivedType(typeof(StopListElement),      "StopListElement")]
[JsonDerivedType(typeof(DepartureGridElement), "DepartureGridElement")]
[JsonDerivedType(typeof(NotesLegendElement),   "NotesLegendElement")]
public abstract class TemplateElement
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Pozice a rozměry v mm, relativně k levému hornímu rohu sekce
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }

    /// <summary>
    /// Výška v mm. Null u dynamických elementů (TableElement) —
    /// výšku vypočítá print engine za běhu, designer zobrazí placeholder.
    /// </summary>
    public float? Height { get; set; }

    public int ZIndex { get; set; }
    public bool IsLocked { get; set; }

    /// <summary>
    /// Výraz pro podmíněnou viditelnost, např. "{POCET} > 0".
    /// Null nebo prázdný = vždy viditelný. Vyhodnocuje se v print enginu.
    /// </summary>
    public string? VisibilityExpression { get; set; }

    /// <summary>
    /// Id skupiny. Elementy se stejným GroupId se chovají jako celek —
    /// kliknutí vybere celou skupinu, drag přesune všechny členy.
    /// Null = element nepatří do žádné skupiny.
    /// </summary>
    public string? GroupId { get; set; }

    /// <summary>Bag pro budoucí vlastnosti bez zásahu do modelu.</summary>
    public Dictionary<string, string>? CustomProperties { get; set; }

    // ── Vizuální obal (společný pro všechny elementy) ──────────────────────────

    /// <summary>Barva pozadí elementu (#AARRGGBB). "#00FFFFFF" = průhledné.</summary>
    public string BackgroundColor { get; set; } = "#00FFFFFF";

    /// <summary>Barva rámečku ve formátu #AARRGGBB. "#00FFFFFF" = žádný rámeček.</summary>
    public string BorderColor { get; set; } = "#00FFFFFF";

    /// <summary>Šířka rámečku v mm. 0 = žádný rámeček.</summary>
    public float BorderWidth { get; set; } = 0f;

    [JsonIgnore]
    public SKColor SkBackgroundColor => SKColor.Parse(BackgroundColor);

    [JsonIgnore]
    public SKColor SkBorderColor => SKColor.Parse(BorderColor);

    /// <summary>
    /// Vykreslí rámeček elementu. Volá se ve DRUHÉM průchodu renderování (po obsahu
    /// všech elementů), takže žádný fill ho nemůže překrýt. Stroke je centrovaný
    /// na hraně — sousední elementy sdílejí stejný pixel a rámeček vypadá jednoduše.
    /// </summary>
    protected void RenderBorder(RenderContext ctx, SKRect rect)
    {
        if (BorderWidth <= 0 || SkBorderColor.Alpha == 0) return;
        float strokePx = MathF.Max(1f, MathF.Round(BorderWidth * ctx.MmToPx));

        // Snap hrany na celé pixely — sousední elementy pak sdílejí stejný pixel
        var snappedRect = new SKRect(
            MathF.Round(rect.Left),
            MathF.Round(rect.Top),
            MathF.Round(rect.Right),
            MathF.Round(rect.Bottom));

        using var borderPaint = new SKPaint
        {
            Color = SkBorderColor, Style = SKPaintStyle.Stroke,
            StrokeWidth = strokePx, IsAntialias = false
        };
        ctx.Canvas.DrawRect(snappedRect, borderPaint);
    }

    /// <summary>
    /// Veřejná metoda pro druhý průchod renderování — vykreslí rámeček elementu.
    /// Canvas volá tuto metodu po tom, co se vykreslí obsah všech elementů.
    /// </summary>
    public virtual void RenderBorderPass(RenderContext ctx)
    {
        RenderBorder(ctx, ctx.ToCanvasRect(X, Y, Width, EffectiveDesignerHeight));
    }

    // ---------------------------------------------------------------------------

    /// <summary>
    /// Vykreslí element na canvas. Volá designer, preview i print engine.
    /// Souřadnice v ctx jsou přepočítány do px — element pracuje s vlastními mm hodnotami.
    /// </summary>
    public abstract void Render(RenderContext ctx);

    /// <summary>
    /// Vytvoří hlubokou kopii elementu s novým Id. Používá se při copy/paste.
    /// </summary>
    public abstract TemplateElement Clone();

    /// <summary>
    /// Vrátí seznam deskriptorů vlastností pro panel vlastností.
    /// Panel generuje UI řádky automaticky — přidání nové vlastnosti = přidat deskriptor sem.
    /// </summary>
    public abstract IReadOnlyList<PropertyDescriptor> GetPropertyDescriptors();

    /// <summary>
    /// Helper — vrátí deskriptory společných vlastností pozice a ZIndex.
    /// Volej na konci GetPropertyDescriptors() pomocí spread operátoru: [.. GetPositionDescriptors()]
    /// </summary>
    protected IReadOnlyList<PropertyDescriptor> GetPositionDescriptors() =>
    [
        new FloatPropertyDescriptor { Label = "X",       Group = "Pozice", Get = () => X,       Set = v => X = v,      Min = 0,  Max = 9999, Unit = "mm" },
        new FloatPropertyDescriptor { Label = "Y",       Group = "Pozice", Get = () => Y,       Set = v => Y = v,      Min = 0,  Max = 9999, Unit = "mm" },
        new FloatPropertyDescriptor { Label = "Šířka",   Group = "Pozice", Get = () => Width,   Set = v => Width = v,  Min = 1,  Max = 9999, Unit = "mm" },
        .. (IsDynamic
            ? new PropertyDescriptor[] { new StringPropertyDescriptor { Label = "Výška", Group = "Pozice",
                Get = () => "(dynamická)", Set = _ => { } } }
            : new PropertyDescriptor[] { new FloatPropertyDescriptor { Label = "Výška", Group = "Pozice",
                Get = () => Height ?? EffectiveDesignerHeight, Set = v => Height = v,
                Min = 1, Max = 9999, Unit = "mm" } }),
        new FloatPropertyDescriptor { Label = "ZIndex",  Group = "Pozice", Get = () => ZIndex,  Set = v => ZIndex = (int)Math.Round(v), Min = 0, Max = 100, Format = "0" },
        new BoolPropertyDescriptor  { Label = "Zamčeno", Group = "Pozice", Get = () => IsLocked, Set = v => IsLocked = v },

        new ColorPropertyDescriptor { Label = "Barva rámečku", Group = "Rámeček", Get = () => BorderColor, Set = v => BorderColor = v },
        new FloatPropertyDescriptor { Label = "Šířka rámečku", Group = "Rámeček", Get = () => BorderWidth, Set = v => BorderWidth = v, Min = 0, Max = 5, Unit = "mm", Format = "0.0#" },

        new StringPropertyDescriptor
        {
            Label = "Viditelnost", Group = "Podmínky",
            // null / prázdný = vždy viditelný
            // Výrazy: "{TISK_SIPKY}", "{POCET} > 0", "{TYP} == \"JR\" && !{TISK_TARIFU}"
            Get = () => VisibilityExpression ?? "",
            Set = v  => VisibilityExpression = string.IsNullOrWhiteSpace(v) ? null : v.Trim(),
            Validate = Expressions.ExpressionEvaluator.Validate
        },

        // GroupId — zobrazit jen pokud element patří do skupiny (readonly informace)
        .. (GroupId is not null
            ? [new StringPropertyDescriptor
            {
                Label = "Skupina", Group = "Skupina",
                Get = () => GroupId ?? "",
                Set = _ => { }  // readonly — skupiny se mění přes Ctrl+G / Ctrl+Shift+G
            }]
            : Array.Empty<PropertyDescriptor>()),
    ];

    // ── Scale ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Proporcionálně přeškáluje pozici, rozměry a vizuální properties elementu.
    /// Každý element overridne a přidá vlastní mm/pt properties.
    /// </summary>
    public virtual void ApplyScale(float scale)
    {
        X *= scale; Y *= scale; Width *= scale;
        if (Height is not null) Height *= scale;
        if (BorderWidth > 0) BorderWidth *= scale;
    }

    // ── Clone helper ──────────────────────────────────────────────────────────

    /// <summary>
    /// Zkopíruje společné base properties do cílového elementu (nové Id, pozice, border, background, ...).
    /// Volej v Clone() místo manuálního kopírování 10+ řádků.
    /// </summary>
    protected void CopyBaseTo(TemplateElement target)
    {
        target.Id = Guid.NewGuid();
        target.X = X; target.Y = Y; target.Width = Width; target.Height = Height;
        target.ZIndex = ZIndex; target.IsLocked = IsLocked;
        target.VisibilityExpression = VisibilityExpression;
        target.CustomProperties = CustomProperties is null ? null : new(CustomProperties);
        target.GroupId = GroupId;
        target.BackgroundColor = BackgroundColor;
        target.BorderColor = BorderColor; target.BorderWidth = BorderWidth;
    }

    // ---------------------------------------------------------------------------

    [JsonIgnore]
    public bool IsDynamic => !Height.HasValue;

    /// <summary>Výška pro zobrazení v designeru — Height nebo placeholder dynamické sekce.</summary>
    [JsonIgnore]
    public float EffectiveDesignerHeight => Height ?? DesignerPlaceholderHeight;

    protected virtual float DesignerPlaceholderHeight => 20f;

    // ---------------------------------------------------------------------------
    // Sdílené utility pro zobrazení v UI (strom, debug)

    /// <summary>Zkrácený popis elementu pro strom šablony a debug výpisy.</summary>
    public static string GetSummary(TemplateElement el) => el switch
    {
        TextElement t      => $"\"{TruncateText(t.Text, 50)}\"  {t.Style.FontFamily ?? "Arial"} {t.Style.FontSize ?? 10f}pt",
        LineElement l      => $"{l.Orientation} {l.StrokeWidth}mm {l.Color}",
        RectangleElement r => $"Fill={r.BackgroundColor} Stroke={r.StrokeColor}",
        ImageElement       => "Image",
        BarcodeElement b   => $"{b.BarcodeType}",
        TableElement t     => $"{t.Mode} cols={t.Columns.Count}",
        StopListElement      => "Seznam zastávek",
        DepartureGridElement d => $"Odjezdy {(d.SkupinaDnuId.Length > 0 ? d.SkupinaDnuId : "auto")}",
        NotesLegendElement   => "Legenda poznámek",
        _                  => $"({el.X:0.#}, {el.Y:0.#}) {el.Width:0.#}x{(el.Height?.ToString("0.#") ?? "auto")}"
    };

    /// <summary>Zkrátí text na maxLen znaků, nahradí odřádkování mezerou.</summary>
    public static string TruncateText(string? text, int maxLen)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var oneLine = text.Replace("\n", " ").Replace("\r", "");
        return oneLine.Length <= maxLen ? oneLine : oneLine[..(maxLen - 3)] + "...";
    }
}
