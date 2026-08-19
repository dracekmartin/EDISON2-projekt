using ALWA.SYS;

namespace EDControls;

/// <summary>
/// Abstrakce nad <c>MLRest</c> pro testovatelnost. Pokrývá dva volací patterny
/// (CVar i <c>object[]</c>) plus connection property (mutable pro přepnutí firmy,
/// read-only identity).
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

    /// <summary>Oddělovač polí pro stará <c>object[]</c> Caché volání (typicky <c>'\0'</c>).</summary>
    char Delimiter { get; }

    /// <summary>
    /// Režim serverových kontrol per request (sys <c>"U","Locks"</c>): 0 = žádné,
    /// 1 = obnova ACTIVITY historie + LockExist + version gate, 2 = jen idle reaper (poll).
    /// Sdílená session jede po úspěšném VerifyLic s 1 (nastavuje <c>LicenceVerifier</c>);
    /// jednorázové odchylky dělej klonem přes <see cref="With"/>, tohle mutuj jen tam,
    /// kde se mění session (přepnutí firmy).
    /// </summary>
    int CacheLocks { get; set; }

    /// <summary>
    /// Vrátí **novou** instanci se stejnou identitou (server/port/login/mlp/GUID/verze/delimiter),
    /// ale s přepsaným <paramref name="cacheLocks"/> / <paramref name="idfa"/> /
    /// <paramref name="dataNamespace"/> (null = ponechat). Pro jednorázové requesty, které nesmí
    /// měnit sdílenou instanci ani se prát se souběžnými voláními: poll bez obnovy aktivity
    /// (<c>CacheLocks=2</c>), logout cizí firmy (<c>idfa</c>). MLRest dělá HTTP per-call, takže
    /// samostatný klon (se stejným GUID = pro server tatáž session) je bezpečný.
    /// </summary>
    IMLRest With(int? cacheLocks = null, string? idfa = null, string? dataNamespace = null);
}
