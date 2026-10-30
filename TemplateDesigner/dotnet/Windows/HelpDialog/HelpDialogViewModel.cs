using EDControls;
using EDControls.EDViewModel;
using TemplateDesigner.Models;
using static TemplateDesigner.Designer.KeyboardShortcuts;

namespace TemplateDesigner;

/// <summary>Jeden řádek nápovědy: akce + zkratka/umístění.</summary>
public record HelpEntry(string Action, string Shortcut);

/// <summary>Kategorie nápovědy - nadpis, řádky a volitelný tip pod tabulkou.</summary>
public class HelpSection
{
    public required string Title { get; init; }
    public required List<HelpEntry> Entries { get; init; }
    public string? Tip { get; init; }
    public bool HasTip => !string.IsNullOrEmpty(Tip);
}

/// <summary>
/// Nápověda F1 - datově řízený obsah (zkratky, workflow, tipy) s fulltext filtrem.
/// Zkratky se berou z <see cref="TemplateDesigner.Designer.KeyboardShortcuts"/>, ručně se tu píše jen popis.
/// </summary>
public class HelpDialogViewModel : EDViewModel
{
    private readonly List<HelpSection> _all;

    private List<HelpSection> _filteredSections;
    public List<HelpSection> FilteredSections
    {
        get => _filteredSections;
        private set => Set(ref _filteredSections, value);
    }

    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set { Set(ref _searchText, value); ApplyFilter(); }
    }

    public bool NothingFound => FilteredSections.Count == 0;

    /// <param name="nazev">Název instance aktuální agendy (linka) - nápověda o výběru a tisku mluví jeho slovy.</param>
    public HelpDialogViewModel(NazevInstance? nazev = null)
    {
        _all = BuildContent(nazev ?? NazevInstance.Obecny);
        _filteredSections = _all;
    }

    private void ApplyFilter()
    {
        var query = Fold(SearchText.Trim());
        if (query.Length == 0)
        {
            FilteredSections = _all;
            OnPropertyChanged(nameof(NothingFound));
            return;
        }

        var result = new List<HelpSection>();
        foreach (var section in _all)
        {
            bool titleMatch = Fold(section.Title).Contains(query);
            bool tipMatch = section.Tip is not null && Fold(section.Tip).Contains(query);
            var entries = titleMatch
                ? section.Entries
                : section.Entries.Where(e => Fold(e.Action).Contains(query) || Fold(e.Shortcut).Contains(query)).ToList();

            if (entries.Count > 0 || tipMatch)
            {
                result.Add(new HelpSection
                {
                    Title = section.Title,
                    Entries = entries,
                    Tip = (titleMatch || tipMatch) ? section.Tip : null,
                });
            }
        }
        FilteredSections = result;
        OnPropertyChanged(nameof(NothingFound));
    }

    private static string Fold(string s) => SearchNormalization.NormalizeForSearch(s);

    // ─────────────────────────────────────────────────────────────────────
    // Obsah - zkratky z KeyboardShortcuts, popis ručně
    // ─────────────────────────────────────────────────────────────────────

    private static List<HelpSection> BuildContent(NazevInstance n) =>
    [
        new HelpSection
        {
            Title = "🚀 Rychlý start",
            Entries =
            [
                new("1. Otevřít šablonu z databáze / založit novou", Gestures(Open, New)),
                new("2. Zkontrolovat agendu a variantu (zelený štítek ve stavovém řádku); změna", ChangeAgenda.Gesture),
                new($"3. Vybrat {n.AkuzativMnozne} pro náhled (modrý štítek „{NazevInstance.SVelkym(n.Jednotne)}“ ve stavovém řádku)", SelectInstances.Gesture),
                new("4. Přidat sekce (tlačítko Přidat sekci) a prvky (táhnout z palety)", "myš"),
                new("5. Napojit data - pole ze záložky „Datová pole“ táhnout na plochu", "táhnout myší"),
                new($"6. Upravit vzhled - záložka Vlastnosti (rychlé) / {Properties.Gesture} (kompletní dialog)", Properties.Gesture),
                new("7. Uložit do databáze a vytisknout s reálnými daty", $"{Save.Gesture}, {Print.Gesture}"),
            ],
            Tip = $"Živý náhled vpravo ukazuje výsledek s daty {n.Shoda("vybrané", "vybraného", "vybraného", "vybraného")} {n.Genitiv} průběžně. " +
                  "Databáze je primární úložiště šablon - soubor JSON slouží jen pro přenos (menu Soubor → Import/Export).",
        },

        new HelpSection
        {
            Title = "📄 Soubor a šablony",
            Entries =
            [
                new("Nová šablona", New.Gesture),
                new("Otevřít z databáze (přehled šablon s filtrem)", Open.Gesture),
                new("Uložit do databáze (nová se zeptá na jméno)", Save.Gesture),
                new("Uložit jako (nová kopie v databázi)", "menu Soubor"),
                new("Import / export JSON souboru (přenos šablony)", "menu Soubor"),
                new("Vlastnosti stránky (formát, orientace, okraje, jazyk)", PageProperties.Gesture),
            ],
            Tip = "Při změně formátu nebo orientace stránky se návrhář zeptá, zda obsah přepočítat na novou velikost - " +
                  "úprava okrajů je běžná vratná změna.",
        },

        new HelpSection
        {
            Title = $"🖨 Tisk a export ({Print.Gesture})",
            Entries =
            [
                new($"Vytvořit PDF / PNG - z vybraných {n.GenitivMnozne}, bez {n.Shoda("vybrané", "vybraného", "vybraného", "vybraného")} {n.Genitiv} z ukázkových dat", $"{Print.Gesture} → Vytvořit"),
                new($"Členění výstupu - vše do jednoho / jeden PDF na {n.Akuzativ} / jeden PDF na zastávku a směr", "dialog tisku"),
                new("Název souboru z datových polí, např. {CISLO_LINKY}", "dialog tisku"),
                new("Víc stránek na jeden papír (šablona o víc stránkách)", "dialog tisku → Uspořádání na list"),
                new("Druhá stránka dokladu vedle první, nebo na zadní stranu listu", "dialog tisku → Uspořádání na list"),
                new($"Kontrola všech vybraných {n.GenitivMnozne} bez tisku", "dialog tisku → Zkontrolovat, lupa na liště"),
                new("Hlášky k tisku - informace, upozornění, problém", "blok vpravo ve stavovém řádku"),
            ],
            Tip = "Hlášky tisk nezastaví. Problém znamená, že se část obsahu na papír nedostane (ořízne se, " +
                  "nevejde se); rada u hlášky říká, co upravit. Volby výstupu si aplikace pamatuje pro každou agendu zvlášť.",
        },

        new HelpSection
        {
            Title = $"🗂 Data, agenda a {n.Mnozne}",
            Entries =
            [
                new("Změnit agendu / variantu (založí novou prázdnou šablonu)", ChangeAgenda.Gesture),
                new($"Výběr {n.GenitivMnozne} pro náhled a tisk", $"{SelectInstances.Gesture} / modrý štítek"),
                new($"Předchozí a další z vybraných {n.GenitivMnozne}", "◀ ▶ u štítku"),
                new($"Seznam vybraných {n.GenitivMnozne} k přepnutí", "▲ u štítku"),
                new("Obnovit data z databáze (znovu načte ze serveru)", RefreshData.Gesture),
                new("Datová pole → nový text s {POLE} na ploše", "táhnout pole na plochu"),
                new("Datová pole → přidat {POLE} do existujícího textu", "táhnout pole na text"),
                new("Zkopírovat {NAZEV_POLE} do schránky", "dvojklik na pole"),
                new("Parametry sestavy (živě mění náhled)", "tab Parametry vlevo / tiskový dialog"),
            ],
            Tip = "Agenda říká JAKOU sestavu děláte (Zastávkový JŘ, …), varianta odkud jsou data (AD / MHD). " +
                  "Šablona je vázaná na konkrétní kombinaci - vidíte ji na zeleném štítku a v titulku okna.",
        },

        new HelpSection
        {
            Title = "⌨ Výběr a myš",
            Entries =
            [
                new("Výběr prvku", "klik"),
                new("Přidat do výběru", AddToSelection.Gesture),
                new("Vybrat jen jeden prvek ze skupiny", SelectFromGroup.Gesture),
                new("Výběr více prvků oblastí", "táhnout přes prázdno"),
                new("Vybrat vše v aktivní sekci", SelectAll.Gesture),
                new("Vlastnosti výběru (1 prvek / společné vlastnosti více)", $"{Properties.Gesture} / dvojklik"),
                new("Úprava textu přímo na plátně (textový prvek)", EditContent.Gesture),
                new("Zrušit výběr / probíhající akci", Cancel.Gesture),
            ],
            Tip = $"{EditContent.Gesture}: {ConfirmText.Gesture} = potvrdit, {NewLine.Gesture} = nový řádek, {DiscardText.Gesture} = zrušit.",
        },

        new HelpSection
        {
            Title = "✏ Editace",
            Entries =
            [
                new("Zpět / Vpřed", Gestures(Undo, Redo)),
                new("Vyjmout / Kopírovat / Vložit", Gestures(Cut, Copy, Paste)),
                new("Duplikovat (kopie posunutá o 5 mm)", Duplicate.Gesture),
                new("Duplikovat vedle originálu s mezerou pro přichytávání (výchozí 2 mm)", DuplicateBeside.Gesture),
                new("Kopírovat / vložit STYL (písmo, barvy, rámeček)", Gestures(CopyStyle, PasteStyle)),
                new("Seskupit / rozeskupit prvky", Gestures(Group, Ungroup)),
                new("Rozdělit prvek vodorovně / svisle na poloviny", Gestures(SplitHorizontally, SplitVertically)),
                new("Smazat výběr", Delete.Gesture),
                new("Posun o 1 mm / o krok mřížky", Gestures(Move, MoveByGrid)),
                new("Změna velikosti o 1 mm (pravá a dolní hrana)", Resize.Gesture),
                new("Vrstva: přenést dopředu / odeslat dozadu (zcela: +Shift)", Gestures(BringForward, SendBackward)),
                new("Zarovnání a rozložení vybraných prvků", "lišta nástrojů / menu Upravit"),
                new("Normalizovat pozice (zaokrouhlit na krok)", NormalizePositions.Gesture),
            ],
            Tip = "Hromadná úprava: vyberte víc prvků a stiskněte Enter - dialog společných vlastností.",
        },

        new HelpSection
        {
            Title = "📑 Sekce",
            Entries =
            [
                new("Přidat sekci", "tlačítko Přidat sekci"),
                new("Přesunout aktivní sekci nahoru / dolů", Gestures(MoveSectionUp, MoveSectionDown)),
                new("Přesunout sekci tažením", "táhnout myší v panelu"),
                new("Výška sekce myší - patička se tahá za HORNÍ hranu (roste nahoru)", "táhnout hranu na ploše"),
                new("Vlastnosti / smazání sekce", "pravý klik"),
                new("Uložit sekci jako šablonu pro opakované použití", "pravý klik → Uložit jako šablonu"),
            ],
            Tip = "Typ sekce určuje, kde se tiskne: obsah jednou v pořadí šablony, hlavička a patička na " +
                  "stránkách podle volby Tisknout na. U obsahu se volí přetečení (držet pohromadě / rozdělit " +
                  "přes stránky) a Začít na nové stránce.",
        },

        new HelpSection
        {
            Title = "👁 Zobrazení",
            Entries =
            [
                new("Přizpůsobit stránku oknu", FitToPage.Gesture),
                new("Skutečná velikost 100 %", ActualSize.Gesture),
                new("Zoom ke kurzoru", ZoomAtCursor.Gesture),
                new("Sbalit / rozbalit levý panel", ToggleLeftPanel.Gesture),
                new("Mřížka plátna, přichytávání, velikost kroku (0,2–10 mm)", "lišta nástrojů"),
                new("Strom šablony (sekce → skupiny → prvky)", "záložka Strom"),
                new("Nápověda (tento dialog)", TemplateDesigner.Designer.KeyboardShortcuts.Help.Gesture),
            ],
        },

        new HelpSection
        {
            Title = "🔲 Prvky",
            // Prvky a jejich popis z katalogu - totéž, co ukazuje bublina palety.
            Entries = [.. Models.ElementCatalog.All.Select(e =>
                new HelpEntry($"{e.Name} - {e.Hint}", e.IsGeneric ? "paleta" : "paleta (JŘ agenda)"))],
            Tip = "Tabulka s řádky z dat umí: počítané sloupce ({CENA}*{POCET}), podmíněnou viditelnost řádku, " +
                  "zvýraznění řádku podle výrazu, řazení řádků (Řadit podle + Směr řazení), nejméně a nejvýš řádků " +
                  "a souhrnné řádky (SUM, COUNT, AVG…). Vše v dialogu tabulky (dvojklik).",
        },

        new HelpSection
        {
            Title = "▦ Pevná tabulka - vyplňování",
            Entries =
            [
                new("Dovnitř do buňky (rovnou do té, na kterou kliknete)", EnterCell.Gesture),
                new("Dovnitř do naposledy kliknuté buňky / upravit obsah", EditCell.Gesture),
                new("Posun po buňkách / potvrdit a dál", $"šipky / {NextCell.Gesture} / {CellBelow.Gesture}"),
                new("Psaní přepíše obsah buňky, Esc vede ven", $"psaní / {LeaveCells.Gesture}"),
                new("Nabídka datových polí uvnitř buňky", "napsat {"),
                new("Výběr více buněk (obdélník)", $"táhnout / {ExtendCellSelectionByClick.Gesture} / {ExtendCellSelection.Gesture} / {SelectAllCells.Gesture}"),
                new("Tučné / kurzíva / podtržení buňky (i během psaní)", Gestures(CellBold, CellItalic, CellUnderline)),
                new("Vlastnosti buňky - písmo, barvy, formát hodnoty", CellProperties.Gesture),
                new("Vymazat obsah buněk (ne prvek)", ClearCells.Gesture),
                new("Řádky a sloupce, sloučení buněk, vlastnosti řádku", "pravý klik"),
                new("Šířka sloupce / výška řádku", "táhnout vnitřní hranici"),
            ],
            Tip = $"Obsah z Excelu jde vložit rovnou ({PasteCells.Gesture}) - tabulka se dorovná. Vlastnosti řádku "
                  + "nabízejí podmínku viditelnosti: řádek se vytiskne, jen když podmínka platí "
                  + "(např. {VS} schová řádek u dokladu bez variabilního symbolu).",
        },

        new HelpSection
        {
            Title = "🚌 Vývěska JŘ - obsah a značky",
            Entries =
            [
                new("Které poznámky se tisknou (negativní značky, speciální znaky, textové, poznámky linky)",
                    "prvek Poznámky → Obsah poznámek"),
                new("Značky u odjezdů: přeprava kol, místenky, pokračující spoje",
                    "prvek Poznámky → Značky u odjezdů"),
                new("Souhrn bezbariérových spojů (jedna věta místo značky u každého odjezdu)",
                    "dtto Značky u odjezdů"),
                new("Poznámky linky - výluky, tarifní, všeobecné, radiobus",
                    "prvek Poznámky → Poznámky linky"),
                new("Řazení poznámek a směr plnění sloupců legendy", "prvek Poznámky → Rozložení"),
                new("Dlouhá trasa na užší vývěsku - rozdělit seznam zastávek na oddíly vedle sebe",
                    "prvek Seznam zastávek → Počet oddílů"),
                new("Aktuální a projeté zastávky, šipka směru", "prvek Seznam zastávek"),
                new("Skupiny dnů, šířka minuty, mezera mezi skupinami dnů, značky u minut", "prvek Minuty odjezdů"),
                new("Které sloupce vývěsky vzniknou (vzory i vlastní definice)",
                    "prvek Minuty odjezdů → Definice sloupců"),
                new("Písmeno varianty u minuty říká, který sloupec časů pro spoj platí; zda má "
                    + "písmeno i první varianta", "tab Parametry → První varianta bez písmene"),
                new("Kolik vývěsek se vytiskne (zastávka × směr) a co je na které", "lišta stránkování nad náhledem"),
            ],
            Tip = "Značky u odjezdů a souhrn bezbariérovosti se zapínají v prvku Poznámky, " +
                  "NE v parametrech sestavy - dřív to byly parametry tisku. Značka u odjezdu a její vysvětlivka " +
                  "jsou jedna volba, takže nemůže vzniknout značka, kterou legenda nevysvětlí. " +
                  "Když šablona prvek Poznámky nemá, značky se nevytisknou vůbec. " +
                  "Rozvržení sloupců drží šablona: parametr sestavy „Rozdělení sloupců podle platnosti“ je jen pohled " +
                  "na definici v prvku odjezdů, takže se ta dvě nastavení nemůžou přebíjet. " +
                  "V seznamu zastávek jsou jen zastávky, kam z té vývěsky opravdu nějaký spoj jede.",
        },

        new HelpSection
        {
            Title = "🚆 Tabulka spojů - linkový JŘ",
            Entries =
            [
                new("Které popisné sloupce se tisknou (km, tarifní číslo, zóna, MHD, označník)",
                    "prvek Tabulka spojů → Upravit sloupce…"),
                new("Řádek platnosti pod číslem spoje - denní značky, místenka, atributy vozidla",
                    "prvek Tabulka spojů → Řádky"),
                new("Obousměrný tisk - spoje opačného směru na druhé straně od zastávek", "parametry sestavy → Obecné"),
                new("Tabulka širší než prvek se rozřeže na bloky pod sebe", "automaticky"),
                new("Dorovnat prázdnými sloupci - volné místo vpravo se vyplní prázdnými sloupci", "prvek Tabulka spojů → Šířka sloupců"),
                new("Mezery u značek a vnitřní okraje buňky - řídí, kolik spojů je na bloku",
                    "prvek Tabulka spojů → Šířka sloupců, Datové buňky"),
                new("Stejně široké sloupce spojů - jinak je každý tak široký, jak potřebuje",
                    "prvek Tabulka spojů → Šířka sloupců"),
                new("Tučné zastávky a čáry u zastávek podle atributů trasy", "prvek Tabulka spojů → Datové buňky"),
                new("Sloučit popis u příjezdu a odjezdu (tvar postskriptového tisku)", "prvek Tabulka spojů"),
                new("Přípojné řádky Vyčká / Navazuje pod zastávkou", "prvek Tabulka spojů → Řádky"),
            ],
            Tip = "Zastávka, která má v trase atribut příjezdu a odjezdu, zabírá dva řádky. Vypnuté „Sloučit popis“ " +
                  "zopakuje název a km na obou (tvar windowsového tisku), zapnuté je vypíše jednou přes oba " +
                  "a mezi časy dá vodorovnou úsečku. Značky u zastávky a čáry z atributů trasy jdou z jízdních " +
                  "řádů - vypínají se v prvku, ne v parametrech sestavy.",
        },

        new HelpSection
        {
            Title = "ƒ Výrazy a datová pole",
            Entries =
            [
                new("Hodnota pole", "{CISLO_LINKY}"),
                new("Formát čísla / textu", "{CENA:N2}, {TEXT:UPPER}, {POLE:LEFT(4)}"),
                new("Aritmetika (počítané sloupce)", "{CENA}*{POCET}"),
                new("Podmínky (viditelnost prvku/sekce/řádku)", "{POCET} > 0 && {TYP} == 'A'"),
                new("Agregace v souhrnných řádcích tabulky", "SUM({CENA}*{POCET}), COUNT, AVG, MIN, MAX"),
                new("Číslo stránky / celkem stran", "{STRANA} / {POCET_STRAN}"),
                new("Pokračování / opačný směr (jen v sekci na každé stránce)", "{POKRACOVANI}"),
            ],
            Tip = "Výraz viditelnosti se zadává ve vlastnosti „Podmínka viditelnosti“ - prázdný = vždy viditelné. " +
                  $"Formátování čísel a datumů řídí jazyk šablony ({PageProperties.Gesture}), ne nastavení Windows.",
        },

        new HelpSection
        {
            Title = "💡 Tipy a triky",
            Entries =
            [
                new("Proporcionální škálování celé šablony", "Upravit → Přepočítat šablonu na jinou velikost"),
                new("Zamknout prvek proti posunu/změně velikosti", "vlastnost Uzamčeno"),
                new("Chytré přichytávání k hranám sousedů (i v mezeře pro přichytávání)", "při tažení / změně velikosti"),
                new("Výška podle obsahu - prvek roste s obsahem (text se zalamuje)", "Výška prázdná"),
                new("JŘ prvek s pevnou výškou zmenší písmo, aby se obsah vešel", "automatické zmenšení"),
                new("AI průvodce - zeptejte se, kde co v aplikaci najdete", "záložka Pomocník"),
            ],
        },
    ];
}
