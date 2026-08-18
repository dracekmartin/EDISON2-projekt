namespace TemplateDesigner.Serialization;

/// <summary>
/// Jedna položka v registru agend - vrácená z Caché auto-discovery (`ListAgendy`) nebo
/// synteticky doplněná v .NET vrstvě (GENERAL).
///
/// <para><b>Dvouosý model (2026-07-11):</b> položka = konfigurace <b>agenda × varianta</b>.
/// <see cref="Agenda"/>/<see cref="AgendaLabel"/> = nejvyšší úroveň členění („Zastávkový JŘ",
/// „Faktury"), <see cref="Variant"/>/<see cref="VariantLabel"/> = datový zdroj („AD"/„MHD",
/// prázdné = bez variant). <see cref="Code"/> zůstává stabilní kombinovaný persistovaný klíč
/// (hodnoty jako "ZASTAVKOVY_JR_AD" se nemění - žijí v šablonách, DB indexu, preferencích
/// a EDISON launch kontraktu). <see cref="Label"/> = kombinovaný label pro zpětnou kompatibilitu.</para>
///
/// <para><see cref="InstanceParamName"/> + <see cref="ElementKind"/> jsou capability metadata
/// (deklarovaná v Caché agenda class jako parametry) - okno je čte data-driven místo větvení
/// podle názvu agendy. Aditivní: starší Caché bez těchto klíčů → defaulty.</para>
///
/// <para><see cref="Released"/> = zveřejněná pro uživatele; nezveřejněné (pilotní) agendy se
/// nenabízejí v UI výběru, ale zůstávají funkční pro existující šablony a EDISON launch.
/// Default true = starší Caché bez klíče ukáže vše (dnešní chování).</para>
/// </summary>
public record AgendaRegistryEntry(
    string Code,
    string Label,
    string CacheClass,
    string InstanceParamName = "INSTANCE_ID",
    string ElementKind = "",
    string InstanceLabel = "instance",
    string InstanceLabelPlural = "instance",
    string Agenda = "",
    string AgendaLabel = "",
    string Variant = "",
    string VariantLabel = "",
    bool Released = true)
{
    /// <summary>Label agendy s fallbackem pro starší Caché bez dvouosých klíčů (→ kombinovaný Label).</summary>
    public string EffectiveAgendaLabel => AgendaLabel is { Length: > 0 } ? AgendaLabel : Label;

    /// <summary>Zobrazovací text konfigurace: „Zastávkový JŘ · AD", bez varianty jen label agendy.</summary>
    public string DisplayName => Variant is { Length: > 0 }
        ? $"{EffectiveAgendaLabel} · {Variant}"
        : EffectiveAgendaLabel;
}

/// <summary>
/// Per-firma registr dostupných agend v TemplateDesigneru. Od 5/2026 nahrazuje
/// `appsettings.json` agendas[] auto-discovery z Caché `BaseAgenda` subclasses
/// (`ALVA.TemplateDesigner.API.AgendyRegistr.ListAgendy`).
///
/// GENERAL agenda (šablona bez DB vazby) je injektována syntheticky implementací
/// - nemá Caché class, je vždy dostupná.
/// </summary>
public interface IAgendaRegistry
{
    /// <summary>Vrátí dostupné agendy včetně GENERAL. Async - volá MLRest na Caché.</summary>
    Task<IReadOnlyList<AgendaRegistryEntry>> ListAsync();
}
