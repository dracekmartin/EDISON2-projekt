using System.Collections.Generic;
using System.Globalization;
using ALVA.LICENCE.EDLicence;
using EDControls;
using EDControls.EDViewModel;

namespace EDISONMAIN2;

/// <summary>
/// Načte aplikační přístup uživatele přes <c>ALVA.SYS.Login.GetEdisonAccess</c>
/// (per user × IdFa). Aplikuje legacy expanzní pravidla (např. element 38001 AURIS
/// → přidat parent FAMILY 30 + Partnerská řešení 24) a admin fallback
/// (vždy SpolecnaEvidence, formEDISONMAINSettingsShow; pokud licence Type ≠ "Nezadána",
/// navíc formUSERSMain + formTMList) tak jak to dělá legacy
/// <c>EDUsers.GetEdisonAccess</c> + <c>frmMain.AddAdminRights</c>.
/// </summary>
public static class AccessLoader
{
    /// <summary>
    /// apId=3 (server-side APVse / SuperUser) — admin má všechen přístup,
    /// klient si nemusí enumerovat vyčísleně, stačí flag v <see cref="AccessTable"/>.
    /// </summary>
    private const int ApIdAdmin = 3;

    // EDFormInterfaceLic.vb konstanty potřebné pro admin fallback:
    private const string SpolecnaEvidence = "0";          // EDMainPackageEnum.SpolecnaEvidence
    private const string FormEdisonmainSettingsShow = "2002"; // EDPackageEnum.EDISONMAIN(2000)+2
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
            ApplyLegacyExpansionRules(allowed, licence);
        }

        // Admin fallback — viz frmMain.AddAdminRights:765-775. Aplikuje se i pokud
        // user není apId=3, ale je IsAdmin (např. lokálně označený admin jinou cestou).
        // Pravidlo: doplňuje pouze accessTable, NIKDY licence (admin nevidí moduly
        // mimo licenci firmy — jen se neuzamkne sám sobě přístup k user mgmt).
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

    /// <summary>
    /// Legacy expanzní pravidla z <c>EDUsers.GetEdisonAccess</c>:554-600.
    /// Historické workarounds pro AP profily v DB — kdy přidělení leafu znamená
    /// automatický přístup k parent FAMILY a kategorii. 1:1 port aby chování
    /// EDISONMAIN2 odpovídalo legacy.
    /// </summary>
    private static void ApplyLegacyExpansionRules(HashSet<string> allowed, Lic? licence)
    {
        if (allowed.Contains("11001")) allowed.Add("35");          // Zákazníci alias
        if (allowed.Contains("34")) allowed.Add("1.1");            // E-služby
        AddPartnersFamily(allowed, "38001", "30", "24");           // AURIS → AURIS FAMILY + Partnerská řešení
        AddPartnersFamily(allowed, "58001", "17", "24");           // BCD Clearing
        AddPartnersFamily(allowed, "59001", "18", "24");           // BCD Standard
        AddPartnersFamily(allowed, "60001", "19", "24");           // BCD Optim1
        AddPartnersFamily(allowed, "61001", "20", "24");           // BCD Cena jízdného
        AddPartnersFamily(allowed, "79001", "27", "24");           // BCD Intenzita provozu
        AddPartnersFamily(allowed, "87001", "32", "24");           // FSSOFT
        AddPartnersFamily(allowed, "95001", "33", "24");           // CHAPS

        // Mini-aplikace (element 15 v licenci) → přidat všechny 52001-52999
        if (licence?.Elements != null && licence.Elements.Contains("15"))
        {
            allowed.Add("15");
            for (int i = 52001; i <= 52999; i++) allowed.Add(i.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static void AddPartnersFamily(HashSet<string> allowed, string trigger, string family, string parent)
    {
        if (!allowed.Contains(trigger)) return;
        allowed.Add(family);
        allowed.Add(parent);
    }
}
