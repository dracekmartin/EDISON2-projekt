using EDControls;
using TemplateDesigner.Config;

namespace TemplateDesigner.Tests.Services;

/// <summary>
/// Převzetí session shellu z EDISONMAIN2 launch kontraktu (HD 13132/F17). Neprázdný ShellGuid
/// = sdílená shell session: TD musí jet pod shell GUID s CacheLocks=1, jinak jeho requesty
/// nedrží ACTIVITY historii shellu a idle timeout odhlásí uživatele uprostřed práce.
/// Prázdný guid (starý shell, standalone) musí zůstat u vlastního GUID bez kontrol -
/// CacheLocks=1 bez vlastního ACTIVITY zámku by padal na LockExist na každém requestu.
/// </summary>
public class CacheSchemaWiringLaunchTests
{
    private static EdisonLaunchArgs Launch(string shellGuid) => new(
        Server: "server", RestPort: 57772, Namespace: "ALVA", Idfa: "CK_TEST",
        LoginName: "novak", FullName: "Jan Novák", Mlp: "42",
        FormName: "", ShellGuid: shellGuid, InputParam: "");

    [Fact]
    public void NeprazdnyShellGuid_PrevezmeGuidAZapneCacheLocks()
    {
        var mlRest = CacheSchemaWiring.CreateMlRest(Launch("shell-guid-123"));

        Assert.Equal("shell-guid-123", mlRest.GUID);
        Assert.Equal(1, mlRest.CacheLocks);
    }

    [Fact]
    public void PrazdnyShellGuid_VlastniSessionBezKontrol()
    {
        var mlRest = CacheSchemaWiring.CreateMlRest(Launch(""));

        Assert.NotEmpty(mlRest.GUID);
        Assert.NotEqual("shell-guid-123", mlRest.GUID);
        Assert.Equal(0, mlRest.CacheLocks);
    }

    [Fact]
    public void SessionKontextSePrebiraVzdy()
    {
        var mlRest = CacheSchemaWiring.CreateMlRest(Launch(""));

        Assert.Equal("CK_TEST", mlRest.Idfa);
        Assert.Equal("novak", mlRest.LoginName);
        Assert.Equal("Jan Novák", mlRest.FullName);
        Assert.Equal("42", mlRest.Mlp);
    }
}
