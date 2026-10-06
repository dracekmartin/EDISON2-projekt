using EDISONMAIN2.Desktop;
using Point = System.Windows.Point;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Gesto tažení společné stromu (přidání na plochu) i ploše (přeuspořádání): drag až za prahem,
/// právě jednou za stisk, a klik, který po tažení přijde, se nesmí provést jako akce.
/// </summary>
public class DesktopDragGestureTests
{
    private static readonly Point Origin = new(10, 10);
    private static readonly Point Near = new(11, 10);
    private static readonly Point Far = new(110, 10);

    [Fact]
    public void BelowThreshold_NoDrag_AndClickStillFires()
    {
        var gesture = new DesktopDragGesture<string>();
        gesture.Press(Origin, "modul");

        Assert.Null(gesture.Move(Near, leftPressed: true));
        Assert.True(gesture.TakeClick());
    }

    [Fact]
    public void PastThreshold_StartsDragOnce_AndSwallowsTheFollowingClickOnly()
    {
        var gesture = new DesktopDragGesture<string>();
        gesture.Press(Origin, "modul");

        Assert.Equal("modul", gesture.Move(Far, leftPressed: true));
        Assert.Null(gesture.Move(Far, leftPressed: true));   // další pohyb během dragu nový drag nespustí
        Assert.False(gesture.TakeClick());                    // uvolnění po tažení není klik
        Assert.True(gesture.TakeClick());                     // další klik už platí
    }

    [Fact]
    public void ButtonReleased_NoDrag()
    {
        var gesture = new DesktopDragGesture<string>();
        gesture.Press(Origin, "modul");

        Assert.Null(gesture.Move(Far, leftPressed: false));
    }

    [Fact]
    public void PressWithoutItem_NoDrag()
    {
        // Stisk na rodině ve stromu (není co táhnout).
        var gesture = new DesktopDragGesture<string>();
        gesture.Press(Origin, null);

        Assert.Null(gesture.Move(Far, leftPressed: true));
        Assert.True(gesture.TakeClick());
    }

    [Fact]
    public void NestedPress_InnermostItemWins()
    {
        // PreviewMouseDown tuneluje: nejdřív rodina (null), pak vnořený modul.
        var gesture = new DesktopDragGesture<string>();
        gesture.Press(Origin, null);
        gesture.Press(Origin, "modul");

        Assert.Equal("modul", gesture.Move(Far, leftPressed: true));
    }
}
