namespace TemplateDesigner.PdfImport;

/// <summary>
/// Seskupení extrahovaných slov do logických textových bloků (proximity + font).
/// </summary>
public static class PdfTextGrouper
{
    public static List<ExtractedTextBlock> GroupWords(List<ExtractedWord> words)
    {
        if (words.Count == 0) return [];

        // 1. Seřadit podle Y (shora dolů), pak podle X
        var sorted = words.OrderBy(w => w.Y).ThenBy(w => w.X).ToList();

        // 2. Rozdělit do řádků (slova se stejnou Y ± tolerance)
        var lines = GroupIntoLines(sorted);

        // 3. V rámci každého řádku spojit slova se stejným fontem blízko sebe
        var lineSegments = new List<LineSegment>();
        foreach (var line in lines)
        {
            lineSegments.AddRange(MergeWordsOnLine(line));
        }

        // 4. Sloučit víceřádkové segmenty se stejným fontem a překrývajícím se X
        var blocks = MergeIntoMultilineBlocks(lineSegments);

        return blocks;
    }

    private static List<List<ExtractedWord>> GroupIntoLines(List<ExtractedWord> sorted)
    {
        var lines = new List<List<ExtractedWord>>();
        List<ExtractedWord>? currentLine = null;
        float currentY = float.MinValue;

        foreach (var word in sorted)
        {
            float tolerance = word.Height * 0.5f;
            if (tolerance < 1f) tolerance = 1f;

            if (currentLine == null || Math.Abs(word.Y - currentY) > tolerance)
            {
                currentLine = [word];
                lines.Add(currentLine);
                currentY = word.Y;
            }
            else
            {
                currentLine.Add(word);
            }
        }

        // Seřadit slova v rámci řádku podle X
        foreach (var line in lines)
            line.Sort((a, b) => a.X.CompareTo(b.X));

        return lines;
    }

    private static List<LineSegment> MergeWordsOnLine(List<ExtractedWord> lineWords)
    {
        var segments = new List<LineSegment>();
        if (lineWords.Count == 0) return segments;

        var current = new LineSegment(lineWords[0]);

        for (int i = 1; i < lineWords.Count; i++)
        {
            var word = lineWords[i];
            float gap = word.X - (current.Right);

            // Spojit pokud: stejný font, velikost, barva a mezera < 2× průměrná šířka znaku
            float maxGap = current.AvgCharWidth * 2f;
            if (maxGap < 2f) maxGap = 2f;

            bool sameStyle = string.Equals(current.FontFamily, word.FontFamily, StringComparison.OrdinalIgnoreCase)
                && Math.Abs(current.FontSizePt - word.FontSizePt) < 0.5f
                && current.IsBold == word.IsBold
                && current.IsItalic == word.IsItalic
                && current.ColorHex == word.ColorHex;

            if (sameStyle && gap >= -1f && gap < maxGap)
            {
                current.AppendWord(word);
            }
            else
            {
                segments.Add(current);
                current = new LineSegment(word);
            }
        }

        segments.Add(current);
        return segments;
    }

    private static List<ExtractedTextBlock> MergeIntoMultilineBlocks(List<LineSegment> segments)
    {
        if (segments.Count == 0) return [];

        // Seřadit podle Y, pak X
        segments.Sort((a, b) =>
        {
            int cmp = a.Y.CompareTo(b.Y);
            return cmp != 0 ? cmp : a.X.CompareTo(b.X);
        });

        var used = new bool[segments.Count];
        var blocks = new List<ExtractedTextBlock>();

        for (int i = 0; i < segments.Count; i++)
        {
            if (used[i]) continue;
            used[i] = true;

            var group = new List<LineSegment> { segments[i] };

            // Hledat následující řádky se stejným stylem a překrývajícím se X
            for (int j = i + 1; j < segments.Count; j++)
            {
                if (used[j]) continue;

                var last = group[^1];
                var candidate = segments[j];

                float verticalGap = candidate.Y - (last.Y + last.Height);
                // Spojovat řádky pouze pokud jsou těsně pod sebou (standardní řádkování).
                // Faktor 0.8 = mezera menší než výška řádku - zabraňuje
                // agresivnímu slučování vizuálně oddělených bloků.
                float maxVerticalGap = last.Height * 0.8f;

                bool sameStyle = string.Equals(last.FontFamily, candidate.FontFamily, StringComparison.OrdinalIgnoreCase)
                    && Math.Abs(last.FontSizePt - candidate.FontSizePt) < 0.5f
                    && last.IsBold == candidate.IsBold
                    && last.IsItalic == candidate.IsItalic
                    && last.ColorHex == candidate.ColorHex;

                bool xOverlaps = candidate.X < last.Right + 2f && candidate.Right > last.X - 2f;

                if (sameStyle && verticalGap >= -0.5f && verticalGap < maxVerticalGap && xOverlaps)
                {
                    used[j] = true;
                    group.Add(candidate);
                }
            }

            // Sestavit blok
            string text = string.Join("\n", group.Select(s => s.Text));
            float x = group.Min(s => s.X);
            float y = group.Min(s => s.Y);
            float right = group.Max(s => s.Right);
            float bottom = group.Max(s => s.Y + s.Height);

            var first = group[0];
            blocks.Add(new ExtractedTextBlock(
                text, x, y, right - x, bottom - y,
                first.FontFamily, first.FontSizePt, first.IsBold, first.IsItalic, first.ColorHex));
        }

        return blocks;
    }

    /// <summary>Pomocná třída pro slučování slov na jednom řádku.</summary>
    private class LineSegment
    {
        public string Text { get; private set; }
        public float X { get; }
        public float Y { get; private set; }
        public float Right { get; private set; }
        public float Height { get; private set; }
        public string FontFamily { get; }
        public float FontSizePt { get; }
        public bool IsBold { get; }
        public bool IsItalic { get; }
        public string ColorHex { get; }

        private int _charCount;

        public float AvgCharWidth => _charCount > 0 ? (Right - X) / _charCount : 2f;

        public LineSegment(ExtractedWord word)
        {
            Text = word.Text;
            X = word.X;
            Y = word.Y;
            Right = word.X + word.Width;
            Height = word.Height;
            FontFamily = word.FontFamily;
            FontSizePt = word.FontSizePt;
            IsBold = word.IsBold;
            IsItalic = word.IsItalic;
            ColorHex = word.ColorHex;
            _charCount = word.Text.Length;
        }

        public void AppendWord(ExtractedWord word)
        {
            Text += " " + word.Text;
            Right = word.X + word.Width;
            Height = Math.Max(Height, word.Height);
            Y = Math.Min(Y, word.Y);
            _charCount += word.Text.Length + 1;
        }
    }
}
