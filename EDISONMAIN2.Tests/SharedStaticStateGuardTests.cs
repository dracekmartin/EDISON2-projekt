using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using EDControls;
using EDControls.EDViewModel;
using EDControls.ErrorReporting;
using EDISONMAIN2.Modules;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Třída, která (i přes helper) mění stav procesu, musí být v <see cref="SharedStaticStateCollection"/> - při
/// paralelním běhu by náhodně shazovala jiné testy. Assembly dnes běží sekvenčně (xunit.runner.json), guard drží
/// izolaci pro případ, že se paralelní běh vrátí.
/// Hledá volání mutátorů v IL testové assembly včetně lambd, async, iterátorů, helperů a fixture.
/// </summary>
public class SharedStaticStateGuardTests
{
    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                                          | BindingFlags.Static | BindingFlags.DeclaredOnly;

    // Metody, které mění stav sdílený celým procesem testů. Nová settable statika = přidat sem.
    private static readonly MethodBase[] Mutators =
    [
        typeof(EDServices).GetMethod(nameof(EDServices.Initialize), Declared)!,
        typeof(EDServices).GetMethod(nameof(EDServices.Reset), Declared)!,
        Setter(typeof(InternalDiagnostics), nameof(InternalDiagnostics.Enabled)),
        Setter(typeof(LocalPrefs), nameof(LocalPrefs.FilePath)),
        Setter(typeof(ErrorReports), nameof(ErrorReports.Enabled)),
        Setter(typeof(EDUsageTelemetry), nameof(EDUsageTelemetry.Enabled)),
        typeof(StartupOptions).GetMethod(nameof(StartupOptions.ApplyToProcess), Declared)!,
        typeof(EDAppearance).GetMethod(nameof(EDAppearance.UseCommonFolder), Declared)!,
        typeof(MlineCommonPaths).GetMethod(nameof(MlineCommonPaths.Restore), Declared)!,
        // Skutečný krok bootstrapperu (testy ho jinak přepisují): Initialize služeb.
        typeof(AppBootstrapper).GetMethod("InitializeServices", Declared)!,
        .. typeof(Environment).GetMethods().Where(m => m.Name == nameof(Environment.SetEnvironmentVariable)),
    ];

    private static MethodBase Setter(Type type, string property) =>
        type.GetProperty(property, Declared)!.SetMethod!;

    [Fact]
    public void EveryTestClassMutatingProcessState_IsInSharedStaticStateCollection()
    {
        // Jiná kolekce nestačí: běžela by paralelně se SharedStaticState a o statiku by se prala dál.
        var offenders = MutatingTestClasses()
            .Where(t => CollectionName(t) != SharedStaticStateCollection.Name)
            .Select(t => t.Name)
            .ToList();

        Assert.True(offenders.Count == 0,
            $"Třídy mění stav procesu bez [Collection(SharedStaticStateCollection.Name)]: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void Guard_FindsDirectAndIndirectMutators_NotPureTests()
    {
        // Pojistka hlídače: kdyby čtení IL přestalo fungovat, předchozí test by tiše prošel.
        var mutating = MutatingTestClasses().ToHashSet();

        Assert.Contains(typeof(LocalPrefsTests), mutating);            // přímo setter
        Assert.Contains(typeof(StartupOptionsTests), mutating);        // přes ApplyToProcess
        Assert.DoesNotContain(typeof(VerzeInfTests), mutating);
        Assert.DoesNotContain(typeof(SharedStaticStateGuardTests), mutating);
        // Cesty bez přímého tokenu v IL volající třídy.
        Assert.True(IsMutating(typeof(GuardSampleAsyncUser)));
        Assert.True(IsMutating(typeof(GuardSampleIteratorUser)));
        Assert.True(IsMutating(typeof(GuardSampleFixtureUser)));
    }

    private static readonly Lazy<HashSet<(Guid, int)>> Mutating =
        new(() => MutatingMethods(typeof(SharedStaticStateGuardTests).Assembly));

    private static IEnumerable<Type> MutatingTestClasses() =>
        typeof(SharedStaticStateGuardTests).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.DeclaringType is null && IsTestClass(t))
            .Where(IsMutating);

    private static bool IsMutating(Type type) =>
        MethodsWithNested(type).Concat(FixtureTypes(type).SelectMany(MethodsWithNested))
            .Any(m => Mutating.Value.Contains(Key(m)));

    // Fixture vytváří xUnit reflexí, v IL testové třídy na její ctor ani Dispose žádné volání nevede.
    private static IEnumerable<Type> FixtureTypes(Type type)
    {
        var name = CollectionName(type);
        var definitions = name is null ? [] : type.Assembly.GetTypes()
            .Where(d => d.GetCustomAttributesData().Any(a => a.AttributeType == typeof(CollectionDefinitionAttribute)
                && a.ConstructorArguments.FirstOrDefault().Value as string == name));
        return definitions.Prepend(type)
            .SelectMany(t => t.GetInterfaces())
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() is var d
                        && (d == typeof(IClassFixture<>) || d == typeof(ICollectionFixture<>)))
            .Select(i => i.GetGenericArguments()[0]);
    }

    private static string? CollectionName(Type type) => type.GetCustomAttributesData()
        .FirstOrDefault(a => a.AttributeType == typeof(CollectionAttribute))?
        .ConstructorArguments.FirstOrDefault().Value as string;

    private static bool IsTestClass(Type type) =>
        type.GetMethods().Any(m => m.GetCustomAttributes<FactAttribute>(inherit: true).Any());

    /// <summary>Mutátory a transitivně každá metoda testové assembly, která je volá.</summary>
    private static HashSet<(Guid, int)> MutatingMethods(Assembly assembly)
    {
        var calls = assembly.GetTypes()
            .SelectMany(t => t.GetMethods(Declared).Cast<MethodBase>().Concat(t.GetConstructors(Declared)))
            .ToDictionary(Key, m => CalledMethods(m).Select(Key).ToHashSet());
        var mutating = Mutators.Select(Key).ToHashSet();
        bool changed;
        do
        {
            changed = false;
            foreach (var (method, callees) in calls)
                if (!mutating.Contains(method) && callees.Overlaps(mutating))
                    changed = mutating.Add(method);
        } while (changed);
        return mutating;
    }

    // Lambdy a async stavové automaty kompilátor dává do vnořených typů; počítají se jako kód třídy.
    private static IEnumerable<MethodBase> MethodsWithNested(Type type)
    {
        for (var t = type; t != null && t.Assembly == type.Assembly; t = t.BaseType)
        {
            foreach (var m in t.GetMethods(Declared).Cast<MethodBase>().Concat(t.GetConstructors(Declared)))
                yield return m;
            foreach (var nested in t.GetNestedTypes(Declared))
                foreach (var m in MethodsWithNested(nested))
                    yield return m;
        }
    }

    // Definice metody bez ohledu na generickou instanci.
    private static (Guid, int) Key(MethodBase method) => (method.Module.ModuleVersionId, method.MetadataToken);

    private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(f => (OpCode)f.GetValue(null)!)
        .ToDictionary(op => op.Value);

    private static List<MethodBase> CalledMethods(MethodBase method)
    {
        var result = new List<MethodBase>();
        var il = method.GetMethodBody()?.GetILAsByteArray();
        if (il is null) return result;
        var typeArgs = method.DeclaringType is { IsGenericType: true } owner ? owner.GetGenericArguments() : null;
        var methodArgs = method is MethodInfo { IsGenericMethod: true } info ? info.GetGenericArguments() : null;

        for (int i = 0; i < il.Length;)
        {
            short value = il[i] == 0xFE ? (short)(0xFE00 | il[i + 1]) : il[i];
            i += il[i] == 0xFE ? 2 : 1;
            var op = OpCodesByValue[value];
            if (op.OperandType == OperandType.InlineMethod)
            {
                try { result.Add(method.Module.ResolveMethod(BitConverter.ToInt32(il, i), typeArgs, methodArgs)!); }
                // Token, který reflexe nerozřeší (vararg, nedostupná assembly), mutátor stejně není.
                catch (Exception e) when (e is ArgumentException or TypeLoadException or MissingMemberException
                                              or FileNotFoundException or BadImageFormatException) { }
            }
            i += OperandSize(op.OperandType, il, i);
        }
        // Async a iterátor: tělo běží v MoveNext stavového automatu, volající sahá jen na jeho ctor/Start.
        if (method.GetCustomAttribute<System.Runtime.CompilerServices.StateMachineAttribute>() is { } machine
            && machine.StateMachineType.GetMethod("MoveNext", Declared) is { } moveNext)
            result.Add(moveNext);
        return result;
    }

    private static int OperandSize(OperandType type, byte[] il, int at) => type switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, at),
        _ => 4,
    };
}

// Vzorky pro pojistku hlídače: nejsou to testy a nikdy se nespouští, hlídač čte jen jejich IL.
internal static class GuardSampleHelpers
{
    internal static async Task ResetAsync()
    {
        await Task.Yield();
        EDServices.Reset();
    }

    internal static IEnumerable<int> ResetLazily()
    {
        EDServices.Reset();
        yield return 0;
    }
}

internal sealed class GuardSampleAsyncUser
{
    internal Task Use() => GuardSampleHelpers.ResetAsync();
}

internal sealed class GuardSampleIteratorUser
{
    internal int Use() => GuardSampleHelpers.ResetLazily().Count();
}

internal sealed class GuardSampleFixture
{
    public GuardSampleFixture() => EDServices.Reset();
}

internal sealed class GuardSampleFixtureUser : IClassFixture<GuardSampleFixture>;
