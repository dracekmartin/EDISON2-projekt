Include (%occInclude, GENERAL)

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

/// `G2`: parametr se stejným jménem ve víc agendách je **týž parametr** - popisek, typ, skupina, výchozí
/// hodnota, volby i příznak načtení dat se shodují. Kopie (parametry vývěsek, linkové AD × MHD) se jinak
/// rozjedou a uživatel uvidí totéž nastavení v každé agendě jinak; při slučování v `B56` (R5) test
/// ohlídá, že se nic nezmění. Kontroluje se výstup serveru, takže přežije přesun kódu.
Method TestSdileneParametryShodne()
{
	do ..SetupSys(.sys)
	do ..SberParametru(..Agendy(), .sys, .def)
	do ..Rozdily(.def, .rozdil, .sdilene)
	do $$$AssertTrue($data(sdilene), "žádný sdílený parametr - sběr parametrů nefunguje")
	set jmeno = "", nalez = ""
	for {
		set jmeno = $order(rozdil(jmeno))
		quit:jmeno=""
		set pole = ""
		for {
			set pole = $order(rozdil(jmeno, pole))
			quit:pole=""
			continue:..Vyjimka(jmeno, pole)
			set nalez = nalez_$char(13,10)_jmeno_" "_pole_": "_rozdil(jmeno, pole)
		}
	}
	do $$$AssertEquals(nalez, "", "sdílené parametry se liší:"_nalez)
}

/// Vědomé rozdíly sdíleného parametru mezi agendami (stav 29. 10.). Nová výjimka jen s důvodem.
ClassMethod Vyjimka(jmeno As %String, pole As %String) As %Boolean
{
	// Výchozí tvar podle starého modulu: vývěska „Zkrácený" (dvojčárku srážel), linkový „Plný CIS"
	// (nechává ji) - potvrzeno Janem 24. 8. (`BaseAgenda.ParametrTvaruNazvuZastavky`).
	quit:jmeno_"|"_pole="TVAR_NAZVU_ZASTAVKY|vychozi" 1
	// MHD nemá nastavení u linky, takže nemá ani „Automaticky (dle linky)"; počet stran až 3, protože
	// v MHD jezdí víc spojů (rozhodnutí Jan 22. 10., `LinkovyJrAd.ParametrPoctuStran`).
	quit:(jmeno="POCET_STRAN_SESTAVY")&&((pole="volby")||(pole="vychozi")) 1
	quit:(jmeno="ZASTAVKY_BEZ_ZASTAVENI")&&((pole="volby")||(pole="vychozi")) 1
	// Skupina nese název prvku, kterého se parametr týká - barvy spojů kreslí v linkovém JŘ Tabulka
	// spojů, v zastávkovém Minuty odjezdů.
	quit:jmeno_"|"_pole="TISKNOUT_BARVY_SPOJU|skupina" 1
	quit 0
}

/// Definice parametrů všech agend podle jména, tak jak je server pošle (i se jménem skládaným za běhu):
/// def(jmeno, kodAgendy, pole) = hodnota.
ClassMethod SberParametru(agendy As %DynamicArray, ByRef sys, ByRef def)
{
	kill def
	set iter = agendy.%GetIterator()
	while iter.%GetNext(.i, .a) {
		kill ^||ML
		do $classmethod(a.cacheClass, "GetParameters", .sys)
		set params = ##class(%DynamicArray).%FromJSON($get(^||ML("OUT", 1), "[]"))
		set pi = params.%GetIterator()
		while pi.%GetNext(.j, .p) {
			set volby = ""
			if $isobject(p.options) {
				set oi = p.options.%GetIterator()
				while oi.%GetNext(.k, .v) {
					set volby = volby_$select(volby="":"", 1:" / ")_v
				}
			}
			set def(p.name, a.code, "popisek") = p.label
			set def(p.name, a.code, "typ") = p.type
			set def(p.name, a.code, "skupina") = p.group
			set def(p.name, a.code, "vychozi") = p.defaultValue
			set def(p.name, a.code, "volby") = volby
			set def(p.name, a.code, "serverSide") = +p.serverSide
		}
	}
	kill ^||ML
}

/// Parametry, které má víc agend: sdilene(jmeno) = agendy; rozdil(jmeno, pole) = „AGENDA=hodnota; …"
/// u pole, ve kterém se kopie liší.
ClassMethod Rozdily(ByRef def, ByRef rozdil, ByRef sdilene)
{
	kill rozdil, sdilene
	set jmeno = ""
	for {
		set jmeno = $order(def(jmeno))
		quit:jmeno=""
		set prvni = $order(def(jmeno, ""))
		continue:$order(def(jmeno, prvni))=""
		set kod = "", agendy = ""
		for {
			set kod = $order(def(jmeno, kod))
			quit:kod=""
			set agendy = agendy_$select(agendy="":"", 1:", ")_kod
		}
		set sdilene(jmeno) = agendy
		for pole = "popisek", "typ", "skupina", "vychozi", "volby", "serverSide" {
			set kod = "", lisi = 0, popis = ""
			for {
				set kod = $order(def(jmeno, kod))
				quit:kod=""
				set hodnota = $get(def(jmeno, kod, pole))
				set:hodnota'=$get(def(jmeno, prvni, pole)) lisi = 1
				set popis = popis_$select(popis="":"", 1:"; ")_kod_"="_hodnota
			}
			set:lisi rozdil(jmeno, pole) = popis
		}
	}
}

/// Přehled sdílených parametrů a jejich rozdílů - podklad pro výjimky a pro `B56` R5, ne test.
/// Voláno přes `cache_query_cvar`.
ClassMethod Prehled(ByRef sys As %String) As %Status
{
	set err = ""
	try {
		$$$FillParams
		kill ^||ML
		do ##class(ALVA.TemplateDesigner.API.AgendyRegistr).ListAgendy(.sys)
		set agendy = ##class(%DynamicArray).%FromJSON($get(^||ML("OUT","1"), "[]"))
		do ..SberParametru(agendy, .sys, .def)
		do ..Rozdily(.def, .rozdil, .sdilene)
		set jmeno = ""
		for {
			set jmeno = $order(sdilene(jmeno))
			quit:jmeno=""
			set radek = jmeno_" ("_sdilene(jmeno)_"): "
			if '$data(rozdil(jmeno)) {
				set radek = radek_"shodné"
			} else {
				set pole = ""
				for {
					set pole = $order(rozdil(jmeno, pole))
					quit:pole=""
					set radek = radek_" | "_pole_": "_rozdil(jmeno, pole)
				}
			}
			set ^||ML("OUT", $increment(^||ML("OUT"))) = radek
		}
	}
	catch (ex) { $$$CatchErrRest }
	ret $$$OK
}

}
