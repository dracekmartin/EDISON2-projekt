using System.Collections.Generic;
using EDControls.EDInputForm;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Nové pole ve formuláři jde na server hned, ale do programu až s vydáním - starší program tedy
/// dostane schéma s polem, ke kterému nemá vlastnost. Když je takové pole povinné, nešlo formulář
/// vůbec potvrdit (prefill nemá co načíst, hodnota se stejně zahodí) a uživatel nemohl uložit ani
/// zbytek. Hlásil to kolega 13. 8. u „Rozestupu odjezdů".
/// </summary>
public class NeznamePolePrefillTests
{
    private static List<Field> Required(string key) =>
    [
        new Field { Key = key, Label = key, FieldType = FieldType.Text, Required = true }
    ];

    [Fact]
    public void PovinnePoleBezVlastnosti_PrestaneBytPovinne()
    {
        var element = new TextElement();
        var fields = Required("PoleZBudoucihoVydani");

        ElementFormBinder.Prefill(fields, element);

        Assert.False(fields[0].Required);
    }

    [Fact]
    public void PovinnePoleSVlastnosti_ZustavaPovinne()
    {
        var element = new TextElement { Text = "Ahoj" };
        var fields = Required("Text");

        ElementFormBinder.Prefill(fields, element);

        Assert.True(fields[0].Required);
        Assert.Equal("Ahoj", fields[0].Value);
    }

    /// <summary>Vnořená cesta se řeší stejně - „Style.NecoNoveho" na cíli taky neexistuje.</summary>
    [Fact]
    public void PovinneVnorenePoleBezVlastnosti_PrestaneBytPovinne()
    {
        var element = new TextElement();
        var fields = Required("Style.NecoNoveho");

        ElementFormBinder.Prefill(fields, element);

        Assert.False(fields[0].Required);
    }
}
