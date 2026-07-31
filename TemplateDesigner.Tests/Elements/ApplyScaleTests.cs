using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Škálování šablony (<c>ApplyScale</c>) - nálezy revize 28. 7.: font i padding stylů,
/// per-sloupcové/skupinové overridy, výška řádku právě jednou (ne dvakrát), LineSpacing beze změny.
/// </summary>
public class ApplyScaleTests
{
    private static TextStyle Styl(float font, float pad) => new()
    {
        FontSize = font, PaddingTop = pad, PaddingLeft = pad,
        LineSpacing = 1.2f,   // bezrozměrný násobek - NESMÍ se škálovat
    };

    [Fact]
    public void TextStyleApplyScale_ForceFontAPadding_NeLineSpacing()
    {
        var s = Styl(10f, 2f);
        s.ApplyScale(2f);
        Assert.Equal(20f, s.FontSize);
        Assert.Equal(4f, s.PaddingTop);
        Assert.Equal(4f, s.PaddingLeft);
        Assert.Equal(1.2f, s.LineSpacing);   // beze změny
    }

    [Fact]
    public void StopList_SkaluujeStylyIPerSloupec_DataRowHeightJenJednou()
    {
        var el = new StopListElement
        {
            DataRowHeight = 5f,
            DataStyle = Styl(8f, 1f),
            HeaderStyle = Styl(9f, 1f),
            CurrentStopFontSize = 8f,
            Columns =
            [
                new StopColumn { Width = 10f, Style = new ColumnVisualStyle { Data = Styl(7f, 0.5f) } },
                new StopColumn { Width = null },   // auto - neškáluje se
            ],
        };

        el.ApplyScale(2f);

        Assert.Equal(10f, el.DataRowHeight);          // 5 × 2, právě jednou (ne 20)
        Assert.Equal(16f, el.DataStyle.FontSize);
        Assert.Equal(2f, el.DataStyle.PaddingTop);    // padding se nově škáluje taky
        Assert.Equal(16f, el.CurrentStopFontSize);
        Assert.Equal(20f, el.Columns[0].Width);
        Assert.Equal(14f, el.Columns[0].Style!.Data!.FontSize);   // per-sloupcový override
        Assert.Null(el.Columns[1].Width);              // auto zůstává auto
    }

    /// <summary>Milimetrové rozměry šipky směru a mřížky musí škálovat spolu se šablonou -
    /// jinak se při zvětšení vývěsky ztratí (šipka zůstane vlásková).</summary>
    [Fact]
    public void StopList_SkaluujeSipkuIMrizku()
    {
        var el = new StopListElement
        {
            DirectionArrowWidth = 0.3f,
            DirectionArrowHeadSize = 1.5f,
            GridLineWidth = 0.2f,
            PartGap = 4f,
        };
        var auto = new StopListElement();   // tloušťka mřížky nenastavená = dle rámečku

        el.ApplyScale(2f);
        auto.ApplyScale(2f);

        Assert.Equal(0.6f, el.DirectionArrowWidth);
        Assert.Equal(3f, el.DirectionArrowHeadSize);
        Assert.Equal(0.4f, el.GridLineWidth);
        Assert.Equal(8f, el.PartGap);          // mezera mezi oddíly je taky rozměr v mm
        Assert.Null(auto.GridLineWidth);
    }

    [Fact]
    public void DepartureGrid_SkaluujeSkupinoveOverridy_AWidthOverride()
    {
        var el = new DepartureGridElement
        {
            DataRowHeight = 4f,
            MinuteStyle = Styl(7f, 0.5f),
            GroupColumns =
            [
                new DepartureGroupColumnStyle
                {
                    SkupinaDnuId = "1",
                    WidthOverride = 12f,
                    Style = new ColumnVisualStyle { Header = Styl(9f, 1f) },
                },
            ],
        };

        el.ApplyScale(2f);

        Assert.Equal(8f, el.DataRowHeight);
        Assert.Equal(14f, el.MinuteStyle.FontSize);
        Assert.Equal(24f, el.GroupColumns[0].WidthOverride);
        Assert.Equal(18f, el.GroupColumns[0].Style.Header!.FontSize);
    }

    [Fact]
    public void DynamicTable_SkaluujeElementIPerSloupcoveStyly_ProcentaNe()
    {
        var el = new DynamicTableElement
        {
            RowHeight = 6f,
            HeaderStyle = Styl(9f, 1f),
            Columns =
            [
                new TableColumn { Width = 30f, WidthMode = ColumnWidthMode.Mm, DataStyle = Styl(8f, 0.5f) },
                new TableColumn { Width = 50f, WidthMode = ColumnWidthMode.Percent },   // % se neškáluje
            ],
        };

        el.ApplyScale(2f);

        Assert.Equal(12f, el.RowHeight);
        Assert.Equal(18f, el.HeaderStyle!.FontSize);
        Assert.Equal(60f, el.Columns[0].Width);
        Assert.Equal(16f, el.Columns[0].DataStyle!.FontSize);
        Assert.Equal(50f, el.Columns[1].Width);   // procenta beze změny
    }
}
