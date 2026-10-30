using TemplateDesigner.Models;
using TemplateDesigner.Services;

namespace TemplateDesigner.Serialization;

/// <summary>
/// Kontrola šablony zvenku (ze souboru, z AI asistenta, z importu PDF), než nahradí otevřenou
/// šablonu. Návrhář ji přijme, jen když agendu šablony zná, uživatel ji smí použít a prvky jízdních
/// řádů v ní patří k té agendě. Agendu přepne podle šablony, stejně jako otevření z databáze.
/// </summary>
/// <remarks>
/// Do 30. 10. se načetla šablona s neznámou agendou i s tabulkou spojů v zastávkovém JŘ a zůstalo
/// jen upozornění ve stavovém řádku. Šablony z databáze se nekontrolují (rozhodnutí Jan 30. 10.).
/// Tvar šablony hlídá <see cref="TemplateSerializer.LoadAsync"/>.
/// </remarks>
public static class TemplateImportCheck
{
    /// <summary>Proč šablonu nejde přijmout, srozumitelně pro uživatele; null = v pořádku.</summary>
    /// <param name="aktualniAgenda">Agenda otevřené šablony - nezveřejněnou agendu, ve které uživatel
    /// pracuje, nabízí i okno Změnit agendu.</param>
    /// <param name="skryteAgendy">Vývojář vidí i nezveřejněné agendy (<c>dev.hiddenAgendas</c>).</param>
    public static string? ProcNelzePrijmout(Template sablona, IReadOnlyList<AgendaRegistryEntry> agendy,
        string? aktualniAgenda, bool skryteAgendy)
    {
        // Ze souboru to hlídá už načtení, šablonu od AI asistenta ne.
        if (sablona.Sections.Count == 0)
            return "Šablona nemá žádnou sekci a nejde ji načíst.";
        // Obecnou agendu doplňuje návrhář vždy, agendy se třídou na serveru jen s načteným seznamem.
        if (!agendy.Any(a => a.CacheClass.Length > 0))
            return "Seznam agend se nepodařilo načíst ze serveru, takže šablonu teď nejde ověřit. "
                + "Zkuste import znovu, až bude server dostupný.";

        var kod = string.IsNullOrEmpty(sablona.AgendaCode) ? CacheTemplateService.GeneralAgendaCode : sablona.AgendaCode;
        var agenda = agendy.FirstOrDefault(a => a.Code.Equals(kod, StringComparison.OrdinalIgnoreCase));
        if (agenda is null)
            return $"Šablona je pro agendu „{kod}“, kterou návrhář nezná, a nejde ji načíst.";
        if (!agenda.Released && !skryteAgendy && !kod.Equals(aktualniAgenda, StringComparison.OrdinalIgnoreCase))
            return $"Šablona je pro agendu „{agenda.DisplayName}“, která se zatím nenabízí, a nejde ji načíst.";

        var cizi = sablona.Sections
            .SelectMany(s => s.Elements)
            .Where(p => !AgendaCapabilities.Allows(agenda.ElementKind, p))
            .Select(ElementNames.ForUser)
            .Distinct()
            .ToList();
        return cizi.Count > 0
            ? $"Šablona agendy „{agenda.DisplayName}“ obsahuje prvky jiné agendy ({string.Join(", ", cizi)}) a nejde ji načíst."
            : null;
    }
}
