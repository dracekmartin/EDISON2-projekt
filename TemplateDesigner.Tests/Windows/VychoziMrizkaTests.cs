using TemplateDesigner.Designer;
using TemplateDesigner.Services;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Výchozí mřížka plátna je 5 mm bez přichytávání (rozhodnutí Jan 30. 10.). Do 30. 10. měl stav
/// návrháře 1 mm se zapnutým přichytáváním a výchozí předvolby uživatele 5 mm bez něj - kdo
/// předvolby měl, dostal jedno, kdo ne (testy, start bez serveru), druhé, a příručka tvrdila první.
/// </summary>
public class VychoziMrizkaTests
{
    [Fact]
    public void StavNavrhare_APredvolby_MajiStejnouVychoziMrizku()
    {
        var stav = new DesignerState();
        var predvolby = MainStatePref.Default;

        Assert.Equal(stav.GridSize, predvolby.GridSize);
        Assert.Equal(stav.SnapToGrid, predvolby.SnapToGrid);
        Assert.Equal(stav.SnapGapMm, predvolby.SnapGapMm);
    }

    [Fact]
    public void VychoziMrizka_5mmBezPrichytavani()
    {
        var stav = new DesignerState();

        Assert.Equal(5f, stav.GridSize);
        Assert.False(stav.SnapToGrid);
    }
}
