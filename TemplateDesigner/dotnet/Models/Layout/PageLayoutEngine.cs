using System.Globalization;
using TemplateDesigner.Models.Expressions;

namespace TemplateDesigner.Models.Layout;

/// <summary>
/// Hlavní layout engine — Template + DataContext → LayoutResult (seznam stránek).
/// Čistá logika, žádný SkiaSharp/WPF. Renderer kreslí hotový výsledek.
/// </summary>
public class PageLayoutEngine
{
    private readonly IElementHeightCalculator _heightCalc;

    public PageLayoutEngine(IElementHeightCalculator heightCalculator)
    {
        _heightCalc = heightCalculator;
    }

    /// <summary>
    /// Provede layout šablony s daty a vrátí rozložení na stránky.
    /// </summary>
    public LayoutResult Layout(Template template, DataContext? data)
    {
        // Krok 0: Propagace kultury
        data = ApplyCulture(template, data);

        // Krok 0a: Klasifikace sekcí
        var allSections = template.Sections;
        var headers = new List<Section>();
        var footers = new List<Section>();
        var bodySections = new List<Section>();

        ClassifySections(allSections, headers, footers, bodySections);

        // Krok 1: Výpočet výšek sekcí (body + headers + footers)
        var sectionHeights = new Dictionary<Guid, float>();
        float printableWidth = template.PageSize.PrintableWidth;

        foreach (var section in allSections)
            sectionHeights[section.Id] = CalculateSectionHeight(section, printableWidth, data);

        // Krok 2: Dvouprůchodový layout
        // První průchod: bez OnLastPageOnly → zjistíme totalPages
        var firstPassResult = LayoutBody(template, data, headers, footers, bodySections,
            sectionHeights, includeLastPageOnly: false);
        int totalPages = firstPassResult.TotalPages;
        if (totalPages == 0) totalPages = 1;

        // Druhý průchod: s OnLastPageOnly (pokud existují)
        bool hasLastPageOnly = footers.Any(s => s.PrintBehavior == SectionPrintBehavior.OnLastPageOnly)
            || headers.Any(s => s.PrintBehavior == SectionPrintBehavior.OnLastPageOnly);

        LayoutResult result;
        if (hasLastPageOnly && totalPages > 0)
            result = LayoutBody(template, data, headers, footers, bodySections,
                sectionHeights, includeLastPageOnly: true, knownTotalPages: totalPages);
        else
            result = firstPassResult;

        // Krok 3: Přidat hlavičky a patičky na každou stránku
        AddHeadersAndFooters(result, template, data, headers, footers, sectionHeights);

        // Krok 4: Injekce PAGE/TOTAL_PAGES
        InjectPageNumbers(result);

        return result;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Klasifikace sekcí

    private static void ClassifySections(List<Section> all,
        List<Section> headers, List<Section> footers, List<Section> body)
    {
        // Najít index posledního body sekce (Once/PerDataRow) — repeating sekce
        // za ním s Role=Body se automaticky klasifikují jako footer (pozice v šabloně)
        int lastBodyIndex = -1;
        for (int i = all.Count - 1; i >= 0; i--)
        {
            if (all[i].PrintBehavior is SectionPrintBehavior.Once
                                     or SectionPrintBehavior.PerDataRow)
            {
                lastBodyIndex = i;
                break;
            }
        }

        for (int i = 0; i < all.Count; i++)
        {
            var section = all[i];

            // Once / PerDataRow → vždy body
            if (section.PrintBehavior is SectionPrintBehavior.Once
                                     or SectionPrintBehavior.PerDataRow)
            {
                body.Add(section);
                continue;
            }

            // Opakující se sekce: Role určuje umístění (Header nahoře, Footer dole)
            if (section.Role == SectionRole.Footer)
                footers.Add(section);
            else if (section.Role == SectionRole.Header)
                headers.Add(section);
            else
            {
                // Role=Body (default) u repeating sekce — heuristika podle pozice:
                // za posledním body sectionem → footer, jinak header
                if (lastBodyIndex >= 0 && i > lastBodyIndex)
                    footers.Add(section);
                else
                    headers.Add(section);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Výpočet výšky sekce

    private float CalculateSectionHeight(Section section, float printableWidth, DataContext? data)
    {
        // Odfiltrovat neviditelné elementy
        var visibleElements = section.Elements
            .Where(e => ExpressionEvaluator.IsVisible(e.VisibilityExpression, data))
            .ToList();

        if (visibleElements.Count == 0)
            return section.DesignerHeight ?? 0f;

        // Flow layout: dynamické elementy posouvají elementy pod sebou
        var effectiveHeights = new Dictionary<Guid, float>();
        float maxBottom = 0f;

        // Seřadit podle Y (shora dolů)
        var sorted = visibleElements.OrderBy(e => e.Y).ToList();
        float cumulativeDeltaY = 0f;

        foreach (var element in sorted)
        {
            float elementHeight = _heightCalc.CalculateHeight(element, printableWidth, data);
            effectiveHeights[element.Id] = elementHeight;

            float effectiveY = element.Y + cumulativeDeltaY;
            float bottom = effectiveY + elementHeight;
            maxBottom = Math.Max(maxBottom, bottom);

            // Pokud je dynamický a výška se liší od designer výšky, posunout elementy pod ním
            if (!element.Height.HasValue)
            {
                float designerH = element.EffectiveDesignerHeight;
                float deltaY = elementHeight - designerH;
                if (Math.Abs(deltaY) > 0.01f)
                    cumulativeDeltaY += deltaY;
            }
        }

        // Výška sekce = max z vypočítaného obsahu a DesignerHeight
        float computedHeight = maxBottom;
        if (section.DesignerHeight.HasValue)
            return Math.Max(section.DesignerHeight.Value, computedHeight);
        return computedHeight > 0 ? computedHeight : 0f;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Výpočet prostoru na stránce

    private static float GetHeaderHeight(int pageNum, int totalPages,
        List<Section> headers, Dictionary<Guid, float> heights)
    {
        float h = 0f;
        foreach (var section in headers)
        {
            if (IsHeaderVisibleOnPage(section, pageNum, totalPages))
                h += heights[section.Id];
        }
        return h;
    }

    private static float GetFooterHeight(int pageNum, int totalPages,
        List<Section> footers, Dictionary<Guid, float> heights)
    {
        float h = 0f;
        foreach (var section in footers)
        {
            if (IsFooterVisibleOnPage(section, pageNum, totalPages))
                h += heights[section.Id];
        }
        return h;
    }

    private static bool IsHeaderVisibleOnPage(Section s, int pageNum, int totalPages) =>
        s.PrintBehavior switch
        {
            SectionPrintBehavior.OnEveryPage => true,
            SectionPrintBehavior.OnFirstPageOnly => pageNum == 1,
            SectionPrintBehavior.OnEveryPageExceptFirst => pageNum > 1,
            SectionPrintBehavior.OnLastPageOnly => pageNum == totalPages,
            _ => false
        };

    private static bool IsFooterVisibleOnPage(Section s, int pageNum, int totalPages) =>
        s.PrintBehavior switch
        {
            SectionPrintBehavior.OnEveryPage => true,
            SectionPrintBehavior.OnFirstPageOnly => pageNum == 1,
            SectionPrintBehavior.OnEveryPageExceptFirst => pageNum > 1,
            SectionPrintBehavior.OnLastPageOnly => pageNum == totalPages,
            _ => false
        };

    // ─────────────────────────────────────────────────────────────────────
    // Body layout

    private LayoutResult LayoutBody(Template template, DataContext? data,
        List<Section> headers, List<Section> footers, List<Section> bodySections,
        Dictionary<Guid, float> sectionHeights,
        bool includeLastPageOnly, int knownTotalPages = 0)
    {
        var result = new LayoutResult();
        float printableHeight = template.PageSize.PrintableHeight;
        float printableWidth = template.PageSize.PrintableWidth;

        int currentPageNum = 1;
        var currentPage = new Page { PageNumber = currentPageNum };
        result.Pages.Add(currentPage);

        // Estimovat totalPages pro header/footer height výpočet
        int estTotal = knownTotalPages > 0 ? knownTotalPages : 1000;

        float headerH = GetHeaderHeight(currentPageNum, estTotal, headers, sectionHeights);
        float footerH = GetFooterHeight(currentPageNum, estTotal, footers, sectionHeights);
        float availableHeight = printableHeight - headerH - footerH;
        float cursorY = headerH;

        // Expand body sections (PerDataRow → N kopií)
        var expandedSections = ExpandBodySections(bodySections, data);

        foreach (var (section, sectionData) in expandedSections)
        {
            // Vyhodnotit VisibilityExpression na sekci
            if (!ExpressionEvaluator.IsVisible(section.VisibilityExpression, sectionData ?? data))
                continue;

            float sectionH = section.PrintBehavior == SectionPrintBehavior.PerDataRow
                ? CalculateSectionHeight(section, printableWidth, sectionData)
                : sectionHeights[section.Id];

            // Vejde se na stránku?
            if (cursorY + sectionH <= headerH + availableHeight)
            {
                // Vejde se — umístit
                currentPage.Sections.Add(new PlacedSection
                {
                    Source = section,
                    Y = cursorY,
                    Height = sectionH,
                    Data = sectionData ?? data
                });
                cursorY += sectionH;
            }
            else if (section.OverflowBehavior == SectionOverflowBehavior.KeepTogether)
            {
                // Nová stránka
                currentPageNum++;
                currentPage = new Page { PageNumber = currentPageNum };
                result.Pages.Add(currentPage);

                headerH = GetHeaderHeight(currentPageNum, estTotal, headers, sectionHeights);
                footerH = GetFooterHeight(currentPageNum, estTotal, footers, sectionHeights);
                availableHeight = printableHeight - headerH - footerH;
                cursorY = headerH;

                currentPage.Sections.Add(new PlacedSection
                {
                    Source = section,
                    Y = cursorY,
                    Height = sectionH,
                    Data = sectionData ?? data
                });
                cursorY += sectionH;
            }
            else
            {
                // SplitAcrossPages / AllowSplit
                float remaining = sectionH;
                float clipTop = 0f;

                while (remaining > 0.01f)
                {
                    float spaceOnPage = headerH + availableHeight - cursorY;
                    if (spaceOnPage <= 0.01f)
                    {
                        currentPageNum++;
                        currentPage = new Page { PageNumber = currentPageNum };
                        result.Pages.Add(currentPage);

                        headerH = GetHeaderHeight(currentPageNum, estTotal, headers, sectionHeights);
                        footerH = GetFooterHeight(currentPageNum, estTotal, footers, sectionHeights);
                        availableHeight = printableHeight - headerH - footerH;
                        cursorY = headerH;
                        spaceOnPage = availableHeight;
                    }

                    float chunk = Math.Min(remaining, spaceOnPage);
                    currentPage.Sections.Add(new PlacedSection
                    {
                        Source = section,
                        Y = cursorY,
                        Height = chunk,
                        ClipTop = clipTop,
                        ClipBottom = clipTop + chunk,
                        Data = sectionData ?? data
                    });

                    clipTop += chunk;
                    remaining -= chunk;
                    cursorY += chunk;

                    if (remaining > 0.01f)
                    {
                        currentPageNum++;
                        currentPage = new Page { PageNumber = currentPageNum };
                        result.Pages.Add(currentPage);

                        headerH = GetHeaderHeight(currentPageNum, estTotal, headers, sectionHeights);
                        footerH = GetFooterHeight(currentPageNum, estTotal, footers, sectionHeights);
                        availableHeight = printableHeight - headerH - footerH;
                        cursorY = headerH;
                    }
                }
            }
        }

        return result;
    }

    // ─────────────────────────────────────────────────────────────────────
    // PerDataRow expanze

    private static List<(Section section, DataContext? data)> ExpandBodySections(
        List<Section> bodySections, DataContext? data)
    {
        var expanded = new List<(Section, DataContext?)>();

        foreach (var section in bodySections)
        {
            if (section.PrintBehavior == SectionPrintBehavior.PerDataRow
                && section.DataSource is not null)
            {
                var rows = data?.GetRows(section.DataSource) ?? [];
                if (rows.Count == 0)
                {
                    // Žádná data — přidat jednou s prázdnými daty
                    expanded.Add((section, data));
                }
                else
                {
                    foreach (var row in rows)
                        expanded.Add((section, row));
                }
            }
            else
            {
                expanded.Add((section, null));
            }
        }

        return expanded;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Hlavičky a patičky

    private static void AddHeadersAndFooters(LayoutResult result, Template template,
        DataContext? data, List<Section> headers, List<Section> footers,
        Dictionary<Guid, float> sectionHeights)
    {
        int totalPages = result.TotalPages;
        float printableHeight = template.PageSize.PrintableHeight;

        foreach (var page in result.Pages)
        {
            int pageNum = page.PageNumber;

            // Hlavičky nahoře
            float y = 0f;
            foreach (var header in headers)
            {
                if (!IsHeaderVisibleOnPage(header, pageNum, totalPages)) continue;

                // VisibilityExpression per stránku (může obsahovat {PAGE})
                var pageData = data?.WithExtraValues(new Dictionary<string, object?>
                {
                    ["PAGE"] = pageNum,
                    ["TOTAL_PAGES"] = totalPages
                });
                if (!ExpressionEvaluator.IsVisible(header.VisibilityExpression, pageData)) continue;

                float h = sectionHeights[header.Id];
                page.Sections.Insert(0, new PlacedSection
                {
                    Source = header,
                    Y = y,
                    Height = h,
                    Data = data
                });
                y += h;
            }

            // Patičky dole
            float totalFooterH = 0f;
            foreach (var footer in footers)
            {
                if (IsFooterVisibleOnPage(footer, pageNum, totalPages))
                    totalFooterH += sectionHeights[footer.Id];
            }

            float footerY = printableHeight - totalFooterH;
            foreach (var footer in footers)
            {
                if (!IsFooterVisibleOnPage(footer, pageNum, totalPages)) continue;

                var pageData = data?.WithExtraValues(new Dictionary<string, object?>
                {
                    ["PAGE"] = pageNum,
                    ["TOTAL_PAGES"] = totalPages
                });
                if (!ExpressionEvaluator.IsVisible(footer.VisibilityExpression, pageData)) continue;

                float h = sectionHeights[footer.Id];
                page.Sections.Add(new PlacedSection
                {
                    Source = footer,
                    Y = footerY,
                    Height = h,
                    Data = data
                });
                footerY += h;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // PAGE / TOTAL_PAGES

    private static void InjectPageNumbers(LayoutResult result)
    {
        int totalPages = result.TotalPages;
        foreach (var page in result.Pages)
        {
            foreach (var placed in page.Sections)
            {
                if (placed.Data is not null)
                {
                    placed.Data = placed.Data.WithExtraValues(new Dictionary<string, object?>
                    {
                        ["PAGE"] = page.PageNumber,
                        ["TOTAL_PAGES"] = totalPages
                    });
                }
                else
                {
                    placed.Data = new DataContext(
                        new Dictionary<string, object?>
                        {
                            ["PAGE"] = page.PageNumber,
                            ["TOTAL_PAGES"] = totalPages
                        });
                }
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Kultura

    private static DataContext? ApplyCulture(Template template, DataContext? data)
    {
        if (template.CultureCode is null) return data;
        try
        {
            var culture = CultureInfo.GetCultureInfo(template.CultureCode);
            return data?.WithCulture(culture);
        }
        catch
        {
            return data;
        }
    }
}
