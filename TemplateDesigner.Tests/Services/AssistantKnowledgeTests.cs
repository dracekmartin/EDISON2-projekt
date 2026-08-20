using TemplateDesigner.Services;

namespace TemplateDesigner.Tests.Services;

/// <summary>
/// Znalostní báze AI průvodce. Katalog vlastností se generuje z prvků, takže nedriftuje - ale
/// ručně psaný úvod ano, a nejdražší chyba je věta „to zatím neumíme" u featury, která existuje:
/// uživatel ji pak nehledá. Ban list drží, co jsme dodělali (sloučení buněk `B76`, zaoblené rohy);
/// při další takové featuře sem přidej její tvar.
/// </summary>
public class AssistantKnowledgeTests
{
    private static string Neumime =>
        CapabilityCatalog.GeneralIntro[CapabilityCatalog.GeneralIntro.IndexOf("ZATÍM neumí")..];

    [Theory]
    [InlineData("Sloučené buňky")]   // B76, 17. 8. - vodorovné sloučení umíme
    [InlineData("zaoblené rohy")]    // rohy má každý prvek (Rohy + Velikost rohu u rámečku)
    public void NeumimeNezminujeFeaturyKtereUmime(string tvar)
    {
        Assert.DoesNotContain(tvar, Neumime, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Featura, kterou uživatel nenajde sám (je pod pravým klikem), musí být v mapě UI.</summary>
    [Theory]
    [InlineData("Sloučit / Rozdělit buňky")]
    [InlineData("Alt+Enter")]
    [InlineData("Vlastnosti řádku")]
    public void MapaUiZnaPraciUvnitrTabulky(string tvar)
    {
        Assert.Contains(tvar, CapabilityCatalog.GeneralIntro);
    }

    /// <summary>Katalog se staví z prvků - když se prvek rozbije, ať to padne tady, ne u uživatele.</summary>
    [Fact]
    public void KatalogObsahujePrvkyJizdnihoRadu()
    {
        var catalog = CapabilityCatalog.BuildElementCatalog("STOP");

        Assert.Contains("Pozice šipky", catalog);
        Assert.DoesNotContain("vlastnosti se nepodařilo načíst", catalog);
    }
}
