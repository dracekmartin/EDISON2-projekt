using SkiaSharp;
using System.Text.Json.Serialization;
using TemplateDesigner.Models.Expressions;
using TemplateDesigner.Rendering;

namespace TemplateDesigner.Models.Elements;

// ─── Enums ──────────────────────────────────────────────────────────────────

public enum TableMode { Fixed, Dynamic }

public enum ColumnWidthMode { Mm, Percent }

// ─── TableColumn ────────────────────────────────────────────────────────────

public class TableColumn
{
    /// <summary>Text záhlaví sloupce.</summary>
    public string Header { get; set; } = "";

    /// <summary>Šířka sloupce - v mm nebo procentech (dle WidthMode).</summary>
    public float Width { get; set; } = 30f;

    public ColumnWidthMode WidthMode { get; set; } = ColumnWidthMode.Mm;

    /// <summary>
    /// Klíč datového pole ze schématu agendy (např. "CAS_ODJ").
    /// Dynamic: použije se jako zdroj dat řádku.
    /// Fixed: použití v {PLACEHOLDER} syntaxi je na uživateli.
    /// </summary>
    public string? Field { get; set; }

    /// <summary>Formátovací řetězec - "dd.MM.yyyy", "N2", "HH:mm" apod. Null = výchozí.</summary>
    public string? Format { get; set; }

    /// <summary>
    /// Počítané pole - výraz nad daty řádku (aritmetika, např. "{CENA} * {POCET}"). Má-li hodnotu,
    /// buňka se počítá z výrazu místo <see cref="Field"/> (Field se ignoruje). Číselný výsledek lze
    /// naformátovat přes <see cref="Format"/> (např. "N2"). Null/prázdné = běžný datový sloupec.
    /// </summary>
    public string? Expression { get; set; }

    public TextHorizontalAlignment Alignment { get; set; } = TextHorizontalAlignment.Left;

    public bool Visible { get; set; } = true;

    /// <summary>Per-column styl záhlaví. Null = dědit z elementu.</summary>
    public TextStyle? HeaderStyle { get; set; }

    /// <summary>Per-column styl datových buněk. Null = dědit z elementu.</summary>
    public TextStyle? DataStyle { get; set; }

    public TableColumn Clone() => new()
    {
        Header      = Header,
        Width       = Width,
        WidthMode   = WidthMode,
        Field       = Field,
        Format      = Format,
        Expression  = Expression,
        Alignment   = Alignment,
        Visible     = Visible,
        HeaderStyle = HeaderStyle?.Clone(),
        DataStyle   = DataStyle?.Clone(),
    };

    /// <summary>Naškáluje šířku (jen v mm, ne %) a per-sloupcové styly (font/padding).</summary>
    public void ApplyScale(float scale)
    {
        if (WidthMode == ColumnWidthMode.Mm) Width *= scale;
        HeaderStyle?.ApplyScale(scale);
        DataStyle?.ApplyScale(scale);
    }
}

// ─── TableRow / TableCell (Fixed mód) ────────────────────────────────────────

public class TableRow
{
    public List<TableCell> Cells { get; set; } = [];

    public string? BackgroundColor { get; set; }

    /// <summary>Vlastní výška řádku v mm. Null = společná výška tabulky (<c>RowHeight</c>);
    /// vyplní se tažením vodorovné čáry.</summary>
    public float? Height { get; set; }

    /// <summary>Podmíněná viditelnost vyhodnocovaná print enginem. Null = vždy viditelný.</summary>
    public string? VisibilityExpression { get; set; }

    public TableRow Clone() => new()
    {
        Cells               = Cells.Select(c => c.Clone()).ToList(),
        BackgroundColor     = BackgroundColor,
        Height              = Height,
        VisibilityExpression = VisibilityExpression
    };
}

public class TableCell
{
    /// <summary>Statický text nebo "{POLE}" placeholder.</summary>
    public string Content { get; set; } = "";

    public string? Format { get; set; }

    /// <summary>Per-cell styl. Null = výchozí. Jen nenullové properties přepisují default.</summary>
    public TextStyle? Style { get; set; }

    /// <summary>
    /// Přes kolik sloupců je buňka sloučená; 1 = běžná buňka. Aditivní pole - starší šablony ho
    /// nemají a vypadají stejně jako dřív. Buňky, které sloučená překryla, v modelu **zůstávají**
    /// (jen se nekreslí), takže rozdělení je prosté vrácení téhle hodnoty na 1.
    /// </summary>
    public int ColSpan { get; set; } = 1;

    public TableCell Clone() => new()
    {
        Content = Content,
        Format  = Format,
        Style   = Style?.Clone(),
        ColSpan = ColSpan,
    };
}

// ─── TableElement ────────────────────────────────────────────────────────────

/// <summary>
/// Pevná (ručně vyplňovaná) tabulka - mřížka m × n, jejíž obsah zadává uživatel; buňka nese text
/// nebo {PLACEHOLDER} vyhodnocený z dat. Nahrazuje skládání hlaviček a bloků (platební údaje,
/// rekapitulace) z jednotlivých textových prvků: chová se jako **jeden** prvek.
///
/// Řádky se generují z dat u <see cref="DynamicTableElement"/>, ne tady. Mód
/// <see cref="TableMode.Dynamic"/> zůstal jen kvůli **starým šablonám** - z UI se nenastavuje
/// (přepínač režimu v panelu není), nové tabulky vznikají přes <see cref="CreateGrid"/> jako Fixed.
///
/// Výchozí nová tabulka je bez řádku záhlaví: záhlaví = první řádek, který si uživatel udělá tučný
/// (rozhodnutí Jan 12. 8.). <see cref="ShowHeader"/> zůstává kvůli šablonám, které ho už mají.
/// </summary>
public class TableElement : TemplateElement
{
    /// <summary>
    /// Tabulka dřív kreslila vlastní vnější rámec natvrdo; ten je nově rámeček elementu, takže si ho
    /// nová tabulka přednastaví na tentýž vzhled (0,7 px při 96 DPI = 0,19 mm). `B40`.
    /// </summary>
    public TableElement()
    {
        BorderColor = "#FF999999";
        BorderWidth = 0.19f;
    }

    /// <summary>
    /// Nová prázdná mřížka pro vložení z palety - viditelné čáry, bez řádku záhlaví, sloupce
    /// stejně široké (auto). Bez tohohle by se vložil prvek bez sloupců, který se nevykreslí.
    /// </summary>
    public static TableElement CreateGrid(int columns, int rows)
    {
        var t = new TableElement
        {
            Mode        = TableMode.Fixed,
            HeaderStyle = new TextStyle { BackgroundColor = TableStyleDefaults.NoveZahlaviPozadi },
            Columns     = [.. Enumerable.Range(0, columns).Select(_ => new TableColumn { Header = "", Width = 0f })],
        };
        t.Rows = [.. Enumerable.Range(0, rows).Select(_ => new TableRow
        {
            Cells = [.. Enumerable.Range(0, columns).Select(_ => new TableCell())]
        })];
        return t;
    }

    public TableMode Mode { get; set; } = TableMode.Dynamic;

    /// <summary>
    /// Dynamic mód: klíč datové kolekce v DataContext._rows (= DataFieldCollection.CollectionKey).
    /// Fixed mód: null (nepoužívá se).
    /// </summary>
    public string? DataSource { get; set; }

    public List<TableColumn> Columns { get; set; } = [];

    /// <summary>
    /// Fixed mód: definice řádků tabulky.
    /// Dynamic mód: null - print engine generuje řádky za běhu.
    /// </summary>
    public List<TableRow>? Rows { get; set; }

    /// <summary>Výška jednoho datového řádku v mm.</summary>
    public float RowHeight { get; set; } = 6f;

    /// <summary>Zvláštní řádek záhlaví. Výchozí je vypnutý - u ručně vyplňované tabulky si záhlaví
    /// dělá uživatel z prvního řádku. Uložené šablony nesou hodnotu vždy explicitně, takže změna
    /// výchozí hodnoty jim vzhled nemění.</summary>
    public bool ShowHeader { get; set; }

    /// <summary>Výška řádku záhlaví v mm.</summary>
    public float HeaderHeight { get; set; } = 8f;

    /// <summary>Styl záhlaví tabulky. Null = výchozí (Bold, #333333 text, bez podbarvení).</summary>
    public TextStyle? HeaderStyle { get; set; }

    /// <summary>Styl datových buněk tabulky. Null = výchozí (#222222 text, průhledné pozadí).</summary>
    public TextStyle? DataStyle { get; set; }

    /// <summary>Mřížka = vnitřní čáry. Vnější obrys kreslí rámeček elementu (viz <see cref="GridStyle"/>).
    /// Zapnutá: bez čar by u prázdné ručně vyplňované tabulky nebylo poznat, kam se píše.</summary>
    public GridStyle Grid { get; set; } = new() { ShowRowLines = true, ShowColumnLines = true, Color = "#FFCCCCCC" };

    /// <summary>Barva střídajících se řádků (#AARRGGBB). Null = bez střídání.</summary>
    public string? AlternatingRowColor { get; set; }

    /// <summary>
    /// Souhrnné řádky zobrazené pod datovými řádky (zápatí tabulky).
    /// Buňky mohou obsahovat agregační funkce: SUM({POLE}), COUNT(), AVG({POLE}), MIN({POLE}), MAX({POLE}).
    /// Null = žádné souhrnné řádky.
    /// </summary>
    public List<TableRow>? SummaryRows { get; set; }

    // ─── Designer helpers ────────────────────────────────────────────

    /// <summary>Počet ukázkových řádků zobrazených v designeru pro Dynamic mód.</summary>
    private int DesignerPreviewRows => 3;

    protected override float DesignerPlaceholderHeight => Mode == TableMode.Fixed
        // Aspoň jeden řádek - prázdná tabulka by jinak měla nulovou výšku a nešlo by ji chytit myší.
        ? (Rows is { Count: > 0 } r ? NaturalHeight(r) : NaturalHeight(1))
        : 40f + (SummaryRows?.Count ?? 0) * RowHeight;

    /// <summary>Neškálovaná výška obsahu v mm - každý řádek svou výškou (<see cref="TableRow.Height"/>,
    /// bez ní společná <see cref="RowHeight"/>).</summary>
    private float NaturalHeight(IReadOnlyList<TableRow> rows) =>
        (ShowHeader ? HeaderHeight : 0f)
        + rows.Sum(r => r.Height ?? RowHeight)
        + (SummaryRows?.Count ?? 0) * RowHeight;

    /// <summary>Totéž pro řádky z dat - ty vlastní výšku nemají, takže stačí počet.</summary>
    private float NaturalHeight(int rowCount) =>
        (ShowHeader ? HeaderHeight : 0f) + rowCount * RowHeight + (SummaryRows?.Count ?? 0) * RowHeight;

    /// <summary>
    /// Řádky, které se mají kreslit - **jediné místo**, kudy jdou render i výpočet výšky, takže se
    /// nemůžou rozejít v počtu. **Bez dat se nefiltruje** - v návrháři žádná data nejsou,
    /// takže by podmíněný řádek zmizel i z plátna a uživatel by ho nemohl vybrat ani zbavit
    /// podmínky, kterou mu právě dal (nález 18. 8. při zavádění podmíněných řádků). Schovává se
    /// tedy až tam, kde je proti čemu podmínku vyhodnotit: v náhledu a při tisku.
    /// </summary>
    private List<TableRow> VisibleFixedRows(DataContext? data) =>
        Mode == TableMode.Fixed && Rows is { Count: > 0 }
            ? [.. Rows.Where(r => data is null || ExpressionEvaluator.IsVisible(r.VisibilityExpression, data))]
            : [];

    /// <summary>
    /// Dynamická výška v mm z dat - pro print engine (přes <c>SkiaHeightCalculator</c>, stejně jako
    /// ostatní tabulkové elementy). Fixed = počet viditelných řádků, Dynamic = řádky z <see cref="DataSource"/>
    /// (bez dat ukázkové řádky jako v designeru).
    /// </summary>
    public float CalculateHeightFromData(DataContext? data)
    {
        if (Mode == TableMode.Fixed) return NaturalHeight(VisibleFixedRows(data));

        var rows = data?.GetRows(DataSource ?? "") ?? [];
        return NaturalHeight(rows.Count > 0 ? rows.Count : DesignerPreviewRows);
    }

    // ─── Style resolution (kaskáda column → element → table defaults → CellDefaults) ──

    /// <summary>Parent kaskády pro element-level záhlaví CellStyleVM (= baked defaults).</summary>
    public ResolvedTextStyle ResolveElementHeaderParent() => TableStyleDefaults.Header;

    /// <summary>Parent kaskády pro element-level data CellStyleVM (= baked defaults).</summary>
    public ResolvedTextStyle ResolveElementDataParent() => TableStyleDefaults.Data;

    /// <summary>Parent kaskády pro per-column header override.</summary>
    public ResolvedTextStyle ResolveColumnHeaderParent() =>
        (HeaderStyle ?? new TextStyle()).Resolve(TableStyleDefaults.Header);

    /// <summary>Parent kaskády pro per-column data override.</summary>
    public ResolvedTextStyle ResolveColumnDataParent() =>
        (DataStyle ?? new TextStyle()).Resolve(TableStyleDefaults.Data);

    // Kaskáda column → element → baked delegována do sdíleného jádra (TableLayout.ResolveCascade) -
    // stejně jako DynamicTable/StopList. Legacy col.Alignment = HAlign fallback jen když ho
    // nedefinuje sloupec ani element.
    private ResolvedTextStyle ResolveColumnHeaderStyle(TableColumn col) =>
        TableLayout.ResolveCascade(col.HeaderStyle, ResolveColumnHeaderParent(), col.Alignment,
            elementDefinesHAlign: HeaderStyle?.HAlign is not null);

    private ResolvedTextStyle ResolveColumnDataStyle(TableColumn col) =>
        TableLayout.ResolveCascade(col.DataStyle, ResolveColumnDataParent(), col.Alignment,
            elementDefinesHAlign: DataStyle?.HAlign is not null);

    // ─── Render ──────────────────────────────────────────────────────

    public override void Render(RenderContext ctx)
    {
        if (Width <= 0) return;

        float tableHeight = Height ?? (ctx.Data is not null ? CalculateHeightFromData(ctx.Data) : EffectiveDesignerHeight);
        var tableRect = ctx.ToCanvasRect(X, Y, Width, tableHeight);

        using var bgPaint = new SKPaint { Color = SKColors.White };
        // Jediný paint pro vnitřní čáry; vnější obrys kreslí RenderBorderPass z rámečku elementu.
        using var sepPaint = Grid.CreatePaint(ctx, BorderWidth);

        ctx.Canvas.DrawRect(tableRect, bgPaint);

        var cols = Columns.Where(c => c.Visible).ToList();
        if (cols.Count == 0)
        {
            // Bez sloupců se dřív nevykreslilo vůbec nic - vložený prvek byl neviditelný a uživatel
            // nevěděl, kam kliknul. Stejný naváděcí placeholder jako u dynamické tabulky.
            ctx.Canvas.DrawRect(tableRect, sepPaint);
            using var hintFont  = new SKFont { Size = 3.2f * ctx.MmToPx };
            using var hintPaint = new SKPaint { Color = new SKColor(0xFF999999), IsAntialias = true };
            ctx.Canvas.DrawText("Tabulka - přidejte sloupce", tableRect.MidX,
                tableRect.MidY + hintFont.Size * 0.35f, SKTextAlign.Center, hintFont, hintPaint);
            return;
        }

        var layout = ComputeLayout(ctx.Data, cols);
        float[] colWidths = layout.ColumnWidths;
        float headerH = layout.HeaderHeightMm;
        float rowH    = layout.RowHeightMm;
        float fontScale = layout.FontScale;

        // Pre-resolve styly (kaskáda je deterministická)
        var headerStyles = cols.Select(ResolveColumnHeaderStyle).ToArray();
        var dataStyles   = cols.Select(ResolveColumnDataStyle).ToArray();

        var fixedRows = layout.Rows;
        IReadOnlyList<DataContext> dataRows =
            Mode == TableMode.Dynamic ? ctx.Data?.GetRows(DataSource ?? "") ?? [] : [];

        // Clip na hranice elementu - chrání před přesahem, pokud součet šířek sloupců
        // přesahuje Width (nekonzistentní konfigurace). Konzistentní s ostatními tabulkovými elementy.
        ctx.Canvas.Save();
        ctx.Canvas.ClipRect(tableRect);
        try
        {
            // Plán řádků (R1) - element tvaruje data, kreslí sdílená smyčka.
            var plan = new List<RowSpec>();
            if (ShowHeader)
                plan.Add(HeaderRow(cols, colWidths, headerStyles, sepPaint, headerH));
            plan.AddRange(Mode == TableMode.Fixed
                ? FixedRowSpecs(ctx, cols, colWidths, dataStyles, sepPaint, fixedRows, layout.RowHeights)
                : DynamicRowSpecs(ctx, cols, colWidths, dataStyles, sepPaint, dataRows, rowH));

            float currentY = RowPlanRenderer.DrawRows(ctx, X, Y, Width, plan, fontScale);

            if (SummaryRows is { Count: > 0 })
            {
                // Oddělovač bloku souhrnů se kreslí PŘED pozadím prvního souhrnného řádku - opačné
                // pořadí než u řádkových čar, proto zůstává tady a nejde do plánu.
                ctx.Canvas.DrawLine(ctx.ToCanvas(X, currentY), ctx.ToCanvas(X + Width, currentY), sepPaint);

                RowPlanRenderer.DrawRows(ctx, X, currentY, Width,
                    SummaryRows.Select(r => SummaryRow(ctx, cols, colWidths, r, dataRows, rowH, sepPaint)),
                    fontScale);
            }
        }
        finally
        {
            ctx.Canvas.Restore();
        }
    }

    private static List<TableLayout.ColWidth> WidthProjection(List<TableColumn> cols) =>
        [.. cols.Select(c => new TableLayout.ColWidth(c.Width <= 0 ? null : c.Width, c.WidthMode))];

    /// <summary>
    /// Rozměry mřížky v mm relativně k prvku. **Jediný zdroj** pro kreslení i pro trefování myší -
    /// kdyby si je editace na plátně počítala po svém, klikalo by se vedle při každém zmenšení
    /// nebo roztažení tabulky.
    /// </summary>
    /// <param name="Rows">Viditelné řádky - **objekty z modelu**, takže se do nich dá rovnou psát.</param>
    /// <param name="ColumnIndexes">Index viditelného sloupce → index v <see cref="Columns"/>. Buňky
    /// v řádku jsou uložené podle modelu, takže bez tohohle by se u skryté ho sloupce psalo vedle.</param>
    /// <param name="RowHeights">Výška každého viditelného řádku v mm (už naškálovaná).</param>
    /// <param name="RowHeightMm">Společná výška - pro souhrnné řádky a pro režim „z dat".</param>
    public readonly record struct GridLayout(
        float[] ColumnWidths, float[] RowHeights, float RowHeightMm, float HeaderHeightMm,
        float FontScale, IReadOnlyList<TableRow> Rows, int[] ColumnIndexes);

    public GridLayout ComputeLayout(DataContext? data) =>
        ComputeLayout(data, [.. Columns.Where(c => c.Visible)]);

    private GridLayout ComputeLayout(DataContext? data, List<TableColumn> cols)
    {
        float[] colWidths = TableLayout.ResolveWidths(WidthProjection(cols), Width);
        var rows = VisibleFixedRows(data);

        int rowCount = Mode == TableMode.Fixed
            ? rows.Count
            : ((data?.GetRows(DataSource ?? "").Count ?? 0) is var n && n > 0 ? n : DesignerPreviewRows);
        float naturalH = Mode == TableMode.Fixed ? NaturalHeight(rows) : NaturalHeight(rowCount);
        float heightRatio = Height.HasValue && naturalH > 0 ? Height.Value / naturalH : 1f;
        float widthScale  = TableLayout.ComputeWidthScale(WidthProjection(cols), Width);

        // Písmo se jen **zmenšuje**, aby se obsah vešel (jako StopList/DepartureGrid/DynamicTable);
        // nikdy se nezvětšuje - vyšší tabulka má mít vyšší řádky, ne větší text.
        float fontScale = MathF.Min(1f, MathF.Min(heightRatio, widthScale));

        float[] rowHeights = ResolveRowHeights(rows, rowCount, fontScale);
        int[] columnIndexes = [.. cols.Select(c => Columns.IndexOf(c))];

        return new(colWidths, rowHeights, RowHeight * fontScale, HeaderHeight * fontScale,
                   fontScale, rows, columnIndexes);
    }

    /// <summary>
    /// Výšky řádků **stejnou mechanikou jako šířky sloupců** (<see cref="TableLayout.ResolveWidths"/>):
    /// řádek s vlastní výškou si ji drží, ostatní si rovným dílem rozdělí zbytek prvku. Tažení
    /// vodorovné hranice se tak chová přesně jako tažení svislé, jen kolmo - a celková výška prvku
    /// se přitom nemění.
    /// <para>
    /// ⚠️ Dřív se místo rozdělení celý obsah **škáloval** na výšku prvku. Tažení hranice se tím
    /// rozjelo samo proti sobě: vyšší řádek = vyšší obsah = větší zmenšení = jiné rozměry pod myší
    /// při dalším pohybu (nahlášeno 12. 8.).
    /// </para>
    /// Bez pevné výšky prvku se nerozděluje nic - výška je prostě součet řádků.
    /// </summary>
    private float[] ResolveRowHeights(IReadOnlyList<TableRow> rows, int rowCount, float fontScale)
    {
        if (Mode != TableMode.Fixed)
            return [.. Enumerable.Repeat(RowHeight * fontScale, rowCount)];

        if (!Height.HasValue)
            return [.. rows.Select(r => r.Height ?? RowHeight)];

        // Záhlaví i souhrny se kreslí zmenšené stejnou škálou jako text, takže se musí odečítat
        // zmenšené - jinak by řádkům zbylo méně, než kolik je reálně volného místa, a dole by
        // zůstal pruh bílé (nález revize 12. 8.).
        float proRadky = Height.Value
                         - (ShowHeader ? HeaderHeight * fontScale : 0f)
                         - (SummaryRows?.Count ?? 0) * RowHeight * fontScale;

        // Minimum i pro řádky bez vlastní výšky: bez něj je sdílené jádro stáhne na nulu, jakmile
        // samotné pevné výšky přerostou místo - a nulový řádek už nejde chytit myší.
        return TableLayout.ResolveWidths(
            [.. rows.Select(r => new TableLayout.ColWidth(r.Height, ColumnWidthMode.Mm))],
            MathF.Max(0f, proRadky), MinRowHeightMm);
    }

    // ─── Editace na plátně ───────────────────────────────────────────

    // ─── Sloučené buňky ──────────────────────────────────────────────

    /// <summary>
    /// Sloupec, který dané místo **vlastní**: u sloučené buňky je to ta její levá, jinak sloupec
    /// sám. Díky tomu klik doprostřed sloučeného nadpisu vybere nadpis, ne překrytou buňku pod ním.
    /// </summary>
    public int SpanAnchorColumn(DataContext? data, int row, int col)
    {
        var layout = ComputeLayout(data);
        if (row < 0 || row >= layout.Rows.Count || col <= 0) return Math.Max(0, col);

        // Zleva doprava se hledat nedá bez projití všech; stačí jít od kliknutého sloupce doleva
        // a vzít první, jehož sloučení sahá až sem. Překryté buňky mají span 1, takže nepřekáží.
        for (int i = col - 1; i >= 0; i--)
            if (i + SpanOf(layout, row, i) > col) return i;
        return col;
    }

    /// <summary>Přes kolik viditelných sloupců buňka sahá (nejméně 1, nejvýš do konce mřížky).</summary>
    public int SpanAt(DataContext? data, int row, int col)
    {
        var layout = ComputeLayout(data);
        if (row < 0 || row >= layout.Rows.Count || col < 0 || col >= layout.ColumnIndexes.Length) return 1;
        return SpanOf(layout, row, col);
    }

    private static int SpanOf(GridLayout layout, int row, int visibleCol)
    {
        if (visibleCol < 0 || visibleCol >= layout.ColumnIndexes.Length) return 1;
        var cells = layout.Rows[row].Cells;
        int modelCol = layout.ColumnIndexes[visibleCol];
        int span = modelCol < cells.Count ? cells[modelCol].ColSpan : 1;
        return Math.Clamp(span, 1, layout.ColumnIndexes.Length - visibleCol);
    }

    /// <summary>
    /// Sloučí buňky výběru - **po řádcích**, protože slučujeme jen vodorovně. Neprázdné obsahy se
    /// spojí mezerou, ať se žádný text tiše neztratí (kdo slučuje, dělá to obvykle na prázdné
    /// tabulce; tohle je pojistka). Vrací false, když není co slučovat.
    /// </summary>
    public bool MergeRange(DataContext? data, CellRange range)
    {
        if (range.ColumnCount < 2) return false;

        bool zmena = false;
        for (int row = range.Top; row <= range.Bottom; row++)
        {
            var texts = new List<string>();
            for (int col = range.Left; col <= range.Right; col++)
                if (EnsureCell(data, row, col) is { Content.Length: > 0 } cell) texts.Add(cell.Content);

            if (EnsureCell(data, row, range.Left) is not { } anchor) continue;
            anchor.Content = string.Join(" ", texts);
            anchor.ColSpan = range.ColumnCount;

            for (int col = range.Left + 1; col <= range.Right; col++)
                if (EnsureCell(data, row, col) is { } covered)
                {
                    covered.Content = "";
                    covered.ColSpan = 1;
                }
            zmena = true;
        }
        return zmena;
    }

    /// <summary>Rozdělí sloučené buňky ve výběru; obsah zůstává v té první (nedělí se zpátky -
    /// nešlo by poznat kam). Vrací false, když ve výběru žádná sloučená není.</summary>
    public bool SplitRange(DataContext? data, CellRange range)
    {
        bool zmena = false;
        foreach (var (row, col) in range.Cells())
            if (EnsureCell(data, row, col) is { ColSpan: > 1 } cell)
            {
                cell.ColSpan = 1;
                zmena = true;
            }
        return zmena;
    }

    /// <summary>Je ve výběru sloučená buňka? Řídí nabídku „Rozdělit buňky".</summary>
    public bool HasMergedCell(DataContext? data, CellRange range) =>
        range.Cells().Any(c => EnsureCell(data, c.Row, c.Col) is { ColSpan: > 1 });

    /// <summary>Obdélník buňky v mm **relativně k prvku** (0,0 = levý horní roh tabulky).
    /// Null = buňka mimo mřížku. Řádky i sloupce jsou indexované podle toho, co je vidět.
    /// U sloučené buňky je obdélník přes všechny její sloupce - a překrytá souřadnice vrací
    /// tentýž obdélník, takže se zvýrazní celý nadpis, ne kus pod kurzorem.</summary>
    public SKRect? CellRectMm(DataContext? data, int row, int col)
    {
        var layout = ComputeLayout(data);
        if (row < 0 || row >= layout.Rows.Count || col < 0 || col >= layout.ColumnWidths.Length) return null;

        int anchor = SpanAnchorColumn(data, row, col);
        int span   = SpanAt(data, row, anchor);

        float x = 0f;
        for (int i = 0; i < anchor; i++) x += layout.ColumnWidths[i];
        float width = 0f;
        for (int i = anchor; i < anchor + span && i < layout.ColumnWidths.Length; i++) width += layout.ColumnWidths[i];
        float y = ShowHeader ? layout.HeaderHeightMm : 0f;
        for (int i = 0; i < row; i++) y += layout.RowHeights[i];

        return new SKRect(x, y, x + width, y + layout.RowHeights[row]);
    }

    /// <summary>Viditelné řádky mřížky - **objekty z modelu**, takže se do nich dá rovnou psát
    /// (vlastnosti řádku). Řádek skrytý podmínkou mezi nimi není, stejně jako ho nevidí kreslení.</summary>
    public IReadOnlyList<TableRow> VisibleRows(DataContext? data) => VisibleFixedRows(data);

    /// <summary>Obdélník celého řádku v mm relativně k prvku - pro odznak podmíněného řádku
    /// na plátně. Null = řádek mimo mřížku.</summary>
    public SKRect? RowRectMm(DataContext? data, int row)
    {
        var layout = ComputeLayout(data);
        if (row < 0 || row >= layout.Rows.Count) return null;

        float y = ShowHeader ? layout.HeaderHeightMm : 0f;
        for (int i = 0; i < row; i++) y += layout.RowHeights[i];
        return new SKRect(0f, y, Width, y + layout.RowHeights[row]);
    }

    /// <summary>Řádky s podmínkou viditelnosti (index viditelného řádku + výraz) - plátno u nich
    /// kreslí týž odznak jako u podmíněného prvku, aby bylo poznat, že se nemusí vytisknout.</summary>
    public IEnumerable<(int Row, string Expression)> ConditionalRows(DataContext? data)
    {
        var rows = VisibleFixedRows(data);
        for (int i = 0; i < rows.Count; i++)
            if (rows[i].VisibilityExpression is { Length: > 0 } expression)
                yield return (i, expression);
    }

    /// <summary>Buňka pod bodem (mm relativně k prvku), nebo null mimo mřížku a v záhlaví.</summary>
    public (int Row, int Col)? HitTestCell(DataContext? data, float xMm, float yMm)
    {
        var layout = ComputeLayout(data);
        if (layout.Rows.Count == 0 || layout.ColumnWidths.Length == 0 || layout.RowHeightMm <= 0) return null;

        float top = ShowHeader ? layout.HeaderHeightMm : 0f;
        if (yMm < top) return null;

        // Řádky mohou mít každý jinou výšku, takže se prochází, ne dělí.
        int row = -1;
        float y = top;
        for (int i = 0; i < layout.RowHeights.Length; i++)
        {
            if (yMm >= y && yMm < y + layout.RowHeights[i]) { row = i; break; }
            y += layout.RowHeights[i];
        }
        if (row < 0 || row >= layout.Rows.Count) return null;

        float x = 0f;
        for (int col = 0; col < layout.ColumnWidths.Length; col++)
        {
            // Klik do překryté buňky patří té sloučené - jinak by uživatel „vybral" něco,
            // co na plátně vůbec nevidí.
            if (xMm >= x && xMm < x + layout.ColumnWidths[col]) return (row, SpanAnchorColumn(data, row, col));
            x += layout.ColumnWidths[col];
        }
        return null;
    }

    /// <summary>Nejmenší šířka sloupce v mm - pod ni tažení nepustí, jinak by sloupec zmizel
    /// a nešel chytit zpět.</summary>
    public const float MinColumnWidthMm = 3f;

    /// <summary>
    /// Sloupec, jehož **pravá hrana** je do tolerance od bodu - pro tažení šířky. Poslední (vnější)
    /// hrana se vědomě nenabízí: to je okraj prvku a mění se tažením za úchyt prvku.
    /// </summary>
    public int? HitTestColumnDivider(DataContext? data, float xMm, float toleranceMm)
    {
        var layout = ComputeLayout(data);
        float x = 0f;
        for (int i = 0; i < layout.ColumnWidths.Length - 1; i++)
        {
            x += layout.ColumnWidths[i];
            if (MathF.Abs(xMm - x) <= toleranceMm) return i;
        }
        return null;
    }

    /// <summary>
    /// Tažení svislé hranice: nastaví šířku sloupce **vlevo od ní** a zbytek dvojice dá sousedovi
    /// vpravo. Součet dvojice zůstává, takže se ostatní sloupce vůbec nehnou - hrana se chová jako
    /// v tabulkovém editoru, kde tažení mění právě ty dva sloupce, mezi kterými je.
    /// </summary>
    /// <param name="pairSumMm">Součet šířek dvojice zjištěný na začátku tažení.</param>
    public void SetColumnPair(DataContext? data, int divider, float leftWidthMm, float pairSumMm)
    {
        int leva  = ModelColumnIndex(data, divider);
        int prava = ModelColumnIndex(data, divider + 1);
        if (leva < 0 || prava < 0) return;

        float left = Math.Clamp(leftWidthMm, MinColumnWidthMm, pairSumMm - MinColumnWidthMm);
        SetColumnWidthMm(leva,  left);
        SetColumnWidthMm(prava, pairSumMm - left);
    }

    private void SetColumnWidthMm(int modelCol, float widthMm)
    {
        Columns[modelCol].Width     = widthMm;
        Columns[modelCol].WidthMode = ColumnWidthMode.Mm;
    }

    /// <summary>Nejmenší výška řádku v mm.</summary>
    public const float MinRowHeightMm = 2f;

    /// <summary>
    /// Řádek, jehož **dolní hrana** je do tolerance od bodu - pro tažení výšky. Poslední hrana
    /// se nenabízí ze stejného důvodu jako u sloupců: je to okraj prvku.
    /// </summary>
    public int? HitTestRowDivider(DataContext? data, float yMm, float toleranceMm)
    {
        var layout = ComputeLayout(data);
        float y = ShowHeader ? layout.HeaderHeightMm : 0f;
        for (int i = 0; i < layout.RowHeights.Length - 1; i++)
        {
            y += layout.RowHeights[i];
            if (MathF.Abs(yMm - y) <= toleranceMm) return i;
        }
        return null;
    }

    /// <summary>
    /// Tažení vodorovné hranice - kolmý protějšek <see cref="SetColumnPair"/>: mění se jen řádek
    /// nad hranou a jeho soused pod ní, ostatní zůstávají, kde byly.
    /// </summary>
    public void SetRowPair(DataContext? data, int divider, float topHeightMm, float pairSumMm)
    {
        var rows = VisibleFixedRows(data);
        if (divider < 0 || divider + 1 >= rows.Count) return;

        float horni = Math.Clamp(topHeightMm, MinRowHeightMm, pairSumMm - MinRowHeightMm);
        rows[divider].Height     = horni;
        rows[divider + 1].Height = pairSumMm - horni;
    }

    /// <summary>Rozměry mřížky pro pohyb po buňkách (Tab/Enter/šipky).</summary>
    public (int Rows, int Cols) GridSize(DataContext? data)
    {
        var layout = ComputeLayout(data);
        return (layout.Rows.Count, layout.ColumnWidths.Length);
    }

    /// <summary>
    /// Buňka k editaci; chybějící se doplní. Staré šablony mají řádky kratší než počet sloupců
    /// (buňky se dopisovaly jen tam, kde byl obsah), takže bez dorovnání by do posledních sloupců
    /// nešlo psát.
    /// </summary>
    public TableCell? EnsureCell(DataContext? data, int row, int col)
    {
        var layout = ComputeLayout(data);
        if (row < 0 || row >= layout.Rows.Count || col < 0 || col >= layout.ColumnIndexes.Length) return null;

        int modelCol = layout.ColumnIndexes[col];
        var tableRow = layout.Rows[row];
        while (tableRow.Cells.Count <= modelCol) tableRow.Cells.Add(new TableCell());
        return tableRow.Cells[modelCol];
    }

    /// <summary>
    /// Vyřešený rodičovský styl buňky = kaskáda sloupec → „Styl buněk" prvku → zapečené výchozí.
    /// Dialog buňky ho ukazuje jako zděděné hodnoty a render z něj vychází (<see cref="FixedRowSpecs"/>),
    /// takže formulář nemůže tvrdit jinou hodnotu, než se vytiskne.
    /// </summary>
    public ResolvedTextStyle ResolveCellParent(DataContext? data, int visibleCol)
    {
        var layout = ComputeLayout(data);
        if (visibleCol < 0 || visibleCol >= layout.ColumnIndexes.Length) return ResolveColumnDataParent();
        return ResolveColumnDataStyle(Columns[layout.ColumnIndexes[visibleCol]]);
    }

    /// <summary>Stav jedné buňky pro vratnou změnu - adresovaný souřadnicí, ne instancí. Nese
    /// i obsah a sloučení, aby jedním snímkem šlo vrátit i sloučení buněk (spojený text zpět).</summary>
    public readonly record struct CellState(
        int Row, int Col, TextStyle? Style, string? Format, string Content = "", int ColSpan = 1);

    /// <summary>
    /// Snímek formátování skupiny buněk pro undo. Buňky se adresují **souřadnicí**: každá změna
    /// struktury (vložení řádku, tažení hranice, vložení ze schránky) mřížku naklonuje, takže
    /// příkaz držící instanci buňky by po zpětném kroku psal do odpojeného objektu.
    /// </summary>
    public sealed record CellSnapshot(List<CellState> Cells);

    public CellSnapshot CaptureCells(DataContext? data, IEnumerable<(int Row, int Col)> cells) =>
        new([.. cells.Select(c =>
        {
            var cell = EnsureCell(data, c.Row, c.Col);
            return new CellState(c.Row, c.Col, cell?.Style?.Clone(), cell?.Format,
                cell?.Content ?? "", cell?.ColSpan ?? 1);
        })]);

    public void RestoreCells(DataContext? data, CellSnapshot snapshot)
    {
        foreach (var state in snapshot.Cells)
        {
            if (EnsureCell(data, state.Row, state.Col) is not { } cell) continue;
            cell.Style   = state.Style?.Clone();
            cell.Format  = state.Format;
            cell.Content = state.Content;
            cell.ColSpan = state.ColSpan;
        }
    }

    /// <summary>Index sloupce v modelu pro viditelný sloupec (vkládání a mazání sloupců).</summary>
    public int ModelColumnIndex(DataContext? data, int visibleCol)
    {
        var layout = ComputeLayout(data);
        return visibleCol >= 0 && visibleCol < layout.ColumnIndexes.Length
            ? layout.ColumnIndexes[visibleCol] : -1;
    }

    // ─── Struktura (řádky a sloupce) ─────────────────────────────────

    /// <summary>Snímek struktury pro undo - řádky, sloupce a výška v jednom.</summary>
    public sealed record StructureSnapshot(List<TableRow> Rows, List<TableColumn> Columns, float? Height);

    public StructureSnapshot CaptureStructure() =>
        new([.. (Rows ?? []).Select(r => r.Clone())], [.. Columns.Select(c => c.Clone())], Height);

    public void RestoreStructure(StructureSnapshot snapshot)
    {
        Rows    = [.. snapshot.Rows.Select(r => r.Clone())];
        Columns = [.. snapshot.Columns.Select(c => c.Clone())];
        Height  = snapshot.Height;
    }

    /// <summary>
    /// Vloží řádek na danou pozici (index = pozice v modelu). Tabulka se o řádek **zvětší**, pokud
    /// má pevnou výšku - jinak by přidání řádku zmenšilo obsah, což nikdo nečeká (roste jako ve Wordu).
    /// </summary>
    public void InsertRow(int index)
    {
        Rows ??= [];
        index = Math.Clamp(index, 0, Rows.Count);
        Rows.Insert(index, new TableRow { Cells = [.. Columns.Select(_ => new TableCell())] });
        if (Height.HasValue) Height += RowHeight;
    }

    /// <summary>Smaže řádek; poslední řádek nechá být, prázdná tabulka by se nedala vyplnit.</summary>
    public bool RemoveRow(int index)
    {
        if (Rows is null || Rows.Count <= 1 || index < 0 || index >= Rows.Count) return false;
        Rows.RemoveAt(index);
        if (Height.HasValue) Height = MathF.Max(RowHeight, Height.Value - RowHeight);
        return true;
    }

    /// <summary>Vloží sloupec; šířku dostane „auto", takže se místo rozdělí rovným dílem.</summary>
    public void InsertColumn(int index)
    {
        index = Math.Clamp(index, 0, Columns.Count);
        Columns.Insert(index, new TableColumn { Header = "", Width = 0f });
        foreach (var row in Rows ?? [])
        {
            while (row.Cells.Count < index) row.Cells.Add(new TableCell());
            row.Cells.Insert(index, new TableCell());
        }
    }

    /// <summary>
    /// Vloží blok textu (řádky × sloupce) od dané buňky a **tabulku dorovná**, když se blok
    /// nevejde. Bez dorovnání by se z vloženého bloku tiše ztratil přesah, což se pozná až
    /// při kontrole výsledku.
    /// </summary>
    public void PasteBlock(DataContext? data, int startRow, int startCol,
        IReadOnlyList<IReadOnlyList<string>> block)
    {
        if (block.Count == 0) return;
        Rows ??= [];

        // Indexy jsou **viditelné** (tak jak uživatel vidí mřížku), obsah se ale ukládá po modelu -
        // se skrytým sloupcem nebo řádkem by se bez převodu psalo vedle (nález revize 12. 8.).
        var viditelneRadky   = VisibleFixedRows(data);
        var viditelneSloupce = Columns.Where(c => c.Visible).ToList();

        while (viditelneRadky.Count < startRow + block.Count)
        {
            InsertRow(Rows.Count);
            viditelneRadky = VisibleFixedRows(data);
        }
        while (viditelneSloupce.Count < startCol + block.Max(r => r.Count))
        {
            InsertColumn(Columns.Count);
            viditelneSloupce = [.. Columns.Where(c => c.Visible)];
        }

        for (int r = 0; r < block.Count; r++)
            for (int c = 0; c < block[r].Count; c++)
            {
                var row = viditelneRadky[startRow + r];
                int modelCol = Columns.IndexOf(viditelneSloupce[startCol + c]);
                while (row.Cells.Count <= modelCol) row.Cells.Add(new TableCell());
                row.Cells[modelCol].Content = block[r][c];
            }
    }

    /// <summary>
    /// Obsah výběru ve tvaru, kterému rozumí Excel a Sheets (řádky konci řádku, buňky tabulátorem) -
    /// tedy přesně to, co se z nich vkládá zpátky. Prázdná buňka je prázdné pole, ne vynechané.
    /// </summary>
    public string CopyRangeToText(DataContext? data, CellRange range)
    {
        var lines = new List<string>();
        for (int row = range.Top; row <= range.Bottom; row++)
        {
            var cells = new List<string>();
            for (int col = range.Left; col <= range.Right; col++)
                cells.Add(EnsureCell(data, row, col)?.Content ?? "");
            lines.Add(string.Join("\t", cells));
        }
        return string.Join("\r\n", lines);
    }

    /// <summary>Vyplní celý výběr jednou hodnotou - vložení jediné hodnoty do označené oblasti
    /// se v Excelu chová takhle a je to jediný způsob, jak jedním krokem srovnat víc buněk.</summary>
    public void FillRange(DataContext? data, CellRange range, string content)
    {
        foreach (var (row, col) in range.Cells())
            if (EnsureCell(data, row, col) is { } cell) cell.Content = content;
    }

    /// <summary>Vymaže obsah buněk výběru; styl ani formát se nemažou (na to je „Resetovat styl").</summary>
    public bool ClearRangeContent(DataContext? data, CellRange range)
    {
        bool zmena = false;
        foreach (var (row, col) in range.Cells())
            if (EnsureCell(data, row, col) is { Content.Length: > 0 } cell)
            {
                cell.Content = "";
                zmena = true;
            }
        return zmena;
    }

    /// <summary>
    /// Rozseká text ze schránky na buňky. Excel a Sheets dávají do schránky **řádky oddělené
    /// koncem řádku a buňky tabulátorem**, takže stačí tohle - uvozovkování hodnot s vnořeným
    /// koncem řádku vědomě neřešíme (v tabulkách tohoto typu se nevyskytuje).
    /// </summary>
    public static List<List<string>> ParseClipboardBlock(string text)
    {
        var radky = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();
        while (radky.Count > 0 && radky[^1].Length == 0) radky.RemoveAt(radky.Count - 1);
        return [.. radky.Select(r => r.Split('\t').ToList())];
    }

    /// <summary>Smaže sloupec i jeho buňky; poslední sloupec nechá být.</summary>
    public bool RemoveColumn(int index)
    {
        if (Columns.Count <= 1 || index < 0 || index >= Columns.Count) return false;
        Columns.RemoveAt(index);
        foreach (var row in Rows ?? [])
            if (index < row.Cells.Count) row.Cells.RemoveAt(index);
        return true;
    }

    /// <summary>Pero svislých čar mezi sloupci; null = bez čar. Poslední (vnější) čáru DrawRow
    /// nekreslí - dřív ji tabulka kreslila a zdvojovala tím vnější rámeček elementu.</summary>
    private SKPaint? ColumnGridPaint(SKPaint sepPaint) => Grid.ShowColumnLines ? sepPaint : null;

    /// <summary>Řádek záhlaví - pozadí z kaskády, dolní hrana je strukturální (kreslí se vždy).</summary>
    private RowSpec HeaderRow(List<TableColumn> cols, float[] colWidths,
        ResolvedTextStyle[] headerStyles, SKPaint sepPaint, float headerH)
    {
        var specs = new ColumnRenderer.CellSpec[cols.Count];
        for (int i = 0; i < cols.Count; i++)
            specs[i] = new(colWidths[i], Rendering.ColumnHeader.Resolve(cols[i].Header, ""), headerStyles[i]);

        return new(RowKind.Header, headerH, specs)
        {
            Background        = ResolveColumnHeaderParent().ParsedBackgroundColor,
            VerticalLinePaint = ColumnGridPaint(sepPaint),
            LineBelowPaint    = sepPaint,
        };
    }

    /// <summary>Ručně vyplněné řádky: text buňky = obsah s vyhodnocenými {POLE}, styl v kaskádě
    /// buňka → sloupec → element.</summary>
    private IEnumerable<RowSpec> FixedRowSpecs(RenderContext ctx, List<TableColumn> cols, float[] colWidths,
        ResolvedTextStyle[] dataStyles, SKPaint sepPaint, IReadOnlyList<TableRow> rows, float[] rowHeights)
    {
        SKColor? altColor = ColorHex.ParseOrNull(AlternatingRowColor);
        var gridPaint    = ColumnGridPaint(sepPaint);
        var rowLinePaint = Grid.ShowRowLines ? sepPaint : null;

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];

            // Sloučená buňka je **jedna** buňka široká přes své sloupce; překryté se přeskočí.
            // Kreslicí smyčka se tím nemění - dostane prostě méně širších buněk.
            var specs = new List<ColumnRenderer.CellSpec>(cols.Count);
            for (int ci = 0; ci < cols.Count; ci++)
            {
                var cellStyle = dataStyles[ci];
                string content = "";
                int span = 1;
                if (ci < row.Cells.Count)
                {
                    var cell = row.Cells[ci];
                    span = Math.Clamp(cell.ColSpan, 1, cols.Count - ci);
                    content = ctx.Data?.ResolvePlaceholders(cell.Content) ?? cell.Content;
                    var cellFormat = cell.Format ?? cols[ci].Format;
                    if (cellFormat is not null)
                        content = DataContext.FormatValue(content, cellFormat, ctx.Data?.Culture);

                    if (cell.Style is { } cs) cellStyle = cs.Resolve(dataStyles[ci]);
                }

                float width = 0f;
                for (int k = ci; k < ci + span; k++) width += colWidths[k];
                specs.Add(new(width, content, cellStyle));
                ci += span - 1;
            }

            // Vlastní pozadí řádku přebíjí střídání řádků - dřív se kreslila obě přes sebe, takže
            // průhledná vlastní barva propouštěla zebru pod sebou.
            yield return new(RowKind.Data, rowHeights[i], specs)
            {
                Background = ColorHex.ParseOrNull(row.BackgroundColor)
                             ?? (altColor.HasValue && i % 2 == 1 ? altColor : null),
                VerticalLinePaint = gridPaint,
                LineBelowPaint    = rowLinePaint,
            };
        }
    }

    /// <summary>Řádky z dat - jen pro staré šablony, které mají tenhle prvek v režimu Dynamic
    /// (nové dynamické tabulky dělá <see cref="DynamicTableElement"/>).</summary>
    private IEnumerable<RowSpec> DynamicRowSpecs(RenderContext ctx, List<TableColumn> cols, float[] colWidths,
        ResolvedTextStyle[] dataStyles, SKPaint sepPaint, IReadOnlyList<DataContext> rows, float rowH)
    {
        bool hasRows = rows.Count > 0;
        int renderCount = hasRows ? rows.Count : DesignerPreviewRows;

        SKColor? altColor = ColorHex.ParseOrNull(AlternatingRowColor);
        var gridPaint    = ColumnGridPaint(sepPaint);
        var rowLinePaint = Grid.ShowRowLines ? sepPaint : null;

        // Placeholder styl pro designer - šedivý text
        ResolvedTextStyle[] effectiveStyles = hasRows
            ? dataStyles
            : [.. dataStyles.Select(s => s with { TextColor = "#FF999999" })];

        for (int i = 0; i < renderCount; i++)
        {
            var specs = new ColumnRenderer.CellSpec[cols.Count];
            for (int ci = 0; ci < cols.Count; ci++)
            {
                var col = cols[ci];
                string sample;
                if (hasRows)
                {
                    sample = rows[i].GetString(col.Field ?? "");
                    if (col.Format is not null)
                        sample = DataContext.FormatValue(sample, col.Format, ctx.Data?.Culture);
                }
                else
                {
                    sample = $"{{{col.Field ?? col.Header}}}";
                }
                specs[ci] = new(colWidths[ci], sample, effectiveStyles[ci]);
            }

            yield return new(RowKind.Data, rowH, specs)
            {
                Background = altColor.HasValue && i % 2 == 1 ? altColor : null,
                VerticalLinePaint = gridPaint,
                LineBelowPaint    = rowLinePaint,
            };
        }
    }

    /// <summary>Souhrnný řádek - obsah přes agregace (SUM/COUNT/…) nad datovými řádky.</summary>
    private RowSpec SummaryRow(RenderContext ctx, List<TableColumn> cols, float[] colWidths,
        TableRow row, IReadOnlyList<DataContext> dataRows, float rowH, SKPaint sepPaint)
    {
        var specs = new ColumnRenderer.CellSpec[cols.Count];
        for (int colIdx = 0; colIdx < cols.Count; colIdx++)
        {
            var col = cols[colIdx];
            var cellStyle = TableStyleDefaults.Summary;
            string content = "";

            if (colIdx < row.Cells.Count)
            {
                var cell = row.Cells[colIdx];
                content = AggregationResolver.Resolve(cell.Content, dataRows);
                content = ctx.Data?.ResolvePlaceholders(content) ?? content;
                var cellFormat = cell.Format ?? col.Format;
                if (cellFormat is not null)
                    content = DataContext.FormatValue(content, cellFormat, ctx.Data?.Culture);

                if (cell.Style is { } cs) cellStyle = cs.Resolve(TableStyleDefaults.Summary);

                // Legacy col.Alignment fallback, jen pokud cell.Style neurčil HAlign
                if (cell.Style?.HAlign is null)
                    cellStyle = cellStyle with { HAlign = col.Alignment };
            }
            else
            {
                cellStyle = cellStyle with { HAlign = col.Alignment };
            }

            specs[colIdx] = new(colWidths[colIdx], content, cellStyle);
        }

        return new(RowKind.Summary, rowH, specs)
        {
            Background        = ColorHex.Parse(row.BackgroundColor, TableStyleDefaults.Summary.ParsedBackgroundColor),
            VerticalLinePaint = ColumnGridPaint(sepPaint),
            LineBelowPaint    = Grid.ShowRowLines ? sepPaint : null,
        };
    }

    // ─── PropertyDescriptors ─────────────────────────────────────────

    /// <summary>Lazy init helper - HeaderStyle/DataStyle jsou nullable, setter vždy potřebuje non-null instanci.</summary>
    private TextStyle Header() => HeaderStyle ??= new TextStyle();
    private TextStyle Data()   => DataStyle   ??= new TextStyle();

    public override IReadOnlyList<PropertyDescriptor> GetPropertyDescriptors() =>
    [
        // Tlačítko „Upravit sloupce" tu vědomě NENÍ (rozhodnutí Jan 12. 8.): sloupce, řádky
        // i obsah se dělají na plátně (Alt+klik do buňky, pravý klik na řádky a sloupce).
        // Druhá cesta k témuž by se s tou první nutně rozešla.
        // Přepínač režimu tu vědomě NENÍ: dynamickou tabulku dělá DynamicTableElement, a přepnutí
        // by uživatele dostalo do stavu, ze kterého se nedostane zpět. Zdroj dat zůstává viditelný
        // jen u starých šablon, které tenhle prvek v režimu Dynamic mají.
        .. Mode == TableMode.Dynamic
            ? (PropertyDescriptor[])[new StringPropertyDescriptor
            {
                Label = "Zdroj dat",
                Group = "Tabulka",
                Get   = () => DataSource ?? "",
                Set   = v => DataSource = string.IsNullOrWhiteSpace(v) ? null : v
            }]
            : [],
        new FloatPropertyDescriptor
        {
            Label = "Výška řádku",
            Group = "Tabulka",
            Get   = () => RowHeight,
            Set   = v => RowHeight = v,
            Min   = 2, Max = 50, Unit = "mm"
        },
        new BoolPropertyDescriptor
        {
            Label = "Záhlaví",
            Group = "Tabulka",
            Get   = () => ShowHeader,
            Set   = v => ShowHeader = v
        },
        new FloatPropertyDescriptor
        {
            Label = "Výška záhlaví",
            Group = "Tabulka",
            Get   = () => HeaderHeight,
            Set   = v => HeaderHeight = v,
            Min   = 2, Max = 50, Unit = "mm"
        },
        .. GetGridDescriptors(Grid),
        new ColorPropertyDescriptor
        {
            Label = "Střídání řádků",
            Group = "Tabulka",
            Get   = () => AlternatingRowColor ?? "#00FFFFFF",
            Set   = v => AlternatingRowColor = v == "#00FFFFFF" ? null : v
        },

        // ── Záhlaví / Datové buňky - element-level základní barvy.
        // Detailní styl (font, zarovnání, vnitřní okraje) je ve formuláři TableElementEdit.cls.
        new BoolPropertyDescriptor
        {
            Label = "Tučné záhlaví",
            Group = "Záhlaví",
            Get   = () => HeaderStyle?.Bold ?? true,
            Set   = v => Header().Bold = v
        },
        new ColorPropertyDescriptor
        {
            Label = "Pozadí záhlaví",
            Group = "Záhlaví",
            Get   = () => HeaderStyle?.BackgroundColor ?? "#FFE8E8E8",
            Set   = v => Header().BackgroundColor = v
        },
        new ColorPropertyDescriptor
        {
            Label = "Barva textu záhlaví",
            Group = "Záhlaví",
            Get   = () => HeaderStyle?.TextColor ?? "#FF333333",
            Set   = v => Header().TextColor = v
        },
        new ColorPropertyDescriptor
        {
            Label = "Pozadí buněk",
            Group = "Datové buňky",
            Get   = () => DataStyle?.BackgroundColor ?? "#00FFFFFF",
            Set   = v => Data().BackgroundColor = v
        },
        new ColorPropertyDescriptor
        {
            Label = "Barva textu buněk",
            Group = "Datové buňky",
            Get   = () => DataStyle?.TextColor ?? "#FF222222",
            Set   = v => Data().TextColor = v
        },

        .. GetPositionDescriptors()
    ];

    // ─── Scale ───────────────────────────────────────────────────────

    public override void ApplyScale(float scale)
    {
        base.ApplyScale(scale);
        RowHeight *= scale;
        HeaderHeight *= scale;
        HeaderStyle?.ApplyScale(scale);   // element-level font/padding (dřív se neškálovalo)
        DataStyle?.ApplyScale(scale);
        foreach (var col in Columns) col.ApplyScale(scale);   // šířka mm + per-sloupcové styly
        // Fixed mód: per-buňkové styly v řádcích i souhrnech
        foreach (var cell in (Rows ?? []).Concat(SummaryRows ?? []).SelectMany(r => r.Cells))
            cell.Style?.ApplyScale(scale);
    }

    // ─── Clone ───────────────────────────────────────────────────────

    public override TemplateElement Clone()
    {
        var clone = new TableElement
        {
            Mode                = Mode,
            DataSource          = DataSource,
            Columns             = Columns.Select(c => c.Clone()).ToList(),
            Rows                = Rows?.Select(r => r.Clone()).ToList(),
            SummaryRows         = SummaryRows?.Select(r => r.Clone()).ToList(),
            RowHeight           = RowHeight,
            ShowHeader          = ShowHeader,
            HeaderHeight        = HeaderHeight,
            HeaderStyle         = HeaderStyle?.Clone(),
            DataStyle           = DataStyle?.Clone(),
            Grid                = Grid.Clone(),
            AlternatingRowColor = AlternatingRowColor,
        };
        CopyBaseTo(clone);
        return clone;
    }

    /// <summary>Výška z dat - aby rámeček, pozadí i ořez rohů obepnuly celý prvek, ne jen plochu
    /// z návrháře.</summary>
    public override float RenderedHeightMm(RenderContext ctx) =>
        Height ?? (ctx.Data is not null ? CalculateHeightFromData(ctx.Data) : EffectiveDesignerHeight);
}
