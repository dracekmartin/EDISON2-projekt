using System.IO;
using System.Text.RegularExpressions;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Driftová pojistka <b>nabídky dat agend</b> (celý kód agend, <see cref="ZdrojeAgend"/>). Pole aspoň dvou
/// agend JŘ jsou od 30. 10. ve slovníku <c>Jr/NabidkaDat.cls</c> (<c>B56</c> S9); agenda je bere přes
/// <c>AddPole</c> / <c>AddPoleKolekce</c> a test jim dosadí popisek a typ ze slovníku.
///
/// <para><see cref="EDInputFormSchemaDriftTests"/> hlídá schémata <b>formulářů</b> (vlastnosti prvku).
/// Slovník polí, který uživatel vidí v nabídce dat a který jde do AI pomocníka, ale žije v agendách
/// a do 17. 8. ho nehlídalo nic - proto se linkový JŘ rozešel se zastávkovým: kolekce se jmenovaly
/// stejně (<c>ZASTAVKY</c>), ale řádky uvnitř nesly <c>POPIS</c> místo <c>NAZEV_ZASTAVKY</c>.
/// Vypadalo to kompatibilně a nebylo - tabulka s polem <c>{NAZEV_ZASTAVKY}</c> vykreslila
/// v linkovém JŘ tiše prázdno.</para>
///
/// <para><b>Porovnává se v rámci téhož místa</b> (skaláry × konkrétní kolekce), ne globálně:
/// <c>TYP</c> v kolekci zastávek („Typ řádku") a <c>TYP</c> v poznámkách („Typ poznámky") jsou
/// dvě různá pole ve dvou různých skupinách nabídky, ne drift.</para>
///
/// <para>Test čte <c>.cls</c> přímo z repa (offline, bez Caché serveru).</para>
/// </summary>
public class AgendaSchemaDriftTests
{
    /// <summary>
    /// Vědomé rozdíly popisku téhož pole mezi agendami - varianta upřesňuje význam pro svou doménu.
    /// Klíč = <c>&lt;místo&gt;|&lt;pole&gt;</c>. Nová výjimka jen s důvodem, jinak se to zvrhne
    /// v seznam nedodělků.
    /// </summary>
    /// <remarks>Do 23. 10. tu byla obě čísla linky (AD „Číslo linky" × MHD „… (dle licence)").
    /// Od `B193b` nesou obě čísla všechny agendy JŘ, takže se musí všude jmenovat stejně.</remarks>
    private static readonly HashSet<string> ExemptLabel = new(StringComparer.Ordinal)
    {
        // Vývěska MHD nese v CISLO_ZASTAVKA číslo i s označníkem („101-A"), AD jen číslo (Jan 30. 10.).
        "fields|CISLO_ZASTAVKA",
    };

    /// <summary>Vědomé rozdíly typu téhož pole. Prázdné = žádný takový dnes není.</summary>
    private static readonly HashSet<string> ExemptType = new(StringComparer.Ordinal);

    /// <summary>
    /// Vědomé případy, kdy stejný popisek nesou různá pole (<c>B194f</c> G3). Klíč = <c>&lt;místo&gt;|&lt;popisek&gt;</c>,
    /// hodnota = pole, která ho smí nést, a důvod. Výjimka platí jen pro tuhle sadu polí - další pole
    /// pod týmž popiskem test zase zastaví.
    /// </summary>
    private static readonly Dictionary<string, (string[] Keys, string Proc)> ExemptKey = new(StringComparer.Ordinal)
    {
        ["fields|Číslo aktuální zastávky"] = (["AKTUALNI_ZASTAVKA_CZST", "CISLO_ZASTAVKA"],
            "totéž číslo zastávky - vývěska AD ho posílá jako CISLO_ZASTAVKA, linkové JŘ jako " +
            "AKTUALNI_ZASTAVKA_CZST; sjednotit klíč s převodem šablon případně v B56 (Jan 30. 10.)"),
    };

    /// <summary>Pole vyřazená z nabídky dat. Vydané agendy je dál posílají kvůli šablonám, které je
    /// mají, ale nový autor je nesmí najít - jinak se vyřazení nikdy nedokončí.</summary>
    private static readonly (string Key, string Proc)[] Vyrazena =
    [
        ("POPIS_PLATNOSTI", "hotová věta platnosti - každá agenda ji skládala jinak, šablona si ji složí z dat od-do (Jan 23. 10.)"),
        ("PUVODNI_POPIS_PLATNOSTI", "totéž u původní verze rozdílového JŘ"),
        ("TYP", "interní kód pro prvky (znak/specialni/nz/text u poznámek, zastavka/prijezd u řádků) - uživatel s ním nic neudělá (Jan 27. 10.)"),
    ];

    [Fact]
    public void VyrazenaPole_NejsouVNabidceDat()
    {
        var hits = AllFields()
            .SelectMany(e => Vyrazena.Where(v => v.Key == e.Key).Select(v => $"{e.File} {e.Scope}|{e.Key}: {v.Proc}"))
            .Distinct()
            .ToList();

        Assert.True(hits.Count == 0, "Vyřazené pole je zpátky v nabídce dat:\n" + string.Join("\n", hits));
    }

    /// <summary>Totéž pro pole vyřazené jen z jedné agendy - jinde znamená něco jiného a zůstává.</summary>
    private static readonly (string File, string Key, string Proc)[] VyrazenaVAgende =
    [
        ("LinkovyJrAd.cls", "PORADI", "nese tarifní číslo, ne pořadí 1..N jako jinde; v nabídce je „Tarifní číslo“ (Jan 27. 10.)"),
    ];

    [Fact]
    public void VyrazenaPoleAgendy_NejsouVJejiNabidce()
    {
        var hits = AllFields()
            .SelectMany(e => VyrazenaVAgende.Where(v => v.File == e.File && v.Key == e.Key)
                .Select(v => $"{e.File} {e.Scope}|{e.Key}: {v.Proc}"))
            .Distinct()
            .ToList();

        Assert.True(hits.Count == 0, "Vyřazené pole je zpátky v nabídce dat agendy:\n" + string.Join("\n", hits));
    }

    /// <summary>Cíl je proměnná (<c>gf</c>), nebo pole kolekce převzaté z jiné agendy
    /// (<c>kolekce.%Get("fields")</c> - porovnání verzí tak rozšiřuje kolekce linkového JŘ).</summary>
    private static readonly Regex AddFieldRx = new(
        """AddField\(\s*(\w+(?:\.%Get\("fields"\))?)\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*[,)]""",
        RegexOptions.Compiled);

    private static readonly Regex CollectionKeyRx = new(
        "collectionKey\\s*=\\s*\"([A-Z_0-9]+)\"",
        RegexOptions.Compiled);

    /// <summary>Podmínka na kolekci ve smyčce přes převzaté kolekce: <c>d:klic="SPOJE" ..AddField(…)</c>
    /// platí pro řádek, <c>continue:klic'="ZASTAVKY"</c> pro řádky pod ním.</summary>
    private static readonly Regex KlicJeRx = new("(?<!')klic\\s*=\\s*\"([A-Z_0-9]+)\"", RegexOptions.Compiled);
    private static readonly Regex KlicNeniRx = new("klic\\s*'=\\s*\"([A-Z_0-9]+)\"", RegexOptions.Compiled);

    /// <summary>Pole ze slovníku: <c>AddPole(fields, "KLIC")</c>, s nepovinným vlastním popiskem
    /// u vědomého rozdílu (<c>AddPole(fields, "KLIC", "Popisek")</c>).</summary>
    private static readonly Regex AddPoleRx = new(
        """AddPole\(\s*\w+\s*,\s*"([A-Z_0-9]+)"\s*(?:,\s*"([^"]+)"\s*)?\)""", RegexOptions.Compiled);

    private static readonly Regex AddKolekceRx = new(
        """AddKolekce\(\s*\w+\s*,\s*"([A-Z_0-9]+)"\s*\)""", RegexOptions.Compiled);

    private static readonly Regex AddPoleKolekceRx = new(
        """AddPoleKolekce\(\s*\w+\s*,\s*"([A-Z_0-9]+)"\s*,\s*"([A-Z_0-9]+)"\s*\)""", RegexOptions.Compiled);

    private static readonly Regex DefinicePoleRx = new(
        """^\s*s\s+p\("([A-Z_0-9]+)"\)\s*=\s*\$lb\("([^"]+)",\s*"([^"]+)"\)""", RegexOptions.Compiled);

    private static readonly Regex DefinicePoleKolekceRx = new(
        """^\s*s\s+k\("([A-Z_0-9]+)",\s*"([A-Z_0-9]+)"\)\s*=\s*\$lb\("([^"]+)",\s*"([^"]+)"\)""", RegexOptions.Compiled);

    /// <summary><c>ZeSlovniku</c> = agenda pole bere ze slovníku; popisek a typ jsou odtud
    /// (prázdné, když ve slovníku chybí).</summary>
    private record Entry(string Scope, string Key, string Label, string Type, string File, bool ZeSlovniku = false);

    /// <summary>Slovník nabídky dat: <c>&lt;místo&gt;|&lt;pole&gt;</c> → popisek a typ.</summary>
    private static readonly Lazy<Dictionary<string, (string Label, string Type)>> Slovnik = new(() =>
    {
        var d = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
        foreach (var line in File.ReadAllLines(Path.Combine(ZdrojeAgend.Slozka, "Jr", "NabidkaDat.cls")))
        {
            var m = DefinicePoleRx.Match(line);
            if (m.Success)
                d[$"fields|{m.Groups[1].Value}"] = (m.Groups[2].Value, m.Groups[3].Value);
            m = DefinicePoleKolekceRx.Match(line);
            if (m.Success)
                d[$"collection:{m.Groups[1].Value}|{m.Groups[2].Value}"] = (m.Groups[3].Value, m.Groups[4].Value);
        }
        return d;
    });

    private static Entry ZeSlovniku(string scope, string key, string? vlastniPopisek, string file)
    {
        var def = Slovnik.Value.TryGetValue($"{scope}|{key}", out var d) ? d : ("", "");
        return new Entry(scope, key, vlastniPopisek ?? def.Item1, def.Item2, file, ZeSlovniku: true);
    }

    private static List<Entry> ParseAgenda(string path) =>
        ParseLines(File.ReadAllLines(path), Path.GetFileName(path));

    /// <summary>
    /// Vytáhne pole ze schématu jedné agendy. Čte se <b>sekvenčně</b>: dokud nepadne první
    /// <c>collectionKey</c>, jsou pole skalární; každý další <c>collectionKey</c> otevře novou
    /// kolekci. Proměnná držící pole se v agendách recykluje (<c>gf</c>), takže podle názvu
    /// proměnné to poznat nejde. Zakomentovaný řádek se přeskočí - pole, které agenda neposílá,
    /// by test jinak porovnával s ostatními (`G7`).
    /// </summary>
    private static List<Entry> ParseLines(IEnumerable<string> lines, string file)
    {
        var result = new List<Entry>();
        string scope = "fields";
        string? klicNeni = null;

        foreach (var line in lines)
        {
            var t = line.TrimStart();
            if (t.StartsWith("//", StringComparison.Ordinal) || t.StartsWith("#;", StringComparison.Ordinal)
                || t.StartsWith(';')) continue;

            var col = CollectionKeyRx.Match(line);
            if (col.Success) scope = "collection:" + col.Groups[1].Value;
            var kol = AddKolekceRx.Match(line);
            if (kol.Success) scope = "collection:" + kol.Groups[1].Value;
            var neni = KlicNeniRx.Match(line);
            if (neni.Success) klicNeni = neni.Groups[1].Value;

            foreach (Match m in AddPoleRx.Matches(line))
                result.Add(ZeSlovniku("fields", m.Groups[1].Value, m.Groups[2].Success ? m.Groups[2].Value : null, file));
            foreach (Match m in AddPoleKolekceRx.Matches(line))
                result.Add(ZeSlovniku("collection:" + m.Groups[1].Value, m.Groups[2].Value, null, file));

            foreach (Match m in AddFieldRx.Matches(line))
            {
                string s = scope;
                if (m.Groups[1].Value.Contains("%Get", StringComparison.Ordinal))
                {
                    var je = KlicJeRx.Match(line);
                    s = "collection:" + (je.Success ? je.Groups[1].Value : klicNeni ?? "?");
                }
                result.Add(new Entry(s, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value, file));
            }
        }
        return result;
    }

    private static List<Entry> AllFields() =>
        [.. ZdrojeAgend.KodAgend()
            .Where(p => !Path.GetFileName(p).Equals("BaseAgenda.cls", StringComparison.OrdinalIgnoreCase))
            .SelectMany(ParseAgenda)];

    /// <summary>
    /// Pojistka proti tiše zelenému testu: kdyby se změnil zápis schématu a regulární výraz
    /// přestal chytat, ostatní testy projdou na prázdném vstupu a nikdo si toho nevšimne.
    /// </summary>
    [Fact]
    public void Parser_NajdePoleVeVsechAgendach()
    {
        var perFile = AllFields().GroupBy(e => e.File).ToDictionary(g => g.Key, g => g.Count());

        var expected = new[]
        {
            "ZastavkoveJrAd.cls", "ZastavkoveJrMhd.cls", "LinkovyJrAd.cls", "LinkovyJrMhd.cls",
        };
        foreach (var f in expected)
            Assert.True(perFile.GetValueOrDefault(f) >= 10,
                $"{f}: parser našel {perFile.GetValueOrDefault(f)} polí schématu - čekalo se aspoň 10. " +
                "Nejspíš se změnil zápis AddField/collectionKey a test tiše přestal hlídat.");

        Assert.Contains(AllFields(),
            e => e.Scope == "collection:ZASTAVKY" && e.Key == "NAZEV_ZASTAVKY");
        // Porovnání verzí rozšiřuje kolekce linkového JŘ jiným zápisem; do 29. 10. je parser neviděl.
        Assert.Contains(AllFields(), e => e.Scope == "collection:SPOJE" && e.Key == "VERZE");
        Assert.Contains(AllFields(), e => e.Scope == "collection:ZASTAVKY" && e.Key == "TC_PUVODNI");
        Assert.DoesNotContain(AllFields(), e => e.Scope == "collection:?");
    }

    [Fact]
    public void Parser_PreskociZakomentovanePole()
    {
        string[] radky =
        [
            "        d ..AddField(fields, \"CISLO_LINKY\", \"Číslo linky\", \"text\")",
            "        // d ..AddField(fields, \"STARE_POLE\", \"Staré pole\", \"text\")",
            "        #; d ..AddField(fields, \"JESTE_STARSI\", \"Ještě starší\", \"text\")",
        ];

        var pole = ParseLines(radky, "test.cls");

        Assert.Equal(["CISLO_LINKY"], pole.Select(e => e.Key));
    }

    [Fact]
    public void Parser_CteSlovnikAPoleZNej()
    {
        Assert.True(Slovnik.Value.Count(d => d.Key.StartsWith("fields|", StringComparison.Ordinal)) >= 20,
            "Ze slovníku Jr/NabidkaDat.cls se nenačetla pole stránky - změnil se zápis definic?");
        Assert.True(Slovnik.Value.Count(d => d.Key.StartsWith("collection:", StringComparison.Ordinal)) >= 15,
            "Ze slovníku Jr/NabidkaDat.cls se nenačetla pole kolekcí - změnil se zápis definic?");

        string[] radky =
        [
            "    d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, \"CISLO_LINKY\")",
            "    d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, \"CISLO_ZASTAVKA\", \"Vlastní popisek\")",
            "    // d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPole(fields, \"SMER\")",
            "    s pole = ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddKolekce(collections, \"ZASTAVKY\")",
            "    d ##class(ALVA.TemplateDesigner.Agendy.Jr.NabidkaDat).AddPoleKolekce(pole, \"ZASTAVKY\", \"ZONY\")",
            "    d ..AddField(pole, \"TC\", \"Tarifní číslo\", \"number\")",
        ];

        var pole = ParseLines(radky, "test.cls");

        Assert.Equal(
            [("fields", "CISLO_LINKY", "Číslo linky (dle licence)"), ("fields", "CISLO_ZASTAVKA", "Vlastní popisek"),
             ("collection:ZASTAVKY", "ZONY", "Zóny"), ("collection:ZASTAVKY", "TC", "Tarifní číslo")],
            pole.Select(e => (e.Scope, e.Key, e.Label)));
    }

    /// <summary>Pole, které agenda bere ze slovníku a slovník ho nezná, by na serveru shodilo celou
    /// nabídku dat (<c>&lt;UNDEFINED&gt;</c>) - tady se to pozná dřív, než se třída nasadí.</summary>
    [Fact]
    public void PoleZeSlovniku_JsouVeSlovniku()
    {
        var chybi = AllFields()
            .Where(e => e.ZeSlovniku && !Slovnik.Value.ContainsKey($"{e.Scope}|{e.Key}"))
            .Select(e => $"{e.File} {e.Scope}|{e.Key}")
            .Distinct()
            .ToList();

        Assert.True(chybi.Count == 0, "Agenda bere ze slovníku Jr/NabidkaDat.cls pole, které v něm není:\n" + string.Join("\n", chybi));
    }

    /// <summary>Pravidlo druhého použití (R8): do slovníku patří pole, které nabízejí aspoň dvě agendy;
    /// pole jedné agendy si agenda přidá sama. Jinak by se slovník plnil „pro jistotu".</summary>
    [Fact]
    public void Slovnik_DrziJenPoleAsponDvouAgend()
    {
        var pouziti = AllFields().Where(e => e.ZeSlovniku)
            .GroupBy(e => $"{e.Scope}|{e.Key}")
            .ToDictionary(g => g.Key, g => g.Select(e => e.File).Distinct().Count(), StringComparer.Ordinal);

        var malo = Slovnik.Value.Keys
            .Where(k => pouziti.GetValueOrDefault(k) < 2)
            .Select(k => $"{k}: {pouziti.GetValueOrDefault(k)}× ")
            .ToList();

        Assert.True(malo.Count == 0,
            "Pole ve slovníku nabídky dat nabízí méně než dvě agendy - přesuň ho do agendy (AddField), nebo smaž:\n" +
            string.Join("\n", malo));
    }

    /// <summary>Pole, které slovník má, agenda nepíše znovu s vlastním popiskem přes <c>AddField</c> -
    /// jinak by se popisek psal zase na dvou místech a slovník by nic nehlídal.</summary>
    [Fact]
    public void PoleZeSlovniku_AgendaNepiseZnovu()
    {
        var znovu = AllFields()
            .Where(e => !e.ZeSlovniku && Slovnik.Value.ContainsKey($"{e.Scope}|{e.Key}"))
            .Select(e => $"{e.File} {e.Scope}|{e.Key}")
            .Distinct()
            .ToList();

        Assert.True(znovu.Count == 0,
            "Agenda přidává přes AddField pole, které má slovník Jr/NabidkaDat.cls - použij AddPole / AddPoleKolekce:\n" +
            string.Join("\n", znovu));
    }

    /// <summary>Společné pořadí skalárních polí agend JŘ: linka → dopravce → platnost → zastávka a směr →
    /// poznámky a doplňky (Jan 27. 10., `B194` D5). Nabídka dat pole nepřeřazuje, takže by stejné pole
    /// bylo v každé agendě jinde. Pole, které tu není, se nekontroluje - nové pole JŘ sem patří.</summary>
    private static readonly string[] PoradiPoliJr =
    [
        "CISLO_LINKY", "LOKALNI_OZNACENI_LINKY", "TITULEK", "POPIS_LINKY", "TYP_JR", "BARVA_LINKY", "BARVA_LINKY_2",
        "POPIS_DOPRAVCE", "DOPRAVCI_SOUHRNNY_JR", "NAZEV_DOPRAVCE", "ADRESA_DOPRAVCE", "TELEFON_DOPRAVCE",
        "EMAIL_DOPRAVCE", "ICO_DOPRAVCE",
        "DATUM_PLATNOSTI_OD", "DATUM_PLATNOSTI_DO",
        "POPIS_ZASTAVKA", "POPIS_ZASTAVKA_CIS", "CISLO_ZASTAVKA", "CISLO_ZASTAVKA_CIS", "AKTUALNI_ZASTAVKA_CZST",
        "AKTUALNI_ZASTAVKA_TC", "VYLEP_ZASTAVKY", "NASTUPISTE", "SLOUPEK", "SLOUPEK_POPIS", "SMER",
        "VYCHOZI_ZASTAVKA", "KONECNA_ZASTAVKA", "KONECNA_ZASTAVKA_CIS", "CISLO_ZASTAVKA_VYCHOZI_CIS",
        "CISLO_ZASTAVKA_CILOVA_CIS",
        "POZNAMKA_NAD_LINKU", "POZNAMKA_POD_DOPRAVCE", "INFORMACE", "VYLUKOVE_TEXTY", "QR_KOD",
    ];

    [Fact]
    public void SkalarniPoleJr_MajiSpolecnePoradi()
    {
        var hits = new List<string>();
        foreach (var agenda in AllFields().Where(e => e.Scope == "fields").GroupBy(e => e.File))
        {
            var poradi = agenda.Select(e => Array.IndexOf(PoradiPoliJr, e.Key)).Where(i => i >= 0).ToList();
            for (int i = 1; i < poradi.Count; i++)
                if (poradi[i] < poradi[i - 1])
                    hits.Add($"{agenda.Key}: {PoradiPoliJr[poradi[i]]} stojí za {PoradiPoliJr[poradi[i - 1]]}");
        }
        Assert.True(hits.Count == 0, "Pole agendy JŘ mimo společné pořadí:\n" + string.Join("\n", hits));
    }

    [Fact]
    public void StejnePole_MaVeVsechAgendachStejnyPopisek()
    {
        var drift = AllFields()
            .GroupBy(e => $"{e.Scope}|{e.Key}")
            .Where(g => !ExemptLabel.Contains(g.Key))
            .Select(g => (Id: g.Key, Labels: g.GroupBy(e => e.Label).ToList()))
            .Where(x => x.Labels.Count > 1)
            .Select(x => $"{x.Id}: " + string.Join(" × ", x.Labels.Select(
                l => $"\"{l.Key}\" ({string.Join(",", l.Select(e => e.File).Distinct())})")))
            .ToList();

        Assert.True(drift.Count == 0,
            "Totéž pole má v různých agendách různý popisek - uživatel by v nabídce dat viděl " +
            "dva názvy pro jednu věc (kanon ux-conventions §8). Sjednoť, nebo doplň výjimku s důvodem:\n" +
            string.Join("\n", drift));
    }

    /// <summary>
    /// Opačný směr k <see cref="StejnePole_MaVeVsechAgendachStejnyPopisek"/>: stejný popisek patří jednomu
    /// poli. Jinak uživatel vidí v nabídce dvou agend stejný název, ale šablona z jedné agendy ve druhé
    /// vykreslí prázdno, protože pole se jmenuje jinak (odtud nálezy <c>D1</c> a <c>D3</c>).
    /// </summary>
    [Fact]
    public void StejnyPopisek_PatriJednomuPoli()
    {
        var drift = AllFields()
            .GroupBy(e => $"{e.Scope}|{e.Label}")
            .Select(g => (Id: g.Key, Keys: g.GroupBy(e => e.Key).ToList()))
            .Where(x => x.Keys.Count > 1)
            .Where(x => !(ExemptKey.TryGetValue(x.Id, out var v) && x.Keys.Select(k => k.Key).Order().SequenceEqual(v.Keys.Order())))
            .Select(x => $"{x.Id}: " + string.Join(" × ", x.Keys.Select(
                k => $"{k.Key} ({string.Join(",", k.Select(e => e.File).Distinct())})")))
            .ToList();

        Assert.True(drift.Count == 0,
            "Stejný popisek nesou v nabídce dat různá pole - uživatel nepozná, že jde o dvě věci, a šablona " +
            "z jedné agendy ve druhé vykreslí prázdno. Rozliš popisky, nebo doplň výjimku s důvodem:\n" +
            string.Join("\n", drift));
    }

    [Fact]
    public void StejnePole_MaVeVsechAgendachStejnyTyp()
    {
        var drift = AllFields()
            .GroupBy(e => $"{e.Scope}|{e.Key}")
            .Where(g => !ExemptType.Contains(g.Key))
            .Select(g => (Id: g.Key, Types: g.GroupBy(e => e.Type).ToList()))
            .Where(x => x.Types.Count > 1)
            .Select(x => $"{x.Id}: " + string.Join(" × ", x.Types.Select(
                t => $"{t.Key} ({string.Join(",", t.Select(e => e.File).Distinct())})")))
            .ToList();

        Assert.True(drift.Count == 0,
            "Totéž pole je v různých agendách jiného typu - formátování hodnoty by se lišilo " +
            "podle toho, ze které agendy data přišla:\n" + string.Join("\n", drift));
    }

    [Fact]
    public void ZadneZakazaneTerminy_VPopiscichPoliAgend()
    {
        var hits = new List<string>();
        foreach (var e in AllFields())
        {
            if (EDInputFormSchemaDriftTests.BannedExactLabels.Contains(e.Label))
                hits.Add($"{e.File} {e.Scope}|{e.Key}: \"{e.Label}\"");
            else
                foreach (var sub in EDInputFormSchemaDriftTests.BannedLabelSubstrings)
                    if (e.Label.Contains(sub, StringComparison.Ordinal))
                        hits.Add($"{e.File} {e.Scope}|{e.Key}: \"{e.Label}\" (obsahuje \"{sub}\")");
        }

        Assert.True(hits.Count == 0,
            "Zakázané termíny v popiscích polí agend (kanon ux-conventions §8):\n" +
            string.Join("\n", hits.Distinct()));
    }
}
