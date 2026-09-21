using EDISONMAIN2.Windows.ConnectionWindow;
using NSubstitute;
using static EDISONMAIN2.Tests.EdisonMainViewModelHarness;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Dialog „Připojit k databázi": výchozí připojení (EDISONini.xml) je první a neodebíratelné,
/// uložená se persistují hned při přidání/odebrání, OK bez výběru okno nezavře.
/// </summary>
public class ConnectionViewModelTests
{
    private static readonly ConnectionChoice Default = new("Výchozí", "srv", 57770, IsDefault: true);

    private static ILocalPrefs Prefs(params SavedConnection[] saved)
    {
        var prefs = Substitute.For<ILocalPrefs>();
        prefs.Connections = saved.Length == 0 ? null : [.. saved];
        return prefs;
    }

    private static ConnectionViewModel CreateVm(ILocalPrefs prefs) => new(MockServices(), prefs, Default);

    [Fact]
    public void Ctor_DefaultFirstThenSaved_DefaultSelected()
    {
        var vm = CreateVm(Prefs(new SavedConnection { Name = "Test", Server = "10.0.0.5", RestPort = 1 }));

        Assert.Equal(2, vm.Choices.Count);
        Assert.True(vm.Choices[0].IsDefault);
        Assert.Equal("Test", vm.Choices[1].Name);
        Assert.Equal("10.0.0.5:1", vm.Choices[1].Address);
        Assert.Same(vm.Choices[0], vm.Selected);
        Assert.False(vm.CanRemove);
    }

    [Fact]
    public void CanRemove_TrueOnlyForSavedConnection()
    {
        var vm = CreateVm(Prefs(new SavedConnection { Name = "Test", Server = "s", RestPort = 1 }));

        vm.Selected = vm.Choices[1];
        Assert.True(vm.CanRemove);

        vm.Selected = vm.Choices[0];
        Assert.False(vm.CanRemove);
    }

    [Fact]
    public void Add_Valid_AppendsSelectsPersistsAndClearsFields()
    {
        var prefs = Prefs();
        var vm = CreateVm(prefs);
        vm.NewName = " Test ";
        vm.NewServer = " 10.0.0.5 ";
        vm.NewRestPort = "57770";

        vm.AddCommand.Execute(null);

        var added = Assert.Single(vm.Choices, c => !c.IsDefault);
        Assert.Equal(new ConnectionChoice("Test", "10.0.0.5", 57770, IsDefault: false), added);
        Assert.Same(added, vm.Selected);
        var saved = Assert.Single(prefs.Connections!);
        Assert.Equal(("Test", "10.0.0.5", 57770), (saved.Name, saved.Server, saved.RestPort));
        prefs.Received(1).Save();
        Assert.Equal("", vm.NewName);
        Assert.Equal("", vm.NewServer);
        Assert.Equal("", vm.NewRestPort);
        Assert.False(vm.HasValidationMessage);
    }

    [Theory]
    [InlineData("", "s", "1", "název")]
    [InlineData("n", "", "1", "server")]
    [InlineData("n", "s", "abc", "REST port")]
    [InlineData("n", "s", "0", "REST port")]
    public void Add_Invalid_ShowsMessageAndDoesNotPersist(string name, string server, string port, string expectedWord)
    {
        var prefs = Prefs();
        var vm = CreateVm(prefs);
        vm.NewName = name;
        vm.NewServer = server;
        vm.NewRestPort = port;

        vm.AddCommand.Execute(null);

        Assert.Single(vm.Choices);
        Assert.True(vm.HasValidationMessage);
        Assert.Contains(expectedWord, vm.ValidationMessage);
        prefs.DidNotReceive().Save();
    }

    [Fact]
    public void Delete_Saved_RemovesPersistsAndSelectsDefault()
    {
        var prefs = Prefs(new SavedConnection { Name = "A", Server = "a", RestPort = 1 },
                          new SavedConnection { Name = "B", Server = "b", RestPort = 2 });
        var vm = CreateVm(prefs);
        vm.Selected = vm.Choices[1];

        vm.DeleteCommand.Execute(null);

        Assert.Equal(["Výchozí", "B"], vm.Choices.Select(c => c.Name));
        Assert.Equal(["B"], prefs.Connections!.Select(c => c.Name));
        prefs.Received(1).Save();
        Assert.Same(vm.Choices[0], vm.Selected);
    }

    [Fact]
    public void Delete_Default_NoOp()
    {
        var prefs = Prefs(new SavedConnection { Name = "A", Server = "a", RestPort = 1 });
        var vm = CreateVm(prefs);

        vm.DeleteCommand.Execute(null);

        Assert.Equal(2, vm.Choices.Count);
        prefs.DidNotReceive().Save();
    }

    [Fact]
    public void Ok_WithoutSelection_DoesNotClose()
    {
        var vm = CreateVm(Prefs());
        var results = new List<bool?>();
        vm.RequestClose += results.Add;

        vm.Selected = null;
        vm.OkCommand.Execute(null);
        Assert.Empty(results);

        vm.Selected = vm.Choices[0];
        vm.OkCommand.Execute(null);
        Assert.Equal([true], results);
    }
}
