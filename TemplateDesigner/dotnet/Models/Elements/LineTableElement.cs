using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SkiaSharp;
using TemplateDesigner.Rendering;

namespace TemplateDesigner.Models.Elements;

/// <summary>
/// Knižní / linkový jízdní řád — celá linka v jedné tabulce: svislý seznam zastávek
/// (tarifní čísla) × vodorovné sloupce jednotlivých spojů s časy v buňkách.
/// Datový zdroj: agenda KNIZNI_JR_AD (kolekce ZASTAVKY + SPOJE + SKUPINY_DNU).
/// První řez: bez 2D bin-packingu přes stránky (stránkuje PageLayoutEngine per sekce),
/// symboly jako textový fallback (Timetable font = roadmap 42b).
/// </summary>
public class LineTableElement : ScheduleElementBase
{
    // ─── DataSource ──────────────────────────────────────────────────────────

    /// <summary>Klíč kolekce spojů (sloupce matice).</summary>
    public string SpojeDataSource { get; set; } = "SPOJE";

    // ─── Viditelnost částí ──────────────────────────────────────────────────

    public bool ShowZonaColumn { get; set; } = true;
    public bool ShowKmColumns { get; set; } = true;
    public bool ShowTcColumn { get; set; } = true;
    /// <summary>MHD/IDS sloupec; automaticky se skryje, když je pro celou linku prázdný (legacy).</summary>
    public bool ShowMhdColumn { get; set; } = true;
    /// <summary>Řádek znaků týdenní platnosti (X, 6, 7, +) nad časy spojů.</summary>
    public bool ShowPlatnostRow { get; set; } = true;
    /// <summary>Řádek negativních značek (NZ) nad časy spojů.</summary>
    public bool ShowNzRow { get; set; } = true;
    /// <summary>Přípojné řádky Vyčká/Navazuje vložené za zastávku.</summary>
    public bool ShowPripojeRows { get; set; } = true;
    /// <summary>Pás názvů skupin dnů (Pracovní dny / Soboty / …) nad čísly spojů.</summary>
    public bool ShowGroupHeaders { get; set; } = true;

    // ─── Šířky sloupců (mm) ─────────────────────────────────────────────────

    public float ZonaColumnWidth { get; set; } = 8f;
    public float KmColumnWidth { get; set; } = 6f;
    public float TcColumnWidth { get; set; } = 6f;
    public float MhdColumnWidth { get; set; } = 8f;
    public float StopNameColumnWidth { get; set; } = 42f;
    /// <summary>Šířka sloupce spoje v mm; 0 = auto (zbytek šířky rovnoměrně mezi spoje).</summary>
    public float TripColumnWidth { get; set; } = 0f;

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

    // ─── Layout ─────────────────────────────────────────────────────────────

    private sealed record FixedCol(float Width, string Header, Func<LineStopData, string> Value);

    private List<FixedCol> BuildFixedColumns(int kmCount, bool anyMhd)
    {
        var cols = new List<FixedCol>();
        if (ShowZonaColumn) cols.Add(new(ZonaColumnWidth, "Zóna", s => s.JePrijezdovy ? "" : s.Zona));
        if (ShowKmColumns)
            for (int k = 0; k < kmCount; k++)
            {
                int kk = k;
                cols.Add(new(KmColumnWidth, "km", s => s.JePrijezdovy || kk >= s.Km.Count ? "" : s.Km[kk]));
            }
        if (ShowTcColumn) cols.Add(new(TcColumnWidth, "Tč", s => s.JePrijezdovy ? "" : s.Tc));
        // název zastávky (příjezdový podřádek název netiskne); atributy (x = na znamení) před názvem
        cols.Add(new(StopNameColumnWidth, "Zastávka", s =>
            s.JePrijezdovy ? "" :
            s.Atributy.Count > 0 ? string.Join("", s.Atributy.Where(IsDisplayableStopAttribute)) + " " + s.Popis : s.Popis));
        // MHD sloupec se automaticky odstraní, když je pro celou linku prázdný (legacy pravidlo)
        if (ShowMhdColumn && anyMhd) cols.Add(new(MhdColumnWidth, "MHD", s => s.JePrijezdovy ? "" : s.Mhd));
        return cols;
    }

    /// <summary>Atributy trasy k zobrazení před názvem — jen významové značky pro cestujícího
    /// (x = na znamení). Řídicí atributy (T/P/S/E/…) se v prvním řezu netisknou.</summary>
    private static bool IsDisplayableStopAttribute(string znak) => znak == "x";

    private float ComputeTripColumnWidth(float fixedSum, int tripCount)
    {
        if (TripColumnWidth > 0f) return TripColumnWidth;
        if (tripCount <= 0) return 8f;
        return MathF.Max(5f, (Width - fixedSum) / tripCount);
    }

    private int MaxKmCount(List<LineStopData> stops) =>
        ShowKmColumns && stops.Count > 0 ? stops.Max(s => s.Km.Count) : 0;

    private bool HasPlatnostRow(List<LineTripData> trips) => ShowPlatnostRow && trips.Any(t => t.Platnost.Count > 0);
    private bool HasNzRow(List<LineTripData> trips) => ShowNzRow && trips.Any(t => t.Nz.Length > 0);
    private bool HasGroupHeaderBand(List<LineTripData> trips, List<LineSkupinaData> groups) =>
        ShowGroupHeaders && trips.Select(t => t.Skupina).Distinct().Count() > 1
        && groups.Any(g => g.Nadpis.Length > 0);

    private int PripojeRowCount(List<LineTripData> trips) =>
        ShowPripojeRows ? trips.Sum(t => t.Pripoje.Count(p => p.Radek)) : 0;

    public override float CalculateHeightFromData(DataContext? data)
    {
        if (data is null) return EffectiveDesignerHeight;
        EnsureParsed(data);
        var stops = _cachedStops!;
        var trips = _cachedTrips!;
        if (stops.Count == 0 || trips.Count == 0) return EffectiveDesignerHeight;

        float headerFontPt = HeaderStyle.Resolve(ScheduleHeaderDefaults).FontSize;
        float dataFontPt = DataStyle.Resolve(ScheduleDataDefaults).FontSize;
        float headerH = ComputeHeaderH(headerFontPt);
        float rowH = DataRowHeight ?? ComputeRowH(dataFontPt);

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

        var headerResolved = HeaderStyle.Resolve(ScheduleHeaderDefaults);
        var dataResolved = DataStyle.Resolve(ScheduleDataDefaults);
        var dataCentered = dataResolved with { HAlign = TextHorizontalAlignment.Center };
        var headerCentered = headerResolved with { HAlign = TextHorizontalAlignment.Center };

        float headerH = ComputeHeaderH(headerResolved.FontSize) * scale;
        float rowH = (DataRowHeight ?? ComputeRowH(dataResolved.FontSize)) * scale;

        int kmCount = MaxKmCount(stops);
        bool anyMhd = stops.Any(s => s.Mhd.Length > 0);
        var fixedCols = BuildFixedColumns(kmCount, anyMhd);
        float fixedSum = fixedCols.Sum(c => c.Width);
        float tripW = ComputeTripColumnWidth(fixedSum, trips.Count);

        var clipRect = ctx.ToCanvasRect(X, Y, Width, Height ?? CalculateHeightFromData(ctx.Data));
        ctx.Canvas.Save();
        ctx.Canvas.ClipRect(clipRect);
        try
        {
            float currentY = Y;

            // pás názvů skupin dnů — spojité úseky sloupců spojů stejné skupiny
            if (HasGroupHeaderBand(trips, groups))
            {
                // prázdná buňka přes fixní sloupce
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

            // řádek čísel spojů
            var headerSpecs = new List<ColumnRenderer.CellSpec>();
            foreach (var c in fixedCols) headerSpecs.Add(new(c.Width, c.Header, headerResolved));
            foreach (var t in trips) headerSpecs.Add(new(tripW, t.CisloSpoje, headerCentered));
            ColumnRenderer.DrawRow(ctx, X, currentY, headerH, headerSpecs, paints.Grid, scale);
            DrawRowLine(ctx, currentY + headerH, paints);
            currentY += headerH;

            // řádek platnosti (znaky X/6/7/+ nad spojem)
            if (HasPlatnostRow(trips))
            {
                var specs = new List<ColumnRenderer.CellSpec>();
                foreach (var c in fixedCols) specs.Add(new(c.Width, "", dataResolved));
                foreach (var t in trips) specs.Add(new(tripW, string.Join(" ", t.Platnost), dataCentered));
                ColumnRenderer.DrawRow(ctx, X, currentY, rowH, specs, paints.Grid, scale);
                currentY += rowH;
            }

            // řádek NZ (negativní značky)
            if (HasNzRow(trips))
            {
                var specs = new List<ColumnRenderer.CellSpec>();
                foreach (var c in fixedCols) specs.Add(new(c.Width, "", dataResolved));
                foreach (var t in trips) specs.Add(new(tripW, t.Nz, dataCentered));
                ColumnRenderer.DrawRow(ctx, X, currentY, rowH, specs, paints.Grid, scale);
                DrawRowLine(ctx, currentY + rowH, paints);
                currentY += rowH;
            }

            // řádky zastávek (+ vložené přípojné řádky)
            for (int si = 0; si < stops.Count; si++)
            {
                var stop = stops[si];
                bool oddTc = HighlightOddTc && int.TryParse(stop.Tc, out var tcNum) && tcNum % 2 == 1;

                var rowStyle = dataResolved;
                var rowStyleCentered = dataCentered;
                if (oddTc)
                {
                    rowStyle = rowStyle with { BackgroundColor = OddTcBackColor };
                    rowStyleCentered = rowStyleCentered with { BackgroundColor = OddTcBackColor };
                }

                var specs = new List<ColumnRenderer.CellSpec>();
                for (int ci = 0; ci < fixedCols.Count; ci++)
                {
                    bool isName = ci == fixedCols.Count - 1;
                    specs.Add(new(fixedCols[ci].Width, fixedCols[ci].Value(stop), isName ? rowStyle : rowStyleCentered));
                }
                foreach (var t in trips)
                    specs.Add(new(tripW, si < t.Casy.Count ? t.Casy[si] : "", rowStyleCentered));
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
                                bool isName = ci == fixedCols.Count - 1;
                                string txt = isName ? PripojText(pr) : "";
                                prSpecs.Add(new(fixedCols[ci].Width, txt, dataResolved));
                            }
                            foreach (var t2 in trips)
                                prSpecs.Add(new(tripW, ReferenceEquals(t2, t) ? pr.Cas : "", dataCentered));
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
        var headerResolved = HeaderStyle.Resolve(ScheduleHeaderDefaults);
        var dataResolved = DataStyle.Resolve(ScheduleDataDefaults);
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
            ShowZonaColumn = ShowZonaColumn,
            ShowKmColumns = ShowKmColumns,
            ShowTcColumn = ShowTcColumn,
            ShowPlatnostRow = ShowPlatnostRow,
            ShowNzRow = ShowNzRow,
            ShowPripojeRows = ShowPripojeRows,
            ShowGroupHeaders = ShowGroupHeaders,
            ShowMhdColumn = ShowMhdColumn,
            MhdColumnWidth = MhdColumnWidth,
            ZonaColumnWidth = ZonaColumnWidth,
            KmColumnWidth = KmColumnWidth,
            TcColumnWidth = TcColumnWidth,
            StopNameColumnWidth = StopNameColumnWidth,
            TripColumnWidth = TripColumnWidth,
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
        ZonaColumnWidth *= scale;
        MhdColumnWidth *= scale;
        KmColumnWidth *= scale;
        TcColumnWidth *= scale;
        StopNameColumnWidth *= scale;
        if (TripColumnWidth > 0f) TripColumnWidth *= scale;
    }

    public override IReadOnlyList<PropertyDescriptor> GetPropertyDescriptors() =>
    [
        new BoolPropertyDescriptor { Label = "Sloupec zóny", Group = "Sloupce", Get = () => ShowZonaColumn, Set = v => ShowZonaColumn = v },
        new BoolPropertyDescriptor { Label = "Sloupce km", Group = "Sloupce", Get = () => ShowKmColumns, Set = v => ShowKmColumns = v },
        new BoolPropertyDescriptor { Label = "Sloupec Tč", Group = "Sloupce", Get = () => ShowTcColumn, Set = v => ShowTcColumn = v },
        new FloatPropertyDescriptor { Label = "Šířka zóny", Group = "Sloupce", Get = () => ZonaColumnWidth, Set = v => ZonaColumnWidth = v, Min = 3, Max = 30, Unit = "mm" },
        new FloatPropertyDescriptor { Label = "Šířka km", Group = "Sloupce", Get = () => KmColumnWidth, Set = v => KmColumnWidth = v, Min = 3, Max = 30, Unit = "mm" },
        new FloatPropertyDescriptor { Label = "Šířka Tč", Group = "Sloupce", Get = () => TcColumnWidth, Set = v => TcColumnWidth = v, Min = 3, Max = 30, Unit = "mm" },
        new BoolPropertyDescriptor { Label = "Sloupec MHD", Group = "Sloupce", Get = () => ShowMhdColumn, Set = v => ShowMhdColumn = v },
        new FloatPropertyDescriptor { Label = "Šířka MHD", Group = "Sloupce", Get = () => MhdColumnWidth, Set = v => MhdColumnWidth = v, Min = 3, Max = 30, Unit = "mm" },
        new FloatPropertyDescriptor { Label = "Šířka zastávek", Group = "Sloupce", Get = () => StopNameColumnWidth, Set = v => StopNameColumnWidth = v, Min = 10, Max = 120, Unit = "mm" },
        new FloatPropertyDescriptor { Label = "Šířka spoje (0=auto)", Group = "Sloupce", Get = () => TripColumnWidth, Set = v => TripColumnWidth = v, Min = 0, Max = 40, Unit = "mm" },
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
