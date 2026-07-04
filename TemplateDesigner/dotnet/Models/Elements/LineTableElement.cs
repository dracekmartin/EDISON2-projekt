using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SkiaSharp;
using TemplateDesigner.Rendering;

namespace TemplateDesigner.Models.Elements;

/// <summary>
/// Knižní / linkový jízdní řád — celá linka v jedné tabulce: svislý seznam zastávek
/// (tarifní čísla) × vodorovné sloupce jednotlivých spojů s časy v buňkách.
/// Datový zdroj: agenda KNIZNI_JR_AD / KNIZNI_JR_MHD (kolekce ZASTAVKY + SPOJE + SKUPINY_DNU).
///
/// Sloupce (`Columns`, model <see cref="StopColumn"/> — sdílený se StopList) definují popisové
/// sloupce: Zóna, Km, Tč, Zastávka, MHD + „Spoj" (řídí šířku/styl časových sloupců spojů, které
/// vznikají z dat). Editace přes 2-tab dialog (Obecné = EDInputForm, Sloupce = sdílený
/// `ColumnListEditor` + `CellStyleEditor`) — konzistentní se StopList/DepartureGrid.
///
/// První řez: bez 2D bin-packingu přes stránky (stránkuje PageLayoutEngine per sekce),
/// symboly jako textový fallback (Timetable font = roadmap 42b).
/// </summary>
public class LineTableElement : ScheduleElementBase
{
    // ─── DataSource ──────────────────────────────────────────────────────────

    /// <summary>Klíč kolekce spojů (časové sloupce matice).</summary>
    public string SpojeDataSource { get; set; } = "SPOJE";

    // ─── Sloupce (pořadí, šířky, viditelnost, per-column styl) — sdílený StopColumn model ──

    public List<StopColumn> Columns { get; set; } = GetDefaultColumns();

    /// <summary>Klíč sloupce časů spojů — řídí šířku/styl všech spojových sloupců (vznikají z dat).</summary>
    public const string TripKey = "Trip";

    public static List<StopColumn> GetDefaultColumns() =>
    [
        new() { Key = "Zone",     Header = "Zóna",     Width = 8f,  Visible = true, Alignment = TextHorizontalAlignment.Center },
        new() { Key = "Km",       Header = "km",       Width = 6f,  Visible = true, Alignment = TextHorizontalAlignment.Center },
        new() { Key = "Tc",       Header = "Tč",       Width = 6f,  Visible = true, Alignment = TextHorizontalAlignment.Center },
        new() { Key = "StopName", Header = "Zastávka", Width = 42f, Visible = true, Alignment = TextHorizontalAlignment.Left },
        new() { Key = "Mhd",      Header = "MHD",      Width = 8f,  Visible = true, Alignment = TextHorizontalAlignment.Center },
        new() { Key = TripKey,    Header = "Spoj",     Width = null, Visible = true, Alignment = TextHorizontalAlignment.Center },
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
    private sealed record RenderCol(float Width, string Header, ResolvedTextStyle HeaderStyle,
        ResolvedTextStyle DataStyle, Func<LineStopData, string> Value);

    private List<StopColumn> StructuralColumns() =>
        Columns.Where(c => c.Visible && c.Key != TripKey).ToList();

    private StopColumn TripColumn() =>
        Columns.FirstOrDefault(c => c.Key == TripKey) ?? new StopColumn { Key = TripKey, Width = null };

    private static string DefaultHeaderFor(string key) => key switch
    {
        "Zone" => "Zóna", "Km" => "km", "Tc" => "Tč", "StopName" => "Zastávka", "Mhd" => "MHD", _ => ""
    };

    /// <summary>Hodnota buňky popisového sloupce dle klíče (kmIndex jen pro Km sub-sloupce).</summary>
    private Func<LineStopData, string> ValueFor(string key, int kmIndex) => key switch
    {
        "Zone"     => s => s.JePrijezdovy ? "" : s.Zona,
        "Km"       => s => s.JePrijezdovy || kmIndex >= s.Km.Count ? "" : s.Km[kmIndex],
        "Tc"       => s => s.JePrijezdovy ? "" : s.Tc,
        "Mhd"      => s => s.JePrijezdovy ? "" : s.Mhd,
        "StopName" => s => s.JePrijezdovy ? "" :
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
            (c.Key != "Km" || kmCount > 0) &&
            (c.Key != "Mhd" || anyMhd)).ToList();

        // šířky: pevné (Width != null) honorovány; auto (null) sdílí zbytek se spoji
        float fixedSum = structural.Where(c => c.Width.HasValue).Sum(c => c.Key == "Km" ? c.Width!.Value * kmCount : c.Width!.Value);
        int autoStructural = structural.Count(c => !c.Width.HasValue);
        int autoUnits = autoStructural + (trip.Width.HasValue ? 0 : tripCount);
        float autoEach = autoUnits > 0 ? MathF.Max(3f, (Width - fixedSum) / autoUnits) : 8f;

        var result = new List<RenderCol>();
        int nameIndex = -1;
        foreach (var col in structural)
        {
            var hs = ResolveHeaderStyle(col);
            var ds = ResolveDataStyle(col);
            string header = col.Header != "" ? col.Header : DefaultHeaderFor(col.Key);
            float w = col.Width ?? autoEach;
            if (col.Key == "Km")
            {
                for (int k = 0; k < kmCount; k++)
                    result.Add(new RenderCol(w, header, hs, ds, ValueFor("Km", k)));
            }
            else
            {
                if (col.Key == "StopName") nameIndex = result.Count;
                result.Add(new RenderCol(w, header, hs, ds, ValueFor(col.Key, 0)));
            }
        }
        float tripW = trip.Width ?? autoEach;
        return (result, tripW, nameIndex);
    }

    /// <summary>Atributy trasy k zobrazení před názvem — jen významové značky pro cestujícího
    /// (x = na znamení). Řídicí atributy (T/P/S/E/…) se v prvním řezu netisknou.</summary>
    private static bool IsDisplayableStopAttribute(string znak) => znak == "x";

    private int MaxKmCount(List<LineStopData> stops) =>
        Columns.Any(c => c.Visible && c.Key == "Km") && stops.Count > 0 ? stops.Max(s => s.Km.Count) : 0;

    private bool HasPlatnostRow(List<LineTripData> trips) => ShowPlatnostRow && trips.Any(t => t.Platnost.Count > 0);
    private bool HasNzRow(List<LineTripData> trips) => ShowNzRow && trips.Any(t => t.Nz.Length > 0);
    private bool HasGroupHeaderBand(List<LineTripData> trips, List<LineSkupinaData> groups) =>
        ShowGroupHeaders && trips.Select(t => t.Skupina).Distinct().Count() > 1
        && groups.Any(g => g.Nadpis.Length > 0);

    private int PripojeRowCount(List<LineTripData> trips) =>
        ShowPripojeRows ? trips.Sum(t => t.Pripoje.Count(p => p.Radek)) : 0;

    /// <summary>Největší resolved font záhlaví/dat napříč viditelnými sloupci (vč. spoje) — pro výšky.</summary>
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
        h += stops.Count * rowH;
        h += PripojeRowCount(trips) * rowH;
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

        float scale = ComputeFontScale(CalculateHeightFromData(ctx.Data));

        // element-level styly pro pás skupin / řádek platnosti / NZ (nejsou vázané na sloupec)
        var headerResolved = ResolveColumnHeaderParent();
        var dataResolved = ResolveColumnDataParent();
        var headerCentered = headerResolved with { HAlign = TextHorizontalAlignment.Center };
        var dataCentered = dataResolved with { HAlign = TextHorizontalAlignment.Center };

        float headerH = ComputeHeaderH(MaxHeaderFontPt()) * scale;
        float rowH = (DataRowHeight ?? ComputeRowH(MaxDataFontPt())) * scale;

        int kmCount = MaxKmCount(stops);
        bool anyMhd = stops.Any(s => s.Mhd.Length > 0);
        var (fixedCols, tripW, nameIndex) = BuildColumns(kmCount, anyMhd, trips.Count);
        var tripCol = TripColumn();
        var tripHeaderStyle = ResolveHeaderStyle(tripCol);
        var tripDataStyle = ResolveDataStyle(tripCol);
        float fixedSum = fixedCols.Sum(c => c.Width);

        var clipRect = ctx.ToCanvasRect(X, Y, Width, Height ?? CalculateHeightFromData(ctx.Data));
        ctx.Canvas.Save();
        ctx.Canvas.ClipRect(clipRect);
        try
        {
            float currentY = Y;

            // pás názvů skupin dnů — spojité úseky sloupců spojů stejné skupiny
            if (HasGroupHeaderBand(trips, groups))
            {
                CellRenderer.DrawCell(ctx, new SKRect(X, currentY, X + fixedSum, currentY + headerH), "", headerResolved, scale);
                float gx = X + fixedSum;
                int i = 0;
                while (i < trips.Count)
                {
                    int start = i;
                    int skupina = trips[i].Skupina;
                    while (i < trips.Count && trips[i].Skupina == skupina) i++;
                    float w = (i - start) * tripW;
                    string nadpis = groups.FirstOrDefault(g => g.Id == skupina)?.Nadpis ?? "";
                    CellRenderer.DrawCell(ctx, new SKRect(gx, currentY, gx + w, currentY + headerH), nadpis, headerCentered, scale);
                    gx += w;
                }
                DrawRowLine(ctx, currentY + headerH, paints);
                currentY += headerH;
            }

            // řádek čísel spojů — popisové sloupce per-column styl, spoje z Trip sloupce
            var headerSpecs = new List<ColumnRenderer.CellSpec>();
            foreach (var c in fixedCols) headerSpecs.Add(new(c.Width, c.Header, c.HeaderStyle));
            foreach (var t in trips) headerSpecs.Add(new(tripW, t.CisloSpoje, tripHeaderStyle));
            ColumnRenderer.DrawRow(ctx, X, currentY, headerH, headerSpecs, paints.Grid, scale);
            DrawRowLine(ctx, currentY + headerH, paints);
            currentY += headerH;

            // řádek platnosti (znaky X/6/7/+ nad spojem)
            if (HasPlatnostRow(trips))
            {
                var specs = new List<ColumnRenderer.CellSpec>();
                foreach (var c in fixedCols) specs.Add(new(c.Width, "", c.DataStyle));
                foreach (var t in trips) specs.Add(new(tripW, string.Join(" ", t.Platnost), tripDataStyle));
                ColumnRenderer.DrawRow(ctx, X, currentY, rowH, specs, paints.Grid, scale);
                currentY += rowH;
            }

            // řádek NZ (negativní značky)
            if (HasNzRow(trips))
            {
                var specs = new List<ColumnRenderer.CellSpec>();
                foreach (var c in fixedCols) specs.Add(new(c.Width, "", c.DataStyle));
                foreach (var t in trips) specs.Add(new(tripW, t.Nz, tripDataStyle));
                ColumnRenderer.DrawRow(ctx, X, currentY, rowH, specs, paints.Grid, scale);
                DrawRowLine(ctx, currentY + rowH, paints);
                currentY += rowH;
            }

            // řádky zastávek (+ vložené přípojné řádky)
            for (int si = 0; si < stops.Count; si++)
            {
                var stop = stops[si];
                bool oddTc = HighlightOddTc && int.TryParse(stop.Tc, out var tcNum) && tcNum % 2 == 1;

                var specs = new List<ColumnRenderer.CellSpec>();
                foreach (var c in fixedCols)
                    specs.Add(new(c.Width, c.Value(stop), OddTc(c.DataStyle, oddTc)));
                var tripCellStyle = OddTc(tripDataStyle, oddTc);
                foreach (var t in trips)
                    specs.Add(new(tripW, si < t.Casy.Count ? t.Casy[si] : "", tripCellStyle));
                ColumnRenderer.DrawRow(ctx, X, currentY, rowH, specs, paints.Grid, scale);
                currentY += rowH;

                // přípojné řádky (vyčká/navazuje) za touto zastávkou
                if (ShowPripojeRows)
                {
                    foreach (var t in trips)
                    {
                        foreach (var pr in t.Pripoje.Where(p => p.Radek && p.Poradi == stop.Poradi))
                        {
                            var prSpecs = new List<ColumnRenderer.CellSpec>();
                            for (int ci = 0; ci < fixedCols.Count; ci++)
                            {
                                string txt = ci == nameIndex ? PripojText(pr) : "";
                                prSpecs.Add(new(fixedCols[ci].Width, txt, fixedCols[ci].DataStyle));
                            }
                            foreach (var t2 in trips)
                                prSpecs.Add(new(tripW, ReferenceEquals(t2, t) ? pr.Cas : "", tripDataStyle));
                            ColumnRenderer.DrawRow(ctx, X, currentY, rowH, prSpecs, paints.Grid, scale);
                            DrawRowLine(ctx, currentY, paints);
                            DrawRowLine(ctx, currentY + rowH, paints);
                            currentY += rowH;
                        }
                    }
                }
            }
        }
        finally
        {
            ctx.Canvas.Restore();
        }
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
        using var headerFont = new SKFont(GetTypeface(headerResolved.FontFamily, SKFontStyle.Bold), headerFontPx);
        float currentY = RenderSectionHeader(ctx, "Knižní JŘ — zastávky × spoje", X, Y, Width, headerH,
            headerFont, paints.HeaderBg, paints.HeaderText, paints.Grid);

        float dataFontPx = dataResolved.FontSize * PtToMm * ctx.MmToPx;
        using var dataFont = new SKFont(GetTypeface(dataResolved.FontFamily), dataFontPx);
        RenderPlaceholderRows(ctx, 5, "Zastávka", X, currentY, Width, rowH, dataFont, paints.Text, paints.Grid);
    }

    // ─── Clone / škálování / descriptory ────────────────────────────────────

    public override TemplateElement Clone()
    {
        var clone = new LineTableElement
        {
            SpojeDataSource = SpojeDataSource,
            Columns = Columns.Select(c => c.Clone()).ToList(),
            ShowPlatnostRow = ShowPlatnostRow,
            ShowNzRow = ShowNzRow,
            ShowPripojeRows = ShowPripojeRows,
            ShowGroupHeaders = ShowGroupHeaders,
            HighlightOddTc = HighlightOddTc,
            OddTcBackColor = OddTcBackColor,
        };
        CopyBaseTo(clone);
        CopyScheduleBaseProperties(clone);
        return clone;
    }

    public override void ApplyScale(float scale)
    {
        base.ApplyScale(scale);
        foreach (var c in Columns)
            if (c.Width is float w) c.Width = w * scale;
    }

    public override IReadOnlyList<PropertyDescriptor> GetPropertyDescriptors() =>
    [
        new BoolPropertyDescriptor { Label = "Řádek platnosti", Group = "Řádky", Get = () => ShowPlatnostRow, Set = v => ShowPlatnostRow = v },
        new BoolPropertyDescriptor { Label = "Řádek NZ", Group = "Řádky", Get = () => ShowNzRow, Set = v => ShowNzRow = v },
        new BoolPropertyDescriptor { Label = "Přípojné řádky", Group = "Řádky", Get = () => ShowPripojeRows, Set = v => ShowPripojeRows = v },
        new BoolPropertyDescriptor { Label = "Nadpisy skupin dnů", Group = "Řádky", Get = () => ShowGroupHeaders, Set = v => ShowGroupHeaders = v },
        new BoolPropertyDescriptor { Label = "Podbarvit lichá Tč", Group = "Vzhled", Get = () => HighlightOddTc, Set = v => HighlightOddTc = v },
        new ColorPropertyDescriptor { Label = "Barva lichých Tč", Group = "Vzhled", Get = () => OddTcBackColor, Set = v => OddTcBackColor = v },
        .. GetScheduleBaseDescriptors(),
        .. GetCellZoneBaseColorDescriptors(),
        GetDataRowHeightDescriptor(),
        .. GetPositionDescriptors(),
    ];
}
