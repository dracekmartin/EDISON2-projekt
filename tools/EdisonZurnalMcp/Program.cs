using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace EdisonZurnalMcp;

/// <summary>
/// MCP server (stdio) pro behovy zurnal agenta.
/// POZOR: na stdout smi jit VYHRADNE JSON-RPC zpravy - jakykoli jiny vypis rozbije
/// protokol a Claude Code server odmitne. Diagnostika patri na stderr.
/// </summary>
internal static class Program
{
    private const string NazevServeru = "edison-zurnal";
    private const string VerzeServeru = "0.1.0";
    private const string ZalozniVerzeProtokolu = "2025-06-18";

    private static Zurnal _zurnal = null!;

    private static async Task<int> Main()
    {
        Console.OutputEncoding = new UTF8Encoding(false);

        _zurnal = new Zurnal(
            slozka: Environment.GetEnvironmentVariable("ZURNAL_DIR"),
            rezim: Environment.GetEnvironmentVariable("ZURNAL_REZIM"),
            behId: Environment.GetEnvironmentVariable("ZURNAL_BEH"));

        Console.Error.WriteLine($"[zurnal] beh={_zurnal.BehId} rezim={_zurnal.Rezim} soubor={_zurnal.Soubor}");

        // MCP stdio transport = zpravy oddelene radky, zadne Content-Length hlavicky.
        while (await Console.In.ReadLineAsync() is { } radek)
        {
            if (string.IsNullOrWhiteSpace(radek)) continue;

            JsonNode? zprava;
            try
            {
                zprava = JsonNode.Parse(radek);
            }
            catch (JsonException ex)
            {
                Console.Error.WriteLine($"[zurnal] nevalidni JSON na vstupu: {ex.Message}");
                continue;
            }

            var metoda = zprava?["method"]?.GetValue<string>();
            var id = zprava?["id"];
            if (metoda is null) continue;

            // Notifikace (bez id) se nikdy neodpovida.
            if (id is null)
            {
                Console.Error.WriteLine($"[zurnal] notifikace {metoda}");
                continue;
            }

            try
            {
                Odesli(Uspech(id, ZpracujPozadavek(metoda, zprava!["params"])));
            }
            catch (MetodaNeznamaException)
            {
                Odesli(Chyba(id, -32601, $"Neznama metoda: {metoda}"));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[zurnal] chyba v {metoda}: {ex}");
                Odesli(Chyba(id, -32603, ex.Message));
            }
        }

        return 0;
    }

    private static JsonObject ZpracujPozadavek(string metoda, JsonNode? parametry) => metoda switch
    {
        "initialize" => new JsonObject
        {
            ["protocolVersion"] = parametry?["protocolVersion"]?.GetValue<string>() ?? ZalozniVerzeProtokolu,
            ["capabilities"] = new JsonObject { ["tools"] = new JsonObject() },
            ["serverInfo"] = new JsonObject { ["name"] = NazevServeru, ["version"] = VerzeServeru },
        },
        "ping" => new JsonObject(),
        "tools/list" => new JsonObject { ["tools"] = SchemataNastroju() },
        "tools/call" => ZavolejNastroj(parametry),
        _ => throw new MetodaNeznamaException(),
    };

    // --- Nastroje ---------------------------------------------------------------

    private static JsonArray SchemataNastroju()
    {
        var typy = new JsonArray(Zurnal.VsechnyTypyKromRozhodnuti().Select(t => (JsonNode)t!).ToArray());

        return new JsonArray
        {
            new JsonObject
            {
                ["name"] = "zurnal_zapis",
                ["description"] =
                    "Zapis udalost do behoveho zurnalu. Zapisuj JEN to, podle ceho by se uzivatel zachoval jinak - "
                    + "cteni souboru, grepy ani vnitrni postup do zurnalu nepatri. Typy: "
                    + "dusledek (co se zmeni v pouzivani aplikace), pozor (zmenu stroj neuvidi, uved klikaci cestu), "
                    + "hotovo (polozka dokoncena, cim je podlozena), zamer (co delam a proc), "
                    + "zjisteni (nalez v kodu, ktery meni plan; uved file:line), brana (co se overovalo a vysledek), "
                    + "predpoklad (vzal jsem neco za dane bez dukazu). "
                    + "Na rozhodnuti pouzij zurnal_rozhodnuti, ne tento nastroj.",
                ["inputSchema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["typ"] = new JsonObject { ["type"] = "string", ["enum"] = typy },
                        ["nazev"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Kratky titulek, jedna radka.",
                        },
                        ["telo"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Co to konkretne znamena. Nepovinne u samozrejmych udalosti.",
                        },
                    },
                    ["required"] = new JsonArray { "typ", "nazev" },
                },
            },
            new JsonObject
            {
                ["name"] = "zurnal_rozhodnuti",
                ["description"] =
                    "Zapis rozhodnuti, ktere pocitil uzivatel aplikace - vychozi hodnota, chovani, umisteni v UI, "
                    + "pojmenovani, chovani v hranicnim pripade, zpetna kompatibilita. Vnitrni volby, ktere uzivatel "
                    + "nepozna (algoritmus, pomocny skript, poradi kroku), do zurnalu NEPATRI. "
                    + "Kdyz si vybrat neumis, nech 'vybral' prazdne - beh se zastavi a pocka na uzivatele. "
                    + "Vrati 'pokracuj', nebo 'cekej id=<id>'; pri cekani volej zurnal_odpoved(id), dokud nedostanes odpoved.",
                ["inputSchema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["nazev"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "O cem se rozhoduje, jazykem uzivatele. Napr. 'Prestupy u zastavek - vychozi stav'.",
                        },
                        ["varianty"] = new JsonObject
                        {
                            ["type"] = "array",
                            ["items"] = new JsonObject { ["type"] = "string" },
                            ["description"] = "Schudne moznosti vcetne te vybrane, aspon dve.",
                        },
                        ["vybral"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Kterou variantu beru. Prazdne = neumim rozhodnout, cekam na uzivatele.",
                        },
                        ["proc"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Duvod volby. Povinny, kdyz je 'vybral' vyplnene.",
                        },
                        ["telo"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Doplnujici kontext, kdyz nazev nestaci.",
                        },
                    },
                    ["required"] = new JsonArray { "nazev", "varianty" },
                },
            },
            new JsonObject
            {
                ["name"] = "zurnal_odpoved",
                ["description"] =
                    "Zjisti, jestli uzivatel odpovedel na cekajici rozhodnuti. Vrati 'ceka' (zavolej znovu), "
                    + "'pokracuj', 'poznamka: <text>' (zapracuj a pokracuj), nebo 'zrus' (zastav a nahlas stav). "
                    + "Mezi dotazy nespam - staci obcas.",
                ["inputSchema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["id"] = new JsonObject { ["type"] = "string", ["description"] = "Id z odpovedi zurnal_rozhodnuti." },
                    },
                    ["required"] = new JsonArray { "id" },
                },
            },
        };
    }

    private static JsonObject ZavolejNastroj(JsonNode? parametry)
    {
        var nazev = parametry?["name"]?.GetValue<string>() ?? "";
        var argumenty = parametry?["arguments"];

        return nazev switch
        {
            "zurnal_zapis" => Text(Zapis(argumenty)),
            "zurnal_rozhodnuti" => Text(Rozhodnuti(argumenty)),
            "zurnal_odpoved" => Text(OdpovedNaRozhodnuti(argumenty)),
            _ => Text($"Neznamy nastroj: {nazev}", chyba: true),
        };
    }

    private static string Zapis(JsonNode? a)
    {
        var typ = a?["typ"]?.GetValue<string>() ?? "";
        var nazev = a?["nazev"]?.GetValue<string>() ?? "";

        if (!Zurnal.ZnamyTyp(typ) || typ == "rozhodnuti")
            return $"Neplatny typ '{typ}'. Povolene: {string.Join(", ", Zurnal.VsechnyTypyKromRozhodnuti())}.";
        if (string.IsNullOrWhiteSpace(nazev))
            return "Chybi 'nazev'.";

        _zurnal.Zapis(typ, nazev, a?["telo"]?.GetValue<string>());
        return "zapsano";
    }

    private static string Rozhodnuti(JsonNode? a)
    {
        var nazev = a?["nazev"]?.GetValue<string>() ?? "";
        var varianty = (a?["varianty"] as JsonArray)?
            .Select(v => v?.GetValue<string>() ?? "")
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList() ?? [];
        var vybral = a?["vybral"]?.GetValue<string>();
        var proc = a?["proc"]?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(nazev)) return "Chybi 'nazev'.";
        if (varianty.Count < 2) return "Uved aspon dve varianty - jinak to neni rozhodnuti.";
        if (!string.IsNullOrWhiteSpace(vybral) && string.IsNullOrWhiteSpace(proc))
            return "Kdyz je 'vybral' vyplnene, musis uvest 'proc'.";

        var id = _zurnal.ZapisRozhodnuti(nazev, varianty, vybral, proc, a?["telo"]?.GetValue<string>());
        return id is null
            ? "pokracuj"
            : $"cekej id={id} - volej zurnal_odpoved(\"{id}\"), dokud nedostanes odpoved";
    }

    private static string OdpovedNaRozhodnuti(JsonNode? a)
    {
        var id = a?["id"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(id)) return "Chybi 'id'.";

        var odpoved = _zurnal.VyzvedniOdpoved(id);
        return odpoved.Volba switch
        {
            "ceka" => "ceka",
            "poznamka" => $"poznamka: {odpoved.Text}",
            "zrus" => "zrus",
            _ => "pokracuj",
        };
    }

    // --- JSON-RPC obalky --------------------------------------------------------

    private static JsonObject Text(string text, bool chyba = false)
    {
        var vysledek = new JsonObject
        {
            ["content"] = new JsonArray
            {
                new JsonObject { ["type"] = "text", ["text"] = text },
            },
        };
        if (chyba) vysledek["isError"] = true;
        return vysledek;
    }

    private static JsonObject Uspech(JsonNode id, JsonObject vysledek) => new()
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id.DeepClone(),
        ["result"] = vysledek,
    };

    private static JsonObject Chyba(JsonNode id, int kod, string zprava) => new()
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id.DeepClone(),
        ["error"] = new JsonObject { ["code"] = kod, ["message"] = zprava },
    };

    private static void Odesli(JsonObject zprava)
    {
        Console.Out.WriteLine(zprava.ToJsonString(Zurnal.Kompaktni));
        Console.Out.Flush();
    }

    private sealed class MetodaNeznamaException : Exception;
}
