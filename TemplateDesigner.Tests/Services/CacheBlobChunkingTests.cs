using TemplateDesigner.Services;

namespace TemplateDesigner.Tests.Services;

/// <summary>
/// Split → Join musí být bezeztrátové pro libovolnou velikost blobu. Chrání ukládání
/// šablon s velkým inline obrázkem (blob přes limit délky Caché řetězce se posílá po kusech).
/// </summary>
public class CacheBlobChunkingTests
{
    [Theory]
    [InlineData(0)]        // prázdný
    [InlineData(1)]        // jeden znak
    [InlineData(999)]      // pod hranicí
    [InlineData(1000)]     // přesně jeden kus
    [InlineData(1001)]     // o znak přes → 2 kusy
    [InlineData(2500)]     // 3 kusy, poslední neúplný
    [InlineData(10000)]    // 10 kusů
    public void SplitThenJoin_RoundTrips(int length)
    {
        var data = MakeData(length);

        var chunks = CacheBlobChunking.Split(data, maxChunkChars: 1000);
        var joined = CacheBlobChunking.Join(chunks);

        Assert.Equal(data, joined);
    }

    [Theory]
    [InlineData(0, 1000, 0)]
    [InlineData(1000, 1000, 1)]
    [InlineData(1001, 1000, 2)]
    [InlineData(2500, 1000, 3)]
    public void Split_ProducesExpectedChunkCount(int length, int chunkSize, int expectedChunks)
    {
        var chunks = CacheBlobChunking.Split(MakeData(length), chunkSize);
        Assert.Equal(expectedChunks, chunks.Count);
    }

    [Fact]
    public void Split_ChunksNeverExceedMax()
    {
        var chunks = CacheBlobChunking.Split(MakeData(9999), maxChunkChars: 1000);
        Assert.All(chunks, c => Assert.True(c.Length <= 1000));
    }

    [Fact]
    public void Split_EmptyInput_ReturnsEmptyList()
    {
        Assert.Empty(CacheBlobChunking.Split(""));
    }

    /// <summary>Kus musí zůstat hluboko pod serverovým limitem délky řádku (~32 656 znaků při
    /// <c>%Stream.ReadLine()</c> ve <c>StreamToGlobal</c>). Moc velký kus (dřív 1 000 000) shodil
    /// příjem requestu na <c>&lt;SYNTAX&gt;</c> - tenhle test chrání proti návratu té regrese.</summary>
    [Fact]
    public void DefaultChunkSize_StaysSafelyBelowServerLineLimit()
    {
        Assert.True(CacheBlobChunking.DefaultMaxChunkChars <= 30_000,
            $"DefaultMaxChunkChars={CacheBlobChunking.DefaultMaxChunkChars} je nebezpečně blízko/nad limitem řádku ~32 656.");
    }

    [Fact]
    public void Split_NonPositiveChunkSize_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CacheBlobChunking.Split("abc", 0));
    }

    /// <summary>Simuluje reálný blob: velký kus opakovaného textu ~6 MB (velikost problémového obrázku).</summary>
    [Fact]
    public void SplitThenJoin_LargeImageSizedBlob_RoundTrips()
    {
        var data = MakeData(6 * 1024 * 1024);
        var chunks = CacheBlobChunking.Split(data);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.True(c.Length <= CacheBlobChunking.DefaultMaxChunkChars));
        Assert.Equal(data, CacheBlobChunking.Join(chunks));
    }

    private static string MakeData(int length)
    {
        // Deterministický vzor, ať se pozná posun/ztráta znaků na hranicích kusů.
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789+/";
        var buf = new char[length];
        for (int i = 0; i < length; i++)
            buf[i] = alphabet[i % alphabet.Length];
        return new string(buf);
    }
}
