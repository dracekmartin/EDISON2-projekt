using TemplateDesigner;
using TemplateDesigner.Services;
using static TemplateDesigner.CacheTemplatePickerDialogViewModel;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Strom v okně šablon (`B190`): čtení odpovědi serveru (<see cref="TemplateCatalog.Parse"/>), uzly stromu
/// (<see cref="TemplateCatalogNodeVM.Build"/>) a co udělá tlačítko výchozí šablony pro tisk.
/// </summary>
public class TemplateCatalogTests
{
    // Tvar odpovědi `API.StromSablon.GetStrom` (ověřený voláním serveru 2. 11.), kódy jsou smyšlené.
    private const string Odpoved = """
        {"oblasti":[
          {"code":"JIZDNI_RADY","label":"Jízdní řády","agendy":[
            {"code":"LINKA","label":"Linkový JŘ","varianty":[
              {"code":"LINKA_AD","label":"AD","pocet":3,"tiskne":true,
               "vychozi":{"templateId":"3c4b2f8e-6a1d-4e57-9b0a-7f2d5c8e1a64","name":"A4 poznámky pod","zdroj":"mline"}},
              {"code":"LINKA_MHD","label":"MHD","pocet":2,"tiskne":true,"vychozi":{}}]},
            {"code":"RELACE","label":"Relace","varianty":[
              {"code":"RELACE_AD","label":"AD","pocet":1,"tiskne":true,
               "vychozi":{"templateId":"11111111-2222-3333-4444-555555555555","name":"Naše relace","zdroj":"firma"}}]}]},
          {"code":"OBECNE","label":"Obecné","agendy":[
            {"code":"OBECNA","label":"Obecná (bez dat)","varianty":[
              {"code":"OBECNA","label":"","pocet":4,"tiskne":false,"vychozi":{}}]}]}]}
        """;

    [Fact]
    public void Parse_CteOblastiAgendyVariantyAVychozi()
    {
        var strom = TemplateCatalog.Parse(Odpoved);

        Assert.Equal(["JIZDNI_RADY", "OBECNE"], strom.Areas.Select(a => a.Code));
        var ad = strom.Areas[0].Agendas[0].Variants[0];
        Assert.Equal(("LINKA_AD", "AD", 3, true), (ad.Code, ad.Label, ad.Count, ad.CanPrint));
        Assert.Equal("A4 poznámky pod", ad.Default?.Name);
        Assert.False(ad.Default!.IsCompanyDefault);
        Assert.True(strom.Areas[0].Agendas[1].Variants[0].Default!.IsCompanyDefault);
        Assert.Null(strom.Areas[0].Agendas[0].Variants[1].Default);
        Assert.False(strom.Areas[1].Agendas[0].Variants[0].CanPrint);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("není json")]
    [InlineData("{}")]
    public void Parse_BezOdpovedi_PrazdnyStrom(string? json) =>
        Assert.Empty(TemplateCatalog.Parse(json).Areas);

    [Fact]
    public void Build_PrvniVsechnySablonyBezFiltru()
    {
        var uzly = TemplateCatalogNodeVM.Build(TemplateCatalog.Parse(Odpoved));

        var vse = uzly[0];
        Assert.Equal((TemplateCatalogNodeVM.AllKey, "Všechny šablony"), (vse.Key, vse.Label));
        Assert.Empty(vse.AgendaCodes);
        Assert.Null(vse.Count);
        Assert.Equal(["Všechny šablony", "Jízdní řády", "Obecné"], uzly.Select(u => u.Label));
    }

    [Fact]
    public void Build_OblastFiltrujeVsechnyAgendyASectePocty()
    {
        var oblast = TemplateCatalogNodeVM.Build(TemplateCatalog.Parse(Odpoved))[1];

        Assert.Equal("oblast:JIZDNI_RADY", oblast.Key);
        Assert.Equal("LINKA_AD,LINKA_MHD,RELACE_AD", oblast.FilterValue);
        Assert.Equal(6, oblast.Count);
        Assert.True(oblast.IsExpanded, "oblasti jsou rozbalené");
    }

    [Fact]
    public void Build_AgendaSVicVariantami_MaUzlyVariant()
    {
        var linka = TemplateCatalogNodeVM.Build(TemplateCatalog.Parse(Odpoved))[1].Children[0];

        Assert.Equal(("agenda:LINKA", "LINKA_AD,LINKA_MHD", 5), (linka.Key, linka.FilterValue, linka.Count));
        Assert.Null(linka.Detail);
        Assert.Equal(["varianta:LINKA_AD", "varianta:LINKA_MHD"], linka.Children.Select(v => v.Key));
        Assert.Equal("Pro tisk: A4 poznámky pod", linka.Children[0].Detail);
        Assert.Equal("Bez šablony pro tisk", linka.Children[1].Detail);
    }

    [Fact]
    public void Build_AgendaSJednouVariantou_JedenUzel()
    {
        var oblasti = TemplateCatalogNodeVM.Build(TemplateCatalog.Parse(Odpoved));
        var relace = oblasti[1].Children[1];
        var obecna = oblasti[2].Children[0];

        Assert.Equal(("agenda:RELACE", "Relace", "RELACE_AD"), (relace.Key, relace.Label, relace.FilterValue));
        Assert.Empty(relace.Children);
        Assert.Equal("Pro tisk: Naše relace", relace.Detail);
        Assert.Null(obecna.Detail);
        Assert.False(obecna.HasDetail, "Obecná se z modulu netiskne, nic o tisku neukazuje");
    }

    [Fact]
    public void FindAPathTo_NajdouVariantuIJejiPredky()
    {
        var uzly = TemplateCatalogNodeVM.Build(TemplateCatalog.Parse(Odpoved));

        var mhd = TemplateCatalogNodeVM.Find(uzly, "varianta:LINKA_MHD");
        Assert.NotNull(mhd);
        Assert.Equal(["Jízdní řády", "Linkový JŘ", "MHD"], TemplateCatalogNodeVM.PathTo(uzly, mhd!).Select(u => u.Label));
        Assert.Null(TemplateCatalogNodeVM.Find(uzly, "varianta:NENI"));
    }

    [Theory]
    [InlineData("", true, DefaultAction.Set)]
    [InlineData(null, true, DefaultAction.Set)]
    [InlineData("firma", true, DefaultAction.Clear)]
    [InlineData("mline", true, DefaultAction.AlreadyDefault)]
    [InlineData("", false, DefaultAction.None)]
    [InlineData("firma", false, DefaultAction.None)]
    public void VychoziSablona_CoUdelaTlacitko(string? vychozi, bool canPrint, DefaultAction ocekavana) =>
        Assert.Equal(ocekavana, DecideDefaultAction(vychozi, canPrint));

    [Fact]
    public void Build_UrovenUzluProVzhled()
    {
        var uzly = TemplateCatalogNodeVM.Build(TemplateCatalog.Parse(Odpoved));
        var linka = uzly[1].Children[0];

        Assert.Equal(TemplateCatalogNodeKind.All, uzly[0].Kind);
        Assert.Equal(TemplateCatalogNodeKind.Area, uzly[1].Kind);
        Assert.Equal(TemplateCatalogNodeKind.Agenda, linka.Kind);
        Assert.Equal(TemplateCatalogNodeKind.Variant, linka.Children[0].Kind);
        Assert.Equal(TemplateCatalogNodeKind.Agenda, uzly[1].Children[1].Kind);   // agenda s jedinou variantou
    }

    [Fact]
    public void Pruh_UkazujeVybranyUzelVariantaSAgendou()
    {
        var uzly = TemplateCatalogNodeVM.Build(TemplateCatalog.Parse(Odpoved));
        var linka = uzly[1].Children[0];

        Assert.Equal("Všechny šablony", TitleFor(uzly, null));
        Assert.Equal("Všechny šablony", TitleFor(uzly, uzly[0]));
        Assert.Equal("Jízdní řády", TitleFor(uzly, uzly[1]));
        Assert.Equal("Linkový JŘ", TitleFor(uzly, linka));
        Assert.Equal("Linkový JŘ · MHD", TitleFor(uzly, linka.Children[1]));
        Assert.Equal("Relace", TitleFor(uzly, uzly[1].Children[1]));
    }

    [Fact]
    public void Napoveda_RekneJakZalozitNovouSablonu()
    {
        var uzly = TemplateCatalogNodeVM.Build(TemplateCatalog.Parse(Odpoved));
        var linka = uzly[1].Children[0];

        // Uzel bez jedné agendy: nová šablona až po výběru agendy.
        Assert.Contains("až vlevo vyberete agendu", HintFor(uzly, uzly[0]));
        Assert.Contains("až vlevo vyberete agendu", HintFor(uzly, linka));
        // Agenda: kde nová šablona vznikne a že M-line se otevře jako kopie.
        var mhd = HintFor(uzly, linka.Children[1]);
        Assert.Contains("pro Linkový JŘ · MHD založíte ikonou Nový (Ctrl+N) nad seznamem", mhd);
        Assert.Contains("jako kopie", mhd);
    }

    [Fact]
    public void Napoveda_AgendaBezSablon()
    {
        var odpoved = Odpoved.Replace("\"pocet\":4", "\"pocet\":0");
        var uzly = TemplateCatalogNodeVM.Build(TemplateCatalog.Parse(odpoved));

        Assert.Equal("Obecná (bez dat) zatím nemá žádnou šablonu. Prázdnou šablonu založíte ikonou Nový (Ctrl+N) nad seznamem.",
            HintFor(uzly, uzly[2].Children[0]));
    }

    [Theory]
    [InlineData("Linkový JŘ", "AD", "Linkový JŘ · AD")]
    [InlineData("Obecná (bez dat)", "", "Obecná (bez dat)")]
    public void Upravit_AgendaJakoVSeznamu(string agenda, string varianta, string ocekavana) =>
        Assert.Equal(ocekavana, AgendaLabelOf(new EDControls.EDList.EDListRow("1",
            new Dictionary<string, string?> { ["agenda"] = agenda, ["varianta"] = varianta })));

    [Fact]
    public void NovaSablona_JenUzelJedneAgendy()
    {
        var uzly = TemplateCatalogNodeVM.Build(TemplateCatalog.Parse(Odpoved));
        var linka = uzly[1].Children[0];

        Assert.Null(uzly[0].NewTemplateAgendaCode);              // Všechny šablony
        Assert.Null(uzly[1].NewTemplateAgendaCode);              // oblast s víc agendami
        Assert.Null(linka.NewTemplateAgendaCode);                // agenda s víc variantami
        Assert.Equal("LINKA_MHD", linka.Children[1].NewTemplateAgendaCode);
        Assert.Equal("RELACE_AD", uzly[1].Children[1].NewTemplateAgendaCode);
    }

    [Theory]
    [InlineData("LINKA_MHD", "varianta:LINKA_MHD")]
    [InlineData("linka_ad", "varianta:LINKA_AD")]
    [InlineData("RELACE_AD", "agenda:RELACE")]
    [InlineData("OBECNA", "agenda:OBECNA")]   // ne oblast Obecné, i když má jen tuhle agendu
    public void FindAgenda_NejhlubsiUzelAgendy(string agendaCode, string ocekavanyKlic)
    {
        var uzly = TemplateCatalogNodeVM.Build(TemplateCatalog.Parse(Odpoved));

        Assert.Equal(ocekavanyKlic, TemplateCatalogNodeVM.FindAgenda(uzly, agendaCode)?.Key);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("LINKA_AD_ROZDILOVY")]   // nezveřejněná agenda ve stromu není
    public void FindAgenda_AgendaMimoStrom_Null(string? agendaCode) =>
        Assert.Null(TemplateCatalogNodeVM.FindAgenda(TemplateCatalogNodeVM.Build(TemplateCatalog.Parse(Odpoved)), agendaCode));
}
