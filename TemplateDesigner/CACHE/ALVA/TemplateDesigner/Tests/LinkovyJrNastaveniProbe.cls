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

/// Najde **jednu** linku, kde má klíč pod `PATCH` danou hodnotu. `hodnota` = `""` hledá linku,
/// kde klíč **není nastavený vůbec**. `jenSKm` = jen linka, která má nakonfigurované km sloupce
/// (bez toho by tvrzení „km se netisknou" vyšlo triviálně i tam, kde žádné nejsou).
/// Vrací 1 = našel, a plní `db`/`sku`/`lin`.
ClassMethod NajdiLinku(klic As %String, hodnota As %String, ByRef db, ByRef sku, ByRef lin, jenSKm As %Boolean = 0) As %Boolean
{
	set db = "", sku = "", lin = ""
	for fi = 1:1:$length(..#FIRMY, ",") {
		set firma = $piece(..#FIRMY, ",", fi)
		continue:'##class(%SYS.Namespace).Exists(firma)
		set s1 = ""
		for  {
			set s1 = $order(^[firma]JRL(s1))
			quit:s1=""
			continue:'$isvalidnum(s1)
			set l1 = ""
			for  {
				set l1 = $order(^[firma]JRL(s1, l1))
				quit:l1=""
				continue:'$data(^[firma]JRL(s1, l1, "I"))
				continue:$get(^[firma]JRL(s1, l1, "PATCH", klic))'=hodnota
				continue:jenSKm&&('+$get(^[firma]JRL(s1, l1, "PATCH", "KmsSmer", "T")))
				// Linka bez spojů by neřekla nic - výstup by byl prázdný z jiného důvodu.
				continue:'+$get(^[firma]JRL(s1, l1, "T"))
				// `quit` s argumentem uvnitř bloku `{ }` padá na <COMMAND> - vyskočí se přes `ret`,
				// což je tady i čitelnější: první nalezená linka je ta, kterou chceme.
				set db = firma, sku = s1, lin = l1
				ret 1
			}
		}
	}
	quit 0
}

/// Najde linku, u které **spoje mají v datech nastavenou barvu** (a linka je nepotlačuje).
/// Je to „předpoklad nad zdrojovými daty": bez něj by tvrzení „barvy posíláme" vyšlo nula i tam,
/// kde není co posílat, a nic by nedokazovalo. Vrací 1 = našel.
ClassMethod NajdiLinkuSBarvouSpoje(ByRef db, ByRef sku, ByRef lin) As %Boolean
{
	set db = "", sku = "", lin = ""
	for fi = 1:1:$length(..#FIRMY, ",") {
		set firma = $piece(..#FIRMY, ",", fi)
		continue:'##class(%SYS.Namespace).Exists(firma)
		set s1 = ""
		for  {
			set s1 = $order(^[firma]JRL(s1))
			quit:s1=""
			continue:'$isvalidnum(s1)
			set l1 = ""
			for  {
				set l1 = $order(^[firma]JRL(s1, l1))
				quit:l1=""
				continue:'$data(^[firma]JRL(s1, l1, "I"))
				// linka, která barvy potlačuje, by tvrzení popřela z jiného důvodu
				continue:+$get(^[firma]JRL(s1, l1, "PATCH", "barevnyTiskSpoju"))
				for smer = "T", "Z" {
					set sp = ""
					for  {
						set sp = $order(^[firma]JRL(s1, l1, smer, sp))
						quit:sp=""
						continue:'$isvalidnum(sp)
						set barva = $piece($get(^[firma]JRL(s1, l1, smer, sp)), "#", 12)
						continue:(barva="")||(barva="0;0;0")
						set db = firma, sku = s1, lin = l1
						ret 1
					}
				}
			}
		}
	}
	quit 0
}

/// Najde linku, která má **v označení zóny písmeno** a zóny se u ní tisknou. Druhý „předpoklad nad
/// zdrojovými daty": bez něj by „v zóně nejsou písmena" vyšlo i tam, kde žádná nebyla.
/// `odstranit` = 1 hledá linku, která má odstranění zapnuté, 0 vypnuté (přes vlastní i globální
/// nastavení - u linky se ukládá hodnota+1, 0 znamená „platí globální").
ClassMethod NajdiLinkuSPismenemVZone(odstranit As %Boolean, ByRef db, ByRef sku, ByRef lin) As %Boolean
{
	set db = "", sku = "", lin = ""
	for fi = 1:1:$length(..#FIRMY, ",") {
		set firma = $piece(..#FIRMY, ",", fi)
		continue:'##class(%SYS.Namespace).Exists(firma)
		set globalne = +$get(^[firma]JRSET("Definice", "ZonaIdsDelZnk"))
		set s1 = ""
		for  {
			set s1 = $order(^[firma]JRL(s1))
			quit:s1=""
			continue:'$isvalidnum(s1)
			set l1 = ""
			for  {
				set l1 = $order(^[firma]JRL(s1, l1))
				quit:l1=""
				continue:'$data(^[firma]JRL(s1, l1, "I"))
				// zóny se musí vůbec tisknout, jinak by výstup neměl kde písmeno mít
				set linIds = +$get(^[firma]JRL(s1, l1, "PATCH", "linIDS"))
				continue:(linIds'=1)&&(linIds'=2)
				set uLinky = +$get(^[firma]JRL(s1, l1, "PATCH", "ZonaIdsDelZnk"))
				set plati = $select(uLinky:uLinky - 1, 1:globalne)
				continue:plati'=odstranit
				// a v datech musí být písmeno, jinak tvrzení nic nedokazuje
				set poradi = "", maPismeno = 0
				for  {
					set poradi = $order(^[firma]JRL(s1, l1, "J", poradi))
					quit:poradi=""
					continue:'$isvalidnum(poradi)
					set zona = $piece($piece($get(^[firma]JRL(s1, l1, "J", poradi)), ":", 5), "#", 2)
					continue:zona=""
					continue:$zconvert(zona,"U")=$zconvert(zona,"L")
					set maPismeno = 1
					quit
				}
				continue:'maPismeno
				set db = firma, sku = s1, lin = l1
				ret 1
			}
		}
	}
	quit 0
}

/// Najde linku s **ručně nastaveným pořadím spojů**. `platne` = 1 hledá pořadí, které projde
/// kontrolou (a tedy se použije), 0 hledá rozbité (spoj v datech chybí v pořadí nebo naopak) -
/// tam se musí pořadí spočítat. Ručně nastavené je na 4 409 linkách z měřených firem.
ClassMethod NajdiLinkuSRucnimPoradim(platne As %Boolean, ByRef db, ByRef sku, ByRef lin) As %Boolean
{
    set db = "", sku = "", lin = ""
    for fi = 1:1:$length(..#FIRMY, ",")  {
        set firma = $piece(..#FIRMY, ",", fi)
        continue:'##class(%SYS.Namespace).Exists(firma)
        set s1 = ""
        for  {
            set s1 = $order(^[firma]JRL(s1))
            quit:s1=""
            continue:'$isvalidnum(s1)
            set l1 = ""
            for  {
                set l1 = $order(^[firma]JRL(s1, l1))
                quit:l1=""
                continue:'$data(^[firma]JRL(s1, l1, "I"))
                continue:'$data(^[firma]JRL(s1, l1, "PATCH", "SORT"))
                continue:'+$get(^[firma]JRL(s1, l1, "T"))
                set jePlatne = ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).RucniPoradiSpoju(firma, s1, l1, "T", .poradi, .pocet)
                continue:''jePlatne'=''platne
                set db = firma, sku = s1, lin = l1
                ret 1
            }
        }
    }
    quit 0
}

/// Vypíše ručně nastavené pořadí spojů jedné linky. IN: `DB`, `SKU`, `LIN`.
ClassMethod RucniPoradi(ByRef sys As %String) As %Status
{
    set err = ""
    try {
        $$$FillParams
        set db = $get(^||ML("IN","DB")), sku = $get(^||ML("IN","SKU")), lin = $get(^||ML("IN","LIN"))
        kill ^||ML, radky
        for smer = "T", "Z" {
            set vDatech = "", sp = ""
            for  {
                set sp = $order(^[db]JRL(sku, lin, smer, sp))
                quit:sp=""
                set vDatech = vDatech_$select(vDatech="":"", 1:",")_sp
            }
            set rucne = "", poradi = ""
            for  {
                set poradi = $order(^[db]JRL(sku, lin, "PATCH", "SORT", smer, poradi))
                quit:poradi=""
                set rucne = rucne_$select(rucne="":"", 1:",")_$get(^[db]JRL(sku, lin, "PATCH", "SORT", smer, poradi))
            }
            set radky($increment(radky)) = smer_" vDatech: "_$extract(vDatech,1,300)
            set radky($increment(radky)) = smer_" rucne:   "_$extract(rucne,1,300)
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

/// Měření k **pořadí spojů**: kolik linek má ruční pořadí, kolik jich je souhrnných, jak často
/// vede trasa přes víc záznamů a jak často je v trase pobyt (příjezd/čekání). Podklad k tomu,
/// které z pěti možných odchylek řazení má cenu řešit.
ClassMethod RazeniVyskyt(ByRef sys As %String) As %Status
{
    set err = ""
    try {
        $$$FillParams
        kill ^||ML, radky
        for fi = 1:1:$length(..#FIRMY, ",") {
            set db = $piece(..#FIRMY, ",", fi)
            continue:'##class(%SYS.Namespace).Exists(db)
            set linek = 0, sSort = 0, souhrn = 0, viceW = 0, pobyt = 0
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
                    set linek = linek + 1
                    if $data(^[db]JRL(sku, lin, "PATCH", "SORT")) set sSort = sSort + 1
                    if +$get(^[db]JRL(sku, lin, "I")) = 50 set souhrn = souhrn + 1
                    // trasa přes víc záznamů: legacy skládá W(c), W(c+.01), ... my čteme jen první
                    set c = ""
                    for  {
                        set c = $order(^[db]JRL(sku, lin, "W", c))
                        quit:c=""
                        if $data(^[db]JRL(sku, lin, "W", c + .01)) set viceW = viceW + 1  quit
                    }
                    // pobyt v trase: čas ve tvaru „příjezd/čekání"
                    set c = ""
                    for  {
                        set c = $order(^[db]JRL(sku, lin, "W", c))
                        quit:c=""
                        if $get(^[db]JRL(sku, lin, "W", c)) [ "/" set pobyt = pobyt + 1  quit
                    }
                }
            }
            set radky($increment(radky)) = db_": linek="_linek_" rucniPoradi="_sSort_" souhrnnych="_souhrn_" viceZaznamuTrasy="_viceW_" pobytVTrase="_pobyt
            set radky($increment(radky)) = db_": PS_PrintPorSpTR="_$get(^[db]JRSET("Definice","PS_PrintPorSpTR"))_" PS_PrintSouhrnPorSpPri="_$get(^[db]JRSET("Definice","PS_PrintSouhrnPorSpPri"))_" PS_PrintSouhrnPorSpTR="_$get(^[db]JRSET("Definice","PS_PrintSouhrnPorSpTR"))
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

/// Najde linky, na kterých má daný klíč danou hodnotu - podklad pro test, který se má o ten stav
/// opřít. IN: `KLIC` (klíč pod `PATCH`), `HODNOTA`, `FIRMY` (nepovinné), `MAX` (nepovinné).
ClassMethod Priklady(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set klic = $get(^||ML("IN","KLIC")), hledana = $get(^||ML("IN","HODNOTA"))
		set firmy = $get(^||ML("IN","FIRMY"))
		if firmy = "" set firmy = ..#FIRMY
		set maxPrikladu = +$get(^||ML("IN","MAX"))
		if 'maxPrikladu set maxPrikladu = 10
		kill ^||ML, radky
		set nalezeno = 0
		for fi = 1:1:$length(firmy, ",") {
			set db = $zstrip($piece(firmy, ",", fi), "<>W")
			continue:db=""
			continue:'##class(%SYS.Namespace).Exists(db)
			set sku = ""
			for  {
				set sku = $order(^[db]JRL(sku))
				quit:sku=""
				quit:nalezeno'<maxPrikladu
				continue:'$isvalidnum(sku)
				set lin = ""
				for  {
					set lin = $order(^[db]JRL(sku, lin))
					quit:lin=""
					quit:nalezeno'<maxPrikladu
					continue:'$data(^[db]JRL(sku, lin, "I"))
					continue:$get(^[db]JRL(sku, lin, "PATCH", klic))'=hledana
					set nalezeno = nalezeno + 1
					set radky($increment(radky)) = db_"~"_sku_"~"_lin
						_"   spoju T="_+$get(^[db]JRL(sku,lin,"T"))_" Z="_+$get(^[db]JRL(sku,lin,"Z"))
						_"   tc="_+$get(^[db]JRL(sku,lin,"J"))
						_"   kmSmer="_+$get(^[db]JRL(sku,lin,"PATCH","KmsSmer","T"))
				}
			}
		}
		if 'nalezeno set radky($increment(radky)) = "žádná linka s "_klic_" = "_hledana
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

/// Kolik spojů má v datech **nastavenou barvu**. Odpověď na otázku, jestli má smysl barvy spojů
/// vůbec posílat: přepínač „barevný tisk spojů" je u 1 662 linek, ale sám o sobě neříká, že někdo
/// barvu reálně zadal. IN: `FIRMY`, `MAX` (kolik linek per firma; 0 = vše). OUT: rozložení hodnot.
ClassMethod BarvySpoju(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set firmy = $get(^||ML("IN","FIRMY"))
		if firmy = "" set firmy = ..#FIRMY
		set maxLinek = +$get(^||ML("IN","MAX"))
		kill ^||ML, radky, rozlozeni
		set spoju = 0, sBarvou = 0, prvni = ""
		for fi = 1:1:$length(firmy, ",") {
			set db = $zstrip($piece(firmy, ",", fi), "<>W")
			continue:db=""
			continue:'##class(%SYS.Namespace).Exists(db)
			set sku = ""
			for  {
				set sku = $order(^[db]JRL(sku))
				quit:sku=""
				continue:'$isvalidnum(sku)
				set linek = 0, lin = ""
				for  {
					set lin = $order(^[db]JRL(sku, lin))
					quit:lin=""
					quit:maxLinek&&(linek'<maxLinek)
					continue:'$data(^[db]JRL(sku, lin, "I"))
					set linek = linek + 1
					for smer = "T", "Z" {
						set sp = ""
						for  {
							set sp = $order(^[db]JRL(sku, lin, smer, sp))
							quit:sp=""
							continue:'$isvalidnum(sp)
							set spoju = spoju + 1
							// Barva spoje je 12. „#"-pole záznamu ve tvaru „r;g;b"; 0;0;0 = černá,
							// tedy „nenastaveno". Ověřuje se hodnotou, ne jménem pole.
							set barva = $piece($get(^[db]JRL(sku, lin, smer, sp)), "#", 12)
							// Prázdný podskript ObjectScript nebere (<SUBSCRIPT>), proto zástupná hodnota.
							set klicB = $select(barva="":"(prázdné)", 1:barva)
							set rozlozeni(klicB) = $get(rozlozeni(klicB)) + 1
							continue:(barva="")||(barva="0;0;0")
							set sBarvou = sBarvou + 1
							if prvni = "" set prvni = db_" "_lin_" spoj "_sp_" = "_barva
						}
					}
				}
			}
		}
		set radky($increment(radky)) = "spojů "_spoju_", s nastavenou barvou "_sBarvou
		if prvni '= "" set radky($increment(radky)) = "první: "_prvni
		set h = "", vypsano = 0
		for  {
			set h = $order(rozlozeni(h))
			quit:h=""
			set vypsano = vypsano + 1
			continue:vypsano>12
			set radky($increment(radky)) = "   „"_h_""" x "_rozlozeni(h)
		}
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
