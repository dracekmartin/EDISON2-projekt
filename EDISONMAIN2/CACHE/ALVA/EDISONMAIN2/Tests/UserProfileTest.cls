Include %occInclude

/// %UnitTest pro ALVA.EDISONMAIN2.EDInputForm.UserProfile (EDInputForm schema).
/// Ověřuje strukturu formuláře (Init) a validační chování (editovatelnost cesty dle
/// přepínače, neprázdnost při ukládání). Data samotná drží .NET klient — netestuje se tu.
///
/// OnBeforeOneTest (kill ^||ML) + SetupSys zděděny z MLTestCase. Chyba je v návratové
/// hodnotě (ret="1~..."), ne v ^||ML("ERR") — testy zachytávají ret.
Class ALVA.EDISONMAIN2.Tests.UserProfileTest Extends ALVA.EDTesting.MLTestCase
{

/// Sestaví FIELD $lb pro ^||ML("IN") tak, jak ho čte Form.ReadFromIn
/// (pozice: 3=key, 5=type, 6=value, 7=validateOnChange, 10=editable).
ClassMethod Fld(key As %String, type As %String, value As %String, editable As %Integer = 1, voc As %Integer = 0) As %List [ Private ]
{
	q $lb("FIELD", 1, key, key, type, value, voc, 0, "", editable, 0, "")
}

/// Najde v ^||ML("OUT") FIELD záznam daného klíče, nebo "" když není.
ClassMethod OutField(key As %String) As %List [ Private ]
{
	s result = "", i = ""
	f {
		s i = $o(^||ML("OUT", i))
		q:i=""
		continue:i'?1.N
		s rec = ^||ML("OUT", i)
		i ($lg(rec,1)="FIELD") && ($lg(rec,3)=key) s result = rec q
	}
	q result
}

/// Init vytvoří všechna očekávaná pole + správné typy klíčových prvků.
Method TestInitBuildsExpectedFields()
{
	do ..SetupSys(.sys)
	set ret = ##class(ALVA.EDISONMAIN2.EDInputForm.UserProfile).Init(.sys)
	do $$$AssertEquals(+ret, 0, "Init nesmí vrátit chybu, got: "_ret)

	for key = "fullName", "loginName", "mlp", "changePassword", "certificates", "defaultWindow", "usePersonDir", "personDir", "centralDir" {
		do $$$AssertNotTrue((..OutField(key)=""), "Init musí obsahovat pole: "_key)
	}

	// Osobní adresář = FILE s folder-picker filtrem.
	set pd = ..OutField("personDir")
	do $$$AssertEquals($lg(pd,5), "FILE", "personDir musí být typu FILE")
	do $$$AssertEquals($lg(pd,14), "$FOLDER", "personDir musí mít folder-picker filtr")

	// Přepínač validuje při změně (živě přepočítá editovatelnost cesty).
	do $$$AssertEquals($lg(..OutField("usePersonDir"),7), 1, "usePersonDir musí mít validateOnChange=1")

	// Read-only přehledová pole.
	do $$$AssertEquals($lg(..OutField("fullName"),10), 0, "fullName musí být read-only")
}

/// usePersonDir=0 → osobní adresář read-only.
Method TestPersonDirReadonlyWhenUnchecked()
{
	do ..SetupSys(.sys)
	set ^||ML("IN","trigger") = "Change"
	set ^||ML("IN",1) = ..Fld("usePersonDir", "BOOL", "0", 1, 1)
	set ^||ML("IN",2) = ..Fld("personDir", "FILE", "", 1)
	set ret = ##class(ALVA.EDISONMAIN2.EDInputForm.UserProfile).Validate(.sys)
	do $$$AssertEquals(+ret, 0, "Validate nesmí vrátit chybu, got: "_ret)

	do $$$AssertEquals($lg(..OutField("personDir"),10), 0, "personDir musí být read-only když usePersonDir=0")
}

/// usePersonDir=1 → osobní adresář editovatelný.
Method TestPersonDirEditableWhenChecked()
{
	do ..SetupSys(.sys)
	set ^||ML("IN","trigger") = "Change"
	set ^||ML("IN",1) = ..Fld("usePersonDir", "BOOL", "1", 1, 1)
	set ^||ML("IN",2) = ..Fld("personDir", "FILE", "", 0)
	set ret = ##class(ALVA.EDISONMAIN2.EDInputForm.UserProfile).Validate(.sys)
	do $$$AssertEquals(+ret, 0, "Validate nesmí vrátit chybu, got: "_ret)

	do $$$AssertEquals($lg(..OutField("personDir"),10), 1, "personDir musí být editovatelný když usePersonDir=1")
}

/// BeforeSave + použít osobní adresář + prázdná cesta → validační chyba na personDir.
Method TestBeforeSaveEmptyPersonDirErrors()
{
	do ..SetupSys(.sys)
	set ^||ML("IN","trigger") = "BeforeSave"
	set ^||ML("IN",1) = ..Fld("usePersonDir", "BOOL", "1", 1, 1)
	set ^||ML("IN",2) = ..Fld("personDir", "FILE", "", 1)
	set ret = ##class(ALVA.EDISONMAIN2.EDInputForm.UserProfile).Validate(.sys)
	do $$$AssertEquals(+ret, 0, "Validate nesmí vrátit chybu, got: "_ret)

	do $$$AssertNotTrue(($lg(..OutField("personDir"),9)=""), "prázdná cesta při zaškrtnutém použití musí dát chybu")
}

/// BeforeSave + použít osobní adresář + vyplněná cesta → bez chyby.
Method TestBeforeSaveFilledPersonDirOk()
{
	do ..SetupSys(.sys)
	set ^||ML("IN","trigger") = "BeforeSave"
	set ^||ML("IN",1) = ..Fld("usePersonDir", "BOOL", "1", 1, 1)
	set ^||ML("IN",2) = ..Fld("personDir", "FILE", "C:\Export", 1)
	set ret = ##class(ALVA.EDISONMAIN2.EDInputForm.UserProfile).Validate(.sys)
	do $$$AssertEquals(+ret, 0, "Validate nesmí vrátit chybu, got: "_ret)

	do $$$AssertEquals($lg(..OutField("personDir"),9), "", "vyplněná cesta nesmí dát chybu")
}

/// BeforeSave + odškrtnuté použití → prázdná cesta je OK (nevaliduje se).
Method TestBeforeSaveUncheckedEmptyOk()
{
	do ..SetupSys(.sys)
	set ^||ML("IN","trigger") = "BeforeSave"
	set ^||ML("IN",1) = ..Fld("usePersonDir", "BOOL", "0", 1, 1)
	set ^||ML("IN",2) = ..Fld("personDir", "FILE", "", 0)
	set ret = ##class(ALVA.EDISONMAIN2.EDInputForm.UserProfile).Validate(.sys)
	do $$$AssertEquals(+ret, 0, "Validate nesmí vrátit chybu, got: "_ret)

	do $$$AssertEquals($lg(..OutField("personDir"),9), "", "odškrtnuté použití + prázdná cesta je OK")
}

/// Save je no-op (persist řeší klient) — jen nesmí vrátit chybu.
Method TestSaveIsNoOp()
{
	do ..SetupSys(.sys)
	set ret = ##class(ALVA.EDISONMAIN2.EDInputForm.UserProfile).Save(.sys)
	do $$$AssertEquals(+ret, 0, "Save nesmí vrátit chybu, got: "_ret)
}

}
