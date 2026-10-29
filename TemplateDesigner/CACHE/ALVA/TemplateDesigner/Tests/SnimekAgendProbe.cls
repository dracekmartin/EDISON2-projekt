Include (%occInclude, GENERAL)

/// Snímek výstupu všech agend před sloučením kódu agend (`B56`, `konzistence-agend-jr.md §7`): seznam volání
/// a jedno volání se správnou firmou. Sonda, ne test - výstup závisí na datech dev serveru; ukládá
/// a porovnává ho `SnimekAgendNaostro` v .NET. Linky z testovacích sad, výchozí parametry. Po každém
/// kroku `B56` musí vyjít bajtově stejně.
Class ALVA.TemplateDesigner.Tests.SnimekAgendProbe Extends %RegisteredObject
{

/// OUT(i) = "nazev~trida~metoda~idfa~db~vstup", vstup = "KLIC=hodnota;KLIC=hodnota".
/// Prázdné idfa = výchozí firma testů (`MLTestCase.SetupSys`), prázdné db = namespace serveru.
ClassMethod Seznam(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		kill ^||ML
		do ##class(ALVA.TemplateDesigner.API.AgendyRegistr).ListAgendy(.sys)
		set agendy = ##class(%DynamicArray).%FromJSON($get(^||ML("OUT","1"), "[]"))
		kill ^||ML, volani
		set iter = agendy.%GetIterator()
		while iter.%GetNext(.i, .a) {
			for metoda = "GetSchema", "GetParameters", "GetMockData" {
				set volani($increment(volani)) = a.code_"-"_metoda_"~"_a.cacheClass_"~"_metoda_"~~~"
			}
		}
		set ag = "ALVA.TemplateDesigner.Agendy."
		do ##class(ALVA.TemplateDesigner.Tests.ZastavkoveJrAdSadaTest).Sada(.sada)
		for i=1:1:$get(sada) {
			do ..PridejAd(.volani, "ZASTAVKOVY_JR_AD", ag_"ZastavkoveJrAd", sada(i))
		}
		do ##class(ALVA.TemplateDesigner.Tests.LinkovyJrSadaTest).Sada(.sada)
		for i=1:1:$get(sada) {
			do ..PridejAd(.volani, "LINKOVY_JR_AD", ag_"LinkovyJrAd", sada(i))
		}
		// Porovnání verzí: táž dvojice jako spuštění z Porovnání linek (`SpusteniPdfNaostro`).
		set volani($increment(volani)) = "LINKOVY_JR_AD_ROZDILOVY-CB_CSADAD-0380760~"_ag_"LinkovyJrAdRozdilovy~GetData~~CB_CSADAD~SKUPINA_LINEK_ID=307;LINKA_ID=0380760;POROVNAT_SKUPINU=207;POROVNAT_LINKU=0380760"
		// Zastávkový MHD: linky zlatého odtisku (`ZastavkoveJrMhdGoldTest`).
		for lin = 1001, 1003, 1006 {
			set volani($increment(volani)) = "ZASTAVKOVY_JR_MHD-CB_DP-"_lin_"~"_ag_"ZastavkoveJrMhd~GetData~CB_DP~CB_DP~LINKA_ID="_lin
		}
		set linky = ##class(ALVA.TemplateDesigner.Tests.LinkovyJrMhdTest).#LINKY
		for i=1:1:$length(linky, ",") {
			set idfa = $piece($piece(linky, ",", i), ":", 1), lin = $piece($piece(linky, ",", i), ":", 2)
			set volani($increment(volani)) = "LINKOVY_JR_MHD-"_idfa_"-"_lin_"~"_ag_"LinkovyJrMhd~GetData~"_idfa_"~"_idfa_"~LINKA_ID="_lin
		}
		for i=1:1:$get(volani) {
			set ^||ML("OUT", i) = volani(i)
		}
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

/// Položka sady AD („firma~skupina~linka~…") jako volání GetData, firma testů jako v sadových testech.
ClassMethod PridejAd(ByRef volani, kod As %String, trida As %String, polozka As %String)
{
	set db = $piece(polozka, "~", 1), sku = $piece(polozka, "~", 2), lin = $piece(polozka, "~", 3)
	set volani($increment(volani)) = kod_"-"_db_"-"_lin_"~"_trida_"~GetData~~"_db_"~SKUPINA_LINEK_ID="_sku_";LINKA_ID="_lin
}

/// Jedno volání ze seznamu. IN: trida, metoda, idfa, db, vstup. OUT = výstup agendy beze změny;
/// chyba agendy jde do OUT("CHYBA") - v ^||ML("ERR") by ji klient vyhodil jako chybu sondy.
ClassMethod Vystup(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		set trida = $get(^||ML("IN","trida")), metoda = $get(^||ML("IN","metoda"))
		set idfa = $get(^||ML("IN","idfa")), db = $get(^||ML("IN","db")), vstup = $get(^||ML("IN","vstup"))
		kill ^||ML
		if (db '= "") && '##class(%SYS.Namespace).Exists(db) {
			set ^||ML("OUT","PRESKOCENO") = "firma "_db_" na serveru není"
			quit
		}
		do ##class(ALVA.EDTesting.MLTestCase).SetupSys(.s, $select(idfa'="":idfa, 1:"CB_DP"))
		set:db'="" s("U","DATA") = db
		for i=1:1:$length(vstup, ";") {
			set par = $piece(vstup, ";", i)
			continue:par=""
			set ^||ML("IN", $piece(par, "=", 1)) = $piece(par, "=", 2, *)
		}
		do $classmethod(trida, metoda, .s)
		if $data(^||ML("ERR")) {
			set ^||ML("OUT","CHYBA") = ^||ML("ERR")
			kill ^||ML("ERR")
		}
		kill ^||ML("IN")
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

}
