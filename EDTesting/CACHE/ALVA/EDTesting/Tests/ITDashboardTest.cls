Include (%occInclude, ALVAMINIAPLIKACE)

Class ALVA.EDTesting.Tests.ITDashboardTest Extends ALVA.EDTesting.MLTestCase
{

Parameter AppType = 52001;

/// Seed zapíše Přehled pod uživatele 0 i přihlášeného, Clean ho celý smaže.
Method TestSeedAndCleanUnderTestFirm()
{
	s db = ##class(ALVA.EDISON.MiniAplikaceAPI).GetDatabase()
	try {
		d ..SetupSys(.sys, "TEST_EDISON2_IT", "999")
		s ^||ML("IN","appType") = ..#AppType
		d ##class(ALVA.EDTesting.ITDashboard).Seed(.sys)
		do $$$AssertEquals($g(^||ML("ERR")), "", "Seed skončil chybou")
		for appUserID = 0, 999 {
			do $$$AssertEquals($g(^[db]ALVA.MINIAPLIKACE("TEST_EDISON2_IT","data",..#AppType,appUserID,"data",2,$$$IndexType)), $$$ObjectTable, "tabulka uživatele "_appUserID)
			do $$$AssertEquals($lg($g(^[db]ALVA.MINIAPLIKACE("TEST_EDISON2_IT","data",..#AppType,appUserID,"data",2,$$$IndexData,"tr",2,"style")),2), "#FF0000", "styl řádku uživatele "_appUserID)
		}

		d ##class(ALVA.EDTesting.ITDashboard).Clean(.sys)
		do $$$AssertEquals($g(^||ML("ERR")), "", "Clean skončil chybou")
		do $$$AssertNotTrue($d(^[db]ALVA.MINIAPLIKACE("TEST_EDISON2_IT","data",..#AppType)), "Přehled po Clean zůstal")
	} catch ex {
		s error = ex.DisplayString()
	}
	// Úklid i po výjimce: Přehled testovací firmy by jinak zůstal na sdíleném serveru.
	k ^[db]ALVA.MINIAPLIKACE("TEST_EDISON2_IT","data",..#AppType)
	do $$$AssertEquals($g(error), "", "výjimka v testu")
}

/// Pod jinou firmou Seed i Clean skončí chybou a její Přehled nezmění. Vymyšlená firma, ne skutečná:
/// rozbitá ochrana by jinak smazala Přehledy firmy na sdíleném serveru.
Method TestRejectsOtherFirm()
{
	s db = ##class(ALVA.EDISON.MiniAplikaceAPI).GetDatabase()
	s other = "TEST_EDISON2_GUARD"
	k ^[db]ALVA.MINIAPLIKACE(other)
	s ^[db]ALVA.MINIAPLIKACE(other,"data",..#AppType,0,"param",$$$ParamTimeStamp) = "puvodni"
	d ..SetupSys(.sys, other, "999")
	s ^||ML("IN","appType") = ..#AppType

	d ##class(ALVA.EDTesting.ITDashboard).Seed(.sys)
	do $$$AssertNotEquals($g(^||ML("ERR")), "", "Seed pod jinou firmou neskončil chybou")
	k ^||ML("ERR")
	d ##class(ALVA.EDTesting.ITDashboard).Clean(.sys)
	do $$$AssertNotEquals($g(^||ML("ERR")), "", "Clean pod jinou firmou neskončil chybou")

	do $$$AssertEquals($g(^[db]ALVA.MINIAPLIKACE(other,"data",..#AppType,0,"param",$$$ParamTimeStamp)), "puvodni", "Clean smazal Přehled jiné firmy")
	do $$$AssertNotTrue($d(^[db]ALVA.MINIAPLIKACE(other,"data",..#AppType,999)), "Seed zapsal pod jinou firmu")
	k ^[db]ALVA.MINIAPLIKACE(other)
}

}
