using TemplateDesigner.Commands;
using TemplateDesigner.Commands.Implementations;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Commands;

/// <summary>
/// Zámek undo/redo příkazů - čistá logika bez UI. Hlídá zejména vlastnosti, které se snadno
/// rozbijí: CompositeCommand undo v opačném pořadí, ScaleTemplateCommand inverzní round-trip,
/// obnovení smazaného prvku na původní index.
/// </summary>
public class UndoRedoCommandTests
{
    private static TextElement Text(float x = 0, float y = 0, float w = 10, float? h = 10) =>
        new() { X = x, Y = y, Width = w, Height = h };

    // ── SetPropertyCommand ──────────────────────────────────────────────────
    [Fact]
    public void SetProperty_ExecuteNastaviNovou_UndoVratiStarou()
    {
        var el = Text();
        el.Width = 10;
        var cmd = new SetPropertyCommand("Šířka", el.Width, 42f, v => el.Width = (float)v!);

        cmd.Execute();
        Assert.Equal(42f, el.Width);
        cmd.Undo();
        Assert.Equal(10f, el.Width);
    }

    // ── MoveElementCommand ──────────────────────────────────────────────────
    [Fact]
    public void MoveElement_RoundTrip()
    {
        var el = Text(5, 5);
        var cmd = new MoveElementCommand(el, 5, 5, 30, 40);

        cmd.Execute();
        Assert.Equal((30f, 40f), (el.X, el.Y));
        cmd.Undo();
        Assert.Equal((5f, 5f), (el.X, el.Y));
    }

    // ── ResizeElementCommand (vč. nullable Height) ──────────────────────────
    [Fact]
    public void ResizeElement_RoundTrip_ZFixniNaDynamickouVysku()
    {
        var el = Text(0, 0, 20, 15);
        var cmd = new ResizeElementCommand(el, 0, 0, 20, 15, 2, 3, 50, null);

        cmd.Execute();
        Assert.Equal((2f, 3f, 50f), (el.X, el.Y, el.Width));
        Assert.Null(el.Height);

        cmd.Undo();
        Assert.Equal((0f, 0f, 20f), (el.X, el.Y, el.Width));
        Assert.Equal(15f, el.Height);
    }

    // ── Add / Delete element ─────────────────────────────────────────────────
    [Fact]
    public void AddElement_ExecutePrida_UndoOdebere()
    {
        var section = new Section();
        var el = Text();
        var cmd = new AddElementCommand(section, el);

        cmd.Execute();
        Assert.Contains(el, section.Elements);
        cmd.Undo();
        Assert.DoesNotContain(el, section.Elements);
    }

    [Fact]
    public void DeleteElement_Undo_ObnoviNaPuvodniIndex()
    {
        var section = new Section();
        var a = Text(); var b = Text(); var c = Text();
        section.Elements.AddRange([a, b, c]);
        var cmd = new DeleteElementCommand(section, b);   // prostřední

        cmd.Execute();
        Assert.Equal([a, c], section.Elements);
        cmd.Undo();
        Assert.Equal([a, b, c], section.Elements);   // b zpět na indexu 1
    }

    // ── Add / Delete / Move / Reorder sekce ──────────────────────────────────
    [Fact]
    public void AddSection_ExecutePrida_UndoOdebere()
    {
        var t = new Template();
        var s = new Section();
        var cmd = new AddSectionCommand(t, s);

        cmd.Execute();
        Assert.Contains(s, t.Sections);
        cmd.Undo();
        Assert.DoesNotContain(s, t.Sections);
    }

    [Fact]
    public void DeleteSection_Undo_ObnoviNaPuvodniIndex()
    {
        var t = new Template();
        var s0 = new Section(); var s1 = new Section(); var s2 = new Section();
        t.Sections.AddRange([s0, s1, s2]);
        var cmd = new DeleteSectionCommand(t, s1);

        cmd.Execute();
        Assert.Equal([s0, s2], t.Sections);
        cmd.Undo();
        Assert.Equal([s0, s1, s2], t.Sections);
    }

    [Fact]
    public void MoveSection_Dolu_ARollback()
    {
        var t = new Template();
        var s0 = new Section(); var s1 = new Section();
        t.Sections.AddRange([s0, s1]);
        var cmd = new MoveSectionCommand(t, s0, +1);   // dolů

        cmd.Execute();
        Assert.Equal([s1, s0], t.Sections);
        cmd.Undo();
        Assert.Equal([s0, s1], t.Sections);
    }

    [Fact]
    public void MoveSection_NaOkraji_JeNoOp()
    {
        var t = new Template();
        var s0 = new Section(); var s1 = new Section();
        t.Sections.AddRange([s0, s1]);
        var cmd = new MoveSectionCommand(t, s0, -1);   // už je první → nahoru nelze

        cmd.Execute();
        Assert.Equal([s0, s1], t.Sections);
    }

    [Fact]
    public void ReorderSection_NaCilovyIndex_ARollback()
    {
        var t = new Template();
        var s0 = new Section(); var s1 = new Section(); var s2 = new Section();
        t.Sections.AddRange([s0, s1, s2]);
        var cmd = new ReorderSectionCommand(t, s2, fromIndex: 2, toIndex: 0);

        cmd.Execute();
        Assert.Equal([s2, s0, s1], t.Sections);
        cmd.Undo();
        Assert.Equal([s0, s1, s2], t.Sections);
    }

    // ── Group / Ungroup ──────────────────────────────────────────────────────
    [Fact]
    public void Group_NastaviGroupId_UndoVratiPuvodni()
    {
        var a = Text(); var b = Text();
        a.GroupId = "old"; b.GroupId = null;
        var cmd = new GroupCommand([a, b], "G1");

        cmd.Execute();
        Assert.Equal("G1", a.GroupId);
        Assert.Equal("G1", b.GroupId);
        cmd.Undo();
        Assert.Equal("old", a.GroupId);
        Assert.Null(b.GroupId);
    }

    [Fact]
    public void Ungroup_VynulujeGroupId_UndoVratiPuvodni()
    {
        var a = Text(); var b = Text();
        a.GroupId = "G1"; b.GroupId = "G1";
        var cmd = new UngroupCommand([a, b]);

        cmd.Execute();
        Assert.Null(a.GroupId);
        Assert.Null(b.GroupId);
        cmd.Undo();
        Assert.Equal("G1", a.GroupId);
        Assert.Equal("G1", b.GroupId);
    }

    // ── CompositeCommand - klíčová vlastnost: undo v OPAČNÉM pořadí ───────────
    [Fact]
    public void Composite_ExecuteVPoradi_UndoVOpacnem()
    {
        var log = new List<string>();
        var cmd = new CompositeCommand("Test", [
            new RecordingCommand(log, "A"),
            new RecordingCommand(log, "B"),
            new RecordingCommand(log, "C"),
        ]);

        cmd.Execute();
        cmd.Undo();

        Assert.Equal(
            ["exec:A", "exec:B", "exec:C", "undo:C", "undo:B", "undo:A"],
            log);
    }

    // ── ScaleTemplateCommand - inverzní round-trip ───────────────────────────
    [Fact]
    public void ScaleTemplate_UndoVratiPuvodniRozmery()
    {
        var t = new Template
        {
            PageSize = new PageSize
            {
                Width = 100, Height = 200,
                MarginTop = 10, MarginBottom = 10, MarginLeft = 10, MarginRight = 10,
            },
        };
        var section = new Section { DesignerHeight = 30f };
        var el = Text(20, 20, 40, 20);
        section.Elements.Add(el);
        t.Sections.Add(section);

        var cmd = new ScaleTemplateCommand(t, 2f);

        cmd.Execute();
        Assert.Equal(200f, t.PageSize.Width);
        Assert.Equal(20f, t.PageSize.MarginTop);
        Assert.Equal(60f, section.DesignerHeight);
        Assert.Equal((40f, 40f, 80f), (el.X, el.Y, el.Width));
        Assert.Equal(40f, el.Height);

        cmd.Undo();   // inverzní 1/2
        Assert.Equal(100f, t.PageSize.Width);
        Assert.Equal(10f, t.PageSize.MarginTop);
        Assert.Equal(30f, section.DesignerHeight);
        Assert.Equal((20f, 20f, 40f), (el.X, el.Y, el.Width));
        Assert.Equal(20f, el.Height);
    }

    private sealed class RecordingCommand(List<string> log, string id) : IDesignerCommand
    {
        public string Description => id;
        public void Execute() => log.Add($"exec:{id}");
        public void Undo() => log.Add($"undo:{id}");
    }
}
