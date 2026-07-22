namespace TemplateDesigner.Services;

/// <summary>
/// Rozdělení JSON blobu šablony na kusy pod limitem délky Caché řetězce a jejich zpětné složení.
///
/// <para>
/// Caché neunese jeden řetězec delší než ~3,64 MB (max string length). Celý blob šablony
/// (vč. inline Base64 obrázků) může tuto hranici přesáhnout — pak selže už příjem requestu
/// na serveru (<c>StreamToGlobal</c>), dřív než se spustí ukládací logika. Blob proto posíláme
/// po kusech; server je ukládá jako samostatné uzly a nikdy neslepuje do jednoho řetězce.
/// Klient (.NET) žádný takový limit nemá, takže složení kusů zpět je bezpečné.
/// </para>
///
/// <para>
/// Serializovaný JSON je čisté ASCII (default encoder escapuje non-ASCII na <c>\uXXXX</c>),
/// takže dělení po znacích je bezeztrátové — žádný multibyte znak ani surrogate pár se
/// na hranici kusu nerozpůlí.
/// </para>
/// </summary>
public static class CacheBlobChunking
{
    /// <summary>Výchozí velikost kusu ve znacích — bezpečně pod limitem Caché řetězce (~3,64 MB).</summary>
    public const int DefaultMaxChunkChars = 1_000_000;

    /// <summary>Rozdělí <paramref name="data"/> na kusy o max. <paramref name="maxChunkChars"/> znacích.
    /// Prázdný vstup vrátí prázdný seznam.</summary>
    public static List<string> Split(string data, int maxChunkChars = DefaultMaxChunkChars)
    {
        if (maxChunkChars <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxChunkChars));
        if (string.IsNullOrEmpty(data))
            return [];

        var chunks = new List<string>((data.Length / maxChunkChars) + 1);
        for (int i = 0; i < data.Length; i += maxChunkChars)
            chunks.Add(data.Substring(i, Math.Min(maxChunkChars, data.Length - i)));
        return chunks;
    }

    /// <summary>Složí kusy zpět do původního blobu (prosté zřetězení v pořadí).</summary>
    public static string Join(IEnumerable<string> chunks) => string.Concat(chunks);
}
