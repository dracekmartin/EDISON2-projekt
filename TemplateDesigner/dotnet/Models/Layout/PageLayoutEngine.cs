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
    /// Míry zmenšení, které se zkoušejí, když by tabulka jinak vyjela o stránku víc (`B151`).
    /// Od nejmenšího zásahu, mez je 90 % (rozhodnutí Jan 14. 9.) - pod ní se radši řeže.
    /// </summary>
    private static readonly float[] ZmensovaciZebrik = [0.97f, 0.94f, 0.90f];

    /// <summary>
    /// Rozvrhne šablonu a cestou zkusí, jestli se **nevyplatí tabulku o kousek zmenšit** místo
    /// zakládání další stránky.
    /// </summary>
    /// <remarks>
    /// <para>Zmenšuje se <b>celý prvek jednou hodnotou</b> - kdyby se zmenšovala jen přetékající
    /// část, měly by sousední stránky viditelně jiné písmo. A když ani 90 % nestačí, vrací se plná
    /// velikost a řeže se jako dřív - řez ve zmenšeném písmu by stál obojí naráz (rozhodnutí
    /// Jan 14. 9.).</para>
    ///
    /// <para>⚠️ Spouštěčem je <b>počet stránek, na které se rozlezla sama tabulka</b>, ne počet
    /// stránek celého dokumentu (upřesnění Jan 14. 9. po zkoušení). Řešíme „ať na druhé straně
    /// nevisí dva řádky tabulky"; kde skončí poznámky pod ní, je jiná věc. Podle počtu stránek
    /// dokumentu to nešlo: poznámky drží druhou stranu skoro vždycky, takže by se nezmenšilo
    /// nikdy - přesně v tom případu, kvůli kterému to vzniklo.</para>
    ///
    /// <para>„Zmenši všechno, ať se sestava vejde na jednu stranu" je jiná featura (`#8785`) -
    /// sahá na celý výstup včetně poznámek a má vlastní strop.</para>
    /// </remarks>
    public LayoutResult Layout(Template template, DataContext? data)
    {
        var zmensitelne = ZmensitelnePrvky(template);
        NastavZmenseni(zmensitelne, 1f);
        var vysledek = LayoutBezZmenseni(template, data);
        if (zmensitelne.Count == 0) return vysledek;

        var sekceTabulek = SekceZmensitelnych(template);
        int stranekPlne = StranekSekci(vysledek, sekceTabulek);
        if (stranekPlne <= 1) return vysledek;      // tabulka se nedělí, není co šetřit

        foreach (float mira in ZmensovaciZebrik)
        {
            NastavZmenseni(zmensitelne, mira);
            var uzsi = LayoutBezZmenseni(template, data);
            if (StranekSekci(uzsi, sekceTabulek) < stranekPlne) return uzsi;
        }

        NastavZmenseni(zmensitelne, 1f);
        return vysledek;
    }

    /// <summary>Sekce, ve kterých leží zmenšitelná tabulka.</summary>
    private static HashSet<Guid> SekceZmensitelnych(Template template) =>
        [.. template.Sections
            .Where(s => s.Elements.OfType<LineTableElement>().Any(e => e.Height is null))
            .Select(s => s.Id)];

    /// <summary>Na kolika stránkách se ty sekce objeví.</summary>
    private static int StranekSekci(LayoutResult vysledek, HashSet<Guid> sekce) =>
        vysledek.Pages.Count(p => p.Sections.Any(s => sekce.Contains(s.Source.Id)));

    /// <summary>Prvky, které umí ušetřit stránku zmenšením - tabulka bez pevné výšky, která se
    /// sama skládá z dílů. Prvek s pevnou výškou se nestránkuje, takže tam není co šetřit.</summary>
    private static List<LineTableElement> ZmensitelnePrvky(Template template) =>
        [.. template.Sections
            .SelectMany(s => s.Elements)
            .OfType<LineTableElement>()
            .Where(e => e.Height is null)];

    private static void NastavZmenseni(List<LineTableElement> prvky, float mira)
    {
        foreach (var p in prvky) p.LayoutZmenseni = mira;
    }

    private LayoutResult LayoutBezZmenseni(Template template, DataContext? data)
    {
        // Krok 0: Propagace kultury
        data = ApplyCulture(template, data);

        // Krok 0a: Klasifikace sekcí
        var allSections = template.Sections;
        var headers = new List<Section>();
        var footers = new List<Section>();
        var bodySections = new List<Section>();
        var plovouci = new List<Section>();

        ClassifySections(allSections, headers, footers, bodySections, plovouci);

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
        var firstPassResult = LayoutBody(template, data, headers, footers, plovouci, bodySections,
            sectionHeights, includeLastPageOnly: false);
        int totalPages = firstPassResult.TotalPages;
        if (totalPages == 0) totalPages = 1;

        // Druhý průchod: s OnLastPageOnly (pokud existují)
        bool hasLastPageOnly = footers.Any(s => s.PrintBehavior == SectionPrintBehavior.OnLastPageOnly)
            || headers.Any(s => s.PrintBehavior == SectionPrintBehavior.OnLastPageOnly);

        LayoutResult result;
        if (hasLastPageOnly && totalPages > 0)
            result = LayoutBody(template, data, headers, footers, plovouci, bodySections,
                sectionHeights, includeLastPageOnly: true, knownTotalPages: totalPages);
        else
            result = firstPassResult;

        // Krok 3: Přidat hlavičky a patičky na každou stránku
        AddHeadersAndFooters(result, template, data, headers, footers, plovouci, sectionHeights);

        // Krok 4: Injekce PAGE/TOTAL_PAGES
        InjectPageNumbers(result);

        return result;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Klasifikace sekcí

    /// <summary>
    /// Rozdělí sekce podle toho, kam na stránce patří.
    /// </summary>
    /// <remarks>
    /// <para>Opakovaná sekce s Umístěním <b>„Pod obsahem"</b> plave za obsahem té stránky
    /// (`B134`, požadavek Jan 7. 9.: „poznámky musí být na každé stránce a hned pod tabulkou").
    /// Do 10. 9. tahle kombinace nešla nastavit vůbec - „Jednou" plavalo, ale bylo jen jednou,
    /// a „Každá stránka" sedělo na spodní hraně papíru.</para>
    ///
    /// <para>⚠️ **U „Obsahu" zůstává heuristika podle pozice** (za poslední obsahovou → patička,
    /// jinak hlavička). Vypadá to jako zbytek, který by se dal uklidit, ale nejde: „Obsah" je
    /// **výchozí** hodnota, takže ho má každá opakovaná sekce, na jejíž Umístění nikdo nesáhl.
    /// První verze `B134` ten význam přepsala na „plav za obsahem" a hlavičky existujících šablon
    /// se sesypaly pod tabulku (nález Jan 10. 9. na `linkovy_hlavickapod_mutace`). Nové chování
    /// proto dostalo vlastní hodnotu.</para>
    /// </remarks>
    private static void ClassifySections(List<Section> all,
        List<Section> headers, List<Section> footers, List<Section> body, List<Section> plovouci)
    {
        // Index poslední obsahové sekce - podle něj se zařadí Umístění „Obsah" (viz remarks).
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

            // Opakující se sekce: Umístění určuje, kam na stránce patří
            if (section.Role == SectionRole.Footer)
                footers.Add(section);
            else if (section.Role == SectionRole.Header)
                headers.Add(section);
            else if (section.Role == SectionRole.BelowContent)
                plovouci.Add(section);
            else
            {
                // Obsah = „rozhodni to za mě" podle pozice v šabloně, viz remarks
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
    /// <b>Lepší úroveň má přednost, i když leží výš.</b> Hranice bloku je přirozený konec stránky,
    /// hranice mezi zastávkami náhrada pro blok, který se na stránku nevejde celý, a hranice uvnitř
    /// zastávky nouze pro zastávku vyšší než celá stránka. Brát prostě „poslední, co se vejde" by
    /// řezalo uprostřed bloku pokaždé, když v něm nějaký řádek zbyl, a doprostřed rozdvojené
    /// zastávky pokaždé, když vyšla na hranici stránky (`B135`).
    /// Zlom musí posunout o kus dál, jinak by se smyčka řezala na místě.
    /// </remarks>
    private static Zlom? PosledniZlomDo(List<Zlom> zlomy, float od, float doKam)
    {
        Zlom? nejlepsi = null;
        foreach (var z in zlomy)
        {
            if (z.Mm <= od + 0.01f || z.Mm > doKam + 0.01f) continue;
            if (nejlepsi is not { } n || z.Uroven > n.Uroven || (z.Uroven == n.Uroven && z.Mm > n.Mm))
                nejlepsi = z;
        }
        return nejlepsi;
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
        List<Section> headers, List<Section> footers, List<Section> plovouci,
        List<Section> bodySections, Dictionary<Guid, float> sectionHeights,
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
        float plovouciH = GetHeaderHeight(currentPageNum, estTotal, plovouci, sectionHeights);
        float availableHeight = printableHeight - headerH - footerH - plovouciH;
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

            // Vejde se na stránku? Tolerance kryje smetí z desetinných výšek sekcí - bez ní
            // odejde poslední sekce na druhou stránku kvůli dvěma setinám milimetru.
            if (cursorY + sectionH <= headerH + availableHeight + SectionLayoutMath.TolerancePrekryvuMm)
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
                    plovouciH = GetHeaderHeight(currentPageNum, estTotal, plovouci, sectionHeights);
                    availableHeight = printableHeight - headerH - footerH - plovouciH;
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
                        plovouciH = GetHeaderHeight(currentPageNum, estTotal, plovouci, sectionHeights);
                        availableHeight = printableHeight - headerH - footerH - plovouciH;
                        cursorY = headerH;
                        spaceOnPage = availableHeight - hlavicka;
                    }

                    float chunk = Math.Min(remaining, spaceOnPage);
                    // Prázdno těsně před zlomem, které se při zlomu stránky nekreslí ani nepřenáší
                    // na další stranu - viz `Zlom.MrtvaMezeraMm`.
                    float mrtvaMezera = 0f;
                    if (remaining - chunk > 0.01f)   // opravdu se řeže, ne poslední díl
                    {
                        var zlom = PosledniZlomDo(zlomySekce, clipTop, clipTop + chunk);
                        if (zlom is { } z && z.Mm - z.MrtvaMezeraMm - clipTop > 0.01f)
                        {
                            chunk = z.Mm - z.MrtvaMezeraMm - clipTop;
                            mrtvaMezera = z.MrtvaMezeraMm;
                        }
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

                    // Mrtvá mezera se přeskočí: nekreslí se (chunk ji nezahrnuje) a další strana
                    // začíná až za ní, takže `clipTop` dosedne přesně na zlom. Na tom stojí
                    // i rozhodnutí, jestli pokračovací strana opakuje hlavičku bloku.
                    clipTop += chunk + mrtvaMezera;
                    remaining -= chunk + mrtvaMezera;
                    cursorY += chunk + hlavicka;

                    if (remaining > 0.01f)
                    {
                        currentPageNum++;
                        currentPage = new Page { PageNumber = currentPageNum };
                        result.Pages.Add(currentPage);

                        headerH = GetHeaderHeight(currentPageNum, estTotal, headers, sectionHeights);
                        footerH = GetFooterHeight(currentPageNum, estTotal, footers, sectionHeights);
                        plovouciH = GetHeaderHeight(currentPageNum, estTotal, plovouci, sectionHeights);
                        availableHeight = printableHeight - headerH - footerH - plovouciH;
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
        DataContext? data, List<Section> headers, List<Section> footers, List<Section> plovouci,
        Dictionary<Guid, float> sectionHeights)
    {
        int totalPages = result.TotalPages;
        float printableHeight = template.PageSize.PrintableHeight;

        foreach (var page in result.Pages)
        {
            int pageNum = page.PageNumber;

            // Kde na téhle stránce skončil obsah - odsud plavou sekce s Umístěním „Obsah".
            // Musí se zjistit TEĎ, než se do stránky vloží hlavičky a patičky (`B134`).
            float konecObsahu = 0f;
            foreach (var placed in page.Sections)
                konecObsahu = MathF.Max(konecObsahu, placed.Y + placed.Height);

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

            // Plovoucí sekce hned za obsahem stránky - ne na spodní hraně papíru. Na prázdné
            // stránce (obsah se nevešel vůbec) začíná pod hlavičkami.
            float plovouciY = MathF.Max(konecObsahu, y);
            foreach (var sekce in plovouci)
            {
                if (!IsHeaderVisibleOnPage(sekce, pageNum, totalPages)) continue;

                var pageData = data?.WithExtraValues(new Dictionary<string, object?>
                {
                    ["PAGE"] = pageNum,
                    ["TOTAL_PAGES"] = totalPages
                });
                if (!ExpressionEvaluator.IsVisible(sekce.VisibilityExpression, pageData)) continue;

                float h = sectionHeights[sekce.Id];
                page.Sections.Add(new PlacedSection
                {
                    Source = sekce,
                    Y = plovouciY,
                    Height = h,
                    Data = data
                });
                plovouciY += h;
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
