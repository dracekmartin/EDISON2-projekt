/// Obousměrný tisk linkového JŘ (`L6b`) - **datová** část.
///
/// Se zapnutým parametrem „Obousměrný tisk" vrací agenda **jednu** stránku se spoji obou směrů nad
/// společným seznamem zastávek; každý spoj nese svůj `SMER`. O tom, který směr se kreslí vlevo,
/// rozhoduje prvek - server posílá jen fakta (požadavek Jan 28. 8.: layout drží klient v C#, ať
/// nedopadneme jako starý modul, kde konkrétní rozvržení počítá Caché).
///
/// Legacy má na totéž `in("A",2,"arr",7)` (`JRapiLinPrtWin.mac:598`) a slévá oba směry taky na
/// serveru (krok `MT2A`), takže tvar dat je s ním zajedno. **Tichý fallback legacy vědomě nemáme** -
/// starý modul zrcadlo vypne, když se blok nevejde na stránku bez řezu (`:627`), protože tiskl
/// naslepo do pevné stránky; u nás autor šablony vidí náhled.
Class ALVA.TemplateDesigner.Tests.LinkovyJrObousmernyTest Extends ALVA.EDTesting.MLTestCase
{

/// Linka jádra sady, na které jsou spoje v obou směrech.
Parameter TESTDB = "CB_CSADAD";

Parameter TESTSKU = 1;

Parameter TESTLIN = "0330511";

/// Vypnutý parametr = dnešní chování: stránka per směr.
Method TestVypnuty()
{
	if '..NactiLinku(0, .pocetStranek, .smery, .zastavek) quit
	do $$$AssertEquals(pocetStranek, 2, "bez parametru mají být dvě stránky (tam, zpět), je "_pocetStranek)
}

/// Zapnutý parametr = jedna stránka, na ní spoje obou směrů.
Method TestZapnutyDavaJednuStranku()
{
	if '..NactiLinku(1, .pocetStranek, .smery, .zastavek) quit
	do $$$AssertEquals(pocetStranek, 1, "s obousměrným tiskem má být jedna stránka, je "_pocetStranek)
	do $$$AssertTrue($get(smery("T")) > 0, "na sloučené stránce chybí spoje směru tam")
	do $$$AssertTrue($get(smery("Z")) > 0, "na sloučené stránce chybí spoje směru zpět")
}

/// Sloučením se nesmí ztratit ani jeden spoj - součet obou směrů musí být týž jako po stránkách.
Method TestSlouceniNeztratiSpoje()
{
	if '..NactiLinku(0, .stranekVyp, .smeryVyp, .zastavekVyp) quit
	if '..NactiLinku(1, .stranekZap, .smeryZap, .zastavekZap) quit
	set vypnuto = $get(smeryVyp("T")) + $get(smeryVyp("Z"))
	set zapnuto = $get(smeryZap("T")) + $get(smeryZap("Z"))
	do $$$AssertEquals(zapnuto, vypnuto, "sloučená stránka má "_zapnuto_" spojů, po směrech jich je "_vypnuto)
}

/// Časy zpětných spojů musí sedět na **společný** seznam zastávek - tedy mít stejnou délku jako on.
/// Kdyby se zarovnávaly podle pozice ve svém směru, vyšly by proti řádkům obráceně nebo nakřivo.
Method TestCasyZpetnychSpojuSedNaSpolecneRadky()
{
	if '..NactiLinku(1, .pocetStranek, .smery, .zastavek, .smerStranky, .delkaCasu) quit
	// ⚠️ Bez téhle pojistky test nic nedokazuje: po směrech má každá stránka svých 31 zastávek
	// i svoje časy, takže délky sedí i tehdy, když se vůbec neslévá (odhalila mutace).
	do $$$AssertEquals(pocetStranek, 1, "stránky se neslily, délky časů by se měřily po směrech")
	do $$$AssertTrue($get(delkaCasu("Z")) '= 0, "na stránce nejsou zpětné spoje, test by nic neověřil")
	do $$$AssertEquals($get(delkaCasu("Z")), zastavek, "časy zpětného spoje mají "_$get(delkaCasu("Z"))_" hodnot, zastávek je "_zastavek)
	do $$$AssertEquals($get(delkaCasu("T")), zastavek, "časy spoje tam mají "_$get(delkaCasu("T"))_" hodnot, zastávek je "_zastavek)
}

/// U sloučené stránky nesmí `SMER` u stránky tvrdit jeden ze směrů - stránka nese oba.
Method TestSmerStrankyJePrazdny()
{
	if '..NactiLinku(1, .pocetStranek, .smery, .zastavek, .smerStranky) quit
	do $$$AssertEquals(smerStranky, "", "sloučená stránka hlásí SMER=" _ smerStranky _ ", má být prázdný")
}

/// Km sloupce má **každý směr vlastní** a v obou počtech; sloučená stránka je proto musí nést
/// obojí, jinak se km opačného směru ztratí. Legacy je počítá zvlášť (`Nkm2T`/`Nkm2Z`, šířky
/// `KmcharT`/`KmcharZ`, `JRapiLinPrtPS2.mac:79-93,189`) a tiskne do prostředního bloku.
Method TestKmObouSmeruJsouNaSlouceneStrance()
{
	set db = ..#TESTDB, sku = ..#TESTSKU, lin = ..#TESTLIN
	if '##class(%SYS.Namespace).Exists(db) {
		do $$$AssertTrue(1, "firma "_db_" není na tomto serveru - test přeskočen")
		quit
	}
	set kmT = +$get(^[db]JRL(sku, lin, "PATCH", "KmsSmer", "T"))
	set kmZ = +$get(^[db]JRL(sku, lin, "PATCH", "KmsSmer", "Z"))
	if (kmT = 0) || (kmZ = 0) {
		do $$$AssertTrue(1, "linka nemá km sloupce v obou směrech - test přeskočen")
		quit
	}

	if '..DelkaKm(0, .jenTam) quit
	if '..DelkaKm(1, .slouceno) quit

	set hlaskaT = "jednosměrná stránka má " _ jenTam _ " km sloupců, linka jich má " _ kmT
	set hlaskaOba = "sloučená stránka má " _ slouceno _ " km sloupců, oba směry dohromady " _ (kmT + kmZ)
	do $$$AssertEquals(jenTam, kmT, hlaskaT)
	do $$$AssertEquals(slouceno, kmT + kmZ, hlaskaOba)
}

/// Na sloučené stránce jsou zastávky v pořadí hlavního směru, takže **všechny** km sloupce musí
/// shora dolů narůstat - i ty opačného směru. Odečtení od maxima (správné na samostatné stránce
/// směru "zpět", kde jsou zastávky obráceně) tady vyrábělo sloupce klesající odshora dolů, což ve
/// starém tisku není (nález Jan 7. 9.).
Method TestKmOpacnehoSmeruNarustajiShoraDolu()
{
	set db = ..#TESTDB
	if '##class(%SYS.Namespace).Exists(db) {
		do $$$AssertTrue(1, "firma "_db_" není na tomto serveru - test přeskočen")
		quit
	}
	set kmZ = +$get(^[db]JRL(..#TESTSKU, ..#TESTLIN, "PATCH", "KmsSmer", "Z"))
	if kmZ = 0 {
		do $$$AssertTrue(1, "linka nemá km sloupce směru zpět - test přeskočen")
		quit
	}

	do ..SetupSys(.sys)
	kill parametry
	set parametry("OBOUSMERNY_TISK") = "true"
	do ##class(ALVA.TemplateDesigner.Tests.LinkovyJrSadaTest).NactiLinku(.sys, db, ..#TESTSKU, ..#TESTLIN, .parametry)
	set page = ##class(%DynamicObject).%FromJSON(^||ML("OUT", 1))
	set zastavky = page.rows.ZASTAVKY

	// Poslední sloupec pole KM patří poslednímu sloupci opačného směru.
	set posledni = zastavky.%Get(0).KM.%Size() - 1
	set prvniHodnota = "", posledniHodnota = "", klesa = 0
	for i = 0:1:zastavky.%Size() - 1 {
		set v = zastavky.%Get(i).KM.%Get(posledni)
		continue:'$isvalidnum(v)
		if prvniHodnota = "" set prvniHodnota = v
		if (posledniHodnota '= "") && (v < posledniHodnota) set klesa = 1
		set posledniHodnota = v
	}

	set hlaska = "km opačného směru klesají odshora dolů (od " _ prvniHodnota _ " k " _ posledniHodnota _ ")"
	do $$$AssertNotTrue(klesa, hlaska)
}

/// Délka pole KM první zastávky první stránky. Vrací 0, když linka na serveru není.
Method DelkaKm(obousmerne As %Boolean, ByRef delka) As %Boolean
{
	do ..SetupSys(.sys)
	kill parametry
	set parametry("OBOUSMERNY_TISK") = $select(obousmerne:"true", 1:"false")
	do ##class(ALVA.TemplateDesigner.Tests.LinkovyJrSadaTest).NactiLinku(.sys, ..#TESTDB, ..#TESTSKU, ..#TESTLIN, .parametry)

	set page = ##class(%DynamicObject).%FromJSON(^||ML("OUT", 1))
	set delka = page.rows.ZASTAVKY.%Get(0).KM.%Size()
	quit 1
}

/// Zavolá GetData a rozebere výsledek. Vrací 0, když linka na tomhle serveru není (skip).
/// ByRef: počet stránek, počty spojů po směrech, počet zastávek 1. stránky, SMER 1. stránky;
/// `delkaCasu(smer)` = délka pole CASY posledního spoje toho směru.
Method NactiLinku(obousmerne As %Boolean, ByRef pocetStranek, ByRef smery, ByRef zastavek, ByRef smerStranky, ByRef delkaCasu) As %Boolean
{
	set db = ..#TESTDB, sku = ..#TESTSKU, lin = ..#TESTLIN
	if '##class(%SYS.Namespace).Exists(db) {
		do $$$AssertTrue(1, "firma "_db_" není na tomto serveru - test přeskočen")
		quit 0
	}
	if '$data(^[db]JRL(sku, lin, "I")) {
		do $$$AssertTrue(0, "testovací linka "_lin_" chybí v "_db_" - rozbitý fixture?")
		quit 0
	}

	do ..SetupSys(.sys)
	kill parametry
	set parametry("OBOUSMERNY_TISK") = $select(obousmerne:"true", 1:"false")
	do ##class(ALVA.TemplateDesigner.Tests.LinkovyJrSadaTest).NactiLinku(.sys, db, sku, lin, .parametry)

	kill smery, delkaCasu
	set pocetStranek = 0, zastavek = 0, smerStranky = ""
	set idx = ""
	for  {
		set idx = $order(^||ML("OUT", idx))
		quit:idx=""
		continue:idx="ERR"
		continue:'$isvalidnum(idx)
		set pocetStranek = pocetStranek + 1
		set page = ##class(%DynamicObject).%FromJSON(^||ML("OUT", idx))
		if pocetStranek = 1 {
			set zastavek = page.rows.ZASTAVKY.%Size()
			set smerStranky = page.values.%Get("SMER")
		}
		set it = page.rows.SPOJE.%GetIterator()
		while it.%GetNext(.i, .spoj) {
			set sm = spoj.%Get("SMER")
			set smery(sm) = $get(smery(sm)) + 1
			set delkaCasu(sm) = spoj.CASY.%Size()
		}
	}
	quit 1
}

}
