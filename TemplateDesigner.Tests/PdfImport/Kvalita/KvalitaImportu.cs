using System.IO;
using System.Text.Json;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.PdfImport;

namespace TemplateDesigner.Tests.PdfImport.Kvalita;

/// <summary>
/// Co má import z předlohy vrátit - v souřadnicích stránky (mm od levého horního rohu papíru).
/// U našeho tisku se odvodí ze zdrojové šablony (<see cref="KvalitaImportu.ZeSablony"/>), u skutečného
/// PDF se zapíše ručně do <c>sada/&lt;jméno&gt;.ocekavani.json</c>.
/// </summary>
public sealed record OcekavaniImportu
{
    public string Nazev { get; init; } = "";
    /// <summary>„pod" = poznámky pod tabulkou, „vedle" = v téže sekci vedle ní.</summary>
    public string Kostra { get; init; } = "pod";
    public bool? Paticka { get; init; }
    public List<OcekavanyText> Texty { get; init; } = [];
    public OcekavanaTabulka? Tabulka { get; init; }
    public OcekavanePoznamky? Poznamky { get; init; }
}

/// <summary>Text hlavičky nebo patičky. <c>Obsah</c> s poli („Platí od {DATUM_PLATNOSTI_OD}"),
/// <c>Doslovne</c> tak, jak je vytištěný; <c>X</c>, <c>Y</c> = <b>střed napsaného textu</b>.</summary>
/// <remarks>Střed, ne roh prvku: import text do prvku vystředí, kdežto zdroj ho může mít vlevo
/// v širokém prvku - na papíře totéž místo, rohy prvků o centimetry jinde.</remarks>
public sealed record OcekavanyText(string Obsah, string Doslovne, float X, float Y);

public sealed record OcekavanaTabulka
{
    public float X { get; init; }
    public float Y { get; init; }
    public float Sirka { get; init; }
    /// <summary>Popisné sloupce zleva doprava, jak jsou na papíře vidět.</summary>
    public List<string> Sloupce { get; init; } = [];
    public float? PismoBunekPt { get; init; }
    public string? PozadiZahlavi { get; init; }
    public string? PismoZahlavi { get; init; }
    /// <summary>Barva podbarvení lichých Tč; prázdné = bez podbarvení, null = nekontrolovat.</summary>
    public string? LicheTc { get; init; }
    public float? MezeraMeziSkupinami { get; init; }
}

public sealed record OcekavanePoznamky
{
    public float? X { get; init; }
    public float? Sirka { get; init; }
    public int? Sloupce { get; init; }
    public bool? Zahlavi { get; init; }
    public string? Pismo { get; init; }
}

/// <summary>Jedna kontrola výsledku importu proti očekávání.</summary>
public sealed record Kontrola(string Oblast, string Nazev, bool Ok, string Detail);

public sealed record Hodnoceni(IReadOnlyList<Kontrola> Kontroly, IReadOnlyList<float> OdchylkyTextuMm)
{
    public int Splneno => Kontroly.Count(k => k.Ok);
    public float? PrumernaOdchylkaTextuMm => OdchylkyTextuMm.Count > 0 ? OdchylkyTextuMm.Average() : null;
}

/// <summary>
/// Měření kvality importu z PDF: očekávání odvozené ze zdrojové šablony nebo zapsané ručně
/// a porovnání výsledné šablony s ním. Bez měření se ladilo podle jednoho běhu, který se příště
/// nezopakoval (2. 10.) - tohle dává číslo, podle kterého jde poznat, jestli změna pomohla.
/// </summary>
public static class KvalitaImportu
{
    private const float TolerancePolohyTextuMm = 2f;
    private const float TolerancePolohyTabulkyMm = 3f;
    private const float ToleranceSirkyMm = 4f;
    private const int ToleranceBarvy = 8;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static OcekavaniImportu Nacti(string cesta) =>
        JsonSerializer.Deserialize<OcekavaniImportu>(File.ReadAllText(cesta), Json)!;

    public static string DoJson(OcekavaniImportu o) => JsonSerializer.Serialize(o, Json);

    // ── Poloha prvků na stránce ─────────────────────────────────────────

    /// <summary>
    /// Kde prvek leží na papíře, jak to vidí návrh: hlavičky odshora, první obsah pod nimi,
    /// patičky odspodu. Obsah za první sekcí obsahu má polohu podle dat, takže null.
    /// </summary>
    public static Dictionary<TemplateElement, (float X, float? Y)> PoziceNaStrance(Template t)
    {
        var ps = t.PageSize;
        var vysledek = new Dictionary<TemplateElement, (float X, float? Y)>();
        float shora = ps.MarginTop;
        bool obsahZacal = false, prvniObsah = true;

        foreach (var s in t.Sections.Where(s => s.Role != SectionRole.Footer))
        {
            bool hlavicka = s.Role == SectionRole.Header
                            || (s.Role == SectionRole.Body && s.PrintBehavior == SectionPrintBehavior.OnEveryPage && !obsahZacal);
            float? top = null;
            if (hlavicka) top = shora;
            else if (prvniObsah) { top = shora; prvniObsah = false; obsahZacal = true; }

            foreach (var e in s.Elements)
                vysledek[e] = (ps.MarginLeft + e.X, top is float y ? y + e.Y : null);
            if (hlavicka) shora += s.DesignerHeight ?? 0f;
        }

        float zdola = ps.Height - ps.MarginBottom;
        foreach (var s in t.Sections.Where(s => s.Role == SectionRole.Footer).Reverse())
        {
            float top = zdola - (s.DesignerHeight ?? 0f);
            foreach (var e in s.Elements) vysledek[e] = (ps.MarginLeft + e.X, top + e.Y);
            zdola = top;
        }
        return vysledek;
    }

    // ── Očekávání ze zdrojové šablony ───────────────────────────────────

    /// <summary>Co má vyjít z PDF, které ze šablony <paramref name="t"/> s daty <paramref name="data"/>
    /// vytiskl náš tisk; <paramref name="vytisk"/> je jeho první stránka (odtud středy textů).</summary>
    public static OcekavaniImportu ZeSablony(Template t, DataContext data, ExtractedPage vytisk, string nazev)
    {
        var pozice = PoziceNaStrance(t);
        var hlavickyAPaticky = t.Sections
            .Where(s => s.Role is SectionRole.Header or SectionRole.Footer
                        || (s.Role == SectionRole.Body && s.PrintBehavior == SectionPrintBehavior.OnEveryPage))
            .SelectMany(s => s.Elements);

        var texty = new List<OcekavanyText>();
        foreach (var te in hlavickyAPaticky.OfType<TextElement>())
        {
            string doslovne = data.ResolvePlaceholders(te.Text).Trim();
            if (doslovne.Length == 0 || pozice[te].Y is not float y) continue;
            float x = pozice[te].X, h = te.Height ?? te.EffectiveDesignerHeight;
            var slova = vytisk.Words.Where(w => w.X + w.Width / 2 > x - 0.5f && w.X + w.Width / 2 < x + te.Width + 0.5f
                                                && w.Y + w.Height / 2 > y - 0.5f && w.Y + w.Height / 2 < y + h + 0.5f).ToList();
            if (slova.Count == 0) continue;   // nevytiskl se (přetekl, schovaný) - není co najít
            texty.Add(new OcekavanyText(te.Text, doslovne,
                (slova.Min(w => w.X) + slova.Max(w => w.X + w.Width)) / 2,
                (slova.Min(w => w.Y) + slova.Max(w => w.Y + w.Height)) / 2));
        }

        var tab = t.Sections.SelectMany(s => s.Elements).OfType<LineTableElement>().FirstOrDefault();
        var pozn = t.Sections.SelectMany(s => s.Elements).OfType<NotesLegendElement>().FirstOrDefault();
        var sekceTabulky = t.Sections.FirstOrDefault(s => tab is not null && s.Elements.Contains(tab));
        bool vedle = pozn is not null && sekceTabulky?.Elements.Contains(pozn) == true;

        // Šířka tabulky, jak je na papíře: sloupce se počítají z obsahu, takže tabulka bývá užší
        // než prvek. Import vidí jen papír - porovnávat s šířkou prvku by hlásilo chybu, která není.
        float sirka = tab?.Width ?? 0f;
        if (tab is not null && pozice[tab].Y is float ty)
        {
            float l = pozice[tab].X;
            float hranice = vedle ? pozice[pozn!].X - 0.5f : t.PageSize.Width;
            sirka = vytisk.Lines.Select(c => (L: Math.Min(c.X1, c.X2), R: Math.Max(c.X1, c.X2), Y: Math.Min(c.Y1, c.Y2)))
                .Concat(vytisk.Rectangles.Select(r => (L: r.X, R: r.X + r.Width, Y: r.Y)))
                .Where(s => s.L >= l - 1f && s.R <= hranice && s.Y >= ty - 0.5f && s.Y <= ty + 40f)
                .Select(s => s.R).DefaultIfEmpty(l + tab.Width).Max() - l;
        }

        return new OcekavaniImportu
        {
            Nazev = nazev,
            Kostra = vedle ? "vedle" : "pod",
            Paticka = t.Sections.Any(s => s.Role == SectionRole.Footer && s.Elements.Count > 0),
            Texty = texty,
            Tabulka = tab is null ? null : new OcekavanaTabulka
            {
                X = pozice[tab].X,
                Y = pozice[tab].Y ?? 0f,
                Sirka = sirka,
                // Šipka nemá text a bez rozdvojených zastávek ji na papíře nevidět; sloupec spojů
                // vzniká z dat.
                Sloupce = [.. tab.Columns.Where(c => c.Visible && c.Key is not (ColumnKeys.Arrow or ColumnKeys.Trip))
                                         .Select(c => c.Key)],
                PismoBunekPt = tab.ResolveColumnDataParent().FontSize,
                PozadiZahlavi = tab.ResolveColumnHeaderParent().BackgroundColor,
                PismoZahlavi = tab.ResolveColumnHeaderParent().TextColor,
                LicheTc = tab.OddTcBackColor ?? "",
                // Bez nastavené mezery ji tisk může vložit sám (oddělí skupiny dnů při dělení
                // tabulky) - na papíře je, v šabloně ne; co má import převzít, se pak nedá říct.
                MezeraMeziSkupinami = tab.MezeraMeziSkupinamiMm > 0 ? tab.MezeraMeziSkupinamiMm : null,
            },
            Poznamky = pozn is null ? null : new OcekavanePoznamky
            {
                X = pozice[pozn].X,
                Sirka = pozn.Width,
                Sloupce = pozn.Columns,
                Zahlavi = pozn.ShowHeaderBar,
                Pismo = pozn.DataStyle.TextColor ?? "#FF000000",
            },
        };
    }

    // ── Hodnocení ───────────────────────────────────────────────────────

    public static Hodnoceni Vyhodnot(Template imp, OcekavaniImportu o)
    {
        var k = new List<Kontrola>();
        var odchylky = new List<float>();
        var pozice = PoziceNaStrance(imp);
        var prvky = imp.Sections.SelectMany(s => s.Elements).ToList();
        var texty = prvky.OfType<TextElement>().ToList();
        var pouzite = new HashSet<TextElement>();

        foreach (var e in o.Texty)
        {
            string nazev = $"„{Zkrat(e.Doslovne)}\"";
            var t = NajdiText(texty, e, pozice);
            k.Add(new("Texty", $"{nazev} je v šabloně", t is not null, t is null ? "chybí" : $"„{Zkrat(t.Text)}\""));
            if (t is null) continue;
            pouzite.Add(t);

            var pole = Pole(e.Obsah);
            if (pole.Count > 0)
            {
                var chybi = pole.Where(p => !t.Text.Contains(p, StringComparison.OrdinalIgnoreCase)).ToList();
                k.Add(new("Texty", $"{nazev} má pole", chybi.Count == 0,
                    chybi.Count == 0 ? string.Join(" ", pole) : "chybí " + string.Join(" ", chybi)));
            }

            if (Stred(t, pozice) is { } s)
            {
                float d = MathF.Sqrt(MathF.Pow(s.X - e.X, 2) + MathF.Pow(s.Y - e.Y, 2));
                odchylky.Add(d);
                k.Add(new("Texty", $"{nazev} na místě", d <= TolerancePolohyTextuMm, $"odchylka {d:0.0} mm"));
            }
        }

        var volne = texty.Where(t => !pouzite.Contains(t) && !t.Text.Contains('{')).ToList();
        k.Add(new("Texty", "žádné volné texty navíc", volne.Count == 0,
            volne.Count == 0 ? "" : $"{volne.Count}: " + string.Join(" | ", volne.Take(4).Select(t => Zkrat(t.Text)))));

        HodnotTabulku(k, prvky, pozice, o.Tabulka);
        HodnotPoznamky(k, imp, prvky, pozice, o);

        if (o.Paticka is bool p)
        {
            bool ma = imp.Sections.Any(s => s.Role == SectionRole.Footer && s.Elements.Count > 0);
            k.Add(new("Kostra", "patička", ma == p, ma ? "je" : "není"));
        }
        return new Hodnoceni(k, odchylky);
    }

    private static void HodnotTabulku(List<Kontrola> k, List<TemplateElement> prvky,
        Dictionary<TemplateElement, (float X, float? Y)> pozice, OcekavanaTabulka? o)
    {
        if (o is null) return;
        var tab = prvky.OfType<LineTableElement>().FirstOrDefault();
        k.Add(new("Tabulka", "tabulka spojů je v šabloně", tab is not null, ""));
        if (tab is null) return;

        float dx = pozice[tab].X - o.X;
        k.Add(new("Tabulka", "levý okraj", MathF.Abs(dx) <= TolerancePolohyTabulkyMm, $"{dx:+0.0;-0.0} mm"));
        if (pozice[tab].Y is float y)
            k.Add(new("Tabulka", "horní okraj", MathF.Abs(y - o.Y) <= TolerancePolohyTabulkyMm, $"{y - o.Y:+0.0;-0.0} mm"));
        // Aspoň tak široká jako na papíře: tabulka se kreslí podle obsahu, širší prvek nevadí,
        // užší by ji rozdělil na víc částí.
        float dw = tab.Width - o.Sirka;
        k.Add(new("Tabulka", "šířka", dw >= -ToleranceSirkyMm, $"{dw:+0.0;-0.0} mm"));

        var poradi = tab.Columns.Where(c => c.Visible && o.Sloupce.Contains(c.Key)).Select(c => c.Key).ToList();
        k.Add(new("Tabulka", "pořadí sloupců", poradi.SequenceEqual(o.Sloupce),
            $"{string.Join(",", poradi)} (čekáno {string.Join(",", o.Sloupce)})"));

        if (o.PismoBunekPt is float pt)
        {
            float imp = tab.ResolveColumnDataParent().FontSize;
            // Tisk tabulku, které chybí málo, zmenší až na 90 % - to je pořád „totéž písmo".
            k.Add(new("Tabulka", "písmo buněk", MathF.Abs(imp - pt) <= pt * 0.12f + 0.2f, $"{imp:0.0} pt (čekáno {pt:0.0})"));
        }
        if (o.PozadiZahlavi is { } bg)
            k.Add(Barva("Tabulka", "pozadí záhlaví", tab.ResolveColumnHeaderParent().BackgroundColor, bg));
        if (o.PismoZahlavi is { } fg)
            k.Add(Barva("Tabulka", "písmo záhlaví", tab.ResolveColumnHeaderParent().TextColor, fg));
        if (o.LicheTc is { } liche)
        {
            if (liche.Length == 0)
                k.Add(new("Tabulka", "bez podbarvení lichých Tč", tab.OddTcBackColor is null, tab.OddTcBackColor ?? ""));
            else
                k.Add(Barva("Tabulka", "podbarvení lichých Tč", tab.OddTcBackColor ?? "", liche));
        }
        if (o.MezeraMeziSkupinami is float m)
            k.Add(new("Tabulka", "mezera mezi skupinami", MathF.Abs(tab.MezeraMeziSkupinamiMm - m) <= 1f,
                $"{tab.MezeraMeziSkupinamiMm:0.0} mm (čekáno {m:0.0})"));
    }

    private static void HodnotPoznamky(List<Kontrola> k, Template imp, List<TemplateElement> prvky,
        Dictionary<TemplateElement, (float X, float? Y)> pozice, OcekavaniImportu o)
    {
        var pozn = prvky.OfType<NotesLegendElement>().FirstOrDefault();
        var tab = prvky.OfType<LineTableElement>().FirstOrDefault();
        if (pozn is not null && tab is not null)
        {
            bool vedle = imp.Sections.Any(s => s.Elements.Contains(tab) && s.Elements.Contains(pozn));
            k.Add(new("Kostra", $"poznámky {o.Kostra} tabulkou", (vedle ? "vedle" : "pod") == o.Kostra, vedle ? "vedle" : "pod"));
        }
        if (o.Poznamky is not { } op) return;

        k.Add(new("Poznámky", "poznámky jsou v šabloně", pozn is not null, ""));
        if (pozn is null) return;
        if (op.X is float x)
            k.Add(new("Poznámky", "levý okraj", MathF.Abs(pozice[pozn].X - x) <= TolerancePolohyTabulkyMm, $"{pozice[pozn].X - x:+0.0;-0.0} mm"));
        if (op.Sirka is float w)
            k.Add(new("Poznámky", "šířka", MathF.Abs(pozn.Width - w) <= ToleranceSirkyMm * 2, $"{pozn.Width - w:+0.0;-0.0} mm"));
        if (op.Sloupce is int c)
            k.Add(new("Poznámky", "počet sloupců", pozn.Columns == c, $"{pozn.Columns} (čekáno {c})"));
        if (op.Zahlavi is bool z)
            k.Add(new("Poznámky", "záhlaví", pozn.ShowHeaderBar == z, pozn.ShowHeaderBar ? "je" : "není"));
        if (op.Pismo is { } p)
            k.Add(Barva("Poznámky", "barva písma", pozn.DataStyle.TextColor ?? "#FF000000", p));
    }

    // ── Pomocné ────────────────────────────────────────────────────────

    /// <summary>Text šablony k očekávanému: podle obsahu s poli, jinak podle vytištěného znění;
    /// z víc kandidátů nejbližší.</summary>
    private static TextElement? NajdiText(List<TextElement> texty, OcekavanyText e,
        Dictionary<TemplateElement, (float X, float? Y)> pozice)
    {
        string obsah = Norm(e.Obsah), doslovne = Norm(e.Doslovne);
        bool Odpovida(string kandidat, string cil) => kandidat.Length > 0 && cil.Length > 0
            && (kandidat.Contains(cil) || (cil.Contains(kandidat) && kandidat.Length * 2 >= cil.Length));

        var kandidati = texty.Where(t => Odpovida(Norm(t.Text), obsah)).ToList();
        if (kandidati.Count == 0) kandidati = [.. texty.Where(t => Odpovida(Norm(t.Text), doslovne))];
        return kandidati
            .OrderBy(t => Stred(t, pozice) is { } s ? MathF.Abs(s.X - e.X) + MathF.Abs(s.Y - e.Y) : float.MaxValue)
            .FirstOrDefault();
    }

    /// <summary>Střed prvku na papíře - tam import text kreslí (vystředěný).</summary>
    private static (float X, float Y)? Stred(TextElement t, Dictionary<TemplateElement, (float X, float? Y)> pozice) =>
        pozice[t].Y is float y ? (pozice[t].X + t.Width / 2, y + (t.Height ?? t.EffectiveDesignerHeight) / 2) : null;

    private static List<string> Pole(string text) =>
        [.. System.Text.RegularExpressions.Regex.Matches(text, @"\{[A-Z0-9_]+\}").Select(m => m.Value).Distinct()];

    private static Kontrola Barva(string oblast, string nazev, string? skutecna, string cekana)
    {
        var a = ColorHex.ParseOrNull(skutecna);
        var b = ColorHex.ParseOrNull(cekana);
        bool ok = a is { } x && b is { } y
                  && Math.Abs(x.Red - y.Red) <= ToleranceBarvy && Math.Abs(x.Green - y.Green) <= ToleranceBarvy
                  && Math.Abs(x.Blue - y.Blue) <= ToleranceBarvy;
        return new(oblast, nazev, ok, $"{skutecna} (čekáno {cekana})");
    }

    private static string Norm(string s) =>
        System.Text.RegularExpressions.Regex.Replace(s.ToLowerInvariant().Replace('\n', ' '), @"\s+", " ").Trim();

    private static string Zkrat(string s) => s.Length <= 40 ? s : s[..37] + "...";
}
