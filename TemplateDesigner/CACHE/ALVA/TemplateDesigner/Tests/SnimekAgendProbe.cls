Include (%occInclude, GENERAL)

/// Snímek výstupu všech agend před sloučením kódu agend (`B56`, `legacy-nahrada.md §4`): seznam volání
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
		// Data linek: týž seznam, na kterém kontrakt hlídá, že data odpovídají nabídce.
		do ##class(ALVA.TemplateDesigner.Tests.KontraktAgendTest).LinkyAgend(.data)
		for i=1:1:$get(data) {
			set volani($increment(volani)) = $piece(data(i), "~", 2, 3)_"~GetData~"_$piece(data(i), "~", 4, 6)
		}
		for i=1:1:$get(volani) {
			set ^||ML("OUT", i) = volani(i)
		}
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
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
