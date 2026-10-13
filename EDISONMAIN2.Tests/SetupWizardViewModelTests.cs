using EDControls;
using EDControls.EDViewModel;
using EDISONMAIN2.Windows.SetupWindow;
using NSubstitute;

namespace EDISONMAIN2.Tests;

/// <summary>Setup wizard: uložit jde jen otestované připojení, zápis nese legacy paritu polí.</summary>
public class SetupWizardViewModelTests
{
    private readonly List<(string Server, int Port)> _probed = [];
    private readonly List<ServerSettings> _saved = [];
    private bool _probeResult = true;
    private Func<IReadOnlyList<string>> _databases = () => ["ALVA", "VS_CSAD"];
    private Exception? _saveThrows;

    private static ServerSettings Initial() => new()
    {
        Name = "CACHE", Host = "10.0.0.5", SuperPort = 51972, Port = 12345,
        Iris = true, Database = "VS_CSAD", IdFa = "FA",
    };

    private SetupWizardViewModel Create(ServerSettings? initial = null) =>
        new(new EDServices(null, Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()),
            initial ?? Initial(),
            (server, port) =>
            {
                _probed.Add((server, port));
                return _probeResult;
            },
            (_, _) => _databases(),
            settings =>
            {
                if (_saveThrows is not null) throw _saveThrows;
                _saved.Add(settings);
            });

    private SetupWizardViewModel Tested()
    {
        var vm = Create();
        vm.TestCommand.Execute(null);
        return vm;
    }

    [Fact]
    public void Open_PrefillsFromSettings_SaveLockedUntilTested()
    {
        var vm = Create();

        Assert.Equal(("CACHE", "10.0.0.5", "51972", "12345", true, "FA"),
                     (vm.Name, vm.Server, vm.SuperPort, vm.RestPort, vm.Iris, vm.Idfa));
        Assert.Equal(["VS_CSAD"], vm.Databases);
        Assert.Equal("VS_CSAD", vm.SelectedDatabase);
        Assert.False(vm.CanSave);
        Assert.False(vm.IsConnectionTested);
        Assert.False(vm.HasStatus);
    }

    [Fact]
    public void Open_WithoutSuperPortAndDatabase_LeavesThemEmpty()
    {
        var vm = Create(new ServerSettings { Host = "", Port = 57770 });

        Assert.Equal("", vm.SuperPort);
        Assert.Empty(vm.Databases);
        Assert.Null(vm.SelectedDatabase);
    }

    [Fact]
    public void Test_Succeeds_UnlocksSaveAndKeepsSelectedDatabase()
    {
        var vm = Tested();

        Assert.Equal([("10.0.0.5", 12345)], _probed);
        Assert.True(vm.CanSave);
        Assert.True(vm.IsConnectionTested);
        Assert.Equal(["ALVA", "VS_CSAD"], vm.Databases);
        Assert.Equal("VS_CSAD", vm.SelectedDatabase);
        Assert.Equal(("Připojení v pořádku.", false), (vm.StatusText, vm.StatusIsError));
    }

    [Fact]
    public void Test_ServerUnreachable_KeepsSaveLocked()
    {
        _probeResult = false;

        var vm = Tested();

        Assert.False(vm.CanSave);
        Assert.False(vm.IsConnectionTested);
        Assert.Equal(("Nelze se připojit, zkontrolujte parametry.", true), (vm.StatusText, vm.StatusIsError));
    }

    [Fact]
    public void Test_DatabaseListFails_KeepsSaveLocked()
    {
        _databases = () => throw new InvalidOperationException("timeout");

        var vm = Tested();

        Assert.False(vm.CanSave);
        Assert.False(vm.IsConnectionTested);
        Assert.Equal(("Seznam databází nelze načíst (timeout).", true), (vm.StatusText, vm.StatusIsError));
    }

    [Fact]
    public void Test_InvalidRestPort_DoesNotContactServer()
    {
        var vm = Create();
        vm.RestPort = "abc";

        vm.TestCommand.Execute(null);

        Assert.Empty(_probed);
        Assert.False(vm.CanSave);
        Assert.Equal(("Zadejte platný REST port.", true), (vm.StatusText, vm.StatusIsError));
    }

    [Theory]
    [InlineData(nameof(SetupWizardViewModel.Server))]
    [InlineData(nameof(SetupWizardViewModel.RestPort))]
    public void ConnectionChangedAfterTest_LocksSaveUntilTestedAgain(string field)
    {
        var vm = Tested();

        if (field == nameof(SetupWizardViewModel.Server)) vm.Server = "10.0.0.6";
        else vm.RestPort = "12346";

        Assert.False(vm.CanSave);
        Assert.False(vm.IsConnectionTested);
        Assert.False(vm.HasStatus);
    }

    [Fact]
    public void SuperPortChangedAfterTest_KeepsSaveUnlocked()
    {
        // Parita s oknem před převodem: superserver port shell nepoužívá, test připojení na něm nezávisí.
        var vm = Tested();

        vm.SuperPort = "51973";

        Assert.True(vm.CanSave);
    }

    [Fact]
    public void Save_AfterTest_SavesTrimmedSettingsAndCloses()
    {
        var vm = Create();
        vm.Server = " 10.0.0.5 ";
        vm.TestCommand.Execute(null);
        vm.Name = " NOVE ";
        vm.Idfa = " JINA_FA ";
        vm.SelectedDatabase = "ALVA";
        bool? closed = null;
        vm.RequestClose += result => closed = result;

        vm.SaveCommand.Execute(null);

        var saved = Assert.Single(_saved);
        Assert.Equal(("NOVE", "10.0.0.5", 51972, 12345, true, "ALVA", "JINA_FA"),
                     (saved.Name, saved.Host, saved.SuperPort, saved.Port, saved.Iris, saved.Database, saved.IdFa));
        Assert.True(closed);
    }

    [Fact]
    public void Save_BeforeTest_SavesNothing()
    {
        var vm = Create();

        vm.SaveCommand.Execute(null);

        Assert.Empty(_saved);
    }

    [Fact]
    public void Save_MissingIdfa_ShowsErrorAndStaysOpen()
    {
        var vm = Tested();
        vm.Idfa = " ";
        bool? closed = null;
        vm.RequestClose += result => closed = result;

        vm.SaveCommand.Execute(null);

        Assert.Empty(_saved);
        Assert.Null(closed);
        Assert.Equal(("Zadejte IDFa firmy.", true), (vm.StatusText, vm.StatusIsError));
        Assert.True(vm.CanSave);
    }

    [Fact]
    public void Save_WriteFails_ShowsErrorAndStaysOpen()
    {
        _saveThrows = new UnauthorizedAccessException("přístup odepřen");
        var vm = Tested();
        bool? closed = null;
        vm.RequestClose += result => closed = result;

        vm.SaveCommand.Execute(null);

        Assert.Null(closed);
        Assert.Equal(("Nastavení nelze uložit (přístup odepřen).", true), (vm.StatusText, vm.StatusIsError));
    }
}
