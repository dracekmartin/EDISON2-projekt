Include GENERAL

Class ALVA.EDISONMAIN2.API.Tree Extends %RegisteredObject
{

/// Vrátí strom modulů hlavního okna.
/// Formát položek v ^||ML("OUT", i):
///   FAMILY~nazev~expanded  ... složka (expanded: 1=rozbalena, 0=sbalena)
///   EXE~nazev~exe~form     ... spustitelný modul
///   FAMILY_END             ... konec složky
ClassMethod GetTree(ByRef sys As %String) As %Status
{
	$$$Init
	try
	{
		$$$FillParams
		s i=0

		s ^||ML("OUT",$i(i))="FAMILY"_del_"MHD"_del_"1"

		s ^||ML("OUT",$i(i))="EXE"_del_"Plán dopravy"_del_"MHD2PlanDopravy.exe"_del_"ALVA.MHD2.PlanDopravy.formMHD2PlanDopravyMain"
		s ^||ML("OUT",$i(i))="EXE"_del_"Linky a oběhy"_del_"MHD2Linky.exe"_del_"ALVA.MHD2.Linky.formMHD2LinkyHlavniOkno"

		s ^||ML("OUT",$i(i))="FAMILY_END"
	}
	catch (ex) { $$$CatchErrRest }
	ret ret
}

}
