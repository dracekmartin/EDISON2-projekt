using System.Numerics;
using EDControls.EDActionInProgress;

namespace EDControls.Tests;

/// <summary>Testy čisté logiky skládání krychle na kartě „Akce probíhá": geometrie, pořadí, časová osa.</summary>
public class CubeAssemblyTests
{
    private static void AssertAssembled(CubeAssembly cube)
    {
        Assert.All(cube.Atoms, a =>
        {
            Assert.Equal(1, a.Scale, 9);
            Assert.Equal(1, a.Opacity, 9);
            Assert.Equal(0, a.OffsetX, 9);
            Assert.Equal(0, a.OffsetY, 9);
        });
        Assert.All(cube.Bonds, b =>
        {
            Assert.Equal(1, b.Progress, 9);
            Assert.Equal(1, b.Opacity);
        });
    }

    // --- Geometrie ---

    [Fact]
    public void Atoms_OnEveryCorner_InsideCanvas()
    {
        var cube = new CubeAssembly();

        Assert.Equal(Enumerable.Range(0, 8), cube.Atoms.Select(a => a.Vertex));
        Assert.All(cube.Atoms, a =>
        {
            Assert.InRange(a.X - a.Radius, 0, CubeAssembly.CanvasSize);
            Assert.InRange(a.X + a.Radius, 0, CubeAssembly.CanvasSize);
            Assert.InRange(a.Y - a.Radius, 0, CubeAssembly.CanvasSize);
            Assert.InRange(a.Y + a.Radius, 0, CubeAssembly.CanvasSize);
        });
    }

    [Fact]
    public void Bonds_TwelveCubeEdges()
    {
        var cube = new CubeAssembly();

        Assert.Equal(12, cube.Bonds.Count);
        Assert.All(cube.Bonds, b => Assert.Equal(1, BitOperations.PopCount((uint)(b.From.Vertex ^ b.To.Vertex))));
    }

    [Fact]
    public void NearerParts_AreLarger()
    {
        var cube = new CubeAssembly();
        var atoms = cube.Atoms.OrderBy(a => a.Depth).ToList();
        var bonds = cube.Bonds.OrderBy(b => b.Depth).ToList();

        Assert.Equal(0, atoms[0].Depth, 9);
        Assert.Equal(1, atoms[^1].Depth, 9);
        Assert.True(atoms[^1].Radius > atoms[0].Radius);
        Assert.True(bonds[^1].Width > bonds[0].Width);
    }

    [Fact]
    public void DrawOrder_FarthestFirst_BondsBeforeTheirAtoms()
    {
        var cube = new CubeAssembly();
        var index = cube.DrawOrder.Select((p, i) => (p, i)).ToDictionary(x => x.p, x => x.i);

        Assert.Equal(20, cube.DrawOrder.Count);
        for (int i = 1; i < cube.DrawOrder.Count; i++)
            Assert.True(cube.DrawOrder[i].DrawKey >= cube.DrawOrder[i - 1].DrawKey);
        Assert.All(cube.Bonds, b =>
        {
            Assert.True(index[b] < index[b.From]);
            Assert.True(index[b] < index[b.To]);
        });
    }

    // --- Pořadí skládání ---

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(7)]
    public void AssemblyOrder_WalksAlongEdges_EndsAtGivenCorner(int lastVertex)
    {
        var order = CubeAssembly.AssemblyOrder(lastVertex);

        Assert.Equal(Enumerable.Range(0, 8), order.Order());
        Assert.Equal(lastVertex, order[^1]);
        for (int i = 1; i < order.Length; i++)
            Assert.Equal(1, BitOperations.PopCount((uint)(order[i] ^ order[i - 1])));
    }

    [Fact]
    public void NearestAtom_LandsLast()
    {
        var cube = new CubeAssembly();

        var last = cube.Atoms.MaxBy(a => a.Order)!;

        Assert.Equal(7, last.Order);
        Assert.Equal(1, last.Depth, 9);
    }

    [Fact]
    public void Bonds_GrowFromEarlierAtom_PathBondsLinkConsecutiveAtoms()
    {
        var cube = new CubeAssembly();

        Assert.All(cube.Bonds, b => Assert.True(b.From.Order < b.To.Order));
        var path = cube.Bonds.Where(b => b.IsPathBond).ToList();
        Assert.Equal(7, path.Count);
        Assert.All(path, b => Assert.Equal(b.From.Order + 1, b.To.Order));
    }

    // --- Časová osa ---

    [Fact]
    public void ClosingBonds_StartAfterLastAtom()
    {
        var cube = new CubeAssembly();
        double lastAtomStart = cube.Atoms.Max(a => a.StartMs);

        Assert.All(cube.Bonds.Where(b => !b.IsPathBond), b => Assert.True(b.StartMs > lastAtomStart));
        Assert.Equal(cube.DrawOrder.Max(p => p.StartMs + p.DurationMs), cube.AssemblyMs);
    }

    [Fact]
    public void TimelineAt_AssemblesHoldsComesApartAndRepeats()
    {
        var cube = new CubeAssembly();
        double assembly = cube.AssemblyMs;
        double outStart = assembly + CubeAssembly.HoldMs;
        double outEnd = cube.CycleMs - CubeAssembly.GapMs;

        Assert.Equal(100, cube.TimelineAt(100), 9);
        Assert.Equal(assembly, cube.TimelineAt(assembly + CubeAssembly.HoldMs / 2), 9);
        Assert.Equal(assembly / 2, cube.TimelineAt((outStart + outEnd) / 2), 9);
        Assert.Equal(0, cube.TimelineAt(cube.CycleMs - CubeAssembly.GapMs / 2), 9);
        Assert.Equal(100, cube.TimelineAt(cube.CycleMs + 100), 9);
    }

    [Fact]
    public void Apply_Start_NothingVisible()
    {
        var cube = new CubeAssembly();

        cube.Apply(0);

        Assert.All(cube.Atoms, a => Assert.Equal(0, a.Opacity));
        Assert.All(cube.Bonds, b => Assert.Equal(0, b.Opacity));
    }

    [Fact]
    public void Apply_BeforeLastAtom_FirstVisibleLastHidden()
    {
        var cube = new CubeAssembly();
        var first = cube.Atoms.Single(a => a.Order == 0);
        var last = cube.Atoms.Single(a => a.Order == 7);

        cube.Apply(last.StartMs - 1);

        Assert.True(first.Opacity > 0);
        Assert.Equal(0, last.Opacity);
    }

    [Fact]
    public void Apply_AtomFliesInFromOutside()
    {
        var cube = new CubeAssembly();
        var atom = cube.Atoms.Single(a => a.Order == 0);

        cube.Apply(atom.StartMs + atom.DurationMs / 4);

        // Posun míří od středu stejným směrem jako koule sama
        Assert.True(atom.OffsetX * (atom.X - cube.CenterX) > 0);
        Assert.True(atom.OffsetY * (atom.Y - cube.CenterY) > 0);
    }

    [Fact]
    public void Apply_AfterAssembly_EverythingInPlace()
    {
        var cube = new CubeAssembly();

        cube.Apply(cube.AssemblyMs);

        AssertAssembled(cube);
    }

    [Fact]
    public void Bond_CurrentEnd_FollowsProgress()
    {
        var cube = new CubeAssembly();
        var bond = cube.Bonds[0];

        bond.Progress = 0.5;

        Assert.Equal((bond.From.X + bond.To.X) / 2, bond.CurrentX2, 9);
        Assert.Equal((bond.From.Y + bond.To.Y) / 2, bond.CurrentY2, 9);
    }

    [Fact]
    public void Easing_StartsAtZero_EndsAtOne_BackOvershoots()
    {
        Assert.Equal(0, CubeAssembly.EaseOutCubic(0), 9);
        Assert.Equal(1, CubeAssembly.EaseOutCubic(1), 9);
        Assert.Equal(0, CubeAssembly.EaseOutBack(0), 9);
        Assert.Equal(1, CubeAssembly.EaseOutBack(1), 9);
        Assert.True(CubeAssembly.EaseOutBack(0.7) > 1);
    }
}
