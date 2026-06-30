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

}
