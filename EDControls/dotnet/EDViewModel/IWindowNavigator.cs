namespace EDControls.EDViewModel;

public interface IWindowNavigator
{
    /// <summary>Opens a window modlessly in the entry assembly.</summary>
    void Show(string windowTypeName, EDWindowInput? input = null);

    /// <summary>Opens a window modlessly in the specified assembly.</summary>
    void Show(string windowTypeName, string assemblyName, EDWindowInput? input = null);

    /// <summary>Opens a window as a modal dialog in the entry assembly.</summary>
    bool? ShowDialog(string windowTypeName, EDWindowInput? input = null);

    /// <summary>Opens a window as a modal dialog in the specified assembly.</summary>
    bool? ShowDialog(string windowTypeName, string assemblyName, EDWindowInput? input = null);
}
