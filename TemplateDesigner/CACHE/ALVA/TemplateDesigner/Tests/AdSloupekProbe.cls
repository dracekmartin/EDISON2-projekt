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
