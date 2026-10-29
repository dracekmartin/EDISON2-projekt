using EDControls;
using EDControls.EDViewModel;
using NSubstitute;
using TemplateDesigner.Controls;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Rendering;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Sdílená záložka Sloupce (`B194` E13c) - logika, kterou do 28. 10. měla každá ze čtyř tabulek
/// vlastní kopií: posun s pevnými řádky, hlídání součtu šířek, přizpůsobení a přidávání sloupců.
/// </summary>
public class ZalozkaSloupceTests
{
    public ZalozkaSloupceTests() =>
        EDServices.Initialize(new EDServices(Substitute.For<IMLRest>(),
            Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()));

    private sealed class Radek(string nazev, float? sirka = null, bool pevny = false, bool doSirky = true)
        : ColumnRowVM(CellStyleVM.FromModel(null, CellDefaults.Default), CellStyleVM.FromModel(null, CellDefaults.Default))
    {
        public override string DisplayName => nazev;
        public override bool CanMove => !pevny;
        public override bool CanHide => !pevny;
        public override bool CountsTowardWidth => doSirky;
        public Radek Sirka() { Width = sirka; return this; }
    }

    private static List<string> Poradi(ColumnsTabViewModel tab) => tab.Rows.Select(r => r.DisplayName).ToList();

    /// <summary>Pevný řádek na začátku (hodiny u Minut) nejde přeskočit - první skupina dnů výš nejde.</summary>
    [Fact]
    public void PevnyRadekNaZacatku_NejdePreskocit()
    {
        var tab = new ColumnsTabViewModel([new Radek("Hodiny", pevny: true), new Radek("PD"), new Radek("So")]);

        tab.SelectedRow = tab.Rows[1];
        tab.MoveUpCommand.Execute(null);
        Assert.Equal(["Hodiny", "PD", "So"], Poradi(tab));

        tab.SelectedRow = tab.Rows[0];
        tab.MoveDownCommand.Execute(null);
        Assert.Equal(["Hodiny", "PD", "So"], Poradi(tab));
    }

    /// <summary>Pevný řádek uprostřed (sloupec spojů) zůstane na svém místě a sousedé se kolem něj
    /// prohodí - jinak by šipka za spoji nešla přesunout před zastávku.</summary>
    [Fact]
    public void PevnyRadekUprostred_ZustaneNaMiste()
    {
        var tab = new ColumnsTabViewModel([new Radek("Zastávka"), new Radek("Spoje", pevny: true), new Radek("Šipka")]);

        tab.SelectedRow = tab.Rows[2];
        tab.MoveUpCommand.Execute(null);

        Assert.Equal(["Šipka", "Spoje", "Zastávka"], Poradi(tab));
        Assert.Equal("Šipka", tab.SelectedRow!.DisplayName);
    }

    /// <summary>Seznam v okně nesmí při posunu ani na okamžik držet týž řádek dvakrát - seznam by ztratil
    /// výběr a editor stylu pod ním by na chvíli zmizel (`B194h` H4; do 28. 10. se prohazovalo přiřazením).</summary>
    [Fact]
    public void Posun_KazdyRadekJeVSeznamuVzdyJednou()
    {
        var tab = new ColumnsTabViewModel([new Radek("Zastávka"), new Radek("Spoje", pevny: true), new Radek("Šipka")]);
        var zdvojeno = new List<string>();
        tab.Rows.CollectionChanged += (_, _) =>
        {
            if (tab.Rows.Distinct().Count() != tab.Rows.Count) zdvojeno.Add(string.Join(",", Poradi(tab)));
        };

        tab.SelectedRow = tab.Rows[0];
        tab.MoveDownCommand.Execute(null);

        Assert.Empty(zdvojeno);
        Assert.Equal(["Šipka", "Spoje", "Zastávka"], Poradi(tab));
        Assert.Equal("Zastávka", tab.SelectedRow!.DisplayName);
    }

    /// <summary>Do součtu šířek jdou jen viditelné sloupce, které se počítají do šířky prvku
    /// (spoje v Tabulce spojů ne); okno bez šířky prvku přesah nehlídá (Minuty).</summary>
    [Fact]
    public void Presah_PocitaJenViditelneSloupceDoSirky()
    {
        var tab = new ColumnsTabViewModel([new Radek("A", 30).Sirka(), new Radek("Spoje", 40, doSirky: false).Sirka(),
            new Radek("B", 30).Sirka()])
        { ElementWidth = () => 50f };

        Assert.Equal(60f, tab.TotalWidth);
        Assert.True(tab.HasColumnOverflow);

        tab.Rows[2].Visible = false;
        Assert.False(tab.HasColumnOverflow);

        var bezSirky = new ColumnsTabViewModel([new Radek("A", 300).Sirka()]);
        Assert.False(bezSirky.HasColumnOverflow);
        Assert.Equal("", bezSirky.ColumnOverflowText);
    }

    /// <summary>Přizpůsobit zmenší pevné šířky ve stejném poměru; automatické nechá automatické.</summary>
    [Fact]
    public void Prizpusobit_ZmensiPevneSirkyVePomeru()
    {
        var tab = new ColumnsTabViewModel([new Radek("A", 60).Sirka(), new Radek("B", 40).Sirka(), new Radek("Auto")])
        { ElementWidth = () => 50f, CanFit = true };

        Assert.True(tab.ShowFit);
        tab.FitCommand.Execute(null);

        Assert.Equal(30f, tab.Rows[0].Width);
        Assert.Equal(20f, tab.Rows[1].Width);
        Assert.Null(tab.Rows[2].Width);
        Assert.False(tab.HasColumnOverflow);
    }

    /// <summary>Procentní šířka sloupce tabulky z dat se do součtu přepočte na mm podle šířky prvku.</summary>
    [Fact]
    public void ProcentniSirka_SePrepocteNaMm()
    {
        var sloupec = DynamicTableColumnVM.FromModel(
            new TableColumn { Header = "Cena", Width = 50f, WidthMode = ColumnWidthMode.Percent }, new DynamicTableElement());
        var tab = new ColumnsTabViewModel([sloupec]) { ElementWidth = () => 80f };

        Assert.Equal("Šířka (%)", sloupec.WidthLabel);
        Assert.Equal(40f, tab.TotalWidth);
    }

    /// <summary>Přidat a Odebrat jsou jen tam, kde okno dodá nový sloupec (Tabulka z dat).</summary>
    [Fact]
    public void PridatOdebrat_JenSTovarnouNovehoSloupce()
    {
        var bez = new ColumnsTabViewModel([new Radek("A")]);
        Assert.False(bez.CanAddRemove);
        bez.RemoveCommand.Execute(null);
        Assert.Single(bez.Rows);

        var s = new ColumnsTabViewModel([new Radek("A")]) { NewRow = () => new Radek("Nový") };
        s.AddCommand.Execute(null);
        Assert.Equal(["A", "Nový"], Poradi(s));
        Assert.Equal("Nový", s.SelectedRow!.DisplayName);

        s.RemoveCommand.Execute(null);
        Assert.Equal(["A"], Poradi(s));
        Assert.Equal("A", s.SelectedRow!.DisplayName);
    }

    /// <summary>Hodiny se tisknou vždy a vždy první - zaškrtávátko ani šipky u nich nesmí nic slibovat.</summary>
    [Fact]
    public void Minuty_HodinyNejdeSkrytAniPosunout()
    {
        var prvek = new DepartureGridElement();
        var vm = new DepartureGroupEditorDialogViewModel(prvek, [], prvek.GroupColumns, prvek.HourColumnStyle,
            prvek.CustomColumns);

        var hodiny = Assert.IsType<DepartureGroupColumnVM>(vm.Columns.Rows[0]);
        Assert.True(hodiny.IsHourColumn);
        Assert.False(hodiny.CanHide);
        Assert.False(hodiny.CanMove);
    }

    /// <summary>Sloupec spojů Tabulky spojů viditelnost ani pořadí nečte, takže je nenabízí.</summary>
    [Fact]
    public void TabulkaSpoju_SloupecSpojuNejdeSkrytAniPosunout()
    {
        var vm = new LineTableColumnsEditorDialogViewModel(new LineTableElement(), LineTableElement.GetDefaultColumns());
        var spoje = vm.Columns.Rows.Cast<LineTableColumnVM>().Single(r => r.Key == LineTableElement.TripKey);

        Assert.False(spoje.CanHide);
        Assert.False(spoje.CanMove);
        Assert.False(spoje.CountsTowardWidth);
    }
}
