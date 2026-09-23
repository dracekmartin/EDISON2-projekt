using ALWA.SYS;
using EDControls.EDInputForm;

namespace EDISON2.IntegrationTests;

/// <summary>EDInputForm: skutečné kódování řádků GROUP / FIELD a round trip hodnot, triggeru a changedKey.</summary>
[Trait("Category", "Integration")]
public class InputFormTests
{
    private const string UserProfileClass = "ALVA.EDISONMAIN2.EDInputForm.UserProfile";

    // UserProfileWindow do těchto polí po Init doplňuje hodnoty a SetField neznámý klíč tiše přeskočí.
    private static readonly string[] HostPrefilledKeys =
        ["fullName", "loginName", "mlp", "usePersonDir", "personDir", "centralDir", "defaultWindow"];

    [DevServerFact]
    public void Init_UserProfile_ContainsFieldsTheHostPrefills()
    {
        var form = new EDInputFormController(DevServer.Connect(), UserProfileClass);

        form.Init();

        var keys = form.Items.OfType<Field>().Select(f => f.Key).ToList();
        Assert.All(HostPrefilledKeys, key => Assert.Contains(key, keys));
    }

    // ParseItems krátký řádek doplní defaulty a neznámý typ zahodí; posun pozic hlídá jen surový výstup.
    [DevServerFact]
    public void Init_RawRows_MatchParserLayout()
    {
        CVar raw = DevServer.Connect().QueryOrThrow(UserProfileClass, "Init", new CVar());

        Assert.Contains(raw, kvp => int.TryParse(kvp.Key, out _));
        foreach (KeyValuePair<string, CVar> kvp in raw)
        {
            if (!int.TryParse(kvp.Key, out _)) continue;
            CList row = kvp.Value;
            switch (row.GetString(1))
            {
                case "GROUP":
                    Assert.Equal(6, row.Length);
                    break;
                case "FIELD":
                    Assert.Equal(19, row.Length);
                    break;
                default:
                    Assert.Fail($"Řádek {kvp.Key}: neznámý typ '{row.GetString(1)}', ParseItems by ho zahodil.");
                    break;
            }
        }
    }

    // Pole s validateOnChange: hodnota, trigger a changedKey jdou na server a editovatelnost zpět.
    [DevServerFact]
    public void ValidateAndSave_TogglePersonDir_ServerDrivesEditabilityAndErrors()
    {
        var form = new EDInputFormController(DevServer.Connect(), UserProfileClass);
        form.Init();
        Assert.False(FieldOf(form, "personDir").Editable);

        FieldOf(form, "usePersonDir").Value = "1";
        form.Validate("usePersonDir");
        Assert.True(FieldOf(form, "personDir").Editable);

        // BeforeSave: zapnutý osobní adresář bez cesty server odmítne a Save se nezavolá.
        Assert.False(form.Save());
        Assert.NotNull(FieldOf(form, "personDir").ValidationError);

        FieldOf(form, "personDir").Value = @"C:\Export";
        Assert.True(form.Save());
    }

    private static Field FieldOf(EDInputFormController form, string key) =>
        form.Items.OfType<Field>().Single(f => f.Key == key);
}
