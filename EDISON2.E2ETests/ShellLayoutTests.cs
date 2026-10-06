using Xunit.Abstractions;

namespace EDISON2.E2ETests;

/// <summary>Lišta menu v minimální velikosti okna a výchozím písmu: přepínač firem se stropem šířky
/// nesmí menu zalomit (firmy podle serveru, se kterým test běží).</summary>
[Trait("Category", "E2E")]
public class ShellLayoutTests(ITestOutputHelper output)
{
    [E2EFact]
    public void MinimumWindowSize_MenuBarStaysOnOneRow()
    {
        using var shell = ShellSession.Start();

        shell.ShrinkToMinimumSize();

        var items = shell.MenuBarItems();
        foreach (var (name, bounds) in items) output.WriteLine($"{name}: {bounds}");
        // Zalomené menu (WrapPanel) posune položky druhého řádku níž.
        Assert.All(items, item => Assert.Equal(items[0].Bounds.Top, item.Bounds.Top));

        shell.ExitViaMenu();

        Assert.Equal(0, shell.ExitCode);
    }
}
