using System.IO;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Šablona ze souboru přichází zvenku: návrhář ji přijme, jen když má tvar šablony, její agendu zná
/// a uživatel ji smí použít a prvky jízdních řádů v ní patří k té agendě. Jinak ji odmítne
/// srozumitelnou hláškou a otevřená šablona zůstane. Do 30. 10. se načetl i <c>{}</c> (šablona bez
/// sekce), neznámá agenda i tabulka spojů v zastávkovém JŘ a prvek bez typu shodil import chybou programu.
/// Testy jdou cestou importu: soubor na disku → <see cref="TemplateSerializer.LoadAsync"/> →
/// <see cref="TemplateImportCheck.ProcNelzePrijmout"/>; že je okno návrháře takhle volá, ověřuje
/// <see cref="TemplateDesigner.Tests.Windows.OtevreniCizihoSouboruTests"/>.
/// </summary>
public class ImportSablonyZeSouboruTests
{
    private static readonly AgendaRegistryEntry Obecna = new("GENERAL", "Obecná (bez dat)", "");

    private static readonly AgendaRegistryEntry[] Agendy =
    [
        Obecna,
        new("ZASTAVKOVY_JR_AD", "Zastávkový JŘ (AD)", "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd",
            ElementKind: "STOP", Agenda: "ZASTAVKOVY_JR", AgendaLabel: "Zastávkový JŘ", Variant: "AD", VariantLabel: "AD"),
        new("LINKOVY_JR_AD", "Linkový JŘ (AD)", "ALVA.TemplateDesigner.Agendy.LinkovyJrAd",
            ElementKind: "LINE", Agenda: "LINKOVY_JR", AgendaLabel: "Linkový JŘ", Variant: "AD", VariantLabel: "AD"),
        new("LINKOVY_JR_AD_ROZDILOVY", "Linkový JŘ (AD_ROZDILOVY)", "ALVA.TemplateDesigner.Agendy.LinkovyJrAdRozdilovy",
            ElementKind: "LINE", Agenda: "LINKOVY_JR", AgendaLabel: "Linkový JŘ", Variant: "AD_ROZDILOVY",
            VariantLabel: "AD - porovnání verzí", Released: false),
    ];

    [Fact]
    public async Task ExportovanaSablona_SePrijme()
    {
        var (sablona, duvod, _) =await Import(Export("ZASTAVKOVY_JR_AD", new StopListElement(), new TextElement()));

        Assert.Null(duvod);
        Assert.Equal("ZASTAVKOVY_JR_AD", sablona!.AgendaCode);
    }

    /// <summary>Import nahradí celou šablonu, takže šablona jiné agendy, než je otevřená, agendu
    /// přepne - stejně jako otevření z databáze.</summary>
    [Fact]
    public async Task SablonaJineAgendy_SePrijmeSeSvouAgendou()
    {
        var (sablona, duvod, _) =await Import(Export("LINKOVY_JR_AD", new LineTableElement()), aktualni: "ZASTAVKOVY_JR_AD");

        Assert.Null(duvod);
        Assert.Equal("LINKOVY_JR_AD", sablona!.AgendaCode);
    }

    [Fact]
    public async Task ObecnaSablonaBezKoduAgendy_SePrijme()
    {
        var (_, duvod, _) =await Import(Export(null, new TextElement()));

        Assert.Null(duvod);
    }

    /// <summary>JSON, který šablonou vůbec není (šablona z návrháře má vždy aspoň jednu sekci):
    /// uživatel vybral jiný soubor, je to informace, ne chyba programu.</summary>
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"foo\":1}")]
    [InlineData("[1,2]")]
    [InlineData("null")]
    [InlineData("{\"sections\":[]}")]
    public async Task CiziJson_SeOdmitneJakoInformace(string json)
    {
        var vysledek = await Import(json);

        Assert.Null(vysledek.Sablona);
        Assert.False(vysledek.Chyba);
        Assert.StartsWith("Soubor „", vysledek.Duvod);
        Assert.Contains("není šablona návrháře", vysledek.Duvod);
    }

    /// <summary>Šablona, kterou nejde přečíst, zůstává chybou - poškozený export může být naše chyba
    /// (<see cref="TemplateDesigner.Tests.Windows.OtevreniCizihoSouboruTests"/>). Hláška ale musí
    /// uživateli říct, co je špatně, ne opsat výjimku: neznámý typ prvku hlásil „type discriminator",
    /// cizí tvar „migraci ze starší verze se nepodařilo dokončit" a prvek bez typu shodil import.</summary>
    [Theory]
    [InlineData("{abc", "není to platný JSON")]
    [InlineData("{\"sections\":\"text\"}", "sekce nebo prvky")]
    [InlineData("{\"sections\":[\"text\"]}", "sekce nebo prvky")]
    [InlineData("{\"sections\":[{\"name\":\"A\",\"elements\":{}}]}", "sekce nebo prvky")]
    [InlineData("{\"sections\":[{\"name\":\"A\",\"elements\":[{\"x\":0}]}]}", "prvek v sekci „A“ nemá uvedený typ")]
    [InlineData("{\"sections\":[{\"name\":\"A\",\"elements\":[{\"type\":\"NeznamyPrvek\",\"x\":0}]}]}", "neznámého typu „NeznamyPrvek“")]
    [InlineData("{\"sections\":[{\"name\":\"A\",\"elements\":[{\"x\":0,\"type\":\"TextElement\"}]}]}", "typ uvedený jako první")]
    [InlineData("{\"sections\":[{\"name\":\"A\",\"elements\":[{\"type\":\"TextElement\",\"x\":\"vlevo\"}]}]}", "$.sections[0].elements[0].x")]
    public async Task PoskozenaSablona_JeChybaSeSrozumitelnouHlaskou(string json, string coJeSpatne)
    {
        var vysledek = await Import(json);

        Assert.Null(vysledek.Sablona);
        Assert.True(vysledek.Chyba);
        Assert.StartsWith("Šablonu ze souboru „", vysledek.Duvod);
        Assert.Contains(coJeSpatne, vysledek.Duvod);
    }

    [Fact]
    public async Task NeznamaAgenda_SeOdmitne()
    {
        var (_, duvod, _) =await Import(Export("NEEXISTUJE", new TextElement()));

        Assert.NotNull(duvod);
        Assert.Contains("NEEXISTUJE", duvod);
    }

    [Fact]
    public async Task NezverejnenaAgenda_SeUzivateliOdmitne()
    {
        var json = Export("LINKOVY_JR_AD_ROZDILOVY", new LineTableElement());

        var (_, duvod, _) =await Import(json);

        Assert.NotNull(duvod);
        Assert.Contains("Linkový JŘ · AD - porovnání verzí", duvod);
    }

    /// <summary>Okno Změnit agendu nabízí nezveřejněnou agendu vývojáři se skrytými agendami
    /// a vždy tu, ve které uživatel právě pracuje - import se řídí stejně.</summary>
    [Fact]
    public async Task NezverejnenaAgenda_SePrijmeVyvojariIVeStejneAgende()
    {
        var json = Export("LINKOVY_JR_AD_ROZDILOVY", new LineTableElement());

        Assert.Null((await Import(json, skryte: true)).Duvod);
        Assert.Null((await Import(json, aktualni: "LINKOVY_JR_AD_ROZDILOVY")).Duvod);
    }

    [Fact]
    public async Task PrvekJineAgendy_SeOdmitneSJmenemPrvku()
    {
        var (_, duvod, _) =await Import(Export("ZASTAVKOVY_JR_AD", new StopListElement(), new LineTableElement()));

        Assert.NotNull(duvod);
        Assert.Contains(ElementNames.ForUser(new LineTableElement()), duvod);
        Assert.DoesNotContain(ElementNames.ForUser(new StopListElement()), duvod);
    }

    /// <summary>Obecná šablona s Minutami odjezdů ukazovala prázdnou záložku vlastních sloupců
    /// (review 27. 10.).</summary>
    [Fact]
    public async Task ObecnaSablonaSPrvkemJizdnihoRadu_SeOdmitne()
    {
        var (_, duvod, _) =await Import(Export(null, new DepartureGridElement()));

        Assert.NotNull(duvod);
        Assert.Contains(ElementNames.ForUser(new DepartureGridElement()), duvod);
    }

    /// <summary>Bez seznamu agend ze serveru nejde agendu ani prvky ověřit (rozhodnutí Jan 30. 10.).
    /// Obecná agenda je v seznamu vždy, i když se ze serveru nenačetlo nic.</summary>
    [Fact]
    public async Task BezSeznamuAgendZeServeru_SeOdmitne()
    {
        var json = Export(null, new TextElement());

        Assert.NotNull((await Import(json, agendy: [Obecna])).Duvod);
        Assert.NotNull((await Import(json, agendy: [])).Duvod);
    }

    /// <summary>Kontrola platí jen pro import; šablony v databázi se otevírají jako dřív (Jan 30. 10.:
    /// staré šablony neřešíme).</summary>
    [Fact]
    public void ZDatabaze_SePrvekJineAgendyNacteJakoDriv()
    {
        var sablona = TemplateSerializer.Deserialize(Export("ZASTAVKOVY_JR_AD", new LineTableElement()));

        Assert.IsType<LineTableElement>(Assert.Single(sablona.Sections.Single().Elements));
    }

    private static string Export(string? agenda, params TemplateElement[] prvky)
    {
        var sekce = new Section { Name = "Obsah" };
        sekce.Elements.AddRange(prvky);
        var sablona = new Template { Name = "Zkouška", AgendaCode = agenda };
        sablona.Sections.Add(sekce);
        return TemplateSerializer.Serialize(sablona);
    }

    /// <summary>Výsledek importu: přijatá šablona, nebo důvod odmítnutí. <c>Chyba</c> = návrhář ho
    /// ukáže jako chybu programu, jinak jako informaci.</summary>
    private sealed record Vysledek(Template? Sablona, string? Duvod, bool Chyba);

    /// <summary>Tatáž cesta jako import v návrháři; jiná výjimka než odmítnutá šablona test shodí.</summary>
    private static async Task<Vysledek> Import(string json,
        IReadOnlyList<AgendaRegistryEntry>? agendy = null, string? aktualni = "ZASTAVKOVY_JR_AD", bool skryte = false)
    {
        var cesta = Path.Combine(Path.GetTempPath(), $"td-import-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(cesta, json);
        try
        {
            var sablona = await TemplateSerializer.LoadAsync(cesta);
            return TemplateImportCheck.ProcNelzePrijmout(sablona, agendy ?? Agendy, aktualni, skryte) is { } duvod
                ? new(null, duvod, Chyba: false)
                : new(sablona, null, Chyba: false);
        }
        catch (TemplateDeserializationException ex)
        {
            return new(null, ex.Message, Chyba: !ex.NeniSablona);
        }
        finally
        {
            File.Delete(cesta);
        }
    }
}
