using TemplateDesigner.Models;
using TemplateDesigner.PdfImport;
using TemplateDesigner.Services;

namespace TemplateDesigner.Tests.PdfImport;

/// <summary>
/// Nastavení sestavy poznané z předlohy. Hodnoty uložené v šabloně mají při tisku přednost před
/// tím, co si uživatel nastavil pro agendu - proto se ukládá jen to, co model opravdu poznal
/// (#12843: uložené volby, kterých se nikdo nedotkl, tiše přebily nastavení uživatele).
/// </summary>
public class ImportPrintParametersTests
{
    private static AiVisionImportPipeline Pipeline(params string[] knownParams) =>
        new("klic", schemaProvider: null, agendaCode: "ZASTAVKOVY_JR_MHD",
            knownParameters: [.. knownParams.Select(n => new TemplateParameter { Name = n, Label = n, Type = "bool" })]);

    private static AiVisionResult Vision(params (string Key, string Value)[] parameters) =>
        new() { TemplateParameters = parameters.ToDictionary(p => p.Key, p => p.Value) };

    [Fact]
    public void PoznanaVolbaSeUloziKeSablone()
    {
        var result = Pipeline("TISK_SIPKY", "TISK_PROJETYCH_ZAST")
            .BuildPrintParameters(Vision(("TISK_SIPKY", "false")));

        Assert.NotNull(result);
        Assert.Equal("false", result["TISK_SIPKY"]);
    }

    /// <summary>Volby, o kterých model nic neřekl, se do šablony psát nesmí - jinak by přebily
    /// to, co si uživatel nastavil pro celou agendu.</summary>
    [Fact]
    public void NepoznaneVolbySeNeukladajiVubec()
    {
        var result = Pipeline("TISK_SIPKY", "TISK_PROJETYCH_ZAST")
            .BuildPrintParameters(Vision(("TISK_SIPKY", "false")));

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.False(result.ContainsKey("TISK_PROJETYCH_ZAST"));
    }

    [Fact]
    public void BezRozpoznaneVolbySeNeuloziNic()
    {
        Assert.Null(Pipeline("TISK_SIPKY").BuildPrintParameters(Vision()));
    }

    /// <summary>Volba, kterou agenda nemá, by se při tisku stejně zahodila - do šablony nepatří.</summary>
    [Fact]
    public void CiziVolbaSePreskoci()
    {
        var result = Pipeline("TISK_SIPKY").BuildPrintParameters(Vision(("NECO_JINEHO", "true")));

        Assert.Null(result);
    }
}
