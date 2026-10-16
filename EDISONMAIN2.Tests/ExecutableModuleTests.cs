using System.Reflection;
using EDISONMAIN2.Modules;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Uzel stromu je třída s referenční rovností a dekorace vznikají kopírovacím konstruktorem
/// místo record <c>with</c> - kopie musí nést všechno, co by nesl <c>with</c>.
/// </summary>
public class ExecutableModuleTests
{
    private static IEnumerable<PropertyInfo> WritableProperties =>
        typeof(ExecutableModule).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite);

    // Nová vlastnost bez řádku v kopírovacím konstruktoru by dekorací (filtr, Mark) tiše zmizela.
    [Fact]
    public void CopyConstructor_CopiesEveryWritableProperty()
    {
        var source = new ExecutableModule();
        foreach (var p in WritableProperties)
            p.SetValue(source, p.PropertyType == typeof(bool) ? !(bool)p.GetValue(source)! : p.Name + "-hodnota");

        var copy = new ExecutableModule(source);

        Assert.All(WritableProperties, p => Assert.Equal(p.GetValue(source), p.GetValue(copy)));
    }

    [Fact]
    public void CopyConstructor_DoesNotShareSubscribers()
    {
        var source = new ExecutableModule { Name = "A" };
        var notified = 0;
        source.PropertyChanged += (_, _) => notified++;

        var copy = new ExecutableModule(source) { DisplaySuffix = " - stub" };
        copy.IsFavorite = true;

        Assert.Equal(0, notified);
    }

    // Dva shodně vyplněné uzly (týž modul ve dvou stromech) nesmí splynout ve výběru ani v List.Remove.
    [Fact]
    public void IdenticalModules_AreDistinct()
    {
        var first = new ExecutableModule { Name = "Turnusy", ElementId = "100", ExecutablePath = "MHD2.exe" };
        var second = new ExecutableModule(first);
        var list = new List<ExecutableModule> { first, second };

        Assert.NotEqual(first, second);
        list.Remove(second);

        Assert.Same(first, Assert.Single(list));
    }
}
