using System.Reflection;
using ALWA.SYS;

namespace EDControls.Tests;

/// <summary>
/// <see cref="MLRestAdapter.With"/> kopíruje identitu ručně - na novou property MLRest se snadno
/// zapomene a hodnota se tiše ztratí v klonech (poll, logout jiné firmy). Reflection test hlídá,
/// že každá public settable property je po With() shodná, nebo je vyjmenovaná níž.
/// </summary>
public class MLRestAdapterWithTests
{
    /// <summary>Properties, které With() záměrně NEkopíruje.</summary>
    private static readonly HashSet<string> Excluded =
    [
        "ClientIP",      // dopočítává se v konstruktoru MLRest
        "SysNamespace",  // dopočítává se v konstruktoru MLRest
        "RestStatus",    // stav posledního requestu - klon začíná čistý
        "UserStatus",    // stav posledního requestu (MLRStatus) - klon začíná čistý
    ];

    [Fact]
    public void With_CopiesEveryPublicSettableProperty()
    {
        var source = new MLRest("srv", 1234, "NS", "IDFA");

        var candidates = typeof(MLRest)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.SetMethod!.IsPublic && !Excluded.Contains(p.Name))
            .ToList();

        // Nastav každé property unikátní značku - nepodporovaný typ musí test rozbít,
        // aby se sem doplnil (jinak by property tiše vypadla z pokrytí).
        var covered = new List<PropertyInfo>();
        int i = 0;
        foreach (var p in candidates)
        {
            object value = p.PropertyType switch
            {
                var t when t == typeof(string) => $"val{i}",
                var t when t == typeof(int)    => 1000 + i,
                var t when t == typeof(char)   => (char)('A' + (i % 26)),
                var t when t == typeof(bool)   => true,
                var t when t == typeof(KeyValuePair<string, string>) =>
                    new KeyValuePair<string, string>($"key{i}", $"val{i}"),
                _ => throw new Xunit.Sdk.XunitException(
                    $"MLRest.{p.Name} má nepodporovaný typ {p.PropertyType.Name} - doplň značku do testu."),
            };
            p.SetValue(source, value);
            covered.Add(p);
            i++;
        }
        Assert.NotEmpty(covered);

        var clone = ((MLRestAdapter)new MLRestAdapter(source).With()).Inner;

        var missed = covered
            .Where(p => !Equals(p.GetValue(source), p.GetValue(clone)))
            .Select(p => p.Name)
            .ToList();
        Assert.True(missed.Count == 0,
            $"MLRestAdapter.With() nekopíruje: {string.Join(", ", missed)} - doplň do initializeru klonu.");
    }

    [Fact]
    public void With_AppliesRequestedOverrides_AndKeepsRest()
    {
        var source = new MLRest("srv", 1234, "NS", "IDFA") { CacheLocks = 1, GUID = "g-1" };
        var adapter = new MLRestAdapter(source);

        var clone = ((MLRestAdapter)adapter.With(cacheLocks: 2, idfa: "OTHER", dataNamespace: "NS2")).Inner;

        Assert.Equal(2, clone.CacheLocks);
        Assert.Equal("OTHER", clone.Idfa);
        Assert.Equal("NS2", clone.DataNamespace);
        Assert.Equal("srv", clone.Server);
        Assert.Equal(1234, clone.Port);
        Assert.Equal("g-1", clone.GUID);
        Assert.Equal(1, source.CacheLocks);
        Assert.Equal("IDFA", source.Idfa);
    }
}
