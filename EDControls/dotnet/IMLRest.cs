using ALWA.SYS;

namespace EDControls;

/// <summary>
/// Abstraction over MLRest for testability. Covers the two call patterns
/// (CVar and object[]) plus the mutable connection properties that
/// application code sets at runtime.
/// </summary>
public interface IMLRest
{
    CVar QueryOrThrow(string cacheClass, string method, CVar input);
    object[] QueryOrThrow(string cacheClass, string method, object[] input);

    string DataNamespace { get; set; }
    string Idfa { get; set; }
}
