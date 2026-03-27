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
    public void ButtonField_RoundTripsCorrectly()
    {
        var data = new CVar();
        data["1"] = new CList("FIELD", "1", "btn", "Label", "BUTTON", "Click me", "0", "0");
        var mlRest = MockMLRest(data);
        var controller = new EDInputFormController(mlRest, CacheClass);

        controller.Init();

        var f = Assert.IsType<Field>(Assert.Single(controller.Items));
        Assert.Equal("btn", f.Key);
        Assert.Equal(EFieldType.Button, f.FieldType);
        Assert.Equal("Click me", f.Value);
    }

    [Fact]
    public void ButtonField_ValidateWithKey_SendsChangedKey()
    {
        var data = new CVar();
        data["1"] = new CList("FIELD", "1", "btn", "Label", "BUTTON", "Click me", "0", "0");
        var mlRest = MockMLRest(data);
        var controller = new EDInputFormController(mlRest, CacheClass);
        controller.Init();

        controller.Validate("btn");

        mlRest.Received().QueryOrThrow(CacheClass, "Validate",
            Arg.Is<CVar>(c =>
                c["trigger"].Value != null && c["trigger"].Value.ToString() == "Change" &&
                c["changedKey"].Value != null && c["changedKey"].Value.ToString() == "btn"));
    }

    [Fact]
    public void ButtonField_SerializesWithButtonType()
    {
        var items = new List<Item>
        {
            new Field { Number = 1, Key = "btn", Label = "L", FieldType = EFieldType.Button, Value = "Go" }
        };

        var cVar = EDControls.EDInputForm.EDInputForm.SerializeItemsCVar(items);

        CList entry = cVar["1"];
        Assert.Equal("BUTTON", entry.GetString(5));
        Assert.Equal("Go", entry.GetString(6));
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

    // ─── HelpTextMode tests ─────────────────────────────────────────────────

    [Fact]
    public void Init_ReadsHelpTextModeFromInitResponse()
    {
        var data = FieldData();
        data["helpTextMode"] = new CVar("column");
        var mlRest = MockMLRest(data);
        var controller = new EDInputFormController(mlRest, CacheClass);

        controller.Init();

        Assert.Equal(EHelpTextMode.Column, controller.HelpTextMode);
    }

    [Fact]
    public void Init_DefaultsToTooltipWhenHelpTextModeMissing()
    {
        var mlRest = MockMLRest(FieldData());
        var controller = new EDInputFormController(mlRest, CacheClass);

        controller.Init();

        Assert.Equal(EHelpTextMode.Tooltip, controller.HelpTextMode);
    }

    [Fact]
    public void Validate_UpdatesHelpTextModeFromResponse()
    {
        var initData = FieldData();
        var columnData = FieldData();
        columnData["helpTextMode"] = new CVar("column");
        var mlRest = Substitute.For<IMLRest>();
        mlRest.QueryOrThrow(CacheClass, "Init", Arg.Any<CVar>()).Returns(initData);
        // First Validate (during Init) returns tooltip, second (explicit) returns column
        mlRest.QueryOrThrow(CacheClass, "Validate", Arg.Any<CVar>())
            .Returns(initData, columnData);
        var controller = new EDInputFormController(mlRest, CacheClass);
        controller.Init();
        Assert.Equal(EHelpTextMode.Tooltip, controller.HelpTextMode);

        controller.Validate();

        Assert.Equal(EHelpTextMode.Column, controller.HelpTextMode);
    }

    [Fact]
    public void Init_PreservesFieldHelpText()
    {
        var data = new CVar();
        data["1"] = new CList("FIELD", "1", "k", "L", "TEXT", "v", "0", "0", "", "1", "0", "Help text here");
        var mlRest = MockMLRest(data);
        var controller = new EDInputFormController(mlRest, CacheClass);

        controller.Init();

        var f = controller.Items.OfType<Field>().Single();
        Assert.Equal("Help text here", f.HelpText);
    }

    [Fact]
    public void ButtonClick_TogglesHelpTextMode()
    {
        // Init returns tooltip mode with a toggle button
        var initData = new CVar();
        initData["1"] = new CList("FIELD", "1", "toggleHelp", "Režim", "BUTTON", "Přepnout na sloupec", "0", "0");

        // First Validate (Init trigger) returns tooltip, second (button click) returns column
        var tooltipData = new CVar();
        tooltipData["1"] = new CList("FIELD", "1", "toggleHelp", "Režim", "BUTTON", "Přepnout na tooltip", "0", "0");
        tooltipData["helpTextMode"] = new CVar("tooltip");

        var columnData = new CVar();
        columnData["1"] = new CList("FIELD", "1", "toggleHelp", "Režim", "BUTTON", "Přepnout na tooltip", "0", "0");
        columnData["helpTextMode"] = new CVar("column");

        var mlRest = Substitute.For<IMLRest>();
        mlRest.QueryOrThrow(CacheClass, "Init", Arg.Any<CVar>()).Returns(initData);
        mlRest.QueryOrThrow(CacheClass, "Validate", Arg.Any<CVar>())
            .Returns(tooltipData, columnData);
        var controller = new EDInputFormController(mlRest, CacheClass);

        controller.Init();
        Assert.Equal(EHelpTextMode.Tooltip, controller.HelpTextMode);

        // Simulate button click — server toggles to column
        controller.Validate("toggleHelp");
        Assert.Equal(EHelpTextMode.Column, controller.HelpTextMode);
        Assert.Equal("Přepnout na tooltip", controller.Items.OfType<Field>().Single().Value);
    }
}
