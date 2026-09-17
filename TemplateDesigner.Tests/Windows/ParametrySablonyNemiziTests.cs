namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Uložení šablony nesmí zahodit nastavení sestavy, které server právě nenabízí (`B156`).
/// </summary>
/// <remarks>
/// <para>Hlásil TRANSDEV 16. 9. jako „po aktualizaci se mi nastavení resetují". Do 16. 9. se seznam
/// hodnot stavěl <b>od nuly</b> jen z parametrů, které server v tu chvíli vrátil - cokoli mimo ně
/// ze šablony tiše zmizelo a při prázdném seznamu se smazalo všechno.</para>
/// <para>Nebyl to teoretický okraj: volba „Písmena variant u odjezdů" se 17. 8. zrušila (`B90`)
/// a 11. 9. vrátila pod týmž klíčem (`B141`). Kdo mezitím šablonu uložil, přišel o svou hodnotu
/// a po návratu volby dostal výchozí stav. Stačí k tomu i výpadek serveru při ukládání, dočasně
/// skrytá agenda nebo starší třída na serveru.</para>
/// </remarks>
public class ParametrySablonyNemiziTests
{
    /// <summary>Hodnota parametru, který server právě nenabízí, ze šablony nezmizí.</summary>
    [Fact]
    public void NeznamyKlicZustaneVSablone()
    {
        var stare = new Dictionary<string, string>
        {
            ["PISMENA_VARIANT"] = "false",     // volba, kterou server dočasně nevrací
            ["CASOVA_OSA"]      = "Souvislá",
        };
        var nove = new Dictionary<string, string> { ["CASOVA_OSA"] = "Souvislá" };

        var vysledek = TemplateDesignerViewModel.SlucHodnotyParametru(stare, nove);

        Assert.NotNull(vysledek);
        Assert.Equal("false", vysledek["PISMENA_VARIANT"]);
    }

    /// <summary>
    /// Když server nevrátí parametry vůbec (výpadek, skrytá agenda), šablona si nechá všechny.
    /// </summary>
    [Fact]
    public void ZadneParametryZeServeru_SablonaSiNechaSve()
    {
        var stare = new Dictionary<string, string>
        {
            ["PISMENA_VARIANT"]      = "false",
            ["TISK_PROJETYCH_ZAST"]  = "true",
        };

        var vysledek = TemplateDesignerViewModel.SlucHodnotyParametru(stare, new Dictionary<string, string>());

        Assert.NotNull(vysledek);
        Assert.Equal(2, vysledek.Count);
    }

    /// <summary>Co uživatel právě nastavil, přebíjí uloženou hodnotu - jinak by nešlo nic změnit.</summary>
    [Fact]
    public void NovaHodnotaPrebijeUlozenou()
    {
        var stare = new Dictionary<string, string> { ["PISMENA_VARIANT"] = "false" };
        var nove  = new Dictionary<string, string> { ["PISMENA_VARIANT"] = "true" };

        var vysledek = TemplateDesignerViewModel.SlucHodnotyParametru(stare, nove);

        Assert.Equal("true", vysledek!["PISMENA_VARIANT"]);
    }

    /// <summary>
    /// Šablona bez uložených hodnot a bez parametrů zůstane bez nich - null, ne prázdný slovník.
    /// </summary>
    /// <remarks>Prázdný slovník by se serializoval jako `"printParameters": {}` a zbytečně
    /// zvětšoval každou šablonu bez parametrů.</remarks>
    [Fact]
    public void NicUlozenehoANicNoveho_VracíNull()
    {
        Assert.Null(TemplateDesignerViewModel.SlucHodnotyParametru(null, new Dictionary<string, string>()));
    }

    /// <summary>První uložení: hodnoty vzniknou i tam, kde šablona zatím nic neměla.</summary>
    [Fact]
    public void PrvniUlozeni_HodnotySeZapisou()
    {
        var nove = new Dictionary<string, string> { ["CASOVA_OSA"] = "Souvislá" };

        var vysledek = TemplateDesignerViewModel.SlucHodnotyParametru(null, nove);

        Assert.Equal("Souvislá", vysledek!["CASOVA_OSA"]);
    }
}
