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
    List<TemplateElement>? MatchedElements = null,
    bool IsSectionCenter = false);

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
    /// <summary>Výchozí mezera pro gap snap a směrové duplikování v mm.</summary>
    public const float DefaultGapMm = 2f;

    /// <summary>Mezera pro gap snap v mm.</summary>
    public float GapMm { get; set; } = DefaultGapMm;

    public SnapResult Calculate(
        TemplateElement refElement,
        (float X, float Y) refInitPos,
        float rawDeltaX, float rawDeltaY,
        IEnumerable<TemplateElement> otherElements,
        float thresholdMm,
        IReadOnlyList<float>? extraXAnchors = null,
        IReadOnlyList<float>? extraYAnchors = null,
        IReadOnlyList<float>? edgeXAnchors = null,
        IReadOnlyList<float>? edgeYAnchors = null,
        float? sectionCenterX = null)
    {
        float refW = refElement.Width;
        float refH = refElement.Height ?? refElement.EffectiveDesignerHeight;

        var others = otherElements.ToList();
        bool hasAnyTargets = others.Count > 0
            || (extraXAnchors is { Count: > 0 })
            || (extraYAnchors is { Count: > 0 })
            || (edgeXAnchors is { Count: > 0 })
            || (edgeYAnchors is { Count: > 0 });
        if (!hasAnyTargets)
            return new SnapResult(
                new AxisSnap(rawDeltaX, false),
                new AxisSnap(rawDeltaY, false));

        var axisX = SnapAxis(
            refInitPos.X, refW, rawDeltaX, thresholdMm, GapMm,
            others, e => e.X, e => e.Width,
            refInitPos.Y + rawDeltaY, refH, e => e.Y, e => e.Height ?? e.EffectiveDesignerHeight,
            isHorizontalGap: true,
            extraAnchors: extraXAnchors,
            edgeAnchors: edgeXAnchors,
            sectionCenterAnchor: sectionCenterX);

        var axisY = SnapAxis(
            refInitPos.Y, refH, rawDeltaY, thresholdMm, GapMm,
            others, e => e.Y, e => e.Height ?? e.EffectiveDesignerHeight,
            refInitPos.X + rawDeltaX, refW, e => e.X, e => e.Width,
            isHorizontalGap: false,
            extraAnchors: extraYAnchors,
            edgeAnchors: edgeYAnchors);

        return new SnapResult(axisX, axisY);
    }

    private enum MatchKind { Edge, Center, Gap, SectionCenter }

    private record struct Candidate(
        float Dist, float SnapTo, float RefEdge,
        TemplateElement? El, MatchKind Kind,
        float GapFrom, float GapTo);    // GapFrom/GapTo jen pro Kind=Gap; El=null pro extra anchors

    private static AxisSnap SnapAxis(
        float refPos, float refSize, float rawDelta, float threshold, float gapMm,
        List<TemplateElement> others,
        Func<TemplateElement, float> getPos, Func<TemplateElement, float> getSize,
        float refCrossPos, float refCrossSize,
        Func<TemplateElement, float> getCrossPos, Func<TemplateElement, float> getCrossSize,
        bool isHorizontalGap,
        IReadOnlyList<float>? extraAnchors = null,
        IReadOnlyList<float>? edgeAnchors = null,
        float? sectionCenterAnchor = null)
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

        // ── Extra anchors (point targets bez elementu) ──
        // Cross-section X snap. Edge-to-point matching, žádný gap snap.
        if (extraAnchors is not null)
        {
            foreach (float anchor in extraAnchors)
            {
                TryAdd(candidates, refStart, anchor, threshold, null!, MatchKind.Edge);
                TryAdd(candidates, refEnd,   anchor, threshold, null!, MatchKind.Edge);
            }
        }

        // ── Section center (center-to-anchor) ──
        // Aligns element center s daným X (typicky střed sekce na ose).
        // Označeno samostatným MatchKind aby DesignerCanvas mohl vykreslit guide line.
        if (sectionCenterAnchor is { } scAnchor)
        {
            TryAdd(candidates, refCenter, scAnchor, threshold, null!, MatchKind.SectionCenter);
        }

        // ── Edge anchors (hrany sekce / printable area) ──
        // Edge-to-point (element hranou na okraji) + gap-to-point (element s mezerou od okraje).
        if (edgeAnchors is not null)
        {
            foreach (float anchor in edgeAnchors)
            {
                // Edge alignment — element hranou přesně na okraji.
                TryAdd(candidates, refStart, anchor, threshold, null!, MatchKind.Edge);
                TryAdd(candidates, refEnd,   anchor, threshold, null!, MatchKind.Edge);

                if (gapMm > 0)
                {
                    // Ref je uvnitř, anchor je levý/horní okraj → element nalevo má refStart = anchor + gap.
                    TryAdd(candidates, refStart, anchor + gapMm, threshold, null!, MatchKind.Gap,
                        gapFrom: anchor, gapTo: anchor + gapMm);
                    // Ref je uvnitř, anchor je pravý/spodní okraj → element napravo má refEnd = anchor - gap.
                    TryAdd(candidates, refEnd, anchor - gapMm, threshold, null!, MatchKind.Gap,
                        gapFrom: anchor - gapMm, gapTo: anchor);
                }
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
        var matchedEls = winners.Select(c => c.El).Where(e => e is not null).Cast<TemplateElement>().Distinct().ToList();

        float adjustedDelta = rawDelta + (snapTo - refEdge);

        // Gap indikátor — krátká spojnice v mezeře
        // CrossMid se počítá z pozic cílových elementů (ty jsou stabilní).
        // Pro anchor-based gap (hrana sekce) použijeme cross střed ref elementu.
        GapIndicator? gap = null;
        if (matchKind == MatchKind.Gap)
        {
            float crossMid = matchedEls.Count > 0
                ? getCrossPos(matchedEls[0]) + getCrossSize(matchedEls[0]) / 2f
                : refCrossPos + refCrossSize / 2f;
            gap = new GapIndicator(isHorizontalGap, gf, gt, crossMid);
        }

        bool isSectionCenter = matchKind == MatchKind.SectionCenter;
        return new AxisSnap(adjustedDelta, true, gap, matchedEls, isSectionCenter);
    }

    private static void TryAdd(List<Candidate> list,
        float refEdge, float target, float threshold, TemplateElement? el, MatchKind kind,
        float gapFrom = 0, float gapTo = 0)
    {
        float dist = MathF.Abs(refEdge - target);
        if (dist < threshold)
            list.Add(new Candidate(dist, target, refEdge, el, kind, gapFrom, gapTo));
    }

    /// <summary>
    /// Snap pro resize — snapuje pouze hrany, které se mění.
    /// movingLeft/Right/Top/Bottom = true pokud daný handle mění tuto hranu.
    /// Vrací snapnuté hodnoty (newX, newY, newW, newH).
    /// </summary>
    public (float X, float Y, float W, float? H, List<TemplateElement> Targets, List<GapIndicator> Gaps)
        CalculateResize(
            float newX, float newY, float newW, float? newH,
            bool movingLeft, bool movingRight, bool movingTop, bool movingBottom,
            IEnumerable<TemplateElement> otherElements,
            TemplateElement resizeElement,
            float thresholdMm)
    {
        var others = otherElements.ToList();
        var targets = new List<TemplateElement>();
        var gaps = new List<GapIndicator>();

        if (others.Count == 0)
            return (newX, newY, newW, newH, targets, gaps);

        float h = newH ?? 0f;

        // X osa
        if (movingLeft || movingRight)
        {
            float refLeft  = newX;
            float refRight = newX + newW;
            var candidates = new List<Candidate>();

            foreach (var other in others)
            {
                float oL = other.X;
                float oR = other.X + other.Width;

                if (movingLeft)
                {
                    TryAdd(candidates, refLeft, oL, thresholdMm, other, MatchKind.Edge);
                    TryAdd(candidates, refLeft, oR, thresholdMm, other, MatchKind.Edge);
                    if (GapMm > 0)
                        TryAdd(candidates, refLeft, oR + GapMm, thresholdMm, other, MatchKind.Gap,
                            gapFrom: oR, gapTo: oR + GapMm);
                }
                if (movingRight)
                {
                    TryAdd(candidates, refRight, oL, thresholdMm, other, MatchKind.Edge);
                    TryAdd(candidates, refRight, oR, thresholdMm, other, MatchKind.Edge);
                    if (GapMm > 0)
                        TryAdd(candidates, refRight, oL - GapMm, thresholdMm, other, MatchKind.Gap,
                            gapFrom: oL - GapMm, gapTo: oL);
                }
            }

            if (candidates.Count > 0)
            {
                float minDist = candidates.Min(c => c.Dist);
                var winners = candidates.Where(c => c.Dist <= minDist + 0.01f).ToList();
                var best = winners[0];
                float shift = best.SnapTo - best.RefEdge;
                targets.AddRange(winners.Select(c => c.El).Where(e => e is not null).Cast<TemplateElement>().Distinct());

                if (best.Kind == MatchKind.Gap && winners[0].El is TemplateElement gapEl)
                {
                    float crossMid = gapEl.Y + (gapEl.Height ?? gapEl.EffectiveDesignerHeight) / 2f;
                    gaps.Add(new GapIndicator(true, best.GapFrom, best.GapTo, crossMid));
                }

                if (movingLeft && best.RefEdge == refLeft)
                { newX += shift; newW -= shift; }
                else if (movingRight && best.RefEdge == refRight)
                { newW += shift; }
            }
        }

        // Y osa
        if (movingTop || movingBottom)
        {
            float refTop    = newY;
            float refBottom = newY + h;
            var candidates = new List<Candidate>();

            foreach (var other in others)
            {
                float oH = other.Height ?? other.EffectiveDesignerHeight;
                float oT = other.Y;
                float oB = other.Y + oH;

                if (movingTop)
                {
                    TryAdd(candidates, refTop, oT, thresholdMm, other, MatchKind.Edge);
                    TryAdd(candidates, refTop, oB, thresholdMm, other, MatchKind.Edge);
                    if (GapMm > 0)
                        TryAdd(candidates, refTop, oB + GapMm, thresholdMm, other, MatchKind.Gap,
                            gapFrom: oB, gapTo: oB + GapMm);
                }
                if (movingBottom)
                {
                    TryAdd(candidates, refBottom, oT, thresholdMm, other, MatchKind.Edge);
                    TryAdd(candidates, refBottom, oB, thresholdMm, other, MatchKind.Edge);
                    if (GapMm > 0)
                        TryAdd(candidates, refBottom, oT - GapMm, thresholdMm, other, MatchKind.Gap,
                            gapFrom: oT - GapMm, gapTo: oT);
                }
            }

            if (candidates.Count > 0)
            {
                float minDist = candidates.Min(c => c.Dist);
                var winners = candidates.Where(c => c.Dist <= minDist + 0.01f).ToList();
                var best = winners[0];
                float shift = best.SnapTo - best.RefEdge;
                targets.AddRange(winners.Select(c => c.El).Where(e => e is not null).Cast<TemplateElement>().Distinct());

                if (best.Kind == MatchKind.Gap && winners[0].El is TemplateElement gapEl)
                {
                    float crossMid = gapEl.X + gapEl.Width / 2f;
                    gaps.Add(new GapIndicator(false, best.GapFrom, best.GapTo, crossMid));
                }

                if (movingTop && best.RefEdge == refTop)
                { newY += shift; h -= shift; }
                else if (movingBottom && best.RefEdge == refBottom)
                { h += shift; }
            }
        }

        return (newX, newY, newW, newH.HasValue ? h : null, targets, gaps);
    }
}
