using EDISONMAIN2.Windows.EdisonMainWindow.Modules;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Testy sdílené služby plochy (zdroj pravdy pro dlaždice i ★ ve stromu). Plocha je nadmnožina
/// oblíbených - drží i položky, které v hlavní nabídce nejsou (tiskové sestavy, pohledy DS).
/// Čistě in-memory - bez persistence a launch implementace (ty drží EdisonMainViewModel).
/// </summary>
public class ModuleShortcutsServiceTests
{
    private static ExecutableModule Exe(string id, string name = "M") =>
        new() { DisplayedName = name, ElementId = id, ExecutablePath = id + ".exe" };

    // Uložený zástupce modulu hlavní nabídky (jak ho zapíše ★ / DnD ze stromu).
    private static DesktopShortcutDto Menu(string elementId, int? col = null, int? row = null) =>
        new(nameof(ShortcutKind.MenuModule), elementId, elementId, "M", Col: col, Row: row);

    private static (int Col, int Row) Cell(DesktopShortcut s) => (s.Col, s.Row);

    [Fact]
    public void Seed_PopulatesInOrder_AndSetsIsFavorite()
    {
        var a = Exe("100"); var b = Exe("200"); var c = Exe("300");
        var svc = new ModuleShortcutsService();

        svc.Seed([a, b, c], [Menu("300"), Menu("100")]);

        Assert.Equal(["300", "100"], svc.Items.Select(i => i.ElementId));
        Assert.True(c.IsFavorite);
        Assert.True(a.IsFavorite);
        Assert.False(b.IsFavorite);
    }

    [Fact]
    public void Seed_ModuleMissingFromTree_HiddenButKeptForPersistence()
    {
        // Modul zmizel ze stromu (odebraná licence / hidden / mlineOnly) - nesmí se tiše smazat.
        var a = Exe("100");
        var svc = new ModuleShortcutsService();

        svc.Seed([a], [Menu("999"), Menu("100")]);

        Assert.Equal(["100"], svc.Items.Select(i => i.ElementId));
        Assert.Equal(["100", "999"], svc.Persistable().Select(d => d.ElementId));   // zobrazené + přenesené
    }

    [Fact]
    public void Seed_UnknownKind_KeptForPersistence()
    {
        var svc = new ModuleShortcutsService();

        svc.Seed([], [new DesktopShortcutDto("SomethingNewer", "5", "31001", "X")]);

        Assert.Empty(svc.Items);
        Assert.Equal("SomethingNewer", Assert.Single(svc.Persistable()).Kind);
    }

    [Fact]
    public void Seed_DoesNotRaiseMembershipChanged()
    {
        // Načtená plocha se nesmí znovu ukládat - persistence visí na tomhle eventu.
        var svc = new ModuleShortcutsService();
        int changes = 0;
        svc.MembershipChanged += () => changes++;

        svc.Seed([Exe("100")], [Menu("100")]);

        Assert.Equal(0, changes);
    }

    [Fact]
    public void AddAndRemove_RaiseMembershipChanged_OnlyOnRealChange()
    {
        var a = Exe("100");
        var svc = new ModuleShortcutsService();
        svc.Seed([a], []);
        int changes = 0;
        svc.MembershipChanged += () => changes++;

        svc.Add(a);
        svc.Add(a);       // idempotentní - beze změny
        svc.Remove(a);
        svc.Remove(a);    // už není na ploše

        Assert.Equal(2, changes);
    }

    [Fact]
    public void Batch_RaisesMembershipChangedOnce_AfterAllMutations()
    {
        var a = Exe("100"); var b = Exe("200");
        var svc = new ModuleShortcutsService();
        svc.Seed([a, b], []);
        int itemsAtEvent = -1, changes = 0;
        svc.MembershipChanged += () => { changes++; itemsAtEvent = svc.Items.Count; };

        svc.Batch(() =>
        {
            svc.Add(a);
            svc.Batch(() => svc.Add(b));   // vnořený blok event nepouští
        });

        Assert.Equal(1, changes);
        Assert.Equal(2, itemsAtEvent);
    }

    [Fact]
    public void Batch_WithoutChange_RaisesNothing()
    {
        var a = Exe("100");
        var svc = new ModuleShortcutsService();
        svc.Seed([a], [Menu("100")]);
        int changes = 0;
        svc.MembershipChanged += () => changes++;

        svc.Batch(() => svc.Add(a));   // už je na ploše

        Assert.Equal(0, changes);
    }

    [Fact]
    public void Seed_NullStored_EmptyAndNotLoaded_EmptyListIsLoaded()
    {
        // null = load selhal: prázdná plocha se nesmí uložit přes uživatelovu; [] = blob opravdu prázdný.
        var svc = new ModuleShortcutsService();

        svc.Seed([Exe("100")], null);
        Assert.Empty(svc.Items);
        Assert.False(svc.IsLoaded);

        svc.Seed([Exe("100")], []);
        Assert.True(svc.IsLoaded);
    }

    [Theory]
    [InlineData("0")]    // Enum.TryParse bere číselné stringy → "0" by prošlo jako MenuModule
    [InlineData("7")]    // …a "7" jako nedefinovaná hodnota enumu, kterou by plocha vykreslila
    public void Seed_NumericKind_IsNotAcceptedAsEnum(string kind)
    {
        var a = Exe("31001");
        var svc = new ModuleShortcutsService();

        svc.Seed([a], [new DesktopShortcutDto(kind, "PrehledSkod", "31001", "Přehled škod")]);

        Assert.Empty(svc.Items);
        Assert.False(a.IsFavorite);
        Assert.Equal(kind, Assert.Single(svc.Persistable()).Kind);
    }

    [Fact]
    public void Add_DoesNotReuseCellOfHiddenItem()
    {
        // Skrytá položka (modul mimo strom) se ukládá se svou pozicí; kdyby ji nová dlaždice
        // převzala, po návratu licence by obě seděly na téže buňce mřížky.
        var a = Exe("100");
        var svc = new ModuleShortcutsService();
        svc.Seed([a], [Menu("999", col: 0, row: 0)]);

        svc.Add(a);

        Assert.Equal((1, 0), Cell(Assert.Single(svc.Items)));
    }

    [Fact]
    public void Seed_CollidingCells_AreSeparated()
    {
        // Kolizi umí vyrobit ruční editace blobu i mix uložené pozice s doplněným pořadím.
        var a = Exe("100"); var b = Exe("200");
        var svc = new ModuleShortcutsService();

        svc.Seed([a, b], [Menu("100", col: 2, row: 0), Menu("200", col: 2, row: 0)]);

        Assert.Equal([(2, 0), (3, 0)], svc.Items.Select(Cell));
    }

    [Fact]
    public void Seed_HiddenItemCell_IsNotTakenByShownItem()
    {
        var a = Exe("100");
        var svc = new ModuleShortcutsService();

        svc.Seed([a], [Menu("999", col: 0, row: 0), Menu("100", col: 0, row: 0)]);

        Assert.Equal((1, 0), Cell(Assert.Single(svc.Items)));   // buňku (0,0) drží skrytá "999"
    }

    [Fact]
    public void Seed_NonMenuKind_NeedsNoTreeResolution_AndKeepsReportPayload()
    {
        var svc = new ModuleShortcutsService();
        var report = new ReportPayload("ES", "EkonomickySystem", "1", "S12", "P7", JeVerejny: true);

        svc.Seed([], [new DesktopShortcutDto(nameof(ShortcutKind.PrintReport), "PrehledSkod", "31001",
                                             "Přehled škod", "VOZIDLA2.exe", Report: report)]);

        var item = Assert.Single(svc.Items);
        Assert.Equal(ShortcutKind.PrintReport, item.Kind);
        Assert.Equal("Přehled škod", item.DisplayedName);
        Assert.Null(item.Module);
        Assert.Equal(report, item.Report);
        Assert.Equal(report, Assert.Single(svc.Persistable()).Report);
    }

    [Fact]
    public void Key_DistinguishesKinds_SoBothCoexistOverSameForm()
    {
        var a = Exe("31001");
        var svc = new ModuleShortcutsService();

        svc.Seed([a], [Menu("31001"), new DesktopShortcutDto(nameof(ShortcutKind.PrintReport), "31001", "31001", "Sestava")]);

        Assert.Equal(2, svc.Items.Count);
        Assert.Equal([ShortcutKind.MenuModule, ShortcutKind.PrintReport], svc.Items.Select(i => i.Kind));
    }

    [Fact]
    public void Seed_ClearsPreviousItems_AndResetsOldFlags()
    {
        var old = Exe("100");
        var svc = new ModuleShortcutsService();
        svc.Seed([old], [Menu("100")]);
        Assert.True(old.IsFavorite);

        var fresh = Exe("200");
        svc.Seed([fresh], [Menu("200")]);

        Assert.False(old.IsFavorite);
        Assert.Equal(["200"], svc.Items.Select(i => i.ElementId));
    }

    // Dlaždice nemá kontext stromu → text = dlouhý název, cesta agendou jde do ToolTipu.
    [Fact]
    public void Seed_TileShowsLongName_AndTreePathForToolTip()
    {
        var a = new ExecutableModule {
            DisplayedName = "Nastavení", ElementId = "100", ExecutablePath = "VOZ.exe",
            FormName = "f", LongName = "Nastavení vozidel"
        };
        var svc = new ModuleShortcutsService();

        svc.Seed([a], [Menu("100")], _ => "Vozidla › Číselníky › Nastavení");

        var tile = Assert.Single(svc.Items);
        Assert.Equal("Nastavení", tile.DisplayedName);
        Assert.Equal("Nastavení vozidel", tile.DisplayedLongName);
        Assert.Equal("Vozidla › Číselníky › Nastavení", tile.TreePath);
    }

    [Fact]
    public void Seed_ModuleWithoutLongName_TileFallsBackToShortName()
    {
        var a = Exe("100", "Objednávky");
        var svc = new ModuleShortcutsService();

        svc.Seed([a], [Menu("100")], _ => "Autobusová doprava › Objednávky");

        Assert.Equal("Objednávky", Assert.Single(svc.Items).DisplayedLongName);
    }

    // Bez znalosti cesty (položka mimo strom) by prázdný ToolTip byl jen prázdný rámeček.
    [Fact]
    public void Seed_WithoutPathProvider_ToolTipFallsBackToName()
    {
        var a = Exe("100", "Objednávky");
        var svc = new ModuleShortcutsService();

        svc.Seed([a], [Menu("100")]);

        Assert.Equal("Objednávky", Assert.Single(svc.Items).TreePath);
    }

    [Fact]
    public void Add_UsesPathProviderFromLastSeed()
    {
        var a = Exe("100", "Objednávky");
        var svc = new ModuleShortcutsService();
        svc.Seed([a], [], _ => "Autobusová doprava › Objednávky");

        svc.Add(a);

        Assert.Equal("Autobusová doprava › Objednávky", Assert.Single(svc.Items).TreePath);
    }

    [Fact]
    public void Add_IsIdempotent_AndSetsFlag()
    {
        var a = Exe("100");
        var svc = new ModuleShortcutsService();
        svc.Seed([a], []);

        svc.Add(a);
        svc.Add(a);

        Assert.Same(a, Assert.Single(svc.Items).Module);
        Assert.True(a.IsFavorite);
    }

    [Fact]
    public void Add_IgnoresEmptyElementId()
    {
        var svc = new ModuleShortcutsService();

        svc.Add(Exe("", "NoId"));

        Assert.Empty(svc.Items);
    }

    [Fact]
    public void Remove_ByModule_ClearsFlag()
    {
        var a = Exe("100");
        var svc = new ModuleShortcutsService();
        svc.Seed([a], [Menu("100")]);

        svc.Remove(a);

        Assert.Empty(svc.Items);
        Assert.False(a.IsFavorite);
    }

    [Fact]
    public void Remove_ByShortcut_ClearsFlag()
    {
        var a = Exe("100");
        var svc = new ModuleShortcutsService();
        svc.Seed([a], [Menu("100")]);

        svc.Remove(svc.Items[0]);

        Assert.Empty(svc.Items);
        Assert.False(a.IsFavorite);
    }

    [Fact]
    public void Seed_AssignsOrderCells_WhenPositionMissing()
    {
        var a = Exe("100"); var b = Exe("200");
        var svc = new ModuleShortcutsService();

        svc.Seed([a, b], [Menu("200"), Menu("100")]);

        Assert.Equal((0, 0), Cell(svc.Items[0]));   // první v pořadí
        Assert.Equal((1, 0), Cell(svc.Items[1]));
    }

    [Fact]
    public void Seed_UsesPersistedCells_WhenProvided()
    {
        var a = Exe("100"); var b = Exe("200");
        var svc = new ModuleShortcutsService();

        svc.Seed([a, b], [Menu("100", col: 5, row: 1), Menu("200", col: 2, row: 0)]);

        Assert.Equal((5, 1), Cell(svc.Items[0]));
        Assert.Equal((2, 0), Cell(svc.Items[1]));
    }

    [Fact]
    public void Seed_HalfCoordinate_FallsBackToInsertionOrder()
    {
        // Půlka souřadnice (jen Col) je horší než pořadí - dlaždice by skončila v řádku 0 klidně
        // na cizí buňce. "200" pozici nemá vůbec → pořadí (order=1).
        var a = Exe("100"); var b = Exe("200");
        var svc = new ModuleShortcutsService();

        svc.Seed([a, b], [Menu("100", col: 3), Menu("200")]);

        Assert.Equal((0, 0), Cell(svc.Items[0]));
        Assert.Equal((1, 0), Cell(svc.Items[1]));
    }

    [Fact]
    public void Seed_DuplicateKeys_AddedOnce()
    {
        var a = Exe("100");
        var svc = new ModuleShortcutsService();

        svc.Seed([a], [Menu("100"), Menu("100")]);

        Assert.Same(a, Assert.Single(svc.Items).Module);
    }

    [Fact]
    public void PlaceAt_Guards_NoOp_DoNotRaisePositionsChanged()
    {
        var a = Exe("100"); var b = Exe("200");
        var svc = new ModuleShortcutsService();
        svc.Seed([a, b], [Menu("100")]);   // a=(0,0), b není na ploše
        var orphan = DesktopShortcut.ForModule(b);
        var raised = false;
        svc.PositionsChanged += () => raised = true;

        svc.PlaceAt(svc.Items[0], -1, 0);   // záporná souřadnice
        svc.PlaceAt(svc.Items[0], 0, -1);
        svc.PlaceAt(orphan, 2, 0);          // dlaždice mimo plochu
        svc.PlaceAt(svc.Items[0], 0, 0);    // stejná buňka

        Assert.Equal((0, 0), Cell(svc.Items[0]));
        Assert.False(raised);
    }

    [Fact]
    public void PlaceAt_StaleInstanceOfSameKey_MovesTheLiveTile()
    {
        // Instance z doby před Seed (přestavba stromů) má týž Key, ale už není v kolekci;
        // zápis do ní by se nikam neprojevil a persistoval by se špatný layout.
        var a = Exe("100");
        var svc = new ModuleShortcutsService();
        svc.Seed([a], [Menu("100")]);
        var stale = svc.Items[0];
        svc.Seed([a], [Menu("100")]);   // nové instance téhož klíče

        svc.PlaceAt(stale, 4, 1);

        Assert.Equal((4, 1), Cell(Assert.Single(svc.Items)));
    }

    [Fact]
    public void PlaceAt_Swap_ExchangesBothCoordinates_AndRaisesOnce()
    {
        var a = Exe("100"); var b = Exe("200");
        var svc = new ModuleShortcutsService();
        svc.Seed([a, b], [Menu("100", col: 0, row: 0), Menu("200", col: 3, row: 2)]);
        int raised = 0;
        svc.PositionsChanged += () => raised++;

        svc.PlaceAt(svc.Items[0], 3, 2);   // obsazeno b → swap

        Assert.Equal((3, 2), Cell(svc.Items[0]));
        Assert.Equal((0, 0), Cell(svc.Items[1]));
        Assert.Equal(1, raised);
    }

    [Fact]
    public void Add_AssignsLowestFreeCell()
    {
        var a = Exe("100"); var b = Exe("200"); var c = Exe("300");
        var svc = new ModuleShortcutsService();
        svc.Seed([a, b, c], []);

        svc.Add(a);   // (0,0)
        svc.Add(b);   // (1,0)
        svc.Remove(a);
        svc.Add(c);   // znovu nejnižší volná = (0,0)

        Assert.Equal((0, 0), Cell(svc.Items.Single(i => i.ElementId == "300")));
        Assert.Equal((1, 0), Cell(svc.Items.Single(i => i.ElementId == "200")));
    }

    [Fact]
    public void Add_WrapsToNextRow_AtGridColumns()
    {
        var a = Exe("100"); var b = Exe("200"); var c = Exe("300");
        var svc = new ModuleShortcutsService { GridColumns = 2 };
        svc.Seed([a, b, c], []);

        svc.Add(a); svc.Add(b); svc.Add(c);

        Assert.Equal([(0, 0), (1, 0), (0, 1)], svc.Items.Select(Cell));
    }

    [Fact]
    public void PlaceAt_MovesToEmptyCell_LeavingGap()
    {
        var a = Exe("100");
        var svc = new ModuleShortcutsService();
        svc.Seed([a], [Menu("100")]);   // (0,0)

        svc.PlaceAt(svc.Items[0], 4, 2);

        Assert.Equal((4, 2), Cell(svc.Items[0]));   // mezery zůstanou prázdné
    }

    [Fact]
    public void Resolve_FindsAcrossAllExecutables_NotJustPlacedOnes()
    {
        var a = Exe("100"); var b = Exe("200");
        var svc = new ModuleShortcutsService();
        svc.Seed([a, b], [Menu("100")]);

        Assert.Same(b, svc.Resolve("200"));
        Assert.Null(svc.Resolve("999"));
    }

    [Fact]
    public void RequestLaunch_RaisesWithTheShortcut()
    {
        var a = Exe("100");
        var svc = new ModuleShortcutsService();
        svc.Seed([a], [Menu("100")]);
        DesktopShortcut? launched = null;
        svc.LaunchRequested += s => launched = s;

        svc.RequestLaunch(svc.Items[0]);

        Assert.Same(svc.Items[0], launched);
        Assert.Same(a, launched!.Module);
    }
}
