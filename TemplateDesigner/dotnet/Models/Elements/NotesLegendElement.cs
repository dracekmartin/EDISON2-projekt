using SkiaSharp;

namespace TemplateDesigner.Models.Elements;

/// <summary>
/// Legenda poznámek pod mřížkou odjezdů.
/// Zobrazuje: negativní značky (invertované barvy), speciální znaky, textové poznámky.
/// Height = null → dynamická výška; Height = pevná → auto-scale fontu při přetečení.
/// Uživatel umístí na canvas pod StopListElement/DepartureGridElement.
/// </summary>
public class NotesLegendElement : StopScheduleElementBase
{
    // ═══════════════════════════════════════════
    // Vizuální vlastnosti
    // ═══════════════════════════════════════════
    public string HeaderText { get; set; } = "Poznámky";
    public string NoteSeparator { get; set; } = " - ";
    public float NoteFontSize { get; set; } = 7f;
    public float NoteLineHeight { get; set; } = 4f;

    // ═══════════════════════════════════════════
    // Rozložení legendy (sloupce / řazení / zarovnání markeru)
    // ═══════════════════════════════════════════
    /// <summary>Počet sloupců legendy (1 = jednosloupcová, výchozí). Legacy „Počet sloupců".</summary>
    public int Columns { get; set; } = 1;
    /// <summary>Směr plnění při >1 sloupci: po sloupcích (výchozí, jako noviny) / po řádcích.</summary>
    public LegendWrap WrapSmer { get; set; } = LegendWrap.PoSloupcich;
    /// <summary>Pevná šířka levé buňky se znakem (mm) → svisle zarovnané markery. 0 = inline (výchozí, jako dnes).</summary>
    public float MarkerSirka { get; set; } = 0f;
    /// <summary>Řazení poznámek: výchozí (pořadí z dat) / podle délky textu. Legacy „Uspořádat dle délky".</summary>
    public LegendRazeni Razeni { get; set; } = LegendRazeni.DleTypu;

    // ═══════════════════════════════════════════
    // Negativní značky
    // ═══════════════════════════════════════════
    public string NegativniZnackaBackColor { get; set; } = "#FF000000";
    public string NegativniZnackaForeColor { get; set; } = "#FFFFFFFF";

    // ═══════════════════════════════════════════
    // Filtrování
    // ═══════════════════════════════════════════
    public bool ShowNegativniZnacky { get; set; } = true;
    public bool ShowSpecialniZnaky { get; set; } = true;
    public bool ShowTextovePoznamky { get; set; } = true;

    /// <summary>
    /// Jak blok zachází s **výlukovými** poznámkami linky (Caché kategorie „vyluka"). Klient je chce mít
    /// na vývěsce zvlášť od ostatních (#13093), takže si položí dva bloky: v prvním „Bez výluk", ve druhém
    /// „Jen výluky". Výchozní „Včetně" = dosavadní chování (jeden blok se vším).
    /// </summary>
    public VylukyFiltr Vyluky { get; set; } = VylukyFiltr.Vcetne;

    /// <summary>
    /// Kategorie poznámek zadaných u linky, které blok bere (#13093 bod 1: „příp. filtrovat Poznámky 1,
    /// Poznámky 2?"). Klient je chce rozložit do víc bloků, protože mají jiné rozložení sloupců než
    /// seznam značek - dlouhé odstavce vs. krátké řádky „značka + text".
    ///
    /// Výchozí vše zapnuté = dosavadní chování. Neplést s <see cref="ShowTextovePoznamky"/>, které
    /// vypne **všechny** textové poznámky včetně ručních komentářů a rozdílů tras; tyto tři vybírají
    /// jen mezi poznámkami linky. Výluky mají vlastní volbu (<see cref="Vyluky"/>), protože se
    /// nefiltrují podle kategorie zdroje, ale podle prefixu textu.
    /// </summary>
    public bool ShowPoznamkyLinkyTarifni { get; set; } = true;

    /// <inheritdoc cref="ShowPoznamkyLinkyTarifni"/>
    public bool ShowPoznamkyLinkyVseobecne { get; set; } = true;

    /// <inheritdoc cref="ShowPoznamkyLinkyTarifni"/>
    public bool ShowPoznamkyLinkyRadiobus { get; set; } = true;

    // ─── Designer ───────────────────────────────────────────────────────────

    protected override float DesignerPlaceholderHeight => 25f;

    // ─── Render ─────────────────────────────────────────────────────────────

    public override void Render(RenderContext ctx)
    {
        if (Width <= 0) return;

        var poznamky = BuildPoznamky(ctx.Data);
        var filtered = FilterPoznamky(poznamky);
        bool hasData = filtered.Count > 0;

        // Auto-scale: pokud Height je pevné a obsah přetéká, zmenšit font
        float scale = hasData ? ComputeFontScale(CalculateHeightFromData(ctx.Data)) : 1f;

        float effHeaderFontSize = HeaderStyle.FontSize ?? 9f;
        // Kaskáda místo ruční rodiny: nese i řez písma (váha / kurzíva), který ruční cesta dřív zahazovala.
        var dataResolved = DataStyle.Resolve(ScheduleDataDefaults);
        // Font záhlaví je samostatný; fallback na font poznámek (pak dokumentový default).
        var headerResolved = HeaderStyle.FontFamily is null && DataStyle.FontFamily is not null
            ? HeaderStyle.Resolve(ScheduleHeaderDefaults) with { FontFamily = dataResolved.FontFamily }
            : HeaderStyle.Resolve(ScheduleHeaderDefaults);
        // Zvýrazněný text poznámky (značka / symbol) - tučně jen jako default; explicitní volba
        // uživatele v DataStyle (vč. vypnutého tučného) má přednost (anti-pattern scalar override).
        var boldResolved = DataStyle.Bold is null ? dataResolved with { Bold = true } : dataResolved;

        float noteFontPx = NoteFontSize * scale * PtToMm * ctx.MmToPx;
        float headerFontPx = effHeaderFontSize * scale * PtToMm * ctx.MmToPx;
        float headerH = ComputeHeaderH(effHeaderFontSize * scale);
        float scaledNoteLineH = NoteLineHeight * scale;

        using var paints = CreateStandardPaints(ctx);
        // Barva textu poznámek = „Barva textu buněk" (DataStyle) - legenda kreslí text přímo tímto
        // paintem (ne přes CellRenderer), takže sdílenou natvrdo-barvu ze SchedulePaints přebíjíme.
        paints.Text.Color = ColorHex.Parse(DataStyle.TextColor, ScheduleDataDefaults.TextColor);

        using var noteFont = new SKFont(GetTypeface(dataResolved), noteFontPx);
        using var headerFont = new SKFont(GetTypeface(headerResolved), headerFontPx);
        using var boldFont = new SKFont(GetTypeface(boldResolved), noteFontPx);
        // Symbolový JŘ font pro jednoznakové piktogramy (H=vozíček, F, x, {…) - dle jazyka šablony.
        using var symbolFont = new SKFont(GetTypeface(FontResolver.SymbolFontFamily(ctx.Data?.Culture)), noteFontPx);

        // Záhlaví
        float currentY = RenderSectionHeader(ctx, HeaderText,
            X, Y, Width, headerH, headerFont, paints.HeaderBg, paints.HeaderText, paints.Grid);

        if (!hasData)
        {
            // Placeholder se ukazuje jen v designeru (bez DataContext) - print s prázdnými
            // poznámkami nesmí vykreslovat fake řádky.
            if (ctx.Data is null)
            {
                paints.Text.Color = new SKColor(0xFF999999);
                for (int i = 0; i < 3; i++)
                {
                    var pt = ctx.ToCanvas(X + 1f, currentY + NoteLineHeight * 0.75f);
                    ctx.Canvas.DrawText($"Poznámka {i + 1}", pt.X, pt.Y, noteFont, paints.Text);
                    currentY += NoteLineHeight;
                }
            }
        }
        else
        {
            var ordered = OrderNotes(filtered);
            int cols = Math.Max(1, Columns);
            int rowsPerCol = Math.Max(1, (int)Math.Ceiling(ordered.Count / (double)cols));
            float colW = Width / cols;
            float markerCell = MarkerSirka * scale;   // 0 = inline (jako dnes)
            float headerBottom = currentY;

            // Y kurzor per sloupec - poznámky mohou být víceřádkové (text se zalamuje na šířku
            // sloupce), proto výšku akumulujeme místo pevné mřížky řádků.
            var colY = new float[cols];
            for (int c = 0; c < cols; c++) colY[c] = headerBottom;

            for (int i = 0; i < ordered.Count; i++)
            {
                var pozn = ordered[i];

                // Přiřazení do sloupce dle směru plnění.
                int col = WrapSmer == LegendWrap.PoSloupcich
                    ? Math.Min(cols - 1, i / rowsPerCol)
                    : i % cols;

                float colX     = X + col * colW;
                float colRight = colX + colW;
                float noteY    = colY[col];
                float lineX    = colX + 1f;
                // Pevná marker buňka → text všech řádků zarovnán na colX+markerCell; jinak inline.
                float? textStartX = markerCell > 0 ? colX + markerCell : null;

                int lines = pozn.Typ switch
                {
                    TypPoznamky.NegativniZnacka =>
                        RenderNegativniZnacka(ctx, pozn, ref lineX, noteY, scaledNoteLineH, noteFont, boldFont, paints.Text, colRight, textStartX),
                    TypPoznamky.SpecialniZnak =>
                        RenderSymbolWithText(ctx, pozn, ref lineX, noteY, scaledNoteLineH,
                            JePiktogram(pozn.Znak) ? symbolFont : boldFont, noteFont, paints.Text, false, colRight, textStartX,
                            JePiktogram(pozn.Znak)),
                    _ when pozn.Znak.Length > 0 =>
                        RenderSymbolWithText(ctx, pozn, ref lineX, noteY, scaledNoteLineH, noteFont, noteFont, paints.Text, true, colRight, textStartX,
                            piktogram: false),
                    _ =>
                        DrawWrappedBody(ctx, pozn.Text, textStartX ?? lineX, noteY, colRight, noteFont, paints.Text, scaledNoteLineH),
                };

                colY[col] = noteY + Math.Max(1, lines) * scaledNoteLineH;
            }
        }

        // Outer border je řízen TemplateElement.BorderColor/BorderWidth (druhý průchod);
        // sjednocená cesta s ostatními elementy.
    }

    private int RenderNegativniZnacka(RenderContext ctx, PoznamkaData pozn,
        ref float lineX, float currentY, float lineH, SKFont noteFont, SKFont boldFont, SKPaint textPaint,
        float colRight, float? textStartX = null)
    {
        var nzBgColor = ColorHex.ParseOrBlack(NegativniZnackaBackColor);
        var nzFgColor = ColorHex.Parse(NegativniZnackaForeColor, "#FFFFFFFF");

        // Invertovaný obdélník se znakem
        float znakW = boldFont.MeasureText(pozn.Znak) / ctx.MmToPx + 1.5f;
        float boxH = lineH * 0.85f;
        float boxY = currentY + (lineH - boxH) / 2f;

        using var nzBgPaint = new SKPaint { Color = nzBgColor };
        var boxRect = ctx.ToCanvasRect(lineX, boxY, znakW, boxH);
        ctx.Canvas.DrawRect(boxRect, nzBgPaint);

        using var nzFgPaint = new SKPaint { Color = nzFgColor, IsAntialias = true };
        var znakPt = ctx.ToCanvas(lineX + 0.75f, currentY + lineH * 0.75f);
        ctx.Canvas.DrawText(pozn.Znak, znakPt.X, znakPt.Y, boldFont, nzFgPaint);

        lineX += znakW + 1f;

        // Text za značkou - zalomený na šířku sloupce; zarovnaný na marker buňku (textStartX) nebo za znakem.
        float textX = textStartX ?? lineX;
        return DrawWrappedBody(ctx, pozn.Text, textX, currentY, colRight, noteFont, textPaint, lineH);
    }

    /// <summary>
    /// Vykreslí symbol (znak) ve volitelné barvě z DB (<c>pozn.ForeColor</c>),
    /// následovaný textem v default barvě (<c>textPaint</c>).
    /// <paramref name="suffixSeparator"/>=true → mezi symbolem a textem se vloží <c>NoteSeparator</c>.
    /// BackColor zatím **ignorován** - rezervováno pro budoucí rozšíření (klient zatím chce jen barvu znaku).
    /// </summary>
    private int RenderSymbolWithText(RenderContext ctx, PoznamkaData pozn,
        ref float lineX, float currentY, float lineH,
        SKFont symbolFont, SKFont textFont, SKPaint textPaint, bool suffixSeparator,
        float colRight, float? textStartX = null, bool piktogram = false)
    {
        // Symbol - pokud má pozn.ForeColor z DB, použij ji; jinak default textPaint barva.
        SKPaint symbolPaint = textPaint;
        SKPaint? customSymbolPaint = null;
        if (!string.IsNullOrEmpty(pozn.ForeColor))
        {
            customSymbolPaint = new SKPaint { Color = ColorHex.ParseOrBlack(pozn.ForeColor), IsAntialias = true };
            symbolPaint = customSymbolPaint;
        }

        try
        {
            var znakPt = ctx.ToCanvas(lineX, currentY + lineH * 0.75f);
            // Znak legendy musí být týž glyf jako značka u zastávky - u piktogramů se pozice ve
            // symbolovém fontu neshoduje s písmenem číselníku (autobus „A" → „z", kolo „K" → „L").
            // Jen u piktogramů: značka kreslená normálním fontem (ruční komentáře) je písmeno,
            // které si zákazník zadal, a překlad by z „A" udělal „z".
            string znak = piktogram ? SymbolGlyph(pozn.Znak) : pozn.Znak;
            ctx.Canvas.DrawText(znak, znakPt.X, znakPt.Y, symbolFont, symbolPaint);
            lineX += symbolFont.MeasureText(znak) / ctx.MmToPx + 1.5f;

            // Text - vždy default barva (klient výslovně chce barvit jen znak).
            // Při pevné marker buňce (textStartX) text zarovnán; jinak hned za znakem (bez separátoru).
            float textX = textStartX ?? lineX;
            string displayText = (suffixSeparator && textStartX is null) ? $"{NoteSeparator}{pozn.Text}" : pozn.Text;
            return DrawWrappedBody(ctx, displayText, textX, currentY, colRight, textFont, textPaint, lineH);
        }
        finally
        {
            customSymbolPaint?.Dispose();
        }
    }

    /// <summary>
    /// Vykreslí tělo poznámky zalomené na šířku sloupce (od xMm po colRightMm). Pokračovací řádky
    /// začínají na stejném xMm (visutý odsazený blok). Vrací počet vykreslených řádků (min 1).
    /// </summary>
    private static int DrawWrappedBody(RenderContext ctx, string text, float xMm, float yMm, float colRightMm,
        SKFont font, SKPaint paint, float lineH)
    {
        if (string.IsNullOrEmpty(text)) return 1;
        float availMm = colRightMm - xMm - 0.5f;
        System.Collections.Generic.List<string> lines = availMm > 1f
            ? TextWrapHelper.WrapText(text, font, availMm * ctx.MmToPx)
            : [text];
        for (int k = 0; k < lines.Count; k++)
        {
            var pt = ctx.ToCanvas(xMm, yMm + lineH * 0.75f + k * lineH);
            ctx.Canvas.DrawText(lines[k], pt.X, pt.Y, font, paint);
        }
        return Math.Max(1, lines.Count);
    }

    // ─── Výpočet výšky ──────────────────────────────────────────────────────

    public override float CalculateHeightFromData(DataContext? data)
    {
        var poznamky = BuildPoznamky(data);
        var filtered = FilterPoznamky(poznamky);
        if (filtered.Count == 0) return DesignerPlaceholderHeight;

        // Výška = záhlaví + nejvyšší sloupec (poznámky se zalamují → akumulujeme řádky per sloupec).
        var ordered = OrderNotes(filtered);
        int cols = Math.Max(1, Columns);
        int rowsPerCol = Math.Max(1, (int)Math.Ceiling(ordered.Count / (double)cols));
        float colW = Width / cols;

        const float refMmToPx = 96f / 25.4f;
        // Měření musí trefit stejný font jako render (vč. řezu) - jinak se počet zalomených řádků rozejde.
        var typeface = GetTypeface(DataStyle.Resolve(ScheduleDataDefaults));
        using var font = new SKFont(typeface, NoteFontSize * ResolvedTextStyle.PtToMm * refMmToPx);
        float availMm = colW - (MarkerSirka > 0 ? MarkerSirka : 2f) - 0.5f;

        var colLines = new int[cols];
        for (int i = 0; i < ordered.Count; i++)
        {
            int col = WrapSmer == LegendWrap.PoSloupcich ? Math.Min(cols - 1, i / rowsPerCol) : i % cols;
            int lines = 1;
            var txt = ordered[i].Text;
            if (!string.IsNullOrEmpty(txt) && availMm > 1f)
                lines = Math.Max(1, TextWrapHelper.WrapText(txt, font, availMm * refMmToPx).Count);
            colLines[col] += lines;
        }
        int maxLines = 1;
        foreach (var n in colLines) if (n > maxLines) maxLines = n;
        return ComputeHeaderH(HeaderStyle.FontSize ?? 9f) + maxLines * NoteLineHeight;
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    /// <summary>Testovací vstup do filtru - render ho volá zároveň s daty, což se v testu neposkládá.</summary>
    internal List<PoznamkaData> FiltrujPoznamkyProTest(List<PoznamkaData> poznamky) => FilterPoznamky(poznamky);

    private List<PoznamkaData> FilterPoznamky(List<PoznamkaData> poznamky)
    {
        return poznamky.Where(p =>
            ((p.Typ == TypPoznamky.NegativniZnacka && ShowNegativniZnacky) ||
             (p.Typ == TypPoznamky.SpecialniZnak && ShowSpecialniZnaky) ||
             (p.Typ == TypPoznamky.Text && ShowTextovePoznamky))
            && VylukaProjde(p)
            && KategorieLinkyProjde(p)
        ).ToList();
    }

    /// <summary>Kategorie, kterou Caché dává výlukovým poznámkám linky (text začíná „Výluka").</summary>
    private const string VylukaKategorie = "vyluka";

    /// <summary>Kategorie poznámek zadaných u linky (Caché tagy dle klíče ve vlastnostech linky).</summary>
    private const string KatLinkyTarifni   = "poznamka-linky-tarifni";
    private const string KatLinkyVseobecne = "poznamka-linky-vseobecne";
    private const string KatLinkyRadiobus  = "poznamka-linky-radiobus";

    /// <summary>Pustí poznámku linky dle zaškrtnutých kategorií. Poznámky, které z linky nejsou
    /// (značky, omezení, ruční komentáře), tímhle filtrem projdou vždy - jinak by je vypnutá
    /// kategorie schovala jako vedlejší efekt.</summary>
    private bool KategorieLinkyProjde(PoznamkaData p) => p.Kategorie switch
    {
        KatLinkyTarifni   => ShowPoznamkyLinkyTarifni,
        KatLinkyVseobecne => ShowPoznamkyLinkyVseobecne,
        KatLinkyRadiobus  => ShowPoznamkyLinkyRadiobus,
        _                 => true,
    };

    /// <summary>Pustí poznámku dle <see cref="Vyluky"/>. Slouží k tomu, aby si uživatel složil dva bloky
    /// nad sebou: jeden bez výluk, druhý jen s nimi (KOVED #13093).</summary>
    private bool VylukaProjde(PoznamkaData p) => Vyluky switch
    {
        VylukyFiltr.Bez => p.Kategorie != VylukaKategorie,
        VylukyFiltr.Jen => p.Kategorie == VylukaKategorie,
        _               => true,
    };

    /// <summary>Řazení poznámek dle <see cref="Razeni"/> (stabilní - v rámci tieru/pořadí zachová pořadí z dat).</summary>
    private List<PoznamkaData> OrderNotes(List<PoznamkaData> notes) => Razeni switch
    {
        LegendRazeni.DleTypu           => notes.OrderBy(TypPriorita).ToList(),
        LegendRazeni.DleDelkyVzestupne => notes.OrderBy(p => p.Text.Length).ToList(),
        LegendRazeni.DleDelkySestupne  => notes.OrderByDescending(p => p.Text.Length).ToList(),
        _                              => notes,
    };

    /// <summary>Priorita typu poznámky pro řazení „Dle typu": značky → atributy → text.</summary>
    private static int TypPriorita(PoznamkaData p) => p.Typ switch
    {
        TypPoznamky.NegativniZnacka => 0,   // 1. negativní značky
        TypPoznamky.SpecialniZnak   => 1,   // 2. atributy (na znamení, bezbariér)
        _                           => 2,   // 3. text / ruční / zbytek
    };

    // ─── PropertyDescriptors ────────────────────────────────────────────────

    public override IReadOnlyList<PropertyDescriptor> GetPropertyDescriptors() =>
    [
        .. GetScheduleBaseDescriptors(),
        .. GetCellZoneBaseColorDescriptors(),

        new StringPropertyDescriptor { Label = "Nadpis",               Group = "Záhlaví",      Get = () => HeaderText,    Set = v => HeaderText = v },
        new StringPropertyDescriptor { Label = "Font",                 Group = "Záhlaví",      Get = () => HeaderStyle.FontFamily ?? "Arial", Set = v => HeaderStyle.FontFamily = string.IsNullOrWhiteSpace(v) ? null : v },

        new StringPropertyDescriptor { Label = "Font",                Group = "Datové buňky", Get = () => DataStyle.FontFamily ?? "Arial", Set = v => DataStyle.FontFamily = string.IsNullOrWhiteSpace(v) ? null : v },
        new StringPropertyDescriptor { Label = "Oddělovač",           Group = "Datové buňky", Get = () => NoteSeparator,  Set = v => NoteSeparator = v },
        new FloatPropertyDescriptor  { Label = "Velikost poznámek",   Group = "Datové buňky", Get = () => NoteFontSize,   Set = v => NoteFontSize = v,   Min = 4, Max = 20, Unit = "pt" },
        new FloatPropertyDescriptor  { Label = "Výška řádku",         Group = "Datové buňky", Get = () => NoteLineHeight, Set = v => NoteLineHeight = v, Min = 2, Max = 15, Unit = "mm" },

        new FloatPropertyDescriptor  { Label = "Počet sloupců",       Group = "Rozložení", Get = () => Columns,     Set = v => Columns = (int)v, Min = 1, Max = 4, Format = "0" },
        new EnumPropertyDescriptor   { Label = "Směr plnění",         Group = "Rozložení", Get = () => WrapSmer,    Set = v => WrapSmer = (LegendWrap)v,
            Options = [("Po sloupcích", (object)LegendWrap.PoSloupcich), ("Po řádcích", (object)LegendWrap.PoRadcich)] },
        new EnumPropertyDescriptor   { Label = "Řazení",             Group = "Rozložení", Get = () => Razeni,      Set = v => Razeni = (LegendRazeni)v,
            Options = [("Dle typu (značky → atributy → text)", (object)LegendRazeni.DleTypu), ("Výchozí (dle dat)", (object)LegendRazeni.Vychozi), ("Délka vzestupně", (object)LegendRazeni.DleDelkyVzestupne), ("Délka sestupně", (object)LegendRazeni.DleDelkySestupne)] },
        new FloatPropertyDescriptor  { Label = "Šířka buňky značky",  Group = "Rozložení", Get = () => MarkerSirka, Set = v => MarkerSirka = v, Min = 0, Max = 30, Unit = "mm" },

        new BoolPropertyDescriptor  { Label = "Negativní značky",    Group = "Filtrování", Get = () => ShowNegativniZnacky, Set = v => ShowNegativniZnacky = v },
        new BoolPropertyDescriptor  { Label = "Speciální znaky",     Group = "Filtrování", Get = () => ShowSpecialniZnaky,  Set = v => ShowSpecialniZnaky = v },
        new BoolPropertyDescriptor  { Label = "Textové poznámky",    Group = "Filtrování", Get = () => ShowTextovePoznamky, Set = v => ShowTextovePoznamky = v },
        new EnumPropertyDescriptor
        {
            Label = "Výlukové poznámky", Group = "Filtrování",
            Get = () => Vyluky, Set = v => Vyluky = (VylukyFiltr)v,
            Options =
            [
                ("Včetně", (object)VylukyFiltr.Vcetne),
                ("Bez výluk", (object)VylukyFiltr.Bez),
                ("Jen výluky", (object)VylukyFiltr.Jen)
            ]
        },
        new BoolPropertyDescriptor  { Label = "Tarifní podmínky linky",   Group = "Filtrování", Get = () => ShowPoznamkyLinkyTarifni,   Set = v => ShowPoznamkyLinkyTarifni = v },
        new BoolPropertyDescriptor  { Label = "Všeobecné poznámky linky", Group = "Filtrování", Get = () => ShowPoznamkyLinkyVseobecne, Set = v => ShowPoznamkyLinkyVseobecne = v },
        new BoolPropertyDescriptor  { Label = "Radiobus poznámky linky",  Group = "Filtrování", Get = () => ShowPoznamkyLinkyRadiobus,  Set = v => ShowPoznamkyLinkyRadiobus = v },

        new ColorPropertyDescriptor { Label = "NZ pozadí",  Group = "Negativní značky", Get = () => NegativniZnackaBackColor, Set = v => NegativniZnackaBackColor = v },
        new ColorPropertyDescriptor { Label = "NZ barva",   Group = "Negativní značky", Get = () => NegativniZnackaForeColor, Set = v => NegativniZnackaForeColor = v },

        .. GetPositionDescriptors()
    ];

    // ─── Scale ──────────────────────────────────────────────────────────────

    public override void ApplyScale(float scale)
    {
        base.ApplyScale(scale);
        NoteFontSize *= scale;
        NoteLineHeight *= scale;
        MarkerSirka *= scale;
    }

    // ─── Clone ──────────────────────────────────────────────────────────────

    public override TemplateElement Clone()
    {
        var clone = new NotesLegendElement
        {
            HeaderText               = HeaderText,
            NoteSeparator            = NoteSeparator,
            NoteFontSize             = NoteFontSize,
            NoteLineHeight           = NoteLineHeight,
            Columns                  = Columns,
            WrapSmer                 = WrapSmer,
            MarkerSirka              = MarkerSirka,
            Razeni                   = Razeni,
            NegativniZnackaBackColor = NegativniZnackaBackColor,
            NegativniZnackaForeColor = NegativniZnackaForeColor,
            ShowNegativniZnacky      = ShowNegativniZnacky,
            Vyluky                   = Vyluky,
            ShowSpecialniZnaky       = ShowSpecialniZnaky,
            ShowTextovePoznamky      = ShowTextovePoznamky,
            ShowPoznamkyLinkyTarifni   = ShowPoznamkyLinkyTarifni,
            ShowPoznamkyLinkyVseobecne = ShowPoznamkyLinkyVseobecne,
            ShowPoznamkyLinkyRadiobus  = ShowPoznamkyLinkyRadiobus,
        };
        CopyBaseTo(clone);
        CopyStopScheduleProperties(clone);
        return clone;
    }
}
