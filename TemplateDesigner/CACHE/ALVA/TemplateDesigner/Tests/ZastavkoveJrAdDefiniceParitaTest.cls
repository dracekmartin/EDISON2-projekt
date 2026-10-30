Include %occInclude

/// Živá parita vlastních sloupců vývěsky: GetData s preset definicí (postavenou z konfigurace
/// linky, přesně jako editor v TD) musí dát stejný výstup jako režim „automaticky". Hlavní
/// pojistka, že presety jsou 1:1 náhrada dnešních pevných rozvržení - měří se digest celého
/// výstupu (GoldUtil), takže parita pokrývá sloupce, odjezdy, denní značky i legendu.
///
/// ⚠️ Stejně jako gold testy je test pevně vůči CB_CSADAD datům (jiný server → skip). Na rozdíl
/// od nich ale neporovnává zmrazenou konstantu, jen dva režimy téhož běhu - drift dat mu nevadí.
Class ALVA.TemplateDesigner.Tests.ZastavkoveJrAdDefiniceParitaTest Extends ALVA.EDTesting.MLTestCase
{

/// Namespace, kde žijí gold data (AD je per-firma-namespace).
Parameter GOLDDB = "CB_CSADAD";

Method TestParitaPresetuNaZivychDatech()
{
	set db = ..#GOLDDB
	if '##class(%SYS.Namespace).Exists(db) {
		do $$$AssertSkipped("namespace "_db_" není na tomto serveru - parita přeskočena")
		quit
	}
	do ..AssertParita(db, 1, "0365001")
	do ..AssertParita(db, 1, "0365005")
}

Method AssertParita(db As %String, sku As %String, lin As %String)
{
	if '$data(^[db]JRL(sku,lin,"I")) {
		do $$$AssertTrue(0, "linka "_lin_" chybí v "_db_" (namespace je, data ne) - rozbitý fixture?")
		quit
	}
	set ftl = +$get(^[db]JRL(sku,lin,"PATCH","MHD_TYP_R"))
	set nz = $get(^[db]JRL(sku,lin,"PATCH","MHD_NZ1_NZ2"))
	if ((ftl = 1) || (ftl = 3)) && ($translate(nz,"~") = "") {
		// Vědomá odchylka: prázdninové rozvržení bez NZ konfigurace dává v legacy pracovní spoj
		// jen do 1. sloupce (prázdné nzftl1 == prázdné cnz je „exact match"), definice do obou
		// pracovních. Degenerovaný případ dokumentovaný v TestDefiniceFtl1BezNzKonfigurace.
		// Vědomá výjimka, ne přeskočení: druhá linka testu paritu ověří, takže metoda přeskočená není.
		do ..LogMessage("linka "_lin_": FTL "_ftl_" bez NZ konfigurace - parita se neměří (vědomá odchylka)")
		quit
	}

	set digestAuto = ..DigestGetData(db, sku, lin, "")
	set digestDef  = ..DigestGetData(db, sku, lin, ..PresetJson(db, sku, lin))
	do $$$AssertEquals(digestDef, digestAuto, "linka "_lin_" (FTL "_ftl_"): preset == automaticky")
}

/// Zavolá GetData (s volitelnou definicí sloupců) a vrátí digest. V režimu definice před digestem
/// odebere NEZARAZENE_SPOJE a OBDOBI_MIMO_PLATNOST - obě pole existují jen tam (legacy nerozpoznané
/// platnosti tiše přeskakuje, definice je hlásí; vytištěné odjezdy jsou stejné).
Method DigestGetData(db As %String, sku As %String, lin As %String, definice As %String) As %String
{
	do ..SetupSys(.sys)
	set sys("U","DATA") = db
	kill ^||ML
	set ^||ML("IN","SKUPINA_LINEK_ID") = sku
	set ^||ML("IN","LINKA_ID") = lin
	if definice '= "" set ^||ML("IN","DEFINICE_SLOUPCU") = definice
	do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).GetData(.sys)
	if definice '= "" {
		for p = 1:1:+$get(^||ML("OUT")) {
			set obj = ##class(%DynamicObject).%FromJSON(^||ML("OUT",p))
			do obj.values.%Remove("NEZARAZENE_SPOJE")
			do obj.values.%Remove("NEZARAZENE_SPOJE_CISLA")
			do obj.values.%Remove("OBDOBI_MIMO_PLATNOST")
			set ^||ML("OUT",p) = obj.%ToJSON()
		}
	}
	quit ##class(ALVA.TemplateDesigner.Tests.GoldUtil).Digest()
}

/// Definice sloupců z vzoru odpovídajícího dnešnímu rozvržení linky. Vzory bere ze **serveru**
/// (`PresetySloupcu`) - tedy z téhož místa, odkud je vezme editor v TD. Dřív si je test skládal
/// vlastním zrcadlem pravidel a hlídal tím kopii, ne to, co klient reálně posílá.
ClassMethod PresetJson(db As %String, sku As %String, lin As %String) As %String
{
	set ftl = +$get(^[db]JRL(sku,lin,"PATCH","MHD_TYP_R"))
	set klic = $case(ftl, 1:"PRAZDNINY_VIKEND", 2:"PRAC_VIKEND", 3:"PRAZDNINY_SO_NE", :"PRAC_SO_NE")
	set vzory = ##class(%DynamicObject).%FromJSON(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).PresetySloupcu(db, sku, lin))
	set def = ##class(%DynamicObject).%New()
	do def.%Set("sloupce", vzory.%Get(klic))
	quit def.%ToJSON()
}

}
