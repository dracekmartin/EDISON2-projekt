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

/// Load má stejný scope/key guard jako Save — samostatná cesta, samostatný test.
Method TestLoadWithoutScopeReturnsError()
{
	do ..SetupSys(.sys)
	set ^||ML("IN","key") = "x"
	set sc = ##class(ALVA.EDControls.UserData).Load(.sys)
	do $$$AssertNotTrue((sc = 0), "Load bez scope musí vrátit chybu, ne 0")
	do $$$AssertTrue((sc [ "scope is required"), "Chyba musí zmínit chybějící scope, dostal: "_sc)
}

/// Delete má stejný scope/key guard jako Save — samostatná cesta, samostatný test.
Method TestDeleteWithoutKeyReturnsError()
{
	do ..SetupSys(.sys)
	set ^||ML("IN","scope") = "EDControls.Tests"
	set sc = ##class(ALVA.EDControls.UserData).Delete(.sys)
	do $$$AssertNotTrue((sc = 0), "Delete bez key musí vrátit chybu, ne 0")
	do $$$AssertTrue((sc [ "key is required"), "Chyba musí zmínit chybějící key, dostal: "_sc)
}

/// Save podruhé přepíše hodnotu (dokumentované "Přepíše existující").
Method TestSaveOverwritesExistingValue()
{
	do ..SetupSys(.sys)
	set ^||ML("IN","scope") = "EDControls.Tests"
	set ^||ML("IN","key") = "overwrite"
	set ^||ML("IN","data") = "first"
	do ##class(ALVA.EDControls.UserData).Save(.sys)

	kill ^||ML
	do ..SetupSys(.sys)
	set ^||ML("IN","scope") = "EDControls.Tests"
	set ^||ML("IN","key") = "overwrite"
	set ^||ML("IN","data") = "second"
	do ##class(ALVA.EDControls.UserData).Save(.sys)

	kill ^||ML
	do ..SetupSys(.sys)
	set ^||ML("IN","scope") = "EDControls.Tests"
	set ^||ML("IN","key") = "overwrite"
	do ##class(ALVA.EDControls.UserData).Load(.sys)
	do $$$AssertEquals($get(^||ML("OUT","data")), "second", "Druhý Save musí přepsat první hodnotu")

	kill ^||ML
	do ..SetupSys(.sys)
	set ^||ML("IN","scope") = "EDControls.Tests"
	set ^||ML("IN","key") = "overwrite"
	do ##class(ALVA.EDControls.UserData).Delete(.sys)
}

/// Storage je klíčovaný (IdFa, userID, scope, key) — uložení pod jednou firmou
/// nesmí být viditelné pod jinou (ModulePreferencesService spoléhá na automatickou IdFa izolaci).
Method TestDataIsolatedByIdFa()
{
	do ..SetupSys(.sys, "CB_DP")
	set ^||ML("IN","scope") = "EDControls.Tests"
	set ^||ML("IN","key") = "isolation"
	set ^||ML("IN","data") = "cb-data"
	do ##class(ALVA.EDControls.UserData).Save(.sys)

	kill ^||ML
	do ..SetupSys(.sys, "JI_DP")
	set ^||ML("IN","scope") = "EDControls.Tests"
	set ^||ML("IN","key") = "isolation"
	do ##class(ALVA.EDControls.UserData).Load(.sys)
	do $$$AssertTrue(($data(^||ML("OUT","data")) = 0), "Data z CB_DP nesmí být vidět pod JI_DP")

	// cleanup
	kill ^||ML
	do ..SetupSys(.sys, "CB_DP")
	set ^||ML("IN","scope") = "EDControls.Tests"
	set ^||ML("IN","key") = "isolation"
	do ##class(ALVA.EDControls.UserData).Delete(.sys)
}

/// Hranice missing vs empty: Save prázdného blobu vytvoří definovaný uzel s "",
/// Load pak zapíše OUT("data")="" ($d=1) — .NET to mapuje na empty-not-null.
/// (Na tomto rozdílu stojí null-vs-empty kontrakt ModulePreferencesService.)
Method TestEmptyBlobIsStoredAsDefinedEmpty()
{
	do ..SetupSys(.sys)
	set ^||ML("IN","scope") = "EDControls.Tests"
	set ^||ML("IN","key") = "emptyblob"
	set ^||ML("IN","data") = ""
	do ##class(ALVA.EDControls.UserData).Save(.sys)

	kill ^||ML
	do ..SetupSys(.sys)
	set ^||ML("IN","scope") = "EDControls.Tests"
	set ^||ML("IN","key") = "emptyblob"
	do ##class(ALVA.EDControls.UserData).Load(.sys)
	do $$$AssertTrue(($data(^||ML("OUT","data")) = 1), "Prázdný blob musí být definovaný (missing != empty)")
	do $$$AssertEquals($get(^||ML("OUT","data")), "", "Prázdný blob se načte jako """"")

	kill ^||ML
	do ..SetupSys(.sys)
	set ^||ML("IN","scope") = "EDControls.Tests"
	set ^||ML("IN","key") = "emptyblob"
	do ##class(ALVA.EDControls.UserData).Delete(.sys)
}

}
