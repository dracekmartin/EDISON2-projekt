using System.Collections.Generic;
using System.Globalization;
using ALVA.LICENCE.EDLicence;
using EDControls;
using EDControls.EDViewModel;

namespace EDISONMAIN2;

/// <summary>
/// Načte aplikační přístup uživatele přes <c>ALVA.SYS.Login.GetEdisonAccess</c>
/// (per user × IdFa). Aplikuje admin fallback (vždy SpolecnaEvidence,
/// formEDISONMAINSettingsShow; pokud licence Type ≠ "Nezadána", navíc formUSERSMain +
/// formTMList) podle legacy <c>frmMain.AddAdminRights</c>.
/// </summary>
public static class AccessLoader
{
    /// <summary>
    /// apId=3 (server-side APVse / SuperUser) - admin má všechen přístup,
    /// klient si nemusí enumerovat vyčísleně, stačí flag v <see cref="AccessTable"/>.
    /// </summary>
    private const int ApIdAdmin = 3;

    // EDFormInterfaceLic.vb konstanty potřebné pro admin fallback (internal - sdílí je
    // fallback licence v LicenceService):
    internal const string SpolecnaEvidence = "0";          // EDMainPackageEnum.SpolecnaEvidence
    internal const string FormEdisonmainSettingsShow = "2002"; // EDPackageEnum.EDISONMAIN(2000)+2
    private const string FormUsersMain = "5001";          // EDPackageEnum.USERS(5000)+1
    private const string FormTmList = "40001";            // EDPackageEnum.TM(40000)+1

    public static AccessTable Load(IMLRest mlRest, Lic? licence, bool isAdmin)
    {
        // Wire formát: numeric subscripty (legacy object[] pattern).
        // output[1] = apId, output[2..n] = povolené element IDs (string).
        var output = mlRest.QueryOrThrow("ALVA.SYS.Login", "GetEdisonAccess", new object[] { });
        int apId = int.Parse(output[1]?.ToString() ?? "0", CultureInfo.InvariantCulture);

        var allowed = new HashSet<string>();
        bool isAllAccess = apId == ApIdAdmin;

        if (!isAllAccess)
        {
            for (int i = 2; i <= output.GetUpperBound(0); i++)
            {
                var id = output[i]?.ToString();
                if (!string.IsNullOrEmpty(id)) allowed.Add(id);
            }
        }

        // Admin fallback - viz frmMain.AddAdminRights. Aplikuje se i pokud
        // user není apId=3, ale je IsAdmin (např. lokálně označený admin jinou cestou).
        // Pravidlo: doplňuje pouze accessTable, NIKDY licence (admin nevidí moduly
        // mimo licenci firmy - jen se neuzamkne sám sobě přístup k user mgmt).
        if (isAdmin && !isAllAccess)
        {
            allowed.Add(SpolecnaEvidence);
            allowed.Add(FormEdisonmainSettingsShow);
            if (licence != null && licence.Type != "Nezadána")
            {
                allowed.Add(FormUsersMain);
                allowed.Add(FormTmList);
            }
        }

        return new AccessTable(allowed, isAllAccess);
    }
}
