using static EDISONMAIN2.LegacyLastCompany;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Poslední firma starého shellu z registru (ConnectionList): výběr záznamu pro server a REST port shellu.
/// Čtení registru samo je tenké, testuje se výběr.
/// </summary>
public class LegacyLastCompanyTests
{
    [Fact]
    public void Pick_SameServerAndRestPort_ReturnsItsCompany()
    {
        Entry[] entries = [new("10.0.0.5", "57772", "CB_DP", false), new("192.168.16.1", "57770", "JI_DP", false)];

        Assert.Equal("JI_DP", Pick(entries, "192.168.16.1", 57770));
    }

    [Fact]
    public void Pick_ServerComparedIgnoringCase()
    {
        Entry[] entries = [new("SERVER.firma.local", "57772", "CB_DP", false)];

        Assert.Equal("CB_DP", Pick(entries, "server.firma.local", 57772));
    }

    // Starý shell vede víc připojení na týž server pod různými jmény - rozhodne jeho výchozí připojení.
    [Fact]
    public void Pick_SeveralConnectionsToServer_PrefersLegacyDefault()
    {
        Entry[] entries = [new("srv", "57772", "PI_DP", false), new("srv", "57772", "CB_DP", true)];

        Assert.Equal("CB_DP", Pick(entries, "srv", 57772));
    }

    [Fact]
    public void Pick_OtherPortOrMissingCompany_Null()
    {
        Entry[] entries = [new("srv", "57773", "CB_DP", true), new("srv", "57772", "", true), new("srv", "57772", null, false)];

        Assert.Null(Pick(entries, "srv", 57772));
    }

    // Legacy kopíruje restport i server z EDISONini.xml doslova, shell port parsuje jako číslo.
    [Fact]
    public void Pick_PortAndServerWithSpaces_StillMatch()
    {
        Entry[] entries = [new(" srv ", "57772 ", "CB_DP", false)];

        Assert.Equal("CB_DP", Pick(entries, "srv", 57772));
    }

    [Fact]
    public void Pick_NoEntries_Null() => Assert.Null(Pick([], "srv", 57772));
}
