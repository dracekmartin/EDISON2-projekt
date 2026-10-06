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

/// Návratová hodnota posledního Call - chyba metody je tam ("1~zpráva") i v ^||ML("ERR").
Property lastRet As %String;

/// Zavolá Add/Remove/Exists s daným JSON a vrátí hodnotu z OUT.
Method Call(pMethod As %String, pItem As %String, pOut As %String) As %String
{
	do ..SetupSys(.sys)
	kill ^||ML("IN"), ^||ML("OUT")
	set ^||ML("IN","item") = pItem
	if pMethod = "Add" { set ..lastRet = ##class(ALVA.EDControls.DesktopShortcuts).Add(.sys) }
	elseif pMethod = "Remove" { set ..lastRet = ##class(ALVA.EDControls.DesktopShortcuts).Remove(.sys) }
	else { set ..lastRet = ##class(ALVA.EDControls.DesktopShortcuts).Exists(.sys) }
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
	do $$$AssertEquals($p(..lastRet, "~"), 0, "Odebrání neexistující položky není chyba")
	do ..Cleanup()
}

Method TestCorruptBlobDoesNotBreakAdd()
{
	do ..Cleanup()
	do ..WriteRaw("{tohle neni pole}")
	do $$$AssertEquals(..Call("Add", ..Item("PrintReport", "1003"), "count"), 1, "Poškozený blob se nahradí novým seznamem")
	do $$$AssertEquals($p(..lastRet, "~"), 0, "Poškozený blob nesmí shodit registraci")
	do ..Cleanup()
}

/// Chybějící item je chyba - a musí být poznat na návratové hodnotě, jinak by předchozí
/// dva testy „bez chyby" procházely i kdyby metoda padala.
Method TestMissingItemIsReportedOnReturnValue()
{
	do ..Cleanup()
	do ..Call("Add", "", "count")
	do $$$AssertEquals($p(..lastRet, "~"), 1, "Chybějící item -> ret začíná 1~")
	do $$$AssertNotEquals($p(..lastRet, "~", 2), "", "Chyba nese zprávu")
	do ..Cleanup()
}

/// Identita položky = "Kind|ElementId|Id" - musí sedět s DesktopShortcut.Key na klientovi,
/// jinak by shell a agenda každá viděly jinou množinu dlaždic.
Method TestKeyOfIdentity()
{
	set obj = ##class(%DynamicObject).%FromJSON("{""Kind"":""PrintReport"",""Id"":""1003"",""ElementId"":""145008""}")
	do $$$AssertEquals(##class(ALVA.EDControls.DesktopShortcuts).KeyOf(obj), "PrintReport|145008|1003")
}

/// Chybějící property nesmí hodit <PROPERTY DOES NOT EXIST> - blob píšou i agendy.
Method TestKeyOfToleratesMissingProperties()
{
	set obj = ##class(%DynamicObject).%FromJSON("{""Kind"":""MenuModule""}")
	do $$$AssertEquals(##class(ALVA.EDControls.DesktopShortcuts).KeyOf(obj), "MenuModule|")
	set obj = ##class(%DynamicObject).%FromJSON("{""Kind"":""PrintReport""}")
	do $$$AssertEquals(##class(ALVA.EDControls.DesktopShortcuts).KeyOf(obj), "PrintReport||")
}

/// Modul menu nese variantu v Input (Převody AD/MHD) - klíč jako ExecutableModule.VariantKey na klientovi.
Method TestKeyOfMenuModuleVariant()
{
	set obj = ##class(%DynamicObject).%FromJSON("{""Kind"":""MenuModule"",""Id"":""146016"",""ElementId"":""146016"",""Input"":""MHD""}")
	do $$$AssertEquals(##class(ALVA.EDControls.DesktopShortcuts).KeyOf(obj), "MenuModule|146016|MHD")
	set obj = ##class(%DynamicObject).%FromJSON("{""Kind"":""MenuModule"",""Id"":""146016"",""ElementId"":""146016""}")
	do $$$AssertEquals(##class(ALVA.EDControls.DesktopShortcuts).KeyOf(obj), "MenuModule|146016", "Bez Input holé ElementId")
}

Method TestMenuModuleVariantsCoexist()
{
	do ..Cleanup()
	do ..Call("Add", ..Item("MenuModule", "145008", "N", ",""Input"":""AD"""), "count")
	do $$$AssertEquals(..Call("Add", ..Item("MenuModule", "145008", "N", ",""Input"":""MHD"""), "count"), 2, "Dvě varianty téže formy jsou dvě dlaždice")
	do $$$AssertEquals(..Call("Remove", ..Item("MenuModule", "145008", "N", ",""Input"":""MHD"""), "count"), 1, "Odebrání varianty nechá druhou")
	do $$$AssertEquals(..Call("Exists", ..Item("MenuModule", "145008", "N", ",""Input"":""AD"""), "exists"), 1, "Zůstala varianta AD")
	do ..Cleanup()
}

/// Bez Kind není identita - položka se nesmí tvářet jako platná.
Method TestKeyOfWithoutKindIsEmpty()
{
	set obj = ##class(%DynamicObject).%FromJSON("{""Id"":""1003""}")
	do $$$AssertEquals(##class(ALVA.EDControls.DesktopShortcuts).KeyOf(obj), "")
	do $$$AssertEquals(##class(ALVA.EDControls.DesktopShortcuts).KeyOf(""), "", "Neobjekt -> prázdný klíč")
}

}
