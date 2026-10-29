using EDControls;
using EDControls.EDViewModel;
using NSubstitute;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Windows;

/// <summary>Nastavení skupin dnů v okně Minut odjezdů (`B194d` E13f, E13g).</summary>
public class OknoMinutSkupinyTests
{
    public OknoMinutSkupinyTests() =>
        EDServices.Initialize(new EDServices(Substitute.For<IMLRest>(),
            Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()));

    private static SkupinaDnuData Skupina(string id) => new() { Id = id, Nadpis = id };

    private static DepartureGroupEditorDialogViewModel Okno(DepartureGridElement prvek, params string[] skupinyVDatech) =>
        new(prvek, [.. skupinyVDatech.Select(Skupina)], prvek.GroupColumns, prvek.HourColumnStyle, prvek.CustomColumns);

    private static DepartureGroupColumnVM Radek(DepartureGroupEditorDialogViewModel okno, string id) =>
        okno.Columns.Rows.Cast<DepartureGroupColumnVM>().Single(r => r.SkupinaDnuId == id);

    /// <summary>Neplatnou barvu kreslení přečte jako průhlednou: skupina by ztratila střídání a přebila
    /// i barvu prvku. Pole ji proto nepřijme, stejně jako barvy v editoru stylu.</summary>
    [Theory]
    [InlineData("nesmysl", "#FF112233")]
    [InlineData("#FF33{X}", "#FF112233")]
    [InlineData("#FFAABBCC", "#FFAABBCC")]
    [InlineData("{BARVA_LINKY}", "{BARVA_LINKY}")]
    [InlineData("", "")]
    public void BarvaStridaniRadku_NeplatnaSeNeprijme(string zadano, string ocekavano)
    {
        var prvek = new DepartureGridElement
        {
            GroupColumns = [new DepartureGroupColumnStyle { SkupinaDnuId = "PD", AlternatingRowColor = "#FF112233" }],
        };
        var radek = Radek(Okno(prvek, "PD"), "PD");

        radek.AlternatingRowColor = zadano;

        Assert.Equal(ocekavano, radek.AlternatingRowColor);
    }

    /// <summary>Chyba ve Vlastní definici sloupců je vidět jen na její záložce. OK z jiné záložky do 28. 10.
    /// nic neudělalo a neřeklo proč; okno teď na záložku přepne a vybere chybný sloupec (`B194d` E13h).</summary>
    [Theory]
    [InlineData("abc", "")]
    [InlineData("", "nesmysl")]
    public void ChybaVeVlastniDefinici_PrepneNaZalozkuAVybereSloupec(string znacky, string obdobi)
    {
        var prvek = new DepartureGridElement();
        var okno = Okno(prvek);
        var dobry = new CustomColumnVM { Id = "1", Header = "Pracovní dny" };
        var chybny = new CustomColumnVM { Id = "2", Header = "Prázdniny", IncludeMarks = znacky, Period = obdobi };
        okno.CustomColumnVMs.Add(dobry);
        okno.CustomColumnVMs.Add(chybny);
        okno.UseCustomColumns = true;
        okno.SelectedCustomVM = dobry;
        okno.SelectedTabIndex = 1;

        okno.OkCommand.Execute(null);

        Assert.NotNull(okno.CustomColumnsError);
        Assert.Equal(DepartureGroupEditorDialogViewModel.VlastniDefiniceTabIndex, okno.SelectedTabIndex);
        Assert.Same(chybny, okno.SelectedCustomVM);
    }

    private static DepartureGroupColumnStyle Styl(string id, string zahlavi) => new() { SkupinaDnuId = id, HeaderOverride = zahlavi };

    /// <summary>Okno nabízí jen skupiny z dat vybrané linky, šablona ale slouží víc linkám. Nastavení skupiny,
    /// kterou tahle linka nemá (sobota), musí OK přežít - do 28. 10. ho OK zahodilo (`B194d` E13g).</summary>
    [Fact]
    public void SkupinaMimoData_ZustaneVSablone()
    {
        var prvek = new DepartureGridElement { GroupColumns = [Styl("PD", "Pracovní dny"), Styl("SO", "Sobota!")] };
        var okno = Okno(prvek, "PD");

        Radek(okno, "PD").Header = "Po-Pá";
        okno.OkCommand.Execute(null);

        Assert.Equal("Sobota!", Assert.Single(okno.ResultGroupColumns, g => g.SkupinaDnuId == "SO").HeaderOverride);
        Assert.Equal("Po-Pá", Assert.Single(okno.ResultGroupColumns, g => g.SkupinaDnuId == "PD").HeaderOverride);
    }

    /// <summary>Pořadí skupin v šabloně je uživatelovo pořadí ▲▼ - skupina mimo data musí zůstat na svém místě,
    /// jinak by se u linky, která ji má, přeházely sloupce.</summary>
    [Fact]
    public void SkupinaMimoData_DrziSvePoradi()
    {
        var prvek = new DepartureGridElement { GroupColumns = [Styl("A", "a"), Styl("B", "b"), Styl("C", "c")] };
        var okno = Okno(prvek, "A", "C");

        Radek(okno, "A").Header = "a2";
        okno.OkCommand.Execute(null);

        Assert.Equal(["A", "B", "C"], okno.ResultGroupColumns.Select(g => g.SkupinaDnuId));
    }

    /// <summary>Výchozí nastavení podle pořadí (`__INDEX_n`, ze vzorů a importu) platí pro linky s jinými
    /// skupinami - OK ho nesmí nahradit nastavením jen pro skupiny vybrané linky.</summary>
    [Fact]
    public void NastaveniPodlePoradi_ZustaneVSablone()
    {
        var index0 = GroupColumnIndexResolver.IndexKeyPrefix + "0";
        var prvek = new DepartureGridElement { GroupColumns = [Styl(index0, "První")] };
        var okno = Okno(prvek, "PD", "SO");

        Radek(okno, "SO").Header = "Víkend";
        okno.OkCommand.Execute(null);

        Assert.Contains(okno.ResultGroupColumns, g => g.SkupinaDnuId == index0 && g.HeaderOverride == "První");
    }

    /// <summary>OK beze změny se skupinami v datech nesmí šablonu měnit (`B194d` E13d): okno pro skupiny bez
    /// uloženého nastavení vytvoří výchozí řádky a dřív je zapsalo.</summary>
    [Fact]
    public void OkBezeZmeny_SeSkupinamiVDatech_NeniKrok()
    {
        var prvek = new DepartureGridElement { GroupColumns = [Styl(GroupColumnIndexResolver.IndexKeyPrefix + "0", "První")] };
        var okno = Okno(prvek, "PD", "SO");
        var navrhar = new TemplateDesignerViewModel();

        okno.OkCommand.Execute(null);
        navrhar.ApplyDepartureGridEdit(prvek, null, okno.ResultGroupColumns, true, okno.ResultHourColumnStyle,
            okno.ResultHourColumnHeader, okno.ResultHourColumnWidth, okno.ResultCustomColumns);

        Assert.False(navrhar.CanUndo);
        Assert.False(navrhar.IsDirty);
    }
}
