using TemplateDesigner.Models;

namespace TemplateDesigner.Designer;

/// <summary>
/// Indikátor mezery mezi dvěma elementy při gap snapu.
/// Krátká čárka uprostřed mezery kolmá na osu snapu.
/// </summary>
public record struct GapIndicator(
    bool IsHorizontalGap,   // true = mezera na X ose, false = mezera na Y ose
    float GapStartMm,       // začátek mezery (hlavní osa, section mm)
    float GapEndMm,         // konec mezery (hlavní osa, section mm)
    float CrossMidMm);      // střed na kolmé ose kde se kreslí spojnice

public record struct AxisSnap(
    float AdjustedDelta,
    bool HasSnap,
    GapIndicator? Gap = null,
    List<TemplateElement>? MatchedElements = null);

public record struct SnapResult(AxisSnap X, AxisSnap Y);

/// <summary>
/// Snap-to-element engine. Pro každou osu hledá nejbližší shodu:
///   - Edge↔Edge (left↔left, left↔right, right↔left, right↔right)
///   - Center↔Center (nikdy center↔edge)
///   - Gap snap (hrana ref. elementu ve vzdálenosti gapMm od hrany jiného)
/// Bez kategorické priority — vyhraje nejbližší match.
/// </summary>
public class SnapEngine
{
    /// <summary>Mezera pro gap snap v mm.</summary>
    public float GapMm { get; set; } = 4f;

    public SnapResult Calculate(
        TemplateElement refElement,
        (float X, float Y) refInitPos,
        float rawDeltaX, float rawDeltaY,
        IEnumerable<TemplateElement> otherElements,
        float thresholdMm)
    {
        float refW = refElement.Width;
        float refH = refElement.Height ?? refElement.EffectiveDesignerHeight;

        var others = otherElements.ToList();
        if (others.Count == 0)
            return new SnapResult(
                new AxisSnap(rawDeltaX, false),
                new AxisSnap(rawDeltaY, false));

        var axisX = SnapAxis(
            refInitPos.X, refW, rawDeltaX, thresholdMm, GapMm,
            others, e => e.X, e => e.Width,
            refInitPos.Y + rawDeltaY, refH, e => e.Y, e => e.Height ?? e.EffectiveDesignerHeight,
            isHorizontalGap: true);

        var axisY = SnapAxis(
            refInitPos.Y, refH, rawDeltaY, thresholdMm, GapMm,
            others, e => e.Y, e => e.Height ?? e.EffectiveDesignerHeight,
            refInitPos.X + rawDeltaX, refW, e => e.X, e => e.Width,
            isHorizontalGap: false);

        return new SnapResult(axisX, axisY);
    }

    private enum MatchKind { Edge, Center, Gap }

    private record struct Candidate(
        float Dist, float SnapTo, float RefEdge,
        TemplateElement El, MatchKind Kind,
        float GapFrom, float GapTo);    // GapFrom/GapTo jen pro Kind=Gap

    private static AxisSnap SnapAxis(
        float refPos, float refSize, float rawDelta, float threshold, float gapMm,
        List<TemplateElement> others,
        Func<TemplateElement, float> getPos, Func<TemplateElement, float> getSize,
        float refCrossPos, float refCrossSize,
        Func<TemplateElement, float> getCrossPos, Func<TemplateElement, float> getCrossSize,
        bool isHorizontalGap)
    {
        float projected = refPos + rawDelta;
        float refStart  = projected;
        float refCenter = projected + refSize / 2f;
        float refEnd    = projected + refSize;

        var candidates = new List<Candidate>();

        foreach (var other in others)
        {
            float oStart  = getPos(other);
            float oSize   = getSize(other);
            float oCenter = oStart + oSize / 2f;
            float oEnd    = oStart + oSize;

            // ── Edge↔Edge (4 kombinace) ──
            TryAdd(candidates, refStart, oStart, threshold, other, MatchKind.Edge);
            TryAdd(candidates, refStart, oEnd,   threshold, other, MatchKind.Edge);
            TryAdd(candidates, refEnd,   oStart, threshold, other, MatchKind.Edge);
            TryAdd(candidates, refEnd,   oEnd,   threshold, other, MatchKind.Edge);

            // ── Center↔Center ──
            TryAdd(candidates, refCenter, oCenter, threshold, other, MatchKind.Center);

            // ── Gap snap ──
            if (gapMm > 0)
            {
                // ref je vlevo od other: refEnd + gap = oStart
                TryAdd(candidates, refEnd, oStart - gapMm, threshold, other, MatchKind.Gap,
                    gapFrom: oStart - gapMm, gapTo: oStart);
                // ref je vpravo od other: refStart - gap = oEnd
                TryAdd(candidates, refStart, oEnd + gapMm, threshold, other, MatchKind.Gap,
                    gapFrom: oEnd, gapTo: oEnd + gapMm);
            }
        }

        if (candidates.Count == 0)
            return new AxisSnap(rawDelta, false);

        // Vyber nejbližší match (bez kategorické priority); seber všechny se stejnou vzdáleností
        float minDist = candidates.Min(c => c.Dist);
        var winners = candidates.Where(c => c.Dist <= minDist + 0.01f).ToList();

        var best = winners[0];
        float snapTo   = best.SnapTo;
        float refEdge  = best.RefEdge;
        var matchKind  = best.Kind;
        float gf = best.GapFrom, gt = best.GapTo;
        var matchedEls = winners.Select(c => c.El).Distinct().ToList();

        float adjustedDelta = rawDelta + (snapTo - refEdge);

        // Gap indikátor — krátká spojnice v mezeře
        // CrossMid se počítá z pozic cílových elementů (ty jsou stabilní),
        // ne z pozice ref elementu (ta závisí na myši a "popojíždí").
        GapIndicator? gap = null;
        if (matchKind == MatchKind.Gap)
        {
            float crossMid = getCrossPos(matchedEls[0]) + getCrossSize(matchedEls[0]) / 2f;
            gap = new GapIndicator(isHorizontalGap, gf, gt, crossMid);
        }

        return new AxisSnap(adjustedDelta, true, gap, matchedEls);
    }

    private static void TryAdd(List<Candidate> list,
        float refEdge, float target, float threshold, TemplateElement el, MatchKind kind,
        float gapFrom = 0, float gapTo = 0)
    {
        float dist = MathF.Abs(refEdge - target);
        if (dist < threshold)
            list.Add(new Candidate(dist, target, refEdge, el, kind, gapFrom, gapTo));
    }
}
