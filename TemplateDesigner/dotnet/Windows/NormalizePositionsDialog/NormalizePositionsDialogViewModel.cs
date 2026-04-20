using EDControls.EDViewModel;
using TemplateDesigner.Models;

namespace TemplateDesigner;

public record StepOption(string Label, float Value);

public class NormalizePositionsDialogViewModel : EDViewModel
{
    private readonly IReadOnlyList<TemplateElement> _elements;
    private int _selectedStepIndex = 1;

    public IReadOnlyList<StepOption> StepOptions { get; } =
    [
        new("0,1 mm", 0.1f),
        new("0,5 mm", 0.5f),
        new("1 mm",   1.0f),
        new("2 mm",   2.0f),
        new("5 mm",   5.0f),
    ];

    public int SelectedStepIndex
    {
        get => _selectedStepIndex;
        set { if (Set(ref _selectedStepIndex, value)) OnPropertyChanged(nameof(AffectedCount)); }
    }

    public float Step => StepOptions[_selectedStepIndex].Value;

    public int ElementCount => _elements.Count;

    public int AffectedCount => _elements.Count(WillChange);

    public NormalizePositionsDialogViewModel(IReadOnlyList<TemplateElement> elements)
    {
        _elements = elements;
    }

    private bool WillChange(TemplateElement el)
    {
        float s = Step;
        return Changed(el.X, s) || Changed(el.Y, s) || Changed(el.Width, s)
            || (el.Height is not null && Changed(el.Height.Value, s));
    }

    private static bool Changed(float v, float step) =>
        MathF.Abs(MmMath.Snap(v, step) - v) > 0.001f;
}
