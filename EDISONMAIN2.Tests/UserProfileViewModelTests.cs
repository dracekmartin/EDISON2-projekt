using EDControls;
using EDControls.EDViewModel;
using EDISONMAIN2.Windows.UserProfileWindow;
using NSubstitute;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Workflow okna „Vlastnosti uživatele" bez WPF: plnění formuláře po Init schématu a OK
/// (server validace → existence složky → SaveUser → propagace adresáře do session).
/// </summary>
public class UserProfileViewModelTests
{
    private const char D = '~';

    private sealed class FakeForm : IUserProfileForm
    {
        public Dictionary<string, string> Fields { get; } = [];
        public bool ServerAccepts { get; set; } = true;
        /// <summary>Pořadí volání: <c>set:klíč</c> a <c>validate</c>.</summary>
        public List<string> Calls { get; } = [];

        public bool Save() => ServerAccepts;
        public void Validate() => Calls.Add("validate");
        public string? Get(string key) => Fields.GetValueOrDefault(key);

        public void Set(string key, string value)
        {
            Fields[key] = value;
            Calls.Add($"set:{key}");
        }
    }

    private static IMLRest Session()
    {
        var ml = Substitute.For<IMLRest>();
        ml.Delimiter.Returns(D);
        ml.FullName.Returns("Jan Novák");
        ml.LoginName.Returns("jan.novak");
        ml.Mlp.Returns("201");
        ml.QueryOrThrow("ALVA.EDISON.Main", "LoadUser", Arg.Any<object[]>())
            .Returns(new object[] { "", $"personDir{D}1{D}C:\\Export" });
        ml.QueryOrThrow("ALVA.EDISON.Main", "LoadSettings", Arg.Any<object[]>())
            .Returns(new object[] { "", $"system{D}export{D}\\\\srv\\central" });
        return ml;
    }

    private static (UserProfileViewModel Vm, FakeForm Form, EDServices Services) Create(
        IMLRest? session, bool directoryExists = true)
    {
        var services = new EDServices(session, Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>());
        var form = new FakeForm();
        var vm = new UserProfileViewModel(services, "Plán dopravy", _ => directoryExists) { Form = form };
        return (vm, form, services);
    }

    private static List<bool?> CloseRequests(UserProfileViewModel vm)
    {
        var requests = new List<bool?>();
        vm.RequestClose += requests.Add;
        return requests;
    }

    private static void AssertNoSave(IMLRest ml) =>
        ml.DidNotReceive().QueryOrThrow("ALVA.EDISON.Main", "SaveUser", Arg.Any<object[]>());

    [Fact]
    public void FillForm_FillsSessionAndServerValues_ThenRevalidates()
    {
        var (vm, form, _) = Create(Session());

        vm.FillForm();

        Assert.Equal("Jan Novák", form.Fields["fullName"]);
        Assert.Equal("jan.novak", form.Fields["loginName"]);
        Assert.Equal("201", form.Fields["mlp"]);
        Assert.Equal("1", form.Fields["usePersonDir"]);
        Assert.Equal("C:\\Export", form.Fields["personDir"]);
        Assert.Equal("\\\\srv\\central", form.Fields["centralDir"]);
        Assert.Equal("Plán dopravy", form.Fields["defaultWindow"]);
        // Re-validace až po naplnění - editovatelnost cesty se řídí přepínačem.
        Assert.Equal("validate", form.Calls[^1]);
        Assert.Single(form.Calls, "validate");
    }

    [Fact]
    public void FillForm_WithoutSession_LeavesFormEmpty()
    {
        var (vm, form, _) = Create(session: null);

        vm.FillForm();

        Assert.Empty(form.Calls);
    }

    [Fact]
    public void Ok_ExistingDir_SavesPropagatesAndCloses()
    {
        var ml = Session();
        var (vm, form, services) = Create(ml);
        form.Fields["usePersonDir"] = "1";
        form.Fields["personDir"] = "D:\\Nove";
        var closes = CloseRequests(vm);

        vm.OkCommand.Execute(null);

        ml.Received(1).QueryOrThrow("ALVA.EDISON.Main", "SaveUser",
            Arg.Is<object[]>(a => a.Length == 1 && (string)a[0] == $"personDir{D}1{D}D:\\Nove"));
        // Legacy moduly spuštěné odteď dostanou nový adresář (launch arg [16]).
        Assert.Equal("D:\\Nove", services.UserDirectory);
        Assert.Equal([true], closes);
    }

    [Fact]
    public void Ok_MissingDir_ShowsInfoAndKeepsDialogOpen()
    {
        var ml = Session();
        var (vm, form, services) = Create(ml, directoryExists: false);
        form.Fields["usePersonDir"] = "1";
        form.Fields["personDir"] = "X:\\Neni";
        services.UserDirectory = "C:\\Puvodni";
        var closes = CloseRequests(vm);

        vm.OkCommand.Execute(null);

        services.PopupDisplayer.Received(1).Info("Zadaný osobní adresář neexistuje.");
        AssertNoSave(ml);
        Assert.Equal("C:\\Puvodni", services.UserDirectory);
        Assert.Empty(closes);
    }

    [Fact]
    public void Ok_ServerValidationFails_NothingPersisted()
    {
        // Chybu BeforeSave ukazuje formulář sám - VM nic nehlásí ani neukládá.
        var ml = Session();
        var (vm, form, services) = Create(ml);
        form.ServerAccepts = false;
        services.UserDirectory = "C:\\Puvodni";
        var closes = CloseRequests(vm);

        vm.OkCommand.Execute(null);

        AssertNoSave(ml);
        services.PopupDisplayer.DidNotReceiveWithAnyArgs().Info(default!);
        services.PopupDisplayer.DidNotReceiveWithAnyArgs().Error(default!);
        Assert.Equal("C:\\Puvodni", services.UserDirectory);
        Assert.Empty(closes);
    }

    [Fact]
    public void Ok_SaveUserFails_KeepsSessionDirectoryAndDialogOpen()
    {
        // Neuložený adresář nesmí dostat legacy moduly (launch arg [16]); chybu ukáže příkaz.
        var ml = Session();
        var failure = new InvalidOperationException("SaveUser selhal");
        ml.QueryOrThrow("ALVA.EDISON.Main", "SaveUser", Arg.Any<object[]>()).Returns(_ => throw failure);
        var (vm, form, services) = Create(ml);
        form.Fields["usePersonDir"] = "1";
        form.Fields["personDir"] = "D:\\Nove";
        services.UserDirectory = "C:\\Puvodni";
        var closes = CloseRequests(vm);

        vm.OkCommand.Execute(null);

        services.PopupDisplayer.Received(1).Error(failure);
        Assert.Equal("C:\\Puvodni", services.UserDirectory);
        Assert.Empty(closes);
    }

    [Fact]
    public void Ok_PersonalDirDisabled_SavesWithoutExistenceCheck()
    {
        var ml = Session();
        var (vm, form, services) = Create(ml, directoryExists: false);
        form.Fields["usePersonDir"] = "0";
        form.Fields["personDir"] = "";
        var closes = CloseRequests(vm);

        vm.OkCommand.Execute(null);

        ml.Received(1).QueryOrThrow("ALVA.EDISON.Main", "SaveUser",
            Arg.Is<object[]>(a => (string)a[0] == $"personDir{D}0{D}"));
        Assert.Equal("", services.UserDirectory);
        Assert.Equal([true], closes);
    }

    [Theory]
    [InlineData(true, false, false)]   // osobní adresář zapnutý a neexistuje → blokovat
    [InlineData(true, true, true)]
    [InlineData(false, false, true)]   // vypnutý přepínač existenci neřeší
    public void ValidateExportDir_BlocksSaveOnlyForMissingEnabledDir(bool use, bool exists, bool savable)
    {
        var (vm, _, _) = Create(Session(), directoryExists: exists);

        var error = vm.ValidateExportDir(use, @"C:\Export");

        Assert.Equal(savable, error is null);
        if (!savable) Assert.Equal("Zadaný osobní adresář neexistuje.", error);
    }
}
