namespace TemplateDesigner.Serialization;

/// <summary>
/// Jedna položka v registru agend — vrácená z Caché auto-discovery (`ListAgendy`) nebo
/// synteticky doplněná v .NET vrstvě (GENERAL).
/// </summary>
public record AgendaRegistryEntry(string Code, string Label, string CacheClass);

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
