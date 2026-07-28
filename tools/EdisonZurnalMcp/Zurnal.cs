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

    private readonly string _slozkaOdpovedi;
    private readonly object _zamek = new();
    private int _poradiRozhodnuti;

    public string BehId { get; }
    public string Soubor { get; }

    /// "auto" = rozhodnuti se jen zapisuji a bezi se dal (dnesni stav, zadne UI neposlouchá).
    /// "potvrzovat" = agent po kazdem rozhodnuti ceka na odpoved.
    /// Rezim je knoflik serveru - skilly ho neznaji a nemeni se pri prepnuti.
    public string Rezim { get; }

    public Zurnal(string? slozka, string? rezim, string? behId = null)
    {
        Rezim = rezim is "potvrzovat" ? "potvrzovat" : "auto";

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
        if (!File.Exists(cesta)) return new Odpoved("ceka", null);

        JsonNode? uzel;
        try
        {
            uzel = JsonNode.Parse(File.ReadAllText(cesta));
        }
        catch (JsonException)
        {
            return new Odpoved("ceka", null);   // rozepsany soubor - priste to bude cele
        }

        var volba = uzel?["volba"]?.GetValue<string>() ?? "pokracuj";
        var text = uzel?["text"]?.GetValue<string>();

        var zaznam = ZakladniZaznam("odpoved");
        zaznam["id"] = id;
        zaznam["volba"] = volba;
        if (!string.IsNullOrWhiteSpace(text)) zaznam["text"] = text;
        Priloz(zaznam);

        return new Odpoved(volba, text);
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

    internal readonly record struct Odpoved(string Volba, string? Text);
}
