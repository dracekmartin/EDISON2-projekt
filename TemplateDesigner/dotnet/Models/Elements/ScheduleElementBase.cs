using System.Collections.Concurrent;
using SkiaSharp;
using TemplateDesigner.Rendering;

namespace TemplateDesigner.Models.Elements;

/// <summary>
/// Abstraktní základ pro všechny JŘ elementy. Společné DataSource klíče,
/// vizuální properties a helpery pro stavbu runtime dat z DataContext.
/// Height může být null (dynamický) nebo pevné (auto-scale fontu při přetečení).
/// </summary>
public abstract class ScheduleElementBase : TemplateElement
{
    // ─── Konstanty ──────────────────────────────────────────────────────────

    /// <summary>Konverze bodů (pt) na milimetry: 1pt = 25.4/72 mm.</summary>
    protected const float PtToMm = 0.3528f;

    /// <summary>Výška řádku = fontSizeMm × tento faktor.</summary>
    protected const float RowHeightFactor = 1.6f;

    /// <summary>Výška záhlaví = headerFontMm × tento faktor.</summary>
    protected const float HeaderHeightFactor = 1.8f;

    /// <summary>Vertikální offset baseline textu v řádku (poměr výšky řádku).</summary>
    protected const float TextBaselineFactor = 0.72f;

    /// <summary>Vertikální offset baseline textu v záhlaví (poměr výšky záhlaví).</summary>
    protected const float HeaderBaselineFactor = 0.7f;

    // ─── Společné vizuální properties ───────────────────────────────────────

    /// <summary>
    /// Výchozí baked-in styl záhlaví pro JŘ elementy (Arial 9pt, Bold, šedé pozadí, černý text).
    /// Poslední vrstva kaskády před CellDefaults - user může kterýkoli atribut přepsat v
    /// element.HeaderStyle nebo per-column, nebo kliknout „×" a vrátit se sem.
    /// </summary>
    // Getter (ne static readonly) + bez explicitního FontFamily → font dědí z CellDefaults.Default,
    // do kterého se přes DocumentDefaults propíše výchozí font šablony. FontSize zůstává baked (9pt).
    public static ResolvedTextStyle ScheduleHeaderDefaults => CellDefaults.Default with
    {
        FontSize = 9f, Bold = true,
        TextColor = "#FF000000",
    };

    /// <summary>
    /// Podbarvení záhlaví, které dostane <b>nově vložený</b> prvek. Zapisuje se do prvku, není to
    /// záloha kaskády - jinak by nešlo rozlišit „nechal jsem to být" od „nechci žádné pozadí"
    /// a vymazání barvy by se po uložení vrátilo (nález Pavel 1. 9., #13158).
    /// </summary>
    public const string NoveZahlaviPozadi = "#FFE0E0E0";

    /// <summary>Výchozí baked-in styl datových buněk pro JŘ elementy (8pt; font z dokumentového defaultu).</summary>
    public static ResolvedTextStyle ScheduleDataDefaults => CellDefaults.Default with
    {
        FontSize = 8f,
    };

    /// <summary>Výchozí styl datových buněk (font, velikost). Per-column TextStyle cascaduje od tohoto.</summary>
    public TextStyle DataStyle { get; set; } = new();

    /// <summary>
    /// Výchozí styl záhlaví. Per-column TextStyle cascaduje od tohoto, tohle pak od
    /// <see cref="ScheduleHeaderDefaults"/>. Podbarvení nese <b>zapsané</b>
    /// (<see cref="NoveZahlaviPozadi"/>), aby se vymazaná barva nevracela zpátky.
    /// </summary>
    public TextStyle HeaderStyle { get; set; } = new() { BackgroundColor = NoveZahlaviPozadi };

    /// <summary>Mřížka = vnitřní čáry prvku. Vnější obrys je rámeček elementu (viz <see cref="GridStyle"/>).</summary>
    public GridStyle Grid { get; set; } = new() { ShowColumnLines = true };

    /// <summary>
    /// Výška jednoho datového řádku v mm. Null = auto-výpočet z fontu (<see cref="ComputeRowH"/>).
    /// Sdíleno všemi JŘ elementy (StopList, DepartureGrid, NotesLegend), které mají stejnou
    /// semantiku „jeden řádek = jedna položka dat".
    /// </summary>
    public float? DataRowHeight { get; set; }

    /// <summary>Vyřešený parent pro element-level záhlaví CellStyleVM (= baked defaults, protože není nic nad tímhle).</summary>
    public ResolvedTextStyle ResolveElementHeaderParent() => ScheduleHeaderDefaults;

    /// <summary>Vyřešený parent pro element-level data CellStyleVM.</summary>
    public ResolvedTextStyle ResolveElementDataParent() => ScheduleDataDefaults;

    // ─── Auto-scale ──────────────────────────────────────────────────────────

    /// <summary>
    /// Pokud element má pevnou Height a přirozený obsah ji přesahuje,
    /// vrátí scale faktor &lt; 1 pro zmenšení fontu. Jinak vrátí 1.
    /// </summary>
    protected float ComputeFontScale(float naturalHeightMm)
    {
        if (!Height.HasValue || naturalHeightMm <= 0) return 1f;
        float ratio = Height.Value / naturalHeightMm;
        return ratio >= 1f ? 1f : ratio;
    }

    /// <summary>Výška datového řádku v mm po aplikaci scale - pravidlo „fixed-then-shrink": pevná
    /// <see cref="DataRowHeight"/> (jinak font-driven auto) je VŽDY násobená <paramref name="factor"/>,
    /// takže se při přetečení pevné výšky elementu <b>zmenší do fitu</b> (ne přeteče/ořízne). Jediné místo
    /// pravidla - sdíleno StopListem i DepartureGridem, ať se render výšky řádku nerozejde (drift z B2).
    /// <paramref name="factor"/> = scale (u DepartureGridu scale × grow). Full row-layout unifikace = krok C.</summary>
    protected float ScaledDataRowHeight(float dataFontPt, float factor)
        => (DataRowHeight ?? ComputeRowH(dataFontPt)) * factor;

    /// <summary>Přirozená (neořezaná) výška obsahu v mm - počítá konkrétní JŘ element.</summary>
    public abstract float CalculateHeightFromData(DataContext? data);

    /// <summary>Výška z dat - aby rámeček, pozadí i ořez rohů obepnuly celý prvek, ne jen plochu
    /// z návrháře.</summary>
    public override float RenderedHeightMm(RenderContext ctx) =>
        Height ?? (ctx.Data is not null ? CalculateHeightFromData(ctx.Data) : EffectiveDesignerHeight);


    /// <summary>
    /// Přetéká obsah pevnou Height? Funguje i bez renderu (pre-detekce pro varování v preview).
    /// Dynamické elementy (Height == null) se neořezávají → false (geometrickou kontrolu řeší PreviewCanvas).
    /// </summary>
    public bool ContentOverflows(DataContext? data)
    {
        if (!Height.HasValue) return false;
        float natural = CalculateHeightFromData(data);
        // Přetéká, jakmile je přirozený obsah vyšší než box (0.5mm tolerance proti float false-positive).
        return natural > Height.Value + 0.5f;
    }

    // ─── Parameter override helpery ──────────────────────────────────────────
    // Parametry z tiskového dialogu se mergují do DataContext.
    // Element přečte parametr jako override své property - šablona = default, parametr = override.

    /// <summary>Přečte bool parametr z DataContext, nebo vrátí výchozí hodnotu z property.</summary>
    protected static bool GetBoolParam(DataContext? data, string key, bool defaultValue)
    {
        if (data?.GetValue(key) is not null)
            return ParseBool(data.GetString(key));
        return defaultValue;
    }

    /// <summary>Přečte int parametr z DataContext, nebo vrátí výchozí hodnotu z property.</summary>
    protected static int GetIntParam(DataContext? data, string key, int defaultValue)
    {
        if (data?.GetValue(key) is not null)
        {
            var s = data.GetString(key);
            return int.TryParse(s, out var v) ? v : defaultValue;
        }
        return defaultValue;
    }

    /// <summary>Přečte string parametr z DataContext, nebo vrátí výchozí hodnotu z property.</summary>
    protected static string GetStringParam(DataContext? data, string key, string defaultValue)
    {
        if (data?.GetValue(key) is not null)
        {
            var s = data.GetString(key);
            return s.Length > 0 ? s : defaultValue;
        }
        return defaultValue;
    }

    // ─── Typeface cache ─────────────────────────────────────────────────────

    // SKTypeface loading (HarfBuzz) je drahý - per frame se volá 10+×.
    // SKFont nevlastní typeface (reference, ne owned), sdílení je bezpečné.
    private static readonly ConcurrentDictionary<(string Family, int Weight, int Width, SKFontStyleSlant Slant), SKTypeface> _typefaceCache = new();

    /// <summary>
    /// Vrátí sdílený SKTypeface pro vyřešený styl - rodina i řez (váha/sklon) jdou z kaskády.
    /// Ruční cesty (měření, placeholdery) musí volat tuhle variantu, aby nepadly zpět na Normal/Bold
    /// a nezahodily uživatelův řez písma.
    /// </summary>
    protected static SKTypeface GetTypeface(ResolvedTextStyle style) =>
        GetTypeface(style.FontFamily, style.ToSKFontStyle());

    /// <summary>Vrátí sdílený SKTypeface pro kombinaci (family, style). Nikdy nevrací null.</summary>
    protected static SKTypeface GetTypeface(string? family, SKFontStyle? style = null)
    {
        var fam = string.IsNullOrEmpty(family) ? "Arial" : family!;
        var st = style ?? SKFontStyle.Normal;
        var key = (fam, st.Weight, st.Width, st.Slant);
        return _typefaceCache.GetOrAdd(key, k =>
            FontResolver.Resolve(k.Family, new SKFontStyle(k.Weight, k.Width, k.Slant)));
    }

    // ─── Render helpery ─────────────────────────────────────────────────────

    /// <summary>Změří šířku textu v mm pomocí daného fontu a MmToPx koeficientu.</summary>
    protected static float MeasureTextMm(SKFont font, string text, float mmToPx) =>
        font.MeasureText(text) / mmToPx;

    /// <summary>Vypočítá výšku řádku v mm pro danou velikost fontu (pt).</summary>
    protected static float ComputeRowH(float fontSizePt) => fontSizePt * PtToMm * RowHeightFactor;

    /// <summary>Vypočítá výšku záhlaví v mm pro danou velikost fontu záhlaví (pt).</summary>
    protected static float ComputeHeaderH(float headerFontSizePt) => headerFontSizePt * PtToMm * HeaderHeightFactor;

    /// <summary>Vytvoří standardní sadu paintů pro JŘ elementy. Volající musí dispose.</summary>
    protected SchedulePaints CreateStandardPaints(RenderContext ctx)
    {
        var (dashMm, gapMm) = LineElement.GetDefaultDashPattern(Grid.Style);
        return new(
            ColorHex.ParseOrBlack(Grid.Color),
            // Pozadí přes kaskádu a ParseOrTransparent: prázdná hodnota znamená „nekreslit nic",
            // ne „vezmi zálohu". Zálohou by se vymazané podbarvení vrátilo zpátky na šedou.
            ColorHex.ParseOrTransparent(HeaderStyle.Resolve(ScheduleHeaderDefaults).BackgroundColor),
            ColorHex.Parse(HeaderStyle.TextColor, ColorHex.Black),
            gridStrokePx: ComputeGridStrokePx(ctx),
            gridDashPx: ctx.Px(dashMm),
            gridGapPx: ctx.Px(gapMm));
    }

    /// <summary>
    /// Tloušťka mřížky v px = `Grid.Width * MmToPx`; bez vlastní hodnoty `BorderWidth * MmToPx`
    /// (sjednocená s outer borderem), minimum 0.5px aby byla vždy viditelná i když je border vypnutý.
    /// </summary>
    private float ComputeGridStrokePx(RenderContext ctx) =>
        MathF.Max(0.5f, (Grid.Width ?? BorderWidth) * ctx.MmToPx);

    /// <summary>Vykreslí záhlaví sekce: pozadí + text + horizontální čára pod záhlavím.</summary>
    protected static float RenderSectionHeader(
        RenderContext ctx, string text,
        float x, float y, float width, float headerH,
        SKFont headerFont, SKPaint headerBgPaint, SKPaint headerTextPaint, SKPaint gridPaint)
    {
        var headerRect = ctx.ToCanvasRect(x, y, width, headerH);
        ctx.Canvas.DrawRect(headerRect, headerBgPaint);

        var pt = ctx.ToCanvas(x + 1f, y + headerH * HeaderBaselineFactor);
        ctx.Canvas.DrawText(text, pt.X, pt.Y, headerFont, headerTextPaint);

        var lineStart = ctx.ToCanvas(x, y + headerH);
        var lineEnd = ctx.ToCanvas(x + width, y + headerH);
        ctx.Canvas.DrawLine(lineStart, lineEnd, gridPaint);

        return y + headerH;
    }


    /// <summary>Vykreslí placeholder řádky (šedý text, mřížka).</summary>
    protected static float RenderPlaceholderRows(
        RenderContext ctx, int count, string labelPrefix,
        float x, float y, float width, float rowH,
        SKFont font, SKPaint textPaint, SKPaint gridPaint)
    {
        var origColor = textPaint.Color;
        textPaint.Color = new SKColor(0xFF999999);

        float currentY = y;
        for (int i = 0; i < count; i++)
        {
            var pt = ctx.ToCanvas(x + 1f, currentY + rowH * TextBaselineFactor);
            ctx.Canvas.DrawText($"{labelPrefix} {i + 1}", pt.X, pt.Y, font, textPaint);

            var lineStart = ctx.ToCanvas(x, currentY + rowH);
            var lineEnd = ctx.ToCanvas(x + width, currentY + rowH);
            ctx.Canvas.DrawLine(lineStart, lineEnd, gridPaint);
            currentY += rowH;
        }

        textPaint.Color = origColor;
        return currentY;
    }

    // ─── Scale ──────────────────────────────────────────────────────────────

    public override void ApplyScale(float scale)
    {
        base.ApplyScale(scale);
        DataStyle.ApplyScale(scale);     // FontSize + paddingy (dřív jen FontSize)
        HeaderStyle.ApplyScale(scale);
        if (Grid.Width is not null) Grid.Width *= scale;
        if (DataRowHeight is not null) DataRowHeight *= scale;   // dřív se neškálovalo (LineTable/StopList/DepartureGrid)
    }

    // ─── Společné property descriptory ──────────────────────────────────────

    /// <summary>
    /// Non-style element-level descriptory pro PropertiesPanel v tabu „Obecné".
    /// Pokročilé Header/Data styling (font, alignment, padding) zůstává v dialogu sloupců;
    /// základní barvy (bg + text pro Header a Data) najdeš v <see cref="GetCellZoneBaseColorDescriptors"/>.
    /// </summary>
    /// <param name="withLineToggles">False u prvku, který mřížkou kreslí jen čáru pod záhlavím.</param>
    protected IReadOnlyList<PropertyDescriptor> GetScheduleBaseDescriptors(bool withLineToggles = true) =>
        GetGridDescriptors(Grid, withLineToggles);

    /// <summary>
    /// Sjednocený descriptor pro <see cref="DataRowHeight"/> - výška datového řádku v mm.
    /// Get vrací reálnou hodnotu (explicit nebo auto-vypočtenou z fontu),
    /// Set rozpozná „auto" zpětně přes epsilon comparison - uživatel po editaci nemusí vědět magickou hodnotu.
    /// </summary>
    protected FloatPropertyDescriptor GetDataRowHeightDescriptor(string group = "Řádky") => new()
    {
        Label = "Výška řádku",
        Group = group,
        Get = () => DataRowHeight ?? ComputeRowH(DataStyle.FontSize ?? 8f),
        Set = v =>
        {
            float autoVal = ComputeRowH(DataStyle.FontSize ?? 8f);
            DataRowHeight = MathF.Abs(v - autoVal) < 0.01f ? null : v;
        },
        Min = 1, Max = 50, Unit = "mm",
    };

    /// <summary>
    /// Element-level základní barvy pro zóny Header a Data - bg + text v properties panelu.
    /// Sjednocuje rychlou cestu k nejčastějším úpravám napříč JŘ elementy
    /// (StopList, DepartureGrid, NotesLegend) bez nutnosti otevírat dialog sloupců.
    /// Detailní cascade (font, alignment, padding) zůstává v dialogu kde má per-column granularitu.
    /// </summary>
    protected IReadOnlyList<PropertyDescriptor> GetCellZoneBaseColorDescriptors(
        string headerGroup = "Záhlaví",
        string dataGroup = "Datové buňky") =>
    [
        new ColorPropertyDescriptor
        {
            Label = "Pozadí záhlaví", Group = headerGroup,
            Get = () => HeaderStyle.BackgroundColor ?? ScheduleHeaderDefaults.BackgroundColor,
            Set = v => HeaderStyle.BackgroundColor = v,
        },
        new ColorPropertyDescriptor
        {
            Label = "Barva textu záhlaví", Group = headerGroup,
            Get = () => HeaderStyle.TextColor ?? ScheduleHeaderDefaults.TextColor,
            Set = v => HeaderStyle.TextColor = v,
        },
        new ColorPropertyDescriptor
        {
            Label = "Pozadí buněk", Group = dataGroup,
            Get = () => DataStyle.BackgroundColor ?? ScheduleDataDefaults.BackgroundColor,
            Set = v => DataStyle.BackgroundColor = v,
        },
        new ColorPropertyDescriptor
        {
            Label = "Barva textu buněk", Group = dataGroup,
            Get = () => DataStyle.TextColor ?? ScheduleDataDefaults.TextColor,
            Set = v => DataStyle.TextColor = v,
        },
    ];

    /// <summary>
    /// Element-level **velikost písma** pro zóny Záhlaví a Datové buňky - protějšek
    /// <see cref="GetCellZoneBaseColorDescriptors"/> pro to, co u tabulky rozhoduje o rozvržení.
    /// </summary>
    /// <remarks>
    /// ⚠️ Velikost buněk nese i **výšku řádku** (auto se počítá z fontu) a **šířku sloupce spoje**,
    /// takže bez ní se rozměr tabulky nedal změnit jinak než po sloupcích v dialogu, nebo přeškálováním
    /// celé šablony (nález Jan 7. 9. při stavbě šablony: 9 pt proti 4,8 pt je na 31 zastávkách
    /// rozdíl 158 proti 65 mm).
    ///
    /// <para>Je to **vrstva prvku** v kaskádě, ne přepis: per-sloupcová velikost z dialogu dál
    /// vyhrává, protože se resolvuje nad tímhle stylem.</para>
    /// </remarks>
    protected IReadOnlyList<PropertyDescriptor> GetCellZoneFontSizeDescriptors(
        string headerGroup = "Záhlaví",
        string dataGroup = "Datové buňky") =>
    [
        GetHeaderFontSizeDescriptor(headerGroup),
        new FloatPropertyDescriptor
        {
            Label = "Velikost", Group = dataGroup, Unit = "pt", Min = 3, Max = 72,
            Get = () => DataStyle.FontSize ?? ScheduleDataDefaults.FontSize,
            Set = v => DataStyle.FontSize = v,
        },
    ];

    /// <summary>Velikost písma záhlaví samostatně - pro prvek, jehož datová zóna má velikost jinde
    /// (Poznámky ji mají jako „Velikost poznámek" ve skupině Rozložení).</summary>
    protected PropertyDescriptor GetHeaderFontSizeDescriptor(string headerGroup = "Záhlaví") =>
        new FloatPropertyDescriptor
        {
            Label = "Velikost", Group = headerGroup, Unit = "pt", Min = 3, Max = 72,
            Get = () => HeaderStyle.FontSize ?? ScheduleHeaderDefaults.FontSize,
            Set = v => HeaderStyle.FontSize = v,
        };

    // ─── Parse helpery ──────────────────────────────────────────────────────

    protected static int ParseInt(string s) =>
        int.TryParse(s, out var v) ? v : 0;

    protected static bool ParseBool(string s) =>
        s is "true" or "True" or "1" or "TRUE";

    protected static string? NullIfEmpty(string? s) =>
        string.IsNullOrEmpty(s) ? null : s;

    // ─── Kopírování base properties ─────────────────────────────────────────

    /// <summary>Kopie generických (styly, grid) properties do klonu. Zastávkové DataSource klíče
    /// kopíruje <c>StopScheduleElementBase.CopyStopScheduleProperties</c>.</summary>
    protected void CopyScheduleBaseProperties(ScheduleElementBase target)
    {
        target.DataStyle     = DataStyle.Clone();
        target.HeaderStyle   = HeaderStyle.Clone();
        target.Grid = Grid.Clone();
        target.DataRowHeight = DataRowHeight;
    }
}

/// <summary>
/// Standardní sada SKPaint objektů pro JŘ elementy. Implementuje IDisposable.
/// </summary>
public sealed class SchedulePaints : IDisposable
{
    public SKPaint Grid { get; }
    public SKPaint HeaderBg { get; }
    public SKPaint Text { get; }
    public SKPaint HeaderText { get; }

    public SchedulePaints(SKColor gridColor, SKColor headerBgColor, SKColor headerFgColor,
        float gridStrokePx = 1f, float gridDashPx = 0f, float gridGapPx = 0f)
    {
        // Adaptivní stroke (stejně jako RenderBorder): sub-pixel AA pro tenké, jinak pixel-snap.
        bool thin = gridStrokePx < 1f;
        Grid       = new SKPaint { Color = gridColor, IsStroke = true, StrokeWidth = gridStrokePx, IsAntialias = thin };
        if (gridDashPx > 0f)
            Grid.PathEffect = SKPathEffect.CreateDash([gridDashPx, gridGapPx], 0);
        HeaderBg   = new SKPaint { Color = headerBgColor };
        Text       = new SKPaint { Color = new SKColor(0xFF222222), IsAntialias = true };
        HeaderText = new SKPaint { Color = headerFgColor, IsAntialias = true };
    }

    public void Dispose()
    {
        Grid.PathEffect?.Dispose();
        Grid.Dispose();
        HeaderBg.Dispose();
        Text.Dispose();
        HeaderText.Dispose();
    }
}
