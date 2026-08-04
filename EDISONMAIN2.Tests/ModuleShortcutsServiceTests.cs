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
    private static DesktopShortcutDto Menu(string elementId, int? slot = null) =>
        new(nameof(ShortcutKind.MenuModule), elementId, elementId, "M", Slot: slot);

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
    public void Add_DoesNotReuseSlotOfHiddenItem()
    {
        // Skrytá položka (modul mimo strom) se ukládá se svým slotem; kdyby ho nová dlaždice
        // převzala, po návratu licence by obě seděly na téže pozici v mřížce.
        var a = Exe("100");
        var svc = new ModuleShortcutsService();
        svc.Seed([a], [Menu("999", slot: 0)]);

        svc.Add(a);

        Assert.Equal(1, Assert.Single(svc.Items).Slot);
    }

    [Fact]
    public void Seed_CollidingSlots_AreSeparated()
    {
        // Kolizi umí vyrobit ruční editace blobu i mix uloženého slotu s doplněným pořadím.
        var a = Exe("100"); var b = Exe("200");
        var svc = new ModuleShortcutsService();

        svc.Seed([a, b], [Menu("100", slot: 2), Menu("200", slot: 2)]);

        Assert.Equal([2, 3], svc.Items.Select(i => i.Slot));
    }

    [Fact]
    public void Seed_HiddenItemSlot_IsNotTakenByShownItem()
    {
        var a = Exe("100");
        var svc = new ModuleShortcutsService();

        svc.Seed([a], [Menu("999", slot: 0), Menu("100", slot: 0)]);

        Assert.Equal(1, Assert.Single(svc.Items).Slot);   // slot 0 drží skrytá "999"
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
    public void Seed_AssignsOrderSlots_WhenSlotMissing()
    {
        var a = Exe("100"); var b = Exe("200");
        var svc = new ModuleShortcutsService();

        svc.Seed([a, b], [Menu("200"), Menu("100")]);

        Assert.Equal(0, svc.Items[0].Slot);   // první v pořadí
        Assert.Equal(1, svc.Items[1].Slot);
    }

    [Fact]
    public void Seed_UsesPersistedSlots_WhenProvided()
    {
        var a = Exe("100"); var b = Exe("200");
        var svc = new ModuleShortcutsService();

        svc.Seed([a, b], [Menu("100", slot: 5), Menu("200", slot: 2)]);

        Assert.Equal(5, svc.Items[0].Slot);
        Assert.Equal(2, svc.Items[1].Slot);
    }

    [Fact]
    public void Seed_MissingSlotForOneItem_FallsBackToInsertionOrder()
    {
        // Uložený slot pro "100" je 3; u "200" chybí → spadne na pořadí (order=1).
        var a = Exe("100"); var b = Exe("200");
        var svc = new ModuleShortcutsService();

        svc.Seed([a, b], [Menu("100", slot: 3), Menu("200")]);

        Assert.Equal(3, svc.Items[0].Slot);
        Assert.Equal(1, svc.Items[1].Slot);
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
    public void PlaceAt_Guards_NoOp_DoNotRaiseSlotsChanged()
    {
        var a = Exe("100"); var b = Exe("200");
        var svc = new ModuleShortcutsService();
        svc.Seed([a, b], [Menu("100")]);   // a=slot 0, b není na ploše
        var orphan = DesktopShortcut.ForModule(b);
        var raised = false;
        svc.SlotsChanged += () => raised = true;

        svc.PlaceAt(svc.Items[0], -1);   // záporný slot
        svc.PlaceAt(orphan, 2);          // dlaždice mimo plochu
        svc.PlaceAt(svc.Items[0], 0);    // stejný slot

        Assert.Equal(0, svc.Items[0].Slot);
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

        svc.PlaceAt(stale, 4);

        Assert.Equal(4, Assert.Single(svc.Items).Slot);
    }

    [Fact]
    public void PlaceAt_Swap_RaisesSlotsChangedOnce()
    {
        var a = Exe("100"); var b = Exe("200");
        var svc = new ModuleShortcutsService();
        svc.Seed([a, b], [Menu("100"), Menu("200")]);   // a=0, b=1
        int raised = 0;
        svc.SlotsChanged += () => raised++;

        svc.PlaceAt(svc.Items[0], 1);   // obsazeno b → swap

        Assert.Equal(1, svc.Items[0].Slot);
        Assert.Equal(0, svc.Items[1].Slot);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void Add_AssignsLowestFreeSlot()
    {
        var a = Exe("100"); var b = Exe("200"); var c = Exe("300");
        var svc = new ModuleShortcutsService();
        svc.Seed([a, b, c], []);

        svc.Add(a);   // slot 0
        svc.Add(b);   // slot 1
        svc.Remove(a);
        svc.Add(c);   // znovu nejnižší volný = 0

        Assert.Equal(0, svc.Items.Single(i => i.ElementId == "300").Slot);
        Assert.Equal(1, svc.Items.Single(i => i.ElementId == "200").Slot);
    }

    [Fact]
    public void PlaceAt_MovesToEmptySlot_LeavingGap()
    {
        var a = Exe("100");
        var svc = new ModuleShortcutsService();
        svc.Seed([a], [Menu("100")]);   // slot 0

        svc.PlaceAt(svc.Items[0], 4);

        Assert.Equal(4, svc.Items[0].Slot);   // mezera 0–3
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
