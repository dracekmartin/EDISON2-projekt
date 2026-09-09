using System.IO;
using EDUPDATE2;

namespace EDUPDATE2.Tests;

public class UpdaterArgsTests
{
    [Fact]
    public void Parse_AllKeys_Populated()
    {
        var args = UpdaterArgs.Parse(new[]
        {
            @"SourcePath=\\server\Edison",
            @"TargetPath=C:\Edison",
            "Version=260825.2",
            "WaitProcess=JinyProces",
            "RunExe=EDISONMAIN2.exe",
            "NucenyUpdate=1",
            "NucenyUpdateTime=66234,60449",
        });

        Assert.Equal(@"\\server\Edison", args.SourcePath);
        Assert.Equal(@"C:\Edison", args.TargetPath);
        Assert.Equal("260825.2", args.Version);
        Assert.Equal("JinyProces", args.WaitProcess);
        Assert.Equal("EDISONMAIN2.exe", args.RunExe);
        Assert.True(args.NucenyUpdate);
        Assert.Equal("66234,60449", args.NucenyUpdateTime);
    }

    [Fact]
    public void Parse_Defaults_WaitProcessIsShell()
    {
        var args = UpdaterArgs.Parse(new[] { @"SourcePath=\\server\Edison" });

        Assert.Equal("EDISONMAIN2", args.WaitProcess);
        Assert.Equal(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar), args.TargetPath);
        Assert.Equal("", args.RunExe);
        Assert.False(args.NucenyUpdate);
    }

    [Fact]
    public void Parse_ValueMayContainEquals()
    {
        var args = UpdaterArgs.Parse(new[] { "Version=a=b" });
        Assert.Equal("a=b", args.Version);
    }

    [Fact]
    public void Parse_DuplicateKey_LastWins()
    {
        var args = UpdaterArgs.Parse(new[] { "Version=1", "Version=2" });
        Assert.Equal("2", args.Version);
    }

    [Fact]
    public void Parse_UnknownAndMalformedTokens_ReportedAndIgnored()
    {
        var unknown = new List<string>();
        var args = UpdaterArgs.Parse(new[] { "Foo=1", "bez-rovnitka", "Version=3" }, unknown.Add);

        Assert.Equal("3", args.Version);
        Assert.Equal(new[] { "Foo=1", "bez-rovnitka" }, unknown);
    }

    [Fact]
    public void Parse_NucenyUpdateZero_False()
    {
        Assert.False(UpdaterArgs.Parse(new[] { "NucenyUpdate=0" }).NucenyUpdate);
        Assert.False(UpdaterArgs.Parse(new[] { "NucenyUpdate=True" }).NucenyUpdate);
    }
}
