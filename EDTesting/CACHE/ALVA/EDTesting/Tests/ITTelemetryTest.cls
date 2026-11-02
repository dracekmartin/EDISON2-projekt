Include %occInclude

Class ALVA.EDTesting.Tests.ITTelemetryTest Extends ALVA.EDTesting.MLTestCase
{

Parameter Form = "EDISON2.ITTelemetryTest";

/// Formulář, jehož čítač Clean nechá (souběžný .NET test pod stejnou firmou).
Parameter OtherForm = "EDISON2.ITTelemetryTest.Other";

Method DataSys() As %String
{
	quit ##class(ALVA.EDISON.Main).GetSystemDB()
}

/// Read vrátí čítač volajícího s časy ve formátu klienta, Clean smaže jen formulář z IN.
Method TestReadAndClean()
{
	s db = ..DataSys()
	try {
		s ^[db]ALVA.ISY.ISYCloud.Client.Telemetry("usage","TEST_EDISON2_IT","999",..#Form,"OPEN") = $lb(3, $zdth("2026-07-16 08:00:00",3), $zdth("2026-07-16 09:30:00",3))
		s ^[db]ALVA.ISY.ISYCloud.Client.Telemetry("usage","TEST_EDISON2_IT","999",..#OtherForm,"OPEN") = $lb(1)
		d ..SetupSys(.sys, "TEST_EDISON2_IT", "999")
		s ^||ML("IN","form") = ..#Form, ^||ML("IN","control") = "OPEN"
		d ##class(ALVA.EDTesting.ITTelemetry).Read(.sys)
		do $$$AssertEquals($g(^||ML("ERR")), "", "Read skončil chybou")
		do $$$AssertEquals($g(^||ML("OUT","count")), 3, "počet použití")
		do $$$AssertEquals($g(^||ML("OUT","first")), "2026-07-16 08:00:00", "první použití")
		do $$$AssertEquals($g(^||ML("OUT","last")), "2026-07-16 09:30:00", "poslední použití")

		d ##class(ALVA.EDTesting.ITTelemetry).Clean(.sys)
		do $$$AssertEquals($g(^||ML("ERR")), "", "Clean skončil chybou")
		do $$$AssertNotTrue($d(^[db]ALVA.ISY.ISYCloud.Client.Telemetry("usage","TEST_EDISON2_IT","999",..#Form)), "čítač formuláře po Clean zůstal")
		do $$$AssertTrue($d(^[db]ALVA.ISY.ISYCloud.Client.Telemetry("usage","TEST_EDISON2_IT","999",..#OtherForm)), "Clean smazal čítač jiného formuláře")
	} catch ex {
		s error = ex.DisplayString()
	}
	// Úklid i po výjimce: SendTelemetryToMLWeb by testovací firmu odeslal. Jen vlastní formuláře, ať neruší běžící .NET test.
	k ^[db]ALVA.ISY.ISYCloud.Client.Telemetry("usage","TEST_EDISON2_IT","999",..#Form)
	k ^[db]ALVA.ISY.ISYCloud.Client.Telemetry("usage","TEST_EDISON2_IT","999",..#OtherForm)
	do $$$AssertEquals($g(error), "", "výjimka v testu")
}

/// Pod jinou firmou Read i Clean skončí chybou a její telemetrii nechají.
Method TestRejectsOtherFirm()
{
	s db = ..DataSys(), other = "TEST_EDISON2_GUARD"
	s ^[db]ALVA.ISY.ISYCloud.Client.Telemetry("usage",other,"999",..#Form,"OPEN") = $lb(1)
	d ..SetupSys(.sys, other, "999")
	s ^||ML("IN","form") = ..#Form, ^||ML("IN","control") = "OPEN"

	d ##class(ALVA.EDTesting.ITTelemetry).Read(.sys)
	do $$$AssertNotEquals($g(^||ML("ERR")), "", "Read pod jinou firmou neskončil chybou")
	k ^||ML("ERR")
	d ##class(ALVA.EDTesting.ITTelemetry).Clean(.sys)
	do $$$AssertNotEquals($g(^||ML("ERR")), "", "Clean pod jinou firmou neskončil chybou")
	do $$$AssertTrue($d(^[db]ALVA.ISY.ISYCloud.Client.Telemetry("usage",other)), "Clean smazal telemetrii jiné firmy")
	k ^[db]ALVA.ISY.ISYCloud.Client.Telemetry("usage",other)
}

}
