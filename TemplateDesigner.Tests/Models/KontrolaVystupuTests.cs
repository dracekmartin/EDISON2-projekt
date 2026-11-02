using TemplateDesigner.Models;

namespace TemplateDesigner.Tests.Models;

/// <summary>`B178`: kontrola v tiskovém okně řekne, kterých linek se hláška týká.</summary>
public class KontrolaVystupuTests
{
    private static readonly NazevInstance Linka = NazevInstance.Z("linka,linky,linku,linky,linek", "F");

    private static DataContext Stranka(string linka) =>
        new(new Dictionary<string, object?> { [KontrolaVystupu.LinkaKlic] = linka });

    private static readonly Hlaseni Preteceni =
        new(ZavaznostHlaseni.Chyba, "Poznámky", "nevejde se na stránku", "rozdělit přes stránky");
    private static readonly Hlaseni Zmenseni =
        new(ZavaznostHlaseni.Informace, "Tabulka spojů", "zmenšena na 94 %");

    [Fact]
    public void HlaskaJenZJedneLinky_JmenujeTuLinku()
    {
        var souhrn = KontrolaVystupu.Souhrn(new Template(),
            [Stranka("1 - Horní Planá"), Stranka("2 - Kaplice"), Stranka("3 - Frymburk")],
            data => data[0].GetValue(KontrolaVystupu.LinkaKlic) as string == "2 - Kaplice" ? [Preteceni] : [], Linka);

        var h = Assert.Single(souhrn);
        Assert.Equal("2 - Kaplice · Poznámky", h.Kde);
    }

    [Fact]
    public void StejnaHlaskaNaVsechLinkach_JedenRadek()
    {
        var souhrn = KontrolaVystupu.Souhrn(new Template(),
            [Stranka("1 - A"), Stranka("2 - B")], _ => [Zmenseni], Linka);

        var h = Assert.Single(souhrn);
        Assert.StartsWith("Všechny linky (2)", h.Kde);
    }

    /// <summary>Agenda, která netiskne linky, mluví svým slovem a se shodou podle rodu (`B194e` A5).</summary>
    [Theory]
    [InlineData("oběh,oběhu,oběh,oběhy,oběhů", "M", "Všechny oběhy (2)")]
    [InlineData("zaměstnanec,zaměstnance,zaměstnance,zaměstnanci,zaměstnanců,zaměstnance", "MZ", "Všichni zaměstnanci (2)")]
    public void StejnaHlaskaVeVsech_JinaAgenda(string tvary, string rod, string ocekavano)
    {
        var souhrn = KontrolaVystupu.Souhrn(new Template(),
            [Stranka("1"), Stranka("2")], _ => [Zmenseni], NazevInstance.Z(tvary, rod));

        Assert.StartsWith(ocekavano, Assert.Single(souhrn).Kde);
    }

    [Fact]
    public void JedinaLinka_BezPredpony_ChybyPrvni()
    {
        var souhrn = KontrolaVystupu.Souhrn(new Template(), [Stranka("1 - A")], _ => [Zmenseni, Preteceni], Linka);

        Assert.Equal(ZavaznostHlaseni.Chyba, souhrn[0].Zavaznost);
        Assert.Equal("Poznámky", souhrn[0].Kde);
    }
}
