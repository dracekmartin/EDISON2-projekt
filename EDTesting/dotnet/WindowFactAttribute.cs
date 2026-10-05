using Xunit;

namespace EDTesting;

/// <summary>
/// Fact pro test, který zobrazí skutečné okno (<c>Show()</c>): při vývoji by vyskakovalo a kradlo
/// fokus, proto běží jen s <c>EDISON_WINDOW_TESTS=1</c> - nastavuje ji pre-push hook.
/// </summary>
public sealed class WindowFactAttribute : FactAttribute
{
    public WindowFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("EDISON_WINDOW_TESTS") != "1")
            Skip = "Test zobrazuje skutečné okno - běží v pre-push, ručně s EDISON_WINDOW_TESTS=1.";
    }
}
