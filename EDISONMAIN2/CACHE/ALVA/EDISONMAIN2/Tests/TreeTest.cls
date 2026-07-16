Include %occInclude

Class ALVA.EDISONMAIN2.Tests.TreeTest Extends ALVA.EDTesting.MLTestCase
{

/// %UnitTest pro ALVA.EDISONMAIN2.API.Tree.GetTree.
/// OnBeforeOneTest (kill ^||ML) a SetupSys (sys array boilerplate) zděděny z MLTestCase.
///
/// Spuštění (jednou per namespace):
///   set ^UnitTestRoot = "c:\temp\unittests\"
///   do ##class(%UnitTest.Manager).RunTest(":ALVA.EDISONMAIN2.Tests.TreeTest", "/noload")
/// Nebo programově:
///   do ##class(ALVA.EDTesting.TestRunner).RunPackage(.sys)   ; package="ALVA.EDISONMAIN2.Tests"

/// Default input (tree="") -> main tree. První záznam je validní wire-format uzel
/// (FAMILY nebo EXE — main začíná rozbalenou agendou-rodinou po materializaci z legacy XML).
Method TestDefaultInputReturnsMainTree()
{
	do ..SetupSys(.sys)
	do ##class(ALVA.EDISONMAIN2.API.Tree).GetTree(.sys)

	do $$$AssertNotTrue(($data(^||ML("OUT",1))=0), "OUT global must not be empty")

	set firstRecord = ^||ML("OUT",1)
	set firstType = $listget(firstRecord, 1)
	do $$$AssertNotTrue((",FAMILY,EXE,"'[(","_firstType_",")), "First record of main tree must be FAMILY or EXE, got: "_firstType)
}

/// System tree má jiný obsah -- minimální sanity check.
Method TestSystemTreeReturnsNonEmpty()
{
	do ..SetupSys(.sys)
	set ^||ML("IN","tree") = "system"
	do ##class(ALVA.EDISONMAIN2.API.Tree).GetTree(.sys)

	do $$$AssertNotTrue(($data(^||ML("OUT",1))=0), "System tree must not be empty")
}

/// Partners tree může být prázdný (žádné 3rd-party řešení yet), ale GetTree nesmí nahlásit error.
/// Error location dle GENERAL.inc $$$CatchErrRest: ^||ML("ERR"), ne ^||ML("OUT","ERR").
Method TestPartnersTreeDoesNotError()
{
	do ..SetupSys(.sys)
	set ^||ML("IN","tree") = "partners"
	do ##class(ALVA.EDISONMAIN2.API.Tree).GetTree(.sys)

	set errMarker = $get(^||ML("ERR"))
	do $$$AssertEquals(errMarker, "", "Partners tree must not set ERR marker")
}

/// Wire-format invariant: každý záznam má jako první element jeden ze tří validních typů.
Method TestAllRecordsHaveValidType()
{
	do ..SetupSys(.sys)
	set ^||ML("IN","tree") = "main"
	do ##class(ALVA.EDISONMAIN2.API.Tree).GetTree(.sys)

	set i = "", validTypes = ",FAMILY,EXE,FAMILY_END,"
	for {
		set i = $order(^||ML("OUT", i))
		quit:i=""
		continue:i'?1.N
		set record = ^||ML("OUT", i)
		set type = $listget(record, 1)
		do $$$AssertNotTrue((validTypes'[(","_type_",")), "Record #"_i_" has unknown type: "_type)
	}
}

/// Strom musí být strukturálně balancovaný -- počet FAMILY = počet FAMILY_END.
Method TestFamilyOpenCloseBalance()
{
	do ..SetupSys(.sys)
	set ^||ML("IN","tree") = "main"
	do ##class(ALVA.EDISONMAIN2.API.Tree).GetTree(.sys)

	set i = "", openCount = 0, closeCount = 0
	for {
		set i = $order(^||ML("OUT", i))
		quit:i=""
		continue:i'?1.N
		set record = ^||ML("OUT", i)
		set type = $listget(record, 1)
		if (type = "FAMILY") { set openCount = openCount + 1 }
		if (type = "FAMILY_END") { set closeCount = closeCount + 1 }
	}

	do $$$AssertEquals(openCount, closeCount, "FAMILY open ("_openCount_") must match FAMILY_END close ("_closeCount_")")
}

/// FAMILY záznamy musí mít 5 polí dle wire-format kontraktu (type, name, expanded,
/// elementId, licensed) — elementId neprázdné (gate složek proti licenci/AP).
Method TestFamilyRecordsHaveFiveFieldsWithElementId()
{
	do ..SetupSys(.sys)
	set ^||ML("IN","tree") = "system"
	do ##class(ALVA.EDISONMAIN2.API.Tree).GetTree(.sys)

	set i = ""
	for {
		set i = $order(^||ML("OUT", i))
		quit:i=""
		continue:i'?1.N
		set record = ^||ML("OUT", i)
		if ($listget(record, 1) '= "FAMILY") { continue }
		set fieldCount = $listlength(record)
		do $$$AssertEquals(fieldCount, 5, "FAMILY record #"_i_" has "_fieldCount_" fields, expected 5")
		do $$$AssertNotTrue(($listget(record, 4) = ""), "FAMILY record #"_i_" must have non-empty elementId")
	}
}

/// EXE záznamy musí mít 9 polí dle wire-format kontraktu.
Method TestExeRecordsHaveNineFields()
{
	do ..SetupSys(.sys)
	set ^||ML("IN","tree") = "main"
	do ##class(ALVA.EDISONMAIN2.API.Tree).GetTree(.sys)

	set i = ""
	for {
		set i = $order(^||ML("OUT", i))
		quit:i=""
		continue:i'?1.N
		set record = ^||ML("OUT", i)
		if ($listget(record, 1) '= "EXE") { continue }
		set fieldCount = $listlength(record)
		do $$$AssertEquals(fieldCount, 9, "EXE record #"_i_" has "_fieldCount_" fields, expected 9")
	}
}

}
