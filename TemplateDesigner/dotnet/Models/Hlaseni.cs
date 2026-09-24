namespace TemplateDesigner.Models;

/// <summary>Jak vážná je hláška vpravo dole (`B178`).</summary>
/// <remarks>
/// Rozhoduje <b>následek na papíře</b>, ne to, která část návrháře hlášku vyrobila (rozhodnutí
/// Jan 24. 9.): chyba (v UI „problém") = část obsahu se nevytiskne; upozornění = výstup se liší
/// od nastavení šablony a uživatel o tom nemusí vědět (náhradní font, malý kód, nesplněný počet
/// stran); informace = obsah je upravený podle nastavení šablony (zmenšené písmo, počet stran) -
/// nesmí vypadat jako vada, jinak z ní uživatel zpanikaří.
/// </remarks>
public enum ZavaznostHlaseni { Informace, Upozorneni, Chyba }

/// <summary>
/// Jedna hláška vpravo dole v jednotném tvaru (rozhodnutí Jan 24. 9.): <paramref name="Kde"/> =
/// prvek nebo sekce a co nejpřesnější místo v nich (sloupec, spoj, stránky), <paramref name="Co"/> =
/// co se stane na papíře, <paramref name="Rada"/> = krátká rada, pár slov, ne návod.
/// <paramref name="PrvekId"/> = prvek, ke kterému patří - klik na řádek ho vybere; null = šablona, data.
/// </summary>
public sealed record Hlaseni(ZavaznostHlaseni Zavaznost, string Kde, string Co, string? Rada = null,
    Guid? PrvekId = null)
{
    /// <summary>Linka, na kterou klik v okně kontroly přepne (první z těch, kterých se hláška
    /// týká); null = aktuální linka.</summary>
    public string? Linka { get; init; }

    /// <summary>Řádek seznamu jde prokliknout na prvek nebo linku.</summary>
    public bool MaPrvek => PrvekId is not null || Linka is not null;

    /// <summary>Kde a co - do bloku s jedinou hláškou, kde se rada nevejde.</summary>
    public string KratkyText => $"{Kde}: {Co}";

    /// <summary>Celá hláška jedním řádkem - souhrn v tiskovém okně.</summary>
    public string Text => Rada is null ? KratkyText : $"{KratkyText} - {Rada}";

    /// <summary>Rada s oddělovačem pro řádek seznamu; prázdná, když rada není.</summary>
    public string RadaVRadku => Rada is null ? "" : $"  → {Rada}";
}
