using System;
using System.Diagnostics;
using System.Threading.Tasks;
using ALVA.LICENCE.EDLicence;
using EDControls;
using EDControls.EDViewModel;

namespace EDISONMAIN2.Windows.EdisonMainWindow
{
    /// <summary>
    /// Licence session: chip upozornění (nástěnka starého shellu), stažení licence z M-line po
    /// přihlášení a ruční „Aktualizovat licenci". Novou licenci načte <c>reloadLicence</c>,
    /// přestavbu session dělá VM přes <c>rebuild</c>.
    /// </summary>
    internal sealed class SessionLicenceCoordinator(
        IRestCallRunner runner,
        Func<IMLRest, LicenceService.MlineSyncResult> licenceSync,
        SessionEpoch session,
        Func<Task> rebuild,
        Action reloadLicence,
        Func<bool> syncsAfterLogin,
        Action noticeChanged)
    {
        /// <summary>Chip upozornění licence a verze; null = skrytý.</summary>
        internal LicenceNotice.Notice? Notice { get; private set; }

        /// <summary>
        /// Upozornění pro licenci právě přestavěné session. Počty jsou requesty do agendových balíků
        /// (VOZIDLA, MZDY2), proto na pozadí; do té doby chip ukáže jen datum a verzi.
        /// </summary>
        internal async Task RefreshNoticeAsync(Lic? licence)
        {
            var token = session.Capture();
            SetNotice(LicenceNotice.Build(licence, DateTime.Today, VerzeInf.Current, null, null));
            if (licence == null || (licence.CountsRZ <= 0 && licence.CountsZAM <= 0)) return;
            try
            {
                var vehicles = licence.CountsRZ > 0 ? runner.RunAsync(LicenceNotice.LoadVehicleCount) : Task.FromResult<int?>(null);
                var employees = licence.CountsZAM > 0 ? runner.RunAsync(LicenceNotice.LoadEmployeeCount) : Task.FromResult<int?>(null);
                await Task.WhenAll(vehicles, employees);
                if (!session.IsCurrent(token)) return;
                SetNotice(LicenceNotice.Build(licence, DateTime.Today, VerzeInf.Current, vehicles.Result, employees.Result));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Licence notice counts failed: {ex.Message}");
            }
        }

        private void SetNotice(LicenceNotice.Notice? notice)
        {
            if (notice == Notice) return;
            Notice = notice;
            noticeChanged();
        }

        /// <summary>
        /// Parita s legacy <c>EDUsers.Login</c> (<c>EDUsers.vb:170</c>): admin a uživatel s právem UpdateEdison
        /// stahují při přihlášení licenci z M-line a posílají feedback. Na pozadí, spojení do M-line bývá
        /// pomalé a start na něj nesmí čekat; nová licence se promítne tichou přestavbou session.
        /// </summary>
        internal async Task SyncAfterLoginAsync()
        {
            if (!syncsAfterLogin()) return;
            var token = session.Capture();
            try
            {
                var sync = await runner.RunAsync(licenceSync);
                if (sync.Error.Length > 0) Debug.WriteLine($"M-line licence sync: {sync.Error}");
                if (!sync.Changed || !session.IsCurrent(token)) return;
                reloadLicence();
                await rebuild();
            }
            catch (Exception ex)
            {
                // Legacy výsledek také nekontroloval; M-line vývojářům chybu ukáže sám EDUsers.
                Debug.WriteLine($"M-line licence sync failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Parita s legacy „Aktualizovat licenci" (<c>frmNoMDI.vb:3498-3505</c>): stažení z M-line smí každý
        /// uživatel. Přestaví i při chybě EDUsers - licence mohla být uložena a selhat až feedback; chybu
        /// vrací ve výsledku, ukazuje ji volající až po přestavbě.
        /// </summary>
        internal async Task<LicenceService.MlineSyncResult> RefreshAsync(Func<IDisposable> busy)
        {
            session.Invalidate();
            LicenceService.MlineSyncResult sync;
            using (busy())
                sync = await runner.RunAsync(licenceSync);
            reloadLicence();
            await rebuild();
            return sync;
        }
    }
}
