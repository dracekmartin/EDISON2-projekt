using System.Linq;
using EDControls;
using EDISONMAIN2.Windows.UserProfileWindow;
using NSubstitute;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Testy wire vrstvy okna „Vlastnosti uživatele" - parsování delimited object[] z
/// Main.LoadUser / Main.LoadSettings a odesílané payloady SaveUser / (Set|Get)UserDefaultForm.
/// </summary>
public class UserProfileServiceTests
{
    private const char D = '~';

    private static IMLRest Mock(object[]? loadUser = null, object[]? loadSettings = null,
        object[]? getDefaultWindow = null)
    {
        var ml = Substitute.For<IMLRest>();
        ml.Delimiter.Returns(D);
        ml.QueryOrThrow("ALVA.EDISON.Main", "LoadUser", Arg.Any<object[]>())
            .Returns(loadUser ?? new object[] { "" });
        ml.QueryOrThrow("ALVA.EDISON.Main", "LoadSettings", Arg.Any<object[]>())
            .Returns(loadSettings ?? new object[] { "" });
        ml.QueryOrThrow("ALVA.USERS.ApiEdison", "GetUserDefaultForm", Arg.Any<object[]>())
            .Returns(getDefaultWindow ?? new object[] { "", "0" });
        return ml;
    }

    private static object[] Rows(params string[] rows) =>
        new object[] { "" }.Concat(rows).ToArray();

    [Fact]
    public void Load_ParsesPersonDirFormAndCentralDir()
    {
        var ml = Mock(
            loadUser: Rows($"personDir{D}1{D}C:\\Users\\jan\\Export", $"email{D}jan@x.cz", $"form{D}17"),
            loadSettings: Rows($"system{D}database{D}CB_X{D}CB_X", $"system{D}export{D}\\\\srv\\central"));

        var p = UserProfileService.Load(ml);

        Assert.True(p.UsePersonDir);
        Assert.Equal("C:\\Users\\jan\\Export", p.PersonDir);
        Assert.Equal(17, p.DefaultWindowId);
        Assert.Equal("\\\\srv\\central", p.CentralExportDir);
    }

    // Příznak osobního adresáře: legacy ho ukládal i jako -1 (VB True) → nenulové = true.
    [Theory]
    [InlineData("-1", true)]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("", false)]
    public void Load_PersonDirFlag_NonZeroIsTrue(string flag, bool expected)
    {
        var ml = Mock(loadUser: Rows($"personDir{D}{flag}{D}C:\\x"));

        Assert.Equal(expected, UserProfileService.Load(ml).UsePersonDir);
    }

    [Fact]
    public void Load_NoFormRow_DefaultWindowIdZero()
    {
        // Server řádek „form" pošle jen když ≠ 0; jeho absence = žádný výchozí okno.
        var ml = Mock(loadUser: Rows($"personDir{D}0{D}"));

        Assert.Equal(0, UserProfileService.Load(ml).DefaultWindowId);
    }

    [Fact]
    public void Load_NoExportSetting_CentralDirEmpty()
    {
        var ml = Mock(loadSettings: Rows($"system{D}database{D}CB_X{D}CB_X"));

        Assert.Equal("", UserProfileService.Load(ml).CentralExportDir);
    }

    [Fact]
    public void SaveExportDir_SendsCanonicalRow()
    {
        var ml = Mock();

        UserProfileService.SaveExportDir(ml, usePersonDir: true, personDir: "C:\\Export");

        ml.Received().QueryOrThrow("ALVA.EDISON.Main", "SaveUser",
            Arg.Is<object[]>(a => a.Length == 1 && (string)a[0] == $"personDir{D}1{D}C:\\Export"));
    }

    [Fact]
    public void SaveExportDir_Unchecked_SendsZeroFlag()
    {
        var ml = Mock();

        UserProfileService.SaveExportDir(ml, usePersonDir: false, personDir: "");

        ml.Received().QueryOrThrow("ALVA.EDISON.Main", "SaveUser",
            Arg.Is<object[]>(a => (string)a[0] == $"personDir{D}0{D}"));
    }

    [Theory]
    [InlineData("17", 17)]
    [InlineData("0", 0)]
    [InlineData("", 0)]
    public void LoadDefaultWindow_ParsesOutput(string value, int expected)
    {
        var ml = Mock(getDefaultWindow: new object[] { "", value });

        Assert.Equal(expected, UserProfileService.LoadDefaultWindow(ml));
    }

    [Fact]
    public void LoadDefaultWindow_EmptyOutput_Zero()
    {
        var ml = Mock(getDefaultWindow: new object[] { "" });

        Assert.Equal(0, UserProfileService.LoadDefaultWindow(ml));
    }

    [Fact]
    public void SetDefaultWindow_SendsFormId()
    {
        var ml = Mock();

        UserProfileService.SetDefaultWindow(ml, 23);

        ml.Received().QueryOrThrow("ALVA.USERS.ApiEdison", "SetUserDefaultForm",
            Arg.Is<object[]>(a => a.Length == 1 && (int)a[0] == 23));
    }
}
