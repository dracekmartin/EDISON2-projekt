Include %occInclude

/// `B194f` G1: kontrakt všech agend, které najde registr - i těch, které ještě nikdo nenapsal.
/// Testy agend je do 29. 10. vyjmenovávaly ručně, takže nová agenda nebo varianta zůstala bez pojistky
/// (odtud `C1`). Malá podoba: volání kontraktu bez chyby a s platným JSON, jednoznačný kód, známý
/// druh prvků, výchozí volba mezi volbami a „Obecné" jako první skupina parametrů. Pojistka před `B56`:
/// přesun sdíleného kódu, který kteroukoli agendu rozbije, spadne tady.
Class ALVA.TemplateDesigner.Tests.KontraktAgendTest Extends ALVA.EDTesting.MLTestCase
{

/// Agendy z registru tak, jak je dostane návrhář.
Method Agendy() As %DynamicArray
{
	do ..SetupSys(.sys)
	kill ^||ML
	do ##class(ALVA.TemplateDesigner.API.AgendyRegistr).ListAgendy(.sys)
	do $$$AssertEquals($get(^||ML("ERR")), "", "registr agend bez chyby: "_$get(^||ML("ERR")))
	quit ##class(%DynamicArray).%FromJSON($get(^||ML("OUT","1"), "[]"))
}

/// Dvě třídy se stejným kódem by registr vrátil obě a návrhář by otevíral tu první - šablony druhé
/// by tiše četly cizí data. Druh prvků mimo známé hodnoty by v návrháři schoval paletu JŘ.
Method TestRegistrNajdeAgendyJr()
{
	set iter = ..Agendy().%GetIterator()
	kill kody
	while iter.%GetNext(.i, .a) {
		do $$$AssertTrue('$data(kody(a.code)), "kód "_a.code_" mají dvě třídy: "_$get(kody(a.code))_" a "_a.cacheClass)
		set kody(a.code) = a.cacheClass
		do $$$AssertTrue($listfind($listbuild("", "STOP", "LINE"), a.elementKind), a.code_": neznámý druh prvků „"_a.elementKind_"“")
	}
	for kod = "ZASTAVKOVY_JR_AD", "ZASTAVKOVY_JR_MHD", "LINKOVY_JR_AD", "LINKOVY_JR_MHD", "LINKOVY_JR_AD_ROZDILOVY" {
		do $$$AssertTrue($data(kody(kod)), "registr nenašel agendu "_kod)
	}
}

/// Schéma, parametry a ukázková data volá návrhář při každém otevření agendy; výpadek kterékoli
/// metody znamená, že agendu nejde vybrat. Neplatný JSON by u klienta skončil výjimkou, ne hláškou.
Method TestKontraktKazdeAgendy()
{
	do ..SetupSys(.sys)
	set iter = ..Agendy().%GetIterator()
	while iter.%GetNext(.i, .a) {
		for metoda = "GetSchema", "GetParameters", "GetMockData" {
			kill ^||ML
			do $classmethod(a.cacheClass, metoda, .sys)
			set chyba = $get(^||ML("ERR"))
			do $$$AssertEquals(chyba, "", a.code_" "_metoda_" bez chyby: "_chyba)
			set json = $get(^||ML("OUT", 1))
			do $$$AssertNotEquals(json, "", a.code_" "_metoda_" musí něco vrátit")
			set ok = 1
			try { do ##class(%DynamicAbstractObject).%FromJSON(json) } catch { set ok = 0 }
			do $$$AssertTrue(ok, a.code_" "_metoda_" musí vrátit platný JSON")
		}
	}
}

/// Výchozí volba mimo nabídku by panel ukázal prázdný a uložila by se hodnota, kterou server nezná.
/// „Obecné" první (`ux-conventions`) - u vývěsek a linkového MHD skládají parametry pomocné metody
/// a offline kontrola pořadí je nevidí (`G7`), proto se kontroluje výstup serveru.
Method TestParametryKazdeAgendy()
{
	do ..SetupSys(.sys)
	set iter = ..Agendy().%GetIterator()
	while iter.%GetNext(.i, .a) {
		kill ^||ML
		do $classmethod(a.cacheClass, "GetParameters", .sys)
		set params = ##class(%DynamicArray).%FromJSON($get(^||ML("OUT", 1), "[]"))
		continue:params.%Size()=0
		set prvni = params.%Get(0)
		do $$$AssertEquals(prvni.group, "Obecné", a.code_": první parametr "_prvni.name_" má být ve skupině Obecné")
		set pi = params.%GetIterator()
		while pi.%GetNext(.j, .p) {
			continue:p.type'="enum"
			if '$isobject(p.options) {
				do $$$AssertTrue(0, a.code_": výběr "_p.name_" nemá volby")
				continue
			}
			continue:p.defaultValue=""
			set nalezeno = 0, oi = p.options.%GetIterator()
			while oi.%GetNext(.k, .volba) {
				set:volba=p.defaultValue nalezeno = 1
			}
			do $$$AssertTrue(nalezeno, a.code_": výchozí hodnota „"_p.defaultValue_"“ parametru "_p.name_" není mezi volbami")
		}
	}
}

}
