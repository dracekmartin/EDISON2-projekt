namespace TemplateDesigner.Models;

/// <summary>Jak vážná je hláška vpravo dole (`B178`).</summary>
/// <remarks>
/// Rozhoduje <b>následek na papíře</b>, ne to, která část návrháře hlášku vyrobila (rozhodnutí
/// Jan 24. 9.): chyba = něco chybí nebo přetéká, upozornění = vytiskne se jinak, než je nastavené,
/// informace = návrhář něco upravil, jak má (zmenšení písma, o které si zákazníci sami řekli) -
/// nesmí vypadat jako vada, jinak z ní uživatel zpanikaří.
/// </remarks>
public enum ZavaznostHlaseni { Informace, Upozorneni, Chyba }

/// <summary>
/// Jedna hláška vpravo dole. <paramref name="PrvekId"/> = prvek, ke kterému patří - klik na řádek
/// seznamu ho vybere na plátně; null = patří celé šabloně nebo datům.
/// </summary>
public sealed record Hlaseni(ZavaznostHlaseni Zavaznost, string Text, Guid? PrvekId = null)
{
    /// <summary>Řádek seznamu jde prokliknout na prvek.</summary>
    public bool MaPrvek => PrvekId is not null;
}
