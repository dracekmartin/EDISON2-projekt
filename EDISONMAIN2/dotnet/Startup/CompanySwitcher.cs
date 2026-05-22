using System;
using System.Collections.Generic;
using System.Linq;
using ALVA.LICENCE.EDLicence;
using EDControls;
using EDControls.EDViewModel;
using static ALVA.EDISONAPI.Database;

namespace EDISONMAIN2;

/// <summary>
/// Výsledek pokusu o přepnutí firmy. <see cref="LicenceAcquireOutcome.Accepted"/> značí, že
/// MLRest má novou Idfa+Namespace a <see cref="Licence"/> je platná pro cílovou firmu.
/// Jinak rollback proběhl a <see cref="Licence"/> je původní hodnota.
/// </summary>
public record SwitchOutcome(LicenceAcquireOutcome Outcome, Lic? Licence);

/// <summary>
/// Sdílená logika pro výběr a přepnutí firmy. <see cref="PickInitial"/> řeší startup
/// (App.xaml.cs), <see cref="Switch"/> runtime přepnutí ve VM. Drift mezi oběma cestami
/// (priorita firmy, ACTIVITY zámek, licenční retry, rollback) byl historicky zdrojem
/// bugů — proto společný service.
/// </summary>
public class CompanySwitcher(
    IMLRest mlRest,
    Func<Lic?> licenceLoader,
    bool isAdmin,
    IPopupDisplayer popup,
    IWindowNavigator windowNavigator,
    Action closeAllWindows)
{
    /// <summary>
    /// Vybere výchozí firmu pro startup: priorita <paramref name="preferredIdfa"/> →
    /// <c>IsDefault</c> flag → <c>companies[0]</c>. Stateless, volá se v App.xaml.cs
    /// před vytvořením MLRest connection.
    /// </summary>
    public static CompanyStruct PickInitial(IReadOnlyList<CompanyStruct> companies, string? preferredIdfa)
    {
        var match = companies.FirstOrDefault(c => c.Id == preferredIdfa);
        if (match.Id == null) match = companies.FirstOrDefault(c => c.IsDefault);
        if (match.Id == null) match = companies[0];
        return match;
    }

    /// <summary>
    /// Přepne firmu: zavře všechna agendová okna, vymění Idfa+Namespace v MLRest a získá
    /// licenci přes <see cref="LicenceVerifier"/> (s admin USERS.exe retry). Při neúspěchu
    /// rollbackuje MLRest na původní hodnoty. <see cref="LocalPrefs.LastIdfa"/> aktualizuje
    /// při <see cref="LicenceAcquireOutcome.Accepted"/>. Confirm popup je odpovědnost
    /// callsite (VM ho zobrazí před voláním).
    /// </summary>
    /// <param name="newIdfa">Cílová Idfa.</param>
    /// <param name="newNamespace">Cílový DataNamespace.</param>
    /// <param name="currentLicence">Aktuální licence; null = bez verify (testy / pre-licence stav).</param>
    public SwitchOutcome Switch(string newIdfa, string newNamespace, Lic? currentLicence)
    {
        var originalIdfa = mlRest.Idfa;
        var originalNamespace = mlRest.DataNamespace;

        closeAllWindows();
        mlRest.Idfa = newIdfa;
        mlRest.DataNamespace = newNamespace;

        // ACTIVITY zámek + počet licencí jsou per-IdFa — pro VerifyLic potřebujeme
        // licenci cílové firmy, ne původní (jinak bychom kontrolovali špatný limit).
        // Při překročení licencí + admin nabídne LicenceVerifier okno aktivity uživatelů.
        // Null currentLicence (typicky testy) → skip verify, switch je auto-accept.
        Lic? newLicence = currentLicence;
        var outcome = LicenceAcquireOutcome.Accepted;
        if (currentLicence != null)
            (outcome, newLicence) = LicenceVerifier.TryAcquireSession(
                mlRest, licenceLoader, isAdmin, popup, windowNavigator);

        if (outcome == LicenceAcquireOutcome.Accepted)
        {
            LocalPrefs.Current.LastIdfa = newIdfa;
            LocalPrefs.Current.Save();
            return new SwitchOutcome(outcome, newLicence);
        }

        mlRest.Idfa = originalIdfa;
        mlRest.DataNamespace = originalNamespace;
        return new SwitchOutcome(outcome, newLicence);
    }
}
