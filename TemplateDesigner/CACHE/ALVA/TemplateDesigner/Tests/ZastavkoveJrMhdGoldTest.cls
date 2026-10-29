Include %occInclude

/// Základní regresní pojistka: 1:1 kontrola výstupu GetData agendy ZastavkoveJrMhd proti
/// zmrazenému "golden" digestu na pár reprezentativních MHD linkách CB_DP.
///
/// ⚠️ MHD data jsou multi-tenant (^[dataDB]ALVA.MHD.LSO(IdFa,...)) - gold je přibitý na
/// IdFa CB_DP × namespace CB_DP (stabilní historická data 2018, nízké riziko driftu).
/// Pre-push runner jede právě pod IdFa CB_DP → gold reálně běží (na rozdíl od AD/CB_CSADAD
/// není per-firma exotický namespace). Jiný server bez CB_DP → legitimní skip; namespace
/// PŘÍTOMNÝ ale linka chybí → FAIL (ne tichá zelená).
///
/// Přegenerování baseline po ZÁMĚRNÉ změně výstupu: spusť test, z hlášky „aktuální: <digest>"
/// vezmi nový digest a přepiš konstantu v příslušné TestGold* metodě.
Class ALVA.TemplateDesigner.Tests.ZastavkoveJrMhdGoldTest Extends ALVA.EDTesting.MLTestCase
{

/// Firma + namespace, kde žijí gold MHD data (MHD je multi-tenant → obojí = CB_DP).
Parameter GOLDDB = "CB_DP";

Parameter GOLDIDFA = "CB_DP";

Method TestGold1001()
{
	// Re-baseline 2026-08-31 (#13158): POPIS_DOPRAVCE už není "Dopravce: <název>", ale dopravce
	// s adresou a telefonem po řádcích (shodně s AD vývěskou); POPIS_SMER přejmenováno na
	// KONECNA_ZASTAVKA. Obojí mění **jen md5** - počty stránek, zastávek, odjezdů i poznámek
	// zůstaly shodné, tedy změněný obsah pole a přejmenování, ne změna chování.
	// Re-baseline 2026-09-14: stránky jdou po zastávkách a u každé oba směry za sebou, ne celý
	// směr Tam a za ním celý Zpět (shodně s AD vývěskou a se starým tiskem). ⚠️ **Mění se jen
	// md5**, počty pages/zast/odj/pozn jsou shodné do jednoho - doklad, že se změnilo pořadí
	// stránek a nic jiného.
	// Re-baseline 2026-10-23 (`B194a` C3): přestup na vlak jde písmenem „L" místo kódu fontu „x"
	// a jeho poznámka má kategorii „prestup". Jen md5, počty beze změny.
	// Re-baseline 2026-10-24 (`B194a` C5): každá stránka nese navíc pole NAZEV_DOPRAVCE. Jen md5,
	// počty beze změny.
	// Re-baseline 2026-10-27 (`B194` D3): každá stránka nese navíc pole VYCHOZI_ZASTAVKA. Jen md5,
	// počty beze změny.
	// Re-baseline 2026-10-27 (`B194c` D4): údaje dopravce po polích (ADRESA, TELEFON, EMAIL, ICO
	// _DOPRAVCE) jako ve všech agendách JŘ. Jen md5, počty beze změny.
	do ..AssertLine("1001", "pages=54 zast=1543 odj=6597 pozn=58 md5=aM2OsDckjrmDwloemdcWXw==")
}

Method TestGold1003()
{
	// Re-baseline 2026-08-31 (#13158) - viz gold 1001. Jen md5, počty beze změny.
	// Re-baseline 2026-09-14: pořadí stránek po zastávkách - viz gold 1001. Jen md5.
	// Re-baseline 2026-10-23: přestup písmenem - viz gold 1001. Jen md5.
	// Re-baseline 2026-10-24: pole NAZEV_DOPRAVCE - viz gold 1001. Jen md5.
	// Re-baseline 2026-10-27: pole VYCHOZI_ZASTAVKA - viz gold 1001. Jen md5.
	// Re-baseline 2026-10-27 (`B194c` D4): údaje dopravce po polích - viz gold 1001. Jen md5.
	do ..AssertLine("1003", "pages=20 zast=220 odj=7780 pozn=22 md5=mYIDuT7vH0nWQLAwLxcj/Q==")
}

Method TestGold1006()
{
	// Re-baseline 2026-08-31 (#13158) - viz gold 1001. Jen md5, počty beze změny.
	// Re-baseline 2026-09-14: pořadí stránek po zastávkách - viz gold 1001. Jen md5.
	// Re-baseline 2026-10-23: přestup písmenem - viz gold 1001. Jen md5.
	// Re-baseline 2026-10-24: pole NAZEV_DOPRAVCE - viz gold 1001. Jen md5.
	// Re-baseline 2026-10-27: pole VYCHOZI_ZASTAVKA - viz gold 1001. Jen md5.
	// Re-baseline 2026-10-27 (`B194c` D4): údaje dopravce po polích - viz gold 1001. Jen md5.
	do ..AssertLine("1006", "pages=53 zast=1458 odj=5982 pozn=58 md5=oQCenOGLg/YUCCud+5oMdw==")
}

/// `B194a` C3: přestup na vlak (CB_DP, zastávka Nádraží linky 11350) má v seznamu zastávek i v poznámkách
/// písmeno „L" a poznámka kategorii „prestup". Do 23. 10. šel kódem fontu „x", který návrhář tiskne
/// textem jako „na znamení", a volba „Přestupy" na něj nepůsobila.
Method TestPrestupNaVlakPismenem()
{
	set db = ..#GOLDDB
	if '##class(%SYS.Namespace).Exists(db) {
		do $$$AssertSkipped("namespace "_db_" není na tomto serveru")
		quit
	}
	do ##class(ALVA.TemplateDesigner.Tests.MhdLegacyOracle).SetupOur(.sys, ..#GOLDIDFA, db)
	kill ^||ML
	set ^||ML("IN","LINKA_ID") = "11350"
	do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd).GetData(.sys)
	set atributy = "", poznamky = "", p = ""
	for {
		set p = $order(^||ML("OUT",p), 1, json)
		quit:p=""
		continue:'$isvalidnum(p)
		set page = ##class(%DynamicObject).%FromJSON(json)
		set iz = page.rows.%Get("ZASTAVKY").%GetIterator()
		while iz.%GetNext(.zk, .z) {
			set ia = z.%Get("ATRIBUTY").%GetIterator()
			while ia.%GetNext(.ak, .a) { set:a.%Get("POPIS")["železniční" atributy(a.%Get("ZNAK")) = "" }
		}
		set ip = page.rows.%Get("POZNAMKY").%GetIterator()
		while ip.%GetNext(.pk, .pz) { set:pz.%Get("TEXT")["železniční" poznamky(pz.%Get("ZNAK")_"/"_pz.%Get("KATEGORIE")) = "" }
	}
	do $$$AssertTrue($data(atributy("L")) && '$data(atributy("x")), "značka přestupu v seznamu zastávek: L")
	do $$$AssertTrue($data(poznamky("L/prestup")) && ($order(poznamky(""))="L/prestup") && ($order(poznamky(""),-1)="L/prestup"), "poznámka přestupu: L s kategorií prestup")
}

/// Ověří digest linky proti očekávané hodnotě. Rozlišuje:
///   namespace chybí        → skip (jiný server, legitimní)
///   namespace je, linka ne  → FAIL (gold data zmizela / rozbitý fixture)
///   jinak                   → 1:1 digest assert
Method AssertLine(lin As %String, expected As %String)
{
	set db = ..#GOLDDB
	if '##class(%SYS.Namespace).Exists(db) {
		do $$$AssertTrue(1, "namespace "_db_" není na tomto serveru - gold "_lin_" přeskočen")
		quit
	}
	set idfa = ..#GOLDIDFA
	if '$data(^[db]ALVA.MHD.LSO(idfa, 1, "DTA", lin, "DTA", 1, "PlatiOd")) {
		do $$$AssertTrue(0, "gold linka "_lin_" chybí v "_db_"/"_idfa_" (namespace je, data ne) - rozbitý fixture?")
		quit
	}

	do ..SetupSys(.sys, idfa)
	set sys("U","DATA") = db
	kill ^||ML
	set ^||ML("IN","LINKA_ID") = lin
	do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd).GetData(.sys)

	set digest = ##class(ALVA.TemplateDesigner.Tests.GoldUtil).Digest()
	do $$$AssertEquals(digest, expected, "gold "_lin_" digest (aktuální: "_digest_")")
}

}
