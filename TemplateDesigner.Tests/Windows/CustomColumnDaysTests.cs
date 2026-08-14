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

    /// <summary>Sobota a neděle „X" neblokují - vyhláška zakazuje jen kombinaci s pracovními dny.</summary>
    [Fact]
    public void SobotaANedeleNechavajiPracovniDnyDostupne()
    {
        var vm = Sloupec();
        vm.So = true;
        vm.Ne = true;

        Assert.True(vm.LzePracovni);
    }

    /// <summary>⚠️ „Ne" a „Neděle a svátky" jdou zaškrtnout **zároveň** - zákaz kombinace `+` se `7`
    /// z vyhlášky platí pro značky u spoje, ne pro sadu dnů sloupce. Standardní sloupec
    /// „Neděle a svátky" má `7+` a vzory ho tak posílají; kdyby se ty dvě volby vylučovaly, nešel by
    /// z editoru složit vůbec (a otevřený vzor by měl obě zaslepené, takže by ho nešlo ani upravit).</summary>
    [Fact]
    public void NedeleASvatkyJdouZaroven()
    {
        var vm = Sloupec();
        vm.Ne = true;
        vm.Svatek = true;

        Assert.Equal("7+", vm.ToModel().Days);
    }

    [Fact]
    public void VzorNedeleASvatky_ZustaneStejny()
    {
        var vm = CustomColumnVM.FromModel(new DepartureColumnDefinition { Days = "7+" });

        Assert.True(vm.Ne);
        Assert.True(vm.Svatek);
        Assert.Equal("7+", vm.ToModel().Days);
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
