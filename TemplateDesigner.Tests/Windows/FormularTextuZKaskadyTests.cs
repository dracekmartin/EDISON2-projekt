using EDControls.EDInputForm;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Formulář textového prvku musí u nenastavené vlastnosti stylu ukázat hodnotu, se kterou se prvek
/// vytiskne, ne výchozí hodnotu formuláře. Text z importu PDF nemá svislé zarovnání: tiskl se
/// „Nahoře", formulář ukazoval „Na střed".
/// Cesta jako v panelu i v okně vlastností: zděděné hodnoty → <see cref="ElementFormBinder.Prefill(List{Field}, object)"/>.
/// </summary>
public class FormularTextuZKaskadyTests
{
    /// <summary>Pole formuláře textu, jak je vydá server, i s výchozími hodnotami z `TextElementEdit.cls`.</summary>
    private static List<Field> TextForm() =>
    [
        new Field { Key = "Style.HAlign",     Label = "Zarovnání vodorovně", FieldType = FieldType.List, Value = "Center" },
        new Field { Key = "Style.VAlign",     Label = "Zarovnání svisle",    FieldType = FieldType.List, Value = "Middle" },
        new Field { Key = "Style.FontSize",   Label = "Velikost",            FieldType = FieldType.Decimal, Value = "10" },
    ];

    private static List<Field> OpenForm(TemplateElement element)
    {
        var fields = TextForm();
        ElementFormBinder.ApplyInheritedValues(fields, ElementFormBinder.InheritedValuesFor(element));
        ElementFormBinder.Prefill(fields, element);
        return fields;
    }

    [Fact]
    public void TextZImportuPdf_UkazeZarovnaniSeKterymSeTiskne()
    {
        // Tak ho staví import PDF (`PdfToTemplateConverter`): vodorovné zarovnání ano, svislé ne.
        var text = new TextElement { Text = "Platí od 1. 12.", Style = new TextStyle { HAlign = TextHorizontalAlignment.Left, FontSize = 9f } };

        var fields = OpenForm(text);

        Assert.Equal(text.Style.Resolve().VAlign.ToString(), fields.Single(f => f.Key == "Style.VAlign").Value);
        Assert.Equal("Top", fields.Single(f => f.Key == "Style.VAlign").Value);
        Assert.Equal("Left", fields.Single(f => f.Key == "Style.HAlign").Value);
        Assert.Equal("9", fields.Single(f => f.Key == "Style.FontSize").Value);
    }

    /// <summary>Panel si zděděné hodnoty buňky pevné tabulky nesmí přenést k dalšímu prvku - u prvku
    /// bez kaskády stylu zůstanou výchozí hodnoty formuláře.</summary>
    [Fact]
    public void PrvekBezKaskadyStylu_NemaZdedeneHodnoty()
    {
        Assert.Null(ElementFormBinder.InheritedValuesFor(new LineElement()));
    }
}
