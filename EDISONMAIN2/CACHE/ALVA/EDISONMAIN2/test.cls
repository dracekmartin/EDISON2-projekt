Include (GENERAL, EDInputForm)

Class ALVA.EDISONMAIN2.test Extends %RegisteredObject
{

/// Returns the form definition for InputForm on the .NET side.
ClassMethod Init(ByRef sys As %String) As %Status
{
	$$$Init
	try
	{
		$$$FillParams

		s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()

		s f = form.AddFieldToLastGroup("toggleHelp", "Režim nápovědy", $$$FieldTypeButton, 0, 0)
		s f.value = "Přepnout na sloupec"

		d form.AddFieldToLastGroup("poznamka", "Poznámka", $$$FieldTypeText, 0, 0)

		d form.AddGroup("osobni", "Osobní údaje")
		s form.GetGroup("osobni").helpText = "Vyplňte osobní údaje cestujícího"
		s f = form.AddFieldToLastGroup("jmeno",    "Jméno",    $$$FieldTypeText,   1, 1)
		s f.helpText = "Křestní jméno cestujícího"
		s f = form.AddFieldToLastGroup("prijmeni", "Příjmení", $$$FieldTypeText,   1, 1)
		s f.helpText = "Příjmení bez titulů"
		d form.AddFieldToLastGroup("vek",      "Věk",      $$$FieldTypeNumber, 1, 0)

		d form.AddGroup("adresa", "Adresa")
		d form.AddFieldToLastGroup("ulice", "Ulice", $$$FieldTypeText,   0, 0)
		d form.AddFieldToLastGroup("mesto", "Město", $$$FieldTypeText,   0, 0)
		s f = form.AddFieldToLastGroup("psc",   "PSČ",   $$$FieldTypeNumber, 1, 0)
		s f.helpText = "Poštovní směrovací číslo (5 číslic)"

		d form.AddGroup("kontakt", "Kontakt")
		s f = form.AddFieldToLastGroup("email",   "E-mail",  $$$FieldTypeText,   0, 0)
		s f.helpText = "Kontaktní e-mailová adresa"
		d form.AddFieldToLastGroup("telefon", "Telefon", $$$FieldTypeNumber, 0, 0)

		d form.AddGroup("dalsi", "Další údaje")
		s f = form.AddFieldToLastGroup("narozen", "Narozen",  $$$FieldTypeDate,    0, 0)
		s f.value = "2024-05-15"
		s f = form.AddFieldToLastGroup("plat",    "Plat",     $$$FieldTypeDecimal, 0, 0)
		s f.value = "45000.50"
		d form.AddFieldToLastGroup("aktivni",  "Aktivní",  $$$FieldTypeBool, 0, 0)
		d form.AddFieldToLastGroup("smazano",  "Smazáno",  $$$FieldTypeBool, 0, 0)
		s f = form.AddFieldToLastGroup("stav", "Stav", $$$FieldTypeList, 1, 0)
		d f.AddListItem("A", "Aktivní")
		d f.AddListItem("N", "Neaktivní")
		d f.AddListItem("P", "Pozastavený")
		s f.value = "A"

		s f = form.AddFieldToLastGroup("defaults", "Výchozí hodnoty", $$$FieldTypeButton, 0, 0)
		s f.value = "Vyplnit výchozí"

		d form.AddGroup("nemoc", "Nemoc")
		s form.GetGroup("nemoc").helpText = "Vyplňte pouze v případě nemoci"
		d form.AddFieldToLastGroup("nemocny", "Nemocný", $$$FieldTypeBool, 1, 0)
		s f = form.AddFieldToLastGroup("od", "Od", $$$FieldTypeDate, 0, 0)
		s f.editable = 0
		s f = form.AddFieldToLastGroup("do", "Do", $$$FieldTypeDate, 0, 0)
		s f.editable = 0

		d form.WriteToOut()
	}
	catch (ex) { $$$CatchErrRest }
	ret ret
}

/// Validates submitted field values and returns the full updated form.
ClassMethod Validate(ByRef sys As %String) As %Status
{
	$$$Init
	try
	{
		$$$FillParams

		s form = ##class(ALVA.EDControls.EDInputForm.Form).ReadFromIn()

		// Format / business rules — run on Change and BeforeSave, skip on Init
		i 'form.IsInitValidation() {
			s f = form.GetField("vek")
			i f.value'="" {
				i +f.value<=0 s f.validationError = "Věk musí být kladné číslo"
			}

			s f = form.GetField("psc")
			i f.value'="" {
				i $l(f.value)'=5 s f.validationError = "PSČ musí mít 5 číslic"
			}
		}

		// "Přepnout režim nápovědy" button — toggles helpTextMode between tooltip and column
		i form.HasChangedField() && (form.changedKey = "toggleHelp") {
			i form.helpTextMode = $$$HelpTextModeTooltip {
				s form.helpTextMode = $$$HelpTextModeColumn
				s form.GetField("toggleHelp").value = "Přepnout na tooltip"
			} else {
				s form.helpTextMode = $$$HelpTextModeTooltip
				s form.GetField("toggleHelp").value = "Přepnout na sloupec"
			}
		}

		// "Vyplnit výchozí" button — fills default values
		i form.HasChangedField() && (form.changedKey = "defaults") {
			s form.GetField("jmeno").value = "Jan"
			s form.GetField("prijmeni").value = "Novák"
			s form.GetField("vek").value = "30"
			s form.GetField("ulice").value = "Hlavní 1"
			s form.GetField("mesto").value = "Praha"
			s form.GetField("psc").value = "11000"
			s form.GetField("email").value = "jan.novak@example.com"
		}

		s f = form.GetField("jmeno")
		i f.value="" s f.validationError = "Jméno je povinné"

		s f = form.GetField("prijmeni")
		i f.value="" s f.validationError = "Příjmení je povinné"

		// Nemocný checkbox controls editability of Od/Do date fields
		s nemocny = form.GetField("nemocny")
		s fOd = form.GetField("od")
		s fDo = form.GetField("do")
		i nemocny.value="1" {
			s fOd.editable = 1
			s fDo.editable = 1
		} else {
			s fOd.editable = 0
			s fDo.editable = 0
			s fOd.value = ""
			s fDo.value = ""
		}

		d form.WriteToOut()
	}
	catch (ex) { $$$CatchErrRest }
	ret ret
}

/// Saves submitted field values and returns the full updated form.
/// Persists to ^EDISONMAIN2Test(userID, key) for test purposes.
ClassMethod Save(ByRef sys As %String) As %Status
{
	$$$Init
	try
	{
		$$$FillParams

		s form = ##class(ALVA.EDControls.EDInputForm.Form).ReadFromIn()

		k ^EDISONMAIN2Test(userID)
		s cnt = form.items.Count()
		f idx=1:1:cnt {
			s item = form.items.GetAt(idx)
			i item.%IsA("ALVA.EDControls.EDInputForm.Field") {
				s ^EDISONMAIN2Test(userID, item.key) = item.value
			}
		}

		d form.WriteToOut()
	}
	catch (ex) { $$$CatchErrRest }
	ret ret
}

}
