namespace TemplateDesigner.Models;

/// <summary>
/// Designer kontext uložený se šablonou — grid a snap nastavení.
/// Pomáhá zachovat designové prostředí, ve kterém byla šablona laděná
/// (např. 0,1mm grid pro precizní práci → po reopen se nezobrazí default 5mm grid).
/// Všechny vlastnosti jsou nullable — null = aplikace použije globální default.
/// </summary>
public class TemplateEditorPreferences
{
    public float? GridSize { get; set; }
    public bool? SnapToGrid { get; set; }
    public bool? SnapToElements { get; set; }
    public bool? ShowGrid { get; set; }
}
