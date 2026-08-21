Include (%occInclude, GENERAL)

/// **Matice nastavení** linkového JŘ (vlna `L2`): ke každému přepínači z EDISONu test, který ověří,
/// že se podle něj tisk reálně chová - a to v **každém stavu**, který se v datech vyskytuje.
///
/// **Proč zvlášť od `LinkovyJrAdLegacyTest`.** Porovnání celé linky se starým modulem hlídá, že se
/// nelišíme, ale **neřekne, čím to je**: nastavení, které si nikdo na testovaných linkách nezapnul,
/// projde jako zelené a nikdo se nedozví, že ho ignorujeme. Tenhle test naopak vezme nastavení jako
/// vstup: **najde linku, kde ten stav je**, a tvrdí, co má být na výstupu.
///
/// **Nastavení uložená v datech se z testu přepnout nedají**, takže se nehledá „zapni a vypni", ale
/// „najdi linku, kde to zapnuté je, a druhou, kde není". Stavy, které v datech nejsou, se
/// **přeskočí a nahlásí** - nikdy se netváří jako ověřené.
/// Změřený výskyt: `linkovy-knizni-jr-implementace-analyza.md` §13.6.
Class ALVA.TemplateDesigner.Tests.LinkovyJrNastaveniTest Extends ALVA.EDTesting.MLTestCase
{

/// Čtyřstavový přepínač zóny × km (`PATCH,linIDS`): 0 i nenastaveno = jen km, 1 = jen zóna,
/// 2 = obojí, 3 = ani jedno (1:1 legacy `gPLSetWin`, `JRapiLinPrtWin.mac:1861-1868`).
///
/// Do 21. 8. jsme z toho čtli jen „je neprázdné → tiskni zónu", takže u hodnoty `3` (1 126 linek
/// v datech) jsme zónu tiskli, přestože ji starý modul netiskne, a km sloupce jsme neřídili vůbec.
Method TestZonyAKmDleLinIDS()
{
	do ..SetupSys(.sys)
	// Stavy přepínače: `hodnota|očekávaná zóna|očekávané km`, prázdná hodnota = nenastaveno.
	// Záměrně jako seznam, ne pole klíčované hodnotou - prázdný podskript je v ObjectScriptu past.
	set stavy = "|0|1;0|0|1;1|1|0;2|1|1;3|0|0"

	set overeno = 0, preskoceno = "", nalezy = ""
	for si = 1:1:$length(stavy, ";") {
		set stav = $piece(stavy, ";", si)
		set hodnota = $piece(stav, "|", 1)
		set cekanaZona = $piece(stav, "|", 2)
		set cekanaKm = $piece(stav, "|", 3)
		set popisHodnoty = $select(hodnota="":"nenastaveno", 1:"„"_hodnota_"""")

		// km sloupce musí být u linky nakonfigurované, jinak by „km se netisknou" vyšlo triviálně
		if '##class(ALVA.TemplateDesigner.Tests.LinkovyJrNastaveniProbe).NajdiLinku("linIDS", hodnota, .db, .sku, .lin, 1) {
			set preskoceno = preskoceno _ $select(preskoceno="":"", 1:", ") _ popisHodnoty
			continue
		}
		set overeno = overeno + 1
		kill parametry
		do ##class(ALVA.TemplateDesigner.Tests.LinkovyJrSadaTest).NactiLinku(.sys, db, sku, lin, .parametry)
		do ..ZmerZonyAKm(.maZonu, .maKm)
		set kde = db_" "_lin_" (linIDS "_popisHodnoty_")"
		if maZonu '= cekanaZona {
			set nalezy = nalezy _ $select(nalezy="":"", 1:" | ") _ kde _ ": zóna měla být "_cekanaZona_", je "_maZonu
		}
		if maKm '= cekanaKm {
			set nalezy = nalezy _ $select(nalezy="":"", 1:" | ") _ kde _ ": km měly být "_cekanaKm_", jsou "_maKm
		}
	}

	if 'overeno {
		do $$$AssertTrue(1, "žádná firma s linkami na tomto serveru - přeskočeno")
		quit
	}
	do $$$AssertEquals(nalezy, "", "přepínač zóny × km se nedodržuje: "_nalezy)
	// Stav, který v datech není, se NESMÍ tvářit jako ověřený - proto je v hlášce vidět.
	if preskoceno '= "" {
		do $$$AssertTrue(1, "ověřeno stavů: "_overeno_"; v datech nejsou a zůstávají NEOVĚŘENÉ: "_preskoceno)
	} else {
		do $$$AssertEquals(overeno > 0, 1, "ověřeno všech "_overeno_" stavů přepínače")
	}
}

/// Diagnostika mimo runner (`cache_query_cvar`): co pro každý stav přepínače reálně vyjde.
/// Runner hlásí jen pass/fail, tak je potřeba mít kde se podívat, čím to je.
ClassMethod DiagZonyAKm(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		kill ^||ML, radky
		set stavy = "|0|1;0|0|1;1|1|0;2|1|1;3|0|0"
		for si = 1:1:$length(stavy, ";") {
			set stav = $piece(stavy, ";", si)
			set hodnota = $piece(stav, "|", 1)
			set cekanaZona = $piece(stav, "|", 2), cekanaKm = $piece(stav, "|", 3)
			if '##class(ALVA.TemplateDesigner.Tests.LinkovyJrNastaveniProbe).NajdiLinku("linIDS", hodnota, .db, .sku, .lin, 1) {
				set radky($increment(radky)) = "linIDS „"_hodnota_""" = žádná vhodná linka"
				continue
			}
			kill parametry
			do ##class(ALVA.TemplateDesigner.Tests.LinkovyJrSadaTest).NactiLinku(.sys, db, sku, lin, .parametry)
			do ..ZmerZonyAKm(.maZonu, .maKm)
			set radky($increment(radky)) = "linIDS „"_hodnota_""" "_db_" "_sku_"/"_lin
				_"   zona="_maZonu_" (čekáno "_cekanaZona_")"
				_"   km="_maKm_" (čekáno "_cekanaKm_")"
				_"   kmSmerT="_+$get(^[db]JRL(sku,lin,"PATCH","KmsSmer","T"))
				_"   chyba="_$get(^||ML("OUT","ERR"))
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

/// Změří, jestli výstup v `^||ML("OUT")` nese zóny a km. 1 = aspoň jedna zastávka je má.
///
/// ⚠️ **Pomlčka se počítá.** Otázka není „má zastávka zónu", ale „tiskne se sloupec zón" - a legacy
/// u zapnutého sloupce bez hodnoty tiskne právě „-". Kdyby se pomlčka nepočítala, vyšla by linka
/// se zapnutými zónami, ale prázdnými hodnotami, jako „zóny se netisknou" (na tom test 21. 8. padl).
ClassMethod ZmerZonyAKm(ByRef maZonu, ByRef maKm)
{
	set maZonu = 0, maKm = 0, key = ""
	for  {
		set key = $order(^||ML("OUT", key))
		quit:key=""
		continue:key="ERR"
		set page = ##class(%DynamicObject).%FromJSON(^||ML("OUT", key))
		set zast = page.rows.%Get("ZASTAVKY")
		continue:'$isobject(zast)
		set it = zast.%GetIterator()
		while it.%GetNext(.i, .radek) {
			if radek.%Get("ZONY") '= "" set maZonu = 1
			set km = radek.%Get("KM")
			if $isobject(km), km.%Size() > 0 set maKm = 1
		}
	}
}

}
