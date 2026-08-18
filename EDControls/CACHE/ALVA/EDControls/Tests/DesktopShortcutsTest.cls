Include %occInclude

Class ALVA.EDControls.Tests.DesktopShortcutsTest Extends ALVA.EDTesting.MLTestCase
{

/// %UnitTest pro ALVA.EDControls.DesktopShortcuts (registrační kanál agend na plochu).
/// OnBeforeOneTest (kill ^||ML) a SetupSys zděděny z MLTestCase.
///
/// Testy jdou schválně jen přes MLRest metody (vč. čtení blobu přes UserData.Load) - jinak
/// by musely znát userID, které si $$$FillParams odvozuje sám. Blob je sdílený se shellem,
/// proto se každý test po sobě uklidí.

/// Blob plochy jako %DynamicArray (přes veřejné Load, ne přes global).
Method Desktop() As %DynamicArray
{
	do ..SetupSys(.sys)
	kill ^||ML("IN"), ^||ML("OUT")
	set ^||ML("IN","scope") = "EDISONMAIN2", ^||ML("IN","key") = "Shortcuts"
	do ##class(ALVA.EDControls.UserData).Load(.sys)
	set raw = $get(^||ML("OUT","data"))
	ret $select(raw = "":##class(%DynamicArray).%New(), 1:##class(%DynamicArray).%FromJSON(raw))
}

Method WriteRaw(pRaw As %String)
{
	do ..SetupSys(.sys)
	kill ^||ML("IN"), ^||ML("OUT")
	set ^||ML("IN","scope") = "EDISONMAIN2", ^||ML("IN","key") = "Shortcuts", ^||ML("IN","data") = pRaw
	do ##class(ALVA.EDControls.UserData).Save(.sys)
}

Method Cleanup()
{
	do ..SetupSys(.sys)
	kill ^||ML("IN"), ^||ML("OUT")
	set ^||ML("IN","scope") = "EDISONMAIN2", ^||ML("IN","key") = "Shortcuts"
	do ##class(ALVA.EDControls.UserData).Delete(.sys)
}

/// Zavolá Add/Remove/Exists s daným JSON a vrátí hodnotu z OUT.
Method Call(pMethod As %String, pItem As %String, pOut As %String) As %String
{
	do ..SetupSys(.sys)
	kill ^||ML("IN"), ^||ML("OUT")
	set ^||ML("IN","item") = pItem
	if pMethod = "Add" { do ##class(ALVA.EDControls.DesktopShortcuts).Add(.sys) }
	elseif pMethod = "Remove" { do ##class(ALVA.EDControls.DesktopShortcuts).Remove(.sys) }
	else { do ##class(ALVA.EDControls.DesktopShortcuts).Exists(.sys) }
	ret $get(^||ML("OUT",pOut))
}

Method Item(pKind As %String, pId As %String, pName As %String = "N", pExtra As %String = "") As %String
{
	ret "{""Kind"":"""_pKind_""",""Id"":"""_pId_""",""ElementId"":""145008"",""Name"":"""_pName_""""_pExtra_"}"
}

Method TestAddThenExists()
{
	do ..Cleanup()
	do $$$AssertEquals(..Call("Add", ..Item("PrintReport", "1003"), "count"), 1, "Po přidání je na ploše jedna položka")
	do $$$AssertEquals($get(^||ML("ERR")), "", "Add nesmí nahlásit chybu")
	do $$$AssertEquals(..Call("Exists", ..Item("PrintReport", "1003"), "exists"), 1, "Přidaný zástupce musí být nalezen")
	do ..Cleanup()
}

Method TestAddSameKeyReplacesAndKeepsPosition()
{
	do ..Cleanup()
	// Col/Row je uživatelovo umístění dlaždice - aktualizace z modulu ho nesmí zahodit.
	do ..Call("Add", ..Item("PrintReport", "1003", "Puvodni", ",""Col"":3,""Row"":7"), "count")
	do $$$AssertEquals(..Call("Add", ..Item("PrintReport", "1003", "Prejmenovano"), "count"), 1, "Stejný klíč nesmí vyrobit duplicitu")

	set arr = ..Desktop()
	do $$$AssertEquals(arr.%Get(0).Name, "Prejmenovano", "Aktualizace musí přepsat data")
	do $$$AssertEquals(arr.%Get(0).Col, 3, "Sloupec musí zůstat zachovaný")
	do $$$AssertEquals(arr.%Get(0).Row, 7, "Řádek musí zůstat zachovaný")
	do ..Cleanup()
}

Method TestRemove()
{
	do ..Cleanup()
	do ..Call("Add", ..Item("PrintReport", "1003"), "count")
	do $$$AssertEquals(..Call("Remove", ..Item("PrintReport", "1003"), "count"), 0, "Po odebrání je plocha prázdná")
	do $$$AssertEquals(..Call("Exists", ..Item("PrintReport", "1003"), "exists"), 0, "Odebraný zástupce nesmí být nalezen")
	do ..Cleanup()
}

Method TestDifferentKindsCoexist()
{
	do ..Cleanup()
	// Táž forma jako modul z menu i jako sestava - jsou to dvě různé položky plochy.
	do ..Call("Add", ..Item("PrintReport", "1003"), "count")
	do $$$AssertEquals(..Call("Add", ..Item("MenuModule", "145008"), "count"), 2, "Různé druhy nad touž formou koexistují")
	do ..Cleanup()
}

Method TestRemoveMissingIsNoError()
{
	do ..Cleanup()
	do ..Call("Remove", ..Item("PrintReport", "9999"), "count")
	do $$$AssertEquals($get(^||ML("ERR")), "", "Odebrání neexistující položky není chyba")
	do ..Cleanup()
}

Method TestCorruptBlobDoesNotBreakAdd()
{
	do ..Cleanup()
	do ..WriteRaw("{tohle neni pole}")
	do $$$AssertEquals(..Call("Add", ..Item("PrintReport", "1003"), "count"), 1, "Poškozený blob se nahradí novým seznamem")
	do $$$AssertEquals($get(^||ML("ERR")), "", "Poškozený blob nesmí shodit registraci")
	do ..Cleanup()
}

}
