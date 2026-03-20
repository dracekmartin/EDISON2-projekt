using ALWA.SYS;
using EDControls;
using EDControls.EDInputForm;
using NSubstitute;

namespace EDControls.Tests;

public class EDInputFormControllerTests
{
    private const string CacheClass = "TEST.InputForm";

    private static CVar FieldData(string key = "k", string value = "v",
        bool validateOnChange = false, bool required = false, string? error = null)
    {
        var data = new CVar();
        data["1"] = error != null
            ? new CList("FIELD", "1", key, "Label", "TEXT", value,
                validateOnChange ? "1" : "0", required ? "1" : "0", error)
            : new CList("FIELD", "1", key, "Label", "TEXT", value,
                validateOnChange ? "1" : "0", required ? "1" : "0");
        return data;
    }

    private static IMLRest MockMLRest(CVar initReturn, CVar? validateReturn = null, CVar? saveReturn = null)
    {
        var mock = Substitute.For<IMLRest>();
        mock.QueryOrThrow(CacheClass, "Init", Arg.Any<CVar>()).Returns(initReturn);
        mock.QueryOrThrow(CacheClass, "Validate", Arg.Any<CVar>())
            .Returns(validateReturn ?? initReturn);
        if (saveReturn != null)
            mock.QueryOrThrow(CacheClass, "Save", Arg.Any<CVar>()).Returns(saveReturn);
        return mock;
    }

    [Fact]
    public void Init_CallsInitThenValidate()
    {
        var mlRest = MockMLRest(FieldData());
        var controller = new EDInputFormController(mlRest, CacheClass);

        controller.Init();

        mlRest.Received(1).QueryOrThrow(CacheClass, "Init", Arg.Any<CVar>());
        mlRest.Received(1).QueryOrThrow(CacheClass, "Validate", Arg.Any<CVar>());
    }

    [Fact]
    public void Init_PopulatesItems()
    {
        var mlRest = MockMLRest(FieldData(key: "jmeno", value: "Jan"));
        var controller = new EDInputFormController(mlRest, CacheClass);

        controller.Init();

        var f = Assert.IsType<Field>(Assert.Single(controller.Items));
        Assert.Equal("jmeno", f.Key);
    }

    [Fact]
    public void Validate_ReturnsTrueWhenNoErrors()
    {
        var mlRest = MockMLRest(FieldData());
        var controller = new EDInputFormController(mlRest, CacheClass);
        controller.Init();

        bool result = controller.Validate();

        Assert.True(result);
    }

    [Fact]
    public void Validate_ReturnsFalseWhenFieldHasError()
    {
        var cleanData = FieldData();
        var errorData = FieldData(error: "Povinné pole");
        var mlRest = MockMLRest(cleanData, validateReturn: errorData);
        var controller = new EDInputFormController(mlRest, CacheClass);
        controller.Init();

        bool result = controller.Validate();

        Assert.False(result);
        var f = Assert.IsType<Field>(Assert.Single(controller.Items));
        Assert.Equal("Povinné pole", f.ValidationError);
    }

    [Fact]
    public void Save_ReturnsFalseWhenValidationFails()
    {
        var cleanData = FieldData();
        var errorData = FieldData(error: "Chyba");
        var mlRest = MockMLRest(cleanData, validateReturn: errorData);
        var controller = new EDInputFormController(mlRest, CacheClass);
        controller.Init();

        bool result = controller.Save();

        Assert.False(result);
        mlRest.DidNotReceive().QueryOrThrow(CacheClass, "Save", Arg.Any<CVar>());
    }

    [Fact]
    public void Save_CallsSaveWhenValidationPasses()
    {
        var data = FieldData();
        var mlRest = MockMLRest(data, saveReturn: data);
        var controller = new EDInputFormController(mlRest, CacheClass);
        controller.Init();

        bool result = controller.Save();

        Assert.True(result);
        mlRest.Received(1).QueryOrThrow(CacheClass, "Save", Arg.Any<CVar>());
    }

    [Fact]
    public void Init_BuildsFromScratch_DoesNotPreserveState()
    {
        var data = new CVar();
        data["1"] = new CList("GROUP", "1", "sekce", "Sekce");
        data["2"] = new CList("FIELD", "2", "k", "L", "TEXT", "", "0", "0");
        var mlRest = MockMLRest(data);
        var controller = new EDInputFormController(mlRest, CacheClass);

        controller.Init();
        var group = controller.Items.OfType<Group>().Single();
        group.IsExpanded = false; // sets Visibility = Collapsed

        // Re-init builds from scratch — server sends Visible, so group is expanded again
        controller.Init();
        var groupAfter = controller.Items.OfType<Group>().Single();
        Assert.True(groupAfter.IsExpanded);
    }

    [Fact]
    public void Validate_SendsTriggerInInput()
    {
        var data = FieldData();
        var mlRest = MockMLRest(data);
        var controller = new EDInputFormController(mlRest, CacheClass);
        controller.Init();

        controller.Validate();

        // The third Validate call (first two are from Init: init + validate)
        // should have "Change" trigger
        mlRest.Received().QueryOrThrow(CacheClass, "Validate",
            Arg.Is<CVar>(c => c["trigger"].Value != null && c["trigger"].Value.ToString() == "Change"));
    }

    [Fact]
    public void Validate_WithChangedKey_SendsChangedKeyInInput()
    {
        var data = FieldData();
        var mlRest = MockMLRest(data);
        var controller = new EDInputFormController(mlRest, CacheClass);
        controller.Init();

        controller.Validate("k");

        mlRest.Received().QueryOrThrow(CacheClass, "Validate",
            Arg.Is<CVar>(c =>
                c["trigger"].Value != null && c["trigger"].Value.ToString() == "Change" &&
                c["changedKey"].Value != null && c["changedKey"].Value.ToString() == "k"));
    }

    [Fact]
    public void Validate_WithoutChangedKey_DoesNotSendChangedKey()
    {
        var data = FieldData();
        var mlRest = MockMLRest(data);
        var controller = new EDInputFormController(mlRest, CacheClass);
        controller.Init();

        controller.Validate();

        mlRest.Received().QueryOrThrow(CacheClass, "Validate",
            Arg.Is<CVar>(c =>
                c["trigger"].Value != null && c["trigger"].Value.ToString() == "Change" &&
                (c["changedKey"].Value == null || c["changedKey"].Value.ToString() == "")));
    }

    [Fact]
    public void Init_DoesNotSendChangedKey()
    {
        var data = FieldData();
        var mlRest = MockMLRest(data);
        var controller = new EDInputFormController(mlRest, CacheClass);

        controller.Init();

        mlRest.Received().QueryOrThrow(CacheClass, "Validate",
            Arg.Is<CVar>(c =>
                c["trigger"].Value != null && c["trigger"].Value.ToString() == "Init" &&
                (c["changedKey"].Value == null || c["changedKey"].Value.ToString() == "")));
    }

    [Fact]
    public void Validate_ServerVisibilityIsAuthoritative()
    {
        var visibleData = new CVar();
        visibleData["1"] = new CList("FIELD", "1", "k", "L", "TEXT", "v", "0", "0");
        var hiddenData = new CVar();
        hiddenData["1"] = new CList("FIELD", "1", "k", "L", "TEXT", "v", "0", "0", "", "1", "3");
        var mlRest = MockMLRest(visibleData, validateReturn: hiddenData);
        var controller = new EDInputFormController(mlRest, CacheClass);

        controller.Init();

        // Validate — server returns ProgrammerHidden
        controller.Validate();
        var fieldAfter = controller.Items.OfType<Field>().Single();
        Assert.Equal(EItemVisibility.ProgrammerHidden, fieldAfter.Visibility);
    }
}
