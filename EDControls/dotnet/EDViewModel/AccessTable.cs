using System.Collections.Generic;

namespace EDControls.EDViewModel;

/// <summary>
/// Aplikační přístup uživatele - whitelist element IDs (form / package), kterými
/// se filtruje strom modulů a runtime checky v agendách.
/// Vrací <c>ALVA.SYS.Login.GetEdisonAccess</c> per (user × IdFa). Pro admina
/// (server-side apId=3 / LoginName=SuperUser) je <see cref="IsAllAccess"/> true →
/// <see cref="Contains"/> vrací vždy true. Per-IdFa - reload při přepnutí firmy.
/// </summary>
public sealed class AccessTable
{
    private readonly HashSet<string> _allowed;

    /// <summary>True = apId=3 (admin), Contains vrací vždy true bez kontroly hash setu.</summary>
    public bool IsAllAccess { get; }

    public AccessTable(HashSet<string> allowed, bool isAllAccess)
    {
        _allowed = allowed;
        IsAllAccess = isAllAccess;
    }

    public bool Contains(string elementId) =>
        IsAllAccess || _allowed.Contains(elementId);
}
