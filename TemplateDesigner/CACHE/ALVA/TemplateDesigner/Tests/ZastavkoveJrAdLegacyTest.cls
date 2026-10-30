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
	// 320113 z dat zmizela (30. 10.). Na první stránce 320150 je dosažitelná celá trasa, takže seznam
	// zastávek odpovídá starému modulu; vývěska nedosažitelné zastávky vynechává (#7959).
	do ..AssertZony("CB_GWBUS", 1, 320150, 1)
}

Method TestZonyVsLegacyKvdp()
{
	do ..AssertZony("KV_DP", 1, 410101, 1)
}

/// Záskok zóny na registr zastávek nad **živými daty** (KOVED #13091 bod 4): když trasa linky
/// zónu nemá, musí se vzít z `^JRZ` `$`-pole 11 - to je přesně případ, na který si klient stěžoval.
/// Očekávanou hodnotu čte test ze stejného globálu, takže nepadá na driftu zákaznických dat;
/// dokazuje zapojení (čtení pole + záskok + odstranění písmen), ne konkrétní číslo.
Method TestZaskokZonyNaRegistrZastavek()
{
	set db = "CB_GWBUS", czst = 4842
	if '##class(%SYS.Namespace).Exists(db) {
		do $$$AssertTrue(1, "namespace "_db_" není na serveru - záskok zón přeskočen")
		quit
	}
	set zonaRegistru = $piece($piece($get(^[db]JRZ(czst)),"$",11),"~",1)
	if zonaRegistru = "" {
		do $$$AssertTrue(1, "zastávka "_czst_" nemá v registru zónu - záskok nelze ověřit na těchto datech")
		quit
	}
	// J-věta bez zóny (9. #-segment prázdný) → hodnota musí přijít z registru zastávek.
	set vetaBezZony = czst_":x##:#1###0:0#:##0:0##:##0#"
	set cls = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
	set zeZaskoku = $classmethod(cls, "ZonyZastavky", db, vetaBezZony, czst, 0)
	do $$$AssertEquals(zeZaskoku, zonaRegistru, "prázdná zóna u trasy → zóna z registru: "_zonaRegistru)

	// Se zapnutým odstraněním písmen zůstane jen číslo z číselníku zón.
	set bezPismen = $classmethod(cls, "ZonyZastavky", db, vetaBezZony, czst, 1)
	set cekano = $translate(zonaRegistru, "QWERTYUIOPASDFGHJKLZXCVBNMqwertyuiopasdfghjklzxcvbnm", "")
	do $$$AssertEquals(bezPismen, cekano, "odstranění písmen: "_zonaRegistru_" → "_bezPismen)

	// Zóna u trasy má přednost před registrem (legacy priorita).
	set vetaSeZonou = czst_":x##:#1###0:0#:#101#0:0##:##0#"
	set zTrasy = $classmethod(cls, "ZonyZastavky", db, vetaSeZonou, czst, 0)
	do $$$AssertEquals(zTrasy, "101", "zóna u trasy linky přebíjí registr zastávek")
}

/// Barva linky z JŘ modulu (#12997). JŘ umí od 3. 8. víc barev na linku, čteme je podle pořadí -
/// gold linka `0365001` má nastavenou jednu (`149;202;100`), takže druhá musí vyjít prázdná, ne
/// omylem tatáž. Kdyby se pořadí ignorovalo, obě pole by nesla stejnou barvu a nikdo by si nevšiml.
Method TestBarvaLinkyDlePoradi()
{
	set cls = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
	set db = "CB_CSADAD", sku = 1, lin = "0365001"
	if '##class(%SYS.Namespace).Exists(db) {
		do $$$AssertTrue(1, "namespace "_db_" není na serveru - barva linky přeskočena")
		quit
	}
	if $get(^[db]JRL(sku, lin, "PATCH", "BARVA")) = "" {
		do $$$AssertTrue(1, "linka "_lin_" nemá nastavenou barvu - test přeskočen")
		quit
	}

	set prvni = $classmethod(cls, "BarvaLinky", "CB_CSADAD", db, sku, lin, 1)
	set druha = $classmethod(cls, "BarvaLinky", "CB_CSADAD", db, sku, lin, 2)
	do $$$AssertEquals(prvni, "#FF95CA64", "první barva linky jako hex: "_prvni)
	do $$$AssertEquals(druha, "", "druhá barva není nastavená → prázdno, ne kopie první")
	// Bez pořadí = první barva (zpětná kompatibilita volání i uložených šablon).
	do $$$AssertEquals($classmethod(cls, "BarvaLinky", "CB_CSADAD", db, sku, lin), prvni, "bez pořadí = první barva")
}

/// CIS číslo zastávky je 9. pole registru, ne interní `czst` (KOVED #13088). Na datech KOVED se ty dvě
/// hodnoty shodují (číslování přebrané z CIS), takže test hlídá **zdroj**, ne shodu s czst - jinak by
/// prošel i kdybychom omylem vraceli czst. Šestimístné číslo pro strojky (`419940`) to být nesmí.
Method TestCisloZastavkyCisJe9Pole()
{
	set cls = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
	set db = "ZL_KOVED", czst = 41994
	if '##class(%SYS.Namespace).Exists(db) {
		do $$$AssertTrue(1, "namespace "_db_" není na serveru - CIS číslo přeskočeno")
		quit
	}
	if '$data(^[db]JRZ(czst)) {
		do $$$AssertTrue(1, "zastávka "_czst_" v "_db_" chybí - CIS číslo přeskočeno")
		quit
	}

	set cis = $classmethod(cls, "CisloZastavkyCis", db, czst)
	set ocekavane = $piece($get(^[db]JRZ(czst)), "$", 9)
	set strojky = $piece($piece($get(^[db]JRZ(czst)), "$", 5), "~", 4)
	do $$$AssertEquals(cis, ocekavane, "CIS číslo = 9. pole registru: "_cis)
	do $$$AssertTrue(cis '= "", "CIS číslo není prázdné")
	do $$$AssertTrue(cis '= strojky, "CIS číslo není číslo pro strojky ("_strojky_")")
	do $$$AssertEquals($classmethod(cls, "CisloZastavkyCis", db, ""), "", "prázdný czst → prázdno")
}

/// Popis sloupku se čte z číselníku zastávky (KOVED #13088). Zlín, aut. nádr. (czst 41994) má
/// sloupky s popisy směrů - ověřuje, že bereme `~`-pole 4, ne číslo nebo GPS.
Method TestPopisSloupkuZCiselniku()
{
	set cls = "ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd"
	set db = "ZL_KOVED", czst = 41994
	if '##class(%SYS.Namespace).Exists(db) {
		do $$$AssertTrue(1, "namespace "_db_" není na serveru - popis sloupku přeskočen")
		quit
	}
	if '$data(^[db]JRZ(czst, "Sloupek", 2)) {
		do $$$AssertTrue(1, "zastávka "_czst_" nemá číselník sloupků - popis sloupku přeskočen")
		quit
	}

	set popis = $classmethod(cls, "PopisSloupku", db, czst, 2)
	set ocekavany = $piece($get(^[db]JRZ(czst, "Sloupek", 2)), "~", 4)
	do $$$AssertEquals(popis, ocekavany, "popis = 4. pole číselníku: "_popis)
	do $$$AssertTrue(popis '= "", "popis sloupku není prázdný")
	do $$$AssertTrue('$isvalidnum(popis), "popis je text, ne číslo sloupku ani GPS: "_popis)
	do $$$AssertEquals($classmethod(cls, "PopisSloupku", db, czst, ""), "", "bez sloupku → prázdný popis")
}

/// Tvar názvu zastávky se propíše do reálných dat (KOVED #13089) - end-to-end přes `GetData`,
/// ne jen čistá funkce. Běží na `ZL_KOVED` (data KOVED), protože právě tam mají zastávky název
/// s dvojčárkou a tečkovanými zkratkami; výchozí tvar musí zůstat dosavadní (zkrácený).
Method TestTvarNazvuVDatech()
{
	set db = "ZL_KOVED", sku = 1, lin = "0822995"
	if '##class(%SYS.Namespace).Exists(db) {
		do $$$AssertTrue(1, "namespace "_db_" není na serveru - tvar názvu přeskočen")
		quit
	}
	if '$data(^[db]JRL(sku, lin, "I")) {
		do $$$AssertTrue(1, "linka "_lin_" v "_db_" chybí - tvar názvu přeskočen")
		quit
	}

	set vychozi = ..PrvniNazevZastavky(db, sku, lin, "")
	set typo = ..PrvniNazevZastavky(db, sku, lin, "Typograficky upravený")
	set plny = ..PrvniNazevZastavky(db, sku, lin, "Plný CIS")

	// Bez parametru = dosavadní tvar (bez dvojčárky, bez mezer za tečkami) - na tom stojí gold.
	do $$$AssertTrue(vychozi '[ ",,", "výchozí tvar nemá dvojčárku: "_vychozi)
	// Typografická úprava přidá mezeru za čárku; plný CIS naopak vrátí dvojčárku.
	do $$$AssertTrue(typo [ ", ", "typograficky upravený má mezeru za čárkou: "_typo)
	do $$$AssertTrue(plny [ ",,", "plný CIS má dvojčárku: "_plny)
}

/// Název první zastávky ze `GetData` při daném tvaru názvu - pomocník pro test výše.
ClassMethod PrvniNazevZastavky(db As %String, sku As %String, lin As %String, tvar As %String) As %String
{
	do ..SetupSysFor(.sys, db)
	kill ^||ML
	set ^||ML("IN","SKUPINA_LINEK_ID") = sku
	set ^||ML("IN","LINKA_ID") = lin
	set:tvar'="" ^||ML("IN","TVAR_NAZVU_ZASTAVKY") = tvar
	do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).GetData(.sys)

	set page = $get(^||ML("OUT",1))
	if page = "" quit ""
	set obj = ##class(%DynamicObject).%FromJSON(page)
	// %Get, ne dot-notace - v klíči je podtržítko a to je v ObjectScriptu operátor spojení.
	set rows = obj.%Get("rows")
	if '$isobject(rows) quit ""
	set zast = rows.%Get("ZASTAVKY")
	if '$isobject(zast) quit ""
	set prvni = zast.%Get(0)
	quit $select($isobject(prvni):prvni.%Get("NAZEV_ZASTAVKY"), 1:"")
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
	set ^||ML("IN","PRVNI_BEZ_PISMENE") = "true"
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
/// Odjezdy každé vývěsky linky shodné se starým tiskem (oracle `ALVA.TISKMHD.Data.LoadData`):
/// zastávka, počet a minuty stránku po stránce. Linka má zastávky „jen výstup" i spoje končící
/// uprostřed trasy - bez kontroly obojího se lišilo 11 stránek a na konečné vznikala stránka navíc
/// (TRANSDEV #12843, 25. 9.). Hodiny se nesrovnávají, jen minuty a jejich počty.
Method TestOdjezdyVsLegacyGwbus()
{
	set db = "CB_GWBUS"
	if '##class(%SYS.Namespace).Exists(db) || '##class(%Dictionary.CompiledClass).%ExistsId("ALVA.TISKMHD.Data") {
		do $$$AssertTrue(1, "namespace "_db_" nebo legacy ALVA.TISKMHD.Data není na serveru - přeskočeno")
		quit
	}
	kill ^||ML("IN"), ^||ML("OUT")
	set ^||ML("IN","1") = "DB~"_db, ^||ML("IN","2") = "SKU~1", ^||ML("IN","3") = "LIN~0320113"
	set ^||ML("IN","4") = "NAZEV~*", ^||ML("IN","5") = "STRAN~60"
	do ..ParitaOdjezduProbe(.sys)
	set rozdily = "", k = ""
	for { set k = $order(^||ML("OUT",k)) quit:k=""  continue:k="SOUHRN"  set rozdily = rozdily_$get(^||ML("OUT",k))_"; " }
	do $$$AssertEquals($get(^||ML("OUT","SOUHRN")), "SOUHRN = hotovo", "porovnání doběhlo")
	do $$$AssertEquals(rozdily, "", "odjezdy 0320113 shodné se starým tiskem")
}

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
		if dj = "" { set ^||ML("OUT","ERR") = "stránka prázdná"  ret $$$OK }
		set dz = ##class(%DynamicObject).%FromJSON(dj).rows.%Get("ZASTAVKY"), di = dz.%GetIterator(), rn = 0, dump = ""
		while di.%GetNext(.dk, .d) {
			set rn = rn + 1
			set dump = dump_rn_":"_d.%Get("NAZEV_ZASTAVKY")_"[cas="_d.%Get("CAS_JIZDY")_" tm="_..JoinArr(d.%Get("TRASA_MIN"))_" nv="_..JoinArr(d.%Get("NASTUPISTE_VARIANT"))_"] "
		}
		set ^||ML("OUT","DUMP") = dump
		ret $$$OK
	}
	set nPages = 0, stranky = "", p = ""
	for {
		set p = $order(^||ML("OUT",p)) quit:p=""
		continue:'$isvalidnum(p)
		set json = $get(^||ML("OUT",p)) continue:json=""
		set nPages = nPages + 1
		set obj = ##class(%DynamicObject).%FromJSON(json)
		set vyveska = obj.values.%Get("POPIS_ZASTAVKA"), smer = obj.values.%Get("KONECNA_ZASTAVKA")
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

/// Diagnostika (cache_query): stránka `PAGE` legacy `LoadData` - řádky, jejichž klíč obsahuje
/// `FILTR` (prázdný = seznam klíčů). IN jako u `ZonyProbe`.
ClassMethod LegacyStrankaProbe(ByRef sys As %String) As %Status
{
	set k = ""
	for {
		set k = $order(^||ML("IN",k)) quit:k=""
		set v = $get(^||ML("IN",k))
		set inp($piece(v,"~",1)) = $piece(v,"~",2,999)
	}
	set db = $get(inp("DB")), sku = $get(inp("SKU"),1), lin = $get(inp("LIN"))
	set page = +$get(inp("PAGE"),1), filtr = $get(inp("FILTR"))
	do ..SetupSysFor(.sys, db)
	kill ^||ML("IN"), ^||ML("OUT")
	set ^||ML("IN","1") = "VOLANO_Z~JRMAIN"
	set ^||ML("IN","2") = "SESTAVA~1"
	set ^||ML("IN","3") = "VALUE~"_sku_"#"_lin_"#"_page_"#3#0-23#0##"
	do ##class(ALVA.TISKMHD.Data).LoadData(.sys)
	set i = "", n = 0
	for {
		set i = $order(^||ML("OUT",i)) quit:i=""
		set radek = $get(^||ML("OUT",i)), klic = $piece(radek,"~",1)
		set:klic="" klic = "(prazdny)"
		if filtr = "" { set klice(klic) = $get(klice(klic)) + 1  continue }
		continue:klic'[filtr
		set n = n + 1, vystup(n) = $extract(radek, 1, 400)
	}
	kill ^||ML("OUT")
	if filtr = "" {
		set klic = "", s = ""
		for { set klic = $order(klice(klic)) quit:klic=""  set s = s_klic_"×"_klice(klic)_" " }
		set ^||ML("OUT","KLICE") = s
	}
	for j=1:1:n set ^||ML("OUT","R"_j) = vystup(j)
	quit $$$OK
}

/// Text omezení u značky se shoduje se starým zastávkovým tiskem AD (`B194` T3). CB_CSADAD 0330510 má
/// na první stránce osm různých omezení, CB_GWBUS 216/0103942 spoj jen v lichých týdnech (T3a - do
/// 26. 10. ho Zastávkový JŘ AD vynechával, i když starý tisk i Linkový JŘ AD ho mají). Rok podle
/// nastavení firmy (T3c): CB_GWBUS 216/0125850 bez roku, KV_DP 0410107 celý rok. Předložky z číselníku
/// firmy (T3d): CB_CSADAD 990/0680731 „jede jen v sudých týdnech od …" (dřív zkratka „v su.") - ta je
/// až na pozdější stránce, hledá se podle textu starého tisku. Pořadí typů (T3e): CB_CSADAD 997/0370702
/// „jede od … , jede také …" (dřív „jede také" první).
Method TestTextyOmezeniProtiLegacy()
{
	set overeno = 0, nalezy = ""
	for pripad = "CB_CSADAD~1~0330510", "CB_GWBUS~216~0103942", "CB_GWBUS~216~0125850", "KV_DP~1~0410107", "CB_CSADAD~990~0680731~sudých týdnech od", "CB_CSADAD~997~0370702~12.12.20, jede také" {
		set db = $piece(pripad,"~",1), sku = $piece(pripad,"~",2), lin = $piece(pripad,"~",3), hledany = $piece(pripad,"~",4)
		set page = 1
		if hledany '= "" {
			set page = ..StrankaSTextem(.sys, db, sku, lin, hledany)
			if 'page {
				set:##class(%SYS.Namespace).Exists(db) nalezy = nalezy_" | "_db_" "_lin_": starý tisk nemá „"_hledany_""" - fixture vyhnila?"
				continue
			}
		}
		continue:'..NactiLegendy(.sys, db, sku, lin, page, .leg, .nase)
		set overeno = overeno + 1, pocet = 0, t = ""
		for {
			set t = $order(leg(t)) quit:t=""
			// typ 2 = negativní značka (omezení), ostatní řádky legendy hlídají jiné testy
			continue:$piece(leg(t),"#",1)'=2
			set pocet = pocet + 1
			continue:$data(nase(t))
			set nalezy = nalezy_" | "_db_" "_lin_" značka "_$piece(leg(t),"#",2)_": starý ["_t_"] u nás není"
		}
		if 'pocet set nalezy = nalezy_" | "_db_" "_lin_": starý tisk nemá žádné omezení - fixture vyhnila?"
	}
	if 'overeno {
		do $$$AssertTrue(1, "starý zastávkový tisk nebo firmy tu nejsou - přeskočeno")
		quit
	}
	do $$$AssertEquals(nalezy, "", "text omezení se liší od starého zastávkového tisku:"_nalezy)
}

/// První stránka, na které má starý zastávkový tisk v legendě text obsahující `hledany`; 0 = žádná
/// z prvních `maxStran` (nebo starý tisk tu není). Jen starý tisk - náš GetData se volá až nad ní.
ClassMethod StrankaSTextem(ByRef sys, db As %String, sku As %String, lin As %String, hledany As %String, maxStran As %Integer = 40) As %Integer
{
	quit:'##class(%Dictionary.CompiledClass).%ExistsId("ALVA.TISKMHD.Data") 0
	quit:'##class(%SYS.Namespace).Exists(db) 0
	quit:'$data(^[db]JRL(sku, lin, "I")) 0
	do ..SetupSysFor(.sys, db)
	set nalezena = 0
	for page = 1:1:maxStran {
		kill ^||ML("IN"), ^||ML("OUT")
		set ^||ML("IN","1") = "VOLANO_Z~JRMAIN"
		set ^||ML("IN","2") = "SESTAVA~1"
		set ^||ML("IN","3") = "VALUE~"_sku_"#"_lin_"#"_page_"#3#0-23#0##"
		do ##class(ALVA.TISKMHD.Data).LoadData(.sys)
		set i = ""
		for {
			set i = $order(^||ML("OUT",i)) quit:i=""
			set radek = $get(^||ML("OUT",i))
			continue:$piece(radek,"~",1)'="PoznamkyDataPoznamky"
			if radek [ hledany set nalezena = page  quit
		}
		quit:nalezena
	}
	kill ^||ML
	quit nalezena
}

/// Legenda stránky `page`: starý zastávkový tisk AD `leg(text) = typ#znak` a náš `GetData`
/// `nase(text) = typ#znak#kategorie` (text useknutý na 200 znaků). Stránky se párují pořadím -
/// výchozí pořadí stránek je u obou zastávka × směr. 0 = starý tisk, firma nebo linka tu není.
ClassMethod NactiLegendy(ByRef sys, db As %String, sku As %String, lin As %String, page As %Integer, ByRef leg, ByRef nase) As %Boolean
{
	kill leg, nase
	quit:'##class(%Dictionary.CompiledClass).%ExistsId("ALVA.TISKMHD.Data") 0
	quit:'##class(%SYS.Namespace).Exists(db) 0
	quit:'$data(^[db]JRL(sku, lin, "I")) 0
	do ..SetupSysFor(.sys, db)
	kill ^||ML("IN"), ^||ML("OUT")
	set ^||ML("IN","1") = "VOLANO_Z~JRMAIN"
	set ^||ML("IN","2") = "SESTAVA~1"
	set ^||ML("IN","3") = "VALUE~"_sku_"#"_lin_"#"_page_"#3#0-23#0##"
	do ##class(ALVA.TISKMHD.Data).LoadData(.sys)
	set i = ""
	for {
		set i = $order(^||ML("OUT",i)) quit:i=""
		set radek = $get(^||ML("OUT",i))
		continue:$piece(radek,"~",1)'="PoznamkyDataPoznamky"
		set text = $piece($piece(radek,"~",2,999),"#",3,999)
		continue:text=""
		set leg($extract(text,1,200)) = $piece($piece(radek,"~",2),"#",1,2)
	}
	kill ^||ML
	set ^||ML("IN","SKUPINA_LINEK_ID") = sku, ^||ML("IN","LINKA_ID") = lin
	do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).GetData(.sys)
	set json = $get(^||ML("OUT", page))
	if json '= "" {
		set ip = ##class(%DynamicObject).%FromJSON(json).rows.POZNAMKY.%GetIterator()
		while ip.%GetNext(.j, .p) {
			continue:p.%Get("TEXT")=""
			set nase($extract(p.%Get("TEXT"),1,200)) = p.%Get("TYP")_"#"_p.%Get("ZNAK")_"#"_p.%Get("KATEGORIE")
		}
	}
	kill ^||ML
	quit 1
}

/// Diagnostika (cache_query): legenda stránky `PAGE` vedle sebe (`NactiLegendy`) - `SHODA` /
/// `JEN LEG` / `JEN MY` podle textu. IN jako u `ZonyProbe`.
ClassMethod LegendaVedleSebeProbe(ByRef sys As %String) As %Status
{
	set k = ""
	for {
		set k = $order(^||ML("IN",k)) quit:k=""
		set v = $get(^||ML("IN",k))
		set inp($piece(v,"~",1)) = $piece(v,"~",2,999)
	}
	set db = $get(inp("DB")), sku = $get(inp("SKU"),1), lin = $get(inp("LIN")), page = +$get(inp("PAGE"),1)
	if '..NactiLegendy(.sys, db, sku, lin, page, .leg, .nase) {
		kill ^||ML
		set ^||ML("OUT", 1) = "SKIP: starý tisk, firma nebo linka tu není"
		quit $$$OK
	}
	set t = "", n = 0
	for {
		set t = $order(leg(t)) quit:t=""
		set n = n + 1, ^||ML("OUT", n) = $select($data(nase(t)):"SHODA", 1:"JEN LEG")_" ~ "_leg(t)_" ~ "_t
	}
	set t = ""
	for {
		set t = $order(nase(t)) quit:t=""
		continue:$data(leg(t))
		set n = n + 1, ^||ML("OUT", n) = "JEN MY ~ "_nase(t)_" ~ "_t
	}
	quit $$$OK
}

/// Diagnostika parity odjezdů (cache_query): pro zastávku `NAZEV` vypíše po stránkách počet odjezdů
/// a seřazené minuty - legacy `LoadData` (`LEG~stránka`) i náš `GetData` (`OUR~stránka~směr`).
/// IN jako u `ZonyProbe` + `NAZEV`, `STRAN` (kolik legacy stránek projít, výchozí 60).
ClassMethod ParitaOdjezduProbe(ByRef sys As %String) As %Status
{
	set k = ""
	for {
		set k = $order(^||ML("IN",k)) quit:k=""
		set v = $get(^||ML("IN",k))
		set inp($piece(v,"~",1)) = $piece(v,"~",2,999)
	}
	set db = $get(inp("DB")), sku = $get(inp("SKU"),1), lin = $get(inp("LIN"))
	set nazev = $get(inp("NAZEV")), stran = +$get(inp("STRAN"),60)
	do ..SetupSysFor(.sys, db)
	for page=1:1:stran {
		kill ^||ML("IN"), ^||ML("OUT"), minuty
		set ^||ML("IN","1") = "VOLANO_Z~JRMAIN"
		set ^||ML("IN","2") = "SESTAVA~1"
		set ^||ML("IN","3") = "VALUE~"_sku_"#"_lin_"#"_page_"#3#0-23#0##"
		do ##class(ALVA.TISKMHD.Data).LoadData(.sys)
		set i = "", idx = "", nazvy = "", pocet = 0
		for {
			set i = $order(^||ML("OUT",i)) quit:i=""
			set radek = $get(^||ML("OUT",i)), klic = $piece(radek,"~",1), hodnota = $piece(radek,"~",2,999)
			if klic = "SeznamZastavekIndexAktualniZastavky" set idx = +hodnota
			if klic = "SeznamZastavekDataSloupecNazevZastavky" set nazvy = hodnota
			continue:klic'="MinutyOdjezduDataHodiny"
			for m=1:1:$length(hodnota,"#") {
				set mm = $piece($piece(hodnota,"#",m),"^",1)
				continue:mm=""
				set pocet = pocet + 1, minuty(+mm) = $get(minuty(+mm)) + 1
			}
		}
		continue:idx=""
		set jmeno = $piece($piece(nazvy,"#",idx),"^",1)
		if nazev = "*" { set leg(jmeno_"~"_pocet_"~"_..SeznamMinut(.minuty)) = $get(leg(jmeno_"~"_pocet_"~"_..SeznamMinut(.minuty))) + 1  continue }
		continue:jmeno'=nazev
		set vysledek("LEG~"_page) = pocet_"~"_..SeznamMinut(.minuty)
	}
	kill ^||ML("IN"), ^||ML("OUT")
	set ^||ML("IN","SKUPINA_LINEK_ID") = sku
	set ^||ML("IN","LINKA_ID") = lin
	do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).GetData(.sys)
	set p = ""
	for {
		set p = $order(^||ML("OUT",p)) quit:p=""
		continue:'$isvalidnum(p)
		set obj = ##class(%DynamicObject).%FromJSON($get(^||ML("OUT",p)))
		continue:(nazev'="*")&&(obj.values.%Get("POPIS_ZASTAVKA")'=nazev)
		kill minuty
		set odj = obj.rows.%Get("ODJEZDY"), pocet = 0
		if $isobject(odj) {
			set it = odj.%GetIterator()
			while it.%GetNext(.ok, .o) { set pocet = pocet + 1, minuty(+o.%Get("MINUTA")) = $get(minuty(+o.%Get("MINUTA"))) + 1 }
		}
		if nazev = "*" {
			set kl = obj.values.%Get("POPIS_ZASTAVKA")_"~"_pocet_"~"_..SeznamMinut(.minuty)
			set our(kl) = $get(our(kl)) + 1
			continue
		}
		set vysledek("OUR~"_p_"~"_obj.values.%Get("SMER")) = pocet_"~"_..SeznamMinut(.minuty)
	}
	// Režim „*": jen stránky, které jsou v jednom výstupu a ve druhém ne (nebo jinak často).
	if nazev = "*" {
		set kl = ""
		for { set kl = $order(leg(kl)) quit:kl=""  set:$get(our(kl))'=leg(kl) vysledek("LEG-JEN~"_kl) = leg(kl)_" vs "_$get(our(kl),0) }
		for { set kl = $order(our(kl)) quit:kl=""  set:'$data(leg(kl)) vysledek("OUR-JEN~"_kl) = our(kl) }
		set vysledek("SOUHRN") = "hotovo"
	}
	kill ^||ML("OUT")
	set klic = ""
	for { set klic = $order(vysledek(klic)) quit:klic=""  set ^||ML("OUT",klic) = klic_" = "_vysledek(klic) }
	quit $$$OK
}

ClassMethod SeznamMinut(ByRef minuty) As %String
{
	set s = "", m = ""
	for { set m = $order(minuty(m)) quit:m=""  set s = s_m_"×"_minuty(m)_" " }
	quit s
}

}
