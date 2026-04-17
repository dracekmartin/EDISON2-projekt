using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SkiaSharp;

namespace TemplateDesigner.Models.Elements;

/// <summary>
/// Abstraktní základ pro všechny JŘ elementy. Společné DataSource klíče,
/// vizuální properties a helpery pro stavbu runtime dat z DataContext.
/// Height může být null (dynamický) nebo pevné (auto-scale fontu při přetečení).
/// </summary>
public abstract class ScheduleElementBase : TemplateElement
{
    // ─── DataSource klíče (editovatelné v šabloně) ──────────────────────────

    public string ZastavkyDataSource { get; set; } = "ZASTAVKY";
    public string SkupinyDnuDataSource { get; set; } = "SKUPINY_DNU";
    public string OdjezdyDataSource { get; set; } = "ODJEZDY";
    public string PoznamkyDataSource { get; set; } = "POZNAMKY";

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

    /// <summary>Výchozí styl datových buněk (font, velikost). Per-column TextStyle cascaduje od tohoto.</summary>
    public TextStyle DataStyle { get; set; } = new() { FontFamily = "Arial", FontSize = 8f };

    /// <summary>Výchozí styl záhlaví (font, velikost, bold, barvy). Per-column TextStyle cascaduje od tohoto.</summary>
    public TextStyle HeaderStyle { get; set; } = new()
    {
        FontFamily = "Arial", FontSize = 9f, Bold = true,
        BackgroundColor = "#FFE0E0E0", TextColor = "#FF000000"
    };

    public string GridColor { get; set; } = "#FFD0D0D0";

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

    // ─── Cache ─────────────────────────────────────────────────────────────
    // Build helpery parsují vnořené JsonArray při každém volání.
    // Cache zajistí, že se parsuje jednou per DataContext reference.

    [JsonIgnore] private DataContext? _cachedDataRef;
    [JsonIgnore] private List<ZastavkaData>? _cachedZastavky;
    [JsonIgnore] private List<SkupinaDnuData>? _cachedSkupinyDnu;
    [JsonIgnore] private Dictionary<string, Dictionary<int, List<OdjezdData>>>? _cachedOdjezdy;
    [JsonIgnore] private List<PoznamkaData>? _cachedPoznamky;

    /// <summary>Invaliduje cache — volat při změně DataSource klíčů.</summary>
    protected void InvalidateCache() => _cachedDataRef = null;

    private bool IsCacheValid(DataContext? data) =>
        data is not null && ReferenceEquals(data, _cachedDataRef);

    /// <summary>
    /// Nastaví _cachedDataRef a vyčistí všechny cached kolekce.
    /// Volat na začátku každého Build helperu při detekci nového DataContext.
    /// </summary>
    private void SetCacheRef(DataContext data)
    {
        if (!ReferenceEquals(data, _cachedDataRef))
        {
            _cachedDataRef = data;
            _cachedZastavky = null;
            _cachedSkupinyDnu = null;
            _cachedOdjezdy = null;
            _cachedPoznamky = null;
        }
    }

    // ─── Parameter override helpery ──────────────────────────────────────────
    // Parametry z tiskového dialogu se mergují do DataContext.
    // Element přečte parametr jako override své property — šablona = default, parametr = override.

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

    // ─── Font/Paint helpery (sdílené pro per-column styling) ─────────────────

    /// <summary>Vytvoří SKFontStyle z bold/italic flagů.</summary>
    protected static SKFontStyle MakeFontStyle(bool bold, bool italic) => (bold, italic) switch
    {
        (true, true)   => SKFontStyle.BoldItalic,
        (true, false)  => SKFontStyle.Bold,
        (false, true)  => SKFontStyle.Italic,
        (false, false) => SKFontStyle.Normal,
    };

    // Typeface cache. SKTypeface loading (HarfBuzz) je drahý — per frame se volá 10+×.
    // SKFont nevlastní typeface (reference, ne owned), sdílení je bezpečné.
    private static readonly ConcurrentDictionary<(string Family, int Weight, int Width, SKFontStyleSlant Slant), SKTypeface> _typefaceCache = new();

    /// <summary>Vrátí sdílený SKTypeface pro kombinaci (family, style). Nikdy nevrací null.</summary>
    protected static SKTypeface GetTypeface(string? family, SKFontStyle? style = null)
    {
        var fam = string.IsNullOrEmpty(family) ? "Arial" : family!;
        var st = style ?? SKFontStyle.Normal;
        var key = (fam, st.Weight, st.Width, st.Slant);
        return _typefaceCache.GetOrAdd(key, k =>
            SKTypeface.FromFamilyName(k.Family, new SKFontStyle(k.Weight, k.Width, k.Slant))
            ?? SKTypeface.Default);
    }

    /// <summary>
    /// Vytvoří SKFont z TextStyle. Vrátí null pokud TextStyle nemá žádné fontové override.
    /// Volající musí výsledek uvolnit (přidat do manualDisposables).
    /// </summary>
    protected SKFont? CreateCellFont(TextStyle? style, float defaultSizePt, bool defaultBold, float scale, float mmToPx)
    {
        if (style is null) return null;
        if (style.FontFamily is null && style.FontSize is null && style.Bold is null && style.Italic is null)
            return null;

        var family = style.FontFamily ?? DataStyle.FontFamily ?? "Arial";
        var sizePx = (style.FontSize ?? defaultSizePt) * PtToMm * scale * mmToPx;
        var fontStyle = MakeFontStyle(style.Bold ?? defaultBold, style.Italic ?? false);
        return new SKFont(GetTypeface(family, fontStyle), sizePx);
    }

    /// <summary>Vytvoří SKPaint pro text z TextStyle.TextColor. Null pokud nemá override.</summary>
    protected static SKPaint? CreateCellTextPaint(TextStyle? style)
    {
        if (style?.TextColor is null) return null;
        return new SKPaint { Color = SKColor.Parse(style.TextColor), IsAntialias = true };
    }

    /// <summary>Vytvoří SKPaint pro pozadí z TextStyle.BackgroundColor. Null pokud nemá override.</summary>
    protected static SKPaint? CreateCellBgPaint(TextStyle? style)
    {
        if (style?.BackgroundColor is null) return null;
        return new SKPaint { Color = SKColor.Parse(style.BackgroundColor) };
    }

    /// <summary>Vykreslí pozadí buňky pokud existuje per-column bgPaint.</summary>
    protected static void RenderCellBackground(RenderContext ctx, SKPaint? bgPaint, float x, float y, float w, float h)
    {
        if (bgPaint is null) return;
        var rect = ctx.ToCanvasRect(x, y, w, h);
        ctx.Canvas.DrawRect(rect, bgPaint);
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
    protected SchedulePaints CreateStandardPaints() => new(
        SKColor.Parse(GridColor),
        SKColor.Parse(HeaderStyle.BackgroundColor ?? "#FFE0E0E0"),
        SKColor.Parse(HeaderStyle.TextColor ?? "#FF000000"));

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

    /// <summary>Vykreslí vnější rámeček elementu.</summary>
    protected static void RenderOuterBorder(
        RenderContext ctx, float x, float y, float width, float height, SKPaint gridPaint)
    {
        var outerRect = ctx.ToCanvasRect(x, y, width, height);
        ctx.Canvas.DrawRect(outerRect, gridPaint);
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

    // ─── Protected helpery pro podtřídy (s cache) ─────────────────────────

    protected List<ZastavkaData> BuildZastavky(DataContext? data)
    {
        if (data is null) return [];
        SetCacheRef(data);
        if (_cachedZastavky is not null)
            return _cachedZastavky;

        var rows = data.GetRows(ZastavkyDataSource);
        var result = new List<ZastavkaData>(rows.Count);

        foreach (var row in rows)
        {
            var z = new ZastavkaData
            {
                Id            = row.GetString("ID"),
                Popis         = row.GetString("POPIS"),
                TarifniCislo  = row.GetString("TC"),
                CasJizdy      = ParseInt(row.GetString("CAS_JIZDY")),
                Poradi        = ParseInt(row.GetString("PORADI")),
                JeAktualni    = ParseBool(row.GetString("JE_AKTUALNI")),
                Odsazeni      = ParseInt(row.GetString("ODSAZENI")),
                ZakladniTrasa = ParseBool(row.GetString("ZAKLADNI_TRASA", "true")),
                NaZnameni     = ParseBool(row.GetString("NA_ZNAMENI")),
            };

            // Vnořená pole — z JsonArray
            if (row.GetValue("ZONY") is JsonArray zonyArr)
                z.Zony = zonyArr.Select(n => n?.ToString() ?? "").Where(s => s.Length > 0).ToList();

            if (row.GetValue("PRUBEHY") is JsonArray prubehyArr)
                z.Prubehy = prubehyArr.Select(n => n?.ToString() ?? "").Where(s => s.Length > 0).ToList();

            if (row.GetValue("ATRIBUTY") is JsonArray attrArr)
                z.Atributy = attrArr
                    .Where(n => n is not null)
                    .Select(n => new AtributData
                    {
                        Znak  = n!["ZNAK"]?.ToString() ?? "",
                        Popis = n!["POPIS"]?.ToString() ?? ""
                    }).ToList();

            if (row.GetValue("POZNAMKY_ZASTAVKY") is JsonArray pozArr)
                z.PoznamkyZastavky = pozArr
                    .Where(n => n is not null)
                    .Select(n => new PoznamkaZastavkyData
                    {
                        Znak = n!["ZNAK"]?.ToString() ?? "",
                        Typ  = ParseTypPoznamky(n!["TYP"]?.ToString()),
                        Text = n!["TEXT"]?.ToString() ?? ""
                    }).ToList();

            result.Add(z);
        }

        _cachedZastavky = result;
        return result;
    }

    protected List<SkupinaDnuData> BuildSkupinyDnu(DataContext? data)
    {
        if (data is null) return [];
        SetCacheRef(data);
        if (_cachedSkupinyDnu is not null)
            return _cachedSkupinyDnu;

        var rows = data.GetRows(SkupinyDnuDataSource);
        var result = rows.Select(row => new SkupinaDnuData
        {
            Id     = row.GetString("ID"),
            Nadpis = row.GetString("NADPIS"),
            Nadpis2 = row.GetString("NADPIS2"),
            Poradi = ParseInt(row.GetString("PORADI"))
        }).OrderBy(s => s.Poradi).ToList();

        _cachedSkupinyDnu = result;
        return result;
    }

    protected Dictionary<string, Dictionary<int, List<OdjezdData>>> BuildOdjezdyBySkupina(DataContext? data)
    {
        if (data is null) return [];
        SetCacheRef(data);
        if (_cachedOdjezdy is not null)
            return _cachedOdjezdy;

        var rows = data.GetRows(OdjezdyDataSource);
        var result = new Dictionary<string, Dictionary<int, List<OdjezdData>>>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var odjezd = new OdjezdData
            {
                ZastavkaId              = row.GetString("ZASTAVKA_ID"),
                SkupinaDnuId            = row.GetString("SKUPINA_DNU_ID"),
                Hodina                  = ParseInt(row.GetString("HODINA")),
                Minuta                  = ParseInt(row.GetString("MINUTA")),
                ZkraceneOznaceniLinky   = row.GetString("ZKRACENE_OZNACENI_LINKY"),
                ForeColor               = NullIfEmpty(row.GetString("FORE_COLOR")),
                BackColor               = NullIfEmpty(row.GetString("BACK_COLOR")),
            };

            // Anotace — vnořené pole
            if (row.GetValue("ANOTACE") is JsonArray anotArr)
                odjezd.Anotace = anotArr
                    .Where(n => n is not null)
                    .Select(n => new AnotaceData
                    {
                        Typ  = ParseTypAnotace(n!["TYP"]?.ToString()),
                        Znak = n!["ZNAK"]?.ToString() ?? ""
                    }).ToList();

            if (!result.TryGetValue(odjezd.SkupinaDnuId, out var byHour))
            {
                byHour = new Dictionary<int, List<OdjezdData>>();
                result[odjezd.SkupinaDnuId] = byHour;
            }

            if (!byHour.TryGetValue(odjezd.Hodina, out var list))
            {
                list = [];
                byHour[odjezd.Hodina] = list;
            }

            list.Add(odjezd);
        }

        // Seřadit minuty v každé hodině
        foreach (var byHour in result.Values)
            foreach (var list in byHour.Values)
                list.Sort((a, b) => a.Minuta.CompareTo(b.Minuta));

        _cachedOdjezdy = result;
        return result;
    }

    protected List<PoznamkaData> BuildPoznamky(DataContext? data)
    {
        if (data is null) return [];
        SetCacheRef(data);
        if (_cachedPoznamky is not null)
            return _cachedPoznamky;

        var rows = data.GetRows(PoznamkyDataSource);
        var result = rows.Select(row => new PoznamkaData
        {
            Typ  = ParseTypPoznamky(row.GetString("TYP")),
            Znak = row.GetString("ZNAK"),
            Text = row.GetString("TEXT")
        }).ToList();

        _cachedPoznamky = result;
        return result;
    }

    // ─── Scale ──────────────────────────────────────────────────────────────

    public override void ApplyScale(float scale)
    {
        base.ApplyScale(scale);
        if (DataStyle.FontSize is not null) DataStyle.FontSize *= scale;
        if (HeaderStyle.FontSize is not null) HeaderStyle.FontSize *= scale;
    }

    // ─── Společné property descriptory ──────────────────────────────────────

    protected IReadOnlyList<PropertyDescriptor> GetScheduleBaseDescriptors() =>
    [
        new StringPropertyDescriptor
        {
            Label = "Font", Group = "Výchozí styl",
            Get = () => DataStyle.FontFamily ?? "Arial", Set = v => DataStyle.FontFamily = v
        },
        new FloatPropertyDescriptor
        {
            Label = "Velikost písma", Group = "Výchozí styl",
            Get = () => DataStyle.FontSize ?? 8f, Set = v => DataStyle.FontSize = v,
            Min = 4, Max = 30, Unit = "pt"
        },
        new FloatPropertyDescriptor
        {
            Label = "Velikost záhlaví", Group = "Výchozí styl",
            Get = () => HeaderStyle.FontSize ?? 9f, Set = v => HeaderStyle.FontSize = v,
            Min = 4, Max = 30, Unit = "pt"
        },
        new ColorPropertyDescriptor
        {
            Label = "Barva mřížky", Group = "Výchozí styl",
            Get = () => GridColor, Set = v => GridColor = v
        },
        new ColorPropertyDescriptor
        {
            Label = "Pozadí záhlaví", Group = "Výchozí styl",
            Get = () => HeaderStyle.BackgroundColor ?? "#FFE0E0E0", Set = v => HeaderStyle.BackgroundColor = v
        },
        new ColorPropertyDescriptor
        {
            Label = "Barva záhlaví", Group = "Výchozí styl",
            Get = () => HeaderStyle.TextColor ?? "#FF000000", Set = v => HeaderStyle.TextColor = v
        },
    ];

    // ─── Parse helpery ──────────────────────────────────────────────────────

    protected static int ParseInt(string s) =>
        int.TryParse(s, out var v) ? v : 0;

    protected static bool ParseBool(string s) =>
        s is "true" or "True" or "1" or "TRUE";

    protected static string? NullIfEmpty(string? s) =>
        string.IsNullOrEmpty(s) ? null : s;

    protected static TypPoznamky ParseTypPoznamky(string? s) => s?.ToLowerInvariant() switch
    {
        "nz" or "negativniznacka" => TypPoznamky.NegativniZnacka,
        "specialni" or "specialniznak" => TypPoznamky.SpecialniZnak,
        _ => TypPoznamky.Text
    };

    protected static TypAnotace ParseTypAnotace(string? s) => s?.ToLowerInvariant() switch
    {
        "nz" or "negativniznacka" => TypAnotace.NegativniZnacka,
        "specialni" or "specialniznak" => TypAnotace.SpecialniZnak,
        _ => TypAnotace.Text
    };

    // ─── Kopírování base properties ─────────────────────────────────────────

    protected void CopyScheduleBaseProperties(ScheduleElementBase target)
    {
        target.ZastavkyDataSource   = ZastavkyDataSource;
        target.SkupinyDnuDataSource = SkupinyDnuDataSource;
        target.OdjezdyDataSource    = OdjezdyDataSource;
        target.PoznamkyDataSource   = PoznamkyDataSource;
        target.DataStyle            = DataStyle.Clone();
        target.HeaderStyle          = HeaderStyle.Clone();
        target.GridColor            = GridColor;
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

    public SchedulePaints(SKColor gridColor, SKColor headerBgColor, SKColor headerFgColor)
    {
        Grid       = new SKPaint { Color = gridColor, IsStroke = true, StrokeWidth = 1f, IsAntialias = false };
        HeaderBg   = new SKPaint { Color = headerBgColor };
        Text       = new SKPaint { Color = new SKColor(0xFF222222), IsAntialias = true };
        HeaderText = new SKPaint { Color = headerFgColor, IsAntialias = true };
    }

    public void Dispose()
    {
        Grid.Dispose();
        HeaderBg.Dispose();
        Text.Dispose();
        HeaderText.Dispose();
    }
}
