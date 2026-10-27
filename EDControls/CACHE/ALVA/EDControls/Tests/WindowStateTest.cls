Include %occInclude

/// %UnitTest pro ALVA.EDControls.WindowState.ResetUserStates.
/// Test běží pod skutečným MLP sdíleného serveru (SetupSys), proto pracuje jen s formID pod
/// vlastním prefixem s $j a po sobě je smaže. Prefixy EDISON2/TD by smazaly skutečné pozice oken.
Class ALVA.EDControls.Tests.WindowStateTest Extends ALVA.EDTesting.MLTestCase
{

Method TestResetDeletesOnlyPrefixedStateOfCurrentUser()
{
	do ..SetupSys(.sys)
	set mlp = sys("U","MLP"), otherMlp = "WSTEST-OTHER"
	set prefix = ..Prefix()
	set win = prefix_".Win", other = "ZZWSOTHER"_$job_".Win", noDot = prefix_"X.Win"
	do ..Cleanup(sys("U","IdFa"), win, other, noDot)

	do ..Store(sys("U","IdFa"), win, "state", mlp)
	do ..Store(sys("U","IdFa"), win, "props", mlp)
	do ..Store(sys("U","IdFa"), win, "state", otherMlp)
	do ..Store(sys("U","IdFa"), other, "state", mlp)
	do ..Store(sys("U","IdFa"), noDot, "state", mlp)

	set ^||ML("IN","prefixes") = $listbuild(prefix)
	do ##class(ALVA.EDControls.WindowState).ResetUserStates(.sys)

	do $$$AssertEquals($get(^||ML("ERR")), "", "Reset nesmí nahlásit chybu: "_$get(^||ML("ERR")))
	do $$$AssertEquals($get(^||ML("OUT","deleted")), 1, "Smaže se právě jeden záznam")
	do $$$AssertNotTrue(..Exists(sys("U","IdFa"), win, "state", mlp), "Geometrie okna s prefixem je smazaná")
	do $$$AssertTrue(..Exists(sys("U","IdFa"), win, "props", mlp), "Legacy props téhož formID zůstanou")
	do $$$AssertTrue(..Exists(sys("U","IdFa"), win, "state", otherMlp), "Záznam jiného uživatele zůstane")
	do $$$AssertTrue(..Exists(sys("U","IdFa"), other, "state", mlp), "Jiný prefix zůstane")
	do $$$AssertTrue(..Exists(sys("U","IdFa"), noDot, "state", mlp), "Prefix bez tečky se nepočítá")

	do ..Cleanup(sys("U","IdFa"), win, other, noDot)
}

Method TestMissingPrefixesReturnsError()
{
	do ..SetupSys(.sys)
	set sc = ##class(ALVA.EDControls.WindowState).ResetUserStates(.sys)
	do $$$AssertNotTrue((sc = 0), "Reset bez prefixů musí vrátit chybu")
	do $$$AssertNotEquals($get(^||ML("ERR")), "", "Chyba je v ^||ML(""ERR"")")
}

Method TestEmptyPrefixReturnsError()
{
	// Prázdný prefix by smazal geometrii všech oken, jejichž formID začíná tečkou.
	do ..SetupSys(.sys)
	set ^||ML("IN","prefixes") = $listbuild(..Prefix(), "")
	set sc = ##class(ALVA.EDControls.WindowState).ResetUserStates(.sys)
	do $$$AssertNotTrue((sc = 0), "Prázdný prefix musí vrátit chybu")
	do $$$AssertEquals($get(^||ML("OUT","deleted")), "", "Při chybě se nic nemaže")
}

ClassMethod Prefix() As %String
{
	quit "ZZWSTEST"_$job
}

ClassMethod Store(idfa As %String, formID As %String, ciselnik As %String, userID As %String)
{
	kill in, out
	set in("IdFa") = idfa, in("oblast") = "LIBRARY", in("ciselnik") = "Form", in("denyKill") = 1
	set in("Dta", formID, ciselnik, userID, "location") = "10;20"
	do ##class(ISML.SYS.MLFirma.API.Ciselnik).Save(.in, .out)
}

ClassMethod Exists(idfa As %String, formID As %String, ciselnik As %String, userID As %String) As %Boolean
{
	kill in, out
	set in("IdFa") = idfa, in("oblast") = "LIBRARY", in("ciselnik") = "Form"
	set in("indexPath") = formID_"~"_ciselnik_"~"_userID, in("pathDelimiter") = "~"
	do ##class(ISML.SYS.MLFirma.API.Ciselnik).Load(.in, .out)
	quit ($data(out("Dta")) > 0)
}

ClassMethod Cleanup(idfa As %String, formIDs... As %String)
{
	for i = 1:1:formIDs {
		kill in, out
		set in("IdFa") = idfa, in("oblast") = "LIBRARY", in("ciselnik") = "Form"
		set in("indexPath") = formIDs(i), in("pathDelimiter") = "~"
		do ##class(ISML.SYS.MLFirma.API.Ciselnik).Delete(.in, .out)
	}
}

}
