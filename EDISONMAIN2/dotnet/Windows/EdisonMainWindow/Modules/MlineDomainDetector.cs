using EDControls.ErrorReporting;

namespace EDISONMAIN2.Windows.EdisonMainWindow.Modules
{
    /// <summary>
    /// Gate pro mlineOnly uzly stromu (legacy <c>ALVA.CORE.Network.IsDomainMline</c>, EDCORE/clsCore.vb):
    /// interní M-line nástroje se mimo doménu M-line nezobrazují. Stanici pozná stejně jako hlášení chyb.
    /// </summary>
    internal static class MlineDomainDetector
    {
        /// <summary>Suffix názvu mlineOnly uzlů v doméně M-line (legacy frmMain.vb:1228).</summary>
        internal const string Suffix = " [### M-line ###]";

        public static bool IsDomainMline() => MlineEnvironment.IsInternal;
    }
}
