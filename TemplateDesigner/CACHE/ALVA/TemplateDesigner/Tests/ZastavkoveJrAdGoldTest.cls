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
	// Re-baseline 2026-07-31 (#13045): denní značky týdenní platnosti se přestaly tisknout tam,
	// kde je říká nadpis sloupce → o 14 řádků legendy méně (pozn 110 → 96), odjezdy beze změny.
	// Re-baseline 2026-07-31 (#13008): do `values` přibylo pole DOPRAVCI_SOUHRNNY_JR → mění se JEN
	// md5, počty stránek, zastávek, odjezdů i poznámek zůstaly shodné (podpis přidaného pole).
	// Kontrola při dalším posunu: **nejdřív porovnej složky digestu** - když sedí počty a liší se
	// jen md5, je to přidané/přejmenované pole, ne změna chování (past, na kterou jsem 31. 7. najel).
	// Re-baseline 2026-08-03 (#13088): přibylo pole SLOUPEK ve `values` i v řádcích ZASTAVKY → opět jen md5.
	// Re-baseline 2026-08-03 (#13088 podruhé): + SLOUPEK_SPOJU, SLOUPEK_POPIS, SLOUPEK_VARIANT → dtto.
	// Re-baseline 2026-08-03 (#13088 potřetí): + ZDROJ_STANOVISTE. Výchozí zdroj stanoviště zůstal
	// označník, takže se kromě nového pole nic nezměnilo - a to je zároveň důkaz, že nový přepínač
	// je zpětně neutrální.
	// Re-baseline 2026-08-03 (#13088 počtvrté): + CIS čísla zastávek, pak přejmenovaná na příponu
	// `_CIS` dle konvence agendy (CISLO_ZASTAVKA_CIS …) → dtto, jen md5.
	// Re-baseline 2026-08-03 (#13048 bod 1): přestupy dostaly řádek legendy (kategorie „prestup") →
	// ⚠️ **mění se i POČTY**, ne jen md5: pozn 96 → 157 (+61 na 39 stránek, tedy max 2 řádky na
	// stránku pro „A" a „L"). Je to záměrná změna výstupu, ne přidané pole. Na vývěsce se ty řádky
	// objeví jen když prvek zastávek přestupy tiskne - Caché posílá nadmnožinu a prořezává ji
	// render-time filtr (`NoteVisibilityFilter.ShouldHidePrestupLegend`).
	// Re-baseline 2026-08-03 (#12997): + pole BARVA_LINKY_2 (druhá barva linky) → zpět jen md5.
	// Re-baseline 2026-08-03 (#13093 bod 1): + pole VYLUKOVE_TEXTY. **Počty poznámek se nezměnily**,
	// což je zároveň důkaz, že nový parametr „Poznámky zadané u linky" je defaultně vypnutý a
	// existujícím šablonám se legenda nerozrostla.
	// Re-baseline 2026-08-10 (#13045b vlastní sloupce): + pole NZ_KONFIG a NEVYPISOVAT_ZNACKY_SLOUPCU
	// (podklad pro prázdninové vzory v editoru sloupců) → jen md5. Počty beze změny = režim
	// „automaticky" se vlastními sloupci nehnul; paritu presetů hlídá ZastavkoveJrAdDefiniceParitaTest.
	// Re-baseline 2026-08-10 (#13045b etapa 2): + pole PRESETY_SLOUPCU → jen md5, počty beze změny.
	// Re-baseline 2026-08-12 (#12843): varianty tras se rozlišují jen podle úseku OD vývěskové zastávky
	// dál (1:1 legacy, dřív se srovnával celý směr). ⚠️ Mění se **obsah**, ne počty: sloupců variant
	// 116 → 60, protože 56 z nich mělo v tištěném úseku identická čísla a lišilo se jen před vývěskou.
	// Počty stránek/zastávek/odjezdů/poznámek zůstaly shodné = neztratil se žádný spoj ani řádek.
	// Re-baseline 2026-08-12 (#12843 podruhé): jediný sloupec časů se vybírá podle POKRYTÍ seznamu
	// (kolik zastávek trasa od vývěsky obslouží), ne podle četnosti spojů - nejvíc spojů měla často
	// zkrácená trasa a sloupec končil v polovině. Mění se hodnoty CAS_JIZDY, počty zůstávají.
	do ..AssertLine(1, "0365001", "pages=39 zast=800 odj=844 pozn=235 md5=yBhnqVEGT9BcblETwSNetg==")
}

Method TestGold0365005()
{
	// Re-baseline 2026-07-31 (#13045): dtto - o 15 řádků legendy méně (pozn 183 → 168),
	// počty stránek, zastávek i odjezdů beze změny.
	// Re-baseline 2026-07-31 (#13008): + pole DOPRAVCI_SOUHRNNY_JR ve `values` → jen md5.
	// Re-baseline 2026-08-03 (#13088): + pole SLOUPEK, pak + SLOUPEK_SPOJU/POPIS/VARIANT
	// a nakonec + ZDROJ_STANOVISTE → dtto, jen md5.
	// Re-baseline 2026-08-03 (#13088 počtvrté): + CIS čísla zastávek (pak přejmenovaná) → dtto, jen md5.
	// Re-baseline 2026-08-03 (#13048 bod 1): + řádky legendy pro přestupy → ⚠️ mění i počty
	// (pozn 168 → 198, +30 na 38 stránek). Viz vysvětlení u gold 0365001.
	// Re-baseline 2026-08-03 (#12997): + pole BARVA_LINKY_2 → jen md5.
	// Re-baseline 2026-08-03 (#13093 bod 1): + VYLUKOVE_TEXTY, počty beze změny (parametr je vypnutý).
	// Re-baseline 2026-08-10 (#13045b): + NZ_KONFIG a NEVYPISOVAT_ZNACKY_SLOUPCU → jen md5, počty beze změny.
	// Re-baseline 2026-08-10 (#13045b etapa 2): + pole PRESETY_SLOUPCU → jen md5, počty beze změny.
	// Re-baseline 2026-08-12 (#12843): varianty jen z úseku od vývěskové zastávky - viz gold 0365001,
	// tady taky jen obsah TRASA_MIN, počty beze změny.
	// Re-baseline 2026-08-12 (#12843 podruhé): volba trasy pro jediný sloupec dle pokrytí - viz 0365001.
	do ..AssertLine(1, "0365005", "pages=38 zast=762 odj=498 pozn=236 md5=JzHFBIUre8MjFBN2GZq2VQ==")
}

/// Dva variantní sloupce se **nesmí** v tištěném úseku lišit jen před vývěskovou zastávkou - to dá
/// dva sloupce s identickými čísly a uživatel v nich hledá rozdíl, který tam není (TRANSDEV #12843).
/// Před opravou to na 0365001 bylo 56 duplicitních sloupců ze 116; digest sám tuhle vadu nechytí,
/// protože je to hash - tenhle test hlídá ten invariant přímo.
Method TestZadneDuplicitniVariantniSloupce()
{
	set db = ..#GOLDDB
	if '##class(%SYS.Namespace).Exists(db) {
		do $$$AssertTrue(1, "namespace "_db_" není na tomto serveru - přeskočeno")
		quit
	}
	do ..SetupSys(.sys)
	set sys("U","DATA") = db
	kill ^||ML
	set ^||ML("IN","SKUPINA_LINEK_ID") = 1
	set ^||ML("IN","LINKA_ID") = "0365001"
	do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).GetData(.sys)

	set duplicit = 0, sloupcu = 0, kde = "", key = ""
	for  {
		set key = $order(^||ML("OUT", key))
		quit:key=""
		continue:key="ERR"
		set page = ##class(%DynamicObject).%FromJSON(^||ML("OUT", key))
		set zast = page.rows.ZASTAVKY
		if '$isobject(zast) continue
		// Sloupce jako svislé řezy TRASA_MIN, ale jen od vývěskové zastávky dál (projeté se tisknou
		// jen na přání a tam se varianty legitimně liší).
		kill sloupec
		set poVyvesce = 0, it = zast.%GetIterator()
		while it.%GetNext(.i, .radek) {
			if $zcvt(radek.%Get("AKTUALNI_ZASTAVKA"),"L") = "true" set poVyvesce = 1
			continue:'poVyvesce
			set casy = radek.%Get("TRASA_MIN")
			continue:'$isobject(casy)
			for ci = 0:1:casy.%Size()-1 { set sloupec(ci) = $g(sloupec(ci))_"~"_casy.%Get(ci) }
		}
		kill videno
		set ci = ""
		for  {
			set ci = $order(sloupec(ci))
			quit:ci=""
			set sloupcu = sloupcu + 1
			if $data(videno(sloupec(ci))) {
				set duplicit = duplicit + 1
				if kde = "" set kde = "stránka "_key_" ("_page.values.%Get("POPIS_ZASTAVKA")_", směr "_page.values.SMER_")"
			}
			set videno(sloupec(ci)) = ""
		}
	}
	do $$$AssertTrue(sloupcu > 0, "gold 0365001 nedal žádný variantní sloupec - rozbitý fixture?")
	do $$$AssertEquals(duplicit, 0, "duplicitní variantní sloupce ("_duplicit_" z "_sloupcu_"), první: "_kde)
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
