using System.Collections.Generic;

namespace EDISONMAIN2.Windows.SetupWindow
{
    /// <summary>Rozhodovací logika setup wizardu bez WPF - okno jen mapuje pole na volání.</summary>
    internal static class SetupWizardLogic
    {
        /// <summary>Validace připojení; null = OK, jinak hláška pro StatusText.</summary>
        internal static string? ValidateConnection(string server, string superPortText, string restPortText,
            out int superPort, out int restPort)
        {
            superPort = 0;
            restPort = 0;
            if (server.Length == 0)
                return "Zadejte server.";
            if (!int.TryParse(superPortText, out superPort) || superPort <= 0)
                return "Zadejte platný port.";
            if (!int.TryParse(restPortText, out restPort) || restPort <= 0)
                return "Zadejte platný REST port.";
            return null;
        }

        /// <summary>
        /// Výběr v combu po přenačtení seznamu databází: zachovej předchozí volbu, jediná
        /// databáze se vybere sama, jinak bez výběru (uživatel musí zvolit).
        /// </summary>
        internal static int PickDatabaseIndex(IReadOnlyList<string> databases, string? previousSelection)
        {
            if (previousSelection is not null)
            {
                for (int i = 0; i < databases.Count; i++)
                    if (databases[i] == previousSelection) return i;
            }
            return databases.Count == 1 ? 0 : -1;
        }

        /// <summary>Validace před uložením; null = OK, jinak hláška pro StatusText.</summary>
        internal static string? ValidateSave(string name, string? database, string idfa)
        {
            if (name.Trim().Length == 0) return "Zadejte název připojení.";
            if (string.IsNullOrEmpty(database)) return "Vyberte databázi.";
            if (idfa.Trim().Length == 0) return "Zadejte IDFa firmy.";
            return null;
        }
    }
}
