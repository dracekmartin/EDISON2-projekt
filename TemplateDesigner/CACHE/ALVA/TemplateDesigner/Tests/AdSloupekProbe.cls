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
