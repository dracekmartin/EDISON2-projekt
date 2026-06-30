Include %occInclude

Class ALVA.EDControls.Tests.UserDataTest Extends ALVA.EDTesting.MLTestCase
{

/// %UnitTest pro ALVA.EDControls.UserData (Save/Load/Delete).
/// OnBeforeOneTest (kill ^||ML) a SetupSys zděděny z MLTestCase.
/// Chyby: $$$CatchErrRest je ukládá do ^||ML("ERR") (viz GENERAL.inc), ne do OUT.

Method TestSaveLoadDeleteRoundTrip()
{
	do ..SetupSys(.sys)
	set ^||ML("IN","scope") = "EDControls.Tests"
	set ^||ML("IN","key") = "roundtrip"
	set ^||ML("IN","data") = "blob-value-123"
	do ##class(ALVA.EDControls.UserData).Save(.sys)
	do $$$AssertEquals($get(^||ML("ERR")), "", "Save nesmí nahlásit chybu")

	kill ^||ML
	set ^||ML("IN","scope") = "EDControls.Tests"
	set ^||ML("IN","key") = "roundtrip"
	do ##class(ALVA.EDControls.UserData).Load(.sys)
	do $$$AssertEquals($get(^||ML("OUT","data")), "blob-value-123", "Load musí vrátit uložená data")

	kill ^||ML
	set ^||ML("IN","scope") = "EDControls.Tests"
	set ^||ML("IN","key") = "roundtrip"
	do ##class(ALVA.EDControls.UserData).Delete(.sys)
	do $$$AssertEquals($get(^||ML("ERR")), "", "Delete nesmí nahlásit chybu")

	kill ^||ML
	set ^||ML("IN","scope") = "EDControls.Tests"
	set ^||ML("IN","key") = "roundtrip"
	do ##class(ALVA.EDControls.UserData).Load(.sys)
	do $$$AssertTrue(($data(^||ML("OUT","data")) = 0), "Load po Delete musí být prázdný")
}

Method TestLoadMissingKeyReturnsEmpty()
{
	do ..SetupSys(.sys)
	set ^||ML("IN","scope") = "EDControls.Tests"
	set ^||ML("IN","key") = "neexistujici-klic"
	do ##class(ALVA.EDControls.UserData).Delete(.sys)

	kill ^||ML
	set ^||ML("IN","scope") = "EDControls.Tests"
	set ^||ML("IN","key") = "neexistujici-klic"
	do ##class(ALVA.EDControls.UserData).Load(.sys)
	do $$$AssertTrue(($data(^||ML("OUT","data")) = 0), "Neexistující klíč → prázdný OUT")
	do $$$AssertEquals($get(^||ML("ERR")), "", "Neexistující klíč není chyba")
}

/// Regrese: bez $$$Init padal $$$CatchErrRest na <UNDEFINED> err. Chyba je nesena návratovou
/// hodnotou metody (ret = "1~zpráva"); ^||ML("ERR") se plní až v MLRest HTTP vrstvě, ne in-process.
Method TestSaveWithoutScopeReturnsError()
{
	do ..SetupSys(.sys)
	set ^||ML("IN","key") = "x"
	set sc = ##class(ALVA.EDControls.UserData).Save(.sys)
	do $$$AssertNotTrue((sc = 0), "Save bez scope musí vrátit chybu, ne 0")
	do $$$AssertTrue((sc [ "scope is required"), "Chyba musí zmínit chybějící scope, dostal: "_sc)
}

Method TestSaveWithoutKeyReturnsError()
{
	do ..SetupSys(.sys)
	set ^||ML("IN","scope") = "EDControls.Tests"
	set sc = ##class(ALVA.EDControls.UserData).Save(.sys)
	do $$$AssertNotTrue((sc = 0), "Save bez key musí vrátit chybu, ne 0")
	do $$$AssertTrue((sc [ "key is required"), "Chyba musí zmínit chybějící key, dostal: "_sc)
}

}
