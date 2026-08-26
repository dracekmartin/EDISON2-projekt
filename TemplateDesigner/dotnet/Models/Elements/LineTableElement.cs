using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SkiaSharp;
using TemplateDesigner.Models.Expressions;
using TemplateDesigner.Rendering;

namespace TemplateDesigner.Models.Elements;

/// <summary>
/// Linkový jízdní řád - celá linka v jedné tabulce: svislý seznam zastávek
/// (tarifní čísla) × vodorovné sloupce jednotlivých spojů s časy v buňkách.
/// Datový zdroj: agenda LINKOVY_JR_AD / LINKOVY_JR_MHD (kolekce ZASTAVKY + SPOJE + SKUPINY_DNU).
///
/// Sloupce (`Columns`, model <see cref="StopColumn"/> - sdílený se StopList) definují popisové
/// sloupce: Zóna, Km, Tč, Zastávka, MHD + „Spoj" (řídí šířku/styl časových sloupců spojů, které
/// vznikají z dat). Editace přes 2-tab dialog (Obecné = EDInputForm, Sloupce = sdílený
/// `ColumnListEditor` + `CellStyleEditor`) - konzistentní se StopList/DepartureGrid.
///
/// První řez: bez 2D bin-packingu přes stránky (stránkuje PageLayoutEngine per sekce),
/// symboly jako textový fallback (Timetable font = roadmap 42b).
/// </summary>
public class LineTableElement : ScheduleElementBase, IRowHighlightable, IColumnLayoutOwner,
    INedelitelneDily
{
    // ─── DataSource ──────────────────────────────────────────────────────────
    // Vlastní DataSource klíče (linkový formát) - NE zastávkové z base. LineTable dědí jen
    // generickou vrstvu ScheduleElementBase, zastávkový balast (StopScheduleElementBase) netahá.

    /// <summary>Klíč kolekce zastávek (řádky matice).</summary>
    public string ZastavkyDataSource { get; set; } = "ZASTAVKY";

    /// <summary>Klíč kolekce spojů (časové sloupce matice).</summary>
    public string SpojeDataSource { get; set; } = "SPOJE";

    /// <summary>Klíč kolekce skupin dnů.</summary>
    public string SkupinyDnuDataSource { get; set; } = "SKUPINY_DNU";

    // ─── Sloupce (pořadí, šířky, viditelnost, per-column styl) - sdílený StopColumn model ──

    public List<StopColumn> Columns { get; set; } = GetDefaultColumns();

    /// <summary>Klíč sloupce časů spojů - řídí šířku/styl všech spojových sloupců (vznikají z dat).</summary>
    public const string TripKey = ColumnKeys.Trip;

    public static List<StopColumn> GetDefaultColumns() =>
    [
        new() { Key = ColumnKeys.Zone,     Header = "Zóna",     Width = 8f,  Visible = true, Alignment = TextHorizontalAlignment.Center },
        new() { Key = ColumnKeys.Km,       Header = "km",       Width = 6f,  Visible = true, Alignment = TextHorizontalAlignment.Center },
        new() { Key = ColumnKeys.Tc,       Header = "Tč",       Width = 6f,  Visible = true, Alignment = TextHorizontalAlignment.Center },
        new() { Key = ColumnKeys.StopName, Header = "Zastávka", Width = 42f, Visible = true, Alignment = TextHorizontalAlignment.Left },
        new() { Key = ColumnKeys.Mhd,      Header = "MHD",      Width = 8f,  Visible = true, Alignment = TextHorizontalAlignment.Center },
        new() { Key = TripKey,             Header = "Spoj",     Width = null, Visible = true, Alignment = TextHorizontalAlignment.Center },
    ];

    // ─── Řádky ──────────────────────────────────────────────────────────────

    /// <summary>Řádek znaků týdenní platnosti (X, 6, 7, +) nad časy spojů.</summary>
    public bool ShowPlatnostRow { get; set; } = true;
    /// <summary>Řádek negativních značek (NZ) nad časy spojů.</summary>
    public bool ShowNzRow { get; set; } = true;
    /// <summary>Přípojné řádky Vyčká/Navazuje vložené za zastávku.</summary>
    public bool ShowPripojeRows { get; set; } = true;

    /// <summary>
    /// Dorovnat neúplný blok prázdnými sloupci, aby měly všechny bloky stejný počet sloupců.
    /// </summary>
    /// <remarks>
    /// Poslední blok bývá neúplný a nechá vpravo volné místo. Starý modul umí to místo vyplnit
    /// prázdnými sloupci, takže tabulka končí rovně a bloky vypadají stejně (#8372; v legacy
    /// „padding sloupce", které sjednocují počet sloupců napříč stránkami).
    /// </remarks>
    public bool PadEmptyColumns { get; set; }
    /// <summary>Pás názvů skupin dnů (Pracovní dny / Soboty / …) nad čísly spojů.</summary>
    public bool ShowGroupHeaders { get; set; } = true;

    /// <summary>
    /// Nadpis nad popisnými sloupci (zóna, km, Tč, zastávka, MHD) v pásu skupin dnů - vzor IDZK 940
    /// tam má „↓ Směr jízdy ↓". Prázdné = pás nad nimi zůstane bez textu, jako dosud.
    /// </summary>
    /// <remarks>
    /// Popisné sloupce jsou <b>jedna skupina</b> (změřeno na vzoru 940: km, zastávka a zóny mají
    /// jeden společný nadpis, ne každý svůj) - proto jeden text, ne text per sloupec.
    /// </remarks>
    public string PopisneSloupceNadpis { get; set; } = "";

    /// <summary>
    /// Popisné sloupce se kreslí jako <b>jeden slitý blok</b> - svislé čáry mezi nimi se potlačí,
    /// mezi sloupci spojů zůstanou. Vzor 940 to tak má (km má dva podsloupce a nejsou oddělené).
    /// </summary>
    /// <remarks>
    /// ⚠️ Bez tohohle šel vypnout jen <b>celý</b> <c>Grid.ShowColumnLines</c>, tedy i čáry mezi
    /// spoji - a to je právě ta stížnost, že „ohraničení jednotlivých km sloupců nejde odstranit".
    /// </remarks>
    public bool PopisneSloupceBezVnitrnichCar { get; set; }

    // ─── Vzhled ─────────────────────────────────────────────────────────────

    /// <summary>Podbarvení řádků s lichým tarifním číslem (legacy 25% šedá).</summary>
    public bool HighlightOddTc { get; set; } = true;

    /// <summary>Vlastní definice sloupců spojů. Null nebo prázdné = server rozdělí spoje podle
    /// hrubého přepínače dělení, jako dosud.</summary>
    /// <remarks>
    /// ⚠️ Definice je táž jako u vývěskových odjezdů (i wire tvar), ale **spotřebuje se jinak**:
    /// tady je skupina <b>pás nad několika sloupci spojů</b> a spoj patřící do dvou skupin se
    /// vypíše <b>dvakrát</b>. Význam definice drží server, aby se obě rodiny agend nerozešly.
    /// </remarks>
    public List<DepartureColumnDefinition>? CustomColumns { get; set; }

    /// <summary>Wire JSON definice sloupců pro parametr DEFINICE_SLOUPCU.</summary>
    public string? BuildColumnsDefinitionWireJson() => ColumnLayoutWire.Build(CustomColumns);

    /// <summary>Styl přípojných řádků (vyčká/navazuje). Nevyplněné = dědí se z datových buněk.</summary>
    /// <remarks>
    /// #8360 (KU HK): přípojný řádek má být od časů spojů odlišitelný - obvykle menším a kurzívním
    /// písmem, aby v tabulce „nekřičel" stejně jako odjezdy. Je to <b>vlastnost šablony</b>, ne dat:
    /// data nesou, že přípoj existuje, jak vypadá rozhoduje návrhář.
    /// </remarks>
    public TextStyle PripojStyle { get; set; } = new();
    public string OddTcBackColor { get; set; } = "#FFEEEEEE";

    // Podmíněné řádkové featury (sdílené přes RowConditions, výraz nad daty zastávky = stop.SourceRow).
    // Prázdný výraz = vypnuto (nulová změna chování).
    /// <summary>Zastávka, kde výraz neplatí, se nevykreslí (řádek + její přípojné řádky; nezapočítá do výšky).</summary>
    public string? RowVisibilityExpression { get; set; }
    /// <summary>Podmíněné zvýraznění: seznam pravidel (výraz → barva pozadí), první splněné vyhrává.
    /// Null/prázdné = bez zvýraznění.</summary>
    public List<HighlightRule>? HighlightRules { get; set; }

    protected override float DesignerPlaceholderHeight => 60f;

    // ─── Runtime data (neserializují se) ────────────────────────────────────

    public sealed class LineStopData
    {
        public int Poradi;
        public string Typ = "";        // "zastavka" (hlavní/odjezdový) | "prijezd" (vložený příjezdový)
        public string Popis = "";
        public string Tc = "";
        public string Zona = "";
        public string Mhd = "";
        public string Nastupiste = "";
        public List<string> Atributy = [];
        public List<string> Km = [];

        /// <summary>Zdrojový řádek dat zastávky - pro vyhodnocení řádkových podmínek (zvýraznění). Neserializuje se.</summary>
        public DataContext? SourceRow;

        public bool JePrijezdovy => Typ == "prijezd";
    }

    public sealed class LinePripojData
    {
        public string Typ = "";      // "vycka" | "navazuje"
        public int Poradi;
        public string Text = "";
        public string Cas = "";
        public string Linka = "";
        public string Spoj = "";
        public bool Radek;           // legacy „typ zobrazení": tisknout jako vložený řádek matice
    }

    public sealed class LineTripData
    {
        public string CisloSpoje = "";
        public int Skupina = 1;
        public string Nz = "";
        /// <summary>Barva textu časů spoje (prezentační nápověda z dat, R3). Null = řeší šablona.
        /// Server ji posílá jen u spoje, který barvu skutečně má, a jen když ji linka nepotlačila.</summary>
        public string? ForeColor;
        public List<string> Platnost = [];
        public List<string> Casy = [];
        public List<LinePripojData> Pripoje = [];
    }

    public sealed class LineSkupinaData
    {
        public int Id = 1;
        public string Nadpis = "";
    }

    // vlastní cache (base cache je pro zastávkový formát; tady jiné kolekce)
    [JsonIgnore] private DataContext? _lineCacheRef;
    [JsonIgnore] private List<LineStopData>? _cachedStops;
    [JsonIgnore] private List<LineTripData>? _cachedTrips;
    [JsonIgnore] private List<LineSkupinaData>? _cachedGroups;

    private void EnsureParsed(DataContext data)
    {
        if (ReferenceEquals(data, _lineCacheRef) && _cachedStops is not null) return;
        _lineCacheRef = data;

        _cachedStops = [.. data.GetRows(ZastavkyDataSource).Select(row => new LineStopData
        {
            Poradi = ParseInt(row.GetString("PORADI")),
            Typ = row.GetString("TYP"),
            Popis = row.GetString("NAZEV_ZASTAVKY"),
            Tc = row.GetString("TC"),
            Zona = row.GetString("ZONY"),
            Mhd = row.GetString("MHD"),
            Nastupiste = row.GetString("NASTUPISTE"),
            Atributy = ParseStringArray(row.GetValue("ATRIBUTY")),
            Km = ParseStringArray(row.GetValue("KM")),
            SourceRow = row,
        })];

        _cachedTrips = [.. data.GetRows(SpojeDataSource).Select(row => new LineTripData
        {
            CisloSpoje = row.GetString("CISLO_SPOJE"),
            Skupina = ParseInt(row.GetString("SKUPINA")),
            Nz = row.GetString("NZ"),
            ForeColor = row.GetString("FORE_COLOR") is { Length: > 0 } fc ? fc : null,
            Platnost = ParseStringArray(row.GetValue("PLATNOST")),
            Casy = ParseStringArray(row.GetValue("CASY")),
            Pripoje = ParsePripoje(row.GetValue("PRIPOJE")),
        })];

        _cachedGroups = [.. data.GetRows(SkupinyDnuDataSource).Select(row => new LineSkupinaData
        {
            Id = ParseInt(row.GetString("ID")),
            Nadpis = row.GetString("NADPIS"),
        })];
    }

    private static List<string> ParseStringArray(object? value) =>
        value is JsonArray arr ? [.. arr.Select(n => n?.ToString() ?? "")] : [];

    private static List<LinePripojData> ParsePripoje(object? value)
    {
        if (value is not JsonArray arr) return [];
        return [.. arr.OfType<JsonObject>().Select(o => new LinePripojData
        {
            Typ = o["TYP"]?.ToString() ?? "",
            Poradi = int.TryParse(o["PORADI"]?.ToString(), out var p) ? p : 0,
            Text = o["TEXT"]?.ToString() ?? "",
            Cas = o["CAS"]?.ToString() ?? "",
            Linka = o["LINKA"]?.ToString() ?? "",
            Spoj = o["SPOJ"]?.ToString() ?? "",
            Radek = o["RADEK"]?.ToString() == "1",
        })];
    }

    /// <summary>Co přípojný řádek dědí, když si nic nenastaví - datové buňky.
    /// Čte to i drift test formuláře, aby ověřil, že panel neukazuje jinou hodnotu, než se vykreslí.</summary>
    public ResolvedTextStyle ResolveElementPripojParent() => ResolveColumnDataParent();

    /// <summary>Vyřešený styl přípojného řádku - dědí z datových buněk, dokud si ho někdo nepřebije.</summary>
    public ResolvedTextStyle ResolvePripojStyle() => PripojStyle.Resolve(ResolveElementPripojParent());

    /// <summary>Výška přípojného řádku. Řídí se <b>vlastním</b> písmem, aby menší přípoj zabral
    /// míň místa - proto se nepočítá z výšky datového řádku.</summary>
    private float PripojRowH() => DataRowHeight ?? ComputeRowH(ResolvePripojStyle().FontSize);

    /// <summary>Tarifní čísla, která mají odjezdový řádek zastávky.</summary>
    private static HashSet<int> PoradiSOdjezdem(IEnumerable<LineStopData> stops) =>
        [.. stops.Where(s => !s.JePrijezdovy).Select(s => s.Poradi)];

    /// <summary>Vypisují se u tohohle řádku přípojné řádky?</summary>
    /// <remarks>
    /// Přípoje se párují na <b>tarifní číslo</b>, ne na řádek. U zastávky rozdvojené na příjezd
    /// a odjezd sedí obě půlky na totéž číslo, takže se celý blok přípojů vypsal <b>dvakrát</b> -
    /// jednou nad zastávkou a jednou pod ní (nahlásil Jan 24. 8. na lince 330511). Patří
    /// k <b>odjezdovému</b> řádku: „navazuje" i „vyčká" se vztahují k odjezdu.
    /// Zastávka, která má jen příjezdový řádek (konečná), si je nechává - jinak by zmizely.
    /// </remarks>
    private static bool NesePripoje(LineStopData stop, HashSet<int> poradiSOdjezdem) =>
        !stop.JePrijezdovy || !poradiSOdjezdem.Contains(stop.Poradi);

    // ─── Style resolution (kaskáda column → element → CellDefaults; vzor StopList) ─────────

    /// <summary>Parent kaskády pro per-column header override (co sloupec dědí bez vlastního stylu).</summary>
    public ResolvedTextStyle ResolveColumnHeaderParent() => HeaderStyle.Resolve(ScheduleHeaderDefaults);

    /// <summary>Parent kaskády pro per-column data override.</summary>
    public ResolvedTextStyle ResolveColumnDataParent() => DataStyle.Resolve(ScheduleDataDefaults);

    // Kaskáda column → element → baked delegována do sdíleného jádra (TableLayout.ResolveCascade).
    private ResolvedTextStyle ResolveHeaderStyle(StopColumn col) =>
        TableLayout.ResolveCascade(col.Style?.Header, ResolveColumnHeaderParent(), col.Alignment,
            elementDefinesHAlign: HeaderStyle.HAlign is not null);

    private ResolvedTextStyle ResolveDataStyle(StopColumn col) =>
        TableLayout.ResolveCascade(col.Style?.Data, ResolveColumnDataParent(), col.Alignment,
            elementDefinesHAlign: DataStyle.HAlign is not null);

    // ─── Layout ─────────────────────────────────────────────────────────────

    /// <summary>Vyřešený popisový sloupec připravený k renderu (šířka + styly + hodnota z dat).</summary>
    /// <summary>Nouzové dno šířky sloupce (mm) pro případ, že se písmo nepodaří změřit.</summary>
    private const float MinAutoColumnWidthMm = 3f;

    /// <summary>
    /// Nejmenší šířka automatického sloupce (mm) - tolik, aby se do ní vešel čas „00:00" i s odsazením.
    /// </summary>
    /// <remarks>
    /// Dokud tu stála pevná 3 mm, řídila **kde se tabulka rozřízne**: dělení nasypalo do bloku tolik
    /// spojů, kolik se jich při té šířce vešlo - a čas se do 3 mm nevejde v žádném písmu, takže bloky
    /// sice vycházely, ale byly nečitelné (nahlásil Jan 25. 8.). Dno musí vycházet z písma, jinak
    /// zvětšení fontu tabulku jen zhustí, místo aby ubralo spojů na blok.
    /// </remarks>
    private float MinAutoSloupecMm()
    {
        var st = ResolveDataStyle(TripColumn());
        using var font = new SKFont(GetTypeface(st), st.FontSize);   // velikost v pt → míra vyjde v pt
        float mm = (font.MeasureText("00:00") * PtToMm) + st.PaddingLeft + st.PaddingRight;
        return MathF.Max(MinAutoColumnWidthMm, mm);
    }

    private sealed record RenderCol(float Width, string Header, ResolvedTextStyle HeaderStyle,
        ResolvedTextStyle DataStyle, Func<LineStopData, string> Value);

    private List<StopColumn> StructuralColumns() =>
        Columns.Where(c => c.Visible && c.Key != TripKey).ToList();

    private StopColumn TripColumn() =>
        Columns.FirstOrDefault(c => c.Key == TripKey) ?? new StopColumn { Key = TripKey, Width = null };

    private static string DefaultHeaderFor(string key) => key switch
    {
        ColumnKeys.Zone => "Zóna", ColumnKeys.Km => "km", ColumnKeys.Tc => "Tč",
        ColumnKeys.StopName => "Zastávka", ColumnKeys.Mhd => "MHD", _ => ""
    };

    /// <summary>Hodnota buňky popisového sloupce dle klíče (kmIndex jen pro Km sub-sloupce).</summary>
    private Func<LineStopData, string> ValueFor(string key, int kmIndex) => key switch
    {
        ColumnKeys.Zone     => s => s.JePrijezdovy ? "" : s.Zona,
        ColumnKeys.Km       => s => s.JePrijezdovy || kmIndex >= s.Km.Count ? "" : s.Km[kmIndex],
        ColumnKeys.Tc       => s => s.JePrijezdovy ? "" : s.Tc,
        ColumnKeys.Mhd      => s => s.JePrijezdovy ? "" : s.Mhd,
        ColumnKeys.StopName => s => s.JePrijezdovy ? "" :
                           s.Atributy.Count > 0
                               ? string.Join("", s.Atributy.Where(IsDisplayableStopAttribute)) + " " + s.Popis
                               : s.Popis,
        _          => _ => ""
    };

    /// <summary>
    /// Sestaví vyřešené popisové sloupce (Km expandované na kmCount sub-sloupců) + spočítá šířku
    /// spojového sloupce. Auto (Width==null) sdílí zbytek šířky s auto-spoji.
    /// <para>
    /// <b>Prázdný sloupec se netiskne.</b> Když pro něj nemá hodnotu ani jedna zastávka, zmizí -
    /// stejně jako to dělá starý modul. Je to zároveň cesta, kterou se uplatní nastavení u linky:
    /// když linka řekne „netiskni tarifní čísla", server hodnoty nepošle a sloupec se schová sám,
    /// takže o tom nemusí vědět ani šablona, ani prvek. Dřív to platilo jen pro km a MHD.
    /// </para>
    /// </summary>
    private (List<RenderCol> Fixed, float TripWidth, int NameIndex) BuildColumns(
        List<LineStopData> stops, int kmCount, int tripCount)
    {
        var structural = StructuralColumns();
        var trip = TripColumn();

        // Km má vlastní podmínku - jeho hodnoty jsou pole, ne jedna hodnota, a počet sub-sloupců
        // se odvozuje z dat (kmCount == 0 znamená „žádné km sloupce", ne „prázdné hodnoty").
        structural = structural.Where(c => c.Key == ColumnKeys.Km
            ? kmCount > 0
            : stops.Any(st => ValueFor(c.Key, 0)(st).Length > 0)).ToList();

        // šířky: pevné (Width != null) honorovány; auto (null) sdílí zbytek se spoji
        float fixedSum = structural.Where(c => c.Width.HasValue).Sum(c => c.Key == ColumnKeys.Km ? c.Width!.Value * kmCount : c.Width!.Value);
        int autoStructural = structural.Count(c => !c.Width.HasValue);
        int autoUnits = autoStructural + (trip.Width.HasValue ? 0 : tripCount);
        float autoEach = autoUnits > 0 ? MathF.Max(MinAutoSloupecMm(), (Width - fixedSum) / autoUnits) : 8f;

        var result = new List<RenderCol>();
        int nameIndex = -1;
        foreach (var col in structural)
        {
            var hs = ResolveHeaderStyle(col);
            var ds = ResolveDataStyle(col);
            string header = col.Header != "" ? col.Header : DefaultHeaderFor(col.Key);
            float w = col.Width ?? autoEach;
            if (col.Key == ColumnKeys.Km)
            {
                for (int k = 0; k < kmCount; k++)
                    result.Add(new RenderCol(w, header, hs, ds, ValueFor(ColumnKeys.Km, k)));
            }
            else
            {
                if (col.Key == ColumnKeys.StopName) nameIndex = result.Count;
                result.Add(new RenderCol(w, header, hs, ds, ValueFor(col.Key, 0)));
            }
        }
        float tripW = trip.Width ?? autoEach;
        return (result, tripW, nameIndex);
    }

    /// <summary>Mezera mezi stránkovými bloky, ať na sebe tabulky nenavazují bez předělu.</summary>
    private const float MezeraMeziBlokyMm = 4f;

    /// <summary>
    /// Rozdělení sloupců spojů do stránkových bloků - <b>místo dřívějšího mačkání</b>.
    /// </summary>
    /// <remarks>
    /// <para>Do 25. 8. se tabulka, která se nevešla, celá <b>zmenšila</b> (sloupce i písmo), takže
    /// u linky se stovkou spojů nebylo vidět nic. Starý modul místo toho <b>řeže</b> - a protože
    /// prvek má automatickou výšku a jeho sekce se umí rozdělit přes stránky, stačí bloky vysázet
    /// <b>pod sebe</b> a stránkování je rozřeže samo.</para>
    /// <para>Šířka sloupce spoje se pro dělení bere <b>minimální</b> - tolik se jich vejde nejvíc.
    /// Při kreslení se pak sloupce toho bloku roztáhnou na celou šířku, takže se blok nedrží
    /// při levém okraji.</para>
    /// </remarks>
    private List<TableLayout.SloupcovyBlok> SloupcoveBloky(List<LineStopData> stops, int kmCount, int pocetSpoju)
    {
        if (pocetSpoju <= 0) return [];

        // Kolik spojů se vejde, **nejde spočítat jedním výpočtem**: popisné sloupce jsou z větší
        // části automatické, takže jejich šířka závisí na tom, kolik spojů s nimi sdílí místo.
        // Hledá se proto největší počet, který se **při vlastním rozvržení** opravdu vejde - kdyby
        // se pravidlo pro šířky sloupců změnilo, dělení se přizpůsobí samo místo aby se rozešlo.
        // O čitelnost bloku se stará dno šířky sloupce, viz `MinAutoSloupecMm`.
        int naBlok = NejvicSpojuNaBlok(stops, kmCount, pocetSpoju);
        var bloky = new List<TableLayout.SloupcovyBlok>();
        for (int od = 0; od < pocetSpoju; od += naBlok)
            bloky.Add(new TableLayout.SloupcovyBlok(od, Math.Min(naBlok, pocetSpoju - od)));
        return bloky;
    }

    /// <summary>Jak vyjde jeden blok: které spoje nese, jak je široký a kolik místa zbylo
    /// na jeden sloupec spoje.</summary>
    internal readonly record struct BlokRozvrh(int Od, int Pocet, float SirkaMm, float SloupecSpojeMm);

    /// <summary>
    /// Rozřezání tabulky do bloků i s výslednou šířkou každého z nich.
    /// </summary>
    /// <remarks>
    /// Existuje kvůli jediné podmínce, kterou nejde ověřit zvenčí a jejíž porušení je hned vidět
    /// v tisku: <b>každý blok se musí do šířky prvku vejít</b>. Když se počet spojů na blok počítal
    /// z popisných sloupců zmáčknutých na minimum, první blok jich pobral skoro všechny a přetekl.
    /// </remarks>
    internal IReadOnlyList<BlokRozvrh> Rozvrzeni(DataContext data)
    {
        EnsureParsed(data);
        var stops = _cachedStops!;
        var trips = _cachedTrips!;
        if (stops.Count == 0 || trips.Count == 0) return [];

        int kmCount = MaxKmCount(stops);
        var bloky = SloupcoveBloky(stops, kmCount, trips.Count);
        int naBlok = bloky.Count > 0 ? bloky.Max(b => b.Pocet) : 0;
        return bloky.Select(b =>
        {
            var (cols, tripW, _) = BuildColumns(stops, kmCount, naBlok);
            return new BlokRozvrh(b.Od, b.Pocet, cols.Sum(c => c.Width) + (b.Pocet * tripW), tripW);
        }).ToList();
    }

    /// <summary>Největší počet spojů, který se do šířky vejde i s popisnými sloupci spočítanými
    /// pro týž počet. Půlením intervalu, ať se `BuildColumns` nevolá pro každý počet zvlášť.</summary>
    private int NejvicSpojuNaBlok(List<LineStopData> stops, int kmCount, int pocetSpoju)
    {
        bool Vejde(int n)
        {
            var (cols, tripW, _) = BuildColumns(stops, kmCount, n);
            return cols.Sum(c => c.Width) + n * tripW <= Width + 0.01f;
        }

        if (Vejde(pocetSpoju)) return pocetSpoju;

        int dolni = 1, horni = pocetSpoju;
        while (dolni < horni)
        {
            int stred = (dolni + horni + 1) / 2;
            if (Vejde(stred)) dolni = stred; else horni = stred - 1;
        }
        return dolni;   // aspoň jeden, jinak by dělení nikdy neskončilo
    }

    /// <summary>Atributy trasy k zobrazení před názvem - jen významové značky pro cestujícího
    /// (x = na znamení). Řídicí atributy (T/P/S/E/…) se v prvním řezu netisknou.</summary>
    private static bool IsDisplayableStopAttribute(string znak) => znak == "x";

    private int MaxKmCount(List<LineStopData> stops) =>
        Columns.Any(c => c.Visible && c.Key == ColumnKeys.Km) && stops.Count > 0 ? stops.Max(s => s.Km.Count) : 0;

    private bool HasPlatnostRow(List<LineTripData> trips) => ShowPlatnostRow && trips.Any(t => t.Platnost.Count > 0);
    private bool HasNzRow(List<LineTripData> trips) => ShowNzRow && trips.Any(t => t.Nz.Length > 0);
    /// <summary>
    /// Kreslí se pás nad sloupci? Buď proto, že je co rozdělit na skupiny dnů, <b>nebo</b> proto,
    /// že má popisný blok svůj nadpis.
    /// </summary>
    /// <remarks>
    /// ⚠️ Ta druhá půlka podmínky není kosmetika: nadpis popisných sloupců je vlastnost
    /// <b>popisné skupiny</b>, ne skupin dnů. Bez ní by se „↓ Směr jízdy ↓" nevykreslil na lince,
    /// která má všechny spoje v jedné skupině dnů - a to je vazba, kterou by nikdo nehledal
    /// (odhalil test 26. 8., ne úvaha).
    /// </remarks>
    private bool HasGroupHeaderBand(List<LineTripData> trips, List<LineSkupinaData> groups) =>
        ShowGroupHeaders
        && ((trips.Select(t => t.Skupina).Distinct().Count() > 1 && groups.Any(g => g.Nadpis.Length > 0))
            || PopisneSloupceNadpis.Length > 0);

    /// <summary>Největší resolved font záhlaví/dat napříč viditelnými sloupci (vč. spoje) - pro výšky.</summary>
    private float MaxHeaderFontPt() => MaxFontPt(ResolveHeaderStyle, HeaderStyle.FontSize ?? 9f);
    private float MaxDataFontPt() => MaxFontPt(ResolveDataStyle, DataStyle.FontSize ?? 8f);

    private float MaxFontPt(Func<StopColumn, ResolvedTextStyle> resolve, float fallback)
    {
        var cols = Columns.Where(c => c.Visible).ToList();
        return cols.Count > 0 ? cols.Max(c => resolve(c).FontSize) : fallback;
    }

    public override float CalculateHeightFromData(DataContext? data)
    {
        if (data is null) return EffectiveDesignerHeight;
        EnsureParsed(data);
        var stops = _cachedStops!;
        var trips = _cachedTrips!;
        if (stops.Count == 0 || trips.Count == 0) return EffectiveDesignerHeight;

        // Bloky se sázejí pod sebe a **každý zopakuje celou tabulku zastávek**. Musí to být týž
        // výpočet jako v `Render`, jinak nesedí stránkování.
        var vysky = VyskyBloku();
        return vysky.Sum() + (MezeraMeziBlokyMm * (vysky.Count - 1));
    }

    /// <summary>Výška každého bloku zvlášť - bloky <b>nejsou stejně vysoké</b>, protože přípojné
    /// řádky patří ke spojům a ty jsou v každém bloku jiné. Volá se až po <c>EnsureParsed</c>.</summary>
    private List<float> VyskyBloku()
    {
        var trips = _cachedTrips!;
        return [.. SloupcoveBloky(_cachedStops!, MaxKmCount(_cachedStops!), trips.Count)
            .Select(b =>
            {
                var (hlava, radky) = VyskyRadkuBloku(trips.GetRange(b.Od, b.Pocet));
                return hlava.Sum() + radky.Sum();
            })];
    }

    /// <summary>
    /// Výšky řádků jednoho bloku ve stejném pořadí, v jakém je skládá <see cref="BuildRowPlan"/>:
    /// hlava (pás skupin, čísla spojů, platnost, negativní značky) a pod ní řádky zastávek
    /// i s jejich přípojnými podřádky.
    /// </summary>
    /// <remarks>
    /// Jeden zdroj pro výšku prvku i pro místa, kudy se smí řezat. Kdyby to byly dva výpočty,
    /// rozešly by se a stránkování by řezalo jinam, než kde jsou doopravdy hranice řádků.
    /// Volá se až po <c>EnsureParsed</c>.
    /// </remarks>
    private (List<float> Hlava, List<float> Radky) VyskyRadkuBloku(IReadOnlyList<LineTripData> spojeBloku)
    {
        var trips = _cachedTrips!;
        float headerH = ComputeHeaderH(MaxHeaderFontPt());
        float rowH = DataRowHeight ?? ComputeRowH(MaxDataFontPt());

        var hlava = new List<float>();
        if (HasGroupHeaderBand(trips, _cachedGroups!)) hlava.Add(headerH);   // pás skupin dnů
        hlava.Add(headerH);                                                  // řádek čísel spojů
        if (HasPlatnostRow(trips)) hlava.Add(rowH);
        if (HasNzRow(trips)) hlava.Add(rowH);

        // Jen viditelné zastávky a jejich přípojné řádky (matched na Poradi zastávky - jako render).
        var visibleStops = RowConditions.VisibleRows(_cachedStops!, s => s.SourceRow, RowVisibilityExpression);
        var sOdjezdem = ShowPripojeRows ? PoradiSOdjezdem(visibleStops) : null;
        var radky = new List<float>();
        foreach (var zastavka in visibleStops)
        {
            radky.Add(rowH);
            if (sOdjezdem is null || !NesePripoje(zastavka, sOdjezdem)) continue;
            // ⚠️ Jen ze spojů **tohohle bloku** - kreslí se taky tak. Počítat je ze všech spojů
            // linky znamenalo, že se bloky ve výpočtu posouvaly dopředu proti tomu, kde se opravdu
            // kreslí, a řez stránky padl doprostřed hlavičky dalšího bloku (nahlásil Jan 25. 8.:
            // na konci stránky osiřelý pás skupin, na další hlavička dvakrát).
            int pripoju = spojeBloku.Sum(t => t.Pripoje.Count(p => p.Radek && p.Poradi == zastavka.Poradi));
            for (int i = 0; i < pripoju; i++) radky.Add(PripojRowH());
        }
        return (hlava, radky);
    }

    /// <summary>Pokračovací stránka zopakuje hlavu bloku - pás skupin dnů, čísla spojů,
    /// platnost a negativní značky. Bez ní nejde poznat, který sloupec je který spoj.</summary>
    public float OpakovanaHlavickaMm(DataContext? data)
    {
        if (data is null) return 0f;
        EnsureParsed(data);
        if (_cachedStops!.Count == 0 || _cachedTrips!.Count == 0) return 0f;
        return VyskyRadkuBloku(_cachedTrips!).Hlava.Sum();
    }

    /// <summary>
    /// Řezat se má mezi bloky (silný zlom) - blok useknutý uprostřed je tabulka bez konce.
    /// Uvnitř bloku se nabízejí ještě hranice řádků zastávek (slabý zlom) pro případ, kdy je
    /// blok sám vyšší než stránka a rozříznout se musí; bez nich by řez padl doprostřed textu.
    /// </summary>
    public IReadOnlyList<Zlom> ZlomyMm(DataContext? data)
    {
        if (data is null) return [];
        EnsureParsed(data);
        if (_cachedStops!.Count == 0 || _cachedTrips!.Count == 0) return [];

        var trips = _cachedTrips!;
        var bloky = SloupcoveBloky(_cachedStops!, MaxKmCount(_cachedStops!), trips.Count);

        var zlomy = new List<Zlom>();
        float zacatekBloku = 0f;
        for (int b = 0; b < bloky.Count; b++)
        {
            var (hlava, radky) = VyskyRadkuBloku(trips.GetRange(bloky[b].Od, bloky[b].Pocet));
            if (b > 0) zlomy.Add(new Zlom(zacatekBloku, Silny: true));

            // hranice mezi řádky zastávek - první vynechat, jinak by zůstala hlava sama na stránce
            float y = zacatekBloku + hlava.Sum();
            for (int i = 0; i < radky.Count - 1; i++)
            {
                y += radky[i];
                zlomy.Add(new Zlom(y, Silny: false));
            }
            zacatekBloku += hlava.Sum() + radky.Sum() + MezeraMeziBlokyMm;
        }
        return zlomy;
    }

    // ─── Render ─────────────────────────────────────────────────────────────

    public override void Render(RenderContext ctx)
    {
        if (Width <= 0) return;

        using var paints = CreateStandardPaints(ctx);

        if (ctx.Data is null)
        {
            RenderPlaceholder(ctx, paints);
            return;
        }

        EnsureParsed(ctx.Data);
        var stops = _cachedStops!;
        var trips = _cachedTrips!;
        var groups = _cachedGroups!;
        if (stops.Count == 0 || trips.Count == 0)
        {
            RenderPlaceholder(ctx, paints);
            return;
        }

        // element-level styly pro pás skupin / řádek platnosti / NZ (nejsou vázané na sloupec)
        var headerResolved = ResolveColumnHeaderParent();
        var dataResolved = ResolveColumnDataParent();
        var headerCentered = headerResolved with { HAlign = TextHorizontalAlignment.Center };
        var dataCentered = dataResolved with { HAlign = TextHorizontalAlignment.Center };

        int kmCount = MaxKmCount(stops);
        var tripCol = TripColumn();
        var tripHeaderStyle = ResolveHeaderStyle(tripCol);
        var tripDataStyle = ResolveDataStyle(tripCol);
        var bloky = SloupcoveBloky(stops, kmCount, trips.Count);

        float scale = ComputeFontScale(CalculateHeightFromData(ctx.Data));

        float vyskaObsahu = Height ?? CalculateHeightFromData(ctx.Data);

        // ⚠️ Kreslí-li stránka zopakovanou hlavičku, je obsah posunutý o její výšku dolů - a tím
        // by se do viditelného pásu dostal **konec předchozí stránky** a prosvítal zpod hlavičky
        // (nahlásil Jan 25. 8.: „obrovská mezera mezi hlavičkou a daty"; ve skutečnosti přes sebe
        // ležely tři řádky zastávek). Obsah se proto ořízne až od řezu; hlavička se kreslí zvlášť
        // nad ním, mimo tenhle ořez.
        float orezOd = ctx.OpakovanaHlavickaMm > 0.01f ? MathF.Max(Y, ctx.ClipTopMm) : Y;
        var clipRect = ctx.ToCanvasRect(X, orezOd, Width, Y + vyskaObsahu - orezOd);
        List<RowSpec>? hlavaKZopakovani = null;
        ctx.Canvas.Save();
        ctx.Canvas.ClipRect(clipRect);
        try
        {
            // ⚠️ Šířky se počítají pro **nejplnější** blok, ne pro ten kreslený, a platí pro
            // všechny stejně. Podle vlastního počtu spojů vycházel poslední blok se čtyřmi spoji
            // se sloupci 58,8 mm proti 6,4 mm v prvním - čas uprostřed šesticentimetrového pole
            // (změřeno 25. 8.). Zbylé místo vpravo zůstane prázdné, jako ve starém tisku; jeho
            // dorovnání prázdnými sloupci je samostatné přání zákazníka (#8372).
            int naBlok = bloky.Max(b => b.Pocet);
            float y = Y;
            foreach (var blok in bloky)
            {
                var (fixedCols, tripW, nameIndex) = BuildColumns(stops, kmCount, naBlok);
                var spojeBloku = trips.GetRange(blok.Od, blok.Pocet);

                float headerH = ComputeHeaderH(MaxHeaderFontPt()) * scale;
                float rowH = (DataRowHeight ?? ComputeRowH(MaxDataFontPt())) * scale;
                float fixedSum = fixedCols.Sum(c => c.Width);

                var plan = BuildRowPlan(stops, spojeBloku, trips, groups, fixedCols, tripW, nameIndex, fixedSum,
                    headerH, rowH, PripojRowH() * scale, paints.Grid, headerResolved, headerCentered,
                    tripHeaderStyle, tripDataStyle,
                    ctx, GetTypeface(FontResolver.SymbolFontFamily(ctx.Data?.Culture)), scale);

                // Neúplný blok dorovnat prázdnými sloupci, ať mají všechny bloky stejný počet
                // sloupců a tabulka končí rovně (#8372). Styl podle druhu řádku, jinak by prázdné
                // buňky v záhlaví vypadly z jeho podbarvení.
                int prazdnych = PadEmptyColumns ? naBlok - blok.Pocet : 0;
                if (prazdnych > 0)
                    plan = [.. plan.Select(r => r with
                    {
                        Cells = [.. r.Cells, .. Enumerable.Repeat(
                            new ColumnRenderer.CellSpec(tripW, "",
                                r.Kind is RowKind.Header or RowKind.GroupHeader ? tripHeaderStyle : tripDataStyle),
                            prazdnych)]
                    })];

                float konecBloku = y + plan.Sum(r => r.HeightMm);

                // Řez padl doprostřed tohohle bloku → jeho hlavu zopakovat nad pokračováním.
                // Kreslí se až po obsahu, mimo jeho ořez - viz `orezOd` výše.
                if (ctx.OpakovanaHlavickaMm > 0.01f && ctx.ClipTopMm > y && ctx.ClipTopMm < konecBloku)
                    hlavaKZopakovani = [.. plan.Take(VyskyRadkuBloku(spojeBloku).Hlava.Count)];

                y = RowPlanRenderer.DrawRows(ctx, X, y, Width, plan, scale) + MezeraMeziBlokyMm;
            }
        }
        finally
        {
            ctx.Canvas.Restore();
        }

        if (hlavaKZopakovani is not null)
        {
            float vrch = ctx.ClipTopMm - ctx.OpakovanaHlavickaMm;
            ctx.Canvas.Save();
            ctx.Canvas.ClipRect(ctx.ToCanvasRect(X, vrch, Width, ctx.OpakovanaHlavickaMm));
            try { RowPlanRenderer.DrawRows(ctx, X, vrch, Width, hlavaKZopakovani, scale); }
            finally { ctx.Canvas.Restore(); }
        }
    }

    /// <summary>
    /// Popíše tabulku jako **plán řádků** (R1 řádkové jádro) - jen tvaruje data, nic nekreslí.
    /// Druhy řádků linkového JŘ: pás skupin dnů (sloučené sloupce) → čísla spojů → platnost →
    /// negativní značky → zastávky (s podřádky přípojů). Vodorovné čáry a pozadí zvýraznění nese
    /// <see cref="RowSpec"/>, samotné kreslení <see cref="RowPlanRenderer"/>.
    /// </summary>
    private List<RowSpec> BuildRowPlan(
        List<LineStopData> stops, List<LineTripData> trips, List<LineTripData> vsechnySpoje,
        List<LineSkupinaData> groups,
        List<RenderCol> fixedCols, float tripW, int nameIndex, float fixedSum,
        float headerH, float rowH, float pripojRowH, SKPaint gridPaint,
        ResolvedTextStyle headerResolved, ResolvedTextStyle headerCentered,
        ResolvedTextStyle tripHeaderStyle, ResolvedTextStyle tripDataStyle,
        RenderContext ctx, SKTypeface symbolFace, float scale)
    {
        var plan = new List<RowSpec>();

        // pás názvů skupin dnů - spojité úseky sloupců spojů stejné skupiny (sloučené buňky,
        // proto bez svislých čar, které by nadpis rozsekaly)
        // ⚠️ O tom, které řádky tabulka MÁ, rozhodují **všechny spoje linky**, ne spoje bloku.
        // Podle bloku to znamenalo, že pás skupin dnů byl jen nad prvním blokem (nahlásil Jan
        // 25. 8.) - druhý blok padl celý do jedné skupiny, takže nad ním nebylo poznat, ke kterým
        // dnům patří. Totéž by potkalo řádek platnosti a negativních značek a rozešlo by to výšku
        // s `CalculateHeightFromData`, které počítá z celé linky.
        if (HasGroupHeaderBand(vsechnySpoje, groups))
        {
            // Popisné sloupce jsou jedna skupina, takže jedna sloučená buňka s jedním nadpisem.
            var band = new List<ColumnRenderer.CellSpec>
                { new(fixedSum, PopisneSloupceNadpis, PopisneSloupceNadpis == "" ? headerResolved : headerCentered) };
            int i = 0;
            while (i < trips.Count)
            {
                int start = i;
                int skupina = trips[i].Skupina;
                while (i < trips.Count && trips[i].Skupina == skupina) i++;
                string nadpis = groups.FirstOrDefault(g => g.Id == skupina)?.Nadpis ?? "";
                band.Add(new((i - start) * tripW, nadpis, headerCentered));
            }
            // sloucene bunky -> bez svislych car (rozsekaly by nadpis pres nekolik sloupcu spoju)
            plan.Add(new(RowKind.GroupHeader, headerH, band) { LineBelowPaint = gridPaint });
        }

        // řádek čísel spojů - popisové sloupce per-column styl, spoje z Trip sloupce
        var headerSpecs = new List<ColumnRenderer.CellSpec>();
        for (int ci = 0; ci < fixedCols.Count; ci++)
            headerSpecs.Add(new(fixedCols[ci].Width, fixedCols[ci].Header, fixedCols[ci].HeaderStyle,
                SuppressLeadingLine: BezCaryVlevo(ci)));
        foreach (var t in trips) headerSpecs.Add(new(tripW, t.CisloSpoje, tripHeaderStyle));
        plan.Add(new(RowKind.Header, headerH, headerSpecs)
            { VerticalLinePaint = gridPaint, LineBelowPaint = gridPaint });

        // řádek platnosti (znaky X/6/7/+ nad spojem)
        if (HasPlatnostRow(vsechnySpoje))
        {
            // Značky se kreslí ručně nad hotovým řádkem, aby šel v jedné buňce střídat symbolový
            // a běžný font („⚒ BEV"). Buňky proto zůstávají prázdné - jinak by se text zdvojil.
            var platnostRuns = trips
                .Select(t => SymbolRunRenderer.BuildRuns(t.Platnost, SymbolMap.ResolveLinkovy))
                .ToList();
            var prazdne = TripTextRow(fixedCols, trips, tripW, tripDataStyle, _ => "");
            plan.Add(new(RowKind.HeaderExtra, rowH, prazdne)
            {
                VerticalLinePaint = gridPaint,
                // `DrawRows` kreslí buňky od X prvku, takže i ruční kreslení musí začít tam.
                Decorate = rowY => DrawPlatnostRuns(ctx, rowY, rowH, X + fixedSum, tripW,
                    platnostRuns, tripDataStyle, symbolFace, scale),
            });
        }

        // řádek NZ (negativní značky)
        if (HasNzRow(vsechnySpoje))
            plan.Add(new(RowKind.HeaderExtra, rowH,
                TripTextRow(fixedCols, trips, tripW, tripDataStyle, t => t.Nz))
            { VerticalLinePaint = gridPaint, LineBelowPaint = gridPaint });

        // řádky zastávek (+ vložené přípojné řádky)
        var poradiSOdjezdem = PoradiSOdjezdem(
            stops.Where(s => RowConditions.IsVisible(RowVisibilityExpression, s.SourceRow)));
        // Přípojný řádek má vlastní styl (#8360) - jinak vypadá stejně jako čas spoje.
        // Výška chodí zvenčí už přeškálovaná, ať se nepočítá dvakrát jinak než v `Render`.
        var pripojStyle = ResolvePripojStyle();
        for (int si = 0; si < stops.Count; si++)
        {
            var stop = stops[si];

            // Podmíněné skrytí řádku zastávky (i s jejími přípojnými řádky). Iterujeme přes plný
            // index si → zarovnání časů spojů (Casy[si]) zůstane zachováno; jen tuto zastávku nekreslíme.
            if (!RowConditions.IsVisible(RowVisibilityExpression, stop.SourceRow)) continue;

            bool oddTc = HighlightOddTc && int.TryParse(stop.Tc, out var tcNum) && tcNum % 2 == 1;

            var specs = new List<ColumnRenderer.CellSpec>();
            for (int ci = 0; ci < fixedCols.Count; ci++)
                specs.Add(new(fixedCols[ci].Width, fixedCols[ci].Value(stop),
                    OddTc(fixedCols[ci].DataStyle, oddTc), SuppressLeadingLine: BezCaryVlevo(ci)));
            var tripCellStyle = OddTc(tripDataStyle, oddTc);
            // Značka návaznosti se tiskne **vedle času** ve stejné buňce (doloženo tiskem staré
            // linky 330511). Buňka umí jen jeden font, takže se taková buňka nechá prázdná
            // a nakreslí se ručně - stejně jako řádek platnosti.
            var znackyVBunce = new Dictionary<int, List<SymbolRunRenderer.Run>>();
            for (int ti = 0; ti < trips.Count; ti++)
            {
                var t = trips[ti];
                string znak = ZnakNavaznosti(t, stop.Poradi);
                if (znak == "") continue;
                string cas = si < t.Casy.Count ? t.Casy[si] : "";
                if (cas == "") continue;
                var runs = SymbolRunRenderer.BuildRuns([znak], SymbolMap.ResolveLinkovy);
                if (!SymbolRunRenderer.MaPiktogram(runs)) continue;   // neověřená značka → nech text
                runs.Add(new(" " + cas, false));
                znackyVBunce[ti] = runs;
            }
            for (int ti = 0; ti < trips.Count; ti++)
            {
                var t = trips[ti];
                // Barva spoje z dat přebíjí barvu textu z kaskády - je to prezentační nápověda,
                // kterou prvek smí respektovat (R3). Spoj bez barvy nechá kaskádu být.
                var cellStyle = t.ForeColor is null ? tripCellStyle : tripCellStyle with { TextColor = t.ForeColor };
                string text = znackyVBunce.ContainsKey(ti) ? "" : (si < t.Casy.Count ? t.Casy[si] : "");
                specs.Add(new(tripW, text, cellStyle));
            }

            // Podmíněné zvýraznění řádku zastávky (celá šířka, pod sloupci) - sdílené s DynamicTable/StopList.
            string? rowHl = RowConditions.HighlightColor(HighlightRules, stop.SourceRow);
            var znackyRadku = znackyVBunce;
            // Piktogram zastávky (přestup na linkovou dopravu) se v původním tisku kreslí
            // **vpravo ve sloupci zastávek**, za názvem - proto overlay, ne předsazení před název.
            var znakZastavky = SymbolRunRenderer.BuildRuns(
                stop.JePrijezdovy ? [] : stop.Atributy.Where(JePiktogramZastavky), SymbolMap.ResolveLinkovy);
            plan.Add(new(RowKind.Data, rowH, specs)
            {
                Background = ColorHex.ParseOrNull(rowHl),
                VerticalLinePaint = gridPaint,
                Decorate = (znackyRadku.Count == 0) && (znakZastavky.Count == 0) ? null : rowY =>
                {
                    if (znackyRadku.Count > 0)
                        DrawBunkySeZnackou(ctx, rowY, rowH, X + fixedSum, tripW, znackyRadku, tripCellStyle, symbolFace, scale);
                    if (znakZastavky.Count > 0 && nameIndex >= 0)
                        DrawZnakZastavky(ctx, rowY, rowH, X, fixedCols, nameIndex, znakZastavky,
                            OddTc(fixedCols[nameIndex].DataStyle, oddTc), symbolFace, scale);
                },
            });

            if (!ShowPripojeRows) continue;
            if (!NesePripoje(stop, poradiSOdjezdem)) continue;

            // přípojné řádky (vyčká/navazuje) za touto zastávkou
            foreach (var t in trips)
            {
                foreach (var pr in t.Pripoje.Where(p => p.Radek && p.Poradi == stop.Poradi))
                {
                    var prSpecs = new List<ColumnRenderer.CellSpec>();
                    for (int ci = 0; ci < fixedCols.Count; ci++)
                        prSpecs.Add(new(fixedCols[ci].Width,
                            ci == nameIndex ? PripojText(pr) : "", pripojStyle,
                            SuppressLeadingLine: BezCaryVlevo(ci)));
                    foreach (var t2 in trips)
                        prSpecs.Add(new(tripW, ReferenceEquals(t2, t) ? pr.Cas : "", pripojStyle));

                    plan.Add(new(RowKind.SubRow, pripojRowH, prSpecs)
                    { VerticalLinePaint = gridPaint, LineAbovePaint = gridPaint, LineBelowPaint = gridPaint });
                }
            }
        }

        return plan;
    }

    /// <summary>Řádek, kde popisové sloupce jsou prázdné a spojové nesou znak z <paramref name="text"/>
    /// (platnost, negativní značky).</summary>
    /// <summary>Atribut zastávky, který se kreslí piktogramem (dnes „přestup na linkovou dopravu").
    /// Řídicí atributy (T, P, S, E…) se netisknou - v tisku pro cestujícího nemají význam.</summary>
    private static bool JePiktogramZastavky(string znak) =>
        znak != "x" && SymbolMap.ResolveLinkovy(znak).Mode == SymbolRenderMode.FontGlyf;

    /// <summary>Nakreslí piktogram zastávky vpravo ve sloupci zastávek - jako v původním tisku.</summary>
    /// <remarks>
    /// ⚠️ Je to <b>overlay nad hotovou buňkou</b>, ne součást jejího textu: název se kreslí vlevo
    /// a zmenšuje se, aby se vešel, kdežto piktogram má sedět u pravého okraje. U velmi dlouhého
    /// názvu se můžou potkat - kdyby se to ukázalo, patří to řešit rezervací místa v buňce.
    /// </remarks>
    private static void DrawZnakZastavky(RenderContext ctx, float rowTopMm, float rowH, float xMm,
        List<RenderCol> fixedCols, int nameIndex, List<SymbolRunRenderer.Run> runs,
        ResolvedTextStyle style, SKTypeface symbolFace, float scale)
    {
        float leftMm = xMm;
        for (int i = 0; i < nameIndex; i++) leftMm += fixedCols[i].Width;
        var cellMm = new SKRect(leftMm, rowTopMm, leftMm + fixedCols[nameIndex].Width, rowTopMm + rowH);
        SymbolRunRenderer.Draw(ctx, CellLayout.ApplyPadding(cellMm, style),
            runs, style with { HAlign = TextHorizontalAlignment.Right }, symbolFace, scale);
    }

    /// <summary>Značka návaznosti, kterou má spoj u tohohle tarifního čísla - prázdné, když žádná.
    /// „Vyčká" je dutý trojúhelník, „navazuje" plný (doloženo tiskem staré linky 330511).</summary>
    private static string ZnakNavaznosti(LineTripData t, int poradi)
    {
        foreach (var pr in t.Pripoje)
        {
            if (pr.Poradi != poradi) continue;
            if (pr.Typ == "vycka") return "m";
            if (pr.Typ == "navazuje") return "^";
        }
        return "";
    }

    /// <summary>Nakreslí buňky, kde je vedle času značka návaznosti - piktogram symbolovým fontem,
    /// čas běžným.</summary>
    private static void DrawBunkySeZnackou(RenderContext ctx, float rowTopMm, float rowH,
        float prvniSloupecMm, float tripW, Dictionary<int, List<SymbolRunRenderer.Run>> znacky,
        ResolvedTextStyle style, SKTypeface symbolFace, float scale)
    {
        foreach (var (ti, runs) in znacky)
        {
            float leftMm = prvniSloupecMm + ti * tripW;
            var cellMm = new SKRect(leftMm, rowTopMm, leftMm + tripW, rowTopMm + rowH);
            SymbolRunRenderer.Draw(ctx, CellLayout.ApplyPadding(cellMm, style), runs, style, symbolFace, scale);
        }
    }

    /// <summary>Nakreslí značky platnosti do sloupců spojů - piktogramy symbolovým fontem,
    /// zkratky běžným, i když jsou v jedné buňce vedle sebe.</summary>
    /// <remarks>
    /// ⚠️ Dělá se to ručně, protože buňka umí jen jeden font. První verze proto přepínala font
    /// „buď celá buňka, nebo nic" a v jednom řádku pak stálo u spoje 5 kladivo a u spoje 9 písmeno
    /// „X", protože ten měl v buňce navíc „BEV" (nález Jan 24. 8.).
    /// </remarks>
    private static void DrawPlatnostRuns(RenderContext ctx, float rowTopMm, float rowH,
        float fixedSumMm, float tripW, List<List<SymbolRunRenderer.Run>> runsPerTrip,
        ResolvedTextStyle style, SKTypeface symbolFace, float scale)
    {
        for (int i = 0; i < runsPerTrip.Count; i++)
        {
            var runs = runsPerTrip[i];
            if (runs.Count == 0) continue;
            float leftMm = fixedSumMm + i * tripW;
            var cellMm = new SKRect(leftMm, rowTopMm, leftMm + tripW, rowTopMm + rowH);
            SymbolRunRenderer.Draw(ctx, CellLayout.ApplyPadding(cellMm, style), runs, style, symbolFace, scale);
        }
    }

    private List<ColumnRenderer.CellSpec> TripTextRow(
        List<RenderCol> fixedCols, List<LineTripData> trips, float tripW,
        ResolvedTextStyle tripDataStyle, Func<LineTripData, string> text)
    {
        var specs = new List<ColumnRenderer.CellSpec>();
        for (int ci = 0; ci < fixedCols.Count; ci++)
            specs.Add(new(fixedCols[ci].Width, "", fixedCols[ci].DataStyle,
                SuppressLeadingLine: BezCaryVlevo(ci)));
        foreach (var t in trips) specs.Add(new(tripW, text(t), tripDataStyle));
        return specs;
    }

    /// <summary>Má se na levé hraně popisného sloupce potlačit svislá čára? První sloupec bloku
    /// nikdy - tam je vnější hrana, kterou kreslí rámeček prvku, ne mřížka.</summary>
    private bool BezCaryVlevo(int indexPopisnehoSloupce) =>
        PopisneSloupceBezVnitrnichCar && indexPopisnehoSloupce > 0;

    private ResolvedTextStyle OddTc(ResolvedTextStyle style, bool odd) =>
        odd ? style with { BackgroundColor = OddTcBackColor } : style;

    private static string PripojText(LinePripojData pr)
    {
        string sloveso = pr.Typ == "vycka" ? "vyčká na spoj" : "navazuje spoj";
        return $"{sloveso} {pr.Spoj} linky {pr.Linka} do {pr.Text}";
    }

    private void DrawRowLine(RenderContext ctx, float yMm, SchedulePaints paints)
    {
        var s = ctx.ToCanvas(X, yMm);
        var e = ctx.ToCanvas(X + Width, yMm);
        ctx.Canvas.DrawLine(s, e, paints.Grid);
    }

    private void RenderPlaceholder(RenderContext ctx, SchedulePaints paints)
    {
        var headerResolved = ResolveColumnHeaderParent();
        var dataResolved = ResolveColumnDataParent();
        float headerH = ComputeHeaderH(headerResolved.FontSize);
        float rowH = DataRowHeight ?? ComputeRowH(dataResolved.FontSize);

        float headerFontPx = headerResolved.FontSize * PtToMm * ctx.MmToPx;
        using var headerFont = new SKFont(GetTypeface(headerResolved), headerFontPx);
        float currentY = RenderSectionHeader(ctx, "Linkový JŘ - zastávky × spoje", X, Y, Width, headerH,
            headerFont, paints.HeaderBg, paints.HeaderText, paints.Grid);

        float dataFontPx = dataResolved.FontSize * PtToMm * ctx.MmToPx;
        using var dataFont = new SKFont(GetTypeface(dataResolved), dataFontPx);
        RenderPlaceholderRows(ctx, 5, "Zastávka", X, currentY, Width, rowH, dataFont, paints.Text, paints.Grid);
    }

    // ─── Clone / škálování / descriptory ────────────────────────────────────

    public override TemplateElement Clone()
    {
        var clone = new LineTableElement
        {
            ZastavkyDataSource = ZastavkyDataSource,
            SpojeDataSource = SpojeDataSource,
            SkupinyDnuDataSource = SkupinyDnuDataSource,
            Columns = Columns.Select(c => c.Clone()).ToList(),
            ShowPlatnostRow = ShowPlatnostRow,
            ShowNzRow = ShowNzRow,
            ShowPripojeRows = ShowPripojeRows,
            PadEmptyColumns = PadEmptyColumns,
            ShowGroupHeaders = ShowGroupHeaders,
            PopisneSloupceNadpis = PopisneSloupceNadpis,
            PopisneSloupceBezVnitrnichCar = PopisneSloupceBezVnitrnichCar,
            HighlightOddTc = HighlightOddTc,
            OddTcBackColor = OddTcBackColor,
            RowVisibilityExpression = RowVisibilityExpression,
            HighlightRules = HighlightRules?.Select(r => r.Clone()).ToList(),
            PripojStyle = PripojStyle.Clone(),
            CustomColumns = CustomColumns?.Select(c => c.Clone()).ToList(),
        };
        CopyBaseTo(clone);
        CopyScheduleBaseProperties(clone);
        return clone;
    }

    public override void ApplyScale(float scale)
    {
        base.ApplyScale(scale);   // DataRowHeight + Header/Data styl škáluje base
        PripojStyle.ApplyScale(scale);
        foreach (var c in Columns)
        {
            if (c.Width is float w) c.Width = w * scale;
            c.Style?.ApplyScale(scale);   // per-sloupcové font/padding overridy
        }
    }

    public override IReadOnlyList<PropertyDescriptor> GetPropertyDescriptors() =>
    [
        new BoolPropertyDescriptor { Label = "Řádek platnosti", Group = "Řádky", Get = () => ShowPlatnostRow, Set = v => ShowPlatnostRow = v },
        new BoolPropertyDescriptor { Label = "Řádek negativních značek", Group = "Řádky", Get = () => ShowNzRow, Set = v => ShowNzRow = v },
        new BoolPropertyDescriptor { Label = "Přípojné řádky", Group = "Řádky", Get = () => ShowPripojeRows, Set = v => ShowPripojeRows = v },
        new BoolPropertyDescriptor { Label = "Dorovnat prázdnými sloupci", Group = "Sloupce", Get = () => PadEmptyColumns, Set = v => PadEmptyColumns = v },
        new BoolPropertyDescriptor { Label = "Nadpisy skupin dnů", Group = "Řádky", Get = () => ShowGroupHeaders, Set = v => ShowGroupHeaders = v },
        new StringPropertyDescriptor { Label = "Nadpis popisných sloupců", Group = "Sloupce",
            Get = () => PopisneSloupceNadpis, Set = v => PopisneSloupceNadpis = v },
        new BoolPropertyDescriptor { Label = "Popisné sloupce bez vnitřních čar", Group = "Sloupce",
            Get = () => PopisneSloupceBezVnitrnichCar, Set = v => PopisneSloupceBezVnitrnichCar = v },
        new BoolPropertyDescriptor { Label = "Podbarvit lichá Tč", Group = "Vzhled", Get = () => HighlightOddTc, Set = v => HighlightOddTc = v },
        new ColorPropertyDescriptor { Label = "Barva lichých Tč", Group = "Vzhled", Get = () => OddTcBackColor, Set = v => OddTcBackColor = v },
        RowConditionDescriptors.RowVisibility(() => RowVisibilityExpression, v => RowVisibilityExpression = v),
        // #8360: přípojný řádek se má dát odlišit od časů spojů. Prázdné = dědí se z buněk.
        new FloatPropertyDescriptor { Label = "Velikost", Group = "Přípojné řádky", Unit = "pt", Min = 4, Max = 200,
            Get = () => PripojStyle.FontSize ?? ResolveElementPripojParent().FontSize, Set = v => PripojStyle.FontSize = v },
        new BoolPropertyDescriptor { Label = "Tučné", Group = "Přípojné řádky",
            Get = () => PripojStyle.Bold ?? ResolveElementPripojParent().Bold, Set = v => PripojStyle.Bold = v },
        new BoolPropertyDescriptor { Label = "Kurzíva", Group = "Přípojné řádky",
            Get = () => PripojStyle.Italic ?? ResolveElementPripojParent().Italic, Set = v => PripojStyle.Italic = v },
        new ColorPropertyDescriptor { Label = "Barva textu", Group = "Přípojné řádky",
            Get = () => PripojStyle.TextColor ?? ResolveElementPripojParent().TextColor, Set = v => PripojStyle.TextColor = v },
        .. GetScheduleBaseDescriptors(),
        .. GetCellZoneBaseColorDescriptors(),
        GetDataRowHeightDescriptor(),
        .. GetPositionDescriptors(),
    ];
}
