using EDControls.EDViewModel;
using TemplateDesigner.Serialization;
using TemplateDesigner.Services;

namespace TemplateDesigner;

/// <summary>Zásilka v Přijatých šablonách: id, název a firma, která ji poslala.</summary>
public sealed record Shipment(string Id, string Name, string Sender);

/// <summary>
/// Převzetí a odmítnutí zásilek šablon od jiných firem (`B189`, R9). Zásilka se vyřizuje celá (Jan 3. 11.), víc
/// zaškrtnutých zásilek naráz; šablona, kterou převzít nejde, zastaví jen svou zásilku. U šablony, kterou firma od téže
/// firmy už převzala, se uživatel rozhodne, jestli ji nová verze přepíše, nebo se uloží jako další šablona.
/// </summary>
public sealed class ReceivedShipments(ITemplateSharing sharing, IPopupDisplayer popup)
{
    /// <summary>
    /// Převezme zásilky jednu po druhé a nakonec jednou zprávou řekne, co se převzalo a co ne; true, když se aspoň jedna
    /// převzala. Po sobě, ne napřed všechny otázky: obsah další zásilky se načte až po převzetí předchozí, takže dvě
    /// verze téže šablony v různých zásilkách nezaloží dvě nové šablony. Storno zastaví zbylé zásilky.
    /// </summary>
    public async Task<bool> AcceptAsync(IReadOnlyList<Shipment> shipments)
    {
        var taken = new List<TakenTemplate>();
        var agendas = new List<string>();
        var problems = new List<string>();
        int accepted = 0;
        bool stopped = false;
        foreach (var shipment in shipments)
        {
            try
            {
                var templates = await sharing.GetShipmentTemplatesAsync(shipment.Id);
                if (ReasonNotAccepted(templates, TemplateMigrator.CurrentVersion) is { } reason)
                {
                    problems.Add($"Zásilku „{shipment.Name}“ nejde převzít: {reason}");
                    continue;
                }
                if (AskOverwrite(templates, shipment.Sender) is not { } overwrite)
                {
                    stopped = true;
                    break;
                }
                taken.AddRange(await sharing.AcceptShipmentAsync(shipment.Id, overwrite));
                agendas.AddRange(templates.Select(t => t.AgendaTitle));
                accepted++;
            }
            catch (CacheTemplateException ex)
            {
                problems.Add($"Zásilku „{shipment.Name}“ se nepodařilo převzít: {ex.Message}");
            }
        }
        if (Summary(taken, agendas, problems, stopped) is { } text) popup.Info(text);
        return accepted > 0;
    }

    /// <summary>Šablony, které nová verze přepíše; null = Storno.</summary>
    private List<string>? AskOverwrite(IReadOnlyList<ReceivedTemplate> templates, string sender)
    {
        var overwrite = new List<string>();
        foreach (var template in templates.Where(t => t.Existing is not null))
        {
            bool? choice = popup.ConfirmYesNoCancel(OverwriteQuestion(template, sender), "Šablona už je převzatá");
            if (choice is null) return null;
            if (choice == true) overwrite.Add(template.FileId);
        }
        return overwrite;
    }

    /// <summary>Odmítne zásilky po jednom potvrzení; true, když se aspoň jedna odmítla.</summary>
    public async Task<bool> RejectAsync(IReadOnlyList<Shipment> shipments)
    {
        if (shipments.Count == 0 || !popup.Confirm(RejectQuestion(shipments))) return false;
        var problems = new List<string>();
        int rejected = 0;
        foreach (var shipment in shipments)
        {
            try
            {
                await sharing.RejectShipmentAsync(shipment.Id);
                rejected++;
            }
            catch (CacheTemplateException ex)
            {
                problems.Add($"Zásilku „{shipment.Name}“ se nepodařilo odmítnout: {ex.Message}");
            }
        }
        if (problems.Count > 0) popup.Info(string.Join("\n\n", problems));
        return rejected > 0;
    }

    internal static string RejectQuestion(IReadOnlyList<Shipment> shipments) =>
        shipments is [var one]
            ? $"Odmítnout zásilku „{one.Name}“ od firmy {one.Sender}? Šablony se nepřevezmou a firma uvidí, "
              + "že jste zásilku odmítli."
            : $"Odmítnout {ReceivedSummary.Pocet(shipments.Count, "zásilku", "zásilky", "zásilek")} "
              + $"({string.Join(", ", shipments.Select(s => $"„{s.Name}“"))})? Šablony se nepřevezmou a firmy uvidí, "
              + "že jste zásilky odmítli.";

    /// <summary>Proč zásilku převzít nejde (prázdná, agenda, kterou firma nemá, nebo formát novější než návrhář);
    /// null = jde. Text navazuje na „Zásilku … nejde převzít:".</summary>
    internal static string? ReasonNotAccepted(IReadOnlyList<ReceivedTemplate> templates, string currentVersion)
    {
        if (templates.Count == 0)
            return "neobsahuje žádnou šablonu. Zásilku můžete odmítnout.";
        if (templates.FirstOrDefault(t => !t.AgendaKnown) is { } unknown)
            return $"šablona „{unknown.Name}“ patří do agendy {unknown.AgendaCode}, kterou návrhář této firmy nemá. "
                   + "Zásilku můžete odmítnout.";
        if (templates.FirstOrDefault(t => VersionComparer.Instance.Compare(t.Version, currentVersion) > 0) is { } newer)
            return $"šablona „{newer.Name}“ je z novější verze návrháře. Aktualizujte návrhář a zásilku převezměte znovu.";
        return null;
    }

    /// <summary>Otázka u šablony, kterou firma už převzala; dialog má jen Ano, Ne a Storno, proto je volba ve větě.</summary>
    internal static string OverwriteQuestion(ReceivedTemplate template, string sender) =>
        template.Existing is { Modified: true } existing
            ? $"Šablonu „{existing.Name}“ jste od firmy {sender} už převzali a od té doby ji upravili.\n\n"
              + "Ano - přepsat ji novou verzí, vaše úpravy se ztratí.\n"
              + "Ne - uložit novou verzi jako další šablonu, vaše šablona zůstane, jak je."
            : $"Šablonu „{template.Existing?.Name}“ jste od firmy {sender} už převzali.\n\n"
              + "Ano - nahradit ji novou verzí.\n"
              + "Ne - uložit novou verzi jako další šablonu.";

    /// <summary>Zpráva po převzetí: kde převzaté šablony najít a které zásilky převzít nešly; null = nic se nestalo
    /// (Storno hned u první zásilky).</summary>
    internal static string? Summary(IReadOnlyList<TakenTemplate> taken, IReadOnlyList<string> agendas,
        IReadOnlyList<string> problems, bool stopped)
    {
        var parts = new List<string>();
        if (taken.Count > 0) parts.Add(AcceptedMessage(taken, agendas));
        parts.AddRange(problems);
        if (parts.Count == 0) return null;
        if (stopped) parts.Add("Ostatní zůstaly mezi přijatými šablonami.");
        return string.Join("\n\n", parts);
    }

    /// <summary>Kde převzaté šablony najít.</summary>
    internal static string AcceptedMessage(IReadOnlyList<TakenTemplate> taken, IReadOnlyList<string> agendas)
    {
        var distinct = agendas.Where(a => a != "").Distinct().ToList();
        var where = distinct.Count switch
        {
            0 => "ve své agendě",
            1 => $"v agendě {distinct[0]}",
            _ => $"v agendách {string.Join(", ", distinct)}",
        };
        return taken.Count == 1
            ? $"Šablona „{taken[0].Name}“ je převzatá. Najdete ji mezi vlastními šablonami {where}."
            : $"Převzato šablon: {taken.Count}. Najdete je mezi vlastními šablonami {where}.";
    }
}
