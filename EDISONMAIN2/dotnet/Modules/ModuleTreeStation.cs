using System;

namespace EDISONMAIN2.Modules
{
    /// <summary>
    /// Fakta stanice, která strom modulů míchá do dat: doména M-line (gate <c>mlineOnly</c>) a dekorace
    /// launchovatelnosti (<c>File.Exists</c> exe, suffixy pro diagnostiku). Injekce kvůli testům - ve
    /// statice by výsledek závisel na stroji, kde test běží.
    /// </summary>
    /// <param name="IsMlineDomain">Stanice v doméně M-line (vidí moduly <c>mlineOnly</c>).</param>
    /// <param name="Decorate">Úprava modulu po filtraci, produkčně <see cref="ModuleLaunchabilityCheck.Mark"/>.</param>
    internal sealed record ModuleTreeStation(bool IsMlineDomain, Func<IModule, IModule> Decorate)
    {
        /// <summary>Per stanice konstantní (DNS doména / registry override) - stačí vyhodnotit jednou.</summary>
        internal static readonly bool IsMlineDomainStation = MlineDomainDetector.IsDomainMline();

        internal static ModuleTreeStation Production { get; } =
            new(IsMlineDomainStation, ModuleLaunchabilityCheck.Mark);
    }
}
