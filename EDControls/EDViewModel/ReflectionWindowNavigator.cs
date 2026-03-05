using System.IO;
using System.Reflection;
using System.Windows;

namespace EDControls.EDViewModel;

public class ReflectionWindowNavigator(string assemblyDirectory) : IWindowNavigator
{
    public void Show(string typeName, params object[] args) =>
        Show(typeName, Assembly.GetEntryAssembly()!.GetName().Name!, args);

    public void Show(string typeName, string assemblyName, params object[] args)
    {
        var window = CreateWindow(typeName, assemblyName, args);
        window.Owner = Application.Current.MainWindow;
        window.Show();
    }

    public bool? ShowDialog(string typeName, params object[] args) =>
        ShowDialog(typeName, Assembly.GetEntryAssembly()!.GetName().Name!, args);

    public bool? ShowDialog(string typeName, string assemblyName, params object[] args)
    {
        var window = CreateWindow(typeName, assemblyName, args);
        window.Owner = Application.Current.MainWindow;
        return window.ShowDialog();
    }

    private Window CreateWindow(string typeName, string assemblyName, object[] args)
    {
        var type = FindTypeInAssembly(typeName, assemblyName)
            ?? throw new InvalidOperationException(
                $"Window type '{typeName}' not found in assembly '{assemblyName}'.");
        return (Window)Activator.CreateInstance(type, args)!;
    }

    private Type? FindTypeInAssembly(string typeName, string assemblyName)
    {
        var loaded = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == assemblyName ||
                                 Path.GetFileNameWithoutExtension(a.Location) == assemblyName);
        if (loaded != null)
            return loaded.GetTypes().FirstOrDefault(t => t.Name == typeName || t.FullName == typeName);

        var fileName = Path.HasExtension(assemblyName) ? assemblyName : assemblyName + ".dll";
        var fullPath = Path.Combine(assemblyDirectory, fileName);
        if (!File.Exists(fullPath))
            throw new InvalidOperationException(
                $"Assembly '{assemblyName}' not found in '{assemblyDirectory}'.");

        var asm = Assembly.LoadFrom(fullPath);
        return asm.GetTypes().FirstOrDefault(t => t.Name == typeName || t.FullName == typeName);
    }
}
