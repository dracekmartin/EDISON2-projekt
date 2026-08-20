Include %occInclude

Class ALVA.EDISONMAIN2.Tests.TreeTest Extends ALVA.EDTesting.MLTestCase
{

/// %UnitTest pro ALVA.EDISONMAIN2.API.Tree.GetTree.
/// OnBeforeOneTest (kill ^||ML) a SetupSys (sys array boilerplate) zděděny z MLTestCase.
/// Pozn.: chyba je nesena návratovou hodnotou GetTree (ret="1~zpráva"), NE v ^||ML("ERR")
/// - ten plní až MLRest HTTP vrstva, ne in-process. Testy proto vždy zachytávají ret.

/// Default input (tree="") -> main tree. První záznam je validní wire-format uzel
/// (FAMILY nebo EXE - main začíná rozbalenou agendou-rodinou po materializaci z legacy XML).
Method TestDefaultInputReturnsMainTree()
{
	do ..SetupSys(.sys)
	set ret = ##class(ALVA.EDISONMAIN2.API.Tree).GetTree(.sys)
	do $$$AssertEquals(+ret, 0, "GetTree nesmí vrátit chybu, got: "_ret)

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
	set ret = ##class(ALVA.EDISONMAIN2.API.Tree).GetTree(.sys)
	do $$$AssertEquals(+ret, 0, "GetTree(system) nesmí vrátit chybu, got: "_ret)

	do $$$AssertNotTrue(($data(^||ML("OUT",1))=0), "System tree must not be empty")
}

/// Partners tree může být prázdný (žádné 3rd-party řešení yet), ale GetTree nesmí nahlásit error.
/// Chyba je v návratové hodnotě, ne v ^||ML("ERR") (ten je in-process vždy prázdný).
Method TestPartnersTreeDoesNotError()
{
	do ..SetupSys(.sys)
	set ^||ML("IN","tree") = "partners"
	set ret = ##class(ALVA.EDISONMAIN2.API.Tree).GetTree(.sys)
	do $$$AssertEquals(+ret, 0, "Partners tree nesmí vrátit chybu, got: "_ret)
}

/// Neznámý název stromu propadne všemi třemi větvemi -> prázdný OUT, bez chyby.
/// Pin: "tiše prázdný" je zamýšlené chování (žádná výjimka na neznámý vstup).
Method TestUnknownTreeReturnsEmptyWithoutError()
{
	do ..SetupSys(.sys)
	set ^||ML("IN","tree") = "bogus"
	set ret = ##class(ALVA.EDISONMAIN2.API.Tree).GetTree(.sys)
	do $$$AssertEquals(+ret, 0, "Neznámý strom není chyba, got: "_ret)
	do $$$AssertTrue(($data(^||ML("OUT",1))=0), "Neznámý strom musí dát prázdný OUT")
}

/// Wire-format invariant: každý záznam má jako první element jeden ze tří validních typů.
Method TestAllRecordsHaveValidType()
{
	do ..SetupSys(.sys)
	set ^||ML("IN","tree") = "main"
	set ret = ##class(ALVA.EDISONMAIN2.API.Tree).GetTree(.sys)
	do $$$AssertEquals(+ret, 0, "GetTree(main) nesmí vrátit chybu, got: "_ret)

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

/// Strom musí být strukturálně balancovaný -- running depth nikdy záporná, končí na 0.
/// (Silnější než pouhé počítání FAMILY=FAMILY_END: chytí i FAMILY_END před FAMILY.)
Method TestFamilyOpenCloseBalance()
{
	do ..SetupSys(.sys)
	set ^||ML("IN","tree") = "main"
	do ##class(ALVA.EDISONMAIN2.API.Tree).GetTree(.sys)

	set i = "", depth = 0
	for {
		set i = $order(^||ML("OUT", i))
		quit:i=""
		continue:i'?1.N
		set type = $listget(^||ML("OUT", i), 1)
		if (type = "FAMILY") { set depth = depth + 1 }
		if (type = "FAMILY_END") {
			set depth = depth - 1
			do $$$AssertNotTrue((depth < 0), "FAMILY_END #"_i_" bez odpovídajícího FAMILY (depth < 0)")
		}
	}
	do $$$AssertEquals(depth, 0, "Nevyvážený strom -- otevřených FAMILY zbylo: "_depth)
}

/// FAMILY záznamy (napříč VŠEMI stromy) musí mít 6 polí dle wire-format kontraktu
/// (type, name, expanded, elementId, licensed, mlineOnly) - elementId neprázdné u
/// gate-ovaných složek. Gating z package IDs bije hlavně v main/partners, ne jen v system.
Method TestFamilyRecordsHaveSixFieldsAllTrees()
{
	for tree = "main", "system", "partners" {
		kill ^||ML
		do ..SetupSys(.sys)
		set ^||ML("IN","tree") = tree
		do ##class(ALVA.EDISONMAIN2.API.Tree).GetTree(.sys)

		set i = ""
		for {
			set i = $order(^||ML("OUT", i))
			quit:i=""
			continue:i'?1.N
			set record = ^||ML("OUT", i)
			if ($listget(record, 1) '= "FAMILY") { continue }
			set fieldCount = $listlength(record)
			do $$$AssertEquals(fieldCount, 6, "FAMILY #"_i_" ("_tree_") má "_fieldCount_" polí, čekáno 6")
		}
	}
}

/// EXE záznamy musí mít 11 polí dle wire-format kontraktu (i když volání AddExe dlouhý
/// název neuvádí - default parametru ho doplní, aby byl řádek uniformní).
Method TestExeRecordsHaveElevenFields()
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
		do $$$AssertEquals(fieldCount, 11, "EXE record #"_i_" has "_fieldCount_" fields, expected 11")
	}
}

/// Dlouhý název (legacy nameEx) musí přežít regeneraci z XML - bez něj by plocha i menu
/// spuštěných modulů tiše spadly na krátký název. Pin na "Fakturace" ze zpracování AD.
Method TestLongNameSurvivesGeneration()
{
	do ..SetupSys(.sys)
	set ^||ML("IN","tree") = "main"
	do ##class(ALVA.EDISONMAIN2.API.Tree).GetTree(.sys)

	set i = "", withLongName = 0, found = 0
	for {
		set i = $order(^||ML("OUT", i))
		quit:i=""
		continue:i'?1.N
		set record = ^||ML("OUT", i)
		if ($listget(record, 1) '= "EXE") { continue }
		if ($listget(record, 11) '= "") { set withLongName = withLongName + 1 }
		if ($listget(record, 11) = "Fakturace - zpracování AD") { set found = 1 }
	}
	do $$$AssertNotTrue((withLongName = 0), "Main strom musí mít EXE s neprázdným dlouhým názvem (pole 11)")
	do $$$AssertTrue(found, "Fakturace ze zpracování AD musí mít dlouhý název ""Fakturace - zpracování AD""")
}

/// Wire hodnoty (ne jen počet polí): AddExe/AddFamily mapuje %Boolean → "1"/"0" string,
/// na kterém závisí .NET parser (row.GetString(n)=="1"). Kontroluje mlineOnly EXE
/// "Doklady OLD" (92101, mlineOnly=1) v main stromu - field 10="1", hidden field 8="0".
Method TestMlineOnlyExeHasWireFlagsSet()
{
	do ..SetupSys(.sys)
	set ^||ML("IN","tree") = "main"
	do ##class(ALVA.EDISONMAIN2.API.Tree).GetTree(.sys)

	set i = "", found = 0
	for {
		set i = $order(^||ML("OUT", i))
		quit:i=""
		continue:i'?1.N
		set record = ^||ML("OUT", i)
		if ($listget(record, 1) '= "EXE") { continue }
		if ($listget(record, 5) '= "92101") { continue }
		set found = 1
		do $$$AssertEquals($listget(record, 10), "1", "mlineOnly EXE 92101 musí mít field 10 = ""1""")
		do $$$AssertEquals($listget(record, 8), "0", "EXE 92101 není hidden -> field 8 = ""0""")
	}
	do $$$AssertTrue(found, "mlineOnly EXE 92101 (Doklady OLD) musí být v main stromu")
}

/// Licencovaná složka: alespoň jedna FAMILY v main má licensed field 5 = "1"
/// (package ID gate - legacy BuildTreeXML kontroloval složky proti licenci stejně jako formy).
Method TestMainTreeHasLicensedFamily()
{
	do ..SetupSys(.sys)
	set ^||ML("IN","tree") = "main"
	do ##class(ALVA.EDISONMAIN2.API.Tree).GetTree(.sys)

	set i = "", licensedCount = 0
	for {
		set i = $order(^||ML("OUT", i))
		quit:i=""
		continue:i'?1.N
		set record = ^||ML("OUT", i)
		if ($listget(record, 1) '= "FAMILY") { continue }
		if ($listget(record, 5) = "1") { set licensedCount = licensedCount + 1 }
	}
	do $$$AssertNotTrue((licensedCount = 0), "Main strom musí mít aspoň jednu licencovanou FAMILY (field 5 = ""1"")")
}

}
