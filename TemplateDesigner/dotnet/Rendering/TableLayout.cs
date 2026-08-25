using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Rendering;

/// <summary>
/// Sdílené řádkové tabulkové jádro (R1) - algoritmy společné všem řádkovým tabulkovým elementům
/// (StopList, DynamicTable, LineTable). Stojí NAD <see cref="CellRenderer"/> / <see cref="ColumnRenderer"/>.
///
/// **Záměrně nesjednocuje datovou třídu sloupce** - StopColumn i TableColumn zůstávají (odlišná
/// serializace, odlišné potřeby). Elementy sem jen promítnou neutrální tvar (<see cref="ColWidth"/>,
/// per-column TextStyle). Tím je nulové riziko pro uložené šablony. DepartureGrid je mimo (pivot).
///
/// Viz `.claude/docs/r1-sdilene-tabulkove-jadro-navrh.md`.
/// </summary>
public static class TableLayout
{
    /// <summary>Neutrální popis šířky sloupce pro <see cref="ResolveWidths"/>.
    /// <paramref name="WidthMm"/> = null → auto (rovnoměrné rozdělení zbytku).</summary>
    public readonly record struct ColWidth(float? WidthMm, ColumnWidthMode Mode = ColumnWidthMode.Mm);

    /// <summary>
    /// Vyřeší šířky sloupců v mm. Pevné (Width != null) se honorují (Percent přepočítá na mm z šířky
    /// elementu), auto (null) si rovným dílem rozdělí zbytek po pevných sloupcích. Nikdy záporné.
    ///
    /// Sjednocuje `StopListElement.ResolveColumnWidths` (mm, auto = null) a
    /// `DynamicTableElement.ComputeColumnWidths` (mm/%, auto = Width&lt;=0) do jednoho vzorce -
    /// behavior-preserving pro oba (auto sentinel se převádí na hranici v projekci elementu).
    /// </summary>
    public static float[] ResolveWidths(IReadOnlyList<ColWidth> cols, float elementWidthMm,
        float minAutoWidthMm = 0f)
    {
        float Fixed(ColWidth c) => FixedWidth(c, elementWidthMm);
        var (fixedSum, autoCount) = SplitFixedAuto(cols, elementWidthMm);

        // Nevejde se ani při minimálních auto sloupcích → šířky se proporcionálně zmenší, aby
        // tabulka dosedla přesně na šířku elementu (pravidlo „fixed-then-shrink" jako u výšky).
        // Element musí stejnou škálou zmenšit i fonty - viz ComputeWidthScale.
        float scale = WidthScale(fixedSum, autoCount, elementWidthMm, minAutoWidthMm);
        if (scale < 1f)
        {
            var shrunk = new float[cols.Count];
            for (int i = 0; i < cols.Count; i++)
                shrunk[i] = (cols[i].WidthMm.HasValue ? Fixed(cols[i]) : minAutoWidthMm) * scale;
            return shrunk;
        }

        float autoEach = autoCount > 0 ? MathF.Max(0f, (elementWidthMm - fixedSum) / autoCount) : 0f;

        var result = new float[cols.Count];
        for (int i = 0; i < cols.Count; i++)
            result[i] = cols[i].WidthMm.HasValue ? Fixed(cols[i]) : autoEach;
        return result;
    }

    /// <summary>
    /// Škála, kterou musí element použít na **fonty a výšky řádků**, aby se tabulka vešla do své
    /// šířky; 1 = vejde se, nic se nemění. Vodorovný protějšek `ComputeFontScale` (ta řeší jen výšku).
    ///
    /// Bez toho se tabulka, která je širší než element, **oříznula** (svislé varování ji nezachytí -
    /// hlídá jen výšku), a jediné, co na šířku reagovalo, bylo zmenšování textu v jednotlivé buňce -
    /// to ale každá buňka počítá sama, takže vedle sebe vycházely různé velikosti písma.
    /// </summary>
    /// <param name="minAutoWidthMm">Nejmenší rozumná šířka auto sloupce v mm (pod ní je obsah
    /// nečitelný). Určuje, kdy tabulka „přeteče" - u sloupců bez vlastní šířky je to jejich minimum.</param>
    public static float ComputeWidthScale(IReadOnlyList<ColWidth> cols, float elementWidthMm,
        float minAutoWidthMm = 0f)
    {
        var (fixedSum, autoCount) = SplitFixedAuto(cols, elementWidthMm);
        return WidthScale(fixedSum, autoCount, elementWidthMm, minAutoWidthMm);
    }

    /// <summary>Škála pro element, který si šířky sloupců počítá sám (LineTable) - z celkové
    /// požadované šířky při škále 1.</summary>
    public static float ComputeWidthScale(float requiredWidthMm, float elementWidthMm) =>
        requiredWidthMm <= 0f || elementWidthMm <= 0f || requiredWidthMm <= elementWidthMm
            ? 1f : elementWidthMm / requiredWidthMm;

    private static float FixedWidth(ColWidth c, float elementWidthMm) =>
        c.Mode == ColumnWidthMode.Percent ? elementWidthMm * (c.WidthMm ?? 0f) / 100f : (c.WidthMm ?? 0f);

    private static (float FixedSum, int AutoCount) SplitFixedAuto(
        IReadOnlyList<ColWidth> cols, float elementWidthMm)
    {
        float fixedSum = 0f;
        int autoCount = 0;
        foreach (var c in cols)
        {
            if (c.WidthMm.HasValue) fixedSum += FixedWidth(c, elementWidthMm);
            else autoCount++;
        }
        return (fixedSum, autoCount);
    }

    private static float WidthScale(float fixedSum, int autoCount, float elementWidthMm,
        float minAutoWidthMm) =>
        ComputeWidthScale(fixedSum + autoCount * minAutoWidthMm, elementWidthMm);

    /// <summary>
    /// Vyřeší styl buňky sloupce kaskádou `column-override → element-parent → (baked v parentu)`.
    /// <paramref name="columnLayer"/> = per-column TextStyle (u StopColumn `Style.Header/Data`,
    /// u TableColumn `HeaderStyle/DataStyle`); null = dědit z elementu.
    /// Legacy <paramref name="legacyAlignment"/> (column.Alignment) se použije jako HAlign jen když
    /// ho nedefinuje ani sloupec, ani element (<paramref name="elementDefinesHAlign"/>).
    ///
    /// Sjednocuje `ResolveHeaderStyle`/`ResolveDataStyle` (3× tentýž vzorec ve StopList/DynamicTable/LineTable).
    /// </summary>
    public static ResolvedTextStyle ResolveCascade(
        TextStyle? columnLayer,
        ResolvedTextStyle elementParent,
        TextHorizontalAlignment legacyAlignment,
        bool elementDefinesHAlign)
    {
        var resolved = columnLayer is { } layer ? layer.Resolve(elementParent) : elementParent;
        if (columnLayer?.HAlign is null && !elementDefinesHAlign)
            resolved = resolved with { HAlign = legacyAlignment };
        return resolved;
    }

    /// <summary>Jeden stránkový blok tabulky: které sloupce spojů do něj patří.</summary>
    /// <param name="Od">Index prvního sloupce spoje (0-based).</param>
    /// <param name="Pocet">Kolik sloupců spojů blok nese.</param>
    public readonly record struct SloupcovyBlok(int Od, int Pocet);

    /// <summary>
    /// Rozdělí sloupce spojů do <b>stránkových bloků</b>, které se vejdou na šířku - to, čemu starý
    /// modul říká „střih". Popisové sloupce (zastávky, zóny, km…) se opakují v <b>každém</b> bloku,
    /// takže na spoje zbývá jen zbytek šířky.
    /// </summary>
    /// <remarks>
    /// <para><b>Řez je čistě geometrický</b> - podle šířek a písma, ne podle skupin dnů. Změřeno
    /// na starém modulu (analýza §16): na lince s 30 pracovními, 5 sobotními a 6 nedělními spoji
    /// padl řez na 21+20 a 39+2, tedy nikdy na hranici skupiny. Blok proto běžně obsahuje konec
    /// jedné skupiny a začátek další.</para>
    /// <para><b>Vejde-li se všechno, vrátí jediný blok</b> - volající pak nemusí řešit zvláštní
    /// případ.</para>
    /// <para>⚠️ Do bloku jde <b>vždy aspoň jeden</b> sloupec spoje, i když se nevejde ani ten:
    /// jinak by dělení nikdy neskončilo. Takový blok přeteče a je to lepší než zacyklení nebo
    /// tiše zahozený spoj.</para>
    /// </remarks>
    public static List<SloupcovyBlok> RozdelSpojeDoBloku(
        int pocetSpoju, float popisneSloupceMm, float sloupecSpojeMm, float sirkaMm)
    {
        var bloky = new List<SloupcovyBlok>();
        if (pocetSpoju <= 0) return bloky;

        float proSpoje = sirkaMm - popisneSloupceMm;
        int naBlok = sloupecSpojeMm > 0.01f && proSpoje > 0.01f
            ? (int)MathF.Floor(proSpoje / sloupecSpojeMm)
            : 0;
        if (naBlok < 1) naBlok = 1;

        for (int od = 0; od < pocetSpoju; od += naBlok)
            bloky.Add(new SloupcovyBlok(od, Math.Min(naBlok, pocetSpoju - od)));
        return bloky;
    }

}
