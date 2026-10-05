using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Schema;
using TemplateDesigner.Services;

namespace TemplateDesigner.PdfImport;

/// <summary>
/// Kombinuje AI sémantické hints (jaké elementy kde jsou, relativní souřadnice 0.0–1.0)
/// s přesnými PdfPig daty (fonty, čáry) a vytváří schedule elementy s rozměry v mm.
///
/// Princip: AI říká CO kde je (relativní pozice), engine přepočítá na mm
/// a zpřesní detaily (font sizes, row heights) z PdfPig geometrie.
/// </summary>
public static class AiGuidedScheduleBuilder
{
    // Detekční prahy (ladění přes testy na reálných PDF):
    private const int HourDigitDetectionMin = 4;        // minutové číslice v gridu jsou <4 nebo >23 → filtrujeme jen hodiny
    private const int HourDigitDetectionMax = 23;
    private const float MinHeaderSpaceAboveScheduleMm = 10f;  // pod touto výškou už nedává smysl vkládat placeholder do záhlaví
    private const float HeaderDataSeparatorOffsetMm = 10f;    // obdélníky pod gridTop+10mm jsou zebra, ne záhlaví
    private const float HeaderRectYToleranceMm = 15f;         // hlavička skupiny = rects v této Y-vzdálenosti od gridTop

    /// <summary>Debug info z posledního BuildFromVision volání.</summary>
    public static string? LastDebugInfo { get; private set; }

    /// <summary>
    /// Z AI vision výsledku (relativní souřadnice) + PdfPig dat vytvoří seznam elementů.
    /// Souřadnice se přepočítají: relativní × rozměry stránky → mm.
    /// </summary>
    public static List<TemplateElement> BuildFromVision(
        AiVisionResult vision,
        ExtractedPage page,
        float marginLeft,
        float marginTop,
        DataSchema? schema = null)
    {
        float pageW = page.WidthMm;
        float pageH = page.HeightMm;

        var elements = new List<TemplateElement>();
        int zIndex = 0;
        var debugLines = new List<string>();
        SkippedNumericBlocks = 0;
        UserNotes.Clear();
        debugLines.Add($"Page: {pageW:0.#} × {pageH:0.#} mm, images: {page.Images.Count}, rectangles: {page.Rectangles.Count}, lines: {page.Lines.Count}, words: {page.Words.Count}");
        foreach (var img in page.Images)
            debugLines.Add($"  Image: X={img.X:0.#} Y={img.Y:0.#} W={img.Width:0.#} H={img.Height:0.#}");

        debugLines.Add($"AI elements (relative → mm):");
        foreach (var el in vision.Elements)
        {
            float elX = el.Left * pageW;
            float elY = el.Top * pageH;
            float elR = el.Right * pageW;
            float elB = el.Bottom * pageH;
            debugLines.Add($"  {el.Type}: rel[{el.Left:0.###},{el.Top:0.###} → {el.Right:0.###},{el.Bottom:0.###}] → mm[{elX:0.#},{elY:0.#} → {elR:0.#},{elB:0.#}]");
        }

        // 1. StopListElement
        var stopListAi = vision.Elements.FirstOrDefault(e =>
            e.Type.Equals("StopList", StringComparison.OrdinalIgnoreCase));
        if (stopListAi is not null)
        {
            var stopEl = BuildStopList(stopListAi, page, marginLeft, marginTop, ref zIndex, debugLines);
            if (stopEl is not null)
                elements.Add(stopEl);
        }

        // 2. DepartureGridElement
        var gridAi = vision.Elements.FirstOrDefault(e =>
            e.Type.Equals("DepartureGrid", StringComparison.OrdinalIgnoreCase));
        if (gridAi is not null)
        {
            var gridEl = BuildDepartureGrid(gridAi, page, marginLeft, marginTop, ref zIndex, debugLines);
            if (gridEl is not null)
                elements.Add(gridEl);
        }

        // 3. NotesLegendElement
        var notesAi = vision.Elements.FirstOrDefault(e =>
            e.Type.Equals("NotesLegend", StringComparison.OrdinalIgnoreCase));
        if (notesAi is not null)
        {
            var notesEl = BuildNotesLegend(notesAi, page, marginLeft, marginTop, ref zIndex, debugLines);
            if (notesEl is not null)
                elements.Add(notesEl);
        }

        // 3b. LineTableElement (linkový JŘ) - jedna tabulka přes všechny oblasti, které model
        //     vrátil. Skupina dnů odsazená vedle (víkendy u 940 IDZK), část tabulky pod ní i opačný
        //     směr vytiskne týž prvek sám; vzít jen jednu oblast by tabulku zúžilo a texty z ostatních
        //     by se doplnily jako volné.
        var lineTablesAi = vision.Elements
            .Where(e => e.Type.Equals("LineTable", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (lineTablesAi.Count > 0)
        {
            var poznamkyAi = vision.Elements
                .Where(e => e.Type.Equals("NotesLegend", StringComparison.OrdinalIgnoreCase)).ToList();
            elements.Add(BuildLineTable(MergeRegions(lineTablesAi), page, marginLeft, marginTop, ref zIndex,
                debugLines, poznamkyAi));
            bool podSebou = lineTablesAi.Any(a => lineTablesAi.Any(b => b.Top >= a.Bottom - 0.01f));
            if (podSebou)
                UserNotes.Add("Předloha má víc tabulek spojů pod sebou (části jedné tabulky nebo oba směry). "
                              + "Šablona má jednu tabulku - další části i opačný směr vytiskne sestava sama.");
        }

        // 4. Textové elementy z AI mapování
        var consumedBlocks = new HashSet<ExtractedTextBlock>();
        var textElements = BuildTextElements(vision, page, marginLeft, marginTop, ref zIndex, debugLines, consumedBlocks);
        elements.AddRange(textElements);

        // 4b. Korekce překryvu
        AdjustOverlaps(vision, page, elements, debugLines);

        // 4c. Barevné plochy hlavičky za texty
        elements.AddRange(BuildHeaderBackgrounds(vision, page, marginLeft, marginTop, debugLines));

        // 5–7. Obrázky (PdfPig + AI mapování + prázdné oblasti)
        var imageElements = BuildImageElements(vision, page, marginLeft, marginTop, elements, ref zIndex, debugLines);
        elements.AddRange(imageElements);

        // Zajistit kladné pozice
        foreach (var el in elements)
        {
            if (el.X < 0) el.X = 0;
            if (el.Y < 0) el.Y = 0;
        }

        // Natáhnout StopList po spodní okraj
        StretchStopList(elements, debugLines);

        // Texty, které model nezmínil, doplnit až TEĎ - podle skutečných rozměrů prvků. Dokud se
        // porovnávalo s oblastmi od modelu, propadly texty, které do prvku spadly až po natažení
        // seznamu zastávek (v šabloně pak ležel název zastávky přes tabulku - nález Jan 18. 8.).
        var doplnene = BuildUnmappedTextElements(
            page, marginLeft, marginTop, ref zIndex, debugLines, consumedBlocks,
            [.. elements.OfType<TextElement>()], ScheduleRects(elements));
        elements.AddRange(doplnene);

        debugLines.Add($"Final elements: {elements.Count}");
        foreach (var el in elements)
            debugLines.Add($"  {el.GetType().Name}: X={el.X:0.#} Y={el.Y:0.#} W={el.Width:0.#} H={el.Height?.ToString("0.#") ?? "null"}");
        LastDebugInfo = string.Join("\n", debugLines);

        return elements;
    }

    // ── Section builders ────────────────────────────────────────────────

    /// <summary>Vytvoří StopListElement z AI vision dat a PdfPig metrik.</summary>
    private static StopListElement? BuildStopList(
        AiVisionElement ai, ExtractedPage page,
        float marginLeft, float marginTop, ref int zIndex, List<string> debug)
    {
        float pageW = page.WidthMm;
        float pageH = page.HeightMm;
        float x = ai.Left * pageW - marginLeft;
        float y = ai.Top * pageH - marginTop;
        float w = (ai.Right - ai.Left) * pageW;
        float h = (ai.Bottom - ai.Top) * pageH;

        var columns = BuildStopColumnsFromAi(ai, w);
        var sp = ai.Properties;

        // Měření fontů přímo z regionu StopListu
        // Odhad výšky záhlaví: ~15% výšky regionu nebo 4mm
        float estHeaderH = ai.HasHeader ? Math.Min(h * 0.15f, 6f) : 0;
        var stopMeasure = MeasureRegionFont(ai, page, estHeaderH);
        float stopDataFont = stopMeasure.DataFontSizePt > 0 ? stopMeasure.DataFontSizePt : 7f;
        float stopHeaderFont = stopMeasure.HeaderFontSizePt > 0 ? stopMeasure.HeaderFontSizePt : stopDataFont;
        float stopRowH = stopMeasure.RowHeightMm > 0 ? stopMeasure.RowHeightMm : 3.5f;
        debug.Add($"StopList measurements: dataFont={stopDataFont:0.#}pt, headerFont={stopHeaderFont:0.#}pt, rowH={stopRowH:0.#}mm");

        string headerBg = NormalizeHexColor(GetProp(sp, "headerBgColor", "#FFE0E0E0"));

        return new StopListElement
        {
            Id = Guid.NewGuid(),
            X = Math.Max(0, x),
            Y = Math.Max(0, y),
            Width = w,
            Height = h,
            ZIndex = zIndex++,
            DataStyle = new TextStyle
            {
                FontFamily = stopMeasure.FontFamily,
                FontSize = stopDataFont,
            },
            HeaderStyle = new TextStyle
            {
                FontFamily = stopMeasure.FontFamily,
                FontSize = stopHeaderFont,
                Bold = true,
                BackgroundColor = headerBg
            },
            ShowHeader = ai.HasHeader,
            Grid = { ShowRowLines = GetBoolProp(sp, "showRowSeparators", true) },
            DataRowHeight = stopRowH,
            Columns = columns,
        };
    }

    /// <summary>Vytvoří DepartureGridElement z AI vision dat a PdfPig metrik.</summary>
    private static DepartureGridElement? BuildDepartureGrid(
        AiVisionElement ai, ExtractedPage page,
        float marginLeft, float marginTop, ref int zIndex, List<string> debug)
    {
        float pageW = page.WidthMm;
        float pageH = page.HeightMm;
        float x = ai.Left * pageW - marginLeft;
        float y = ai.Top * pageH - marginTop;
        float w = (ai.Right - ai.Left) * pageW;
        float h = (ai.Bottom - ai.Top) * pageH;

        // Měření DepartureGrid-specifických metrik z PdfPig
        var gridMeasure = MeasureGrid(ai, page);
        float minuteFontSize = gridMeasure.MinuteFontSizePt > 0 ? gridMeasure.MinuteFontSizePt : 7f;
        float gridHeaderFont = gridMeasure.HeaderFontSizePt > 0 ? gridMeasure.HeaderFontSizePt : Math.Max(minuteFontSize - 1, 6f);
        float rowHeight = gridMeasure.RowHeightMm > 0 ? gridMeasure.RowHeightMm : 3.5f;
        float hourColW = gridMeasure.HourColumnWidthMm > 0 ? gridMeasure.HourColumnWidthMm : 6f;
        debug.Add($"Grid measurements: minuteFont={minuteFontSize:0.#}pt, headerFont={gridHeaderFont:0.#}pt, rowH={rowHeight:0.#}mm, hourColW={hourColW:0.#}mm");

        var gp = ai.Properties;

        // Rozsah hodin, který AI z předlohy vyčte, se do šablony nepřenáší - časovou osu řídí
        // parametr sestavy (CASOVA_OSA/HODINY_OD/DO), takže hodnota v prvku by se při tisku
        // stejně nepoužila. Uživatel si osu nastaví v parametrech.

        // Extrahovat per-group barvy z PdfPig obdélníků
        var perGroupColors = ExtractPerGroupColors(ai, page, pageW, pageH);
        var groupColumns = new List<DepartureGroupColumnStyle>();
        for (int gi = 0; gi < perGroupColors.Count; gi++)
        {
            var gc = perGroupColors[gi];
            groupColumns.Add(new DepartureGroupColumnStyle
            {
                SkupinaDnuId = $"{GroupColumnIndexResolver.IndexKeyPrefix}{gi}",
                IsVisible = true,
                AlternatingRowColor = gc.AlternatingRowColor,
                // Jen barva pozadí - typografie patří prvku. Zapečené `Bold = true` tady stálo
                // za tím, že se v záhlaví odjezdů nedalo tučné vypnout: per-sloupcová hodnota
                // přebíjela ovladač prvku a vypadalo to jako rozbité tlačítko (14. 6.).
                Style = new ColumnVisualStyle
                {
                    Header = new TextStyle { BackgroundColor = gc.HeaderBgColor },
                }
            });
            debug.Add($"  GroupColumn[{gi}]: header={gc.HeaderBgColor}, zebra={gc.AlternatingRowColor ?? "null"}");
        }

        // Výška řádku záhlaví: celková výška / odhadnutý počet řádků textu
        int estHeaderLines = ai.DayGroups.Count > 0
            ? ai.DayGroups.Max(dg => Math.Max(1, (int)Math.Ceiling(dg.Header.Length / 12.0)))
            : 1;
        float headerLineH = gridMeasure.HeaderHeightMm > 0
            ? Math.Clamp(gridMeasure.HeaderHeightMm / Math.Max(1, estHeaderLines), 3f, 7f)
            : 4f;
        debug.Add($"  HeaderLineH: totalH={gridMeasure.HeaderHeightMm:0.#}, estLines={estHeaderLines}, lineH={headerLineH:0.#}");

        // Globální headerBg fallback (pro případ bez per-group barev)
        string gridHeaderBg = NormalizeHexColor(GetProp(gp, "headerBgColor", "#FFE0E0E0"));

        return new DepartureGridElement
        {
            Id = Guid.NewGuid(),
            X = Math.Max(0, x),
            Y = Math.Max(0, y),
            Width = w,
            Height = h,
            ZIndex = zIndex++,
            HourColumnWidth = hourColW,
            HourColumnPerGroup = GetBoolProp(gp, "hourColumnPerGroup", false),
            MinuteWidth = gridMeasure.MinuteCellWidthMm > 0 ? gridMeasure.MinuteCellWidthMm : 4f,
            MinuteFontSize = minuteFontSize,
            MinuteBold = GetBoolProp(gp, "minuteBold", true),
            DataRowHeight = rowHeight,
            AlternatingRowColor = perGroupColors.Count > 0 ? null : GetPropOrNull(gp, "alternateRowColor"),
            SkupinaDnuHeaderLineHeight = headerLineH,
            ShowSkupinaDnuHeader = ai.DayGroups.Count > 0,
            GroupSeparatorWidth = EstimateGroupSeparatorFromAi(ai, w),
            GroupColumns = groupColumns,
            DataStyle = new TextStyle
            {
                FontFamily = gridMeasure.FontFamily,
                FontSize = minuteFontSize,
            },
            HeaderStyle = new TextStyle
            {
                FontFamily = gridMeasure.FontFamily,
                FontSize = gridHeaderFont,
                Bold = true,
                BackgroundColor = perGroupColors.Count > 0 ? null : gridHeaderBg
            },
        };
    }

    /// <summary>Vytvoří NotesLegendElement z AI vision dat a PdfPig metrik.</summary>
    private static NotesLegendElement? BuildNotesLegend(
        AiVisionElement ai, ExtractedPage page,
        float marginLeft, float marginTop, ref int zIndex, List<string> debug)
    {
        float pageW = page.WidthMm;
        float pageH = page.HeightMm;
        float x = ai.Left * pageW - marginLeft;
        float y = ai.Top * pageH - marginTop;
        float w = (ai.Right - ai.Left) * pageW;
        float h = (ai.Bottom - ai.Top) * pageH;

        // Měření fontů z regionu NotesLegend
        var notesMeasure = MeasureRegionFont(ai, page);
        float noteFont = notesMeasure.DataFontSizePt > 0 ? notesMeasure.DataFontSizePt : 7f;
        float noteLineH = notesMeasure.RowHeightMm > 0
            ? notesMeasure.RowHeightMm
            : Math.Max(noteFont * ResolvedTextStyle.PtToMm * 1.4f, 3f); // fallback: fontHeight × 1.4
        debug.Add($"Notes measurements: font={noteFont:0.#}pt, lineH={noteLineH:0.#}mm");

        var np = ai.Properties;

        // Barva písma poznámek z PDF (u 940 IDZK tmavě modrá) - model se na barvy neptá.
        var slova = GetWordsInRegion(ai, page);
        string? pismo = slova.Count > 0 ? PrevazujiciBarva(slova) : null;
        if (pismo == "#FF000000") pismo = null;

        return new NotesLegendElement
        {
            DataStyle = new TextStyle { TextColor = pismo },
            Id = Guid.NewGuid(),
            X = Math.Max(0, x),
            Y = Math.Max(0, y),
            Width = w,
            // Pevný box z AI bboxu (ne null): import replikuje plakát s pevným layoutem - delší
            // legenda jiné linky se do boxu vejde auto-scalem, dynamická výška by přetekla sekci.
            Height = Math.Max(h, 10f),
            ZIndex = zIndex++,
            NoteFontSize = Math.Max(noteFont, 6f),
            NoteLineHeight = noteLineH,
            HeaderText = GetProp(np, "headerText", "Poznámky"),
        };
    }

    /// <summary>Jedna oblast přes všechny; sloupce z té, která jich vyjmenovala nejvíc (část bez
    /// popisných sloupců, třeba odsazené víkendy, žádné nemá).</summary>
    internal static AiVisionElement MergeRegions(IReadOnlyList<AiVisionElement> regions) => regions.Count == 1
        ? regions[0]
        : new AiVisionElement
        {
            Type = regions[0].Type,
            Left = regions.Min(r => r.Left),
            Top = regions.Min(r => r.Top),
            Right = regions.Max(r => r.Right),
            Bottom = regions.Max(r => r.Bottom),
            HasHeader = regions.Any(r => r.HasHeader),
            Columns = regions.MaxBy(r => r.Columns.Count)!.Columns,
            Properties = regions[0].Properties,
        };

    /// <summary>
    /// Vytvoří LineTableElement z oblasti od modelu a písma změřeného v PDF. Zjednodušené proti
    /// zastávkovému JŘ: šířky sloupců se neměří (sloupce spojů vznikají z dat a šířku si prvek
    /// počítá sám), převezme se pořadí popisných sloupců a velikost písma.
    /// </summary>
    /// <remarks>Do stylu se zapisuje jen rodina a velikost písma. Zapečené tučné záhlaví by
    /// přebilo ovladač prvku - stejná chyba jako u záhlaví odjezdů (14. 6.).</remarks>
    private static LineTableElement BuildLineTable(
        AiVisionElement ai, ExtractedPage page,
        float marginLeft, float marginTop, ref int zIndex, List<string> debug,
        IReadOnlyList<AiVisionElement> jineOblasti)
    {
        float pageW = page.WidthMm;
        float pageH = page.HeightMm;
        float x = ai.Left * pageW - marginLeft;
        float y = ai.Top * pageH - marginTop;
        float w = (ai.Right - ai.Left) * pageW;
        float h = (ai.Bottom - ai.Top) * pageH;

        float estHeaderH = ai.HasHeader ? Math.Min(h * 0.15f, 10f) : 0;
        var measure = MeasureRegionFont(ai, page, estHeaderH);

        // Písmo buněk = písmo, kterým je vysázená většina znaků (názvy zastávek, hodiny). Medián
        // sem netrefí: minuty jsou menším písmem (formát „hh mm") a stáhnou ho dolů - u demo výlepu
        // vyšlo 4,3 pt místo 4,8 pt.
        float headerBottom = ai.Top * page.HeightMm + estHeaderH;
        var dataWords = GetWordsInRegion(ai, page).Where(wd => wd.Y >= headerBottom - 1).ToList();
        float dataFont = dataWords.Count > 0 ? PrevazujiciVelikostPisma(dataWords) : measure.DataFontSizePt;

        // Řádek nižší než písmo je chyba měření (hodiny a minuty mají jinou horní hranu, řádky
        // značek jiné písmo) - pevná výška by tabulku stlačila; prázdná výška se počítá z písma.
        float glyphMm = dataFont * ResolvedTextStyle.PtToMm;
        float? rowH = measure.RowHeightMm >= glyphMm * 1.1f ? MmMath.Round(measure.RowHeightMm) : null;

        debug.Add($"LineTable measurements: dataFont={dataFont:0.#}pt (median {measure.DataFontSizePt:0.#}), "
                  + $"headerFont={measure.HeaderFontSizePt:0.#}pt, rowH={measure.RowHeightMm:0.#}mm"
                  + (rowH is null ? " (pod výškou písma, nepoužito)" : "")
                  + $", columns=[{string.Join(", ", ai.Columns)}]");

        var el = new LineTableElement
        {
            Id = Guid.NewGuid(),
            X = Math.Max(0, x),
            Y = Math.Max(0, y),
            Width = w,
            Height = h,
            ZIndex = zIndex++,
            Columns = BuildLineColumnsFromAi(ai.Columns),
        };
        // Velikost z PDF vychází s šumem (7,0004 pt) - ve formuláři by tak i stála.
        if (dataFont > 0)
        {
            el.DataStyle.FontFamily = measure.FontFamily;
            el.DataStyle.FontSize = MathF.Round(dataFont, 1);
        }
        if (measure.HeaderFontSizePt > 0)
        {
            el.HeaderStyle.FontFamily = measure.FontFamily;
            el.HeaderStyle.FontSize = MathF.Round(measure.HeaderFontSizePt, 1);
        }
        el.DataRowHeight = rowH;
        ApplyLineTableColors(el, ai, page, headerBottom, debug, jineOblasti);
        return el;
    }

    /// <summary>
    /// Barvy tabulky spojů změřené z PDF: záhlaví podle pruhu nadpisů skupin, podbarvení řádků
    /// podle pruhů pod zastávkami, písmo buněk a čáry podle převládající barvy. Model se na ně
    /// neptá - jeho odhad barvy je horší než změřená hodnota.
    /// </summary>
    /// <remarks>Barva záhlaví jde do stylu celé tabulky, ne po sloupcích: zapečená hodnota sloupce
    /// by přebila ovladač prvku (táž chyba jako záhlaví odjezdů 14. 6.). Řádek čísel spojů proto
    /// dostane barvu pruhu skupin, i když ho předloha může mít bílý.</remarks>
    internal static void ApplyLineTableColors(LineTableElement el, AiVisionElement ai, ExtractedPage page,
        float headerBottomMm, List<string> debug, IReadOnlyList<AiVisionElement>? jineOblasti = null)
    {
        float l = ai.Left * page.WidthMm, t = ai.Top * page.HeightMm;
        float r = ai.Right * page.WidthMm, b = ai.Bottom * page.HeightMm;

        // Pruh záhlaví poznámek vedle tabulky má tutéž barvu a výšku jako pruhy skupin - bez
        // vyloučení se počítal jako skupina dnů a mezera vyšla 7 mm místo 0 (měření 2. 10.).
        bool VJineOblasti(ExtractedRectangle x) => jineOblasti?.Any(o =>
            x.X + x.Width / 2 > o.Left * page.WidthMm && x.X + x.Width / 2 < o.Right * page.WidthMm
            && x.Y + x.Height / 2 > o.Top * page.HeightMm - ZahlaviPasMm && x.Y + x.Height / 2 < o.Bottom * page.HeightMm) == true;
        bool Vodorovne(ExtractedRectangle x) => x.X >= l - 2f && x.X + x.Width <= r + 2f && !VJineOblasti(x);
        static bool Barevna(string? hex) => ColorHex.ParseOrNull(hex) is { Alpha: > 0 } c
                                            && !(c.Red > 0xF0 && c.Green > 0xF0 && c.Blue > 0xF0);
        bool Uvnitr(ExtractedWord w, ExtractedRectangle x) =>
            w.X + w.Width / 2 > x.X && w.X + w.Width / 2 < x.X + x.Width
            && w.Y + w.Height / 2 > x.Y && w.Y + w.Height / 2 < x.Y + x.Height;

        // Pruh nadpisů skupin („Pracovní dny") - v pásu nad tabulkou nebo v jejím záhlaví.
        var pruhy = page.Rectangles
            .Where(x => Vodorovne(x) && x.Width >= 10f && Barevna(x.FillColorHex)
                        && x.Y + x.Height / 2 >= t - ZahlaviPasMm && x.Y + x.Height / 2 <= headerBottomMm)
            .ToList();
        if (pruhy.Count > 0)
        {
            string barva = pruhy.GroupBy(x => x.FillColorHex!)
                .OrderByDescending(g => g.Sum(x => x.Width * x.Height)).First().Key;
            el.HeaderStyle.BackgroundColor = barva;
            var slova = page.Words.Where(w => pruhy.Any(x => x.FillColorHex == barva && Uvnitr(w, x))).ToList();
            if (slova.Count > 0)
                el.HeaderStyle.TextColor = PrevazujiciBarva(slova);
            debug.Add($"LineTable záhlaví: pozadí {barva}, písmo {el.HeaderStyle.TextColor ?? "(beze změny)"}");

            if (MezeraMeziSkupinami(pruhy.Where(x => x.FillColorHex == barva)) is float mezera)
            {
                el.MezeraMeziSkupinamiMm = mezera;
                debug.Add($"LineTable mezera mezi skupinami: {mezera:0.#} mm");
            }
        }

        // Podbarvení řádků - aspoň tři řádky stejné světlé barvy přes většinu šířky pod záhlavím.
        // Řádek může být jeden pruh i řada podbarvených buněk (tak ho kreslí náš tisk), proto se
        // šířky sčítají po řádcích.
        var radky = page.Rectangles
            .Where(x => Vodorovne(x) && x.Height <= 8f
                        && x.Y >= headerBottomMm - 1f && x.Y + x.Height <= b + 2f
                        && Barevna(x.FillColorHex) && !JeTmava(x.FillColorHex))
            .GroupBy(x => (Barva: x.FillColorHex!, Radek: MathF.Round(x.Y * 2f)))
            .Where(g => g.Sum(x => x.Width) >= (r - l) * 0.3f)
            .GroupBy(g => g.Key.Barva)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();
        if (radky is not null && radky.Count() >= 3)
        {
            el.HighlightOddTc = true;
            el.OddTcBackColor = radky.Key;
            debug.Add($"LineTable podbarvení řádků: {radky.Key} ({radky.Count()} řádků)");
        }
        else
        {
            // Řádky jsou na papíře vždy, takže chybějící podbarvení je volba, ne prázdná data.
            // Výchozí zapnuté by předloze bez podbarvení přidalo šedé pruhy (měření 2. 10.).
            el.HighlightOddTc = false;
            debug.Add("LineTable podbarvení řádků: žádné");
        }

        var slovaDat = page.Words.Where(w => w.X >= l - 2f && w.X <= r && w.Y >= headerBottomMm && w.Y <= b).ToList();
        if (slovaDat.Count > 0 && PrevazujiciBarva(slovaDat) is var pismo && pismo != "#FF000000")
        {
            el.DataStyle.TextColor = pismo;
            debug.Add($"LineTable písmo buněk: {pismo}");
        }

        var cary = page.Lines
            .Where(c => Math.Min(c.X1, c.X2) >= l - 2f && Math.Max(c.X1, c.X2) <= r + 2f
                        && Math.Min(c.Y1, c.Y2) >= t - 2f && Math.Max(c.Y1, c.Y2) <= b + 2f)
            .GroupBy(c => c.ColorHex)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();
        if (cary is not null && cary.Count() >= 5 && ColorHex.IsValid(cary.Key))
        {
            el.Grid.Color = cary.Key;
            debug.Add($"LineTable čáry: {cary.Key} ({cary.Count()}×)");
        }
    }

    /// <summary>
    /// Mezera mezi skupinami sloupců podle mezer mezi pruhy nadpisů („Směr jízdy" | „Pracovní dny" |
    /// „Víkendy"). Prvek má na všechny mezery jednu hodnotu, a viditelný je hlavně rozestup mezi
    /// skupinami dnů - proto se bere z mezer za prvním pruhem (ten nese popisné sloupce), když jsou.
    /// Null = pruhy na sebe navazují, mezera v předloze není.
    /// </summary>
    internal static float? MezeraMeziSkupinami(IEnumerable<ExtractedRectangle> pruhy)
    {
        var serazene = pruhy.OrderBy(x => x.X).ToList();
        var mezery = serazene.Zip(serazene.Skip(1), (a, c) => c.X - (a.X + a.Width))
            .Where(m => m >= 1f && m <= 20f)
            .ToList();
        if (mezery.Count == 0) return null;

        var mezeryDnu = mezery.Count > 1 ? mezery.Skip(1).ToList() : mezery;
        return MathF.Round(Median(mezeryDnu) * 2f) / 2f;
    }

    /// <summary>Barva, kterou je napsaná většina znaků.</summary>
    private static string PrevazujiciBarva(IEnumerable<ExtractedWord> words) =>
        words.GroupBy(w => w.ColorHex).OrderByDescending(g => g.Sum(w => w.Text.Length)).First().Key;

    /// <summary>
    /// Popisné sloupce v pořadí, v jakém je model na předloze viděl; ostatní za nimi ve výchozím
    /// pořadí a sloupec spojů na konci.
    /// </summary>
    /// <remarks>⚠️ Sloupec, který model neviděl, se <b>neskrývá</b>: prázdný sloupec se netiskne
    /// sám, takže na předloze chybí i tehdy, když linka hodnoty jen nemá (km, MHD, zóny). Skrytý
    /// by u jiné linky schoval data, která tam patří. A dialog sloupců neumí sloupec přidat, takže
    /// zůstat v seznamu musí všechny.</remarks>
    internal static List<StopColumn> BuildLineColumnsFromAi(IReadOnlyList<string> seenKeys)
    {
        var defaults = LineTableElement.GetDefaultColumns();
        var popisne = defaults.Where(c => c.Key != LineTableElement.TripKey).ToList();

        var result = new List<StopColumn>();
        foreach (string key in seenKeys)
        {
            var col = popisne.FirstOrDefault(c => c.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (col is not null && !result.Contains(col)) result.Add(col);
        }
        result.AddRange(popisne.Where(c => !result.Contains(c)));
        result.AddRange(defaults.Where(c => c.Key == LineTableElement.TripKey));
        return result;
    }

    /// <summary>Vytvoří TextElement-y z AI text mapování, přeskočí texty uvnitř schedule oblastí.</summary>
    private static List<TextElement> BuildTextElements(
        AiVisionResult vision, ExtractedPage page,
        float marginLeft, float marginTop, ref int zIndex, List<string> debug,
        HashSet<ExtractedTextBlock>? consumedBlocks = null)
    {
        float pageW = page.WidthMm;
        float pageH = page.HeightMm;
        var result = new List<TextElement>();
        var allTextBlocks = PdfTextGrouper.GroupWords(page.Words);
        var textByBlock = new Dictionary<ExtractedTextBlock, TextElement>();

        // Přeskočit texty které leží uvnitř schedule elementu (řeší je schedule element sám)
        var scheduleBoxes = vision.Elements.Select(e => (
            L: e.Left * pageW, T: e.Top * pageH, R: e.Right * pageW, B: e.Bottom * pageH
        )).ToList();

        foreach (var mapping in vision.TextMappings)
        {
            if (mapping.Role.Equals("decoration", StringComparison.OrdinalIgnoreCase)
                && mapping.DbField is null)
                continue;

            // Přeskočit text který ZAČÍNÁ výrazně uvnitř schedule elementu (2mm tolerance)
            // Text na hraně schedule (tyTop ≈ b.T) je header text, ne schedule obsah
            float txCenter = mapping.X * pageW + (mapping.W * pageW) / 2;
            float tyTop = mapping.Y * pageH;
            bool insideSchedule = scheduleBoxes.Any(b =>
                txCenter >= b.L && txCenter <= b.R && tyTop >= b.T + 2f && tyTop <= b.B);
            if (insideSchedule)
            {
                debug.Add($"  Skipping text inside schedule: \"{mapping.Text}\" top=({txCenter:0.#},{tyTop:0.#})");
                continue;
            }

            string displayText = mapping.Text;
            if (mapping.PlaceholderText is not null && mapping.Confidence >= 0.7f)
                displayText = mapping.PlaceholderText;

            // Najít odpovídající textový blok v PdfPig datech pro přesnou pozici
            float mappingXMm = mapping.X * pageW;
            float mappingYMm = mapping.Y * pageH;
            var matchedBlock = FindMatchingTextBlock(page, mapping, mappingXMm, mappingYMm);
            if (matchedBlock is not null) consumedBlocks?.Add(matchedBlock);

            float elX, elY, elW, elH;
            string fontFamily = "Arial";
            float fontSize = 10f;
            bool bold = false;
            bool italic = false;
            string color = "#FF000000";

            // Model vrací texty po řádcích, PDF je ale může mít sloučené do jednoho bloku
            // („Platí od …" a „do …" pod sebou) - pak patří textu jen jeho řádek bloku.
            int? lineOfBlock = matchedBlock is null ? null : LineOfBlock(matchedBlock, mapping.Text);

            // Model rozdělil jeden řádek PDF na víc textů („Linka 771940 | IDZK | …" u 940): na
            // stejném bloku by ležely přes sebe, takže se připojí k už vloženému textu.
            if (matchedBlock is not null && lineOfBlock is null
                && textByBlock.TryGetValue(matchedBlock, out var sameBlock))
            {
                sameBlock.Text += " " + displayText;
                debug.Add($"  Připojeno k textu na stejném bloku: \"{mapping.Text}\"");
                continue;
            }

            var pieces = matchedBlock is not null && lineOfBlock is null
                ? SameLinePieces(allTextBlocks, matchedBlock, mapping.Text)
                : [];
            foreach (var piece in pieces) consumedBlocks?.Add(piece);

            if (matchedBlock is not null)
            {
                float fontHeightMm = matchedBlock.FontSizePt * ResolvedTextStyle.PtToMm;
                int lineCount = matchedBlock.Text.Count(c => c == '\n') + 1;

                elX = matchedBlock.X - marginLeft;
                elY = matchedBlock.Y - marginTop;
                elW = Math.Max(matchedBlock.Width, fontHeightMm * 2);
                if (pieces.Count > 1)
                {
                    elX = pieces.Min(p => p.X) - marginLeft;
                    elW = pieces.Max(p => p.X + p.Width) - pieces.Min(p => p.X);
                }
                // Výška z font size (ne z PdfPig bounding boxu, který může být nafouklý)
                elH = fontHeightMm * 1.3f * lineCount;
                if (lineOfBlock is int line)
                {
                    elY += matchedBlock.Height / lineCount * line;
                    elH = fontHeightMm * 1.3f;
                }
                fontFamily = matchedBlock.FontFamily;
                fontSize = matchedBlock.FontSizePt;
                bold = matchedBlock.IsBold;
                italic = matchedBlock.IsItalic;
                color = matchedBlock.ColorHex;
            }
            else
            {
                // Fallback: AI relativní pozice → mm
                elX = mappingXMm - marginLeft;
                elY = mappingYMm - marginTop;
                elW = mapping.W > 0 ? mapping.W * pageW : Math.Max(mapping.Text.Length * 2f, 20f);
                elH = mapping.H > 0 ? mapping.H * pageH : mapping.Role switch
                {
                    "title" => 12f, "header" => 8f, _ => 6f
                };

                if (mapping.ApproxFontSize > 0)
                    fontSize = mapping.ApproxFontSize;
                else
                    fontSize = mapping.Role switch { "title" => 16f, "header" => 10f, _ => 8f };

                bold = mapping.Role is "title" or "header";
            }

            // Najít enclosing rectangle pro rámeček
            string borderColor = "#00FFFFFF";
            float borderWidth = 0f;
            string bgColor = "#00FFFFFF";

            // Rámeček kolem víceřádkového bloku patří všem jeho řádkům, ne jednomu.
            if (matchedBlock is not null && lineOfBlock is null && pieces.Count <= 1)
            {
                var enclosingRect = FindEnclosingRectangle(page.Rectangles, matchedBlock, allTextBlocks);
                if (enclosingRect is not null)
                {
                    elX = enclosingRect.X - marginLeft;
                    elY = enclosingRect.Y - marginTop;
                    elW = enclosingRect.Width;
                    elH = enclosingRect.Height;
                    borderColor = enclosingRect.StrokeColorHex ?? "#FF000000";
                    borderWidth = Math.Clamp(enclosingRect.StrokeWidth, 0.1f, 0.5f);
                    bgColor = enclosingRect.FillColorHex ?? "#00FFFFFF";
                }
            }

            var textElement = new TextElement
            {
                Id = Guid.NewGuid(),
                X = Math.Max(0, elX),
                Y = Math.Max(0, elY),
                Width = elW,
                Height = elH,
                ZIndex = zIndex++,
                Text = displayText,
                Style = new TextStyle
                {
                    FontFamily = fontFamily,
                    FontSize = fontSize,
                    Bold = bold,
                    Italic = italic,
                    TextColor = color,
                    HAlign = TextHorizontalAlignment.Center,
                    VAlign = TextVerticalAlignment.Middle,
                },
                WordWrap = displayText.Contains('\n'),
                BorderColor = borderColor,
                BorderWidth = borderWidth,
                BackgroundColor = bgColor,
            };
            result.Add(textElement);
            if (matchedBlock is not null && lineOfBlock is null)
                foreach (var blok in pieces.Count > 0 ? pieces : [matchedBlock])
                    textByBlock[blok] = textElement;
        }

        return result;
    }

    /// <summary>
    /// Kusy, na které PDF rozdělilo jeden text na řádku - trasa „Kroměříž - Kyselovice - Chropyně
    /// - Přerov" u 940 IDZK nemá pomlčky v textové vrstvě, takže z ní jsou tři bloky. Bere se souvislá
    /// řada bloků kolem nalezeného, jejichž text je obsažen v textu od modelu; nalezený blok je
    /// v řadě vždy. Jeden kus = text nerozdělený.
    /// </summary>
    internal static List<ExtractedTextBlock> SameLinePieces(
        IReadOnlyList<ExtractedTextBlock> blocks, ExtractedTextBlock matched, string mappingText)
    {
        string text = NormalizeText(mappingText);
        float maxGap = matched.FontSizePt * ResolvedTextStyle.PtToMm * 3f;   // pomlčka s mezerami

        var row = blocks
            .Where(b => b == matched
                || (!b.Text.Contains(NewLine)
                    && Math.Abs(b.Y - matched.Y) < Math.Max(matched.Height, b.Height) * 0.5f
                    && NormalizeText(b.Text) is { Length: >= 2 } t
                    && text.Contains(t, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(b => b.X)
            .ToList();

        int i = row.IndexOf(matched);
        if (i < 0) return [matched];
        int from = i, to = i;
        while (from > 0 && row[from].X - (row[from - 1].X + row[from - 1].Width) <= maxGap) from--;
        while (to < row.Count - 1 && row[to + 1].X - (row[to].X + row[to].Width) <= maxGap) to++;
        return row.GetRange(from, to - from + 1);
    }

    /// <summary>
    /// Řádek víceřádkového bloku, kterému odpovídá text od modelu; null = blok má jeden řádek,
    /// nebo text odpovídá celému bloku (pak se bere blok celý, jako dosud).
    /// </summary>
    internal static int? LineOfBlock(ExtractedTextBlock block, string mappingText)
    {
        var lines = block.Text.Split(NewLine);
        if (lines.Length < 2) return null;

        string text = NormalizeText(mappingText);
        var normalized = lines.Select(NormalizeText).ToList();

        int exact = normalized.FindIndex(l => l.Length > 0 && string.Equals(l, text, StringComparison.OrdinalIgnoreCase));
        if (exact >= 0) return exact;

        // Text přes víc řádků bloku (celý blok) = blok celý; jinak řádek, ve kterém text leží.
        var hits = Enumerable.Range(0, lines.Length).Where(i => normalized[i].Length > 0
            && (text.Contains(normalized[i], StringComparison.OrdinalIgnoreCase)
                || normalized[i].Contains(text, StringComparison.OrdinalIgnoreCase))).ToList();
        return hits.Count == 1 ? hits[0] : null;
    }

    /// <summary>Obdélníky jízdněřádových prvků tak, jak nakonec v šabloně leží (v mm).</summary>
    private static List<(float L, float T, float R, float B)> ScheduleRects(List<TemplateElement> elements) =>
    [
        .. elements
            .Where(IsScheduleElement)
            .Select(e => (e.X, e.Y, e.X + e.Width, e.Y + (e.Height ?? 0)))
    ];

    /// <summary>Prvek, který si obsah kreslí sám z dat - texty v jeho oblasti do šablony nepatří.</summary>
    private static bool IsScheduleElement(TemplateElement e) =>
        e is StopListElement or DepartureGridElement or NotesLegendElement or LineTableElement;

    /// <summary>
    /// Doplní texty, které v předloze prokazatelně jsou, ale model je nezmínil. Model vrací
    /// <b>význam</b> (co je nadpis, co je pole agendy), geometrii textu má PDF přesnou - a když
    /// model text vynechá, uživateli po importu chybí (číslo linky, trasa, řádek s dopravcem).
    /// Doplněné texty jsou prosté: bez role a bez napojení na datové pole, ať se nepředstírá
    /// význam, který nikdo neurčil.
    ///
    /// <para>Vynechá se, co si kreslí jízdněřádový prvek sám (leží uvnitř jeho oblasti) a co už
    /// v šabloně je - i když to model pojmenoval jinak, než jak to stojí v PDF.</para>
    /// </summary>
    private static List<TextElement> BuildUnmappedTextElements(
        ExtractedPage page,
        float marginLeft, float marginTop, ref int zIndex, List<string> debug,
        HashSet<ExtractedTextBlock> consumedBlocks, List<TextElement> alreadyPlaced,
        List<(float L, float T, float R, float B)> scheduleBoxes)
    {
        var result = new List<TextElement>();

        var candidates = PdfTextGrouper.GroupWords(page.Words)
            .Where(b => !consumedBlocks.Contains(b) && !string.IsNullOrWhiteSpace(b.Text))
            .Where(b => !PatriKJizdnimuRadu(b, scheduleBoxes))
            .ToList();

        // Roj samotných čísel = obsah jízdního řádu, který se modelu nevešel do vyznačené oblasti
        // (odjezdy pod mřížkou, časy vedle ní). Jako texty by v šabloně byly vždycky špatně - data
        // si prvek při tisku vezme sám. Jedno dvě čísla naopak nechat: číslo linky je taky číslo.
        var cislaBezTextu = candidates.Where(b => !b.Text.Any(char.IsLetter)).ToList();
        if (cislaBezTextu.Count >= 3)
        {
            debug.Add($"Vynecháno samostatných čísel (obsah jízdního řádu): {cislaBezTextu.Count}");
            SkippedNumericBlocks = cislaBezTextu.Count;
            candidates = [.. candidates.Except(cislaBezTextu)];
        }

        foreach (var block in candidates)
        {
            float x = block.X - marginLeft;
            float y = block.Y - marginTop;
            if (alreadyPlaced.Any(t => Overlaps(t, x, y, block.Width, block.Height))) continue;

            result.Add(new TextElement
            {
                Id = Guid.NewGuid(),
                X = Math.Max(0, x),
                Y = Math.Max(0, y),
                Width = Math.Max(block.Width, 2f),
                Height = Math.Max(block.FontSizePt * ResolvedTextStyle.PtToMm * 1.3f * LineCount(block.Text), 2f),
                ZIndex = zIndex++,
                Text = block.Text,
                Style = new TextStyle
                {
                    FontFamily = block.FontFamily,
                    FontSize = block.FontSizePt,
                    Bold = block.IsBold,
                    Italic = block.IsItalic,
                    TextColor = block.ColorHex,
                    VAlign = TextVerticalAlignment.Middle,
                },
                WordWrap = LineCount(block.Text) > 1,
            });
        }

        if (result.Count > 0)
            debug.Add($"Doplněno textů, které AI nezmínila: {result.Count}");
        return result;
    }

    private static int LineCount(string text) => text.Split(NewLine).Length;

    private const char NewLine = '\n';

    /// <summary>Pod touto výškou je „vyplněný obdélník" v PDF čára, ne pozadí hlavičky (mm).</summary>
    private const float MinHeaderRectHeightMm = 1.5f;

    /// <summary>Tmavá výplň = čára nebo rámeček, ne pozadí záhlaví (text na ní není čitelný).</summary>
    private static bool JeTmava(string? hex)
    {
        var c = ColorHex.ParseOrNull(hex);
        if (c is null) return false;
        return (c.Value.Red + c.Value.Green + c.Value.Blue) / 3 < 0x50;
    }

    /// <summary>Pás nad vyznačenou oblastí, který k prvku ještě patří (mm).</summary>
    /// <remarks>Model často vyznačí oblast až od dat a záhlaví (popisky sloupců, skupiny dnů,
    /// „hod") nechá venku. Jako samostatné texty je to vždycky špatně - je to záhlaví jedné
    /// tabulky, ne dvacet popisků. Rozpadlo se to takhle 18. 8. u vývěsky ČSAD.</remarks>
    private const float ZahlaviPasMm = 8f;

    /// <summary>Přesah pod vyznačenou oblastí, který k prvku ještě patří (mm).</summary>
    /// <remarks>Spodní hrana je od modelu taky jen odhad. Pokračovací řádek legendy poznámek
    /// (dlouhá poznámka zalomená na víc řádků) nebo poslední řádek tabulky spadne těsně pod ni
    /// a jako samostatný text v šabloně přebývá - nález Jan 18. 8. Menší než pás záhlaví: pod
    /// prvkem bývají legitimní popisky (tarifní doložka pod jízdním řádem), nad ním ne.</remarks>
    private const float PresahPodPrvkemMm = 4f;

    /// <summary>
    /// Leží blok uvnitř jízdněřádového prvku, nebo v pásu jeho záhlaví těsně nad ním? Pak si ho
    /// prvek kreslí sám a jako text do šablony nepatří.
    /// </summary>
    private static bool PatriKJizdnimuRadu(
        ExtractedTextBlock b, List<(float L, float T, float R, float B)> boxes)
    {
        float cy = b.Y + b.Height / 2;
        return boxes.Any(box =>
            b.X + b.Width > box.L && b.X < box.R           // vodorovně se překrývá s prvkem
            && cy >= box.T - ZahlaviPasMm                  // uvnitř nebo v pásu záhlaví nad prvkem
            && cy <= box.B + PresahPodPrvkemMm);           // nebo těsně pod jeho spodní hranou
    }

    /// <summary>Kolik samostatných čísel se při posledním importu vynechalo - pro protokol.</summary>
    public static int SkippedNumericBlocks { get; private set; }

    /// <summary>Co si má uživatel z posledního importu dodělat ručně - do protokolu.</summary>
    public static List<string> UserNotes { get; } = [];

    private static string RoleCesky(string role) => role switch
    {
        "logo"   => "logo",
        "qrCode" => "QR kód",
        _        => "grafiku"
    };

    /// <summary>Překryv obdélníků v mm - dvě třetiny plochy stačí na „je to tentýž text".</summary>
    private static bool Overlaps(TextElement placed, float x, float y, float w, float h)
    {
        float ph = placed.Height ?? 0;
        float ix = Math.Min(placed.X + placed.Width, x + w) - Math.Max(placed.X, x);
        float iy = Math.Min(placed.Y + ph, y + h) - Math.Max(placed.Y, y);
        if (ix <= 0 || iy <= 0) return false;

        float smaller = Math.Min(placed.Width * ph, w * h);
        return smaller > 0 && ix * iy / smaller > 0.66f;
    }

    /// <summary>
    /// Korekce překryvu: pokud textové elementy přesahují do schedule oblasti,
    /// posune schedule elementy dolů.
    /// </summary>
    private static void AdjustOverlaps(
        AiVisionResult vision, ExtractedPage page,
        List<TemplateElement> elements, List<string> debug)
    {
        float pageH = page.HeightMm;

        float scheduleTopMm = vision.Elements.Count > 0
            ? vision.Elements.Min(e => e.Top) * pageH
            : pageH * 0.15f;

        // Najít spodní hranu hlavičkových textů (nad schedule)
        float headerBottom = 0;
        foreach (var te in elements.OfType<TextElement>())
        {
            float bottom = te.Y + (te.Height ?? 0);
            if (te.Y < scheduleTopMm && bottom > headerBottom)
                headerBottom = bottom;
        }

        float overlap = headerBottom - scheduleTopMm;
        if (overlap > 0)
        {
            float shift = overlap + 1f; // +1mm mezera
            debug.Add($"Header-schedule overlap: {overlap:0.#}mm, shifting schedule down by {shift:0.#}mm");

            foreach (var el in elements)
            {
                if (IsScheduleElement(el))
                    el.Y += shift;
            }
        }
    }

    /// <summary>
    /// Vytvoří image elementy ze tří zdrojů: PdfPig obrázky (sekce 5),
    /// AI image mapování (sekce 6) a prázdné oblasti (sekce 7).
    /// </summary>
    private static List<TemplateElement> BuildImageElements(
        AiVisionResult vision, ExtractedPage page,
        float marginLeft, float marginTop,
        List<TemplateElement> existingElements, ref int zIndex, List<string> debug)
    {
        float pageW = page.WidthMm;
        float pageH = page.HeightMm;
        var result = new List<TemplateElement>();

        // 5. PdfPig obrázky - přeskočit ty které leží uvnitř schedule elementu
        var scheduleRegionsMm = vision.Elements.Select(el => (
            Left: el.Left * pageW, Top: el.Top * pageH,
            Right: el.Right * pageW, Bottom: el.Bottom * pageH
        )).ToList();

        debug.Add($"Schedule regions (mm) for image filtering:");
        foreach (var r in scheduleRegionsMm)
            debug.Add($"  [{r.Left:0.#},{r.Top:0.#} → {r.Right:0.#},{r.Bottom:0.#}]");

        var usedPdfImages = new HashSet<ExtractedImage>();
        foreach (var img in page.Images)
        {
            float imgCx = img.X + img.Width / 2;
            float imgCy = img.Y + img.Height / 2;

            bool insideScheduleElement = scheduleRegionsMm.Any(r =>
                imgCx >= r.Left + 2 && imgCx <= r.Right - 2
                && imgCy >= r.Top + 2 && imgCy <= r.Bottom - 2);

            debug.Add($"  Image center=({imgCx:0.#},{imgCy:0.#}), inside={insideScheduleElement}");

            if (insideScheduleElement)
                continue;

            usedPdfImages.Add(img);
            result.Add(new ImageElement
            {
                Id = Guid.NewGuid(),
                X = Math.Max(0, img.X - marginLeft),
                Y = Math.Max(0, img.Y - marginTop),
                Width = img.Width,
                Height = img.Height,
                ZIndex = zIndex++,
                ImageBase64 = img.Base64Png
            });
        }

        // 6. AI-detekované obrázky (logo, QR kód) - pokud PdfPig je nenašel
        debug.Add($"AI image mappings: {vision.ImageMappings.Count}");
        foreach (var imgMap in vision.ImageMappings)
        {
            float imgX = imgMap.X * pageW - marginLeft;
            float imgY = imgMap.Y * pageH - marginTop;
            float imgW = imgMap.W > 0 ? imgMap.W * pageW : 15f;
            float imgH = imgMap.H > 0 ? imgMap.H * pageH : 15f;

            debug.Add($"  AI image: role={imgMap.Role}, pos=({imgX:0.#},{imgY:0.#}), size={imgW:0.#}×{imgH:0.#}");

            // Hlášení bez rozměru je šum (u 940 přišlo „0, 0" bez šířky a výšky) - rámeček z něj
            // by ležel v rohu stránky naslepo.
            if (imgMap.W <= 0 || imgMap.H <= 0)
            {
                debug.Add("    → bez rozměru - skip");
                continue;
            }

            // Uvnitř tabulky nebo poznámek logo ani QR není - model tam „grafiku" ohlásil u 940 IDZK
            // a v šabloně z ní byl prázdný rámeček přes poznámky.
            float imgCenterX = imgMap.X * pageW + imgW / 2, imgCenterY = imgMap.Y * pageH + imgH / 2;
            if (scheduleRegionsMm.Any(r => imgCenterX >= r.Left + 2 && imgCenterX <= r.Right - 2
                                           && imgCenterY >= r.Top + 2 && imgCenterY <= r.Bottom - 2))
            {
                debug.Add("    → leží v oblasti jízdního řádu - skip");
                continue;
            }

            // Zkusit najít odpovídající obrázek z PdfPig (dle pozice v mm)
            float imgMapXMm = imgMap.X * pageW;
            float imgMapYMm = imgMap.Y * pageH;
            var matchedPdfImage = page.Images.FirstOrDefault(pi =>
                Math.Abs(pi.X - imgMapXMm) < 10f && Math.Abs(pi.Y - imgMapYMm) < 10f);

            if (matchedPdfImage is not null)
            {
                // Dedup: obrázek z kroku 5 už v šabloně je - druhý přes něj nedávat.
                if (!usedPdfImages.Add(matchedPdfImage))
                {
                    debug.Add($"    → PdfPig image already added in step 5, skip (dedup)");
                    continue;
                }
                result.Add(new ImageElement
                {
                    Id = Guid.NewGuid(),
                    X = Math.Max(0, matchedPdfImage.X - marginLeft),
                    Y = Math.Max(0, matchedPdfImage.Y - marginTop),
                    Width = matchedPdfImage.Width,
                    Height = matchedPdfImage.Height,
                    ZIndex = zIndex++,
                    ImageBase64 = matchedPdfImage.Base64Png,
                });
                debug.Add($"    → matched PdfPig image");
            }
            else if (imgMap.Role == "qrCode" && imgMap.DbField is not null)
            {
                // Anti-halucinace: QR vytvořit jen když ho potvrzuje PdfPig geometrie
                // (shluk malých čtverečků v místě) - samotné tvrzení modelu nestačí.
                if (!LooksLikeQrRegion(page, imgMapXMm + imgMap.W * pageW / 2, imgMapYMm + imgMap.H * pageH / 2))
                {
                    debug.Add($"    → qrCode NEPOTVRZEN geometrií (žádný shluk čtverečků) - skip");
                    continue;
                }
                result.Add(new BarcodeElement
                {
                    Id = Guid.NewGuid(),
                    X = Math.Max(0, imgX),
                    Y = Math.Max(0, imgY),
                    Width = imgW,
                    Height = imgH,
                    ZIndex = zIndex++,
                    BarcodeType = BarcodeElementType.QR,
                    Content = $"{{{imgMap.DbField}}}",
                });
                debug.Add($"    → created BarcodeElement (QR) with {{{imgMap.DbField}}}");
            }
            else
            {
                // Placeholder jen když se nepřekrývá s už přidaným obrázkem (jinak „falešný
                // element" přes logo - model občas ohlásí týž objekt dvakrát s posunem).
                bool overlapsExisting = result.Any(r =>
                    imgX < r.X + r.Width + 2 && imgX + imgW > r.X - 2
                    && imgY < r.Y + (r.Height ?? 0) + 2 && imgY + imgH > r.Y - 2);
                if (overlapsExisting)
                {
                    debug.Add($"    → placeholder se překrývá s existujícím obrázkem - skip");
                    continue;
                }
                // Obrázek v předloze nejde vytáhnout (bývá nakreslený čarami, ne vložený jako
                // bitmapa), ale model ho vidí a ví KDE je - vloží se prázdný rámeček na tu pozici,
                // aby uživateli stačilo nahrát soubor. Šedý „placeholder" to není: prázdný rámeček
                // je regulérní obrázkový prvek, jen zatím bez obsahu.
                result.Add(new ImageElement
                {
                    Id = Guid.NewGuid(),
                    X = Math.Max(0, imgX),
                    Y = Math.Max(0, imgY),
                    Width = imgW,
                    Height = imgH,
                    ZIndex = zIndex++,
                    ImageBase64 = null,
                });
                UserNotes.Add($"Na pozici {imgX:0.#}, {imgY:0.#} mm je v předloze "
                              + $"{RoleCesky(imgMap.Role)} - vložen prázdný rámeček, nahrajte do něj obrázek.");
                debug.Add($"    → prázdný rámeček na pozici z předlohy (obrázek nejde z PDF vytáhnout)");
            }
        }

        // 7. Prázdné orámované boxy z předlohy = místo pro obrázek (logo). Bere se geometrie
        //    papíru, ne odhad modelu: model logo hlásí jen někdy a pokaždé jinde, kdežto rámeček
        //    v PDF má přesnou pozici i velikost. Text uvnitř = rámeček kolem textu, ne slot.
        foreach (var ramecek in PrazdneRamecky(page, vision, result, existingElements))
        {
            result.Add(new ImageElement
            {
                Id = Guid.NewGuid(),
                X = Math.Max(0, ramecek.X - marginLeft),
                Y = Math.Max(0, ramecek.Y - marginTop),
                Width = ramecek.Width,
                Height = ramecek.Height,
                ZIndex = zIndex++,
                ImageBase64 = null,
            });
            UserNotes.Add($"V předloze je prázdný rámeček na pozici {ramecek.X:0.#}, {ramecek.Y:0.#} mm "
                          + "(bývá to místo pro logo) - vložen prázdný obrázek, nahrajte do něj soubor.");
            debug.Add($"  → prázdný rámeček z předlohy: ({ramecek.X:0.#},{ramecek.Y:0.#}) {ramecek.Width:0.#}×{ramecek.Height:0.#}mm");
        }

        // Krok „prázdné místo v záhlaví = nejspíš logo" byl zrušen (Jan 18. 8.): dělil záhlaví
        // na šest stejných sloupců a do prázdného položil rámeček, takže jeho pozice nevycházela
        // z předlohy, ale ze šířky stránky - v šabloně pak seděl jinde než logo na papíře.
        // Prázdný rámeček vzniká jen tam, kde model obrázek opravdu viděl (krok 6).

        return result;
    }

    /// <summary>Nejmenší šířka barevné plochy hlavičky (mm) - užší bývá podbarvení jedné hodnoty.</summary>
    private const float MinPozadiSirkaMm = 20f;

    /// <summary>
    /// Barevné plochy hlavičky (pruh pod číslem a trasou linky) jako obdélníky za texty. Bez nich
    /// zůstane bílý text na bílém - u 940 IDZK byla celá hlavička neviditelná. Bere se plocha nad
    /// jízdním řádem, která není bílá a leží na ní text (prázdná je rámeček na logo, viz krok 7).
    /// </summary>
    internal static List<RectangleElement> BuildHeaderBackgrounds(
        AiVisionResult vision, ExtractedPage page, float marginLeft, float marginTop, List<string> debug)
    {
        float scheduleTop = vision.Elements.Count > 0
            ? vision.Elements.Min(e => e.Top) * page.HeightMm
            : page.HeightMm * 0.2f;
        var scheduleBoxes = vision.Elements
            .Select(e => (L: e.Left * page.WidthMm, T: e.Top * page.HeightMm,
                          R: e.Right * page.WidthMm, B: e.Bottom * page.HeightMm))
            .ToList();

        // Pruh nadpisu skupiny („Pracovní dny") těsně nad tabulkou si kreslí tabulka sama - model
        // její oblast často začne až pod ním, jako u textů záhlaví (ZahlaviPasMm).
        bool vZahlaviTabulky(ExtractedRectangle r)
        {
            float cy = r.Y + r.Height / 2;
            return scheduleBoxes.Any(b => r.X + r.Width > b.L && r.X < b.R
                                          && cy >= b.T - ZahlaviPasMm && cy <= b.B);
        }

        var plochy = page.Rectangles
            .Where(r => r.Width >= MinPozadiSirkaMm && r.Height >= MinHeaderRectHeightMm
                        && r.Y + r.Height <= scheduleTop + 1f
                        && !vZahlaviTabulky(r))
            .Where(r => ColorHex.ParseOrNull(r.FillColorHex) is { Alpha: > 0 } c
                        && !(c.Red > 0xF0 && c.Green > 0xF0 && c.Blue > 0xF0))
            .Where(r => page.Words.Any(w =>
                w.X + w.Width / 2 > r.X && w.X + w.Width / 2 < r.X + r.Width
                && w.Y + w.Height / 2 > r.Y && w.Y + w.Height / 2 < r.Y + r.Height))
            .OrderByDescending(r => r.Width * r.Height)   // větší plocha dospodu
            .ToList();

        var result = new List<RectangleElement>();
        for (int i = 0; i < plochy.Count; i++)
        {
            var r = plochy[i];
            result.Add(new RectangleElement
            {
                Id = Guid.NewGuid(),
                X = Math.Max(0, r.X - marginLeft),
                Y = Math.Max(0, r.Y - marginTop),
                Width = r.Width,
                Height = r.Height,
                ZIndex = i - plochy.Count,   // za všemi texty (ty číslují od 0)
                BackgroundColor = r.FillColorHex!,
                StrokeColor = r.StrokeColorHex ?? r.FillColorHex!,
                StrokeWidth = r.StrokeColorHex is null ? 0f : Math.Clamp(r.StrokeWidth, 0.1f, 0.5f),
            });
            debug.Add($"Pozadí hlavičky: ({r.X:0.#},{r.Y:0.#}) {r.Width:0.#}×{r.Height:0.#}mm {r.FillColorHex}");
        }
        return result;
    }

    /// <summary>Nejmenší rozumný rámeček pro obrázek (mm) - menší bývá rámeček kolem hodnoty.</summary>
    private const float MinRamecekMm = 8f;

    /// <summary>
    /// Prázdné orámované boxy předlohy - obdélník, který je vidět (má obrys), nic v něm není
    /// napsané a nepřekrývá se s jízdněřádovým prvkem ani s tím, co už v šabloně je. V tiskových
    /// sestavách to bývá místo vyhrazené pro logo; import z něj udělá prázdný obrázkový prvek,
    /// takže uživateli stačí nahrát soubor.
    /// </summary>
    private static List<ExtractedRectangle> PrazdneRamecky(
        ExtractedPage page, AiVisionResult vision,
        List<TemplateElement> jizVlozene, List<TemplateElement> existujici)
    {
        var scheduleBoxes = vision.Elements
            .Select(e => (L: e.Left * page.WidthMm, T: e.Top * page.HeightMm,
                          R: e.Right * page.WidthMm, B: e.Bottom * page.HeightMm))
            .ToList();

        // Místo pro obrázek se hledá jen v ZÁHLAVÍ sestavy (nad jízdním řádem). Prázdných
        // orámovaných boxů bývá v předloze víc - vedle jízdního řádu a pod ním jsou to jen
        // vyhrazené plochy rozvržení (pokračování sloupce poznámek), ne sloty na logo.
        float zahlaviDo = scheduleBoxes.Count > 0
            ? scheduleBoxes.Min(b => b.T)
            : page.HeightMm * 0.2f;

        var result = new List<ExtractedRectangle>();
        foreach (var r in page.Rectangles)
        {
            if (r.Width < MinRamecekMm || r.Height < MinRamecekMm) continue;
            if (r.Y + r.Height / 2 >= zahlaviDo) continue;
            if (r.StrokeColorHex is null || r.StrokeColorHex == ColorHex.Transparent) continue;

            bool obsahujeText = page.Words.Any(w =>
                w.X + w.Width / 2 > r.X && w.X + w.Width / 2 < r.X + r.Width
                && w.Y + w.Height / 2 > r.Y && w.Y + w.Height / 2 < r.Y + r.Height);
            if (obsahujeText) continue;

            float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
            if (scheduleBoxes.Any(b => cx >= b.L && cx <= b.R && cy >= b.T && cy <= b.B)) continue;

            bool prekryv = jizVlozene.Concat(existujici).Any(e =>
                cx >= e.X && cx <= e.X + e.Width && cy >= e.Y && cy <= e.Y + (e.Height ?? 0));
            if (prekryv) continue;

            result.Add(r);
        }
        return result;
    }

    /// <summary>
    /// Ověří v PdfPig geometrii, že v okolí bodu je QR-like shluk malých čtverečků.
    /// Anti-halucinační guard: BarcodeElement se vytváří jen s tímto důkazem.
    /// </summary>
    private static bool LooksLikeQrRegion(ExtractedPage page, float centerXMm, float centerYMm)
    {
        const float radius = 12f;
        int smallSquares = page.Rectangles.Count(r =>
            r.Width is > 0.1f and < 3f && r.Height is > 0.1f and < 3f
            && Math.Abs(r.Width - r.Height) < 0.3f
            && Math.Abs(r.X + r.Width / 2 - centerXMm) < radius
            && Math.Abs(r.Y + r.Height / 2 - centerYMm) < radius);
        return smallSquares >= 15;
    }

    /// <summary>
    /// Natáhne StopList po spodní okraj - najde nejspodnější bod ostatních schedule elementů.
    /// </summary>
    private static void StretchStopList(List<TemplateElement> elements, List<string> debug)
    {
        if (elements.OfType<StopListElement>().FirstOrDefault() is StopListElement stopEl
            && stopEl.Height is float stopH)
        {
            // Spodní okraj = max z (vlastní spodek, spodek ostatních schedule elementů, spodek stránky - margin)
            float maxBottom = elements
                .Where(e => e is not StopListElement)
                .Select(e => e.Y + (e.Height ?? 0))
                .DefaultIfEmpty(stopEl.Y + stopH)
                .Max();
            // Jen natáhnout, ne zkrátit
            if (maxBottom > stopEl.Y + stopH + 2)
            {
                debug.Add($"Stretching StopList: {stopH:0.#}mm → {maxBottom - stopEl.Y:0.#}mm");
                stopEl.Height = maxBottom - stopEl.Y;
            }
        }
    }

    // ── Per-group color extraction ─────────────────────────────────────

    private record GroupColorInfo(string HeaderBgColor, string? AlternatingRowColor);

    /// <summary>
    /// Analyzuje PdfPig obdélníky uvnitř DepartureGrid regionu a extrahuje
    /// per-group barevné schéma (hlavička + zebra) dle X pozice.
    /// </summary>
    private static List<GroupColorInfo> ExtractPerGroupColors(
        AiVisionElement gridAi, ExtractedPage page, float pageW, float pageH)
    {
        var result = new List<GroupColorInfo>();
        if (gridAi.DayGroups.Count == 0) return result;

        float gridLeft = gridAi.Left * pageW;
        float gridTop = gridAi.Top * pageH;
        float gridRight = gridAi.Right * pageW;
        float gridBottom = gridAi.Bottom * pageH;

        // Obdélníky uvnitř grid regionu s fill barvou (ne průhlednou)
        var gridRects = page.Rectangles
            .Where(r => r.FillColorHex is not null
                && r.FillColorHex != "#00FFFFFF"
                && r.X >= gridLeft - 2 && r.X + r.Width <= gridRight + 4
                && r.Y >= gridTop - 2 && r.Y + r.Height <= gridBottom + 4)
            .ToList();

        if (gridRects.Count == 0) return result;

        // Pro každou skupinu dnů najít obdélníky dle X pozice
        var sortedGroups = gridAi.DayGroups.OrderBy(g => g.RelativeLeft).ToList();

        for (int i = 0; i < sortedGroups.Count; i++)
        {
            float groupLeftMm = sortedGroups[i].RelativeLeft * pageW;
            float groupRightMm = (i + 1 < sortedGroups.Count)
                ? sortedGroups[i + 1].RelativeLeft * pageW
                : gridRight;

            var groupRects = gridRects
                .Where(r => r.X + r.Width / 2 >= groupLeftMm - 2
                         && r.X + r.Width / 2 <= groupRightMm + 2)
                .ToList();

            if (groupRects.Count == 0)
            {
                result.Add(new GroupColorInfo("#FFE0E0E0", null));
                continue;
            }

            // Hlavička = horní obdélník (nejblíž k gridTop). Vodorovné čáry a tmavé pruhy se
            // musí přeskočit: čára je v PDF taky „vyplněný obdélník", jen tenká, a když se vzala
            // jako pozadí hlavičky, vyšlo záhlaví skupiny dnů celé černé (nález Jan 18. 8.).
            var headerRect = groupRects
                .Where(r => r.Y < gridTop + HeaderRectYToleranceMm
                            && r.Height >= MinHeaderRectHeightMm
                            && !JeTmava(r.FillColorHex))
                .OrderBy(r => r.Y)
                .FirstOrDefault();
            string headerColor = headerRect?.FillColorHex is not null
                ? NormalizeHexColor(headerRect.FillColorHex)
                : "#FFE0E0E0";

            // Zebra = nejčastější barva datových obdélníků (pod hlavičkou)
            string? zebraColor = null;
            var dataRects = groupRects.Where(r => r.Y > gridTop + HeaderDataSeparatorOffsetMm).ToList();
            if (dataRects.Count >= 2)
            {
                var mostCommon = dataRects
                    .Where(r => r.FillColorHex is not null)
                    .GroupBy(r => r.FillColorHex!)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault();
                if (mostCommon is not null)
                    zebraColor = NormalizeHexColor(mostCommon.Key);
            }

            result.Add(new GroupColorInfo(headerColor, zebraColor));
        }

        return result;
    }

    /// <summary>
    /// Normalizuje "#RRGGBB" na "#FFRRGGBB" (přidá neprůhledný alfa kanál).
    /// "#AARRGGBB" nechá beze změny. Neplatný formát (špatná délka nebo ne-hex znaky)
    /// vrátí fallback "#FFE0E0E0" (světle šedá).
    /// </summary>
    private static string NormalizeHexColor(string color)
    {
        const string fallback = "#FFE0E0E0";
        if (string.IsNullOrEmpty(color) || color[0] != '#') return fallback;

        string hex = color[1..];
        if (hex.Length != 6 && hex.Length != 8) return fallback;
        foreach (char c in hex)
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                return fallback;

        return hex.Length == 6 ? "#FF" + hex : color;
    }

    // ── PdfPig measurements ─────────────────────────────────────────────

    private record GridMeasurements(
        float MinuteFontSizePt,
        float HeaderFontSizePt,
        float RowHeightMm,
        float HourColumnWidthMm,
        float MinuteCellWidthMm,
        float HeaderHeightMm,
        string FontFamily = "Arial");

    private record RegionFontMeasurement(
        float DataFontSizePt, float HeaderFontSizePt, float RowHeightMm,
        string FontFamily = "Arial");

    /// <summary>
    /// Vrátí slova z PdfPig která leží uvnitř AI regionu (s malou tolerancí).
    /// </summary>
    private static List<ExtractedWord> GetWordsInRegion(AiVisionElement ai, ExtractedPage page)
    {
        float pageW = page.WidthMm;
        float pageH = page.HeightMm;
        float left = ai.Left * pageW;
        float top = ai.Top * pageH;
        float right = ai.Right * pageW;
        float bottom = ai.Bottom * pageH;

        return page.Words.Where(w =>
            w.X >= left - 2 && w.X + 2 <= right + 4
            && w.Y >= top - 2 && w.Y <= bottom + 2).ToList();
    }

    /// <summary>
    /// Změří font size z PdfPig slov v libovolném regionu.
    /// Vrátí medián fontu datových slov a (pokud má header) fontu záhlaví.
    /// </summary>
    private static RegionFontMeasurement MeasureRegionFont(
        AiVisionElement ai, ExtractedPage page, float headerHeightMm = 0)
    {
        var words = GetWordsInRegion(ai, page);
        if (words.Count == 0) return new RegionFontMeasurement(0, 0, 0);

        float pageH = page.HeightMm;
        float regionTop = ai.Top * pageH;
        float headerBottomY = regionTop + headerHeightMm;

        // Rozdělit na header a data slova
        List<ExtractedWord> headerWords, dataWords;
        if (headerHeightMm > 2)
        {
            headerWords = words.Where(w => w.Y < headerBottomY + 1).ToList();
            dataWords = words.Where(w => w.Y >= headerBottomY - 1).ToList();
        }
        else
        {
            headerWords = [];
            dataWords = words;
        }

        float dataFont = dataWords.Count > 0
            ? Median(dataWords.Select(w => w.FontSizePt)) : 0;
        float headerFont = headerWords.Count > 0
            ? PrevazujiciVelikostPisma(headerWords) : dataFont;

        // Row height: medián vzdáleností mezi po sobě jdoucími Y
        float rowHeight = MeasureRowHeight(dataWords);

        // Dominant font family z datových slov
        string fontFamily = DetectDominantFontFamily(dataWords.Count > 0 ? dataWords : words);

        return new RegionFontMeasurement(dataFont, headerFont, rowHeight, fontFamily);
    }

    /// <summary>
    /// Změří DepartureGrid-specifické metriky (minutový font, hour column width, atd.).
    /// </summary>
    private static GridMeasurements MeasureGrid(AiVisionElement gridAi, ExtractedPage page)
    {
        float pageW = page.WidthMm;
        float pageH = page.HeightMm;

        float gridLeft = gridAi.Left * pageW;
        float gridTop = gridAi.Top * pageH;

        var gridWords = GetWordsInRegion(gridAi, page);
        if (gridWords.Count == 0)
            return new GridMeasurements(0, 0, 0, 0, 0, 0);

        // Minutové číslice (1-2 znaky, 0-59)
        var minuteWords = gridWords
            .Where(w => w.Text.Length <= 2 && int.TryParse(w.Text, out int n) && n >= 0 && n <= 59)
            .ToList();

        float minuteFont = minuteWords.Count > 0
            ? Median(minuteWords.Select(w => w.FontSizePt))
            : Median(gridWords.Select(w => w.FontSizePt));

        // Hodinové číslice (pro detekci řádků)
        var hourWords = gridWords
            .Where(w => w.Text.Length <= 2 && int.TryParse(w.Text, out int n)
                     && n >= HourDigitDetectionMin && n <= HourDigitDetectionMax)
            .OrderBy(w => w.Y)
            .ToList();

        // Row height z hodinových číslic
        float rowHeight = 0;
        if (hourWords.Count >= 2)
        {
            var diffs = new List<float>();
            for (int i = 1; i < hourWords.Count; i++)
            {
                float diff = hourWords[i].Y - hourWords[i - 1].Y;
                if (diff > 1.5f && diff < 10f)
                    diffs.Add(diff);
            }
            if (diffs.Count > 0)
                rowHeight = Median(diffs);
        }

        // Hour column width: vzdálenost od levé hrany gridu k prvním minutám
        float hourColWidth = 0;
        if (minuteWords.Count > 0 && hourWords.Count > 0)
        {
            float minMinuteX = minuteWords.Min(w => w.X);
            hourColWidth = minMinuteX - gridLeft;
            if (hourColWidth < 3) hourColWidth = 6;
        }

        // Minute cell width: medián vzdáleností mezi sousedními minutami na řádku
        float minuteCellWidth = 0;
        if (minuteWords.Count >= 2)
        {
            var byRow = minuteWords
                .GroupBy(w => Math.Round(w.Y / (rowHeight > 0 ? rowHeight : 3), 0))
                .Where(g => g.Count() >= 3)
                .ToList();

            var cellWidths = new List<float>();
            foreach (var row in byRow)
            {
                var sorted = row.OrderBy(w => w.X).ToList();
                for (int i = 1; i < sorted.Count; i++)
                {
                    float diff = sorted[i].X - sorted[i - 1].X;
                    if (diff > 1f && diff < 15f)
                        cellWidths.Add(diff);
                }
            }
            if (cellWidths.Count > 0)
                minuteCellWidth = Median(cellWidths);
        }

        // Header height: vzdálenost od gridTop k prvnímu hodinovému řádku
        float headerHeight = 0;
        if (hourWords.Count > 0)
        {
            headerHeight = hourWords[0].Y - gridTop;
            if (headerHeight < 2) headerHeight = 5;
        }

        // Header font: slova nad prvním datovým řádkem
        float headerFont = minuteFont;
        if (headerHeight > 2 && hourWords.Count > 0)
        {
            var headerWords = gridWords.Where(w => w.Y < hourWords[0].Y - 0.5f).ToList();
            if (headerWords.Count > 0)
                headerFont = PrevazujiciVelikostPisma(headerWords);
        }

        string fontFamily = DetectDominantFontFamily(minuteWords.Count > 0 ? minuteWords : gridWords);

        return new GridMeasurements(minuteFont, headerFont, rowHeight, hourColWidth, minuteCellWidth, headerHeight, fontFamily);
    }

    /// <summary>
    /// Výška řádku = obvyklá vzdálenost mezi <b>řádky</b>, ne mezi jednotlivými slovy. Slova
    /// jednoho řádku nemají v PDF přesně stejnou Y (různá velikost písma, dolní dotažnice), takže
    /// brát každou odlišnou Y jako nový řádek vrací hodnotu menší, než jaká je ve skutečnosti -
    /// řádky pak v šabloně vyjdou natěsno. Slova se proto nejdřív shluknou do řádků; hranice
    /// shluku se odvozuje z výšky písma, aby pravidlo platilo pro drobné i velké sestavy.
    /// </summary>
    internal static float MeasureRowHeight(List<ExtractedWord> words)
    {
        if (words.Count < 2) return 0;

        float typickaVyskaPisma = Median(words.Select(w => w.FontSizePt)) * ResolvedTextStyle.PtToMm;
        float tolerance = Math.Max(typickaVyskaPisma * 0.6f, 0.4f);

        var radky = new List<float>();
        foreach (float y in words.Select(w => w.Y).OrderBy(y => y))
            if (radky.Count == 0 || y - radky[^1] > tolerance)
                radky.Add(y);

        if (radky.Count < 2) return 0;
        var diffs = new List<float>();
        for (int i = 1; i < radky.Count; i++)
        {
            float diff = radky[i] - radky[i - 1];
            if (diff > 1f && diff < 10f)
                diffs.Add(diff);
        }
        return diffs.Count > 0 ? Median(diffs) : 0;
    }

    /// <summary>Detekuje nejčastější font family z PdfPig slov (mapped přes PdfFontMapper).</summary>
    private static string DetectDominantFontFamily(List<ExtractedWord> words)
    {
        if (words.Count == 0) return "Arial";
        var dominant = words
            .Where(w => !string.IsNullOrWhiteSpace(w.FontFamily))
            .GroupBy(w => w.FontFamily)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();
        return dominant?.Key ?? "Arial";
    }

    /// <summary>
    /// Velikost písma, kterou je vysázená <b>většina znaků</b> dané oblasti. Prostřední hodnota
    /// (medián) tu selhává: v pásu záhlaví bývá vedle popisku skupiny ještě několik drobných
    /// pomocných popisků (nad sloupci) a těch je početně víc, takže se za písmo záhlaví vzalo to
    /// nejmenší a nadpis se vytiskl drobně. Váha podle počtu znaků odpovídá tomu, co je na papíře
    /// vidět; při shodě vyhrává větší písmo (nadpis, ne pomocný popisek).
    /// </summary>
    internal static float PrevazujiciVelikostPisma(List<ExtractedWord> words)
    {
        if (words.Count == 0) return 0;
        return words
            .GroupBy(w => MathF.Round(w.FontSizePt, 1))
            .OrderByDescending(g => g.Sum(w => w.Text.Length))
            .ThenByDescending(g => g.Key)
            .First().Key;
    }

    /// <summary>Medián z kolekce floatů.</summary>
    private static float Median(IEnumerable<float> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        return sorted.Count > 0 ? sorted[sorted.Count / 2] : 0;
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    internal static List<StopColumn> BuildStopColumnsFromAi(AiVisionElement stopListAi, float totalWidthMm)
    {
        var columns = new List<StopColumn>();

        if (stopListAi.Columns.Contains("TravelTime"))
        {
            columns.Add(new StopColumn
            {
                Key = StopFields.TravelTime,
                Header = StopColumnHeaders.TravelTime,
                Width = Math.Min(totalWidthMm * 0.2f, 10f),
                Visible = true,
                Alignment = TextHorizontalAlignment.Right
            });
        }

        // Tarifní číslo agendy nevystavují (nese jen interní klíč) - poziční potřebu kryje PORADI.
        if (stopListAi.Columns.Contains("TariffNumber"))
        {
            columns.Add(new StopColumn
            {
                Key = StopFields.Poradi,
                Header = StopColumnHeaders.Poradi,
                Width = Math.Min(totalWidthMm * 0.15f, 8f),
                Visible = true,
                Alignment = TextHorizontalAlignment.Center
            });
        }

        if (stopListAi.Columns.Contains("Platform"))
        {
            columns.Add(new StopColumn
            {
                Key = StopFields.Nastupiste,
                Header = StopColumnHeaders.Nastupiste,
                Width = Math.Min(totalWidthMm * 0.15f, 10f),
                Visible = true,
                Alignment = TextHorizontalAlignment.Center
            });
        }

        float remainingWidth = totalWidthMm - columns.Sum(c => c.Width ?? 0f);

        if (stopListAi.Columns.Contains("Zone"))
        {
            float zoneWidth = Math.Min(totalWidthMm * 0.15f, 8f);
            remainingWidth -= zoneWidth;
            columns.Add(new StopColumn
            {
                Key = StopFields.StopName,
                Header = StopColumnHeaders.StopName,
                Width = Math.Max(remainingWidth, 15f),
                Visible = true,
                Alignment = TextHorizontalAlignment.Left
            });
            columns.Add(new StopColumn
            {
                Key = StopFields.Zone,
                Header = "Zóna",
                Width = zoneWidth,
                Visible = true,
                Alignment = TextHorizontalAlignment.Center
            });
        }
        else
        {
            columns.Add(new StopColumn
            {
                Key = StopFields.StopName,
                Header = StopColumnHeaders.StopName,
                Width = Math.Max(remainingWidth, 15f),
                Visible = true,
                Alignment = TextHorizontalAlignment.Left
            });
        }

        StopColumnHeaders.InsertVariantColumn(columns);
        return columns;
    }

    private static float EstimateGroupSeparatorFromAi(AiVisionElement gridAi, float gridWidthMm)
    {
        if (gridAi.DayGroups.Count < 2) return 0.5f;
        float avgGroupWidth = gridWidthMm / gridAi.DayGroups.Count;
        return Math.Clamp(avgGroupWidth * 0.01f, 0.3f, 2f);
    }

    /// <summary>
    /// Najde textový blok v PdfPig datech nejbližší AI mapovanému textu.
    /// Strategie: normalizovaná shoda → obsahuje → slovní průnik → pozice.
    /// </summary>
    private static ExtractedTextBlock? FindMatchingTextBlock(
        ExtractedPage page, AiVisionTextMapping mapping,
        float mappingXMm, float mappingYMm)
    {
        var textBlocks = PdfTextGrouper.GroupWords(page.Words);
        string aiText = NormalizeText(mapping.Text);

        // 1. Přesná normalizovaná shoda
        var exact = textBlocks.FirstOrDefault(b =>
            string.Equals(NormalizeText(b.Text), aiText, StringComparison.OrdinalIgnoreCase));
        if (exact is not null) return exact;

        // Částečná shoda platí jen v okolí místa od modelu: krátká slova („od", „do", čísla) se
        // najdou kdekoli na stránce a text pak skončil o 100 mm vedle (platnost u výlepu, 2. 10.).
        bool hasPosition = mappingXMm > 0 || mappingYMm > 0;
        var nearBlocks = textBlocks.Where(b => !hasPosition
            || (Math.Abs(b.X - mappingXMm) < PartialMatchRadiusMm && Math.Abs(b.Y - mappingYMm) < PartialMatchRadiusMm))
            .ToList();

        // 2. Obsahuje (obousměrně). Blok uvnitř textu od modelu musí být aspoň jeho polovina -
        //    jinak se dlouhý řádek s dopravcem přichytil k „940", protože je to kus „771940".
        var contains = nearBlocks.FirstOrDefault(b =>
        {
            string blockText = NormalizeText(b.Text);
            return blockText.Contains(aiText, StringComparison.OrdinalIgnoreCase)
                   || (blockText.Length * 2 >= aiText.Length
                       && aiText.Contains(blockText, StringComparison.OrdinalIgnoreCase));
        });
        if (contains is not null) return contains;

        // 3. Slovní průnik: AI "Horní nádraží – Tržnice" vs PdfPig "Horní nádraží" + "Tržnice"
        var aiWords = aiText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (aiWords.Length >= 2)
        {
            ExtractedTextBlock? bestWordMatch = null;
            float bestScore = 0;

            foreach (var block in nearBlocks)
            {
                var blockWords = NormalizeText(block.Text).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                int commonWords = aiWords.Count(aw =>
                    blockWords.Any(bw => bw.Equals(aw, StringComparison.OrdinalIgnoreCase)));
                float score = (float)commonWords / aiWords.Length;

                if (score > bestScore && score >= 0.5f)
                {
                    bestScore = score;
                    bestWordMatch = block;
                }
            }

            if (bestWordMatch is not null) return bestWordMatch;
        }

        // 4. Pozice - nejbližší blok k AI souřadnicím v mm (tolerance 25mm). Blok bez jediného
        //    společného slova je cizí text (u 940 malý popisek o řádek níž) - pak radši pozice
        //    od modelu.
        if (mappingXMm > 0 || mappingYMm > 0)
        {
            var closest = textBlocks
                .Where(b => Math.Abs(b.X - mappingXMm) < 25f && Math.Abs(b.Y - mappingYMm) < 25f)
                .Where(b => NormalizeText(b.Text).Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Any(bw => bw.Length >= 3 && aiWords.Any(aw => aw.Equals(bw, StringComparison.OrdinalIgnoreCase))))
                .OrderBy(b => Math.Abs(b.X - mappingXMm) + Math.Abs(b.Y - mappingYMm))
                .FirstOrDefault();
            return closest;
        }

        return null;
    }

    /// <summary>Normalizuje text: sjednotí whitespace, odstraní speciální znaky (–, -, _).</summary>
    private static string NormalizeText(string text)
    {
        text = text.Replace('–', ' ').Replace('-', ' ').Replace('_', ' ').Replace('\t', ' ');
        while (text.Contains("  ")) text = text.Replace("  ", " ");
        return text.Trim();
    }

    // ── Property helpers ───────────────────────────────────────────────

    private static string GetProp(Dictionary<string, string> props, string key, string defaultValue)
        => props.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : defaultValue;

    private static bool GetBoolProp(Dictionary<string, string> props, string key, bool defaultValue)
        => props.TryGetValue(key, out var v) ? v.Equals("true", StringComparison.OrdinalIgnoreCase) : defaultValue;

    private static string? GetPropOrNull(Dictionary<string, string> props, string key)
    {
        if (!props.TryGetValue(key, out var v) || string.IsNullOrWhiteSpace(v)) return null;
        // Normalizovat #RGB na #AARRGGBB
        if (v.StartsWith("#") && v.Length == 7)
            return "#FF" + v[1..];
        return v;
    }

    /// <summary>Okruh (mm) kolem pozice od modelu, ve kterém platí částečná shoda textu.</summary>
    private const float PartialMatchRadiusMm = 40f;

    /// <summary>Najde obdélník obklopující textový blok (rámeček).</summary>
    /// <remarks>Rámeček, ve kterém je i jiný text, patří skupině textů, ne tomuto - text by se
    /// roztáhl přes sousední (název linky přes číslo linky v jednom rámečku, 2. 10.).</remarks>
    internal static ExtractedRectangle? FindEnclosingRectangle(
        List<ExtractedRectangle> rectangles, ExtractedTextBlock block,
        IReadOnlyList<ExtractedTextBlock>? allBlocks = null)
    {
        const float tolerance = 2f;
        ExtractedRectangle? best = null;
        float bestArea = float.MaxValue;

        foreach (var rect in rectangles)
        {
            if (rect.StrokeWidth <= 0 && string.IsNullOrEmpty(rect.StrokeColorHex)) continue;
            if (block.X < rect.X - tolerance) continue;
            if (block.Y < rect.Y - tolerance) continue;
            if (block.X + block.Width > rect.X + rect.Width + tolerance) continue;
            if (block.Y + block.Height > rect.Y + rect.Height + tolerance) continue;

            float rectArea = rect.Width * rect.Height;
            float textArea = Math.Max(block.Width, 1f) * Math.Max(block.Height, 1f);
            if (rectArea > textArea * 20f) continue;

            bool sharedWithOtherText = allBlocks?.Any(o => o != block
                && o.X + o.Width / 2 > rect.X && o.X + o.Width / 2 < rect.X + rect.Width
                && o.Y + o.Height / 2 > rect.Y && o.Y + o.Height / 2 < rect.Y + rect.Height) == true;
            if (sharedWithOtherText) continue;

            if (rectArea < bestArea) { best = rect; bestArea = rectArea; }
        }

        return best;
    }
}
