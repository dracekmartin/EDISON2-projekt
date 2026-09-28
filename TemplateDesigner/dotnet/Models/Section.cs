using System.Text.Json.Serialization;

namespace TemplateDesigner.Models;

/// <summary>
/// Univerzální sekce šablony.
/// Chování sekce je určeno properties (PrintBehavior, OverflowBehavior), ne typem třídy.
/// </summary>
public class Section
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Sekce";

    /// <summary>
    /// Nejmenší výška sekce v mm - sazba bere <c>max(tahle hodnota, obsah)</c>, takže obsah ji
    /// může zvětšit (jako „Can Grow" v Crystal Reports). Null = výška jen podle obsahu; designer
    /// pak zobrazí placeholder výšku.
    /// </summary>
    public float? DesignerHeight { get; set; } = 30f;

    /// <summary>Určuje, kdy a jak často se sekce tiskne.</summary>
    public SectionPrintBehavior PrintBehavior { get; set; } = SectionPrintBehavior.Once;

    /// <summary>
    /// Obsahová sekce začne na novém listu (`B177`, podnět zákazníka: poznámky vždy na samostatné
    /// stránce). Na listu, kde ještě nic není, se nový nezakládá; u opakovaných sekcí se nepoužije.
    /// </summary>
    /// <remarks>Nová vlastnost s výchozím <c>false</c> - starší exe ji přeskočí a sekci vytiskne
    /// za předchozím obsahem jako dřív.</remarks>
    public bool ZacitNaNoveStrance { get; set; }

    /// <summary>Určuje, jak se sekce chová při přetečení stránky.</summary>
    /// <remarks>Stará hodnota <see cref="SectionOverflowBehavior.AllowSplit"/> se čte jako
    /// „Rozdělit přes stránky" - sazba je nikdy nerozlišovala (`B177`, 24. 9.).</remarks>
    public SectionOverflowBehavior OverflowBehavior
    {
        get => _overflowBehavior;
        set => _overflowBehavior = value == SectionOverflowBehavior.AllowSplit
            ? SectionOverflowBehavior.SplitAcrossPages
            : value;
    }
    private SectionOverflowBehavior _overflowBehavior = SectionOverflowBehavior.KeepTogether;

    /// <summary>
    /// Role sekce na stránce - Header (nahoře), Body (střed), Footer (dole).
    /// Relevantní pro opakující se sekce (OnEveryPage, OnFirstPageOnly, ...):
    /// Header i Footer se opakují, ale Header je umístěn nahoře a Footer dole na stránce.
    /// Pro PrintBehavior=Once nemá vliv na umístění (sekce je tam kde je v pořadí).
    /// </summary>
    public SectionRole Role { get; set; } = SectionRole.Body;

    /// <summary>
    /// Podmínka viditelnosti (volitelné). Null = vždy viditelné.
    /// Příklady: "{STRANA} > 1" (kromě první stránky), "{POCET} > 0" (pokud jsou data).
    /// Vyhodnocuje print engine, designer zobrazuje vždy.
    /// </summary>
    public string? VisibilityExpression { get; set; }

    // --- Rámeček sekce v tisku/preview ---

    /// <summary>Zobrazit rámeček kolem sekce v preview a PDF.</summary>
    public bool ShowBorder { get; set; } = false;

    /// <summary>Barva rámečku sekce (#AARRGGBB). Výchozí: černá.</summary>
    public string BorderColor { get; set; } = "#FF000000";

    /// <summary>Tloušťka rámečku sekce v mm.</summary>
    public float BorderWidth { get; set; } = 0.3f;

    // --- Speciální properties pro některé typy sekcí ---

    /// <summary>
    /// Klíč datového zdroje pro opakující se řádky (SectionTableDesigned).
    /// Používá se pouze pokud PrintBehavior = PerDataRow.
    /// Příklad: "JIZDNI_RAD", "POLOZKY_FAKTURY".
    /// </summary>
    public string? DataSource { get; set; }

    /// <summary>
    /// Výška jednoho řádku v mm (SectionTableDesigned).
    /// Používá se pouze pokud PrintBehavior = PerDataRow.
    /// </summary>
    public float? RowHeight { get; set; }

    // --- Elementy a extensibility ---

    public List<TemplateElement> Elements { get; set; } = [];

    /// <summary>Bag pro budoucí vlastnosti bez zásahu do modelu.</summary>
    public Dictionary<string, string>? CustomProperties { get; set; }

    // --- Formulář sekce: Typ + Stránky (`B177`) ---

    /// <summary>
    /// Kde se sekce tiskne - jak ji ukazuje formulář. Jen jiný pohled na <see cref="PrintBehavior"/>
    /// a <see cref="Role"/>; do šablony se zapisují ty dvě, formát se nemění (rozhodnutí Jan 24. 9.).
    /// </summary>
    /// <remarks>Opakovaná sekce s <see cref="SectionRole.Body"/> („podle pořadí") se při otevření
    /// šablony převede na hlavičku nebo patičku (<c>TemplateSerializer</c>), takže tady už nenastane;
    /// kdyby přesto, ukáže se jako hlavička, protože tak ji zařadí sazba, leží-li nad obsahem.</remarks>
    [JsonIgnore]
    public TypSekce Typ
    {
        get => PrintBehavior is SectionPrintBehavior.Once or SectionPrintBehavior.PerDataRow
            ? TypSekce.Obsah
            : Role switch
            {
                SectionRole.Footer       => TypSekce.PatickaStranky,
                SectionRole.BelowContent => TypSekce.PodObsahemStranky,
                _                        => TypSekce.HlavickaStranky,
            };
        set
        {
            if (value == TypSekce.Obsah)
            {
                if (PrintBehavior != SectionPrintBehavior.PerDataRow) PrintBehavior = SectionPrintBehavior.Once;
                return;
            }
            Role = value switch
            {
                TypSekce.PatickaStranky    => SectionRole.Footer,
                TypSekce.PodObsahemStranky => SectionRole.BelowContent,
                _                          => SectionRole.Header,
            };
            // Pod obsahem je vždy na všech stránkách: jednou na konci je totéž co Obsah za tabulkou
            // a jen pod první částí nedává smysl (rozhodnutí Jan 24. 9.).
            if (value == TypSekce.PodObsahemStranky
                || PrintBehavior is SectionPrintBehavior.Once or SectionPrintBehavior.PerDataRow)
                PrintBehavior = SectionPrintBehavior.OnEveryPage;
        }
    }

    /// <summary>Na kterých stránkách se hlavička nebo patička tiskne. U obsahu a sekce pod obsahem
    /// se nepoužije.</summary>
    [JsonIgnore]
    public StrankySekce Stranky
    {
        get => PrintBehavior switch
        {
            SectionPrintBehavior.OnFirstPageOnly => StrankySekce.JenPrvni,
            SectionPrintBehavior.OnLastPageOnly  => StrankySekce.JenPosledni,
            _                                    => StrankySekce.Vsechny,
        };
        set
        {
            if (Typ is TypSekce.Obsah or TypSekce.PodObsahemStranky) return;
            PrintBehavior = value switch
            {
                StrankySekce.JenPrvni    => SectionPrintBehavior.OnFirstPageOnly,
                StrankySekce.JenPosledni => SectionPrintBehavior.OnLastPageOnly,
                _                        => SectionPrintBehavior.OnEveryPage,
            };
        }
    }

    // --- Computed properties ---

    /// <summary>Skutečná výška pro designer - DesignerHeight nebo placeholder.</summary>
    [JsonIgnore]
    public float EffectiveDesignerHeight => DesignerHeight ?? DefaultPlaceholderHeight;

    /// <summary>Výchozí placeholder výška pro dynamické sekce (null DesignerHeight).</summary>
    [JsonIgnore]
    public virtual float DefaultPlaceholderHeight => 30f;

    /// <summary>
    /// Vytvoří hlubokou kopii sekce s novým Id a novými Id všech elementů.
    /// Používá se při duplicitě a načítání custom šablon.
    /// </summary>
    public Section Clone()
    {
        var clone = new Section
        {
            Id = Guid.NewGuid(),
            Name = Name,
            PrintBehavior = PrintBehavior,
            OverflowBehavior = OverflowBehavior,
            ZacitNaNoveStrance = ZacitNaNoveStrance,
            Role = Role,
            ShowBorder = ShowBorder,
            BorderColor = BorderColor,
            BorderWidth = BorderWidth,
            DesignerHeight = DesignerHeight,
            VisibilityExpression = VisibilityExpression,
            DataSource = DataSource,
            RowHeight = RowHeight,
            Elements = Elements.Select(e => e.Clone()).ToList(),
            CustomProperties = CustomProperties is not null
                ? new Dictionary<string, string>(CustomProperties)
                : null
        };
        return clone;
    }
}

// ---------------------------------------------------------------------------

/// <summary>Typ sekce ve formuláři (`B177`) - neukládá se, viz <see cref="Section.Typ"/>.</summary>
public enum TypSekce { HlavickaStranky, Obsah, PodObsahemStranky, PatickaStranky }

/// <summary>Stránky opakované sekce ve formuláři - neukládá se, viz <see cref="Section.Stranky"/>.</summary>
public enum StrankySekce { Vsechny, JenPrvni, JenPosledni }

public enum SectionPrintBehavior
{
    /// <summary>Vytiskne se jednou na místě kde je.</summary>
    Once,
    /// <summary>Na každé stránce (záhlaví, zápatí).</summary>
    OnEveryPage,
    /// <summary>Pouze na první stránce.</summary>
    OnFirstPageOnly,
    /// <summary>Pouze na poslední stránce.</summary>
    OnLastPageOnly,
    /// <summary>Zůstává kvůli uloženým šablonám - při načtení se převede na <see cref="OnEveryPage"/>
    /// s podmínkou <c>{STRANA} &gt; 1</c> (`B177`, 24. 9.).</summary>
    OnEveryPageExceptFirst,
    /// <summary>Opakuje se per řádek dat (tabulky s designem vlastních řádků).</summary>
    PerDataRow
}

public enum SectionRole
{
    /// <summary>Běžný obsah stránky (výchozí).</summary>
    Body,
    /// <summary>Hlavička - umístěna nahoře na stránce.</summary>
    Header,
    /// <summary>Patička - umístěna dole na stránce.</summary>
    Footer,
    /// <summary>
    /// Na každé stránce hned za obsahem té stránky.
    /// </summary>
    /// <remarks>
    /// ⚠️ **Vlastní hodnota, ne nový význam <see cref="Body"/>** (`B134`, rozhodnutí Jan 10. 9.).
    /// První pokus přepsal chování „Obsahu" a rozbil tím existující šablony: `Body` je **výchozí
    /// hodnota**, takže ji má každá opakovaná sekce, na jejíž Umístění nikdo nesáhl - včetně
    /// hlaviček, které se tím sesypaly pod tabulku.
    /// </remarks>
    BelowContent
}

public enum SectionOverflowBehavior
{
    /// <summary>Celá sekce přejde na další stránku pokud se nevejde.</summary>
    KeepTogether,
    /// <summary>Sekce se rozdělí - pokračuje na další stránce (tabulky).</summary>
    SplitAcrossPages,
    /// <summary>Zůstává jen kvůli uloženým šablonám - chovala se jako <see cref="SplitAcrossPages"/>
    /// a při načtení se na ni převede. Z nabídky zmizela 24. 9. (`B177`).</summary>
    AllowSplit
}
