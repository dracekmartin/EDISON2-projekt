Include %occInclude

Class ALVA.EDTesting.Tests.ITSessionTest Extends ALVA.EDTesting.MLTestCase
{

Parameter TestGuid = "test-edison2-itsession";

/// GUID jiné session, jejíž požadavek na ukončení musí úklid přežít.
Parameter OtherGuid = "test-edison2-itsession-other";

Method Sys(ByRef sys, idfa As %String = "TEST_EDISON2_IT", guid As %String = {..#TestGuid})
{
	k sys
	d ..SetupSys(.sys, idfa, "999")
	s sys("C","GUID") = guid
}

Method Call(method As %String, ByRef sys) As %String
{
	k ^||ML("ERR"), ^||ML("OUT")
	d $classmethod("ALVA.EDTesting.ITSession", method, .sys)
	quit $g(^||ML("ERR"))
}

Method KillEndRequests()
{
	s cas = ""
	for {
		s cas = $o(^CacheTempEDISON("System","UkoncitEdisona",cas)) q:cas=""
		k ^CacheTempEDISON("System","UkoncitEdisona",cas,..#TestGuid)
		k ^CacheTempEDISON("System","UkoncitEdisona",cas,..#OtherGuid)
	}
}

Method EndRequested(guid As %String) As %Boolean
{
	s cas = "", found = 0
	for {
		s cas = $o(^CacheTempEDISON("System","UkoncitEdisona",cas)) q:cas=""
		if $d(^CacheTempEDISON("System","UkoncitEdisona",cas,guid)) s found = 1 q
	}
	quit found
}

Method TestLockSetIsDel()
{
	d ..Sys(.sys)
	try {
		do $$$AssertEquals(..Call("LockSet", .sys), "", "LockSet skončil chybou")
		do $$$AssertEquals(..Call("LockIs", .sys), "", "LockIs skončil chybou")
		do $$$AssertEquals($g(^||ML("OUT","locked")), 1, "zámek po LockSet chybí")
		do $$$AssertEquals(..Call("LockDel", .sys), "", "LockDel skončil chybou")
		d ..Call("LockIs", .sys)
		do $$$AssertEquals($g(^||ML("OUT","locked")), 0, "zámek po LockDel zůstal")
	} catch ex {
		s error = ex.DisplayString()
	}
	// Úklid i po výjimce: zámek by visel v ^CacheTempSYSLock sdíleného serveru.
	d ..Call("LockDel", .sys)
	do $$$AssertEquals($g(error), "", "výjimka v testu")
}

/// Úklid smaže jen požadavek GUIDu volajícího, požadavek jiné session zůstane.
Method TestRequestEndAndCleanKeepOtherGuid()
{
	d ..KillEndRequests()
	try {
		d ..Sys(.sys)
		s ^||ML("IN","sec") = 100000
		do $$$AssertEquals(..Call("RequestEnd", .sys), "", "RequestEnd skončil chybou")
		d ..Sys(.other,, ..#OtherGuid)
		s ^||ML("IN","sec") = 100000
		d ..Call("RequestEnd", .other)
		do $$$AssertTrue(..EndRequested(..#TestGuid), "požadavek pro GUID volajícího chybí")

		do $$$AssertEquals(..Call("CleanEndRequest", .sys), "", "CleanEndRequest skončil chybou")
		do $$$AssertNotTrue(..EndRequested(..#TestGuid), "požadavek volajícího po úklidu zůstal")
		do $$$AssertTrue(..EndRequested(..#OtherGuid), "úklid smazal požadavek jiné session")
	} catch ex {
		s error = ex.DisplayString()
	}
	d ..KillEndRequests()
	do $$$AssertEquals($g(error), "", "výjimka v testu")
}

/// Jiná firma ani prázdný GUID nic nezapíšou: zámek skutečné firmy nebo požadavek bez GUID by zasáhl cizí session.
Method TestRejectsOtherFirmAndEmptyGuid()
{
	d ..KillEndRequests()
	d ..Sys(.sys, "TEST_EDISON2_GUARD")
	s ^||ML("IN","sec") = 100000
	for method = "LockSet", "LockIs", "LockDel", "RequestEnd", "CleanEndRequest" {
		do $$$AssertNotEquals(..Call(method, .sys), "", method_" pod jinou firmou neskončil chybou")
	}
	do $$$AssertNotTrue(..EndRequested(..#TestGuid), "RequestEnd zapsal pod jinou firmou")

	d ..Sys(.sys,, "")
	s ^||ML("IN","sec") = 100000
	do $$$AssertNotEquals(..Call("RequestEnd", .sys), "", "RequestEnd bez GUID neskončil chybou")
	do $$$AssertNotEquals(..Call("LockSet", .sys), "", "LockSet bez GUID neskončil chybou")
	d ..KillEndRequests()
}

}
