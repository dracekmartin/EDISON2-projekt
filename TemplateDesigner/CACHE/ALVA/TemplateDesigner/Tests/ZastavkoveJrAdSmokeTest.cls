Include %occInclude

/// Strukturální smoke test GetData agendy ZastavkoveJrAd nad ŽIVÝMI daty (^JRL v MLINE).
/// Linku hledá dynamicky přes PrvniLinka (žádná hardcoded gold linka) a assertuje
/// STRUKTURU wire formátu, ne konkrétní hodnoty - přežije editace JŘ dat.
/// Přesné 1:1 porovnání (zmrazený digest) = ZastavkoveJrAdGoldTest (AD) / ZastavkoveJrMhdGoldTest (MHD).
Class ALVA.TemplateDesigner.Tests.ZastavkoveJrAdSmokeTest Extends ALVA.EDTesting.MLTestCase
{

Method TestGetDataVraciValidniStrankyProPrvniLinku()
{
	do ..SetupSys(.sys)
	do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).PrvniLinka($namespace, .sku, .lin)
	if lin = "" {
		do $$$AssertTrue(1, "Namespace nemá žádnou linku v ^JRL - smoke přeskočen")
		quit
	}

	set ^||ML("IN","SKUPINA_LINEK_ID") = sku
	set ^||ML("IN","LINKA_ID") = lin
	do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).GetData(.sys)

	do $$$AssertTrue(($get(^||ML("OUT")) >= 1), "GetData vrací aspoň 1 stránku (zastávka×směr), linka "_sku_"/"_lin)

	// Každá stránka = validní JSON se strukturou { values, rows: { ZASTAVKY, SKUPINY_DNU, ODJEZDY, POZNAMKY } }
	set page = ##class(%DynamicObject).%FromJSON(^||ML("OUT",1))
	do $$$AssertTrue($isobject(page.values), "stránka má values")
	do $$$AssertTrue($isobject(page.rows), "stránka má rows")
	do $$$AssertNotEquals(page.values.%Get("CISLO_LINKY"), "", "values.CISLO_LINKY vyplněno")
	do $$$AssertNotEquals(page.values.%Get("POPIS_ZASTAVKA"), "", "values.POPIS_ZASTAVKA vyplněno")

	set zastavky = page.rows.%Get("ZASTAVKY")
	do $$$AssertTrue($isobject(zastavky) && (zastavky.%Size() > 0), "ZASTAVKY neprázdné")
	set skupiny = page.rows.%Get("SKUPINY_DNU")
	do $$$AssertTrue($isobject(skupiny) && (skupiny.%Size() > 0), "SKUPINY_DNU neprázdné")
	do $$$AssertTrue($isobject(page.rows.%Get("ODJEZDY")), "ODJEZDY existují")
	do $$$AssertTrue($isobject(page.rows.%Get("POZNAMKY")), "POZNAMKY existují")

	// Kontrakt řádků: zastávka nese ID/POPIS/CAS_JIZDY, odjezd HODINA/MINUTA + vazby
	set z = zastavky.%Get(0)
	do $$$AssertNotEquals(z.%Get("ID"), "", "zastávka má ID")
	do $$$AssertNotEquals(z.%Get("POPIS"), "", "zastávka má POPIS")
	set odjezdy = page.rows.%Get("ODJEZDY")
	if odjezdy.%Size() > 0 {
		set o = odjezdy.%Get(0)
		do $$$AssertTrue((o.%Get("HODINA") >= 0) && (o.%Get("HODINA") <= 29), "odjezd má validní HODINA")
		do $$$AssertTrue((o.%Get("MINUTA") >= 0) && (o.%Get("MINUTA") <= 59), "odjezd má validní MINUTA")
		do $$$AssertNotEquals(o.%Get("ZASTAVKA_ID"), "", "odjezd má vazbu na zastávku")
		do $$$AssertNotEquals(o.%Get("SKUPINA_DNU_ID"), "", "odjezd má vazbu na skupinu dnů")
	}
}

Method TestGetDataBezLinkyNevraciStranky()
{
	do ..SetupSys(.sys)
	do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).GetData(.sys)
	do $$$AssertEquals($get(^||ML("OUT")), "", "GetData bez LINKA_ID nevrací žádné stránky (chyba)")
}

Method TestGetDataNeexistujiciLinka()
{
	do ..SetupSys(.sys)
	set ^||ML("IN","LINKA_ID") = "9999999"
	do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).GetData(.sys)
	do $$$AssertEquals($get(^||ML("OUT")), "", "Neexistující linka nevrací stránky (chyba)")
}

}
