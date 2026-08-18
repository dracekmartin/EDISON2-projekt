using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Zadávání dnů vlastního sloupce v abecedě vyhlášky 122/2014 (`B82`). Vyhláška zakazuje kombinaci
/// „X" (pracovní dny) s jednotlivými pracovními dny a „+" (neděle a svátky) s nedělí; editor je
/// proto nemá nabídnout zároveň - zákaz vyplývá z ovládání, ne z hlášky po OK
/// (rozhodnutí Jan 14. 8.).
/// </summary>
public class CustomColumnDaysTests
{
    private static CustomColumnVM Sloupec() => CustomColumnVM.FromModel(new DepartureColumnDefinition());

    [Fact]
    public void PracovniDny_SeZapisujiJakoX()
    {
        var vm = Sloupec();
        vm.Pracovni = true;

        Assert.Equal("X", vm.ToModel().Days);
    }

    /// <summary>„X" je samostatná kategorie, ne zkratka - proto se nerozepisuje na 1-5.</summary>
    [Fact]
    public void PracovniDny_SeNerozepisujiNaJednotliveDny()
    {
        var vm = Sloupec();
        vm.Pracovni = true;
        vm.So = true;

        Assert.Equal("X6", vm.ToModel().Days);   // legální kombinace: vyhláška zakazuje jen X s 1-5
    }

    [Fact]
    public void ZaskrtnutePondeli_ZaslepiPracovniDny()
    {
        var vm = Sloupec();
        Assert.True(vm.LzePracovni);

        vm.Po = true;

        Assert.False(vm.LzePracovni);
    }

    [Fact]
    public void ZaskrtnuteX_ZaslepiPoAzPa()
    {
        var vm = Sloupec();
        Assert.True(vm.LzePoPa);

        vm.Pracovni = true;

        Assert.False(vm.LzePoPa);
    }

    /// <summary>Sobota a neděle „X" neblokují - vyhláška zakazuje jen kombinaci s pracovními dny,
    /// takže víkendový sloupec „So+Ne" jde s „X" složit dohromady.</summary>
    [Fact]
    public void SobotaANedeleNechavajiPracovniDnyDostupne()
    {
        var vm = Sloupec();
        vm.So = true;
        vm.Ne = true;

        Assert.True(vm.LzePracovni);
        Assert.Equal("67", vm.ToModel().Days);
    }

    /// <summary>
    /// „Ne" a „Neděle a svátky" se **vylučují** - stejně jako „X" s Po-Pá (rozhodnutí Jan 17. 8.).
    /// <para>
    /// Dřív šly zaškrtnout zároveň s odůvodněním, že vzor „Neděle a svátky" posílá `7+`. To už
    /// neplatí: od `B82` posílá samotné `+` a server si „7" doplní sám, takže dvojice nic nepřidá -
    /// ověřeno během nad linkou 0330530, kde sloupce `7`, `+` i `7+` daly identický výstup.
    /// </para>
    /// </summary>
    [Fact]
    public void NedeleASvatky_SeVylucuji()
    {
        var vm = Sloupec();
        Assert.True(vm.LzeNe);
        Assert.True(vm.LzeSvatek);

        vm.Svatek = true;
        Assert.False(vm.LzeNe);

        vm.Svatek = false;
        vm.Ne = true;
        Assert.False(vm.LzeSvatek);
    }

    /// <summary>Starší uložený sloupec s `7+` se načte jako samotné „+". Je to **záměrné
    /// znormalizování** starého tvaru - dialog by jinak otevřel stav, který sám zakazuje.</summary>
    [Fact]
    public void StaryTvarSedmPlus_SeNacteJakoPlus()
    {
        var vm = CustomColumnVM.FromModel(new DepartureColumnDefinition { Days = "7+" });

        Assert.False(vm.Ne);
        Assert.True(vm.Svatek);
        Assert.Equal("+", vm.ToModel().Days);
    }

    /// <summary>Dnešní tvary se otevřením a zavřením měnit nesmí - normalizace se smí dotknout
    /// jen toho starého `7+`.</summary>
    [Theory]
    [InlineData("+")]
    [InlineData("7")]
    [InlineData("6")]
    [InlineData("X6")]
    public void OtevritAZavrit_DefiniciNemeni(string dny)
    {
        var vm = CustomColumnVM.FromModel(new DepartureColumnDefinition { Days = dny });

        Assert.Equal(dny, vm.ToModel().Days);
    }

    /// <summary>Načtení uložené definice musí „X" poznat, jinak by se sloupec po otevření dialogu
    /// tiše překlopil na prázdné dny.</summary>
    [Fact]
    public void NacteniDefinice_PoznaX()
    {
        var vm = CustomColumnVM.FromModel(new DepartureColumnDefinition { Days = "X6" });

        Assert.True(vm.Pracovni);
        Assert.True(vm.So);
        Assert.False(vm.Po);
        Assert.Equal("X6", vm.ToModel().Days);   // otevřít a zavřít beze změny nesmí definici změnit
    }
}
