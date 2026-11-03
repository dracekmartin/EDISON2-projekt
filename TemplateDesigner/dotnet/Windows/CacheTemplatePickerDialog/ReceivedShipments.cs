using EDControls.EDViewModel;
using TemplateDesigner.Serialization;
using TemplateDesigner.Services;

namespace TemplateDesigner;

/// <summary>
/// Převzetí a odmítnutí zásilky šablon od jiné firmy (`B189`, R9). Zásilka se vyřizuje celá (Jan 3. 11.); šablona,
/// kterou převzít nejde, zastaví celou zásilku. U šablony, kterou firma od téže firmy už převzala, se uživatel
/// rozhodne, jestli ji nová verze přepíše, nebo se uloží jako další šablona.
/// </summary>
public sealed class ReceivedShipments(ITemplateSharing sharing, IPopupDisplayer popup)
{
    /// <summary>Převezme celou zásilku; true, když se šablony uložily.</summary>
    public async Task<bool> AcceptAsync(string shipmentId, string sender)
    {
        var templates = await sharing.GetShipmentTemplatesAsync(shipmentId);
        if (templates.Count == 0)
        {
            popup.Info("Zásilka neobsahuje žádnou šablonu. Odmítněte ji.");
            return false;
        }
        if (ReasonNotAccepted(templates, TemplateMigrator.CurrentVersion) is { } reason)
        {
            popup.Info(reason);
            return false;
        }

        var overwrite = new List<string>();
        foreach (var template in templates.Where(t => t.Existing is not null))
        {
            bool? choice = popup.ConfirmYesNoCancel(OverwriteQuestion(template, sender), "Šablona už je převzatá");
            if (choice is null) return false;
            if (choice == true) overwrite.Add(template.FileId);
        }

        var taken = await sharing.AcceptShipmentAsync(shipmentId, overwrite);
        popup.Info(AcceptedMessage(taken, templates));
        return true;
    }

    /// <summary>Odmítne celou zásilku po potvrzení; true, když se odmítla.</summary>
    public async Task<bool> RejectAsync(string shipmentId, string name, string sender)
    {
        if (!popup.Confirm($"Odmítnout zásilku „{name}“ od firmy {sender}? Šablony se nepřevezmou a firma uvidí, "
                           + "že jste zásilku odmítli."))
            return false;
        await sharing.RejectShipmentAsync(shipmentId);
        return true;
    }

    /// <summary>Proč zásilku převzít nejde (agenda, kterou firma nemá, nebo formát novější než návrhář); null = jde.</summary>
    internal static string? ReasonNotAccepted(IReadOnlyList<ReceivedTemplate> templates, string currentVersion)
    {
        if (templates.FirstOrDefault(t => !t.AgendaKnown) is { } unknown)
            return $"Zásilku nejde převzít: šablona „{unknown.Name}“ patří do agendy {unknown.AgendaCode}, kterou návrhář "
                   + "této firmy nemá. Zásilku můžete odmítnout.";
        if (templates.FirstOrDefault(t => VersionComparer.Instance.Compare(t.Version, currentVersion) > 0) is { } newer)
            return $"Zásilku nejde převzít: šablona „{newer.Name}“ je z novější verze návrháře. Aktualizujte návrhář "
                   + "a zásilku převezměte znovu.";
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

    /// <summary>Kde převzaté šablony najít.</summary>
    internal static string AcceptedMessage(IReadOnlyList<TakenTemplate> taken, IReadOnlyList<ReceivedTemplate> templates)
    {
        var agendas = string.Join(", ", templates.Select(t => t.AgendaTitle).Where(a => a != "").Distinct());
        return taken.Count == 1
            ? $"Šablona „{taken[0].Name}“ je převzatá. Najdete ji mezi vlastními šablonami v agendě {agendas}."
            : $"Převzato šablon: {taken.Count}. Najdete je mezi vlastními šablonami v agendách {agendas}.";
    }
}
