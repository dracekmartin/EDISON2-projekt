using System.IO;
using System.Text.RegularExpressions;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>Pole formuláře prvku (<c>*Edit.cls</c>) tak, jak ho uvidí uživatel: klíč, popisek, skupina, druh, meze, jednotka, volby.</summary>
internal sealed record PoleFormulare(
    string Klic, string Popisek, string Skupina, string Druh,
    string? Min, string? Max, string Jednotka, List<(string Kod, string Popisek)> Volby);

/// <summary>
/// Čte formuláře prvků z <c>.cls</c> offline. Sdílené části (<c>StyleFields.Add</c>, <c>BaseElementFields.Add*</c>) rozbalí
/// s parametry volání - předponou klíče a popisku, výchozími hodnotami z hlavičky metody a podmíněnými bloky
/// (<c>i bunka {</c>, <c>i withCorners {</c>, <c>i defHeight = "" {</c>). Podmínky v samotném formuláři prvku se
/// nevyhodnocují, bere se každé pole. Zápis formulářů je pravidelný; kdyby se změnil, poznají to testy podle počtu polí.
/// </summary>
internal static class FormularePrvku
{
    private static readonly Regex Skupina = new(@"form\.AddGroup\(\s*(?<a>[^,]+),\s*(?<b>[^,)]+)", RegexOptions.Compiled);
    private static readonly Regex PridaniPole = new(@"form\.(?<m>AddFieldToLastGroup|AddDecimal|AddNumber|AddBool|AddText)\((?<args>.*)\)", RegexOptions.Compiled);
    private static readonly Regex Volba = new(@"AddListItem\(\s*""(?<kod>[^""]*)"",\s*""(?<popisek>[^""]*)""\)", RegexOptions.Compiled);
    private static readonly Regex Pomocnik = new(@"##class\(ALVA\.TemplateDesigner\.EDInputForm\.(?<trida>StyleFields|BaseElementFields)\)\.(?<metoda>\w+)\((?<args>.*)\)", RegexOptions.Compiled);
    private static readonly Regex Hlavicka = new(@"^ClassMethod (?<metoda>\w+)\((?<par>.*)\)", RegexOptions.Compiled);

    public static string Slozka => RepoCesty.Cache("EDInputForm");

    /// <summary>Pole formuláře <paramref name="trida"/> (bez přípony), v pořadí, jak se přidávají.</summary>
    public static List<PoleFormulare> Pole(string trida)
    {
        var vysledek = new List<PoleFormulare>();
        var skupina = "";
        Projdi(File.ReadAllLines(Path.Combine(Slozka, trida + ".cls")), new Dictionary<string, string>(), vysledek, ref skupina, vyhodnocovatPodminky: false);
        return vysledek;
    }

    private static void Projdi(IEnumerable<string> radky, Dictionary<string, string> promenne, List<PoleFormulare> vysledek,
        ref string skupina, bool vyhodnocovatPodminky)
    {
        // Zásobník podmíněných bloků: true = blok platí.
        var bloky = new Stack<bool>();
        foreach (var radek in radky)
        {
            var t = radek.Trim();
            if (t.StartsWith("//", StringComparison.Ordinal) || t.StartsWith("///", StringComparison.Ordinal)) continue;

            if (vyhodnocovatPodminky)
            {
                var blok = Regex.Match(t, @"^i\s+(?<podminka>.+?)\s*\{$");
                if (blok.Success) { bloky.Push(Plati(blok.Groups["podminka"].Value, promenne)); continue; }
                if (Regex.IsMatch(t, @"^\}\s*else\s*\{$") && bloky.Count > 0) { bloky.Push(!bloky.Pop()); continue; }
                if (t == "}" && bloky.Count > 0) { bloky.Pop(); continue; }
                if (bloky.Any(b => !b)) continue;
                // Jednořádková podmínka „i newGroup d form.AddGroup(…)".
                var radkova = Regex.Match(t, @"^i\s+(?<podminka>\w+)\s+d\s");
                if (radkova.Success && !Plati(radkova.Groups["podminka"].Value, promenne)) continue;
            }

            var sk = Skupina.Match(t);
            if (sk.Success) { skupina = Hodnota(sk.Groups["b"].Value, promenne); continue; }

            var pom = Pomocnik.Match(t);
            if (pom.Success) { Rozbal(pom.Groups["trida"].Value, pom.Groups["metoda"].Value, Argumenty(pom.Groups["args"].Value), vysledek, ref skupina); continue; }

            var p = PridaniPole.Match(t);
            if (p.Success)
            {
                var a = Argumenty(p.Groups["args"].Value).Select(x => Hodnota(x, promenne)).ToList();
                string Arg(int i) => i < a.Count ? a[i] : "";
                var (druh, min, max, jednotka) = p.Groups["m"].Value switch
                {
                    "AddDecimal" => ("decimal", Arg(3), Arg(4), Arg(5)),
                    "AddNumber" => ("number", Arg(3), Arg(4), ""),
                    "AddBool" => ("bool", (string?)null, (string?)null, ""),
                    "AddText" => ("text", null, null, ""),
                    _ => (Arg(2).Replace("$$$FieldType", "", StringComparison.Ordinal).ToLowerInvariant(), null, null, ""),
                };
                vysledek.Add(new PoleFormulare(Arg(0), Arg(1), skupina, druh, min, max, jednotka, []));
            }

            foreach (Match v in Volba.Matches(t))
                if (vysledek.Count > 0) vysledek[^1].Volby.Add((v.Groups["kod"].Value, v.Groups["popisek"].Value));
        }
    }

    /// <summary>Rozbalí volání sdílené části: parametry z hlavičky metody (s výchozími hodnotami) dosadí z argumentů.</summary>
    private static void Rozbal(string trida, string metoda, List<string> argumenty, List<PoleFormulare> vysledek, ref string skupina)
    {
        var radky = File.ReadAllLines(Path.Combine(Slozka, trida + ".cls"));
        int od = Array.FindIndex(radky, r => Hlavicka.Match(r) is { Success: true } m && m.Groups["metoda"].Value == metoda);
        if (od < 0) throw new InvalidOperationException($"{trida}.{metoda} ve formulářích není");

        var parametry = Hlavicka.Match(radky[od]).Groups["par"].Value.Split(',').Select(p => p.Trim()).ToList();
        var promenne = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < parametry.Count; i++)
        {
            var jmeno = parametry[i].Split(' ')[0];
            var vychozi = parametry[i].Contains('=') ? parametry[i][(parametry[i].IndexOf('=') + 1)..].Trim().Trim('"') : "";
            promenne[jmeno] = i < argumenty.Count ? argumenty[i].Trim().Trim('"') : vychozi;
        }

        int konec = Array.FindIndex(radky, od + 1, r => r == "}");
        Projdi(radky[(od + 2)..konec], promenne, vysledek, ref skupina, vyhodnocovatPodminky: true);
    }

    /// <summary>Podmínka sdílené části: proměnná (pravdivá, když není prázdná ani 0), nebo „proměnná = """.</summary>
    private static bool Plati(string podminka, Dictionary<string, string> promenne)
    {
        var rovno = Regex.Match(podminka, @"^(\w+)\s*=\s*""""$");
        if (rovno.Success) return promenne.GetValueOrDefault(rovno.Groups[1].Value, "") == "";
        var hodnota = promenne.GetValueOrDefault(podminka.Trim(), "");
        return hodnota is not ("" or "0");
    }

    /// <summary>Hodnota výrazu: literál, proměnná, nebo jejich spojení přes <c>_</c> (<c>prefix_".FontSize"</c>).</summary>
    private static string Hodnota(string vyraz, Dictionary<string, string> promenne) =>
        // „_" je v ObjectScriptu spojení textů, proto ho jméno proměnné obsahovat nemůže.
        string.Concat(Regex.Matches(vyraz.Trim(), @"""((?:[^""]|"""")*)""|[A-Za-z0-9$.+#-]+")
            .Select(m => m.Value.StartsWith('"') ? m.Groups[1].Value.Replace("\"\"", "\"")
                : promenne.TryGetValue(m.Value, out var v) ? v : m.Value));

    /// <summary>Argumenty volání oddělené čárkou mimo uvozovky a závorky.</summary>
    private static List<string> Argumenty(string text)
    {
        var vysledek = new List<string>();
        int hloubka = 0, zacatek = 0;
        bool vTextu = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '"') vTextu = !vTextu;
            else if (!vTextu && c == '(') hloubka++;
            else if (!vTextu && c == ')') { if (hloubka == 0) { text = text[..i]; break; } hloubka--; }
            else if (!vTextu && hloubka == 0 && c == ',') { vysledek.Add(text[zacatek..i].Trim()); zacatek = i + 1; }
        }
        if (zacatek <= text.Length) vysledek.Add(text[zacatek..].Trim());
        return vysledek;
    }
}
