using System.Text.Json.Serialization;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextElement),      "TextElement")]
[JsonDerivedType(typeof(RectangleElement), "RectangleElement")]
[JsonDerivedType(typeof(LineElement),      "LineElement")]
[JsonDerivedType(typeof(ImageElement),     "ImageElement")]
[JsonDerivedType(typeof(TableElement),     "TableElement")]
[JsonDerivedType(typeof(BarcodeElement),  "BarcodeElement")]
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

    /// <summary>
    /// Vrátí seznam deskriptorů vlastností pro panel vlastností.
    /// Panel generuje UI řádky automaticky — přidání nové vlastnosti = přidat deskriptor sem.
    /// </summary>
    public abstract IReadOnlyList<PropertyDescriptor> GetPropertyDescriptors();

    /// <summary>
    /// Helper — vrátí deskriptory společných vlastností pozice a ZIndex.
    /// Volej na konci GetPropertyDescriptors() pomocí spread operátoru: [.. GetPositionDescriptors()]
    /// </summary>
    protected IReadOnlyList<PropertyDescriptor> GetPositionDescriptors() =>
    [
        new FloatPropertyDescriptor { Label = "X",       Group = "Pozice", Get = () => X,       Set = v => X = v,      Min = 0,  Max = 9999, Unit = "mm" },
        new FloatPropertyDescriptor { Label = "Y",       Group = "Pozice", Get = () => Y,       Set = v => Y = v,      Min = 0,  Max = 9999, Unit = "mm" },
        new FloatPropertyDescriptor { Label = "Šířka",   Group = "Pozice", Get = () => Width,   Set = v => Width = v,  Min = 1,  Max = 9999, Unit = "mm" },
        new FloatPropertyDescriptor { Label = "Výška",   Group = "Pozice",
            Get = () => Height ?? EffectiveDesignerHeight,
            Set = v => Height = v,
            Min = 1, Max = 9999, Unit = "mm" },
        new FloatPropertyDescriptor { Label = "ZIndex",  Group = "Pozice", Get = () => ZIndex,  Set = v => ZIndex = (int)Math.Round(v), Min = 0, Max = 100, Format = "0" },
        new BoolPropertyDescriptor  { Label = "Zamčeno", Group = "Pozice", Get = () => IsLocked, Set = v => IsLocked = v },

        new BoolPropertyDescriptor
        {
            Label = "Tisknout", Group = "Podmínky",
            // null / prázdný výraz = tisknout; "false" = skrýt
            // Ostatní výrazy (budoucí V2 syntax) se považují za "tisknout"
            Get   = () => !string.Equals(VisibilityExpression, "false", StringComparison.OrdinalIgnoreCase),
            Set   = v  => VisibilityExpression = v ? null : "false"
        },
    ];

    // ---------------------------------------------------------------------------

    [JsonIgnore]
    public bool IsDynamic => !Height.HasValue;

    /// <summary>Výška pro zobrazení v designeru — Height nebo placeholder dynamické sekce.</summary>
    [JsonIgnore]
    public float EffectiveDesignerHeight => Height ?? DesignerPlaceholderHeight;

    protected virtual float DesignerPlaceholderHeight => 20f;
}
