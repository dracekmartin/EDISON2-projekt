using EDISONMAIN2.Windows.SetupWindow;

namespace EDISONMAIN2.Tests;

/// <summary>Parsování legacy delimited výstupu ALVA.EDISON.Main.LoadDatabaseList.</summary>
public class SetupWindowTests
{
    [Fact]
    public void ParseDatabaseRows_SkipsIndexZeroAndForeignRows()
    {
        object[] output = ["ERR~", "database~ALVA", "jine~X", "database~VS_CSAD", null!, "database"];

        var databases = SetupWindow.ParseDatabaseRows(output, '~');

        Assert.Equal(["ALVA", "VS_CSAD"], databases);
    }
}
