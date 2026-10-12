Include %occInclude

Class ALVA.EDTesting.Tests.ITAccountTest Extends ALVA.EDTesting.MLTestCase
{

/// Get vrací účet z globálu a namespace, ve kterém ho čte. Global jen čte: drží skutečný účet DEV.
Method TestGetReturnsGlobalAndNamespace()
{
	d ..SetupSys(.sys)
	d ##class(ALVA.EDTesting.ITAccount).Get(.sys)

	do $$$AssertEquals($g(^||ML("ERR")), "", "Get skončil chybou")
	do $$$AssertEquals($g(^||ML("OUT","namespace")), $namespace)
	// Hodnoty do zprávy nedávat: heslo by skončilo v logu testů.
	do $$$AssertTrue(($d(^||ML("OUT","login"))>0)&&($g(^||ML("OUT","login"))=$g(^EDTesting("ITAccount","login"))), "login neodpovídá globálu")
	do $$$AssertTrue(($d(^||ML("OUT","password"))>0)&&($g(^||ML("OUT","password"))=$g(^EDTesting("ITAccount","password"))), "password neodpovídá globálu")
}

}
