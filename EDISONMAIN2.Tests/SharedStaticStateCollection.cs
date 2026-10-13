namespace EDISONMAIN2.Tests;

/// <summary>
/// Třídy, které mění stav procesu (<c>EDServices.Initialize</c>, i přes helper, a statiky shellu). Assembly dnes
/// běží celá sekvenčně (xunit.runner.json), kolekce drží izolaci pro případ, že se paralelní běh vrátí. Členství
/// hlídá <see cref="SharedStaticStateGuardTests"/>.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class SharedStaticStateCollection
{
    public const string Name = "SharedStaticState";
}
