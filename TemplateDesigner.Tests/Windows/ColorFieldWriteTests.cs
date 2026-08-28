using System.Collections.Generic;
using System.Linq;
using EDControls.EDInputForm;
using TemplateDesigner;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Vymazání nebo překlep v HEX poli se dřív uložily do šablony jako nesmyslná hodnota a kreslení
/// na ní padalo (i po znovuotevření šablony). Kreslení je teď fail-soft (<c>ColorHex</c>), tady se
/// hlídá druhá vrstva: z formuláře se do modelu nesmyslná barva vůbec nedostane, a vymazání má
/// srozumitelný význam - u zděditelné barvy „dědit", u ostatních „nic nekreslit".
/// </summary>
public class ColorFieldWriteTests
{
    private static List<Field> ColorField(string key, string? value) =>
    [
        new Field { Key = key, Label = "Barva", FieldType = FieldType.Color, Value = value }
    ];

    private static void Apply(List<Field> fields, object target, string key, string original)
    {
        var preFilled = new Dictionary<string, string> { [key] = original };
        foreach (var cmd in ElementFormBinder.ExtractCommands(fields, target, preFilled))
            cmd.Execute();
    }

    [Theory]
    [InlineData("#ZZZZZZ")]
    [InlineData("blbost")]
    [InlineData("#12345")]
    public void NeplatnaBarva_SeDoModeluNezapise(string typed)
    {
        var element = new TextElement { BorderColor = "#FF000000" };

        Apply(ColorField("BorderColor", typed), element, "BorderColor", "#FF000000");

        Assert.Equal("#FF000000", element.BorderColor);   // zůstala poslední platná
    }

    [Fact]
    public void VymazaniZdeditelneBarvy_ZnamenaDedit()
    {
        // Style.TextColor je nullable (null = zdědit z dokumentu/rodiče).
        var element = new TextElement { Style = new TextStyle { TextColor = "#FFFF0000" } };

        Apply(ColorField("Style.TextColor", ""), element, "Style.TextColor", "#FFFF0000");

        Assert.Null(element.Style.TextColor);
    }

    [Fact]
    public void VymazaniNezdeditelneBarvy_ZnamenaNicNekreslit()
    {
        // BorderColor není nullable - prázdný string by byl v šabloně uložený nesmysl.
        var element = new TextElement { BorderColor = "#FF000000" };

        Apply(ColorField("BorderColor", ""), element, "BorderColor", "#FF000000");

        Assert.Equal(ColorHex.Transparent, element.BorderColor);
        Assert.Equal(SkiaSharp.SKColors.Transparent, element.SkBorderColor);
    }

    [Fact]
    public void PlatnaBarva_SeZapisePorad()
    {
        var element = new TextElement { BorderColor = "#FF000000" };

        Apply(ColorField("BorderColor", "#FF3366CC"), element, "BorderColor", "#FF000000");

        Assert.Equal("#FF3366CC", element.BorderColor);
    }

    [Fact]
    public void VymazaniAZpetkaJdeVratitPresUndo()
    {
        var element = new TextElement { Style = new TextStyle { TextColor = "#FFFF0000" } };
        var fields = ColorField("Style.TextColor", "");
        var preFilled = new Dictionary<string, string> { ["Style.TextColor"] = "#FFFF0000" };

        var commands = ElementFormBinder.ExtractCommands(fields, element, preFilled);
        Assert.Single(commands);

        commands[0].Execute();
        Assert.Null(element.Style.TextColor);

        commands[0].Undo();
        Assert.Equal("#FFFF0000", element.Style.TextColor);
    }

    [Fact]
    public void NeplatnaBarva_NevytvoriAniPrikaz()
    {
        // Bez tohoto by v historii undo visel krok, který nic nezměnil.
        var element = new TextElement { BorderColor = "#FF000000" };
        var fields = ColorField("BorderColor", "#ZZZZZZ");
        var preFilled = new Dictionary<string, string> { ["BorderColor"] = "#FF000000" };

        Assert.Empty(ElementFormBinder.ExtractCommands(fields, element, preFilled));
    }

    [Fact]
    public void NebarevnaPoleZustavajiBezeZmeny()
    {
        // Kontrola se smí týkat jen barevných polí - textové pole může být klidně prázdné.
        var element = new TextElement { Text = "Ahoj" };
        List<Field> fields = [new Field { Key = "Text", Label = "Text", FieldType = FieldType.Text, Value = "" }];

        Apply(fields, element, "Text", "Ahoj");

        Assert.Equal("", element.Text);
    }
}
