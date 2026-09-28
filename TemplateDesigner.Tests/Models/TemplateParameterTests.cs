using TemplateDesigner.Models;

namespace TemplateDesigner.Tests.Models;

/// <summary>
/// Číselný parametr sestavy bez hodnoty zůstává prázdný, nepřevádí se na 0.
/// </summary>
/// <remarks>
/// U „Nejvýš sloupců variant tras" znamená prázdné pole „bez omezení". Dokud se prázdno měnilo
/// na 0, ukazoval formulář nulu a vypadalo to jako „netisknout žádné" (nález Jan 25. 9.).
/// </remarks>
public class TemplateParameterTests
{
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void PrazdneCislo_ZustanePrazdne(string? vychozi)
    {
        var p = new TemplateParameter { Name = "MAX_SLOUPCU_VARIANT", Type = "number", DefaultValue = vychozi };

        Assert.Null(p.GetDefaultTyped());
    }

    [Fact]
    public void VyplneneCislo_SePrecte()
    {
        var p = new TemplateParameter { Name = "HODINY_OD", Type = "number", DefaultValue = "4" };

        Assert.Equal(4f, p.GetDefaultTyped());
    }
}
