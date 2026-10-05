using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;

namespace EDISONMAIN2.Tests;

/// <summary>
/// SingleFilePublish.targets odmítne v Release publish Debug build legacy knihovny z C:\Edison
/// (vydání 29. 9. neslo Debug EDLicence). Fixture: vyrobená EDLicence.dll v dočasném „C:\Edison"
/// a minimální projekt s HintPath na ni, publikovaný skutečným dotnet publish.
/// </summary>
public class SingleFilePublishTargetsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sfp-" + Guid.NewGuid().ToString("N"));
    private readonly string _legacyDir;

    public SingleFilePublishTargetsTests()
    {
        _legacyDir = Path.Combine(_root, "Edison");
        Directory.CreateDirectory(_legacyDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } // build server může chvíli držet obj
    }

    [Fact]
    public void ReleasePublish_DebugLegacyDll_FailsNamingTheDll()
    {
        WriteLegacyAssembly("EDLicence", DebuggableAttribute.DebuggingModes.Default
            | DebuggableAttribute.DebuggingModes.DisableOptimizations
            | DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints
            | DebuggableAttribute.DebuggingModes.EnableEditAndContinue);

        var (exitCode, output) = Publish();

        Assert.NotEqual(0, exitCode);
        Assert.Contains("EDLicence.dll", output);
        Assert.Contains("AllowDebugLegacyAssemblies", output);
        AssertNothingPublished();
    }

    [Fact]
    public void ReleasePublish_UnsignedLegacyDllWithUnreadableAttributes_FailsClosed()
    {
        // Atribut z WPF assembly, kterou proces MSBuildu nemá: atributy nejdou přečíst a Debug by jinak prošel bez kontroly.
        WriteLegacyAssembly("EDLicence", DebuggableAttribute.DebuggingModes.Default
            | DebuggableAttribute.DebuggingModes.DisableOptimizations,
            new CustomAttributeBuilder(typeof(System.Windows.ThemeInfoAttribute).GetConstructor(
                    [typeof(System.Windows.ResourceDictionaryLocation), typeof(System.Windows.ResourceDictionaryLocation)])!,
                [System.Windows.ResourceDictionaryLocation.None, System.Windows.ResourceDictionaryLocation.None]));

        var (exitCode, output) = Publish();

        Assert.NotEqual(0, exitCode);
        Assert.Contains("Nelze ověřit", output);
        Assert.Contains("EDLicence.dll", output);
        AssertNothingPublished();
    }

    [Theory]
    [InlineData(false, "")]
    [InlineData(true, "-p:AllowDebugLegacyAssemblies=true")]
    public void ReleasePublish_ReleaseLegacyDllOrExplicitOverride_Passes(bool debugBuild, string extraArgument)
    {
        WriteLegacyAssembly("EDLicence", debugBuild
            ? DebuggableAttribute.DebuggingModes.Default | DebuggableAttribute.DebuggingModes.DisableOptimizations
            : DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints);

        var (exitCode, output) = Publish(extraArgument);

        Assert.True(exitCode == 0, output);
    }

    private void WriteLegacyAssembly(string name, DebuggableAttribute.DebuggingModes modes,
        CustomAttributeBuilder? extraAttribute = null)
    {
        var assembly = new PersistedAssemblyBuilder(new AssemblyName(name), typeof(object).Assembly);
        assembly.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(DebuggableAttribute).GetConstructor([typeof(DebuggableAttribute.DebuggingModes)])!, [modes]));
        if (extraAttribute != null) assembly.SetCustomAttribute(extraAttribute);
        assembly.DefineDynamicModule(name);
        assembly.Save(Path.Combine(_legacyDir, name + ".dll"));
    }

    // Kontrola běží po ComputeResolvedFilesToPublishList, tedy před bundlem i kopií do PublishDir.
    private void AssertNothingPublished()
    {
        var outDir = Path.Combine(_root, "out");
        Assert.False(Directory.Exists(outDir) && Directory.EnumerateFileSystemEntries(outDir).Any(),
            "Odmítnutý publish zapsal do výstupu: " + string.Join(", ", Directory.Exists(outDir)
                ? Directory.EnumerateFileSystemEntries(outDir).Select(Path.GetFileName) : []));
    }

    private (int ExitCode, string Output) Publish(string extraArgument = "")
    {
        var projectDir = Path.Combine(_root, "consumer");
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(Path.Combine(projectDir, "consumer.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <EdisonLegacyDir>{_legacyDir}</EdisonLegacyDir>
              </PropertyGroup>
              <ItemGroup>
                <Reference Include="EDLicence">
                  <HintPath>{Path.Combine(_legacyDir, "EDLicence.dll")}</HintPath>
                </Reference>
              </ItemGroup>
              <Import Project="{Path.Combine(RepoRoot(), "SingleFilePublish.targets")}" />
            </Project>
            """);

        var startInfo = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            WorkingDirectory = projectDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            // dotnet píše UTF-8, výchozí kódová stránka konzole by rozbila české hlášky.
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
        };
        foreach (var argument in new[] { "publish", "-c", "Release", "-o", Path.Combine(_root, "out"), "-nologo",
                     "-nodeReuse:false", "-p:UseSharedCompilation=false" })
            startInfo.ArgumentList.Add(argument);
        if (extraArgument != "") startInfo.ArgumentList.Add(extraArgument);
        // Proměnné MSBuildu z běžícího dotnet test by vnořený build navedly do cizí instalace SDK.
        foreach (var key in startInfo.Environment.Keys.Where(k => k.StartsWith("MSBuild", StringComparison.OrdinalIgnoreCase)).ToList())
            startInfo.Environment.Remove(key);

        using var process = Process.Start(startInfo)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(TimeSpan.FromMinutes(3)))
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail("dotnet publish neskončil do 3 minut.");
        }
        return (process.ExitCode, stdout.Result + stderr.Result);
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "SingleFilePublish.targets"))) return dir.FullName;
        throw new InvalidOperationException("SingleFilePublish.targets nenalezen nad " + AppContext.BaseDirectory);
    }
}
