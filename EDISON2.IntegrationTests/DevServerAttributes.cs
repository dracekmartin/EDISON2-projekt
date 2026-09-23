namespace EDISON2.IntegrationTests;

/// <summary>Fact proti dev serveru: bez serveru Skipped, s <c>EDISON_IT_REQUIRED=1</c> běží a selže v <c>Connect</c>.</summary>
public sealed class DevServerFactAttribute : FactAttribute
{
    public DevServerFactAttribute()
    {
        if (!DevServer.Required && !DevServer.IsReachable) Skip = DevServer.Unreachable;
    }
}

/// <summary>Theory proti dev serveru, pravidla jako <see cref="DevServerFactAttribute"/>.</summary>
public sealed class DevServerTheoryAttribute : TheoryAttribute
{
    public DevServerTheoryAttribute()
    {
        if (!DevServer.Required && !DevServer.IsReachable) Skip = DevServer.Unreachable;
    }
}
