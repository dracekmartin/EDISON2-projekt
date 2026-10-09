using System.Reflection;
using ALWA.SYS;

namespace EDControls.Tests;

/// <summary>
/// <see cref="MLRestAdapter.With"/> kopíruje identitu ručně - na novou property MLRest se snadno
/// zapomene a hodnota se tiše ztratí v klonech (poll, logout jiné firmy). Reflection test hlídá,
/// že každá public settable property je po With() i po snímku session shodná, nebo je vyjmenovaná níž.
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

    [Theory]
    [InlineData(nameof(MLRestAdapter.With))]
    [InlineData(nameof(MLRestAdapter.SnapshotSession))]
    public void SameSessionClone_CopiesEveryPublicSettableProperty(string via)
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

        var clone = SameSessionClone(new MLRestAdapter(source), via);

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

    /// <summary>Klon téže session (RestCallRunner) dostane od serveru zaniklý zámek nebo jinou verzi - ukončení
    /// aplikace (shell, TD) ale odebírá jen sdílená instance. Bez přeposlání by se aplikace neukončila.</summary>
    [Theory]
    [InlineData("OnEdisonActivityFailed", "ACTIVITY: Lock exist failed!")]
    [InlineData("OnEdisonActivityFailed", "ACTIVITY: Lock set failed!")]
    [InlineData("OnEdisonVersionFailed", "EdisonVersionFailed!")]
    public void With_SameSessionClone_ForwardsSessionEndToSharedInstance(string raise, string serverError)
    {
        var source = new MLRest("srv", 1234, "NS", "IDFA") { CacheLocks = 1 };
        var (activity, version) = Subscribe(source);

        var clone = ((MLRestAdapter)new MLRestAdapter(source).With()).Inner;
        RaiseAsServerError(clone, raise, serverError);

        Assert.Equal(raise == "OnEdisonActivityFailed" ? 1 : 0, activity());
        Assert.Equal(raise == "OnEdisonVersionFailed" ? 1 : 0, version());
    }

    /// <summary>Snímek runneru je klon téže session: konec session přepošle stejně jako With().</summary>
    [Fact]
    public void SnapshotSession_ForwardsSessionEndToSharedInstance()
    {
        var source = new MLRest("srv", 1234, "NS", "IDFA") { CacheLocks = 1 };
        var (activity, _) = Subscribe(source);

        var clone = SameSessionClone(new MLRestAdapter(source), nameof(MLRestAdapter.SnapshotSession));
        RaiseAsServerError(clone, "OnEdisonActivityFailed", "ACTIVITY: Lock exist failed!");

        Assert.Equal(1, activity());
    }

    /// <summary>Snímek pořízený před přepnutím firmy a postavený po něm nese starou firmu, zámek staré firmy
    /// jsme zrušili sami a ukončení aplikace se spustit nesmí.</summary>
    [Fact]
    public void SnapshotSession_TakenBeforeCompanySwitch_KeepsOldCompanyAndDoesNotForward()
    {
        var source = new MLRest("srv", 1234, "NS", "IDFA") { CacheLocks = 1 };
        var (activity, _) = Subscribe(source);
        var snapshot = new MLRestAdapter(source).SnapshotSession();

        source.CacheLocks = 0;
        source.Idfa = "OTHER";
        source.DataNamespace = "NS2";
        var clone = ((MLRestAdapter)snapshot()).Inner;
        RaiseAsServerError(clone, "OnEdisonActivityFailed", "ACTIVITY: Lock exist failed!");

        Assert.Equal(("IDFA", "NS", 1), (clone.Idfa, clone.DataNamespace, clone.CacheLocks));
        Assert.Equal(0, activity());
    }

    /// <summary>Klon klonu přepošle až na sdílenou instanci (řetěz přes mezilehlé přeposílání).</summary>
    [Fact]
    public void With_CloneOfClone_ForwardsToSharedInstance()
    {
        var source = new MLRest("srv", 1234, "NS", "IDFA") { CacheLocks = 1 };
        var (activity, _) = Subscribe(source);

        var nested = ((MLRestAdapter)new MLRestAdapter(source).With().With()).Inner;
        RaiseAsServerError(nested, "OnEdisonActivityFailed", "ACTIVITY: Lock exist failed!");

        Assert.Equal(1, activity());
    }

    /// <summary>Dávka na klonu přežije přepnutí firmy: zámek staré firmy jsme zrušili sami, ukončení by shodilo
    /// shell hned po úspěšném přepnutí. Během switche má sdílená instance CacheLocks=0, po něm jinou firmu.</summary>
    [Theory]
    [InlineData(0, "IDFA", "NS")]
    [InlineData(1, "OTHER", "NS2")]
    public void With_SharedInstanceSwitchedSinceClone_DoesNotForward(int sharedLocks, string sharedIdfa, string sharedNamespace)
    {
        var source = new MLRest("srv", 1234, "NS", "IDFA") { CacheLocks = 1 };
        var (activity, _) = Subscribe(source);
        var clone = ((MLRestAdapter)new MLRestAdapter(source).With()).Inner;

        source.CacheLocks = sharedLocks;
        source.Idfa = sharedIdfa;
        source.DataNamespace = sharedNamespace;
        RaiseAsServerError(clone, "OnEdisonActivityFailed", "ACTIVITY: Lock exist failed!");

        Assert.Equal(0, activity());
    }

    /// <summary>Souběžné klony (SessionDataLoader pouští 9 najednou) by jinak vrstvily dialog „aplikace bude ukončena".</summary>
    [Fact]
    public void With_SeveralClonesFail_ForwardsOnce()
    {
        var source = new MLRest("srv", 1234, "NS", "IDFA") { CacheLocks = 1 };
        var (activity, version) = Subscribe(source);
        var adapter = new MLRestAdapter(source);

        RaiseAsServerError(((MLRestAdapter)adapter.With()).Inner, "OnEdisonActivityFailed", "ACTIVITY: Lock exist failed!");
        RaiseAsServerError(((MLRestAdapter)adapter.With()).Inner, "OnEdisonActivityFailed", "ACTIVITY: Lock exist failed!");
        RaiseAsServerError(((MLRestAdapter)adapter.With()).Inner, "OnEdisonVersionFailed", "EdisonVersionFailed!");

        Assert.Equal(1, activity() + version());
    }

    /// <summary>Klony s přepsanými parametry (poll s CacheLocks=2, logout cizí firmy) ukončení spouštět nemají.</summary>
    [Theory]
    [InlineData(0, null)]
    [InlineData(2, null)]
    [InlineData(null, "OTHER")]
    public void With_OverriddenClone_DoesNotForward(int? cacheLocks, string? idfa)
    {
        var source = new MLRest("srv", 1234, "NS", "IDFA") { CacheLocks = 1 };
        var (activity, version) = Subscribe(source);

        var clone = ((MLRestAdapter)new MLRestAdapter(source).With(cacheLocks: cacheLocks, idfa: idfa)).Inner;
        RaiseAsServerError(clone, "OnEdisonActivityFailed", "ACTIVITY: Lock exist failed!");
        RaiseAsServerError(clone, "OnEdisonVersionFailed", "EdisonVersionFailed!");

        Assert.Equal(0, activity());
        Assert.Equal(0, version());
    }

    private static MLRest SameSessionClone(MLRestAdapter adapter, string via) =>
        ((MLRestAdapter)(via == nameof(MLRestAdapter.With) ? adapter.With() : adapter.SnapshotSession()())).Inner;

    private static (Func<int> Activity, Func<int> Version) Subscribe(MLRest source)
    {
        int activity = 0, version = 0;
        source.EdisonActivityFailed += (sender, _) => { Assert.Same(source, sender); activity++; };
        source.EdisonVersionFailed += (sender, _) => { Assert.Same(source, sender); version++; };
        return (() => activity, () => version);
    }

    // Stejná cesta jako MLRest při chybě requestu (catch volá OnEdison*Failed(err)), jen bez sítě.
    private static void RaiseAsServerError(MLRest instance, string method, string serverError) =>
        typeof(MLRest).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(instance, [serverError]);
}
