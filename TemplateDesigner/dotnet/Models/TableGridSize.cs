namespace TemplateDesigner.Models;

/// <summary>
/// Rozměr nově vkládané pevné tabulky - jen dvě čísla, která se ptá rychlý formulář při vložení
/// (schéma <c>TableInsertEdit.cls</c>). Není to prvek ani nic, co by se ukládalo do šablony;
/// existuje proto, aby se formulář dal postavit toutéž cestou jako všechny ostatní (reflexe přes
/// property path), a ne zvláštním oknem jen pro dvě políčka.
/// </summary>
public class TableGridSize
{
    /// <summary>Výchozí 3 × 3 - kdo rozměr neřeší, jen potvrdí a dostane, co dostával dřív.</summary>
    public int Columns { get; set; } = 3;

    public int Rows { get; set; } = 3;
}
