using EDISONMAIN2.Windows.SetupWindow;

namespace EDISONMAIN2.Tests;

/// <summary>Rozhodovací logika setup wizardu (validace, výběr databáze po testu připojení).</summary>
public class SetupWizardLogicTests
{
    [Fact]
    public void ValidateConnection_Valid_ParsesBothPorts()
    {
        var error = SetupWizardLogic.ValidateConnection("10.0.0.5", "51972", "12345",
            out int superPort, out int restPort);

        Assert.Null(error);
        Assert.Equal(51972, superPort);
        Assert.Equal(12345, restPort);
    }

    [Theory]
    [InlineData("", "51972", "12345", "Zadejte server.")]
    [InlineData("10.0.0.5", "", "12345", "Zadejte platný port.")]
    [InlineData("10.0.0.5", "abc", "12345", "Zadejte platný port.")]
    [InlineData("10.0.0.5", "0", "12345", "Zadejte platný port.")]
    [InlineData("10.0.0.5", "51972", "", "Zadejte platný REST port.")]
    [InlineData("10.0.0.5", "51972", "-1", "Zadejte platný REST port.")]
    public void ValidateConnection_Invalid_ReturnsFirstError(
        string server, string superPort, string restPort, string expected)
    {
        Assert.Equal(expected, SetupWizardLogic.ValidateConnection(server, superPort, restPort, out _, out _));
    }

    [Fact]
    public void PickDatabaseIndex_KeepsPreviousSelectionWhenStillPresent()
    {
        Assert.Equal(1, SetupWizardLogic.PickDatabaseIndex(["CB_X", "VS_CSAD"], "VS_CSAD"));
    }

    [Fact]
    public void PickDatabaseIndex_SingleDatabase_AutoSelects()
    {
        Assert.Equal(0, SetupWizardLogic.PickDatabaseIndex(["CB_X"], null));
        Assert.Equal(0, SetupWizardLogic.PickDatabaseIndex(["CB_X"], "JINA"));
    }

    [Fact]
    public void PickDatabaseIndex_MultipleWithoutMatch_NoSelection()
    {
        Assert.Equal(-1, SetupWizardLogic.PickDatabaseIndex(["CB_X", "VS_CSAD"], null));
        Assert.Equal(-1, SetupWizardLogic.PickDatabaseIndex(["CB_X", "VS_CSAD"], "JINA"));
        Assert.Equal(-1, SetupWizardLogic.PickDatabaseIndex([], null));
    }

    [Theory]
    [InlineData("", "CB_X", "CB_X", "Zadejte název připojení.")]
    [InlineData("  ", "CB_X", "CB_X", "Zadejte název připojení.")]
    [InlineData("CACHE", null, "CB_X", "Vyberte databázi.")]
    [InlineData("CACHE", "", "CB_X", "Vyberte databázi.")]
    [InlineData("CACHE", "CB_X", "", "Zadejte IDFa firmy.")]
    [InlineData("CACHE", "CB_X", "  ", "Zadejte IDFa firmy.")]
    public void ValidateSave_Invalid_ReturnsFirstError(string name, string? database, string idfa, string expected)
    {
        Assert.Equal(expected, SetupWizardLogic.ValidateSave(name, database, idfa));
    }

    [Fact]
    public void ValidateSave_AllFilled_Ok()
    {
        Assert.Null(SetupWizardLogic.ValidateSave("CACHE", "CB_X", "CB_X"));
    }
}
