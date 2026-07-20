Include %occInclude

Class ALVA.EDControls.Tests.ListTest Extends ALVA.EDTesting.MLTestCase
{

/// %UnitTest pro ALVA.EDControls.EDList.List — JSON serializace sloupců a řádků do ^||ML("OUT").

Method TestColumnsJson()
{
	s list = ##class(ALVA.EDControls.EDList.List).%New()
	d list.AddColumn("rz", "RZ", "TEXT", 100)
	d list.AddColumn("pocet", "Count", "NUMBER", 80, "RIGHT", 1, "N0")
	d list.WriteColumnsToOut()

	s arr = ##class(%DynamicArray).%FromJSON(^||ML("OUT", 1))
	do $$$AssertEquals(arr.%Size(), 2)
	do $$$AssertEquals(arr.%Get(0).key, "rz")
	do $$$AssertEquals(arr.%Get(0).type, "TEXT")
	do $$$AssertEquals(arr.%Get(1).key, "pocet")
	do $$$AssertEquals(arr.%Get(1).format, "N0")
	do $$$AssertEquals(arr.%Get(1).align, "RIGHT")
	do $$$AssertEquals(arr.%Get(1).sortable, 1)
}

Method TestRowsJson()
{
	s list = ##class(ALVA.EDControls.EDList.List).%New()
	s r1 = list.AddRow("42")
	s r1.nazev = "Praha"
	s r2 = list.AddRow("43")
	s r2.nazev = "Brno"
	d list.WriteRowsToOut()

	s o1 = ##class(%DynamicObject).%FromJSON(^||ML("OUT", 1))
	s o2 = ##class(%DynamicObject).%FromJSON(^||ML("OUT", 2))
	do $$$AssertEquals(o1.id, "42")
	do $$$AssertEquals(o1.nazev, "Praha")
	do $$$AssertEquals(o2.id, "43")
	do $$$AssertEquals(o2.nazev, "Brno")
}

/// isLink + visible se serializují do JSON sloupce (List.cls:62-63) — live featura (MockList isLink=1).
/// Defaulty: visible=1, sortable=0, width=0.
Method TestColumnLinkAndVisibleFlags()
{
	s list = ##class(ALVA.EDControls.EDList.List).%New()
	s linkCol = list.AddColumn("odkaz", "Odkaz", "TEXT")
	s linkCol.isLink = 1
	d list.AddColumn("plain", "Plain", "TEXT")
	d list.WriteColumnsToOut()

	s arr = ##class(%DynamicArray).%FromJSON(^||ML("OUT", 1))
	do $$$AssertEquals(arr.%Get(0).isLink, 1, "isLink=1 se serializuje")
	do $$$AssertEquals(arr.%Get(0).visible, 1, "visible default = 1")
	do $$$AssertEquals(arr.%Get(1).isLink, 0, "plain sloupec isLink default = 0")
	do $$$AssertEquals(arr.%Get(1).sortable, 0, "sortable default = 0")
	do $$$AssertEquals(arr.%Get(1).width, 0, "width default = 0")
}

/// Reálný protokol: sloupce i řádky v JEDNÉ odpovědi přes sdílený $i(^||ML("OUT")) čítač.
/// Sloupce -> OUT 1, řádky -> OUT 2,3. Pin sdíleného čítače (nejčastější regrese při
/// "opravě" indexace jedné z metod).
Method TestColumnsThenRowsShareOutCounter()
{
	s list = ##class(ALVA.EDControls.EDList.List).%New()
	d list.AddColumn("id", "Id", "TEXT")
	s r1 = list.AddRow("1")
	s r2 = list.AddRow("2")

	d list.WriteColumnsToOut()
	d list.WriteRowsToOut()

	do $$$AssertEquals(^||ML("OUT"), 3, "3 OUT záznamy: 1 sloupce + 2 řádky")
	// OUT(1) je JSON pole sloupců, OUT(2)/OUT(3) jsou objekty řádků.
	s cols = ##class(%DynamicArray).%FromJSON(^||ML("OUT", 1))
	do $$$AssertEquals(cols.%Get(0).key, "id", "OUT(1) = sloupce")
	do $$$AssertEquals(##class(%DynamicObject).%FromJSON(^||ML("OUT", 2)).id, "1", "OUT(2) = 1. řádek")
	do $$$AssertEquals(##class(%DynamicObject).%FromJSON(^||ML("OUT", 3)).id, "2", "OUT(3) = 2. řádek")
}

/// Prázdný list: WriteRowsToOut nic nezapíše, čítač zůstane nedotčený.
Method TestWriteRowsEmptyListWritesNothing()
{
	s list = ##class(ALVA.EDControls.EDList.List).%New()
	d list.WriteRowsToOut()
	do $$$AssertTrue(($data(^||ML("OUT")) = 0), "Prázdný list -> žádný OUT záznam")
}

}
