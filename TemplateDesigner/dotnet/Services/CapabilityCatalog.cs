using System.Text;
using TemplateDesigner.Models;

namespace TemplateDesigner.Services;

/// <summary>
/// Sestaví znalostní bázi pro pasivního AI průvodce.
///
/// Hybrid přístup: ručně psaný obecný úvod (co je nástroj, jak se ovládá — stabilní,
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

    public const string GeneralIntro = @"# TemplateDesigner — co to je

TemplateDesigner je WPF nástroj pro vizuální návrh tiskových šablon (sestav).
Šablona = stránka rozdělená na sekce, v sekcích jsou elementy (text, obrázek,
tabulka, čára, kód, specializované jízdněřádové elementy). Při tisku se šablona spojí
s daty z agendy → PDF.

## Základní pojmy
- Šablona — kořenový dokument: velikost stránky, okraje, kultura (cs-CZ…), sekce.
- Sekce — vodorovný pruh stránky. Má chování tisku (jednou / na každé stránce /
  jen první / jen poslední) a chování při přetečení (nezalamovat / rozdělit přes stránky).
  Role: záhlaví / tělo / patička.
- Element — konkrétní obsah v sekci. Pozice jsou v milimetrech, relativně
  k levému hornímu rohu sekce. Výška = auto znamená, že se dopočítá z obsahu.
- Placeholder — {NAZEV_POLE} se při tisku nahradí hodnotou z dat agendy.
  Lze i formátovat: {CENA:N2}, {TEXT:UPPER}, {POLE:LEFT(4)}.
- Agenda — zdroj dat (např. zastávkový jízdní řád). Určuje, jaká pole jsou k dispozici
  a jaké specializované elementy dávají smysl.

## Jak se to ovládá (hlavní cesty)
- Přidání elementu — paleta vlevo (táhni na canvas) nebo tlačítko 'Přidat element'.
- Výběr — klik na element; Alt+klik vybere jen jeden prvek ze skupiny.
- Vlastnosti — záložka 'Vlastnosti' v levém panelu (rychlý přístup), nebo Enter /
  dvojklik otevře dialog. U tabulkových elementů je detailní nastavení sloupců v dialogu.
- Sekce — panel sekcí vlevo: přidat, přejmenovat, přetáhnout pořadí, smazat.
- Náhled — pravá část okna ukazuje reálný výsledek s daty (live preview).
- Tisk / export — dialog tisku (parametry + export do PDF), nebo export náhledu do PDF/PNG.

## Klávesové zkratky (nejdůležitější)
- Ctrl+Z / Ctrl+Y — zpět / vpřed
- Ctrl+X / Ctrl+C / Ctrl+V — vyjmout / kopírovat / vložit
- Ctrl+D — duplikovat (Ctrl+D + šipka = kopie vedle s mezerou)
- Ctrl+Shift+C / Ctrl+Shift+V — kopírovat / vložit styl
- Ctrl+G / Ctrl+Shift+G — seskupit / rozeskupit
- Ctrl+S / Ctrl+Shift+S — uložit / uložit jako
- Ctrl+O / Ctrl+N — otevřít / nová šablona
- Ctrl+0 — přizpůsobit stránku oknu (zoom)
- Ctrl+Shift+A — změnit agendu
- Ctrl+Shift+N — normalizovat pozice (přichytit na mřížku)
- Delete — smazat výběr; Enter — vlastnosti výběru

## Menu (horní lišta)
- Soubor — Nová / Otevřít / Uložit / Uložit jako / Otevřít z Caché / Uložit do Caché / Export PDF.
- Upravit — Zpět/Vpřed, schránka, zarovnání, mřížka a snap, zoom.
- Zobrazení — panely (sekce, vlastnosti, strom), live náhled.
- Nástroje — Změnit agendu, Šablony v Caché, AI Asistent šablon, Import PDF, AI průvodce.";

    // ─── Pokyny pro model (jak má odpovídat) ──────────────────────────────────

    public const string SystemRole = @"Jsi vestavěný průvodce aplikací TemplateDesigner. Radíš uživateli, kde co najde
a jak něco nastavit. Jsi pasivní — sám nic neměníš, jen navádíš.

Styl odpovědí (DŮLEŽITÉ):
- Odpovídej VÝHRADNĚ na to, na co se uživatel ptá. Žádné pozdravy, žádné úvodní fráze
  ('Jasně', 'Ahoj', 'Rád pomohu'), žádné shrnutí na konci, žádné vycpávky.
- Maximálně stručně. Když stačí jedna věta, napiš jednu větu. Když je to postup,
  dej krátké číslované kroky a nic víc.
- Žádné opakování otázky, žádné omluvy.

Pravidla obsahu:
- Opírej se VÝHRADNĚ o znalosti níže (popis aplikace + katalog elementů a jejich vlastností).
  Když něco v katalogu není, řekni jednou větou, že to aplikace (zatím) neumí.
  NIKDY si nevymýšlej vlastnosti, tlačítka ani zkratky, které nejsou uvedené.
- Při hledání nastavení uveď přesně: který element, která záložka/dialog, v jaké skupině vlastností.
- Nejednoznačný dotaz → krátká doplňující otázka.";

    // ─── Auto-generovaný katalog elementů ─────────────────────────────────────

    /// <summary>
    /// Sestaví kompletní system prompt pro daný kontext.
    /// </summary>
    public static string BuildSystemPrompt(string? agendaCode, string? agendaName,
        string? selectedElementType, int sectionCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine(SystemRole);
        sb.AppendLine();
        sb.AppendLine(GeneralIntro);
        sb.AppendLine();
        sb.AppendLine(BuildCurrentContext(agendaName, selectedElementType, sectionCount));
        sb.AppendLine();
        sb.AppendLine(BuildElementCatalog(agendaCode));
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
    public static string BuildElementCatalog(string? agendaCode)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Katalog elementů a jejich nastavení");
        sb.AppendLine("(Vlastnosti jsou seskupené tak, jak je uživatel vidí v panelu/dialogu.)");
        sb.AppendLine();

        foreach (var type in RelevantElementTypes(agendaCode))
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
        ElementTypes.Line, ElementTypes.Barcode, ElementTypes.Table
    ];

    private static readonly string[] ScheduleTypes =
    [
        ElementTypes.StopList, ElementTypes.DepartureGrid, ElementTypes.NotesLegend
    ];

    /// <summary>
    /// Které typy elementů poslat modelu. Obecné vždy; JŘ elementy jen u jízdněřádové agendy.
    /// </summary>
    private static IEnumerable<string> RelevantElementTypes(string? agendaCode)
    {
        foreach (var t in GenericTypes) yield return t;
        if (IsScheduleAgenda(agendaCode))
            foreach (var t in ScheduleTypes) yield return t;
    }

    /// <summary>
    /// Jízdněřádová agenda? Heuristika z kódu agendy (konvence: ZASTAVKOVY_JR_*, MHD, …).
    /// Až přibudou další JŘ typy (linkový, knižní…) s vlastními elementy, rozšíří se zde mapování.
    /// </summary>
    private static bool IsScheduleAgenda(string? agendaCode)
    {
        if (string.IsNullOrEmpty(agendaCode)) return false;
        var c = agendaCode.ToUpperInvariant();
        return c.Contains("JR") || c.Contains("MHD") || c.Contains("JIZDNI");
    }

    private static string FriendlyName(string type) => type switch
    {
        ElementTypes.Text          => "Text",
        ElementTypes.Image         => "Obrázek",
        ElementTypes.Rectangle     => "Obdélník",
        ElementTypes.Line          => "Čára",
        ElementTypes.Barcode       => "Kód (QR / čárový)",
        ElementTypes.Table         => "Tabulka",
        ElementTypes.StopList      => "Seznam zastávek (zastávkový JŘ)",
        ElementTypes.DepartureGrid => "Mřížka odjezdů (zastávkový JŘ)",
        ElementTypes.NotesLegend   => "Legenda poznámek (zastávkový JŘ)",
        _ => type
    };

    private static string? ElementNote(string type) => type switch
    {
        ElementTypes.Table =>
            "Dva režimy: Fixed (pevné řádky v šabloně) a Dynamic (řádky z dat agendy). Sloupce a styl se nastavují v dialogu tabulky.",
        ElementTypes.StopList =>
            "Tabulka zastávek linky. Sloupce a jejich styl se nastavují ve vlastním dialogu sloupců.",
        ElementTypes.DepartureGrid =>
            "Mřížka minut odjezdů po skupinách dnů. Skupiny a sloupce mají vlastní dialog.",
        ElementTypes.NotesLegend =>
            "Legenda vysvětlivek/poznámek pod jízdním řádem.",
        _ => null
    };
}
