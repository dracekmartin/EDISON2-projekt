Include GENERAL

/// Usage telemetrie pro integrační test EDUsageTelemetry: přečte a smaže čítače, které zapsal
/// ALVA.ISY.ISYCloud.APIdotNet.Telemetry.LogUsageBatch. Pracuje jen pod testovací firmou.
Class ALVA.EDTesting.ITTelemetry Extends %RegisteredObject
{

Parameter TestIdFa = "TEST_EDISON2_IT";

/// IN["form"], IN["control"]. OUT["count"], OUT["first"], OUT["last"] (yyyy-MM-dd HH:mm:ss) čítače volajícího.
ClassMethod Read(ByRef sys As %String) As %Status
{
    $$$Init
    try {
        $$$FillParams
        d ..Guard(IdFa)
        s form = $g(^||ML("IN","form")), control = $g(^||ML("IN","control"))
        if (form = "") || (control = "") $$$ThrowStatus($$$ERROR($$$GeneralError, "form and control are required"))
        s rec = $g(^[..DataSys()]ALVA.ISY.ISYCloud.Client.Telemetry("usage",IdFa,userID,form,control))
        s ^||ML("OUT","count") = +$lg(rec,1)
        s ^||ML("OUT","first") = $s($lg(rec,2)="":"", 1:$zdt($lg(rec,2),3))
        s ^||ML("OUT","last") = $s($lg(rec,3)="":"", 1:$zdt($lg(rec,3),3))
    } catch (ex) { $$$CatchErrRest }
    ret ret
}

/// IN["form"]. Smaže čítače formuláře volajícího, jinak by je SendTelemetryToMLWeb odeslal s telemetrií serveru.
/// Jen jeden formulář: souběžný běh jiného testu pod stejnou firmou má vlastní.
ClassMethod Clean(ByRef sys As %String) As %Status
{
    $$$Init
    try {
        $$$FillParams
        d ..Guard(IdFa)
        s form = $g(^||ML("IN","form"))
        if form = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "form is required"))
        k ^[..DataSys()]ALVA.ISY.ISYCloud.Client.Telemetry("usage",IdFa,userID,form)
    } catch (ex) { $$$CatchErrRest }
    ret ret
}

ClassMethod DataSys() As %String [ Private ]
{
    quit ##class(ALVA.EDISON.Main).GetSystemDB()
}

/// Jiná firma = chyba, ať test nepřečte ani nesmaže telemetrii skutečné firmy.
ClassMethod Guard(IdFa As %String) [ Private ]
{
    if IdFa '= ..#TestIdFa $$$ThrowStatus($$$ERROR($$$GeneralError, "ITTelemetry pracuje jen pod firmou "_..#TestIdFa))
}

}
