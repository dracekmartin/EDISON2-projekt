using System.Text;
using TemplateDesigner.Models;

namespace TemplateDesigner.Services;

/// <summary>
/// Sestaví znalostní bázi pro pasivního AI průvodce.
///
/// Hybrid přístup: ručně psaný obecný úvod (co je nástroj, jak se ovládá - stabilní,
/// nemění se) + auto-generovaný katalog vlastností elementů z <c>GetPropertyDescriptors()</c>
/// (nikdy nedriftuje od kódu).
///
/// Katalog je agenda-aware: vždy obsahuje obecné elementy, specializované JŘ elementy
/// se přidají jen u jízdněřádových agend (aby se modelu poslaly právě ty, se kterými
/// uživatel pracuje).
/// </summary>
public static class CapabilityCatalog
{
    // ─── Obecný úvod (ručně psaný, stabilní) ──────────────────────────────────

    public const string GeneralIntro = @"# TemplateDesigner - co to je

TemplateDesigner je WPF nástroj pro vizuální návrh tiskových šablon (sestav).
Šablona = stránka rozdělená na sekce, v sekcích jsou elementy (text, obrázek,
tabulka, čára, kód, specializované jízdněřádové elementy). Při tisku se šablona spojí
s daty z agendy → PDF.

## Základní pojmy
- Šablona - kořenový dokument: velikost stránky, okraje, kultura (cs-CZ…), sekce.
  Ukládá se do DATABÁZE (Ctrl+S / Ctrl+O); soubor JSON je jen import/export.
- Sekce - vodorovný pruh stránky. Má chování tisku (jednou / na každé stránce /
  jen první / jen poslední) a chování při přetečení (nezalamovat / rozdělit přes stránky).
  Role: záhlaví / tělo / patička.
- Element - konkrétní obsah v sekci. Pozice jsou v milimetrech, relativně
  k levému hornímu rohu sekce. Výška = auto znamená, že se dopočítá z obsahu.
- Placeholder - {NAZEV_POLE} se při tisku nahradí hodnotou z dat agendy.
- Agenda a Varianta - agenda říká, JAKOU sestavu dělám (Zastávkový JŘ, Faktury…),
  varianta říká zdroj dat (AD = linková doprava, MHD). Šablona je vázaná na konkrétní
  kombinaci; zobrazuje ji zelený chip ve status baru a titulek okna.
- Instance - konkrétní záznam, nad kterým se tiskne (u JŘ = linka). Vybírá se
  modrým chipem ve status baru - chip je popsaný podle agendy (u jízdních řádů 'Linka');
  klik = výběr více linek, ◀▶ = listování.

## Výrazový jazyk (v placeholderech a podmínkách)
- Formátování: {CENA:N2} (2 desetinná místa), {TEXT:UPPER}, {POLE:LEFT(4)}.
- Aritmetika: {CENA}*{POCET}, závorky a priorita fungují - použitelné v počítaných
  sloupcích tabulky (Expression) a v agregacích.
- Podmínky: {POCET} > 0, {TYP} == 'A', kombinace přes && a || - použitelné ve
  viditelnosti elementu/sekce (VisibilityExpression), viditelnosti řádku tabulky
  a v pravidlech zvýraznění řádku.
- Agregace v souhrnných řádcích tabulky: SUM({CENA}*{POCET}), COUNT, AVG, MIN, MAX.

## UI mapa - kde co najdu (Co → Kde)
### Chci tisknout / exportovat
- Tisk s reálnými daty: Ctrl+P → dialog tisku (parametry sestavy + tlačítka exportu).
  Aliasy: 'vytisknout linku', 'PDF se skutečnými daty'.
- Více vývěsek na jeden list / tisk 2×A5 na A4 / řezání (imposice): Ctrl+P → skupina
  'Export' → pole 'Uspořádání na list' (Vypnuto / A4 / A3 × výška/šířka) + 'Mřížka listu'
  + 'Každá stránka na celý list (pro řezání)' + 'Ořezové značky'. Volba se pamatuje
  per agenda - 1 stránka = 1 list vrátíš volbou 'Vypnuto'.
- Rozdělení výstupu do více souborů: Ctrl+P → 'Členění výstupu' (vše do jednoho /
  per linka / per zastávka×směr) + 'Název souboru' s {POLE} tokeny.
- Rychlý export náhledu bez dialogu: toolbar PDF/PNG (exportuje aktuální instance).
- Náhled se nevejde na stránku: červený rámeček stránky v náhledu + varování ve
  status baru; řeší se zmenšením obsahu, pevnou výškou elementu (auto-scale) nebo
  rozdělitelnou sekcí.

### Chci pracovat s daty
- Změnit agendu/variantu: Ctrl+Shift+A, nebo klik na zelený chip ve status baru.
  Vytvoří NOVOU prázdnou šablonu (obsah se mezi agendami nepřenáší).
- Vybrat linky/instance: Ctrl+Shift+I, nebo klik na modrý chip ve status baru (u JŘ popsaný
  'Linka') → výběr se search boxem; ◀▶ listuje, ▲ rozbalí seznam vybraných, ↻ (nebo F5) znovu
  načte data.
- Pole agendy: záložka 'Pole agendy' v levém panelu; pole lze táhnout na canvas
  (vytvoří text) nebo na existující text (přidá placeholder); dvojklik = do schránky.
- Parametry sestavy: lišta parametrů nad náhledem (živě mění náhled) - tytéž
  hodnoty jsou v dialogu tisku.

### Chci upravit vzhled
- Výchozí (globální) font celé šablony: Ctrl+Shift+P (Vlastnosti stránky) → pole
  'Výchozí font'. Je to kořen kaskády - zdědí ho všechny elementy, které nemají
  vlastní font nastavený. Prázdné = vestavěný default (Arial).
- Rychlé vlastnosti: záložka 'Vlastnosti' v levém panelu (aktualizuje se dle výběru).
- Kompletní nastavení: Enter nebo dvojklik na element → dialog; u tabulkových
  elementů má dialog záložku/tlačítko 'Sloupce' (pořadí, šířky, viditelnost,
  per-sloupec styl písma a barev).
- Zaoblené nebo zkosené rohy: nastavení prvku → skupina rámečku → 'Rohy' + 'Velikost rohu'
  (platí pro každý prvek, nejen pro obdélník).
- Kopírování stylu mezi elementy: Ctrl+Shift+C / Ctrl+Shift+V.
- Hromadná úprava: vybrat víc elementů (rubber band / Ctrl+klik) → Enter → dialog
  společných vlastností.
- Podmíněné zvýraznění řádků tabulky: dialog tabulky → 'Zvýraznění řádků' → pravidla
  s výrazem nad daty řádku (např. {CENA} > 1000 → barva pozadí).

### Chci pracovat uvnitř pevné tabulky
- Rozměr (počet sloupců a řádků) se zadává rovnou při vkládání tabulky.
- Dovnitř: Alt+klik do buňky, nebo F2, nebo pravý klik → 'Vyplnit tabulku'. Ven Esc.
  Dvojklik zůstává na nastavení prvku, stejně jako u ostatních prvků.
- Uvnitř: psaní přepíše buňku, F2 upraví stávající obsah, Tab/Enter posunou, šipky chodí
  po buňkách, Delete vymaže obsah (ne prvek), Ctrl+B/I/U formátují buňku i během psaní.
- Výběr: tažení přes buňky = obdélníková oblast, Shift+šipky rozšíří, Ctrl+A vybere celou
  tabulku. Schránka pracuje s buňkami ve tvaru čitelném Excelem; jedna hodnota vyplní
  označenou oblast.
- Panel vlastností i lišta patří uvnitř tabulky VYBRANÉ BUŇCE; nastavení celé tabulky je
  přes tlačítko 'Nastavení celé tabulky' (nebo Esc ven). Vlastnosti buňky i v okně: Alt+Enter.
- Pravý klik: 'Sloučit / Rozdělit buňky' (vodorovně) a 'Vlastnosti řádku' (podmínka
  viditelnosti řádku + pozadí).
- Šířky sloupců a výšky řádků: tažením vnitřní hranice; vnější hrana mění velikost prvku.
- Datové pole do buňky: napiš '{' a vyber z nabídky (Enter/Tab doplní i koncovou závorku).

### Chci spravovat šablony
- Otevřít z databáze: Ctrl+O → přehled šablon (sloupce Agenda + Varianta, filtr,
  sekce Systémové M-line = read-only předlohy / Moje).
- Uložit: Ctrl+S (nová šablona se zeptá na jméno; existující tiše aktualizuje).
  'Uložit jako' je v menu Soubor (bez zkratky).
- Import/export JSON souboru: menu Soubor → Importovat/Exportovat JSON.
- Vlastnosti stránky (rozměr, orientace, okraje, kultura): Ctrl+Shift+P.
  POZOR: změna rozměru/orientace = nová prázdná šablona (s potvrzením).

### Navigace a zobrazení
- Zoom: Ctrl+kolečko (k kurzoru), Ctrl+0 = přizpůsobit stránce, Ctrl+1 = 100 %.
- Mřížka a přichytávání: toolbar - zobrazit mřížku, snap na mřížku (velikost 0,2–10 mm),
  snap na elementy; Ctrl+Shift+N normalizuje pozice existujících elementů.
- Levý panel: záložky Sekce / Pole agendy / Vlastnosti / Strom / Pomocník; Ctrl+B sbalí.
- Strom šablony: záložka 'Strom' - hierarchie sekce → skupiny → elementy.

## Klávesové zkratky (kompletní)
- Ctrl+Z / Ctrl+Y - zpět / vpřed
- Ctrl+X / Ctrl+C / Ctrl+V - vyjmout / kopírovat / vložit; Ctrl+A - vybrat vše v sekci
- Ctrl+D - duplikovat (+šipka = kopie vedle s mezerou)
- Ctrl+Shift+C / Ctrl+Shift+V - kopírovat / vložit styl
- Ctrl+G / Ctrl+Shift+G - seskupit / rozeskupit; Alt+klik - jeden prvek ze skupiny
- Ctrl+Shift+H / Ctrl+Shift+J - rozdělit element vodorovně / svisle
- Ctrl+Shift+N - normalizovat pozice na mřížku
- Šipky - posun elementu (Shift = velký krok, Ctrl = jemný)
- Ctrl+Shift+↑/↓ - přesun aktivní sekce nahoru / dolů
- Ctrl+PageUp / Ctrl+PageDown - pořadí ve vrstvě (Z-order); +Shift = úplně dopředu/dozadu
- Ctrl+N / Ctrl+O / Ctrl+S - nová / otevřít z databáze / uložit ('Uložit jako' zkratku NEMÁ)
- Ctrl+P - tisk; Ctrl+Shift+P - vlastnosti stránky; Ctrl+Shift+A - změnit agendu;
  Ctrl+Shift+I - výběr instancí
- Ctrl+0 - přizpůsobit stránce; Ctrl+1 - 100 %; Ctrl+B - sbalit levý panel
- F2 - inline editace textu; Enter - vlastnosti výběru; Delete - smazat
- F5 - obnovit data z databáze; F1 - nápověda; Esc - zrušit probíhající akci

## Menu Nástroje
Změnit agendu, Šablony v databázi (správce), Import PDF (z existujícího PDF vznikne
editovatelná šablona - u jízdních řádů rozpozná strukturu AI), AI průvodce (tento chat).
(Generátor šablon z popisu je zatím skrytý, nenabízej ho.)

## Písmo
- Nabídka fontů = rodiny nainstalované v tomhle počítači (+ pár vestavěných). Font, který
  si uživatel do Windows nainstaluje, se v nabídce objeví.
- Když šablona chce font, který na počítači není, aplikace to řekne (status bar po otevření
  a hláška po uložení PDF) a vykreslí místo něj náhradu. Šablony se sdílejí přes databázi,
  takže táž šablona může u kolegy vypadat jinak - proto to upozornění.
- Vložit soubor s fontem do šablony NEJDE; font musí být nainstalovaný.

## Co aplikace ZATÍM neumí (odpovídej 'zatím neumíme')
Excel export, odeslání PDF e-mailem přímo z aplikace, brožura/duplex řazení stránek při
imposici, vložení vlastního souboru s fontem do šablony, svislé sloučení buněk (vodorovné
umí), import starých šablon z EDISONu.";

    // ─── Pokyny pro model (jak má odpovídat) ──────────────────────────────────

    public const string SystemRole = @"Jsi vestavěný průvodce aplikací TemplateDesigner. Radíš uživateli, kde co najde
a jak něco nastavit. Jsi pasivní - sám nic neměníš, jen navádíš.

Styl odpovědí (DŮLEŽITÉ):
- Odpovídej VÝHRADNĚ na to, na co se uživatel ptá. Žádné pozdravy, žádné úvodní fráze
  ('Jasně', 'Ahoj', 'Rád pomohu'), žádné shrnutí na konci, žádné vycpávky.
- Maximálně stručně. Když stačí jedna věta, napiš jednu větu. Když je to postup,
  dej krátké číslované kroky a nic víc.
- Žádné opakování otázky, žádné omluvy.

Pravidla obsahu (ZÁVAZNÁ):
- Opírej se VÝHRADNĚ o znalosti níže (popis aplikace + UI mapa + katalog elementů).
- KAŽDÝ krok návodu musí citovat přesnou cestu z těchto znalostí: název menu/tlačítka,
  klávesovou zkratku, dialog nebo skupinu vlastností. Nikdy nepiš krok, jehož umístění
  neumíš doložit z podkladů - vlastnosti, tlačítka ani zkratky si NEVYMÝŠLEJ.
- Tři stavy jistoty:
  1. Funkce JE v podkladech → poraď s přesnou cestou.
  2. VÍŠ, že funkce chybí (v podkladech je uvedená jako neexistující, nebo jde o zjevně
     jinou aplikaci) → řekni jednou větou, že to aplikace zatím neumí.
  3. NEJSI SI JISTÝ (v podkladech to není, ale mohlo by existovat) → řekni 'Tím si nejsem
     jistý - mrkněte do nápovědy (F1) nebo na podporu M-line.' NEtvrď, že to neexistuje.
- Nejednoznačný dotaz → krátká doplňující otázka.";

    // ─── Auto-generovaný katalog elementů ─────────────────────────────────────

    /// <summary>
    /// Sestaví kompletní system prompt pro daný kontext.
    /// </summary>
    public static string BuildSystemPrompt(string? agendaName,
        string? selectedElementType, int sectionCount, string? elementKind)
    {
        var sb = new StringBuilder();
        sb.AppendLine(SystemRole);
        sb.AppendLine();
        sb.AppendLine(GeneralIntro);
        sb.AppendLine();
        sb.AppendLine(BuildCurrentContext(agendaName, selectedElementType, sectionCount, elementKind));
        sb.AppendLine();
        sb.AppendLine(BuildElementCatalog(elementKind));
        return sb.ToString();
    }

    /// <summary>
    /// Aktuální kontext + <b>pravidlo přednosti prvků</b>.
    /// </summary>
    /// <remarks>
    /// ⚠️ Bez pravidla přednosti dostane model specializovaný prvek a obecné tabulky jako
    /// rovnocenné, takže na dotaz „jak zruším čáry mezi km sloupci" **začne otázkou, o kterou
    /// tabulku jde** - přestože km sloupce má v linkové agendě jen tabulka linky (nález Jan 16. 9.).
    /// Uživatel se ptá na to, co má před sebou, ne na obecnou tabulku, kterou v šabloně nemá.
    /// </remarks>
    private static string BuildCurrentContext(string? agendaName, string? selectedElementType,
        int sectionCount, string? elementKind)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Aktuální kontext");
        sb.AppendLine($"- Agenda: {(string.IsNullOrEmpty(agendaName) ? "obecná (bez dat)" : agendaName)}");
        sb.AppendLine($"- Počet sekcí v šabloně: {sectionCount}");
        sb.AppendLine(selectedElementType is null
            ? "- Vybraný element: žádný"
            : $"- Vybraný element: {FriendlyName(selectedElementType)}");

        var specialni = AgendaCapabilities.ScheduleElementTypes(elementKind);
        if (specialni.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Který prvek má přednost");
            sb.AppendLine($"Tahle agenda má specializované prvky: {string.Join(", ", specialni.Select(FriendlyName))}.");
            sb.AppendLine("Dotaz o obsahu jízdního řádu (zastávky, spoje, časy, km, zóny, tarifní čísla,");
            sb.AppendLine("skupiny dnů, poznámky, přípoje) **odpovídej podle nich** - uživatel se ptá na to,");
            sb.AppendLine("co má v šabloně před sebou. Obecnou dynamickou nebo pevnou tabulku zmiň jen tehdy,");
            sb.AppendLine("když se na ni ptá výslovně, nebo když specializovaný prvek tu vlastnost opravdu nemá.");
            sb.AppendLine("**Nenabízej výběr mezi nimi** a neptej se zpět, o kterou tabulku jde.");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Auto-generovaný katalog: pro každý relevantní typ elementu vypíše jeho
    /// nastavitelné vlastnosti seskupené podle skupin (přímo z PropertyDescriptors).
    /// </summary>
    public static string BuildElementCatalog(string? elementKind)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Katalog elementů a jejich nastavení");
        sb.AppendLine("(Vlastnosti jsou seskupené tak, jak je uživatel vidí v panelu/dialogu.)");
        sb.AppendLine();

        foreach (var type in RelevantElementTypes(elementKind))
        {
            sb.AppendLine($"## {FriendlyName(type)}");
            var note = ElementNote(type);
            if (note is not null) sb.AppendLine(note);

            try
            {
                var element = TemplateDesignerViewModel.CreateElementByType(type, 0, 0);
                var byGroup = element.GetPropertyDescriptors()
                    .GroupBy(d => d.Group);

                foreach (var group in byGroup)
                {
                    sb.AppendLine($"- {group.Key}: {string.Join(", ", group.Select(DescribeDescriptor))}");
                }
            }
            catch
            {
                sb.AppendLine("- (vlastnosti se nepodařilo načíst)");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string DescribeDescriptor(PropertyDescriptor d) => d switch
    {
        EnumPropertyDescriptor e =>
            $"{e.Label} (volby: {string.Join(" / ", e.Options.Select(o => o.Label))})",
        FloatPropertyDescriptor f =>
            $"{f.Label} (číslo {f.Min:0.#}–{f.Max:0.#}{(string.IsNullOrEmpty(f.Unit) ? "" : " " + f.Unit)})",
        BoolPropertyDescriptor =>
            $"{d.Label} (ano/ne)",
        ColorPropertyDescriptor =>
            $"{d.Label} (barva)",
        ButtonPropertyDescriptor b =>
            $"{d.Label} → tlačítko '{b.ButtonLabel}' (otevře editor)",
        TextWithPickerPropertyDescriptor =>
            $"{d.Label} (text s poli {{POLE}})",
        SuggestStringPropertyDescriptor =>
            $"{d.Label} (text / výběr z nabídky)",
        StringPropertyDescriptor s =>
            $"{s.Label} ({(s.MultiLine ? "víceřádkový text" : "text")})",
        _ => d.Label
    };

    // ─── Mapování typů ────────────────────────────────────────────────────────

    private static readonly string[] GenericTypes =
    [
        ElementTypes.Text, ElementTypes.Image, ElementTypes.Rectangle,
        ElementTypes.Line, ElementTypes.Barcode, ElementTypes.DynamicTable, ElementTypes.Table
    ];

    /// <summary>
    /// Které typy elementů poslat modelu. Obecné vždy; JŘ elementy podle druhu agendy
    /// (ElementKind z registru) přes <see cref="AgendaCapabilities"/> - jediný zdroj pravdy,
    /// shodný s paletou/menu (žádná duplicitní heuristika z kódu agendy).
    /// </summary>
    private static IEnumerable<string> RelevantElementTypes(string? elementKind)
    {
        foreach (var t in GenericTypes) yield return t;
        foreach (var t in AgendaCapabilities.ScheduleElementTypes(elementKind)) yield return t;
    }

    private static string FriendlyName(string type) => type switch
    {
        ElementTypes.Text          => "Text",
        ElementTypes.Image         => "Obrázek",
        ElementTypes.Rectangle     => "Obdélník",
        ElementTypes.Line          => "Čára",
        ElementTypes.Barcode       => "Kód (QR / čárový)",
        ElementTypes.DynamicTable  => "Tabulka (dynamická)",
        ElementTypes.Table         => "Tabulka (pevná)",
        ElementTypes.StopList      => "Seznam zastávek (zastávkový JŘ)",
        ElementTypes.DepartureGrid => "Minuty odjezdů (zastávkový JŘ)",
        ElementTypes.NotesLegend   => "Legenda poznámek (zastávkový JŘ)",
        ElementTypes.LineTable     => "Tabulka linky (linkový JŘ)",
        _ => type
    };

    private static string? ElementNote(string type) => type switch
    {
        ElementTypes.Table =>
            "Mřížka, jejíž obsah vyplňuje uživatel - buňka nese text nebo pole {CISLO_LINKY}. " +
            "Nahrazuje skládání hlaviček a bloků (platební údaje, rekapitulace) z jednotlivých textů: " +
            "je to jeden prvek, se kterým se hýbe vcelku. Řádky se z dat negenerují - na to je tabulka dynamická.",
        ElementTypes.DynamicTable =>
            "Řádky se generují z datové kolekce agendy (DataSource). Dialog tabulky (dvojklik) umí navíc: " +
            "sloupce s mapováním na pole NEBO počítaným výrazem ({CENA}*{POCET}) a formátem; " +
            "podmíněnou viditelnost řádku (výraz nad daty řádku); pravidla zvýraznění řádku (výraz → barva pozadí); " +
            "min/max počet řádků (min doplní prázdné = pevná výška, max ořízne); " +
            "řazení řádků (Řadit podle + Směr; prázdné = pořadí z agendy); " +
            "souhrnné řádky s agregacemi SUM/COUNT/AVG/MIN/MAX (i nad výrazem, např. SUM({CENA}*{POCET})).",
        ElementTypes.StopList =>
            "Tabulka zastávek linky. Sloupce a jejich styl se nastavují ve vlastním dialogu sloupců " +
            "(vč. volitelného sloupce Označník). Podporuje podmíněnou viditelnost a zvýraznění řádků. " +
            "Dlouhou trasu lze rozdělit na víc oddílů vedle sebe (Rozdělit na více oddílů + Mezera mezi oddíly) - " +
            "každý oddíl nese celou tabulku znovu včetně záhlaví, takže prvek je nižší a širší.",
        ElementTypes.DepartureGrid =>
            "Mřížka minut odjezdů po skupinách dnů. Skupiny a sloupce mají vlastní dialog " +
            "(vč. přepisu nadpisu skupiny a sloupce hodin per skupina).",
        ElementTypes.NotesLegend =>
            "Legenda vysvětlivek/poznámek pod jízdním řádem (1–4 sloupce, řazení, směr plnění, filtr typů). " +
            "⚠️ Tady se zapínají i ZNAČKY U ODJEZDŮ - přeprava kol, místenky, pokračující spoje - a souhrn " +
            "bezbariérových spojů; NE v parametrech sestavy (dřív to parametry tisku byly). Značka a její " +
            "vysvětlivka jsou jedna volba. Bez tohoto prvku v šabloně se značky nevytisknou vůbec.",
        ElementTypes.LineTable =>
            "Sloupcová tabulka celé linky (zastávky × spoje) pro linkový/knižní JŘ. Zastávky jsou řádky, " +
            "spoje sloupce. Viditelnost, pořadí, šířky a styl sloupců má vlastní dialog (Upravit sloupce…). " +
            "POPISNÉ SLOUPCE (zóna, km, Tč, zastávka, MHD, šipka) jsou skupina: dají se přejmenovat, " +
            "dostat jeden společný nadpis ('Nadpis popisných sloupců'), slít bez vnitřních čar " +
            "('Popisné sloupce bez vnitřních čar') a oddělit od spojů mezerou ('Mezera mezi skupinami sloupců'). " +
            "⚠️ ŠÍŘKA SLOUPCE SPOJE rozhoduje o všem ostatním: kolik spojů se vejde vedle sebe, na kolik ČÁSTÍ " +
            "se tabulka rozřeže a kolik vyjde stran. Ovlivňují ji 'Nejmenší/Největší šířka sloupce spoje', " +
            "'Stejně široké sloupce spojů', vnitřní okraj datových buněk a velikost jejich písma " +
            "(to řídí i výšku řádku, když je prázdná). " +
            "Skupiny dnů (pracovní / sobota / neděle) se při dělení oddělí samy, když to nestojí část navíc. " +
            "Když tabulce do stránky chybí málo, písmo se samo zmenší (do 90 %), místo aby přibyla strana. " +
            "Přípojné řádky 'vyčká/navazuje' se vkládají pod zastávku a dají se slučovat podle směru. " +
            "Zastávka s atributem P/J se rozdvojí na příjezd a odjezd; 'Sloučit popis u příjezdu a odjezdu' " +
            "vypíše km, Tč, název a MHD jednou přes oba řádky a mezi časy dá vodorovnou úsečku.",
        _ => null
    };
}
