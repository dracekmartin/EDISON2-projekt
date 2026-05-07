using ALWA.SYS;

namespace EDControls;

/// <summary>
/// Abstraction over MLRest for testability. Covers the two call patterns
/// (CVar and object[]) plus the connection properties (mutable for
/// company-switching, read-only identity).
/// </summary>
public interface IMLRest
{
    CVar QueryOrThrow(string cacheClass, string method, CVar input);
    object[] QueryOrThrow(string cacheClass, string method, object[] input);

    /// <summary>Caché data namespace pro aktuální firmu. Mění se při přepnutí firmy.</summary>
    string DataNamespace { get; set; }

    /// <summary>ID firmy. Mění se při přepnutí firmy.</summary>
    string Idfa { get; set; }

    /// <summary>Hostname Caché serveru. Konstanta po dobu života aplikace.</summary>
    string Server { get; }

    /// <summary>REST port Caché serveru. Konstanta po dobu života aplikace.</summary>
    int Port { get; }

    /// <summary>Login uživatele. Konstanta.</summary>
    string LoginName { get; }

    /// <summary>Plné jméno uživatele. Konstanta.</summary>
    string FullName { get; }

    /// <summary>Mlp (interní user ID v Caché) jako string. Konstanta.</summary>
    string Mlp { get; }

    /// <summary>Session GUID propagovaný do hostovaných modulů (sdílená ACTIVITY session). Konstanta.</summary>
    string Guid { get; }
}
