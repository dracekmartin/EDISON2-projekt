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
