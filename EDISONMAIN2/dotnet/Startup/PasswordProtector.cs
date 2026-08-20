using System.Security.Cryptography;
using System.Text;

namespace EDISONMAIN2;

/// <summary>
/// Lokální šifrování zapamatovaného hesla přes Windows DPAPI (<see cref="DataProtectionScope.CurrentUser"/>).
/// Klíč spravuje OS a je vázaný na Windows účet + stanici - blob nejde rozšifrovat jiným
/// Windows uživatelem ani na jiném stroji. Žádný klíč se neukládá (na rozdíl od legacy shellu,
/// který AES klíč ukládal vedle šifry = jen obfuskace).
/// </summary>
public static class PasswordProtector
{
    // App-specific entropy - oddělí náš blob od jiných DPAPI blobů (není to tajemství).
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("EDISONMAIN2.RememberPassword.v1");
    private const char Sep = '\0';

    /// <summary>Zašifruje <c>username\0password</c> → Base64 DPAPI blob. Jméno je v blobu, aby
    /// šlo při čtení ověřit, že heslo patří k očekávanému uživateli.</summary>
    public static string Protect(string username, string password)
    {
        byte[] plain = Encoding.UTF8.GetBytes(username + Sep + password);
        byte[] blob = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(blob);
    }

    /// <summary>Vrátí heslo, pokud blob jde rozšifrovat a uložené jméno == <paramref name="expectedUsername"/>;
    /// jinak null (poškozený blob / jiný uživatel / jiná stanice / nesouhlasí jméno).</summary>
    public static string? Unprotect(string? blob, string expectedUsername)
    {
        if (string.IsNullOrEmpty(blob)) return null;
        try
        {
            byte[] plain = ProtectedData.Unprotect(Convert.FromBase64String(blob), Entropy, DataProtectionScope.CurrentUser);
            string[] parts = Encoding.UTF8.GetString(plain).Split(Sep);
            return parts.Length == 2 && parts[0] == expectedUsername ? parts[1] : null;
        }
        catch
        {
            return null;
        }
    }
}
