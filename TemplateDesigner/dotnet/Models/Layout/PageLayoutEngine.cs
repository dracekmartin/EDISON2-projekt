using System.Globalization;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Expressions;

namespace TemplateDesigner.Models.Layout;

/// <summary>
/// Hlavní layout engine - Template + DataContext → LayoutResult (seznam stránek).
/// Čistá logika, žádný SkiaSharp/WPF. Renderer kreslí hotový výsledek.
/// </summary>
public class PageLayoutEngine
{
    private readonly IElementHeightCalculator _heightCalc;

    /// <summary>Místa (mm od horního okraje sekce), kde se sekce smí rozříznout - nasbíraná od
    /// prvků, které se skládají z nedělitelných dílů. Prázdné = řez kdekoli, jako dřív.</summary>
    private Dictionary<Guid, List<Zlom>> _sectionBreaks = [];

    /// <summary>Výška hlavičky, kterou sekce opakuje na pokračovací stránce (0 = žádná).</summary>
    private Dictionary<Guid, float> _sectionHeaders = [];

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

        var sectionBreaks = new Dictionary<Guid, List<Zlom>>();
        var sectionHeaders = new Dictionary<Guid, float>();

        foreach (var section in allSections)
        {
            var (vyska, zlomy, hlavicka) = CalculateSectionHeight(section, printableWidth, data);
            sectionHeights[section.Id] = vyska;
            sectionBreaks[section.Id] = zlomy;
            sectionHeaders[section.Id] = hlavicka;
        }
        _sectionBreaks = sectionBreaks;
        _sectionHeaders = sectionHeaders;

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
        // Najít index posledního body sekce (Once/PerDataRow) - repeating sekce
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
                // Role=Body (default) u repeating sekce - heuristika podle pozice:
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

    /// <summary>
    /// Poslední nabídnutý zlom, který ještě padne do zbytku stránky; null = žádný se nevejde.
    /// </summary>
    /// <remarks>
    /// <b>Silný zlom má přednost, i když je výš.</b> Hranice bloku je přirozený konec stránky;
    /// hranice řádku je jen náhrada pro blok, který se na stránku nevejde celý. Brát prostě
    /// „poslední, co se vejde" by řezalo uprostřed bloku pokaždé, když v něm nějaký řádek zbyl.
    /// Zlom musí posunout o kus dál, jinak by se smyčka řezala na místě.
    /// </remarks>
    private static float? PosledniZlomDo(List<Zlom> zlomy, float od, float doKam)
    {
        float? silny = null, slaby = null;
        foreach (var z in zlomy)
        {
            if (z.Mm <= od + 0.01f || z.Mm > doKam + 0.01f) continue;
            if (z.Silny) { if (silny is null || z.Mm > silny) silny = z.Mm; }
            else if (slaby is null || z.Mm > slaby) slaby = z.Mm;
        }
        return silny ?? slaby;
    }

    private (float Vyska, List<Zlom> Zlomy, float Hlavicka) CalculateSectionHeight(
        Section section, float printableWidth, DataContext? data)
    {
        var zlomy = new List<Zlom>();
        float opakovanaHlavicka = 0f;
        // Odfiltrovat neviditelné elementy
        var visibleElements = section.Elements
            .Where(e => ExpressionEvaluator.IsVisible(e.VisibilityExpression, data))
            .ToList();

        if (visibleElements.Count == 0)
            return (section.DesignerHeight ?? 0f, zlomy, opakovanaHlavicka);

        // ⚠️ **Uvnitř sekce se prvky neposouvají** - výška sekce sahá tam, kam se doopravdy kreslí.
        // Do 25. 8. se tu rostoucímu prvku přičítal přírůstek k pozici všech prvků pod ním, jenže
        // kreslení je nechává na jejich souřadnicích: sekce si nárokovala místo pro posun, ke
        // kterému nikdy nedošlo. Změřeno: text narostl na 75,4 mm, prvek pod ním zůstal na 12 mm.
        // Tok patří **mezi sekce** a tam funguje - sekce pod rostoucí se odsune o její výšku;
        // proto má nová šablona záhlaví a obsah jako dvě sekce.
        var effectiveHeights = new Dictionary<Guid, float>();
        float maxBottom = 0f;

        var sorted = visibleElements.OrderBy(e => e.Y).ToList();
        const float cumulativeDeltaY = 0f;

        foreach (var element in sorted)
        {
            float elementHeight = _heightCalc.CalculateHeight(element, printableWidth, data);
            effectiveHeights[element.Id] = elementHeight;

            float effectiveY = element.Y + cumulativeDeltaY;
            float bottom = effectiveY + elementHeight;
            maxBottom = Math.Max(maxBottom, bottom);

            // Prvek složený z nedělitelných dílů (linková tabulka rozřezaná na bloky) nabídne
            // místa, kudy se řezat smí - přepočtená z jeho souřadnic do souřadnic sekce.
            if (element is INedelitelneDily deleny)
            {
                zlomy.AddRange(deleny.ZlomyMm(data).Select(z => z with { Mm = effectiveY + z.Mm }));
                opakovanaHlavicka = Math.Max(opakovanaHlavicka, deleny.OpakovanaHlavickaMm(data));
            }

        }

        // Výška sekce = max z vypočítaného obsahu a DesignerHeight
        float computedHeight = maxBottom;
        zlomy.Sort((a, b) => a.Mm.CompareTo(b.Mm));
        if (section.DesignerHeight.HasValue)
            return (Math.Max(section.DesignerHeight.Value, computedHeight), zlomy, opakovanaHlavicka);
        return (computedHeight > 0 ? computedHeight : 0f, zlomy, opakovanaHlavicka);
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

            float sectionH;
            List<Zlom> zlomySekce;
            float hlavickaSekce;
            if (section.PrintBehavior == SectionPrintBehavior.PerDataRow)
                (sectionH, zlomySekce, hlavickaSekce) = CalculateSectionHeight(section, printableWidth, sectionData);
            else
            {
                sectionH = sectionHeights[section.Id];
                zlomySekce = _sectionBreaks.TryGetValue(section.Id, out var z) ? z : [];
                hlavickaSekce = _sectionHeaders.TryGetValue(section.Id, out var hh) ? hh : 0f;
            }

            // Vejde se na stránku?
            if (cursorY + sectionH <= headerH + availableHeight)
            {
                // Vejde se - umístit
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
                // Posunout na novou stránku jen pokud aktuální stránka už něco obsahuje.
                // Je-li prázdná, sekce se stejně nikam menšího nevejde (je vyšší než celá
                // tisknutelná oblast) - umístit ji sem, ať nevzniká prázdná úvodní stránka.
                if (currentPage.Sections.Count > 0)
                {
                    currentPageNum++;
                    currentPage = new Page { PageNumber = currentPageNum };
                    result.Pages.Add(currentPage);

                    headerH = GetHeaderHeight(currentPageNum, estTotal, headers, sectionHeights);
                    footerH = GetFooterHeight(currentPageNum, estTotal, footers, sectionHeights);
                    availableHeight = printableHeight - headerH - footerH;
                    cursorY = headerH;
                }

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
                    // Pokračovací stránka, která začíná uprostřed nedělitelného dílu, zopakuje
                    // jeho hlavičku - a musí si na ni napřed ubrat místo, jinak by ji přetiskla
                    // přes obsah.
                    float hlavicka = clipTop > 0.01f && hlavickaSekce > 0.01f
                                     && !zlomySekce.Any(z => z.Silny && Math.Abs(z.Mm - clipTop) < 0.05f)
                        ? hlavickaSekce
                        : 0f;
                    float spaceOnPage = headerH + availableHeight - cursorY - hlavicka;
                    if (spaceOnPage <= 0.01f)
                    {
                        currentPageNum++;
                        currentPage = new Page { PageNumber = currentPageNum };
                        result.Pages.Add(currentPage);

                        headerH = GetHeaderHeight(currentPageNum, estTotal, headers, sectionHeights);
                        footerH = GetFooterHeight(currentPageNum, estTotal, footers, sectionHeights);
                        availableHeight = printableHeight - headerH - footerH;
                        cursorY = headerH;
                        spaceOnPage = availableHeight - hlavicka;
                    }

                    float chunk = Math.Min(remaining, spaceOnPage);
                    if (remaining - chunk > 0.01f)   // opravdu se řeže, ne poslední díl
                    {
                        float? zlom = PosledniZlomDo(zlomySekce, clipTop, clipTop + chunk);
                        if (zlom is float z)
                            chunk = z - clipTop;
                        else if (cursorY > headerH + 0.01f)
                        {
                            // Nedělitelný díl se do zbytku stránky nevejde. Zkusit ho na čisté
                            // stránce - vynucením prázdného místa se o ni postará začátek smyčky.
                            // Na čisté stránce už `cursorY == headerH`, takže se díl rozřízne
                            // geometricky (je vyšší než celá stránka) a smyčka se nezacyklí.
                            cursorY = headerH + availableHeight;
                            continue;
                        }
                    }

                    currentPage.Sections.Add(new PlacedSection
                    {
                        Source = section,
                        Y = cursorY,
                        Height = chunk + hlavicka,
                        ClipTop = clipTop,
                        ClipBottom = clipTop + chunk,
                        Data = sectionData ?? data,
                        OpakovanaHlavickaMm = hlavicka
                    });

                    clipTop += chunk;
                    remaining -= chunk;
                    cursorY += chunk + hlavicka;

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
                    // Žádná data - přidat jednou s prázdnými daty
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
