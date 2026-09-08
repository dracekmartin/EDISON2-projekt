using EDISONMAIN2.Windows.LoginWindow;

namespace EDISONMAIN2.Tests;

public class LoginWindowLogicTests
{
    [Theory]
    [InlineData("jan", "tajne")]  // zapamatované heslo → převzetí jiným uživatelem
    [InlineData(null, "tajne")]
    public void PickInitialFocus_PrefilledPassword_FocusesUsername(string? username, string? password) =>
        Assert.Equal(LoginFocus.Username, LoginWindowLogic.PickInitialFocus(username, password));

    [Theory]
    [InlineData("jan", null)]     // jen jméno → rovnou na heslo
    [InlineData("jan", "")]
    public void PickInitialFocus_OnlyUsername_FocusesPassword(string? username, string? password) =>
        Assert.Equal(LoginFocus.Password, LoginWindowLogic.PickInitialFocus(username, password));

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    public void PickInitialFocus_NoPrefill_Default(string? username, string? password) =>
        Assert.Equal(LoginFocus.Default, LoginWindowLogic.PickInitialFocus(username, password));

    [Fact]
    public void ShouldForgetRememberedPassword_OnlyWhenRejectedPasswordIsThePrefilledOne()
    {
        // Odmítnuté předvyplněné heslo se zapomíná; ručně přepsané ne (to není zapamatované).
        Assert.True(LoginWindowLogic.ShouldForgetRememberedPassword("tajne", "tajne"));
        Assert.False(LoginWindowLogic.ShouldForgetRememberedPassword("tajne", "jine"));
        Assert.False(LoginWindowLogic.ShouldForgetRememberedPassword("", "tajne"));
    }

    [Theory]
    [InlineData("", "", "Zadejte nové heslo.")]
    [InlineData("", "neco", "Zadejte nové heslo.")]
    [InlineData("nove", "jine", "Nová hesla se neshodují.")]
    public void ValidateNewPassword_Invalid(string newPassword, string confirm, string expected)
    {
        Assert.Equal(expected, LoginWindowLogic.ValidateNewPassword(newPassword, confirm));
    }

    [Fact]
    public void ValidateNewPassword_Matching_Ok()
    {
        Assert.Null(LoginWindowLogic.ValidateNewPassword("nove", "nove"));
    }
}
