using System;
using EDControls.EDViewModel;

namespace EDISONMAIN2;

/// <summary>Reakce stanice na výsledek <see cref="Updater.Check"/>, společná pro start i ruční kontrolu z Nástrojů.</summary>
internal static class StationUpdate
{
    /// <returns>true = aplikace má skončit (spuštěný updater stanice nebo rozbitá instalace).</returns>
    internal static bool Apply(Updater updater, UpdateSettings settings, UpdateCheckResult result, IPopupDisplayer popup)
    {
        switch (result.Outcome)
        {
            case UpdateOutcome.UpdateRequired:
                popup.Info(
                    $"K dispozici je nová verze programu [{result.Version}]. Aplikace se nyní zaktualizuje a bude ukončena.");
                try
                {
                    updater.CopyUpdater(settings.SourceDir);
                    updater.LaunchUpdate(settings.SourceDir, result.Version, result.Forced, settings.ForcedStamp);
                }
                catch (Exception ex)
                {
                    popup.Info($"Aktualizaci nelze spustit ({ex.Message}). Kontaktujte Vašeho správce IS.");
                }
                return true;
            case UpdateOutcome.Broken:
                popup.Info(result.Message);
                return true;
            default:
                return false;
        }
    }
}
