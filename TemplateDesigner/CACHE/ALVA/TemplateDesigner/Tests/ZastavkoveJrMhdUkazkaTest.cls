Include %occInclude

/// Náhled bez vybrané linky u Zastávkového JŘ MHD (`B194` D7): první vývěska první linky výchozí
/// skupiny, stejně jako ostatní agendy JŘ berou první skutečnou linku - ne vymyšlená brněnská linka,
/// která se od dat lišila stavbou a tiskla se do PDF z ukázky.
Class ALVA.TemplateDesigner.Tests.ZastavkoveJrMhdUkazkaTest Extends ALVA.EDTesting.MLTestCase
{

/// Firma + namespace gold MHD dat (MHD je multi-tenant → obojí CB_DP), stejně jako gold test.
Parameter GOLDDB = "CB_DP";

/// Ukázka je jediná stránka a shoduje se s první stránkou tisku téže linky.
Method TestUkazkaJePrvniVyveskaPrvniLinky()
{
	set db = ..#GOLDDB
	if '##class(%SYS.Namespace).Exists(db) {
		do $$$AssertTrue(1, "namespace "_db_" není na tomto serveru - přeskočeno")
		quit
	}
	do ..SetupSys(.sys, db)
	set sys("U","DATA") = db
	set mhd = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd"
	set skupina = $classmethod(mhd, "GetDefaultSkupinaLinekID", db, db)
	set linka = $order(^[db]ALVA.MHD.LSO(db, 1, "IDX", 1, 1, 1, skupina, ""))
	if linka = "" {
		do $$$AssertTrue(1, db_" nemá linku ve výchozí skupině - přeskočeno")
		quit
	}

	kill ^||ML
	do $classmethod(mhd, "GetMockData", .sys)
	do $$$AssertTrue('$data(^||ML("ERR")), "ukázka nemá skončit chybou: "_$get(^||ML("ERR")))
	do $$$AssertTrue($data(^||ML("OUT", 1)) && '$data(^||ML("OUT", 2)), "ukázka má být právě jedna stránka")
	set ukazka = $get(^||ML("OUT", 1))
	do $$$AssertTrue(ukazka '[ "Líšeň", "ukázka nesmí být vymyšlená brněnská linka")

	kill ^||ML
	set ^||ML("IN", "LINKA_ID") = linka
	do $classmethod(mhd, "GetData", .sys)
	if $data(^||ML("OUT", 1)) {
		do $$$AssertEquals(ukazka, ^||ML("OUT", 1), "ukázka = první vývěska tisku linky "_linka)
	}
	else {
		do $$$AssertTrue(ukazka [ $classmethod(mhd, "TextUkazkyBezDat"), "linka "_linka_" bez vývěsek - ukázka má říct, ať uživatel vybere linku")
	}
}

}
