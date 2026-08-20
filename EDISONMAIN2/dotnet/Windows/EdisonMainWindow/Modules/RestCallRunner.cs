using System;
using System.Threading.Tasks;
using EDControls;

namespace EDISONMAIN2.Windows.EdisonMainWindow.Modules
{
    /// <summary>Spustí blokující MLRest volání mimo UI thread. Implementace určuje, zda smí běžet souběžně.</summary>
    public interface IRestCallRunner
    {
        Task<T> RunAsync<T>(Func<IMLRest, T> call);
    }

    /// <summary>
    /// Produkční runner: <c>Task.Run</c> nad **klonem** sdílené instance. Klon je nutný, ne kosmetika -
    /// <c>MLRestAdapter.QueryOrThrow</c> čte <c>RestStatus</c> až po <c>Query</c>, takže dvě souběžná
    /// volání na téže instanci si stavy prohodí (spolknutá chyba / cizí výjimka). Klon má stejný GUID,
    /// pro server je to tatáž session; ověřeno, že každý souběžný request obsluhuje jiný Caché proces,
    /// takže si nešlapou ani po process-private <c>^||ML</c>.
    /// </summary>
    public sealed class RestCallRunner(Func<IMLRest> rest) : IRestCallRunner
    {
        public Task<T> RunAsync<T>(Func<IMLRest, T> call)
        {
            // Snapshot instance na volajícím (UI) threadu - celá dávka pak jede proti téže session
            // i kdyby mezitím proběhl switch firmy nebo uživatele.
            var current = rest();
            return Task.Run(() => call(current.With()));
        }
    }
}
