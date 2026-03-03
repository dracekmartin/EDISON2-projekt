namespace TemplateDesigner.Commands.Implementations;

/// <summary>
/// Undoable změna jedné vlastnosti elementu.
/// Generický — funguje pro string, float, bool, barvu i enum.
/// </summary>
public class SetPropertyCommand : IDesignerCommand
{
    private readonly Action<object?> _apply;
    private readonly object?         _oldValue;
    private readonly object?         _newValue;

    public string Description { get; }

    public SetPropertyCommand(string label, object? oldValue, object? newValue, Action<object?> apply)
    {
        Description = $"Změna: {label}";
        _oldValue   = oldValue;
        _newValue   = newValue;
        _apply      = apply;
    }

    public void Execute() => _apply(_newValue);
    public void Undo()    => _apply(_oldValue);
}
