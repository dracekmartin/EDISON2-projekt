using TemplateDesigner.Models.Elements;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Popisky vzorů sloupců posílá server u voleb parametru „Rozdělení sloupců" (klíč vzoru v <c>optionKeys</c>).
/// Do 30. 10. je měl klient opsané pro vývěsku i linku zvlášť a musely souhlasit se zamčenými volbami
/// na serveru. Že server klíče posílá, hlídá Caché test kontraktu (<c>KontraktAgendTest</c>).
/// </summary>
public class VzorySloupcuZParametruTests
{
    private const string Parametr = """
        [{"name":"ROZDELENI_SLOUPCU","type":"enum",
          "options":["Automaticky (dle linky)","Pracovní / Sobota / Neděle+svátky","","Pracovní / víkend (So+Ne+svátky)","Vlastní rozložení"],
          "optionKeys":["","PRAC_SO_NE","NIC","PRAC_VIKEND",""]}]
        """;

    [Fact]
    public void VzoryJsouVolbySKlicem()
    {
        var nabidka = DepartureColumnPresets.FromParameter(Assert.Single(CacheAgendaProvider.ParseParameters(Parametr)));

        Assert.Equal(
            [new("PRAC_SO_NE", "Pracovní / Sobota / Neděle+svátky"), new("PRAC_VIKEND", "Pracovní / víkend (So+Ne+svátky)")],
            nabidka);
    }

    /// <summary>Prázdná volba se vyřadí i se svým klíčem - jinak by se klíče posunuly k sousedním volbám.</summary>
    [Fact]
    public void PrazdnaVolbaVyradiISvujKlic()
    {
        var parametr = Assert.Single(CacheAgendaProvider.ParseParameters(Parametr));

        Assert.Equal(parametr.Options!.Count, parametr.OptionKeys!.Count);
        Assert.DoesNotContain("NIC", parametr.OptionKeys);
    }

    /// <summary>Server posílá u voleb zobrazovaný popisek (`optionLabels`), stejný ve vývěsce i linkovém JŘ (`P2`).
    /// Uživatel vidí popisek, ale parametr dál ukládá text volby - jinak by se uložené volby tiše vrátily na výchozí.</summary>
    private const string ParametrSPopisky = """
        [{"name":"ROZDELENI_SLOUPCU","type":"enum",
          "options":["Automaticky (dle linky)","Pracovní / Sobota / Neděle+svátky","","Pracovní / víkend (So+Ne+svátky)","Vlastní rozložení"],
          "optionKeys":["","PRAC_SO_NE","NIC","PRAC_VIKEND",""],
          "optionLabels":["Automaticky (dle linky)","Pracovní dny / Soboty / Neděle+svátky","NIC","Pracovní dny / Soboty+Neděle+svátky",""]}]
        """;

    [Fact]
    public void VzorUkazujePopisekAleUkladaTextVolby()
    {
        var parametr = Assert.Single(CacheAgendaProvider.ParseParameters(ParametrSPopisky));
        var nabidka = DepartureColumnPresets.FromParameter(parametr);

        Assert.Equal(["Pracovní dny / Soboty / Neděle+svátky", "Pracovní dny / Soboty+Neděle+svátky"], nabidka.Select(p => p.Label));
        Assert.Equal(["Pracovní / Sobota / Neděle+svátky", "Pracovní / víkend (So+Ne+svátky)"], nabidka.Select(p => p.Option));
        Assert.Equal("PRAC_VIKEND", DepartureColumnPresets.KeyForOption(nabidka, "Pracovní / víkend (So+Ne+svátky)"));
        Assert.Null(DepartureColumnPresets.KeyForOption(nabidka, "Pracovní dny / Soboty+Neděle+svátky"));
    }

    /// <summary>Popisky jdou ve dvojici s volbou jako klíče; prázdný popisek = ukáže se text volby.</summary>
    [Fact]
    public void PopiskySediKVolbam()
    {
        var parametr = Assert.Single(CacheAgendaProvider.ParseParameters(ParametrSPopisky));

        Assert.Equal(parametr.Options!.Count, parametr.OptionLabels!.Count);
        Assert.Equal("Pracovní dny / Soboty+Neděle+svátky", parametr.OptionLabel("Pracovní / víkend (So+Ne+svátky)"));
        Assert.Equal("Vlastní rozložení", parametr.OptionLabel("Vlastní rozložení"));
        Assert.Equal("neznámá", parametr.OptionLabel("neznámá"));
    }

    /// <summary>Lišta parametrů a tiskové okno ukážou popisek, hodnota volby je uložený text.</summary>
    [Fact]
    public void NabidkaParametruUkazujePopisek()
    {
        var parametr = Assert.Single(CacheAgendaProvider.ParseParameters(ParametrSPopisky));
        var vm = new ParameterControlVM(parametr, [], onChanged: null);

        Assert.Contains(new("Pracovní / víkend (So+Ne+svátky)", "Pracovní dny / Soboty+Neděle+svátky"), vm.Options);
        Assert.Equal(parametr.Options, vm.Options.Select(o => o.Value));
    }

    /// <summary>Starší server klíče neposílá - vzory se pak nenabízejí, klient si popisky nevymýšlí.</summary>
    [Fact]
    public void BezKlicuNejsouVzory()
    {
        var parametr = Assert.Single(CacheAgendaProvider.ParseParameters(
            """[{"name":"ROZDELENI_SLOUPCU","type":"enum","options":["Automaticky (dle linky)","Pracovní / Sobota / Neděle+svátky"]}]"""));

        Assert.Null(parametr.OptionKeys);
        Assert.Null(parametr.OptionLabels);
        Assert.Equal("Pracovní / Sobota / Neděle+svátky", parametr.OptionLabel("Pracovní / Sobota / Neděle+svátky"));
        Assert.Empty(DepartureColumnPresets.FromParameter(parametr));
    }
}
