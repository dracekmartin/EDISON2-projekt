namespace EDISON2.E2ETests;

/// <summary>Fact, který převezme plochu: běží jen s <c>EDISON_E2E=1</c>, jinak Skipped.</summary>
public sealed class E2EFactAttribute : FactAttribute
{
    public E2EFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("EDISON_E2E") != "1")
            Skip = "UI E2E test spouští shell na ploše - zapíná se EDISON_E2E=1.";
    }
}
