using System;
using System.IO;
using System.Text.Json.Nodes;
using EDControls.EDViewModel;
using EDControls.ErrorReporting;

namespace EDISONMAIN2.Windows.EdisonMainWindow;

/// <summary>
/// Kontrola generického hosta legacy modulů po startu shellu. Chybí-li, odejde jedno hlášení a jedna hláška,
/// kliknutí na modul pak hlášku jen zopakuje (dřív chybový dialog a hlášení za každé kliknutí).
/// </summary>
internal sealed record LegacyHostCheck(Func<string?> MissingMessage, Action<string> Report)
{
    internal static LegacyHostCheck For(WindowNavigator nav) => new(nav.MissingLegacyHostMessage, message =>
        ErrorReports.ReportEvent(ErrorReportType.Warning, "EDISONMAIN2API.exe", "Chybí host modulů", message, new JsonObject
        {
            ["shellDirectory"] = AppContext.BaseDirectory,
            // Bez EDISON.exe běží shell z neúplné kopie: EDLOADER by bez hosta nový shell nespustil.
            ["loaderPresent"] = File.Exists(Path.Combine(AppContext.BaseDirectory, "EDISON.exe")),
        }));
}
