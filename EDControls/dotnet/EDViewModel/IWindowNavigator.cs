namespace EDControls.EDViewModel;

public interface IWindowNavigator
{
    /// <summary>Opens a window modlessly in the entry assembly.</summary>
    void Show(string windowTypeName, params object[] args);

    /// <summary>Opens a window modlessly in the specified assembly.</summary>
    void Show(string windowTypeName, string assemblyName, params object[] args);

    /// <summary>Opens a window as a modal dialog in the entry assembly.</summary>
    bool? ShowDialog(string windowTypeName, params object[] args);

    /// <summary>Opens a window as a modal dialog in the specified assembly.</summary>
    bool? ShowDialog(string windowTypeName, string assemblyName, params object[] args);
}
