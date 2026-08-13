Include %occInclude

/// Typová sada linek zastávkového JŘ z AD dat: **každá situace, na které záleží, má svou linku**.
/// Jedna linka pokrývá jen svůj tvar dat - gold 0365001 například vůbec nevyrobí zastávku, kterou
/// vybraná trasa projíždí bez stání, takže by změny v tom stavu prošly jako inertní.
///
/// Sada je sestavená z měření (`AdSloupekProbe.ProfilyLinek` nad čtyřmi firmami, 13. 8.), ne z odhadu.
/// `TestSadaPokryvaTypy` je pojistka proti hnilobě: kdyby zákazník data upravil a linka svůj typ
/// přestala nést, test to řekne - jinak by ostatní testy dál běžely, ale nic by nekontrolovaly.
///
/// Firma, která na serveru není, se přeskočí; sada nesmí padat kvůli tomu, kde běží.
Class ALVA.TemplateDesigner.Tests.ZastavkoveJrAdSadaTest Extends ALVA.EDTesting.MLTestCase
{

/// Sada: `firma~skupina~linka~typy`. Typy jsou značky, které musí být na té lince reálně vidět
/// (kontroluje `TestSadaPokryvaTypy`).
ClassMethod Sada(ByRef sada)
{
	kill sada
	set sada($increment(sada)) = "CB_CSADAD~1~0365001~kontrolniVzorek"
	set sada($increment(sada)) = "CB_CSADAD~1~0330530~hodneSloupcu,mensitko"
	set sada($increment(sada)) = "CB_GWBUS~1~0320118~hodneSloupcu,mensitko,prazdno,viceStanovist,tydenniPlatnost,zony,poznamkyLinky,pokracovani"
	set sada($increment(sada)) = "SKNI_MHDNITRA~1~0000012~mensitko,prazdno,tydenniPlatnost,prestupy,rucniKomentare"
	set sada($increment(sada)) = "SKNI_MHDNITRA~1~0000009~kola,viceStanovist,smerove,mensitko,prazdno"
	set sada($increment(sada)) = "SKNI_MHDNITRA~1~0000006~bezVariant,mensitko"
	set sada($increment(sada)) = "SKNI_MHDNITRA~1~0000002~pokracovani,rucniRezim"
	set sada($increment(sada)) = "SKNI_MHDNITRA~1~0000014~rozdilTrasy,mensitko"
	set sada($increment(sada)) = "ZL_KOVED~1~0823995~mistenky,vyluky,poznamkyLinky,zony,mensitko,prazdno"
	set sada($increment(sada)) = "ZL_KOVED~1~0823955~mistenky,vyluky,prazdno"
}

/// Jádro sady pro **drahé** invarianty (každý průchod = GetData nad celou linkou): pět linek ze čtyř
/// firem s nejrozdílnějším tvarem - nejvíc sloupců, jednovariantní, ruční režim, zóny a místenky.
ClassMethod Jadro(ByRef jadro)
{
	kill jadro
	set jadro($increment(jadro)) = "CB_GWBUS~1~0320118"
	set jadro($increment(jadro)) = "SKNI_MHDNITRA~1~0000012"
	set jadro($increment(jadro)) = "SKNI_MHDNITRA~1~0000006"
	set jadro($increment(jadro)) = "CB_CSADAD~1~0330530"
	set jadro($increment(jadro)) = "ZL_KOVED~1~0823995"
}

/// Změří, co ve výstupu jedné linky (už načtené v `^||ML("OUT")`) reálně je. Klíče `p(...)`:
/// `stranek`, `maxSloupcu`, `sVariantami`, `mensitko`, `prazdnoCas`, `viceStanovist`, `zony`,
/// `prestupy`, `naZnameni`, `kat_<kategorie>` (anotace u minut) a `legenda_<kategorie>`.
/// Sdílené s `AdSloupekProbe.ProfilyLinek`, aby se sonda a test nerozešly.
ClassMethod ZmerProfil(ByRef p)
{
	kill p
	set p("stranek") = 0, key = ""
	for  {
		set key = $order(^||ML("OUT", key))
		quit:key=""
		continue:key="ERR"
		set page = ##class(%DynamicObject).%FromJSON(^||ML("OUT", key))
		set zast = page.rows.ZASTAVKY
		continue:'$isobject(zast)
		set p("stranek") = p("stranek") + 1
		set sloupcu = 0
		if $isobject(page.values.%Get("VARIANTY_TRAS")) set sloupcu = page.values.%Get("VARIANTY_TRAS").%Size()
		if sloupcu > $get(p("maxSloupcu")) set p("maxSloupcu") = sloupcu
		if sloupcu > 1 set p("sVariantami") = $get(p("sVariantami")) + 1
		set it = zast.%GetIterator()
		while it.%GetNext(.i, .radek) {
			set c = radek.%Get("CAS_JIZDY")
			if c = "<" { set p("mensitko") = $get(p("mensitko")) + 1 }
			elseif c = "" { set p("prazdnoCas") = $get(p("prazdnoCas")) + 1 }
			set nv = radek.%Get("NASTUPISTE_VARIANT")
			if $isobject(nv) {
				kill stan
				for ni = 0:1:nv.%Size()-1 { set v = nv.%Get(ni)  if v '= "" set stan(v) = "" }
				set pocetStan = 0, sv = ""
				for  { set sv = $order(stan(sv))  quit:sv=""  set pocetStan = pocetStan + 1 }
				if pocetStan > 1 set p("viceStanovist") = $get(p("viceStanovist")) + 1
			}
			set zn = radek.%Get("ZONY")
			if $isobject(zn), zn.%Size() > 0 set p("zony") = $get(p("zony")) + 1
			set atr = radek.%Get("ATRIBUTY")
			if $isobject(atr) {
				for ai = 0:1:atr.%Size()-1 {
					set znak = atr.%Get(ai).%Get("ZNAK")
					if (znak = "A") || (znak = "L") { set p("prestupy") = $get(p("prestupy")) + 1 }
					elseif znak = "x" { set p("naZnameni") = $get(p("naZnameni")) + 1 }
				}
			}
		}
		set odj = page.rows.ODJEZDY
		if $isobject(odj) {
			set io = odj.%GetIterator()
			while io.%GetNext(.i2, .o) {
				set an = o.%Get("ANOTACE")
				continue:'$isobject(an)
				for ai = 0:1:an.%Size()-1 {
					set kat = an.%Get(ai).%Get("KATEGORIE")
					continue:kat=""
					set p("kat_"_kat) = $get(p("kat_"_kat)) + 1
				}
			}
		}
		set pozn = page.rows.POZNAMKY
		if $isobject(pozn) {
			set ip = pozn.%GetIterator()
			while ip.%GetNext(.i3, .r) {
				set kat = r.%Get("KATEGORIE")
				continue:kat=""
				set p("legenda_"_kat) = $get(p("legenda_"_kat)) + 1
			}
		}
	}
}

/// Je typ ve změřeném profilu vidět? Jedno místo, kde se značka ze sady překládá na měření - jinak
/// by se sada a kontrola rozešly.
ClassMethod MaTyp(typ As %String, ByRef p, db As %String, sku As %String, lin As %String) As %Boolean
{
	// ⚠️ Číselné srovnání vždy přes `+$get(...)`: `=` je v ObjectScriptu řetězcové, takže nedefinovaná
	// hodnota („") se nerovná 0 a kontrola typu by mlčky vycházela naopak.
	if typ = "bezVariant"      quit +$get(p("maxSloupcu")) = 1
	if typ = "hodneSloupcu"    quit $get(p("maxSloupcu")) '< 8
	if typ = "mensitko"        quit $get(p("mensitko")) > 0
	if typ = "prazdno"         quit $get(p("prazdnoCas")) > 0
	if typ = "viceStanovist"   quit $get(p("viceStanovist")) > 0
	if typ = "zony"            quit $get(p("zony")) > 0
	if typ = "prestupy"        quit $get(p("prestupy")) > 0
	if typ = "pokracovani"     quit $get(p("kat_pokracovani")) > 0
	if typ = "kola"            quit $get(p("kat_odjezd-kola")) > 0
	if typ = "mistenky"        quit $get(p("kat_odjezd-mistenka")) > 0
	if typ = "rozdilTrasy"     quit $get(p("kat_rozdil-trasy")) > 0
	if typ = "tydenniPlatnost" quit $get(p("kat_tydenni-platnost")) > 0
	if typ = "rucniKomentare"  quit $get(p("kat_rucni-komentar")) > 0
	if typ = "vyluky"          quit $get(p("legenda_vyluka")) > 0
	if typ = "smerove"         quit $get(p("legenda_smerove")) > 0
	if typ = "poznamkyLinky"   quit ($get(p("legenda_poznamka-linky-tarifni")) + $get(p("legenda_poznamka-linky-vseobecne"))) > 0
	// Kontrolní vzorek: linka, kde ani jeden z nových stavů Času nevzniká (gold). Drží ho tam
	// záměrně - kdyby začal vznikat, digest se hne a je fér vědět proč.
	if typ = "kontrolniVzorek" quit (+$get(p("mensitko")) = 0) && (+$get(p("prazdnoCas")) = 0)
	// Ruční režim je nastavení u linky, ne obsah výstupu.
	if typ = "rucniRezim"      quit '+$get(^[db]JRL(sku,lin,"PATCH","mhd","par",1))
	quit 0
}

/// Načte jednu linku (GetData) s volitelnými parametry sestavy; 0 = firma nebo linka na serveru není.
/// `parametry` je pole `parametry(<klíč IN>) = <hodnota>`.
ClassMethod NactiLinku(ByRef sys, db As %String, sku As %String, lin As %String, ByRef parametry) As %Boolean
{
	if '##class(%SYS.Namespace).Exists(db) quit 0
	if '$data(^[db]JRL(sku, lin, "I")) quit 0
	set sys("U","DATA") = db
	kill ^||ML
	set ^||ML("IN","SKUPINA_LINEK_ID") = sku
	set ^||ML("IN","LINKA_ID") = lin
	set k = ""
	for  {
		set k = $order(parametry(k))
		quit:k=""
		set ^||ML("IN", k) = parametry(k)
	}
	do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).GetData(.sys)
	quit 1
}

/// Sada musí pokrývat typy, které tvrdí. Pojistka proti tichému vyhnití fixture: kdyby linka svůj
/// typ přestala nést (zákazník upravil jízdní řád), ostatní testy by běžely dál a nic nekontrolovaly.
Method TestSadaPokryvaTypy()
{
	do ..SetupSys(.sys)
	do ..Sada(.sada)
	kill parametry
	set chybi = "", overeno = 0, prehled = "", i = ""
	for  {
		set i = $order(sada(i))
		quit:i=""
		set db = $piece(sada(i),"~",1), sku = $piece(sada(i),"~",2), lin = $piece(sada(i),"~",3), typy = $piece(sada(i),"~",4)
		continue:'..NactiLinku(.sys, db, sku, lin, .parametry)
		set overeno = overeno + 1
		do ..ZmerProfil(.p)
		set prehled = prehled _ db _ " " _ lin _ " (stranek " _ $get(p("stranek")) _ ", sloupcu " _ $get(p("maxSloupcu")) _ "); "
		for ti = 1:1:$length(typy, ",") {
			set typ = $piece(typy, ",", ti)
			continue:typ=""
			continue:..MaTyp(typ, .p, db, sku, lin)
			set chybi = chybi _ $select(chybi="":"", 1:", ") _ db _ " " _ lin _ ": " _ typ
		}
	}
	if 'overeno {
		do $$$AssertTrue(1, "žádná firma z testovací sady není na tomto serveru - přeskočeno")
		quit
	}
	do $$$AssertEquals(chybi, "", "sada už tyhle typy nepokrývá: "_chybi)
	do $$$AssertNotEquals(prehled, "", "pokryto: "_prehled)
}

/// Variantní pole jedné zastávky musí být **stejně dlouhá jako hlavičky sloupců** - časy, stanoviště
/// i sloupky se čtou po indexech, takže o jeden kratší pole znamená, že se čas přiřadí cizí variantě
/// (a v tisku to vypadá jako platný údaj). Klient si proti tomu pomáhá minimem délek, ale to je záplata,
/// ne kontrakt.
Method TestZarovnaniVariantnichPoli()
{
	do ..SetupSys(.sys)
	do ..Jadro(.jadro)
	kill parametry
	set rozjezdu = 0, kde = "", overeno = 0, radku = 0, i = ""
	for  {
		set i = $order(jadro(i))
		quit:i=""
		set db = $piece(jadro(i),"~",1), sku = $piece(jadro(i),"~",2), lin = $piece(jadro(i),"~",3)
		continue:'..NactiLinku(.sys, db, sku, lin, .parametry)
		set overeno = overeno + 1
		set key = ""
		for  {
			set key = $order(^||ML("OUT", key))
			quit:key=""
			continue:key="ERR"
			set page = ##class(%DynamicObject).%FromJSON(^||ML("OUT", key))
			set zast = page.rows.ZASTAVKY
			continue:'$isobject(zast)
			set hlavicek = 0
			if $isobject(page.values.%Get("VARIANTY_TRAS")) set hlavicek = page.values.%Get("VARIANTY_TRAS").%Size()
			set it = zast.%GetIterator()
			while it.%GetNext(.j, .radek) {
				set radku = radku + 1
				for pole = "TRASA_MIN", "NASTUPISTE_VARIANT", "SLOUPEK_VARIANT" {
					set arr = radek.%Get(pole)
					set delka = $select($isobject(arr):arr.%Size(), 1:0)
					continue:delka=hlavicek
					set rozjezdu = rozjezdu + 1
					if kde = "" set kde = db_" "_lin_" "_radek.%Get("NAZEV_ZASTAVKY")_": "_pole_" má "_delka_", hlaviček "_hlavicek
				}
			}
		}
	}
	if 'overeno {
		do $$$AssertTrue(1, "žádná firma z jádra sady není na tomto serveru - přeskočeno")
		quit
	}
	do $$$AssertEquals(radku > 0, 1, "jádro sady nedalo žádnou zastávku - rozbitý fixture?")
	do $$$AssertEquals(rozjezdu, 0, "variantní pole nesedí s počtem hlaviček ("_rozjezdu_" z "_radku_" řádků), první: "_kde)
}

/// V režimu **písmen variant** musí každá značka u minuty odkazovat na sloupec, který na vývěsce
/// existuje - jinak si cestující přečte písmeno, ke kterému není co dohledat. Zároveň se přepnutím
/// režimu nesmí změnit **počet stránek ani odjezdů**: je to volba zobrazení, ne filtr dat.
Method TestPismenaVariantOdkazujiNaExistujiciSloupec()
{
	do ..SetupSys(.sys)
	do ..Jadro(.jadro)
	set sirotku = 0, kde = "", znacek = 0, overeno = 0, rozdilPoctu = "", i = ""
	for  {
		set i = $order(jadro(i))
		quit:i=""
		set db = $piece(jadro(i),"~",1), sku = $piece(jadro(i),"~",2), lin = $piece(jadro(i),"~",3)
		// nejdřív výchozí režim - počty stránek a odjezdů pro porovnání
		kill parametry
		continue:'..NactiLinku(.sys, db, sku, lin, .parametry)
		set overeno = overeno + 1
		do ..ZmerProfil(.pVychozi)
		set odjezduVychozi = ..PocetOdjezdu()
		// pak režim písmen
		kill parametry
		set parametry("PISMENA_VARIANT") = "true"
		do ..NactiLinku(.sys, db, sku, lin, .parametry)
		do ..ZmerProfil(.pPismena)
		set odjezduPismena = ..PocetOdjezdu()
		if ($get(pVychozi("stranek")) '= $get(pPismena("stranek"))) || (odjezduVychozi '= odjezduPismena) {
			if rozdilPoctu = "" set rozdilPoctu = db_" "_lin_": stránek "_$get(pVychozi("stranek"))_" → "_$get(pPismena("stranek"))_", odjezdů "_odjezduVychozi_" → "_odjezduPismena
		}
		set key = ""
		for  {
			set key = $order(^||ML("OUT", key))
			quit:key=""
			continue:key="ERR"
			set page = ##class(%DynamicObject).%FromJSON(^||ML("OUT", key))
			set odj = page.rows.ODJEZDY
			continue:'$isobject(odj)
			kill hlavicky
			set hl = page.values.%Get("VARIANTY_TRAS")
			if $isobject(hl) {
				for hi = 0:1:hl.%Size()-1 { set h = hl.%Get(hi)  if h '= "" set hlavicky(h) = "" }
			}
			set io = odj.%GetIterator()
			while io.%GetNext(.j, .o) {
				set an = o.%Get("ANOTACE")
				continue:'$isobject(an)
				for ai = 0:1:an.%Size()-1 {
					continue:an.%Get(ai).%Get("KATEGORIE")'="varianta-trasy"
					set znak = an.%Get(ai).%Get("ZNAK")
					continue:znak=""
					set znacek = znacek + 1
					continue:$data(hlavicky(znak))
					set sirotku = sirotku + 1
					if kde = "" set kde = db_" "_lin_", stránka "_key_" ("_page.values.%Get("POPIS_ZASTAVKA")_"), značka "_znak
				}
			}
		}
	}
	if 'overeno {
		do $$$AssertTrue(1, "žádná firma z jádra sady není na tomto serveru - přeskočeno")
		quit
	}
	do $$$AssertEquals(znacek > 0, 1, "režim písmen nedal ani jednu značku u minut - přestal fungovat parametr?")
	do $$$AssertEquals(sirotku, 0, "značka u minuty odkazuje na neexistující sloupec ("_sirotku_" z "_znacek_"), první: "_kde)
	do $$$AssertEquals(rozdilPoctu, "", "přepnutí režimu písmen změnilo počty, to je volba zobrazení: "_rozdilPoctu)
}

/// Značka varianty a rozdílová poznámka jsou dvě nezávislé věci a musí běžet vedle sebe: značka
/// řekne „kterým sloupcem", poznámka „kudy". Do 13. 8. režim písmen poznámky vypínal, takže si
/// uživatel musel vybrat - a sloupce bez písmene („T1") neměly ani jedno. Tak to má i starý modul,
/// který u minuty skládá obojí zvlášť (`JRapiLinPrtMhd1.mac:529`, značka sloupce `:297`).
Method TestPismenaVariantNevypinajiRozdilovePoznamky()
{
	do ..SetupSys(.sys)
	// Linka vybraná pro typ „rozdilTrasy" - jinde by test prošel prázdnem.
	set db = "SKNI_MHDNITRA", sku = 1, lin = "0000014"
	kill parametry
	set parametry("PISMENA_VARIANT") = "true"
	if '..NactiLinku(.sys, db, sku, lin, .parametry) {
		do $$$AssertTrue(1, "firma "_db_" není na tomto serveru - přeskočeno")
		quit
	}
	do ..ZmerProfil(.p)

	do $$$AssertEquals($get(p("kat_varianta-trasy")) > 0, 1, "režim písmen nedal ani jednu značku varianty u minut")
	do $$$AssertEquals($get(p("kat_rozdil-trasy")) > 0, 1, "v režimu písmen zmizely rozdílové poznámky u minut - vylučují se zase navzájem?")
	do $$$AssertEquals($get(p("legenda_rozdil-trasy")) > 0, 1, "v režimu písmen zmizelo vysvětlení rozdílu trasy z legendy")
}

/// Řazení variant je volba ZOBRAZENÍ, ne filtr: přeskládá sloupce, ale nesmí ubrat ani přidat
/// stránku, odjezd nebo sloupec. „Dle délky jízdy" navíc musí sloupce opravdu seřadit - kdyby se
/// jen tvářilo, že řadí, nikdo by to nepoznal (pořadí variant je na první pohled nahodilé).
Method TestRazeniVariantDleDelkyJizdy()
{
	do ..SetupSys(.sys)
	set db = "SKNI_MHDNITRA", sku = 1, lin = "0000012"
	kill parametry
	if '..NactiLinku(.sys, db, sku, lin, .parametry) {
		do $$$AssertTrue(1, "firma "_db_" není na tomto serveru - přeskočeno")
		quit
	}
	do ..ZmerProfil(.pVychozi)
	set odjezduVychozi = ..PocetOdjezdu()

	kill parametry
	set parametry("RAZENI_VARIANT") = "Dle délky jízdy (nejkratší první)"
	do ..NactiLinku(.sys, db, sku, lin, .parametry)
	do ..ZmerProfil(.pDelka)
	set odjezduDelka = ..PocetOdjezdu()

	do $$$AssertEquals($get(pVychozi("stranek")), $get(pDelka("stranek")), "řazení variant změnilo počet stránek - je to volba zobrazení, ne filtr")
	do $$$AssertEquals(odjezduVychozi, odjezduDelka, "řazení variant změnilo počet odjezdů")
	do $$$AssertEquals($get(pVychozi("maxSloupcu")), $get(pDelka("maxSloupcu")), "řazení variant změnilo počet sloupců")

	// Sloupce musí jít od nejkratší jízdy k nejdelší. Poslední čas ve sloupci je jeho dojezd,
	// takže stačí porovnat poslední vyplněnou hodnotu sousedních sloupců.
	set spatne = "", stranekSVariantami = 0, key = ""
	for  {
		set key = $order(^||ML("OUT", key)) quit:key=""
		continue:key="ERR"
		set page = ##class(%DynamicObject).%FromJSON(^||ML("OUT", key))
		set zast = page.rows.ZASTAVKY
		continue:'$isobject(zast)
		set sloupcu = 0
		if $isobject(page.values.%Get("VARIANTY_TRAS")) set sloupcu = page.values.%Get("VARIANTY_TRAS").%Size()
		continue:sloupcu<2
		set stranekSVariantami = stranekSVariantami + 1
		kill dojezd
		set it = zast.%GetIterator()
		while it.%GetNext(.i, .radek) {
			set tm = radek.%Get("TRASA_MIN")
			continue:'$isobject(tm)
			for ti = 0:1:tm.%Size()-1 {
				set v = tm.%Get(ti)
				continue:'$isvalidnum(v)
				set dojezd(ti) = v
			}
		}
		// ⚠️ Za podmínkou příkazu nesmí být mezery - `dojezd(ti - 1)` ukončí argument u první mezery
		// a třída se nepřeloží (chyba „Missing right parenthesis").
		for ti = 1:1:sloupcu-1 {
			set predchozi = ti-1
			continue:'$data(dojezd(ti))
			continue:'$data(dojezd(predchozi))
			continue:dojezd(ti)'<dojezd(predchozi)
			if spatne = "" set spatne = "stránka "_key_" ("_page.values.%Get("POPIS_ZASTAVKA")_"): sloupec "_(ti+1)_" dojíždí za "_dojezd(ti)_", předchozí až za "_dojezd(predchozi)
		}
	}
	do $$$AssertEquals(stranekSVariantami > 0, 1, "linka nemá ani jednu stránku s víc variantami - test by neověřil nic")
	do $$$AssertEquals(spatne, "", "sloupce nejsou seřazené od nejkratší jízdy: "_spatne)

	// Obrácená volba musí dát obrácené pořadí, ne totéž - jinak by se lišila jen popiskem.
	kill parametry
	set parametry("RAZENI_VARIANT") = "Dle délky jízdy (nejdelší první)"
	do ..NactiLinku(.sys, db, sku, lin, .parametry)
	set spatneOpacne = "", key = ""
	for  {
		set key = $order(^||ML("OUT", key)) quit:key=""
		continue:key="ERR"
		set page = ##class(%DynamicObject).%FromJSON(^||ML("OUT", key))
		set zast = page.rows.ZASTAVKY
		continue:'$isobject(zast)
		set sloupcu = 0
		if $isobject(page.values.%Get("VARIANTY_TRAS")) set sloupcu = page.values.%Get("VARIANTY_TRAS").%Size()
		continue:sloupcu<2
		kill dojezd
		set it = zast.%GetIterator()
		while it.%GetNext(.i, .radek) {
			set tm = radek.%Get("TRASA_MIN")
			continue:'$isobject(tm)
			for ti = 0:1:tm.%Size()-1 {
				set v = tm.%Get(ti)
				continue:'$isvalidnum(v)
				set dojezd(ti) = v
			}
		}
		for ti = 1:1:sloupcu-1 {
			set predchozi = ti-1
			continue:'$data(dojezd(ti))
			continue:'$data(dojezd(predchozi))
			continue:dojezd(ti)'>dojezd(predchozi)
			if spatneOpacne = "" set spatneOpacne = "stránka "_key_" ("_page.values.%Get("POPIS_ZASTAVKA")_"): sloupec "_(ti+1)_" dojíždí za "_dojezd(ti)_", předchozí už za "_dojezd(predchozi)
		}
	}
	do $$$AssertEquals(spatneOpacne, "", "obrácená volba neseřadila od nejdelší jízdy: "_spatneOpacne)
}

/// Počet odjezdů ve všech stránkách právě načteného výstupu.
ClassMethod PocetOdjezdu() As %Integer
{
	set pocet = 0, key = ""
	for  {
		set key = $order(^||ML("OUT", key))
		quit:key=""
		continue:key="ERR"
		set page = ##class(%DynamicObject).%FromJSON(^||ML("OUT", key))
		set odj = page.rows.ODJEZDY
		continue:'$isobject(odj)
		set pocet = pocet + odj.%Size()
	}
	quit pocet
}

}
