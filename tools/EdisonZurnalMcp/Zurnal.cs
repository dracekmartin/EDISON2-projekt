using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace EdisonZurnalMcp;

/// <summary>Uloziste zurnalu: append-only JSONL + vyzvednuti odpovedi na cekajici rozhodnuti.</summary>
internal sealed class Zurnal
{
    /// Typy patrici do uzivatelskeho kanalu. Zbytek je technicky.
    /// Filtr: "zmeni to, co uzivatel uvidi, na co klikne, nebo co dostane vytisknute?"
    private static readonly HashSet<string> UzivatelskeTypy =
        new(StringComparer.Ordinal) { "rozhodnuti", "dusledek", "pozor", "hotovo" };

    private static readonly HashSet<string> TechnickeTypy =
        new(StringComparer.Ordinal) { "zamer", "zjisteni", "brana", "predpoklad" };

    /// Bez relaxovaneho encoderu by se cestina i "+" v casove zone zapsaly jako \uXXXX -
    /// zurnal ma cist clovek i viewer, takze escapovany JSON je tu vada, ne detail.
    internal static readonly JsonSerializerOptions Kompaktni = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private const int VychoziTimeoutSekund = 300;

    private readonly string _slozkaOdpovedi;
    private readonly object _zamek = new();
    private readonly Dictionary<string, DateTime> _cekajiciOd = new(StringComparer.Ordinal);
    private int _poradiRozhodnuti;

    public string BehId { get; }
    public string Soubor { get; }

    /// Jak dlouho se ceka na odpoved, nez se rozhodnuti uzavre jako nezodpovezene.
    /// Zadne volani nastroje se timhle neblokuje - timeout se vyhodnocuje mezi dotazy.
    public int TimeoutSekund { get; }

    /// "auto" = rozhodnuti se jen zapisuji a bezi se dal (dnesni stav, zadne UI neposlouchá).
    /// "potvrzovat" = agent po kazdem rozhodnuti ceka na odpoved.
    /// Rezim je knoflik serveru - skilly ho neznaji a nemeni se pri prepnuti.
    public string Rezim { get; }

    public Zurnal(string? slozka, string? rezim, string? behId = null, string? timeoutSekund = null)
    {
        Rezim = rezim is "potvrzovat" ? "potvrzovat" : "auto";
        TimeoutSekund = int.TryParse(timeoutSekund, out var t) && t > 0 ? t : VychoziTimeoutSekund;

        var korenSlozky = string.IsNullOrWhiteSpace(slozka)
            ? Path.Combine(Directory.GetCurrentDirectory(), ".claude", "zurnal")
            : slozka;

        // Pojmenovany beh (napr. cislo tiketu) je pro L1 analyzy - zurnal pak lze
        // odlozit jako docs/analyzy/<beh>.jsonl bez prejmenovavani.
        BehId = string.IsNullOrWhiteSpace(behId)
            ? $"{DateTime.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}"
            : BezpecnyNazev(behId);
        Soubor = Path.Combine(korenSlozky, $"{BehId}.jsonl");
        _slozkaOdpovedi = Path.Combine(korenSlozky, "odpovedi");

        Directory.CreateDirectory(korenSlozky);
        Directory.CreateDirectory(_slozkaOdpovedi);
    }

    public static bool ZnamyTyp(string typ) => UzivatelskeTypy.Contains(typ) || TechnickeTypy.Contains(typ);

    public static IEnumerable<string> VsechnyTypyKromRozhodnuti() =>
        UzivatelskeTypy.Concat(TechnickeTypy).Where(t => t != "rozhodnuti").OrderBy(t => t, StringComparer.Ordinal);

    public void Zapis(string typ, string nazev, string? telo)
    {
        var zaznam = ZakladniZaznam(typ);
        zaznam["nazev"] = nazev;
        if (!string.IsNullOrWhiteSpace(telo)) zaznam["telo"] = telo;
        Priloz(zaznam);
    }

    /// Zapise rozhodnuti a vrati jeho id, pokud se na nej ma cekat; jinak null.
    /// Prazdne "vybral" = agent si nevybral a ceka vzdy, bez ohledu na rezim.
    public string? ZapisRozhodnuti(string nazev, IReadOnlyList<string> varianty, string? vybral, string? proc, string? telo)
    {
        var neumimRozhodnout = string.IsNullOrWhiteSpace(vybral);
        var maCekat = neumimRozhodnout || Rezim == "potvrzovat";

        string? id = null;
        lock (_zamek)
        {
            id = $"r{++_poradiRozhodnuti}";
            if (maCekat) _cekajiciOd[id] = DateTime.UtcNow;
        }

        var zaznam = ZakladniZaznam("rozhodnuti");
        zaznam["id"] = id;
        zaznam["nazev"] = nazev;
        if (!string.IsNullOrWhiteSpace(telo)) zaznam["telo"] = telo;
        zaznam["varianty"] = new JsonArray(varianty.Select(v => (JsonNode)v!).ToArray());
        if (!neumimRozhodnout) zaznam["vybral"] = vybral;
        if (!string.IsNullOrWhiteSpace(proc)) zaznam["proc"] = proc;

        // "stav" je v souboru zamerne: viewer pozna cekajici rozhodnuti ctenim JSONL
        // a nemusi se ptat serveru. Diky tomu muze bezet nula nebo N vieweru.
        zaznam["stav"] = maCekat ? "ceka" : "hotovo";

        Priloz(zaznam);
        return maCekat ? id : null;
    }

    /// Odpoved dodava UI (nebo clovek rucne) jako soubor odpovedi/&lt;BehId&gt;-&lt;id&gt;.json
    /// ve tvaru { "volba": "pokracuj" | "poznamka" | "zrus", "text": "..." }.
    public Odpoved VyzvedniOdpoved(string id)
    {
        var cesta = Path.Combine(_slozkaOdpovedi, $"{BehId}-{id}.json");

        if (!File.Exists(cesta))
        {
            // Nezname id = preklep, nebo dotaz na rozhodnuti z jineho behu. Nehlasit "nikdo
            // neodpovedel" - to by tvrdilo, ze cekani vyprselo, i kdyz o nem nic nevime.
            if (!JeCekajici(id)) return new Odpoved("neznameId", null, 0);

            var zbyva = ZbyvaSekund(id);
            if (zbyva > 0) return new Odpoved("ceka", null, zbyva);

            // Timeout: rozhodnuti se uzavira jako nezodpovezene. Zapisuje se do zurnalu,
            // aby otevrena volba dorazila do reportu a neztratila se tim, ze se slo dal.
            ZapisOdpoved(id, "bezOdpovedi", null);
            return new Odpoved("bezOdpovedi", null, 0);
        }

        JsonNode? uzel;
        try
        {
            uzel = JsonNode.Parse(File.ReadAllText(cesta));
        }
        catch (JsonException)
        {
            return new Odpoved("ceka", null, ZbyvaSekund(id));   // rozepsany soubor - priste to bude cele
        }

        var volba = uzel?["volba"]?.GetValue<string>() ?? "pokracuj";
        var text = uzel?["text"]?.GetValue<string>();
        ZapisOdpoved(id, volba, text);
        return new Odpoved(volba, text, 0);
    }

    private void ZapisOdpoved(string id, string volba, string? text)
    {
        var zaznam = ZakladniZaznam("odpoved");
        zaznam["id"] = id;
        zaznam["volba"] = volba;
        if (!string.IsNullOrWhiteSpace(text)) zaznam["text"] = text;
        Priloz(zaznam);

        lock (_zamek) _cekajiciOd.Remove(id);
    }

    private bool JeCekajici(string id)
    {
        lock (_zamek) return _cekajiciOd.ContainsKey(id);
    }

    private int ZbyvaSekund(string id)
    {
        DateTime od;
        lock (_zamek)
        {
            if (!_cekajiciOd.TryGetValue(id, out od)) return 0;
        }
        var zbyva = TimeoutSekund - (int)(DateTime.UtcNow - od).TotalSeconds;
        return zbyva > 0 ? zbyva : 0;
    }

    private JsonObject ZakladniZaznam(string typ) => new()
    {
        ["cas"] = DateTimeOffset.Now.ToString("o"),
        ["kanal"] = UzivatelskeTypy.Contains(typ) || typ == "odpoved" ? "uzivatelsky" : "technicky",
        ["typ"] = typ,
    };

    private void Priloz(JsonObject zaznam)
    {
        var radek = zaznam.ToJsonString(Kompaktni);
        lock (_zamek)
        {
            File.AppendAllText(Soubor, radek + Environment.NewLine);
        }
    }

    private static string BezpecnyNazev(string vstup)
    {
        var zakazane = Path.GetInvalidFileNameChars();
        var ocisteny = new string(vstup.Trim().Select(z => zakazane.Contains(z) ? '-' : z).ToArray());
        return ocisteny.Length == 0 ? "beh" : ocisteny;
    }

    internal readonly record struct Odpoved(string Volba, string? Text, int ZbyvaSekund);
}
