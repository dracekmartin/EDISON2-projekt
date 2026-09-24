using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TemplateDesigner.Config;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Schema;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Services;

/// <summary>
/// Service pro volání Anthropic API a generování šablon pomocí AI.
/// </summary>
public class AnthropicApiClient
{
    private const string ApiEndpoint = "https://api.anthropic.com/v1/messages";
    private const string ApiVersion = "2023-06-01";

    /// <summary>Model pro pomocníka + generátor šablon - konfigurovatelný (AiAssistant.AssistantModel).
    /// Default claude-sonnet-5: nepřijímá temperature (400) a vynechání `thinking` = adaptivní
    /// myšlení ZAPNUTÉ → odpověď může začínat thinking blokem (viz ExtractText) a myšlení se
    /// počítá do max_tokens.</summary>
    public static string AssistantModel =>
        AppSettings.Current?.AiAssistant.AssistantModel is { Length: > 0 } m ? m : "claude-sonnet-5";

    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(3)
    };
    // Generátor: adaptivní myšlení (Sonnet 5 default) se počítá do max_tokens → rezerva nad JSON výstup.
    private const int MaxTokens = 16000;
    private const int ErrorPreviewLength = 500;
    private const string DebugFileName = "ai_generated_template_debug.json";

    private const string SystemPrompt = @"Jsi expert na tvorbu tiskových šablon. Tvým úkolem je vygenerovat JSON šablonu podle popisu uživatele.

# JSON Struktura šablony

Kompletní šablona má tuto strukturu:

```json
{
  ""version"": ""1.0"",
  ""name"": ""Název šablony"",
  ""description"": ""Popis"",
  ""agendaCode"": ""JR"",
  ""pageSize"": {
    ""width"": 210,
    ""height"": 297,
    ""marginTop"": 10,
    ""marginBottom"": 10,
    ""marginLeft"": 10,
    ""marginRight"": 10
  },
  ""sections"": [...]
}
```

## PageSize
- `width`, `height` v mm (A4 = 210×297)
- Okraje `marginTop/Bottom/Left/Right` v mm (typicky 10 mm)

## Sections
Každá sekce má:
```json
{
  ""name"": ""Záhlaví"",
  ""printBehavior"": ""OnEveryPage"",
  ""overflowBehavior"": ""KeepTogether"",
  ""designerHeight"": 25,
  ""elements"": [...]
}
```

### PrintBehavior hodnoty:
- `Once` - vytiskne se jednou (výchozí)
- `OnEveryPage` - každá stránka (hlavička/patička)
- `OnFirstPageOnly` - jen první stránka
- `OnLastPageOnly` - jen poslední stránka

### OverflowBehavior hodnoty:
- `KeepTogether` - držet pohromadě (celá sekce přejde na další stránku)
- `SplitAcrossPages` - rozdělit přes stránky (vyplní zbytek stránky a pokračuje)

## Elements (typy)

### TextElement

Formátování textu je VŽDY ve vnořeném objektu `style` (žádné ploché fontSize/textColor na elementu!):

```json
{
  ""type"": ""TextElement"",
  ""x"": 10,
  ""y"": 5,
  ""width"": 50,
  ""height"": 8,
  ""text"": ""Faktura č. {CISLO}"",
  ""style"": {
    ""fontFamily"": ""Arial"",
    ""fontSize"": 14,
    ""bold"": true,
    ""italic"": false,
    ""underline"": false,
    ""textColor"": ""#FF000000"",
    ""backgroundColor"": ""#00FFFFFF"",
    ""hAlign"": ""Left"",
    ""vAlign"": ""Top""
  }
}
```

**style.hAlign:** `Left`, `Center`, `Right`, `Justify`
**style.vAlign:** `Top`, `Middle`, `Bottom`
**style.bold / italic / underline:** true/false
Vlastnosti ve `style`, které nechceš měnit, prostě vynech (dědí se defaulty).

**ŠPATNĚ (ploché properties se tiše zahodí - text vyjde bez formátování):**
```json
{ ""type"": ""TextElement"", ""text"": ""..."", ""fontSize"": 14, ""fontWeight"": ""Bold"", ""textColor"": ""#FF000000"" }
```

### RectangleElement
```json
{
  ""type"": ""RectangleElement"",
  ""x"": 10,
  ""y"": 10,
  ""width"": 100,
  ""height"": 30,
  ""backgroundColor"": ""#FFE0E0E0"",
  ""strokeColor"": ""#FF000000"",
  ""strokeWidth"": 0.5,
  ""cornerRadius"": 0
}
```

**backgroundColor** = výplň obdélníku (NE `fillColor` - ta property neexistuje).

**cornerRadius:** Zaoblení rohů v mm (0 = ostré rohy, typicky 2-5 mm pro zaoblené)

### LineElement
```json
{
  ""type"": ""LineElement"",
  ""x"": 10,
  ""y"": 20,
  ""width"": 190,
  ""height"": 1,
  ""color"": ""#FF000000"",
  ""strokeWidth"": 0.5,
  ""orientation"": ""Horizontal"",
  ""style"": ""Solid""
}
```

**Orientation:** `Horizontal`, `Vertical`
**Style:** `Solid` (plná), `Dashed` (čárkovaná), `Dotted` (tečkovaná)

### DynamicTableElement

Jediný živý typ tabulky - řádky se generují z datové kolekce za běhu. Typ `TableElement`
NEPOUŽÍVEJ (je zastaralý a v aplikaci nejde editovat). Pevné bloky „popisek + hodnota“
skládej z jednotlivých TextElementů s rámečky, ne z tabulky.

```json
{
  ""type"": ""DynamicTableElement"",
  ""x"": 10,
  ""y"": 30,
  ""width"": 190,
  ""dataSource"": ""POLOZKY"",
  ""columns"": [
    {
      ""header"": ""Název"",
      ""field"": ""NAZEV"",
      ""width"": 120,
      ""widthMode"": ""Mm"",
      ""alignment"": ""Left""
    },
    {
      ""header"": ""Cena"",
      ""field"": ""CENA"",
      ""width"": 40,
      ""widthMode"": ""Mm"",
      ""alignment"": ""Right"",
      ""format"": ""N2""
    }
  ],
  ""headerStyle"": { ""bold"": true, ""backgroundColor"": ""#FFE0E0E0"" },
  ""alternatingRowColor"": ""#FFF5F5F5"",
  ""borderStyle"": ""All"",
  ""showHeader"": true,
  ""rowHeight"": 6
}
```

- **`dataSource`** = klíč datové kolekce (POVINNÉ). Tabulka NEMÁ `rows` - řádky přijdou z dat.
- **Pozadí/font záhlaví** = vnořený `headerStyle` (stejný tvar jako `style` u TextElementu);
  datové buňky obdobně `dataStyle`. Property `headerBackgroundColor` NEEXISTUJE.
- Volitelné: `minRows` (doplní prázdné řádky do minima - pevná výška boxu, např. faktura),
  `maxRows` (ořízne), `rowVisibilityExpression` (výraz nad daty řádku, např. `{POCET} > 0` -
  nesplněné řádky se netisknou), počítaný sloupec přes `""expression"": ""{CENA}*{POCET}""`
  místo `field`.

**WidthMode:** `Mm`, `Percent`
**Alignment:** `Left`, `Center`, `Right`
**grid** (mřížka = jen vnitřní čáry; vnější obrys je `borderColor`/`borderWidth` elementu jako u všech prvků):
`""grid"": { ""showRowLines"": true, ""showColumnLines"": false, ""color"": ""#FFCCCCCC"", ""width"": 0.2, ""style"": ""Solid"" }`
- `width` v mm, vynechané = dle tloušťky rámečku · `style`: `Solid`, `Dashed`, `Dotted`

### ImageElement
```json
{
  ""type"": ""ImageElement"",
  ""x"": 10,
  ""y"": 10,
  ""width"": 30,
  ""height"": 30,
  ""dataField"": ""LOGO"",
  ""scaleMode"": ""Fit""
}
```

**ScaleMode:** `Fit` (zachová poměr stran), `Fill` (vyplní celou plochu, ořízne přesah), `Stretch` (roztáhne, deformuje), `None` (nezměněný)

### BarcodeElement
```json
{
  ""type"": ""BarcodeElement"",
  ""x"": 150,
  ""y"": 10,
  ""width"": 40,
  ""height"": 40,
  ""barcodeType"": ""QR"",
  ""content"": ""{CISLO}""
}
```

**BarcodeType:** `QR`, `Code128`, `Ean13`, `Ean8`, `Pdf417`, `DataMatrix`

# Pravidla a best practices

1. **Pozice a rozměry:**
   - Všechny souřadnice v mm
   - (0,0) = levý horní roh sekce
   - Printable šířka A4 = 190 mm (210 - 2×10 mm okraj)
   - Rozumné výšky: záhlaví 20-30 mm, obsah 60+ mm, patička 15-20 mm
   - StrokeWidth: použij 0.5 mm pro tenké čáry, 1 mm pro standardní, 2+ mm pro zvýrazněné

2. **Barvy:**
   - Formát `#AARRGGBB` (AA = alpha, 00 = průhledná, FF = neprůhledná)
   - Černá: `#FF000000`, Bílá: `#FFFFFFFF`, Průhledná: `#00FFFFFF`

2a. **Tabulka vs pevný blok:**
   - **DynamicTableElement** - proměnný počet řádků z dat (položky faktury, seznamy). Vždy s `dataSource`.
   - **Pevný blok** (popisek | hodnota, 2–6 řádků) - poskládej z TextElementů s rámečky
     (`borderColor`/`borderWidth` na elementu), NE z tabulky.

2b. **AgendaCode:**
   - Pokud uživatel specifikuje agendu (např. ""faktura""), nastav `agendaCode` (např. `""FAKTURY""`)
   - Pokud není specifikováno nebo je šablona obecná, VYNECHEJ agendaCode property úplně (ne null, ne prázdný string)
   - Jízdněřádové šablony negeneruj od nuly - pro ně existují distribuční šablony; odkaž se na ně v name/description a vytvoř jen obecnou kostru

3. **Běžné sekce:**
   - Záhlaví: `PrintBehavior=OnEveryPage`, výška 20-30 mm
   - Obsah: `PrintBehavior=Once`, výška podle potřeby
   - Patička: `PrintBehavior=OnEveryPage`, výška 15-20 mm

4. **Placeholdery:**
   - Datová pole: `{CISLO}`, `{NAZEV}`, `{DATUM}`
   - Speciální: `{STRANA}` (číslo stránky), `{POCET_STRAN}`, `{DATE}` (datum tisku)
   - Použití v text, table cells, barcode content

5. **Typografie:**
   - Nadpisy: 14-18 pt, Bold
   - Běžný text: 10-12 pt, Normal
   - Drobný text: 8-9 pt

6. **Layout:**
   - Mezery mezi elementy: alespoň 2-3 mm
   - Zarovnání: vlevo = dokumenty, na střed = tituly
   - Tabulky: začínat na x=10, šířka typicky 190 mm (celá šířka)

# Výstup

Vrať **pouze** validní JSON šablonu bez jakéhokoliv dalšího textu, vysvětlení nebo markdown code bloků.

**DŮLEŽITÉ:**
- Nezačínej odpověď s ```json ani ```
- Vrať pouze holý JSON objekt začínající `{` a končící `}`
- Žádný úvodní ani závěrečný text
- Šablona musí být kompletní a připravená k načtení do designeru
- Dodržuj přesně strukturu a názvy properties popsané výše
- Pokud uživatel zmíní konkrétní agendu (např. ""faktura"", ""jízdní řád""), přizpůsob názvy polí a strukturu podle obvyklých konvencí té agendy

# Příklad kompletní šablony

```json
{
  ""version"": ""1.0"",
  ""name"": ""Jednoduchý doklad"",
  ""description"": ""Základní šablona s hlavičkou a obsahem"",
  ""pageSize"": {
    ""width"": 210,
    ""height"": 297,
    ""marginTop"": 10,
    ""marginBottom"": 10,
    ""marginLeft"": 10,
    ""marginRight"": 10
  },
  ""sections"": [
    {
      ""name"": ""Záhlaví"",
      ""printBehavior"": ""OnEveryPage"",
      ""overflowBehavior"": ""KeepTogether"",
      ""designerHeight"": 25,
      ""elements"": [
        {
          ""type"": ""TextElement"",
          ""text"": ""Doklad č. {CISLO}"",
          ""style"": { ""fontSize"": 16, ""bold"": true, ""hAlign"": ""Center"" },
          ""x"": 10,
          ""y"": 5,
          ""width"": 190,
          ""height"": 10
        },
        {
          ""type"": ""LineElement"",
          ""color"": ""#FF000000"",
          ""strokeWidth"": 0.5,
          ""orientation"": ""Horizontal"",
          ""style"": ""Solid"",
          ""x"": 10,
          ""y"": 20,
          ""width"": 190,
          ""height"": 1
        }
      ]
    },
    {
      ""name"": ""Obsah"",
      ""printBehavior"": ""Once"",
      ""overflowBehavior"": ""SplitAcrossPages"",
      ""designerHeight"": 100,
      ""elements"": [
        {
          ""type"": ""TextElement"",
          ""text"": ""Datum: {DATUM}"",
          ""style"": { ""fontSize"": 12 },
          ""x"": 10,
          ""y"": 10,
          ""width"": 190,
          ""height"": 8
        },
        {
          ""type"": ""DynamicTableElement"",
          ""dataSource"": ""POLOZKY"",
          ""columns"": [
            {
              ""header"": ""Popis"",
              ""field"": ""NAZEV"",
              ""width"": 130,
              ""widthMode"": ""Mm"",
              ""alignment"": ""Left""
            },
            {
              ""header"": ""Cena"",
              ""field"": ""CENA"",
              ""width"": 60,
              ""widthMode"": ""Mm"",
              ""alignment"": ""Right"",
              ""format"": ""N2""
            }
          ],
          ""headerStyle"": { ""bold"": true, ""backgroundColor"": ""#FFE0E0E0"" },
          ""borderStyle"": ""All"",
          ""showHeader"": true,
          ""x"": 10,
          ""y"": 25,
          ""width"": 190
        }
      ]
    },
    {
      ""name"": ""Patička"",
      ""printBehavior"": ""OnEveryPage"",
      ""overflowBehavior"": ""KeepTogether"",
      ""designerHeight"": 15,
      ""elements"": [
        {
          ""type"": ""TextElement"",
          ""text"": ""Stránka {STRANA}"",
          ""style"": { ""fontSize"": 10, ""hAlign"": ""Center"", ""textColor"": ""#FF666666"" },
          ""x"": 10,
          ""y"": 5,
          ""width"": 190,
          ""height"": 8
        }
      ]
    }
  ]
}
```";

    public async Task<Template?> GenerateTemplateAsync(string description, string? agendaCode, DataSchema? schema, string apiKey)
    {
        // Adaptivní myšlení necháváme zapnuté (Sonnet 5 default) - jednorázová delší úloha,
        // rozmyšlení struktury JSON reálně zlepší; temperature na Sonnetu 5 poslat nelze.
        var userPrompt = BuildUserPrompt(description, agendaCode, schema);
        var responseText = await CallApiAsync(apiKey, SystemPrompt, userPrompt, AssistantModel, MaxTokens);

        if (string.IsNullOrWhiteSpace(responseText))
            return null;

        var debugPath = SaveDebugFile(responseText);

        try
        {
            return TemplateSerializer.Deserialize(responseText);
        }
        catch (Exception ex)
        {
            throw CreateParseException(responseText, debugPath, ex);
        }
    }

    /// <summary>
    /// Sdílená metoda pro volání Anthropic Messages API.
    /// Vrací textovou odpověď (bez markdown code bloků).
    /// </summary>
    /// <param name="temperature">Null = neposílat. Sonnet 5 nedefaultní temperature ODMÍTÁ (400) -
    /// posílat jen u starších modelů (Haiku enricher).</param>
    /// <param name="disableThinking">True = explicitně vypnout myšlení (`thinking: disabled`).
    /// Na Sonnetu 5 je vynechání parametru = adaptivní myšlení ZAPNUTÉ - pro rychlé Q&A
    /// (pomocník) chceme vypnout, pro generování nechat adaptivní.</param>
    public static async Task<string?> CallApiAsync(
        string apiKey, string systemPrompt, string userPrompt,
        string model = "claude-haiku-4-5", int maxTokens = 4096, double? temperature = null,
        bool disableThinking = false)
    {
        var requestBody = new JsonObject
        {
            ["model"] = model,
            ["max_tokens"] = maxTokens,
            ["system"] = systemPrompt,
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "user", ["content"] = userPrompt }
            }
        };
        if (temperature is { } t) requestBody["temperature"] = t;
        if (disableThinking) requestBody["thinking"] = new JsonObject { ["type"] = "disabled" };

        var json = requestBody.ToJsonString();

        using var request = new HttpRequestMessage(HttpMethod.Post, ApiEndpoint);
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", ApiVersion);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        var responseJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Anthropic API vrátilo chybu {(int)response.StatusCode} ({response.StatusCode}):\n{responseJson}");

        using var responseDoc = JsonDocument.Parse(responseJson);
        ReportUsageFrom(responseDoc);
        var text = ExtractText(responseDoc);
        return text is null ? null : RemoveMarkdownCodeBlocks(text);
    }

    /// <summary>Nahlásí spotřebu tokenů z API odpovědi do mlweb (statistiky + limity, best-effort).</summary>
    private static void ReportUsageFrom(JsonDocument responseDoc)
    {
        if (!responseDoc.RootElement.TryGetProperty("usage", out var usage)) return;
        int inTok  = usage.TryGetProperty("input_tokens", out var i) ? i.GetInt32() : 0;
        int outTok = usage.TryGetProperty("output_tokens", out var o) ? o.GetInt32() : 0;
        EdisonAiService.ReportUsage(inTok, outTok);
    }

    /// <summary>
    /// Vytáhne textovou odpověď z content pole. S adaptivním myšlením (Sonnet 5 default)
    /// může content začínat thinking blokem - bere se první NEPRÁZDNÝ blok typu "text",
    /// ne content[0].
    /// </summary>
    private static string? ExtractText(JsonDocument responseDoc)
    {
        if (!responseDoc.RootElement.TryGetProperty("content", out var contentArray))
            return null;

        foreach (var block in contentArray.EnumerateArray())
        {
            if (block.TryGetProperty("type", out var type) && type.GetString() == "text" &&
                block.TryGetProperty("text", out var textProp))
            {
                var text = textProp.GetString();
                if (!string.IsNullOrWhiteSpace(text)) return text;
            }
        }
        return null;
    }

    /// <summary>Modely odmítající nedefaultní temperature (400): Sonnet 5, Fable/Mythos 5.
    /// Guard pro konfigurovatelné modely (VisionModel) - starší modely temperature přijímají.</summary>
    public static bool ModelSupportsTemperature(string model) =>
        !(model.Contains("sonnet-5") || model.Contains("fable") || model.Contains("mythos"));

    /// <summary>
    /// Volání Anthropic Messages API s obrázkem (vision).
    /// Obrázek se posílá jako base64 PNG v content bloku.
    /// </summary>
    /// <param name="temperature">Null = neposílat (nutné pro Sonnet 5+, viz ModelSupportsTemperature).</param>
    public static async Task<string?> CallApiWithImageAsync(
        string apiKey, string systemPrompt, string base64Png, string textPrompt,
        string model = "claude-sonnet-4-6", int maxTokens = 8192, double? temperature = 0.2)
    {
        // Sestavit content array s image + text bloky
        var contentArray = new JsonArray
        {
            new JsonObject
            {
                ["type"] = "image",
                ["source"] = new JsonObject
                {
                    ["type"] = "base64",
                    ["media_type"] = "image/png",
                    ["data"] = base64Png
                }
            },
            new JsonObject
            {
                ["type"] = "text",
                ["text"] = textPrompt
            }
        };

        var requestBody = new JsonObject
        {
            ["model"] = model,
            ["max_tokens"] = maxTokens,
            ["system"] = systemPrompt,
            ["messages"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = contentArray
                }
            }
        };
        if (temperature is { } t) requestBody["temperature"] = t;

        var json = requestBody.ToJsonString();

        using var request = new HttpRequestMessage(HttpMethod.Post, ApiEndpoint);
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", ApiVersion);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        var responseJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Anthropic API vrátilo chybu {(int)response.StatusCode} ({response.StatusCode}):\n{responseJson}");

        using var responseDoc = JsonDocument.Parse(responseJson);
        ReportUsageFrom(responseDoc);
        var text = ExtractText(responseDoc);
        return text is null ? null : RemoveMarkdownCodeBlocks(text);
    }

    private static string RemoveMarkdownCodeBlocks(string text)
    {
        text = text.Trim();

        // Odstranit ```json nebo ```
        if (text.StartsWith("```json"))
            text = text.Substring(7);
        else if (text.StartsWith("```"))
            text = text.Substring(3);

        if (text.EndsWith("```"))
            text = text.Substring(0, text.Length - 3);

        return text.Trim();
    }

    private static string SaveDebugFile(string jsonText)
    {
        // Temp, ne instalační adresář - v C:\Edison\ by zápis tiše selhal / zaneřáďoval složku.
        var debugPath = Path.Combine(Path.GetTempPath(), DebugFileName);
        try
        {
            File.WriteAllText(debugPath, jsonText, Encoding.UTF8);
        }
        catch
        {
            // Selhání zápisu debug souboru není kritické
        }
        return debugPath;
    }

    private static Exception CreateParseException(string jsonText, string debugPath, Exception innerException)
    {
        var preview = jsonText.Length > ErrorPreviewLength
            ? jsonText.Substring(0, ErrorPreviewLength) + "..."
            : jsonText;

        return new Exception(
            $"Chyba při parsování JSON odpovědi od AI ({jsonText.Length} znaků).\n\n" +
            $"Preview:\n{preview}\n\n" +
            $"Kompletní JSON uložen do: {debugPath}\n\n" +
            $"Chyba: {innerException.Message}",
            innerException);
    }

    private static string BuildUserPrompt(string description, string? agendaCode, DataSchema? schema)
    {
        var prompt = $"Vytvoř šablonu podle následujícího popisu:\n\n{description}";

        if (!string.IsNullOrEmpty(agendaCode))
        {
            prompt += $"\n\nAgenda: {agendaCode}";
        }

        // Pokud máme schema, přidáme dostupná pole
        if (schema is not null)
        {
            prompt += "\n\nDostupná datová pole:\n";

            // Skalární pole
            if (schema.Fields.Count > 0)
            {
                prompt += "\nSKALÁRNÍ POLE (použij jako {NAZEV_POLE} placeholdery):\n";
                foreach (var field in schema.Fields)
                {
                    prompt += $"- {field.FieldName}";
                    if (field.DataType != FieldDataType.Text)
                        prompt += $" ({field.DataType.ToString().ToLower()})";
                    if (!string.IsNullOrEmpty(field.Label))
                        prompt += $" - {field.Label}";
                    if (!string.IsNullOrEmpty(field.Example))
                        prompt += $", např. \"{field.Example}\"";
                    prompt += "\n";
                }
            }

            // Kolekce pro Dynamic tabulky
            if (schema.FieldCollections?.Count > 0)
            {
                prompt += "\nKOLEKCE (pro Dynamic tabulky):\n";
                foreach (var collection in schema.FieldCollections)
                {
                    prompt += $"- {collection.CollectionKey} (použij jako dataSource=\"{collection.CollectionKey}\")";
                    if (!string.IsNullOrEmpty(collection.Name))
                        prompt += $" - {collection.Name}";
                    prompt += ":\n";
                    foreach (var field in collection.Fields)
                    {
                        prompt += $"  - {field.FieldName}";
                        if (field.DataType != FieldDataType.Text)
                            prompt += $" ({field.DataType.ToString().ToLower()})";
                        if (!string.IsNullOrEmpty(field.Label))
                            prompt += $" - {field.Label}";
                        prompt += "\n";
                    }
                }
            }

            prompt += "\n❗ DŮLEŽITÉ: Používej POUZE pole z tohoto seznamu! Nevymýšlej vlastní názvy polí.";
        }
        else if (!string.IsNullOrEmpty(agendaCode))
        {
            // Fallback pokud nemáme schema ale máme agendaCode
            prompt += "\n\nPoužij datová pole typická pro tuto agendu (např. pro faktury: CISLO, DATUM, NAZEV_FIRMY, IC, DIC, atd.)";
        }

        return prompt;
    }
}
