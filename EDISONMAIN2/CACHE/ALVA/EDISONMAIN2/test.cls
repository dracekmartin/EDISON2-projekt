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

		d form.AddFieldToLastGroup("poznamka", "Poznámka", $$$FieldTypeText, 0, 0)

		d form.AddGroup("osobni", "Osobní údaje")
		d form.AddFieldToLastGroup("jmeno",    "Jméno",    $$$FieldTypeText,   1, 1)
		d form.AddFieldToLastGroup("prijmeni", "Příjmení", $$$FieldTypeText,   1, 1)
		d form.AddFieldToLastGroup("vek",      "Věk",      $$$FieldTypeNumber, 1, 0)

		d form.AddGroup("adresa", "Adresa")
		d form.AddFieldToLastGroup("ulice", "Ulice", $$$FieldTypeText,   0, 0)
		d form.AddFieldToLastGroup("mesto", "Město", $$$FieldTypeText,   0, 0)
		d form.AddFieldToLastGroup("psc",   "PSČ",   $$$FieldTypeNumber, 1, 0)

		d form.AddGroup("kontakt", "Kontakt")
		d form.AddFieldToLastGroup("email",   "E-mail",  $$$FieldTypeText,   0, 0)
		d form.AddFieldToLastGroup("telefon", "Telefon", $$$FieldTypeNumber, 0, 0)

		d form.AddGroup("dalsi", "Další údaje")
		s f = form.AddFieldToLastGroup("narozen", "Narozen",  $$$FieldTypeDate,    0, 0)
		s f.value = "2024-05-15"
		s f = form.AddFieldToLastGroup("plat",    "Plat",     $$$FieldTypeDecimal, 0, 0)
		s f.value = "45000.50"
		d form.AddFieldToLastGroup("aktivni",  "Aktivní",  $$$FieldTypeBool, 0, 0)
		d form.AddFieldToLastGroup("smazano",  "Smazáno",  $$$FieldTypeBool, 0, 0)

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

		s f = form.GetField("jmeno")
		i f.value="" s f.validationError = "Jméno je povinné"

		s f = form.GetField("prijmeni")
		i f.value="" s f.validationError = "Příjmení je povinné"
		
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
