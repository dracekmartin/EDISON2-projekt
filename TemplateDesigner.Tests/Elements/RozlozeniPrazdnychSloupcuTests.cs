using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Kam se dávají prázdné sloupce dorovnání - `B167`, nález kolegy z 18. 9.
/// </summary>
/// <remarks>
/// Starý tisk je rozdává po jednom střídavě z obou stran, takže výstup vypadá rovnoměrně proložený
/// (`JRapiLinPrtWin.mac:1040-1086`). My jsme je dolepovali na konec bloku, takže spoje zůstaly
/// v hloučku vlevo a volné místo v jednom pruhu vpravo.
/// </remarks>
public class RozlozeniPrazdnychSloupcuTests
{
    /// <summary>Dva prázdné na čtyři spoje jdou na kraje - první zleva, druhý zprava.</summary>
    [Fact]
    public void DvaPrazdneJdouNaObaKraje()
    {
        Assert.Equal([1, 0, 0, 1], LineTableElement.RozlozeniPrazdnych(4, 2));
    }

    /// <summary>Třetí prázdný pokračuje zleva - pořadí je vlevo, vpravo, vlevo…</summary>
    [Fact]
    public void TretiPokracujeZleva()
    {
        Assert.Equal([1, 1, 0, 1], LineTableElement.RozlozeniPrazdnych(4, 3));
    }

    /// <summary>
    /// Když je prázdných víc než spojů, ukazatele se vrátí na kraje a přidá se další řada.
    /// </summary>
    [Fact]
    public void VicPrazdnychNezSpojuSeObtociDokola()
    {
        var rozlozeni = LineTableElement.RozlozeniPrazdnych(2, 5);

        Assert.Equal(5, rozlozeni.Sum());
        Assert.Equal(2, rozlozeni.Length);
    }

    /// <summary>Rozdá se přesně tolik sloupců, kolik se má - nic se cestou neztratí.</summary>
    [Theory]
    [InlineData(1, 3)]
    [InlineData(3, 1)]
    [InlineData(5, 4)]
    [InlineData(8, 8)]
    public void RozdaSePresneTolikKolikMa(int spoju, int prazdnych)
    {
        Assert.Equal(prazdnych, LineTableElement.RozlozeniPrazdnych(spoju, prazdnych).Sum());
    }

    /// <summary>Bez spojů není kam dorovnávat a bez dorovnání se nic nepřidá.</summary>
    [Fact]
    public void PrazdnyVstupNicNevyrobi()
    {
        Assert.Empty(LineTableElement.RozlozeniPrazdnych(0, 4));
        Assert.Equal([0, 0, 0], LineTableElement.RozlozeniPrazdnych(3, 0));
    }
}
