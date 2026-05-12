namespace EDControls.EDViewModel;

public interface IWindowNavigator
{
    /// <summary>Otevře okno modeless v entry assembly.</summary>
    void Show(string windowTypeName, EDWindowInput? input = null);

    /// <summary>Otevře okno modeless v zadané assembly.</summary>
    void Show(string windowTypeName, string assemblyName, EDWindowInput? input = null);

    /// <summary>Otevře okno jako modální dialog v entry assembly.</summary>
    bool? ShowDialog(string windowTypeName, EDWindowInput? input = null);

    /// <summary>Otevře okno jako modální dialog v zadané assembly.</summary>
    bool? ShowDialog(string windowTypeName, string assemblyName, EDWindowInput? input = null);
}
