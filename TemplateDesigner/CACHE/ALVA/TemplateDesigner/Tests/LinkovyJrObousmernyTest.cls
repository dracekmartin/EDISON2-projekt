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

/// Volba se nabízí jen při obou směrech: s jedním ji GetData ignoruje a v panelu by klik nic neudělal.
/// Podmínka musí trefit doslovný text volby „Směru pro tisk", jinak by se schovala natrvalo.
Method TestNabiziSeJenPriObouSmerech()
{
	do ..SetupSys(.sys)
	kill ^||ML
	do ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).GetParameters(.sys)
	set it = ##class(%DynamicArray).%FromJSON(^||ML("OUT",1)).%GetIterator()
	set podminka = "(chybí)", volby = "", popisSmeru = ""
	while it.%GetNext(.k, .p) {
		set:p.name="OBOUSMERNY_TISK" podminka = p.visibleWhen
		if p.name = "SMER_TISKU" {
			set popisSmeru = p.description
			set io = p.options.%GetIterator()
			while io.%GetNext(.ko, .o) { set volby = volby_"|"_o }
		}
	}
	do $$$AssertEquals(podminka, "SMER_TISKU=Oba", "obousměrný tisk se má nabízet jen při obou směrech")
	do $$$AssertTrue(volby [ "|Oba", "Směr pro tisk nemá volbu „Oba"", na kterou podmínka míří: "_volby)
	do $$$AssertTrue(popisSmeru '[ "obousměrný", "popis Směru pro tisk už o obousměrném tisku mluvit nemá: "_popisSmeru)
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

/// Km sloupce: obousměrná stránka bere **společný** seznam, ne oba směrové slepené za sebe.
///
/// Starý tisk to tak dělá výslovně (`JRapiLinPrtWin.mac:2523`, větev `sm="A"`) a společný seznam je
/// odduplikovaná sjednocenina obou směrů - profil obsažený v jiném do něj nejde. Do 7. 9. jsme
/// slepovali oba směrové, takže vznikaly sloupce nesoucí totéž.
///
/// Čísla jsou **změřená** na `0330511` sondou `LinkovyJrSpojeProbe.KmSloupce`: společný 8,
/// směr tam 5, směr zpět 6. Kdyby se linka v datech změnila, test to řekne rozdílem proti globálu,
/// ne pádem na konstantě.
Method TestKmSloupceObousmerneBerouSpolecnySeznam()
{
	do ..SetupSys(.sys)
	set db = ..#TESTDB, sku = ..#TESTSKU, lin = ..#TESTLIN
	if '##class(%SYS.Namespace).Exists(db) do $$$AssertSkipped("firma "_db_" tu není - přeskočeno") quit
	if '$data(^[db]JRL(sku, lin, "I")) do $$$AssertSkipped("linka "_lin_" tu není - přeskočeno") quit

	set spolecnych = +$get(^[db]JRL(sku, lin, "K"))
	set smerovychT = +$get(^[db]JRL(sku, lin, "PATCH", "KmsSmer", "T"))
	set smerovychZ = +$get(^[db]JRL(sku, lin, "PATCH", "KmsSmer", "Z"))
	// ⚠️ Makro assertu musí být na jednom řádku, proto se hlášky skládají dopředu.
	set hlaskaFixtura = "fixtura nic neověří: společný seznam ("_spolecnych_") musí být kratší než oba směrové dohromady ("_smerovychT_"+"_smerovychZ_"), jinak je slepení a sjednocenina totéž"
	do $$$AssertTrue(spolecnych < (smerovychT + smerovychZ), hlaskaFixtura)

	if '..PocetKmVeSloupci(1, .kmObou) quit
	set hlaskaObou = "obousměrná stránka má mít "_spolecnych_" km sloupců (společný seznam), má "_kmObou
	do $$$AssertEquals(kmObou, spolecnych, hlaskaObou)

	if '..PocetKmVeSloupci(0, .kmJednim) quit
	set hlaskaJednim = "jednosměrná stránka má mít "_smerovychT_" km sloupců (směrový seznam), má "_kmJednim
	do $$$AssertEquals(kmJednim, smerovychT, hlaskaJednim)
}

/// Kolik km hodnot nese první zastávka stránky - tedy kolik km sloupců stránka má.
ClassMethod PocetKmVeSloupci(obousmerne As %Boolean, ByRef pocet) As %Boolean
{
	do ..SetupSys(.sys)
	kill parametry
	set parametry("OBOUSMERNY_TISK") = $select(obousmerne:"true", 1:"false")
	if '##class(ALVA.TemplateDesigner.Tests.LinkovyJrSadaTest).NactiLinku(.sys, ..#TESTDB, ..#TESTSKU, ..#TESTLIN, .parametry) quit 0
	set page = ##class(%DynamicObject).%FromJSON($get(^||ML("OUT", 1)))
	if '$isobject(page) quit 0
	set zast = page.rows.ZASTAVKY.%Get(0)
	set pocet = $select($isobject(zast) && $isobject(zast.KM): zast.KM.%Size(), 1: -1)
	quit 1
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

/// Sloučená stránka musí nést km **obou** směrů, ne jen hlavního - jinak se km opačného směru
/// ztratí (tak to bylo do 7. 9.).
///
/// ⚠️ **Netvrdí se součet obou směrových seznamů.** První verze tohohle testu to tvrdila, protože
/// vznikla dřív, než se ukázalo, že legacy má pro obousměrný tisk **společný** seznam - a ten je
/// odduplikovaná sjednocenina, ne součet (`0330511`: 8 proti 5+6). Přesný počet ověřuje
/// `TestKmSloupceObousmerneBerouSpolecnySeznam`; tady jde jen o to, že opačný směr nezmizel,
/// a to platí bez ohledu na to, kolik profilů je v datech duplicitních.
Method TestKmObouSmeruJsouNaSlouceneStrance()
{
	set db = ..#TESTDB, sku = ..#TESTSKU, lin = ..#TESTLIN
	if '##class(%SYS.Namespace).Exists(db) {
		do $$$AssertSkipped("firma "_db_" není na tomto serveru - test přeskočen")
		quit
	}
	set kmT = +$get(^[db]JRL(sku, lin, "PATCH", "KmsSmer", "T"))
	set kmZ = +$get(^[db]JRL(sku, lin, "PATCH", "KmsSmer", "Z"))
	if (kmT = 0) || (kmZ = 0) {
		do $$$AssertSkipped("linka nemá km sloupce v obou směrech - test přeskočen")
		quit
	}

	if '..DelkaKm(0, .jenTam) quit
	if '..DelkaKm(1, .slouceno) quit

	set hlaskaT = "jednosměrná stránka má " _ jenTam _ " km sloupců, linka jich má " _ kmT
	set hlaskaOba = "sloučená stránka má " _ slouceno _ " km sloupců, jen hlavní směr jich má " _ kmT
	do $$$AssertEquals(jenTam, kmT, hlaskaT)
	do $$$AssertTrue(slouceno > kmT, hlaskaOba)
}

/// Na sloučené stránce jsou zastávky v pořadí hlavního směru, takže **všechny** km sloupce musí
/// shora dolů narůstat - i ty opačného směru. Odečtení od maxima (správné na samostatné stránce
/// směru "zpět", kde jsou zastávky obráceně) tady vyrábělo sloupce klesající odshora dolů, což ve
/// starém tisku není (nález Jan 7. 9.).
Method TestKmOpacnehoSmeruNarustajiShoraDolu()
{
	set db = ..#TESTDB
	if '##class(%SYS.Namespace).Exists(db) {
		do $$$AssertSkipped("firma "_db_" není na tomto serveru - test přeskočen")
		quit
	}
	set kmZ = +$get(^[db]JRL(..#TESTSKU, ..#TESTLIN, "PATCH", "KmsSmer", "Z"))
	if kmZ = 0 {
		do $$$AssertSkipped("linka nemá km sloupce směru zpět - test přeskočen")
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
		do $$$AssertSkipped("firma "_db_" není na tomto serveru - test přeskočen")
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
