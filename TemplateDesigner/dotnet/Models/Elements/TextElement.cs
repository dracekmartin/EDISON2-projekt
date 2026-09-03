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
        var resolvedText = ctx.Data?.ResolvePlaceholders(Text) ?? Text;
        if (string.IsNullOrEmpty(resolvedText)) return;

        var rs = Style.Resolve();

        // Dynamická výška: Height ?? dopočítaná z textu (pokud WordWrap)
        float heightMm = Height ?? (WordWrap ? CalculateTextHeight(resolvedText) : EffectiveDesignerHeight);
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
            : rs.FontSize * ComputeShrinkScale(resolvedText);
        float fontSizePx = ctx.Px(effectiveFontSize * ResolvedTextStyle.PtToMm);

        var typeface = FontResolver.Resolve(rs.FontFamily, rs.ToSKFontStyle());
        using var font = new SKFont(typeface, fontSizePx);
        using var paint = rs.CreateTextPaint();

        ctx.Canvas.Save();
        ctx.Canvas.ClipRect(rect);

        if (WordWrap)
        {
            float widthPx = ctx.Px(Width);
            var lines = TextWrapHelper.WrapText(resolvedText, font, widthPx);
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
                    _ => rect.Left + 1f
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
                TextHorizontalAlignment.Right => rect.Right - textWidth - 1f,
                _ => rect.Left + 1f
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
        return Height ?? (WordWrap ? CalculateTextHeight(resolved) : EffectiveDesignerHeight);
    }

    public override void ApplyScale(float scale)
    {
        base.ApplyScale(scale);
        if (Style.FontSize is not null) Style.FontSize *= scale;
        // LineSpacing je bezrozměrný násobitel (1.2 = 120 %) - neškáluje se
    }

    public override IReadOnlyList<PropertyDescriptor> GetPropertyDescriptors() =>
    [
        new StringPropertyDescriptor { Label = "Text",         Group = "Obsah",     Get = () => Text,                Set = v => Text = v,                                   MultiLine = true },

        new StringPropertyDescriptor { Label = "Font",         Group = "Font",      Get = () => Style.FontFamily ?? "Arial",  Set = v => Style.FontFamily = v },
        new FloatPropertyDescriptor  { Label = "Velikost",      Group = "Font",      Get = () => Style.FontSize ?? 10f,        Set = v => Style.FontSize = v,                 Min = 4, Max = 200, Unit = "pt" },
        new BoolPropertyDescriptor   { Label = "Tučné",        Group = "Font",      Get = () => Style.Bold ?? false,          Set = v => Style.Bold = v },
        new BoolPropertyDescriptor   { Label = "Kurzíva",      Group = "Font",      Get = () => Style.Italic ?? false,        Set = v => Style.Italic = v },
        new BoolPropertyDescriptor   { Label = "Podtržení",    Group = "Font",      Get = () => Style.Underline ?? false,      Set = v => Style.Underline = v },
        new BoolPropertyDescriptor   { Label = "Přeškrtnutí", Group = "Font",      Get = () => Style.Strikethrough ?? false,  Set = v => Style.Strikethrough = v },
        new BoolPropertyDescriptor   { Label = "Zalamování",    Group = "Font",      Get = () => WordWrap,                     Set = v => WordWrap = v },
        new FloatPropertyDescriptor  { Label = "Řádkování",    Group = "Font",      Get = () => Style.LineSpacing ?? 1.2f,    Set = v => Style.LineSpacing = v,               Min = 0.8f, Max = 3f, Format = "0.0#" },

        // HAlign rozšířený o Justify - inline (sdílené HAlignOptions Justify nemají, cell elementy ho v rendereru ignorují)
        new EnumPropertyDescriptor   { Label = "Vodorovně", Group = "Zarovnání", Get = () => Style.HAlign ?? TextHorizontalAlignment.Left, Set = v => Style.HAlign = (TextHorizontalAlignment)v,
            Options = [..EnumPropertyDescriptor.HAlignOptions, ("Do bloku", TextHorizontalAlignment.Justify)] },
        new EnumPropertyDescriptor   { Label = "Svisle",    Group = "Zarovnání", Get = () => Style.VAlign ?? TextVerticalAlignment.Top,  Set = v => Style.VAlign = (TextVerticalAlignment)v,  Options = EnumPropertyDescriptor.VAlignOptions },

        new ColorPropertyDescriptor  { Label = "Barva textu",   Group = "Vzhled",    Get = () => Style.TextColor ?? "#FF000000",     Set = v => Style.TextColor = v },
        new ColorPropertyDescriptor  { Label = "Barva pozadí", Group = "Vzhled",    Get = () => Style.BackgroundColor ?? "#00FFFFFF", Set = v => Style.BackgroundColor = v },

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

    // ── Zmenšení do prvku ────────────────────────────────────────────────────

    /// <summary>
    /// Kam nejníž smí zmenšení jít - polovina nastavené velikosti, nikdy pod 4 pt.
    /// </summary>
    /// <remarks>
    /// 4 pt je dolní hranice, kterou dovolí zadat pole Velikost, takže mez nemůže vyrobit písmo,
    /// jaké by uživatel nesměl nastavit ručně. Polovina drží autorův záměr: nadpis 40 pt se
    /// nezmenší pod 20 pt, drobný text 8 pt pod 4 pt. (Rozhodnutí Jan 3. 9. - tabulky a prvky
    /// jízdních řádů mez nemají, u textu se ale schválně nepůjde do nečitelna.)
    /// </remarks>
    public const float MinScale = 0.5f;

    /// <summary>Absolutní dno v bodech - shodné s <c>Min</c> pole Velikost.</summary>
    public const float MinFontPt = 4f;

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
    public float ComputeShrinkScale(string? resolvedText)
    {
        if (string.IsNullOrEmpty(resolvedText) || Width <= 0) return 1f;

        var rs = Style.Resolve();
        if (rs.FontSize <= 0f) return 1f;

        float potreba = WordWrap ? PotrebnaSkalaNaVysku(resolvedText, rs) : PotrebnaSkalaNaSirku(resolvedText, rs);
        if (potreba >= 1f) return 1f;

        float dnoZVelikosti = MinFontPt / rs.FontSize;   // 4 pt jako podíl nastavené velikosti
        return Math.Max(potreba, Math.Max(MinScale, Math.Min(1f, dnoZVelikosti)));
    }

    /// <summary>Text se vejde do prvku i po zmenšení? False = pod mezí se ořízne.</summary>
    public bool ContentOverflows(DataContext? data)
    {
        var text = data?.ResolvePlaceholders(Text) ?? Text;
        if (string.IsNullOrEmpty(text) || Width <= 0) return false;

        var rs = Style.Resolve();
        if (rs.FontSize <= 0f) return false;

        float potreba = WordWrap ? PotrebnaSkalaNaVysku(text, rs) : PotrebnaSkalaNaSirku(text, rs);
        return potreba < ComputeShrinkScale(text) - 0.001f;
    }

    /// <summary>Zalomený text: zmenšit tak, aby se všechny řádky vešly do pevné výšky.</summary>
    private float PotrebnaSkalaNaVysku(string text, ResolvedTextStyle rs)
    {
        if (Height is not { } vyska || vyska <= 0f) return 1f;   // dynamická výška roste sama
        if (VyskaTextu(text, rs, 1f) <= vyska) return 1f;        // vejde se v plné velikosti

        // Škála je vidět až po přeměření (menší font = jiné zalomení = jiný počet řádků), takže
        // se hledá půlením. Deset kroků dá přesnost pod 0,1 %, což je pod rozlišením tisku.
        float dolni = 0f, horni = 1f;
        for (int i = 0; i < 10; i++)
        {
            float stred = (dolni + horni) / 2f;
            if (VyskaTextu(text, rs, stred) <= vyska) dolni = stred; else horni = stred;
        }
        return dolni > 0f ? dolni : horni;
    }

    /// <summary>Jednořádkový text: zmenšit tak, aby se vešel do šířky prvku.</summary>
    private float PotrebnaSkalaNaSirku(string text, ResolvedTextStyle rs)
    {
        var typeface = FontResolver.Resolve(rs.FontFamily, rs.ToSKFontStyle());
        float refMmToPx = 96f / 25.4f;
        using var font = new SKFont(typeface, rs.FontSize * ResolvedTextStyle.PtToMm * refMmToPx);

        float sirkaTextu = font.MeasureText(text);
        float sirkaPrvku = Width * refMmToPx;
        return sirkaTextu > sirkaPrvku && sirkaTextu > 0f ? sirkaPrvku / sirkaTextu : 1f;
    }

    /// <summary>Výška zalomeného textu v mm při dané škále písma.</summary>
    private float VyskaTextu(string text, ResolvedTextStyle rs, float scale)
    {
        var typeface = FontResolver.Resolve(rs.FontFamily, rs.ToSKFontStyle());
        float fontSizeMm = rs.FontSize * scale * ResolvedTextStyle.PtToMm;
        float refMmToPx = 96f / 25.4f;

        using var font = new SKFont(typeface, fontSizeMm * refMmToPx);
        var lines = TextWrapHelper.WrapText(text, font, Width * refMmToPx);
        return lines.Count * fontSizeMm * rs.LineSpacing;
    }

    // ── Word wrap helpers ────────────────────────────────────────────────────

    /// <summary>Vypočítá výšku textu v mm při zalamování do dané šířky.</summary>
    internal float CalculateTextHeight(string text)
    {
        if (string.IsNullOrEmpty(text) || Width <= 0) return 8f;

        var rs = Style.Resolve();

        var typeface = FontResolver.Resolve(rs.FontFamily, rs.ToSKFontStyle());

        float fontSizeMm = rs.FontSize * ResolvedTextStyle.PtToMm;
        float refMmToPx = 96f / 25.4f;
        float fontSizePx = fontSizeMm * refMmToPx;
        float widthPx = Width * refMmToPx;

        using var font = new SKFont(typeface, fontSizePx);
        var lines = TextWrapHelper.WrapText(text, font, widthPx);
        float lineHeightMm = fontSizeMm * rs.LineSpacing;

        return Math.Max(lineHeightMm * lines.Count + fontSizeMm * 0.3f, fontSizeMm);
    }
}

public enum TextHorizontalAlignment { Left, Center, Right, Justify }
public enum TextVerticalAlignment { Top, Middle, Bottom }
