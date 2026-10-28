using SkiaSharp;
using TemplateDesigner.Rendering;

namespace TemplateDesigner.Models.Elements;

/// <summary>
/// Mřížka minut odjezdů - jeden nebo více typů provozu vedle sebe.
/// SkupinaDnuId = "" → zobrazí všechny skupiny dnů vedle sebe se sdíleným sloupcem hodin.
/// SkupinaDnuId = "SMO1" → zobrazí pouze jednu skupinu.
/// SkupinaDnuId = "SMO1,SMO2" → zobrazí vybrané skupiny.
/// Height = null → dynamická výška; Height = pevná → auto-scale fontu při přetečení.
/// </summary>
public class DepartureGridElement : StopScheduleElementBase, IColumnLayoutOwner
{
    // ═══════════════════════════════════════════
    // Filtr skupin dnů
    // ═══════════════════════════════════════════

    /// <summary>
    /// ID skupin dnů k zobrazení. Prázdný = všechny, čárkou oddělené = výběr.
    /// </summary>
    public string SkupinaDnuId { get; set; } = "";

    // ═══════════════════════════════════════════
    // Záhlaví skupin dnů
    // ═══════════════════════════════════════════
    public bool ShowSkupinaDnuHeader { get; set; } = true;
    public float SkupinaDnuHeaderLineHeight { get; set; } = 4f;

    /// <summary>Zarovnání textu záhlaví skupin: Top, Center, Bottom.</summary>
    public TextVerticalAlignment HeaderVerticalAlignment { get; set; } = TextVerticalAlignment.Bottom;

    /// <summary>
    /// Styl 2. řádku záhlaví skupin dnů (např. „den" v „Pracovní / den"). Null = stejný jako HeaderStyle (Line 1).
    /// Aplikuje se POUZE když SkupinaDnu.Nadpis2 je vyplněn. Cascade: HeaderLine2Style → HeaderStyle → ScheduleHeaderDefaults.
    /// </summary>
    public TextStyle? HeaderLine2Style { get; set; }

    // ═══════════════════════════════════════════
    // Mřížka minut
    // ═══════════════════════════════════════════
    public float HourColumnWidth { get; set; } = 8f;
    public float MinuteWidth { get; set; } = 5f;

    /// <summary>Jak se skládají odjezdy do řádku hodiny. Výchozí <see cref="DepartureSpacing.Even"/>
    /// (`B143`, rozhodnutí Jan 11. 9.) = jeden rozestup pro **celý prvek**, takže minuty stojí ve
    /// svislici napříč řádky i sloupci - sázení starého modulu a to, co chce skoro každý.
    /// <see cref="DepartureSpacing.ByContent"/> = slot roste o šířku značek, takže minuta se značkou
    /// odsune ty za sebou. Šablonám uloženým před 12. 8. 2026 dopíše `ByContent` migrace 1.11 → 1.12,
    /// aby se jim tisk nezměnil.</summary>
    public DepartureSpacing MinuteSpacing { get; set; } = DepartureSpacing.Even;
    public float MinuteFontSize { get; set; } = 7f;

    /// <summary>Styl textu minuty (font, bold, barva). Null properties = element defaults.</summary>
    public TextStyle MinuteStyle { get; set; } = new();

    /// <summary>Styl anotací typu speciální/NZ (symboly ×, F, H). Null properties = 80% velikosti minuty.</summary>
    public TextStyle AnotaceSymbolStyle { get; set; } = new();

    /// <summary>Styl anotací typu text (písmena a, A, dny). Null properties = AnotaceFontSize.</summary>
    public TextStyle AnotaceTextStyle { get; set; } = new();

    /// <summary>Mezera mezi minutou a první anotací (mm). 0 = bez mezery.</summary>
    public float AnotaceGap { get; set; } = 0f;

    /// <summary>Mezera mezi anotacemi navzájem (mm). 0 = bez mezery.</summary>
    public float AnotaceInterGap { get; set; } = 0f;

    /// <summary>Mezera mezi odjezdy v rámci jedné buňky (mm). 0 = jen MinuteWidth slot.</summary>
    public float OdjezdGap { get; set; } = 0f;

    /// <summary>Šířka oddělovače mezi skupinami dnů (mm).</summary>
    public float GroupSeparatorWidth { get; set; } = 0.5f;

    /// <summary>Sloupec hodin před každou skupinou dnů (místo jednoho sdíleného vlevo).</summary>
    public bool HourColumnPerGroup { get; set; }

    /// <summary>Maximální počet zobrazených skupin dnů (0 = bez limitu, default). Viditelnost
    /// jednotlivých skupin řídí checkboxy v dialogu sloupců (GroupColumns.IsVisible) - strop je
    /// jen nouzová pojistka; starý default 3 tiše ořezával 4. SMO sloupec (migrace 1.4.0).</summary>
    public int MaxGroupCount { get; set; }

    // ═══════════════════════════════════════════
    // Časová osa a roztažení na výšku
    // ═══════════════════════════════════════════
    // Obojí řídí **parametry sestavy** (`CASOVA_OSA` + `HODINY_OD`/`DO`, `ROZTAHNOUT_NA_VYSKU`).
    // Agendy JŘ je posílají vždy, takže vlastnost prvku by parametr přebil = mrtvé UI. Zůstávají
    // jen výchozí hodnoty shodné s parametry, pro kreslení bez dat.

    private const TimeAxisMode DefaultTimeAxis = TimeAxisMode.Compact;
    private const int DefaultRangeFrom = 4;
    private const int DefaultRangeTo = 23;
    private const bool DefaultAutoExpand = false;

    /// <summary>
    /// Strop pro roztažení na výšku (parametr ROZTAHNOUT_NA_VYSKU) - řádky se roztáhnou max na tento násobek
    /// přirozené výšky. Brání bizarně obřím řádkům u řídkých mřížek (např. linka jedoucí jen v 1 hodinu).
    /// </summary>
    private const float MaxAutoExpandFactor = 1.5f;

    // ═══════════════════════════════════════════
    // Řádky dat (DataRowHeight je na ScheduleElementBase)
    // ═══════════════════════════════════════════

    /// <summary>Minuty tučně.</summary>
    public bool MinuteBold { get; set; } = true;

    /// <summary>Barva pozadí sudých řádků (zebra pattern). Null = bez podbarvení.</summary>
    public string? AlternatingRowColor { get; set; } = "#FFF5F5F5";

    // ═══════════════════════════════════════════
    // Anotace
    // ═══════════════════════════════════════════
    public bool ShowAnotace { get; set; } = true;
    public float AnotaceFontSize { get; set; } = 5f;

    /// <summary>Pozadí invertované NZ u minuty (#AARRGGBB). Default černá.</summary>
    public string NegativniZnackaBackColor { get; set; } = "#FF000000";

    /// <summary>Barva znaku invertované NZ u minuty (#AARRGGBB). Default bílá.</summary>
    public string NegativniZnackaForeColor { get; set; } = "#FFFFFFFF";

    // ═══════════════════════════════════════════
    // Nastavení sloupců
    // ═══════════════════════════════════════════

    /// <summary>Vizuální styl sloupce hodin (záhlaví "Hod" + čísla hodin). Null = element defaults.</summary>
    public ColumnVisualStyle? HourColumnStyle
    {
        get => _hourColumnStyle;
        set { _hourColumnStyle = value; _cachedLayout = null; }
    }
    private ColumnVisualStyle? _hourColumnStyle;

    /// <summary>Override nadpisu sloupce hodin v šabloně. Prázdné = výchozí „Hod"
    /// (přes <see cref="Rendering.ColumnHeader.Resolve"/>, jednotně se skupinami i ostatními komponentami).</summary>
    public string HourColumnHeader { get; set; } = "";

    /// <summary>Obrázek do záhlaví sloupce hodin (Base64 PNG/JPG) - kreslí se v **každé** skupině
    /// dnů, protože sloupec hodin se u každé opakuje (podnět #12843 bod 9: hodinová ikona ve vývěsce
    /// Trenčína). Prázdné = kreslí se nadpis. Když je obrázek nastavený, **nadpis se nekreslí** -
    /// jinak by se překrývaly a jejich vývěska tam žádný text nemá.</summary>
    public string? HourColumnHeaderImageBase64 { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    private readonly Rendering.CachedBase64Image _hourHeaderImage = new();

    public List<DepartureGroupColumnStyle> GroupColumns
    {
        get => _groupColumns;
        set { _groupColumns = value; _cachedLayout = null; }
    }
    private List<DepartureGroupColumnStyle> _groupColumns = [];

    /// <summary>Vlastní definice sloupců vývěsky (dny + značky). Null nebo prázdné = režim
    /// „Automaticky (dle linky)" - server staví sloupce podle nastavení linky jako dosud.
    /// Neprázdné → posílá se serveru jako parametr DEFINICE_SLOUPCU a přebíjí linkové
    /// rozvržení i seznamy značek celé. Jedna definice platí pro celou šablonu (ovlivňuje
    /// data celé vývěsky včetně legendy, ne jen tento prvek).</summary>
    public List<DepartureColumnDefinition>? CustomColumns
    {
        get => _customColumns;
        set { _customColumns = value; _cachedLayout = null; }
    }
    private List<DepartureColumnDefinition>? _customColumns;

    /// <summary>Maximální počet vlastních sloupců - víc se na vývěsku rozumně nevejde.</summary>
    public const int MaxCustomColumns = 5;

    /// <summary>Wire JSON definice sloupců pro parametr DEFINICE_SLOUPCU. Skládá ho sdílené
    /// <see cref="ColumnLayoutWire"/>, ať se tvar nerozejde mezi prvky.</summary>
    public string? BuildColumnsDefinitionWireJson() => ColumnLayoutWire.Build(CustomColumns);

    // ─── Designer ───────────────────────────────────────────────────────────

    protected override float DesignerPlaceholderHeight => 60f;

    // ═══════════════════════════════════════════════════════════════════════════
    // Layout computation
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Jeden řádek záhlaví skupiny dnů. IsSecondary = pochází z Nadpis2 (jiný styl).</summary>
    internal record HeaderLine(string Text, bool IsSecondary);

    /// <summary>Šířky slotů odjezdů po uplatnění režimu rozestupu. `ByContent` je nechá, jak jsou
    /// (každá minuta zabere jen to, co potřebuje). `Even` je srovná na **nejširší z nich**, takže
    /// minuty stojí ve svislici napříč řádky a značka nerozhodí sousedy - za cenu toho, že jeden
    /// odjezd s víc značkami roztáhne rozestup celé skupině.
    /// Šířka skupiny počítá se značkami i rozestupem stejně jako kreslení (<see cref="PotrebneSirkyMinut"/>);
    /// „Šířka minuty" je pro rastr jen spodní mez.</summary>
    internal static List<float> ApplySpacingMode(List<float> slotW, DepartureSpacing mode, float uniformSlotMm)
    {
        if (mode == DepartureSpacing.ByContent || slotW.Count == 0 || uniformSlotMm <= 0) return slotW;
        return [.. slotW.Select(_ => uniformSlotMm)];
    }

    /// <summary>Rozteč pevného rastru: nastavená šířka minuty, nejméně však minuta + mezera + místo
    /// na **jednu** značku + odstup od dalšího odjezdu. Nejde o nejhorší odjezd - kde značka není,
    /// místo zůstane prázdné a minuty stojí v mřížce (vzor vývěsky TRANSDEV k `B160`).
    ///
    /// <para>⚠️ Obě rezervy jsou nutné a každá z jiného důvodu. Bez místa na značku zbylo na ni jen
    /// to, co nedobraly číslice, a zmenšování ji srazilo na mez čitelnosti. Bez odstupu vyšla rozteč
    /// přesně na označkovaný odjezd, takže se slepily (`33₂₃42₇₀49₇₀` místo `33₂₃ 42₇₀ 49₇₀`) -
    /// rovnoměrný rastr to nemá, protože počítá se všemi značkami nejhoršího odjezdu a ten přebytek
    /// odstup udělá sám. Obojí nález Jan 18. 9.</para></summary>
    internal static float PevnyRastr(float nominalMm, float minutaMm, float mezeraMm, float jednaZnackaMm,
        float odstupMm = 0f) =>
        MathF.Max(nominalMm, minutaMm + mezeraMm + jednaZnackaMm + (jednaZnackaMm > 0f ? odstupMm : 0f));

    /// <summary>Škála písma značek v pevném rastru: zmenší je tak, aby se za minutu do rozteče vešly.
    /// Nikdy pod mez zmenšování (<see cref="Rendering.CellRenderer.MinScale"/>) - pod ní by z nich byly
    /// tečky, což je horší než těsný tisk. Vejdou-li se, vrací `dataScale` beze změny.</summary>
    internal static float ZmenseniZnacek(float rastrMm, float minutaMm, float znackyMm, float dataScale,
        params ResolvedTextStyle[] styly)
    {
        float volnoMm = rastrMm - minutaMm;
        if (znackyMm <= 0f || volnoMm <= 0f || znackyMm <= volnoMm) return dataScale;

        float dno = 0f;
        foreach (var s in styly) dno = MathF.Max(dno, Rendering.CellRenderer.MinShrinkScale(s, dataScale));
        return MathF.Max(dataScale * (volnoMm / znackyMm), dno);
    }

    /// <summary>Rastr rovnoměrného rozestupu: **nejširší odjezd celého prvku** (minuta + její
    /// značky), nejméně však nominální šířka minuty. Počítá se přes všechny skupiny dnů, aby čísla
    /// stála pod sebou napříč hodinami **i sloupci** - za řádek by hodina se značkami měla jiný
    /// rozestup než hodina bez nich, za sloupec by se rozešly sloupce mezi sebou (`B143`).</summary>
    internal static float UniformSlotWidth(IEnumerable<float> sloty, float nominalMm)
    {
        float max = nominalMm;
        foreach (float s in sloty) if (s > max) max = s;
        return max;
    }

    /// <summary>Styly značek u minut - jedno místo pro rozvržení i kreslení, aby šířka skupiny počítala
    /// se stejným písmem značek, jakým se nakreslí.</summary>
    private (ResolvedTextStyle Sym, ResolvedTextStyle Text, ResolvedTextStyle Glyph) StylyZnacek(DataContext? data)
    {
        var dataBase = DataStyle.Resolve(ScheduleDataDefaults);
        var minuteElementStyle = ResolveGroupMinuteStyle(null);
        var sym = AnotaceSymbolStyle.Resolve(dataBase);
        if (AnotaceSymbolStyle.FontSize is null)
            sym = sym with { FontSize = minuteElementStyle.FontSize * 0.8f };
        var text = ResolveAnotaceTextStyle(dataBase);
        // Symbolový JŘ font (piktogramy) pro jednoznakové SpecialniZnak - dle jazyka šablony.
        // Uživatelský font v AnotaceSymbolStyle (pokud zvolen) má přednost; jinak embedded TimeTable2.
        var glyph = AnotaceSymbolStyle.FontFamily is null
            ? sym with { FontFamily = FontResolver.SymbolFontFamily(data?.Culture) }
            : sym;
        return (sym, text, glyph);
    }

    /// <summary>Šířky odjezdů (minuta + značky) a rastr rozestupu pro dané měřítko písma. Jeden výpočet
    /// pro rozvržení (měřítko 1) i kreslení - do 26. 10. je rozvržení počítalo bez značek (`B194a` C12).</summary>
    private sealed class SlotyOdjezdu : IDisposable
    {
        public required SKFont Sym { get; set; }
        public required SKFont Glyph { get; set; }
        public required SKFont Text { get; set; }
        /// <summary>Nominální šířka minuty v mm (už v měřítku).</summary>
        public required float MinutaMm { get; init; }
        public required float MezeraPx { get; init; }
        public required float MeziZnackamiPx { get; init; }
        public required float MmToPx { get; init; }
        public required bool SeZnackami { get; init; }
        /// <summary>Rozteč rovnoměrného nebo pevného rastru po skupinách (bez záznamu = podle obsahu).</summary>
        public Dictionary<string, float> Rastr { get; } = [];

        public float SirkaMm(OdjezdData o)
        {
            float extraPx = 0f;
            if (SeZnackami && o.Anotace.Count > 0)
            {
                extraPx = MezeraPx;
                foreach (var a in o.Anotace)
                {
                    SKFont af = JeGlyfSymbol(a) ? Glyph : (a.Typ == TypAnotace.Text ? Text : Sym);
                    bool nzBox = a.Typ == TypAnotace.NegativniZnacka;
                    extraPx += af.MeasureText(GlyfProKresleni(a)) + (nzBox ? af.Size * 0.12f * 2f : 0f) + MeziZnackamiPx;
                }
            }
            return MinutaMm + extraPx / MmToPx;
        }

        public void Dispose()
        {
            Sym.Dispose(); Glyph.Dispose(); Text.Dispose();
        }
    }

    private SlotyOdjezdu PripravSloty(IReadOnlyList<GroupLayout> skupiny, IReadOnlyCollection<SKFont> fontyMinut,
        (ResolvedTextStyle Sym, ResolvedTextStyle Text, ResolvedTextStyle Glyph) styly, float dataScale, float mmToPx)
    {
        var s = new SlotyOdjezdu
        {
            Sym = styly.Sym.CreateFont(dataScale, mmToPx),
            Glyph = styly.Glyph.CreateFont(dataScale, mmToPx),
            Text = styly.Text.CreateFont(dataScale, mmToPx),
            MinutaMm = (MinuteWidth + AnotaceGap) * dataScale,
            MezeraPx = AnotaceGap * dataScale * mmToPx,
            MeziZnackamiPx = AnotaceInterGap * dataScale * mmToPx,
            MmToPx = mmToPx,
            SeZnackami = ShowAnotace,
        };
        var odjezdy = skupiny.SelectMany(g => g.ByHour.Values).SelectMany(o => o).ToList();

        // Rovnoměrný rozestup: jeden rastr na **celý prvek**, ne na řádek ani na sloupec.
        // Za řádek by hodina se značkami měla jiný rozestup než hodina bez nich; za sloupec
        // se rozcházely sloupce mezi sebou - jeden označkovaný spoj roztáhl svůj sloupec
        // a sousední zůstal těsný (nález Jan 11. 9. na Nitře 4, `B143`). Cena: sloupce bez
        // značek zřídnou na šířku toho nejhustšího.
        if (MinuteSpacing == DepartureSpacing.Even)
        {
            float rastr = UniformSlotWidth(odjezdy.Select(s.SirkaMm), s.MinutaMm);
            foreach (var g in skupiny) s.Rastr[g.Skupina.Id] = rastr;
        }
        // Pevný rastr (`B160`): rozteč je nastavená šířka minuty, značky do ní nemluví - hustota
        // vývěsky pak nezávisí na tom, kolik má linka značek. Značky, které se do zbytku rastru
        // nevejdou, se zmenší; jedním poměrem pro celý prvek, aby nebyly každá jinak velká.
        else if (MinuteSpacing == DepartureSpacing.Fixed)
        {
            float minutaMm = 0f, odstupMm = 0f;
            foreach (var f in fontyMinut)
            {
                minutaMm = MathF.Max(minutaMm, f.MeasureText("00") / mmToPx);
                // Odstup = mezera v písmu minut, ať roste s velikostí a nemusí se nastavovat.
                odstupMm = MathF.Max(odstupMm, f.MeasureText(" ") / mmToPx);
            }

            float jednaZnackaMm = 0f, vsechnyZnackyMm = 0f;
            foreach (var o in odjezdy)
            {
                vsechnyZnackyMm = MathF.Max(vsechnyZnackyMm, s.SirkaMm(o) - s.MinutaMm);
                if (!ShowAnotace) continue;
                foreach (var a in o.Anotace)
                {
                    SKFont af = JeGlyfSymbol(a) ? s.Glyph : (a.Typ == TypAnotace.Text ? s.Text : s.Sym);
                    float boxPx = a.Typ == TypAnotace.NegativniZnacka ? af.Size * 0.12f * 2f : 0f;
                    jednaZnackaMm = MathF.Max(jednaZnackaMm, (af.MeasureText(GlyfProKresleni(a)) + boxPx) / mmToPx);
                }
            }

            // V rozteči je místo na **jednu** značku, ne na nejhorší odjezd - kde značka není,
            // místo zůstane prázdné a minuty stojí v mřížce. Doloženo vzorem vývěsky, kterou
            // TRANSDEV posílal k `B160` (linka 32 Nitra): `18 38 58` i `08 к 18 28 к 38` mají
            // tytéž pozice minut. Spoj s víc značkami se do místa vejde zmenšením.
            float rastr = PevnyRastr(s.MinutaMm, minutaMm, AnotaceGap * dataScale, jednaZnackaMm, odstupMm);

            float anotScale = ZmenseniZnacek(rastr, minutaMm, vsechnyZnackyMm, dataScale, styly.Sym, styly.Text, styly.Glyph);
            if (anotScale < dataScale)
            {
                s.Sym.Dispose(); s.Glyph.Dispose(); s.Text.Dispose();
                s.Sym = styly.Sym.CreateFont(anotScale, mmToPx);
                s.Glyph = styly.Glyph.CreateFont(anotScale, mmToPx);
                s.Text = styly.Text.CreateFont(anotScale, mmToPx);
            }
            foreach (var g in skupiny) s.Rastr[g.Skupina.Id] = rastr;
        }
        return s;
    }

    /// <summary>Šířky odjezdů jedné buňky (skupina × hodina) po rozestupu.</summary>
    private List<float> SirkySlotu(SlotyOdjezdu s, string skupinaId, List<OdjezdData> odjezdy) =>
        ApplySpacingMode([.. odjezdy.Select(s.SirkaMm)], MinuteSpacing, s.Rastr.GetValueOrDefault(skupinaId));

    /// <summary>Šířka, kterou skupina potřebuje na minuty nejplnější viditelné hodiny (měřítko 1),
    /// i s vnitřními okraji a půlmilimetrovými odstupy kreslení od hran.</summary>
    private Dictionary<string, float> PotrebneSirkyMinut(List<GroupLayout> skupiny, List<int> hodiny,
        Func<string, ResolvedTextStyle> stylMinut, DataContext? data, float mmToPx)
    {
        // Skupina se stejným ID se v datech opakovat smí (ukázková data šablony MHD) - jeden font na ID.
        var fonty = new Dictionary<string, SKFont>();
        foreach (var g in skupiny)
            if (!fonty.ContainsKey(g.Skupina.Id)) fonty[g.Skupina.Id] = stylMinut(g.Skupina.Id).CreateFont(1f, mmToPx);
        try
        {
            using var sloty = PripravSloty(skupiny, fonty.Values, StylyZnacek(data), 1f, mmToPx);
            var vysledek = new Dictionary<string, float>();
            foreach (var g in skupiny)
            {
                var ms = stylMinut(g.Skupina.Id);
                float minuty = 0f;
                foreach (int h in hodiny)
                {
                    if (!g.ByHour.TryGetValue(h, out var odjezdy) || odjezdy.Count == 0) continue;
                    float blok = SirkySlotu(sloty, g.Skupina.Id, odjezdy).Sum() + (odjezdy.Count - 1) * OdjezdGap;
                    minuty = MathF.Max(minuty, blok);
                }
                vysledek[g.Skupina.Id] = minuty + ms.PaddingLeft + ms.PaddingRight + 1f;
            }
            return vysledek;
        }
        finally
        {
            foreach (var f in fonty.Values) f.Dispose();
        }
    }

    /// <summary>Skupiny pod svou potřebou dorovná na potřebu na úkor skupin s rezervou (úměrně
    /// rezervě); nepinnuté skupiny nad potřebou a pinnuté zůstanou. Když nic nechybí, vrátí šířky
    /// beze změny - vývěsky, které se vešly, se tím nehnou.</summary>
    private static List<GroupLayout> DorovnejNaPotrebu(List<GroupLayout> skupiny, List<float> potrebne,
        Func<GroupLayout, float?> pinnuta)
    {
        float chybi = 0f, rezerva = 0f;
        for (int i = 0; i < skupiny.Count; i++)
        {
            if (pinnuta(skupiny[i]) is not null) continue;
            float rozdil = skupiny[i].GroupWidth - potrebne[i];
            if (rozdil < 0f) chybi -= rozdil; else rezerva += rozdil;
        }
        if (chybi <= 0.001f || rezerva <= 0f) return skupiny;

        float podil = MathF.Min(1f, chybi / rezerva);
        return [.. skupiny.Select((g, i) =>
        {
            if (pinnuta(g) is not null) return g;
            float rozdil = g.GroupWidth - potrebne[i];
            return rozdil < 0f
                ? g with { GroupWidth = potrebne[i] }
                : g with { GroupWidth = g.GroupWidth - rozdil * podil };
        })];
    }

    /// <summary>Slova nadpisu pro výpočet minimální šířky skupiny. **Ruční zlom dělí slova stejně
    /// jako mezera** - jinak by dvouřádkový nadpis byl jedno dlouhé slovo a roztáhl minimální šířku
    /// na součet obou řádků.</summary>
    internal static string[] HeaderWords(string fullName) =>
        [.. Rendering.CellLayout.SplitLines(fullName)
            .SelectMany(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries))];

    /// <summary>Řádky záhlaví skupiny dnů. **Ruční zlom** (Enter nebo token) v nadpisu = další řádek
    /// **téhož** nadpisu, tedy stejný styl. **Druhý nadpis z dat** je jiná věc - má vlastní styl
    /// (`HeaderLine2Style`), proto jde zvlášť jako sekundární řádek a zalamuje se svým písmem (`wrap2`).
    /// Každý řádek se dozalomí podle šířky skupiny. Do 26. 10. se se druhým nadpisem nezalamovalo nic
    /// a dlouhé období („1.7. - 31.8. a 27.12. - …") přetékalo do sousední skupiny (`B194a` C12).</summary>
    internal static HeaderLine[] ComposeHeaderLines(string nadpis, string nadpis2,
        Func<string, IEnumerable<string>> wrap, Func<string, IEnumerable<string>>? wrap2 = null)
    {
        var rucniRadky = Rendering.CellLayout.SplitLines(nadpis);
        var hlavni = rucniRadky.SelectMany(wrap).Select(w => new HeaderLine(w, IsSecondary: false));

        if (nadpis2.Length > 0)
            return [.. hlavni.Concat((wrap2 ?? wrap)(nadpis2).Select(w => new HeaderLine(w, IsSecondary: true)))];

        return [.. hlavni];
    }

    private record GroupLayout(
        SkupinaDnuData Skupina,
        Dictionary<int, List<OdjezdData>> ByHour,
        int MaxMinutesPerHour,
        float GroupWidth,
        HeaderLine[] HeaderLines,
        float MinWidthForText);

    private record GridLayout(
        List<GroupLayout> Groups,
        List<int> Hours,
        float FontSizeMm,
        float HeaderFontMm,
        float MinuteFontMm,
        float RowH,
        float HeaderH,
        float GroupHeaderH,
        int MaxHeaderLines,
        float SirkaSloupceHodin,
        float MeritkoSirky,
        bool NevejdeSe,
        float[] PotrebneSirky);

    /// <summary>Kolikrát se mřížka zmenší, aby se skupiny vešly do šířky prvku (`B194a` C12):
    /// 1 = vejde se. Pod mez zmenšování nejde - zbytek se pak ořízne (<see cref="NevejdeSeNaSirku"/>).</summary>
    private const float MinMeritkoSirky = Rendering.CellRenderer.MinScale;

    // Cache layoutu - aby se nepočítal dvakrát (CalculateHeight + Render) pro stejná data
    [System.Text.Json.Serialization.JsonIgnore]
    private GridLayout? _cachedLayout;
    [System.Text.Json.Serialization.JsonIgnore]
    private DataContext? _cachedLayoutDataRef;
    [System.Text.Json.Serialization.JsonIgnore]
    private int _cachedLayoutFingerprint;

    /// <summary>Rychlý fingerprint layout-affecting properties pro invalidaci cache.</summary>
    private int ComputeLayoutFingerprint() => HashCode.Combine(
        HashCode.Combine(SkupinaDnuHeaderLineHeight,
            MinuteFontSize, MinuteBold, AnotaceFontSize, MinuteWidth, HourColumnWidth, MaxGroupCount, GroupColumns.Count),
        // Styly podle hodnot: formulář je mění na místě, objekt stylu zůstává týž.
        HashCode.Combine(HeaderStyle.ContentHash(), DataStyle.ContentHash(), MinuteStyle.ContentHash(),
            HeaderLine2Style?.ContentHash() ?? 0, AnotaceSymbolStyle.ContentHash(), AnotaceTextStyle.ContentHash()),
        // Časová osa a roztažení na výšku sem nepatří - jsou to parametry sestavy, ne vlastnosti
        // prvku, a cache se invaliduje změnou dat (DataContext), přes které parametry přicházejí.
        HashCode.Combine(DataRowHeight, AnotaceGap, OdjezdGap, GroupSeparatorWidth, HourColumnPerGroup,
            ShowSkupinaDnuHeader, SkupinaDnuId),
        GroupHeaderOverridesHash(),
        // Šířka skupin počítá se značkami a zmenšení s šířkou prvku (`B194a` C12).
        HashCode.Combine(Width, MinuteSpacing, ShowAnotace, AnotaceInterGap));

    /// <summary>Hash layout-affecting per-group nastavení (nadpis override, šířka override, pořadí) -
    /// mění šířku/wrap/pořadí, musí invalidovat layout cache (Count v hlavním fingerprintu to nezachytí).
    /// Order-sensitive (přeskládání skupin mění hash).</summary>
    private int GroupHeaderOverridesHash()
    {
        var h = new HashCode();
        foreach (var gc in GroupColumns)
        {
            h.Add(gc.SkupinaDnuId);
            h.Add(gc.HeaderOverride);
            h.Add(gc.WidthOverride);
        }
        return h.ToHashCode();
    }

    private GridLayout? ComputeLayout(DataContext? data, float mmToPx = 0f)
    {
        // Cache hit - stejná data A stejné layout-affecting properties
        int fp = ComputeLayoutFingerprint();
        if (_cachedLayout is not null && ReferenceEquals(data, _cachedLayoutDataRef) && fp == _cachedLayoutFingerprint)
            return _cachedLayout;

        var skupinyDnu = BuildSkupinyDnu(data);
        var odjezdyBySkupina = BuildOdjezdyBySkupina(data);

        var targetSkupiny = ResolveSkupiny(skupinyDnu);
        if (MaxGroupCount > 0 && targetSkupiny.Count > MaxGroupCount)
            targetSkupiny = targetSkupiny.Take(MaxGroupCount).ToList();
        if (targetSkupiny.Count == 0)
        {
            _cachedLayout = null;
            _cachedLayoutDataRef = data;
            return null;
        }

        // Override nadpisů skupin ze šablony (jednotně přes ColumnHeader.Resolve s ostatními
        // tabulkovými komponentami). Aplikuje se PŘED měřením šířky a wrapem záhlaví.
        targetSkupiny = ApplyHeaderOverrides(targetSkupiny);

        // Font pro měření textu záhlaví (default, přepíše se per-column)
        float effectiveMmToPx = mmToPx > 0 ? mmToPx : 3.78f;

        // Sloupec smí mít vlastní písmo záhlaví, takže se měří i počítá z jeho vyřešeného stylu,
        // ne z elementového - jinak by se rozešla šířka a výška s tím, co se nakreslí.
        var styleLookup = GroupColumnIndexResolver.BuildStyleLookup(
            GroupColumns, targetSkupiny.Select(s => s.Id).ToList());
        ResolvedTextStyle HeaderStyleForGroup(string id) =>
            ResolveGroupHeaderStyle(styleLookup.TryGetValue(id, out var cs) ? cs : null);
        ResolvedTextStyle MinuteStyleForGroup(string id) =>
            ResolveGroupMinuteStyle(styleLookup.TryGetValue(id, out var cs) ? cs : null);

        float defaultHeaderFontPt = HeaderStyle.FontSize ?? 9f;

        var groupLayouts = new List<GroupLayout>();
        foreach (var sk in targetSkupiny)
        {
            if (!odjezdyBySkupina.TryGetValue(sk.Id, out var byHour))
                byHour = new Dictionary<int, List<OdjezdData>>();

            string fullName = sk.Nadpis;
            if (sk.Nadpis2.Length > 0) fullName += " " + sk.Nadpis2;
            var words = HeaderWords(fullName);

            var skHeader = HeaderStyleForGroup(sk.Id);
            float fontPx = skHeader.FontSize * PtToMm * effectiveMmToPx;
            using var measureFont = new SKFont(
                // resolved rodina + řez (vč. dokumentového defaultu) - měření musí trefit stejný font jako render
                GetTypeface(skHeader), fontPx);

            // Minimální šířka = nejdelší slovo záhlaví + 2mm padding
            float maxWordW = 0;
            foreach (var word in words)
            {
                float w = MeasureTextMm(measureFont, word, effectiveMmToPx);
                if (w > maxWordW) maxWordW = w;
            }
            float minTextWidth = maxWordW + 2f;

            // HeaderLines se přepočítají po redistribuci šířek (zatím placeholder)
            groupLayouts.Add(new GroupLayout(sk, byHour, 0, 0, Array.Empty<HeaderLine>(), minTextWidth));
        }

        if (groupLayouts.All(g => g.ByHour.Count == 0))
        {
            _cachedLayout = null;
            _cachedLayoutDataRef = data;
            return null;
        }

        // Parametry z DataContext přepíší properties elementu
        var effectiveTimeAxis = ParseTimeAxisMode(GetStringParam(data, "CASOVA_OSA", DefaultTimeAxis.ToString().ToLowerInvariant()));
        int effectiveRangeFrom = GetIntParam(data, "HODINY_OD", DefaultRangeFrom);
        int effectiveRangeTo = GetIntParam(data, "HODINY_DO", DefaultRangeTo);

        var allHourKeys = groupLayouts.SelectMany(g => g.ByHour.Keys).ToHashSet();
        var hours = GetVisibleHoursFromSet(allHourKeys, effectiveTimeAxis, effectiveRangeFrom, effectiveRangeTo);

        // Pevná šířka skupiny (override) přebíjí auto výpočet; pinnuté se neúčastní přerozdělení.
        float? PinnedWidth(GroupLayout g) =>
            styleLookup.TryGetValue(g.Skupina.Id, out var cs) && cs.WidthOverride is float p && p > 0 ? p : null;

        // Skutečná šířka minut skupiny (se značkami a rozestupem) - týž výpočet jako kreslení.
        // Do 26. 10. se brala jen z počtu minut a skupina se značkami vyšla užší, než co se do ní
        // nakreslilo: minuty přetekly do sousední skupiny nebo se uřízly (`B194a` C12).
        var potrebneMinuty = PotrebneSirkyMinut(groupLayouts, hours, MinuteStyleForGroup, data, effectiveMmToPx);

        // Šířky skupin: pin (override), jinak max(minuty, min. pro text záhlaví)
        var finalGroups = new List<GroupLayout>();
        var potrebne = new List<float>();
        foreach (var g in groupLayouts)
        {
            int maxMin = GetMaxMinutesPerHour(g.ByHour, hours);
            if (maxMin < 1) maxMin = 1;
            float effectiveSlotW = MinuteWidth + AnotaceGap;
            float minuteBasedW = maxMin * effectiveSlotW + Math.Max(0, maxMin - 1) * OdjezdGap;
            float autoW = Math.Max(minuteBasedW, g.MinWidthForText);
            float groupW = PinnedWidth(g) ?? autoW;
            finalGroups.Add(g with { MaxMinutesPerHour = maxMin, GroupWidth = groupW });
            potrebne.Add(PinnedWidth(g) ?? Math.Max(potrebneMinuty[g.Skupina.Id], g.MinWidthForText));
        }

        // Redistribuce extra prostoru - jen auto (nepinnuté) skupiny zaplní zbytek šířky elementu
        float separatorTotal = Math.Max(0, finalGroups.Count - 1) * GroupSeparatorWidth;
        int hourColumnCount = HourColumnPerGroup ? finalGroups.Count : 1;
        float minTotal = hourColumnCount * HourColumnWidth + finalGroups.Sum(g => g.GroupWidth) + separatorTotal;

        if (minTotal < Width)
        {
            float extraSpace = Width - minTotal;
            float sumAutoW = finalGroups.Where(g => PinnedWidth(g) is null).Sum(g => g.GroupWidth);
            if (sumAutoW > 0)
            {
                finalGroups = finalGroups.Select(g =>
                    PinnedWidth(g) is not null
                        ? g
                        : g with { GroupWidth = g.GroupWidth + extraSpace * (g.GroupWidth / sumAutoW) }
                ).ToList();
            }
        }

        // Skupina užší, než kolik potřebují její minuty: když se celek do prvku vejde, dorovná se
        // na úkor skupin s rezervou a ostatní šířky zůstanou jako dosud; když ne, zmenší se celá
        // mřížka (písmo i šířky) jako při přetečení na výšku.
        float hodiny = hourColumnCount * HourColumnWidth;
        float potrebaCelkem = hodiny + potrebne.Sum() + separatorTotal;
        float meritkoSirky = 1f;
        if (potrebaCelkem <= Width + 0.01f)
            finalGroups = DorovnejNaPotrebu(finalGroups, potrebne, PinnedWidth);
        else
        {
            meritkoSirky = MathF.Max(MinMeritkoSirky, (Width - separatorTotal) / (potrebaCelkem - separatorTotal));
            finalGroups = [.. finalGroups.Select((g, i) => g with { GroupWidth = potrebne[i] * meritkoSirky })];
        }
        bool nevejdeSe = hodiny * meritkoSirky + finalGroups.Sum(g => g.GroupWidth) + separatorTotal > Width + 0.01f;

        // Layout řádků záhlaví:
        // - Ruční zlom v nadpisu (Enter nebo token) → řádky ve STEJNÉM stylu
        // - Nadpis2 z dat → navíc sekundární řádek s vlastním stylem (HeaderLine2Style)
        // - Bez zlomu a bez Nadpis2 → word-wrap podle šířky (dosavadní chování)
        finalGroups = finalGroups.Select(g =>
        {
            var skHeader = HeaderStyleForGroup(g.Skupina.Id);
            // Zalamuje se písmem, jakým se nakreslí - po zmenšení na šířku je menší.
            float fontPx = skHeader.FontSize * PtToMm * effectiveMmToPx * meritkoSirky;
            using var wrapFont = new SKFont(
                // resolved rodina + řez (vč. dokumentového defaultu) - wrap musí trefit stejný font jako render
                GetTypeface(skHeader), fontPx);
            var skHeader2 = HeaderLine2Style is { } h2 ? h2.Resolve(skHeader) : skHeader;
            using var wrapFont2 = new SKFont(GetTypeface(skHeader2), skHeader2.FontSize * PtToMm * effectiveMmToPx * meritkoSirky);

            return g with
            {
                HeaderLines = ComposeHeaderLines(g.Skupina.Nadpis, g.Skupina.Nadpis2,
                    radek => WrapHeaderText(radek, g.GroupWidth, wrapFont, effectiveMmToPx),
                    radek => WrapHeaderText(radek, g.GroupWidth, wrapFont2, effectiveMmToPx)),
            };
        }).ToList();

        // Datový řádek nese hodinu (písmo prvku) i minuty, jejichž písmo smí přebít skupina dnů
        // v dialogu skupin - výška proto musí vyjít z toho většího z nich, stejně jako u pruhu
        // záhlaví níž. Z písma prvku samotného se větší číslice kreslily do nízkého řádku.
        float effFontSize = MathF.Max(
            ResolveHourDataStyle().FontSize,
            finalGroups.Count == 0
                ? ResolveGroupMinuteStyle(null).FontSize
                : finalGroups.Max(g => MinuteStyleForGroup(g.Skupina.Id).FontSize));
        // Pruh záhlaví je jeden pro celou mřížku, takže jeho výšku určuje největší písmo záhlaví
        // napříč sloupci (týž vzor jako u seznamu zastávek) - jinak by se větší písmo oříznulo.
        float effHeaderFontSize = MathF.Max(
            ResolveHourHeaderStyle().FontSize,
            finalGroups.Count == 0
                ? defaultHeaderFontPt
                : finalGroups.Max(g => HeaderStyleForGroup(g.Skupina.Id).FontSize));
        float fontSizeMm = effFontSize * PtToMm;
        float headerFontMm = effHeaderFontSize * PtToMm;
        float minuteFontMm = MinuteFontSize * PtToMm;
        float rowH = DataRowHeight ?? ComputeRowH(effFontSize);
        float headerH = ComputeHeaderH(effHeaderFontSize);

        // Výška řádku záhlaví - Line 1 (element font), Line 2 (HeaderLine2Style, element-level).
        // Per-group celková výška = součet výšek jeho řádků (různé výšky pro Line 1 a Line 2).
        float maxGroupBlockH = 0f;
        foreach (var g in finalGroups)
        {
            // Výška řádku se počítá z písma TOHO sloupce; pruh pak dostane výšku nejvyššího z nich.
            float groupFontPt = HeaderStyleForGroup(g.Skupina.Id).FontSize;
            float line1LineH = Math.Max(SkupinaDnuHeaderLineHeight, groupFontPt * PtToMm * 1.5f);
            float line2FontPt = HeaderLine2Style?.FontSize ?? groupFontPt;
            float line2LineH = Math.Max(SkupinaDnuHeaderLineHeight, line2FontPt * PtToMm * 1.5f);

            float blockH = 0f;
            foreach (var hl in g.HeaderLines)
                blockH += hl.IsSecondary ? line2LineH : line1LineH;
            if (blockH > maxGroupBlockH) maxGroupBlockH = blockH;
        }

        int maxHeaderLines = finalGroups.Count == 0 ? 0 : finalGroups.Max(g => g.HeaderLines.Length);
        float groupHeaderH = ShowSkupinaDnuHeader
            ? maxGroupBlockH
            : 0f;

        var result = new GridLayout(finalGroups, hours, fontSizeMm, headerFontMm, minuteFontMm,
            rowH, headerH, groupHeaderH, maxHeaderLines, HourColumnWidth * meritkoSirky, meritkoSirky, nevejdeSe,
            [.. potrebne.Select(p => p * meritkoSirky)]);

        _cachedLayout = result;
        _cachedLayoutDataRef = data;
        _cachedLayoutFingerprint = fp;
        return result;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Style resolution (kaskáda column → element → CellDefaults)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Parent kaskády pro per-column override záhlaví sloupce hodin.</summary>
    public ResolvedTextStyle ResolveHourHeaderParent()
    {
        var p = HeaderStyle.Resolve(ScheduleHeaderDefaults);
        if (HeaderStyle.VAlign is null)
            p = p with { VAlign = HeaderVerticalAlignment };
        return p;
    }

    /// <summary>Parent kaskády pro per-column override datové buňky sloupce hodin.</summary>
    public ResolvedTextStyle ResolveHourDataParent() => DataStyle.Resolve(ScheduleDataDefaults);

    /// <summary>Styl textových anotací (písmena, dny). Scalar <see cref="AnotaceFontSize"/> je
    /// poslední fallback - platí jen když velikost nenastavila žádná vrstva kaskády.</summary>
    internal ResolvedTextStyle ResolveAnotaceTextStyle(ResolvedTextStyle dataBase)
    {
        var s = AnotaceTextStyle.Resolve(dataBase);
        if (AnotaceTextStyle.FontSize is null && DataStyle.FontSize is null)
            s = s with { FontSize = AnotaceFontSize };
        return s;
    }

    /// <summary>Parent kaskády pro per-column override záhlaví skupiny dnů.</summary>
    public ResolvedTextStyle ResolveGroupHeaderParent()
    {
        var p = HeaderStyle.Resolve(ScheduleHeaderDefaults);

        // Legacy fallback: pokud user nenastavil FontSize v HeaderStyle, odvodit z SkupinaDnuHeaderLineHeight.
        // (Priorita: HeaderStyle.FontSize > SkupinaDnuHeaderLineHeight-derived > ScheduleHeaderDefaults.FontSize)
        if (HeaderStyle.FontSize is null)
        {
            float legacyDefaultPt = SkupinaDnuHeaderLineHeight * 0.65f / PtToMm;
            p = p with { FontSize = legacyDefaultPt };
        }
        if (HeaderStyle.VAlign is null)
            p = p with { VAlign = HeaderVerticalAlignment };
        if (HeaderStyle.HAlign is null)
            p = p with { HAlign = TextHorizontalAlignment.Center };
        return p;
    }

    /// <summary>
    /// Z čeho dědí minutová zóna: datový styl prvku, na něm skalární fallback velikosti a tučnosti
    /// (`MinuteFontSize`/`MinuteBold` platí jen když je nenastavil `DataStyle` - jinak by přebíjely
    /// uživatelovu volbu z tabu Obecné). Vlastní vrstva zóny (`MinuteStyle`) se pak resolvuje nad tímhle.
    /// </summary>
    public ResolvedTextStyle ResolveElementMinuteParent()
    {
        var p = DataStyle.Resolve(ScheduleDataDefaults);
        if (DataStyle.FontSize is null) p = p with { FontSize = MinuteFontSize };
        if (DataStyle.Bold is null)     p = p with { Bold = MinuteBold };
        return p;
    }

    /// <summary>Z čeho dědí symboly anotací = datový styl prvku (velikost si zóna dopočítá
    /// z minutové, viz <see cref="Render"/>).</summary>
    public ResolvedTextStyle ResolveElementAnotaceSymbolParent() => DataStyle.Resolve(ScheduleDataDefaults);

    /// <summary>Z čeho dědí text anotací: datový styl prvku + skalární fallback velikosti
    /// (`AnotaceFontSize` platí jen když velikost nenastavil `DataStyle`).</summary>
    public ResolvedTextStyle ResolveElementAnotaceTextParent()
    {
        var p = DataStyle.Resolve(ScheduleDataDefaults);
        if (DataStyle.FontSize is null) p = p with { FontSize = AnotaceFontSize };
        return p;
    }

    /// <summary>Z čeho dědí zóna, kterou otevírá tlačítko „Upravit styl…" formuláře (klíč <c>style:&lt;vlastnost&gt;</c>);
    /// null = prvek takovou zónu nemá a tlačítko by nic neupravilo.</summary>
    public ResolvedTextStyle? ResolveStyleParent(string property) => property switch
    {
        nameof(HeaderStyle)      => ResolveElementHeaderParent(),
        nameof(HeaderLine2Style) => ResolveElementHeaderParent(),   // 2. řádek dědí ze záhlaví
        nameof(DataStyle)        => ResolveElementDataParent(),
        nameof(MinuteStyle)      => ResolveGroupMinuteParent(),
        _                        => null
    };

    /// <summary>Parent kaskády pro per-column override minutové buňky skupiny.</summary>
    public ResolvedTextStyle ResolveGroupMinuteParent()
    {
        var p = MinuteStyle.Resolve(ResolveElementMinuteParent());

        // Minutové buňky: padding default 0 (hustá mřížka)
        p = p with
        {
            PaddingLeft   = MinuteStyle.PaddingLeft   ?? 0f,
            PaddingRight  = MinuteStyle.PaddingRight  ?? 0f,
            PaddingTop    = MinuteStyle.PaddingTop    ?? 0f,
            PaddingBottom = MinuteStyle.PaddingBottom ?? 0f,
        };
        return p;
    }

    /// <summary>Vyřeší styl záhlaví sloupce hodin.</summary>
    private ResolvedTextStyle ResolveHourHeaderStyle()
    {
        var parent = ResolveHourHeaderParent();
        return HourColumnStyle?.Header is { } h ? h.Resolve(parent) : parent;
    }

    /// <summary>Vyřeší styl datové buňky sloupce hodin.</summary>
    private ResolvedTextStyle ResolveHourDataStyle()
    {
        var parent = ResolveHourDataParent();
        return HourColumnStyle?.Data is { } d ? d.Resolve(parent) : parent;
    }

    /// <summary>Vyřeší styl záhlaví skupiny dnů (celá kaskáda včetně per-sloupcové typografie).</summary>
    private ResolvedTextStyle ResolveGroupHeaderStyle(DepartureGroupColumnStyle? cs)
    {
        var parent = ResolveGroupHeaderParent();
        return cs?.Style.Header is { } h ? h.Resolve(parent) : parent;
    }

    /// <summary>`B145`: pozadí **těla** sloupce hodin, když se opakuje u každé skupiny. Bere se
    /// barva **záhlaví** té skupiny, ať pruh hodin drží s barevným nadpisem a minuty pod ním můžou
    /// zůstat bílé (doložený vzor: vývěska Nitra linka 4). Bez podbarveného záhlaví se nic nemění -
    /// spadne to na pozadí minut jako dosud, takže průhledná buňka nechá projít zebru.</summary>
    internal static string HourBodyBackground(ResolvedTextStyle header, ResolvedTextStyle minute) =>
        ColorHex.ParseOrTransparent(header.BackgroundColor).Alpha > 0
            ? header.BackgroundColor
            : minute.BackgroundColor;

    /// <summary>Vyřeší styl minutové buňky (per-skupina).</summary>
    private ResolvedTextStyle ResolveGroupMinuteStyle(DepartureGroupColumnStyle? cs)
    {
        var parent = ResolveGroupMinuteParent();
        return cs?.Style.Data is { } d ? d.Resolve(parent) : parent;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Render
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Jednoznakový speciální symbol (piktogram) → kreslí se symbolovým JŘ fontem.
    /// Víceznakové SpecialniZnak (dny „Po"…) i NZ (čísla) zůstávají v normálním fontu.</summary>
    private static bool JeGlyfSymbol(AnotaceData a) =>
        a.Typ == TypAnotace.SpecialniZnak && JePiktogram(a.Znak);

    /// <summary>Znak, který se u odjezdu skutečně nakreslí. U piktogramů se sémantická značka **musí**
    /// přeložit na glyf (kola „K" → `L`) - bez toho vyjede cizí piktogram, kolo se kreslilo jako glyf
    /// na pozici „K". Překlad jde přes kategorii, ne přes písmeno: značky MHD jsou v datech už jako
    /// pozice ve fontu a přeložit se nesmí. Používat i při **měření** šířky slotu, jinak se rozejde
    /// s vykreslením.</summary>
    internal static string GlyfProKresleni(AnotaceData a) =>
        JeGlyfSymbol(a) ? GlyfDleKategorie(a.Znak, a.Kategorie) : a.Znak;

    public override void Render(RenderContext ctx)
    {
        if (Width <= 0) return;

        RenderGrid(ctx);
    }

    private void RenderGrid(RenderContext ctx)
    {
        var layout = ComputeLayout(ctx.Data, ctx.MmToPx);
        if (layout is null)
        {
            // Placeholder se ukazuje jen v designeru. Print s prázdnými daty nesmí
            // vykreslovat fake mřížku.
            if (ctx.Data is null) RenderPlaceholder(ctx);
            return;
        }

        // Auto-scale: pokud Height je pevné a obsah přetéká, zmenšit vše proporcionálně
        // Záhlaví = jeden sloučený řádek (Hod + názvy skupin)
        float combinedHeaderH = ShowSkupinaDnuHeader
            ? Math.Max(layout.GroupHeaderH, layout.HeaderH)
            : layout.HeaderH;
        // B2: přirozená výška ze skutečné výšky řádku (vč. explicitní DataRowHeight) - shodně
        // s CalculateHeightFromData/ContentOverflows, aby varování a auto-scale nepočítaly jinak.
        // Auto-shrink do fit je konzistentní pattern všech JŘ elementů → velká DataRowHeight se
        // zmenší jako vše ostatní (dřív se tu z ní naopak nezmenšovaly fonty → obsah tiše přetekl).
        float scaleNaturalH = combinedHeaderH + layout.Hours.Count * layout.RowH;
        // Na šířku se zmenšuje stejně jako na výšku - platí přísnější z obou (`B194a` C12).
        float scale = MathF.Min(ComputeFontScale(scaleNaturalH), layout.MeritkoSirky);
        float hourW = layout.SirkaSloupceHodin;

        // Roztáhnout na výšku - pokud obsah je kratší než Height a feature je zapnutá,
        // proporcionálně zvětšit data řádky + jejich fonty (header zůstává).
        // Caché parametr ROZTAHNOUT_NA_VYSKU může override z PrintDialogu.
        bool effectiveAutoExpand = GetBoolParam(ctx.Data, "ROZTAHNOUT_NA_VYSKU", DefaultAutoExpand);
        float dataGrow = 1f;
        if (effectiveAutoExpand && Height.HasValue && scale >= 1f && layout.Hours.Count > 0)
        {
            float scaledCombinedH0 = combinedHeaderH;   // header při scale=1
            float dataNaturalH = layout.Hours.Count * layout.RowH;
            float availableDataH = Height.Value - scaledCombinedH0;
            if (dataNaturalH > 0 && availableDataH > dataNaturalH)
                dataGrow = availableDataH / dataNaturalH;
            // Cap - řídká mřížka (např. jen 1 hodina s odjezdy) by se jinak roztáhla bizarně velká.
            // Vyplníme jen rozumně; zbytek místa zůstane prázdný místo obřích řádků.
            if (dataGrow > MaxAutoExpandFactor) dataGrow = MaxAutoExpandFactor;
        }
        float dataScale = scale * dataGrow;

        // B2/TR4: výška řádku škáluje konzistentně (shrink i grow) - i explicitní DataRowHeight se zmenší,
        // když se obsah nevejde. Škáluje se výška z layoutu (ta už počítá s písmem skupin), ne druhý
        // výpočet z písma prvku - dva zdroje se rozešly a číslice přetekly z řádku.
        float scaledRowH = layout.RowH * dataScale;
        float scaledGroupHeaderH = layout.GroupHeaderH * scale;
        float scaledCombinedHeaderH = combinedHeaderH * scale;

        // Clip na hranice elementu
        float clipH = scaledCombinedHeaderH + layout.Hours.Count * scaledRowH;
        var clipRect = ctx.ToCanvasRect(X, Y, Width, clipH);
        ctx.Canvas.Save();
        ctx.Canvas.ClipRect(clipRect);

        using var paints = CreateStandardPaints(ctx);

        // ── Style lookup - jednou na frame, resolves __INDEX_ keys ──────────
        var layoutGroupIds = layout.Groups.Select(g => g.Skupina.Id).ToList();
        var colStyles = GroupColumnIndexResolver.BuildStyleLookup(GroupColumns, layoutGroupIds);

        // ── Kaskáda stylů (column → element → CellDefaults) ─────────────────
        var hourHeaderStyle = ResolveHourHeaderStyle();
        var hourDataStyle = ResolveHourDataStyle();

        var groupHeaderStyles = new Dictionary<string, ResolvedTextStyle>();
        var groupMinuteStyles = new Dictionary<string, ResolvedTextStyle>();
        foreach (var g in layout.Groups)
        {
            colStyles.TryGetValue(g.Skupina.Id, out var cs);
            groupHeaderStyles[g.Skupina.Id] = ResolveGroupHeaderStyle(cs);
            groupMinuteStyles[g.Skupina.Id] = ResolveGroupMinuteStyle(cs);
        }

        // Anotace - element-level (nedědí z column.Data)
        var stylyZnacek = StylyZnacek(ctx.Data);
        var anotSymStyle = stylyZnacek.Sym;
        var anotTextStyle = stylyZnacek.Text;

        // ── Deklarace zdrojů (perf: 100+ buněk/frame) - plnění až uvnitř try ────
        // Deklarace musí být před try, aby na ně viděl finally i lokální helpery níže;
        // samotná alokace fontů/paintů je uvnitř try, aby finally uvolnil i částečně vytvořené
        // zdroje + clip.
        var groupHeaderFonts       = new Dictionary<string, SKFont>();
        var groupHeaderTextPaints  = new Dictionary<string, SKPaint>();
        var groupHeaderBgPaints    = new Dictionary<string, SKPaint?>();
        var groupMinuteFonts       = new Dictionary<string, SKFont>();
        var groupMinuteTextPaints  = new Dictionary<string, SKPaint>();
        var groupMinuteBgPaints    = new Dictionary<string, SKPaint?>();

        // Line 2 záhlaví - vytváří se pouze pokud HeaderLine2Style != null (jinak Line 2 sdílí Line 1).
        bool hasLine2Override = HeaderLine2Style is not null;
        var groupHeaderStyles2     = new Dictionary<string, ResolvedTextStyle>();
        var groupHeaderFonts2      = new Dictionary<string, SKFont>();
        var groupHeaderTextPaints2 = new Dictionary<string, SKPaint>();

        // Anotace fonty/painty - element-level, sdílené pro všechny buňky (nullable = alokace v try).
        // Fonty a šířky odjezdů drží sdílený výpočet (týž jako rozvržení), painty zvlášť.
        SlotyOdjezdu? sloty = null;
        SKFont?  anotSymFont   = null;
        SKFont?  anotGlyphFont = null;
        SKPaint? anotSymPaint  = null;
        SKFont?  anotTextFont  = null;
        SKPaint? anotTextPaint = null;

        // Zebra - globální + per-group override (alokace uvnitř try).
        SKPaint? zebraPaint = null;
        var perGroupZebraPaints = new Dictionary<string, SKPaint>();
        bool hasPerGroupZebra = false;

        float anotGapPx      = AnotaceGap * dataScale * ctx.MmToPx;
        float anotInterGapPx = AnotaceInterGap * dataScale * ctx.MmToPx;
        float scaledOdjezdGap = OdjezdGap * dataScale; // v mm (model space), ne v px
        float usedWidth = Width;
        float currentY = Y;

        try
        {
            foreach (var g in layout.Groups)
            {
                var id = g.Skupina.Id;
                var hs = groupHeaderStyles[id];
                var ms = groupMinuteStyles[id];

                // Header fonty: použít base scale (růst se neaplikuje na header).
                groupHeaderFonts[id]      = hs.CreateFont(scale, ctx.MmToPx);
                groupHeaderTextPaints[id] = hs.CreateTextPaint();
                groupHeaderBgPaints[id]   = hs.CreateBgPaint();

                if (hasLine2Override)
                {
                    var hs2 = HeaderLine2Style!.Resolve(hs);
                    groupHeaderStyles2[id]     = hs2;
                    groupHeaderFonts2[id]      = hs2.CreateFont(scale, ctx.MmToPx);
                    groupHeaderTextPaints2[id] = hs2.CreateTextPaint();
                }

                // Minutové fonty: použít dataScale (růst se aplikuje, header ne).
                groupMinuteFonts[id]      = ms.CreateFont(dataScale, ctx.MmToPx);
                groupMinuteTextPaints[id] = ms.CreateTextPaint();
                groupMinuteBgPaints[id]   = ms.CreateBgPaint();
            }

            // Anotace patří k datovým buňkám → dataScale. Rozestup (rovnoměrný / pevný rastr)
            // a zmenšení značek v pevném rastru počítá sdílený výpočet, týž jako rozvržení.
            sloty = PripravSloty(layout.Groups, groupMinuteFonts.Values, stylyZnacek, dataScale, ctx.MmToPx);
            anotSymFont   = sloty.Sym;
            anotGlyphFont = sloty.Glyph;
            anotTextFont  = sloty.Text;
            anotSymPaint  = anotSymStyle.CreateTextPaint();
            anotTextPaint = anotTextStyle.CreateTextPaint();

            if (AlternatingRowColor is not null)
                zebraPaint = new SKPaint { Color = ColorHex.ParseOrTransparent(AlternatingRowColor) };

            foreach (var g in layout.Groups)
            {
                if (colStyles.TryGetValue(g.Skupina.Id, out var cs) && cs.AlternatingRowColor is not null)
                    perGroupZebraPaints[g.Skupina.Id] = new SKPaint { Color = ColorHex.ParseOrTransparent(cs.AlternatingRowColor) };
            }
            hasPerGroupZebra = perGroupZebraPaints.Count > 0;

            RenderGridHeader();
            RenderGridRows();
            RenderGridVerticalSeparators();
            // Outer border je řízen TemplateElement.BorderColor/BorderWidth (druhý průchod).
        }
        finally
        {
            foreach (var f in groupHeaderFonts.Values)      f.Dispose();
            foreach (var p in groupHeaderTextPaints.Values) p.Dispose();
            foreach (var p in groupHeaderBgPaints.Values)   p?.Dispose();
            foreach (var f in groupHeaderFonts2.Values)     f.Dispose();
            foreach (var p in groupHeaderTextPaints2.Values) p.Dispose();
            foreach (var f in groupMinuteFonts.Values)      f.Dispose();
            foreach (var p in groupMinuteTextPaints.Values) p.Dispose();
            foreach (var p in groupMinuteBgPaints.Values)   p?.Dispose();
            sloty?.Dispose();   anotSymPaint?.Dispose();   anotTextPaint?.Dispose();
            zebraPaint?.Dispose();
            foreach (var p in perGroupZebraPaints.Values)   p.Dispose();

            ctx.Canvas.Restore(); // Uvolnit clip rect
        }

        // ─── Local helpers (capture outer state) ─────────────────────────────

        // Nakreslí "Hod" záhlaví (celá kombinovaná výška) přes CellRenderer.
        // bgColorOverride: HourColumnPerGroup přebírá barvu pozadí skupiny.
        void DrawHourHeaderCell(float hx, string? bgColorOverride = null)
        {
            var style = bgColorOverride is not null
                ? hourHeaderStyle with { BackgroundColor = bgColorOverride }
                : hourHeaderStyle;
            var rect = new SKRect(hx, currentY, hx + hourW, currentY + scaledCombinedHeaderH);
            var ikona = _hourHeaderImage.Get(HourColumnHeaderImageBase64);
            // obrázek nahrazuje nadpis - dvě věci v jedné buňce by se překryly
            CellRenderer.DrawCell(ctx, rect, ikona is null ? Rendering.ColumnHeader.Resolve(HourColumnHeader, "Hod") : "", style, scale);
            if (ikona is not null) DrawHourHeaderIcon(ctx, ikona, rect);
        }

        // Nakreslí datovou buňku sloupce hodin přes CellRenderer.
        // Hour je „service hour" (raw z dat, 0..47). Header zobrazuje clock hour = H % 24 -
        // night extension (24..) se uživateli ukáže jako 0..3 podle hodin zobrazované doby.
        // Datová buňka → dataScale (růst při roztažení na výšku).
        void DrawHourDataCell(float hx, int hour, string? bgColorOverride = null)
        {
            var style = bgColorOverride is not null
                ? hourDataStyle with { BackgroundColor = bgColorOverride }
                : hourDataStyle;
            var rect = new SKRect(hx, currentY, hx + hourW, currentY + scaledRowH);
            CellRenderer.DrawCell(ctx, rect, (hour % 24).ToString(), style, dataScale);
        }

        // ── Sloučené záhlaví (Hod + názvy skupin dnů) ────────────────────────
        void RenderGridHeader()
        {
            // Base BG přes celou šířku - pokryje mezery mezi skupinami (GroupSeparatorWidth).
            var headerRect = ctx.ToCanvasRect(X, currentY, usedWidth, scaledCombinedHeaderH);
            ctx.Canvas.DrawRect(headerRect, paints.HeaderBg);

            if (!HourColumnPerGroup)
                DrawHourHeaderCell(X);

            if (ShowSkupinaDnuHeader && scaledGroupHeaderH > 0)
            {
                float gx = HourColumnPerGroup ? X : X + hourW;
                foreach (var g in layout.Groups)
                {
                    var gid = g.Skupina.Id;
                    var hs = groupHeaderStyles[gid];
                    var hFont = groupHeaderFonts[gid];
                    var hTextPaint = groupHeaderTextPaints[gid];
                    var hBgPaint = groupHeaderBgPaints[gid];

                    // `B145`: barvu skupiny přebírá jen TĚLO sloupce hodin, ne tahle buňka nad ním -
                    // sedí v ní hodinová ikona a zákazník ji chce na neutrálním podkladu
                    // (vývěska Nitra linka 4). Drží si tedy styl hodinového sloupce z tabu Obecné.
                    if (HourColumnPerGroup)
                    {
                        DrawHourHeaderCell(gx);
                        gx += hourW;
                    }

                    // BG skupiny
                    if (hBgPaint is not null)
                    {
                        var bgRect = ctx.ToCanvasRect(gx, currentY, g.GroupWidth, scaledCombinedHeaderH);
                        ctx.Canvas.DrawRect(bgRect, hBgPaint);
                    }

                    // Per-line font/paint: Line 1 = HeaderStyle (hs), Line 2 (IsSecondary) = HeaderLine2Style cascade.
                    // Pokud HeaderLine2Style == null, IsSecondary řádky sdílí Line 1 styl (žádná regrese).
                    ResolvedTextStyle hs2 = hasLine2Override ? groupHeaderStyles2[gid] : hs;
                    SKFont hFont2 = hasLine2Override ? groupHeaderFonts2[gid] : hFont;
                    SKPaint hTextPaint2 = hasLine2Override ? groupHeaderTextPaints2[gid] : hTextPaint;

                    // Per-line výška řádku - Line 1 z hs, Line 2 z hs2 (pokud override).
                    float line1H = hs.FontSize  * PtToMm * 1.5f * scale;
                    float line2H = hs2.FontSize * PtToMm * 1.5f * scale;

                    int lineCount = g.HeaderLines.Length;
                    float blockH = 0f;
                    foreach (var hl in g.HeaderLines) blockH += hl.IsSecondary ? line2H : line1H;

                    float hPadTop    = hs.PaddingTop    * scale;
                    float hPadBottom = hs.PaddingBottom * scale;
                    float hPadLeft   = hs.PaddingLeft   * scale;
                    float hPadRight  = hs.PaddingRight  * scale;

                    float startLineY = hs.VAlign switch
                    {
                        TextVerticalAlignment.Top    => currentY + hPadTop,
                        TextVerticalAlignment.Middle => currentY + (scaledCombinedHeaderH - blockH) / 2f,
                        _ /* Bottom */               => currentY + scaledCombinedHeaderH - blockH - hPadBottom
                    };

                    float runningY = startLineY;
                    for (int i = 0; i < lineCount; i++)
                    {
                        var hl = g.HeaderLines[i];
                        var lineStyle = hl.IsSecondary ? hs2 : hs;
                        var lineFont  = hl.IsSecondary ? hFont2 : hFont;
                        var linePaint = hl.IsSecondary ? hTextPaint2 : hTextPaint;
                        float lineH   = hl.IsSecondary ? line2H : line1H;

                        float baselineY = runningY + lineH * 0.8f;
                        float textW = MeasureTextMm(lineFont, hl.Text, ctx.MmToPx);
                        float textX = lineStyle.HAlign switch
                        {
                            TextHorizontalAlignment.Left  => gx + hPadLeft + 1f,
                            TextHorizontalAlignment.Right => gx + g.GroupWidth - textW - hPadRight - 1f,
                            _ /* Center */                => gx + hPadLeft + (g.GroupWidth - hPadLeft - hPadRight - textW) / 2f
                        };
                        var pt = ctx.ToCanvas(textX, baselineY);
                        ctx.Canvas.DrawText(hl.Text, pt.X, pt.Y, lineFont, linePaint);
                        lineStyle.DrawTextDecorations(ctx.Canvas, pt.X, pt.Y, textW * ctx.MmToPx, lineFont, linePaint);

                        runningY += lineH;
                    }

                    gx += g.GroupWidth + GroupSeparatorWidth;
                }
            }
            else if (HourColumnPerGroup)
            {
                // Bez záhlaví skupin, ale stále kreslit hour headers per group
                float gx = X;
                foreach (var g in layout.Groups)
                {
                    DrawHourHeaderCell(gx);
                    gx += hourW + g.GroupWidth + GroupSeparatorWidth;
                }
            }

            currentY += scaledCombinedHeaderH;
        }

        // ── Řádky hodin (zebra + hour buňky + minuty + anotace + h-čáry) ─────
        void RenderGridRows()
        {
            int rowIndex = 0;
            foreach (int hour in layout.Hours)
            {
                DrawRowZebra(rowIndex);

                if (!HourColumnPerGroup)
                    DrawHourDataCell(X, hour);

                float gx = HourColumnPerGroup ? X : X + hourW;
                foreach (var g in layout.Groups)
                {
                    var gid = g.Skupina.Id;
                    var ms = groupMinuteStyles[gid];
                    var mBgPaint = groupMinuteBgPaints[gid];

                    if (HourColumnPerGroup)
                    {
                        DrawHourDataCell(gx, hour, HourBodyBackground(groupHeaderStyles[gid], ms));
                        gx += hourW;
                    }

                    // BG datové buňky skupiny (null = průhledná, ponechá zebra)
                    if (mBgPaint is not null)
                    {
                        var bgRect = ctx.ToCanvasRect(gx, currentY, g.GroupWidth, scaledRowH);
                        ctx.Canvas.DrawRect(bgRect, mBgPaint);
                    }

                    if (g.ByHour.TryGetValue(hour, out var odjezdy))
                        DrawMinuteCell(g, gx, odjezdy);

                    gx += g.GroupWidth + GroupSeparatorWidth;
                }

                // „Čáry mezi řádky" = čáry mezi hodinami; v pivotu je řádek právě hodina.
                if (Grid.ShowRowLines)
                {
                    var rowLineStart = ctx.ToCanvas(X, currentY + scaledRowH);
                    var rowLineEnd = ctx.ToCanvas(X + usedWidth, currentY + scaledRowH);
                    ctx.Canvas.DrawLine(rowLineStart, rowLineEnd, paints.Grid);
                }
                currentY += scaledRowH;
                rowIndex++;
            }
        }

        // Zebra podbarvení sudých řádků - globální nebo per-group
        void DrawRowZebra(int rowIndex)
        {
            if (rowIndex % 2 != 1) return;

            if (hasPerGroupZebra)
            {
                float zx = HourColumnPerGroup ? X : X + hourW;

                // Sdílený hour column (pokud není per-group)
                if (!HourColumnPerGroup && zebraPaint is not null)
                {
                    var r = ctx.ToCanvasRect(X, currentY, hourW, scaledRowH);
                    ctx.Canvas.DrawRect(r, zebraPaint);
                }

                foreach (var g in layout.Groups)
                {
                    var gZebra = perGroupZebraPaints.TryGetValue(g.Skupina.Id, out var gzp)
                        ? gzp : zebraPaint;
                    if (gZebra is not null)
                    {
                        if (HourColumnPerGroup)
                        {
                            var hr = ctx.ToCanvasRect(zx, currentY, hourW, scaledRowH);
                            ctx.Canvas.DrawRect(hr, gZebra);
                            zx += hourW;
                        }
                        var dr = ctx.ToCanvasRect(zx, currentY, g.GroupWidth, scaledRowH);
                        ctx.Canvas.DrawRect(dr, gZebra);
                    }
                    else if (HourColumnPerGroup)
                        zx += hourW;

                    zx += g.GroupWidth + GroupSeparatorWidth;
                }
            }
            else if (zebraPaint is not null)
            {
                // Globální zebra fallback - celá šířka
                var zebraRect = ctx.ToCanvasRect(X, currentY, usedWidth, scaledRowH);
                ctx.Canvas.DrawRect(zebraRect, zebraPaint);
            }
        }

        // Nakreslí minuty + anotace jedné buňky (jedna skupina × jedna hodina)
        void DrawMinuteCell(GroupLayout g, float gx, List<OdjezdData> odjezdy)
        {
            var gid = g.Skupina.Id;
            var ms = groupMinuteStyles[gid];
            var mFont = groupMinuteFonts[gid];
            var mTextPaint = groupMinuteTextPaints[gid];

            // Padding minutové buňky - dataScale (s růstem AutoExpandu).
            float padL = ms.PaddingLeft * dataScale;
            float padR = ms.PaddingRight * dataScale;
            float padT = ms.PaddingTop * dataScale;

            float availW = g.GroupWidth - padL - padR;

            var slotW = SirkySlotu(sloty!, gid, odjezdy);
            float blockW = slotW.Sum() + Math.Max(0, odjezdy.Count - 1) * scaledOdjezdGap;
            float alignOffset = padL + ms.HAlign switch
            {
                TextHorizontalAlignment.Center => (availW - blockW) / 2f,
                TextHorizontalAlignment.Right  => availW - blockW - 0.5f,
                _ => 0f
            };
            if (alignOffset < padL) alignOffset = padL;

            float minX = gx + alignOffset;
            SKPaint? customMinutePaint = null;
            SKPaint? customAnotPaint = null;
            SKPaint? nzBgPaint = null;
            SKPaint? nzFgPaint = null;
            try
            {
                for (int oi = 0; oi < odjezdy.Count; oi++)
                {
                    var odj = odjezdy[oi];
                    var minuteText = odj.Minuta.ToString("D2");

                    SKPaint minutePaint;
                    if (odj.ForeColor is not null)
                    {
                        customMinutePaint ??= new SKPaint { IsAntialias = true };
                        customMinutePaint.Color = ColorHex.ParseOrBlack(odj.ForeColor);
                        minutePaint = customMinutePaint;
                    }
                    else
                    {
                        minutePaint = mTextPaint;
                    }

                    var minPt = ctx.ToCanvas(minX + 0.5f, currentY + padT + scaledRowH * TextBaselineFactor);
                    ctx.Canvas.DrawText(minuteText, minPt.X, minPt.Y, mFont, minutePaint);

                    // Anotace - inline za minutou
                    if (ShowAnotace && odj.Anotace.Count > 0)
                    {
                        float minuteTextW = mFont.MeasureText(minuteText);
                        float anotX = minPt.X + minuteTextW + anotGapPx;

                        foreach (var anot in odj.Anotace)
                        {
                            SKFont aFont = JeGlyfSymbol(anot) ? anotGlyphFont! : (anot.Typ == TypAnotace.Text ? anotTextFont! : anotSymFont!);
                            string anotZnak = GlyfProKresleni(anot);

                            // Negativní značka invertovaně - černý box + bílý znak (jako legacy). Vždy zapnuto.
                            if (anot.Typ == TypAnotace.NegativniZnacka)
                            {
                                nzBgPaint ??= new SKPaint { Color = ColorHex.ParseOrBlack(NegativniZnackaBackColor), IsAntialias = true };
                                nzFgPaint ??= new SKPaint { Color = ColorHex.Parse(NegativniZnackaForeColor, "#FFFFFFFF"), IsAntialias = true };
                                float hpad  = aFont.Size * 0.12f;
                                float znakW = aFont.MeasureText(anotZnak);
                                var m = aFont.Metrics;
                                ctx.Canvas.DrawRect(new SKRect(anotX, minPt.Y + m.Ascent, anotX + znakW + hpad * 2, minPt.Y + m.Descent), nzBgPaint);
                                ctx.Canvas.DrawText(anotZnak, anotX + hpad, minPt.Y, aFont, nzFgPaint);
                                anotX += znakW + hpad * 2 + anotInterGapPx;
                                continue;
                            }

                            SKPaint aPaint = anot.Typ == TypAnotace.Text ? anotTextPaint! : anotSymPaint!;

                            // Per-anotace barva z DB (konzistence s legendou poznámek).
                            if (anot.ForeColor is not null)
                            {
                                customAnotPaint ??= new SKPaint { IsAntialias = true };
                                customAnotPaint.Color = ColorHex.ParseOrBlack(anot.ForeColor);
                                aPaint = customAnotPaint;
                            }

                            ctx.Canvas.DrawText(anotZnak, anotX, minPt.Y, aFont, aPaint);
                            anotX += aFont.MeasureText(anotZnak) + anotInterGapPx;
                        }
                    }

                    minX += slotW[oi] + scaledOdjezdGap;
                }
            }
            finally
            {
                customMinutePaint?.Dispose();
                customAnotPaint?.Dispose();
                nzBgPaint?.Dispose();
                nzFgPaint?.Dispose();
            }
        }

        // ── Svislé čáry mezi sloupci hodin a skupinami ───────────────────────
        //
        // ⚠️ Čára za sloupcem hodin se kreslí VŽDY, i s vypnutými „Čárami mezi sloupci" - není to
        // oddělovač sloupců, ale **osa pivotu**: odděluje hodinu od minut, které k ní patří. Bez ní
        // se obojí slije a mřížka přestane být čitelná. Sloupce jsou v tomhle prvku **skupiny dnů**,
        // takže volba řídí jen oddělovače mezi nimi (rozhodnutí Jan 31. 8., `L15`).
        // Není to opomenutí ani skrytá výjimka - je to přečtení toho, co „sloupec" v pivotu znamená.
        void RenderGridVerticalSeparators()
        {
            float vLineTopY = Y;

            if (!HourColumnPerGroup)
            {
                // Jedna čára za sdíleným sloupcem hodin
                var hourVLineTop = ctx.ToCanvas(X + hourW, vLineTopY);
                var hourVLineBottom = ctx.ToCanvas(X + hourW, currentY);
                ctx.Canvas.DrawLine(hourVLineTop, hourVLineBottom, paints.Grid);
            }

            float gxSep = HourColumnPerGroup ? X : X + hourW;
            for (int i = 0; i < layout.Groups.Count; i++)
            {
                if (HourColumnPerGroup)
                {
                    // Čára za hour sloupcem této skupiny
                    gxSep += hourW;
                    var hLineTop = ctx.ToCanvas(gxSep, vLineTopY);
                    var hLineBottom = ctx.ToCanvas(gxSep, currentY);
                    ctx.Canvas.DrawLine(hLineTop, hLineBottom, paints.Grid);
                }

                gxSep += layout.Groups[i].GroupWidth;

                if (i < layout.Groups.Count - 1)
                {
                    // Oddělovač mezi skupinami dnů = to, čemu se v tomhle prvku říká „sloupec".
                    if (Grid.ShowColumnLines)
                    {
                        float sepX = gxSep + GroupSeparatorWidth / 2f;
                        var sepTop = ctx.ToCanvas(sepX, vLineTopY);
                        var sepBottom = ctx.ToCanvas(sepX, currentY);
                        ctx.Canvas.DrawLine(sepTop, sepBottom, paints.Grid);
                    }
                    gxSep += GroupSeparatorWidth;
                }
            }
        }
    }


    /// <summary>Vykreslí ikonu do buňky záhlaví sloupce hodin - vejde se celá, vystředěná,
    /// s malým okrajem, aby se nedotýkala mřížky. Škáluje se s buňkou, takže drží i při zmenšení.
    /// ⚠️ `bunka` je v milimetrech (jako všechny rozměry elementu), plátno kreslí v pixelech -
    /// bez převodu se obrázek vykreslí mimo záběr.</summary>
    private static void DrawHourHeaderIcon(RenderContext ctx, SKImage ikona, SKRect bunkaMm)
    {
        float okraj = MathF.Min(bunkaMm.Width, bunkaMm.Height) * 0.12f;
        var cil = ctx.ToCanvasRect(
            bunkaMm.Left + okraj, bunkaMm.Top + okraj,
            bunkaMm.Width - (2 * okraj), bunkaMm.Height - (2 * okraj));
        if (cil.Width <= 0 || cil.Height <= 0) return;
        using var paint = new SKPaint { IsAntialias = true };
        var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
        ImageElement.DrawImageWithScaleMode(ctx.Canvas, ikona, cil, sampling, paint, ImageScaleMode.Fit);
    }

    private void RenderPlaceholder(RenderContext ctx)
    {
        // Použít kaskádu (element → ScheduleDefaults → CellDefaults), aby placeholder
        // v designeru ukazoval to samé, co preview s daty (bez per-column override).
        var headerResolved = ResolveElementHeaderParent();
        var dataResolved   = ResolveElementDataParent();

        float effFontSize = dataResolved.FontSize;
        float effHeaderFontSize = headerResolved.FontSize;
        float rowH = DataRowHeight ?? ComputeRowH(effFontSize);
        float headerH = ComputeHeaderH(effHeaderFontSize);

        using var paints = CreateStandardPaints(ctx);

        float fontPx = effFontSize * PtToMm * ctx.MmToPx;
        float headerFontPx = effHeaderFontSize * PtToMm * ctx.MmToPx;

        using var font = new SKFont(GetTypeface(dataResolved), fontPx);
        using var headerFont = new SKFont(GetTypeface(headerResolved), headerFontPx);

        float currentY = Y;

        if (ShowSkupinaDnuHeader)
        {
            float placeholderHeaderH = 2 * SkupinaDnuHeaderLineHeight;
            var headerRect = ctx.ToCanvasRect(X, currentY, Width, placeholderHeaderH);
            ctx.Canvas.DrawRect(headerRect, paints.HeaderBg);
            var pt1 = ctx.ToCanvas(X + 1f, currentY + SkupinaDnuHeaderLineHeight * 0.8f);
            ctx.Canvas.DrawText("PRACOVNÍ", pt1.X, pt1.Y, headerFont, paints.HeaderText);
            var pt2 = ctx.ToCanvas(X + 1f, currentY + SkupinaDnuHeaderLineHeight * 1.8f);
            ctx.Canvas.DrawText("DNY", pt2.X, pt2.Y, headerFont, paints.HeaderText);
            currentY += placeholderHeaderH;
        }

        currentY = RenderSectionHeader(ctx, "Hod",
            X, currentY, Width, headerH, headerFont, paints.HeaderBg, paints.HeaderText, paints.Grid);

        paints.Text.Color = new SKColor(0xFF999999);
        for (int h = 5; h <= 8; h++)
        {
            var pt = ctx.ToCanvas(X + 1f, currentY + rowH * TextBaselineFactor);
            ctx.Canvas.DrawText(h.ToString(), pt.X, pt.Y, font, paints.Text);
            var mpt = ctx.ToCanvas(X + HourColumnWidth + 1f, currentY + rowH * TextBaselineFactor);
            ctx.Canvas.DrawText("00  15  30  45", mpt.X, mpt.Y, font, paints.Text);

            var lineS = ctx.ToCanvas(X, currentY + rowH);
            var lineE = ctx.ToCanvas(X + Width, currentY + rowH);
            ctx.Canvas.DrawLine(lineS, lineE, paints.Grid);
            currentY += rowH;
        }

        // Outer border je řízen TemplateElement.BorderColor/BorderWidth (druhý průchod).
    }

    // ─── Výpočet výšky ──────────────────────────────────────────────────────

    public override float CalculateHeightFromData(DataContext? data)
    {
        var layout = ComputeLayout(data);
        if (layout is null) return DesignerPlaceholderHeight;

        float combinedHeaderH = ShowSkupinaDnuHeader
            ? Math.Max(layout.GroupHeaderH, layout.HeaderH)
            : layout.HeaderH;
        // Po zmenšení na šířku je menší i výška - prvek s dynamickou výškou se nakreslí takhle.
        return (combinedHeaderH + layout.Hours.Count * layout.RowH) * layout.MeritkoSirky;
    }

    /// <summary>Kolikrát se písmo zmenší, aby se mřížka vešla na výšku i na šířku (1 = vejde se) -
    /// týž výpočet jako kreslení, ať hláška netvrdí jinou velikost, než se vytiskne.</summary>
    internal float ZmenseniPisma(DataContext? data)
    {
        var layout = ComputeLayout(data);
        if (layout is null) return 1f;
        float combinedHeaderH = ShowSkupinaDnuHeader
            ? Math.Max(layout.GroupHeaderH, layout.HeaderH)
            : layout.HeaderH;
        return MathF.Min(ComputeFontScale(combinedHeaderH + layout.Hours.Count * layout.RowH), layout.MeritkoSirky);
    }

    /// <summary>Skupiny dnů se nevejdou do šířky prvku ani po zmenšení na mez - zbytek se ořízne.</summary>
    internal bool NevejdeSeNaSirku(DataContext? data) => ComputeLayout(data)?.NevejdeSe == true;

    /// <summary>Šířky skupin dnů a to, kolik jejich minuty potřebují (obojí po zmenšení na šířku) -
    /// pro testy rozvržení.</summary>
    internal (float[] Sirky, float[] Potreby, float Meritko) RozvrzeniSirek(DataContext? data) =>
        ComputeLayout(data) is { } l ? ([.. l.Groups.Select(g => g.GroupWidth)], l.PotrebneSirky, l.MeritkoSirky) : ([], [], 1f);

    /// <summary>Otisk nastavení, podle kterého cache rozvržení pozná změnu - pro testy.</summary>
    internal int LayoutFingerprint() => ComputeLayoutFingerprint();

    // ─── Helpers ────────────────────────────────────────────────────────────

    private List<SkupinaDnuData> ResolveSkupiny(List<SkupinaDnuData> all)
    {
        if (all.Count == 0) return [];

        // GroupColumns má přednost - filtruje podle IsVisible a určuje pořadí (přeskládání ▲▼ v editoru).
        if (GroupColumns.Count > 0)
        {
            var hidden = GroupColumns
                .Where(gc => !gc.IsVisible)
                .Select(gc => gc.SkupinaDnuId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Pořadí dle GroupColumns; skupiny mimo seznam zachovají datové pořadí na konci.
            var order = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < GroupColumns.Count; i++)
                if (!order.ContainsKey(GroupColumns[i].SkupinaDnuId))
                    order[GroupColumns[i].SkupinaDnuId] = i;

            var result = all
                .Where(s => !hidden.Contains(s.Id))
                .Select((s, dataIdx) => (s, key: order.TryGetValue(s.Id, out var oi) ? oi : int.MaxValue, dataIdx))
                .OrderBy(t => t.key).ThenBy(t => t.dataIdx)
                .Select(t => t.s)
                .ToList();
            return result.Count > 0 ? result : [all[0]];
        }

        // Fallback: starý SkupinaDnuId filtr (zpětná kompatibilita)
        if (string.IsNullOrWhiteSpace(SkupinaDnuId))
            return all;

        var ids = SkupinaDnuId.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var filtered = new List<SkupinaDnuData>();
        foreach (var id in ids)
        {
            var match = all.Find(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (match is not null) filtered.Add(match);
        }

        return filtered.Count > 0 ? filtered : [all[0]];
    }

    /// <summary>Aplikuje per-group override nadpisu (<see cref="DepartureGroupColumnStyle.HeaderOverride"/>)
    /// přes sdílený <see cref="Rendering.ColumnHeader.Resolve"/>. Když je override neprázdný, nahradí
    /// celý nadpis skupiny (jeden text - dvouřádek z dat se použije jen bez override). Vrací stejný
    /// seznam pokud žádný override není (žádná alokace navíc).</summary>
    private List<SkupinaDnuData> ApplyHeaderOverrides(List<SkupinaDnuData> skupiny)
    {
        if (GroupColumns.Count == 0) return skupiny;
        var lookup = GroupColumnIndexResolver.BuildStyleLookup(
            GroupColumns, skupiny.Select(s => s.Id).ToList());
        if (lookup.Count == 0) return skupiny;

        List<SkupinaDnuData>? result = null;
        for (int i = 0; i < skupiny.Count; i++)
        {
            var sk = skupiny[i];
            lookup.TryGetValue(sk.Id, out var cs);
            var effective = Rendering.ColumnHeader.Resolve(cs?.HeaderOverride, sk.Nadpis);
            if (ReferenceEquals(effective, sk.Nadpis)) continue;   // bez override

            result ??= new List<SkupinaDnuData>(skupiny);
            result[i] = new SkupinaDnuData { Id = sk.Id, Nadpis = effective, Nadpis2 = "", Poradi = sk.Poradi };
        }
        return result ?? skupiny;
    }

    /// <summary>Vrátí dostupné skupiny dnů z naposledy zpracovaných dat (pro dialog).</summary>
    public List<SkupinaDnuData> GetAvailableSkupinyDnu(DataContext? data) => BuildSkupinyDnu(data);

    /// <summary>
    /// Hodiny k zobrazení podle režimu časové osy. „Hodiny od/do" (`HODINY_OD`/`HODINY_DO`) platí
    /// **jen** ve Vlastním rozsahu - jsou to jeho meze, ne globální filtr.
    ///
    /// Dřív zužovaly i ostatní režimy, a protože jsou v nich schované (`visibleWhen`
    /// = „CASOVA_OSA=Vlastní rozsah"), vznikalo tiché přetrvávání: po přepnutí režimu jinam
    /// zadaný rozsah dál řezal mřížku a nebylo ho jak vidět ani zrušit. Navíc výchozí „od" = 4
    /// znamenalo, že se hodiny 0-3 zahazovaly i bez jakéhokoli nastavení uživatele.
    /// Pravidlo: **schovaný ovladač nesmí ovlivňovat výstup.**
    /// </summary>
    internal static List<int> GetVisibleHoursFromSet(
        HashSet<int> allHours, TimeAxisMode timeAxis, int rangeFrom, int rangeTo) =>
        timeAxis switch
        {
            TimeAxisMode.Compact     => allHours.OrderBy(h => h).ToList(),
            TimeAxisMode.DataBounded => DataBoundedHours(allHours),
            TimeAxisMode.Range       => RangeHoursExtended(rangeFrom, rangeTo),
            TimeAxisMode.Full        => Enumerable.Range(0, 24).ToList(),
            _ => allHours.OrderBy(h => h).ToList()
        };

    /// <summary>Souvislé hodiny od min(data) do max(data) - včetně prázdných v rozsahu.
    /// Data s extended hours (24..47) přirozeně rozšíří horní mez.</summary>
    private static List<int> DataBoundedHours(HashSet<int> allHours)
    {
        if (allHours.Count == 0) return new List<int>();
        int min = allHours.Min();
        int max = allHours.Max();
        return Enumerable.Range(min, max - min + 1).ToList();
    }

    /// <summary>Service hours [from..to]. Pokud to &lt; from, wrap přes půlnoc se reprezentuje
    /// jako extended hours: [from..23] + [24..to+24]. Tj. 22→3 = [22, 23, 24, 25, 26, 27].
    /// Tyto hodnoty se na header rendrují jako H%24 (např. 27 = „3"), ale v ByHour lookupu zůstávají
    /// raw - Caché dataset s konvencí extended hours se napáruje 1:1.</summary>
    private static List<int> RangeHoursExtended(int from, int to)
    {
        from = Math.Clamp(from, 0, 23);
        to   = Math.Clamp(to, 0, 23);
        if (to >= from)
            return Enumerable.Range(from, to - from + 1).ToList();
        // Wrap → extended: 22-5 → [22, 23, 24, 25, 26, 27, 28, 29]
        // (data lookup byHour[24] → odjezdy v 0:xx následujícího provozního dne)
        return Enumerable.Range(from, 24 - from)
            .Concat(Enumerable.Range(24, to + 1))
            .ToList();
    }

    private static TimeAxisMode ParseTimeAxisMode(string s) => s.ToLowerInvariant() switch
    {
        // Nové popisné labely (varianta A - z Caché AddStandardJRParameters)
        "jen hodiny s odjezdy"          => TimeAxisMode.Compact,
        "souvislá od první do poslední"
            or "souvisla od prvni do posledni" => TimeAxisMode.DataBounded,
        "vlastní rozsah" or "vlastni rozsah"   => TimeAxisMode.Range,
        // Staré české labely (backward compat - předchozí iterace tohoto enumu)
        "souvislá bez mezer"  or "souvisla bez mezer"  => TimeAxisMode.Compact,
        "souvislá s mezerami" or "souvisla s mezerami" => TimeAxisMode.DataBounded,
        "daná intervalem"     or "dana intervalem"     => TimeAxisMode.Range,
        "plný den"            or "plny den"            => TimeAxisMode.Full,
        // Anglické hodnoty (backward compat pro nejstarší šablony / přímé enum názvy)
        "compact"     => TimeAxisMode.Compact,
        "databounded" => TimeAxisMode.DataBounded,
        "range"       => TimeAxisMode.Range,
        "full"        => TimeAxisMode.Full,
        _             => TimeAxisMode.Compact
    };

    private string[] WrapHeaderText(string fullText, float availableWidthMm, SKFont font, float mmToPx)
    {
        float padding = 2f;
        float maxW = availableWidthMm - padding;

        // Zkusit celý text na jeden řádek
        if (MeasureTextMm(font, fullText, mmToPx) <= maxW)
            return [fullText];

        // Word wrap - skládat slova do řádků
        var words = fullText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var currentLine = "";

        foreach (var word in words)
        {
            var testLine = currentLine.Length == 0 ? word : currentLine + " " + word;
            if (MeasureTextMm(font, testLine, mmToPx) <= maxW || currentLine.Length == 0)
            {
                currentLine = testLine;
            }
            else
            {
                lines.Add(currentLine);
                currentLine = word;
            }
        }

        if (currentLine.Length > 0)
            lines.Add(currentLine);

        return [.. lines];
    }

    private static int GetMaxMinutesPerHour(Dictionary<int, List<OdjezdData>> byHour, List<int> hours)
    {
        int max = 0;
        foreach (var h in hours)
        {
            if (byHour.TryGetValue(h, out var list) && list.Count > max)
                max = list.Count;
        }
        return max;
    }

    // ─── PropertyDescriptors ────────────────────────────────────────────────

    /// <summary>Lazy-init HeaderLine2Style při prvním nastavení libovolné jeho hodnoty z UI.</summary>
    private TextStyle EnsureHeaderLine2Style() => HeaderLine2Style ??= new TextStyle();

    /// <summary>Nastaví element-level velikost písma záhlaví. Typografie záhlaví je element-uniformní
    /// (per-column se ignoruje v ResolveGroupHeaderStyle/ResolveHourHeaderStyle), takže stačí set + invalidace cache.</summary>
    private void SetHeaderFontSize(float v)
    {
        HeaderStyle.FontSize = v;
        _cachedLayout = null;
    }

    public override IReadOnlyList<PropertyDescriptor> GetPropertyDescriptors() =>
    [
        // ── Výchozí styl (základ pro dědění - per-column nastavení ho přepisuje) ──
        .. GetScheduleBaseDescriptors(),
        .. GetCellZoneBaseColorDescriptors(),

        new ButtonPropertyDescriptor { Label = "Sloupce", Group = "Odjezdy", ButtonLabel = "Upravit sloupce…", ActionId = ButtonPropertyDescriptor.ActionOpenDepartureGridEditor },

        new BoolPropertyDescriptor  { FromImage = true, Label = "Záhlaví skupin dnů", Group = "Odjezdy", Get = () => ShowSkupinaDnuHeader,       Set = v => ShowSkupinaDnuHeader = v },
        // Velikost písma záhlaví - přímý ovladač HeaderStyle.FontSize. Bez něj šlo měnit jen
        // „Výška řádku záhl." (mm), která ale text zvětší jen dokud je FontSize null (legacy derive).
        new FloatPropertyDescriptor { Label = "Velikost záhlaví",  Group = "Odjezdy", Get = () => HeaderStyle.FontSize ?? 9f,  Set = SetHeaderFontSize, Min = 3, Max = 72, Unit = "pt" },
        new FloatPropertyDescriptor { Label = "Min. výška řádku záhlaví", Group = "Odjezdy", Get = () => SkupinaDnuHeaderLineHeight,  Set = v => SkupinaDnuHeaderLineHeight = v, Min = 2, Max = 10, Unit = "mm" },
        new EnumPropertyDescriptor  { FromImage = true, Label = "Zarovnání záhlaví svisle", Group = "Odjezdy",
            Get = () => HeaderVerticalAlignment, Set = v => HeaderVerticalAlignment = (TextVerticalAlignment)v,
            Options = EnumPropertyDescriptor.VAlignOptions },
        new FloatPropertyDescriptor { FromImage = true, Label = "Šířka sloupce hodin", Group = "Odjezdy", Get = () => HourColumnWidth,            Set = v => HourColumnWidth = v, Min = 4, Max = 20, Unit = "mm" },

        // Časová osa (rozsah hodin) ani „Roztáhnout na výšku" tu nejsou - řídí je parametry sestavy
        // (CASOVA_OSA/HODINY_OD/DO, ROZTAHNOUT_NA_VYSKU), které vlastnost prvku přebíjejí. Dvě cesty
        // k jedné věci znamenaly, že klik v prvku nic nedělal (hlášení B22/B24).

        new BoolPropertyDescriptor  { FromImage = true, Label = "Hodiny u každé skupiny", Group = "Odjezdy", Get = () => HourColumnPerGroup,        Set = v => HourColumnPerGroup = v },
        new FloatPropertyDescriptor { Label = "Nejvýš skupin dnů",   Group = "Odjezdy", Get = () => MaxGroupCount,             Set = v => MaxGroupCount = (int)v, Min = 0, Max = 10, Format = "0" },
        new FloatPropertyDescriptor { FromImage = true, Label = "Šířka minuty",       Group = "Odjezdy", Get = () => MinuteWidth,                Set = v => MinuteWidth = v, Min = 3, Max = 15, Unit = "mm" },
        new EnumPropertyDescriptor
        {
            FromImage = true, Label = "Rozestup odjezdů", Group = "Odjezdy",
            Get = () => MinuteSpacing,
            Set = v => MinuteSpacing = (DepartureSpacing)v,
            Options =
            [
                // Popisky pojmenovávají VÝSLEDEK, ne mechanismus: první slovo řekne, jestli se
                // zarovnává, a zbytek věty jedinou věc, kterou se druhé dvě liší - odkud se bere
                // rozteč. Dřívější trojice („Podle obsahu / Rovnoměrný / Pevný rastr") mluvila
                // v každé položce o něčem jiném (rozhodnutí Jan 18. 9.).
                ("Bez zarovnání", (object)DepartureSpacing.ByContent),
                ("Zarovnat dle nejširšího odjezdu", (object)DepartureSpacing.Even),
                ("Zarovnat na pevnou šířku", (object)DepartureSpacing.Fixed),
            ]
        },
        new FloatPropertyDescriptor { FromImage = true, Label = "Mezera mezi skupinami dnů", Group = "Odjezdy", Get = () => GroupSeparatorWidth,        Set = v => GroupSeparatorWidth = v, Min = 0, Max = 5, Unit = "mm" },

        GetDataRowHeightDescriptor("Řádky dat"),
        new ColorPropertyDescriptor { FromImage = true, Label = "Střídání řádků", Group = "Řádky dat", Get = () => AlternatingRowColor ?? "", Set = v => AlternatingRowColor = string.IsNullOrEmpty(v) ? null : v },

        // ── Styl 2. řádku záhlaví skupin dnů (např. "den" v "Pracovní / den") ──
        // Aplikuje se POUZE pokud SkupinaDnu.Nadpis2 je vyplněn. Null = stejné jako Line 1 (HeaderStyle).
        new FloatPropertyDescriptor  { Label = "Velikost",     Group = "Záhlaví - 2. řádek", Get = () => HeaderLine2Style?.FontSize ?? HeaderStyle.FontSize ?? 9f, Set = v => EnsureHeaderLine2Style().FontSize = v, Min = 3, Max = 72, Unit = "pt" },
        new BoolPropertyDescriptor   { Label = "Tučné",        Group = "Záhlaví - 2. řádek", Get = () => HeaderLine2Style?.Bold ?? HeaderStyle.Bold ?? true, Set = v => EnsureHeaderLine2Style().Bold = v },
        new BoolPropertyDescriptor   { Label = "Kurzíva",      Group = "Záhlaví - 2. řádek", Get = () => HeaderLine2Style?.Italic ?? HeaderStyle.Italic ?? false, Set = v => EnsureHeaderLine2Style().Italic = v ? true : null },
        new ColorPropertyDescriptor  { Label = "Barva textu",  Group = "Záhlaví - 2. řádek", Get = () => HeaderLine2Style?.TextColor ?? HeaderStyle.TextColor ?? "", Set = v => EnsureHeaderLine2Style().TextColor = string.IsNullOrEmpty(v) ? null : v },

        // ── Styl minuty ──
        new StringPropertyDescriptor { Label = "Minuta: Font",         Group = "Styl minuty", Get = () => MinuteStyle.FontFamily ?? "", Set = v => MinuteStyle.FontFamily = string.IsNullOrWhiteSpace(v) ? null : v },
        new FloatPropertyDescriptor  { Label = "Minuta: Velikost",     Group = "Styl minuty", Get = () => MinuteStyle.FontSize ?? MinuteFontSize, Set = v => MinuteStyle.FontSize = v, Min = 3, Max = 72, Unit = "pt" },
        new BoolPropertyDescriptor   { Label = "Minuta: Tučné",        Group = "Styl minuty", Get = () => MinuteStyle.Bold ?? MinuteBold,         Set = v => MinuteStyle.Bold = v },
        new BoolPropertyDescriptor   { Label = "Minuta: Kurzíva",      Group = "Styl minuty", Get = () => MinuteStyle.Italic ?? false,            Set = v => MinuteStyle.Italic = v ? true : null },
        new ColorPropertyDescriptor  { Label = "Minuta: Barva textu",  Group = "Styl minuty", Get = () => MinuteStyle.TextColor ?? "",            Set = v => MinuteStyle.TextColor = string.IsNullOrEmpty(v) ? null : v },

        // ── Styl anotací - symboly (speciální/NZ) ──
        new StringPropertyDescriptor { Label = "Symbol: Font",         Group = "Značky u odjezdů - symbol", Get = () => AnotaceSymbolStyle.FontFamily ?? "", Set = v => AnotaceSymbolStyle.FontFamily = string.IsNullOrWhiteSpace(v) ? null : v },
        new FloatPropertyDescriptor  { Label = "Symbol: Velikost",     Group = "Značky u odjezdů - symbol", Get = () => AnotaceSymbolStyle.FontSize ?? 0, Set = v => AnotaceSymbolStyle.FontSize = v > 0 ? v : null, Min = 0, Max = 72, Unit = "pt" },
        new BoolPropertyDescriptor   { Label = "Symbol: Tučné",        Group = "Značky u odjezdů - symbol", Get = () => AnotaceSymbolStyle.Bold ?? false,   Set = v => AnotaceSymbolStyle.Bold = v ? true : null },
        new BoolPropertyDescriptor   { Label = "Symbol: Kurzíva",      Group = "Značky u odjezdů - symbol", Get = () => AnotaceSymbolStyle.Italic ?? false, Set = v => AnotaceSymbolStyle.Italic = v ? true : null },
        new ColorPropertyDescriptor  { Label = "Symbol: Barva textu",  Group = "Značky u odjezdů - symbol", Get = () => AnotaceSymbolStyle.TextColor ?? "", Set = v => AnotaceSymbolStyle.TextColor = string.IsNullOrEmpty(v) ? null : v },
        new ColorPropertyDescriptor  { FromImage = true, Label = "Negativní značka: Barva pozadí", Group = "Značky u odjezdů - symbol", Get = () => NegativniZnackaBackColor, Set = v => NegativniZnackaBackColor = v },
        new ColorPropertyDescriptor  { FromImage = true, Label = "Negativní značka: Barva textu", Group = "Značky u odjezdů - symbol", Get = () => NegativniZnackaForeColor, Set = v => NegativniZnackaForeColor = v },

        // ── Styl anotací - text (písmena, dny) ──
        new StringPropertyDescriptor { Label = "Text: Font",         Group = "Značky u odjezdů - text", Get = () => AnotaceTextStyle.FontFamily ?? "", Set = v => AnotaceTextStyle.FontFamily = string.IsNullOrWhiteSpace(v) ? null : v },
        new FloatPropertyDescriptor  { Label = "Text: Velikost",     Group = "Značky u odjezdů - text", Get = () => AnotaceTextStyle.FontSize ?? AnotaceFontSize, Set = v => AnotaceTextStyle.FontSize = v, Min = 3, Max = 72, Unit = "pt" },
        new BoolPropertyDescriptor   { Label = "Text: Tučné",        Group = "Značky u odjezdů - text", Get = () => AnotaceTextStyle.Bold ?? false,   Set = v => AnotaceTextStyle.Bold = v ? true : null },
        new BoolPropertyDescriptor   { Label = "Text: Kurzíva",      Group = "Značky u odjezdů - text", Get = () => AnotaceTextStyle.Italic ?? false, Set = v => AnotaceTextStyle.Italic = v ? true : null },
        new ColorPropertyDescriptor  { Label = "Text: Barva textu",  Group = "Značky u odjezdů - text", Get = () => AnotaceTextStyle.TextColor ?? "", Set = v => AnotaceTextStyle.TextColor = string.IsNullOrEmpty(v) ? null : v },

        new BoolPropertyDescriptor  { Label = "Značky u odjezdů",     Group = "Značky u odjezdů", Get = () => ShowAnotace,              Set = v => ShowAnotace = v },

        new FloatPropertyDescriptor { Label = "Mezera mezi značkou a minutou", Group = "Mezery", Get = () => AnotaceGap,      Set = v => AnotaceGap = v,      Min = 0, Max = 5, Unit = "mm" },
        new FloatPropertyDescriptor { Label = "Mezera mezi značkami",   Group = "Mezery", Get = () => AnotaceInterGap, Set = v => AnotaceInterGap = v, Min = 0, Max = 5, Unit = "mm" },
        new FloatPropertyDescriptor { Label = "Mezera mezi odjezdy",  Group = "Mezery", Get = () => OdjezdGap,       Set = v => OdjezdGap = v,       Min = 0, Max = 10, Unit = "mm" },

        .. GetPositionDescriptors()
    ];

    // ─── Scale ──────────────────────────────────────────────────────────────

    public override void ApplyScale(float scale)
    {
        base.ApplyScale(scale);   // DataRowHeight + Header/Data styl škáluje base
        SkupinaDnuHeaderLineHeight *= scale;
        HourColumnWidth *= scale;
        MinuteWidth *= scale;
        MinuteFontSize *= scale;
        AnotaceFontSize *= scale;
        AnotaceGap *= scale;
        AnotaceInterGap *= scale;
        OdjezdGap *= scale;
        GroupSeparatorWidth *= scale;
        // Zóny stylů mimo Header/Data (font + padding) + per-skupinové overridy + pevné šířky skupin
        HeaderLine2Style?.ApplyScale(scale);
        MinuteStyle.ApplyScale(scale);
        AnotaceSymbolStyle.ApplyScale(scale);
        AnotaceTextStyle.ApplyScale(scale);
        foreach (var gc in GroupColumns)
        {
            gc.Style.ApplyScale(scale);
            if (gc.WidthOverride is float gw) gc.WidthOverride = gw * scale;
        }
    }

    // ─── Clone ──────────────────────────────────────────────────────────────

    public override TemplateElement Clone()
    {
        var clone = new DepartureGridElement
        {
            SkupinaDnuId               = SkupinaDnuId,
            ShowSkupinaDnuHeader       = ShowSkupinaDnuHeader,
            SkupinaDnuHeaderLineHeight = SkupinaDnuHeaderLineHeight,
            HeaderVerticalAlignment    = HeaderVerticalAlignment,
            HeaderLine2Style           = HeaderLine2Style?.Clone(),
            HourColumnWidth            = HourColumnWidth,
            HourColumnPerGroup         = HourColumnPerGroup,
            MaxGroupCount              = MaxGroupCount,
            MinuteWidth                = MinuteWidth,
            MinuteSpacing              = MinuteSpacing,
            MinuteFontSize             = MinuteFontSize,
            MinuteStyle                = MinuteStyle.Clone(),
            AnotaceSymbolStyle         = AnotaceSymbolStyle.Clone(),
            AnotaceTextStyle           = AnotaceTextStyle.Clone(),
            AnotaceGap                 = AnotaceGap,
            AnotaceInterGap            = AnotaceInterGap,
            OdjezdGap                  = OdjezdGap,
            GroupSeparatorWidth        = GroupSeparatorWidth,
            MinuteBold                 = MinuteBold,
            AlternatingRowColor        = AlternatingRowColor,
            ShowAnotace                = ShowAnotace,
            AnotaceFontSize            = AnotaceFontSize,
            NegativniZnackaBackColor   = NegativniZnackaBackColor,
            NegativniZnackaForeColor   = NegativniZnackaForeColor,
            HourColumnStyle            = HourColumnStyle?.Clone(),
            HourColumnHeader           = HourColumnHeader,
            HourColumnHeaderImageBase64 = HourColumnHeaderImageBase64,
            GroupColumns               = GroupColumns.Select(gc => gc.Clone()).ToList(),
            CustomColumns              = CustomColumns?.Select(c => c.Clone()).ToList(),
        };
        CopyBaseTo(clone);
        CopyStopScheduleProperties(clone);
        return clone;
    }
}

/// <summary>Rozestup odjezdů v řádku hodiny.</summary>
public enum DepartureSpacing
{
    /// <summary>Slot každého odjezdu je široký podle svého obsahu (výchozí, chování do 8/2026):
    /// minuta se značkou zabere víc místa a odsune odjezdy za sebou.</summary>
    ByContent,
    /// <summary>Jeden rozestup pro celou skupinu podle nejširšího odjezdu - minuty stojí ve svislici
    /// napříč řádky, značka nerozhodí zarovnání ostatních.</summary>
    Even,
    /// <summary>Rozestup daný jen nastavenou šířkou minuty; značky se do něj nepočítají, a když se
    /// tam nevejdou, úměrně se zmenší. Hustota vývěsky tak nezávisí na tom, kolik má linka značek.</summary>
    Fixed,
}
