Include (%occInclude, EDInputForm)

Class ALVA.EDControls.Tests.FormTest Extends ALVA.EDTesting.MLTestCase
{

/// %UnitTest pro ALVA.EDControls.EDInputForm.Form - round-trip WriteToOut/ReadFromIn,
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

	// .NET by OUT poslal zpět jako IN - zkopíruj numerické i string subscripty.
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
/// a round-trip je zparsuje zpět. Položka bez ~label dostane label = key.
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

/// Decimal format (pozice 17) round-trip.
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

/// Postaví minimální formulář v ^||ML("IN") a vrátí ho přečtený s daným triggerem.
/// Trigger i changedKey jsou wire klíče (^||ML("IN","trigger") / ("changedKey")) - stejné
/// jméno posílá C# strana, takže test drží i tenhle kontrakt.
Method ReadWithTrigger(pTrigger As %String, pChangedKey As %String = "") As ALVA.EDControls.EDInputForm.Form
{
	s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()
	d form.AddGroup("g", "Skupina")
	d form.AddText("t", "Text", "hello")
	d form.AddText("u", "Text2", "world")
	d form.WriteToOut()
	s idx = ""
	f { s idx = $o(^||ML("OUT", idx)) q:idx=""  s ^||ML("IN", idx) = ^||ML("OUT", idx) }
	s ^||ML("IN", "trigger") = pTrigger
	s ^||ML("IN", "changedKey") = pChangedKey
	ret ##class(ALVA.EDControls.EDInputForm.Form).ReadFromIn()
}

/// Change validaci spouští konkrétní pole - helpery ho musí najít a Init/BeforeSave neplést.
Method TestChangeValidationHelpers()
{
	s form = ..ReadWithTrigger("Change", "u")

	do $$$AssertEquals(form.IsChangeValidation(), 1)
	do $$$AssertEquals(form.IsInitValidation(), 0)
	do $$$AssertEquals(form.IsBeforeSaveValidation(), 0)
	do $$$AssertEquals(form.IsWholeFormValidation(), 0, "Change je změna jednoho pole")
	do $$$AssertEquals(form.HasChangedField(), 1)
	do $$$AssertEquals(form.GetChangedField().key, "u", "GetChangedField vrací pole, které změnu spustilo")
	do $$$AssertEquals(form.GetChangedField().value, "world")
}

/// Ruční Change bez konkrétního pole (změnu nespustilo pole) - žádné changed field.
Method TestChangeValidationWithoutChangedKey()
{
	s form = ..ReadWithTrigger("Change", "")

	do $$$AssertEquals(form.IsChangeValidation(), 1)
	do $$$AssertEquals(form.HasChangedField(), 0)
	do $$$AssertEquals(form.GetChangedField(), "", "Bez changedKey není co vrátit")
}

/// Init i BeforeSave jsou round-trip celého formuláře, ne změna pole.
Method TestWholeFormValidationTriggers()
{
	s init = ..ReadWithTrigger("Init")
	do $$$AssertEquals(init.IsInitValidation(), 1)
	do $$$AssertEquals(init.IsWholeFormValidation(), 1)
	do $$$AssertEquals(init.IsChangeValidation(), 0)
	do $$$AssertEquals(init.HasChangedField(), 0)

	k ^||ML("IN"), ^||ML("OUT")
	s save = ..ReadWithTrigger("BeforeSave")
	do $$$AssertEquals(save.IsBeforeSaveValidation(), 1)
	do $$$AssertEquals(save.IsWholeFormValidation(), 1)
	do $$$AssertEquals(save.IsInitValidation(), 0)
}

/// Konstanty triggeru zrcadlí C# ValidationTrigger - hodnoty jdou po wire jako stringy,
/// takže přejmenování na jedné straně rozbije validace bez chyby při buildu.
Method TestTriggerConstantsMatchWireContract()
{
	do $$$AssertEquals(##class(ALVA.EDControls.EDInputForm.Form).#TriggerInit, "Init")
	do $$$AssertEquals(##class(ALVA.EDControls.EDInputForm.Form).#TriggerChange, "Change")
	do $$$AssertEquals(##class(ALVA.EDControls.EDInputForm.Form).#TriggerBeforeSave, "BeforeSave")
}

/// Neznámý trigger nesmí projít jako Change ani jako whole-form - jinak by se validace
/// pustila v režimu, na který není napsaná.
Method TestUnknownTriggerMatchesNothing()
{
	s form = ..ReadWithTrigger("")

	do $$$AssertEquals(form.IsInitValidation(), 0)
	do $$$AssertEquals(form.IsChangeValidation(), 0)
	do $$$AssertEquals(form.IsBeforeSaveValidation(), 0)
	do $$$AssertEquals(form.IsWholeFormValidation(), 0)
}

/// AddFieldToSpecificGroup s neexistujícím group key -> fallback append na konec.
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

/// Round-trip FIELD pozic 10-12, 14 a 19 (editable/visibility/helpText/fileFilter/placeholders)
/// + GROUP pozice 6 (helpText). Formulář chodí tam a zpět (Validate) - co se tady ztratí,
/// zmizí uživateli po prvním kole (viz komentář k pozici 19 v Form.ReadFromIn).
Method TestFieldExtrasRoundTrip()
{
	s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()
	d form.AddGroup("g", "Skupina")
	s form.GetGroup("g").helpText = "nápověda skupiny"
	s f = form.AddText("t", "Text", "hodnota")
	s f.editable = 0
	s f.visibility = $$$ItemVisibilityUserHidden
	s f.helpText = "nápověda pole"
	s f.placeholders = 1
	s ff = form.AddFieldToLastGroup("soubor", "Soubor", $$$FieldTypeFile)
	s ff.fileFilter = $$$FileFilterFolder

	d form.WriteToOut()
	s idx = ""
	f { s idx = $o(^||ML("OUT", idx)) q:idx=""  s ^||ML("IN", idx) = ^||ML("OUT", idx) }

	s form2 = ##class(ALVA.EDControls.EDInputForm.Form).ReadFromIn()
	do $$$AssertEquals(form2.GetGroup("g").helpText, "nápověda skupiny", "GROUP helpText (pozice 6)")
	s t2 = form2.GetField("t")
	do $$$AssertEquals(t2.editable, 0, "editable (pozice 10)")
	do $$$AssertEquals(t2.visibility, $$$ItemVisibilityUserHidden, "visibility (pozice 11)")
	do $$$AssertEquals(t2.helpText, "nápověda pole", "helpText (pozice 12)")
	do $$$AssertEquals(t2.placeholders, 1, "placeholders (pozice 19) nesmí zpáteční cestou zmizet")
	do $$$AssertEquals(form2.GetField("soubor").fileFilter, $$$FileFilterFolder, "fileFilter (pozice 14)")
}

/// helpTextMode: bez IN subscriptu default tooltip, explicitní column se round-tripuje
/// (WriteToOut píše string subscript "helpTextMode", ReadFromIn ho čte s fallbackem).
Method TestHelpTextModeRoundTrip()
{
	s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()
	d form.AddGroup("g", "Skupina")
	d form.AddText("t", "Text")
	s form.helpTextMode = $$$HelpTextModeColumn

	d form.WriteToOut()
	do $$$AssertEquals(^||ML("OUT","helpTextMode"), $$$HelpTextModeColumn, "WriteToOut píše string subscript")
	s idx = ""
	f { s idx = $o(^||ML("OUT", idx)) q:idx=""  s ^||ML("IN", idx) = ^||ML("OUT", idx) }

	do $$$AssertEquals(##class(ALVA.EDControls.EDInputForm.Form).ReadFromIn().helpTextMode, $$$HelpTextModeColumn, "column round-trip")

	k ^||ML("IN")
	s ^||ML("IN",1) = $lb("FIELD",1,"k","L",$$$FieldTypeText,"v",0,0,"")
	do $$$AssertEquals(##class(ALVA.EDControls.EDInputForm.Form).ReadFromIn().helpTextMode, $$$HelpTextModeTooltip, "bez subscriptu default tooltip")
}

/// ReadFromIn → WriteToOut vrátí řádky z IN beze změny, pro pole každého typu. Každá pozice ze Serialize dostane
/// jinou hodnotu, takže pozici, kterou Serialize zapíše a ReadFromIn nepřečte (B44: pozice 19) nebo přečte
/// z vedlejší pozice, test shodí bez úprav. Výjimka: validationError (pozice 9) ReadFromIn vědomě nečte.
Method TestReadFromInWriteToOutKeepsEveryPosition()
{
	s types = $lb($$$FieldTypeText,$$$FieldTypeNumber,$$$FieldTypeDate,$$$FieldTypeDecimal,$$$FieldTypeBool,$$$FieldTypeButton,$$$FieldTypeList,$$$FieldTypeFile,$$$FieldTypeColor,$$$FieldTypeComboBox,$$$FieldTypeImage)
	s groupLen = $ll(##class(ALVA.EDControls.EDInputForm.Group).%New().Serialize())
	s fieldLen = $ll(##class(ALVA.EDControls.EDInputForm.Field).%New().Serialize())
	s row = $lb("GROUP", 0, "g")
	f p=4:1:groupLen s $li(row,p) = "g"_p
	s ^||ML("IN",1) = row
	f i=1:1:$ll(types) {
		s row = $lb("FIELD", i, "pole"_i)
		f p=4:1:fieldLen s $li(row,p) = "v"_i_"_"_p
		// Typ musí být platný literál, listItems ve tvaru, který Serialize zapíše zpět beze změny.
		s $li(row,5) = $li(types,i), $li(row,13) = "a~A"_i_"|b~B"_i
		s ^||ML("IN",i+1) = row
	}

	d ##class(ALVA.EDControls.EDInputForm.Form).ReadFromIn().WriteToOut()

	s idx = "", rows = 0
	f {
		s idx = $o(^||ML("IN",idx))
		q:idx=""
		s rows = rows + 1
		s a = ^||ML("IN",idx), b = $g(^||ML("OUT",idx))
		i $li(a,1)="FIELD" {
			do $$$AssertEquals($li(b,9), "", "řádek "_idx_": validationError se ze vstupu nečte")
			s $li(a,9) = "", $li(b,9) = ""
		}
		do $$$AssertTrue($listsame(a,b), "řádek "_idx_" ("_$li(a,3)_") se po round-tripu liší")
	}
	s idx = "", out = 0
	f {
		s idx = $o(^||ML("OUT",idx))
		q:idx=""
		continue:idx'=+idx
		s out = out + 1
	}
	do $$$AssertEquals(rows, 1+$ll(types), "skupina + pole každého typu")
	do $$$AssertEquals(out, rows, "OUT má stejně řádků jako IN")
}

/// Pojmenovaný klíč v IN, který server nezná (novější klient), ReadFromIn přeskočí a položky přečte dál.
Method TestReadFromInSkipsUnknownNamedKeys()
{
	s ^||ML("IN",1) = $lb("FIELD",1,"k","L",$$$FieldTypeText,"v",0,0,"")
	s ^||ML("IN","x") = "neznámý klíč"

	s form = ##class(ALVA.EDControls.EDInputForm.Form).ReadFromIn()

	do $$$AssertEquals(form.items.Count(), 1, "jen položka z číselného subscriptu")
	do $$$AssertEquals(form.GetField("k").value, "v")
}

/// Zpětná kompatibilita: starší klient pošle krátký $lb (GROUP 4 prvky, FIELD 9 prvků) -
/// ReadFromIn nesmí spadnout a chybějící pozice dostanou defaulty ($ll>=N větve se přeskočí).
Method TestShortLegacyListsGetDefaults()
{
	s ^||ML("IN",1) = $lb("GROUP",0,"g","G")
	s ^||ML("IN",2) = $lb("FIELD",1,"k","L",$$$FieldTypeText,"v",0,1,"")

	s form = ##class(ALVA.EDControls.EDInputForm.Form).ReadFromIn()

	do $$$AssertEquals(form.GetGroup("g").visibility, $$$ItemVisibilityVisible, "GROUP bez pozice 5 -> Visible")
	s f = form.GetField("k")
	do $$$AssertEquals(f.value, "v")
	do $$$AssertEquals(f.required, 1)
	do $$$AssertEquals(f.editable, 1, "bez pozice 10 -> editable default")
	do $$$AssertEquals(f.visibility, $$$ItemVisibilityVisible, "bez pozice 11 -> Visible")
	do $$$AssertEquals(f.placeholders, 0, "bez pozice 19 -> bez placeholderů")
	do $$$AssertEquals(f.listItems.Count(), 0, "bez pozice 13 -> žádné list items")
}

/// GetField/GetGroup s neexistujícím klíčem vrací "" - volající na to spoléhá (viz UserProfile).
Method TestGetFieldGetGroupUnknownKey()
{
	s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()
	d form.AddGroup("g", "Skupina")
	d form.AddText("t", "Text")

	do $$$AssertEquals(form.GetField("neexistuje"), "", "GetField neznámý klíč -> prázdno")
	do $$$AssertEquals(form.GetGroup("neexistuje"), "", "GetGroup neznámý klíč -> prázdno")
	do $$$AssertEquals(form.GetField("g"), "", "GetField nenajde skupinu")
	do $$$AssertEquals(form.GetGroup("t"), "", "GetGroup nenajde pole")
}

/// Builder metody nastavují správný wire typ pole.
Method TestBuilderMethodsSetFieldType()
{
	s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()
	d form.AddGroup("g", "Skupina")

	do $$$AssertEquals(form.AddText("t", "T").type, $$$FieldTypeText)
	do $$$AssertEquals(form.AddBool("b", "B").type, $$$FieldTypeBool)
	do $$$AssertEquals(form.AddNumber("n", "N").type, $$$FieldTypeNumber)
	do $$$AssertEquals(form.AddDecimal("d", "D").type, $$$FieldTypeDecimal)
}

/// Parita konstant wire kontraktu s C# stranou (FieldTypeWire/ItemVisibilityWire/
/// ParseHelpTextMode testy v EDControls.Tests pinují tytéž hodnoty). Hodnoty jdou po
/// drátě jako stringy - přejmenování na jedné straně build nechytí, tenhle pin ano.
Method TestWireConstantsMatchCSharp()
{
	do $$$AssertEquals($$$FieldTypeText, "TEXT")
	do $$$AssertEquals($$$FieldTypeNumber, "NUMBER")
	do $$$AssertEquals($$$FieldTypeDate, "DATE")
	do $$$AssertEquals($$$FieldTypeDecimal, "DECIMAL")
	do $$$AssertEquals($$$FieldTypeBool, "BOOL")
	do $$$AssertEquals($$$FieldTypeButton, "BUTTON")
	do $$$AssertEquals($$$FieldTypeList, "LIST")
	do $$$AssertEquals($$$FieldTypeFile, "FILE")
	do $$$AssertEquals($$$FieldTypeColor, "COLOR")
	do $$$AssertEquals($$$FieldTypeComboBox, "COMBOBOX")
	do $$$AssertEquals($$$FieldTypeImage, "IMAGE")
	do $$$AssertEquals($$$FileFilterFolder, "$FOLDER")
	do $$$AssertEquals($$$ListItemSep, "|")
	do $$$AssertEquals($$$ListKeyValSep, "~")
	do $$$AssertEquals($$$ItemVisibilityVisible, "0")
	do $$$AssertEquals($$$ItemVisibilityCollapsed, "1")
	do $$$AssertEquals($$$ItemVisibilityUserHidden, "2")
	do $$$AssertEquals($$$ItemVisibilityProgrammerHidden, "3")
	do $$$AssertEquals($$$HelpTextModeTooltip, "tooltip")
	do $$$AssertEquals($$$HelpTextModeColumn, "column")
}

}
