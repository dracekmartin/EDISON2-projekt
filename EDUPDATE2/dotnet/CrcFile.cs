using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace EDUPDATE2;

/// <summary>Jedna věta EDISONCRC.bin. Filename smí končit mezerou - netrimovat, na disku tak reálně existují.</summary>
public record CrcRecord(string Filename, string SubPath, string Crc);

/// <summary>
/// Čtení EDISONCRC.bin. Formát píše legacy instalátor EDISONINS (EDISONAPI\clsUpdater.vb
/// v repu EDISON) a měnit nejde - čtení musí zůstat bit-kompatibilní.
///
/// Soubor je posloupnost vět bez oddělovačů a bez hlavičky souboru. Jedna věta:
///   1. 5 bajtů ASCII = délka payloadu v bajtech, zleva doplněná nulami (např. "00120"),
///   2. payload = base64 text (jeho ASCII bajty).
/// Payload po base64 dekódování je TripleDES-ECB s PKCS7 paddingem nad ASCII textem
/// "Filename~SubPath~Crc":
///   - Filename = jméno souboru (bez cesty; NEtrimovat, v produkci jsou i s koncovou mezerou),
///   - SubPath  = "" nebo podadresář (EDFiles, PS, locales),
///   - Crc      = SHA-256 souboru, 64 znaků uppercase hex bez pomlček.
/// Klíč je pro každou větu jiný: MD5(ASCII("System.Security.SecureString" + pořadí věty,
/// 1-based)), tj. 16 B = 2-key 3DES. Ten řetězec je doslova název typu - legacy bug:
/// volá SecureString.ToString(), který nevrací obsah, ale jméno typu; Fibonacci bajty
/// z legacy GetKey se proto nikdy nepoužijí. ECB se známým klíčem není kryptografie,
/// jen zděděná obfuskace. Bit-kompatibilitu jistí zlatý vektor z produkce v CrcFileTests.
/// </summary>
public static class CrcFile
{
    public const string FileName = "EDISONCRC.bin";
    private const int HeaderLength = 5;
    private const char Delimiter = '~';
    private const string KeyText = "System.Security.SecureString";

    /// <summary>Poškozený soubor → InvalidDataException; volající loguje a jde na plnou kopii.</summary>
    public static IReadOnlyList<CrcRecord> Read(Stream stream)
    {
        var records = new List<CrcRecord>();
        var header = new byte[HeaderLength];
        int index = 0;
        while (true)
        {
            int got = stream.ReadAtLeast(header, HeaderLength, throwOnEndOfStream: false);
            if (got == 0) break;
            if (got < HeaderLength)
                throw new InvalidDataException($"{FileName}: useknutá hlavička věty {index + 1}.");
            if (!int.TryParse(Encoding.ASCII.GetString(header), out int length) || length <= 0)
                throw new InvalidDataException($"{FileName}: nečíselná hlavička věty {index + 1}.");

            var payload = new byte[length];
            try
            {
                stream.ReadExactly(payload);
            }
            catch (EndOfStreamException)
            {
                throw new InvalidDataException($"{FileName}: useknutý payload věty {index + 1}.");
            }

            index++;
            string text;
            try
            {
                text = Decrypt(Encoding.ASCII.GetString(payload), index);
            }
            catch (Exception e) when (e is CryptographicException or FormatException)
            {
                throw new InvalidDataException($"{FileName}: věta {index} nejde dešifrovat.", e);
            }

            var parts = text.Split(Delimiter);
            if (parts.Length != 3)
                throw new InvalidDataException($"{FileName}: věta {index} nemá 3 pole.");
            // Manifest je vstup ze serverového share - jméno smí být jen jméno (trailing
            // mezera legální), SubPath jen relativní podadresář. Jinak by věta uměla zapsat
            // mimo cílový adresář.
            if (parts[0].Length == 0 || parts[0].IndexOfAny(['\\', '/', ':']) >= 0
                || parts[0].TrimEnd(' ', '.') is "" or "..")
                throw new InvalidDataException($"{FileName}: věta {index} má nebezpečné jméno souboru.");
            if (parts[1].Contains("..") || parts[1].Contains(':') || Path.IsPathRooted(parts[1]))
                throw new InvalidDataException($"{FileName}: věta {index} má nebezpečný SubPath.");
            records.Add(new CrcRecord(parts[0], parts[1], parts[2]));
        }
        return records;
    }

    /// <summary>Zápis stejným algoritmem - jen pro testy a lokální E2E simulaci, produkci píše legacy.</summary>
    internal static void Write(Stream stream, IEnumerable<CrcRecord> records)
    {
        int index = 0;
        foreach (var record in records)
        {
            index++;
            string payload = Encrypt(string.Join(Delimiter, record.Filename, record.SubPath, record.Crc), index);
            string header = payload.Length.ToString().PadLeft(HeaderLength, '0');
            var bytes = Encoding.ASCII.GetBytes(header + payload);
            stream.Write(bytes);
        }
    }

    /// <summary>SHA-256 souboru v legacy formátu (uppercase hex bez pomlček).</summary>
    public static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static TripleDES CreateCipher(int index)
    {
        var des = TripleDES.Create();
        des.Key = MD5.HashData(Encoding.ASCII.GetBytes(KeyText + index)); // 16 B = 2-key 3DES
        des.Mode = CipherMode.ECB;
        des.Padding = PaddingMode.PKCS7;
        return des;
    }

    private static string Decrypt(string base64, int index)
    {
        using var des = CreateCipher(index);
        var data = Convert.FromBase64String(base64);
        return Encoding.ASCII.GetString(des.CreateDecryptor().TransformFinalBlock(data, 0, data.Length));
    }

    private static string Encrypt(string text, int index)
    {
        using var des = CreateCipher(index);
        var data = Encoding.ASCII.GetBytes(text);
        return Convert.ToBase64String(des.CreateEncryptor().TransformFinalBlock(data, 0, data.Length));
    }
}
