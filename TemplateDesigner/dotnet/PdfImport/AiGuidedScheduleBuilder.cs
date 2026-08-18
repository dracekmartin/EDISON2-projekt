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

        // 4. Textové elementy z AI mapování
        SkippedNumericBlocks = 0;
        UserNotes.Clear();
        var consumedBlocks = new HashSet<ExtractedTextBlock>();
        var textElements = BuildTextElements(vision, page, marginLeft, marginTop, ref zIndex, debugLines, consumedBlocks);
        elements.AddRange(textElements);

        // 4b. Korekce překryvu
        AdjustOverlaps(vision, page, elements, debugLines);

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
                Style = new ColumnVisualStyle
                {
                    Header = new TextStyle { BackgroundColor = gc.HeaderBgColor, Bold = true },
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

        return new NotesLegendElement
        {
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

    /// <summary>Vytvoří TextElement-y z AI text mapování, přeskočí texty uvnitř schedule oblastí.</summary>
    private static List<TextElement> BuildTextElements(
        AiVisionResult vision, ExtractedPage page,
        float marginLeft, float marginTop, ref int zIndex, List<string> debug,
        HashSet<ExtractedTextBlock>? consumedBlocks = null)
    {
        float pageW = page.WidthMm;
        float pageH = page.HeightMm;
        var result = new List<TextElement>();

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

            if (matchedBlock is not null)
            {
                float fontHeightMm = matchedBlock.FontSizePt * ResolvedTextStyle.PtToMm;
                int lineCount = matchedBlock.Text.Count(c => c == '\n') + 1;

                elX = matchedBlock.X - marginLeft;
                elY = matchedBlock.Y - marginTop;
                elW = Math.Max(matchedBlock.Width, fontHeightMm * 2);
                // Výška z font size (ne z PdfPig bounding boxu, který může být nafouklý)
                elH = fontHeightMm * 1.3f * lineCount;
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

            if (matchedBlock is not null)
            {
                var enclosingRect = FindEnclosingRectangle(page.Rectangles, matchedBlock);
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

            result.Add(new TextElement
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
            });
        }

        return result;
    }

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
    /// <summary>Obdélníky jízdněřádových prvků tak, jak nakonec v šabloně leží (v mm).</summary>
    private static List<(float L, float T, float R, float B)> ScheduleRects(List<TemplateElement> elements) =>
    [
        .. elements
            .Where(e => e is StopListElement or DepartureGridElement or NotesLegendElement)
            .Select(e => (e.X, e.Y, e.X + e.Width, e.Y + (e.Height ?? 0)))
    ];

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

    /// <summary>
    /// Leží blok uvnitř jízdněřádového prvku, nebo v pásu jeho záhlaví těsně nad ním? Pak si ho
    /// prvek kreslí sám a jako text do šablony nepatří.
    /// </summary>
    private static bool PatriKJizdnimuRadu(
        ExtractedTextBlock b, List<(float L, float T, float R, float B)> boxes)
    {
        float cy = b.Y + b.Height / 2;
        return boxes.Any(box =>
            b.X + b.Width > box.L && b.X < box.R          // vodorovně se překrývá s prvkem
            && cy >= box.T - ZahlaviPasMm && cy <= box.B); // svisle uvnitř nebo v pásu záhlaví
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
                if (el is StopListElement or DepartureGridElement or NotesLegendElement)
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
                // Zástupný šedý rámeček se nevkládá: v předloze na tom místě žádný obrázek není
                // (model si ho vymyslel, nebo je to jen orámovaná plocha) a prázdný čtverec
                // v šabloně vypadá jako chyba importu. Informace se místo toho napíše do protokolu.
                UserNotes.Add($"Model hlásil obrázek ({RoleCesky(imgMap.Role)}) na pozici "
                              + $"{imgX:0.#}, {imgY:0.#} mm, ale v předloze tam žádný není - "
                              + "pokud tam patří, vložte ho ručně.");
                debug.Add($"    → bez obrázku v PDF: nevkládám nic, jen do protokolu");
            }
        }

        // 7. Detekce nepokrytých grafických oblastí - oblasti stránky kde nejsou ani texty
        //    ani schedule elementy, ale mohly by obsahovat vektorovou grafiku (logo, QR kód)
        {
            var coveredAreas = new List<(float Left, float Top, float Right, float Bottom)>();

            // Schedule elementy pokrývají oblast
            foreach (var el in vision.Elements)
                coveredAreas.Add((el.Left * pageW, el.Top * pageH, el.Right * pageW, el.Bottom * pageH));

            // Textové elementy pokrývají oblast (z existingElements, ne z result)
            foreach (var te in existingElements.OfType<TextElement>())
                coveredAreas.Add((te.X, te.Y, te.X + te.Width, te.Y + (te.Height ?? 6)));

            // Obrázky/QR/placeholdery z kroků 5+6 pokrývají oblast - bez toho krok 7 položí
            // prázdný placeholder přes už přidané logo (překrývající se „falešný element").
            foreach (var el in result)
                coveredAreas.Add((el.X, el.Y, el.X + el.Width, el.Y + (el.Height ?? 6)));

            // Najít prázdné oblasti v záhlaví stránky (nad schedule elementy)
            float scheduleTop = vision.Elements.Count > 0
                ? vision.Elements.Min(e => e.Top) * pageH
                : pageH * 0.15f;

            if (scheduleTop > MinHeaderSpaceAboveScheduleMm)
            {
                // Rozdělit záhlaví na sloupce a najít nepokryté
                const int cols = 6;
                float colW = pageW / cols;

                for (int c = 0; c < cols; c++)
                {
                    float colLeft = c * colW;
                    float colRight = (c + 1) * colW;
                    float colMidX = (colLeft + colRight) / 2;
                    float colMidY = scheduleTop / 2;

                    bool isCovered = coveredAreas.Any(a =>
                        colMidX >= a.Left - 2 && colMidX <= a.Right + 2
                        && colMidY >= a.Top - 2 && colMidY <= a.Bottom + 2);

                    if (!isCovered)
                    {
                        // Nepokrytá oblast - vytvořit prázdný ImageElement placeholder
                        float placeholderW = colW - 4;
                        float placeholderH = Math.Min(scheduleTop - 4, placeholderW); // max čtverec
                        if (placeholderW < 8 || placeholderH < 8) continue;

                        result.Add(new ImageElement
                        {
                            Id = Guid.NewGuid(),
                            X = Math.Max(0, colLeft + 2 - marginLeft),
                            Y = Math.Max(0, 2 - marginTop),
                            Width = placeholderW,
                            Height = placeholderH,
                            ZIndex = zIndex++,
                            ImageBase64 = null, // prázdný placeholder - uživatel nahraje obrázek
                        });
                        debug.Add($"  → Empty area ImageElement placeholder at col {c}: ({colLeft + 2:0.#},{2:0.#}) {placeholderW:0.#}×{placeholderH:0.#}mm");
                    }
                }
            }
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
            ? Median(headerWords.Select(w => w.FontSizePt)) : dataFont;

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
                headerFont = Median(headerWords.Select(w => w.FontSizePt));
        }

        string fontFamily = DetectDominantFontFamily(minuteWords.Count > 0 ? minuteWords : gridWords);

        return new GridMeasurements(minuteFont, headerFont, rowHeight, hourColWidth, minuteCellWidth, headerHeight, fontFamily);
    }

    /// <summary>Medián výšek řádků (vzdáleností po sobě jdoucích Y) z PdfPig slov.</summary>
    private static float MeasureRowHeight(List<ExtractedWord> words)
    {
        if (words.Count < 2) return 0;
        var sortedY = words.Select(w => w.Y).Distinct().OrderBy(y => y).ToList();
        if (sortedY.Count < 2) return 0;
        var diffs = new List<float>();
        for (int i = 1; i < sortedY.Count; i++)
        {
            float diff = sortedY[i] - sortedY[i - 1];
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

        // 2. Obsahuje (obousměrně)
        var contains = textBlocks.FirstOrDefault(b =>
        {
            string blockText = NormalizeText(b.Text);
            return blockText.Contains(aiText, StringComparison.OrdinalIgnoreCase)
                   || aiText.Contains(blockText, StringComparison.OrdinalIgnoreCase);
        });
        if (contains is not null) return contains;

        // 3. Slovní průnik: AI "Horní nádraží – Tržnice" vs PdfPig "Horní nádraží" + "Tržnice"
        var aiWords = aiText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (aiWords.Length >= 2)
        {
            ExtractedTextBlock? bestWordMatch = null;
            float bestScore = 0;

            foreach (var block in textBlocks)
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

        // 4. Pozice - nejbližší blok k AI souřadnicím v mm (tolerance 25mm)
        if (mappingXMm > 0 || mappingYMm > 0)
        {
            var closest = textBlocks
                .Where(b => Math.Abs(b.X - mappingXMm) < 25f && Math.Abs(b.Y - mappingYMm) < 25f)
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

    /// <summary>Najde obdélník obklopující textový blok (rámeček).</summary>
    private static ExtractedRectangle? FindEnclosingRectangle(
        List<ExtractedRectangle> rectangles, ExtractedTextBlock block)
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

            if (rectArea < bestArea) { best = rect; bestArea = rectArea; }
        }

        return best;
    }
}
