using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SkiaSharp;
using TemplateDesigner.Models.Expressions;
using TemplateDesigner.Rendering;

namespace TemplateDesigner.Models.Elements;

/// <summary>
/// Kolik místa zaberou přípojné řádky: buď řádek pro každý spoj, nebo se přípoje jednoho směru
/// slijí do jednoho řádku jako ve starém tisku.
/// </summary>
public enum FormatCasuSpoje
{
    /// <summary>Hodina větším písmem, minuta menším, oddělené mezerou: „16 15". Tvar starého
    /// postskriptového tisku, kde to řídí dvě samostatné velikosti písma (`/Spojscale`,
    /// `/Spojscale1`; u linky nastavení „fonty - hh~mm").</summary>
    HodinaVetsi,
    /// <summary>Celý čas jednou velikostí, oddělený dvojtečkou: „16:15".</summary>
    SDvojteckou,
}

/// <summary>
/// Kolik místa zaberou přípojné řádky: buď řádek pro každý spoj, nebo se přípoje jednoho směru
/// slijí do jednoho řádku jako ve starém tisku.
/// </summary>
public enum PripojSlucovani
{
    /// <summary>Řádek pro každý spoj, popisek s číslem spoje a linky (tvar do 4. 9. 2026).</summary>
    Ne,
    /// <summary>Přípoje téhož směru a druhu dopravy na jednom řádku, popisek „ze směru X".</summary>
    PodleSmeru,
    /// <summary>Jako podle směru, ale každá linka má vlastní řádek.</summary>
    PodleSmeruALinky,
}

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
        // Šipka směru: výchozí **skrytá**, ať se existujícím šablonám nezmění vzhled. Do starších
        // šablon ji doplní migrace, aby ji šlo zapnout v dialogu sloupců (ten neumí sloupce přidávat).
        new() { Key = ColumnKeys.Arrow,    Header = "",         Width = 4f,  Visible = false, Alignment = TextHorizontalAlignment.Center },
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

    /// <summary>Jak se přípojné řádky slučují - viz <see cref="PripojSlucovani"/>.</summary>
    /// <remarks>
    /// Výchozí je <b>podle směru</b>, tedy jako ve starém tisku (rozhodnutí Jan 4. 9.: šetření
    /// místa váží víc než číslo linky u každého řádku). Šablona uložená dřív hodnotu nemá, takže
    /// se jí výchozí uplatní taky - a to je záměr: dnešní tvar byl odchylka, ne volba.
    /// </remarks>
    public PripojSlucovani PripojeSlucovani { get; set; } = PripojSlucovani.PodleSmeru;

    /// <summary>
    /// Dorovnat neúplný blok prázdnými sloupci, aby měly všechny bloky stejný počet sloupců.
    /// </summary>
    /// <remarks>
    /// Poslední blok bývá neúplný a nechá vpravo volné místo. Starý modul umí to místo vyplnit
    /// prázdnými sloupci, takže tabulka končí rovně a bloky vypadají stejně (#8372; v legacy
    /// „padding sloupce", které sjednocují počet sloupců napříč stránkami).
    /// </remarks>
    public bool PadEmptyColumns { get; set; }

    /// <summary>
    /// Všechny sloupce spojů stejně široké (výchozí zapnuto).
    /// </summary>
    /// <remarks>
    /// Vypnutá je parita se starým modulem: sloupec, ve kterém je někde značka návaznosti, je
    /// o tu značku širší než ostatní (starý modul má pro sloupec spoje čtyři šířky podle počtu
    /// značek, <c>JRapiLinPrtWin.mac:1793</c>). Na výtisku to působí roztřepeně, protože se šířka
    /// mění spoj od spoje, a oko to čte jako různě velké písmo.
    /// <para>⚠️ **Výchozí je zapnuto**, tedy odchylka od legacy parity - čitelnost vyhrála
    /// (rozhodnutí Jan 7. 9.). Bezpečné to je proto, že linková agenda ještě nemá u zákazníků
    /// hotové šablony, kterým by se tím změnilo rozvržení.</para>
    /// <para>⚠️ Nejširší se bere přes **všechny** spoje linky, ne přes blok - jinak by bloky pod
    /// sebou měly různě široké sloupce a zastávky by na sebe svisle nesedly (invariant z 25. 8.,
    /// stejný důvod, proč je základ sloupce společný pro všechny bloky).</para>
    /// </remarks>
    public bool JednotnaSirkaSpoju { get; set; } = true;

    /// <summary>
    /// Skupina formuláře, ve které je pohromadě všechno, co rozhoduje o šířce sloupců spojů.
    /// </summary>
    /// <remarks>
    /// Do 8. 9. byla ta nastavení roztroušená (dorovnání a stejné šířky ve „Sloupcích", velikost
    /// písma a okraj v „Datových buňkách") a vedle nich stála nesouvisející nastavení popisných
    /// sloupců. ⚠️ Popisek musí souhlasit s <c>LineTableElementEdit.cls</c>, jinak spadne driftový
    /// test. ⚠️ Ne „Šířka a dělení" - „dělení sloupců" už v UI znamená členění na skupiny dnů.
    /// </remarks>
    internal const string SirkaGroup = "Šířka sloupců";

    /// <summary>Skupina formuláře pro sloupce popisující zastávku (zóna, km, Tč, název, MHD).</summary>
    internal const string PopisneGroup = "Popisné sloupce";

    /// <summary>
    /// Nejmenší šířka sloupce spoje v mm. Null = odvodit z písma (šířka času + vnitřní okraje).
    /// </summary>
    /// <remarks>
    /// Odvozené dno se dá utáhnout jen oklikou přes vnitřní okraj buňky a uživatel musí znát vzorec,
    /// aby výsledek odhadl (požadavek Jan 8. 9.). Vyplněná hodnota proto <b>nahradí</b> celý výpočet
    /// dna, takže okraj na šířku přestane mít vliv a řekne se rovnou, jak úzký sloupec smí být.
    /// <para>Tvrdé dno <see cref="MinAutoColumnWidthMm"/> platí dál - pod ně se nedostane ani
    /// vyplněnou hodnotou, jinak by se do bloku nasypalo tolik spojů, že by čas nebyl čitelný.</para>
    /// </remarks>
    public float? NejmensiSirkaSpojeMm { get; set; }

    /// <summary>
    /// Největší šířka sloupce spoje v mm. Null = bez omezení (sloupce vyplní prvek).
    /// </summary>
    /// <remarks>
    /// ⚠️ Do 8. 9. měla šířka sloupce <b>dno, ale žádný strop</b>: zbytek prvku se vždy rozdělil
    /// mezi sloupce, takže u linky s pár spoji vyšel sloupec extrémně široký a čas stál uprostřed
    /// prázdného pole (na 200mm prvku a pěti spojích kolem 24 mm proti dnu 3,1 mm). Lidem, kteří
    /// dnes tisknou postskriptem, se to nelíbí - ten místo roztahování drží sloupce úzké a zbytek
    /// vyplní prázdnými sloupci.
    /// <para>Strop se počítá na <b>základ</b> sloupce; místo na značku návaznosti se přičítá nad
    /// něj (rozhodnutí Jan 8. 9. - značka není problém, jde o volné místo).</para>
    /// <para>Co se stane s přebytkem, rozhoduje <see cref="PadEmptyColumns"/>: zapnuté ho rozkreslí
    /// prázdnými sloupci, vypnuté ho nechá bílé.</para>
    /// </remarks>
    public float? NejvetsiSirkaSpojeMm { get; set; }

    /// <summary>
    /// Vyplnit tečkami buňky spojů, ve kterých není čas (výchozí vypnuto).
    /// </summary>
    /// <remarks>
    /// Starý postskriptový tisk do takové buňky sází tečky (`TiT = "."`, `JRapiLinPrtPS.mac:58`),
    /// takže tabulka drží svislé vodicí pruhy. ⚠️ **Výchozí vypnuto**: u linky, kde většina spojů
    /// jede jen část trasy, se tabulka tečkami zaplní a čte se hůř než s prázdnem. Windows tisk
    /// starého modulu výplň nemá vůbec - je to vlastnost postskriptu, ne obecná parita.
    /// <para>Buňka se značkou („&lt;" jede po jiné trase, „|" projíždí) se nevyplňuje - není prázdná.</para>
    /// </remarks>
    public bool VyplnitPrazdneBunkySpoju { get; set; }

    /// <summary>
    /// Doplnit za název zastávky tečky až k pravému okraji sloupce (výchozí vypnuto).
    /// </summary>
    /// <remarks>
    /// Vodicí tečky, po kterých oko dojede od názvu zastávky k času - starý postskriptový tisk je
    /// má. Končí **před piktogramem přestupu**, který sedí u pravého okraje buňky; jinak by mu
    /// vedly pod nohama.
    /// </remarks>
    public bool TeckyZaNazvemZastavky { get; set; }

    /// <summary>
    /// Velikost značek platnosti a negativních značek jako podíl velikosti písma datové buňky
    /// (výchozí 1,5).
    /// </summary>
    /// <remarks>
    /// Pás značek nad časy má ve starém tisku **vlastní, větší** velikost: v jeho programu je
    /// `Plascale 9.0` proti `Spojscale 6.0` u časů, tedy právě jedenapůlkrát (odečteno z programu
    /// zákazníka, `^JRSET("PS","NVYVESY")`). Se stejnou velikostí jako časy jsou značky na výtisku
    /// špatně čitelné - a přitom nesou omezení, kvůli kterému spoj někdy nejede.
    /// <para>⚠️ Řádek značek se tím **zvýší** o týž poměr, jinak by se značka do řádku nevešla.
    /// Výšku drží <see cref="VyskyRadkuBloku"/>, aby ji render i výpočet výšky prvku vzaly stejně.</para>
    /// </remarks>
    public float PomerZnacek { get; set; } = 1.5f;

    /// <summary>
    /// Respektovat tučné písmo zadané u zastávky v trase linky (výchozí zapnuto).
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Není to naše rozhodnutí o vzhledu, je to údaj z jízdních řádů.</b> Atribut trasy
    /// <c>T</c> znamená v číselníku atributů trasy (číselník 16, <c>JRapiCis.mac:1284</c>)
    /// doslova „tučně" - dispečer si tak u konkrétní zastávky vyžádá zvýraznění. Atribut
    /// <c>P</c> („podtrženo s příjezdem a odjezdem") ho má taky.
    /// <para>Obě staré cesty to tisknou tučně: postskript dá názvu prefix „#" místo „$"
    /// (<c>JRapiLinPrtPS1.mac:52</c>), Windows tisk nastaví příznak řádku
    /// (<c>JRapiLinPrtWin.mac:1272-1276</c>). Vypnutím se zvýraznění z trasy ignoruje.</para>
    /// </remarks>
    public bool TucneZastavkyDleTrasy { get; set; } = true;

    /// <summary>Jak se sází čas spoje (výchozí hodina větším písmem, jako ve starém tisku).</summary>
    public FormatCasuSpoje FormatCasu { get; set; } = FormatCasuSpoje.HodinaVetsi;

    /// <summary>
    /// Podíl velikosti minut vůči hodině při <see cref="FormatCasuSpoje.HodinaVetsi"/>.
    /// </summary>
    /// <remarks>
    /// Starý tisk má pro hodinu a minutu dvě **nezávislé** velikosti (`/Spojscale`, `/Spojscale1`).
    /// U nás je to jeden podíl, protože velikost hodiny už drží styl datových buněk - dvě čísla
    /// vedle sebe by znamenala, že jedno z nich kaskádu obchází.
    /// </remarks>
    public float PomerMinuty { get; set; } = 0.75f;

    /// <summary>
    /// Kazda zona hranicni zastavky ve vlastnim sloupci (vychozi vypnuto).
    /// </summary>
    /// <remarks>
    /// Zastavka na hranici zon ma dnes vsechny zony v jednom sloupci za sebou; zakaznik s IDS
    /// (PID, IDZK) je chce vedle sebe, jako to umi kilometry (#8576). ⚠️ **Vychozi je vypnuto
    /// zamerne** - vic zon ma 300 tisic zastavek merenych firem, takze zapnuty rozpad by prekreslil
    /// hotove sestavy i tem, kdo o nej nezadali. Stary modul to neumi vubec, neni to tedy parita.
    /// </remarks>
    public bool RozdelitZonyDoSloupcu { get; set; }
    /// <summary>Pás názvů skupin dnů (Pracovní dny / Soboty / …) nad čísly spojů.</summary>
    public bool ShowGroupHeaders { get; set; } = true;

    /// <summary>
    /// Řádky s označením turnusu pod čísly spojů. Vypnuté = server turnusy ani nenačítá.
    /// </summary>
    /// <remarks>
    /// ⚠️ **Je to volba prvku, ne parametr sestavy** (rozhodnutí Jan 28. 8.): turnus je provozní
    /// věc dopravce, ne informace pro cestujícího, takže patří k sestavě, kterou si někdo udělá
    /// pro provoz - stejně jako v legacy, kde to je nastavení sekce v návrháři sestav.
    /// <para>Zapnutí musí serveru dojít, protože turnusy nejsou v datech linky a jejich dotaz
    /// stojí 40-550 ms: klient posílá `TURNUSY_KE_SPOJUM` podle prvku (touž cestou jako
    /// `DEFINICE_SLOUPCU`) a změna spouští re-fetch dat přes `CurrentWireSnapshot`.</para>
    /// </remarks>
    public bool TurnusyKeSpojum { get; set; }

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
    /// ⚠️ Hrubší vypínač je <c>Grid.ShowColumnLines</c>, jenže ten sebere i čáry mezi spoji -
    /// a to je právě ta stížnost, že „ohraničení jednotlivých km sloupců nejde odstranit".
    /// (Do 31. 8. navíc <c>ShowColumnLines</c> nefungoval vůbec, takže tohle bylo jediné,
    /// čím šlo svislou čáru v tabulce potlačit.)
    /// </remarks>
    public bool PopisneSloupceBezVnitrnichCar { get; set; }

    /// <summary>
    /// Mezera mezi <b>skupinami sloupců</b> v mm - za popisným blokem a mezi skupinami dnů.
    /// 0 = bez mezery, tedy dosavadní souvislá mřížka.
    /// </summary>
    /// <remarks>
    /// ⚠️ Mezera se <b>rezervuje ze šířky prvku</b>, takže se do řádku vejde o tolik méně spojů;
    /// u dlouhé linky se tím může přidat blok. Rezervuje se za <b>celou linku</b>, ne za kreslený
    /// blok - jinak by každý blok vyšel s jinak širokými sloupci (viz `RezervaMezerMm`).
    /// </remarks>
    public float MezeraMeziSkupinamiMm { get; set; }

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

        /// <summary>Jednotlive zony hranicni zastavky - viz <c>RozdelitZony</c>.</summary>
        public List<string> Zony = [];
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
        public string Avl = "A";     // druh dopravy: A autobus / V vlak / L loď - seskupovací klíč
        public bool Radek;           // legacy „typ zobrazení": tisknout jako vložený řádek matice
    }

    public sealed class LineTripData
    {
        /// <summary>Směr spoje („T"/„Z"). U obousměrné stránky je to jediné, čím prvek pozná,
        /// na kterou stranu zastávek sloupec patří.</summary>
        public string Smer = "";

        public string CisloSpoje = "";
        public int Skupina = 1;
        public string Nz = "";
        /// <summary>Barva textu časů spoje (prezentační nápověda z dat, R3). Null = řeší šablona.
        /// Server ji posílá jen u spoje, který barvu skutečně má, a jen když ji linka nepotlačila.</summary>
        public string? ForeColor;
        public List<string> Platnost = [];
        /// <summary>Označení turnusů, ve kterých spoj jede („110.01"). Posílá je server jen
        /// se zapnutým parametrem „Turnusy ke spojům" - jinak je seznam prázdný a řádky nevzniknou.</summary>
        public List<string> Turnusy = [];
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
    /// <summary>Kultura naposledy naparsovaných dat - podle ní se vybírá symbolový font (CZ/SK),
    /// a tedy i šířka značky. Šířky sloupců se počítají i mimo kreslení, kde kontext není.</summary>
    [JsonIgnore] private System.Globalization.CultureInfo? _cachedCulture;
    [JsonIgnore] private List<LineStopData>? _cachedStops;
    [JsonIgnore] private List<LineTripData>? _cachedTrips;
    [JsonIgnore] private List<LineSkupinaData>? _cachedGroups;
    /// <summary>Spoje rozdělené na hlavní směr (vpravo od zastávek) a zrcadlený (vlevo).</summary>
    [JsonIgnore] private List<LineTripData>? _cachedHlavni;
    [JsonIgnore] private List<LineTripData>? _cachedZrcadlene;

    private void EnsureParsed(DataContext data)
    {
        if (ReferenceEquals(data, _lineCacheRef) && _cachedStops is not null) return;
        _lineCacheRef = data;
        _cachedCulture = data.Culture;

        _cachedStops = [.. data.GetRows(ZastavkyDataSource).Select(row => new LineStopData
        {
            Poradi = ParseInt(row.GetString("PORADI")),
            Typ = row.GetString("TYP"),
            Popis = row.GetString("NAZEV_ZASTAVKY"),
            Tc = row.GetString("TC"),
            Zona = row.GetString("ZONY"),
            Zony = RozdelZony(row.GetString("ZONY")),
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
            Smer = row.GetString("SMER"),
            Nz = row.GetString("NZ"),
            ForeColor = row.GetString("FORE_COLOR") is { Length: > 0 } fc ? fc : null,
            Platnost = ParseStringArray(row.GetValue("PLATNOST")),
            Turnusy = ParseStringArray(row.GetValue("TURNUSY")),
            Casy = ParseStringArray(row.GetValue("CASY")),
            Pripoje = ParsePripoje(row.GetValue("PRIPOJE")),
        })];

        RozdelPodleSmeru();

        _cachedGroups = [.. data.GetRows(SkupinyDnuDataSource).Select(row => new LineSkupinaData
        {
            Id = ParseInt(row.GetString("ID")),
            Nadpis = row.GetString("NADPIS"),
        })];
    }

    /// <summary>
    /// Rozdělí spoje na <b>hlavní</b> směr (kreslí se vpravo od zastávek) a <b>zrcadlený</b> (vlevo).
    /// </summary>
    /// <remarks>
    /// Hlavní je směr <b>prvního</b> spoje: seznam zastávek je v jeho pořadí, takže jeho časy jdou
    /// odshora dolů. Cokoli jiného je opačný směr a patří nalevo - tak to má i starý modul
    /// (sloupec „Spoj před zastávkou", typ 9). Jednosměrná stránka má zrcadlený seznam prázdný,
    /// takže se pro ni nic nemění.
    /// <para>⚠️ Kterou stranu který směr dostane, se <b>nenastavuje</b> - vyplývá z toho, v jakém
    /// pořadí zastávek stránka přišla. Dvě volby téhož (pořadí zastávek × strana) by si šlapaly.</para>
    /// </remarks>
    private void RozdelPodleSmeru()
    {
        var trips = _cachedTrips!;
        if (trips.Count == 0) { _cachedHlavni = []; _cachedZrcadlene = []; return; }

        string prvni = trips[0].Smer;
        if (trips.TrueForAll(t => t.Smer == prvni))
        {
            // Jednosměrná stránka: všechno vpravo, nic se nemění.
            _cachedHlavni = trips;
            _cachedZrcadlene = [];
            return;
        }
        // ⚠️ **Nalevo jde směr, jehož pořadí sleduje seznam zastávek** - tedy ten, jehož časy jdou
        // odshora dolů. Doloženo tiskem obousměrné linky ze starého modulu (28. 8.): vlevo stojí
        // spoj s časy 07:33 → 07:38 a u první zastávky značka „od", vpravo spoj s časy 07:53 → 07:39
        // a značka „př". Do 28. 8. jsme to měli obráceně.
        _cachedZrcadlene = [.. trips.Where(t => t.Smer == prvni)];
        _cachedHlavni = [.. trips.Where(t => t.Smer != prvni)];
    }

    /// <summary>Kolik sloupců spojů zabere jeden „slot" dělení - u obousměrné sestavy dva
    /// (jeden na každé straně zastávek), jinak jeden.</summary>
    private int SloupcuNaSlot() => (_cachedZrcadlene?.Count ?? 0) > 0 ? 2 : 1;

    /// <summary>Spoje jednoho bloku - týž úsek slotů z <b>obou</b> stran zastávek. Kratší strana
    /// dá méně sloupců (nebo žádné), delší určuje počet bloků.</summary>
    private (List<LineTripData> Hlavni, List<LineTripData> Zrcadlene) SpojeBloku(TableLayout.SloupcovyBlok blok)
    {
        List<LineTripData> Usek(List<LineTripData>? seznam)
        {
            if (seznam is null || seznam.Count == 0) return [];
            int od = Math.Min(blok.Od, seznam.Count);
            return seznam.GetRange(od, Math.Min(blok.Pocet, seznam.Count - od));
        }
        return (Usek(_cachedHlavni), Usek(_cachedZrcadlene));
    }

    /// <summary>Všechny spoje bloku dohromady - pro výpočty, kterým je strana jedno (šířky sloupců).
    /// ⚠️ <b>Ne pro výšku řádků</b>: přípojné řádky se kreslí jen z hlavního směru.</summary>
    private List<LineTripData> SpojeBlokuVse(TableLayout.SloupcovyBlok blok)
    {
        var (hlavni, zrcadlene) = SpojeBloku(blok);
        return zrcadlene.Count == 0 ? hlavni : [.. hlavni, .. zrcadlene];
    }

    /// <summary>
    /// Kolik slotů má linka: u obousměrné ten <b>delší</b> ze směrů.
    /// </summary>
    /// <remarks>
    /// Dělení na bloky jde po slotech, ne po spojích - blok tak vezme z <b>obou</b> stran stejný
    /// úsek a je z něj celá tabulka. Kdyby se dělilo po spojích jednoho seznamu, první blok by
    /// vyšel jen se spoji tam a druhý jen se spoji zpět.
    /// </remarks>
    private int PocetSlotu() =>
        MathF.Max(_cachedHlavni?.Count ?? 0, _cachedZrcadlene?.Count ?? 0) is var n ? (int)n : 0;

    private static List<string> ParseStringArray(object? value) =>
        value is JsonArray arr ? [.. arr.Select(n => n?.ToString() ?? "")] : [];

    /// <summary>
    /// Rozdeli zonu hranicni zastavky na jednotlive zony.
    /// </summary>
    /// <remarks>
    /// Zastavka na hranici zon nese vsechny sve zony v jedne hodnote; oddelovac je carka, u nekterych
    /// firem lomitko (zmereno 31. 8. na sesti firmach: 301 738 zastavek z 1 160 432 ma vic nez jednu
    /// zonu, carka 301 334x, lomitko 404x, nejvic tri zony na zastavku). Delime proto obojim - jeden
    /// z tech tvaru by jinak zustal v jednom sloupci a vypadal jako chyba jen u nekterych zakazniku.
    /// </remarks>
    internal static List<string> RozdelZony(string zona) =>
        string.IsNullOrWhiteSpace(zona)
            ? []
            : [.. zona.Split([',', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

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
            Avl = o["AVL"]?.ToString() ?? "A",
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
    /// Nejmenší vůle kolem času v buňce spoje (mm celkem, půl na každou stranu).
    /// </summary>
    /// <remarks>
    /// Drží se **vždy**, i při nulovém vnitřním okraji a při zadaném dnu menším, než čas měří.
    /// Bez ní procházely svislé čáry vedle číslic a časy sousedních sloupců se slévaly.
    /// <para>Záměrně konstanta, ne další pole formuláře: je to pojistka čitelnosti, ne volba -
    /// vzduch kolem času si uživatel přidává vnitřním okrajem. Hodnota odpovídá výchozímu okraji
    /// (0,5 mm na stranu), takže se dnešním šablonám nic nemění.</para>
    /// </remarks>
    private const float MinVuleKolemCasuMm = 1f;

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
        // ⚠️ Měří se **skutečný tvar času**, ne vždycky „00:00". Při formátu s menšími minutami je
        // obsah „23 59" - hodina bez vedoucí nuly v plné velikosti, minuta zmenšená - a to je
        // znatelně užší. Dokud se tu měřilo „00:00", byl dolní práh sloupce zbytečně velký
        // a do řádku se vešlo míň spojů, než se reálně vejde (nález Jan 7. 9.).
        float textPt;
        if (FormatCasu == FormatCasuSpoje.HodinaVetsi)
        {
            float pomer = Math.Clamp(PomerMinuty, 0.3f, 1f);
            using var maly = new SKFont(GetTypeface(st), st.FontSize * pomer);
            textPt = font.MeasureText("23") + maly.MeasureText(" 59");
        }
        else
        {
            textPt = font.MeasureText("00:00");
        }
        // Kolem času musí zůstat vůle, i když si uživatel vynuluje okraje nebo zadá menší dno,
        // než čas měří - jinak procházejí svislé čáry vedle číslic a časy sousedních sloupců se
        // slévají (nález Jan 8. 9. na vyrenderované stránce při dnu 3 mm proti potřebným 3,99).
        // Vůle **není aditivní** k okraji: platí ta větší z nich, takže větší okraj dál funguje.
        float vzduch = MathF.Max(MinVuleKolemCasuMm, st.PaddingLeft + st.PaddingRight);
        float mm = (textPt * PtToMm) + vzduch;
        // Zadané dno smí sloupec už jen rozšířit. Kdo napíše méně, než se čas vejde, dostane
        // nejužší čitelnou šířku - ne rozbitou tabulku.
        if (NejmensiSirkaSpojeMm is float zadane)
            mm = MathF.Max(mm, zadane);
        return MathF.Max(MinAutoColumnWidthMm, mm);
    }

    /// <summary>Vyřešený popisný sloupec. <c>Key</c> je klíč té definice sloupce, ze které vznikl -
    /// km se expanduje na několik `RenderCol` a všechny nesou týž klíč.</summary>
    private sealed record RenderCol(string Key, float Width, string Header, ResolvedTextStyle HeaderStyle,
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
        ColumnKeys.Zone     => s => s.JePrijezdovy ? ""
            : !RozdelitZonyDoSloupcu ? s.Zona
            : kmIndex < s.Zony.Count ? s.Zony[kmIndex] : "",
        ColumnKeys.Km       => s => s.JePrijezdovy || kmIndex >= s.Km.Count ? "" : s.Km[kmIndex],
        ColumnKeys.Tc       => s => s.JePrijezdovy ? "" : s.Tc,
        ColumnKeys.Mhd      => s => s.JePrijezdovy ? "" : s.Mhd,
        // ⚠️ Mezera se přidává jen tehdy, když opravdu je co postavit před název. Dřív se řídila
        // tím, jestli zastávka má **jakýkoli** atribut - jenže řídicí atributy (T, P) se netisknou,
        // takže zastávka s nimi dostala název odsazený o mezeru bez viditelného důvodu. Vypadalo
        // to jako záměrné odsazení významných zastávek (nález Jan 7. 9.).
        ColumnKeys.StopName => s => s.JePrijezdovy ? "" : NazevSeZnackami(s.Atributy, s.Popis),
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
        List<LineStopData> stops, int kmCount, int tripCount, float rezervaMezerMm = 0f,
        float rezervaZnacekMm = 0f)
    {
        var structural = StructuralColumns();
        var trip = TripColumn();

        // Km má vlastní podmínku - jeho hodnoty jsou pole, ne jedna hodnota, a počet sub-sloupců
        // se odvozuje z dat (kmCount == 0 znamená „žádné km sloupce", ne „prázdné hodnoty").
        // ⚠️ Šipka směru má vlastní podmínku: nemá **žádný text**, takže by ji pravidlo „prázdný
        // sloupec se netiskne" schovalo vždy, i když ji uživatel zapnul. Kreslí se vektorově.
        int zonaCount = MaxZonaCount(stops);
        structural = structural.Where(c => c.Key switch
        {
            ColumnKeys.Km    => kmCount > 0,
            ColumnKeys.Arrow => true,
            // Rozpad zon se ridi poctem zon v datech, stejne jako km - „zadny sloupec" tu znamena
            // „zadna zona", ne „prazdne hodnoty".
            ColumnKeys.Zone when RozdelitZonyDoSloupcu => zonaCount > 0,
            _                => stops.Any(st => ValueFor(c.Key, 0)(st).Length > 0),
        }).ToList();

        // šířky: pevné (Width != null) honorovány; auto (null) sdílí zbytek se spoji
        float fixedSum = structural.Where(c => c.Width.HasValue)
            .Sum(c => c.Width!.Value * PocetPodsloupcu(c.Key, kmCount, zonaCount));
        int autoStructural = structural.Count(c => !c.Width.HasValue);
        int autoUnits = autoStructural + (trip.Width.HasValue ? 0 : tripCount);
        // Mezery mezi skupinami sloupců a místo na značky u času jdou ze stejné šířky jako
        // sloupce, takže je automatické sloupce nesmí rozdělit mezi sebe - jinak by tabulka
        // přetekla přes pravou hranu prvku.
        float autoEach = autoUnits > 0
            ? MathF.Max(MinAutoSloupecMm(), (Width - fixedSum - rezervaMezerMm - rezervaZnacekMm) / autoUnits)
            : 8f;
        // Strop se uplatní až po dnu, aby ho nemohl podlézt: kdo zadá strop menší než dno, dostane
        // dno (a čitelný čas), ne oříznutý sloupec. Platí jen na automatické sloupce - pevná šířka
        // sloupce je výslovné přání a strop ji nepřebíjí.
        if (NejvetsiSirkaSpojeMm is float strop && strop > 0f)
            autoEach = MathF.Max(MinAutoSloupecMm(), MathF.Min(autoEach, strop));

        var result = new List<RenderCol>();
        int nameIndex = -1;
        foreach (var col in structural)
        {
            var hs = ResolveHeaderStyle(col);
            var ds = ResolveDataStyle(col);
            string header = col.Header != "" ? col.Header : DefaultHeaderFor(col.Key);
            float w = col.Width ?? autoEach;
            int podsloupcu = PocetPodsloupcu(col.Key, kmCount, zonaCount);
            if (podsloupcu > 1 || col.Key == ColumnKeys.Km)
            {
                for (int k = 0; k < podsloupcu; k++)
                    result.Add(new RenderCol(col.Key, w, header, hs, ds, ValueFor(col.Key, k)));
            }
            else
            {
                if (col.Key == ColumnKeys.StopName) nameIndex = result.Count;
                result.Add(new RenderCol(col.Key, w, header, hs, ds, ValueFor(col.Key, 0)));
            }
        }
        float tripW = trip.Width ?? autoEach;
        return (result, tripW, nameIndex);
    }

    /// <summary>
    /// Kolik mm si vezme značka návaznosti, která se tiskne <b>vedle času</b> ve stejné buňce.
    /// </summary>
    /// <remarks>
    /// Měří se stejným způsobem jako <see cref="MinAutoSloupecMm"/> (velikost v pt → míra v pt),
    /// aby to šlo spočítat bez kreslicího kontextu - šířky sloupců se počítají i při zjišťování
    /// výšky prvku. Do šířky patří i mezera za glyfem, protože se kreslí „&lt;glyf&gt; &lt;čas&gt;".
    /// </remarks>
    private float SirkaZnackyMm()
    {
        var st = ResolveDataStyle(TripColumn());
        using var normal = new SKFont(GetTypeface(st), st.FontSize);
        using var symbol = new SKFont(
            GetTypeface(FontResolver.SymbolFontFamily(_cachedCulture)), st.FontSize);
        // Vyčká má glyf „A", navazuje „B" - bere se širší z nich, ať se vejde obojí.
        float glyf = MathF.Max(symbol.MeasureText("A"), symbol.MeasureText("B"));
        return (glyf + normal.MeasureText(" ")) * PtToMm;
    }

    /// <summary>
    /// Nejvyšší počet značek, který v tomhle sloupci spoje někde je - napříč všemi zastávkami.
    /// </summary>
    /// <remarks>
    /// ⚠️ Podle tohohle čísla <b>roste šířka sloupce</b>, a je to <b>parita se starým modulem</b>:
    /// ten má pro sloupec spoje čtyři šířky a vybírá mezi nimi právě podle největšího počtu značek
    /// ve sloupci (<c>JRapiLinPrtWin.mac:1793</c>, počet se bere z vedlejšího slotu „znacky spoje",
    /// tamtéž <c>:1769</c> a <c>:2611</c>). Text tedy <b>nezmenšuje</b> - dělá místo.
    /// <para>Podmínka musí být tatáž jako u kreslení (značka se tiskne jen tam, kde je i čas
    /// a kde je značka ověřený piktogram), jinak by se rezervovalo místo pro něco, co se nenakreslí.</para>
    /// </remarks>
    private int ZnacekVeSloupci(LineTripData spoj, List<LineStopData> stops)
    {
        int max = 0;
        for (int si = 0; si < stops.Count; si++)
        {
            var stop = stops[si];
            if (!RowConditions.IsVisible(RowVisibilityExpression, stop.SourceRow)) continue;
            string znak = ZnakNavaznosti(spoj, stop.Poradi);
            if (znak == "") continue;
            if (si >= spoj.Casy.Count || spoj.Casy[si] == "") continue;
            if (!SymbolRunRenderer.MaPiktogram(
                    SymbolRunRenderer.BuildRuns([znak], SymbolMap.ResolveLinkovy))) continue;
            if (max < 1) max = 1;
        }
        return max;
    }

    /// <summary>Šířky sloupců spojů bloku - základ plus místo na značky, které ten sloupec nese.
    /// Se zapnutou <see cref="JednotnaSirkaSpoju"/> dostanou všechny šířku toho nejširšího.</summary>
    private List<float> SirkySpoju(IReadOnlyList<LineTripData> spojeBloku, List<LineStopData> stops,
        float zakladMm, float znackaMm)
    {
        if (!JednotnaSirkaSpoju)
            return [.. spojeBloku.Select(t => zakladMm + ZnacekVeSloupci(t, stops) * znackaMm)];

        float jednotna = zakladMm + NejvicZnacekVeSloupci(stops) * znackaMm;
        return [.. spojeBloku.Select(_ => jednotna)];
    }

    /// <summary>Nejvyšší počet značek, který nese kterýkoli sloupec spoje <b>celé linky</b>.
    /// Přes celou linku, ne přes blok - viz <see cref="JednotnaSirkaSpoju"/>.</summary>
    private int NejvicZnacekVeSloupci(List<LineStopData> stops)
    {
        int max = 0;
        foreach (var t in _cachedTrips ?? [])
        {
            int n = ZnacekVeSloupci(t, stops);
            if (n > max) max = n;
        }
        return max;
    }

    /// <summary>
    /// Kolik místa si vezmou značky v <b>nejhorším</b> bloku o daném počtu spojů.
    /// </summary>
    /// <remarks>
    /// Bere se maximum přes všechna okna po sobě jdoucích spojů, ne součet za celou linku: kdyby se
    /// rezervovalo za linku, u lince plné značek by se šířka rozdala tak, že by se nevešlo nic.
    /// Maximum přes okna drží dvě věci naráz - základ sloupce je pro všechny bloky stejný (invariant
    /// z 25. 8.) a žádný blok nepřeteče.
    /// </remarks>
    private float RezervaZnacekMm(List<LineTripData> trips, List<LineStopData> stops,
        float znackaMm, int naBlok)
    {
        if (znackaMm <= 0f || naBlok <= 0) return 0f;
        // Jednotná šířka: každý sloupec nese místo na nejširší značkový sloupec linky, takže se
        // rezervuje za všechny. Musí to být týž vzorec jako v `SirkySpoju`, jinak by se blok
        // spočítal užší, než se nakreslí, a přetekl by přes hranu prvku.
        if (JednotnaSirkaSpoju) return naBlok * NejvicZnacekVeSloupci(stops) * znackaMm;
        var znacky = trips.Select(t => ZnacekVeSloupci(t, stops)).ToList();
        int nejvic = 0, okno = 0;
        for (int i = 0; i < znacky.Count; i++)
        {
            okno += znacky[i];
            if (i >= naBlok) okno -= znacky[i - naBlok];
            if (okno > nejvic) nejvic = okno;
        }
        return nejvic * znackaMm;
    }

    /// <summary>
    /// Rozvržení sloupců kresleného bloku - <b>jediné místo, kde se ze šířek stávají souřadnice</b>.
    /// </summary>
    /// <remarks>
    /// ⚠️ Do 27. 8. si odsazení sloupce spoje počítala každá cesta sama jako `fixedSum + i * tripW`:
    /// řádek platnosti, značky vedle času i piktogram zastávky. Mezera mezi skupinami sloupců ten
    /// vzorec ruší - odsazení už není lineární v indexu spoje. Proto jedno místo, které o mezerách
    /// ví, a cesty, které se ho jen ptají; jinak by se každá musela opravit zvlášť a ta, na kterou
    /// by se zapomnělo, by kreslila značky mimo svůj sloupec.
    /// </remarks>
    private sealed class SloupcovaMrizka
    {
        private readonly List<int> _mezeraPred;
        private readonly float[] _sirkySpoju;
        private readonly float[] _sirkyZrcadlene;

        public SloupcovaMrizka(IReadOnlyList<RenderCol> popisne, IReadOnlyList<float> sirkySpoju,
            float zakladSpojeMm, float mezeraMm, IEnumerable<int> hraniceSkupin,
            IReadOnlyList<float>? sirkyZrcadlene = null, int prazdnychZrcadlenych = 0)
        {
            PrazdnychZrcadlenych = Math.Max(0, prazdnychZrcadlenych);
            Popisne = popisne;
            _sirkySpoju = [.. sirkySpoju];
            _sirkyZrcadlene = sirkyZrcadlene is null ? [] : [.. sirkyZrcadlene];
            ZakladSpojeMm = zakladSpojeMm;
            MezeraMm = mezeraMm;
            _mezeraPred = mezeraMm > 0f ? [.. hraniceSkupin] : [];
            PopisneSirkaMm = popisne.Sum(c => c.Width);
            ZrcadlenaSirkaMm = _sirkyZrcadlene.Sum() + (PrazdnychZrcadlenych * ZakladSpojeMm);
        }

        public IReadOnlyList<RenderCol> Popisne { get; }
        public float MezeraMm { get; }
        public float PopisneSirkaMm { get; }

        /// <summary>
        /// Kolik sloupců nese <b>zrcadlený</b> blok - spoje opačného směru nalevo od zastávek
        /// (obousměrný tisk). 0 = jednosměrná sestava, tedy dosavadní tvar.
        /// </summary>
        public int ZrcadlenychSpoju => _sirkyZrcadlene.Length;

        /// <summary>Obsazená šířka zrcadleného bloku (bez mezery za ním), včetně dorovnání.</summary>
        public float ZrcadlenaSirkaMm { get; }

        /// <summary>
        /// Kolik prázdných sloupců dorovnává zrcadlenou stranu - stojí <b>před</b> jejími spoji.
        /// </summary>
        /// <remarks>
        /// Dorovnání musí být na **vnější** hraně, jinak se posunou popisné sloupce a zastávky
        /// v posledním bloku nesedí svisle na bloky nad sebou (pravá strana se dorovnává na svém
        /// vnějším okraji, tedy za spoji).
        /// </remarks>
        public int PrazdnychZrcadlenych { get; }

        /// <summary>Mezera za zrcadleným blokem - je to skupina sloupců jako každá jiná.</summary>
        public float MezeraZaZrcadlenymMm => ZrcadlenychSpoju > 0 ? MezeraMm : 0f;

        /// <summary>Šířka konkrétního zrcadleného sloupce spoje.</summary>
        public float SirkaZrcadlenehoMm(int index) =>
            index >= 0 && index < _sirkyZrcadlene.Length ? _sirkyZrcadlene[index] : ZakladSpojeMm;

        /// <summary>Odsazení levé hrany zrcadleného sloupce od levé hrany prvku.</summary>
        public float XZrcadlenehoMm(int index)
        {
            float x = PrazdnychZrcadlenych * ZakladSpojeMm;
            for (int i = 0; i < index && i < _sirkyZrcadlene.Length; i++) x += _sirkyZrcadlene[i];
            return x;
        }

        /// <summary>Šířka sloupce spoje <b>bez</b> místa na značky - pro dorovnání prázdnými
        /// sloupci, které žádné značky nenesou.</summary>
        public float ZakladSpojeMm { get; }

        /// <summary>
        /// Šířka konkrétního sloupce spoje - <b>sloupce nejsou stejně široké</b>.
        /// </summary>
        /// <remarks>
        /// Sloupec, ve kterém je někde značka návaznosti, je o ni širší. Je to parita se starým
        /// modulem, který má pro sloupec spoje čtyři šířky a vybírá podle počtu značek
        /// (<c>JRapiLinPrtWin.mac:1793</c>); rozhodnutí Jan 27. 8. po přečtení legacy.
        /// </remarks>
        public float SirkaSpojeMm(int indexSpoje) =>
            indexSpoje >= 0 && indexSpoje < _sirkySpoju.Length ? _sirkySpoju[indexSpoje] : ZakladSpojeMm;

        /// <summary>Začíná před tímto sloupcem spoje nová skupina, tedy mezera? Index 0 je hranice
        /// mezi popisným blokem a spoji.</summary>
        public bool MezeraPred(int indexSpoje) => _mezeraPred.Contains(indexSpoje);

        /// <summary>Odsazení levé hrany sloupce spoje od levé hrany prvku.</summary>
        public float XSpojeMm(int indexSpoje)
        {
            float x = ZrcadlenaSirkaMm + MezeraZaZrcadlenymMm
                + PopisneSirkaMm + MezeraMm * _mezeraPred.Count(m => m <= indexSpoje);
            for (int i = 0; i < indexSpoje && i < _sirkySpoju.Length; i++) x += _sirkySpoju[i];
            return x;
        }

        /// <summary>Součet šířek sloupců spojů v úseku [od, do) - pro sloučené buňky pásu skupin.</summary>
        public float SirkaUsekuMm(int od, int doExkluzivne)
        {
            float w = 0f;
            for (int i = od; i < doExkluzivne && i < _sirkySpoju.Length; i++) w += _sirkySpoju[i];
            return w;
        }

        /// <summary>Totéž pro zrcadlený blok.</summary>
        public float SirkaUsekuZrcadlenehoMm(int od, int doExkluzivne)
        {
            float w = 0f;
            for (int i = od; i < doExkluzivne && i < _sirkyZrcadlene.Length; i++) w += _sirkyZrcadlene[i];
            return w;
        }

        /// <summary>Odsazení levé hrany popisného sloupce od levé hrany prvku.</summary>
        public float XPopisnehoMm(int indexSloupce)
        {
            float x = ZrcadlenaSirkaMm + MezeraZaZrcadlenymMm;
            for (int i = 0; i < indexSloupce && i < Popisne.Count; i++) x += Popisne[i].Width;
            return x;
        }
    }

    /// <summary>
    /// Indexy sloupců spojů, před kterými začíná nová skupina sloupců. <b>0 je vždy uvnitř</b> -
    /// je to hranice mezi popisným blokem a spoji.
    /// </summary>
    /// <remarks>
    /// Skupiny dnů se do hranic počítají jen se zapnutými nadpisy skupin: bez pásu není z tisku
    /// poznat, že tam nějaká skupina začíná, a mezera by vypadala jako náhodná díra v tabulce.
    /// </remarks>
    private List<int> HraniceSkupin(IReadOnlyList<LineTripData> spoje)
    {
        var hranice = new List<int> { 0 };
        if (!ShowGroupHeaders) return hranice;
        for (int i = 1; i < spoje.Count; i++)
            if (spoje[i].Skupina != spoje[i - 1].Skupina) hranice.Add(i);
        return hranice;
    }

    /// <summary>
    /// Kolik šířky si vezmou mezery mezi skupinami. Počítá se z <b>celé linky</b>, ne z kresleného
    /// bloku.
    /// </summary>
    /// <remarks>
    /// ⚠️ Kdyby se rezervovalo podle bloku, měl by každý blok jinak široké sloupce - a to je přesně
    /// vada, kterou řeší invariant z 25. 8. (poslední blok vycházel se sloupci 58,8 mm proti 6,4 mm
    /// v prvním a čas stál uprostřed šesticentimetrového pole). Cena: blok, do kterého padne méně
    /// skupin, nechá vpravo o mezeru víc volna - stejně jako to volné místo, které tam tabulka
    /// nechává i dnes.
    /// </remarks>
    private float RezervaMezerMm(IReadOnlyList<LineTripData> vsechnySpoje) =>
        MezeraMeziSkupinamiMm <= 0f ? 0f : MezeraMeziSkupinamiMm * HraniceSkupin(vsechnySpoje).Count;

    /// <summary>Šířka, kterou zabere blok s daným počtem spojů - s rezervou na mezery, ne
    /// s mezerami toho bloku, ať se každý blok do prvku vejde.</summary>
    private static float SirkaBlokuMm(IReadOnlyList<RenderCol> popisne, float sloupecSpojeMm,
        float rezervaMezerMm, float rezervaZnacekMm, int pocetSpoju) =>
        popisne.Sum(c => c.Width) + rezervaMezerMm + rezervaZnacekMm + pocetSpoju * sloupecSpojeMm;

    /// <summary>
    /// Kolik slotů spojů se při dané šířce sloupce do prvku vejde - podklad pro dorovnání
    /// prázdnými sloupci.
    /// </summary>
    /// <remarks>
    /// Počítá se ve <b>slotech</b>, ne ve sloupcích: u obousměrné sestavy zabere slot sloupec na
    /// každé straně zastávek, takže se do šířky vejde poloviční počet.
    /// </remarks>
    private static int KapacitaSlotu(IReadOnlyList<RenderCol> popisne, float sloupecSpojeMm,
        float rezervaMezerMm, float rezervaZnacekMm, int sloupcuNaSlot, float sirkaPrvku)
    {
        if (sloupecSpojeMm <= 0f || sloupcuNaSlot <= 0) return 0;
        float volno = sirkaPrvku - popisne.Sum(c => c.Width) - rezervaMezerMm - rezervaZnacekMm;
        return volno <= 0f ? 0 : (int)MathF.Floor(volno / (sloupecSpojeMm * sloupcuNaSlot));
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
    /// <para><b>Kde je z čeho vybírat, dostane přednost oddělení skupin dnů</b> - viz
    /// <see cref="TableLayout.ZvolBloky"/>.</para>
    /// </remarks>
    private List<TableLayout.SloupcovyBlok> SloupcoveBloky(List<LineStopData> stops, int kmCount)
    {
        int pocetSpoju = PocetSlotu();
        if (pocetSpoju <= 0) return [];

        // Kolik spojů se vejde, **nejde spočítat jedním výpočtem**: popisné sloupce jsou z větší
        // části automatické, takže jejich šířka závisí na tom, kolik spojů s nimi sdílí místo.
        // Hledá se proto největší počet, který se **při vlastním rozvržení** opravdu vejde - kdyby
        // se pravidlo pro šířky sloupců změnilo, dělení se přizpůsobí samo místo aby se rozešlo.
        // O čitelnost bloku se stará dno šířky sloupce, viz `MinAutoSloupecMm`.
        int naBlok = NejvicSpojuNaBlok(stops, kmCount, pocetSpoju,
            RezervaMezerMm(_cachedTrips ?? []), SirkaZnackyMm(), SloupcuNaSlot());
        return TableLayout.ZvolBloky(SkupinaSlotu(), pocetSpoju, naBlok);
    }

    /// <summary>Skupina dnů každého slotu dělení, nebo null, když ji nelze jednoznačně určit.</summary>
    /// <remarks>
    /// Null u <b>obousměrné</b> sestavy: slot tam nese spoj z každého směru a jejich skupiny se
    /// neshodují, takže hranice skupiny není jedno místo. Null i bez zapnutých nadpisů skupin -
    /// bez pásu není z tisku poznat, že tam skupina začíná, a oddělený blok by vypadal jako
    /// náhodná díra v tabulce (týž důvod jako u <see cref="HraniceSkupin"/>).
    /// </remarks>
    private List<int>? SkupinaSlotu()
    {
        if (!ShowGroupHeaders) return null;
        if ((_cachedZrcadlene?.Count ?? 0) > 0) return null;
        return _cachedHlavni is { Count: > 0 } hlavni ? [.. hlavni.Select(t => t.Skupina)] : null;
    }

    /// <summary>Jak vyjde jeden blok: které spoje nese, jak je široký, kolik místa zbylo
    /// na jeden sloupec spoje a jak široký je každý sloupec zvlášť.</summary>
    /// <remarks><paramref name="SloupecSpojeMm"/> je <b>základ</b> sloupce, společný všem blokům;
    /// <paramref name="SirkySloupcu"/> jsou skutečné šířky, které se od základu liší o místo
    /// na značky návaznosti. Rozdíl mezi nimi je celý smysl <see cref="JednotnaSirkaSpoju"/>.</remarks>
    internal readonly record struct BlokRozvrh(int Od, int Pocet, float SirkaMm, float SloupecSpojeMm,
        IReadOnlyList<float> SirkySloupcu);

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
        var bloky = SloupcoveBloky(stops, kmCount);
        int naBlok = bloky.Count > 0 ? bloky.Max(b => b.Pocet) : 0;
        // ⚠️ Do šířky se počítá **počet sloupců**, ne slotů: u obousměrné sestavy má slot sloupec
        // na každé straně zastávek. Stejný přepočet dělá kreslení i `Vejde`; dokud tady chyběl,
        // vycházela šířka dvousměrné linky na polovinu a invariant „blok se vejde do šířky prvku"
        // se u ní nehlídal (nález 4. 9., `B122`).
        int sloupcuBloku = naBlok * SloupcuNaSlot();
        float rezerva = RezervaMezerMm(trips);
        float znackaMm = SirkaZnackyMm();
        float rezervaZnacek = RezervaZnacekMm(trips, stops, znackaMm, sloupcuBloku);
        return bloky.Select(b =>
        {
            var (cols, tripW, _) = BuildColumns(stops, kmCount, sloupcuBloku, rezerva, rezervaZnacek);
            var sirky = SirkySpoju(SpojeBlokuVse(b), stops, tripW, znackaMm);
            return new BlokRozvrh(b.Od, b.Pocet,
                cols.Sum(c => c.Width) + rezerva + sirky.Sum(), tripW, sirky);
        }).ToList();
    }

    /// <summary>Největší počet spojů, který se do šířky vejde i s popisnými sloupci spočítanými
    /// pro týž počet. Půlením intervalu, ať se `BuildColumns` nevolá pro každý počet zvlášť.</summary>
    private int NejvicSpojuNaBlok(List<LineStopData> stops, int kmCount, int pocetSpoju,
        float rezervaMezerMm, float znackaMm, int sloupcuNaSlot)
    {
        bool Vejde(int n)
        {
            // U obousměrné sestavy zabere slot sloupec na každé straně zastávek, takže se do šířky
            // musí vejít dvojnásobek - jinak by zrcadlený blok tabulku vystrčil za hranu prvku.
            int sloupcu = n * sloupcuNaSlot;
            float rezervaZnacek = RezervaZnacekMm(_cachedTrips ?? [], stops, znackaMm, sloupcu);
            var (cols, tripW, _) = BuildColumns(stops, kmCount, sloupcu, rezervaMezerMm, rezervaZnacek);
            return SirkaBlokuMm(cols, tripW, rezervaMezerMm, rezervaZnacek, sloupcu) <= Width + 0.01f;
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

    /// <summary>
    /// Text buňky se zastávkou: zobrazitelné značky, mezera, název. Bez značek jen název.
    /// </summary>
    /// <remarks>
    /// ⚠️ Mezera patří jen za skutečnou značku. Dřív se přidávala, kdykoli zastávka měla
    /// **jakýkoli** atribut - jenže řídicí atributy („T" tarifní bod, „P" oddělený příjezd)
    /// se netisknou, takže právě významné zastávky vyšly odsazené o mezeru bez viditelného
    /// důvodu a vypadalo to jako záměr (nález Jan 7. 9.).
    /// </remarks>
    internal static string NazevSeZnackami(IReadOnlyList<string> atributy, string popis)
    {
        string znacky = string.Join("", atributy.Where(IsDisplayableStopAttribute));
        return znacky == "" ? popis : znacky + " " + popis;
    }

    /// <summary>Má zastávka v trase vyžádané tučné písmo? Atribut „T" znamená v číselníku
    /// atributů trasy přímo „tučně", „P" je „podtrženo s příjezdem a odjezdem" a starý tisk
    /// ho sází tučně také.</summary>
    private static bool MaTucneZTrasy(LineStopData s) =>
        s.Atributy.Contains("T") || s.Atributy.Contains("P");

    /// <summary>
    /// Nadpisy popisnych sloupcu - rozpadle zony dostanou <b>jeden spolecny</b> pres vsechny sve dily.
    /// </summary>
    /// <remarks>
    /// „Zona" nad kazdym dilem vypadala jako nekolik ruznych sloupcu, prestoze je to porad tataz vec,
    /// jen rozlozena (rozhodnuti Jan 1. 9. po vyzkouseni). Slouceni znamena jednu sirsi bunku misto
    /// nekolika, takze v radku nadpisu zaroven zmizi cary mezi dily.
    /// <para>⚠️ Kilometry se **zamerne** neslucuji: jejich dily jsou km jednotlivych useku, tedy ruzne
    /// hodnoty se stejnym nazvem, a slouceni by zmenilo hotove sestavy vsem, kdo km sloupce maji.</para>
    /// </remarks>
    private List<(int Index, float Sirka, string Text, ResolvedTextStyle Styl)> NadpisyPopisnychSloupcu(
        IReadOnlyList<RenderCol> fixedCols)
    {
        var vysledek = new List<(int, float, string, ResolvedTextStyle)>();
        for (int ci = 0; ci < fixedCols.Count; ci++)
        {
            int span = 1;
            if (fixedCols[ci].Key == ColumnKeys.Zone && RozdelitZonyDoSloupcu)
                while (ci + span < fixedCols.Count && fixedCols[ci + span].Key == ColumnKeys.Zone) span++;
            float sirka = 0f;
            for (int k = 0; k < span; k++) sirka += fixedCols[ci + k].Width;
            vysledek.Add((ci, sirka, fixedCols[ci].Header, fixedCols[ci].HeaderStyle));
            ci += span - 1;
        }
        return vysledek;
    }

    /// <summary>Nadpisy popisnych sloupcu (text, sirka) tak, jak se vykresli - pro testy.</summary>
    internal List<(string Text, float Sirka)> NadpisyProTest(DataContext data)
    {
        EnsureParsed(data);
        var stops = _cachedStops ?? [];
        var (cols, _, _) = BuildColumns(stops, MaxKmCount(stops), 1);
        return [.. NadpisyPopisnychSloupcu(cols).Select(n => (n.Text, n.Sirka))];
    }

    /// <summary>
    /// Popisove sloupce tak, jak se opravdu vykresli - pro testy: dvojice (klic, hodnota bunky).
    /// </summary>
    /// <remarks>
    /// Rozpad zon meni **pocet** sloupcu, ne jen text v nich, takze na to nestaci porovnat obrazek -
    /// z nej neni poznat, jestli dve zony vedle sebe jsou dva sloupce, nebo jedna bunka s carkou.
    /// </remarks>
    internal List<(string Klic, string Hodnota)> PopisneSloupceProTest(DataContext data, int radek = 0)
    {
        EnsureParsed(data);
        var stops = _cachedStops ?? [];
        var (cols, _, _) = BuildColumns(stops, MaxKmCount(stops), 1);
        return [.. cols.Select(c => (c.Key, stops.Count > radek ? c.Value(stops[radek]) : ""))];
    }

    /// <summary>Na kolik sloupcu se klic rozpadne - km dle dat, zona jen se zapnutym rozpadem.</summary>
    private int PocetPodsloupcu(string key, int kmCount, int zonaCount) => key switch
    {
        ColumnKeys.Km => kmCount,
        ColumnKeys.Zone when RozdelitZonyDoSloupcu => zonaCount,
        _ => 1,
    };

    /// <summary>Nejvic zon na jedne zastavce - urcuje pocet sloupcu zon pri rozpadu.</summary>
    private int MaxZonaCount(List<LineStopData> stops) =>
        RozdelitZonyDoSloupcu && Columns.Any(c => c.Visible && c.Key == ColumnKeys.Zone) && stops.Count > 0
            ? stops.Max(s => s.Zony.Count)
            : 0;

    private int MaxKmCount(List<LineStopData> stops) =>
        Columns.Any(c => c.Visible && c.Key == ColumnKeys.Km) && stops.Count > 0 ? stops.Max(s => s.Km.Count) : 0;

    private bool HasNzRow(List<LineTripData> trips) => ShowNzRow && trips.Any(t => t.Nz.Length > 0);

    /// <summary>Kolik značek platnosti se vejde na jeden řádek, než se zalomí pod něj.</summary>
    /// <remarks>Strop starého modulu (`POCETZNAKUPLATNOSTI`, `JRapiLinPrtWin.mac:25`).</remarks>
    private const int ZnacekNaRadekPlatnosti = 2;

    /// <summary>Kolik řádků platnosti tabulka má = kolik jich potřebuje nejbohatší spoj (0 = žádné).</summary>
    /// <remarks>
    /// Značky se dělí <b>mechanicky po dvou</b> v tom pořadí, v jakém je pošle agenda - ne podle
    /// významu. Řádek proto může nést vedle sebe den i vlastnost vozidla; přesně tak to dělá starý
    /// modul (`Spoj2Tb`, `JRapiLinPrtWin.mac:3752-3775`: čítač uzavře řádek po každé druhé značce
    /// a založí `1.1`, `1.2`…). Doloženo jeho tiskem linky 108360 - spoj 1017 má ⚒ i ♿ na jednom
    /// řádku, spoje 4009/4025 mají ⑥ ✝ na prvním a ♿ na druhém.
    /// <para>Řídí se <b>nejbohatším spojem</b>, ne každým sloupcem zvlášť - jinak by řádky nesedly
    /// na sebe. Spoj s méně značkami má zbylé řádky prázdné.</para>
    /// </remarks>
    private int PocetRadkuPlatnosti(List<LineTripData> trips) =>
        !ShowPlatnostRow || trips.Count == 0
            ? 0
            : trips.Max(t => (t.Platnost.Count + ZnacekNaRadekPlatnosti - 1) / ZnacekNaRadekPlatnosti);

    /// <summary>Značky spoje patřící na <paramref name="radek"/>-tý řádek platnosti (0-based).</summary>
    private static List<string> ZnackyRadku(LineTripData trip, int radek) =>
        [.. trip.Platnost.Skip(radek * ZnacekNaRadekPlatnosti).Take(ZnacekNaRadekPlatnosti)];

    /// <summary>Kolik řádků turnusů tabulka má = nejvíc turnusů u jednoho spoje (0 = žádné).</summary>
    /// <remarks>
    /// Řídí se **nejbohatším spojem**, ne každým sloupcem zvlášť - jinak by řádky nesedly na sebe.
    /// Stejně to počítá starý modul (`TUmax`, `JRapiLinPrtWin.mac:2718`). Turnusy posílá server jen
    /// se zapnutým parametrem, takže bez něj je to nula a tabulka vypadá jako dosud.
    /// </remarks>
    private int PocetTurnusovychRadku(List<LineTripData> trips) =>
        !TurnusyKeSpojum || trips.Count == 0 ? 0 : trips.Max(t => t.Turnusy.Count);
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
        return [.. SloupcoveBloky(_cachedStops!, MaxKmCount(_cachedStops!))
            .Select(b =>
            {
                var (hlava, radky) = VyskyRadkuBloku(SpojeBloku(b).Hlavni, SpojeBloku(b).Zrcadlene);
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
    /// <summary>Výška řádku značek - datový řádek zvětšený o <see cref="PomerZnacek"/>.
    /// Jediné místo výpočtu, ať se render a výpočet výšky prvku nerozejdou.</summary>
    private float VyskaRadkuZnacek(float rowH) => rowH * Math.Clamp(PomerZnacek, 1f, 3f);

    private (List<float> Hlava, List<float> Radky) VyskyRadkuBloku(
        IReadOnlyList<LineTripData> spojeBloku, IReadOnlyList<LineTripData>? zrcadleneBloku = null)
    {
        var trips = _cachedTrips!;
        float headerH = ComputeHeaderH(MaxHeaderFontPt());
        float rowH = DataRowHeight ?? ComputeRowH(MaxDataFontPt());

        var hlava = new List<float>();
        if (HasGroupHeaderBand(trips, _cachedGroups!)) hlava.Add(headerH);   // pás skupin dnů
        hlava.Add(headerH);                                                  // řádek čísel spojů
        for (int tr = 0; tr < PocetTurnusovychRadku(trips); tr++) hlava.Add(rowH);
        float znackaH = VyskaRadkuZnacek(rowH);
        for (int pr = 0; pr < PocetRadkuPlatnosti(trips); pr++) hlava.Add(znackaH);
        if (HasNzRow(trips)) hlava.Add(znackaH);

        // Jen viditelné zastávky a jejich přípojné řádky (matched na Poradi zastávky - jako render).
        var visibleStops = RowConditions.VisibleRows(_cachedStops!, s => s.SourceRow, RowVisibilityExpression);
        var sOdjezdem = ShowPripojeRows ? PoradiSOdjezdem(visibleStops) : null;
        var radky = new List<float>();
        foreach (var zastavka in visibleStops)
        {
            radky.Add(rowH);
            if (sOdjezdem is null || !NesePripoje(zastavka, sOdjezdem)) continue;
            // ⚠️ Ze spojů **tohohle bloku, obou stran** - a kreslí se taky tak (od 7. 9. nese
            // přípojný řádek časy hlavní i zrcadlené strany, `B128`). Třikrát se tu už rozešel
            // výpočet s kreslením a pokaždé to skončilo řezem uprostřed hlavičky dalšího bloku:
            // 25. 8. počítáním ze všech spojů linky, 7. 9. z obou směrů, když se kreslil jen
            // jeden. Teď platí obojí, takže se sem musí předat obě sady - jinak vyjde jiný POČET
            // řádků, protože slučování spojuje přípoj obou směrů do jednoho.
            int pripoju = PripojneRadky(zastavka, spojeBloku, zrcadleneBloku).Count;
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
        var bloky = SloupcoveBloky(_cachedStops!, MaxKmCount(_cachedStops!));

        var zlomy = new List<Zlom>();
        float zacatekBloku = 0f;
        for (int b = 0; b < bloky.Count; b++)
        {
            var (hlava, radky) = VyskyRadkuBloku(SpojeBloku(bloky[b]).Hlavni, SpojeBloku(bloky[b]).Zrcadlene);
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
        var bloky = SloupcoveBloky(stops, kmCount);

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
            int sloupcuNaSlot = SloupcuNaSlot();
            float rezervaMezer = RezervaMezerMm(trips);
            float znackaMm = SirkaZnackyMm();
            float rezervaZnacek = RezervaZnacekMm(trips, stops, znackaMm, naBlok * sloupcuNaSlot);
            float y = Y;
            foreach (var blok in bloky)
            {
                var (fixedCols, tripW, nameIndex) =
                    BuildColumns(stops, kmCount, naBlok * sloupcuNaSlot, rezervaMezer, rezervaZnacek);
                var (spojeBloku, zrcadleneBloku) = SpojeBloku(blok);
                // Hranice skupin se berou ze spojů **tohoto** bloku - mezera se kreslí tam, kde
                // v bloku opravdu začíná nová skupina. Rezerva na šířku je naopak z celé linky.
                // Dorovnání prázdnými sloupci (#8372) se počítá **pro každou stranu zvlášť** -
                // v obousměrném tisku může mít blok jinak plnou levou a pravou stranu (jeden směr
                // má míň spojů). Levá strana ho potřebuje znát už při stavbě mřížky, protože její
                // sloupce posouvá; pravá se dorovnává až v hotovém plánu.
                // Dorovnává se na kapacitu prvku, ne jen na nejplnější blok. Bez stropu je obojí
                // totéž (sloupce zbytek prvku spotřebují), ale se stropem zůstane vpravo volno
                // i u tabulky, která se vůbec neřeže - a právě to má dorovnání vyplnit.
                int kapacita = Math.Max(naBlok, KapacitaSlotu(fixedCols, tripW, rezervaMezer,
                    rezervaZnacek, sloupcuNaSlot, Width));
                int prazdnychVpravo = PadEmptyColumns ? Math.Max(0, kapacita - spojeBloku.Count) : 0;
                int prazdnychVlevo = PadEmptyColumns && sloupcuNaSlot > 1
                    ? Math.Max(0, kapacita - zrcadleneBloku.Count) : 0;

                var mrizka = new SloupcovaMrizka(fixedCols,
                    SirkySpoju(spojeBloku, stops, tripW, znackaMm), tripW,
                    MezeraMeziSkupinamiMm, HraniceSkupin(spojeBloku),
                    SirkySpoju(zrcadleneBloku, stops, tripW, znackaMm), prazdnychVlevo);

                float headerH = ComputeHeaderH(MaxHeaderFontPt()) * scale;
                float rowH = (DataRowHeight ?? ComputeRowH(MaxDataFontPt())) * scale;
                // Pás značek nad časy je vyšší i větším písmem - viz PomerZnacek.
                float znackaRowH = VyskaRadkuZnacek(rowH);
                float znackaScale = scale * Math.Clamp(PomerZnacek, 1f, 3f);

                var plan = BuildRowPlan(stops, spojeBloku, zrcadleneBloku, trips, groups, mrizka, nameIndex,
                    headerH, rowH, PripojRowH() * scale,
                    Grid.ShowColumnLines ? paints.Grid : null,
                    Grid.ShowRowLines ? paints.Grid : null,
                    headerResolved, headerCentered,
                    tripHeaderStyle, tripDataStyle,
                    ctx, GetTypeface(FontResolver.SymbolFontFamily(ctx.Data?.Culture)), scale);

                // Neúplný blok dorovnat prázdnými sloupci, ať mají všechny bloky stejný počet
                // sloupců a tabulka končí rovně (#8372). Styl podle druhu řádku, jinak by prázdné
                // buňky v záhlaví vypadly z jeho podbarvení.
                int prazdnych = prazdnychVpravo;
                if (prazdnych > 0)
                    plan = [.. plan.Select(r => r with
                    {
                        Cells = [.. r.Cells, .. Enumerable.Repeat(
                            new ColumnRenderer.CellSpec(mrizka.ZakladSpojeMm, "",
                                r.Kind is RowKind.Header or RowKind.GroupHeader ? tripHeaderStyle : tripDataStyle),
                            prazdnych)]
                    })];

                float konecBloku = y + plan.Sum(r => r.HeightMm);

                // Řez padl doprostřed tohohle bloku → jeho hlavu zopakovat nad pokračováním.
                // Kreslí se až po obsahu, mimo jeho ořez - viz `orezOd` výše.
                if (ctx.OpakovanaHlavickaMm > 0.01f && ctx.ClipTopMm > y && ctx.ClipTopMm < konecBloku)
                    hlavaKZopakovani = [.. plan.Take(VyskyRadkuBloku(SpojeBloku(blok).Hlavni, SpojeBloku(blok).Zrcadlene).Hlava.Count)];

                float yBloku = y;
                y = RowPlanRenderer.DrawRows(ctx, X, y, SirkaTabulkyMm(plan), plan, scale)
                    + MezeraMeziBlokyMm;
                KresliSipkuSmeru(ctx, mrizka, plan, yBloku);
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
            try { RowPlanRenderer.DrawRows(ctx, X, vrch, SirkaTabulkyMm(hlavaKZopakovani), hlavaKZopakovani, scale); }
            finally { ctx.Canvas.Restore(); }
        }
    }

    /// <summary>
    /// Nakreslí šipku směru jízdy přes řádky zastávek bloku - <b>ve svém sloupci</b>.
    /// </summary>
    /// <remarks>
    /// Kreslí se až nad hotovým blokem, protože jde přes víc řádků; buňka umí jen svůj vlastní.
    /// Rozsah se bere <b>z téhož plánu, který se kreslil</b>, ne z vlastního výpočtu výšek - jinak
    /// by se šipka rozešla s řádky, jakmile by některý z nich změnil výšku.
    /// <para>Barvu si bere z <b>textové barvy toho sloupce</b> (kaskáda stylů), takže nepřidává
    /// další nastavení do panelu; tloušťka a hrot jsou stejné jako u vývěsky.</para>
    /// </remarks>
    /// <summary>
    /// Kam ukazuje šipka v daném popisném sloupci: dolů (v pořadí zastávek), nebo nahoru.
    /// </summary>
    /// <remarks>
    /// Seznam zastávek je v pořadí směru kresleného **nalevo**, takže jeho šipka jde dolů. Sloupec
    /// šipky **za** názvy zastávek obsluhuje protijedoucí blok vpravo, a ten jede nahoru - přesně
    /// jak to vypadá na tisku ze starého modulu (vlevo „od" u první zastávky, vpravo „př").
    /// Na jednosměrné stránce protijedoucí blok není a všechny šipky míří dolů jako dosud.
    /// <para>Legacy na to má dva typy sloupců (5 „šipky za zastávkou", 8 „šipky před zastávkou");
    /// u nás o tom rozhoduje **pozice** sloupce, takže druhá šipka nepotřebuje ani nový klíč
    /// sloupce, ani migraci šablon - uživatel přidá druhý sloupec „Šipka směru" na druhou stranu.</para>
    /// </remarks>
    private static bool HrotDoluUSipky(int indexSloupce, int indexNazvu, bool maZrcadlene) =>
        !(maZrcadlene && indexNazvu >= 0 && indexSloupce > indexNazvu);

    private void KresliSipkuSmeru(RenderContext ctx, SloupcovaMrizka mrizka, List<RowSpec> plan,
        float yBlokuMm)
    {
        var popisne = mrizka.Popisne.ToList();
        var sloupceSipek = Enumerable.Range(0, popisne.Count)
            .Where(i => popisne[i].Key == ColumnKeys.Arrow).ToList();
        if (sloupceSipek.Count == 0) return;
        int indexNazvu = popisne.FindIndex(c => c.Key == ColumnKeys.StopName);

        // Svislý rozsah = od horní hrany prvního řádku zastávky po spodní hranu posledního
        // (včetně přípojných podřádků, ty k zastávce patří).
        float yZacatek = -1f, yKonec = -1f, yBezici = yBlokuMm;
        foreach (var radek in plan)
        {
            if (radek.Kind is RowKind.Data or RowKind.SubRow)
            {
                if (yZacatek < 0f) yZacatek = yBezici;
                yKonec = yBezici + radek.HeightMm;
            }
            yBezici += radek.HeightMm;
        }
        if (yZacatek < 0f) return;

        foreach (int ci in sloupceSipek)
        {
            float stred = X + mrizka.XPopisnehoMm(ci) + popisne[ci].Width * 0.5f;
            var vzhled = new DirectionArrowRenderer.Vzhled(
                popisne[ci].DataStyle.TextColor,
                SipkaTloustkaMm, SipkaHrotMm, ArrowHeadShape.Filled, LineStyle.Solid);
            // Držák se v samostatném sloupci nekreslí - proto `drzakOd == caraX`.
            DirectionArrowRenderer.Draw(ctx, vzhled, stred, stred, yZacatek, yKonec,
                HrotDoluUSipky(ci, indexNazvu, mrizka.ZrcadlenychSpoju > 0));
        }
    }

    /// <summary>Tloušťka a hrot šipky - shodné s vývěskou, ať vypadá stejně v obou sestavách.</summary>
    private const float SipkaTloustkaMm = 0.3f;
    private const float SipkaHrotMm = 1.5f;

    /// <summary>
    /// Šířka, po kterou tabulka opravdu sahá - <b>ne šířka prvku</b>.
    /// </summary>
    /// <remarks>
    /// ⚠️ Vodorovné čáry a podbarvení řádku se kreslí přes šířku, která se sem předá, kdežto buňky
    /// jen po poslední sloupec. Když se předala šířka prvku, čáry pokračovaly do volného místa
    /// vpravo za tabulkou (nález Jan 27. 8.: „pokračují do pravého okraje stránky ty vodorovné čáry,
    /// ale žádné vertikální"). Volné místo vpravo <b>záměr je</b> - blok nemusí vyjít na celou šířku
    /// a dorovnání prázdnými sloupci je samostatná volba (#8372) - ale má být opravdu volné.
    /// <para>Strop je šířka prvku: kdyby se tabulka nevešla, nesmí přetečením malovat vedle.</para>
    /// </remarks>
    private float SirkaTabulkyMm(List<RowSpec> plan) =>
        plan.Count == 0 ? 0f
            : MathF.Min(Width, plan.Max(r => r.Cells.Sum(c => c.WidthMm)));

    /// <summary>
    /// Popíše tabulku jako **plán řádků** (R1 řádkové jádro) - jen tvaruje data, nic nekreslí.
    /// Druhy řádků linkového JŘ: pás skupin dnů (sloučené sloupce) → čísla spojů → platnost →
    /// negativní značky → zastávky (s podřádky přípojů). Vodorovné čáry a pozadí zvýraznění nese
    /// <see cref="RowSpec"/>, samotné kreslení <see cref="RowPlanRenderer"/>.
    /// </summary>
    /// <summary>Sestaví plán řádků tabulky. <paramref name="sloupcovaCara"/> a
    /// <paramref name="radkovaCara"/> jsou <c>null</c>, když si uživatel ten druh čar vypnul -
    /// stejné dělení jako u seznamu zastávek a obou tabulek. Jemnější potlačení svislé čáry
    /// u konkrétního sloupce řeší <c>SuppressLeadingLine</c>, tohle je nadřazený vypínač.</summary>
    private List<RowSpec> BuildRowPlan(
        List<LineStopData> stops, List<LineTripData> trips, List<LineTripData> zrcadlene,
        List<LineTripData> vsechnySpoje,
        List<LineSkupinaData> groups,
        SloupcovaMrizka mrizka, int nameIndex,
        float headerH, float rowH, float pripojRowH,
        SKPaint? sloupcovaCara, SKPaint? radkovaCara,
        ResolvedTextStyle headerResolved, ResolvedTextStyle headerCentered,
        ResolvedTextStyle tripHeaderStyle, ResolvedTextStyle tripDataStyle,
        RenderContext ctx, SKTypeface symbolFace, float scale)
    {
        // Pás značek nad časy je vyšší a s větším písmem - viz PomerZnacek.
        float znackaRowH = VyskaRadkuZnacek(rowH);
        float znackaScale = scale * Math.Clamp(PomerZnacek, 1f, 3f);

        var plan = new List<RowSpec>();
        var fixedCols = mrizka.Popisne;

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
            // ⚠️ **Bez nadpisu se nekreslí nic** - ani podbarvení (`L9-R4`, rozhodnuto 27. 8.).
            // Starý modul nad popisnými sloupci pás vůbec nemá: skupina odjezdů je v jeho kontraktu
            // atribut *jen* sloupců spojů („pouze pro typ sloupce 7,9", `JRapiLinPrtWin.mac:246`),
            // takže z popisných sloupců není z čeho ho nakreslit. Prázdný nadpis = chování legacy,
            // vyplněný = naše featura nad rámec legacy, která se objeví, když si o ni uživatel řekne.
            var band = new List<ColumnRenderer.CellSpec>();
            // Zrcadlený blok má vlastní úseky skupin dnů - jeho spoje jsou z opačného směru, takže
            // do skupin padnou jinak než ty vpravo. Jednosměrná stránka sem nepřidá nic.
            for (int pi = 0; pi < mrizka.PrazdnychZrcadlenych; pi++)
                band.Add(new(mrizka.ZakladSpojeMm, "", headerResolved));
            PridejUsekySkupin(band, zrcadlene, groups, headerCentered,
                mrizka.SirkaUsekuZrcadlenehoMm);
            if (mrizka.MezeraZaZrcadlenymMm > 0f)
                band.Add(BunkaMezery(mrizka.MezeraZaZrcadlenymMm, headerResolved));
            band.Add(new(mrizka.PopisneSirkaMm, PopisneSloupceNadpis,
                PopisneSloupceNadpis == ""
                    ? headerResolved with { BackgroundColor = ColorHex.Transparent }
                    : headerCentered,
                SuppressLeadingLine: mrizka.MezeraZaZrcadlenymMm > 0f));
            int i = 0;
            while (i < trips.Count)
            {
                int start = i;
                int skupina = trips[i].Skupina;
                while (i < trips.Count && trips[i].Skupina == skupina) i++;
                string nadpis = groups.FirstOrDefault(g => g.Id == skupina)?.Nadpis ?? "";
                // Mezera je i v pásu, jinak by nad ní pokračovalo podbarvení záhlaví a skupiny
                // by se v pruhu slily zpátky do jednoho.
                bool mezera = mrizka.MezeraPred(start);
                if (mezera) band.Add(BunkaMezery(mrizka.MezeraMm, headerResolved));
                // Za mezerou už skupina svoji čáru vlevo nekreslí - nese ji buňka mezery, ať je
                // na hraně jedna čára a ne dvě (totéž dělá `PridejSpoje` v ostatních řádcích).
                band.Add(new(mrizka.SirkaUsekuMm(start, i), nadpis, headerCentered,
                    SuppressLeadingLine: mezera));
            }
            // ⚠️ Svislé čáry v pásu **jsou** - ale jen mezi jeho buňkami, a těch je jedna na
            // skupinu, takže padnou přesně na hranice skupin a žádný nadpis neprotnou. Do 27. 8.
            // se tu vypínaly celé „aby nerozsekaly nadpis přes několik sloupců spojů", a hranice
            // skupiny pak začínala až o řádek níž - vypadalo to jako rámeček s chybějícím kusem
            // boku (nález Jan na vzoru 940).
            plan.Add(new(RowKind.GroupHeader, headerH, band)
                { VerticalLinePaint = sloupcovaCara, LineBelowPaint = radkovaCara });
        }

        // řádek čísel spojů - popisové sloupce per-column styl, spoje z Trip sloupce
        var headerSpecs = new List<ColumnRenderer.CellSpec>();
        PridejZrcadlene(headerSpecs, mrizka, zrcadlene.Count, headerResolved,
            ti => (zrcadlene[ti].CisloSpoje, tripHeaderStyle), tripHeaderStyle);
        foreach (var (index, sirka, text, styl) in NadpisyPopisnychSloupcu(fixedCols))
            headerSpecs.Add(new(sirka, text, styl, SuppressLeadingLine: BezCaryVlevo(index, mrizka)));
        PridejSpoje(headerSpecs, mrizka, trips.Count, headerResolved,
            ti => (trips[ti].CisloSpoje, tripHeaderStyle));
        plan.Add(new(RowKind.Header, headerH, headerSpecs)
            { VerticalLinePaint = sloupcovaCara, LineBelowPaint = radkovaCara });

        // řádky turnusů - jeden na turnus, hned pod čísly spojů a **nad** řádkem platnosti.
        // Tak je řadí i starý modul: jeho indexy řádků jdou číslo spoje (1) → turnusy (1.001xx)
        // → platnost (1.1, 1.2…) → negativní značky (1.9), `JRapiLinPrtWin.mac:2948` a `:3795`.
        // Spoj s méně turnusy má zbylé řádky prázdné - řádků je tolik, kolik má nejbohatší spoj.
        int turnusovychRadku = PocetTurnusovychRadku(vsechnySpoje);
        for (int tr = 0; tr < turnusovychRadku; tr++)
        {
            plan.Add(new(RowKind.HeaderExtra, rowH,
                TripTextRow(mrizka, trips, zrcadlene, tripDataStyle,
                    t => tr < t.Turnusy.Count ? t.Turnusy[tr] : ""))
            { VerticalLinePaint = sloupcovaCara });
        }

        // řádky platnosti (znaky X/6/7/+ nad spojem) - nejvýš dvě značky na řádek, zbytek pod něj
        for (int pr = 0; pr < PocetRadkuPlatnosti(vsechnySpoje); pr++)
        {
            // Značky se kreslí ručně nad hotovým řádkem, aby šel v jedné buňce střídat symbolový
            // a běžný font („⚒ BEV"). Buňky proto zůstávají prázdné - jinak by se text zdvojil.
            var platnostRuns = trips
                .Select(t => SymbolRunRenderer.BuildRuns(ZnackyRadku(t, pr), SymbolMap.ResolveLinkovy))
                .ToList();
            // Zrcadlený blok má svoje značky platnosti - bez nich by jeho sloupce neřekly, ve které
            // dny spoj jede, a to je informace, ne ozdoba.
            var platnostRunsZrc = zrcadlene
                .Select(t => SymbolRunRenderer.BuildRuns(ZnackyRadku(t, pr), SymbolMap.ResolveLinkovy))
                .ToList();
            var prazdne = TripTextRow(mrizka, trips, zrcadlene, tripDataStyle, _ => "");
            plan.Add(new(RowKind.HeaderExtra, znackaRowH, prazdne)
            {
                VerticalLinePaint = sloupcovaCara,
                // `DrawRows` kreslí buňky od X prvku, takže i ruční kreslení musí začít tam.
                Decorate = rowY =>
                {
                    DrawPlatnostRuns(ctx, rowY, znackaRowH, ti => X + mrizka.XSpojeMm(ti),
                        mrizka.SirkaSpojeMm, platnostRuns, tripDataStyle, symbolFace, znackaScale);
                    DrawPlatnostRuns(ctx, rowY, znackaRowH, ti => X + mrizka.XZrcadlenehoMm(ti),
                        mrizka.SirkaZrcadlenehoMm, platnostRunsZrc, tripDataStyle, symbolFace, znackaScale);
                },
            });
        }

        // řádek NZ (negativní značky) - kreslí se ručně, protože jde o inverzní box, ne o text
        if (HasNzRow(vsechnySpoje))
        {
            var prazdneNz = TripTextRow(mrizka, trips, zrcadlene, tripDataStyle, _ => "");
            plan.Add(new(RowKind.HeaderExtra, znackaRowH, prazdneNz)
            {
                VerticalLinePaint = sloupcovaCara,
                LineBelowPaint = radkovaCara,
                Decorate = rowY =>
                {
                    DrawNegativniZnacky(ctx, rowY, znackaRowH, ti => X + mrizka.XSpojeMm(ti),
                        mrizka.SirkaSpojeMm, trips, tripDataStyle, znackaScale);
                    DrawNegativniZnacky(ctx, rowY, znackaRowH, ti => X + mrizka.XZrcadlenehoMm(ti),
                        mrizka.SirkaZrcadlenehoMm, zrcadlene, tripDataStyle, znackaScale);
                },
            });
        }

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
            var tripCellStyle = OddTc(tripDataStyle, oddTc);
            // Značka návaznosti se tiskne **vedle času** ve stejné buňce (doloženo tiskem staré
            // linky 330511). Buňka umí jen jeden font, takže se taková buňka nechá prázdná
            // a nakreslí se ručně - stejně jako řádek platnosti. Musí se to vědět **před** sázením
            // buněk, aby se text nezdvojil.
            var znackyVBunce = new Dictionary<int, List<SymbolRunRenderer.Run>>();
            ZnackyVBunce(trips, stop, si, znackyVBunce);
            var znackyVBunceZrc = new Dictionary<int, List<SymbolRunRenderer.Run>>();
            ZnackyVBunce(zrcadlene, stop, si, znackyVBunceZrc);
            int siRadku = si;
            PridejZrcadlene(specs, mrizka, zrcadlene.Count, tripCellStyle, ti =>
            {
                var tz = zrcadlene[ti];
                var st = tz.ForeColor is null ? tripCellStyle : tripCellStyle with { TextColor = tz.ForeColor };
                string cas = znackyVBunceZrc.ContainsKey(ti) ? ""
                    : (siRadku < tz.Casy.Count ? tz.Casy[siRadku] : "");
                if (cas == "" && !znackyVBunceZrc.ContainsKey(ti))
                    cas = VyplnBunky(mrizka.SirkaZrcadlenehoMm(ti), st, ctx);
                return (cas, st);
            });
            var znakZastavkyPro = SymbolRunRenderer.BuildRuns(
                stop.JePrijezdovy ? [] : stop.Atributy.Where(JePiktogramZastavky), SymbolMap.ResolveLinkovy);
            for (int ci = 0; ci < fixedCols.Count; ci++)
            {
                var bunkaStyl = OddTc(fixedCols[ci].DataStyle, oddTc);
                if (ci == nameIndex && TucneZastavkyDleTrasy && MaTucneZTrasy(stop))
                    bunkaStyl = bunkaStyl with { Bold = true };
                string hodnota = fixedCols[ci].Value(stop);
                if (ci == nameIndex)
                    hodnota = SNazvemATeckami(hodnota, fixedCols[ci].Width, bunkaStyl,
                        znakZastavkyPro, symbolFace, ctx);
                specs.Add(new(fixedCols[ci].Width, hodnota, bunkaStyl,
                    SuppressLeadingLine: BezCaryVlevo(ci, mrizka)));
            }
            PridejSpoje(specs, mrizka, trips.Count, tripCellStyle, ti =>
            {
                var t = trips[ti];
                // Barva spoje z dat přebíjí barvu textu z kaskády - je to prezentační nápověda,
                // kterou prvek smí respektovat (R3). Spoj bez barvy nechá kaskádu být.
                var cellStyle = t.ForeColor is null ? tripCellStyle : tripCellStyle with { TextColor = t.ForeColor };
                string text = znackyVBunce.ContainsKey(ti) ? "" : (siRadku < t.Casy.Count ? t.Casy[siRadku] : "");
                if (text == "" && !znackyVBunce.ContainsKey(ti))
                    text = VyplnBunky(mrizka.SirkaSpojeMm(ti), cellStyle, ctx);
                return (text, cellStyle);
            });

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
                VerticalLinePaint = sloupcovaCara,
                Decorate = (znackyRadku.Count == 0) && (znackyVBunceZrc.Count == 0)
                        && (znakZastavky.Count == 0) ? null : rowY =>
                {
                    if (znackyRadku.Count > 0)
                        DrawBunkySeZnackou(ctx, rowY, rowH, ti => X + mrizka.XSpojeMm(ti),
                            mrizka.SirkaSpojeMm, znackyRadku, tripCellStyle, symbolFace, scale);
                    if (znackyVBunceZrc.Count > 0)
                        DrawBunkySeZnackou(ctx, rowY, rowH, ti => X + mrizka.XZrcadlenehoMm(ti),
                            mrizka.SirkaZrcadlenehoMm, znackyVBunceZrc, tripCellStyle, symbolFace, scale);
                    if (znakZastavky.Count > 0 && nameIndex >= 0)
                        DrawZnakZastavky(ctx, rowY, rowH, X, mrizka, nameIndex, znakZastavky,
                            OddTc(fixedCols[nameIndex].DataStyle, oddTc), symbolFace, scale);
                },
            });

            if (!ShowPripojeRows) continue;
            if (!NesePripoje(stop, poradiSOdjezdem)) continue;

            // přípojné řádky (vyčká/navazuje) za touto zastávkou - seskupení drží `PripojneRadky`,
            // aby vyšlo shodně s výpočtem výšky
            foreach (var radek in PripojneRadky(stop, trips, zrcadlene))
            {
                var prSpecs = new List<ColumnRenderer.CellSpec>();
                var casyZrc = radek.CasPerZrcadleny;
                PridejZrcadlene(prSpecs, mrizka, zrcadlene.Count, pripojStyle,
                    ti => (casyZrc.TryGetValue(ti, out var cas) ? cas : "", pripojStyle));
                for (int ci = 0; ci < fixedCols.Count; ci++)
                    prSpecs.Add(new(fixedCols[ci].Width,
                        ci == nameIndex ? radek.Popisek : "", pripojStyle,
                        SuppressLeadingLine: BezCaryVlevo(ci, mrizka)));
                var casy = radek.CasPerSpoj;
                PridejSpoje(prSpecs, mrizka, trips.Count, pripojStyle,
                    ti => (casy.TryGetValue(ti, out var cas) ? cas : "", pripojStyle));

                plan.Add(new(RowKind.SubRow, pripojRowH, prSpecs)
                { VerticalLinePaint = sloupcovaCara, LineAbovePaint = radkovaCara, LineBelowPaint = radkovaCara });
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
        SloupcovaMrizka mrizka, int nameIndex, List<SymbolRunRenderer.Run> runs,
        ResolvedTextStyle style, SKTypeface symbolFace, float scale)
    {
        float leftMm = xMm + mrizka.XPopisnehoMm(nameIndex);
        var cellMm = new SKRect(leftMm, rowTopMm, leftMm + mrizka.Popisne[nameIndex].Width, rowTopMm + rowH);
        SymbolRunRenderer.Draw(ctx, CellLayout.ApplyPadding(cellMm, style),
            runs, style with { HAlign = TextHorizontalAlignment.Right }, symbolFace, scale);
    }

    /// <summary>
    /// Naplní mapu „index spoje → úseky ke kreslení" pro buňky, kde je vedle času značka návaznosti.
    /// </summary>
    /// <remarks>
    /// Buňka umí jen jeden font, takže se taková buňka nechá prázdná a nakreslí se ručně. Sdílí to
    /// hlavní i zrcadlený blok - značka patří ke spoji, ne ke straně tabulky.
    /// </remarks>
    private void ZnackyVBunce(List<LineTripData> spoje, LineStopData stop, int si,
        Dictionary<int, List<SymbolRunRenderer.Run>> cil)
    {
        for (int ti = 0; ti < spoje.Count; ti++)
        {
            var t = spoje[ti];
            string cas = si < t.Casy.Count ? t.Casy[si] : "";
            if (cas == "") continue;

            string znak = ZnakNavaznosti(t, stop.Poradi);
            var runs = znak == "" ? [] : SymbolRunRenderer.BuildRuns([znak], SymbolMap.ResolveLinkovy);
            bool maZnacku = SymbolRunRenderer.MaPiktogram(runs);
            if (!maZnacku) runs.Clear();   // neověřená značka → jako by žádná nebyla

            var casRuns = CasRuns(cas);
            // Buňka jde ručně jen tehdy, když v ní je aspoň jedna věc, kterou obyčejná buňka
            // neumí: piktogram, nebo hodina a minuta různě velké. Jinak ať ji vysází CellRenderer.
            if (!maZnacku && casRuns is null) continue;

            if (maZnacku) runs.Add(new(" ", false));
            if (casRuns is null) runs.Add(new(cas, false));
            else runs.AddRange(casRuns);
            cil[ti] = runs;
        }
    }

    /// <summary>
    /// Rozpad času na hodinu a menší minutu, nebo null, když se má vysázet jako obyčejný text.
    /// </summary>
    /// <remarks>
    /// Dělí se jen tvar <c>HH:MM</c>. Buňka může nést i značku („&lt;" jede po jiné trase,
    /// „|" projíždí) nebo prázdno - to se dělit nesmí, jinak by se z „&lt;" stal nesmysl.
    /// <para>Mezera místo dvojtečky je záměr, ne úspora: starý tisk sází „16 15" a dvojtečka
    /// mezi různě velkými čísly působí jako překlep.</para>
    /// </remarks>
    /// <summary>
    /// Řetězec teček proložených mezerami, který se vejde do <paramref name="volnoMm"/>.
    /// Prázdno, když se nevejde ani jedna.
    /// </summary>
    /// <remarks>
    /// ⚠️ **Tečky jsou proložené mezerou a je jich nejvýš <paramref name="nejvic"/>.** Souvislá
    /// řada teček přes celou šířku vypadá rozdrobeně a hůř se čte než pár výrazných - starý tisk
    /// jich má ve sloupci spoje jen pár u středu (nález Jan 7. 9.).
    /// <para>Ubírá se <see cref="CellRenderer.DefaultShrinkReserveMm"/>, jinak by přesně vyplněná
    /// buňka spustila zmenšování textu a tečky by si samy zmenšily písmo celé buňky.</para>
    /// </remarks>
    private static string Tecky(float volnoMm, SKFont font, float mmToPx, int nejvic = int.MaxValue)
    {
        // Jedna „dlaždice" je tečka a mezera za ní; poslední mezera se na konci ořízne.
        float dlazdiceMm = font.MeasureText(". ") / mmToPx;
        if (dlazdiceMm <= 0f) return "";
        float misto = volnoMm - CellRenderer.DefaultShrinkReserveMm;
        int kolik = Math.Min(nejvic, (int)MathF.Floor(misto / dlazdiceMm));
        return kolik <= 0 ? "" : string.Join(" ", Enumerable.Repeat(".", kolik));
    }

    /// <summary>Výplň prázdné buňky spoje, nebo prázdno když se výplň nepoužívá.</summary>
    private string VyplnBunky(float sirkaSloupceMm, ResolvedTextStyle styl, RenderContext ctx)
    {
        if (!VyplnitPrazdneBunkySpoju) return "";
        using var font = styl.CreateFont(1f, ctx.MmToPx);
        // Nejvýš tři - starý tisk má v prázdné buňce spoje jen krátkou značku u středu, ne výplň
        // celé šířky. Zarovnání si řídí sloupec sám (u spojů je na střed).
        return Tecky(sirkaSloupceMm - styl.PaddingLeft - styl.PaddingRight, font, ctx.MmToPx, nejvic: 3);
    }

    /// <summary>
    /// Název zastávky doplněný tečkami k pravému okraji sloupce, nebo beze změny.
    /// </summary>
    /// <remarks>
    /// ⚠️ Piktogram přestupu se kreslí <b>zarovnaný doprava</b> v téže buňce
    /// (<see cref="DrawZnakZastavky"/>), takže se jeho šířka musí od volného místa odečíst -
    /// jinak by tečky vedly pod ním.
    /// </remarks>
    private string SNazvemATeckami(string nazev, float sirkaSloupceMm, ResolvedTextStyle styl,
        List<SymbolRunRenderer.Run> znakZastavky, SKTypeface symbolFace, RenderContext ctx)
    {
        if (!TeckyZaNazvemZastavky || nazev == "") return nazev;

        using var font = styl.CreateFont(1f, ctx.MmToPx);
        using var symbol = new SKFont(symbolFace, font.Size);
        float obsazenoMm = font.MeasureText(nazev + " ") / ctx.MmToPx;
        foreach (var r in znakZastavky)
            obsazenoMm += (r.Symbol ? symbol : font).MeasureText(r.Text) / ctx.MmToPx;

        float volno = sirkaSloupceMm - styl.PaddingLeft - styl.PaddingRight - obsazenoMm;
        string tecky = Tecky(volno, font, ctx.MmToPx);
        return tecky == "" ? nazev : nazev + " " + tecky;
    }

    internal List<SymbolRunRenderer.Run>? CasRuns(string cas)
    {
        if (FormatCasu != FormatCasuSpoje.HodinaVetsi) return null;
        int dvojtecka = cas.IndexOf(':');
        if (dvojtecka <= 0 || dvojtecka == cas.Length - 1) return null;
        string hodina = cas[..dvojtecka];
        string minuta = cas[(dvojtecka + 1)..];
        if (!hodina.All(char.IsDigit) || !minuta.All(char.IsDigit)) return null;
        // ⚠️ Hodina **bez vedoucí nuly** - legacy ji utne číselným přetypováním
        // (`+$p(cas,":",1)`, `JRapiFce.mac:138`), takže tiskne „7 10", ne „07 10". Není to jen
        // kosmetika: sloupec spoje je o celou číslici užší a vejde se jich do řádku víc.
        hodina = hodina.TrimStart('0');
        if (hodina == "") hodina = "0";
        float pomer = Math.Clamp(PomerMinuty, 0.3f, 1f);
        return [new(hodina, false), new(" ", false, pomer), new(minuta, false, pomer)];
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
        Func<int, float> xSpojeMm, Func<int, float> sirkaSpojeMm,
        Dictionary<int, List<SymbolRunRenderer.Run>> znacky,
        ResolvedTextStyle style, SKTypeface symbolFace, float scale)
    {
        foreach (var (ti, runs) in znacky)
        {
            float leftMm = xSpojeMm(ti);
            var cellMm = new SKRect(leftMm, rowTopMm, leftMm + sirkaSpojeMm(ti), rowTopMm + rowH);
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
        Func<int, float> xSpojeMm, Func<int, float> sirkaSpojeMm,
        List<List<SymbolRunRenderer.Run>> runsPerTrip,
        ResolvedTextStyle style, SKTypeface symbolFace, float scale)
    {
        for (int i = 0; i < runsPerTrip.Count; i++)
        {
            var runs = runsPerTrip[i];
            if (runs.Count == 0) continue;
            float leftMm = xSpojeMm(i);
            var cellMm = new SKRect(leftMm, rowTopMm, leftMm + sirkaSpojeMm(i), rowTopMm + rowH);
            SymbolRunRenderer.Draw(ctx, CellLayout.ApplyPadding(cellMm, style), runs, style, symbolFace, scale);
        }
    }

    /// <summary>Nakreslí negativní značky do sloupců spojů - inverzně, viz
    /// <see cref="NegativniZnackaRenderer"/>.</summary>
    private static void DrawNegativniZnacky(RenderContext ctx, float rowTopMm, float rowH,
        Func<int, float> xSpojeMm, Func<int, float> sirkaSpojeMm,
        List<LineTripData> trips, ResolvedTextStyle style, float scale)
    {
        for (int i = 0; i < trips.Count; i++)
        {
            if (trips[i].Nz.Length == 0) continue;
            float leftMm = xSpojeMm(i);
            var cellMm = new SKRect(leftMm, rowTopMm, leftMm + sirkaSpojeMm(i), rowTopMm + rowH);
            NegativniZnackaRenderer.DrawCell(
                ctx, CellLayout.ApplyPadding(cellMm, style), trips[i].Nz, style, scale);
        }
    }

    private List<ColumnRenderer.CellSpec> TripTextRow(
        SloupcovaMrizka mrizka, List<LineTripData> trips, List<LineTripData> zrcadlene,
        ResolvedTextStyle tripDataStyle, Func<LineTripData, string> text)
    {
        var specs = new List<ColumnRenderer.CellSpec>();
        PridejZrcadlene(specs, mrizka, zrcadlene.Count, tripDataStyle,
            ti => (text(zrcadlene[ti]), tripDataStyle));
        for (int ci = 0; ci < mrizka.Popisne.Count; ci++)
            specs.Add(new(mrizka.Popisne[ci].Width, "", mrizka.Popisne[ci].DataStyle,
                SuppressLeadingLine: BezCaryVlevo(ci, mrizka)));
        PridejSpoje(specs, mrizka, trips.Count, tripDataStyle, ti => (text(trips[ti]), tripDataStyle));
        return specs;
    }

    /// <summary>
    /// Vysází buňky sloupců spojů i s mezerami mezi skupinami sloupců.
    /// </summary>
    /// <remarks>
    /// Existuje proto, že sloupce spojů se skládají na <b>čtyřech</b> místech (čísla spojů, časy
    /// u zastávek, přípojné řádky, řádky platnosti a negativních značek) a mezera musí na všech
    /// padnout na tentýž pixel - jinak se svislé čáry rozejdou o šířku mezery a tabulka vypadá
    /// rozsypaně. Šířky sloupců i mezer proto nikdo nesčítá sám, drží je
    /// <see cref="SloupcovaMrizka"/>.
    /// <para>Čára na hraně skupiny: nese ji <b>buňka mezery</b> (její levá hrana = pravý okraj
    /// předchozí skupiny) a sloupec za mezerou už svoji vlevo nekreslí. Vzor 940 má na okraji
    /// popisného bloku jednu čáru, ne dvě kolem mezery.</para>
    /// </remarks>
    private static void PridejSpoje(List<ColumnRenderer.CellSpec> specs, SloupcovaMrizka mrizka,
        int pocetSpoju, ResolvedTextStyle stylMezery,
        Func<int, (string Text, ResolvedTextStyle Style)> bunka)
    {
        for (int ti = 0; ti < pocetSpoju; ti++)
        {
            bool mezera = mrizka.MezeraPred(ti);
            if (mezera) specs.Add(BunkaMezery(mrizka.MezeraMm, stylMezery));
            var (text, style) = bunka(ti);
            specs.Add(new(mrizka.SirkaSpojeMm(ti), text, style, SuppressLeadingLine: mezera));
        }
    }

    /// <summary>Prázdná buňka mezery mezi skupinami sloupců - bez pozadí, ať mezerou prosvítá
    /// stránka a pruh záhlaví se v ní přeruší.</summary>
    /// <summary>
    /// Vysází do pásu sloučené buňky <b>úseků skupin dnů</b> - jednu na každý souvislý úsek spojů
    /// téže skupiny.
    /// </summary>
    /// <remarks>
    /// Používá to zrcadlený blok; hlavní strana má týž postup u sebe, protože k němu navíc přidává
    /// mezery mezi skupinami sloupců.
    /// </remarks>
    private static void PridejUsekySkupin(List<ColumnRenderer.CellSpec> band,
        List<LineTripData> spoje, List<LineSkupinaData> groups, ResolvedTextStyle styl,
        Func<int, int, float> sirkaUseku)
    {
        int i = 0;
        while (i < spoje.Count)
        {
            int start = i;
            int skupina = spoje[i].Skupina;
            while (i < spoje.Count && spoje[i].Skupina == skupina) i++;
            string nadpis = groups.FirstOrDefault(g => g.Id == skupina)?.Nadpis ?? "";
            band.Add(new(sirkaUseku(start, i), nadpis, styl));
        }
    }

    /// <summary>
    /// Vysází <b>zrcadlený</b> blok (spoje opačného směru) a mezeru za ním - patří na začátek řádku.
    /// </summary>
    /// <remarks>
    /// Jednosměrná stránka sem pošle nulový počet, takže se nic nepřidá a řádek vyjde jako dosud.
    /// </remarks>
    private static void PridejZrcadlene(List<ColumnRenderer.CellSpec> specs, SloupcovaMrizka mrizka,
        int pocet, ResolvedTextStyle stylMezery,
        Func<int, (string Text, ResolvedTextStyle Style)> bunka,
        ResolvedTextStyle? stylPrazdne = null)
    {
        // Dorovnání stojí před spoji - na vnější hraně tabulky (viz PrazdnychZrcadlenych).
        for (int i = 0; i < mrizka.PrazdnychZrcadlenych; i++)
            specs.Add(new(mrizka.ZakladSpojeMm, "", stylPrazdne ?? stylMezery));
        for (int ti = 0; ti < pocet; ti++)
        {
            var (text, style) = bunka(ti);
            specs.Add(new(mrizka.SirkaZrcadlenehoMm(ti), text, style));
        }
        if (mrizka.MezeraZaZrcadlenymMm > 0f)
            specs.Add(BunkaMezery(mrizka.MezeraZaZrcadlenymMm, stylMezery));
    }

    /// <summary>Potlačit svislou čáru na levé hraně popisného sloupce? Kromě slitých popisných
    /// sloupců i tehdy, když je před nimi mezera za zrcadleným blokem - na hraně skupiny má být
    /// jedna čára, ne dvě kolem mezery.</summary>
    private bool BezCaryVlevo(int indexPopisnehoSloupce, SloupcovaMrizka mrizka) =>
        BezCaryVlevo(indexPopisnehoSloupce)
        || (indexPopisnehoSloupce == 0 && mrizka.MezeraZaZrcadlenymMm > 0f);

    private static ColumnRenderer.CellSpec BunkaMezery(float sirkaMm, ResolvedTextStyle vzor) =>
        new(sirkaMm, "", vzor with { BackgroundColor = ColorHex.Transparent }, Mezera: true);

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

    /// <summary>
    /// Popisek <b>sloučeného</b> přípojného řádku - souhrn podle směru, jako ve starém tisku
    /// („ze směru Trhové Sviny" u vyčkání, „směr České Budějovice" u navázání).
    /// </summary>
    /// <remarks>
    /// Sloučený řádek nese časy víc spojů, takže v popisku nemůže být číslo jednoho z nich.
    /// Rozhodnutí Jan 4. 9.: šetření místa váží víc než to, aby cestující věděl, kterou linkou
    /// přípoj přijede - potřebuje čas.
    /// </remarks>
    private static string PripojTextSouhrnny(LinePripojData pr) =>
        pr.Typ == "vycka" ? $"ze směru {pr.Text}" : $"směr {pr.Text}";

    /// <summary>Jeden přípojný řádek: popisek a časy per spoj (index do sady spojů bloku).</summary>
    /// <remarks>
    /// Časy jsou ve <b>dvou</b> mapách, protože obousměrná tabulka má na každé straně zastávek
    /// vlastní sadu spojů a indexy by si v jedné mapě přebily. Řádek tak může nést přípoj obou
    /// směrů naráz - „ze směru Brniště" vyjde jednou, s časy vlevo i vpravo.
    /// </remarks>
    private sealed class PripojRadek
    {
        public string Popisek = "";
        public readonly Dictionary<int, string> CasPerSpoj = [];
        public readonly Dictionary<int, string> CasPerZrcadleny = [];
    }

    /// <summary>
    /// Přípojné řádky jedné zastávky pro danou sadu spojů - <b>jediné místo</b>, kde se rozhoduje,
    /// kolik jich bude.
    /// </summary>
    /// <remarks>
    /// ⚠️ Používá to <b>render i výpočet výšky</b>. Kdyby každý počítal po svém, řez stránky by
    /// padl jinam, než se kreslí - přesně to se stalo, když se počet přípojů bral ze všech spojů
    /// linky místo ze spojů bloku.
    ///
    /// <para><b>Slučování</b> (rozhodnutí Jan 4. 9.) kopíruje klíč starého modulu
    /// (`JRapiLinPrtWin01.mac:89`): tarifní číslo + směr + druh dopravy, volitelně i linka.
    /// Když už ve řádku ten spoj čas má, zakládá se řádek další - jinak by se dva přípoje téhož
    /// spoje přebily. Bez slučování zůstává řádek per (spoj × přípoj), tedy dnešní chování.</para>
    /// </remarks>
    private List<PripojRadek> PripojneRadky(LineStopData stop, IReadOnlyList<LineTripData> trips,
        IReadOnlyList<LineTripData>? zrcadlene = null)
    {
        var radky = new List<PripojRadek>();
        var podleKlice = new Dictionary<string, List<PripojRadek>>();

        // Nejdřív hlavní strana, pak zrcadlená - do TÉŽE mapy klíčů, takže přípoj, který mají oba
        // směry, sdílí jeden řádek a jen si do něj zapíše časy na svou stranu.
        Pridej(trips, zrcadleny: false);
        if (zrcadlene is { Count: > 0 }) Pridej(zrcadlene, zrcadleny: true);
        return radky;

        void Pridej(IReadOnlyList<LineTripData> sada, bool zrcadleny)
        {
            for (int ti = 0; ti < sada.Count; ti++)
            {
                foreach (var pr in sada[ti].Pripoje.Where(p => p.Radek && p.Poradi == stop.Poradi))
                {
                    if (PripojeSlucovani == PripojSlucovani.Ne)
                    {
                        var samostatny = new PripojRadek { Popisek = PripojText(pr) };
                        Casy(samostatny, zrcadleny)[ti] = pr.Cas;
                        radky.Add(samostatny);
                        continue;
                    }

                    string klic = PripojeSlucovani == PripojSlucovani.PodleSmeruALinky
                        ? $"{pr.Typ}|{pr.Text}|{pr.Avl}|{pr.Linka}"
                        : $"{pr.Typ}|{pr.Text}|{pr.Avl}";

                    if (!podleKlice.TryGetValue(klic, out var skupina))
                    {
                        skupina = [];
                        podleKlice[klic] = skupina;
                    }

                    var cil = skupina.FirstOrDefault(r => !Casy(r, zrcadleny).ContainsKey(ti));
                    if (cil is null)
                    {
                        cil = new PripojRadek { Popisek = PripojTextSouhrnny(pr) };
                        skupina.Add(cil);
                        radky.Add(cil);
                    }
                    Casy(cil, zrcadleny)[ti] = pr.Cas;
                }
            }
        }

        static Dictionary<int, string> Casy(PripojRadek radek, bool zrcadleny) =>
            zrcadleny ? radek.CasPerZrcadleny : radek.CasPerSpoj;
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
            TurnusyKeSpojum = TurnusyKeSpojum,
            ShowNzRow = ShowNzRow,
            ShowPripojeRows = ShowPripojeRows,
            PripojeSlucovani = PripojeSlucovani,
            PadEmptyColumns = PadEmptyColumns,
            JednotnaSirkaSpoju = JednotnaSirkaSpoju,
            NejmensiSirkaSpojeMm = NejmensiSirkaSpojeMm,
            NejvetsiSirkaSpojeMm = NejvetsiSirkaSpojeMm,
            FormatCasu = FormatCasu,
            TucneZastavkyDleTrasy = TucneZastavkyDleTrasy,
            PomerZnacek = PomerZnacek,
            PomerMinuty = PomerMinuty,
            VyplnitPrazdneBunkySpoju = VyplnitPrazdneBunkySpoju,
            TeckyZaNazvemZastavky = TeckyZaNazvemZastavky,
            RozdelitZonyDoSloupcu = RozdelitZonyDoSloupcu,
            ShowGroupHeaders = ShowGroupHeaders,
            PopisneSloupceNadpis = PopisneSloupceNadpis,
            PopisneSloupceBezVnitrnichCar = PopisneSloupceBezVnitrnichCar,
            MezeraMeziSkupinamiMm = MezeraMeziSkupinamiMm,
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
        new BoolPropertyDescriptor { Label = "Turnusy ke spojům", Group = "Řádky", Get = () => TurnusyKeSpojum, Set = v => TurnusyKeSpojum = v },
        new BoolPropertyDescriptor { Label = "Řádek negativních značek", Group = "Řádky", Get = () => ShowNzRow, Set = v => ShowNzRow = v },
        new BoolPropertyDescriptor { Label = "Přípojné řádky", Group = "Řádky", Get = () => ShowPripojeRows, Set = v => ShowPripojeRows = v },
        new EnumPropertyDescriptor { Label = "Slučovat přípoje", Group = "Přípojné řádky",
            Get = () => PripojeSlucovani, Set = v => PripojeSlucovani = (PripojSlucovani)v,
            Options =
            [
                ("Ne - řádek pro každý spoj", (object)PripojSlucovani.Ne),
                ("Podle směru",               (object)PripojSlucovani.PodleSmeru),
                ("Podle směru a linky",       (object)PripojSlucovani.PodleSmeruALinky),
            ] },
        new FloatPropertyDescriptor { Label = "Nejmenší šířka sloupce spoje", Group = SirkaGroup,
            Unit = "mm", Min = 0, Max = 50, Format = "0.##",
            Get = () => NejmensiSirkaSpojeMm ?? 0f,
            Set = v => NejmensiSirkaSpojeMm = v <= 0f ? null : v },
        new FloatPropertyDescriptor { Label = "Největší šířka sloupce spoje", Group = SirkaGroup,
            Unit = "mm", Min = 0, Max = 200, Format = "0.##",
            Get = () => NejvetsiSirkaSpojeMm ?? 0f,
            Set = v => NejvetsiSirkaSpojeMm = v <= 0f ? null : v },
        new BoolPropertyDescriptor { Label = "Dorovnat prázdnými sloupci", Group = SirkaGroup, Get = () => PadEmptyColumns, Set = v => PadEmptyColumns = v },
        new BoolPropertyDescriptor { Label = "Stejně široké sloupce spojů", Group = SirkaGroup, Get = () => JednotnaSirkaSpoju, Set = v => JednotnaSirkaSpoju = v },
        new EnumPropertyDescriptor
        {
            Label = "Formát času", Group = "Datové buňky",
            Get = () => FormatCasu, Set = v => FormatCasu = (FormatCasuSpoje)v,
            Options =
            [
                ("Minuty menším písmem (hh mm)", (object)FormatCasuSpoje.HodinaVetsi),
                ("Celý čas stejně (hh:mm)",      (object)FormatCasuSpoje.SDvojteckou),
            ],
        },
        new FloatPropertyDescriptor
        {
            Label = "Velikost minut", Group = "Datové buňky",
            Get = () => PomerMinuty, Set = v => PomerMinuty = v,
            Min = 0.3f, Max = 1f, Format = "0.##",
        },
        new BoolPropertyDescriptor { Label = "Tečky v prázdných buňkách", Group = "Datové buňky", Get = () => VyplnitPrazdneBunkySpoju, Set = v => VyplnitPrazdneBunkySpoju = v },
        new BoolPropertyDescriptor { Label = "Tečky za názvem zastávky", Group = "Datové buňky", Get = () => TeckyZaNazvemZastavky, Set = v => TeckyZaNazvemZastavky = v },
        new BoolPropertyDescriptor { Label = "Tučné zastávky dle trasy", Group = "Datové buňky", Get = () => TucneZastavkyDleTrasy, Set = v => TucneZastavkyDleTrasy = v },
        new FloatPropertyDescriptor
        {
            Label = "Velikost značek", Group = "Řádky",
            Get = () => PomerZnacek, Set = v => PomerZnacek = v,
            Min = 1f, Max = 3f, Format = "0.##",
        },
        new BoolPropertyDescriptor { Label = "Každá zóna ve vlastním sloupci", Group = PopisneGroup,
            Get = () => RozdelitZonyDoSloupcu, Set = v => RozdelitZonyDoSloupcu = v },
        new BoolPropertyDescriptor { Label = "Nadpisy skupin dnů", Group = "Řádky", Get = () => ShowGroupHeaders, Set = v => ShowGroupHeaders = v },
        new StringPropertyDescriptor { Label = "Nadpis popisných sloupců", Group = PopisneGroup,
            Get = () => PopisneSloupceNadpis, Set = v => PopisneSloupceNadpis = v },
        new BoolPropertyDescriptor { Label = "Popisné sloupce bez vnitřních čar", Group = PopisneGroup,
            Get = () => PopisneSloupceBezVnitrnichCar, Set = v => PopisneSloupceBezVnitrnichCar = v },
        new FloatPropertyDescriptor { Label = "Mezera mezi skupinami sloupců", Group = SirkaGroup,
            Unit = "mm", Min = 0, Max = 20, Format = "0.#",
            Get = () => MezeraMeziSkupinamiMm, Set = v => MezeraMeziSkupinamiMm = v },
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
        .. GetCellZoneFontSizeDescriptors(),
        .. GetCellZoneBaseColorDescriptors(),
        // Vnitřní okraj drží dolní práh sloupce spoje (šířka času + okraje, nejméně 3 mm), takže
        // rozhoduje o počtu spojů na blok a o počtu řezů na šířku. Jen vodorovný - svislý by si
        // šel do cesty s vlastním polem „Výška řádku".
        new FloatPropertyDescriptor { Label = "Vnitřní okraj vlevo", Group = "Datové buňky",
            Unit = "mm", Min = 0, Max = 20, Format = "0.##",
            Get = () => DataStyle.PaddingLeft ?? ResolveColumnDataParent().PaddingLeft,
            Set = v => DataStyle.PaddingLeft = v },
        new FloatPropertyDescriptor { Label = "Vnitřní okraj vpravo", Group = "Datové buňky",
            Unit = "mm", Min = 0, Max = 20, Format = "0.##",
            Get = () => DataStyle.PaddingRight ?? ResolveColumnDataParent().PaddingRight,
            Set = v => DataStyle.PaddingRight = v },
        GetDataRowHeightDescriptor(),
        .. GetPositionDescriptors(),
    ];
}
