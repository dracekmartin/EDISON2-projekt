using EDControls;
using EDControls.EDViewModel;
using NSubstitute;
using TemplateDesigner.PdfImport;

namespace TemplateDesigner.Tests.PdfImport;

/// <summary>
/// Grafika obecného importu. Čáry se slévaly do jednoho zamčeného obrázku na pozadí - rastr, který
/// uživatel nemohl upravit a v tisku byl rozmazaný. Obrázek smí v šabloně vzniknout jen z toho,
/// co obrázek opravdu je (logo, QR).
/// </summary>
public class GeneralImportGraphicsTests
{
    public GeneralImportGraphicsTests() =>
        EDServices.Initialize(new EDServices(Substitute.For<IMLRest>(),
            Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()));

    [Fact]
    public void ImportNeslevaGrafikuDoObrazkuNaPozadi()
    {
        var vm = new PdfImportDialogViewModel();

        Assert.Equal(GraphicsMergeMode.None, vm.MergeMode);
    }
}
