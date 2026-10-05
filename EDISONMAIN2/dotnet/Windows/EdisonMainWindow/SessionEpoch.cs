using System;
using EDControls;

namespace EDISONMAIN2.Windows.EdisonMainWindow
{
    /// <summary>
    /// Jediné místo, které ví, zda výsledek práce na pozadí ještě patří session, ve které začala.
    /// Dvě míry, protože obnova licence přestaví session-scoped stav, ale uživatele ani firmu nemění.
    /// </summary>
    internal sealed class SessionEpoch(Func<IMLRest> rest, Func<string> idfa)
    {
        private int _generation;

        /// <summary>
        /// Zneplatní rozběhnutou práci. Změna session začíná prvním dialogem, ne až přestavbou:
        /// dialogy uvnitř synchronního switche pumpují dispatcher a doběhlý výsledek by jinak
        /// přestavěl stav nad rozpracovanou firmou (MLRest už na cílové).
        /// </summary>
        internal void Invalidate() => _generation++;

        internal Stamp Capture() => new(_generation);

        /// <summary>Od zachycení se session-scoped stav nepřestavoval (licence, počty, změna session).</summary>
        internal bool IsCurrent(Stamp stamp) => stamp.Generation == _generation;

        internal Identity CaptureSession() => new(rest(), idfa());

        /// <summary>Tentýž uživatel (jiný = nová instance MLRest) i firma; obnova licence je nemění.</summary>
        internal bool IsSameSession(Identity session) => ReferenceEquals(rest(), session.Rest) && idfa() == session.Idfa;

        internal readonly record struct Stamp(int Generation);

        internal readonly record struct Identity(IMLRest Rest, string Idfa);
    }
}
