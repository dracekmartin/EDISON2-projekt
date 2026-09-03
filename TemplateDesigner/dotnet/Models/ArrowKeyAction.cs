namespace TemplateDesigner.Models;

/// <summary>Co udělají šipky nad vybraným prvkem podle držených modifikátorů.</summary>
public enum ArrowKeyAction
{
    /// <summary>Posun o 1 mm (samotné šipky).</summary>
    Move,

    /// <summary>Posun o velikost mřížky (Ctrl).</summary>
    MoveByGrid,

    /// <summary>Změna velikosti o 1 mm (Shift) - pravá a dolní hrana.</summary>
    Resize,
}
