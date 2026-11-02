using EDControls;
using EDControls.EDViewModel;
using NSubstitute;
using TemplateDesigner.Commands.Implementations;
using TemplateDesigner.Designer;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Okno společných vlastností (Enter nad více vybranými prvky).
/// </summary>
public class SpolecneVlastnostiTests
{
    public SpolecneVlastnostiTests() =>
        EDServices.Initialize(new EDServices(Substitute.For<IMLRest>(),
            Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()));

    private static (DesignerState State, IReadOnlyList<PropertyDescriptor> Vlastnosti, TextElement A, TextElement B) Okno()
    {
        var a = new TextElement { BorderWidth = 0.5f, ZIndex = 1 };
        var b = new TextElement { BorderWidth = 2f, ZIndex = 7 };
        var state = new DesignerState();
        var vm = new ElementPropertiesWindowViewModel(state, () => { });
        return (state, vm.PrepareForMultipleElements([a, b]), a, b);
    }

    /// <summary>Zápis tak, jak ho dělá panel vlastností (<c>PropertiesPanel.Commit</c>).</summary>
    private static void Zapis(DesignerState state, FloatPropertyDescriptor d, float hodnota) =>
        state.Execute(d.MakeCommand?.Invoke(hodnota)
                      ?? new SetPropertyCommand(d.Label, d.Get(), hodnota, v => d.Set((float)v!)));

    /// <summary>
    /// Zpětný krok vrátí každému prvku jeho hodnotu. Do 30. 10. vracel všem hodnotu prvního prvku,
    /// takže rámečky nebo vrstvy, které se předtím lišily, po Ctrl+Z zůstaly srovnané.
    /// </summary>
    [Theory]
    [InlineData("Tloušťka rámečku")]
    [InlineData("Vrstva")]
    public void ZpetnyKrokVratiKazdemuJehoHodnotu(string popisek)
    {
        var (state, vlastnosti, a, b) = Okno();
        var pred = (a.BorderWidth, b.BorderWidth, a.ZIndex, b.ZIndex);

        Zapis(state, (FloatPropertyDescriptor)vlastnosti.Single(d => d.Label == popisek), 3f);
        state.Undo();

        Assert.Equal(pred, (a.BorderWidth, b.BorderWidth, a.ZIndex, b.ZIndex));
    }

    /// <summary>Okno nabízí tytéž vlastnosti jako panel jednoho prvku. Do 30. 10. mělo vlastní kopii
    /// se skupinami „Rámeček" a „Pozice" místo „Rámeček prvku" a „Pozice a velikost".</summary>
    [Fact]
    public void StejneVlastnostiJakoPanelPrvku()
    {
        var (_, vlastnosti, a, _) = Okno();
        var panel = a.GetPropertyDescriptors();

        Assert.NotEmpty(vlastnosti);
        foreach (var d in vlastnosti)
            Assert.Contains(panel, p => p.Label == d.Label && p.Group == d.Group && p.GetType() == d.GetType());
    }

    [Fact]
    public void ZmenaPlatiProVsechnyPrvky()
    {
        var (state, vlastnosti, a, b) = Okno();

        Zapis(state, (FloatPropertyDescriptor)vlastnosti.Single(d => d.Label == "Tloušťka rámečku"), 3f);

        Assert.Equal(3f, a.BorderWidth);
        Assert.Equal(3f, b.BorderWidth);
    }
}
