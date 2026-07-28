namespace TemplateDesigner.Services;

/// <summary>
/// Rozdělení JSON blobu šablony na kusy, které bezpečně projdou příjmem requestu na serveru,
/// a jejich zpětné složení.
///
/// <para>
/// Limit NENÍ max délka Caché řetězce (~3,64 MB), jak se původně myslelo, ale délka jednoho
/// <b>řádku</b> při příjmu requestu. MLRest server (<c>ALWA.SYS.Utility.StreamToGlobal</c>) čte
/// vstup po záznamech přes <c>stream.ReadLine()</c> <b>bez délkového argumentu</b> - <c>%Stream</c>
/// pak vrací nejvýš ~32 656 znaků na volání a <c>StreamToGlobal</c> řádek přes oddělovač
/// neslepuje (ignoruje <c>eol</c>). Jeden CVar záznam delší než tento limit se tedy rozpůlí,
/// pokračování se rozparsuje jako „klíč" plný JSON/base64 znaků → neplatná indirekce
/// <c>s @ref=val</c> → <c>&lt;SYNTAX&gt;</c>. Proto musí <b>každý kus</b> (= hodnota jednoho
/// CVar záznamu) zůstat hluboko pod ~32 KB. Server kusy ukládá jako samostatné uzly a nikdy
/// je neslepuje; klient (.NET) žádný takový limit nemá, takže složení zpět je bezpečné.
/// </para>
///
/// <para>
/// Serializovaný JSON je čisté ASCII (default encoder escapuje non-ASCII na <c>\uXXXX</c>),
/// takže dělení po znacích je bezeztrátové - žádný multibyte znak ani surrogate pár se
/// na hranici kusu nerozpůlí, a znak = bajt (žádné nafouknutí při UTF-8 dekódování na serveru).
/// </para>
/// </summary>
public static class CacheBlobChunking
{
    /// <summary>Výchozí velikost kusu ve znacích. Bezpečně pod serverovým limitem délky řádku
    /// (~32 656 znaků při <c>%Stream.ReadLine()</c>) s velkou rezervou na klíč záznamu i případné
    /// odchylky konfigurace. Rezervu neškrtat pod ~30 000 bez ověření - právě moc velký kus
    /// (dřív 1 000 000) byl příčinou pádu <c>&lt;SYNTAX&gt;</c> při ukládání velkého obrázku.</summary>
    public const int DefaultMaxChunkChars = 20_000;

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
