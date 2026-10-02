Include %occInclude

/// Kontraktní test přepínače nového shellu: ALVA.SETUP.Main.LoadSettings vrací řádek
/// "newShell{del}{0|1}" jen když existuje ^EDISON("System","NewShell"), podle něj EDLOADER
/// spouští EDISONMAIN2.exe. Třída patří do repa EDISON - když na serveru není, test se přeskočí.
/// Uzel je sdílený (dev server), proto se původní stav po každém testu obnoví.
Class ALVA.EDISONMAIN2.Tests.NewShellSwitchContractTest Extends ALVA.EDTesting.MLTestCase
{

/// Systémový namespace (ALVA.EDISON.Main.GetSystemDB, MLRest SysNamespace).
Parameter SYSTEMDB = "MLINE";

Property SavedData As %Integer;

Property SavedValue As %String;

Method OnBeforeOneTest(testname As %String) As %Status
{
	set sc = ##super(testname)
	set ns = ..#SYSTEMDB
	set ..SavedData = $data(^[ns]EDISON("System","NewShell"))
	set ..SavedValue = $get(^[ns]EDISON("System","NewShell"))
	quit sc
}

Method OnAfterOneTest(testname As %String) As %Status
{
	set ns = ..#SYSTEMDB
	if ..SavedData#10 { set ^[ns]EDISON("System","NewShell") = ..SavedValue }
	else { zkill ^[ns]EDISON("System","NewShell") }
	quit $$$OK
}

Method HasSetupMain() As %Boolean
{
	quit ##class(%Dictionary.CompiledClass).%ExistsId("ALVA.SETUP.Main")
}

/// Zavolá LoadSettings a vrátí řádky "newShell" (rows(n) = celý řádek), návratová hodnota = jejich počet.
Method NewShellRows(Output rows) As %Integer
{
	kill ^||ML, rows
	do ..SetupSys(.sys)
	set sys("C","App") = "ALWA.SYS.MLRest"
	do ##class(ALVA.SETUP.Main).LoadSettings(.sys, .in, .out)
	do $$$AssertEquals($get(^||ML("ERR")), "", "LoadSettings nesmí vrátit chybu")

	set count = 0, i = ""
	for {
		set i = $order(^||ML("OUT", i))
		quit:i=""
		continue:i'?1.N
		set row = ^||ML("OUT", i)
		continue:$piece(row, "~", 1)'="newShell"
		set rows($increment(count)) = row
	}
	quit count
}

/// Bez uzlu řádek chybí - totéž vrací server bez téhle změny, EDLOADER pak spustí starý shell.
Method TestNodeMissingEmitsNoRow()
{
	if '..HasSetupMain() { do $$$AssertSkipped("ALVA.SETUP.Main není na serveru") quit }

	set ns = ..#SYSTEMDB
	zkill ^[ns]EDISON("System","NewShell")
	do $$$AssertEquals(..NewShellRows(.rows), 0, "bez uzlu NewShell nesmí LoadSettings vracet řádek newShell")
}

/// Hodnota uzlu je druhé a poslední pole řádku; EDLOADER zapíná jen na "1" (Val(data(1)) = 1).
Method TestNodeValueEmitted()
{
	if '..HasSetupMain() { do $$$AssertSkipped("ALVA.SETUP.Main není na serveru") quit }

	set ns = ..#SYSTEMDB
	for value = 0, 1 {
		set ^[ns]EDISON("System","NewShell") = value
		do $$$AssertEquals(..NewShellRows(.rows), 1, "uzel "_value_": čekám právě jeden řádek newShell")
		do $$$AssertEquals($get(rows(1)), "newShell~"_value, "uzel "_value_": tvar řádku newShell")
	}
}

}
