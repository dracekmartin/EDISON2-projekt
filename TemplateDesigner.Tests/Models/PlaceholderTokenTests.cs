using TemplateDesigner.Models;

namespace TemplateDesigner.Tests.Models;

/// <summary>
/// Rozpoznání rozepsaného <c>{POLE</c> pod kurzorem (našeptávač polí v buňce tabulky).
/// Vzniklo po pádu z 12. 8.: doplnění pole přepsalo text, což synchronně spustilo přepočet
/// nabídky ve chvíli, kdy kurzor stál ještě před závorkou - a výpočet podřetězce dostal
/// zápornou délku. Hraniční pozice kurzoru proto pokrývají testy, ne proklik.
/// </summary>
public class PlaceholderTokenTests
{
    [Fact]
    public void PoNapsaniZavorky_NabidneVsechno()
    {
        Assert.Equal((0, ""), PlaceholderToken.Typing("{", 1));
    }

    [Fact]
    public void RozepsanyNazev_DaPrefixKFiltrovani()
    {
        Assert.Equal((6, "CIS"), PlaceholderToken.Typing("Linka {CIS", 10));
    }

    [Fact]
    public void UzavrenePole_NabidkuNeotevira()
    {
        Assert.Null(PlaceholderToken.Typing("{CISLO_LINKY}", 13));
    }

    [Fact]
    public void KurzorPredZavorkou_NicNenajde()
    {
        // Přesně stav po doplnění pole, na kterém to spadlo.
        Assert.Null(PlaceholderToken.Typing("{CISLO_LINKY}", 0));
    }

    [Fact]
    public void TextBezZavorky_NicNenajde()
    {
        Assert.Null(PlaceholderToken.Typing("Linka 42", 8));
    }

    /// <summary>Druhá závorka bez uzavření té první: našeptávač bere tu bližší ke kurzoru,
    /// protože uživatel píše nové pole, ne pokračování starého.</summary>
    [Fact]
    public void DveZavorkyZaSebou_BereTuBlizsi()
    {
        Assert.Equal((2, "B"), PlaceholderToken.Typing("{A{B", 4));
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("", 5)]        // kurzor za koncem textu
    [InlineData("{ABC", -3)]   // záporná pozice
    public void HranicniPozice_NespadnouAniNevyhodi(string text, int caret)
    {
        var vysledek = PlaceholderToken.Typing(text, caret);

        if (text.Length == 0) Assert.Null(vysledek);
        else Assert.Null(vysledek);   // záporný kurzor se ořízne na 0 = nic rozepsaného
    }

    /// <summary>Pole rozepsané uprostřed textu - našeptávač musí brát závorku nejblíž kurzoru.</summary>
    [Fact]
    public void PoleUprostredTextu_BereNejblizsiZavorku()
    {
        Assert.Equal((8, "DAT"), PlaceholderToken.Typing("{LINKA} {DAT konec", 12));
    }
}
