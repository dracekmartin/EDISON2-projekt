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
        group.IsExpanded = false; // nastaví Visibility = Collapsed

        // Re-init staví od nuly — server pošle Visible, skupina je opět rozbalená
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

        // Třetí Validate volání (první dvě byly z Init: init + validate)
        // má mít trigger "Change"
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

        // Validate — server vrací ProgrammerHidden
        controller.Validate();
        var fieldAfter = controller.Items.OfType<Field>().Single();
        Assert.Equal(EItemVisibility.ProgrammerHidden, fieldAfter.Visibility);
    }

    // ─── HelpTextMode testy ─────────────────────────────────────────────────

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
        // První Validate (z Init) vrátí tooltip, druhé (explicitní) vrátí column
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
        // Init vrátí tooltip mode s toggle tlačítkem
        var initData = new CVar();
        initData["1"] = new CList("FIELD", "1", "toggleHelp", "Režim", "BUTTON", "Přepnout na sloupec", "0", "0");

        // První Validate (Init trigger) vrátí tooltip, druhé (button click) vrátí column
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

        // Simuluj klik na tlačítko — server přepne na column
        controller.Validate("toggleHelp");
        Assert.Equal(EHelpTextMode.Column, controller.HelpTextMode);
        Assert.Equal("Přepnout na tooltip", controller.Items.OfType<Field>().Single().Value);
    }

    // ─── FormatValidationErrors testy ─────────────────────────────────────────

    [Fact]
    public void FormatValidationErrors_NoErrors_ReturnsNull()
    {
        var items = new List<Item>
        {
            new Field { Label = "Jméno", ValidationError = null },
            new Field { Label = "Věk", ValidationError = null },
        };

        Assert.Null(EDInputFormController.FormatValidationErrors(items));
    }

    [Fact]
    public void FormatValidationErrors_SingleError_ReturnsFormattedMessage()
    {
        var items = new List<Item>
        {
            new Field { Label = "Jméno", ValidationError = "Povinné pole" },
        };

        string? result = EDInputFormController.FormatValidationErrors(items);

        Assert.NotNull(result);
        Assert.Contains("Jméno", result);
        Assert.Contains("Povinné pole", result);
        Assert.StartsWith("Formulář obsahuje chyby:", result);
    }

    [Fact]
    public void FormatValidationErrors_MultipleErrors_ListsOnlyErrorFields()
    {
        var items = new List<Item>
        {
            new Field { Label = "Jméno", ValidationError = "Povinné pole" },
            new Field { Label = "Věk", ValidationError = null },
            new Field { Label = "Email", ValidationError = "Neplatný formát" },
        };

        string? result = EDInputFormController.FormatValidationErrors(items);

        Assert.NotNull(result);
        Assert.Contains("Jméno — Povinné pole", result);
        Assert.Contains("Email — Neplatný formát", result);
        Assert.DoesNotContain("Věk", result);
    }

    [Fact]
    public void FormatValidationErrors_SkipsGroupItems()
    {
        var items = new List<Item>
        {
            new Group { GroupName = "Sekce" },
            new Field { Label = "Pole", ValidationError = "Chyba" },
        };

        string? result = EDInputFormController.FormatValidationErrors(items);

        Assert.NotNull(result);
        Assert.DoesNotContain("Sekce", result);
        Assert.Contains("Pole — Chyba", result);
    }

    [Fact]
    public void FormatValidationErrors_EmptyList_ReturnsNull()
    {
        Assert.Null(EDInputFormController.FormatValidationErrors(new List<Item>()));
    }

    // ─── Required validation testy ────────────────────────────────────────────

    [Fact]
    public void Validate_RequiredEmptyField_SetsErrorAndReturnsFalse()
    {
        var data = FieldData(value: "", required: true);
        var mlRest = MockMLRest(data);
        var controller = new EDInputFormController(mlRest, CacheClass);
        controller.Init();

        bool result = controller.Validate();

        Assert.False(result);
        var f = Assert.IsType<Field>(Assert.Single(controller.Items));
        Assert.Equal("Povinné pole", f.ValidationError);
    }

    [Fact]
    public void Validate_RequiredFilledField_NoError()
    {
        var data = FieldData(value: "Jan", required: true);
        var mlRest = MockMLRest(data);
        var controller = new EDInputFormController(mlRest, CacheClass);
        controller.Init();

        bool result = controller.Validate();

        Assert.True(result);
        var f = Assert.IsType<Field>(Assert.Single(controller.Items));
        Assert.Null(f.ValidationError);
    }

    [Fact]
    public void Validate_RequiredFieldWithProgrammerError_ProgrammerErrorPreserved()
    {
        var data = FieldData(value: "", required: true, error: "Vyplňte celé jméno");
        var mlRest = MockMLRest(data);
        var controller = new EDInputFormController(mlRest, CacheClass);
        controller.Init();

        controller.Validate();

        var f = Assert.IsType<Field>(Assert.Single(controller.Items));
        Assert.Equal("Vyplňte celé jméno", f.ValidationError);
    }

    [Fact]
    public void Save_RequiredEmptyField_ReturnsFalseAndDoesNotCallSave()
    {
        var data = FieldData(value: "", required: true);
        var mlRest = MockMLRest(data);
        var controller = new EDInputFormController(mlRest, CacheClass);
        controller.Init();

        bool result = controller.Save();

        Assert.False(result);
        mlRest.DidNotReceive().QueryOrThrow(CacheClass, "Save", Arg.Any<CVar>());
    }

    [Fact]
    public void Validate_RequiredFieldBecomesFilled_ErrorClears()
    {
        var emptyData = FieldData(value: "", required: true);
        var filledData = FieldData(value: "Jan", required: true);
        var mlRest = Substitute.For<IMLRest>();
        mlRest.QueryOrThrow(CacheClass, "Init", Arg.Any<CVar>()).Returns(emptyData);
        mlRest.QueryOrThrow(CacheClass, "Validate", Arg.Any<CVar>())
            .Returns(emptyData, emptyData, filledData);
        var controller = new EDInputFormController(mlRest, CacheClass);
        controller.Init();

        // První Validate — pole je prázdné → required error
        controller.Validate();
        Assert.Equal("Povinné pole", controller.Items.OfType<Field>().Single().ValidationError);

        // Druhý Validate — pole je vyplněné → bez chyby
        controller.Validate();
        Assert.Null(controller.Items.OfType<Field>().Single().ValidationError);
    }
}
