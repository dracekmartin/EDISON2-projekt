using SkiaSharp;
using System.Text.Json.Serialization;

namespace TemplateDesigner.Models.Elements;

public class TextElement : TemplateElement
{
    /// <summary>
    /// Text pro zobrazení. Může obsahovat placeholdery ve tvaru {NAZEV_POLE}.
    /// Print engine je za běhu nahradí reálnými daty.
    /// </summary>
    public string Text { get; set; } = "";

    /// <summary>Styl textu - font, barvy, zarovnání, řádkování, padding.</summary>
    public TextStyle Style { get; set; } = TextStyle.CreateDefault();

    /// <summary>
    /// Zalamovat dlouhý text do dalších řádků. ⚠️ **Výchozí vypnuto** (rozhodnutí Jan 3. 9.):
    /// nový textový prvek je typicky nadpis nebo jeden údaj na jednom řádku, a zalomení tam
    /// rozhodí layout víc, než pomůže. Kdo chce odstavec, si ho zapne.
    /// </summary>
    /// <remarks>
    /// Existující šablony to nepotká - hodnota se do JSON zapisuje vždy (serializace vynechává
    /// jen null), takže si každý prvek nese tu svou.
    ///
    /// <para>Řídí i to, jak se text zmenšuje, když se nevejde: se zalamováním nejdřív přeteče
    /// na další řádek a teprve pak se zmenšuje na výšku, bez zalamování se zmenšuje na šířku.</para>
    /// </remarks>
    public bool WordWrap { get; set; }

    /// <summary>
    /// Když Height = null a WordWrap = true, výška se dopočítá z textu.
    /// Print engine spočítá z reálných dat; designer z raw textu (s placeholdery).
    /// </summary>
    protected override float DesignerPlaceholderHeight =>
        WordWrap ? CalculateTextHeight(Text) : 8f;

    public override void Render(RenderContext ctx)
    {
        if (Width <= 0) return;
        var resolvedText = TextKKresleni(ctx.Data);
        if (string.IsNullOrEmpty(resolvedText)) return;

        var rs = Style.Resolve();

        float sirkaMm = SirkaTextuMm(ctx.SirkaSekceMm);

        // Dynamická výška: Height ?? dopočítaná z textu (pokud WordWrap)
        float heightMm = Height ?? (WordWrap ? CalculateTextHeight(resolvedText, sirkaMm) : EffectiveDesignerHeight);
        var rect = ctx.ToCanvasRect(X, Y, Width, heightMm);

        // Pozadí (pokud není průhledné). Ořez na tvar rohů řeší render loop obecně (ApplyCornerClip).
        using (var bgPaint = rs.CreateBgPaint())
        {
            if (bgPaint is not null)
                ctx.Canvas.DrawRect(rect, bgPaint);
        }

        // V designeru (bez dat) omezit velikost fontu, aby placeholdery typu {NAZEV_POLE}
        // nepřetékaly - zobrazit čitelně místo ve skutečné velikosti.
        // Zmenšit do prvku - stejné pravidlo jako u buněk tabulek a prvků jízdních řádů: pevná
        // výška zmenší písmo, prázdná nechá prvek dorůst. Pod mezí se text ořízne (viz MinScale).
        // Na plátně návrháře (bez dat) se nezmenšuje: text je tam ještě s placeholdery, takže by
        // se škála počítala z jiné délky, než jaká se vytiskne - a font je už omezený na 10 pt.
        float effectiveFontSize = ctx.Data is null
            ? Math.Min(rs.FontSize, 10f)
            : rs.FontSize * ComputeShrinkScale(resolvedText, sirkaMm);
        float fontSizePx = ctx.Px(effectiveFontSize * ResolvedTextStyle.PtToMm);
        if (ctx.Data is not null && ctx.ClippedCells is { } oriznute && ContentOverflows(ctx.Data, sirkaMm))
            oriznute.ReportText();

        var typeface = FontResolver.Resolve(rs.FontFamily, rs.ToSKFontStyle());
        using var font = new SKFont(typeface, fontSizePx);
        using var paint = rs.CreateTextPaint();

        ctx.Canvas.Save();
        ctx.Canvas.ClipRect(rect);

        if (WordWrap)
        {
            float widthPx = ctx.Px(sirkaMm);
            var lines = TextWrapHelper.WrapText(resolvedText, font, widthPx - 2f * OdsazeniPx);
            float lineHeightPx = fontSizePx * rs.LineSpacing;

            // Vertikální zarovnání bloku řádků
            float totalTextHeight = lines.Count * lineHeightPx;
            float yStart = rs.VAlign switch
            {
                TextVerticalAlignment.Middle => rect.Top + (rect.Height - totalTextHeight) / 2 + fontSizePx,
                TextVerticalAlignment.Bottom => rect.Bottom - totalTextHeight + fontSizePx,
                _ => rect.Top + fontSizePx
            };

            float yPx = yStart;
            foreach (var line in lines)
            {
                float lineWidth = font.MeasureText(line);
                float xPx = rs.HAlign switch
                {
                    TextHorizontalAlignment.Center => rect.Left + (widthPx - lineWidth) / 2,
                    TextHorizontalAlignment.Right => rect.Right - lineWidth,
                    _ => rect.Left + OdsazeniPx
                };
                ctx.Canvas.DrawText(line, xPx, yPx, font, paint);
                rs.DrawTextDecorations(ctx.Canvas, xPx, yPx, lineWidth, font, paint);
                yPx += lineHeightPx;
            }
        }
        else
        {
            // Jednořádkový režim - s horizontálním a vertikálním zarovnáním
            float textWidth = font.MeasureText(resolvedText);
            float xPx = rs.HAlign switch
            {
                TextHorizontalAlignment.Center => rect.Left + (rect.Width - textWidth) / 2,
                TextHorizontalAlignment.Right => rect.Right - textWidth - OdsazeniPx,
                _ => rect.Left + OdsazeniPx
            };
            float yPx = rs.VAlign switch
            {
                TextVerticalAlignment.Middle => rect.Top + (rect.Height + fontSizePx) / 2 - fontSizePx * 0.15f,
                TextVerticalAlignment.Bottom => rect.Bottom - fontSizePx * 0.3f,
                _ => rect.Top + fontSizePx
            };
            ctx.Canvas.DrawText(resolvedText, xPx, yPx, font, paint);
            rs.DrawTextDecorations(ctx.Canvas, xPx, yPx, textWidth, font, paint);
        }

        ctx.Canvas.Restore();
    }

    /// <summary>Skutečná výška textu - shodně s výpočtem v Render.</summary>
    public override float RenderedHeightMm(RenderContext ctx)
    {
        var resolved = ctx.Data?.ResolvePlaceholders(Text) ?? Text;
        return Height ?? (WordWrap ? CalculateTextHeight(resolved, SirkaTextuMm(ctx.SirkaSekceMm)) : EffectiveDesignerHeight);
    }

    public override void ApplyScale(float scale)
    {
        base.ApplyScale(scale);
        if (Style.FontSize is not null) Style.FontSize *= scale;
        // LineSpacing je bezrozměrný násobitel (1.2 = 120 %) - neškáluje se
    }

    /// <summary>Popisek pole s obsahem textu jako ve formuláři; podle něj okno vlastností přidá výběr datového pole.</summary>
    internal const string ObsahLabel = "Obsah";

    public override IEnumerable<StyleZone> ResolvedStyleZones() => [new("Style", Style.Resolve())];

    public override IReadOnlyList<PropertyDescriptor> GetPropertyDescriptors() =>
    [
        new StringPropertyDescriptor { Label = ObsahLabel,     Group = "Text",     Get = () => Text,                Set = v => Text = v,                                   MultiLine = true },

        new StringPropertyDescriptor { Label = "Font",         Group = "Font",      Get = () => Style.Resolve().FontFamily, Set = v => Style.FontFamily = v },
        new FloatPropertyDescriptor  { Label = "Velikost",      Group = "Font",      Get = () => Style.Resolve().FontSize,   Set = v => Style.FontSize = v,                 Min = 3, Max = 72, Unit = "pt" },
        new BoolPropertyDescriptor   { Label = "Tučné",        Group = "Font",      Get = () => Style.Resolve().Bold,       Set = v => Style.Bold = v, ToolbarSlot = FormatToolbarSlot.Bold },
        new BoolPropertyDescriptor   { Label = "Kurzíva",      Group = "Font",      Get = () => Style.Resolve().Italic,     Set = v => Style.Italic = v, ToolbarSlot = FormatToolbarSlot.Italic },
        new BoolPropertyDescriptor   { Label = "Podtržení",    Group = "Font",      Get = () => Style.Resolve().Underline,  Set = v => Style.Underline = v, ToolbarSlot = FormatToolbarSlot.Underline },
        new BoolPropertyDescriptor   { Label = "Přeškrtnutí", Group = "Font",      Get = () => Style.Resolve().Strikethrough, Set = v => Style.Strikethrough = v, ToolbarSlot = FormatToolbarSlot.Strikethrough },
        new BoolPropertyDescriptor   { Label = "Zalamovat řádky", Group = "Text",      Get = () => WordWrap,                     Set = v => WordWrap = v },
        new FloatPropertyDescriptor  { Label = "Řádkování",    Group = "Font",      Get = () => Style.Resolve().LineSpacing, Set = v => Style.LineSpacing = v,               Min = 0.8f, Max = 3f, Format = "0.0#" },

        // HAlign rozšířený o Justify - inline (sdílené HAlignOptions Justify nemají, cell elementy ho v rendereru ignorují)
        new EnumPropertyDescriptor   { Label = EnumPropertyDescriptor.LabelZarovnaniVodorovne, Group = "Zarovnání", Get = () => Style.Resolve().HAlign, Set = v => Style.HAlign = (TextHorizontalAlignment)v,
            Options = [..EnumPropertyDescriptor.HAlignOptions, ("Do bloku", TextHorizontalAlignment.Justify)], ToolbarSlot = FormatToolbarSlot.HorizontalAlignment },
        new EnumPropertyDescriptor   { Label = EnumPropertyDescriptor.LabelZarovnaniSvisle, Group = "Zarovnání", Get = () => Style.Resolve().VAlign, Set = v => Style.VAlign = (TextVerticalAlignment)v,  Options = EnumPropertyDescriptor.VAlignOptions, ToolbarSlot = FormatToolbarSlot.VerticalAlignment },

        new ColorPropertyDescriptor  { Label = "Barva textu",   Group = "Barvy",    Get = () => Style.Resolve().TextColor, Set = v => Style.TextColor = v },
        new ColorPropertyDescriptor  { Label = "Barva pozadí", Group = "Barvy",    Get = () => Style.Resolve().BackgroundColor, Set = v => Style.BackgroundColor = v },

        .. GetPositionDescriptors()
    ];

    public override TemplateElement Clone()
    {
        var clone = new TextElement
        {
            Text = Text,
            Style = Style.Clone(),
            WordWrap = WordWrap,
        };
        CopyBaseTo(clone);
        return clone;
    }

    /// <summary>
    /// Odsazení textu od hrany prvku. Měření šířky ho musí odečíst - do 23. 9. počítalo s celou
    /// šířkou, takže text zmenšený „přesně do prvku" přesáhl o odsazení a na hraně stránky
    /// se ořízl poslední znak (závorka za čísly spojů v hlavičce).
    /// </summary>
    private const float OdsazeniPx = 1f;

    /// <summary>
    /// Šířka, do které se text sází: prvek, ale nejvýš k pravému okraji tiskové plochy.
    /// </summary>
    /// <remarks>
    /// Co přečnívá přes okraj, sekce ořízne - text zmenšený do celé šířky prvku by na hraně
    /// stránky přišel o konec (nález Jan 23. 9.: závorka za čísly spojů v hlavičce, prvek
    /// přečníval o 0,5 mm). Prvek se tím nemění, jen se text vejde tam, kde je vidět.
    /// </remarks>
    public float SirkaTextuMm(float? sirkaSekceMm) =>
        sirkaSekceMm is > 0f and var s ? Math.Max(0f, Math.Min(Width, s - X)) : Width;

    // ── Zmenšení do prvku ────────────────────────────────────────────────────

    /// <summary>
    /// Kam nejníž smí zmenšení jít - polovina nastavené velikosti, nikdy pod 4 pt.
    /// </summary>
    /// <remarks>
    /// 4 pt je dolní hranice, kterou dovolí zadat pole Velikost, takže mez nemůže vyrobit písmo,
    /// jaké by uživatel nesměl nastavit ručně. Polovina drží autorův záměr: nadpis 40 pt se
    /// nezmenší pod 20 pt, drobný text 8 pt pod 4 pt. (Rozhodnutí Jan 3. 9.; od 16. 9. platí táž
    /// mez i pro buňky tabulek, proto je hodnota jen jedna - viz <see cref="Rendering.CellRenderer.MinScale"/>.)
    /// </remarks>
    public const float MinScale = Rendering.CellRenderer.MinScale;

    /// <summary>Absolutní dno v bodech - shodné s <c>Min</c> pole Velikost.</summary>
    public const float MinFontPt = Rendering.CellRenderer.MinFontPt;

    /// <summary>
    /// O kolik se musí písmo zmenšit, aby se text vešel do prvku (1 = vejde se). Bere v potaz
    /// jak výšku (zalomené řádky), tak šířku (jednořádkový text), a nikdy nejde pod
    /// <see cref="MinScale"/> ani pod <see cref="MinFontPt"/> - pod mezí se text ořízne jako dřív.
    /// </summary>
    /// <remarks>
    /// ⚠️ Jediné místo výpočtu - <see cref="Render"/> i hláška o zmenšení musí dostat totéž číslo,
    /// jinak se tiskne něco jiného, než co hláška tvrdí. Dynamická výška (prázdná Height) se
    /// nezmenšuje: prvek si doroste, což je totéž pravidlo jako u prvků jízdních řádů.
    /// </remarks>
    public float ComputeShrinkScale(string? resolvedText, float? sirkaMm = null)
    {
        float sirka = sirkaMm ?? Width;
        if (string.IsNullOrEmpty(resolvedText) || sirka <= 0) return 1f;

        var rs = Style.Resolve();
        if (rs.FontSize <= 0f) return 1f;

        float potreba = WordWrap ? PotrebnaSkalaNaVysku(resolvedText, rs, sirka) : PotrebnaSkalaNaSirku(resolvedText, rs, sirka);
        if (potreba >= 1f) return 1f;

        float dnoZVelikosti = MinFontPt / rs.FontSize;   // 4 pt jako podíl nastavené velikosti
        return Math.Max(potreba, Math.Max(MinScale, Math.Min(1f, dnoZVelikosti)));
    }

    /// <summary>
    /// Text, jak se nakreslí: s dosazenými poli, a bez zalamování na jednom řádku.
    /// </summary>
    /// <remarks>Pole z dat může nést víc řádků (popis dopravce u zastávkového JŘ má od 31. 8. název,
    /// sídlo a telefon pod sebou). Jednořádkový prvek je kreslil jako jeden text a zalomení řádku
    /// vyšlo jako čtvereček neznámého znaku („a.s.□Oblast…", nález 5. 10. na výchozí šabloně) -
    /// spojí se proto čárkou, jako je popis dopravce v linkovém JŘ.</remarks>
    internal string TextKKresleni(DataContext? data)
    {
        var text = data?.ResolvePlaceholders(Text) ?? Text;
        if (WordWrap || string.IsNullOrEmpty(text) || text.IndexOfAny(['\n', '\r']) < 0) return text;
        return string.Join(", ", text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(r => r.Trim().TrimEnd(',').Trim())
            .Where(r => r.Length > 0));
    }

    /// <summary>Text se vejde do prvku i po zmenšení? False = pod mezí se ořízne.</summary>
    public bool ContentOverflows(DataContext? data, float? sirkaMm = null)
    {
        float sirka = sirkaMm ?? Width;
        var text = TextKKresleni(data);
        if (string.IsNullOrEmpty(text) || sirka <= 0) return false;

        var rs = Style.Resolve();
        if (rs.FontSize <= 0f) return false;

        float potreba = WordWrap ? PotrebnaSkalaNaVysku(text, rs, sirka) : PotrebnaSkalaNaSirku(text, rs, sirka);
        return potreba < ComputeShrinkScale(text, sirka) - 0.001f;
    }

    /// <summary>Zalomený text: zmenšit tak, aby se všechny řádky vešly do pevné výšky.</summary>
    private float PotrebnaSkalaNaVysku(string text, ResolvedTextStyle rs, float sirkaMm)
    {
        if (Height is not { } vyska || vyska <= 0f) return 1f;   // dynamická výška roste sama
        if (VyskaTextu(text, rs, 1f, sirkaMm) <= vyska) return 1f;        // vejde se v plné velikosti

        // Škála je vidět až po přeměření (menší font = jiné zalomení = jiný počet řádků), takže
        // se hledá půlením. Deset kroků dá přesnost pod 0,1 %, což je pod rozlišením tisku.
        float dolni = 0f, horni = 1f;
        for (int i = 0; i < 10; i++)
        {
            float stred = (dolni + horni) / 2f;
            if (VyskaTextu(text, rs, stred, sirkaMm) <= vyska) dolni = stred; else horni = stred;
        }
        return dolni > 0f ? dolni : horni;
    }

    /// <summary>Jednořádkový text: zmenšit tak, aby se vešel do šířky prvku.</summary>
    private static float PotrebnaSkalaNaSirku(string text, ResolvedTextStyle rs, float sirkaMm)
    {
        var typeface = FontResolver.Resolve(rs.FontFamily, rs.ToSKFontStyle());
        float refMmToPx = 96f / 25.4f;
        using var font = new SKFont(typeface, rs.FontSize * ResolvedTextStyle.PtToMm * refMmToPx);

        float sirkaTextu = font.MeasureText(text);
        float sirkaPrvku = sirkaMm * refMmToPx - 2f * OdsazeniPx;
        return sirkaTextu > sirkaPrvku && sirkaTextu > 0f ? sirkaPrvku / sirkaTextu : 1f;
    }

    /// <summary>Výška zalomeného textu v mm při dané škále písma.</summary>
    private static float VyskaTextu(string text, ResolvedTextStyle rs, float scale, float sirkaMm)
    {
        var typeface = FontResolver.Resolve(rs.FontFamily, rs.ToSKFontStyle());
        float fontSizeMm = rs.FontSize * scale * ResolvedTextStyle.PtToMm;
        float refMmToPx = 96f / 25.4f;

        using var font = new SKFont(typeface, fontSizeMm * refMmToPx);
        var lines = TextWrapHelper.WrapText(text, font, sirkaMm * refMmToPx - 2f * OdsazeniPx);
        return lines.Count * fontSizeMm * rs.LineSpacing;
    }

    // ── Word wrap helpers ────────────────────────────────────────────────────

    /// <summary>Vypočítá výšku textu v mm při zalamování do dané šířky.</summary>
    internal float CalculateTextHeight(string text, float? sirkaMm = null)
    {
        float sirka = sirkaMm ?? Width;
        if (string.IsNullOrEmpty(text) || sirka <= 0) return 8f;

        var rs = Style.Resolve();

        var typeface = FontResolver.Resolve(rs.FontFamily, rs.ToSKFontStyle());

        float fontSizeMm = rs.FontSize * ResolvedTextStyle.PtToMm;
        float refMmToPx = 96f / 25.4f;
        float fontSizePx = fontSizeMm * refMmToPx;
        float widthPx = sirka * refMmToPx - 2f * OdsazeniPx;

        using var font = new SKFont(typeface, fontSizePx);
        var lines = TextWrapHelper.WrapText(text, font, widthPx);
        float lineHeightMm = fontSizeMm * rs.LineSpacing;

        return Math.Max(lineHeightMm * lines.Count + fontSizeMm * 0.3f, fontSizeMm);
    }
}

public enum TextHorizontalAlignment { Left, Center, Right, Justify }
public enum TextVerticalAlignment { Top, Middle, Bottom }
