using System.Text.Json.Serialization;
using SkiaSharp;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Models;

/// <summary>Tvar rohů celého elementu (pozadí + rámeček). Ostré = pravý úhel (výchozí).</summary>
public enum ElementCornerStyle { None, Rounded, Beveled }

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextElement),          ElementTypes.Text)]
[JsonDerivedType(typeof(RectangleElement),     ElementTypes.Rectangle)]
[JsonDerivedType(typeof(LineElement),          ElementTypes.Line)]
[JsonDerivedType(typeof(ImageElement),         ElementTypes.Image)]
[JsonDerivedType(typeof(TableElement),         ElementTypes.Table)]
[JsonDerivedType(typeof(DynamicTableElement),  ElementTypes.DynamicTable)]
[JsonDerivedType(typeof(BarcodeElement),       ElementTypes.Barcode)]
[JsonDerivedType(typeof(StopListElement),      ElementTypes.StopList)]
[JsonDerivedType(typeof(DepartureGridElement), ElementTypes.DepartureGrid)]
[JsonDerivedType(typeof(NotesLegendElement),   ElementTypes.NotesLegend)]
[JsonDerivedType(typeof(LineTableElement),     ElementTypes.LineTable)]
public abstract class TemplateElement
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Pozice a rozměry v mm, relativně k levému hornímu rohu sekce
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }

    /// <summary>
    /// Výška v mm. Null u dynamických elementů (TableElement) -
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
    /// Id skupiny. Elementy se stejným GroupId se chovají jako celek -
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

    /// <summary>Tvar rohů celého elementu (pozadí + rámeček). Výchozí Ostré (pravý úhel).</summary>
    public ElementCornerStyle CornerStyle { get; set; } = ElementCornerStyle.None;

    /// <summary>Velikost rohu v mm - poloměr zaoblení / délka zkosení. Platí jen když <see cref="CornerStyle"/> ≠ Ostré.</summary>
    public float CornerSize { get; set; } = 0f;

    /// <summary>Rotace elementu ve stupních (po směru hodinových ručiček). 0 = žádná rotace.</summary>
    public float Rotation { get; set; } = 0f;

    [JsonIgnore]
    public bool IsRotated => MathF.Abs(Rotation) > 0.001f;

    [JsonIgnore]
    public SKColor SkBackgroundColor => ColorHex.ParseOrTransparent(BackgroundColor);

    [JsonIgnore]
    public SKColor SkBorderColor => ColorHex.ParseOrBlack(BorderColor);

    /// <summary>
    /// Vykreslí rámeček elementu. Volá se ve DRUHÉM průchodu renderování (po obsahu
    /// všech elementů), takže žádný fill ho nemůže překrýt. Stroke je centrovaný
    /// na hraně - sousední elementy sdílejí stejný pixel a rámeček vypadá jednoduše.
    /// </summary>
    protected void RenderBorder(RenderContext ctx, SKRect rect)
    {
        if (BorderWidth <= 0 || SkBorderColor.Alpha == 0) return;

        float widthPx = BorderWidth * ctx.MmToPx;
        bool corners = CornerStyle != ElementCornerStyle.None && CornerSize > 0;

        // Sub-pixel rámečky (tenčí než 1px): fractional stroke + antialiasing.
        // Nad 1px: pixel-snap pro čisté sdílení hran mezi sousedními elementy.
        // U zaoblených/zkosených rohů pixel-snap vynecháváme (kazil by křivku) → AA.
        bool thin = widthPx < 1f;
        float strokePx = thin ? widthPx : MathF.Round(widthPx);
        var drawRect = (thin || corners)
            ? rect
            : new SKRect(MathF.Round(rect.Left), MathF.Round(rect.Top),
                         MathF.Round(rect.Right), MathF.Round(rect.Bottom));

        using var borderPaint = new SKPaint
        {
            Color = SkBorderColor, Style = SKPaintStyle.Stroke,
            StrokeWidth = strokePx, IsAntialias = thin || corners
        };
        DrawElementShape(ctx, drawRect, borderPaint);
    }

    /// <summary>Vykreslí obdélník elementu daným paintem s ohledem na <see cref="CornerStyle"/>
    /// (ostré = DrawRect, zaoblené = DrawRoundRect, zkosené = seříznuté rohy přes SKPath).
    /// Jediné místo tvaru rohů - sdílí ho výplň pozadí i rámeček.</summary>
    protected void DrawElementShape(RenderContext ctx, SKRect rect, SKPaint paint)
    {
        float sizePx = CornerSize * ctx.MmToPx;
        float maxCorner = MathF.Min(rect.Width, rect.Height) / 2f;
        sizePx = Math.Clamp(sizePx, 0f, maxCorner);

        if (CornerStyle == ElementCornerStyle.Rounded && sizePx > 0)
        {
            ctx.Canvas.DrawRoundRect(rect, sizePx, sizePx, paint);
        }
        else if (CornerStyle == ElementCornerStyle.Beveled && sizePx > 0)
        {
            using var path = BuildBevelPath(rect, sizePx);
            ctx.Canvas.DrawPath(path, paint);
        }
        else
        {
            ctx.Canvas.DrawRect(rect, paint);
        }
    }

    /// <summary>Ořízne canvas na tvar elementu dle <see cref="CornerStyle"/> - obsah (výplň, text)
    /// pak nepřeteče mimo zaoblené/zkosené rohy. Volající obklopí Save()/Restore().</summary>
    protected void ClipToElementShape(RenderContext ctx, SKRect rect)
    {
        float sizePx = CornerSize * ctx.MmToPx;
        float maxCorner = MathF.Min(rect.Width, rect.Height) / 2f;
        sizePx = Math.Clamp(sizePx, 0f, maxCorner);

        if (CornerStyle == ElementCornerStyle.Rounded && sizePx > 0)
        {
            ctx.Canvas.ClipRoundRect(new SKRoundRect(rect, sizePx, sizePx), antialias: true);
        }
        else if (CornerStyle == ElementCornerStyle.Beveled && sizePx > 0)
        {
            using var path = BuildBevelPath(rect, sizePx);
            ctx.Canvas.ClipPath(path, antialias: true);
        }
        else
        {
            ctx.Canvas.ClipRect(rect);
        }
    }

    /// <summary>
    /// Výška, do které je prvek <b>právě teď</b> vysoký (mm) - u dynamických roste s daty.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Jedno místo pro rámeček, pozadí, ořez rohů i střed otáčení.</b> Do 25. 8. si každý
    /// z nich bral výšku z plátna, takže u prvku, který narostl s daty, vedl rámeček přes tabulku
    /// a zaoblené rohy ji uřízly na výšku z návrháře (nahlásil Jan). Prvek, který svoji výšku umí
    /// spočítat, tuhle metodu přepíše; ostatní zůstanou na výšce z plátna.
    /// </remarks>
    public virtual float RenderedHeightMm(RenderContext ctx) => Height ?? EffectiveDesignerHeight;

    /// <summary>Rect elementu v canvas px pro ořez obsahu na tvar rohů.</summary>
    public SKRect GetContentClipRect(RenderContext ctx) =>
        ctx.ToCanvasRect(X, Y, Width, RenderedHeightMm(ctx));

    /// <summary>Ořez obsahu elementu na tvar rohů (zaoblené/zkosené) - no-op bez rohů. Volá render loop
    /// kolem obsahu (uvnitř Save/Restore), aby výplně ani obsah nepřetekly mimo zaoblení/zkosení.
    /// Jedno místo pro VŠECHNY elementy - obdoba rámečku (<see cref="RenderBorderPass"/>).</summary>
    public void ApplyCornerClip(RenderContext ctx)
    {
        if (CornerStyle == ElementCornerStyle.None || CornerSize <= 0) return;
        ClipToElementShape(ctx, GetContentClipRect(ctx));
    }

    /// <summary>Osmiúhelníková cesta se seříznutými rohy (délka řezu = <paramref name="c"/> px).</summary>
    private static SKPath BuildBevelPath(SKRect r, float c)
    {
        var p = new SKPath();
        p.MoveTo(r.Left + c, r.Top);
        p.LineTo(r.Right - c, r.Top);
        p.LineTo(r.Right, r.Top + c);
        p.LineTo(r.Right, r.Bottom - c);
        p.LineTo(r.Right - c, r.Bottom);
        p.LineTo(r.Left + c, r.Bottom);
        p.LineTo(r.Left, r.Bottom - c);
        p.LineTo(r.Left, r.Top + c);
        p.Close();
        return p;
    }

    /// <summary>První průchod renderování - výplň pozadí elementu (<see cref="BackgroundColor"/>)
    /// tvarem dle <see cref="CornerStyle"/>. Volá canvas PŘED obsahem, symetricky k
    /// <see cref="RenderBorderPass"/>. Jedno místo pro pozadí celého elementu (jako rámeček).</summary>
    public virtual void RenderBackgroundPass(RenderContext ctx)
    {
        if (SkBackgroundColor.Alpha == 0) return;
        var rect = ctx.ToCanvasRect(X, Y, Width, RenderedHeightMm(ctx));
        bool corners = CornerStyle != ElementCornerStyle.None && CornerSize > 0;
        using var fill = new SKPaint { Color = SkBackgroundColor, IsAntialias = corners };

        if (IsRotated)
        {
            var (cx, cy) = GetCenterPx(ctx);
            ctx.Canvas.Save();
            ctx.Canvas.RotateDegrees(Rotation, cx, cy);
            DrawElementShape(ctx, rect, fill);
            ctx.Canvas.Restore();
        }
        else
        {
            DrawElementShape(ctx, rect, fill);
        }
    }

    /// <summary>
    /// Veřejná metoda pro druhý průchod renderování - vykreslí rámeček elementu.
    /// Canvas volá tuto metodu po tom, co se vykreslí obsah všech elementů.
    /// </summary>
    public virtual void RenderBorderPass(RenderContext ctx)
    {
        var rect = ctx.ToCanvasRect(X, Y, Width, RenderedHeightMm(ctx));
        if (IsRotated)
        {
            var (cx, cy) = GetCenterPx(ctx);
            ctx.Canvas.Save();
            ctx.Canvas.RotateDegrees(Rotation, cx, cy);
            RenderBorder(ctx, rect);
            ctx.Canvas.Restore();
        }
        else
        {
            RenderBorder(ctx, rect);
        }
    }

    /// <summary>Střed elementu v canvas px - pivot pro rotaci.</summary>
    public (float cx, float cy) GetCenterPx(RenderContext ctx) =>
        (ctx.SectionOriginX + (X + Width / 2f) * ctx.MmToPx,
         ctx.SectionOriginY + (Y + RenderedHeightMm(ctx) / 2f) * ctx.MmToPx);

    // ---------------------------------------------------------------------------

    /// <summary>
    /// Vykreslí element na canvas. Volá designer, preview i print engine.
    /// Souřadnice v ctx jsou přepočítány do px - element pracuje s vlastními mm hodnotami.
    /// </summary>
    public abstract void Render(RenderContext ctx);

    /// <summary>
    /// Vytvoří hlubokou kopii elementu s novým Id. Používá se při copy/paste.
    /// </summary>
    public abstract TemplateElement Clone();

    /// <summary>
    /// Vrátí seznam deskriptorů vlastností pro panel vlastností.
    /// Panel generuje UI řádky automaticky - přidání nové vlastnosti = přidat deskriptor sem.
    /// </summary>
    public abstract IReadOnlyList<PropertyDescriptor> GetPropertyDescriptors();

    /// <summary>
    /// Jak se vytiskne každá textová část prvku. Panel, lišta i formulář z toho ukazují hodnotu
    /// nenastavené vlastnosti stylu (revize 30. 10., A2) - do té doby si ji na 40 místech dosazovaly
    /// samy a ukazovaly jinou, než se tiskla. Prvek bez textu nemá žádnou.
    /// </summary>
    public virtual IEnumerable<StyleZone> ResolvedStyleZones() => [];

    /// <summary>Vyřešený styl části prvku podle cesty (<c>HeaderStyle</c>…); null = prvek ji nemá.</summary>
    public ResolvedTextStyle? ResolvedZone(string path) =>
        ResolvedStyleZones().FirstOrDefault(z => z.Path == path)?.Style;

    // Názvy skupin base polí - shodné se sdíleným Caché balíčkem BaseElementFields.cls,
    // ať obě editační cesty řadí tytéž vlastnosti do stejně pojmenovaných skupin.
    internal const string PositionGroup = "Pozice a velikost";
    internal const string BorderGroup = "Rámeček prvku";
    internal const string ConditionsGroup = "Podmínky";
    internal const string GridGroup = "Mřížka";

    /// <summary>
    /// Helper - deskriptory mřížky (vnitřních čar) tabulkového prvku. Obdoba rámečku: jedna sada
    /// polí, všude stejné popisky. Vnější obrys sem nepatří, ten je rámeček elementu.
    /// </summary>
    /// <param name="grid">Instance prvku - deskriptory na ni jen ukazují.</param>
    /// <param name="withLineToggles">
    /// False u prvku, který mřížkou kreslí jen čáru pod záhlavím (legenda poznámek) - přepínače
    /// řádků/sloupců by tam lhaly. Stejný vzor jako <c>withCorners</c> u rámečku.
    /// </param>
    /// <remarks>Popisky a skupiny musí souhlasit se sdíleným Caché balíčkem
    /// <c>BaseElementFields.AddGrid</c> - hlídá <c>EDInputFormSchemaDriftTests</c>.</remarks>
    protected static IReadOnlyList<PropertyDescriptor> GetGridDescriptors(GridStyle grid, bool withLineToggles = true) =>
    [
        .. withLineToggles
            ? new PropertyDescriptor[]
            {
                new BoolPropertyDescriptor { Label = "Čáry mezi řádky",  Group = GridGroup, Get = () => grid.ShowRowLines,    Set = v => grid.ShowRowLines = v },
                new BoolPropertyDescriptor { Label = "Čáry mezi sloupci", Group = GridGroup, Get = () => grid.ShowColumnLines, Set = v => grid.ShowColumnLines = v },
            }
            : [],
        new ColorPropertyDescriptor { Label = "Barva mřížky", Group = GridGroup, Get = () => grid.Color, Set = v => grid.Color = v },
        // 0 = „dle tloušťky rámečku" - číselné pole nemá jak vyjádřit „nenastaveno" (stejný trik jako výška řádku).
        new FloatPropertyDescriptor
        {
            Label = "Tloušťka mřížky", Group = GridGroup,
            Get = () => grid.Width ?? 0f,
            Set = v => grid.Width = v <= 0f ? null : v,
            Min = 0, Max = 5, Unit = "mm", Format = "0.0#"
        },
        new EnumPropertyDescriptor
        {
            Label = "Styl mřížky", Group = GridGroup,
            Get = () => grid.Style, Set = v => grid.Style = (LineStyle)v,
            Options =
            [
                ("Plná", (object)LineStyle.Solid),
                ("Čárkovaná", (object)LineStyle.Dashed),
                ("Tečkovaná", (object)LineStyle.Dotted),
            ]
        },
    ];

    /// <summary>
    /// Helper - vrátí deskriptory společných vlastností pozice a ZIndex.
    /// Volej na konci GetPropertyDescriptors() pomocí spread operátoru: [.. GetPositionDescriptors()]
    /// </summary>
    /// <remarks>Popisky, skupiny a rozsahy musí souhlasit se sdíleným Caché balíčkem
    /// <c>BaseElementFields.cls</c> (druhá editační cesta téže sady polí). <c>EDInputFormSchemaDriftTests</c>
    /// hlídá jen popisky a skupiny, rozsahy zatím nic (`revize-sjednoceni.md` B1).</remarks>
    protected IReadOnlyList<PropertyDescriptor> GetPositionDescriptors() =>
    [
        new FloatPropertyDescriptor { Label = "X",       Group = PositionGroup, Get = () => X,       Set = v => X = v,      Min = 0,  Max = 9999, Unit = "mm", Format = "0.0#" },
        new FloatPropertyDescriptor { Label = "Y",       Group = PositionGroup, Get = () => Y,       Set = v => Y = v,      Min = 0,  Max = 9999, Unit = "mm", Format = "0.0#" },
        new FloatPropertyDescriptor { Label = "Šířka",   Group = PositionGroup, Get = () => Width,   Set = v => Width = v,  Min = 1,  Max = 9999, Unit = "mm", Format = "0.0#" },
        .. (IsDynamic
            ? new PropertyDescriptor[] { new StringPropertyDescriptor { Label = "Výška", Group = PositionGroup,
                Get = () => "(podle obsahu)", Set = _ => { } } }
            : new PropertyDescriptor[] { new FloatPropertyDescriptor { Label = "Výška", Group = PositionGroup,
                Get = () => Height ?? EffectiveDesignerHeight, Set = v => Height = v,
                Min = 1, Max = 9999, Unit = "mm", Format = "0.0#" } }),
        new FloatPropertyDescriptor { Label = "Rotace",  Group = PositionGroup, Get = () => Rotation, Set = v => Rotation = v, Min = -360, Max = 360, Unit = "°", Format = "0.#" },
        ZIndexDescriptor(),
        LockedDescriptor(),

        BorderColorDescriptor(),
        BorderWidthDescriptor(),
        new EnumPropertyDescriptor
        {
            Label = "Rohy", Group = BorderGroup,
            Get = () => CornerStyle, Set = v => CornerStyle = (ElementCornerStyle)v,
            Options =
            [
                ("Ostré",    (object)ElementCornerStyle.None),
                ("Zaoblené", (object)ElementCornerStyle.Rounded),
                ("Zkosené",  (object)ElementCornerStyle.Beveled),
            ]
        },
        new FloatPropertyDescriptor
        {
            Label = "Velikost rohu", Group = BorderGroup,
            Get = () => CornerSize, Set = v => CornerSize = v, Min = 0, Max = 50, Unit = "mm", Format = "0.0#",
            VisibleWhen = () => CornerStyle != ElementCornerStyle.None
        },

        new StringPropertyDescriptor
        {
            Label = "Podmínka viditelnosti", Group = ConditionsGroup,
            // null / prázdný = vždy viditelný
            // Výrazy: "{TISK_SIPKY}", "{POCET} > 0", "{TYP} == \"JR\" && !{TISK_TARIFU}"
            Get = () => VisibilityExpression ?? "",
            Set = v  => VisibilityExpression = string.IsNullOrWhiteSpace(v) ? null : v.Trim(),
            Validate = Expressions.ExpressionEvaluator.Validate
        },

        // GroupId - zobrazit jen pokud element patří do skupiny (readonly informace)
        .. (GroupId is not null
            ? [new StringPropertyDescriptor
            {
                Label = "Skupina", Group = "Skupina",
                Get = () => GroupId ?? "",
                Set = _ => { }  // readonly - skupiny se mění přes Ctrl+G / Ctrl+Shift+G
            }]
            : Array.Empty<PropertyDescriptor>()),
    ];

    /// <summary>
    /// Vlastnosti, které okno společných vlastností nastavuje více prvkům naráz. Tytéž deskriptory jako
    /// v <see cref="GetPositionDescriptors"/> - do 30. 10. mělo okno vlastní kopii s jinými skupinami.
    /// </summary>
    internal IReadOnlyList<PropertyDescriptor> MultiEditDescriptors() =>
        [BorderColorDescriptor(), BorderWidthDescriptor(), ZIndexDescriptor(), LockedDescriptor()];

    private FloatPropertyDescriptor ZIndexDescriptor() =>
        new() { Label = "Vrstva", Group = PositionGroup, Get = () => ZIndex, Set = v => ZIndex = (int)Math.Round(v), Min = 0, Max = 100, Format = "0" };

    private BoolPropertyDescriptor LockedDescriptor() =>
        new() { Label = "Uzamčeno", Group = PositionGroup, Get = () => IsLocked, Set = v => IsLocked = v };

    private ColorPropertyDescriptor BorderColorDescriptor() =>
        new() { Label = "Barva rámečku", Group = BorderGroup, Get = () => BorderColor, Set = v => BorderColor = v };

    private FloatPropertyDescriptor BorderWidthDescriptor() =>
        new() { Label = "Tloušťka rámečku", Group = BorderGroup, Get = () => BorderWidth, Set = v => BorderWidth = v, Min = 0, Max = 5, Unit = "mm", Format = "0.0#" };

    // ── Scale ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Proporcionálně přeškáluje pozici, rozměry a vizuální properties elementu.
    /// Každý element overridne a přidá vlastní mm/pt properties.
    /// </summary>
    public virtual void ApplyScale(float scale)
    {
        X = MmMath.Round(X * scale);
        Y = MmMath.Round(Y * scale);
        Width = MmMath.Round(Width * scale);
        if (Height is not null) Height = MmMath.Round(Height.Value * scale);
        if (BorderWidth > 0) BorderWidth = MmMath.Round(BorderWidth * scale);
        if (CornerSize > 0) CornerSize = MmMath.Round(CornerSize * scale);
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
        target.CornerStyle = CornerStyle; target.CornerSize = CornerSize;
        target.Rotation = Rotation;
    }

    // ---------------------------------------------------------------------------

    [JsonIgnore]
    public bool IsDynamic => !Height.HasValue;

    /// <summary>Výška pro zobrazení v designeru - Height nebo placeholder dynamické sekce.</summary>
    [JsonIgnore]
    public float EffectiveDesignerHeight => Height ?? DesignerPlaceholderHeight;

    protected virtual float DesignerPlaceholderHeight => 20f;
}
