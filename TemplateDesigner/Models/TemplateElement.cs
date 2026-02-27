using System.Text.Json.Serialization;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextElement),      "TextElement")]
[JsonDerivedType(typeof(RectangleElement), "RectangleElement")]
[JsonDerivedType(typeof(LineElement),      "LineElement")]
[JsonDerivedType(typeof(ImageElement),     "ImageElement")]
[JsonDerivedType(typeof(TableElement),     "TableElement")]
public abstract class TemplateElement
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Pozice a rozměry v mm, relativně k levému hornímu rohu sekce
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }

    /// <summary>
    /// Výška v mm. Null u dynamických elementů (TableElement) —
    /// výšku vypočítá print engine za běhu, designer zobrazí placeholder.
    /// </summary>
    public float? Height { get; set; }

    public int ZIndex { get; set; }
    public bool IsLocked { get; set; }

    /// <summary>
    /// Výraz pro podmíněnou viditelnost, např. "{POCET} > 0".
    /// Null nebo prázdný = vždy viditelný. Vyhodnocuje se v print enginu.
    /// </summary>
    public string? VisibilityExpression { get; set; }

    /// <summary>Bag pro budoucí vlastnosti bez zásahu do modelu.</summary>
    public Dictionary<string, string>? CustomProperties { get; set; }

    // ---------------------------------------------------------------------------

    /// <summary>
    /// Vykreslí element na canvas. Volá designer, preview i print engine.
    /// Souřadnice v ctx jsou přepočítány do px — element pracuje s vlastními mm hodnotami.
    /// </summary>
    public abstract void Render(RenderContext ctx);

    /// <summary>
    /// Vytvoří hlubokou kopii elementu s novým Id. Používá se při copy/paste.
    /// </summary>
    public abstract TemplateElement Clone();

    // ---------------------------------------------------------------------------

    [JsonIgnore]
    public bool IsDynamic => !Height.HasValue;

    /// <summary>Výška pro zobrazení v designeru — Height nebo placeholder dynamické sekce.</summary>
    [JsonIgnore]
    public float EffectiveDesignerHeight => Height ?? DesignerPlaceholderHeight;

    protected virtual float DesignerPlaceholderHeight => 20f;
}
