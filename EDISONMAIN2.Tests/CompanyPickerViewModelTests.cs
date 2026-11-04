using EDControls;
using EDControls.EDViewModel;
using EDISONMAIN2.Windows.CompanyPickerWindow;
using NSubstitute;
using static ALVA.EDISONAPI.Database;

namespace EDISONMAIN2.Tests;

/// <summary>Výběr jiné firmy po odmítnuté licenci.</summary>
public class CompanyPickerViewModelTests
{
    private static readonly CompanyStruct Refused = new() { Id = "DIN", Name = "Dinarica", Database = "DIN" };
    private static readonly CompanyStruct Ji = new() { Id = "JI_DP", Name = "DP Jihlava", Database = "JI_DB" };
    private static readonly CompanyStruct Pi = new() { Id = "PI_DP", Name = "", Database = "PI_DB" };

    private static CompanyPickerViewModel Build(params CompanyStruct[] offered) =>
        new(new EDServices(null, Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()), Refused, offered);

    [Fact]
    public void Message_NamesRefusedCompany_LikeCompanySwitcher()
    {
        Assert.Equal("Ve firmě DIN - Dinarica byl překročen počet licencí. Vyberte jinou firmu.", Build(Ji).Message);
    }

    [Fact]
    public void Suggestions_IdAndName_FirstPreselected()
    {
        var vm = Build(Ji, Pi);

        Assert.Equal([new ListItem("JI_DP", "DP Jihlava"), new ListItem("PI_DP", "")], vm.Suggestions);
        Assert.Equal("JI_DP", vm.Picked?.Id);
    }

    [Fact]
    public void Ok_WithSelection_ClosesAsConfirmed_PickedIsSelected()
    {
        var vm = Build(Ji, Pi);
        vm.SelectedCompanyId = "PI_DP";
        bool? result = null;
        vm.RequestClose += r => result = r;

        vm.OkCommand.Execute(null);

        Assert.True(result);
        Assert.Equal("PI_DB", vm.Picked?.Database);
    }

    [Fact]
    public void Ok_WithoutSelection_DoesNotClose()
    {
        // Volající by jinak zakládal session v prázdné firmě.
        var vm = Build(Ji);
        vm.SelectedCompanyId = null;
        bool closed = false;
        vm.RequestClose += _ => closed = true;

        vm.OkCommand.Execute(null);

        Assert.False(closed);
        Assert.Null(vm.Picked);
    }
}
