using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Metody agend se jmenují anglickým slovesem a českou věcí (<c>BuildOdjezd</c>, <c>LoadNastaveniNazvu</c>), ne českým
/// slovesem (<c>cache-api.md</c> pravidlo 5, upřesněné 30. 10., <c>B194g</c> R7). Stávající metody s českým slovesem
/// se přejmenují, až se jejich kód v <c>B56</c> stěhuje; do té doby jsou v <see cref="Stavajici"/>. Nový takový
/// název neprojde a seznam se smí jen zmenšovat. České sloveso se pozná podle koncovky infinitivu nebo podle
/// seznamu rozkazovacích tvarů, které se v kódu objevily - je to odhad, ne slovník.
/// </summary>
public class NazvyMetodAgendTests
{
    /// <summary>Anglická slovesa a slova, která české sloveso jen připomínají koncovkou.</summary>
    private static readonly HashSet<string> NeniCeskeSloveso = new(StringComparer.Ordinal)
    {
        "Get", "Set", "Add", "Build", "Load", "Write", "Format", "Parse", "Convert", "Make", "Push", "Read",
        "Find", "Apply", "Resolve", "Preset", "Platnost", "Pocet",
    };

    private static readonly HashSet<string> Rozkazovaci = new(StringComparer.Ordinal)
    {
        "Nacti", "Sluc", "Sestav", "Dopln", "Vypis", "Serad", "Pridej", "Normalizuj", "Normaliz", "Ziskej", "Vyloz",
        "Vyber", "Uprav", "Sloz", "Prirad", "Prevezmi", "Prerovnej", "Najdi", "Spocti", "Zjisti", "Vrat", "Uloz",
        "Vytvor", "Zapis", "Over", "Nastav", "Vloz", "Odeber", "Smaz", "Projdi", "Prepocti",
    };

    private static readonly Regex Infinitiv = new("(at|it|et|ět|ovat|nout|st|ct)$", RegexOptions.Compiled);
    private static readonly Regex Metoda = new(@"^ClassMethod (\w+)\(", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex PrvniSlovo = new("^[A-Z][a-z0-9]*", RegexOptions.Compiled);

    /// <summary>Stávající metody s českým slovesem (stav 30. 10.) - přejmenovat při přesunu v <c>B56</c>.</summary>
    private static readonly HashSet<string> Stavajici = new(StringComparer.Ordinal)
    {
        "BaseAgenda.UpravDatum", "BaseAgenda.NormalizujSeznamZnacek", "BaseAgenda.ZacitDuvodyPrazdne",
        "BaseAgenda.VydatDuvodPrazdne",
        "LinkovyJrAd.DoplnBarvyLinky", "LinkovyJrAd.VypisVylepy", "LinkovyJrAd.VypisVylep", "LinkovyJrAd.SeradSpoje",
        "LinkovyJrAd.NactiTurnusy", "LinkovyJrAd.PridejTurnusySpoje", "LinkovyJrAd.PridatPoznamkyPlatnosti",
        "LinkovyJrAd.PridatPoznamkyNZ", "LinkovyJrAd.PridatPoznamkyAtributu", "LinkovyJrAd.DoplnOmezeniAtributu",
        "LinkovyJrAd.PridatVetyUpresneni", "LinkovyJrAd.SlozVetu", "LinkovyJrAd.PridatPoznamkySpoju",
        "LinkovyJrAd.PridatTextSpoje", "LinkovyJrAd.PridatPoznamkyMistenkovani", "LinkovyJrAd.PridatStaleZnacky",
        "LinkovyJrAd.PridatStalouZnacku", "LinkovyJrAd.PridatPoznamkyLinky", "LinkovyJrAd.SestavTextOmezeni",
        "LinkovyJrAd.NormalizLinku", "LinkovyJrAd.NactiNastaveniNazvu",
        "LinkovyJrAdRozdilovy.NactiStranky", "LinkovyJrAdRozdilovy.SlucStranky", "LinkovyJrAdRozdilovy.PrevezmiSkupinyDnu",
        "LinkovyJrAdRozdilovy.SlucPoznamky", "LinkovyJrAdRozdilovy.DoplnPuvodni", "LinkovyJrAdRozdilovy.SlucZastavky",
        "LinkovyJrAdRozdilovy.VylozUsek", "LinkovyJrAdRozdilovy.PridejZastavku", "LinkovyJrAdRozdilovy.Prerovnej",
        "LinkovyJrMhd.NactiSkupinyProvozu", "LinkovyJrMhd.SeradJakoLegacy", "LinkovyJrMhd.PriradNegativniZnacky",
        "LinkovyJrMhd.PridatPoznamkuSpoje", "LinkovyJrMhd.PridatPoznamkyNZMhd", "LinkovyJrMhd.PridatPoznamkyAtributuMhd",
        "LinkovyJrMhd.PridatStaleZnackyMhd", "LinkovyJrMhd.PridatPoznamkyTrasyMhd", "LinkovyJrMhd.PridatZnackuBodu",
        "LinkovyJrMhd.PridatAtributyZastavekMhd",
        "ZastavkoveJrAd.VydatStrankyZastavky", "ZastavkoveJrAd.NacistSkupinyDnu", "ZastavkoveJrAd.NacistOdjezdy",
        "ZastavkoveJrAd.ZapsatNaObjednani", "ZastavkoveJrAd.VyberZaklTrasu", "ZastavkoveJrAd.NacistSkupinyDnuDleDefinice",
        "ZastavkoveJrAd.PridatRozsah", "ZastavkoveJrAd.PripravitJede", "ZastavkoveJrAd.ZiskejMarker",
        "ZastavkoveJrAd.ProrezatLegenduRozdilu", "ZastavkoveJrAd.SestavPoznamky", "ZastavkoveJrAd.NepoznamkovatSpoj",
        "ZastavkoveJrAd.SestavTextOmezeni", "ZastavkoveJrAd.NormalizLinku", "ZastavkoveJrAd.PouzitNazevProMhd",
        "ZastavkoveJrAd.VypsatSpojeDopravce", "ZastavkoveJrAd.NactiJazyk", "ZastavkoveJrAd.NactiTextyZnacek",
        "ZastavkoveJrAd.NactiNastaveniNazvu",
        "ZastavkoveJrMhd.SestavitVyvesky", "ZastavkoveJrMhd.PripravitTrasu", "ZastavkoveJrMhd.SloucitTotozneZastavky",
        "ZastavkoveJrMhd.NacistPoznamkyAAtributyZastavek", "ZastavkoveJrMhd.PorovnatBitmapyPlatnosti",
        "ZastavkoveJrMhd.PriraditZnackySmoSkupin", "ZastavkoveJrMhd.PriraditNZSpoji",
        "ZastavkoveJrMhd.VypocitatBitmapyObsluhy", "ZastavkoveJrMhd.SestavitStranky", "ZastavkoveJrMhd.VydatStranku",
        "ZastavkoveJrMhd.SestavitJednuStranku", "ZastavkoveJrMhd.DoplnitPoznamkuRozdilTrasy1001",
        "ZastavkoveJrMhd.DoplnitPoznamkuRozdilTrasy1002",
    };

    [Fact]
    public void NovaMetodaAgendyNemaCeskeSloveso()
    {
        var nove = MetodySCeskymSlovesem().Where(m => !Stavajici.Contains(m)).ToList();

        Assert.True(nove.Count == 0,
            "Metoda agendy začíná českým slovesem - použij anglické (Get, Build, Add, Load, Write, Format…) a českou věc " +
            "(cache-api.md pravidlo 5):\n" + string.Join("\n", nove));
    }

    /// <summary>Přejmenovaná nebo smazaná metoda musí ze seznamu zmizet, jinak by do něj šlo vrátit cokoli.</summary>
    [Fact]
    public void SeznamStavajicichJenUbyva()
    {
        var existuji = MetodySCeskymSlovesem().ToHashSet(StringComparer.Ordinal);
        var navic = Stavajici.Where(s => !existuji.Contains(s)).Order().ToList();

        Assert.True(navic.Count == 0,
            "Metoda ze seznamu Stavajici už s českým slovesem neexistuje (přejmenovaná?) - smaž ji ze seznamu:\n" + string.Join("\n", navic));
    }

    private static List<string> MetodySCeskymSlovesem() =>
        Directory.GetFiles(Path.Combine(Koren(), "TemplateDesigner", "CACHE", "ALVA", "TemplateDesigner", "Agendy"), "*.cls")
            .SelectMany(f => Metoda.Matches(File.ReadAllText(f))
                .Select(m => m.Groups[1].Value)
                .Where(JeCeskeSloveso)
                .Select(jmeno => $"{Path.GetFileNameWithoutExtension(f)}.{jmeno}"))
            .ToList();

    private static bool JeCeskeSloveso(string jmeno)
    {
        var prvni = PrvniSlovo.Match(jmeno).Value;
        return !NeniCeskeSloveso.Contains(prvni) && (Infinitiv.IsMatch(prvni) || Rozkazovaci.Contains(prvni));
    }

    private static string Koren()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EDISON2.sln")))
            dir = dir.Parent;
        dir ??= new FileInfo(ThisFile()).Directory!.Parent!.Parent;
        return dir!.FullName;
    }

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
