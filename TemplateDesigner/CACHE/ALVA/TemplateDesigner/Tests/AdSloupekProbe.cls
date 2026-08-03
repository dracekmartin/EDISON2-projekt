Include (%occInclude, GENERAL)

/// Read-only sonda k #13048 bodu 3 / #13088: kde v datech AD leží „sloupek" (KOVED ho odlišuje
/// od nástupiště = našeho označníku). Umí fulltext přes legacy rutiny (`^rMAC`) a rozpad
/// `#`-segmentů J-věty trasy linky. Nic nemění, po vytěžení smazat.
Class ALVA.TemplateDesigner.Tests.AdSloupekProbe
{

/// Fulltext přes zdrojáky legacy rutin. IN: PREFIX (např. "JRapi"), PATTERN (case-insensitive),
/// MAX (default 60). OUT: "<rutina>:<řádek> = <text>".
ClassMethod GrepRoutines(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set prefix = $get(^||ML("IN","PREFIX"))
		set pattern = $zconvert($get(^||ML("IN","PATTERN")), "L")
		set max = +$get(^||ML("IN","MAX")) if max < 1 set max = 60
		kill ^||ML("OUT")
		if pattern = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "PATTERN je povinný"))

		set nalezeno = 0, prohledano = 0
		set rtn = $select(prefix="":"", 1:$extract(prefix, 1, $length(prefix) - 1) _ $char($ascii($extract(prefix, $length(prefix))) - 1))
		for {
			set rtn = $order(^rMAC(rtn))
			quit:rtn=""
			quit:(prefix'="")&&($extract(rtn, 1, $length(prefix))'=prefix)
			quit:nalezeno>=max
			set prohledano = prohledano + 1
			set i = ""
			for {
				set i = $order(^rMAC(rtn, 0, i))
				quit:i=""
				quit:nalezeno>=max
				set txt = $get(^rMAC(rtn, 0, i))
				continue:txt=""
				continue:'$find($zconvert(txt, "L"), pattern)
				set nalezeno = nalezeno + 1
				set ^||ML("OUT", "HIT", $translate($justify(nalezeno, 3), " ", "0")) = rtn _ ":" _ i _ " = " _ txt
			}
		}
		set ^||ML("OUT", "SOUHRN") = "rutin=" _ prohledano _ " nalezu=" _ nalezeno _ " (max " _ max _ ")"
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Poznámky linky, jak je vydá agenda (#13093 bod 1) - kategorie a výlukové texty.
/// IN: DB, SKU, LIN. Read-only.
ClassMethod PoznamkyLinkyDump(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set sku = $get(^||ML("IN","SKU"))
		set lin = $get(^||ML("IN","LIN"))
		kill ^||ML("OUT")
		if (db = "") || (lin = "") $$$ThrowStatus($$$ERROR($$$GeneralError, "DB a LIN jsou povinné"))

		set arr = ##class(%DynamicArray).%New()
		do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).PoznamkyLinky(db, sku, lin, arr)
		set n = 0, it = arr.%GetIterator()
		while it.%GetNext(.k, .p) {
			set n = n + 1
			set ^||ML("OUT","POZNAMKA", n) = "[" _ p.%Get("KATEGORIE") _ "] " _ $extract(p.%Get("TEXT"), 1, 70)
		}
		set ^||ML("OUT","POCET") = n
		set ^||ML("OUT","VYLUKOVE_TEXTY") = $extract(##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).VylukoveTexty(db, sku, lin), 1, 200)
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Plní zákazník atributy spoje, které chce u odjezdů (#13093 bod 2)? Kola = hlava věty spoje pole
/// **19** (legacy `dta20` PrepJiKola), místenkování = pole **6** (`dta6`, znak R), typ místenkování =
/// pole **11** (`dta12`: 1 nutno, 2 možno AMS, 3 nutno AMS). Navíc návaznosti (subnode 32) pro
/// pokračující spoje (bod 3). IN: DB, MAXLIN. Read-only.
ClassMethod ScanAtributySpoju(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set maxLin = +$get(^||ML("IN","MAXLIN")) if maxLin < 1 set maxLin = 500
		kill ^||ML("OUT")
		if db = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "DB je povinné"))

		set linek = 0, spoju = 0, kola = 0, misten = 0, typMisten = 0, navaz = 0, ukazek = 0
		kill hodnKola, hodnMisten
		set sku = ""
		for {
			set sku = $order(^[db]JRL(sku))
			quit:sku=""
			set lin = ""
			for {
				set lin = $order(^[db]JRL(sku,lin))
				quit:lin=""
				quit:linek>=maxLin
				set linek = linek + 1
				for sm = "T", "Z" {
					set sp = ""
					for {
						set sp = $order(^[db]JRL(sku,lin,sm,sp))
						quit:sp=""
						continue:'$isvalidnum(sp)
						set spoju = spoju + 1
						set hlava = $piece($get(^[db]JRL(sku,lin,sm,sp)), "#", 1)
						set vKola = $piece(hlava, ":", 19)
						set vMist = $piece(hlava, ":", 6)
						set vTyp  = $piece(hlava, ":", 11)
						if vKola '= "" {
						set kola = kola + 1, hodnKola(vKola) = $get(hodnKola(vKola)) + 1
						if $get(ukazekKola) < 5 { set ukazekKola = $get(ukazekKola) + 1, ^||ML("OUT","KOLA_UKAZKY",ukazekKola) = "sku="_sku_" lin="_lin_" smer="_sm_" spoj="_sp_" hodnota="_vKola }
					}
						if vMist '= "" { set misten = misten + 1, hodnMisten(vMist) = $get(hodnMisten(vMist)) + 1 }
						if vTyp '= "" { set typMisten = typMisten + 1 }
						if $data(^[db]JRL(sku,lin,sm,sp,32)) {
							set navaz = navaz + 1
							if ukazek < 5 { set ukazek = ukazek + 1, ^||ML("OUT","NAVAZNOST",ukazek) = "sku="_sku_" lin="_lin_" smer="_sm_" spoj="_sp_": "_$extract($get(^[db]JRL(sku,lin,sm,sp,32)),1,90) }
						}
					}
				}
			}
		}
		set ^||ML("OUT","SOUHRN") = db_" linek="_linek_" spoju="_spoju
		set ^||ML("OUT","KOLA") = "spoju s kolem="_kola
		set ^||ML("OUT","MISTENKOVANI") = "spoju s R="_misten_" s typem mistenkovani="_typMisten
		set ^||ML("OUT","NAVAZNOSTI") = "spoju s navaznosti="_navaz
		set h = "", radek = ""
		for { set h = $order(hodnKola(h)) quit:h=""  set radek = radek_h_"("_hodnKola(h)_"x) " }
		set:radek'="" ^||ML("OUT","KOLA_HODNOTY") = radek
		set h = "", radek = ""
		for { set h = $order(hodnMisten(h)) quit:h=""  set radek = radek_h_"("_hodnMisten(h)_"x) " }
		set:radek'="" ^||ML("OUT","MISTEN_HODNOTY") = radek
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Které linky mají vůbec nastavenou barvu (#12997) - hledá `^JRL(sku,lin,"PATCH","BARVA")`.
/// IN: DB, MAXLIN. Read-only.
ClassMethod NajdiBarvy(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set maxLin = +$get(^||ML("IN","MAXLIN")) if maxLin < 1 set maxLin = 500
		kill ^||ML("OUT")
		if db = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "DB je povinné"))

		set linek = 0, nalez = 0, sku = ""
		for {
			set sku = $order(^[db]JRL(sku))
			quit:sku=""
			set lin = ""
			for {
				set lin = $order(^[db]JRL(sku,lin))
				quit:lin=""
				quit:linek>=maxLin
				set linek = linek + 1
				set barva = $get(^[db]JRL(sku,lin,"PATCH","BARVA"))
				continue:barva=""
				set nalez = nalez + 1
				if nalez <= 10 { set ^||ML("OUT","NALEZ",nalez) = "sku="_sku_" lin="_lin_" barva="_barva }
			}
		}
		set ^||ML("OUT","SOUHRN") = db_" linek="_linek_" s barvou="_nalez
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Barvy linky po Petrově změně (#12997): co vrací JŘ API a co z toho udělá agenda.
/// IN: DB, SKU, LIN. OUT: seznam barev, naše hodnota, kde to leží. Read-only.
ClassMethod BarvyLinky(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set sku = $get(^||ML("IN","SKU"))
		set lin = $get(^||ML("IN","LIN"))
		kill ^||ML("OUT")
		if (db = "") || (lin = "") $$$ThrowStatus($$$ERROR($$$GeneralError, "DB a LIN jsou povinné"))

		set linIdx = $$cnvCsLin^JRapi(lin)
		set ^||ML("OUT","LIN_INDEX") = linIdx
		set ^||ML("OUT","RAW") = $get(^[db]JRL(sku, linIdx, "PATCH", "BARVA"))

		set seznam = ##class(ALVA.ISY.ISYJR2.API.Linky).GetBarvyLinky(IdFa, db, sku, lin)
		set ^||ML("OUT","POCET_BAREV") = $select(seznam="":0, 1:$listlength(seznam))
		if seznam '= "" {
			for i = 1:1:$listlength(seznam) {
				set ^||ML("OUT","BARVA", i) = $listget(seznam, i)
			}
		}
		// jak to dnes vidí naše agenda (volá GetBarvaLinky bez pořadí)
		set ^||ML("OUT","AGENDA_BARVA_LINKY") = ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).BarvaLinky(IdFa, db, sku, lin)
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Sloupky a označníky jedné stránky - kde má „výčet všech" co zobrazit (víc hodnot dle spojů).
/// IN: DB, SKU, LIN, SMER, PORADI. Read-only.
ClassMethod StrankaSloupky(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set sku = $get(^||ML("IN","SKU"))
		set lin = $get(^||ML("IN","LIN"))
		set smer = $get(^||ML("IN","SMER")) set:smer="" smer = "T"
		set poradi = +$get(^||ML("IN","PORADI")) set:'poradi poradi = 1
		kill ^||ML("OUT"), ^||TiskAd
		if (db = "") || (lin = "") $$$ThrowStatus($$$ERROR($$$GeneralError, "DB a LIN jsou povinné"))

		set page = ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).BuildStrankaZastavky(db, sku, lin, poradi, smer, "")
		if '$isobject(page) {
			set ^||ML("OUT","CHYBA") = "stránka se nesestavila"
			ret $$$OK
		}
		set n = 0, vic = 0, it = page.rows.%Get("ZASTAVKY").%GetIterator()
		while it.%GetNext(.k, .z) {
			set n = n + 1
			set spoju = z.%Get("SLOUPEK_SPOJU")
			if $length(spoju, ",") > 1 { set vic = vic + 1 }
			set ^||ML("OUT","ZASTAVKA",n) = z.%Get("NAZEV_ZASTAVKY")_" | sloupek="_z.%Get("SLOUPEK")_" dle spoju="_spoju_" | oznacnik="_z.%Get("NASTUPISTE")_" dle spoju="_z.%Get("NASTUPISTE_SPOJU")
		}
		set ^||ML("OUT","SOUHRN") = "zastavek="_n_" z toho s vice sloupky dle spoju="_vic
		kill ^||TiskAd
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Atributy zastávek a legenda jedné stránky - kontrola, co GetData reálně vydá.
/// IN: DB, SKU, LIN, SMER, PORADI. Read-only.
ClassMethod StrankaAtributy(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set sku = $get(^||ML("IN","SKU"))
		set lin = $get(^||ML("IN","LIN"))
		set smer = $get(^||ML("IN","SMER")) set:smer="" smer = "T"
		set poradi = +$get(^||ML("IN","PORADI")) set:'poradi poradi = 1
		kill ^||ML("OUT"), ^||TiskAd
		if (db = "") || (lin = "") $$$ThrowStatus($$$ERROR($$$GeneralError, "DB a LIN jsou povinné"))

		set page = ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).BuildStrankaZastavky(db, sku, lin, poradi, smer, "")
		if '$isobject(page) {
			set ^||ML("OUT","CHYBA") = "stránka se nesestavila (zastávka není obsluhovaná, nebo za ní už nic nejede)"
			ret $$$OK
		}

		set n = 0, it = page.rows.%Get("ZASTAVKY").%GetIterator()
		while it.%GetNext(.k, .z) {
			set atrTxt = "", ai = ""
			set ita = z.%Get("ATRIBUTY").%GetIterator()
			while ita.%GetNext(.ak, .a) { set atrTxt = atrTxt _ a.%Get("ZNAK") }
			continue:atrTxt=""
			set n = n + 1
			set ^||ML("OUT","ZASTAVKA",n) = z.%Get("PORADI")_" "_z.%Get("NAZEV_ZASTAVKY")_" znaky="_atrTxt
		}
		set ^||ML("OUT","ZASTAVEK_SE_ZNACKOU") = n

		set p = 0, itp = page.rows.%Get("POZNAMKY").%GetIterator()
		while itp.%GetNext(.pk, .pz) {
			set p = p + 1
			set ^||ML("OUT","POZNAMKA",p) = "["_pz.%Get("ZNAK")_"] "_pz.%Get("KATEGORIE")_": "_$extract(pz.%Get("TEXT"),1,60)
		}
		kill ^||TiskAd
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// „Na zavolání / na objednání" - kde je to v datech vyplněné (#13048 bod 1, dotaz zákazníka).
/// Tři různá místa: spoj `Radiobus/jede na objednání` (hlava věty spoje pole 21 = legacy `dta30`),
/// spoj `jede podmínečně` (pole 23 = `dta34`) a **zastávka v trase spoje** (W pozice 5 = atributy
/// trasy spoje, znaky `r` = na objednání / `i` = podmínečně). IN: DB, MAXLIN. Read-only.
ClassMethod ScanNaZavolani(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set maxLin = +$get(^||ML("IN","MAXLIN")) if maxLin < 1 set maxLin = 500
		kill ^||ML("OUT")
		if db = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "DB je povinné"))

		set linek = 0, spoju = 0, naObj = 0, podm = 0, wrRadku = 0, ukazek = 0
		set sku = ""
		for {
			set sku = $order(^[db]JRL(sku))
			quit:sku=""
			set lin = ""
			for {
				set lin = $order(^[db]JRL(sku,lin))
				quit:lin=""
				quit:linek>=maxLin
				set linek = linek + 1
				for sm = "T", "Z" {
					set sp = ""
					for {
						set sp = $order(^[db]JRL(sku,lin,sm,sp))
						quit:sp=""
						continue:'$isvalidnum(sp)
						set spoju = spoju + 1
						set hlava = $piece($get(^[db]JRL(sku,lin,sm,sp)), "#", 1)
						if $piece(hlava, ":", 21) '= "" {
							set naObj = naObj + 1
							if ukazek < 6 { set ukazek = ukazek + 1, ^||ML("OUT","UKAZKA",ukazek) = "SPOJ na objednani: sku="_sku_" lin="_lin_" smer="_sm_" spoj="_sp_" hodnota="_$piece(hlava,":",21) }
						}
						if $piece(hlava, ":", 23) '= "" { set podm = podm + 1 }
					}
				}
				// atributy v trase spoje (W pozice 5) - znaky r / i
				set ct = ""
				for {
					set ct = $order(^[db]JRL(sku,lin,"W",ct))
					quit:ct=""
					set w = $get(^[db]JRL(sku,lin,"W",ct))
					for i = 1:1:$length(w,"#") {
						set atr = $piece($piece(w,"#",i), ":", 5)
						continue:atr=""
						if '$find(atr,"r"), '$find(atr,"i") continue
						set wrRadku = wrRadku + 1
						if ukazek < 6 { set ukazek = ukazek + 1, ^||ML("OUT","UKAZKA",ukazek) = "ZASTAVKA V TRASE SPOJE: sku="_sku_" lin="_lin_" trasa="_ct_" atributy="_atr }
					}
				}
			}
		}
		set ^||ML("OUT","SOUHRN") = db_" linek="_linek_" spoju="_spoju_" | spoj na objednani="_naObj_" spoj podminecne="_podm_" zastavka v trase spoje (r/i)="_wrRadku
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Kde se v datech vyskytuje daný znak atributu - linka, pořadí a zastávka. Doplněk `ScanAtributy`
/// pro vizuální kontrolu. IN: DB, ZNAK, MAXLIN. Read-only.
ClassMethod NajdiAtribut(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set hledany = $get(^||ML("IN","ZNAK"))
		set maxLin = +$get(^||ML("IN","MAXLIN")) if maxLin < 1 set maxLin = 500
		kill ^||ML("OUT")
		if (db = "") || (hledany = "") $$$ThrowStatus($$$ERROR($$$GeneralError, "DB a ZNAK jsou povinné"))

		set linek = 0, nalez = 0, sku = ""
		for {
			set sku = $order(^[db]JRL(sku))
			quit:sku=""
			set lin = ""
			for {
				set lin = $order(^[db]JRL(sku,lin))
				quit:lin=""
				quit:linek>=maxLin
				set linek = linek + 1
				set poradi = ""
				for {
					set poradi = $order(^[db]JRL(sku,lin,"J",poradi))
					quit:poradi=""
					set veta = $get(^[db]JRL(sku,lin,"J",poradi))
					set atr = $piece($piece($piece(veta, "#", 1), ":", 2), "#", 1)
					continue:'$find(atr, hledany)
					set nalez = nalez + 1
					continue:nalez>10
					set czst = $piece(veta, ":", 1)
					set ^||ML("OUT","NALEZ",nalez) = "sku="_sku_" lin="_lin_" poradi="_poradi_" czst="_czst_" atributy="_atr_" nazev="_$piece($get(^[db]JRZ(czst)), "$", 2)
				}
			}
		}
		set ^||ML("OUT","SOUHRN") = db_" znak="_hledany_" linek="_linek_" nalezu="_nalez
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Výskyt znaků atributů zastávky (#13048 bod 1): kolikrát se který znak objeví v registru zastávek
/// (`$`-pole 8) a v trase linky (J-věta `#1:2`). Ukáže, které znaky číselníku 16 zákazník reálně
/// používá - tedy jestli má smysl je tisknout. IN: DB, MAXLIN. Read-only.
ClassMethod ScanAtributy(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set maxLin = +$get(^||ML("IN","MAXLIN")) if maxLin < 1 set maxLin = 500
		kill ^||ML("OUT")
		if db = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "DB je povinné"))

		// registr zastávek (celý)
		kill pocty
		set czst = ""
		for {
			set czst = $order(^[db]JRZ(czst))
			quit:czst=""
			continue:'$isvalidnum(czst)
			set atr = $piece($get(^[db]JRZ(czst)), "$", 8)
			for i = 1:1:$length(atr) {
				set znak = $extract(atr, i)
				continue:znak=","
				set pocty("REGISTR", znak) = $get(pocty("REGISTR", znak)) + 1
			}
		}

		// trasy linek
		set linek = 0, sku = ""
		for {
			set sku = $order(^[db]JRL(sku))
			quit:sku=""
			set lin = ""
			for {
				set lin = $order(^[db]JRL(sku,lin))
				quit:lin=""
				quit:linek>=maxLin
				set linek = linek + 1
				set poradi = ""
				for {
					set poradi = $order(^[db]JRL(sku,lin,"J",poradi))
					quit:poradi=""
					set atr = $piece($piece($piece($get(^[db]JRL(sku,lin,"J",poradi)), "#", 1), ":", 2), "#", 1)
					for i = 1:1:$length(atr) {
						set znak = $extract(atr, i)
						continue:znak=","
						set pocty("TRASA", znak) = $get(pocty("TRASA", znak)) + 1
					}
				}
			}
		}

		set zdroj = ""
		for {
			set zdroj = $order(pocty(zdroj))
			quit:zdroj=""
			set znak = "", radek = ""
			for {
				set znak = $order(pocty(zdroj, znak))
				quit:znak=""
				set radek = radek _ $select(radek="":"", 1:" · ") _ znak _ "=" _ pocty(zdroj, znak)
			}
			set ^||ML("OUT", zdroj) = radek
		}
		set ^||ML("OUT","SOUHRN") = db _ " linek=" _ linek
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Čísla a názvy zastávek jedné stránky - kontrola, že výchozí/cílová sedí na směr stránky.
/// IN: DB, SKU, LIN, SMER, PORADI. Read-only.
ClassMethod StrankaCisla(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set sku = $get(^||ML("IN","SKU"))
		set lin = $get(^||ML("IN","LIN"))
		set smer = $get(^||ML("IN","SMER")) set:smer="" smer = "T"
		set poradi = +$get(^||ML("IN","PORADI")) set:'poradi poradi = 1
		kill ^||ML("OUT"), ^||TiskAd
		if (db = "") || (lin = "") $$$ThrowStatus($$$ERROR($$$GeneralError, "DB a LIN jsou povinné"))

		set page = ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).BuildStrankaZastavky(db, sku, lin, poradi, smer, "")
		if '$isobject(page) {
			set ^||ML("OUT","CHYBA") = "stránka se nesestavila"
			set ^||ML("OUT","DIAG") = "db="_db_" sku="_sku_" lin="_lin_" poradi="_poradi_" I="_$data(^[db]JRL(sku,lin,"I"))
			ret $$$OK
		}
		set klice = $listbuild("POPIS_ZASTAVKA", "CISLO_ZASTAVKA", "CIS_ZASTAVKA", "CIS_ZASTAVKA_VYCHOZI", "CIS_ZASTAVKA_CILOVA", "POPIS_SMER")
		for i = 1:1:$listlength(klice) {
			set k = $list(klice, i)
			set ^||ML("OUT", k) = page.values.%Get(k)
		}
		kill ^||TiskAd
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Kde v datech leží text se zadaným vzorkem (#13088 výluky): hledá v poznámkách linky
/// (`^JRL(sku,lin,"C",key,idx)`) i v komentářích spoje (`^JRL(sku,lin,"MhD","SeT_SpeC",znak)`).
/// IN: DB, PATTERN, MAXLIN (default 500). OUT: počty per zdroj + ukázky.
ClassMethod ScanText(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set pattern = $zconvert($get(^||ML("IN","PATTERN")), "L")
		set maxLin = +$get(^||ML("IN","MAXLIN")) if maxLin < 1 set maxLin = 500
		kill ^||ML("OUT")
		if (db = "") || (pattern = "") $$$ThrowStatus($$$ERROR($$$GeneralError, "DB a PATTERN jsou povinné"))

		set linek = 0, vPozn = 0, vKomentar = 0, ukazek = 0
		set sku = ""
		for {
			set sku = $order(^[db]JRL(sku))
			quit:sku=""
			set lin = ""
			for {
				set lin = $order(^[db]JRL(sku,lin))
				quit:lin=""
				quit:linek>=maxLin
				set linek = linek + 1

				// poznámky linky
				set key = ""
				for {
					set key = $order(^[db]JRL(sku,lin,"C",key))
					quit:key=""
					set idx = ""
					for {
						set idx = $order(^[db]JRL(sku,lin,"C",key,idx))
						quit:idx=""
						set txt = $get(^[db]JRL(sku,lin,"C",key,idx))
						continue:'$find($zconvert(txt,"L"), pattern)
						set vPozn = vPozn + 1
						if ukazek < 8 { set ukazek = ukazek + 1, ^||ML("OUT","UKAZKA",ukazek) = "POZNAMKA_LINKY sku="_sku_" lin="_lin_" key="_key_": "_$extract(txt,1,90) }
					}
				}

				// komentáře spoje (SeT_SpeC)
				set znak = ""
				for {
					set znak = $order(^[db]JRL(sku,lin,"MhD","SeT_SpeC",znak))
					quit:znak=""
					set txt = $get(^[db]JRL(sku,lin,"MhD","SeT_SpeC",znak))
					continue:'$find($zconvert(txt,"L"), pattern)
					set vKomentar = vKomentar + 1
					if ukazek < 8 { set ukazek = ukazek + 1, ^||ML("OUT","UKAZKA",ukazek) = "KOMENTAR_SPOJE sku="_sku_" lin="_lin_" znak="_znak_": "_$extract(txt,1,90) }
				}
			}
		}
		set ^||ML("OUT","SOUHRN") = db_" linek="_linek_" nalezu: poznamky linky="_vPozn_" komentare spoje="_vKomentar
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Řádky implementace metody na SERVERU obsahující vzorek - kontrola, že se `.cls` opravdu nasadila
/// (editace mimo hook se na server nedostane a testy pak běží proti staré kopii).
/// IN: CLASS, METHOD, PATTERN. OUT: nalezené řádky.
ClassMethod ZdrojMetody(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set cls = $get(^||ML("IN","CLASS"))
		set met = $get(^||ML("IN","METHOD"))
		set pattern = $get(^||ML("IN","PATTERN"))
		kill ^||ML("OUT")
		if (cls = "") || (met = "") $$$ThrowStatus($$$ERROR($$$GeneralError, "CLASS a METHOD jsou povinné"))

		set def = ##class(%Dictionary.MethodDefinition).%OpenId(cls _ "||" _ met)
		if '$isobject(def) {
			set ^||ML("OUT","CHYBA") = "metoda " _ cls _ "::" _ met _ " na serveru neexistuje"
			ret $$$OK
		}
		set ^||ML("OUT","SIGNATURA") = def.Name _ "(" _ def.FormalSpec _ ") As " _ def.ReturnType
		set impl = def.Implementation
		do impl.Rewind()
		set n = 0, nalez = 0
		while 'impl.AtEnd {
			set radek = impl.ReadLine()
			set n = n + 1
			continue:(pattern '= "")&&('$find(radek, pattern))
			set nalez = nalez + 1
			set ^||ML("OUT","RADEK",n) = radek
		}
		set ^||ML("OUT","SOUHRN") = "řádků=" _ n _ " nálezů=" _ nalez
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Kolik linek plní per-spoj sloupek (W pozice 13) a označník (W pozice 7) a u kolika zastávek se
/// hodnoty mezi trasami liší (= má vůbec smysl režim „více hodnot" nad sloupkem). IN: DB, MAXLIN.
ClassMethod ScanPerSpoj(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set maxLin = +$get(^||ML("IN","MAXLIN")) if maxLin < 1 set maxLin = 500
		kill ^||ML("OUT")
		if db = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "DB je povinné"))

		set linek = 0, linekSl = 0, linekOzn = 0, multiSl = 0, multiOzn = 0
		set sku = ""
		for {
			set sku = $order(^[db]JRL(sku))
			quit:sku=""
			set lin = ""
			for {
				set lin = $order(^[db]JRL(sku,lin))
				quit:lin=""
				quit:linek>=maxLin
				set linek = linek + 1
				kill hodSl, hodOzn
				set maSl = 0, maOzn = 0
				set ct = ""
				for {
					set ct = $order(^[db]JRL(sku,lin,"W",ct))
					quit:ct=""
					set w = $get(^[db]JRL(sku,lin,"W",ct))
					for i = 1:1:$length(w,"#") {
						set seg = $piece(w,"#",i)
						set poradi = $piece(seg,":",1)
						continue:poradi=""
						set sl = $piece(seg,":",13)
						set ozn = $piece(seg,":",7)
						if sl '= "" { set maSl = 1, hodSl(poradi,sl) = 1 }
						if ozn '= "" { set maOzn = 1, hodOzn(poradi,ozn) = 1 }
					}
				}
				set linekSl = linekSl + maSl, linekOzn = linekOzn + maOzn
				set p = ""
				for {
					set p = $order(hodSl(p))
					quit:p=""
					set n = 0, v = "" for { set v = $order(hodSl(p,v)) quit:v=""  set n = n + 1 }
					if n > 1 {
						set multiSl = multiSl + 1
						if multiSl <= 10 set ^||ML("OUT","MULTISL",multiSl) = db_" sku="_sku_" lin="_lin_" poradi="_p
					}
				}
				set p = ""
				for {
					set p = $order(hodOzn(p))
					quit:p=""
					set n = 0, v = "" for { set v = $order(hodOzn(p,v)) quit:v=""  set n = n + 1 }
					set:n>1 multiOzn = multiOzn + 1
				}
			}
		}
		set ^||ML("OUT","SOUHRN") = db_": linek="_linek_" s per-spoj sloupkem="_linekSl_" s per-spoj označníkem="_linekOzn
		set ^||ML("OUT","MULTI") = "zastávek s víc hodnotami: sloupek="_multiSl_" označník="_multiOzn
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Rozpad J-věty trasy linky na `#`-segmenty. IN: DB, SKU, LIN, MAX (default 8 zastávek).
/// OUT: per zastávka jeden řádek "poradi | seg1 | seg2 | ..." pro ruční porovnání s editorem trasy.
ClassMethod DumpJVeta(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		set db = $get(^||ML("IN","DB"))
		set sku = $get(^||ML("IN","SKU"))
		set lin = $get(^||ML("IN","LIN"))
		set max = +$get(^||ML("IN","MAX")) if max < 1 set max = 8
		kill ^||ML("OUT")
		if (db = "") || (lin = "") $$$ThrowStatus($$$ERROR($$$GeneralError, "DB a LIN jsou povinné"))

		set poradi = "", pocet = 0
		for {
			set poradi = $order(^[db]JRL(sku, lin, "J", poradi))
			quit:poradi=""
			quit:pocet>=max
			set veta = $get(^[db]JRL(sku, lin, "J", poradi))
			continue:veta=""
			set pocet = pocet + 1
			set radek = "poradi=" _ poradi
			for i = 1:1:$length(veta, "#") {
				set seg = $piece(veta, "#", i)
				continue:seg=""
				set radek = radek _ "  [" _ i _ "]" _ seg
			}
			set ^||ML("OUT", "J", $translate($justify(pocet, 3), " ", "0")) = radek
		}
		set ^||ML("OUT", "SOUHRN") = db _ " sku=" _ sku _ " lin=" _ lin _ " zastavek=" _ pocet
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

}
