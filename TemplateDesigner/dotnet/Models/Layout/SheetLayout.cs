using System.Linq;

namespace TemplateDesigner.Models.Layout;

/// <summary>
/// Konfigurace imposice - skládání logických stránek šablony na fyzické listy (N-up).
/// Vlastnost VÝSTUPU, ne šablony: šablona zůstává např. A5, teprve tisk/export skládá
/// 2×A5 na A4 naležato. Stránky se umisťují 1:1 (bez zmenšování), blok se na listu centruje.
/// </summary>
public sealed record SheetLayoutOptions
{
    /// <summary>Šířka fyzického listu v mm.</summary>
    public required float SheetWidth { get; init; }

    /// <summary>Výška fyzického listu v mm.</summary>
    public required float SheetHeight { get; init; }

    /// <summary>Počet řádků mřížky.</summary>
    public required int Rows { get; init; }

    /// <summary>Počet sloupců mřížky.</summary>
    public required int Cols { get; init; }

    /// <summary>Rozměr logické stránky v mm (šířka).</summary>
    public required float PageWidth { get; init; }

    /// <summary>Rozměr logické stránky v mm (výška).</summary>
    public required float PageHeight { get; init; }

    /// <summary>True = každá logická stránka vyplní celý list (táž vývěska N× pro řezání).
    /// False = sekvenční skládání (stránky po sobě).</summary>
    public bool RepeatEachPage { get; init; }

    /// <summary>True = kreslit tenké ořezové čáry na hranicích stránek.</summary>
    public bool DrawCutMarks { get; init; }

    /// <summary>
    /// True = oboustranný tisk dokladů o dvou stranách: každé dvě stránky za sebou jsou líc a rub
    /// jednoho dokladu. Líce jdou na jeden list, ruby na další na tatáž místa (#12854, dvě stazky
    /// A5 na A4). Opakování stránky má přednost - samo střídá líc a rub na celých listech.
    /// </summary>
    public bool Oboustranne { get; init; }

    /// <summary>
    /// Nakreslené uspořádání (`B186`) - která stránka šablony jde do kterého pole přední a zadní
    /// strany. Null = dřívější režimy (za sebou / opakovat / oboustranně) z uložených voleb.
    /// </summary>
    public UsporadaniListu? Plan { get; init; }

    /// <summary>Počet pozic na jednom listu.</summary>
    public int SlotsPerSheet => Rows * Cols;

    /// <summary>X počátek bloku stránek (centrovaný) v mm.</summary>
    public float BlockOriginX => (SheetWidth - Cols * PageWidth) / 2f;

    /// <summary>Y počátek bloku stránek (centrovaný) v mm.</summary>
    public float BlockOriginY => (SheetHeight - Rows * PageHeight) / 2f;

    /// <summary>Krátký popis pro status hlášky exportu - ať je vidět, že se skládalo na listy
    /// (per-agenda persistovaná imposice jinak uživatele překvapí „proč mám půlku stránek").</summary>
    public string ShortLabel => $"imposice {Rows}×{Cols} na list {SheetWidth:0}×{SheetHeight:0} mm";

    /// <summary>
    /// Hláška, když skládání na list **nic neskládá** (mřížka 1×1), nebo null když skládá.
    /// Poradí otočený list, pokud by na něj šlo víc stránek.
    /// </summary>
    /// <remarks>
    /// ⚠️ Tohle je past, na kterou zákazník najel a hlásil ji jako chybu tisku (#13158): šablona A5
    /// na výšku a list „A4 na výšku" dají mřížku 1×1, protože dva A5 na výšku (2 × 148 mm) se do
    /// šířky A4 na výšku (210 mm) nevejdou. Výsledkem je A4 s jednou vývěskou uprostřed - papír se
    /// jen zvětšil. Volba se navíc **persistuje per agenda**, takže o ní uživatel nemusí vědět;
    /// bez hlášky nemá jak zjistit, proč mu z A5 šablony leze A4.
    /// </remarks>
    public string? VarovaniBezEfektu()
    {
        if (SlotsPerSheet > 1) return null;

        string zaklad = $"Uspořádání na list nic neskládá: na list {SheetWidth:0}×{SheetHeight:0} mm "
                      + $"se vejde jen jedna stránka {PageWidth:0.#}×{PageHeight:0.#} mm";

        // Vejde se víc na otočený list? Pak je to ta rada, kterou uživatel potřebuje.
        int otoceneRadky = (int)(SheetWidth / PageHeight);
        int otoceneSloupce = (int)(SheetHeight / PageWidth);
        if (otoceneRadky >= 1 && otoceneSloupce >= 1 && otoceneRadky * otoceneSloupce > 1)
            return zaklad + $". Na list {SheetHeight:0}×{SheetWidth:0} mm (otočený) se vejdou "
                          + $"{otoceneRadky * otoceneSloupce}";

        return zaklad + " - buď uspořádání vypněte, nebo zvolte větší list";
    }

    /// <summary>X souřadnice svislých ořezových čar (všechny hranice sloupců vč. krajních) v mm.</summary>
    public IEnumerable<float> CutLinesX => Enumerable.Range(0, Cols + 1).Select(i => BlockOriginX + i * PageWidth);

    /// <summary>Y souřadnice vodorovných ořezových čar (všechny hranice řádků vč. krajních) v mm.</summary>
    public IEnumerable<float> CutLinesY => Enumerable.Range(0, Rows + 1).Select(j => BlockOriginY + j * PageHeight);
}

/// <summary>Jedna logická stránka umístěná na listu (offset levého horního rohu v mm).</summary>
public sealed record PlacedPage(Page Page, float OffsetX, float OffsetY);

/// <summary>Jeden fyzický list se složenými stránkami.</summary>
public sealed record Sheet(IReadOnlyList<PlacedPage> Pages);

/// <summary>
/// Klíče a hodnoty exportních polí imposice - wire kontrakt s Caché
/// <c>ALVA.TemplateDesigner.EDInputForm.AgendaParams.AddExportFields</c>. Stabilní stringy (parsuje C#).
/// </summary>
public static class SheetLayoutKeys
{
    public const string Sheet = "__EXPORT_SHEET";
    public const string Grid = "__EXPORT_SHEET_GRID";
    public const string Repeat = "__EXPORT_SHEET_REPEAT";
    public const string Marks = "__EXPORT_SHEET_MARKS";
    public const string Oboustranne = "__EXPORT_SHEET_DUPLEX";
    /// <summary>Nakreslené uspořádání jako JSON (<see cref="UsporadaniListu"/>, `B186`).</summary>
    public const string Plan = "__EXPORT_SHEET_PLAN";
    public const string Meritko = "__EXPORT_SCALE";

    public const string SheetNone = "NONE";
    public const string GridAuto = "AUTO";

    /// <summary>Všechny klíče - pro persistence restore (nejsou mezi agenda parametry).</summary>
    public static readonly string[] All = [Sheet, Grid, Repeat, Marks, Oboustranne, Plan, Meritko];
}

/// <summary>
/// Čistá logika imposice: parsování konfigurace z exportních polí + skládání stránek na listy.
/// Bez SkiaSharp/WPF - testovatelné, sdílené PrintDialogem i exportem z hlavního okna.
/// </summary>
public static class SheetComposer
{
    /// <summary>
    /// Sestaví <see cref="SheetLayoutOptions"/> z hodnot exportních polí (sdílený dict parametrů).
    /// Vrací null = imposice vypnutá. Vyhazuje <see cref="ArgumentException"/> s českou hláškou,
    /// když se zvolená mřížka na list 1:1 nevejde (uživatelská chyba konfigurace).
    /// </summary>
    public static SheetLayoutOptions? FromParamValues(IReadOnlyDictionary<string, object?> values, PageSize page)
    {
        // Nakreslené uspořádání má přednost před dřívějšími poli (`B186`).
        if (UsporadaniListu.FromJson(values.GetValueOrDefault(SheetLayoutKeys.Plan) as string) is { } plan)
            return ZPlanu(plan, page, MeritkoZParamValues(values));

        var sheetKey = (values.GetValueOrDefault(SheetLayoutKeys.Sheet) as string) ?? SheetLayoutKeys.SheetNone;
        if (sheetKey is "" or SheetLayoutKeys.SheetNone) return null;

        var (sheetW, sheetH) = ParseSheetFormat(sheetKey)
            ?? throw new ArgumentException($"Neznámý formát listu: {sheetKey}");

        // ⚠️ Nejdřív měřítko, pak skládání: na list se vejde tolik stránek, jak jsou VELKÉ po
        // zvětšení. Bez toho by mřížka počítala s návrhovým rozměrem a stránky by z listu vylezly.
        float meritko = MeritkoZParamValues(values);
        float pageW = page.Width * meritko;
        float pageH = page.Height * meritko;

        var gridKey = (values.GetValueOrDefault(SheetLayoutKeys.Grid) as string) ?? SheetLayoutKeys.GridAuto;
        var (rows, cols) = ResolveGrid(gridKey, pageW, pageH, sheetW, sheetH);

        return new SheetLayoutOptions
        {
            SheetWidth = sheetW,
            SheetHeight = sheetH,
            Rows = rows,
            Cols = cols,
            PageWidth = pageW,
            PageHeight = pageH,
            RepeatEachPage = ParseBool(values.GetValueOrDefault(SheetLayoutKeys.Repeat)),
            DrawCutMarks = ParseBool(values.GetValueOrDefault(SheetLayoutKeys.Marks)),
            Oboustranne = ParseBool(values.GetValueOrDefault(SheetLayoutKeys.Oboustranne)),
        };
    }

    /// <summary>Volby skládání z nakresleného uspořádání. Formát „Vypnuto" = null.</summary>
    public static SheetLayoutOptions? ZPlanu(UsporadaniListu plan, PageSize page, float meritko = 1f)
    {
        if (plan.Format is "" or SheetLayoutKeys.SheetNone) return null;
        var (sheetW, sheetH) = ParseSheetFormat(plan.Format)
            ?? throw new ArgumentException($"Neznámý formát listu: {plan.Format}");
        float pageW = page.Width * meritko, pageH = page.Height * meritko;
        var (rows, cols) = ResolveGrid($"{plan.Radku}x{plan.Sloupcu}", pageW, pageH, sheetW, sheetH);
        return new SheetLayoutOptions
        {
            SheetWidth = sheetW, SheetHeight = sheetH, Rows = rows, Cols = cols,
            PageWidth = pageW, PageHeight = pageH,
            DrawCutMarks = plan.OrezoveZnacky,
            Plan = plan,
        };
    }

    /// <summary>
    /// Složí doklady na listy podle nakresleného uspořádání. Doklad = stránky jedné datové stránky
    /// agendy, u každé stránka šablony, ze které vznikla. Strany PDF jdou za sebou: 1. strana,
    /// a má-li plán 2. stranu, hned za ní 2. strana - oboustranný tisk si zapne tiskárna.
    /// </summary>
    /// <remarks>
    /// <para>Pole bez stránky v dokladu (stránku skryla podmínka, doklady došly) zůstane prázdné.</para>
    /// <para>⚠️ Stránka šablony „podle dat" vydá v jednom dokladu víc stránek a pole ji neumí vzít
    /// po jedné. Pak se pole plní stránkami za sebou bez ohledu na nákres, jen vpředu - nic se tím
    /// neztratí, jen se to neskládá oboustranně. U jízdních řádů (jedna stránka šablony, vývěsky
    /// na A4) na to nedojde.</para>
    /// </remarks>
    public static List<Sheet> ComposeDle(IReadOnlyList<IReadOnlyList<(Page Page, int Stranka)>> doklady,
        SheetLayoutOptions o)
    {
        var plan = (o.Plan ?? throw new ArgumentException("Chybí uspořádání listu")).Normalizuj();
        var sloty = AllSlots(o).ToList();
        var sheets = new List<Sheet>();
        if (doklady.Count == 0) return sheets;

        bool vicestrankove = doklady.Any(d => d.GroupBy(x => x.Stranka).Any(g => g.Count() > 1));
        if (vicestrankove)
            return Compose([.. doklady.SelectMany(d => d.Select(x => x.Page))], o with { Plan = null });

        int naList = plan.DokladuNaList;
        int kroku = (doklady.Count + naList - 1) / naList;
        for (int k = 0; k < kroku; k++)
        {
            sheets.Add(Strana(zadni: false));
            if (plan.DveStrany) sheets.Add(Strana(zadni: true));

            Sheet Strana(bool zadni)
            {
                var placed = new List<PlacedPage>();
                for (int i = 0; i < sloty.Count; i++)
                {
                    if (plan.Pole(zadni, i) is not { } pole) continue;
                    int doklad = k * naList + pole.Doklad;
                    if (doklad >= doklady.Count) continue;
                    var stranka = doklady[doklad].FirstOrDefault(x => x.Stranka == pole.Stranka).Page;
                    if (stranka is null) continue;
                    placed.Add(new PlacedPage(stranka, sloty[i].X, sloty[i].Y));
                }
                return new Sheet(placed);
            }
        }
        return sheets;
    }

    /// <summary>Nejmenší a největší povolené měřítko tisku - shodné s mezemi pole ve formuláři.</summary>
    public const float MeritkoMin = 0.1f, MeritkoMax = 10f;

    /// <summary>
    /// Měřítko tisku z exportních polí. 1 = skutečná velikost, chybějící i nesmyslná hodnota → 1.
    /// </summary>
    /// <remarks>
    /// ⚠️ **Je to vlastnost výstupu, ne šablony** (`B132`, parita s `MeritkoVyves` starého
    /// postskriptu: vývěsky 1,3×, kniha 1×). Sazba se **nepřepočítává** - násobí se až převod
    /// milimetrů na body, takže na stránku jde tentýž počet řádků a mění se jen rozměr na papíře.
    /// Menu „Přepočítat šablonu na jinou velikost" je něco jiného: to sahá na souřadnice prvků
    /// v šabloně a původní velikost tím zaniká.
    /// </remarks>
    public static float MeritkoZParamValues(IReadOnlyDictionary<string, object?> values)
    {
        var raw = values.GetValueOrDefault(SheetLayoutKeys.Meritko);
        float m = raw switch
        {
            float f => f,
            double d => (float)d,
            int i => i,
            string s when float.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var p) => p,
            string s when float.TryParse(s, out var p) => p,
            _ => 1f
        };
        return m is > 0f and <= MeritkoMax && m >= MeritkoMin ? m : 1f;
    }

    /// <summary>Formát listu z wire klíče → (šířka, výška) v mm. Null = neznámý klíč.</summary>
    public static (float W, float H)? ParseSheetFormat(string key) => key switch
    {
        "A4_PORTRAIT"  => (210f, 297f),
        "A4_LANDSCAPE" => (297f, 210f),
        "A3_PORTRAIT"  => (297f, 420f),
        "A3_LANDSCAPE" => (420f, 297f),
        _ => null,
    };

    /// <summary>
    /// Mřížka z wire klíče ("AUTO" nebo "ŘÁDKYxSLOUPCE", např. "1x2"). AUTO = největší 1:1 fit.
    /// Vyhazuje <see cref="ArgumentException"/>, když se stránka na list nevejde ani 1×1,
    /// nebo když explicitní mřížka přesahuje list.
    /// </summary>
    public static (int Rows, int Cols) ResolveGrid(string gridKey, float pageW, float pageH, float sheetW, float sheetH)
    {
        int maxRows = (int)(sheetH / pageH);
        int maxCols = (int)(sheetW / pageW);

        if (maxRows < 1 || maxCols < 1)
            throw new ArgumentException(
                $"Stránka {pageW:0.#}×{pageH:0.#} mm se na list {sheetW:0.#}×{sheetH:0.#} mm nevejde (bez zmenšování).");

        if (gridKey is "" or SheetLayoutKeys.GridAuto)
            return (maxRows, maxCols);

        var parts = gridKey.Split('x', 'X', '×');
        if (parts.Length != 2 || !int.TryParse(parts[0], out int rows) || !int.TryParse(parts[1], out int cols)
            || rows < 1 || cols < 1)
            throw new ArgumentException($"Neplatná mřížka listu: {gridKey}");

        if (rows > maxRows || cols > maxCols)
            throw new ArgumentException(
                $"Mřížka {rows}×{cols} se stránkou {pageW:0.#}×{pageH:0.#} mm se na list {sheetW:0.#}×{sheetH:0.#} mm nevejde 1:1 (max {maxRows}×{maxCols}).");

        return (rows, cols);
    }

    /// <summary>
    /// Stránky všech dokladů za sebou, jak půjdou do <see cref="Compose"/>. Při oboustranném tisku
    /// se doklad s lichým počtem stránek dorovná prázdnou stránkou (`B183`).
    /// </summary>
    /// <remarks>
    /// Skládání páruje líc a rub po dvojicích stránek. Doklad bez rubu, nebo s obsahem přeteklým na
    /// třetí stránku, by bez dorovnání posunul líce a ruby u všech dalších dokladů. Hranici dokladu
    /// zná jen volající (jedna datová stránka agendy = jeden doklad), proto se to děje tady a ne
    /// v <see cref="Compose"/>.
    /// </remarks>
    public static List<Page> StrankyDokladu(IEnumerable<IReadOnlyList<Page>> doklady, SheetLayoutOptions o)
    {
        var stranky = new List<Page>();
        foreach (var doklad in doklady)
        {
            stranky.AddRange(doklad);
            if (o.Oboustranne && !o.RepeatEachPage && doklad.Count % 2 == 1)
                stranky.Add(new Page { PageNumber = doklad.Count + 1 });
        }
        return stranky;
    }

    /// <summary>
    /// Složí logické stránky na listy. Sekvenčně po řádcích (zleva doprava, shora dolů);
    /// <see cref="SheetLayoutOptions.RepeatEachPage"/> = každá stránka vyplní celý list.
    /// Poslední list může mít prázdné pozice (lichý počet stránek).
    /// </summary>
    public static List<Sheet> Compose(IReadOnlyList<Page> pages, SheetLayoutOptions o)
    {
        var sheets = new List<Sheet>();
        if (pages.Count == 0) return sheets;

        if (o.RepeatEachPage)
        {
            foreach (var page in pages)
                sheets.Add(new Sheet([.. AllSlots(o).Select(s => new PlacedPage(page, s.X, s.Y))]));
            return sheets;
        }

        var slots = AllSlots(o).ToList();
        if (o.Oboustranne)
        {
            // Rub jde na totéž místo jako jeho líc: při převracení listu na delší straně se list
            // otočí kolem vodorovné osy a levá polovina zůstane vlevo (ověřeno jen úvahou, ne tiskem).
            for (int i = 0; i < pages.Count; i += 2 * slots.Count)
            {
                var lice = new List<PlacedPage>();
                var ruby = new List<PlacedPage>();
                for (int s = 0; s < slots.Count && i + 2 * s < pages.Count; s++)
                {
                    lice.Add(new PlacedPage(pages[i + 2 * s], slots[s].X, slots[s].Y));
                    if (i + 2 * s + 1 < pages.Count)
                        ruby.Add(new PlacedPage(pages[i + 2 * s + 1], slots[s].X, slots[s].Y));
                }
                sheets.Add(new Sheet(lice));
                // Rubová strana listu existuje, i když je prázdná - jinak by se další líc
                // vytiskl na rub předchozího listu.
                sheets.Add(new Sheet(ruby));
            }
            return sheets;
        }

        for (int i = 0; i < pages.Count; i += slots.Count)
        {
            var placed = new List<PlacedPage>();
            for (int s = 0; s < slots.Count && i + s < pages.Count; s++)
                placed.Add(new PlacedPage(pages[i + s], slots[s].X, slots[s].Y));
            sheets.Add(new Sheet(placed));
        }
        return sheets;
    }

    private static IEnumerable<(float X, float Y)> AllSlots(SheetLayoutOptions o)
    {
        for (int r = 0; r < o.Rows; r++)
            for (int c = 0; c < o.Cols; c++)
                yield return (o.BlockOriginX + c * o.PageWidth, o.BlockOriginY + r * o.PageHeight);
    }

    private static bool ParseBool(object? v) => v is true
        || (v is string s && (s == "1" || string.Equals(s, "true", StringComparison.OrdinalIgnoreCase)));
}
