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
- Vybrat linky/instance: klik na modrý chip ve status baru (u JŘ popsaný 'Linka')
  → výběr se search boxem; ◀▶ listuje, ↻ (nebo F5) znovu načte data.
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
- Kopírování stylu mezi elementy: Ctrl+Shift+C / Ctrl+Shift+V.
- Hromadná úprava: vybrat víc elementů (rubber band / Ctrl+klik) → Enter → dialog
  společných vlastností.
- Podmíněné zvýraznění řádků tabulky: dialog tabulky → 'Zvýraznění řádků' → pravidla
  s výrazem nad daty řádku (např. {CENA} > 1000 → barva pozadí).

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
- Ctrl+P - tisk; Ctrl+Shift+P - vlastnosti stránky; Ctrl+Shift+A - změnit agendu
- Ctrl+0 - přizpůsobit stránce; Ctrl+1 - 100 %; Ctrl+B - sbalit levý panel
- F2 - inline editace textu; Enter - vlastnosti výběru; Delete - smazat
- F5 - obnovit data z databáze; F1 - nápověda; Esc - zrušit probíhající akci

## Menu Nástroje
Změnit agendu, Šablony v databázi (správce), AI Asistent šablon (vygeneruje šablonu
z popisu), Import PDF (existující PDF → šablona, volitelně s AI), AI průvodce (tento chat).

## Co aplikace ZATÍM neumí (odpovídej 'zatím neumíme')
Sloučené buňky tabulky (merge / colspan / rowspan), Excel export, odeslání PDF e-mailem
přímo z aplikace, brožura/duplex řazení stránek při imposici, zaoblené rohy rámečků
elementů (samostatný obdélník zaoblení má), fonty nahrané uživatelem (k dispozici jsou
vestavěné rodiny).";

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
        sb.AppendLine(BuildCurrentContext(agendaName, selectedElementType, sectionCount));
        sb.AppendLine();
        sb.AppendLine(BuildElementCatalog(elementKind));
        return sb.ToString();
    }

    private static string BuildCurrentContext(string? agendaName, string? selectedElementType, int sectionCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Aktuální kontext");
        sb.AppendLine($"- Agenda: {(string.IsNullOrEmpty(agendaName) ? "obecná (bez dat)" : agendaName)}");
        sb.AppendLine($"- Počet sekcí v šabloně: {sectionCount}");
        sb.AppendLine(selectedElementType is null
            ? "- Vybraný element: žádný"
            : $"- Vybraný element: {FriendlyName(selectedElementType)}");
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
        ElementTypes.Line, ElementTypes.Barcode, ElementTypes.DynamicTable
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
        ElementTypes.StopList      => "Seznam zastávek (zastávkový JŘ)",
        ElementTypes.DepartureGrid => "Mřížka odjezdů (zastávkový JŘ)",
        ElementTypes.NotesLegend   => "Legenda poznámek (zastávkový JŘ)",
        ElementTypes.LineTable     => "Tabulka linky (linkový JŘ)",
        _ => type
    };

    private static string? ElementNote(string type) => type switch
    {
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
            "Sloupcová tabulka celé linky (zastávky × spoje) pro linkový/knižní JŘ. Sloupce ve vlastním dialogu.",
        _ => null
    };
}
