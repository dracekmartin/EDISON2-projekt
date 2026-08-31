using TemplateDesigner.Models;

namespace TemplateDesigner.Commands.Implementations;

/// <summary>
/// Změna formátu stránky se zachováním obsahu - přeškáluje prvky na novou velikost a nastaví
/// stránku podle hodnot z dialogu.
/// </summary>
/// <remarks>
/// <para>Do 31. 8. tahle cesta šablonu <b>vymazala</b> a založila prázdné plátno; důvodem bylo, že
/// zachovávat prvky přes změnu rozměrů se chovalo nepředvídatelně (visely mimo stránku). Vyřešilo
/// se to ale nejtvrdším možným způsobem - uživatel stavěl šablonu znovu od nuly. Přepočet ten původní
/// problém řeší přímo: poměr se bere tak, aby se obsah vešel (<see cref="PageFormatChange"/>).</para>
/// <para><b>Nastavení prvků se nedotýká</b> - sloupce, styly, volby, podmínky i datové zdroje
/// zůstávají. Mění se jen geometrie, a to včetně velikosti písma, aby výsledek vypadal jako táž
/// sestava na jiném papíře, ne jako drobný text na velké stránce.</para>
/// <para><b>Rozměr a okraje se berou z dialogu</b>, ne přeškálované - co uživatel zadal, to platí.
/// Proto se zapisují až po přeškálování obsahu.</para>
/// <para>Zpětný krok vrací stránku z odloženého snímku (přesně) a obsah převráceným poměrem, takže
/// se po cestě zaokrouhluje na desetinu milimetru - stejně jako u ručního škálování.</para>
/// </remarks>
public class ChangePageFormatCommand : IDesignerCommand
{
    private readonly Template _template;
    private readonly IReadOnlyDictionary<string, string> _pageFields;
    private readonly float _scale;
    private readonly float _offsetXMm;
    private Dictionary<string, string>? _previousFields;

    public string Description => "Změna formátu stránky";

    public ChangePageFormatCommand(Template template,
        IReadOnlyDictionary<string, string> pageFields, float scale, float offsetXMm)
    {
        _template = template;
        _pageFields = pageFields;
        _scale = scale;
        _offsetXMm = offsetXMm;
    }

    public void Execute()
    {
        // Snímek se bere jen poprvé - opakované Execute (redo) musí vracet do TÉHOŽ stavu.
        _previousFields ??= Snapshot();

        if (_scale != 1f) TemplateScaling.ScaleContent(_template, _scale);
        if (_offsetXMm != 0f) TemplateScaling.OffsetElementsX(_template, _offsetXMm);
        ApplyFields(_pageFields);
    }

    public void Undo()
    {
        if (_offsetXMm != 0f) TemplateScaling.OffsetElementsX(_template, -_offsetXMm);
        if (_scale != 1f) TemplateScaling.ScaleContent(_template, 1f / _scale);
        if (_previousFields is not null) ApplyFields(_previousFields);
    }

    private Dictionary<string, string> Snapshot()
    {
        var snapshot = new Dictionary<string, string>();
        foreach (var key in _pageFields.Keys)
            if (ElementPropertyAccessor.Exists(_template, key))
                snapshot[key] = ElementPropertyAccessor.Get(_template, key);
        return snapshot;
    }

    private void ApplyFields(IReadOnlyDictionary<string, string> fields)
    {
        foreach (var (key, value) in fields)
            if (ElementPropertyAccessor.Exists(_template, key))
                ElementPropertyAccessor.Set(_template, key, value);
    }
}
