using System.Collections.Generic;

namespace EDISONMAIN2;

// Kódy, které legacy EDUsers.GetEdisonAccess (repo EDISON, EDUsers.vb) doplňuje k AP ze serveru - jen pravidla
// s cílem ve stromu. Zmizí-li pravidlo z legacy masteru, smaže se i tady (hlídá LegacyDerivedAccessTests); prázdná tabulka = smazat soubor.
internal static class LegacyDerivedAccess
{
    internal static readonly IReadOnlyDictionary<string, string[]> Rules = new Dictionary<string, string[]>
    {
        // JRMAIN → nové JŘ ISYJR2, dokud je převod práv ISYJR2 nezapíše do AP
        ["6001"] = ["276001", "276005", "60", "60.4", "60.1"],
        // AURIS → skupina AURIS, kterou AP z doby před jejím vznikem nemá
        ["38001"] = ["30"],
    };

    internal static void AddTo(HashSet<string> allowed)
    {
        foreach (var (trigger, derived) in Rules)
            if (allowed.Contains(trigger))
                allowed.UnionWith(derived);
    }
}
