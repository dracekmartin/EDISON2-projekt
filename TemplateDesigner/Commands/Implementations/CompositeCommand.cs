namespace TemplateDesigner.Commands.Implementations;

/// <summary>Složený příkaz — agreguje více příkazů do jednoho undo/redo kroku.</summary>
public class CompositeCommand : IDesignerCommand
{
    private readonly IReadOnlyList<IDesignerCommand> _commands;

    public string Description { get; }

    public CompositeCommand(string description, IEnumerable<IDesignerCommand> commands)
    {
        Description = description;
        _commands   = [.. commands];
    }

    public void Execute() { foreach (var cmd in _commands)          cmd.Execute(); }
    public void Undo()    { foreach (var cmd in _commands.Reverse()) cmd.Undo(); }
}
