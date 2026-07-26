Include %occInclude

/// Gold pojistka „vůči starému": zóny per zastávka z našeho `ZastavkoveJrAd.GetData` vs LEGACY
/// `ALVA.TISKMHD.Data.LoadData` (VolanoZ=JRMAIN) — tentýž generátor, který krmil starý modul.
/// Ověřuje 1:1 shodu zón na reprezentativních linkách napříč firmami (kde zóny reálně jsou).
///
/// ⚠️ Vázané na per-firma namespace (CB_GWBUS / KV_DP). Namespace / legacy třída chybí → skip
/// (jiný server, legitimní). Legacy prázdné zóny (firma je nemá) → skip.
/// Diagnostika mimo runner: `cache_query ZonyProbe` (IN: DB, SKU, LIN, PAGE) → OUT LEG/OUR/MATCH.
Class ALVA.TemplateDesigner.Tests.ZastavkoveJrAdLegacyTest Extends ALVA.EDTesting.MLTestCase
{

Method TestZonyVsLegacyGwbus()
{
	do ..AssertZony("CB_GWBUS", 1, 320113, 1)
}

Method TestZonyVsLegacyKvdp()
{
	do ..AssertZony("KV_DP", 1, 410101, 1)
}

/// Přestupy: autobus „A" na terminálech linky 0330510 (ČB AN) se emituje do ATRIBUTY zastávek.
/// Zamyká rozšíření nad legacy (server je posílá vždy; zobrazení řídí přepínač na prvku StopList).
Method TestPrestupyEmit()
{
	set db = "CB_CSADAD"
	if '##class(%SYS.Namespace).Exists(db) {
		do $$$AssertTrue(1, "namespace "_db_" není na serveru — přestupy přeskočeny")
		quit
	}
	set pocetA = ..PrestupovyPocet(db, 1, "0330510", "A")
	do $$$AssertTrue(pocetA > 0, "autobus ""A"" se emituje na 0330510 (nalezeno "_pocetA_"×)")
}

/// 1:1 porovnání řetězce zón per zastávka (#-delim napříč zastávkami) legacy vs náš.
Method AssertZony(db As %String, sku As %String, lin As %String, page As %Integer)
{
	if '##class(%SYS.Namespace).Exists(db) {
		do $$$AssertTrue(1, "namespace "_db_" není na serveru — zóny "_lin_" přeskočeny")
		quit
	}
	if '##class(%Dictionary.CompiledClass).%ExistsId("ALVA.TISKMHD.Data") {
		do $$$AssertTrue(1, "legacy ALVA.TISKMHD.Data není na serveru — zóny "_lin_" přeskočeny")
		quit
	}
	set leg = ..LegacyZony(db, sku, lin, page)
	if leg = "" {
		do $$$AssertTrue(1, "legacy zóny prázdné pro "_lin_" (firma zóny nemá) — přeskočeno")
		quit
	}
	set our = ..OurZony(db, sku, lin, page)
	do $$$AssertEquals(our, leg, "zóny "_lin_" (náš vs legacy; náš="_our_")")
}

/// Legacy zóny (#-delim per zastávka) z `ALVA.TISKMHD.Data.LoadData`. Soběstačné (vlastní sys).
ClassMethod LegacyZony(db As %String, sku As %String, lin As %String, page As %Integer) As %String
{
	do ..SetupSysFor(.sys, db)
	kill ^||ML("IN"), ^||ML("OUT")
	// Legacy LoadData čte IN jako řetězce "KLÍČ~HODNOTA" (iteruje všechny subscripty).
	set ^||ML("IN","1") = "VOLANO_Z~JRMAIN"
	set ^||ML("IN","2") = "SESTAVA~1"
	set ^||ML("IN","3") = "VALUE~"_sku_"#"_lin_"#"_page_"#3#0-23#0##"
	do ##class(ALVA.TISKMHD.Data).LoadData(.sys)
	// OUT nemá count node — iterovat přes $order; každý uzel je řetězec "KLÍČ~HODNOTA".
	set i = "", zony = ""
	for {
		set i = $order(^||ML("OUT",i)) quit:i=""
		set radek = $get(^||ML("OUT",i))
		if $piece(radek,"~",1) = "SeznamZastavekDataSloupecZony" {
			set zony = $piece(radek,"~",2,999)
			quit
		}
	}
	quit zony
}

/// Naše zóny (#-delim per zastávka) z `ZastavkoveJrAd.GetData`, stránka `page`. Soběstačné.
/// Vícezónová zastávka = hodnoty spojené čárkou uvnitř jednoho pole (jako legacy).
ClassMethod OurZony(db As %String, sku As %String, lin As %String, page As %Integer) As %String
{
	do ..SetupSysFor(.sys, db)
	kill ^||ML("IN"), ^||ML("OUT")
	set ^||ML("IN","SKUPINA_LINEK_ID") = sku
	set ^||ML("IN","LINKA_ID") = lin
	do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).GetData(.sys)
	set json = $get(^||ML("OUT",page))
	if json = "" quit ""
	set obj = ##class(%DynamicObject).%FromJSON(json)
	set zas = obj.rows.%Get("ZASTAVKY")
	set zony = ""
	if $isobject(zas) {
		set it = zas.%GetIterator()
		while it.%GetNext(.k, .z) {
			set arr = z.%Get("ZONY"), zval = ""
			if $isobject(arr) {
				set zi = arr.%GetIterator()
				while zi.%GetNext(.zk, .zv) { set zval = zval_$select(zval="":"",1:",")_zv }
			}
			set zony = zony_$select(zony="":"",1:"#")_zval
		}
	}
	quit zony
}

/// Kolik zastávek napříč všemi stránkami má v ATRIBUTY daný znak (emitovaný GetData).
ClassMethod PrestupovyPocet(db As %String, sku As %String, lin As %String, hledany As %String) As %Integer
{
	do ..SetupSysFor(.sys, db)
	kill ^||ML("IN"), ^||ML("OUT")
	set ^||ML("IN","SKUPINA_LINEK_ID") = sku
	set ^||ML("IN","LINKA_ID") = lin
	do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).GetData(.sys)
	set pocet = 0, p = ""
	for {
		set p = $order(^||ML("OUT",p)) quit:p=""
		continue:'$isvalidnum(p)
		set json = $get(^||ML("OUT",p)) continue:json=""
		set zas = ##class(%DynamicObject).%FromJSON(json).rows.%Get("ZASTAVKY")
		continue:'$isobject(zas)
		set it = zas.%GetIterator()
		while it.%GetNext(.zk, .z) {
			set arr = z.%Get("ATRIBUTY") continue:'$isobject(arr)
			set ai = arr.%GetIterator()
			while ai.%GetNext(.k2, .a) { i a.%Get("ZNAK") = hledany set pocet = pocet + 1 }
		}
	}
	quit pocet
}

/// Naplní sys pro $$$FillParams (IdFa = data namespace; AD data nejsou idfa-scoped, stačí non-empty).
ClassMethod SetupSysFor(ByRef sys, db As %String)
{
	set sys("U","IdFa") = db
	set sys("U","delimiter") = "~"
	set sys("U","SystemDB") = $namespace
	set sys("U","DATA") = db
	set sys("U","MLP") = "1"
	set sys("U","loginName") = "test"
	set sys("U","fullName") = "test"
	// Legacy $$$MakroGlobal přepíná IN/OUT globál dle App — bez toho čte ^CacheTempMLSrv místo ^||ML.
	set sys("C","App") = "ALWA.SYS.MLRest"
}

/// Diagnostika přestupů (cache_query): IN DB/SKU/LIN → OUT ZNAKY = seznam „ZNAK×počet"
/// napříč všemi zastávkami všech stránek (ověří, že L/A přestupy dorazí do ATRIBUTY).
ClassMethod AttrProbe(ByRef sys As %String) As %Status
{
	set k = ""
	for {
		set k = $order(^||ML("IN",k)) quit:k=""
		set v = $get(^||ML("IN",k))
		set inp($piece(v,"~",1)) = $piece(v,"~",2,999)
	}
	set db = $get(inp("DB"))
	if db = "" set db = $get(sys("U","DATA"))
	set sku = $get(inp("SKU"),1), lin = $get(inp("LIN"))
	do ..SetupSysFor(.sys, db)
	kill ^||ML("IN"), ^||ML("OUT")
	set ^||ML("IN","SKUPINA_LINEK_ID") = sku
	set ^||ML("IN","LINKA_ID") = lin
	do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).GetData(.sys)
	kill cnt
	set p = ""
	for {
		set p = $order(^||ML("OUT",p)) quit:p=""
		continue:'$isvalidnum(p)
		set json = $get(^||ML("OUT",p)) continue:json=""
		set obj = ##class(%DynamicObject).%FromJSON(json)
		set zas = obj.rows.%Get("ZASTAVKY") continue:'$isobject(zas)
		set it = zas.%GetIterator()
		while it.%GetNext(.zk, .z) {
			set arr = z.%Get("ATRIBUTY") continue:'$isobject(arr)
			set ai = arr.%GetIterator()
			while ai.%GetNext(.k2, .a) { set znak = a.%Get("ZNAK")  set cnt(znak) = $get(cnt(znak)) + 1 }
		}
	}
	// RAW: zastávky linky, jejichž dta2 má A/L (nezávisle na emisi) — pro dohledání identity.
	set raw = "", poradi = ""
	for {
		set poradi = $order(^[db]JRL(sku,lin,"J",poradi)) quit:poradi=""
		set atr = $piece($piece($get(^[db]JRL(sku,lin,"J",poradi)),":",2),"#",1)
		i (atr["A") || (atr["L") set raw = raw_poradi_"("_atr_") "
	}
	kill ^||ML("OUT")
	set out = "", znak = ""
	for { set znak = $order(cnt(znak)) quit:znak=""  set out = out_znak_"×"_cnt(znak)_" " }
	set ^||ML("OUT","ZNAKY") = out       // histogram emitovaných atributů (přes všechny stránky)
	set ^||ML("OUT","RAW") = raw         // zastávky s A/L v datech (poradi)
	quit $$$OK
}

/// Sken (cache_query): IN DB → OUT LINKY = linky firmy, jejichž libovolná zastávka má
/// v dta2 přestupový atribut (A/L). Najde testovací linku pro ověření přestupů.
ClassMethod ScanTransfers(ByRef sys As %String) As %Status
{
	set k = ""
	for { set k = $order(^||ML("IN",k)) quit:k=""  set v = $get(^||ML("IN",k))  set inp($piece(v,"~",1)) = $piece(v,"~",2,999) }
	set db = $get(inp("DB"))
	if db = "" set db = $get(sys("U","DATA"))
	kill ^||ML("OUT")
	set found = "", cntFound = 0, sku = ""
	for {
		set sku = $order(^[db]JRL(sku)) quit:sku=""
		set lin = ""
		for {
			set lin = $order(^[db]JRL(sku,lin)) quit:lin=""
			quit:cntFound'<25
			set poradi = "", hit = ""
			for {
				set poradi = $order(^[db]JRL(sku,lin,"J",poradi)) quit:poradi=""
				set veta = $get(^[db]JRL(sku,lin,"J",poradi))
				set atr = $piece($piece(veta,":",2),"#",1)
				if (atr["A") || (atr["L") { set hit = hit_$piece(atr,"",1)  set found = found_sku_"/"_lin_"("_atr_") "  set cntFound = cntFound + 1  quit }
			}
		}
	}
	set ^||ML("OUT","LINKY") = found
	quit $$$OK
}

/// Diagnostika (cache_query, mimo runner): IN DB/SKU/LIN/PAGE → OUT LEG/OUR/MATCH.
ClassMethod ZonyProbe(ByRef sys As %String) As %Status
{
	// cache_query mapuje parametry poziciálně jako "KLÍČ~HODNOTA" → rozparsovat do inp().
	set k = "", inKeys = ""
	for {
		set k = $order(^||ML("IN",k)) quit:k=""
		set v = $get(^||ML("IN",k))
		set inp($piece(v,"~",1)) = $piece(v,"~",2,999)
		set inKeys = inKeys_k_"="_v_"; "
	}
	set db = $get(inp("DB"))
	if db = "" set db = $get(sys("U","DATA"))
	set sku = $get(inp("SKU"),1)
	set lin = $get(inp("LIN"))
	set page = +$get(inp("PAGE"),1)
	set leg = ..LegacyZony(db, sku, lin, page)
	set our = ..OurZony(db, sku, lin, page)
	kill ^||ML("OUT")
	set ^||ML("OUT","IN_KEYS") = inKeys
	set ^||ML("OUT","DB") = db
	set ^||ML("OUT","LEG") = leg
	set ^||ML("OUT","OUR") = our
	set ^||ML("OUT","MATCH") = (leg = our)
	quit $$$OK
}

}
