using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SkiaSharp;
using TemplateDesigner.Models.Expressions;
using TemplateDesigner.Rendering;

namespace TemplateDesigner.Models.Elements;

/// <summary>
/// Linkový jízdní řád - celá linka v jedné tabulce: svislý seznam zastávek
/// (tarifní čísla) × vodorovné sloupce jednotlivých spojů s časy v buňkách.
/// Datový zdroj: agenda LINKOVY_JR_AD / LINKOVY_JR_MHD (kolekce ZASTAVKY + SPOJE + SKUPINY_DNU).
///
/// Sloupce (`Columns`, model <see cref="StopColumn"/> - sdílený se StopList) definují popisové
/// sloupce: Zóna, Km, Tč, Zastávka, MHD + „Spoj" (řídí šířku/styl časových sloupců spojů, které
/// vznikají z dat). Editace přes 2-tab dialog (Obecné = EDInputForm, Sloupce = sdílený
/// `ColumnListEditor` + `CellStyleEditor`) - konzistentní se StopList/DepartureGrid.
///
/// První řez: bez 2D bin-packingu přes stránky (stránkuje PageLayoutEngine per sekce),
/// symboly jako textový fallback (Timetable font = roadmap 42b).
/// </summary>
public class LineTableElement : ScheduleElementBase, IRowHighlightable
{
    // ─── DataSource ──────────────────────────────────────────────────────────
    // Vlastní DataSource klíče (linkový formát) - NE zastávkové z base. LineTable dědí jen
    // generickou vrstvu ScheduleElementBase, zastávkový balast (StopScheduleElementBase) netahá.

    /// <summary>Klíč kolekce zastávek (řádky matice).</summary>
    public string ZastavkyDataSource { get; set; } = "ZASTAVKY";

    /// <summary>Klíč kolekce spojů (časové sloupce matice).</summary>
    public string SpojeDataSource { get; set; } = "SPOJE";

    /// <summary>Klíč kolekce skupin dnů.</summary>
    public string SkupinyDnuDataSource { get; set; } = "SKUPINY_DNU";

    // ─── Sloupce (pořadí, šířky, viditelnost, per-column styl) - sdílený StopColumn model ──

    public List<StopColumn> Columns { get; set; } = GetDefaultColumns();

    /// <summary>Klíč sloupce časů spojů - řídí šířku/styl všech spojových sloupců (vznikají z dat).</summary>
    public const string TripKey = ColumnKeys.Trip;

    public static List<StopColumn> GetDefaultColumns() =>
    [
        new() { Key = ColumnKeys.Zone,     Header = "Zóna",     Width = 8f,  Visible = true, Alignment = TextHorizontalAlignment.Center },
        new() { Key = ColumnKeys.Km,       Header = "km",       Width = 6f,  Visible = true, Alignment = TextHorizontalAlignment.Center },
        new() { Key = ColumnKeys.Tc,       Header = "Tč",       Width = 6f,  Visible = true, Alignment = TextHorizontalAlignment.Center },
        new() { Key = ColumnKeys.StopName, Header = "Zastávka", Width = 42f, Visible = true, Alignment = TextHorizontalAlignment.Left },
        new() { Key = ColumnKeys.Mhd,      Header = "MHD",      Width = 8f,  Visible = true, Alignment = TextHorizontalAlignment.Center },
        new() { Key = TripKey,             Header = "Spoj",     Width = null, Visible = true, Alignment = TextHorizontalAlignment.Center },
    ];

    // ─── Řádky ──────────────────────────────────────────────────────────────

    /// <summary>Řádek znaků týdenní platnosti (X, 6, 7, +) nad časy spojů.</summary>
    public bool ShowPlatnostRow { get; set; } = true;
    /// <summary>Řádek negativních značek (NZ) nad časy spojů.</summary>
    public bool ShowNzRow { get; set; } = true;
    /// <summary>Přípojné řádky Vyčká/Navazuje vložené za zastávku.</summary>
    public bool ShowPripojeRows { get; set; } = true;
    /// <summary>Pás názvů skupin dnů (Pracovní dny / Soboty / …) nad čísly spojů.</summary>
    public bool ShowGroupHeaders { get; set; } = true;

    // ─── Vzhled ─────────────────────────────────────────────────────────────

    /// <summary>Podbarvení řádků s lichým tarifním číslem (legacy 25% šedá).</summary>
    public bool HighlightOddTc { get; set; } = true;
    public string OddTcBackColor { get; set; } = "#FFEEEEEE";

    // Podmíněné řádkové featury (sdílené přes RowConditions, výraz nad daty zastávky = stop.SourceRow).
    // Prázdný výraz = vypnuto (nulová změna chování).
    /// <summary>Zastávka, kde výraz neplatí, se nevykreslí (řádek + její přípojné řádky; nezapočítá do výšky).</summary>
    public string? RowVisibilityExpression { get; set; }
    /// <summary>Podmíněné zvýraznění: seznam pravidel (výraz → barva pozadí), první splněné vyhrává.
    /// Null/prázdné = bez zvýraznění.</summary>
    public List<HighlightRule>? HighlightRules { get; set; }

    protected override float DesignerPlaceholderHeight => 60f;

    // ─── Runtime data (neserializují se) ────────────────────────────────────

    public sealed class LineStopData
    {
        public int Poradi;
        public string Typ = "";        // "zastavka" (hlavní/odjezdový) | "prijezd" (vložený příjezdový)
        public string Popis = "";
        public string Tc = "";
        public string Zona = "";
        public string Mhd = "";
        public string Nastupiste = "";
        public List<string> Atributy = [];
        public List<string> Km = [];

        /// <summary>Zdrojový řádek dat zastávky - pro vyhodnocení řádkových podmínek (zvýraznění). Neserializuje se.</summary>
        public DataContext? SourceRow;

        public bool JePrijezdovy => Typ == "prijezd";
    }

    public sealed class LinePripojData
    {
        public string Typ = "";      // "vycka" | "navazuje"
        public int Poradi;
        public string Text = "";
        public string Cas = "";
        public string Linka = "";
        public string Spoj = "";
        public bool Radek;           // legacy „typ zobrazení": tisknout jako vložený řádek matice
    }

    public sealed class LineTripData
    {
        public string CisloSpoje = "";
        public int Skupina = 1;
        public string Nz = "";
        public List<string> Platnost = [];
        public List<string> Casy = [];
        public List<LinePripojData> Pripoje = [];
    }

    public sealed class LineSkupinaData
    {
        public int Id = 1;
        public string Nadpis = "";
    }

    // vlastní cache (base cache je pro zastávkový formát; tady jiné kolekce)
    [JsonIgnore] private DataContext? _lineCacheRef;
    [JsonIgnore] private List<LineStopData>? _cachedStops;
    [JsonIgnore] private List<LineTripData>? _cachedTrips;
    [JsonIgnore] private List<LineSkupinaData>? _cachedGroups;

    private void EnsureParsed(DataContext data)
    {
        if (ReferenceEquals(data, _lineCacheRef) && _cachedStops is not null) return;
        _lineCacheRef = data;

        _cachedStops = [.. data.GetRows(ZastavkyDataSource).Select(row => new LineStopData
        {
            Poradi = ParseInt(row.GetString("PORADI")),
            Typ = row.GetString("TYP"),
            Popis = row.GetString("POPIS"),
            Tc = row.GetString("TC"),
            Zona = row.GetString("ZONA"),
            Mhd = row.GetString("MHD"),
            Nastupiste = row.GetString("NASTUPISTE"),
            Atributy = ParseStringArray(row.GetValue("ATRIBUTY")),
            Km = ParseStringArray(row.GetValue("KM")),
            SourceRow = row,
        })];

        _cachedTrips = [.. data.GetRows(SpojeDataSource).Select(row => new LineTripData
        {
            CisloSpoje = row.GetString("CISLO_SPOJE"),
            Skupina = ParseInt(row.GetString("SKUPINA")),
            Nz = row.GetString("NZ"),
            Platnost = ParseStringArray(row.GetValue("PLATNOST")),
            Casy = ParseStringArray(row.GetValue("CASY")),
            Pripoje = ParsePripoje(row.GetValue("PRIPOJE")),
        })];

        _cachedGroups = [.. data.GetRows(SkupinyDnuDataSource).Select(row => new LineSkupinaData
        {
            Id = ParseInt(row.GetString("ID")),
            Nadpis = row.GetString("NADPIS"),
        })];
    }

    private static List<string> ParseStringArray(object? value) =>
        value is JsonArray arr ? [.. arr.Select(n => n?.ToString() ?? "")] : [];

    private static List<LinePripojData> ParsePripoje(object? value)
    {
        if (value is not JsonArray arr) return [];
        return [.. arr.OfType<JsonObject>().Select(o => new LinePripojData
        {
            Typ = o["TYP"]?.ToString() ?? "",
            Poradi = int.TryParse(o["PORADI"]?.ToString(), out var p) ? p : 0,
            Text = o["TEXT"]?.ToString() ?? "",
            Cas = o["CAS"]?.ToString() ?? "",
            Linka = o["LINKA"]?.ToString() ?? "",
            Spoj = o["SPOJ"]?.ToString() ?? "",
            Radek = o["RADEK"]?.ToString() == "1",
        })];
    }

    // ─── Style resolution (kaskáda column → element → CellDefaults; vzor StopList) ─────────

    /// <summary>Parent kaskády pro per-column header override (co sloupec dědí bez vlastního stylu).</summary>
    public ResolvedTextStyle ResolveColumnHeaderParent() => HeaderStyle.Resolve(ScheduleHeaderDefaults);

    /// <summary>Parent kaskády pro per-column data override.</summary>
    public ResolvedTextStyle ResolveColumnDataParent() => DataStyle.Resolve(ScheduleDataDefaults);

    // Kaskáda column → element → baked delegována do sdíleného jádra (TableLayout.ResolveCascade).
    private ResolvedTextStyle ResolveHeaderStyle(StopColumn col) =>
        TableLayout.ResolveCascade(col.Style?.Header, ResolveColumnHeaderParent(), col.Alignment,
            elementDefinesHAlign: HeaderStyle.HAlign is not null);

    private ResolvedTextStyle ResolveDataStyle(StopColumn col) =>
        TableLayout.ResolveCascade(col.Style?.Data, ResolveColumnDataParent(), col.Alignment,
            elementDefinesHAlign: DataStyle.HAlign is not null);

    // ─── Layout ─────────────────────────────────────────────────────────────

    /// <summary>Vyřešený popisový sloupec připravený k renderu (šířka + styly + hodnota z dat).</summary>
    /// <summary>Nejmenší rozumná šířka sloupce bez vlastní šířky (mm) - pod ní je čas nečitelný.
    /// Když se při ní tabulka nevejde do <c>Width</c>, zmenší se šířky i fonty jednou škálou
    /// (dřív se přebytek tiše oříznul, takže z pravé strany zmizely spoje).</summary>
    private const float MinAutoColumnWidthMm = 3f;

    private sealed record RenderCol(float Width, string Header, ResolvedTextStyle HeaderStyle,
        ResolvedTextStyle DataStyle, Func<LineStopData, string> Value);

    private List<StopColumn> StructuralColumns() =>
        Columns.Where(c => c.Visible && c.Key != TripKey).ToList();

    private StopColumn TripColumn() =>
        Columns.FirstOrDefault(c => c.Key == TripKey) ?? new StopColumn { Key = TripKey, Width = null };

    private static string DefaultHeaderFor(string key) => key switch
    {
        ColumnKeys.Zone => "Zóna", ColumnKeys.Km => "km", ColumnKeys.Tc => "Tč",
        ColumnKeys.StopName => "Zastávka", ColumnKeys.Mhd => "MHD", _ => ""
    };

    /// <summary>Hodnota buňky popisového sloupce dle klíče (kmIndex jen pro Km sub-sloupce).</summary>
    private Func<LineStopData, string> ValueFor(string key, int kmIndex) => key switch
    {
        ColumnKeys.Zone     => s => s.JePrijezdovy ? "" : s.Zona,
        ColumnKeys.Km       => s => s.JePrijezdovy || kmIndex >= s.Km.Count ? "" : s.Km[kmIndex],
        ColumnKeys.Tc       => s => s.JePrijezdovy ? "" : s.Tc,
        ColumnKeys.Mhd      => s => s.JePrijezdovy ? "" : s.Mhd,
        ColumnKeys.StopName => s => s.JePrijezdovy ? "" :
                           s.Atributy.Count > 0
                               ? string.Join("", s.Atributy.Where(IsDisplayableStopAttribute)) + " " + s.Popis
                               : s.Popis,
        _          => _ => ""
    };

    /// <summary>
    /// Sestaví vyřešené popisové sloupce (Km expandované na kmCount sub-sloupců, MHD auto-remove když
    /// prázdné) + spočítá šířku spojového sloupce. Auto (Width==null) sdílí zbytek šířky s auto-spoji.
    /// </summary>
    private (List<RenderCol> Fixed, float TripWidth, int NameIndex) BuildColumns(int kmCount, bool anyMhd, int tripCount)
    {
        var structural = StructuralColumns();
        var trip = TripColumn();

        // km auto-remove když žádná zastávka nemá km; MHD auto-remove když prázdné (legacy)
        structural = structural.Where(c =>
            (c.Key != ColumnKeys.Km || kmCount > 0) &&
            (c.Key != ColumnKeys.Mhd || anyMhd)).ToList();

        // šířky: pevné (Width != null) honorovány; auto (null) sdílí zbytek se spoji
        float fixedSum = structural.Where(c => c.Width.HasValue).Sum(c => c.Key == ColumnKeys.Km ? c.Width!.Value * kmCount : c.Width!.Value);
        int autoStructural = structural.Count(c => !c.Width.HasValue);
        int autoUnits = autoStructural + (trip.Width.HasValue ? 0 : tripCount);
        float autoEach = autoUnits > 0 ? MathF.Max(MinAutoColumnWidthMm, (Width - fixedSum) / autoUnits) : 8f;

        var result = new List<RenderCol>();
        int nameIndex = -1;
        foreach (var col in structural)
        {
            var hs = ResolveHeaderStyle(col);
            var ds = ResolveDataStyle(col);
            string header = col.Header != "" ? col.Header : DefaultHeaderFor(col.Key);
            float w = col.Width ?? autoEach;
            if (col.Key == ColumnKeys.Km)
            {
                for (int k = 0; k < kmCount; k++)
                    result.Add(new RenderCol(w, header, hs, ds, ValueFor(ColumnKeys.Km, k)));
            }
            else
            {
                if (col.Key == ColumnKeys.StopName) nameIndex = result.Count;
                result.Add(new RenderCol(w, header, hs, ds, ValueFor(col.Key, 0)));
            }
        }
        float tripW = trip.Width ?? autoEach;
        return (result, tripW, nameIndex);
    }

    /// <summary>Atributy trasy k zobrazení před názvem - jen významové značky pro cestujícího
    /// (x = na znamení). Řídicí atributy (T/P/S/E/…) se v prvním řezu netisknou.</summary>
    private static bool IsDisplayableStopAttribute(string znak) => znak == "x";

    private int MaxKmCount(List<LineStopData> stops) =>
        Columns.Any(c => c.Visible && c.Key == ColumnKeys.Km) && stops.Count > 0 ? stops.Max(s => s.Km.Count) : 0;

    private bool HasPlatnostRow(List<LineTripData> trips) => ShowPlatnostRow && trips.Any(t => t.Platnost.Count > 0);
    private bool HasNzRow(List<LineTripData> trips) => ShowNzRow && trips.Any(t => t.Nz.Length > 0);
    private bool HasGroupHeaderBand(List<LineTripData> trips, List<LineSkupinaData> groups) =>
        ShowGroupHeaders && trips.Select(t => t.Skupina).Distinct().Count() > 1
        && groups.Any(g => g.Nadpis.Length > 0);

    /// <summary>Největší resolved font záhlaví/dat napříč viditelnými sloupci (vč. spoje) - pro výšky.</summary>
    private float MaxHeaderFontPt() => MaxFontPt(ResolveHeaderStyle, HeaderStyle.FontSize ?? 9f);
    private float MaxDataFontPt() => MaxFontPt(ResolveDataStyle, DataStyle.FontSize ?? 8f);

    private float MaxFontPt(Func<StopColumn, ResolvedTextStyle> resolve, float fallback)
    {
        var cols = Columns.Where(c => c.Visible).ToList();
        return cols.Count > 0 ? cols.Max(c => resolve(c).FontSize) : fallback;
    }

    public override float CalculateHeightFromData(DataContext? data)
    {
        if (data is null) return EffectiveDesignerHeight;
        EnsureParsed(data);
        var stops = _cachedStops!;
        var trips = _cachedTrips!;
        if (stops.Count == 0 || trips.Count == 0) return EffectiveDesignerHeight;

        float headerH = ComputeHeaderH(MaxHeaderFontPt());
        float rowH = DataRowHeight ?? ComputeRowH(MaxDataFontPt());

        float h = headerH;                                            // řádek čísel spojů
        if (HasGroupHeaderBand(trips, _cachedGroups!)) h += headerH;  // pás skupin dnů
        if (HasPlatnostRow(trips)) h += rowH;
        if (HasNzRow(trips)) h += rowH;
        // Počítá se přesně jako v Render (parita výška↔render): jen viditelné zastávky a jejich
        // přípojné řádky (matched na Poradi zastávky - jako render). Prázdný výraz → všechny zastávky.
        var visibleStops = RowConditions.VisibleRows(stops, s => s.SourceRow, RowVisibilityExpression);
        h += visibleStops.Count * rowH;
        if (ShowPripojeRows)
            h += visibleStops.Sum(s => trips.Sum(t => t.Pripoje.Count(p => p.Radek && p.Poradi == s.Poradi))) * rowH;
        return h;
    }

    // ─── Render ─────────────────────────────────────────────────────────────

    public override void Render(RenderContext ctx)
    {
        if (Width <= 0) return;

        using var paints = CreateStandardPaints(ctx);

        if (ctx.Data is null)
        {
            RenderPlaceholder(ctx, paints);
            return;
        }

        EnsureParsed(ctx.Data);
        var stops = _cachedStops!;
        var trips = _cachedTrips!;
        var groups = _cachedGroups!;
        if (stops.Count == 0 || trips.Count == 0)
        {
            RenderPlaceholder(ctx, paints);
            return;
        }

        // element-level styly pro pás skupin / řádek platnosti / NZ (nejsou vázané na sloupec)
        var headerResolved = ResolveColumnHeaderParent();
        var dataResolved = ResolveColumnDataParent();
        var headerCentered = headerResolved with { HAlign = TextHorizontalAlignment.Center };
        var dataCentered = dataResolved with { HAlign = TextHorizontalAlignment.Center };

        int kmCount = MaxKmCount(stops);
        bool anyMhd = stops.Any(s => s.Mhd.Length > 0);
        var (fixedCols, tripW, nameIndex) = BuildColumns(kmCount, anyMhd, trips.Count);
        var tripCol = TripColumn();
        var tripHeaderStyle = ResolveHeaderStyle(tripCol);
        var tripDataStyle = ResolveDataStyle(tripCol);

        // Škála: svisle (pevná Height × obsah) i vodorovně (kolik šířky sloupce reálně potřebují -
        // u linky s mnoha spoji dosednou na minimum a jejich součet přeteče Width). Bere se ta horší
        // ze stran a platí pro šířky sloupců, výšky řádků i fonty, takže tabulka dosedne na Width
        // místo oříznutí (dřív z pravé strany tiše zmizely spoje).
        float widthScale = TableLayout.ComputeWidthScale(
            fixedCols.Sum(c => c.Width) + trips.Count * tripW, Width);
        float scale = MathF.Min(ComputeFontScale(CalculateHeightFromData(ctx.Data)), widthScale);
        if (widthScale < 1f)
        {
            fixedCols = fixedCols.Select(c => c with { Width = c.Width * widthScale }).ToList();
            tripW *= widthScale;
        }

        float headerH = ComputeHeaderH(MaxHeaderFontPt()) * scale;
        float rowH = (DataRowHeight ?? ComputeRowH(MaxDataFontPt())) * scale;
        float fixedSum = fixedCols.Sum(c => c.Width);

        var clipRect = ctx.ToCanvasRect(X, Y, Width, Height ?? CalculateHeightFromData(ctx.Data));
        ctx.Canvas.Save();
        ctx.Canvas.ClipRect(clipRect);
        try
        {
            var plan = BuildRowPlan(stops, trips, groups, fixedCols, tripW, nameIndex, fixedSum,
                headerH, rowH, paints.Grid, headerResolved, headerCentered, tripHeaderStyle, tripDataStyle);

            RowPlanRenderer.DrawRows(ctx, X, Y, Width, plan, scale);
        }
        finally
        {
            ctx.Canvas.Restore();
        }
    }

    /// <summary>
    /// Popíše tabulku jako **plán řádků** (R1 řádkové jádro) - jen tvaruje data, nic nekreslí.
    /// Druhy řádků linkového JŘ: pás skupin dnů (sloučené sloupce) → čísla spojů → platnost →
    /// negativní značky → zastávky (s podřádky přípojů). Vodorovné čáry a pozadí zvýraznění nese
    /// <see cref="RowSpec"/>, samotné kreslení <see cref="RowPlanRenderer"/>.
    /// </summary>
    private List<RowSpec> BuildRowPlan(
        List<LineStopData> stops, List<LineTripData> trips, List<LineSkupinaData> groups,
        List<RenderCol> fixedCols, float tripW, int nameIndex, float fixedSum,
        float headerH, float rowH, SKPaint gridPaint,
        ResolvedTextStyle headerResolved, ResolvedTextStyle headerCentered,
        ResolvedTextStyle tripHeaderStyle, ResolvedTextStyle tripDataStyle)
    {
        var plan = new List<RowSpec>();

        // pás názvů skupin dnů - spojité úseky sloupců spojů stejné skupiny (sloučené buňky,
        // proto bez svislých čar, které by nadpis rozsekaly)
        if (HasGroupHeaderBand(trips, groups))
        {
            var band = new List<ColumnRenderer.CellSpec> { new(fixedSum, "", headerResolved) };
            int i = 0;
            while (i < trips.Count)
            {
                int start = i;
                int skupina = trips[i].Skupina;
                while (i < trips.Count && trips[i].Skupina == skupina) i++;
                string nadpis = groups.FirstOrDefault(g => g.Id == skupina)?.Nadpis ?? "";
                band.Add(new((i - start) * tripW, nadpis, headerCentered));
            }
            // sloucene bunky -> bez svislych car (rozsekaly by nadpis pres nekolik sloupcu spoju)
            plan.Add(new(RowKind.GroupHeader, headerH, band) { LineBelowPaint = gridPaint });
        }

        // řádek čísel spojů - popisové sloupce per-column styl, spoje z Trip sloupce
        var headerSpecs = new List<ColumnRenderer.CellSpec>();
        foreach (var c in fixedCols) headerSpecs.Add(new(c.Width, c.Header, c.HeaderStyle));
        foreach (var t in trips) headerSpecs.Add(new(tripW, t.CisloSpoje, tripHeaderStyle));
        plan.Add(new(RowKind.Header, headerH, headerSpecs)
            { VerticalLinePaint = gridPaint, LineBelowPaint = gridPaint });

        // řádek platnosti (znaky X/6/7/+ nad spojem)
        if (HasPlatnostRow(trips))
            plan.Add(new(RowKind.HeaderExtra, rowH,
                TripTextRow(fixedCols, trips, tripW, tripDataStyle, t => string.Join(" ", t.Platnost)))
            { VerticalLinePaint = gridPaint });

        // řádek NZ (negativní značky)
        if (HasNzRow(trips))
            plan.Add(new(RowKind.HeaderExtra, rowH,
                TripTextRow(fixedCols, trips, tripW, tripDataStyle, t => t.Nz))
            { VerticalLinePaint = gridPaint, LineBelowPaint = gridPaint });

        // řádky zastávek (+ vložené přípojné řádky)
        for (int si = 0; si < stops.Count; si++)
        {
            var stop = stops[si];

            // Podmíněné skrytí řádku zastávky (i s jejími přípojnými řádky). Iterujeme přes plný
            // index si → zarovnání časů spojů (Casy[si]) zůstane zachováno; jen tuto zastávku nekreslíme.
            if (!RowConditions.IsVisible(RowVisibilityExpression, stop.SourceRow)) continue;

            bool oddTc = HighlightOddTc && int.TryParse(stop.Tc, out var tcNum) && tcNum % 2 == 1;

            var specs = new List<ColumnRenderer.CellSpec>();
            foreach (var c in fixedCols)
                specs.Add(new(c.Width, c.Value(stop), OddTc(c.DataStyle, oddTc)));
            var tripCellStyle = OddTc(tripDataStyle, oddTc);
            foreach (var t in trips)
                specs.Add(new(tripW, si < t.Casy.Count ? t.Casy[si] : "", tripCellStyle));

            // Podmíněné zvýraznění řádku zastávky (celá šířka, pod sloupci) - sdílené s DynamicTable/StopList.
            string? rowHl = RowConditions.HighlightColor(HighlightRules, stop.SourceRow);
            plan.Add(new(RowKind.Data, rowH, specs)
            {
                Background = ColorHex.ParseOrNull(rowHl),
                VerticalLinePaint = gridPaint,
            });

            if (!ShowPripojeRows) continue;

            // přípojné řádky (vyčká/navazuje) za touto zastávkou
            foreach (var t in trips)
            {
                foreach (var pr in t.Pripoje.Where(p => p.Radek && p.Poradi == stop.Poradi))
                {
                    var prSpecs = new List<ColumnRenderer.CellSpec>();
                    for (int ci = 0; ci < fixedCols.Count; ci++)
                        prSpecs.Add(new(fixedCols[ci].Width,
                            ci == nameIndex ? PripojText(pr) : "", fixedCols[ci].DataStyle));
                    foreach (var t2 in trips)
                        prSpecs.Add(new(tripW, ReferenceEquals(t2, t) ? pr.Cas : "", tripDataStyle));

                    plan.Add(new(RowKind.SubRow, rowH, prSpecs)
                    { VerticalLinePaint = gridPaint, LineAbovePaint = gridPaint, LineBelowPaint = gridPaint });
                }
            }
        }

        return plan;
    }

    /// <summary>Řádek, kde popisové sloupce jsou prázdné a spojové nesou znak z <paramref name="text"/>
    /// (platnost, negativní značky).</summary>
    private static List<ColumnRenderer.CellSpec> TripTextRow(
        List<RenderCol> fixedCols, List<LineTripData> trips, float tripW,
        ResolvedTextStyle tripDataStyle, Func<LineTripData, string> text)
    {
        var specs = new List<ColumnRenderer.CellSpec>();
        foreach (var c in fixedCols) specs.Add(new(c.Width, "", c.DataStyle));
        foreach (var t in trips) specs.Add(new(tripW, text(t), tripDataStyle));
        return specs;
    }

    private ResolvedTextStyle OddTc(ResolvedTextStyle style, bool odd) =>
        odd ? style with { BackgroundColor = OddTcBackColor } : style;

    private static string PripojText(LinePripojData pr)
    {
        string sloveso = pr.Typ == "vycka" ? "vyčká na spoj" : "navazuje spoj";
        return $"{sloveso} {pr.Spoj} linky {pr.Linka} do {pr.Text}";
    }

    private void DrawRowLine(RenderContext ctx, float yMm, SchedulePaints paints)
    {
        var s = ctx.ToCanvas(X, yMm);
        var e = ctx.ToCanvas(X + Width, yMm);
        ctx.Canvas.DrawLine(s, e, paints.Grid);
    }

    private void RenderPlaceholder(RenderContext ctx, SchedulePaints paints)
    {
        var headerResolved = ResolveColumnHeaderParent();
        var dataResolved = ResolveColumnDataParent();
        float headerH = ComputeHeaderH(headerResolved.FontSize);
        float rowH = DataRowHeight ?? ComputeRowH(dataResolved.FontSize);

        float headerFontPx = headerResolved.FontSize * PtToMm * ctx.MmToPx;
        using var headerFont = new SKFont(GetTypeface(headerResolved), headerFontPx);
        float currentY = RenderSectionHeader(ctx, "Linkový JŘ - zastávky × spoje", X, Y, Width, headerH,
            headerFont, paints.HeaderBg, paints.HeaderText, paints.Grid);

        float dataFontPx = dataResolved.FontSize * PtToMm * ctx.MmToPx;
        using var dataFont = new SKFont(GetTypeface(dataResolved), dataFontPx);
        RenderPlaceholderRows(ctx, 5, "Zastávka", X, currentY, Width, rowH, dataFont, paints.Text, paints.Grid);
    }

    // ─── Clone / škálování / descriptory ────────────────────────────────────

    public override TemplateElement Clone()
    {
        var clone = new LineTableElement
        {
            ZastavkyDataSource = ZastavkyDataSource,
            SpojeDataSource = SpojeDataSource,
            SkupinyDnuDataSource = SkupinyDnuDataSource,
            Columns = Columns.Select(c => c.Clone()).ToList(),
            ShowPlatnostRow = ShowPlatnostRow,
            ShowNzRow = ShowNzRow,
            ShowPripojeRows = ShowPripojeRows,
            ShowGroupHeaders = ShowGroupHeaders,
            HighlightOddTc = HighlightOddTc,
            OddTcBackColor = OddTcBackColor,
            RowVisibilityExpression = RowVisibilityExpression,
            HighlightRules = HighlightRules?.Select(r => r.Clone()).ToList(),
        };
        CopyBaseTo(clone);
        CopyScheduleBaseProperties(clone);
        return clone;
    }

    public override void ApplyScale(float scale)
    {
        base.ApplyScale(scale);   // DataRowHeight + Header/Data styl škáluje base
        foreach (var c in Columns)
        {
            if (c.Width is float w) c.Width = w * scale;
            c.Style?.ApplyScale(scale);   // per-sloupcové font/padding overridy
        }
    }

    public override IReadOnlyList<PropertyDescriptor> GetPropertyDescriptors() =>
    [
        new BoolPropertyDescriptor { Label = "Řádek platnosti", Group = "Řádky", Get = () => ShowPlatnostRow, Set = v => ShowPlatnostRow = v },
        new BoolPropertyDescriptor { Label = "Řádek negativních značek", Group = "Řádky", Get = () => ShowNzRow, Set = v => ShowNzRow = v },
        new BoolPropertyDescriptor { Label = "Přípojné řádky", Group = "Řádky", Get = () => ShowPripojeRows, Set = v => ShowPripojeRows = v },
        new BoolPropertyDescriptor { Label = "Nadpisy skupin dnů", Group = "Řádky", Get = () => ShowGroupHeaders, Set = v => ShowGroupHeaders = v },
        new BoolPropertyDescriptor { Label = "Podbarvit lichá Tč", Group = "Vzhled", Get = () => HighlightOddTc, Set = v => HighlightOddTc = v },
        new ColorPropertyDescriptor { Label = "Barva lichých Tč", Group = "Vzhled", Get = () => OddTcBackColor, Set = v => OddTcBackColor = v },
        RowConditionDescriptors.RowVisibility(() => RowVisibilityExpression, v => RowVisibilityExpression = v),
        .. GetScheduleBaseDescriptors(),
        .. GetCellZoneBaseColorDescriptors(),
        GetDataRowHeightDescriptor(),
        .. GetPositionDescriptors(),
    ];
}
