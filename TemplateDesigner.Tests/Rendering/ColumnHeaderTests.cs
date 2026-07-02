using TemplateDesigner.Rendering;

namespace TemplateDesigner.Tests.Rendering;

public class ColumnHeaderTests
{
    [Fact]
    public void NeprazdnyOverride_PrebijeZdroj()
    {
        Assert.Equal("Můj nadpis", ColumnHeader.Resolve("Můj nadpis", "Pracovní den"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PrazdnyOverride_VraciZdroj(string? authoredOverride)
    {
        Assert.Equal("Pracovní den", ColumnHeader.Resolve(authoredOverride, "Pracovní den"));
    }
}
