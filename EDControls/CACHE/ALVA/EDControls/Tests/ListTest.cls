Include (%occInclude, EDList)

Class ALVA.EDControls.Tests.ListTest Extends ALVA.EDTesting.MLTestCase
{

/// %UnitTest pro ALVA.EDControls.EDList.List - JSON serializace sloupců a řádků do ^||ML("OUT").

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

/// isLink + visible se serializují do JSON sloupce (List.cls:62-63) - live featura (MockList isLink=1).
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

/// AddColumn bez align/format serializuje prázdné stringy - C# ParseAlign("") = Auto
/// (zarovnání dle typu) a prázdný format = bez formátování. Pin defaultní cesty.
Method TestColumnDefaultsSerializeEmptyAlignAndFormat()
{
	s list = ##class(ALVA.EDControls.EDList.List).%New()
	d list.AddColumn("cas", "Čas", "HHMM", 60)
	d list.WriteColumnsToOut()

	s col = ##class(%DynamicArray).%FromJSON(^||ML("OUT", 1)).%Get(0)
	do $$$AssertEquals(col.align, "", "bez align -> prázdný string (klient = Auto dle typu)")
	do $$$AssertEquals(col.format, "", "bez format -> prázdný string (bez formátování)")
	do $$$AssertEquals(col.type, "HHMM", "typ se serializuje beze změny")
}

/// Parita konstant wire kontraktu s C# stranou (EDListParsingTests pinují tytéž literály).
/// Hodnoty jdou po drátě jako stringy - přejmenování na jedné straně build nechytí, tenhle pin ano.
Method TestWireConstantsMatchCSharp()
{
	do $$$AssertEquals($$$ListColumnTypeText, "TEXT")
	do $$$AssertEquals($$$ListColumnTypeNumber, "NUMBER")
	do $$$AssertEquals($$$ListColumnTypeDate, "DATE")
	do $$$AssertEquals($$$ListColumnTypeDateTime, "DATETIME")
	do $$$AssertEquals($$$ListColumnTypeBool, "BOOL")
	do $$$AssertEquals($$$ListColumnTypeImage, "IMAGE")
	do $$$AssertEquals($$$ListColumnTypeColor, "COLOR")
	do $$$AssertEquals($$$ListColumnTypeHHMM, "HHMM")
	do $$$AssertEquals($$$ListColumnTypeCodebook, "CODEBOOK")
	do $$$AssertEquals($ll($$$ListColumnTypes), 9, "výčet typů obsahuje všechny konstanty")
	do $$$AssertEquals($$$ListAlignLeft, "LEFT")
	do $$$AssertEquals($$$ListAlignCenter, "CENTER")
	do $$$AssertEquals($$$ListAlignRight, "RIGHT")
	do $$$AssertEquals($ll($$$ListAligns), 3, "výčet zarovnání obsahuje všechny konstanty")
}

/// Překlep v typu by klient tiše zobrazil jako TEXT s lexikografickým řazením - server ho musí odmítnout.
Method TestAddColumnRejectsUnknownType()
{
	s list = ##class(ALVA.EDControls.EDList.List).%New()
	s thrown = 0
	try { d list.AddColumn("d", "Datum", "DAET") } catch ex { s thrown = 1 }
	do $$$AssertEquals(thrown, 1, "neznámý typ -> výjimka")
	do $$$AssertEquals(list.columns.Count(), 0, "odmítnutý sloupec se nepřidá")
}

/// Typ nastavený přímo na sloupci (vzor s col.isLink = 1) obejde AddColumn - zachytit ho musí zápis.
Method TestWriteColumnsRejectsTypeSetDirectly()
{
	s list = ##class(ALVA.EDControls.EDList.List).%New()
	s col = list.AddColumn("d", "Datum", $$$ListColumnTypeDate)
	s col.type = "DAET"
	s thrown = 0
	try { d list.WriteColumnsToOut() } catch ex { s thrown = 1 }
	do $$$AssertEquals(thrown, 1, "neznámý typ nastavený přímo -> výjimka při zápisu")
	do $$$AssertTrue(($data(^||ML("OUT")) = 0), "nic se nezapíše")
}

Method TestAddColumnRejectsUnknownAlign()
{
	s list = ##class(ALVA.EDControls.EDList.List).%New()
	s thrown = 0
	try { d list.AddColumn("n", "Počet", $$$ListColumnTypeNumber, 80, "RIHGT") } catch ex { s thrown = 1 }
	do $$$AssertEquals(thrown, 1, "neznámé zarovnání -> výjimka")
}

/// Každá konstanta projde a malá písmena taky (C# ParseType/ParseAlign jsou case-insensitive).
Method TestAddColumnAcceptsKnownValues()
{
	s list = ##class(ALVA.EDControls.EDList.List).%New()
	f i=1:1:$ll($$$ListColumnTypes) d list.AddColumn("c"_i, "C", $li($$$ListColumnTypes, i))
	f i=1:1:$ll($$$ListAligns) d list.AddColumn("a"_i, "A", $$$ListColumnTypeText, 0, $li($$$ListAligns, i))
	d list.AddColumn("lower", "L", "number", 0, "right")
	d list.AddColumn("empty", "E", "")
	do $$$AssertEquals(list.columns.Count(), $ll($$$ListColumnTypes) + $ll($$$ListAligns) + 2)
}

}
