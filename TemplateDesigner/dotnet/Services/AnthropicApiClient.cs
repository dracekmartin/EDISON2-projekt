using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    private const string ModelId = "claude-haiku-4-5";

    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(3)
    };
    private const int MaxTokens = 8192;
    private const double Temperature = 0.7;
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
- `Once` — vytiskne se jednou (výchozí)
- `OnEveryPage` — každá stránka (hlavička/patička)
- `OnFirstPageOnly` — jen první stránka
- `OnLastPageOnly` — jen poslední stránka
- `OnEveryPageExceptFirst` — všechny kromě první

### OverflowBehavior hodnoty:
- `KeepTogether` — nepřerušovat
- `SplitAcrossPages` — rozdělí se přes stránky
- `AllowSplit` — rozdělení povoleno ale ne preferováno

## Elements (typy)

### TextElement
```json
{
  ""type"": ""TextElement"",
  ""x"": 10,
  ""y"": 5,
  ""width"": 50,
  ""height"": 8,
  ""text"": ""Faktura č. {CISLO}"",
  ""fontSize"": 14,
  ""fontWeight"": ""Bold"",
  ""textColor"": ""#FF000000"",
  ""backgroundColor"": ""#00FFFFFF"",
  ""horizontalAlignment"": ""Left"",
  ""verticalAlignment"": ""Top""
}
```

**FontWeight:** `Normal`, `Bold`
**HorizontalAlignment:** `Left`, `Center`, `Right`, `Justify`
**VerticalAlignment:** `Top`, `Middle`, `Bottom`

### RectangleElement
```json
{
  ""type"": ""RectangleElement"",
  ""x"": 10,
  ""y"": 10,
  ""width"": 100,
  ""height"": 30,
  ""fillColor"": ""#FFE0E0E0"",
  ""strokeColor"": ""#FF000000"",
  ""strokeWidth"": 0.5,
  ""cornerRadius"": 0
}
```

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

### TableElement

Tabulka podporuje dva režimy:

**Fixed mód** — řádky definované přímo v šabloně (pro pevná data):
```json
{
  ""type"": ""TableElement"",
  ""x"": 10,
  ""y"": 30,
  ""width"": 190,
  ""mode"": ""Fixed"",
  ""columns"": [
    {
      ""header"": ""Položka"",
      ""field"": ""NAZEV"",
      ""width"": 80,
      ""widthMode"": ""Mm"",
      ""alignment"": ""Left""
    },
    {
      ""header"": ""Cena"",
      ""field"": ""CENA"",
      ""width"": 30,
      ""widthMode"": ""Mm"",
      ""alignment"": ""Right""
    }
  ],
  ""rows"": [
    {
      ""cells"": [
        { ""content"": ""Položka 1"" },
        { ""content"": ""{CENA}"", ""format"": ""N2"" }
      ]
    },
    {
      ""cells"": [
        { ""content"": ""Položka 2"" },
        { ""content"": ""{CELKEM}"", ""format"": ""N2"" }
      ]
    }
  ],
  ""headerBackgroundColor"": ""#FFE0E0E0"",
  ""alternatingRowColor"": ""#FFF5F5F5"",
  ""borderStyle"": ""All"",
  ""showHeader"": true
}
```

**Dynamic mód** — řádky generované z dat za běhu (pro seznamy):
```json
{
  ""type"": ""TableElement"",
  ""x"": 10,
  ""y"": 30,
  ""width"": 190,
  ""mode"": ""Dynamic"",
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
  ""headerBackgroundColor"": ""#FFE0E0E0"",
  ""alternatingRowColor"": ""#FFF5F5F5"",
  ""borderStyle"": ""All"",
  ""showHeader"": true,
  ""rowHeight"": 6
}
```

**KRITICKY DŮLEŽITÉ — běžná chyba:**
- **Fixed mód:** Musí mít `""rows""` array s definovanými řádky. Nepoužívá `""dataSource""`.
- **Dynamic mód:** NESMÍ mít `""rows""` property! Musí mít `""dataSource""` (klíč kolekce dat).

ŠPATNĚ (nekombinuj mode: Dynamic s rows):
```json
{
  ""mode"": ""Dynamic"",
  ""dataSource"": ""POLOZKY"",
  ""rows"": [ ... ]  ← CHYBA! Dynamic nemá rows!
}
```

SPRÁVNĚ:
```json
{
  ""mode"": ""Dynamic"",
  ""dataSource"": ""POLOZKY""
  // žádné rows!
}
```

**Mode:** `Fixed`, `Dynamic`
**WidthMode:** `Mm`, `Percent`
**Alignment:** `Left`, `Center`, `Right`
**BorderStyle:** `None`, `Outer` (pouze vnější rámeček), `All` (všechny čáry), `RowsOnly` (jen vodorovné), `ColumnsOnly` (jen svislé)

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

**BarcodeType:** `QR`, `Code128`

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

2a. **Kdy použít Fixed vs Dynamic tabulku:**
   - **Fixed** — pevný počet řádků známý dopředu (např. faktura má vždy 3 řádky s popisem, částkou, daní)
   - **Dynamic** — proměnný počet řádků z databáze (např. položky faktury, seznam zastávek)
   - Pro obecné šablony bez specifikované agendy použij raději **Fixed** s 2-5 řádky

2b. **AgendaCode:**
   - Pokud uživatel specifikuje agendu (""faktura"", ""jízdní řád""), nastav `agendaCode` podle toho
   - Příklady: `""JR""` pro jízdní řády, `""FAKTURY""` pro faktury
   - Pokud není specifikováno nebo je šablona obecná, VYNECHEJ agendaCode property úplně (ne null, ne prázdný string)

3. **Běžné sekce:**
   - Záhlaví: `PrintBehavior=OnEveryPage`, výška 20-30 mm
   - Obsah: `PrintBehavior=Once`, výška podle potřeby
   - Patička: `PrintBehavior=OnEveryPage`, výška 15-20 mm

4. **Placeholdery:**
   - Datová pole: `{CISLO}`, `{NAZEV}`, `{DATUM}`
   - Speciální: `{PAGE}` (číslo stránky), `{DATE}` (datum tisku)
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
          ""fontSize"": 16,
          ""fontWeight"": ""Bold"",
          ""horizontalAlignment"": ""Center"",
          ""textColor"": ""#FF000000"",
          ""backgroundColor"": ""#00FFFFFF"",
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
          ""fontSize"": 12,
          ""fontWeight"": ""Normal"",
          ""horizontalAlignment"": ""Left"",
          ""textColor"": ""#FF000000"",
          ""backgroundColor"": ""#00FFFFFF"",
          ""x"": 10,
          ""y"": 10,
          ""width"": 190,
          ""height"": 8
        },
        {
          ""type"": ""TableElement"",
          ""mode"": ""Fixed"",
          ""columns"": [
            {
              ""header"": ""Popis"",
              ""width"": 130,
              ""widthMode"": ""Mm"",
              ""alignment"": ""Left""
            },
            {
              ""header"": ""Hodnota"",
              ""width"": 60,
              ""widthMode"": ""Mm"",
              ""alignment"": ""Right""
            }
          ],
          ""rows"": [
            {
              ""cells"": [
                { ""content"": ""Celkem"" },
                { ""content"": ""{CELKEM}"", ""format"": ""N2"" }
              ]
            }
          ],
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
          ""text"": ""Stránka {PAGE}"",
          ""fontSize"": 10,
          ""fontWeight"": ""Normal"",
          ""horizontalAlignment"": ""Center"",
          ""textColor"": ""#FF666666"",
          ""backgroundColor"": ""#00FFFFFF"",
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
        var userPrompt = BuildUserPrompt(description, agendaCode, schema);
        var responseText = await CallApiAsync(apiKey, SystemPrompt, userPrompt, ModelId, MaxTokens, Temperature);

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
    public static async Task<string?> CallApiAsync(
        string apiKey, string systemPrompt, string userPrompt,
        string model = "claude-haiku-4-5", int maxTokens = 4096, double temperature = 0.3)
    {
        var requestBody = new
        {
            model,
            max_tokens = maxTokens,
            temperature,
            system = systemPrompt,
            messages = new[]
            {
                new { role = "user", content = userPrompt }
            }
        };

        var json = JsonSerializer.Serialize(requestBody);

        using var request = new HttpRequestMessage(HttpMethod.Post, ApiEndpoint);
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", ApiVersion);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        var responseJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Anthropic API vrátilo chybu {(int)response.StatusCode} ({response.StatusCode}):\n{responseJson}");

        var responseDoc = JsonDocument.Parse(responseJson);

        if (responseDoc.RootElement.TryGetProperty("content", out var contentArray) &&
            contentArray.GetArrayLength() > 0)
        {
            var text = contentArray[0].GetProperty("text").GetString();
            if (string.IsNullOrWhiteSpace(text))
                return null;

            return RemoveMarkdownCodeBlocks(text);
        }

        return null;
    }

    /// <summary>
    /// Volání Anthropic Messages API s obrázkem (vision).
    /// Obrázek se posílá jako base64 PNG v content bloku.
    /// </summary>
    public static async Task<string?> CallApiWithImageAsync(
        string apiKey, string systemPrompt, string base64Png, string textPrompt,
        string model = "claude-sonnet-4-6", int maxTokens = 8192, double temperature = 0.2)
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
            ["temperature"] = temperature,
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

        var json = requestBody.ToJsonString();

        using var request = new HttpRequestMessage(HttpMethod.Post, ApiEndpoint);
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", ApiVersion);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        var responseJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Anthropic API vrátilo chybu {(int)response.StatusCode} ({response.StatusCode}):\n{responseJson}");

        var responseDoc = JsonDocument.Parse(responseJson);

        if (responseDoc.RootElement.TryGetProperty("content", out var contentArr) &&
            contentArr.GetArrayLength() > 0)
        {
            var text = contentArr[0].GetProperty("text").GetString();
            if (string.IsNullOrWhiteSpace(text))
                return null;

            return RemoveMarkdownCodeBlocks(text);
        }

        return null;
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
        var debugPath = Path.Combine(AppContext.BaseDirectory, DebugFileName);
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
                        prompt += $" — {field.Label}";
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
                        prompt += $" — {collection.Name}";
                    prompt += ":\n";
                    foreach (var field in collection.Fields)
                    {
                        prompt += $"  - {field.FieldName}";
                        if (field.DataType != FieldDataType.Text)
                            prompt += $" ({field.DataType.ToString().ToLower()})";
                        if (!string.IsNullOrEmpty(field.Label))
                            prompt += $" — {field.Label}";
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
