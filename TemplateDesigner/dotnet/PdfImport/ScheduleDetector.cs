using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.PdfImport;

/// <summary>
/// Typ importu — uživatel volí apriorně v PdfImportDialog.
/// Engine ví, jakou strukturu hledá, a použije odpovídající heuristiky.
/// </summary>
public enum ScheduleImportType
{
    /// <summary>Obecný import — žádná JŘ detekce.</summary>
    None,
    /// <summary>Zastávkový JŘ MHD (MinutesOnly) — StopList + DepartureGrid + NotesLegend.</summary>
    ZastavkovyJrMhd
}

/// <summary>
/// Detekovaná oblast na stránce PDF.
/// </summary>
public record DetectedRegion(float X, float Y, float Width, float Height);

/// <summary>
/// Výsledek detekce zastávkového JŘ z jedné stránky PDF.
/// </summary>
public class ScheduleDetectionResult
{
    /// <summary>Oblast se zastávkami (vlevo).</summary>
    public DetectedRegion? StopListRegion { get; init; }

    /// <summary>Oblast s odjezdovou mřížkou (vpravo od zastávek).</summary>
    public DetectedRegion? DepartureGridRegion { get; init; }

    /// <summary>Detekované skupiny dnů (sloupce v mřížce odjezdů).</summary>
    public List<DetectedDayGroup> DayGroups { get; init; } = [];

    /// <summary>Oblast poznámek (pod mřížkou nebo jinde).</summary>
    public DetectedRegion? NotesRegion { get; init; }

    /// <summary>Texty mimo rozpoznané oblasti → generic TextElement.</summary>
    public List<ExtractedTextBlock> UnmappedTexts { get; init; } = [];

    /// <summary>Obrázky mimo rozpoznané oblasti → ImageElement.</summary>
    public List<ExtractedImage> UnmappedImages { get; init; } = [];

    // ── Změřené rozměry z PDF ──────────────────────────────────────────────

    /// <summary>Šířka sloupce hodin v mm.</summary>
    public float HourColumnWidth { get; init; }

    /// <summary>Šířka jedné minutové buňky v mm.</summary>
    public float MinuteCellWidth { get; init; }

    /// <summary>Výška datového řádku v mm.</summary>
    public float DataRowHeight { get; init; }

    /// <summary>Výška záhlaví skupin dnů v mm.</summary>
    public float HeaderHeight { get; init; }

    /// <summary>Font size minut v pt.</summary>
    public float MinuteFontSizePt { get; init; }

    /// <summary>Font size zastávek v pt.</summary>
    public float StopFontSizePt { get; init; }

    /// <summary>Šířky sloupců zastávek (Čas, Zastávka, ...) v mm.</summary>
    public List<float> StopColumnWidths { get; init; } = [];

    /// <summary>Spolehlivost detekce 0–1. Pod prahem → AI fallback.</summary>
    public float Confidence { get; init; }
}

/// <summary>Detekovaná skupina dnů v mřížce odjezdů.</summary>
public record DetectedDayGroup(string HeaderText, float X, float Width);

/// <summary>
/// Heuristický detektor zastávkového JŘ z extrahované PDF stránky.
/// Strategie: najdi minutovou mřížku (čísla 0–59 v pravidelném rastru) jako kotvu,
/// pak odvoď ostatní oblasti relativně.
/// </summary>
public static class ScheduleDetector
{
    private const float Tolerance = 1.5f; // mm tolerance pro zarovnání

    /// <summary>
    /// Pokus o detekci zastávkového JŘ z extrahované stránky.
    /// Vrátí null pokud stránka neobsahuje rozpoznatelný JŘ.
    /// </summary>
    public static ScheduleDetectionResult? DetectZastavkovyJr(ExtractedPage page)
    {
        // Krok 1: Najdi všechna malá čísla (0–59, font <=14pt)
        var smallNumbers = FindMinuteNumbers(page.Words);
        if (smallNumbers.Count < 10)
            return null;

        // Krok 2: Najdi hodinové sloupce — vertikální sekvence čísel 0–24 na stejné X pozici.
        // JŘ typicky má 1–3 hodinové sloupce (per skupina dnů nebo sdílený).
        // Klíč: hodiny tvoří sekvenci (5,6,7,...,19) s pravidelným Y rozestupem.
        var hourColumns = FindHourColumns(smallNumbers, page.Words);
        if (hourColumns.Count == 0)
            return null; // žádný hodinový sloupec → ne JŘ

        // Hodinový sloupec nejblíže levému okraji = primární referenční bod
        var primaryHourCol = hourColumns.OrderBy(c => c.x).First();

        // Krok 3: Minuty = čísla vpravo od prvního hodinového sloupce
        float minuteAreaLeft = primaryHourCol.x + primaryHourCol.width + 1f;
        var minuteWords = smallNumbers
            .Where(w => w.X >= minuteAreaLeft)
            .ToList();

        if (minuteWords.Count < 5)
            return null;

        // Krok 4: Seskup minuty do řádků
        var minuteRows = GroupByRows(minuteWords);
        if (minuteRows.Count < 3)
            return null;

        // Krok 5: Změř rozměry
        float minuteAreaRight = minuteWords.Max(w => w.X + w.Width);
        float gridTop = minuteRows.First().y;
        float gridBottom = minuteRows.Last().y + minuteRows.Last().height;

        float hourColWidth = primaryHourCol.width;
        float avgRowHeight = CalculateAverageRowSpacing(minuteRows);
        float avgMinuteWidth = EstimateMinuteCellWidth(minuteWords, minuteRows);

        // Font sizes
        float minuteFontSize = minuteWords.Average(w => w.FontSizePt);
        var allHourWords = hourColumns.SelectMany(c => c.words).ToList();

        // Krok 6: Najdi zastávky — texty vlevo od prvního hodinového sloupce
        var stopWords = FindStopNames(page.Words, primaryHourCol.x, minuteRows);
        // Sloupec jízdních dob (travel times): čísla vlevo od zastávek, zarovnaná s řádky mřížky
        var travelTimeWords = FindTravelTimeNumbers(page.Words, primaryHourCol.x, minuteRows);
        // "min." label nad sloupcem jízdních dob — zahrnout do oblasti
        var minLabel = page.Words.FirstOrDefault(w =>
            w.Text.Equals("min.", StringComparison.OrdinalIgnoreCase)
            && w.X < primaryHourCol.x);
        float stopAreaLeft = Math.Min(
            stopWords.Count > 0 ? stopWords.Min(w => w.X) : primaryHourCol.x,
            travelTimeWords.Count > 0 ? travelTimeWords.Min(w => w.X) : primaryHourCol.x);
        if (minLabel is not null)
            stopAreaLeft = Math.Min(stopAreaLeft, minLabel.X);
        float stopAreaRight = primaryHourCol.x;
        float stopFontSize = stopWords.Count > 0
            ? stopWords.Average(w => w.FontSizePt) : 8f;

        // Krok 7: Záhlaví skupin dnů — využij pozice hodinových sloupců jako kotvy
        var dayGroups = DetectDayGroupsFromHourColumns(page.Words, hourColumns,
            minuteAreaRight, gridTop);

        float headerHeight = dayGroups.Count > 0
            ? gridTop - dayGroups.Min(g => g.headerY) : 5f;

        // Krok 8: Poznámky (texty pod mřížkou)
        float belowGrid = gridBottom + Tolerance;
        var notesTexts = page.Words
            .Where(w => w.Y > belowGrid && w.FontSizePt < minuteFontSize + 2)
            .ToList();

        DetectedRegion? notesRegion = null;
        if (notesTexts.Count >= 2)
        {
            notesRegion = new DetectedRegion(
                notesTexts.Min(w => w.X),
                notesTexts.Min(w => w.Y),
                notesTexts.Max(w => w.X + w.Width) - notesTexts.Min(w => w.X),
                notesTexts.Max(w => w.Y + w.Height) - notesTexts.Min(w => w.Y));
        }

        // Krok 9: Sestav výsledek
        float gridLeft = primaryHourCol.x;
        float gridTopWithHeader = gridTop - headerHeight;

        // StopList Y začíná buď od gridTop nebo od "min." labelu (pokud je výš)
        float stopTop = gridTop;
        if (minLabel is not null)
            stopTop = Math.Min(stopTop, minLabel.Y);

        var stopListRegion = new DetectedRegion(
            stopAreaLeft, stopTop,
            stopAreaRight - stopAreaLeft,
            gridBottom - stopTop);

        var departureGridRegion = new DetectedRegion(
            gridLeft, gridTopWithHeader,
            minuteAreaRight - gridLeft,
            gridBottom - gridTopWithHeader);

        // Unmapped content
        var textBlocks = PdfTextGrouper.GroupWords(page.Words);
        var mappedYRange = (top: Math.Min(gridTopWithHeader, stopTop) - Tolerance,
                           bottom: (notesRegion?.Y + notesRegion?.Height ?? gridBottom) + Tolerance);
        var mappedXRange = (left: stopAreaLeft - Tolerance,
                           right: minuteAreaRight + Tolerance);

        var unmappedTexts = textBlocks
            .Where(b => !IsInRange(b.X, b.Y, b.Width, b.Height, mappedXRange, mappedYRange))
            .ToList();

        // Detekce poznámkových párů (symbol + popis) — typicky "D na znamení", "x přestup na žel."
        // Tyto texty patří do NotesLegend, ne jako samostatné TextElementy.
        var noteLegendBlocks = FindNoteLegendBlocks(unmappedTexts);
        if (noteLegendBlocks.Count > 0)
        {
            unmappedTexts = unmappedTexts.Except(noteLegendBlocks).ToList();

            // Rozšířit notes region aby zahrnul legendové bloky
            if (notesRegion is not null || noteLegendBlocks.Count >= 2)
            {
                float nLeft = notesRegion?.X ?? noteLegendBlocks.Min(b => b.X);
                float nTop = notesRegion?.Y ?? noteLegendBlocks.Min(b => b.Y);
                float nRight = Math.Max(
                    notesRegion is not null ? notesRegion.X + notesRegion.Width : 0,
                    noteLegendBlocks.Max(b => b.X + b.Width));
                float nBottom = Math.Max(
                    notesRegion is not null ? notesRegion.Y + notesRegion.Height : 0,
                    noteLegendBlocks.Max(b => b.Y + b.Height));
                notesRegion = new DetectedRegion(nLeft, nTop, nRight - nLeft, nBottom - nTop);
            }
        }

        var unmappedImages = page.Images
            .Where(i => !IsInRange(i.X, i.Y, i.Width, i.Height, mappedXRange, mappedYRange))
            .ToList();

        float confidence = Math.Min(1f, minuteWords.Count / 20f)
                         * Math.Min(1f, allHourWords.Count / 5f)
                         * (stopWords.Count > 0 ? 1f : 0.5f);

        return new ScheduleDetectionResult
        {
            StopListRegion = stopListRegion,
            DepartureGridRegion = departureGridRegion,
            DayGroups = dayGroups.Select(g => new DetectedDayGroup(g.text, g.x, g.width)).ToList(),
            NotesRegion = notesRegion,
            UnmappedTexts = unmappedTexts,
            UnmappedImages = unmappedImages,
            HourColumnWidth = Math.Max(hourColWidth, 5f),
            MinuteCellWidth = Math.Max(avgMinuteWidth, 3f),
            DataRowHeight = Math.Max(avgRowHeight, 3f),
            HeaderHeight = Math.Max(headerHeight, 3f),
            MinuteFontSizePt = minuteFontSize,
            StopFontSizePt = stopFontSize,
            StopColumnWidths = EstimateStopColumnWidths(stopWords, stopAreaLeft, primaryHourCol.x),
            Confidence = confidence
        };
    }

    // ── Krok 1: Minutová čísla ──────────────────────────────────────────────

    /// <summary>
    /// Najde slova, která jsou čísla v rozsahu 0–59 — kandidáti na minuty odjezdů.
    /// Filtruje jen krátká čísla (max 2 cifry) a ignoruje velké fonty (záhlaví).
    /// </summary>
    private static List<ExtractedWord> FindMinuteNumbers(List<ExtractedWord> words)
    {
        return words
            .Where(w =>
            {
                if (w.Text.Length > 2) return false;
                if (!int.TryParse(w.Text, out int n)) return false;
                if (n < 0 || n > 59) return false;
                // Ignorovat velké fonty (záhlaví, číslo linky)
                if (w.FontSizePt > 14) return false;
                return true;
            })
            .ToList();
    }

    // ── Krok 2: Hodinové sloupce ──────────────────────────────────────────────

    /// <summary>
    /// Najde hodinové sloupce — vertikální sekvence čísel 0–24 na podobné X pozici.
    /// Strategie: clusteruj čísla podle X, pro každý cluster ověř, že tvoří vertikální sekvenci hodin.
    /// Rozlišuje od sloupce jízdních dob (travel times vlevo) pomocí "hod." labelů.
    /// Vrací seznam (x, width, words) pro každý nalezený hodinový sloupec.
    /// </summary>
    private static List<(float x, float width, List<ExtractedWord> words)> FindHourColumns(
        List<ExtractedWord> smallNumbers, List<ExtractedWord> allWords)
    {
        // Filtruj jen čísla 0–24
        var hourCandidates = smallNumbers
            .Where(w => int.TryParse(w.Text, out int n) && n >= 0 && n <= 24)
            .ToList();

        if (hourCandidates.Count < 3)
            return [];

        // Najdi "hod." labely — silný signál pro hodinový sloupec
        var hodLabels = allWords
            .Where(w => w.Text.Equals("hod.", StringComparison.OrdinalIgnoreCase)
                     || w.Text.Equals("hod", StringComparison.OrdinalIgnoreCase)
                     || w.Text.Equals("ČAS", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Clusteruj podle X pozice (tolerance 3mm)
        const float xTolerance = 3f;
        var clusters = new List<List<ExtractedWord>>();

        foreach (var word in hourCandidates.OrderBy(w => w.X))
        {
            var match = clusters.FirstOrDefault(c =>
                Math.Abs(c.Average(w => w.X) - word.X) < xTolerance);

            if (match is not null)
                match.Add(word);
            else
                clusters.Add([word]);
        }

        // Pro každý cluster: ověř, zda tvoří monotónní sekvenci hodin s pravidelným Y rozestupem
        var candidates = new List<(float x, float width, List<ExtractedWord> words, bool hasHodLabel)>();

        foreach (var cluster in clusters)
        {
            if (cluster.Count < 4) continue; // potřebujeme alespoň 4 hodiny

            // Seřaď podle Y (shora dolů)
            var sorted = cluster.OrderBy(w => w.Y).ToList();

            // Parsuj hodnoty
            var values = sorted.Select(w => (word: w, val: int.Parse(w.Text))).ToList();

            // Spočítej kolik po sobě jdoucích párů je monotónně rostoucích
            int monotoneCount = 0;
            for (int i = 1; i < values.Count; i++)
            {
                if (values[i].val >= values[i - 1].val)
                    monotoneCount++;
            }

            // Alespoň 70% párů musí být monotónně rostoucích
            float monotoneRatio = (float)monotoneCount / (values.Count - 1);
            if (monotoneRatio < 0.7f) continue;

            // Zkontroluj pravidelný Y rozestup (dovolíme odchylku 50%)
            var ySpacings = new List<float>();
            for (int i = 1; i < sorted.Count; i++)
                ySpacings.Add(sorted[i].Y - sorted[i - 1].Y);

            float medianSpacing = ySpacings.OrderBy(s => s).ElementAt(ySpacings.Count / 2);
            if (medianSpacing < 1.5f) continue; // příliš hustě → pravděpodobně ne hodiny

            int regularCount = ySpacings.Count(s =>
                s > medianSpacing * 0.5f && s < medianSpacing * 2f);
            float regularRatio = (float)regularCount / ySpacings.Count;
            if (regularRatio < 0.6f) continue;

            // Rozsah hodnot: musí obsahovat hodiny typické pro JŘ (alespoň 4–20)
            int minVal = values.Min(v => v.val);
            int maxVal = values.Max(v => v.val);
            if (maxVal - minVal < 4) continue; // rozsah příliš malý

            float colX = sorted.Min(w => w.X);
            float colWidth = sorted.Max(w => w.X + w.Width) - colX;

            // Zkontroluj, zda existuje "hod." label nad tímto sloupcem (tolerance 5mm v X)
            float colTopY = sorted.Min(w => w.Y);
            bool hasHodLabel = hodLabels.Any(h =>
                Math.Abs(h.X - colX) < 8f && h.Y < colTopY);

            candidates.Add((colX, colWidth, sorted, hasHodLabel));
        }

        // Pokud máme sloupce s "hod." labelem, preferuj je; jinak vrať všechny.
        // Sloupec jízdních dob (travel times) nemá "hod." label a je typicky nejlevější.
        var withLabel = candidates.Where(c => c.hasHodLabel).ToList();
        if (withLabel.Count > 0)
            return withLabel.Select(c => (c.x, c.width, c.words)).ToList();

        // Fallback: pokud žádný nemá "hod." label, ale máme víc kandidátů,
        // vyřaď nejlevější (pravděpodobně travel times) pokud je výrazně vlevo
        if (candidates.Count > 1)
        {
            var sortedByX = candidates.OrderBy(c => c.x).ToList();
            float firstX = sortedByX[0].x;
            float secondX = sortedByX[1].x;
            // Pokud je první sloupec výrazně vlevo (>20mm mezera), je to travel time
            if (secondX - firstX > 20f)
                return sortedByX.Skip(1).Select(c => (c.x, c.width, c.words)).ToList();
        }

        return candidates.Select(c => (c.x, c.width, c.words)).ToList();
    }

    // ── Krok 3: Seskupení do řádků ──────────────────────────────────────────

    /// <summary>
    /// Seskupí slova do řádků podle Y souřadnice (tolerance = výška slova × 0.5).
    /// Vrátí seřazené řádky shora dolů.
    /// </summary>
    private static List<(float y, float height, List<ExtractedWord> words)> GroupByRows(
        List<ExtractedWord> words)
    {
        var sorted = words.OrderBy(w => w.Y).ToList();
        var rows = new List<(float y, float height, List<ExtractedWord> words)>();

        foreach (var word in sorted)
        {
            // Najdi existující řádek s blízkým Y
            var matchRow = rows.FirstOrDefault(r =>
                Math.Abs(r.y - word.Y) < Math.Max(word.Height * 0.5f, Tolerance));

            if (matchRow.words is not null)
            {
                matchRow.words.Add(word);
            }
            else
            {
                rows.Add((word.Y, word.Height, new List<ExtractedWord> { word }));
            }
        }

        return rows.OrderBy(r => r.y).ToList();
    }

    // ── Krok 3: Hodinové čísla ──────────────────────────────────────────────

    /// <summary>
    /// Najde slova, která jsou čísla 0–24 a leží vlevo od minutové mřížky,
    /// zarovnané na Y s řádky mřížky.
    /// </summary>
    private static List<ExtractedWord> FindHourNumbers(
        List<ExtractedWord> words,
        List<(float y, float height, List<ExtractedWord> words)> minuteRows)
    {
        // Najdi levý okraj minutové mřížky
        float minuteLeft = minuteRows.SelectMany(r => r.words).Min(w => w.X);

        return words
            .Where(w =>
            {
                // Musí být číslo 0–24
                if (w.Text.Length > 2) return false;
                if (!int.TryParse(w.Text, out int n)) return false;
                if (n < 0 || n > 24) return false;
                // Musí být vlevo od minutové mřížky
                if (w.X + w.Width > minuteLeft) return false;
                // Musí být zarovnané s některým řádkem minutové mřížky
                bool alignedWithRow = minuteRows.Any(r =>
                    Math.Abs(r.y - w.Y) < Math.Max(r.height * 0.6f, Tolerance));
                return alignedWithRow;
            })
            .ToList();
    }

    // ── Krok 4: Měření rozměrů ──────────────────────────────────────────────

    /// <summary>Průměrný vertikální rozestup řádků minutové mřížky.</summary>
    private static float CalculateAverageRowSpacing(
        List<(float y, float height, List<ExtractedWord> words)> rows)
    {
        if (rows.Count < 2) return 5f;
        var spacings = new List<float>();
        for (int i = 1; i < rows.Count; i++)
            spacings.Add(rows[i].y - rows[i - 1].y);
        return spacings.Average();
    }

    /// <summary>
    /// Odhadne šířku minutové buňky z horizontálních rozestupů minut na řádcích.
    /// </summary>
    private static float EstimateMinuteCellWidth(
        List<ExtractedWord> minuteWords,
        List<(float y, float height, List<ExtractedWord> words)> rows)
    {
        var gaps = new List<float>();
        foreach (var row in rows)
        {
            var sorted = row.words.OrderBy(w => w.X).ToList();
            for (int i = 1; i < sorted.Count; i++)
            {
                float gap = sorted[i].X - sorted[i - 1].X;
                // Ignorovat velké mezery (= oddělení skupin dnů)
                if (gap > 0.5f && gap < 20f)
                    gaps.Add(gap);
            }
        }

        if (gaps.Count == 0) return 5f;

        // Nejčastější malá mezera = šířka buňky
        // Použij medián pro robustnost (odfiltruje odlehlé hodnoty)
        gaps.Sort();
        return gaps[gaps.Count / 2];
    }

    // ── Krok 5: Zastávky ────────────────────────────────────────────────────

    /// <summary>
    /// Najde textová slova vlevo od hodinového sloupce, zarovnaná s řádky minutové mřížky.
    /// Vrací jen nečíselná slova (názvy zastávek, ne čísla).
    /// </summary>
    private static List<ExtractedWord> FindStopNames(
        List<ExtractedWord> words, float hourColumnLeft,
        List<(float y, float height, List<ExtractedWord> words)> minuteRows)
    {
        float gridTop = minuteRows.First().y - Tolerance;
        float gridBottom = minuteRows.Last().y + minuteRows.Last().height + Tolerance;

        return words
            .Where(w =>
            {
                // Vlevo od hodinového sloupce
                if (w.X + w.Width > hourColumnLeft) return false;
                // Ve vertikálním rozsahu mřížky
                if (w.Y < gridTop || w.Y > gridBottom) return false;
                // Ne číslo (odfiltrovat čísla jízdních dob apod. — ale "min" text nechat)
                if (w.Text.Length <= 2 && int.TryParse(w.Text, out _)) return false;
                return true;
            })
            .ToList();
    }

    /// <summary>
    /// Najde čísla jízdních dob (travel times) vlevo od hodinového sloupce, zarovnaná s řádky mřížky.
    /// Tyto čísla patří do StopList oblasti, ale nejsou názvy zastávek.
    /// </summary>
    private static List<ExtractedWord> FindTravelTimeNumbers(
        List<ExtractedWord> words, float hourColumnLeft,
        List<(float y, float height, List<ExtractedWord> words)> minuteRows)
    {
        float gridTop = minuteRows.First().y - Tolerance;
        float gridBottom = minuteRows.Last().y + minuteRows.Last().height + Tolerance;

        return words
            .Where(w =>
            {
                // Vlevo od hodinového sloupce
                if (w.X + w.Width > hourColumnLeft) return false;
                // Ve vertikálním rozsahu mřížky
                if (w.Y < gridTop || w.Y > gridBottom) return false;
                // Musí být číslo (1-2 cifry) — jízdní doby jsou krátká čísla
                if (w.Text.Length > 2 || !int.TryParse(w.Text, out _)) return false;
                return true;
            })
            .ToList();
    }

    /// <summary>
    /// Odhadne šířky sloupců zastávkové tabulky z pozic textů.
    /// Detekuje sloupec s čísly (jízdní doba) a sloupec s texty (názvy).
    /// </summary>
    private static List<float> EstimateStopColumnWidths(
        List<ExtractedWord> stopWords, float areaLeft, float hourColLeft)
    {
        if (stopWords.Count == 0)
            return [hourColLeft - areaLeft];

        // Zkus najít dva sloupce: čísla vlevo (jízdní doby) + text vpravo (názvy)
        float totalWidth = hourColLeft - areaLeft;

        // Hledej přirozené seskupení podle X souřadnice
        var xPositions = stopWords.Select(w => w.X).OrderBy(x => x).ToList();

        // Najdi největší mezeru v X pozicích → oddělovač sloupců
        float maxGap = 0;
        float gapAt = areaLeft;
        for (int i = 1; i < xPositions.Count; i++)
        {
            float gap = xPositions[i] - xPositions[i - 1];
            if (gap > maxGap && gap > 3f) // min 3mm mezera
            {
                maxGap = gap;
                gapAt = (xPositions[i] + xPositions[i - 1]) / 2f;
            }
        }

        if (maxGap > 3f)
        {
            // Dva sloupce
            float col1Width = gapAt - areaLeft;
            float col2Width = totalWidth - col1Width;
            return [col1Width, col2Width];
        }

        // Jeden sloupec
        return [totalWidth];
    }

    // ── Krok 6: Záhlaví skupin dnů ──────────────────────────────────────────

    /// <summary>
    /// Detekuje záhlaví skupin dnů využitím pozic hodinových sloupců jako kotev.
    /// Každý hodinový sloupec odpovídá jedné skupině dnů. Záhlaví hledáme v úzkém pásu
    /// nad mřížkou, v X rozsahu daného hodinového sloupce (po další hodinový sloupec).
    /// </summary>
    private static List<(string text, float x, float width, float headerY)> DetectDayGroupsFromHourColumns(
        List<ExtractedWord> words,
        List<(float x, float width, List<ExtractedWord> words)> hourColumns,
        float minuteAreaRight,
        float gridTop)
    {
        if (hourColumns.Count == 0)
            return [];

        // Seřaď hodinové sloupce zleva doprava
        var sortedHourCols = hourColumns.OrderBy(c => c.x).ToList();

        // Pro každý hodinový sloupec: hledej záhlaví nad ním
        float headerSearchTop = gridTop - 15f;
        float headerSearchBottom = gridTop - 1f; // malý margin pod záhlaví

        var groups = new List<(string text, float x, float width, float headerY)>();

        for (int i = 0; i < sortedHourCols.Count; i++)
        {
            float colLeft = sortedHourCols[i].x;
            float colRight = i + 1 < sortedHourCols.Count
                ? sortedHourCols[i + 1].x
                : minuteAreaRight;
            float colWidth = colRight - colLeft;

            // Najdi slova v záhlaví zóně tohoto sloupce
            // X rozsah: text musí PŘEKRÝVAT oblast sloupce (ne být celý uvnitř)
            // — záhlaví jako "Sobota, neděle a státní svátky" může přesahovat více sloupců
            var headerWords = words
                .Where(w =>
                    w.Y >= headerSearchTop && w.Y < headerSearchBottom
                    && w.X + w.Width > colLeft - 2f && w.X < colRight + 2f // overlap, ne containment
                    && w.IsBold // záhlaví skupin je typicky tučné
                    && w.FontSizePt >= 5f && w.FontSizePt <= 14f)
                .OrderBy(w => w.Y).ThenBy(w => w.X)
                .ToList();

            if (headerWords.Count == 0)
            {
                // Fallback: zkus i ne-tučná slova (overlap test)
                headerWords = words
                    .Where(w =>
                        w.Y >= gridTop - 8f && w.Y < headerSearchBottom
                        && w.X + w.Width > colLeft - 2f && w.X < colRight + 2f
                        && w.FontSizePt >= 5f && w.FontSizePt <= 14f)
                    .OrderBy(w => w.Y).ThenBy(w => w.X)
                    .ToList();
            }

            string groupText = headerWords.Count > 0
                ? string.Join(" ", headerWords.Select(w => w.Text))
                : $"Skupina {i + 1}";
            float headerY = headerWords.Count > 0
                ? headerWords.Min(w => w.Y) : gridTop - 5f;

            groups.Add((groupText, colLeft, colWidth, headerY));
        }

        return groups;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Test zda text patří do JŘ oblasti. Používá overlap pro X (text může přesahovat přes oblast,
    /// typicky záhlaví skupin dnů) a center-point pro Y.
    /// </summary>
    private static bool IsInRange(float x, float y, float w, float h,
        (float left, float right) xRange, (float top, float bottom) yRange)
    {
        // X: overlap — text začíná v oblasti nebo do ní zasahuje
        bool xOverlap = x <= xRange.right + 5f && x + w >= xRange.left;
        // Y: center point v oblasti
        float cy = y + h / 2f;
        bool yInRange = cy >= yRange.top && cy <= yRange.bottom;
        return xOverlap && yInRange;
    }

    /// <summary>
    /// Detekuje poznámkové páry v unmapped textech — krátký symbol (1-2 znaky: D, x, {, H, F)
    /// následovaný popisným textem na stejném řádku. Typicky "D" + "na znamení", "x" + "přestup na žel.".
    /// </summary>
    private static List<ExtractedTextBlock> FindNoteLegendBlocks(List<ExtractedTextBlock> unmappedTexts)
    {
        var result = new List<ExtractedTextBlock>();
        var used = new HashSet<int>();

        for (int i = 0; i < unmappedTexts.Count; i++)
        {
            if (used.Contains(i)) continue;
            var symbol = unmappedTexts[i];

            // Symbol = 1-2 znaky, ne číslo, ne běžné slovo
            if (symbol.Text.Length > 2) continue;
            if (int.TryParse(symbol.Text, out _)) continue;

            // Hledej popisný text na stejném řádku (Y blízko, X napravo od symbolu)
            for (int j = 0; j < unmappedTexts.Count; j++)
            {
                if (i == j || used.Contains(j)) continue;
                var desc = unmappedTexts[j];

                // Popis musí být delší než symbol
                if (desc.Text.Length <= 2) continue;

                // Stejný řádek — Y souřadnice blízko sebe (tolerance 2mm)
                float yDiff = Math.Abs(symbol.Y - desc.Y);
                if (yDiff > 2f) continue;

                // Popis napravo od symbolu (nebo mírně překrývající), vzdálenost max 10mm
                float gap = desc.X - (symbol.X + symbol.Width);
                if (gap < -1f || gap > 10f) continue;

                // Podobná velikost fontu (tolerance 2pt)
                if (Math.Abs(symbol.FontSizePt - desc.FontSizePt) > 2f) continue;

                // Nalezený pár — obojí patří do legendy poznámek
                result.Add(symbol);
                result.Add(desc);
                used.Add(i);
                used.Add(j);
                break; // symbol má jen jeden popis
            }
        }

        return result;
    }
}
