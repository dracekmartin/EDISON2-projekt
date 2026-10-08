using EDISONMAIN2.Desktop;
using Point = System.Windows.Point;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Gesto sdílené stromem, Používanými a plochou: drag až za prahem, právě jednou za stisk, a klik jen se
/// stiskem i uvolněním na téže položce bez tažení mezi nimi, i když se tažení zrušilo nad jiným panelem.
/// </summary>
public class DesktopDragGestureTests
{
    private static readonly Point Origin = new(10, 10);
    private static readonly Point Near = new(11, 10);
    private static readonly Point Far = new(110, 10);

    private readonly DesktopDragGesture _gesture = new();

    [Fact]
    public void BelowThreshold_NoDrag_AndClickStillFires()
    {
        _gesture.Press(Origin, "modul", "modul");

        Assert.Null(_gesture.Move<string>(Near, leftPressed: true));
        Assert.True(_gesture.TakeClick("modul"));
    }

    [Fact]
    public void PastThreshold_StartsDragOnlyOncePerPress()
    {
        _gesture.Press(Origin, "modul", "modul");

        Assert.Equal("modul", _gesture.Move<string>(Far, leftPressed: true));
        Assert.Null(_gesture.Move<string>(Far, leftPressed: true));   // další pohyb během dragu nový drag nespustí
    }

    [Fact]
    public void DragEndedByDrop_NextClickFires()
    {
        // Uvolnění tlačítka při dropu si vezme OLE smyčka DoDragDrop, TakeClick se po něm nezavolá.
        _gesture.Press(Origin, "dlaždice", "dlaždice");
        Assert.Equal("dlaždice", _gesture.Move<string>(Far, leftPressed: true));

        _gesture.Reset();
        _gesture.Press(Far, "dlaždice", "dlaždice");
        Assert.True(_gesture.TakeClick("dlaždice"));
    }

    [Fact]
    public void DragCancelledByEscape_ReleaseIsNotClick()
    {
        // Esc ukončí tažení s tlačítkem dole, uvolnění pak WPF doručí prvku.
        _gesture.Press(Origin, "modul", "modul");
        Assert.Equal("modul", _gesture.Move<string>(Far, leftPressed: true));

        Assert.False(_gesture.TakeClick("modul"));
    }

    [Fact]
    public void DragCancelledOverOtherPanel_ReleaseIsNotClick()
    {
        // Modul ze stromu tažený nad dlaždici, Esc, puštění nad dlaždicí: dlaždice se nesmí spustit,
        // ani když na ni uživatel předtím normálně kliknul.
        _gesture.Press(Origin, "dlaždice", "dlaždice");
        Assert.True(_gesture.TakeClick("dlaždice"));

        _gesture.Reset();
        _gesture.Press(Origin, "modul", "modul");
        Assert.Equal("modul", _gesture.Move<string>(Far, leftPressed: true));

        Assert.False(_gesture.TakeClick("dlaždice"));
    }

    [Fact]
    public void ReleaseOnOtherItem_IsNotClick()
    {
        _gesture.Press(Origin, "modul", "modul");

        Assert.False(_gesture.TakeClick("jiný modul"));
    }

    [Fact]
    public void ItemRecreatedBetweenPressAndRelease_ClickStillFires()
    {
        // Přenačtení plochy po zavření agendy vyrobí nové dlaždice; klíč kliku je obsah, ne instance.
        _gesture.Press(Origin, "dlaždice", "MenuModule|42");

        Assert.True(_gesture.TakeClick(new string("MenuModule|42".ToCharArray())));
    }

    [Fact]
    public void PressOutsideItems_DropsPreviousPress()
    {
        // Uvolnění mimo položku TakeClick nezavolá; další stisk mimo položky (kořen okna) ho zahodí,
        // takže tah přes jinou dlaždici netáhne tu dříve stisknutou a uvolnění na ní není klik.
        _gesture.Press(Origin, "dlaždice", "dlaždice");

        _gesture.Reset();
        Assert.Null(_gesture.Move<string>(Far, leftPressed: true));
        Assert.False(_gesture.TakeClick("dlaždice"));
    }

    [Fact]
    public void AfterClick_DragOverAnotherItem_DoesNotDragTheClickedOne()
    {
        _gesture.Press(Origin, "dlaždice", "dlaždice");
        Assert.True(_gesture.TakeClick("dlaždice"));

        Assert.Null(_gesture.Move<string>(Far, leftPressed: true));
    }

    [Fact]
    public void ButtonReleased_NoDrag()
    {
        _gesture.Press(Origin, "modul", "modul");

        Assert.Null(_gesture.Move<string>(Far, leftPressed: false));
    }

    [Fact]
    public void CandidateOfOtherType_NoDrag()
    {
        // Strom táhne ExecutableModule, plocha DesktopShortcut: pohyb nad cizím panelem drag nespustí.
        _gesture.Press(Origin, new object(), "modul");

        Assert.Null(_gesture.Move<string>(Far, leftPressed: true));
    }

    [Fact]
    public void PressWithoutDragItem_NoDrag_AndClickStillFires()
    {
        // Stisk na rodině ve stromu nebo řádku Používaných (není co táhnout, klik ji rozbalí nebo spustí).
        _gesture.Press(Origin, null, "rodina");

        Assert.Null(_gesture.Move<string>(Far, leftPressed: true));
        Assert.True(_gesture.TakeClick("rodina"));
    }

    [Fact]
    public void NestedPress_InnermostItemWins()
    {
        // PreviewMouseDown tuneluje: nejdřív rodina, pak vnořený modul.
        _gesture.Press(Origin, null, "rodina");
        _gesture.Press(Origin, "modul", "modul");

        Assert.Equal("modul", _gesture.Move<string>(Far, leftPressed: true));
    }

    [Fact]
    public void NestedPress_ClickBelongsToInnermostItem()
    {
        _gesture.Press(Origin, null, "rodina");
        _gesture.Press(Origin, "modul", "modul");

        Assert.True(_gesture.TakeClick("modul"));
    }
}
