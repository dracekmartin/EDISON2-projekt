Include (GENERAL, EDInputForm)

/// EDInputForm schema pro okno „Vlastnosti uživatele" (self-service profil).
/// Data drží .NET klient: prefill hodnot v Initialized (přehled z MLRest, export adresář
/// z ALVA.EDISON.Main.LoadUser/LoadSettings) a persist osobního adresáře po OK
/// (Main.SaveUser). Tato třída definuje jen strukturu formuláře a validaci
/// (editovatelnost cesty dle přepínače, neprázdnost při ukládání) — nevolá legacy
/// MLRest metody (kolidovaly by na ^||ML). Výchozí okno je read-only pole, jeho
/// název dodá host z modulového stromu shellu.
Class ALVA.EDISONMAIN2.EDInputForm.UserProfile Extends %RegisteredObject
{

ClassMethod Init(ByRef sys As %String) As %Status
{
	$$$Init
	try {
		s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()

		d form.AddGroup("user", "Uživatel")
		s f = form.AddText("fullName", "Plné jméno")           s f.editable = 0
		s f = form.AddText("loginName", "Uživatelské jméno")   s f.editable = 0
		s f = form.AddText("mlp", "ID")                        s f.editable = 0
		s f = form.AddFieldToLastGroup("changePassword", "Heslo", $$$FieldTypeButton)
		s f.value = "Změnit heslo…"
		s f = form.AddFieldToLastGroup("certificates", "Certifikáty", $$$FieldTypeButton)
		s f.value = "Seznam certifikátů…"
		s f = form.AddText("defaultWindow", "Výchozí okno")  s f.editable = 0
		s f.helpText = "Nastavíte pravým klikem na modul ve stromu."

		d form.AddGroup("export", "Export dokumentů")
		// validateOnChange=1 → přepnutí přepočítá editovatelnost cesty (Change validace).
		s f = form.AddBool("usePersonDir", "Použít osobní adresář")
		s f.validateOnChange = 1
		s f = form.AddFieldToLastGroup("personDir", "Osobní adresář", $$$FieldTypeFile)
		s f.fileFilter = $$$FileFilterFolder
		s f = form.AddText("centralDir", "Centrální adresář")  s f.editable = 0

		d form.WriteToOut()
	} catch ex { $$$CatchErrRest }
	ret ret
}

ClassMethod Validate(ByRef sys As %String) As %Status
{
	$$$Init
	try {
		s form = ##class(ALVA.EDControls.EDInputForm.Form).ReadFromIn()

		// Osobní adresář je editovatelný jen když je zaškrtnuto jeho použití.
		s use = +form.GetField("usePersonDir").value
		s pd = form.GetField("personDir")
		s pd.editable = use

		// Neprázdnost při ukládání; existenci složky ověří klient (server nevidí filesystém).
		i form.IsBeforeSaveValidation() {
			i use && (pd.value = "") s pd.validationError = "Zadejte osobní adresář, nebo odškrtněte jeho použití."
		}

		d form.WriteToOut()
	} catch ex { $$$CatchErrRest }
	ret ret
}

/// Persist řeší .NET klient (Main.SaveUser) — Caché Save je no-op (bez WriteToOut,
/// controller si ponechá klientské Items), jen potvrdí úspěch.
ClassMethod Save(ByRef sys As %String) As %Status
{
	$$$Init
	try {
	} catch ex { $$$CatchErrRest }
	ret ret
}

}
