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
		do $$$AssertTrue(1, "namespace "_db_" není na tomto serveru - parita přeskočena")
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
		do $$$AssertTrue(1, "linka "_lin_": FTL "_ftl_" bez NZ konfigurace - parita se neměří (vědomá odchylka)")
		quit
	}

	set digestAuto = ..DigestGetData(db, sku, lin, "")
	set digestDef  = ..DigestGetData(db, sku, lin, ..PresetJson(db, sku, lin))
	do $$$AssertEquals(digestDef, digestAuto, "linka "_lin_" (FTL "_ftl_"): preset == automaticky")
}

/// Zavolá GetData (s volitelnou definicí sloupců) a vrátí digest. V režimu definice před digestem
/// odebere NEZARAZENE_SPOJE - pole existuje jen tam (legacy nerozpoznané platnosti tiše přeskakuje,
/// definice je hlásí; vytištěné odjezdy jsou stejné).
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
			set ^||ML("OUT",p) = obj.%ToJSON()
		}
	}
	quit ##class(ALVA.TemplateDesigner.Tests.GoldUtil).Digest()
}

/// Preset definice z konfigurace linky - zrcadlí DepartureColumnPresets.Build v TD (dny dle FTL,
/// vyloučení z NZ konfigurace, per-sloupcové potlačení značek z pozičního seznamu MHD_NEZNZ123).
ClassMethod PresetJson(db As %String, sku As %String, lin As %String) As %String
{
	set ftl = +$get(^[db]JRL(sku,lin,"PATCH","MHD_TYP_R"))
	set nz = $get(^[db]JRL(sku,lin,"PATCH","MHD_NZ1_NZ2"))
	set nenz = $get(^[db]JRL(sku,lin,"PATCH","MHD_NENZ1_NENZ2"))
	set neznz = $get(^[db]JRL(sku,lin,"PATCH","MHD_NEZNZ123"))
	// 1. pozice NZ = „jede o prázdninách", 2. = „nejede"; poziční seznamy legacy prohozené
	set mimoBez   = ..SpojSeznamy($piece(nz,"~",1), $piece(nenz,"~",2))
	set oPrazdBez = ..SpojSeznamy($piece(nz,"~",2), $piece(nenz,"~",1))

	set sloupce = ##class(%DynamicArray).%New()
	if ftl = 1 {
		do sloupce.%Push(..Sloupec("1", "Pracovní dny mimo prázdniny", "12345", mimoBez))
		do sloupce.%Push(..Sloupec("2", "Pracovní dny o prázdninách", "12345", oPrazdBez))
		do sloupce.%Push(..Sloupec("3", "Soboty, neděle a svátky", "67+", ""))
	} elseif ftl = 2 {
		do sloupce.%Push(..Sloupec("1", "Pracovní dny", "12345", ""))
		do sloupce.%Push(..Sloupec("2", "Soboty, neděle a svátky", "67+", ""))
	} elseif ftl = 3 {
		do sloupce.%Push(..Sloupec("1", "Pracovní dny mimo prázdniny", "12345", mimoBez))
		do sloupce.%Push(..Sloupec("2", "Pracovní dny o prázdninách", "12345", oPrazdBez))
		do sloupce.%Push(..Sloupec("3", "Soboty", "6", ""))
		do sloupce.%Push(..Sloupec("4", "Neděle a svátky", "7+", ""))
	} else {
		do sloupce.%Push(..Sloupec("1", "Pracovní dny", "12345", ""))
		do sloupce.%Push(..Sloupec("2", "Soboty", "6", ""))
		do sloupce.%Push(..Sloupec("3", "Neděle a svátky", "7+", ""))
	}
	for i = 1:1:sloupce.%Size() {
		set slot = $piece(neznz, "~", i)
		if slot '= "" do sloupce.%Get(i-1).%Set("nevypisovatZnacky", slot)
	}
	set def = ##class(%DynamicObject).%New()
	do def.%Set("sloupce", sloupce)
	quit def.%ToJSON()
}

ClassMethod Sloupec(id As %String, nadpis As %String, dny As %String, bez As %String) As %DynamicObject
{
	set sl = ##class(%DynamicObject).%New()
	do sl.%Set("id", id)
	do sl.%Set("nadpis", nadpis)
	do sl.%Set("dny", dny)
	if bez '= "" do sl.%Set("znackyBez", bez)
	quit sl
}

ClassMethod SpojSeznamy(a As %String, b As %String) As %String
{
	if a = "" quit b
	if b = "" quit a
	quit a_","_b
}

}
