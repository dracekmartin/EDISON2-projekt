using System.Text.Json.Nodes;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Přejmenování pole <c>POPIS_SMER</c> na <c>KONECNA_ZASTAVKA</c> (#13158, 1.9.0 → 1.10.0).
/// </summary>
/// <remarks>
/// <para>Pole se přejmenovalo <b>načisto</b> - staré nezůstalo žít vedle nového, protože mrtvé pole
/// držené jen kvůli přejmenování je nepořádek, ne zpětná kompatibilita (rozhodnutí Jan 31. 8.).
/// Tahle migrace je tedy jediné, co drží existující šablony funkční: bez ní by na nich zůstal
/// zástupce, který už žádná agenda neposílá, a v tisku by z něj vyšlo prázdno.</para>
/// <para>⚠️ Nejcitlivější místo je <b>hranice tokenu</b>: <c>{POPIS_SMER_CIS}</c> nesmí dostat
/// půlku náhrady z pravidla pro <c>{POPIS_SMER}</c>. Kdyby se to stalo, vznikne
/// <c>{KONECNA_ZASTAVKA_CIS}</c> jen náhodou, nebo naopak nesmysl - proto na to míří vlastní test.</para>
/// </remarks>
public class MigrationKonecnaZastavkaTests
{
    private static JsonNode Sablona(string text) =>
        JsonNode.Parse($$"""
        {
          "version": "1.9.0",
          "sections": [ { "elements": [ { "type": "TextElement", "text": {{text}} } ] } ]
        }
        """)!;

    private static string Text(JsonNode root) =>
        root["sections"]![0]!["elements"]![0]!["text"]!.GetValue<string>();

    private static JsonNode Migruj(JsonNode root) => new MigrationKonecnaZastavka().Migrate(root);

    /// <summary>Zástupce v textu prvku se přepíše na nový název.</summary>
    [Fact]
    public void ZastupceVTextu_SePrepise()
    {
        var root = Migruj(Sablona("\"Směr: {POPIS_SMER}\""));

        Assert.Equal("Směr: {KONECNA_ZASTAVKA}", Text(root));
    }

    /// <summary>Zástupce s formátem si formát ponechá.</summary>
    [Fact]
    public void ZastupceSFormatem_SiFormatPonecha()
    {
        var root = Migruj(Sablona("\"{POPIS_SMER:upper}\""));

        Assert.Equal("{KONECNA_ZASTAVKA:upper}", Text(root));
    }

    /// <summary>
    /// Varianta <c>_CIS</c> se přepíše celá, ne půlkou.
    /// </summary>
    /// <remarks>
    /// Kdyby pravidlo pro <c>{POPIS_SMER}</c> nemělo hranici tokenu, vyrobilo by z
    /// <c>{POPIS_SMER_CIS}</c> tvar <c>{KONECNA_ZASTAVKA_CIS}</c> náhodou - a u jiného delšího
    /// názvu by vyrobilo nesmysl. Test hlídá obojí naráz.
    /// </remarks>
    [Fact]
    public void VariantaCis_SePrepiseCela()
    {
        var root = Migruj(Sablona("\"{POPIS_SMER_CIS} × {POPIS_SMER}\""));

        Assert.Equal("{KONECNA_ZASTAVKA_CIS} × {KONECNA_ZASTAVKA}", Text(root));
    }

    /// <summary>Sloupec tabulky odkazuje na pole holým názvem - přepíše se taky.</summary>
    [Fact]
    public void SloupecTabulky_SePrepise()
    {
        var root = JsonNode.Parse("""
        {
          "version": "1.9.0",
          "sections": [ { "elements": [ { "type": "DynamicTableElement", "columns": [
            { "field": "POPIS_SMER" },
            { "field": "POPIS_SMER_CIS" },
            { "field": "POPIS_ZASTAVKA" }
          ] } ] } ]
        }
        """)!;

        var cols = Migruj(root)["sections"]![0]!["elements"]![0]!["columns"]!.AsArray();

        Assert.Equal("KONECNA_ZASTAVKA", cols[0]!["field"]!.GetValue<string>());
        Assert.Equal("KONECNA_ZASTAVKA_CIS", cols[1]!["field"]!.GetValue<string>());
        Assert.Equal("POPIS_ZASTAVKA", cols[2]!["field"]!.GetValue<string>());
    }

    /// <summary>Podobně pojmenovaná pole zůstanou nedotčená.</summary>
    [Fact]
    public void PolePodobnehoNazvu_ZustanouBezeZmeny()
    {
        var root = Migruj(Sablona("\"{POPIS_ZASTAVKA} {POPIS_DOPRAVCE} {POPIS_LINKY}\""));

        Assert.Equal("{POPIS_ZASTAVKA} {POPIS_DOPRAVCE} {POPIS_LINKY}", Text(root));
    }

    /// <summary>Druhý průchod už nic nezmění - migrace je idempotentní.</summary>
    [Fact]
    public void OpakovanaMigrace_UzNicNezmeni()
    {
        var jednou = Migruj(Sablona("\"{POPIS_SMER} / {POPIS_SMER_CIS}\""));
        string poPrvnim = Text(jednou);

        var dvakrat = Migruj(jednou);

        Assert.Equal(poPrvnim, Text(dvakrat));
        Assert.Equal("{KONECNA_ZASTAVKA} / {KONECNA_ZASTAVKA_CIS}", poPrvnim);
    }

    /// <summary>Migrace je zaregistrovaná v řadě, takže stará šablona projde až na aktuální verzi.</summary>
    /// <remarks>
    /// Bez registrace by testy výše svítily zeleně, ale uživateli by se šablona nezmigrovala vůbec -
    /// to je ta třída chyb, kterou samotný test transformace nechytí.
    /// </remarks>
    [Fact]
    public void MigraceJeZaregistrovana_StaraSablonaProjdeAzNaAktualniVerzi()
    {
        var (migrated, wasMigrated, original) = new TemplateMigrator().Migrate(
            Sablona("\"Směr: {POPIS_SMER}\""));

        Assert.True(wasMigrated);
        Assert.Equal("1.9.0", original);
        Assert.Equal(TemplateMigrator.CurrentVersion, migrated["version"]!.GetValue<string>());
        Assert.Equal("Směr: {KONECNA_ZASTAVKA}", Text(migrated));
    }
}
