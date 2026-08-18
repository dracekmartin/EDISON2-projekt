using SkiaSharp;

namespace TemplateDesigner.Rendering;

/// <summary>
/// Dekódovaný obrázek z Base64 s cache. Drží se obojí - bitmapa i obrázek - protože
/// <c>SetImmutable</c> nechá obrázek sdílet pixely s bitmapou místo kopie.
/// <para>
/// Neúspěch se cachuje taky: bez toho by se poškozená data zkoušela dekódovat při každém
/// překreslení. Cache se pozná podle reference na tentýž řetězec, takže přepsání obrázku
/// v editoru ji shodí.
/// </para>
/// </summary>
internal sealed class CachedBase64Image
{
    private string? _base64;
    private SKBitmap? _bitmap;
    private SKImage? _image;

    /// <summary>Počet skutečných dekódování - testovací sonda, že cache drží.</summary>
    internal int DecodeCount { get; private set; }

    /// <summary>Obrázek pro daná Base64 data, nebo null (prázdná / poškozená data).</summary>
    internal SKImage? Get(string? base64)
    {
        if (ReferenceEquals(base64, _base64)) return _image;

        _image?.Dispose();
        _bitmap?.Dispose();
        _image = null;
        _bitmap = null;
        _base64 = base64;

        if (string.IsNullOrEmpty(base64)) return null;

        DecodeCount++;
        try
        {
            _bitmap = SKBitmap.Decode(System.Convert.FromBase64String(base64));
            if (_bitmap is not null)
            {
                _bitmap.SetImmutable();
                _image = SKImage.FromBitmap(_bitmap);
            }
        }
        catch
        {
            _bitmap?.Dispose();
            _bitmap = null;
            _image = null;
        }
        return _image;
    }
}
