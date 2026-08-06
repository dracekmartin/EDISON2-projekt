using System.Text.Json.Nodes;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Schema;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Testy převodu odpovědí agendy z databáze do modelu (<see cref="CacheAgendaProvider"/>) - wire
/// kontrakt popsaný v <c>.claude/rules/cache-api.md</c>. Testují se čisté parsovací funkce, takže
/// běží bez Caché serveru i bez VPN.
///
/// Proč: chyba tady se projeví jako „v nabídce datových polí nic není" nebo „vytisklo se prázdno",
/// tedy až u zákazníka. Zvlášť **vícestránkový výstup** (zastávkový JŘ = jedna stránka na zastávku
/// × směr) a **přeskočení klíče <c>ERR</c>**, což jsou metadata MLRestu, ne data.
/// </summary>
public class CacheAgendaParsingTests
{
    // ── Schéma agendy (nabídka datových polí) ─────────────────────────────────

    [Fact]
    public void Schema_SkalarniPoleISkupinySeNacteou()
    {
        var schema = CacheAgendaProvider.ParseSchema("MHD", "Zastávkový JŘ MHD", """
            {
              "fields": [
                { "name": "CISLO_LINKY", "label": "Číslo linky", "type": "number" },
                { "name": "NAZEV" }
              ],
              "fieldCollections": [
                { "name": "Zastávky", "collectionKey": "ZASTAVKY",
                  "fields": [ { "name": "POPIS", "label": "Název zastávky", "type": "text" } ] }
              ]
            }
            """);

        Assert.NotNull(schema);
        Assert.Equal("MHD", schema.AgendaCode);
        Assert.Equal("Zastávkový JŘ MHD", schema.AgendaName);

        Assert.Equal(2, schema.Fields.Count);
        Assert.Equal("Číslo linky", schema.Fields[0].Label);
        Assert.Equal(FieldDataType.Number, schema.Fields[0].DataType);
        Assert.Equal("NAZEV", schema.Fields[1].Label);            // chybí label → použije se název pole

        var collection = Assert.Single(schema.FieldCollections);
        Assert.Equal("ZASTAVKY", collection.CollectionKey);
        Assert.Equal("Název zastávky", Assert.Single(collection.Fields).Label);
    }

    [Theory]
    [InlineData("number",   FieldDataType.Number)]
    [InlineData("date",     FieldDataType.Date)]
    [InlineData("time",     FieldDataType.Time)]
    [InlineData("datetime", FieldDataType.DateTime)]
    [InlineData("boolean",  FieldDataType.Boolean)]
    [InlineData("color",    FieldDataType.Color)]
    [InlineData("text",     FieldDataType.Text)]
    [InlineData("NUMBER",   FieldDataType.Number)]     // velikost písmen nerozhoduje
    [InlineData("necoNove", FieldDataType.Text)]       // neznámý typ = text, ne výjimka
    public void Schema_TypPoleSePrelozi(string wireType, FieldDataType expected)
    {
        var schema = CacheAgendaProvider.ParseSchema("A", "A", $$"""
            { "fields": [ { "name": "POLE", "type": "{{wireType}}" } ] }
            """);

        Assert.Equal(expected, schema!.Fields[0].DataType);
    }

    /// <summary>Starší třídy agend posílaly kolekce pod názvem „groups" - musí se pořád načíst,
    /// jinak by po aktualizaci klienta zmizela nabídka polí tabulek.</summary>
    [Fact]
    public void Schema_StarsiNazevKolekciSeJesteNacte()
    {
        var schema = CacheAgendaProvider.ParseSchema("A", "A", """
            { "fields": [],
              "groups": [ { "name": "Spoje", "collectionKey": "SPOJE",
                            "fields": [ { "name": "CAS" } ] } ] }
            """);

        Assert.Equal("SPOJE", Assert.Single(schema!.FieldCollections).CollectionKey);
    }

    [Fact]
    public void Schema_PoleBezNazvuAKolekceBezKliceSePreskoci()
    {
        var schema = CacheAgendaProvider.ParseSchema("A", "A", """
            { "fields": [ { "label": "Bez názvu" }, { "name": "OK" } ],
              "fieldCollections": [ { "name": "Bez klíče" }, { "name": "S klíčem", "collectionKey": "X" } ] }
            """);

        Assert.Equal("OK", Assert.Single(schema!.Fields).FieldName);
        Assert.Equal("X", Assert.Single(schema.FieldCollections).CollectionKey);
    }

    [Fact]
    public void Schema_PoskozenaOdpoved_VratiNic()
    {
        Assert.Null(CacheAgendaProvider.ParseSchema("A", "A", "{tohle není JSON"));
    }

    [Fact]
    public void Schema_PrazdnaOdpoved_MaPrazdneSeznamy()
    {
        var schema = CacheAgendaProvider.ParseSchema("A", "A", "{}");

        Assert.NotNull(schema);
        Assert.Empty(schema.Fields);
        Assert.Empty(schema.FieldCollections);
    }

    // ── Data k tisku: víc stránek v jedné odpovědi ────────────────────────────

    [Fact]
    public void Data_KazdyVystupniKlicJeSamostatnaStranka()
    {
        var pages = CacheAgendaProvider.ParsePages([
            ("1", """{ "values": { "POPIS_ZASTAVKA": "Praha hl.n." } }"""),
            ("2", """{ "values": { "POPIS_ZASTAVKA": "Pardubice" } }"""),
            ("3", """{ "values": { "POPIS_ZASTAVKA": "Brno" } }"""),
        ]);

        Assert.NotNull(pages);
        Assert.Equal(3, pages.Count);
        Assert.Equal("Pardubice", pages[1].GetValue("POPIS_ZASTAVKA"));
    }

    /// <summary>„ERR" je metadata MLRestu, ne data - kdyby se bral jako stránka, přibyl by
    /// v tisku list navíc. Obsah je schválně stránkového tvaru: jinak by test prošel i bez
    /// přeskočení (nestránkový obsah spadne na parsování a zahodí se stejně).</summary>
    [Fact]
    public void Data_KlicERRSePreskoci()
    {
        var pages = CacheAgendaProvider.ParsePages([
            ("ERR", """{ "values": { "A": "metadata" } }"""),
            ("1",   """{ "values": { "A": "data" } }"""),
        ]);

        Assert.Equal("data", Assert.Single(pages!).GetValue("A"));
    }

    [Fact]
    public void Data_PrazdneAPoskozeneStrankySePreskoci_OstatniZustanou()
    {
        var pages = CacheAgendaProvider.ParsePages([
            ("1", """{ "values": { "A": "1" } }"""),
            ("2", ""),
            ("3", null),
            ("4", "{poškozeno"),
            ("5", """{ "values": { "A": "5" } }"""),
        ]);

        Assert.Equal(2, pages!.Count);
        Assert.Equal("1", pages[0].GetValue("A"));
        Assert.Equal("5", pages[1].GetValue("A"));
    }

    [Fact]
    public void Data_ZadnaPouzitelnaStranka_VratiNic()
    {
        Assert.Null(CacheAgendaProvider.ParsePages([("ERR", "0"), ("1", ""), ("2", "{rozbité")]));
        Assert.Null(CacheAgendaProvider.ParsePages([]));
    }

    // ── Obsah jedné stránky ──────────────────────────────────────────────────

    /// <summary>Caché posílá čísla a booly jako JSON čísla/booly, ne texty - musí se převést,
    /// jinak by čtení hodnoty spadlo na výjimku.</summary>
    [Fact]
    public void Stranka_CislaABoolySePrevedouNaText()
    {
        var dc = CacheAgendaProvider.ParseSinglePageData(JsonNode.Parse("""
            { "values": { "TEXT": "abc", "CELE": 42, "DESETINNE": 3.5,
                          "PRAVDA": true, "NEPRAVDA": false, "NIC": null } }
            """)!);

        Assert.NotNull(dc);
        Assert.Equal("abc", dc.GetValue("TEXT"));
        Assert.Equal("42", dc.GetValue("CELE"));
        Assert.Equal("3.5", dc.GetValue("DESETINNE"));       // desetinná tečka, ne čárka
        Assert.Equal("true", dc.GetValue("PRAVDA"));
        Assert.Equal("false", dc.GetValue("NEPRAVDA"));
    }

    [Fact]
    public void Stranka_NazvyPoliNerozlisujiVelikostPismen()
    {
        var dc = CacheAgendaProvider.ParseSinglePageData(
            JsonNode.Parse("""{ "values": { "CISLO_LINKY": "42" } }""")!);

        Assert.Equal("42", dc!.GetValue("cislo_linky"));
    }

    [Fact]
    public void Stranka_RadkoveKolekceSeNacteou()
    {
        var dc = CacheAgendaProvider.ParseSinglePageData(JsonNode.Parse("""
            { "values": {},
              "rows": {
                "ZASTAVKY": [ { "POPIS": "Praha", "CAS": "8:00" }, { "POPIS": "Brno" } ],
                "POZNAMKY": [ { "ZNAK": "x" } ]
              } }
            """)!);

        var zastavky = dc!.GetRows("ZASTAVKY");
        Assert.Equal(2, zastavky.Count);
        Assert.Equal("Praha", zastavky[0].GetValue("POPIS"));
        Assert.Equal("8:00", zastavky[0].GetValue("CAS"));
        Assert.Single(dc.GetRows("POZNAMKY"));
    }

    /// <summary>Vnořená pole (varianty tras, značky u odjezdu) se musí zachovat jako struktura -
    /// prvky si je parsují samy. Kdyby se převedla na text, přišly by o obsah.</summary>
    [Fact]
    public void Stranka_VnorenaPoleZustanouStrukturou()
    {
        var dc = CacheAgendaProvider.ParseSinglePageData(JsonNode.Parse("""
            { "values": { "VARIANTY_TRAS": [ { "PISMENO": "a" } ] },
              "rows": { "ODJEZDY": [ { "CAS": "8:00", "ANOTACE": [ { "ZNAK": "x" } ] } ] } }
            """)!);

        Assert.IsAssignableFrom<JsonArray>(dc!.GetValue("VARIANTY_TRAS"));
        Assert.IsAssignableFrom<JsonArray>(dc.GetRows("ODJEZDY")[0].GetValue("ANOTACE"));
    }

    [Fact]
    public void Stranka_NeobjektovyRadekAKolekceBezPoleSePreskoci()
    {
        var dc = CacheAgendaProvider.ParseSinglePageData(JsonNode.Parse("""
            { "rows": { "ZASTAVKY": [ "text místo objektu", { "POPIS": "Praha" } ],
                        "NENI_POLE": { "POPIS": "x" } } }
            """)!);

        Assert.Equal("Praha", Assert.Single(dc!.GetRows("ZASTAVKY")).GetValue("POPIS"));
        Assert.Empty(dc.GetRows("NENI_POLE"));
    }

    [Fact]
    public void Stranka_BezHodnotIRadku_JeProstePrazdna()
    {
        var dc = CacheAgendaProvider.ParseSinglePageData(JsonNode.Parse("{}")!);

        Assert.NotNull(dc);
        Assert.Null(dc.GetValue("COKOLI"));
        Assert.Empty(dc.GetRows("COKOLI"));
    }

    // ── Předdefinovaný layout tabulky ────────────────────────────────────────

    [Fact]
    public void LayoutTabulky_SloupceIRadkySeNacteou()
    {
        var def = CacheAgendaProvider.ParseTableTemplate("""
            { "columns": [
                { "header": "Položka", "width": 80, "widthMode": "Mm", "alignment": "Left" },
                { "header": "Hodnota", "width": 25, "widthMode": "percent", "alignment": "Right",
                  "field": "CENA", "format": "0.00" }
              ],
              "rows": [
                { "cells": [ { "content": "Linka" }, { "content": "{CISLO_LINKY}" } ] },
                { "backgroundColor": "#FFEEEEEE", "visibilityExpression": "{POCET} > 0",
                  "cells": [ { "content": "Platnost od", "format": "dd.MM.yyyy" } ] }
              ] }
            """);

        Assert.NotNull(def);
        Assert.Equal(2, def.Columns.Count);
        Assert.Equal("Položka", def.Columns[0].Header);
        Assert.Equal(80f, def.Columns[0].Width);
        Assert.Equal(ColumnWidthMode.Mm, def.Columns[0].WidthMode);
        Assert.Equal(TextHorizontalAlignment.Left, def.Columns[0].Alignment);

        Assert.Equal(ColumnWidthMode.Percent, def.Columns[1].WidthMode);
        Assert.Equal(TextHorizontalAlignment.Right, def.Columns[1].Alignment);
        Assert.Equal("CENA", def.Columns[1].Field);
        Assert.Equal("0.00", def.Columns[1].Format);

        Assert.Equal(2, def.Rows.Count);
        Assert.Equal("{CISLO_LINKY}", def.Rows[0].Cells[1].Content);
        Assert.Equal("#FFEEEEEE", def.Rows[1].BackgroundColor);
        Assert.Equal("{POCET} > 0", def.Rows[1].VisibilityExpression);
        Assert.Equal("dd.MM.yyyy", def.Rows[1].Cells[0].Format);
    }

    [Fact]
    public void LayoutTabulky_StylBunkyVznikneJenKdyzJeCoNastavit()
    {
        var def = CacheAgendaProvider.ParseTableTemplate("""
            { "rows": [ { "cells": [
                { "content": "Bez stylu" },
                { "content": "Tučná", "fontWeight": "Bold" },
                { "content": "Barevná", "textColor": "#FF112233", "backgroundColor": "#FFEEEEEE" },
                { "content": "Netučná", "fontWeight": "Normal" }
              ] } ] }
            """);

        var cells = def!.Rows[0].Cells;
        Assert.Null(cells[0].Style);
        Assert.True(cells[1].Style!.Bold);
        Assert.Equal("#FF112233", cells[2].Style!.TextColor);
        Assert.Equal("#FFEEEEEE", cells[2].Style!.BackgroundColor);
        Assert.Null(cells[3].Style!.Bold);          // „Normal" = nevynucovat, dědí se z kaskády
    }

    [Fact]
    public void LayoutTabulky_ChybejiciSirkaMaVychozi()
    {
        var def = CacheAgendaProvider.ParseTableTemplate("""
            { "columns": [ { "header": "Bez šířky" } ] }
            """);

        Assert.Equal(30f, def!.Columns[0].Width);
    }

    [Fact]
    public void LayoutTabulky_PoskozenaOdpoved_VratiNic()
    {
        Assert.Null(CacheAgendaProvider.ParseTableTemplate("{rozbité"));
    }

    // ── Parametry sestavy ────────────────────────────────────────────────────

    [Fact]
    public void ParametrySestavy_SeNacteou()
    {
        var parameters = CacheAgendaProvider.ParseParameters("""
            [ { "name": "LINKA", "label": "Číslo linky", "type": "string", "defaultValue": "304",
                "group": "Výběr", "visibleWhen": "REZIM=linka" },
              { "name": "RAZENI_VARIANT", "type": "enum", "options": [ "Podle času", "Podle trasy", "" ] },
              { "name": "BEZ_LABELU" } ]
            """);

        Assert.Equal(3, parameters.Count);
        Assert.Equal("Číslo linky", parameters[0].Label);
        Assert.Equal("304", parameters[0].DefaultValue);
        Assert.Equal("Výběr", parameters[0].Group);
        Assert.Equal("REZIM=linka", parameters[0].VisibleWhen);

        Assert.Equal(["Podle času", "Podle trasy"], parameters[1].Options);   // prázdná volba se zahodí
        Assert.Equal("BEZ_LABELU", parameters[2].Label);                      // chybí label → název
        Assert.Equal("string", parameters[2].Type);                           // chybí typ → text
    }

    [Fact]
    public void ParametrySestavy_ParametrBezNazvuSePreskoci()
    {
        var parameters = CacheAgendaProvider.ParseParameters("""
            [ { "label": "Bez názvu" }, { "name": "  " }, { "name": "OK" } ]
            """);

        Assert.Equal("OK", Assert.Single(parameters).Name);
    }

    [Fact]
    public void ParametrySestavy_PoskozenaOdpovedNeboNePole_VratiPrazdno()
    {
        Assert.Empty(CacheAgendaProvider.ParseParameters("{rozbité"));
        Assert.Empty(CacheAgendaProvider.ParseParameters("""{ "name": "objekt místo pole" }"""));
    }
}
