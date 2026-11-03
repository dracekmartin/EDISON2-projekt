using System.Windows.Input;
using EDControls;
using EDControls.EDList;
using EDControls.EDViewModel;
using NSubstitute;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Akce nad šablonami v okně šablon (Jan 3. 11.): Nový, Upravit, Odstranit jako ikony v pruhu nad seznamem
/// se zkratkami seznamů legacy EDISONu (BaseForm2). Šablonu M-line upravit ani odstranit nejde.
/// </summary>
public class AkceOknaSablonTests
{
    public AkceOknaSablonTests() =>
        EDServices.Initialize(new EDServices(Substitute.For<IMLRest>(), Substitute.For<IWindowNavigator>(),
            Substitute.For<IPopupDisplayer>()));

    // Služba se v testu nevolá - akce jen vybírají, co by se stalo.
    private static CacheTemplatePickerDialogViewModel Okno() => new(null!);

    private static EDListRow Radek(string zdroj) =>
        new(Guid.NewGuid().ToString(), new Dictionary<string, string?> { ["name"] = "Vývěska", ["zdroj"] = zdroj });

    [Fact]
    public void AkceNovyUpravitOdstranitSeZkratkami()
    {
        var okno = Okno();

        Assert.Equal([(Key.N, ModifierKeys.Control), (Key.E, ModifierKeys.Control), (Key.D, ModifierKeys.Control)],
            okno.Actions!.Select(a => (a.Key, a.Modifiers)));
        Assert.Same(okno.NewTemplateCommand, okno.Actions![0].Command);
        Assert.Same(okno.EditTemplateCommand, okno.Actions![1].Command);
        Assert.Same(okno.DeleteTemplateCommand, okno.Actions![2].Command);
    }

    [Fact]
    public void SablonuMlineNejdeUpravitAniOdstranit()
    {
        var okno = Okno();

        okno.Selected = Radek("M-line");
        Assert.False(okno.EditTemplateCommand.CanExecute(null));
        Assert.False(okno.DeleteTemplateCommand.CanExecute(null));
        // Šedé ikony musí mít vysvětlení - bez něj uživatel nepoznal, proč nejdou (Jan 3. 11.).
        Assert.StartsWith("Šablona M-line je jen ke čtení", okno.Hint);

        okno.Selected = Radek("vlastní");
        Assert.True(okno.EditTemplateCommand.CanExecute(null));
        Assert.True(okno.DeleteTemplateCommand.CanExecute(null));
        Assert.DoesNotContain("jen ke čtení", okno.Hint);
    }
}
