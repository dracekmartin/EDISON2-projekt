Include (%occInclude, EDInputForm)

Class ALVA.EDControls.Tests.FormTest Extends ALVA.EDTesting.MLTestCase
{

/// %UnitTest pro ALVA.EDControls.EDInputForm.Form — round-trip WriteToOut/ReadFromIn,
/// Renumber (čísla polí napříč skupinami) a AddFieldToSpecificGroup (pozice vložení).

/// Postaví form, WriteToOut, simuluje .NET round-trip (OUT→IN), ReadFromIn a ověří zachování.
Method TestWriteReadRoundTrip()
{
	s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()
	d form.AddGroup("g", "Skupina")
	d form.AddText("t", "Text", "hello")
	d form.AddBool("b", "Bool", 1)
	d form.AddNumber("n", "Num", "5", "0", "10", "ks", 1)

	d form.WriteToOut()

	// .NET by OUT poslal zpět jako IN — zkopíruj numerické i string subscripty.
	s idx = ""
	f { s idx = $o(^||ML("OUT", idx)) q:idx=""  s ^||ML("IN", idx) = ^||ML("OUT", idx) }
	s ^||ML("IN", "trigger") = "BeforeSave"

	s form2 = ##class(ALVA.EDControls.EDInputForm.Form).ReadFromIn()

	do $$$AssertEquals(form2.items.Count(), 4, "1 group + 3 fields")
	do $$$AssertEquals(form2.trigger, "BeforeSave")
	do $$$AssertEquals(form2.GetGroup("g").groupName, "Skupina")
	do $$$AssertEquals(form2.GetField("t").value, "hello")
	do $$$AssertEquals(form2.GetField("b").value, 1)
	s nf = form2.GetField("n")
	do $$$AssertEquals(nf.value, 5)
	do $$$AssertEquals(nf.required, 1)
	do $$$AssertEquals(nf.min, 0)
	do $$$AssertEquals(nf.max, 10)
	do $$$AssertEquals(nf.unit, "ks")
}

/// Pole se číslují sekvenčně napříč skupinami (1,2,3...), skupiny mají number 0.
Method TestRenumberAcrossGroups()
{
	s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()
	d form.AddGroup("g1", "G1")
	d form.AddText("a", "A")
	d form.AddText("b", "B")
	d form.AddGroup("g2", "G2")
	d form.AddText("c", "C")

	do $$$AssertEquals(form.GetGroup("g1").number, 0, "groups unnumbered")
	do $$$AssertEquals(form.GetGroup("g2").number, 0)
	do $$$AssertEquals(form.GetField("a").number, 1)
	do $$$AssertEquals(form.GetField("b").number, 2)
	do $$$AssertEquals(form.GetField("c").number, 3)
}

/// AddFieldToSpecificGroup vloží pole za poslední pole cílové skupiny (před další skupinu).
Method TestAddFieldToSpecificGroupInsertsAfterGroupFields()
{
	s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()
	d form.AddGroup("g1", "G1")
	d form.AddText("a", "A")
	d form.AddGroup("g2", "G2")
	d form.AddText("c", "C")

	d form.AddFieldToSpecificGroup("g1", "b", "B")

	// Pořadí: g1, a, b, g2, c
	do $$$AssertEquals(form.items.GetAt(1).key, "g1")
	do $$$AssertEquals(form.items.GetAt(2).key, "a")
	do $$$AssertEquals(form.items.GetAt(3).key, "b")
	do $$$AssertEquals(form.items.GetAt(4).key, "g2")
	do $$$AssertEquals(form.items.GetAt(5).key, "c")
	// Přečíslováno: a=1, b=2, c=3
	do $$$AssertEquals(form.GetField("b").number, 2)
	do $$$AssertEquals(form.GetField("c").number, 3)
}

/// List field: listItems (pozice 13) se serializují přes $$$ListItemSep/$$$ListKeyValSep
/// a round-trip je zparsuje zpět. Položka bez ~label dostane label = key (Form.cls:286).
/// Toto je |/~ wire kontrakt sdílený s C# ParseItems + EDInputForm.inc.
Method TestListItemsRoundTrip()
{
	s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()
	d form.AddGroup("g", "Skupina")
	s lf = form.AddFieldToLastGroup("sel", "Výběr", $$$FieldTypeList)
	d lf.AddListItem("A", "Alfa")
	d lf.AddListItem("B", "")

	d form.WriteToOut()
	s idx = ""
	f { s idx = $o(^||ML("OUT", idx)) q:idx=""  s ^||ML("IN", idx) = ^||ML("OUT", idx) }

	s form2 = ##class(ALVA.EDControls.EDInputForm.Form).ReadFromIn()
	s lf2 = form2.GetField("sel")
	do $$$AssertEquals(lf2.listItems.Count(), 2, "2 list items round-trip")
	do $$$AssertEquals($li(lf2.listItems.GetAt(1), 1), "A", "1. položka key")
	do $$$AssertEquals($li(lf2.listItems.GetAt(1), 2), "Alfa", "1. položka label")
	do $$$AssertEquals($li(lf2.listItems.GetAt(2), 1), "B", "2. položka key")
	do $$$AssertEquals($li(lf2.listItems.GetAt(2), 2), "B", "prázdný label -> label = key")
}

/// Decimal format (pozice 17) round-trip — dosud se testovalo jen min/max/unit.
Method TestDecimalFormatRoundTrip()
{
	s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()
	d form.AddGroup("g", "Skupina")
	d form.AddDecimal("d", "Dec", "1.5", "0", "9.9", "mm", "N2")

	d form.WriteToOut()
	s idx = ""
	f { s idx = $o(^||ML("OUT", idx)) q:idx=""  s ^||ML("IN", idx) = ^||ML("OUT", idx) }

	s df = ##class(ALVA.EDControls.EDInputForm.Form).ReadFromIn().GetField("d")
	do $$$AssertEquals(df.format, "N2", "format round-trip (pozice 17)")
	do $$$AssertEquals(df.unit, "mm", "unit round-trip")
	do $$$AssertEquals(df.min, 0, "min round-trip")
	do $$$AssertEquals(df.max, 9.9, "max round-trip")
}

/// AddFieldToSpecificGroup s neexistujícím group key -> fallback append na konec (Form.cls:135).
Method TestAddFieldToSpecificGroupUnknownGroupAppends()
{
	s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()
	d form.AddGroup("g1", "G1")
	d form.AddText("a", "A")

	d form.AddFieldToSpecificGroup("neexistuje", "z", "Z")

	// Pořadí: g1, a, z (append na konec)
	do $$$AssertEquals(form.items.GetAt(3).key, "z", "Neznámá skupina -> field na konci")
	do $$$AssertEquals(form.GetField("z").number, 2, "z přečíslováno na 2")
}

}
