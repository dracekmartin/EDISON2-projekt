Include GENERAL

Class ALVA.EDISONMAIN2.API.Tree Extends %RegisteredObject
{

/// Vrátí strom modulů hlavního okna.
/// Formát položek v ^||ML("OUT", i) jako CList ($lb):
///   $lb("FAMILY", nazev, expanded)   ... složka (expanded: "1"=rozbalena, "0"=sbalena)
///   $lb("EXE", nazev, exe, form)     ... spustitelný modul
///   $lb("FAMILY_END")                ... konec složky
ClassMethod GetTree(ByRef sys As %String) As %Status
{
	$$$Init
	try
	{
		$$$FillParams
		s i=0

		s ^||ML("OUT",$i(i))=$lb("FAMILY","MHD","1")

		s ^||ML("OUT",$i(i))=$lb("EXE","Plán dopravy","MHD2PlanDopravy.exe","ALVA.MHD2.PlanDopravy.formMHD2PlanDopravyMain")
		s ^||ML("OUT",$i(i))=$lb("EXE","Linky a oběhy","MHD2Linky.exe","ALVA.MHD2.Linky.formMHD2LinkyHlavniOkno")

		s ^||ML("OUT",$i(i))=$lb("FAMILY_END")
	}
	catch (ex) { $$$CatchErrRest }
	ret ret
}

}
