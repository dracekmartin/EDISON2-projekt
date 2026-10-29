using TemplateDesigner.Models;
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

        Assert.Contains("Tvar hrotu", catalog);
        Assert.DoesNotContain("vlastnosti se nepodařilo načíst", catalog);
    }

    /// <summary>
    /// Totéž pro linkové JŘ. Do 16. 9. se měřily jen zastávkové, takže by se nepoznalo, že
    /// průvodce o tabulce linky neví nic - a poradit pro ni neumí.
    /// </summary>
    [Fact]
    public void KatalogObsahujeTabulkuLinky()
    {
        var catalog = CapabilityCatalog.BuildElementCatalog("LINE");

        Assert.Contains(ElementNames.ForType(ElementTypes.LineTable), catalog);
        Assert.DoesNotContain("vlastnosti se nepodařilo načíst", catalog);
    }

    /// <summary>
    /// Průvodce musí u tabulky linky znát **souvislosti**, ne jen výčet vlastností: ten se
    /// generuje z kódu sám, kdežto „šířka sloupce spoje rozhoduje o počtu stran" nikde nestojí.
    /// Bez toho odpoví na „proč mi vyšly čtyři strany" jen tím, že vyjmenuje pole.
    /// </summary>
    [Theory]
    [InlineData("Upravit sloupce")]                  // dialog, který uživatel sám nenajde
    [InlineData("Text záhlaví popisných sloupců")]   // skupina popisných sloupců
    [InlineData("Mezera mezi skupinami dnů")]
    [InlineData("Nejmenší/Největší šířka sloupce spoje")]
    [InlineData("Sloučit popis u příjezdu a odjezdu")]
    public void KatalogLinkoveZnaSouvislosti(string tvar)
    {
        Assert.Contains(tvar, CapabilityCatalog.BuildElementCatalog("LINE"));
    }

    // ── Přednost prvků ──────────────────────────────────────────────────────

    /// <summary>
    /// V jízdněřádové agendě musí prompt říct, že přednost mají její specializované prvky.
    /// </summary>
    /// <remarks>
    /// Bez toho dostane model tabulku linky a obecné tabulky jako rovnocenné a na dotaz
    /// „jak zruším čáry mezi km sloupci" **začne otázkou, o kterou tabulku jde** - přestože
    /// km sloupce má v linkové agendě jen tabulka linky (nález Jan 16. 9. na živé odpovědi).
    /// </remarks>
    [Theory]
    [InlineData("LINE", ElementTypes.LineTable)]
    [InlineData("STOP", ElementTypes.StopList)]
    public void PromptRikaKteryPrvekMaPrednost(string elementKind, string ocekavanyTyp)
    {
        var prompt = CapabilityCatalog.BuildSystemPrompt("Linkový JŘ (AD)", null, 2, elementKind);

        Assert.Contains("Který prvek má přednost", prompt);
        Assert.Contains(ElementNames.ForType(ocekavanyTyp), prompt);
        Assert.Contains("Nenabízej výběr mezi nimi", prompt);
    }

    /// <summary>
    /// U agendy bez specializovaných prvků (faktury, obecná) pravidlo nedává smysl a nesmí tam být -
    /// jinak by model hledal prvky jízdního řádu, které v paletě nejsou.
    /// </summary>
    [Fact]
    public void ObecnaAgendaPravidloPrednostiNema()
    {
        var prompt = CapabilityCatalog.BuildSystemPrompt("Faktury", null, 1, "");

        Assert.DoesNotContain("Který prvek má přednost", prompt);
    }
}
