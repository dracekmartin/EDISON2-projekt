Include %occInclude

/// Základní regresní pojistka: 1:1 kontrola výstupu GetData agendy ZastavkoveJrAd proti
/// zmrazenému "golden" digestu na pár reprezentativních gold linkách.
///
/// ⚠️ ZÁMĚRNĚ PEVNĚ VŮČI CB_CSADAD DATŮM (per-firma namespace). Není to úplná/robustní verze
/// (data mohou driftovat = falešný pád → přegeneruj digest; jiný server bez CB_CSADAD → skip).
/// Robustní verze (zmrazit vstupní ^JRL/^JRZ/^JRSET do izolovaného namespace) = viz roadmap.
/// Smysl: zachytit, že jsme kódem nerozbili nic zásadního v datovém výstupu.
///
/// Přegenerování baseline po ZÁMĚRNÉ změně výstupu: spusť test, z hlášky „aktuální: <digest>"
/// vezmi nový digest a přepiš konstantu Expected*.
Class ALVA.TemplateDesigner.Tests.ZastavkoveJrAdGoldTest Extends ALVA.EDTesting.MLTestCase
{

/// Namespace, kde žijí gold data (AD je per-firma-namespace).
Parameter GOLDDB = "CB_CSADAD";

Method TestGold0365001()
{
	set digest = ..RunLine(1, "0365001")
	if digest = "" {
		do $$$AssertTrue(1, "gold 0365001 není v "_..#GOLDDB_" na tomto serveru — přeskočeno")
		quit
	}
	do $$$AssertEquals(digest, "pages=39 zast=800 odj=844 pozn=96 md5=pX2NNrkdE9sGH/TMw7DV+Q==", "gold 0365001 digest (aktuální: "_digest_")")
}

Method TestGold0365005()
{
	set digest = ..RunLine(1, "0365005")
	if digest = "" {
		do $$$AssertTrue(1, "gold 0365005 není v "_..#GOLDDB_" na tomto serveru — přeskočeno")
		quit
	}
	do $$$AssertEquals(digest, "pages=38 zast=762 odj=498 pozn=168 md5=v4khAof5UfsTYtXSZgkLCw==", "gold 0365005 digest (aktuální: "_digest_")")
}

/// Spustí GetData pro danou linku nad GOLDDB daty a vrátí digest. "" = linka není (skip).
ClassMethod RunLine(sku As %String, lin As %String) As %String
{
	set db = ..#GOLDDB
	quit:'$data(^[db]JRL(sku, lin, "I")) ""

	do ..SetupSys(.sys)
	set sys("U","DATA") = db
	kill ^||ML
	set ^||ML("IN","SKUPINA_LINEK_ID") = sku
	set ^||ML("IN","LINKA_ID") = lin
	do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).GetData(.sys)
	quit ..GoldDigest()
}

/// Kompaktní deterministický digest výstupu: hrubé počty (čitelné, rychlá diagnóza) + MD5 celého
/// spojeného JSON (chytí i jemný posun hodnot). Deterministický, protože GetData je byte-identický.
ClassMethod GoldDigest() As %String
{
	set n = +$get(^||ML("OUT"))
	set all = "", tz = 0, to = 0, tp = 0
	for p = 1:1:n {
		set json = $get(^||ML("OUT",p))
		set all = all_json
		set obj = ##class(%DynamicObject).%FromJSON(json)
		set tz = tz + obj.rows.%Get("ZASTAVKY").%Size()
		set to = to + obj.rows.%Get("ODJEZDY").%Size()
		set tp = tp + obj.rows.%Get("POZNAMKY").%Size()
	}
	set md5 = $system.Encryption.Base64Encode($system.Encryption.MD5Hash(all))
	quit "pages="_n_" zast="_tz_" odj="_to_" pozn="_tp_" md5="_md5
}

}
