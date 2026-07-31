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
///
/// ⚠️ **Kdo přidá pole do `values` nebo do řádků, musí přebaselinovat hned ve svém commitu.**
/// Digest je MD5 celého JSON, takže **nové pole změní md5, ale ne počty** - a pád pak spadne na
/// někoho jiného za dva dny (tak to bylo u #12997 s polem `BARVA_LINKY`). Když počty sedí a liší se
/// jen md5, hledej **přidané/změněné pole**, ne rozbitou logiku; `AdProdlouzeniProbe.StrankaValues`
/// vypíše `values` jedné stránky pro porovnání.
Class ALVA.TemplateDesigner.Tests.ZastavkoveJrAdGoldTest Extends ALVA.EDTesting.MLTestCase
{

/// Namespace, kde žijí gold data (AD je per-firma-namespace).
Parameter GOLDDB = "CB_CSADAD";

Method TestGold0365001()
{
	// Re-baseline 2026-07-31 (#12997, commit 5a93f17): do `values` přibylo pole BARVA_LINKY
	// (barva linky z JŘ modulu) - počty beze změny, jen md5. Předtím 27. 7. NASTUPISTE_VARIANT.
	do ..AssertLine(1, "0365001", "pages=39 zast=800 odj=844 pozn=110 md5=wqcHRGlFy5NgEPf+PwQ1iw==")
}

Method TestGold0365005()
{
	// Re-baseline 2026-07-31 (#12997, commit 5a93f17): + pole BARVA_LINKY ve `values`
	// (počty beze změny). Předtím 27. 7. NASTUPISTE_VARIANT, před ním řazení variantních sloupců.
	do ..AssertLine(1, "0365005", "pages=38 zast=762 odj=498 pozn=183 md5=DnaY+3JpewJ9fibg10cfig==")
}

/// Ověří digest linky proti očekávané hodnotě. Rozlišuje:
///   namespace chybí        → skip (jiný server, legitimní)
///   namespace je, linka ne  → FAIL (gold data zmizela / rozbitý fixture)
///   jinak                   → 1:1 digest assert
Method AssertLine(sku As %String, lin As %String, expected As %String)
{
	set db = ..#GOLDDB
	if '##class(%SYS.Namespace).Exists(db) {
		do $$$AssertTrue(1, "namespace "_db_" není na tomto serveru - gold "_lin_" přeskočen")
		quit
	}
	if '$data(^[db]JRL(sku, lin, "I")) {
		do $$$AssertTrue(0, "gold linka "_lin_" chybí v "_db_" (namespace je, data ne) - rozbitý fixture?")
		quit
	}

	do ..SetupSys(.sys)
	set sys("U","DATA") = db
	kill ^||ML
	set ^||ML("IN","SKUPINA_LINEK_ID") = sku
	set ^||ML("IN","LINKA_ID") = lin
	do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).GetData(.sys)

	set digest = ##class(ALVA.TemplateDesigner.Tests.GoldUtil).Digest()
	do $$$AssertEquals(digest, expected, "gold "_lin_" digest (aktuální: "_digest_")")
}

}
