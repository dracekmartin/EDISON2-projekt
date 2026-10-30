using System.IO;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Klíče, které si klient a server posílají a obě strany je mají zapsané samostatně: definice vlastních sloupců,
/// známé klíče definice parametru a typy sekcí ve formuláři sekce. Do 30. 10. je nic neporovnávalo;
/// překlep na jedné straně se projevil jen tím, že se hodnota tiše ztratila.
/// </summary>
public class KliceKontraktuTests
{
    private static readonly DepartureColumnDefinition PlnySloupec = new()
    {
        Id = "1", Header = "a", Header2 = "b", Days = "X", IncludeMarks = "1", ExcludeMarks = "2", SuppressedMarks = "3", Period = "1.7.-31.8.",
    };

    /// <summary>Klíče jednoho sloupce, jak je klient posílá v <c>DEFINICE_SLOUPCU</c>.</summary>
    private static HashSet<string> KliceKlienta()
    {
        var json = JsonNode.Parse(ColumnLayoutWire.Build([PlnySloupec])!)!;
        return [.. json["sloupce"]![0]!.AsObject().Select(kv => kv.Key)];
    }

    /// <summary>Server čte (<c>ParseDefiniceSloupcu</c>) tytéž klíče, které klient posílá.</summary>
    [Fact]
    public void DefiniceSloupcu_ServerCteKliceKlienta()
    {
        var zdroj = File.ReadAllText(RepoCesty.Cache("Agendy", "Jr", "VlastniSloupce.cls"));
        var metoda = Regex.Match(zdroj, @"ClassMethod ParseDefiniceSloupcu.*?\n\}", RegexOptions.Singleline).Value;
        var cteServer = Regex.Matches(metoda, @"sl\.%Get\(""(\w+)""\)").Select(m => m.Groups[1].Value).ToHashSet();

        Assert.Equal(KliceKlienta().Order(), cteServer.Order());
    }

    /// <summary>Vzory sloupců (<c>PRESETY_SLOUPCU</c>) staví server v témž tvaru a klient je čte stejnými klíči.</summary>
    [Fact]
    public void VzorySloupcu_MajiKliceDefinice()
    {
        var klient = KliceKlienta();
        var cteKlient = Regex.Matches(File.ReadAllText(RepoCesty.Dotnet("Models", "Elements", "DepartureColumnPresets.cs")),
            @"Text\(s, ""(\w+)""\)").Select(m => m.Groups[1].Value).ToHashSet();
        Assert.Equal(klient.Order(), cteKlient.Order());

        foreach (var (trida, metoda) in new[] { ("ZastavkoveJrAd.cls", "PresetSloupce"), ("LinkovyJrAd.cls", "PresetSloupceLinky") })
        {
            var zdroj = File.ReadAllText(RepoCesty.Cache("Agendy", trida));
            var telo = Regex.Match(zdroj, $@"ClassMethod {metoda}\(.*?\n\}}", RegexOptions.Singleline).Value;
            var piseServer = Regex.Matches(telo, @"sl\.%Set\(""(\w+)""").Select(m => m.Groups[1].Value).ToList();
            Assert.NotEmpty(piseServer);
            Assert.All(piseServer, k => Assert.True(klient.Contains(k), $"{trida} {metoda} posílá klíč „{k}\", který definice sloupců nezná"));
        }
    }

    /// <summary>Klíče definice parametru, které návrhář čte - klient (<c>CacheAgendaProvider.ParseParameters</c>) a formulář
    /// parametrů na serveru (<c>AgendaParams.cls</c>, nápověda) - jsou přesně ty, které Caché test kontraktu
    /// (<c>KontraktAgendTest.ZNAMEKLICEPARAMETRU</c>) agendám dovoluje poslat.</summary>
    [Fact]
    public void KliceParametru_KontraktZnaPraveTyCoNavrharCte()
    {
        var zdroj = File.ReadAllText(RepoCesty.Dotnet("Serialization", "CacheAgendaProvider.cs"));
        var metoda = Regex.Match(zdroj, @"static List<TemplateParameter> ParseParameters.*?\n    \}", RegexOptions.Singleline).Value;
        var cteKlient = Regex.Matches(metoda, @"item\[""(\w+)""\]").Select(m => m.Groups[1].Value).ToHashSet();
        var cteFormular = Regex.Matches(File.ReadAllText(RepoCesty.Cache("EDInputForm", "AgendaParams.cls")), @"\bp\.(\w+)")
            .Select(m => m.Groups[1].Value).ToHashSet();

        var kontrakt = Regex.Match(File.ReadAllText(RepoCesty.Cache("Tests", "KontraktAgendTest.cls")),
            @"Parameter ZNAMEKLICEPARAMETRU = ""([^""]+)""").Groups[1].Value.Split(',').ToHashSet();

        Assert.True(cteKlient.Count > 10, $"v ParseParameters se našlo jen {cteKlient.Count} klíčů - hledání je rozbité");
        Assert.Equal(cteKlient.Union(cteFormular).Order(), kontrakt.Order());
    }

    /// <summary>Typy sekce ve formuláři sekce (<c>SectionEdit.cls</c>) odpovídají výčtu <see cref="TypSekce"/>
    /// a názvům, kterými je pojmenovává strom šablony.</summary>
    [Fact]
    public void TypySekce_FormularOdpovidaKodu()
    {
        var zdroj = File.ReadAllText(RepoCesty.Cache("EDInputForm", "SectionEdit.cls"));
        var blok = Regex.Match(zdroj, @"AddFieldToLastGroup\(""Typ"".*?(?=\n\s*s f\.value)", RegexOptions.Singleline).Value;
        var volby = Regex.Matches(blok, @"AddListItem\(""(\w+)"", ""([^""]+)""\)")
            .Select(m => (Kod: m.Groups[1].Value, Popisek: m.Groups[2].Value)).ToList();

        Assert.Equal(Enum.GetNames<TypSekce>().Order(), volby.Select(v => v.Kod).Order());
        Assert.All(volby, v => Assert.Equal(v.Popisek, TemplateTreeNames.NazevTypuSekce(Enum.Parse<TypSekce>(v.Kod))));
    }
}
