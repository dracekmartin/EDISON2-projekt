using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Paměť hledání v oknech vlastností prvku: přežije zavření okna, ale jen u téhož druhu prvku.
/// Do 13. 10. byla společná a hledání z tabulky spojů se objevilo i v poznámkách.
/// </summary>
public class HledaniOkenPrvkuTests
{
    [Fact]
    public void RuzneDruhyPrvku_MajiVlastniHledani() =>
        Assert.NotEqual(
            TemplateDesigner.MainWindow.ElementDialogSearchKey(new LineTableElement()),
            TemplateDesigner.MainWindow.ElementDialogSearchKey(new NotesLegendElement()));

    [Fact]
    public void DvaPrvkyStejnehoDruhu_SdiliHledani() =>
        Assert.Equal(
            TemplateDesigner.MainWindow.ElementDialogSearchKey(new LineTableElement()),
            TemplateDesigner.MainWindow.ElementDialogSearchKey(new LineTableElement()));
}
