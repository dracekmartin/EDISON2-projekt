using TemplateDesigner.Models.Elements;
using TemplateDesigner.Rendering;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// 2. řádek záhlaví Minut odjezdů je text uvnitř záhlaví skupiny: pozadí, zarovnání a okraje mu
/// kreslí 1. řádek. Okno stylu mu je proto nenabízí, jinak by to byla pole, která nic nedělají
/// (`B194` E3, sada Text). Formulář má totéž hlídané v <c>EDInputFormSchemaDriftTests</c>.
/// </summary>
public class SadaStyluTextTests
{
    [Fact]
    public void DruhyRadekZahlavi_NabiziJenPismoABarvuTextu()
    {
        var o = ElementStyleButton.EditOptionsFor(nameof(DepartureGridElement.HeaderLine2Style));

        Assert.False(o.AllowBackgroundColor);
        Assert.False(o.AllowHAlign);
        Assert.False(o.AllowVAlign);
        Assert.False(o.AllowPadding);
        Assert.True(o.AllowFontFamily && o.AllowFontSize && o.AllowBold && o.AllowItalic
                    && o.AllowUnderline && o.AllowStrikethrough && o.AllowTextColor);
    }

    [Fact]
    public void ZahlaviSkupinDnu_NabiziCelouBunku()
    {
        Assert.Equal(CellStyleEditOptions.AllEnabled,
            ElementStyleButton.EditOptionsFor(nameof(DepartureGridElement.HeaderStyle)));
    }
}
