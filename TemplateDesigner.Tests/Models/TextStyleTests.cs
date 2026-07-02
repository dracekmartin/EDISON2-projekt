using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Models;

[Collection("StyleCascade")]
public class TextStyleTests
{
    private static ResolvedTextStyle Parent() => new TextStyle
    {
        FontFamily = "Tahoma",
        FontSize = 11f,
        Bold = true,
        Italic = false,
        Underline = false,
        Strikethrough = false,
        TextColor = "#FF112233",
        BackgroundColor = "#FFEEEEEE",
        HAlign = TextHorizontalAlignment.Right,
        VAlign = TextVerticalAlignment.Bottom,
        PaddingTop = 1f, PaddingRight = 2f, PaddingBottom = 3f, PaddingLeft = 4f,
        LineSpacing = 1.5f,
    }.Resolve();

    [Fact]
    public void PrazdnyStyl_DediVsechnoZRodice()
    {
        var resolved = new TextStyle().Resolve(Parent());

        Assert.Equal("Tahoma", resolved.FontFamily);
        Assert.Equal(11f, resolved.FontSize);
        Assert.True(resolved.Bold);
        Assert.Equal("#FF112233", resolved.TextColor);
        Assert.Equal(TextHorizontalAlignment.Right, resolved.HAlign);
        Assert.Equal(4f, resolved.PaddingLeft);
        Assert.Equal(1.5f, resolved.LineSpacing);
    }

    [Fact]
    public void ExplicitniHodnota_PrebijeRodice_OstatniSeDedi()
    {
        var style = new TextStyle { FontSize = 8f, Bold = false };

        var resolved = style.Resolve(Parent());

        Assert.Equal(8f, resolved.FontSize);      // override
        Assert.False(resolved.Bold);              // override (false je explicitní, ne "dědit")
        Assert.Equal("Tahoma", resolved.FontFamily);  // zděděno
        Assert.Equal("#FF112233", resolved.TextColor); // zděděno
    }

    [Fact]
    public void DefaultParent_SePouzijeKdyzParentNeniPredan()
    {
        // Resolve() bez argumentu → parent = ResolvedTextStyle.Default (kořen kaskády)
        var resolved = new TextStyle().Resolve();

        Assert.Equal(ResolvedTextStyle.Default.FontFamily, resolved.FontFamily);
        Assert.Equal(ResolvedTextStyle.Default.FontSize, resolved.FontSize);
    }

    [Fact]
    public void FontFamily_SeOrezavaOdMezer()
    {
        // Uživatel může do free-text pole napsat mezeru navíc — bez trimu by font tiše spadl na default
        var resolved = new TextStyle { FontFamily = "  Arial  " }.Resolve(Parent());

        Assert.Equal("Arial", resolved.FontFamily);
    }

    [Fact]
    public void TriUrovnovaKaskada_NejblizsivrstvaVyhrava()
    {
        // element default → column override → resolve
        var elementDefault = new TextStyle { FontSize = 10f, Bold = true, TextColor = "#FF000000" };
        var columnOverride = new TextStyle { FontSize = 12f };

        var elementResolved = elementDefault.Resolve(Parent());
        var final = columnOverride.Resolve(elementResolved);

        Assert.Equal(12f, final.FontSize);           // column vrstva
        Assert.True(final.Bold);                     // element vrstva
        Assert.Equal("#FF000000", final.TextColor);  // element vrstva
        Assert.Equal("Tahoma", final.FontFamily);    // kořen (Parent)
    }

    [Fact]
    public void IsEmpty_RozlisujePrazdnyAVyplnenyStyl()
    {
        Assert.True(new TextStyle().IsEmpty);
        Assert.False(new TextStyle { Bold = false }.IsEmpty);  // explicitní false != dědit
        Assert.False(TextStyle.CreateDefault().IsEmpty);
    }

    [Fact]
    public void Clone_JeNezavislaKopie()
    {
        var original = new TextStyle { FontSize = 9f };
        var clone = original.Clone();
        clone.FontSize = 14f;

        Assert.Equal(9f, original.FontSize);
    }

    [Fact]
    public void ResolveJeIdempotentni_OpakovanaAplikaceNemeniVysledek()
    {
        var style = new TextStyle { FontSize = 8f, Italic = true };

        var once = style.Resolve(Parent());
        var twice = style.Resolve(once);   // resolve nad vlastním výsledkem

        Assert.Equal(once, twice);
    }
}
