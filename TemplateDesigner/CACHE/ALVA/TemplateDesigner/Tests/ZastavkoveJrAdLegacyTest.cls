Include %occInclude

/// Gold pojistka „vůči starému": zóny per zastávka z našeho `ZastavkoveJrAd.GetData` vs LEGACY
/// `ALVA.TISKMHD.Data.LoadData` (VolanoZ=JRMAIN) - tentýž generátor, který krmil starý modul.
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
		do $$$AssertTrue(1, "namespace "_db_" není na serveru - přestupy přeskočeny")
		quit
	}
	set pocetA = ..PrestupovyPocet(db, 1, "0330510", "A")
	do $$$AssertTrue(pocetA > 0, "autobus ""A"" se emituje na 0330510 (nalezeno "_pocetA_"×)")
}

/// #8828: parametr „Rozdělit vývěsku per označník" → multi-označníková vývěska dá víc stránek než bez něj
/// (a filtruje odjezdy). Zamyká, že split reálně přidá stránky na 0320680 (ČB aut.nádr. = označníky 11/22).
Method TestSplitPerOznacnik()
{
	set db = "CB_CSADAD"
	if '##class(%SYS.Namespace).Exists(db) {
		do $$$AssertTrue(1, "namespace "_db_" není na serveru - přeskočeno")
		quit
	}
	do ..SetupSysFor(.sys, db)
	set off = ..PocetStranek(.sys, 1, "0320680", 0)
	set on  = ..PocetStranek(.sys, 1, "0320680", 1)
	do $$$AssertTrue(on > off, "rozdělení per označník přidá stránky na 0320680 (off="_off_" on="_on_")")
}

/// KOVED #13048 bod 4 (duplikace): server emituje NASTUPISTE_VARIANT a na 0330510 existuje downstream
/// zastávka se ≥2 stanovišti napříč variantami (podklad pro klientskou duplikaci řádku). Zamyká data.
Method TestNastupisteVariantEmit()
{
	set db = "CB_CSADAD"
	if '##class(%SYS.Namespace).Exists(db) {
		do $$$AssertTrue(1, "namespace "_db_" není na serveru - přeskočeno")
		quit
	}
	set pocet = ..PocetDownstreamMultiStanovist(db, 1, "0330510")
	do $$$AssertTrue(pocet > 0, "≥2 stanoviště variant u downstream zastávky na 0330510 (nalezeno "_pocet_")")
}

/// KOVED #13048 bod 5: režim písmen variant - hlavičky = písmena (první bez písmene), u minut
/// anotace „varianta-trasy" a rozdílové poznámky potlačené. Data-lock 0330510 (11 variant tras).
Method TestPismenaVariant()
{
	set db = "CB_CSADAD"
	if '##class(%SYS.Namespace).Exists(db) {
		do $$$AssertTrue(1, "namespace "_db_" není na serveru - přeskočeno")
		quit
	}
	do ..SetupSysFor(.sys, db)
	kill ^||ML("IN"), ^||TiskAd
	set ^||ML("IN","PISMENA_VARIANT") = "true", ^||ML("IN","PRVNI_BEZ_PISMENE") = "true"
	set page = ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).BuildStrankaZastavky(db, 1, "0330510", 1, "T")
	kill ^||ML("IN"), ^||TiskAd
	do $$$AssertTrue($isobject(page), "stránka 0330510/T/1 se sestavila")
	quit:'$isobject(page)
	set hlavicky = page.values.%Get("VARIANTY_TRAS")
	do $$$AssertTrue(hlavicky.%Size() > 1, "více variantních sloupců ("_hlavicky.%Size()_")")
	do $$$AssertEquals(hlavicky.%Get(0), "", "první varianta bez písmene")
	do $$$AssertEquals(hlavicky.%Get(1), "A", "druhá varianta = A")
	set odjezdy = page.rows.%Get("ODJEZDY"), pocetPismen = 0, pocetRozdilu = 0
	for i=0:1:odjezdy.%Size()-1 {
		set anotace = odjezdy.%Get(i).%Get("ANOTACE")
		for j=0:1:anotace.%Size()-1 {
			if anotace.%Get(j).%Get("KATEGORIE") = "varianta-trasy" set pocetPismen = pocetPismen + 1
			if anotace.%Get(j).%Get("KATEGORIE") = "rozdil-trasy" set pocetRozdilu = pocetRozdilu + 1
		}
	}
	do $$$AssertTrue(pocetPismen > 0, "písmena variant u minut se emitují ("_pocetPismen_"×)")
	do $$$AssertEquals(pocetRozdilu, 0, "rozdílové poznámky v režimu písmen potlačeny")
}

/// #8828 dotažení: rozdělená stránka nese označník v titulku (POPIS_ZASTAVKA) i v řádku vývěskové
/// zastávky (NASTUPISTE) - bez toho stránky označníků 11/22 na 0320680 vypadají identicky.
Method TestSplitTitulekARadek()
{
	set db = "CB_CSADAD"
	if '##class(%SYS.Namespace).Exists(db) {
		do $$$AssertTrue(1, "namespace "_db_" není na serveru - přeskočeno")
		quit
	}
	do ..SetupSysFor(.sys, db)
	kill ^||ML("IN"), ^||TiskAd
	set page = ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).BuildStrankaZastavky(db, 1, "0320680", 1, "T", "22")
	kill ^||TiskAd
	do $$$AssertTrue($isobject(page), "rozdělená stránka (označník 22) se sestavila")
	quit:'$isobject(page)
	do $$$AssertTrue(page.values.%Get("POPIS_ZASTAVKA") [ ", označník 22", "titulek nese označník stránky")
	do $$$AssertEquals(page.values.%Get("NASTUPISTE"), "22", "NASTUPISTE stránky = označník")
	set zastavky = page.rows.%Get("ZASTAVKY"), radkovyOznacnik = ""
	for i=0:1:zastavky.%Size()-1 {
		if zastavky.%Get(i).%Get("AKTUALNI_ZASTAVKA") = "true" set radkovyOznacnik = zastavky.%Get(i).%Get("NASTUPISTE")
	}
	do $$$AssertEquals(radkovyOznacnik, "22", "řádek vývěskové zastávky nese označník stránky")
}

/// 1:1 porovnání řetězce zón per zastávka (#-delim napříč zastávkami) legacy vs náš.
Method AssertZony(db As %String, sku As %String, lin As %String, page As %Integer)
{
	if '##class(%SYS.Namespace).Exists(db) {
		do $$$AssertTrue(1, "namespace "_db_" není na serveru - zóny "_lin_" přeskočeny")
		quit
	}
	if '##class(%Dictionary.CompiledClass).%ExistsId("ALVA.TISKMHD.Data") {
		do $$$AssertTrue(1, "legacy ALVA.TISKMHD.Data není na serveru - zóny "_lin_" přeskočeny")
		quit
	}
	set leg = ..LegacyZony(db, sku, lin, page)
	if leg = "" {
		do $$$AssertTrue(1, "legacy zóny prázdné pro "_lin_" (firma zóny nemá) - přeskočeno")
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
	// OUT nemá count node - iterovat přes $order; každý uzel je řetězec "KLÍČ~HODNOTA".
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

/// Počet downstream (ne aktuálních) zastávek napříč stránkami se ≥2 odlišnými stanovišti variant.
ClassMethod PocetDownstreamMultiStanovist(db As %String, sku As %String, lin As %String) As %Integer
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
			continue:z.%Get("AKTUALNI_ZASTAVKA")
			kill dist  set arr = z.%Get("NASTUPISTE_VARIANT")
			if $isobject(arr) { set ai = arr.%GetIterator()  while ai.%GetNext(.ak,.av) { i av'="" s dist(av)="" } }
			set cnt = 0, d1 = ""  for { set d1 = $order(dist(d1)) quit:d1=""  set cnt = cnt + 1 }
			i cnt > 1 set pocet = pocet + 1
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
	// Legacy $$$MakroGlobal přepíná IN/OUT globál dle App - bez toho čte ^CacheTempMLSrv místo ^||ML.
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
	// RAW: zastávky linky, jejichž dta2 má A/L (nezávisle na emisi) - pro dohledání identity.
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

/// Sken (cache_query): IN DB [LIMIT] → najde linky, kde je multi-stanovištní zastávka DOWNSTREAM
/// a ZOBRAZENÁ (kladný čas, ne aktuální, ne projetá) = duplikace se projeví v běžném pohledu.
ClassMethod ScanVisibleMulti(ByRef sys As %String) As %Status
{
	set k = ""
	for { set k = $order(^||ML("IN",k)) quit:k=""  set v = $get(^||ML("IN",k))  set inp($piece(v,"~",1)) = $piece(v,"~",2,999) }
	set db = $get(inp("DB"))
	if db = "" set db = $get(sys("U","DATA"))
	set limit = +$get(inp("LIMIT"),40)
	do ..SetupSysFor(.sys, db)
	set nalezeno = "", cnt = 0, sku = ""
	for {
		set sku = $order(^[db]JRL(sku)) quit:sku=""
		set lin = ""
		for {
			set lin = $order(^[db]JRL(sku,lin)) quit:lin=""
			quit:cnt'<limit
			set cnt = cnt + 1
			kill ^||ML("IN"), ^||ML("OUT")
			set ^||ML("IN","SKUPINA_LINEK_ID") = sku, ^||ML("IN","LINKA_ID") = lin
			set st = $$$OK
			try { do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).GetData(.sys) } catch { continue }
			set p = ""
			for {
				set p = $order(^||ML("OUT",p)) quit:p=""
				continue:'$isvalidnum(p)
				set json = $get(^||ML("OUT",p)) continue:json=""
				set zas = ##class(%DynamicObject).%FromJSON(json).rows.%Get("ZASTAVKY")
				continue:'$isobject(zas)
				set it = zas.%GetIterator(), hit = ""
				while it.%GetNext(.zk, .z) {
					continue:'(z.%Get("CAS_JIZDY") > 0)      // jen downstream zobrazené (kladný kumul. čas)
					kill dist  set arr = z.%Get("NASTUPISTE_VARIANT")
					if $isobject(arr) { set ai = arr.%GetIterator()  while ai.%GetNext(.ak,.av) { i av'="" s dist(av)="" } }
					set dc = 0, d1 = ""  for { set d1 = $order(dist(d1)) quit:d1=""  set dc = dc + 1 }
					i dc > 1 { set hit = z.%Get("NAZEV_ZASTAVKY")_"("_..JoinKeys(.dist)_")"  quit }
				}
				i hit '= "" { set nalezeno = nalezeno_"lin="_lin_" str="_p_" "_hit_" ; "  quit }
			}
			i nalezeno [ ("lin="_lin_" ") quit
		}
		quit:nalezeno'=""
	}
	kill ^||ML("OUT")
	set ^||ML("OUT","NALEZENO") = $s(nalezeno="":"(nic v prvních "_limit_" linkách)",1:nalezeno)
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

/// #8828 validace (cache_query): IN DB/SKU/LIN/STOP → OUT počet stránek OFF vs ON + rozpad stránek
/// dané vývěskové zastávky při zapnutém rozdělení (NASTUPISTE + počet odjezdů per stránka).
ClassMethod SplitProbe(ByRef sys As %String) As %Status
{
	set k = ""
	for { set k = $order(^||ML("IN",k)) quit:k=""  set v = $get(^||ML("IN",k))  set inp($piece(v,"~",1)) = $piece(v,"~",2,999) }
	set db = $get(inp("DB"))
	if db = "" set db = $get(sys("U","DATA"))
	set sku = $get(inp("SKU"),1), lin = $get(inp("LIN")), stop = $get(inp("STOP"))
	do ..SetupSysFor(.sys, db)
	set pagesOff = ..PocetStranek(.sys, sku, lin, 0)
	set pagesOn  = ..PocetStranek(.sys, sku, lin, 1)
	// rozpad stránek zvolené vývěsky při ON
	kill ^||ML("IN"), ^||ML("OUT")
	set ^||ML("IN","SKUPINA_LINEK_ID") = sku, ^||ML("IN","LINKA_ID") = lin, ^||ML("IN","ROZDELIT_PER_OZNACNIK") = "true"
	do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).GetData(.sys)
	set rozpad = "", p = ""
	for {
		set p = $order(^||ML("OUT",p)) quit:p=""
		continue:'$isvalidnum(p)
		set json = $get(^||ML("OUT",p)) continue:json=""
		set obj = ##class(%DynamicObject).%FromJSON(json)
		continue:obj.values.%Get("POPIS_ZASTAVKA")'=stop
		set odj = obj.rows.%Get("ODJEZDY"), n = 0
		if $isobject(odj) { set it = odj.%GetIterator()  while it.%GetNext(.dk,.dv) { set n = n + 1 } }
		set rozpad = rozpad_"[nast="_obj.values.%Get("NASTUPISTE")_" smer="_obj.values.%Get("SMER")_" odjezdů="_n_"] "
	}
	kill ^||ML("OUT")
	set ^||ML("OUT","STRANEK_OFF") = pagesOff
	set ^||ML("OUT","STRANEK_ON") = pagesOn
	set ^||ML("OUT","ROZPAD_"_stop) = rozpad
	quit $$$OK
}

/// Dump celé sekvence stránek (cache_query): IN DB/SKU/LIN/SPLIT → OUT pořadí stránek
/// „idx:vyveska(smer) nast=X odj=n". Pro diagnostiku listování (jak se střídají označníky).
ClassMethod PageSeqProbe(ByRef sys As %String) As %Status
{
	set k = ""
	for { set k = $order(^||ML("IN",k)) quit:k=""  set v = $get(^||ML("IN",k))  set inp($piece(v,"~",1)) = $piece(v,"~",2,999) }
	set db = $get(inp("DB"))
	if db = "" set db = $get(sys("U","DATA"))
	set sku = $get(inp("SKU"),1), lin = $get(inp("LIN")), split = +$get(inp("SPLIT"),1)
	do ..SetupSysFor(.sys, db)
	kill ^||ML("IN"), ^||ML("OUT")
	set ^||ML("IN","SKUPINA_LINEK_ID") = sku, ^||ML("IN","LINKA_ID") = lin
	set:split ^||ML("IN","ROZDELIT_PER_OZNACNIK") = "true"
	do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).GetData(.sys)
	set seq = "", idx = 0, p = ""
	for {
		set p = $order(^||ML("OUT",p)) quit:p=""
		continue:'$isvalidnum(p)
		set json = $get(^||ML("OUT",p)) continue:json=""
		set idx = idx + 1
		set obj = ##class(%DynamicObject).%FromJSON(json)
		set odj = obj.rows.%Get("ODJEZDY"), n = 0
		if $isobject(odj) { set it = odj.%GetIterator()  while it.%GetNext(.dk,.dv) { set n = n + 1 } }
		set seq = seq_idx_":"_obj.values.%Get("POPIS_ZASTAVKA")_"("_obj.values.%Get("SMER")_") nast="_obj.values.%Get("NASTUPISTE")_" odj="_n_" | "
	}
	kill ^||ML("OUT")
	set ^||ML("OUT","SEQ") = seq
	quit $$$OK
}

ClassMethod PocetStranek(ByRef sys As %String, sku, lin, split) As %Integer
{
	kill ^||ML("IN"), ^||ML("OUT")
	set ^||ML("IN","SKUPINA_LINEK_ID") = sku, ^||ML("IN","LINKA_ID") = lin
	set:split ^||ML("IN","ROZDELIT_PER_OZNACNIK") = "true"
	do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).GetData(.sys)
	set n = 0, p = ""
	for { set p = $order(^||ML("OUT",p)) quit:p=""  i $isvalidnum(p) s n = n + 1 }
	quit n
}

/// Validace chování stránkování (cache_query): IN DB/SKU/LIN → OUT kolik stránek dnes vzniká
/// a zda se nějaká (vývěska × směr) opakuje = zda se už dnes dělí per označník (nemělo by).
ClassMethod PageCountProbe(ByRef sys As %String) As %Status
{
	set k = ""
	for { set k = $order(^||ML("IN",k)) quit:k=""  set v = $get(^||ML("IN",k))  set inp($piece(v,"~",1)) = $piece(v,"~",2,999) }
	set db = $get(inp("DB"))
	if db = "" set db = $get(sys("U","DATA"))
	set sku = $get(inp("SKU"),1), lin = $get(inp("LIN"))
	do ..SetupSysFor(.sys, db)
	kill ^||ML("IN"), ^||ML("OUT")
	set ^||ML("IN","SKUPINA_LINEK_ID") = sku, ^||ML("IN","LINKA_ID") = lin
	do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).GetData(.sys)
	kill klic
	set nPages = 0, maxOpak = 0, p = "", priklad = ""
	for {
		set p = $order(^||ML("OUT",p)) quit:p=""
		continue:'$isvalidnum(p)
		set json = $get(^||ML("OUT",p)) continue:json=""
		set nPages = nPages + 1
		set obj = ##class(%DynamicObject).%FromJSON(json)
		set kl = obj.values.%Get("POPIS_ZASTAVKA")_"|"_obj.values.%Get("SMER")
		set klic(kl) = $g(klic(kl)) + 1
		i klic(kl) > maxOpak set maxOpak = klic(kl), priklad = kl
	}
	kill ^||ML("OUT")
	set ^||ML("OUT","STRANEK") = nPages
	set ^||ML("OUT","MAX_OPAKOVANI_VYVESKA_SMER") = maxOpak_" (nejvíc: "_priklad_")"
	quit $$$OK
}

/// Diagnostika NASTUPISTE_VARIANT (cache_query): IN DB/SKU/LIN/PAGE → OUT řádky zastávek 1. stránky
/// „název | TRASA_MIN | NASTUPISTE_VARIANT" + MULTI = zastávky se ≥2 stanovišti napříč variantami.
ClassMethod NastVarProbe(ByRef sys As %String) As %Status
{
	set k = ""
	for { set k = $order(^||ML("IN",k)) quit:k=""  set v = $get(^||ML("IN",k))  set inp($piece(v,"~",1)) = $piece(v,"~",2,999) }
	set db = $get(inp("DB"))
	if db = "" set db = $get(sys("U","DATA"))
	set sku = $get(inp("SKU"),1), lin = $get(inp("LIN")), page = +$get(inp("PAGE"),1)
	do ..SetupSysFor(.sys, db)
	kill ^||ML("IN"), ^||ML("OUT")
	set ^||ML("IN","SKUPINA_LINEK_ID") = sku
	set ^||ML("IN","LINKA_ID") = lin
	do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).GetData(.sys)
	// Projdi všechny stránky, najdi DOWNSTREAM (ne aktuální) zastávky se ≥2 stanovišti = ty se duplikují.
	// Plný dump jedné stránky (IN DUMPPAGE): každá zastávka name|akt|tm|nv → hledání proč se neduplikuje.
	set dumpPage = $get(inp("DUMPPAGE"))
	if dumpPage '= "" {
		set dj = $get(^||ML("OUT",dumpPage))
		kill ^||ML("OUT")
		if dj = "" { set ^||ML("OUT","ERR") = "stránka prázdná"  quit $$$OK }
		set dz = ##class(%DynamicObject).%FromJSON(dj).rows.%Get("ZASTAVKY"), di = dz.%GetIterator(), rn = 0, dump = ""
		while di.%GetNext(.dk, .d) {
			set rn = rn + 1
			set dump = dump_rn_":"_d.%Get("NAZEV_ZASTAVKY")_"[cas="_d.%Get("CAS_JIZDY")_" tm="_..JoinArr(d.%Get("TRASA_MIN"))_" nv="_..JoinArr(d.%Get("NASTUPISTE_VARIANT"))_"] "
		}
		set ^||ML("OUT","DUMP") = dump
		quit $$$OK
	}
	set nPages = 0, stranky = "", p = ""
	for {
		set p = $order(^||ML("OUT",p)) quit:p=""
		continue:'$isvalidnum(p)
		set json = $get(^||ML("OUT",p)) continue:json=""
		set nPages = nPages + 1
		set obj = ##class(%DynamicObject).%FromJSON(json)
		set vyveska = obj.values.%Get("POPIS_ZASTAVKA"), smer = obj.values.%Get("POPIS_SMER")
		set zas = obj.rows.%Get("ZASTAVKY")
		continue:'$isobject(zas)
		set multiZde = "", it = zas.%GetIterator()
		while it.%GetNext(.zk, .z) {
			continue:z.%Get("AKTUALNI_ZASTAVKA")     // vývěska = jeden řádek, neduplikuje
			kill dist  set arr = z.%Get("NASTUPISTE_VARIANT")
			if $isobject(arr) { set ai = arr.%GetIterator()  while ai.%GetNext(.ak,.av) { i av'="" s dist(av)="" } }
			set cnt = 0, d1 = ""  for { set d1 = $order(dist(d1)) quit:d1=""  set cnt = cnt + 1 }
			i cnt > 1 set multiZde = multiZde_z.%Get("NAZEV_ZASTAVKY")_"("_..JoinKeys(.dist)_") "
		}
		i multiZde '= "" set stranky = stranky_"[str "_p_" vyveska="_vyveska_" ("_smer_") → duplikuje: "_multiZde_"] "
	}
	kill ^||ML("OUT")
	set ^||ML("OUT","PAGES") = nPages
	set ^||ML("OUT","STRANKY_S_DUPLIKACI") = stranky
	quit $$$OK
}

ClassMethod JoinArr(arr) As %String
{
	quit:'$isobject(arr) ""
	set s = "", it = arr.%GetIterator()
	while it.%GetNext(.k,.v) { set s = s_$s(s="":"",1:",")_v }
	quit s
}

ClassMethod JoinKeys(ByRef a) As %String
{
	set s = "", k = ""
	for { set k = $order(a(k)) quit:k=""  set s = s_$s(s="":"",1:",")_k }
	quit s
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
