using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Popisky přípojných řádků jazykem šablony (#9088 bod 7: na slovenské šabloně vycházely česky).
/// </summary>
public class PripojPopiskyTests
{
    private static LineTableElement.LinePripojData Pripoj(string typ) =>
        new() { Typ = typ, Text = "Nitra", Linka = "330540", Spoj = "12" };

    /// <summary>Sloučený řádek doslova jako starý tisk (`Main.cls:218-219`, `:243-244`).</summary>
    [Theory]
    [InlineData("vycka", false, "ze směru Nitra")]
    [InlineData("navazuje", false, "směr Nitra")]
    [InlineData("vycka", true, "zo smeru Nitra")]
    [InlineData("navazuje", true, "smer Nitra")]
    public void SloucenyRadek_JazykemSablony(string typ, bool slovensky, string ocekavano)
    {
        Assert.Equal(ocekavano, LineTableElement.PripojTextSouhrnny(Pripoj(typ), slovensky));
    }

    /// <summary>Slučování i podle linky: bez čísla linky by dva řádky měly týž popisek.</summary>
    [Fact]
    public void SlucovaniPodleLinky_PopisekZacinaCislemLinky()
    {
        Assert.Equal("330540 zo smeru Nitra",
            LineTableElement.PripojTextSouhrnny(Pripoj("vycka"), slovensky: true, sLinkou: true));
    }

    [Theory]
    [InlineData("vycka", false, "vyčká na spoj 12 linky 330540 do Nitra")]
    [InlineData("vycka", true, "počká na spoj 12 linky 330540 do Nitra")]
    [InlineData("navazuje", true, "nadväzuje spoj 12 linky 330540 do Nitra")]
    public void SamostatnyRadek_JazykemSablony(string typ, bool slovensky, string ocekavano)
    {
        Assert.Equal(ocekavano, LineTableElement.PripojText(Pripoj(typ), slovensky));
    }
}
