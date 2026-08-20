Include %occInclude

Class ALVA.EDISONMAIN2.Tests.TelemetryContractTest Extends ALVA.EDTesting.MLTestCase
{

/// Kontraktní test wire formátu ALVA.ISY.ISYCloud.APIdotNet.Telemetry.LogUsageBatch,
/// na kterém závisí EDISON2 usage telemetrie (EDUsageTelemetry.Flush v EDControls).
/// Vstup: ^||ML("IN",i) = "usage" del form del control del ts(yyyy-MM-dd HH:mm:ss);
/// úložiště: ^[dataSys]ALVA.ISY.ISYCloud.Client.Telemetry("usage",idfa,userid,form,control)
///         = $lb(count, firstHorolog, lastHorolog).
/// ISYCloud třídy patří do repa EDISON - když na serveru nejsou, test se přeskočí.

Parameter TESTIDFA = "TEST_EDISON2_TELEMETRY";

Method HasISYCloud() As %Boolean
{
	quit ##class(%Dictionary.CompiledClass).%ExistsId("ALVA.ISY.ISYCloud.APIdotNet.Telemetry")
}

Method KillTestData()
{
	set dataSys = ##class(ALVA.EDISON.Main).GetSystemDB()
	kill ^[dataSys]ALVA.ISY.ISYCloud.Client.Telemetry("usage", ..#TESTIDFA)
}

/// Dávka se agreguje na čítač per (idfa, userid, form, control) + první/poslední horolog.
Method TestLogUsageBatchAggregatesCounts()
{
	if '..HasISYCloud() { do $$$AssertTrue(1, "ISYCloud není na serveru - skip") quit }
	do ..KillTestData()

	do ..SetupSys(.sys, ..#TESTIDFA, "999")
	set ^||ML("IN",1) = "usage~EDISON2.TestWindow~OPEN~2026-07-16 08:00:00"
	set ^||ML("IN",2) = "usage~EDISON2.TestWindow~BtnSave~2026-07-16 08:05:00"
	set ^||ML("IN",3) = "usage~EDISON2.TestWindow~OPEN~2026-07-16 09:30:00"

	set ret = ##class(ALVA.ISY.ISYCloud.APIdotNet.Telemetry).LogUsageBatch(.sys)
	do $$$AssertEquals(+ret, 0, "LogUsageBatch nesmí vrátit chybu, got: "_ret)

	set dataSys = ##class(ALVA.EDISON.Main).GetSystemDB()
	set rec = $get(^[dataSys]ALVA.ISY.ISYCloud.Client.Telemetry("usage", ..#TESTIDFA, "999", "EDISON2.TestWindow", "OPEN"))
	do $$$AssertEquals($listget(rec,1), 2, "OPEN se má agregovat na count=2")
	do $$$AssertEquals($listget(rec,2), $zdth("2026-07-16 08:00:00",3), "první horolog = ts prvního záznamu")
	do $$$AssertEquals($listget(rec,3), $zdth("2026-07-16 09:30:00",3), "poslední horolog = ts posledního záznamu")

	set rec = $get(^[dataSys]ALVA.ISY.ISYCloud.Client.Telemetry("usage", ..#TESTIDFA, "999", "EDISON2.TestWindow", "BtnSave"))
	do $$$AssertEquals($listget(rec,1), 1, "BtnSave má count=1")

	do ..KillTestData()
}

/// Záznamy bez prefixu "usage" metoda ignoruje (a nesmí spadnout).
Method TestLogUsageBatchIgnoresNonUsageRecords()
{
	if '..HasISYCloud() { do $$$AssertTrue(1, "ISYCloud není na serveru - skip") quit }
	do ..KillTestData()

	do ..SetupSys(.sys, ..#TESTIDFA, "999")
	set ^||ML("IN",1) = "garbage~EDISON2.TestWindow~OPEN~2026-07-16 08:00:00"

	set ret = ##class(ALVA.ISY.ISYCloud.APIdotNet.Telemetry).LogUsageBatch(.sys)
	do $$$AssertEquals(+ret, 0, "Ne-usage záznam nesmí způsobit chybu, got: "_ret)

	set dataSys = ##class(ALVA.EDISON.Main).GetSystemDB()
	do $$$AssertEquals($data(^[dataSys]ALVA.ISY.ISYCloud.Client.Telemetry("usage", ..#TESTIDFA)), 0, "Ne-usage záznam se nesmí uložit")

	do ..KillTestData()
}

}
