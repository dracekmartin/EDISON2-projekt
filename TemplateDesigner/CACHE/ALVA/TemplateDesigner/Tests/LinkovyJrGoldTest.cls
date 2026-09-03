Include (%occInclude, GENERAL)

/// Regresní pojistka linkového JŘ: 1:1 kontrola výstupu `LinkovyJrAd.GetData` proti zmrazenému
/// digestu na reprezentativních linkách. Doplněk k `LinkovyJrAdLegacyTest` - ten hlídá shodu se
/// **starým modulem**, tenhle hlídá, že se nám výstup nehne **proti včerejšku** (a chytí to i na
/// serveru, kde starý modul není).
///
/// ⚠️ **Digest neplatí jako doklad parity** (pravidlo R6): je to hash našeho vlastního výstupu.
/// Když se ptáš „chováme se jako starý modul?", odpověď je v parity testu, ne tady.
///
/// Přegenerování baseline po ZÁMĚRNÉ změně výstupu: spusť test, z hlášky „aktuální: &lt;digest&gt;"
/// vezmi novou hodnotu a přepiš konstantu - a **do komentáře napiš proč**.
///
/// ⚠️ **Kdo přidá pole do `values` nebo do řádků, musí přebaselinovat hned ve svém commitu.**
/// Digest je MD5 celého JSON, takže **nové pole změní md5, ale ne počty** - a pád pak spadne na
/// někoho jiného za dva dny. Když počty sedí a liší se jen md5, hledej přidané pole, ne rozbitou logiku.
Class ALVA.TemplateDesigner.Tests.LinkovyJrGoldTest Extends ALVA.EDTesting.MLTestCase
{

/// Nejbohatší linka jádra: přípojné řádky, sloupec MHD, tři skupiny dnů, 43 spojů na stránku.
Method TestGold0330510()
{
	// Re-baseline 2026-08-24 (`L3`): do hlavičky přibylo pole s vzory rozložení sloupců.
	// ⚠️ **Mění se JEN md5**, počty jsou shodné - nové pole, ne ztracená data; přesně případ
	// popsaný v hlavičce téhle třídy, přebaselinováno v témž commitu.
	// Re-baseline 2026-08-24 (`L2`, pruh s dopravcem): text dopravce se skládá jako ve starém
	// tisku - části přes „, ", prázdná se přeskočí, telefon „tel. ". Dřív z toho u provozu bez
	// ulice vycházelo „…,,tel:386100111". ⚠️ **Mění se JEN md5**, počty jsou shodné.
	// Re-baseline 2026-08-24 (`L2`, zvýrazněná zastávka): do řádku zastávky přibylo pole
	// „Zvýrazněná zastávka". Nové pole mění md5 a nechává počty - přesně případ popsaný
	// v hlavičce téhle třídy, přebaselinováno v témž commitu.
	// Re-baseline 2026-08-24 (`L2b`, tvar názvu zastávky): název se skládá jako ve starém tisku -
	// plný tvar s dvojčárkou ve sloupci, zkrácený ve větě poznámky, a upravený název pro MHD jen
	// na městských linkách. ⚠️ **Mění se JEN md5**, počty jsou shodné: jde o jiný text jména,
	// ne o ztracené řádky. Porovnání se starým modulem je u názvů 100 % na třech linkách ze čtyř.
	// Re-baseline 2026-08-21 (`L2`, texty atributů spojů): věty legendy se skládají ze šablon
	// opsaných ze starého modulu a v jeho pořadí, ne z našeho volného opisu. ⚠️ **Mění se JEN md5** -
	// stránky, zastávky, spoje, časy i počet poznámek jsou do jednoho shodné, takže se nic
	// neztratilo, jen se změnil text a pořadí řádků legendy. Odtisk se hnul proto, že jsme se
	// starému modulu **přiblížili**: porovnání s ním je u poznámek na třech ze čtyř linek jádra
	// beze zbytku (43/43, 18/18, 20/20). Rozhodnutí Jan 21. 8.: v základu se to musí chovat
	// stejně jako starý modul.
	// Baseline 2026-08-19 (`L1`, první odtisk linkového JŘ). Sejmuto po opravě značky návaznosti
	// (navazující spoj se tiskl značkou pro vyčkání) - to je jediná změna výstupu, kterou tenhle
	// odtisk oproti stavu před L1 nese.
	// Re-baseline 2026-08-28 (`L6b`, směr u spoje): každý spoj nese `SMER`, aby u obousměrné
	// stránky prvek poznal, kam sloupec patří. ⚠️ **Mění se JEN md5** - počty do jednoho shodné,
	// tedy přesně ten případ „nové pole", který popisuje docstring třídy.
	// Re-baseline 2026-08-31 (#13158): pole POPIS_SMER přejmenováno na KONECNA_ZASTAVKA. Linkového
	// JŘ se týká **jen to přejmenování** - obsah pole ani skládání dat se nezměnily, takže parita
	// se starým tiskem zůstává nedotčená. Mění se jen md5, počty jsou shodné.
	do ..AssertLinka("CB_CSADAD", 1, "0330510", "pages=2 zast=48 spoju=84 casu=2015 pozn=63 md5=zPUK3i5R6Z2s9i0fYewPlg==")
}

/// Druhý tvar: bez skupin dnů, zdvojené řádky příjezd/odjezd, šest km sloupců.
Method TestGold0330511()
{
	// Re-baseline 2026-08-24 (`L3`): do hlavičky přibylo pole s vzory rozložení sloupců.
	// ⚠️ **Mění se JEN md5**, počty jsou shodné - nové pole, ne ztracená data; přesně případ
	// popsaný v hlavičce téhle třídy, přebaselinováno v témž commitu.
	// Re-baseline 2026-08-24 (`L2`, pruh s dopravcem): text dopravce se skládá jako ve starém
	// tisku - části přes „, ", prázdná se přeskočí, telefon „tel. ". Dřív z toho u provozu bez
	// ulice vycházelo „…,,tel:386100111". ⚠️ **Mění se JEN md5**, počty jsou shodné.
	// Re-baseline 2026-08-24 (`L2`, zvýrazněná zastávka): do řádku zastávky přibylo pole
	// „Zvýrazněná zastávka". Nové pole mění md5 a nechává počty - přesně případ popsaný
	// v hlavičce téhle třídy, přebaselinováno v témž commitu.
	// Re-baseline 2026-08-24 (`L2b`, tvar názvu zastávky): název se skládá jako ve starém tisku -
	// plný tvar s dvojčárkou ve sloupci, zkrácený ve větě poznámky, a upravený název pro MHD jen
	// na městských linkách. ⚠️ **Mění se JEN md5**, počty jsou shodné: jde o jiný text jména,
	// ne o ztracené řádky. Porovnání se starým modulem je u názvů 100 % na třech linkách ze čtyř.
	// Re-baseline 2026-08-21 (`L2`, texty atributů spojů): věty legendy se skládají ze šablon
	// opsaných ze starého modulu a v jeho pořadí, ne z našeho volného opisu. ⚠️ **Mění se JEN md5** -
	// stránky, zastávky, spoje, časy i počet poznámek jsou do jednoho shodné, takže se nic
	// neztratilo, jen se změnil text a pořadí řádků legendy. Odtisk se hnul proto, že jsme se
	// starému modulu **přiblížili**: porovnání s ním je u poznámek na třech ze čtyř linek jádra
	// beze zbytku (43/43, 18/18, 20/20). Rozhodnutí Jan 21. 8.: v základu se to musí chovat
	// stejně jako starý modul.
	// Baseline 2026-08-19 (`L1`) - dtto.
	// Re-baseline 2026-08-28 (`L6b`, směr u spoje) - ⚠️ **mění se JEN md5**, počty shodné.
	// Re-baseline 2026-08-31 (#13158) - přejmenování POPIS_SMER, viz gold 0330510. Jen md5.
	do ..AssertLinka("CB_CSADAD", 1, "0330511", "pages=2 zast=62 spoju=17 casu=246 pozn=25 md5=KAsUv3xDMGZwe/pPRi9lmA==")
}

/// Třetí tvar, jiná firma: největší linka jádra (99 spojů, 53 na stránku, osm km sloupců).
/// Záměrně z jiné firmy než dvě předchozí - jeden zákazník nese jen svůj tvar dat.
Method TestGold0410107()
{
	// Re-baseline 2026-08-24 (`L3`): do hlavičky přibylo pole s vzory rozložení sloupců.
	// ⚠️ **Mění se JEN md5**, počty jsou shodné - nové pole, ne ztracená data; přesně případ
	// popsaný v hlavičce téhle třídy, přebaselinováno v témž commitu.
	// Re-baseline 2026-08-24 (`L2`, pruh s dopravcem): text dopravce se skládá jako ve starém
	// tisku - části přes „, ", prázdná se přeskočí, telefon „tel. ". Dřív z toho u provozu bez
	// ulice vycházelo „…,,tel:386100111". ⚠️ **Mění se JEN md5**, počty jsou shodné.
	// Re-baseline 2026-08-24 (`L2`, zvýrazněná zastávka): do řádku zastávky přibylo pole
	// „Zvýrazněná zastávka". Nové pole mění md5 a nechává počty - přesně případ popsaný
	// v hlavičce téhle třídy, přebaselinováno v témž commitu.
	// Re-baseline 2026-08-24 (`L2b`, tvar názvu zastávky): název se skládá jako ve starém tisku -
	// plný tvar s dvojčárkou ve sloupci, zkrácený ve větě poznámky, a upravený název pro MHD jen
	// na městských linkách. ⚠️ **Mění se JEN md5**, počty jsou shodné: jde o jiný text jména,
	// ne o ztracené řádky. Porovnání se starým modulem je u názvů 100 % na třech linkách ze čtyř.
	// Re-baseline 2026-08-21 (`L2`, pořadí spojů): řazení se skládá jako legacy `mkPoradiSp` -
	// tarifními čísly se u „tam" jde od konce, pro řazení se bere příjezd (ne odjezd) a respektuje
	// se ruční pořadí u linky. ⚠️ **Mění se JEN md5**, všechny počty jsou shodné: je to přeskládané
	// pořadí sloupců, ne ztracená data. Porovnání se starým modulem je u pořadí spojů teď 7/7.
	// Baseline 2026-08-19 (`L1`).
	// Re-baseline 2026-08-28 (`L6b`, směr u spoje) - ⚠️ **mění se JEN md5**, počty shodné.
	// Re-baseline 2026-08-31 (#13158) - přejmenování POPIS_SMER, viz gold 0330510. Jen md5.
	do ..AssertLinka("KV_DP", 1, "0410107", "pages=2 zast=98 spoju=99 casu=2362 pozn=34 md5=leCHwU7aiAn9DpZhLbxwDw==")
}

/// Čtvrtý tvar: 109 spojů na **jediné** stránce (jeden směr), bez zón a bez sloupce MHD.
Method TestGold0945001()
{
	// Re-baseline 2026-08-24 (`L3`): do hlavičky přibylo pole s vzory rozložení sloupců.
	// ⚠️ **Mění se JEN md5**, počty jsou shodné - nové pole, ne ztracená data; přesně případ
	// popsaný v hlavičce téhle třídy, přebaselinováno v témž commitu.
	// Re-baseline 2026-08-24 (`L2`, zvýrazněná zastávka): do řádku zastávky přibylo pole
	// „Zvýrazněná zastávka". Nové pole mění md5 a nechává počty - přesně případ popsaný
	// v hlavičce téhle třídy, přebaselinováno v témž commitu.
	// Re-baseline 2026-08-24 (`L2b`, tvar názvu zastávky): název se skládá jako ve starém tisku -
	// plný tvar s dvojčárkou ve sloupci, zkrácený ve větě poznámky, a upravený název pro MHD jen
	// na městských linkách. ⚠️ **Mění se JEN md5**, počty jsou shodné: jde o jiný text jména,
	// ne o ztracené řádky. Porovnání se starým modulem je u názvů 100 % na třech linkách ze čtyř.
	// Re-baseline 2026-08-21 (`L2`, pořadí spojů): řazení se skládá jako legacy `mkPoradiSp` -
	// tarifními čísly se u „tam" jde od konce, pro řazení se bere příjezd (ne odjezd) a respektuje
	// se ruční pořadí u linky. ⚠️ **Mění se JEN md5**, všechny počty jsou shodné: je to přeskládané
	// pořadí sloupců, ne ztracená data. Porovnání se starým modulem je u pořadí spojů teď 7/7.
	// Re-baseline 2026-08-21 (`L2`, texty atributů spojů): věty legendy se skládají ze šablon
	// opsaných ze starého modulu a v jeho pořadí, ne z našeho volného opisu. ⚠️ **Mění se JEN md5** -
	// stránky, zastávky, spoje, časy i počet poznámek jsou do jednoho shodné, takže se nic
	// neztratilo, jen se změnil text a pořadí řádků legendy. Odtisk se hnul proto, že jsme se
	// starému modulu **přiblížili**: porovnání s ním je u poznámek na třech ze čtyř linek jádra
	// beze zbytku (43/43, 18/18, 20/20). Rozhodnutí Jan 21. 8.: v základu se to musí chovat
	// stejně jako starý modul.
	// Baseline 2026-08-19 (`L1`).
	// Re-baseline 2026-08-28 (`L6b`, směr u spoje) - ⚠️ **mění se JEN md5**, počty shodné.
	// Re-baseline 2026-08-31 (#13158) - přejmenování POPIS_SMER, viz gold 0330510. Jen md5.
	do ..AssertLinka("VS_CSAD", 1, "0945001", "pages=1 zast=33 spoju=109 casu=2175 pozn=14 md5=+GoXgHiKOpjvRFWyU7e9SQ==")
}

/// Ověří digest jedné linky proti očekávané hodnotě. Rozlišuje:
///   firma chybí            → skip (jiný server, legitimní)
///   firma je, linka ne     → FAIL (gold data zmizela / rozbitý fixture)
///   jinak                  → 1:1 digest assert
Method AssertLinka(db As %String, sku As %String, lin As %String, ocekavano As %String)
{
	if '##class(%SYS.Namespace).Exists(db) {
		do $$$AssertTrue(1, "firma "_db_" není na tomto serveru - gold "_lin_" přeskočen")
		quit
	}
	if '$data(^[db]JRL(sku, lin, "I")) {
		do $$$AssertTrue(0, "gold linka "_lin_" chybí v "_db_" (firma je, data ne) - rozbitý fixture?")
		quit
	}
	do ..SetupSys(.sys)
	kill parametry
	do ##class(ALVA.TemplateDesigner.Tests.LinkovyJrSadaTest).NactiLinku(.sys, db, sku, lin, .parametry)
	set digest = ##class(ALVA.TemplateDesigner.Tests.GoldUtil).DigestLinkovy()
	do $$$AssertEquals(digest, ocekavano, "gold "_lin_" digest (aktuální: "_digest_")")
}

/// Vypíše aktuální digesty linek jádra - podklad pro (pře)baseline, ne test.
/// Voláno přes `cache_query_cvar`, ne z runneru.
ClassMethod Digesty(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		kill ^||ML, radky
		do ##class(ALVA.TemplateDesigner.Tests.LinkovyJrSadaTest).Jadro(.jadro)
		kill parametry
		set i = ""
		for  {
			set i = $order(jadro(i))
			quit:i=""
			set db = $piece(jadro(i),"~",1), sku = $piece(jadro(i),"~",2), lin = $piece(jadro(i),"~",3)
			continue:'##class(ALVA.TemplateDesigner.Tests.LinkovyJrSadaTest).NactiLinku(.sys, db, sku, lin, .parametry)
			set radky($increment(radky)) = db_" "_lin_" = "_##class(ALVA.TemplateDesigner.Tests.GoldUtil).DigestLinkovy()
		}
		kill ^||ML
		set idx = ""
		for  {
			set idx = $order(radky(idx))
			quit:idx=""
			set ^||ML("OUT", $increment(^||ML("OUT"))) = radky(idx)
		}
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

}
