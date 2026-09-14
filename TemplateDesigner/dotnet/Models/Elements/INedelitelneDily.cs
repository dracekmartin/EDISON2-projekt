namespace TemplateDesigner.Models.Elements;

/// <summary>Jak dobré je to místo řezu. Vyšší hodnota vyhrává, i když leží výš na stránce.</summary>
public enum ZlomUroven
{
    /// <summary>Nouze - hranice řádku uvnitř zastávky. Řez tu rozdělí zastávku od jejího
    /// příjezdového či odjezdového protějšku nebo od přípojných řádků, takže se použije jedině
    /// tehdy, když se jinam říznout nedá (zastávka vyšší než celá stránka). Bez téhle úrovně
    /// by stránkovací smyčka neměla kam říznout a zacyklila by se.</summary>
    Radek = 0,

    /// <summary>Hranice mezi zastávkami - celá zastávka i s přípoji zůstala nahoře.</summary>
    Zastavka = 1,

    /// <summary>Hranice celého dílu (mezi bloky tabulky) - přirozený konec stránky.</summary>
    Blok = 2,
}

/// <summary>Místo, kudy se prvek smí rozříznout mezi stránky.</summary>
/// <param name="Mm">Odsazení od horního okraje prvku.</param>
/// <param name="Uroven">Jak dobré to místo je - viz <see cref="ZlomUroven"/>.</param>
/// <param name="MrtvaMezeraMm">
/// Kolik mm těsně před zlomem je prázdných a při zlomu stránky se nemají kreslit.
/// </param>
/// <remarks>
/// ⚠️ **Mrtvá mezera vzniká tam, kde odstup patří dvojici dílů, ne jednomu z nich** - u tabulky
/// linky je to odstup mezi bloky. Když ty dva díly rozdělí stránka, odstup ztratí smysl: kdyby se
/// kreslil, zůstane na konci strany prázdný pruh a další obsah (poznámky pod tabulkou) začne o něj
/// níž. Vypadá to jako nepravidelnost, protože na poslední straně, kde se neláme, žádný není
/// (nález Jan 10. 9. na lince 330510: řez na 74,9 mm, poslední řádek končí na 70,9).
/// </remarks>
public readonly record struct Zlom(float Mm, ZlomUroven Uroven, float MrtvaMezeraMm = 0f)
{
    /// <summary>Hranice celého dílu. Zkratka pro <see cref="ZlomUroven.Blok"/>.</summary>
    public bool Silny => Uroven == ZlomUroven.Blok;
}

/// <summary>
/// Prvek, který se skládá z dílů, jež <b>nemají smysl rozříznuté</b> - typicky linková tabulka
/// rozřezaná na bloky sloupců spojů.
/// </summary>
/// <remarks>
/// <para>Bez tohohle se sekce řeže čistě geometricky, tedy tam, kde zrovna dojde stránka. Změřeno
/// 25. 8.: dva bloky po 105,7 mm na stránce vysoké 210 mm daly první stránku s blokem a půl
/// a druhou s <b>1,4 mm proužkem</b> - tabulka useknutá tři řádky před koncem.</para>
/// <para>Prvek jen <b>nabídne</b> místa, kde se řezat smí; rozhodnutí zůstává na
/// <c>PageLayoutEngine</c>. Díl vyšší než celá stránka se rozříznout musí - proto slabší úrovně:
/// aspoň ať řez padne mezi zastávkami a ne doprostřed textu.</para>
/// </remarks>
public interface INedelitelneDily
{
    /// <summary>Místa, kde se smí řezat. Prázdné = kdekoli, jako dřív.</summary>
    IReadOnlyList<Zlom> ZlomyMm(DataContext? data);

    /// <summary>
    /// Výška hlavičky, kterou musí zopakovat pokračovací stránka (0 = nic neopakovat).
    /// </summary>
    /// <remarks>
    /// Platí jen pro řez <b>uvnitř</b> dílu - tam čtenář ztratí záhlaví a nepozná, který sloupec
    /// je který spoj. Stránka, která začíná novým dílem, si hlavičku přinese sama.
    /// </remarks>
    float OpakovanaHlavickaMm(DataContext? data);
}
