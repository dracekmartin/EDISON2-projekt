using System.IO;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Testy obarvení stromu dle launchovatelnosti. Po zavedení generického hosta
/// (EDISONMAIN2API.exe) je každá existující exe launchovatelná — odpadl stav „neretrofitnuto".
/// </summary>
public class ModuleLaunchabilityCheckTests
{
    // Existující soubor v AppContext.BaseDirectory (testovací dll) — ResolveExecutablePath ho najde.
    private static string ExistingExeName => Path.GetFileName(typeof(ModuleLaunchabilityCheckTests).Assembly.Location);

    [Fact]
    public void ExistingExe_IsLaunchable_NoColorNoSuffix()
    {
        var module = new ExecutableModule { DisplayedName = "Agenda", ExecutablePath = ExistingExeName, FormName = "X" };

        var marked = (ExecutableModule)ModuleLaunchabilityCheck.Mark(module);

        // Existuje → launchovatelná i bez reference na EDISONMAIN2API. Beze změny názvu i barvy.
        Assert.Equal("Agenda", marked.DisplayedName);
        Assert.Equal("", marked.Color);
    }

    [Fact]
    public void EmptyPath_MarkedAsStub()
    {
        var module = new ExecutableModule { DisplayedName = "Stub", ExecutablePath = "", FormName = "" };

        var marked = (ExecutableModule)ModuleLaunchabilityCheck.Mark(module);

        Assert.Equal("#FFA500", marked.Color);
        Assert.EndsWith(" — stub", marked.DisplayedName);
    }

    [Fact]
    public void MissingExe_MarkedAsBroken()
    {
        var module = new ExecutableModule { DisplayedName = "Chybí", ExecutablePath = "neexistuje_xyz.exe", FormName = "X" };

        var marked = (ExecutableModule)ModuleLaunchabilityCheck.Mark(module);

        Assert.Equal("#FF0000", marked.Color);
        Assert.EndsWith(" — chybí exe", marked.DisplayedName);
    }
}
