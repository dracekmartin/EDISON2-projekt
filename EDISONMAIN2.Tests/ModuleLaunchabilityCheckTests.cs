using System.IO;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Testy obarvení stromu dle launchovatelnosti. Po zavedení generického hosta
/// (EDISONMAIN2API.exe) je každá existující exe launchovatelná - odpadl stav „neretrofitnuto".
/// Suffixy v názvu jsou jen diagnostika M-line; testy je zapínají explicitně a stav obnovují.
/// </summary>
[Collection(SharedStaticStateCollection.Name)]
public class ModuleLaunchabilityCheckTests : IDisposable
{
    private readonly bool _originalDiagnostics = InternalDiagnostics.Enabled;

    public ModuleLaunchabilityCheckTests() => InternalDiagnostics.Enabled = true;

    public void Dispose() => InternalDiagnostics.Enabled = _originalDiagnostics;

    // Existující soubor v AppContext.BaseDirectory (testovací dll) - ResolveExecutablePath ho najde.
    private static string ExistingExeName => Path.GetFileName(typeof(ModuleLaunchabilityCheckTests).Assembly.Location);

    /// <summary>
    /// Barvy stavů se musejí lišit - jinak by asserty níž prošly i s obojím obarveným stejně.
    /// </summary>
    /// <remarks>
    /// ⚠️ Konkrétní odstín se tady **nepíše**. Bere se z theme, a jestli je slovník v běhu testů
    /// mergnutý, rozhoduje to, co ještě běží ve stejném procesu (aplikaci s theme může založit
    /// kterýkoli jiný test, kolekce jdou souběžně). Literál v assertu proto padal obden.
    /// </remarks>
    [Fact]
    public void BarvyStavuSeLisi()
    {
        Assert.NotEqual(ModuleLaunchabilityCheck.StubColor, ModuleLaunchabilityCheck.BrokenColor);
        Assert.StartsWith("#", ModuleLaunchabilityCheck.StubColor);
        Assert.StartsWith("#", ModuleLaunchabilityCheck.BrokenColor);
    }

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

        Assert.Equal(ModuleLaunchabilityCheck.StubColor, marked.Color);
        Assert.EndsWith(" - stub", marked.DisplayedName);
    }

    [Fact]
    public void MissingExe_MarkedAsBroken()
    {
        var module = new ExecutableModule { DisplayedName = "Chybí", ExecutablePath = "neexistuje_xyz.exe", FormName = "X" };

        var marked = (ExecutableModule)ModuleLaunchabilityCheck.Mark(module);

        Assert.Equal(ModuleLaunchabilityCheck.BrokenColor, marked.Color);
        Assert.EndsWith(" - chybí exe", marked.DisplayedName);
    }

    [Fact]
    public void MissingExe_DiagnosticsOff_ColorWithoutSuffix()
    {
        // U zákazníka: červená zůstane, název ne - přetažením na plochu by se suffix uložil do Caché blobu.
        InternalDiagnostics.Enabled = false;
        var module = new ExecutableModule { DisplayedName = "Chybí", ExecutablePath = "neexistuje_xyz.exe", FormName = "X" };

        var marked = (ExecutableModule)ModuleLaunchabilityCheck.Mark(module);

        Assert.Equal(ModuleLaunchabilityCheck.BrokenColor, marked.Color);
        Assert.Equal("Chybí", marked.DisplayedName);
    }

    [Fact]
    public void MarkFamily_DiagnosticsOff_ColorWithoutSuffix()
    {
        InternalDiagnostics.Enabled = false;
        var family = new ModuleFamily { DisplayedName = "F" };
        family.NestedModules.Add(Stub("A"));
        family.NestedModules.Add(Broken("B"));

        var marked = (ModuleFamily)ModuleLaunchabilityCheck.Mark(family);

        Assert.Equal(ModuleLaunchabilityCheck.BrokenColor, marked.Color);
        Assert.Equal("F", marked.DisplayedName);
        Assert.All(marked.NestedModules, m => Assert.DoesNotContain(" - ", m.DisplayedName));
    }

    private static ExecutableModule Launchable(string name = "Live") =>
        new() { DisplayedName = name, ExecutablePath = ExistingExeName, FormName = "X" };

    private static ExecutableModule Stub(string name = "Stub") =>
        new() { DisplayedName = name, ExecutablePath = "", FormName = "" };

    private static ExecutableModule Broken(string name = "Broken") =>
        new() { DisplayedName = name, ExecutablePath = "neexistuje_xyz.exe", FormName = "X" };

    [Fact]
    public void MarkFamily_BrokenGrandchild_PropagatesRedToFamilyAndGrandparent()
    {
        // Rozbité vnouče → červená „rozbité" propaguje na rodinu i prarodiče (sbalená větev signalizuje).
        var parent = new ModuleFamily { DisplayedName = "P" };
        parent.NestedModules.Add(Broken());
        var grandparent = new ModuleFamily { DisplayedName = "GP" };
        grandparent.NestedModules.Add(parent);

        var markedGp = (ModuleFamily)ModuleLaunchabilityCheck.Mark(grandparent);

        Assert.Equal(ModuleLaunchabilityCheck.BrokenColor, markedGp.Color);
        Assert.Equal("GP - rozbité", markedGp.DisplayedName);
        var markedParent = Assert.IsType<ModuleFamily>(Assert.Single(markedGp.NestedModules));
        Assert.Equal(ModuleLaunchabilityCheck.BrokenColor, markedParent.Color);
        Assert.Equal("P - rozbité", markedParent.DisplayedName);
    }

    [Fact]
    public void MarkFamily_LaunchableWinsOverStub_NoColorNoSuffix()
    {
        var family = new ModuleFamily { DisplayedName = "F" };
        family.NestedModules.Add(Launchable());
        family.NestedModules.Add(Stub());

        var marked = (ModuleFamily)ModuleLaunchabilityCheck.Mark(family);

        Assert.Equal("", marked.Color);
        Assert.Equal("F", marked.DisplayedName);
    }

    [Fact]
    public void MarkFamily_OnlyStubs_MarkedOrangeStuby()
    {
        var family = new ModuleFamily { DisplayedName = "F" };
        family.NestedModules.Add(Stub("A"));
        family.NestedModules.Add(Stub("B"));

        var marked = (ModuleFamily)ModuleLaunchabilityCheck.Mark(family);

        Assert.Equal(ModuleLaunchabilityCheck.StubColor, marked.Color);
        Assert.Equal("F - stuby", marked.DisplayedName);
    }

    [Fact]
    public void MarkFamily_CopiesNodeMetadata()
    {
        // Ruční kopie v MarkFamily musí zachovat všechna pole uzlu - jinak by se při obarvení
        // stromu ztratil ElementId / licenční / mlineOnly flag. Launchovatelné dítě → beze suffixu.
        var family = new ModuleFamily
        {
            DisplayedName = "Kmenová evidence",
            IsExpanded    = true,
            ElementId     = "42",
            IsLicensed    = true,
            MlineOnly     = true,
        };
        family.NestedModules.Add(Launchable());

        var marked = (ModuleFamily)ModuleLaunchabilityCheck.Mark(family);

        Assert.Equal("Kmenová evidence", marked.DisplayedName);
        Assert.True(marked.IsExpanded);
        Assert.Equal("42", marked.ElementId);
        Assert.True(marked.IsLicensed);
        Assert.True(marked.MlineOnly);
    }
}
