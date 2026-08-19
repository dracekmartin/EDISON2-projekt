using System.Text.Json.Serialization;

namespace TemplateDesigner.Models;

public class Template
{
    /// <summary>
    /// Schema verze formátu Template JSON (semver, např. "1.0.0").
    /// Při breaking change v JSON schema → bumpni verzi + přidej migraci do <c>TemplateMigrator</c>.
    /// TemplateMigrator čte verzi při loadu a aplikuje pipeline migrací do aktuální verze.
    /// Detail convention: <c>docs/template-schema-versions.md</c>.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// True pokud byla šablona auto-migrována při loadu (verze v souboru byla starší než aktuální).
    /// Transient - neserializuje se. UI vrstva (status bar) podle toho zobrazí warning.
    /// </summary>
    [JsonIgnore]
    public bool WasMigrated { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Nová šablona";
    public string Description { get; set; } = "";

    public PageSize PageSize { get; set; } = PageSize.A4Portrait;
    public List<Section> Sections { get; set; } = [];

    /// <summary>
    /// Kombinovaný kód konfigurace agenda × varianta, např. "ZASTAVKOVY_JR_AD", "FAKTURY".
    /// Null = obecná šablona bez vazby na data. Stabilní persistovaný klíč (JSON `agendaCode`,
    /// DB index ByAgenda, per-agenda preference, EDISON launch `Agenda=&lt;kód&gt;`) - hodnoty se
    /// nemění. Odpovídá Caché parametru <c>AgendaVariantCode</c>; osy Agenda/Variant a labely
    /// („Zastávkový JŘ · AD") se dohledávají z registru agend (<c>AgendaRegistryEntry</c>).
    /// </summary>
    public string? AgendaCode { get; set; }

    /// <summary>
    /// Vzorová data pro preview uložená spolu se šablonou.
    /// Klíč = název pole, hodnota = ukázková hodnota jako string.
    /// </summary>
    public Dictionary<string, string>? SampleData { get; set; }

    /// <summary>
    /// Parametry sestavy - renderovací rozhodnutí vystavená uživateli před tiskem.
    /// Null / prázdný seznam = šablona nemá parametry.
    /// Připraveno pro budoucí offline podporu (bez Caché) a pro přepsání Caché defaultů per šablona.
    /// Aktuálně se parametry načítají pouze z Caché AgendaAPI.GetParameters().
    /// </summary>
    public List<TemplateParameter>? Parameters { get; set; }

    /// <summary>
    /// Hodnoty obsahových tiskových parametrů uložené pro tuto šablonu (klíč = název parametru,
    /// hodnota = string reprezentace). Sdílené mezi uživateli (jsou součástí šablony) a mají přednost
    /// před per-agenda „naposledy použité". Null = šablona vlastní hodnoty nemá → fallback na agendu.
    /// Exportní pole (název souboru, členění, imposice) sem NEpatří - ta zůstávají per agenda.
    /// Aditivní pole - staré šablony ho nemají (žádná migrace, fallback na agendu).
    /// </summary>
    public Dictionary<string, string>? PrintParameters { get; set; }

    /// <summary>
    /// Kód kultury pro formátování čísel a datumů, např. "cs-CZ", "en-US".
    /// Null = výchozí "cs-CZ".
    /// </summary>
    public string? CultureCode { get; set; }

    /// <summary>
    /// Výchozí styl textu pro celou šablonu - kořen kaskády (dědí ho všechny elementy/buňky,
    /// které styl nepřepíší). Null = zapečené defaulty (Arial). Aktuálně se z UI nastavuje jen
    /// font (rodina); ostatní property jsou připravené pro budoucí rozšíření (velikost, barva…).
    /// Aplikuje se přes <see cref="DocumentDefaults"/> jako ambient při změně šablony.
    /// </summary>
    public TextStyle? DefaultTextStyle { get; set; }

    /// <summary>
    /// Designer kontext (grid, snap) - zachová se prostředí, ve kterém byla šablona laděná.
    /// Null = applikace použije globální default při otevření.
    /// </summary>
    public TemplateEditorPreferences? EditorPreferences { get; set; }

    /// <summary>
    /// Odkud šablona přišla, když ji doručil distribuční kanál M-line. Cestuje **uvnitř šablony**,
    /// takže se uloží do databáze firmy s ní - stav „co už máme" tím patří firmě, ne počítači
    /// uživatele (jinak by si každý uživatel stahoval totéž znovu). Null = šablona z kanálu nepřišla.
    /// </summary>
    public TemplateChannelOrigin? ChannelOrigin { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Co se schová u spoje v rozsahu „Spoje, nepoznámkovat" (zastávkový JŘ AD).
/// </summary>
/// <summary>
/// Záznam kanálu, ze kterého šablona přišla. <paramref name="Id"/> je identifikátor v úložišti
/// na helpdesku (adresa), <paramref name="UpdatedAt"/> jeho čas poslední změny - podle něj se pozná,
/// že je k dispozici novější verze. Identitu šablony určuje <see cref="Template.Id"/>, ne tenhle
/// klíč: díky tomu je opakované stažení idempotentní a nevzniknou duplicity.
/// </summary>
public record TemplateChannelOrigin(string Id, DateTime UpdatedAt);

public enum NoteRangeSuppression
{
    /// <summary>Schovat ruční komentáře spoje (výchozí = legacy chování).</summary>
    ManualComment,

    /// <summary>Schovat automatické poznámky o rozdílech tras (zůstane jen ruční komentář) - #7955.</summary>
    AutoRouteNote,

    /// <summary>Schovat ruční komentáře i automatické rozdíly tras.</summary>
    Both,
}
