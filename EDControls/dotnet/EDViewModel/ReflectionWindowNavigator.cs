using System.IO;
using System.Reflection;
using System.Windows;

namespace EDControls.EDViewModel;

public class ReflectionWindowNavigator : IWindowNavigator
{
    public void Show(string typeName, EDWindowInput? input = null) =>
        Show(typeName, Assembly.GetEntryAssembly()!.GetName().Name!, input);

    public void Show(string typeName, string assemblyName, EDWindowInput? input = null)
    {
        var window = CreateWindow(typeName, assemblyName, input);
        window.Owner = Application.Current.MainWindow;
        window.Show();
    }

    public bool? ShowDialog(string typeName, EDWindowInput? input = null) =>
        ShowDialog(typeName, Assembly.GetEntryAssembly()!.GetName().Name!, input);

    public bool? ShowDialog(string typeName, string assemblyName, EDWindowInput? input = null)
    {
        var window = CreateWindow(typeName, assemblyName, input);
        window.Owner = Application.Current.MainWindow;
        return window.ShowDialog();
    }

    private static Window CreateWindow(string typeName, string assemblyName, EDWindowInput? input)
    {
        var type = FindTypeInAssembly(typeName, assemblyName)
            ?? throw new InvalidOperationException(
                $"Window type '{typeName}' not found in assembly '{assemblyName}'.");
        var window = input is null
            ? (Window)Activator.CreateInstance(type)!
            : (Window)Activator.CreateInstance(type, input)!;

        var idfa = EDServices.Current.MLRest?.Idfa;
        if (!string.IsNullOrEmpty(idfa))
            window.Title = $"{window.Title} [{idfa}]";

        return window;
    }

    private static Type? FindTypeInAssembly(string typeName, string assemblyName)
    {
        var loaded = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == assemblyName ||
                                 Path.GetFileNameWithoutExtension(a.Location) == assemblyName);
        if (loaded != null)
            return loaded.GetTypes().FirstOrDefault(t => t.Name == typeName || t.FullName == typeName);

        var fileName = Path.HasExtension(assemblyName) ? assemblyName : assemblyName + ".dll";
        var fullPath = Path.Combine(AppContext.BaseDirectory, fileName);
        if (!File.Exists(fullPath))
            throw new InvalidOperationException(
                $"Assembly '{assemblyName}' not found in '{AppContext.BaseDirectory}'.");

        var asm = Assembly.LoadFrom(fullPath);
        return asm.GetTypes().FirstOrDefault(t => t.Name == typeName || t.FullName == typeName);
    }
}
