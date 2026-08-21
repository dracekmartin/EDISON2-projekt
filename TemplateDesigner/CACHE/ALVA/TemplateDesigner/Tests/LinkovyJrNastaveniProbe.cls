Include (%occInclude, GENERAL)

/// Read-only sonda: **které přepínače tisku linkového JŘ jsou v datech reálně nastavené.**
///
/// **K čemu to je.** `L2` má ke každému nastavení test, který ho zapne i vypne. Nastavení uložená
/// v datech (globální číselník, nastavení u linky, nastavení pod linkou) ale z testu přepnout nejde -
/// porovnávají se tak, jak jsou. Bez tohoto měření by tedy platilo „ověřili jsme, co bylo v datech"
/// a nikdo by nevěděl, co zůstalo nepokryté. Výstup je podklad pro dvě věci:
///
/// 1. co jde ověřit hned (přepínač je někde zapnutý i vypnutý),
/// 2. u čeho se musí do reportu napsat **neověřeno** (nikde zapnutý není).
///
/// Sonda **nic nemění** a nevolá `GetData` - jen čte globály, takže projde všechny linky rychle.
/// Vrstvy odpovídají inventáři v `linkovy-knizni-jr-implementace-analyza.md` §13.2.
Class ALVA.TemplateDesigner.Tests.LinkovyJrNastaveniProbe
{

/// Firmy k prohledání, když se nezadá jiný seznam.
Parameter FIRMY = "CB_CSADAD,CB_GWBUS,KV_DP,VS_CSAD,ZL_KOVED,SKNI_MHDNITRA";

/// Nastavení u linky (`^JRL(sku,lin,"PATCH",<klíč>)`) - jednoduché klíče.
Parameter PATCHKLICE = "linIDS,nazMHD,netisknoutSloupecTC,tisknoutJenTcKdeSpojeZastavuji,podtrzeniPaJ,barevnyTiskSpoju,cisIDS,ZLOM,ZonaIdsDelZnk,Head,OBJ,PoznPodDopr,Plati";

/// Nastavení u linky pod podklíčem `MHD2`. ⚠️ Přes název je to **AD** nastavení - čte je AD engine.
Parameter MHD2KLICE = "netisknoutTC,netisknoutCislaSpoju,netisknoutSipky,netisknoutKladivkaAPodobne,umeleDatDoSekci,rozdelitUmeleSekceNaVlastniStranky";

/// Globální nastavení programu (`^JRSET("Definice",<klíč>)`) - per firma, ne per linka.
Parameter DEFINICEKLICE = "TiskDtm,PAtrSp,MistOR,MistNOD,ZonaIdsDelZnk,Print_Dopr_First,SDOP,PS_PrintSouhrnDoprLinYes";

/// Klíče, u kterých se sleduje **rozložení hodnot**, ne jen „nastaveno". Jen přepínače s krátkým
/// číselníkem - u volného textu (záhlaví, platnost) by výčet hodnot byl delší než celý souhrn
/// a k ničemu: tam stačí vědět, na kolika linkách je vyplněný.
Parameter HODNOTYKLICE = "linIDS,podtrzeniPaJ,cisIDS,barevnyTiskSpoju,netisknoutSloupecTC,tisknoutJenTcKdeSpojeZastavuji,ZonaIdsDelZnk,OBJ,TiskDtm,PAtrSp,MistOR,MistNOD,Print_Dopr_First,SDOP,PS_PrintSouhrnDoprLinYes";

/// Má se u klíče sledovat rozložení hodnot?
ClassMethod SledovatHodnoty(klic As %String) As %Boolean
{
	quit (","_..#HODNOTYKLICE_",") [ (","_klic_",")
}

/// Spočítá výskyt přepínačů. IN: `FIRMY` (nepovinné). OUT: řádky souhrnu.
ClassMethod Vyskyt(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set firmy = $get(^||ML("IN","FIRMY"))
		if firmy = "" set firmy = ..#FIRMY
		kill ^||ML, radky, pocet, hodnoty, celkem

		for fi = 1:1:$length(firmy, ",") {
			set db = $zstrip($piece(firmy, ",", fi), "<>W")
			continue:db=""
			if '##class(%SYS.Namespace).Exists(db) {
				set radky($increment(radky)) = "-- firma "_db_" na serveru není, přeskočena"
				continue
			}
			set firem = $get(firem) + 1

			// ── vrstva B: globální číselník (per firma) ─────────────────────
			for ki = 1:1:$length(..#DEFINICEKLICE, ",") {
				set klic = $piece(..#DEFINICEKLICE, ",", ki)
				set val = $get(^[db]JRSET("Definice", klic))
				continue:val=""
				set pocet("B", klic) = $get(pocet("B", klic)) + 1
				if ..SledovatHodnoty(klic) set hodnoty("B", klic, val) = $get(hodnoty("B", klic, val)) + 1
			}
			for smer = "P", "Z" {
				set val = $get(^[db]JRSET("Definice", "MHDPZ", smer))
				if val '= "" set pocet("B", "MHDPZ."_smer) = $get(pocet("B", "MHDPZ."_smer)) + 1
			}

			// ── vrstvy C a D: per linka a pod linkou ───────────────────────
			set sku = ""
			for  {
				set sku = $order(^[db]JRL(sku))
				quit:sku=""
				continue:'$isvalidnum(sku)
				set lin = ""
				for  {
					set lin = $order(^[db]JRL(sku, lin))
					quit:lin=""
					continue:'$data(^[db]JRL(sku, lin, "I"))
					set celkem = $get(celkem) + 1

					for ki = 1:1:$length(..#PATCHKLICE, ",") {
						set klic = $piece(..#PATCHKLICE, ",", ki)
						set val = $get(^[db]JRL(sku, lin, "PATCH", klic))
						continue:val=""
						set pocet("C", klic) = $get(pocet("C", klic)) + 1
						// U víceStavových se sleduje i rozložení hodnot - „nastaveno" u čtyřstavového
						// přepínače nestačí, potřebujeme vědět, které stavy v datech vůbec jsou.
						if ..SledovatHodnoty(klic) set hodnoty("C", klic, val) = $get(hodnoty("C", klic, val)) + 1
					}
					for ki = 1:1:$length(..#MHD2KLICE, ",") {
						set klic = $piece(..#MHD2KLICE, ",", ki)
						set val = $get(^[db]JRL(sku, lin, "MHD2", klic))
						continue:'+val
						set pocet("C", "MHD2."_klic) = $get(pocet("C", "MHD2."_klic)) + 1
					}
					if +$get(^[db]JRL(sku, lin, "PATCH", "TZPA", "BBP")) set pocet("C", "TZPA.BBP") = $get(pocet("C", "TZPA.BBP")) + 1
					for smer = "T", "Z" {
						if +$get(^[db]JRL(sku, lin, "PATCH", "KmsSmer", smer)) set pocet("C", "KmsSmer."_smer) = $get(pocet("C", "KmsSmer."_smer)) + 1
					}

					// vrstva D - u tarifního čísla
					set tc = ""
					for  {
						set tc = $order(^[db]JRL(sku, lin, "J", tc))
						quit:tc=""
						continue:'$isvalidnum(tc)
						for klic = "KnizniNetisknout", "KnizniNetisknoutTam", "KnizniNetisknoutZpet" {
							continue:'+$get(^[db]JRL(sku, lin, "J", tc, "MHD2", klic))
							set pocet("D", klic) = $get(pocet("D", klic)) + 1
							set dLinky("D", klic, db_" "_lin) = ""
						}
					}
					// vrstva D - u spoje
					for smer = "T", "Z" {
						set sp = ""
						for  {
							set sp = $order(^[db]JRL(sku, lin, smer, sp))
							quit:sp=""
							continue:'$isvalidnum(sp)
							continue:$get(^[db]JRL(sku, lin, smer, sp, "MHD2", "umeleDatDoSekce"))=""
							set pocet("D", "umeleDatDoSekce") = $get(pocet("D", "umeleDatDoSekce")) + 1
							set dLinky("D", "umeleDatDoSekce", db_" "_lin) = ""
						}
					}
				}
			}
		}

		set radky($increment(radky)) = "== prohledáno: firem "_+$get(firem)_", linek "_+$get(celkem)_" =="
		for vrstva = "B", "C", "D" {
			set radky($increment(radky)) = "-- vrstva "_vrstva_" "_$case(vrstva, "B":"(globální, per firma)", "C":"(u linky)", :"(pod linkou)")
			set klic = ""
			for  {
				set klic = $order(pocet(vrstva, klic))
				quit:klic=""
				set r = "   "_klic_" = "_pocet(vrstva, klic)
				// rozložení hodnot u víceStavových
				set h = "", rozlozeni = ""
				for  {
					set h = $order(hodnoty(vrstva, klic, h))
					quit:h=""
					set rozlozeni = rozlozeni_$select(rozlozeni="":"", 1:", ")_"„"_h_"""×"_hodnoty(vrstva, klic, h)
				}
				if rozlozeni '= "" set r = r_"   hodnoty: "_rozlozeni
				// u vrstvy D je zásadní, na KTERÉ lince to je - jinak není kde test postavit
				// Stačí několik příkladů - test se staví nad jednou linkou, ne nad seznamem.
				set kde = "", l = "", kolik = 0
				for  {
					set l = $order(dLinky(vrstva, klic, l))
					quit:l=""
					set kolik = kolik + 1
					continue:kolik>5
					set kde = kde_$select(kde="":"", 1:"; ")_l
				}
				if kde '= "" set r = r_"   příklady ("_kolik_"): "_kde
				set radky($increment(radky)) = r
			}
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
