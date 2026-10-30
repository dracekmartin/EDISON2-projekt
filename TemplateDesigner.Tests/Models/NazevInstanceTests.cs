using System.IO;
using System.Text.RegularExpressions;
using TemplateDesigner.Models;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Models;

/// <summary>
/// Název instance agendy (`B194e` A5): obecné texty návrháře skládá z tvarů a rodu, které hlásí agenda,
/// aby agenda report generátoru netiskla „Žádná vybraná linka nemá data" u oběhů nebo zaměstnanců.
/// </summary>
public class NazevInstanceTests
{
    private static readonly NazevInstance Linka = NazevInstance.Z("linka,linky,linku,linky,linek", "F");
    private static readonly NazevInstance Obeh = NazevInstance.Z("oběh,oběhu,oběh,oběhy,oběhů", "M");
    private static readonly NazevInstance Zamestnanec =
        NazevInstance.Z("zaměstnanec,zaměstnance,zaměstnance,zaměstnanci,zaměstnanců,zaměstnance", "MZ");
    private static readonly NazevInstance Vozidlo = NazevInstance.Z("vozidlo,vozidla,vozidlo,vozidla,vozidel", "N");

    [Fact]
    public void Linka_TextyJakoDosud()
    {
        Assert.Equal("Žádná vybraná linka", Linka.ZadnaVybrana);
        Assert.Equal("vybrané linky", Linka.VybraneAkuzativ);
        Assert.Equal("linku", Linka.Akuzativ);
        Assert.Equal("linky", Linka.Genitiv);
        Assert.Equal("linky", Linka.AkuzativMnozne);   // bez 6. tvaru = 1. pád množného
    }

    [Fact]
    public void ShodaPodleRodu()
    {
        Assert.Equal("Žádný vybraný oběh", Obeh.ZadnaVybrana);
        Assert.Equal("Žádný vybraný zaměstnanec", Zamestnanec.ZadnaVybrana);
        Assert.Equal("Žádné vybrané vozidlo", Vozidlo.ZadnaVybrana);
        Assert.Equal("vybraná vozidla", Vozidlo.VybraneAkuzativ);
        // Mužský životný má 4. pád množného jiný než 1. (Vybrat zaměstnance, ne zaměstnanci).
        Assert.Equal("vybrané zaměstnance", Zamestnanec.VybraneAkuzativ);
        Assert.Equal("zaměstnanci", Zamestnanec.Mnozne);
    }

    [Theory]
    [InlineData(1, "1 linka")]
    [InlineData(3, "3 linky")]
    [InlineData(5, "5 linek")]
    [InlineData(0, "0 linek")]
    public void Pocet(int n, string ocekavano) => Assert.Equal(ocekavano, Linka.Pocet(n));

    /// <summary>Neúplné tvary (starší server, překlep v agendě) dají obecné „instance", ne půl věty.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("linka,linky,linku")]
    [InlineData("linka,,linku,linky,linek")]
    public void NeuplneTvary_Obecny(string? tvary) => Assert.Same(NazevInstance.Obecny, NazevInstance.Z(tvary, "F"));

    [Fact]
    public void Registr_StarsiServerBezTvaru_Obecny()
    {
        var bezTvaru = new AgendaRegistryEntry("X", "X", "C", InstanceLabel: "linka", InstanceLabelPlural: "linky");
        Assert.Same(NazevInstance.Obecny, bezTvaru.Nazev);

        var sTvary = bezTvaru with { InstanceTvary = "oběh,oběhu,oběh,oběhy,oběhů", InstanceRod = "M" };
        Assert.Equal("Žádný vybraný oběh", sTvary.Nazev.ZadnaVybrana);
    }

    // Slovo „link…" v textu, ne v proměnné ({linka}, p.Linka) ani v klíči dat (CISLO_LINKY).
    private static readonly Regex SlovoLinka = new(@"(?<![{.\w])[Ll]ink[a-zěščřžýáíéůú]*\b", RegexOptions.Compiled);
    private static readonly Regex Text = new(@"""[^""\n]*""", RegexOptions.Compiled);

    /// <summary>Texty, které o lince mluví, protože patří k funkci jízdních řádů - s důvodem.</summary>
    private static readonly Dictionary<(string Soubor, string Text), string> Vyjimky = new()
    {
        [("TemplateDesignerViewModel.Columns.cs", "linkového JŘ")] = "rozložení sloupců linkového JŘ, jen agendy JŘ",
        [("TemplateDesignerViewModel.PreviewData.cs", "\"Linka\"")] = "druh stránky podle dat JŘ (obousměrná stránka), jinak „Část\"",
        [("HelpDialogViewModel.cs", "oznámky linky")] = "volba prvku Poznámky „Poznámky linky\"",
        [("HelpDialogViewModel.cs", "linkový JŘ")] = "nápověda k prvku Tabulka spojů, název agendy Linkový JŘ",
    };

    /// <summary>
    /// Obecná okna (hlavní okno, tisk, kontrola, výběr instancí, nápověda F1) „linku" napevno nepíšou - jméno
    /// berou z agendy. Do 30. 10. jich bylo asi 25 a každá nová agenda by je zdědila.
    /// </summary>
    [Fact]
    public void ObecnaOkna_NepisouLinkuNapevno()
    {
        string[] slozky = ["MainWindow", "PrintDialog", "KontrolaVysledkuDialog", "InstancePickerDialog", "HelpDialog"];
        var soubory = slozky
            .SelectMany(s => Directory.GetFiles(RepoCesty.Dotnet("Windows", s)).Where(f => f.EndsWith(".cs") || f.EndsWith(".xaml")))
            .Append(RepoCesty.Dotnet("Models", "KontrolaVystupu.cs"))
            .ToList();
        var nalezy = new List<string>();
        var pouzite = new HashSet<(string, string)>();

        foreach (var soubor in soubory)
        {
            var jmeno = Path.GetFileName(soubor);
            foreach (var (radek, i) in File.ReadLines(soubor).Select((r, i) => (r, i + 1)))
            {
                var t = radek.TrimStart();
                if (t.StartsWith("//", StringComparison.Ordinal) || t.StartsWith("<!--", StringComparison.Ordinal)) continue;
                foreach (Match m in Text.Matches(radek))
                {
                    if (!SlovoLinka.IsMatch(m.Value)) continue;
                    var vyjimka = Vyjimky.Keys.FirstOrDefault(v => v.Soubor == jmeno && m.Value.Contains(v.Text));
                    if (vyjimka != default) { pouzite.Add(vyjimka); continue; }
                    nalezy.Add($"{jmeno}:{i}: {m.Value}");
                }
            }
        }

        Assert.True(soubory.Count > 20, $"prošlo se jen {soubory.Count} souborů - hledání je rozbité");
        Assert.True(nalezy.Count == 0,
            "Obecný text mluví o lince napevno - vezmi jméno z agendy (NazevInstance, `CurrentInstance`):\n" + string.Join("\n", nalezy));
        Assert.Equal(Vyjimky.Keys.OrderBy(k => k.ToString()), pouzite.OrderBy(k => k.ToString()));
    }
}
