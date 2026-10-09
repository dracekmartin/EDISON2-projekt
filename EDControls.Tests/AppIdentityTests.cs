using System.Reflection;
using EDControls.EDViewModel;
using NSubstitute;

namespace EDControls.Tests;

/// <summary>
/// Pořadí, ze kterého hlášení chyb, „Informace o verzi" a feedback berou prefix, jméno a verzi
/// aplikace: služby, entry assembly, konstanta.
/// </summary>
public class AppIdentityTests
{
    private static readonly Assembly Entry = typeof(AppIdentityTests).Assembly;

    private static string EntryInformationalVersion =>
        Entry.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

    private static EDServices Services() =>
        new(null, Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>());

    [Fact]
    public void ExplicitServiceValues_Win()
    {
        var services = Services();
        services.FormIdPrefix = "TD";
        services.AppDisplayName = "M-line EDISON 2";
        services.AppVersion = "25.7.30";

        Assert.Equal(new AppIdentity("TD", "M-line EDISON 2", "25.7.30"), AppIdentity.Resolve(services, Entry));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void ServicesWithoutNameAndVersion_TakeEntryAssembly(string? value)
    {
        // TemplateDesigner nastavuje jen FormIdPrefix - bez fallbacku chodilo version: null.
        var services = Services();
        services.AppDisplayName = value;
        services.AppVersion = value;

        var app = AppIdentity.Resolve(services, Entry);

        Assert.Equal(AppIdentity.DefaultPrefix, app.Prefix);
        Assert.Equal("EDControls.Tests", app.DisplayName);
        Assert.Equal(AppIdentity.ShortenBuildMetadata(EntryInformationalVersion), app.Version);
    }

    [Fact]
    public void WithoutServices_PrefixIsEntryAssemblyName()
    {
        // Pád před EDServices.Initialize (TD věší handlery v konstruktoru App) nesmí přijít jako EDISON2.
        Assert.Equal("EDControls.Tests", AppIdentity.Resolve(null, Entry).Prefix);
    }

    [Fact]
    public void WithoutServicesAndEntryAssembly_Constants()
    {
        Assert.Equal(new AppIdentity("EDISON2", "M-line", "nezjištěna"), AppIdentity.Resolve(null, null));
    }

    [Theory]
    [InlineData("1.0.0+3a7b99dff4556ef21c4c8c66490a3052fe89a13e", "1.0.0+3a7b99df")]
    [InlineData("2.1.0+3a7b99df", "2.1.0+3a7b99df")]
    [InlineData("1.0.0+abc", "1.0.0+abc")]
    [InlineData("25.7.30", "25.7.30")]
    public void ShortenBuildMetadata_KeepsEightCharsOfHash(string version, string expected) =>
        Assert.Equal(expected, AppIdentity.ShortenBuildMetadata(version));
}
