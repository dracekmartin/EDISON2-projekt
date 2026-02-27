namespace TemplateDesigner.Commands;

public interface IDesignerCommand
{
    /// <summary>Popis akce zobrazovaný v historii (Ctrl+Z).</summary>
    string Description { get; }

    void Execute();
    void Undo();
}
