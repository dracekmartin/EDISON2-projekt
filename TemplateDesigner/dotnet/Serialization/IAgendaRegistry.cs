namespace TemplateDesigner.Serialization;

/// <summary>
/// Jedna položka v registru agend — vrácená z Caché auto-discovery (`ListAgendy`) nebo
/// synteticky doplněná v .NET vrstvě (GENERAL).
///
/// <para><see cref="InstanceParamName"/> + <see cref="ElementKind"/> jsou capability metadata
/// (deklarovaná v Caché agenda class jako parametry) — okno je čte data-driven místo větvení
/// podle názvu agendy. Aditivní: starší Caché bez těchto klíčů → defaulty.</para>
/// </summary>
public record AgendaRegistryEntry(
    string Code,
    string Label,
    string CacheClass,
    string InstanceParamName = "INSTANCE_ID",
    string ElementKind = "",
    string InstanceLabel = "instance",
    string InstanceLabelPlural = "instance");

/// <summary>
/// Per-firma registr dostupných agend v TemplateDesigneru. Wave 2 nahradil
/// `appsettings.json` agendas[] auto-discovery z Caché `BaseAgenda` subclasses
/// (`ALVA.TemplateDesigner.API.AgendyRegistr.ListAgendy`).
///
/// GENERAL agenda (šablona bez DB vazby) je injektována syntheticky implementací
/// — nemá Caché class, je vždy dostupná.
/// </summary>
public interface IAgendaRegistry
{
    /// <summary>Vrátí dostupné agendy včetně GENERAL. Async — volá MLRest na Caché.</summary>
    Task<IReadOnlyList<AgendaRegistryEntry>> ListAsync();
}
