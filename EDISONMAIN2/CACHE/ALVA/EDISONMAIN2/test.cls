Include GENERAL

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

		d form.AddGroup("Osobní údaje")
		d form.AddField("jmeno",    "Jméno",    "TEXT",   1, 1)
		d form.AddField("prijmeni", "Příjmení", "TEXT",   1, 1)
		d form.AddField("vek",      "Věk",      "NUMBER", 1, 0)

		d form.AddGroup("Adresa")
		d form.AddField("ulice", "Ulice", "TEXT",   0, 0)
		d form.AddField("mesto", "Město", "TEXT",   0, 0)
		d form.AddField("psc",   "PSČ",   "NUMBER", 1, 0)

		d form.AddGroup("Kontakt")
		d form.AddField("email",   "E-mail",  "TEXT",   0, 0)
		d form.AddField("telefon", "Telefon", "NUMBER", 0, 0)

		d form.WriteToOut(del)
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

		s form = ##class(ALVA.EDControls.EDInputForm.Form).ReadFromIn(del)

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
		
		d form.WriteToOut(del)
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

		s form = ##class(ALVA.EDControls.EDInputForm.Form).ReadFromIn(del)

		k ^EDISONMAIN2Test(userID)
		s cnt = form.items.Count()
		f idx=1:1:cnt {
			s item = form.items.GetAt(idx)
			i item.%IsA("ALVA.EDControls.EDInputForm.Field") {
				s ^EDISONMAIN2Test(userID, item.key) = item.value
			}
		}

		d form.WriteToOut(del)
	}
	catch (ex) { $$$CatchErrRest }
	ret ret
}

}
