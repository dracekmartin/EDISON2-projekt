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

/// Projde jádro sady a vypíše, jak vypadají **přípojné řádky** (vyčká/navazuje zobrazené jako
/// řádek v tabulce, ne jako poznámka pod ní) - podklad k rozhodnutí o jejich slučování.
ClassMethod PripojneRadky(ByRef sys As %String) As %Status
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
            set radku = 0, ukazka = "", key = ""
            kill naTc
            for  {
                set key = $order(^||ML("OUT", key))
                quit:key=""
                continue:key="ERR"
                set page = ##class(%DynamicObject).%FromJSON(^||ML("OUT", key))
                set spoje = page.rows.%Get("SPOJE")
                continue:'$isobject(spoje)
                set it = spoje.%GetIterator()
                while it.%GetNext(.si, .spoj) {
                    set pr = spoj.%Get("PRIPOJE")
                    continue:'$isobject(pr)
                    set it2 = pr.%GetIterator()
                    while it2.%GetNext(.pi, .p) {
                        continue:p.%Get("RADEK")'=1
                        set radku = radku + 1
                        set naTc(p.%Get("PORADI")) = $get(naTc(p.%Get("PORADI"))) + 1
                        if ukazka = "" {
                            set ukazka = "tč "_p.%Get("PORADI")_" | "_p.%Get("TYP")_" | linka "_p.%Get("LINKA")_" spoj "_p.%Get("SPOJ")_" | "_p.%Get("CAS")_" | "_p.%Get("TEXT")
                        }
                    }
                }
            }
            // kolik tarifních čísel nese víc než jeden přípojný řádek - tam by se slučovalo
            set viceNaJednom = 0, tc = ""
            for  {
                set tc = $order(naTc(tc))
                quit:tc=""
                if naTc(tc) > 1 set viceNaJednom = viceNaJednom + 1
            }
            set radky($increment(radky)) = db_" "_lin_": přípojných řádků="_radku_", tč s víc než jedním="_viceNaJednom
            if ukazka '= "" set radky($increment(radky)) = "    "_ukazka
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

/// Kolik spojů je v které skupině dnů - podklad k tomu, jestli starý modul řeže po skupinách,
/// nebo podle šířky. IN: `DB`, `SKU`, `LIN`.
ClassMethod SpojuVeSkupinach(ByRef sys As %String) As %Status
{
    set err = ""
    try {
        $$$FillParams
        set db = $get(^||ML("IN","DB")), sku = $get(^||ML("IN","SKU")), lin = $get(^||ML("IN","LIN"))
        kill ^||ML, radky, parametry
        set parametry("DELENI_SLOUPCU") = "Pracovní dny / Soboty / Neděle+svátky"
        do ##class(ALVA.TemplateDesigner.Tests.LinkovyJrSadaTest).NactiLinku(.sys, db, sku, lin, .parametry)
        set key = ""
        for  {
            set key = $order(^||ML("OUT", key))
            quit:key=""
            continue:key="ERR"
            set page = ##class(%DynamicObject).%FromJSON(^||ML("OUT", key))
            set spoje = page.rows.%Get("SPOJE")
            continue:'$isobject(spoje)
            kill poc
            set it = spoje.%GetIterator()
            while it.%GetNext(.i, .sp) {
                set g = sp.%Get("SKUPINA")
                set poc(g) = $get(poc(g)) + 1
            }
            set vypis = "", g = ""
            for  {
                set g = $order(poc(g))
                quit:g=""
                set vypis = vypis_$select(vypis="":"", 1:", ")_"skupina "_g_": "_poc(g)_" spojů"
            }
            set radky($increment(radky)) = "směr "_page.values.%Get("SMER")_" → "_vypis
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

/// Najde linky, jejichž spoje spadnou do **víc než jedné** skupiny dnů - jen na nich je rozdělení
/// sloupců vidět. Hledá se **ve zdrojových datech** (týdenní platnost spojů), ne přes GetData,
/// aby to prošlo tisíce linek rychle.
ClassMethod KdeJeVidetDeleni(ByRef sys As %String) As %Status
{
    set err = ""
    try {
        $$$FillParams
        kill ^||ML, radky
        set maxLinek = 6
        for fi = 1:1:$length(..#FIRMY, ",") {
            set db = $piece(..#FIRMY, ",", fi)
            continue:'##class(%SYS.Namespace).Exists(db)
            quit:$get(radky)'<maxLinek
            set sku = ""
            for  {
                set sku = $order(^[db]JRL(sku))
                quit:sku=""
                quit:$get(radky)'<maxLinek
                continue:'$isvalidnum(sku)
                set lin = ""
                for  {
                    set lin = $order(^[db]JRL(sku, lin))
                    quit:lin=""
                    quit:$get(radky)'<maxLinek
                    continue:'$data(^[db]JRL(sku, lin, "I"))
                    set maPD = 0, maSo = 0, maNe = 0, sp = ""
                    for  {
                        set sp = $order(^[db]JRL(sku, lin, "T", sp))
                        quit:sp=""
                        continue:'$isvalidnum(sp)
                        set plk = $piece($piece($get(^[db]JRL(sku, lin, "T", sp)), "#", 1), ":", 7)
                        continue:plk=""
                        for di = 1:1:$length(plk) {
                            set z = $extract(plk, di)
                            if "X12345" [ z set maPD = 1
                            if z = 6 set maSo = 1
                            if "7+" [ z set maNe = 1
                        }
                    }
                    // podmínka za `continue:` končí první mezerou, proto mezisoučet a `if`
                    set skupin = maPD + maSo + maNe
                    if skupin < 2 continue
                    set radky($increment(radky)) = db_" sku="_sku_" linka="_lin_"  (pracovní="_maPD_" soboty="_maSo_" neděle="_maNe_")"
                }
            }
        }
        if '$data(radky) set radky(1) = "Žádná linka s víc skupinami dnů nenalezena."
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

/// Projde všechny hodnoty volby dělení spojů a u každé vypíše, kolik vznikne pásů, jak se jmenují
/// a kolik spojů je na stránce - podklad k tomu, jestli se mění data, nebo až vykreslení.
ClassMethod DeleniPorovnani(ByRef sys As %String) As %Status
{
    set err = ""
    try {
        $$$FillParams
        set db = $get(^||ML("IN","DB")), sku = $get(^||ML("IN","SKU")), lin = $get(^||ML("IN","LIN"))
        kill ^||ML, radky
        for volba = "Bez rozlišení", "Pracovní dny / víkend+svátky", "Pracovní dny / Soboty / Neděle+svátky" {
            kill parametry
            set parametry("DELENI_SLOUPCU") = volba
            do ##class(ALVA.TemplateDesigner.Tests.LinkovyJrSadaTest).NactiLinku(.sys, db, sku, lin, .parametry)
            set key = ""
            for  {
                set key = $order(^||ML("OUT", key))
                quit:key=""
                continue:key="ERR"
                set page = ##class(%DynamicObject).%FromJSON(^||ML("OUT", key))
                set sk = page.rows.%Get("SKUPINY_DNU"), spoje = page.rows.%Get("SPOJE")
                set nadpisy = ""
                if $isobject(sk) {
                    set it = sk.%GetIterator()
                    while it.%GetNext(.i, .g) {
                        set nadpisy = nadpisy_$select(nadpisy="":"", 1:" | ")_g.%Get("NADPIS")
                    }
                }
                // kolik různých skupin spoje reálně nesou
                kill videno
                set ruzn = 0
                if $isobject(spoje) {
                    set it2 = spoje.%GetIterator()
                    while it2.%GetNext(.j, .sp) {
                        set g = sp.%Get("SKUPINA")
                        continue:$data(videno(g))
                        set videno(g) = "", ruzn = ruzn + 1
                    }
                }
                set radky($increment(radky)) = volba_" → spojů="_$select($isobject(spoje):spoje.%Size(),1:0)_" různých skupin="_ruzn_" nadpisy: ["_nadpisy_"]"
                quit
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

/// Vypíše hodnotu vzorů rozložení sloupců tak, jak jde na klienta. IN: `DB`, `SKU`, `LIN`.
ClassMethod VzoryNaWire(ByRef sys As %String) As %Status
{
    set err = ""
    try {
        $$$FillParams
        set db = $get(^||ML("IN","DB")), sku = $get(^||ML("IN","SKU")), lin = $get(^||ML("IN","LIN"))
        kill ^||ML, radky, parametry
        do ##class(ALVA.TemplateDesigner.Tests.LinkovyJrSadaTest).NactiLinku(.sys, db, sku, lin, .parametry)
        set key = ""
        for  {
            set key = $order(^||ML("OUT", key))
            quit:key=""
            continue:key="ERR"
            set page = ##class(%DynamicObject).%FromJSON(^||ML("OUT", key))
            set vzory = page.values.%Get("PRESETY_SLOUPCU")
            set radky($increment(radky)) = "stránka "_key_": PRESETY_SLOUPCU = "_$extract(vzory, 1, 600)
            quit
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

/// Vypíše zastávky, které mají na wire nějaký atribut - kontrola, co se dá v tabulce zobrazit.
ClassMethod AtributyZastavekNaWire(ByRef sys As %String) As %Status
{
    set err = ""
    try {
        $$$FillParams
        set db = $get(^||ML("IN","DB")), sku = $get(^||ML("IN","SKU")), lin = $get(^||ML("IN","LIN"))
        kill ^||ML, radky, parametry
        do ##class(ALVA.TemplateDesigner.Tests.LinkovyJrSadaTest).NactiLinku(.sys, db, sku, lin, .parametry)
        set key = ""
        for  {
            set key = $order(^||ML("OUT", key))
            quit:key=""
            continue:key="ERR"
            set page = ##class(%DynamicObject).%FromJSON(^||ML("OUT", key))
            set zast = page.rows.%Get("ZASTAVKY")
            continue:'$isobject(zast)
            set it = zast.%GetIterator()
            while it.%GetNext(.i, .z) {
                set atr = z.%Get("ATRIBUTY")
                continue:'$isobject(atr)
                continue:atr.%Size()=0
                set vypis = "", ai = ""
                for ai = 0:1:atr.%Size()-1 {
                    set vypis = vypis_$select(vypis="":"", 1:",")_atr.%Get(ai)
                }
                quit:$get(radky)>11
                set radky($increment(radky)) = "tč "_z.%Get("PORADI")_" ["_vypis_"] "_$extract(z.%Get("NAZEV_ZASTAVKY"),1,45)
            }
        }
        if '$data(radky) set radky(1) = "Žádná zastávka na téhle lince nemá atribut."
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

/// Vypíše prvních pár poznámek linky tak, jak jdou na wire - kontrola, že nesou druh i značku.
ClassMethod PoznamkyNaWire(ByRef sys As %String) As %Status
{
    set err = ""
    try {
        $$$FillParams
        set db = $get(^||ML("IN","DB")), sku = $get(^||ML("IN","SKU")), lin = $get(^||ML("IN","LIN"))
        kill ^||ML, radky, parametry
        do ##class(ALVA.TemplateDesigner.Tests.LinkovyJrSadaTest).NactiLinku(.sys, db, sku, lin, .parametry)
        set key = ""
        for  {
            set key = $order(^||ML("OUT", key))
            quit:key=""
            continue:key="ERR"
            set page = ##class(%DynamicObject).%FromJSON(^||ML("OUT", key))
            set pozn = page.rows.%Get("POZNAMKY")
            continue:'$isobject(pozn)
            set it = pozn.%GetIterator()
            while it.%GetNext(.i, .p) {
                quit:$get(radky)>11
                set radky($increment(radky)) = "DRUH=["_p.%Get("DRUH")_"] TYP=["_p.%Get("TYP")_"] ZNAK=["_p.%Get("ZNAK")_"] "_$extract(p.%Get("TEXT"),1,60)
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

/// Najde linku, jejíž spoje nesou daný **atribut** (`POS`, `IDS`, `BEV`…). Slouží k rozšíření
/// porovnávací sady tam, kde je věta legendy opsaná ze zdroje, ale porovnáním nepotvrzená.
/// Pozice atributu v hlavičce spoje: POS=12, IDS=14, BEV=15, SAO=16, PCZ=17, OBC=18, PJI=19,
/// NKA=20, RBU=21, CBE=22, RBP=23, wifi=25, zasu=26.
ClassMethod NajdiLinkuSAtributem(klic As %String, ByRef db, ByRef sku, ByRef lin) As %Boolean
{
    set db = "", sku = "", lin = ""
    set poz = $piece("12,14,15,16,17,18,19,20,21,22,23,25,26", ",",
        $listfind($listfromstring("POS,IDS,BEV,SAO,PCZ,OBC,PJI,NKA,RBU,CBE,RBP,wifi,zasu", ","), klic))
    quit:poz="" 0
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
                for smer = "T", "Z" {
                    set sp = ""
                    for  {
                        set sp = $order(^[firma]JRL(s1, l1, smer, sp))
                        quit:sp=""
                        continue:'$isvalidnum(sp)
                        set hlava = $piece($get(^[firma]JRL(s1, l1, smer, sp)), "#", 1)
                        continue:'+$piece(hlava, ":", poz)
                        set db = firma, sku = s1, lin = l1
                        ret 1
                    }
                }
            }
        }
    }
    quit 0
}

/// Vypíše ke každému atributu spoje jednu linku, kde se vyskytuje - podklad pro rozšíření sady.
ClassMethod AtributyLinky(ByRef sys As %String) As %Status
{
    set err = ""
    try {
        $$$FillParams
        kill ^||ML, radky
        for ai = 1:1:13 {
            set klic = $piece("POS,IDS,BEV,SAO,PCZ,OBC,PJI,NKA,RBU,CBE,RBP,wifi,zasu", ",", ai)
            if ..NajdiLinkuSAtributem(klic, .db, .sku, .lin) {
                set radky($increment(radky)) = klic_": "_db_"~"_sku_"~"_lin
            }
            else {
                set radky($increment(radky)) = klic_": nikde"
            }
        }
        kill ^||ML
        set i = ""
        for  {
            set i = $order(radky(i))
            quit:i=""
            set ^||ML("OUT", $increment(^||ML("OUT"))) = radky(i)
        }
    }
    catch (ex) { $$$CatchErrRest }
    ret $$$OK
}

/// Měření k **tvaru názvu zastávky**: kolik linek má zapnutý název pro MHD, kolik z nich je
/// městských (jen u těch ho starý modul použije), a kolik názvů v registru nese dvojčárku,
/// tedy prázdnou část obce („Zlín,,aut.nádr.").
ClassMethod NazvyVyskyt(ByRef sys As %String) As %Status
{
    set err = ""
    try {
        $$$FillParams
        kill ^||ML, radky
        for fi = 1:1:$length(..#FIRMY, ",") {
            set db = $piece(..#FIRMY, ",", fi)
            continue:'##class(%SYS.Namespace).Exists(db)
            set linek = 0, nazMHD = 0, nazMHDmestska = 0, mestskych = 0
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
                    // Typ linky je zakódovaný: surová hodnota / 10 je index do převodní
                    // tabulky (legacy `TYPLINKYREAD`). Městská = 2 (MEST) nebo 9 (MESP).
                    set surovy = +$piece($get(^[db]JRL(sku, lin, "I")), "#", 1)
                    set typ = $piece("0~1~2~3~5~7~6~8~4~9~10~11", "~", (surovy \ 10) + 1)
                    set jeMestska = (typ = 2) || (typ = 9)
                    if jeMestska set mestskych = mestskych + 1
                    continue:'+$get(^[db]JRL(sku, lin, "PATCH", "nazMHD"))
                    set nazMHD = nazMHD + 1
                    if jeMestska set nazMHDmestska = nazMHDmestska + 1
                }
            }
            // registr zastávek: dvojčárka a vyplněný název pro MHD
            set zast = 0, dvojcarka = 0, maMhdNazev = 0, czst = ""
            for  {
                set czst = $order(^[db]JRZ(czst))
                quit:czst=""
                continue:'$isvalidnum(czst)
                set rec = $get(^[db]JRZ(czst))
                set zast = zast + 1
                if $piece(rec, "$", 2) [ ",," set dvojcarka = dvojcarka + 1
                if $piece(rec, "$", 7) '= "" set maMhdNazev = maMhdNazev + 1
            }
            set radky($increment(radky)) = db_": linek="_linek_" mestskych="_mestskych_" nazevProMhdZapnut="_nazMHD_" ztohoMestskych="_nazMHDmestska
            set radky($increment(radky)) = db_": zastavek="_zast_" sDvojcarkou="_dvojcarka_" sNazvemProMhd="_maMhdNazev
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

/// Vypíše linky, na kterých je nová práce `L2` vidět - podklad pro ruční zkoušku v návrháři.
ClassMethod KdeToZkouset(ByRef sys As %String) As %Status
{
    set err = ""
    try {
        $$$FillParams
        kill ^||ML, radky
        if ..NajdiLinkuSRucnimPoradim(1, .db, .sku, .lin) {
            set radky($increment(radky)) = "ruční pořadí spojů: "_db_" sku="_sku_" linka="_lin
        }
        if ..NajdiLinkuSBarvouSpoje(.db, .sku, .lin) {
            set radky($increment(radky)) = "barevné spoje: "_db_" sku="_sku_" linka="_lin
        }
        if ..NajdiLinkuSPismenemVZone(1, .db, .sku, .lin) {
            set radky($increment(radky)) = "písmena ze zóny se odstraňují: "_db_" sku="_sku_" linka="_lin
        }
        if ..NajdiLinku("linIDS", 3, .db, .sku, .lin) {
            set radky($increment(radky)) = "bez zón i bez km: "_db_" sku="_sku_" linka="_lin
        }
        if ..NajdiLinku("netisknoutSloupecTC", 1, .db, .sku, .lin) {
            set radky($increment(radky)) = "schovaný sloupec tarifních čísel: "_db_" sku="_sku_" linka="_lin
        }
        if ..NajdiLinku("tisknoutJenTcKdeSpojeZastavuji", 1, .db, .sku, .lin) {
            set radky($increment(radky)) = "bez zastávek, kde nikdo nestaví: "_db_" sku="_sku_" linka="_lin
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
		kill ^||ML, radky, rozlozeni, nejbarevnejsi
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
							// Skupina linek patří do výpisu: bez ní se linka v návrháři nedá najít,
							// protože výběr instance filtruje právě podle ní (naběhlo 26. 8., kdy
							// odkaz bez skupiny poslal uživatele hledat linku, na kterou nešlo dojít).
							if prvni = "" set prvni = db_" sku="_sku_" linka="_lin_" spoj "_sp_" = "_barva
							set nejbarevnejsi(db, sku, lin) = $get(nejbarevnejsi(db, sku, lin)) + 1
						}
					}
				}
			}
		}
		set radky($increment(radky)) = "spojů "_spoju_", s nastavenou barvou "_sBarvou
		if prvni '= "" set radky($increment(radky)) = "první: "_prvni

		// Linka s **nejvíc** obarvenými spoji, ne první nalezená: na lince s dvěma obarvenými
		// spoji z tisíce není přepnutí volby v tabulce poznat a jako testovací případ neslouží.
		set nejD = "", nejS = "", nejL = "", nejPocet = 0
		set d1 = ""
		for  {
			set d1 = $order(nejbarevnejsi(d1))
			quit:d1=""
			set s1 = ""
			for  {
				set s1 = $order(nejbarevnejsi(d1, s1))
				quit:s1=""
				set l1 = ""
				for  {
					set l1 = $order(nejbarevnejsi(d1, s1, l1))
					quit:l1=""
					// Podmínku nese `if`, ne postconditional: `continue:pole(a, b, c)>x` neprojde
					// kompilací (mezery v podskriptech ukončí argument příkazu).
					set pocet = nejbarevnejsi(d1, s1, l1)
					if pocet > nejPocet {
						set nejPocet = pocet, nejD = d1, nejS = s1, nejL = l1
					}
				}
			}
		}
		if nejPocet > 0 {
			set radky($increment(radky)) = "nejvíc obarvená linka: "_nejD_" sku="_nejS_" linka="_nejL_" ("_nejPocet_" spojů)"
		}
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

/// Nejmenší linky se spoji v **obou** směrech - podklad pro ruční tisk ve starém modulu.
///
/// **K čemu to je.** Starý modul obousměrný tisk tiše vypne, když se společný blok nevejde na
/// stránku bez řezu (`JRapiLinPrtWin.mac:627`). Na velké lince tedy zapnutá volba nic nezmění
/// a nejde poznat, jestli je vada v nastavení, nebo se jen uplatnil fallback. Na malé lince
/// fallback nemá jak spustit, takže je vidět, jak zrcadlo doopravdy vypadá.
///
/// Řadí podle „šířky" (spoje obou směrů), pak podle počtu zastávek. Sonda **nic nemění**.
ClassMethod NejmensiObousmerneLinky(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		// `FIRMA` zúží hledání na jednu firmu - jinak vyhrají degenerované linky odkudkoli
		// a tisk ve starém modulu je potřeba ve firmě, do které je člověk přihlášený.
		set jenFirma = $get(^||ML("IN","FIRMA"))
		kill ^||ML, poradi
		for fi = 1:1:$length(..#FIRMY, ",") {
			set db = $piece(..#FIRMY, ",", fi)
			continue:(jenFirma'="")&&(db'=jenFirma)
			continue:'##class(%SYS.Namespace).Exists(db)
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
					set spojuT = ..PocetSpoju(db, sku, lin, "T")
					set spojuZ = ..PocetSpoju(db, sku, lin, "Z")
					// Bez spojů v obou směrech legacy zrcadlo vypne rovnou - taková linka nic neukáže.
					continue:(spojuT=0)||(spojuZ=0)
					set zastavek = ..PocetZastavek(db, sku, lin)
					continue:zastavek=0
					// Klíč pořadí: sloupců celkem, pak zastávek, pak firma a linka (unikátnost).
					// Skupina linek je v hodnotě - bez ní se linka v modulu nedá najít.
					set poradi(spojuT + spojuZ, zastavek, db, lin) = spojuT_"+"_spojuZ_"~"_sku
				}
			}
		}

		kill ^||ML
		set vypsano = 0, sirka = ""
		for  {
			set sirka = $order(poradi(sirka))
			quit:sirka=""
			quit:vypsano'<8
			set zast = ""
			for  {
				set zast = $order(poradi(sirka, zast))
				quit:zast=""
				quit:vypsano'<8
				set db = ""
				for  {
					set db = $order(poradi(sirka, zast, db))
					quit:db=""
					quit:vypsano'<8
					set lin = ""
					for  {
						set lin = $order(poradi(sirka, zast, db, lin))
						quit:lin=""
						quit:vypsano'<8
						set vypsano = vypsano + 1
						// ⚠️ Jeden výraz = jeden řádek: ObjectScript zlom výrazu neumí (<SYNTAX>).
						set hodnota = poradi(sirka, zast, db, lin)
						set txt = db_" skupina "_$piece(hodnota, "~", 2)_" linka "_lin
						set txt = txt_": spoje "_$piece(hodnota, "~", 1)
						set txt = txt_" (celkem "_sirka_" sloupců), zastávek "_zast
						set ^||ML("OUT", $increment(^||ML("OUT"))) = txt
					}
				}
			}
		}
		if vypsano = 0 set ^||ML("OUT", 1) = "žádná linka nemá spoje v obou směrech"
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Počet spojů linky v daném směru.
ClassMethod PocetSpoju(db As %String, sku As %String, lin As %String, smer As %String) As %Integer
{
	set pocet = 0, sp = ""
	for  {
		set sp = $order(^[db]JRL(sku, lin, smer, sp))
		quit:sp=""
		continue:'$isvalidnum(sp)
		set pocet = pocet + 1
	}
	quit pocet
}

/// Počet zastávek trasy linky (první trasa, stačí na velikost).
ClassMethod PocetZastavek(db As %String, sku As %String, lin As %String) As %Integer
{
	set pocet = 0, tc = ""
	for  {
		set tc = $order(^[db]JRL(sku, lin, "J", tc))
		quit:tc=""
		continue:'$isvalidnum(tc)
		set pocet = pocet + 1
	}
	quit pocet
}

/// **Záleží modulu turnusů na tvaru čísla linky?** (`L6c`) Klíč `^JRL` je doplněný nulami
/// („0108360"), ale odpověď `gSplTur` se filtruje číslem z turnusového záznamu - když se ty dva
/// tvary rozejdou, vypadne prázdno. Sonda zkusí oba tvary.
ClassMethod TurnusyTvarLinky(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN", "DB"), "CB_CSADAD")
		set sku = $get(^||ML("IN", "SKU"), 1)
		set lin = $get(^||ML("IN", "LIN"), "0108360")
		set plati = $get(^[db]JRL(sku, lin, "PATCH", "Plati"))
		set dh = ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).DatumNaDh($piece(plati, "~", 1))
		kill ^||ML
		set ^||ML("OUT", $increment(^||ML("OUT"))) = "linka "_lin_" platí od "_$piece(plati, "~", 1)_" ($H "_dh_")"
		for tvar = 1, 2 {
			set klic = $select(tvar=1:lin, 1:+lin)
			kill tin, tout
			set sp = ""
			for  {
				set sp = $order(^[db]JRL(sku, lin, "T", sp))
				quit:sp=""
				continue:'$isvalidnum(sp)
				set tin("arr", 1, klic, sp) = ""
			}
			set chyba = ##class(ALVA.ADMAIN.MLAPI).gSplTur(db, db, dh, .tin, .tout)
			set nalezeno = 0, l = ""
			for  {
				set l = $order(tout("arr", 1, l))
				quit:l=""
				set s2 = ""
				for  {
					set s2 = $order(tout("arr", 1, l, s2))
					quit:s2=""
					set nalezeno = nalezeno + 1
				}
			}
			set txt = "klíč linky """_klic_""": chyba="_+chyba_", spojů s turnusem "_nalezeno
			set ^||ML("OUT", $increment(^||ML("OUT"))) = txt
		}
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// **Z čeho se skládá to množství turnusů u jednoho spoje** (`L6c`, dotaz Jan 28. 8.).
///
/// Označení má tvar `<turnus>.<varianta>` („110.01"), odpověď navíc rozlišuje **zpracování**
/// a **datum**. Sonda u nejbohatších spojů rozepíše, kolik z toho je různých turnusů a kolik
/// jen variant téhož - podle toho jde rozhodnout, jestli se má tisknout celé označení
/// (parita s legacy) nebo jen číslo turnusu.
ClassMethod TurnusyRozpad(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN", "DB"), "CB_CSADAD")
		set sku = $get(^||ML("IN", "SKU"), 1)
		set lin = $get(^||ML("IN", "LIN"), "0320625")
		set plati = $get(^[db]JRL(sku, lin, "PATCH", "Plati"))
		set dh = ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).DatumNaDh($piece(plati, "~", 1))
		kill tin, tout
		for smer = "T", "Z" {
			set sp = ""
			for  {
				set sp = $order(^[db]JRL(sku, lin, smer, sp))
				quit:sp=""
				continue:'$isvalidnum(sp)
				set tin("arr", 1, +lin, sp) = ""
			}
		}
		set chyba = ##class(ALVA.ADMAIN.MLAPI).gSplTur(db, db, dh, .tin, .tout)
		kill ^||ML
		if +chyba {
			set ^||ML("OUT", 1) = "gSplTur chyba: "_$piece(chyba, "~", 2)
			ret $$$OK
		}
		set ^||ML("OUT", $increment(^||ML("OUT"))) = "linka "_lin_", platnost od "_$piece(plati, "~", 1)
		set vypsano = 0, sp = ""
		for  {
			set sp = $order(tout("arr", 1, +lin, sp))
			quit:sp=""
			quit:vypsano'<6
			kill cele, cisla, zpracovani, datumy
			set zp = ""
			for  {
				set zp = $order(tout("arr", 1, +lin, sp, zp))
				quit:zp=""
				set zpracovani(zp) = ""
				set tu = ""
				for  {
					set tu = $order(tout("arr", 1, +lin, sp, zp, tu))
					quit:tu=""
					set cele(tu) = ""
					set cisla($piece(tu, ".", 1)) = ""
					set hr = ""
					for  {
						set hr = $order(tout("arr", 1, +lin, sp, zp, tu, hr))
						quit:hr=""
						set datumy(hr) = ""
					}
				}
			}
			set pocetCele = 0, k = ""
			for  { set k = $order(cele(k)) quit:k=""  set pocetCele = pocetCele + 1 }
			set pocetCisel = 0, k = ""
			for  { set k = $order(cisla(k)) quit:k=""  set pocetCisel = pocetCisel + 1 }
			set pocetZp = 0, k = ""
			for  { set k = $order(zpracovani(k)) quit:k=""  set pocetZp = pocetZp + 1 }
			set pocetDat = 0, k = ""
			for  { set k = $order(datumy(k)) quit:k=""  set pocetDat = pocetDat + 1 }
			continue:pocetCele<3
			set vypsano = vypsano + 1
			set vzorek = "", k = "", i = 0
			for  {
				set k = $order(cele(k))
				quit:k=""
				set i = i + 1
				quit:i>10
				set vzorek = vzorek_$select(vzorek'="":" ", 1:"")_k
			}
			set txt = "spoj "_sp_": označení "_pocetCele_", z toho různých turnusů "_pocetCisel
			set txt = txt_", zpracování "_pocetZp_", datumů "_pocetDat
			set txt = txt_" | "_vzorek
			set ^||ML("OUT", $increment(^||ML("OUT"))) = txt
		}
		if vypsano = 0 set ^||ML("OUT", $increment(^||ML("OUT"))) = "žádný spoj nemá 3+ turnusy"
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// **Kde jsou turnusy ke spojům a co ten dotaz stojí** (`L6c`).
///
/// Turnusy nejsou v `^JRL` - drží je jiný modul a legacy si je tahá voláním
/// `ALVA.ADMAIN.MLAPI.gSplTur` (`JRapiLinPrtWin.mac:2697`), které si legacy **zapíná
/// nastavením** (`in("A",2,"arr",6)`). Sonda odpovídá na dvě otázky, na kterých závisí
/// návrh: **jak vypadá označení turnusu** (tiskne se přímo, takže o jeho tvaru nemáme
/// co rozhodovat) a **jak dlouho volání trvá** (podle toho, jestli si ho můžeme dovolit
/// vždycky, nebo ho musí zapínat parametr sestavy jako v legacy).
ClassMethod TurnusyVyskyt(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		kill ^||ML
		set nalezeno = 0
		for fi = 1:1:$length(..#FIRMY, ",") {
			set db = $piece(..#FIRMY, ",", fi)
			continue:'##class(%SYS.Namespace).Exists(db)
			set prohledano = 0, nalezenoVeFirme = 0
			set sku = ""
			for  {
				set sku = $order(^[db]JRL(sku))
				quit:sku=""
				continue:'$isvalidnum(sku)
				set lin = ""
				for  {
					set lin = $order(^[db]JRL(sku, lin))
					quit:lin=""
					quit:prohledano'<25
					quit:nalezenoVeFirme'<3
					continue:'$data(^[db]JRL(sku, lin, "I"))
					set prohledano = prohledano + 1
					set popis = ..TurnusyLinky(db, sku, lin)
					continue:popis=""
					set nalezenoVeFirme = nalezenoVeFirme + 1
					set nalezeno = nalezeno + 1
					set txt = db_" skupina "_sku_" linka "_lin_": "_popis
					set ^||ML("OUT", $increment(^||ML("OUT"))) = txt
				}
				quit:prohledano'<25
				quit:nalezenoVeFirme'<3
			}
			if nalezenoVeFirme = 0 {
				set txt = db_": v prvních "_prohledano_" linkách žádné turnusy"
				set ^||ML("OUT", $increment(^||ML("OUT"))) = txt
			}
		}
		if nalezeno = 0 set ^||ML("OUT", $increment(^||ML("OUT"))) = "turnusy nenalezeny nikde"
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Turnusy spojů jedné linky: popis „spojů s turnusem / nejvíc na spoj / vzorky / ms",
/// prázdno když linka turnusy nemá. Datum platnosti bere stejně jako legacy - z `PATCH,"Plati"`.
ClassMethod TurnusyLinky(db As %String, sku As %String, lin As %String) As %String
{
	set plati = $get(^[db]JRL(sku, lin, "PATCH", "Plati"))
	set dh = ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).DatumNaDh($piece(plati, "~", 1))
	quit:dh="" ""
	kill tin, tout
	set pocetSpoju = 0
	for smer = "T", "Z" {
		set sp = ""
		for  {
			set sp = $order(^[db]JRL(sku, lin, smer, sp))
			quit:sp=""
			continue:'$isvalidnum(sp)
			set tin("arr", 1, +lin, sp) = ""
			set pocetSpoju = pocetSpoju + 1
		}
	}
	quit:pocetSpoju=0 ""
	set t0 = $zhorolog
	set chyba = ##class(ALVA.ADMAIN.MLAPI).gSplTur(db, db, dh, .tin, .tout)
	set ms = $fnumber(($zhorolog - t0) * 1000, "", 0)
	quit:+chyba "gSplTur chyba: "_$piece(chyba, "~", 2)
	set spojuSTurnusem = 0, maxNaSpoj = 0, vzorky = ""
	set sp = ""
	for  {
		set sp = $order(tout("arr", 1, +lin, sp))
		quit:sp=""
		kill turnusy
		set zp = ""
		for  {
			set zp = $order(tout("arr", 1, +lin, sp, zp))
			quit:zp=""
			set tu = ""
			for  {
				set tu = $order(tout("arr", 1, +lin, sp, zp, tu))
				quit:tu=""
				set turnusy(tu) = ""
			}
		}
		set pocet = 0, tu = ""
		for  {
			set tu = $order(turnusy(tu))
			quit:tu=""
			set pocet = pocet + 1
			continue:$length(vzorky,";")>5
			set vzorky = vzorky_$select(vzorky'="":";", 1:"")_tu
		}
		continue:pocet=0
		set spojuSTurnusem = spojuSTurnusem + 1
		if pocet > maxNaSpoj set maxNaSpoj = pocet
	}
	quit:spojuSTurnusem=0 ""
	set txt = spojuSTurnusem_"/"_pocetSpoju_" spojů má turnus"
	set txt = txt_", nejvíc na spoj "_maxNaSpoj
	set txt = txt_", vzorky "_vzorky
	set txt = txt_", dotaz "_ms_" ms"
	quit txt
}

/// **Kde má zastávka víc zón najednou a čím jsou v datech oddělené** (`N1`).
///
/// Zákazník chce každou zónu hraniční zastávky ve vlastním sloupci. Než se to začne dělit, musí se
/// vědět, **jak to v datech vypadá** - kolik zón na zastávce bývá a jaký znak je odděluje; jinak by
/// se dělilo podle domněnky. Zóny bere ze stejného místa jako tisk (trasa linky, pole zóny),
/// **jen u linek, které zóny tisknou** (`linIDS`), aby se nepočítaly linky, kde se stejně neukážou.
/// Sonda nic nemění.
///
/// OUT("ODDELOVACE") = které znaky se v hodnotách vyskytly a kolikrát,
/// OUT(n) = ukázka "firma skupina linka zastávka: hodnota", OUT("SOUHRN") = počty.
ClassMethod ZonyViceNaZastavce(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		// Musí se přečíst před `kill` - ten smaže i IN.
		set jenZnak = $get(^||ML("IN","ZNAK"))
		kill ^||ML, oddelovace
		set ukazek = 0, sZonou = 0, viceZon = 0, linekSeZonami = 0
		for fi = 1:1:$length(..#FIRMY, ",") {
			set db = $piece(..#FIRMY, ",", fi)
			continue:'##class(%SYS.Namespace).Exists(db)
			set sku = ""
			for  {
				set sku = $order(^[db]JRL(sku))
				quit:sku=""
				continue:'$isvalidnum(sku)
				set lin = ""
				for  {
					set lin = $order(^[db]JRL(sku, lin))
					quit:lin=""
					// linIDS 1 nebo 2 = linka zóny tiskne (viz BuildStrankaLinky)
					set linIds = +$get(^[db]JRL(sku, lin, "PATCH", "linIDS"))
					continue:'((linIds = 1) || (linIds = 2))
					set linekSeZonami = linekSeZonami + 1
					set poradi = ""
					for  {
						set poradi = $order(^[db]JRL(sku, lin, "J", poradi))
						quit:poradi=""
						set veta = $get(^[db]JRL(sku, lin, "J", poradi))
						set zona = $piece($piece(veta, ":", 5), "#", 2)
						continue:zona=""
						set sZonou = sZonou + 1
						// Který znak hodnotu dělí - hledá se mezi kandidáty, ne hádá se jeden.
						set nalezen = ""
						for zi = 1:1:$length(",;/|+ ") {
							set znak = $extract(",;/|+ ", zi)
							continue:zona'[znak
							set oddelovace(znak) = $get(oddelovace(znak)) + 1
							set:nalezen="" nalezen = znak
						}
						continue:nalezen=""
						set viceZon = viceZon + 1
						// IN ZNAK zúží ukázky na jeden oddělovač - vzácný tvar by se v ukázkách
						// z prvních patnácti nálezů jinak neukázal.
						continue:(jenZnak'="")&&(zona'[jenZnak)
						continue:ukazek'<15
						set ukazek = ukazek + 1
						set czst = $piece(veta, ":", 1)
						set ^||ML("OUT", $increment(^||ML("OUT"))) = db_" "_sku_" "_lin_" "_..NazevZastavkyProSondu(db, czst)_": ["_zona_"]"
					}
				}
			}
		}
		set txt = "", znak = ""
		for  {
			set znak = $order(oddelovace(znak))
			quit:znak=""
			set txt = txt_$select(txt'="":", ", 1:"")_"["_znak_"] "_oddelovace(znak)_"×"
		}
		set ^||ML("OUT","ODDELOVACE") = $select(txt="":"žádný z kandidátů se nevyskytl", 1:txt)
		set ^||ML("OUT","SOUHRN") = "linek se zónami "_linekSeZonami_", zastávek se zónou "_sZonou_", z toho s víc zónami "_viceZon
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Název zastávky pro výpis sondy (druhý „$"-díl záznamu zastávky = čitelný tvar s diakritikou).
ClassMethod NazevZastavkyProSondu(db As %String, czst As %String) As %String
{
	quit $piece($get(^[db]JRZ(czst)), "$", 2)
}

/// **Na které lince by se rozešla věta „všechny spoje…"** podle toho, počítá-li se přes jeden směr,
/// nebo přes celou linku (`L14` bod 1).
///
/// Věta atributu má dva tvary: „**všechny** spoje s bezbariérovým vozidlem", když ho mají všechny
/// spoje, a výčet „spoje 3, 7 …", když jen některé. Rozhodující je tedy poměr *spojů s atributem*
/// ku *všem spojům* - a ten vyjde jinak, počítá-li se v rámci jednoho směru, nebo přes obě.
/// Sonda hledá linky, kde ty dva výpočty **dají jiný tvar věty**; jen na takové lince má smysl
/// pouštět porovnání se starým modulem. Nic nemění.
///
/// OUT(n) = "firma skupina linka ATRIBUT: T x/y Z x/y (celkem x/y)", OUT("SOUHRN") = počet nálezů.
ClassMethod AtributyRozhodujiciLinky(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set klice = "POS,IDS,BEV,SAO,PCZ,OBC,PJI,NKA,RBU,CBE,RBP,wifi,zasu"
		set pozice = "12,14,15,16,17,18,19,20,21,22,23,25,26"
		set nalezeno = 0
		for fi = 1:1:$length(..#FIRMY, ",") {
			set db = $piece(..#FIRMY, ",", fi)
			continue:'##class(%SYS.Namespace).Exists(db)
			set sku = ""
			for  {
				set sku = $order(^[db]JRL(sku))
				quit:sku=""
				continue:'$isvalidnum(sku)
				set lin = ""
				for  {
					set lin = $order(^[db]JRL(sku, lin))
					quit:lin=""
					kill pocty, celkem
					for smerIdx = 1:1:2 {
						set smer = $piece("T,Z", ",", smerIdx)
						continue:'$data(^[db]JRL(sku, lin, smer))
						set sp = ""
						for  {
							set sp = $order(^[db]JRL(sku, lin, smer, sp))
							quit:sp=""
							continue:'$isvalidnum(sp)
							set hlava = $piece($get(^[db]JRL(sku, lin, smer, sp)), "#", 1)
							continue:hlava=""
							set celkem(smer) = $get(celkem(smer)) + 1
							for ai = 1:1:13 {
								continue:'+$piece(hlava, ":", $piece(pozice, ",", ai))
								set klic = $piece(klice, ",", ai)
								set pocty(klic, smer) = $get(pocty(klic, smer)) + 1
							}
						}
					}
					// Obousměrná linka = spoje v obou směrech; jinak se ty dva výpočty ani lišit nemohou.
					continue:'($get(celkem("T")) && $get(celkem("Z")))
					set celkemObe = celkem("T") + celkem("Z")
					set klic = ""
					for  {
						set klic = $order(pocty(klic))
						quit:klic=""
						// Atribut s generickou větou se počtu spojů nedotkne - věta výčet nemá,
						// takže rozsah počítání u něj nic nezmění a do hledání nepatří.
						set patrSp = $get(^[db]JRSET("Definice","PAtrSp"))
						set bbpLinka = +$get(^[db]JRL(sku,lin,"PATCH","TZPA","BBP"))
						continue:##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).GenerickaVeta(klic, patrSp, bbpLinka)
						set pT = +$get(pocty(klic, "T")), pZ = +$get(pocty(klic, "Z"))
						set vseSmer = ((pT = celkem("T")) || (pZ = celkem("Z")))
						set vseLinka = ((pT + pZ) = celkemObe)
						continue:vseSmer=vseLinka
						set nalezeno = nalezeno + 1
						set txt = db_" skupina "_sku_" linka "_lin_" "_klic_": T "_pT_"/"_celkem("T")
						set txt = txt_" Z "_pZ_"/"_celkem("Z")_" (celkem "_(pT+pZ)_"/"_celkemObe_")"
						set ^||ML("OUT", $increment(^||ML("OUT"))) = txt
					}
				}
			}
		}
		set ^||ML("OUT","SOUHRN") = "linek, kde by se tvar věty rozešel: "_nalezeno
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// **Na které lince jsou vidět atributy spoje** (občerstvení, zásilky, radiobus, samoobslužný,
/// nízkokapacitní vůz, …) - podklad pro ruční ověření tisku.
///
/// Atributy leží v hlavičce spoje `^JRL(sku,lin,smer,sp)` jako příznaky v pozicích oddělených
/// dvojtečkou; mapování pozic je v `LinkovyJrAd.BuildSpoj`. Sonda **nic nemění**, jen počítá,
/// kolik spojů linky má který atribut, a vypíše první nalezené linky.
///
/// OUT(n) = "firma skupina linka: ATRIBUT×počet …", OUT("SOUHRN") = kolik linek má který atribut.
ClassMethod AtributySpojuVyskyt(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		kill ^||ML
		set klice = "POS,IDS,BEV,SAO,PCZ,OBC,PJI,NKA,RBU,CBE,RBP,wifi,zasu"
		set pozice = "12,14,15,16,17,18,19,20,21,22,23,25,26"
		set nalezeno = 0
		for fi = 1:1:$length(..#FIRMY, ",") {
			set db = $piece(..#FIRMY, ",", fi)
			continue:'##class(%SYS.Namespace).Exists(db)
			set sku = ""
			for  {
				set sku = $order(^[db]JRL(sku))
				quit:sku=""
				continue:'$isvalidnum(sku)
				set lin = ""
				for  {
					set lin = $order(^[db]JRL(sku, lin))
					quit:lin=""
					kill pocty
					// ⚠️ Směr je písmeno („T" tam / „Z" zpět), ne číslo - filtr na číslo tu
					// mlčky přeskočil úplně všechno a sonda hlásila „nikde nic".
					for smerIdx = 1:1:2 {
						set smer = $piece("T,Z", ",", smerIdx)
						continue:'$data(^[db]JRL(sku, lin, smer))
						set sp = ""
						for  {
							set sp = $order(^[db]JRL(sku, lin, smer, sp))
							quit:sp=""
							continue:'$isvalidnum(sp)
							set hlava = $piece($get(^[db]JRL(sku, lin, smer, sp)), "#", 1)
							continue:hlava=""
							for ai = 1:1:13 {
								set klic = $piece(klice, ",", ai)
								continue:'+$piece(hlava, ":", $piece(pozice, ",", ai))
								set pocty(klic) = $get(pocty(klic)) + 1
							}
						}
					}
					continue:'$data(pocty)
					set nalezeno = nalezeno + 1
					set k = ""
					for  {
						set k = $order(pocty(k))
						quit:k=""
						set souhrn(k) = $get(souhrn(k)) + 1
						// Příklad se drží ten s nejvíc spoji - na něm je atribut nejlíp vidět.
						if pocty(k) > +$piece($get(priklad(k)), "~", 1) {
							set priklad(k) = pocty(k)_"~"_db_" skupina "_sku_" linka "_lin
						}
					}
				}
			}
		}
		for ai = 1:1:13 {
			set klic = $piece(klice, ",", ai)
			if '$data(souhrn(klic)) {
				set ^||ML("OUT", $increment(^||ML("OUT"))) = klic_": nikde"
				continue
			}
			set txt = klic_": "_souhrn(klic)_" linek, nejvíc "_$piece(priklad(klic), "~", 1)
			set txt = txt_" spojů na "_$piece(priklad(klic), "~", 2)
			set ^||ML("OUT", $increment(^||ML("OUT"))) = txt
		}
		set ^||ML("OUT","SOUHRN") = "linek s aspoň jedním atributem: "_nalezeno
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

}
