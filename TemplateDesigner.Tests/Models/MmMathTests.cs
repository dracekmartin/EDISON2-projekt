using TemplateDesigner.Models;

namespace TemplateDesigner.Tests.Models;

public class MmMathTests
{
    [Theory]
    [InlineData(10.500001f, 10.5f)]    // typický IEEE-754 artefakt (10 × 1.05)
    [InlineData(10.449f, 10.4f)]
    [InlineData(0.05f, 0.1f)]          // midpoint → AwayFromZero
    [InlineData(-0.05f, -0.1f)]        // midpoint záporný → AwayFromZero
    [InlineData(7f, 7f)]
    [InlineData(0f, 0f)]
    public void Round_ZaokrouhlujeNa01mm(float input, float expected)
    {
        Assert.Equal(expected, MmMath.Round(input), precision: 4);
    }

    [Fact]
    public void Round_Nullable_NullZustavaNull()
    {
        Assert.Null(MmMath.Round((float?)null));
        Assert.Equal(1.2f, MmMath.Round((float?)1.23f)!.Value, precision: 4);
    }

    [Theory]
    [InlineData(7.3f, 5f, 5f)]
    [InlineData(12.6f, 5f, 15f)]
    [InlineData(4.9f, 1f, 5f)]
    [InlineData(0.26f, 0.5f, 0.5f)]
    [InlineData(10f, 5f, 10f)]         // už na mřížce → beze změny
    public void Snap_PrichytiNaNejblizsiNasobekKroku(float mm, float step, float expected)
    {
        Assert.Equal(expected, MmMath.Snap(mm, step), precision: 4);
    }
}
