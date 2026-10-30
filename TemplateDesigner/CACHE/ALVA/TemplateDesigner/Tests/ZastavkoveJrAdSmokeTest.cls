Include %occInclude

/// Strukturální smoke test GetData agendy ZastavkoveJrAd nad ŽIVÝMI daty (^JRL v MLINE).
/// Linku hledá dynamicky přes PrvniLinka (žádná hardcoded gold linka) a assertuje
/// STRUKTURU wire formátu, ne konkrétní hodnoty - přežije editace JŘ dat.
/// Přesné 1:1 porovnání (zmrazený digest) = ZastavkoveJrAdGoldTest (AD) / ZastavkoveJrMhdGoldTest (MHD).
Class ALVA.TemplateDesigner.Tests.ZastavkoveJrAdSmokeTest Extends ALVA.EDTesting.MLTestCase
{

/// `B181` (TRANSDEV #12843): „Nejvýš sloupců variant tras" nechá prvních N sloupců podle řazení
/// a zbytek netiskne. Linka 0330530 má nejvíc variant ze všech prověřených (až 12 na stránku).
/// Hlídá tři věci: strop, že zůstanou TYTÉŽ první sloupce jako bez stropu (ořez, ne jiný výběr),
/// a že žádný odjezd nenese písmeno sloupce, který se netiskne.
Method TestMaxSloupcuVariantOrizneNaPrvniN()
{
	set db = "CB_CSADAD", max = 2
	if '##class(%SYS.Namespace).Exists(db) {
		do $$$AssertTrue(1, "namespace "_db_" není na serveru - přeskočeno")
		quit
	}
	do ##class(ALVA.TemplateDesigner.Tests.ZastavkoveJrAdLegacyTest).SetupSysFor(.sys, db)
	for rezim = "bez", "s" {
		kill ^||ML("IN"), ^||ML("OUT")
		set ^||ML("IN","SKUPINA_LINEK_ID") = 1, ^||ML("IN","LINKA_ID") = "0330530"
		set:rezim="s" ^||ML("IN","MAX_SLOUPCU_VARIANT") = max
		do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).GetData(.sys)
		merge vystup(rezim) = ^||ML("OUT")
	}
	set orezanych = 0, cizichPismen = 0, p = ""
	for {
		set p = $order(vystup("bez",p)) quit:p=""
		continue:'$isvalidnum(p)
		set bez = ##class(%DynamicObject).%FromJSON(vystup("bez",p)).values.%Get("VARIANTY_TRAS")
		set strS = ##class(%DynamicObject).%FromJSON($get(vystup("s",p)))
		set s = strS.values.%Get("VARIANTY_TRAS")
		set cekano = $select(bez.%Size() > max: max, 1: bez.%Size())
		set:bez.%Size()>max orezanych = orezanych + 1
		do $$$AssertEquals(s.%Size(), cekano, "stránka "_p_": sloupců variant")
		kill tistena
		for i=0:1:(s.%Size()-1) {
			do $$$AssertEquals(s.%Get(i), bez.%Get(i), "stránka "_p_": sloupec "_(i+1)_" shodný s neoříznutou")
			set:s.%Get(i)'="" tistena(s.%Get(i)) = ""
		}
		set it = strS.rows.%Get("ODJEZDY").%GetIterator()
		while it.%GetNext(.k, .o) {
			set ai = o.%Get("ANOTACE").%GetIterator()
			while ai.%GetNext(.ak, .a) {
				continue:a.%Get("KATEGORIE")'="varianta-trasy"
				set:'$data(tistena(a.%Get("ZNAK"))) cizichPismen = cizichPismen + 1
			}
		}
	}
	do $$$AssertTrue(orezanych > 0, "linka má stránky s víc než "_max_" variantami ("_orezanych_") - jinak test nic neověří")
	do $$$AssertEquals(cizichPismen, 0, "odjezdy bez písmene netištěného sloupce")
}

Method TestGetDataVraciValidniStrankyProPrvniLinku()
{
	do ..SetupSys(.sys)
	do ##class(ALVA.TemplateDesigner.Agendy.Jr.DataAd).PrvniLinka($namespace, .sku, .lin)
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

/// Parametry variant tras se v návrháři ukážou, jen když šablona tiskne sloupec Varianty tras
/// (rozhodnutí Jan 5. 10.). Závislost musí nést právě ty parametry, které bez sloupce nic nedělají:
/// bez ní by parametr svítil naprázdno, u jiného parametru by ho návrhář schoval.
Method TestParametryVariantJenSeSloupcemVariant()
{
	do ..SetupSys(.sys)
	do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).GetParameters(.sys)
	set params = ##class(%DynamicAbstractObject).%FromJSON($get(^||ML("OUT",1)))
	set variantni = $lb("RAZENI_VARIANT","MAX_SLOUPCU_VARIANT","SLUCOVAT_PODMNOZINY","PISMENA_VARIANT","PRVNI_BEZ_PISMENE")
	set nalezeno = 0
	set iter = params.%GetIterator()
	while iter.%GetNext(.i, .p) {
		set cekano = $select($listfind(variantni, p.name): "CAS_JIZDY_VARIANTY", 1: "")
		set:cekano'="" nalezeno = nalezeno + 1
		do $$$AssertEquals(p.%Get("visibleWhenColumn"), cekano, p.name_": závislost na sloupci variant")
	}
	do $$$AssertEquals(nalezeno, $listlength(variantni), "všechny parametry variant jsou v GetParameters")
}

}
