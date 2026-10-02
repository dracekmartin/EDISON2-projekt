Include %occInclude

/// Kontraktní test wire formátu ALVA.SYSTEM.Main.maSeUkoncit (cooperative logout),
/// na kterém závisí LogoutWatcher.Parse v shellu: řádek "ukoncit{del}{0|1}{del}{sekundy}".
/// Třída patří do repa EDISON - když na serveru není, test se přeskočí
/// (vzor TelemetryContractTest). Chyba je v návratové hodnotě (ret="1~...").
Class ALVA.EDISONMAIN2.Tests.LogoutContractTest Extends ALVA.EDTesting.MLTestCase
{

Method HasSystemMain() As %Boolean
{
	quit ##class(%Dictionary.CompiledClass).%ExistsId("ALVA.SYSTEM.Main")
}

/// Poll bez signálu (čerstvá test session): bez chyby, řádek "ukoncit" s wire booleanem
/// na pozici 2; sekundy (pozice 3) jsou číslo, nebo prázdné - nečíselné LogoutWatcher
/// tiše nahradí defaultem 60 s a admin-nastavený odpočet by se ztratil.
Method TestMaSeUkoncitWireFormat()
{
	if '..HasSystemMain() { do $$$AssertSkipped("ALVA.SYSTEM.Main není na serveru") quit }

	do ..SetupSys(.sys)
	set ret = ##class(ALVA.SYSTEM.Main).maSeUkoncit(.sys)
	do $$$AssertEquals(+ret, 0, "maSeUkoncit nesmí vrátit chybu, got: "_ret)

	set i = "", found = 0
	for {
		set i = $order(^||ML("OUT", i))
		quit:i=""
		continue:i'?1.N
		set row = ^||ML("OUT", i)
		continue:$piece(row,"~",1)'="ukoncit"
		set found = 1
		set flag = $piece(row,"~",2)
		do $$$AssertNotTrue((",0,1,"'[(","_flag_",")), "ukoncit flag musí být ""0""/""1"", got: "_flag)
		set seconds = $piece(row,"~",3)
		do $$$AssertTrue((seconds="")||(seconds?1.N), "sekundy (pozice 3) musí být číslo nebo prázdné, got: "_seconds)
	}
	do $$$AssertTrue(found, "OUT musí obsahovat řádek ""ukoncit"" - bez něj LogoutWatcher signál nikdy neuvidí")
}

}
