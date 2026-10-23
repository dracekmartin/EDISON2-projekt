using System.IO;
using System.Runtime.CompilerServices;
using TemplateDesigner.Serialization;
using TemplateDesigner.Services;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Šablony jen pro PDF ze spuštění (<c>LaunchTemplates/</c>, dnes rozdílový JŘ). Jsou zabalené v .exe,
/// ale seed je do databáze nenalévá - v návrháři je nikdo neotevře ani neuloží kopii (rozhodnutí
/// Jan 23. 10.: šablona pro rozdílový JŘ zatím není veřejná).
/// </summary>
public class LaunchTemplateTests
{
    private const string Rozdilovy = "LINKOVY_JR_AD_ROZDILOVY";

    [Fact]
    public void RozdilovyJr_MaSablonuJenProSpusteni()
    {
        var t = EmbeddedTemplates.ForAgenda(EmbeddedTemplates.LaunchFolder, Rozdilovy.ToLowerInvariant());

        Assert.NotNull(t);
        Assert.Equal(Rozdilovy, t.AgendaCode);
        Assert.Equal(TemplateMigrator.CurrentVersion, t.Version);
        Assert.NotEmpty(t.Sections);
        // Systémové šablony nalévá seed do databáze - tam rozdílová nesmí být.
        Assert.Null(EmbeddedTemplates.ForAgenda(EmbeddedTemplates.SystemFolder, Rozdilovy));
        Assert.Null(EmbeddedTemplates.ForAgenda(EmbeddedTemplates.LaunchFolder, "NEEXISTUJE"));
    }

    [Fact]
    public void KazdySouborVeSlozce_JeZabalenyVAssembly()
    {
        var soubory = Directory.GetFiles(LaunchTemplatesDir(), "*.json").Select(Path.GetFileName).ToList();
        var zabalene = EmbeddedTemplates.ResourceNames(EmbeddedTemplates.LaunchFolder).ToList();

        Assert.NotEmpty(soubory);
        foreach (var soubor in soubory)
            Assert.Contains(zabalene, n => n.EndsWith("." + soubor, StringComparison.Ordinal));
    }

    [Fact]
    public void IdSeNepotkaSeSystemovou()
    {
        // Stejné Id jako systémová šablona by spuštění vzalo místo ní nebo naopak (načítá se podle Id).
        var systemova = EmbeddedTemplates.ResourceNames(EmbeddedTemplates.SystemFolder)
            .Select(n => TemplateSerializer.Deserialize(EmbeddedTemplates.Read(n)).Id).ToHashSet();

        foreach (var n in EmbeddedTemplates.ResourceNames(EmbeddedTemplates.LaunchFolder))
            Assert.DoesNotContain(TemplateSerializer.Deserialize(EmbeddedTemplates.Read(n)).Id, systemova);
    }

    [Fact]
    public void NazevAniPopisNemajiDlouhouPomlcku()
    {
        foreach (var n in EmbeddedTemplates.ResourceNames(EmbeddedTemplates.LaunchFolder))
        {
            var t = TemplateSerializer.Deserialize(EmbeddedTemplates.Read(n));
            // Název šablony je i v názvu PDF, které uživatel vidí v prohlížeči.
            Assert.False(t.Name.Contains('—') || t.Name.Contains('–'), $"{n}: název má dlouhou pomlčku");
            var popis = t.Description ?? "";
            Assert.False(popis.Contains('—') || popis.Contains('–'), $"{n}: popis má dlouhou pomlčku");
        }
    }

    private static string LaunchTemplatesDir([CallerFilePath] string thisFile = "") =>
        Path.Combine(new FileInfo(thisFile).Directory!.Parent!.Parent!.FullName,
            "TemplateDesigner", "dotnet", EmbeddedTemplates.LaunchFolder);
}
