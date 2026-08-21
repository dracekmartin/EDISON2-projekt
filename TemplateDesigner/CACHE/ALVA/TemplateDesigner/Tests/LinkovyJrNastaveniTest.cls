Include (%occInclude, GENERAL)

/// **Matice nastavení** linkového JŘ (vlna `L2`): ke každému přepínači z EDISONu se ověří, že se
/// podle něj tisk reálně chová - a to v **každém stavu, který se v datech vyskytuje**.
///
/// **Proč zvlášť od `LinkovyJrAdLegacyTest`.** Porovnání celé linky se starým modulem hlídá, že se
/// nelišíme, ale **neřekne, čím to je**: nastavení, které si nikdo na testovaných linkách nezapnul,
/// projde jako zelené a nikdo se nedozví, že ho ignorujeme. Tenhle test bere nastavení jako **vstup**:
/// najde linku, kde ten stav je, a tvrdí, co má být na výstupu.
///
/// ── Jak se přidá další nastavení ──────────────────────────────────────────────
///
/// **Jeden řádek do `Matice`**, nic víc; když k tomu chybí měření, přidá se jedna větev do `Zmer`.
/// Formát řádku: `klíč | hodnota | měření | očekáváno | stav`
///
/// - **klíč** - jméno pod `^JRL(sku,lin,"PATCH",...)`
/// - **hodnota** - hodnota nastavení; prázdná = nenastaveno
/// - **měření** - co se na výstupu měří (viz `Zmer`)
/// - **očekáváno** - `0` nebo `1`; **`?`** = jen změřit a vypsat, netvrdit
/// - **stav** - `plati` = rozdíl je pád · `dluh:<krok> <důvod>` = jen se hlásí
///
/// ⚠️ **`?` a `dluh` nejsou úhybný manévr, jsou to dvě různé pravdy.** `?` znamená „z dat to nejde
/// tvrdit" - nastavení říká „neodstraňuj písmena", ale linka žádná mít nemusí, takže z jejich
/// absence nic nevyplývá. `dluh` znamená „my to ještě neumíme" a má u sebe krok, ve kterém zmizí.
/// Kdyby se obojí slilo do „netestuje se", nikdo by za měsíc nepoznal, co je nedodělek.
///
/// ⚠️ **Past, na kterou matice sama umí najet:** u nastavení, které něco **potlačuje**, je nulové
/// měření splněné i tehdy, když tu vlastnost vůbec neumíme („žádné barvy" vyjde samo, když barvy
/// neposíláme). Takový řádek musí být `?`, dokud nejde doplnit předpoklad nad **zdrojovými** daty
/// („linka má u spojů barvu"). Jinak by matice tvrdila něco, co jsme neudělali.
///
/// Stavy, které v datech nejsou, se **hlásí jako neověřené** - nikdy se netváří jako pokryté.
/// Změřený výskyt nastavení v datech: `linkovy-knizni-jr-implementace-analyza.md` §13.6.
Class ALVA.TemplateDesigner.Tests.LinkovyJrNastaveniTest Extends ALVA.EDTesting.MLTestCase
{

/// Matice: co se u kterého nastavení a stavu čeká na výstupu.
///
/// Zdroj významů je legacy `gPLSetWin` (`JRapiLinPrtWin.mac:1840-1897`); u každé skupiny je
/// v komentáři, co ta hodnota znamená, ať se to nemusí dohledávat ve zdroji starého modulu.
ClassMethod Matice(ByRef m)
{
	kill m

	// ── zóny × km: čtyřstavový přepínač (`:1861-1868`) ──────────────────
	// 0 i nenastaveno = jen km · 1 = jen zóna · 2 = obojí · 3 = ani jedno
	// Opraveno 21. 8.; dřív se čtlo jen „je neprázdné → tiskni zónu".
	set m($increment(m)) = "linIDS||zona|0|plati"
	set m($increment(m)) = "linIDS||km|1|plati"
	set m($increment(m)) = "linIDS|0|zona|0|plati"
	set m($increment(m)) = "linIDS|0|km|1|plati"
	set m($increment(m)) = "linIDS|1|zona|1|plati"
	set m($increment(m)) = "linIDS|1|km|0|plati"
	set m($increment(m)) = "linIDS|2|zona|1|plati"
	set m($increment(m)) = "linIDS|2|km|1|plati"
	set m($increment(m)) = "linIDS|3|zona|0|plati"
	set m($increment(m)) = "linIDS|3|km|0|plati"

	// ── schovat sloupec tarifních čísel ─────────────────────────────────
	// U linky 1 = schovat. Zapnuto na 377 linkách.
	set m($increment(m)) = "netisknoutSloupecTC|1|tc|0|dluh:L2-krok4 třístav „Automaticky (dle linky)"" ještě není"
	set m($increment(m)) = "netisknoutSloupecTC|0|tc|1|plati"

	// ── vyhodit řádky, kde žádný spoj nestaví ───────────────────────────
	// U linky 1 = vyhodit. Zapnuto na 362 linkách.
	set m($increment(m)) = "tisknoutJenTcKdeSpojeZastavuji|1|radkyBezZastaveni|0|dluh:L2-krok6 filtr řádků ještě neumíme"
	set m($increment(m)) = "tisknoutJenTcKdeSpojeZastavuji|0|radkyBezZastaveni|?|plati"

	// ── barvy spojů ─────────────────────────────────────────────────────
	// U linky 1 = tiskni černě (barvy potlačit). Zapnuto na 24 linkách.
	//
	// ⚠️ **Obojí je `?`, a je to poctivější než „OK".** Nulové měření je u potlačovacího nastavení
	// splněné i tehdy, když tu vlastnost **vůbec neumíme**: barvy neposíláme, takže „žádné barvy"
	// vyjde samo a tvrdilo by to něco, co jsme neudělali. Tvrdit to půjde teprve nad linkou, u které
	// se ví, že **spoje barvu v datech mají** - to je předpoklad nad zdrojovými daty, který matice
	// zatím neumí vyjádřit. Dluh je vedený v `§13.6`, ne tady jako falešná zelená.
	set m($increment(m)) = "barevnyTiskSpoju|1|barvySpoju|?|plati"
	set m($increment(m)) = "barevnyTiskSpoju|0|barvySpoju|?|plati"

	// ── odstranit písmena z označení zóny ───────────────────────────────
	// U linky se ukládá hodnota+1 a přebíjí globální (`:1847`): 1 → neodstraňovat, 2 → odstraňovat,
	// 0 nebo nenastaveno → platí globální nastavení.
	// ⚠️ Táž past jako u barev: „v zóně nejsou písmena" vyjde i na lince, jejíž zóny žádná písmena
	// nemají. Potřebuje předpoklad „linka má v zóně písmeno", proto zatím jen měříme.
	set m($increment(m)) = "ZonaIdsDelZnk|2|zonaPismena|?|plati"
	set m($increment(m)) = "ZonaIdsDelZnk|1|zonaPismena|?|plati"
}

/// Hlavní brána matice: **žádný rozdíl u nastavení, které už umíme**.
Method TestMaticeNastaveni()
{
	do ..SetupSys(.sys)
	do ..Matice(.m)

	set nalezy = "", dluhy = 0, neovereno = "", overeno = 0, jenZmereno = 0, i = ""
	for  {
		set i = $order(m(i))
		quit:i=""
		set klic = $piece(m(i), "|", 1)
		set hodnota = $piece(m(i), "|", 2)
		set mereni = $piece(m(i), "|", 3)
		set cekano = $piece(m(i), "|", 4)
		set stav = $piece(m(i), "|", 5)
		set popis = klic_"=„"_hodnota_""" → "_mereni

		// Km sloupce musí být u linky nakonfigurované, jinak by tvrzení „km se netisknou" vyšlo
		// triviálně i tam, kde žádné nejsou.
		if '##class(ALVA.TemplateDesigner.Tests.LinkovyJrNastaveniProbe).NajdiLinku(klic, hodnota, .db, .sku, .lin, mereni = "km") {
			set neovereno = neovereno _ $select(neovereno="":"", 1:", ") _ popis
			continue
		}
		kill parametry
		do ##class(ALVA.TemplateDesigner.Tests.LinkovyJrSadaTest).NactiLinku(.sys, db, sku, lin, .parametry)
		set zmereno = ..Zmer(mereni)

		if cekano = "?" {
			set jenZmereno = jenZmereno + 1
			continue
		}
		if zmereno = cekano {
			set overeno = overeno + 1
			continue
		}
		if $piece(stav, ":", 1) = "dluh" {
			set dluhy = dluhy + 1
			continue
		}
		set nalezy = nalezy _ $select(nalezy="":"", 1:" | ") _ db_" "_lin_" "_popis_": čekáno "_cekano_", změřeno "_zmereno
	}

	if 'overeno && (nalezy = "") {
		do $$$AssertTrue(1, "žádná linka k ověření na tomto serveru - přeskočeno")
		quit
	}
	do $$$AssertEquals(nalezy, "", "nastavení se nedodržuje: "_nalezy)
	// Souhrn je součástí výsledku, ne ozdoba: „zelená" musí říct, kolik toho reálně kryje.
	do $$$AssertNotEquals(overeno, "", "ověřeno "_overeno_", jen změřeno "_jenZmereno_", známých dluhů "_dluhy)
	if neovereno '= "" {
		do $$$AssertTrue(1, "v datech se nevyskytuje, zůstává NEOVĚŘENÉ: "_neovereno)
	}
}

/// Známý dluh, který už se chová správně, je **mrtvý záznam** - ať se z matice přepíše na `plati`
/// a začne se hlídat. Bez toho by tam dluhy zůstaly navěky a matice by tvrdila méně, než umíme.
Method TestZadnyDluhNeniJizSplneny()
{
	do ..SetupSys(.sys)
	do ..Matice(.m)
	set splnene = "", i = ""
	for  {
		set i = $order(m(i))
		quit:i=""
		continue:$piece($piece(m(i), "|", 5), ":", 1)'="dluh"
		set klic = $piece(m(i), "|", 1), hodnota = $piece(m(i), "|", 2)
		set mereni = $piece(m(i), "|", 3), cekano = $piece(m(i), "|", 4)
		continue:'##class(ALVA.TemplateDesigner.Tests.LinkovyJrNastaveniProbe).NajdiLinku(klic, hodnota, .db, .sku, .lin, mereni = "km")
		kill parametry
		do ##class(ALVA.TemplateDesigner.Tests.LinkovyJrSadaTest).NactiLinku(.sys, db, sku, lin, .parametry)
		continue:..Zmer(mereni)'=cekano
		set splnene = splnene _ $select(splnene="":"", 1:", ") _ klic_"=„"_hodnota_""" → "_mereni
	}
	do $$$AssertEquals(splnene, "", "tyhle dluhy už jsou splněné, přepiš je v matici na „plati"": "_splnene)
}

/// Změří jednu vlastnost výstupu v `^||ML("OUT")`. Vrací 0/1.
/// Nová položka matice, která potřebuje nové měření, přidá **jednu větev sem** - ne vlastní test.
ClassMethod Zmer(mereni As %String) As %Integer
{
	set vysledek = 0, key = ""
	for  {
		set key = $order(^||ML("OUT", key))
		quit:key=""
		continue:key="ERR"
		set page = ##class(%DynamicObject).%FromJSON(^||ML("OUT", key))
		set zast = page.rows.%Get("ZASTAVKY")
		set spoje = page.rows.%Get("SPOJE")
		continue:'$isobject(zast)

		if (mereni = "zona") || (mereni = "km") || (mereni = "tc") || (mereni = "zonaPismena") {
			set it = zast.%GetIterator()
			while it.%GetNext(.i, .radek) {
				// ⚠️ U zóny se pomlčka POČÍTÁ: otázka není „má zastávka zónu", ale „tiskne se
				// sloupec zón", a legacy u zapnutého sloupce bez hodnoty tiskne právě „-".
				if mereni = "zona", radek.%Get("ZONY") '= "" set vysledek = 1
				if mereni = "tc", radek.%Get("TC") '= "" set vysledek = 1
				if mereni = "km" {
					set km = radek.%Get("KM")
					if $isobject(km), km.%Size() > 0 set vysledek = 1
				}
				if mereni = "zonaPismena" {
					// Písmeno se pozná tím, že se velká a malá varianta textu liší.
					set z = radek.%Get("ZONY")
					if $zcvt(z, "U") '= $zcvt(z, "L") set vysledek = 1
				}
			}
		}
		elseif mereni = "barvySpoju" {
			continue:'$isobject(spoje)
			set it = spoje.%GetIterator()
			while it.%GetNext(.i, .spoj) {
				if spoj.%Get("FORE_COLOR") '= "" set vysledek = 1
			}
		}
		elseif mereni = "radkyBezZastaveni" {
			// Řádek, na kterém nemá čas ani jeden spoj - přesně to, co legacy umí vyhodit.
			continue:'$isobject(spoje)
			for zi = 0:1:zast.%Size()-1 {
				set maCas = 0
				for si = 0:1:spoje.%Size()-1 {
					set casy = spoje.%Get(si).%Get("CASY")
					continue:'$isobject(casy)
					continue:zi'<casy.%Size()
					if casy.%Get(zi) [ ":" set maCas = 1
				}
				if 'maCas set vysledek = 1
			}
		}
	}
	quit vysledek
}

/// Diagnostika mimo runner (`cache_query_cvar`): celá matice s tím, co reálně vyšlo.
/// Runner hlásí jen pass/fail, takže je potřeba mít kde se podívat, čím to je.
ClassMethod Diag(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		do ..Matice(.m)
		kill ^||ML, radky
		set i = ""
		for  {
			set i = $order(m(i))
			quit:i=""
			set klic = $piece(m(i), "|", 1), hodnota = $piece(m(i), "|", 2)
			set mereni = $piece(m(i), "|", 3), cekano = $piece(m(i), "|", 4), stav = $piece(m(i), "|", 5)
			if '##class(ALVA.TemplateDesigner.Tests.LinkovyJrNastaveniProbe).NajdiLinku(klic, hodnota, .db, .sku, .lin, mereni = "km") {
				set radky($increment(radky)) = klic_"=„"_hodnota_""" "_mereni_" -> V DATECH NENÍ"
				continue
			}
			kill parametry
			do ##class(ALVA.TemplateDesigner.Tests.LinkovyJrSadaTest).NactiLinku(.sys, db, sku, lin, .parametry)
			set zmereno = ..Zmer(mereni)
			set verdikt = $select(cekano="?":"jen změřeno", zmereno=cekano:"OK", $piece(stav,":",1)="dluh":"DLUH", 1:"ROZDÍL")
			set radky($increment(radky)) = klic_"=„"_hodnota_""" "_mereni_" -> "_zmereno_" (čekáno "_cekano_") "_verdikt_"   "_db_" "_sku_"/"_lin
		}
		// Poslední čtení dat nechalo v OUT celý výstup linky - bez vyklizení by se souhrn přilepil
		// za něj a diagnostika by vrátila megabajt JSONu.
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
