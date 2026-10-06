namespace EDISONMAIN2.Tests;

/// <summary>
/// Testy běží paralelně; sériově jen třídy, které mění stav procesu (<c>EDServices.Initialize</c>, i přes
/// <see cref="EdisonMainViewModelHarness.CreateVM"/>, a statiky shellu). Členství hlídá
/// <see cref="SharedStaticStateGuardTests"/>.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class SharedStaticStateCollection
{
    public const string Name = "SharedStaticState";
}
