using EDControls;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Testovací <see cref="IRestCallRunner"/>: volá synchronně na předané instanci, bez klonu
/// a bez <c>Task.Run</c>. Produkční runner pouští větve souběžně, což NSubstitute (není
/// thread-safe) dělá flaky; orchestrace loaderu je jinak stejná - <c>WhenAll</c> nad
/// hotovými tasky. Výjimku vrací ve tasku, ne synchronně, aby chování odpovídalo produkci.
/// </summary>
internal sealed class InlineRestCallRunner(IMLRest rest) : IRestCallRunner
{
    public Task<T> RunAsync<T>(Func<IMLRest, T> call)
    {
        try { return Task.FromResult(call(rest)); }
        catch (Exception ex) { return Task.FromException<T>(ex); }
    }
}
