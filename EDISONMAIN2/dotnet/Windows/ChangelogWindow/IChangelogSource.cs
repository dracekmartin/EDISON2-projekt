using System.Threading.Tasks;

namespace EDISONMAIN2.Windows.ChangelogWindow
{
    /// <summary>Zdroj changelogu modulu. Dnes lokální .INF soubory vedle binárek, později web.</summary>
    public interface IChangelogSource
    {
        /// <summary>
        /// <paramref name="moduleKey"/> = název exe bez přípony ("ADSETTINGS", "MHD2PlanDopravy")
        /// nebo "EDISONMAIN" pro shell. Vrací <c>null</c>, když pro modul žádný changelog neexistuje.
        /// </summary>
        Task<ChangelogContent?> LoadAsync(string moduleKey);
    }

    /// <summary>
    /// Obsah changelogu jednoho balíku agend.
    /// </summary>
    /// <param name="ChangeSetKey">Identita balíku = basename INF ("AD", "MHD", "EDISONMAIN");
    /// kolabuje aliasy agend na jeden soubor, klíčuje snooze i session dedup.</param>
    /// <param name="ChangesText">Změny od poslední instalace (_DIFF.INF); <c>null</c> = žádné
    /// nové změny, auto-popup se nezobrazí.</param>
    /// <param name="FullText">Celá historie změn (.INF).</param>
    /// <param name="ChangeSetId">Identita change-setu = SHA-256 hex dekódovaného
    /// <paramref name="ChangesText"/>; "" když je <c>null</c>. Záměrně hash obsahu, ne verze
    /// EDISONu: Caché-only patch bumpne verzi, ale stale _DIFF.INF nechá na disku - verzový
    /// klíč by snooze zbytečně expiroval (a DEBUG verze je konstantní 999999.9).</param>
    public record ChangelogContent(string ChangeSetKey, string? ChangesText, string FullText, string ChangeSetId);
}
