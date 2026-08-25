namespace TemplateDesigner.Models.Elements;

/// <summary>
/// Prvek, který se skládá z dílů, jež <b>nemají smysl rozříznuté</b> - typicky linková tabulka
/// rozřezaná na bloky sloupců spojů.
/// </summary>
/// <remarks>
/// <para>Bez tohohle se sekce řeže čistě geometricky, tedy tam, kde zrovna dojde stránka. Změřeno
/// 25. 8.: dva bloky po 105,7 mm na stránce vysoké 210 mm daly první stránku s blokem a půl
/// a druhou s <b>1,4 mm proužkem</b> - tabulka useknutá tři řádky před koncem.</para>
/// <para>Prvek jen <b>nabídne</b> místa, kde se řezat smí; rozhodnutí zůstává na
/// <c>PageLayoutEngine</c>. Díl vyšší než celá stránka se rozřízne i tak - jinak by se nevešel nikdy.</para>
/// </remarks>
public interface INedelitelneDily
{
    /// <summary>Odsazení míst (mm od horního okraje prvku), kde se smí řezat. Prázdné = kdekoli.</summary>
    IReadOnlyList<float> ZlomyMm(DataContext? data);
}
