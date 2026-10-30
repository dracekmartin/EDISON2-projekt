Include (%occInclude, GENERAL)

/// `B194f` G1: kontrakt všech agend, které najde registr - i těch, které ještě nikdo nenapsal.
/// Testy agend je do 29. 10. vyjmenovávaly ručně, takže nová agenda nebo varianta zůstala bez pojistky
/// (odtud `C1`). Hlídá volání kontraktu bez chyby a s platným JSON, jednoznačný kód, známý druh prvků,
/// výchozí volbu mezi volbami, „Obecné" jako první skupinu parametrů, jen známé klíče definice parametru
/// a to, že data agendy odpovídají její nabídce dat. Pojistka před `B56`: přesun sdíleného kódu, který
/// kteroukoli agendu rozbije, spadne tady.
Class ALVA.TemplateDesigner.Tests.KontraktAgendTest Extends ALVA.EDTesting.MLTestCase
{

/// Klíče definice parametru, které návrhář čte (`CacheAgendaProvider.ParseParameters` a formulář
/// `EDInputForm.AgendaParams`). Jiný klíč se tiše ignoruje: Faktury do 30. 10. posílaly výchozí hodnotu
/// pod `default` a panel ji nenastavil.
Parameter ZNAMEKLICEPARAMETRU = "name,label,type,group,defaultValue,options,description,serverSide,visibleWhen,visibleWhenColumn,visibleWhenListedIn,perInstance";

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
/// Klíč definice mimo `ZNAMEKLICEPARAMETRU` návrhář tiše ignoruje. „Obecné" první (`ux-conventions`) - u vývěsek a linkového MHD skládají parametry pomocné metody
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
			set ki = p.%GetIterator()
			while ki.%GetNext(.klic, .hodnota) {
				do $$$AssertTrue((","_..#ZNAMEKLICEPARAMETRU_",")[(","_klic_","), a.code_": parametr "_p.name_" má klíč „"_klic_"“, který návrhář nečte")
			}
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
	// (nechává ji) - potvrzeno Janem 24. 8. (`Jr.Parametry.ParametrTvaruNazvuZastavky`).
	quit:jmeno_"|"_pole="TVAR_NAZVU_ZASTAVKY|vychozi" 1
	// MHD nemá nastavení u linky, takže nemá ani „Automaticky (dle linky)"; počet stran až 3, protože
	// v MHD jezdí víc spojů (rozhodnutí Jan 22. 10., `Jr.Parametry.ParametrPoctuStran`).
	quit:(jmeno="POCET_STRAN_SESTAVY")&&((pole="volby")||(pole="vychozi")) 1
	quit:(jmeno="ZASTAVKY_BEZ_ZASTAVENI")&&((pole="volby")||(pole="vychozi")) 1
	// Skupina nese název prvku, kterého se parametr týká - barvy spojů kreslí v linkovém JŘ Tabulka
	// spojů, v zastávkovém Minuty odjezdů.
	quit:jmeno_"|"_pole="TISKNOUT_BARVY_SPOJU|skupina" 1
	quit 0
}

/// Data agendy odpovídají její nabídce dat. Pole, které agenda nabízí a v datech nepošle, vytiskne
/// prázdno. Položka, kterou data posílají, nabídka ji nemá a návrhář ji nečte, je zbytek, o kterém
/// nikdo neví, jestli ho smí smazat. Data jsou z linek `LinkyAgend`: ukázková data agend jsou záměrně
/// prázdná („vyberte linku"). Pole se počítá, když je aspoň na jedné stránce některé linky.
Method TestDataOdpovidajiNabidce()
{
	do ..SetupSys(.sys)
	do ..LinkyAgend(.volani)
	do ..PolozkyMimoNabidku(.mimo)
	set nalez = "", iter = ..Agendy().%GetIterator()
	while iter.%GetNext(.i, .a) {
		kill klice
		set linek = 0, preskoceno = 0
		for v = 1:1:$get(volani) {
			continue:$piece(volani(v), "~", 1)'=a.code
			set chyba = ..SberDat(volani(v), .klice)
			if chyba = "-" {
				set preskoceno = preskoceno + 1
				continue
			}
			do $$$AssertEquals(chyba, "", a.code_" "_$piece(volani(v), "~", 2)_": data bez chyby: "_chyba)
			set linek = linek + 1
		}
		if 'linek {
			// Firmy linek na tomto serveru nejsou - nic k ověření. Agenda bez linek je díra v pokrytí.
			do $$$AssertTrue(preskoceno || ..BezDat(a.code), a.code_": žádná linka s daty, doplň ji do LinkyAgend")
			continue
		}
		kill ^||ML
		do $classmethod(a.cacheClass, "GetSchema", .sys)
		do ..Nabidka($get(^||ML("OUT", 1)), .nabidka)
		kill ^||ML
		set kol = ""
		for {
			set kol = $order(nabidka(kol))
			quit:kol=""
			if (kol'="udaje") && '$data(klice(kol)) {
				set nalez = nalez_$char(13,10)_a.code_": kolekci "_kol_" nabízí, data ji nepošlou"
				continue
			}
			set pole = ""
			for {
				set pole = $order(nabidka(kol, pole))
				quit:pole=""
				continue:$data(klice(kol, pole))
				continue:..SmiChybet(a.code, pole)
				set nalez = nalez_$char(13,10)_a.code_": "_kol_" "_pole_" nabízí, data ho nepošlou"
			}
		}
		set kol = ""
		for {
			set kol = $order(klice(kol))
			quit:kol=""
			continue:($data(mimo(kol))#2)
			if (kol'="udaje") && '$data(nabidka(kol)) {
				set nalez = nalez_$char(13,10)_a.code_": data posílají kolekci "_kol_", kterou nabídka nemá"
				continue
			}
			set pole = ""
			for {
				set pole = $order(klice(kol, pole))
				quit:pole=""
				continue:$data(nabidka(kol, pole))
				continue:$data(mimo(kol, pole))
				set nalez = nalez_$char(13,10)_a.code_": data posílají "_kol_" "_pole_", nabídka ho nemá a v PolozkyMimoNabidku chybí"
			}
		}
	}
	do $$$AssertEquals(nalez, "", "data agend neodpovídají nabídce:"_nalez)
}

/// Linky s daty pro každou agendu JŘ: volani(i) = "kód~název~třída~firma~databáze~vstup", vstup =
/// "KLÍČ=hodnota;KLÍČ=hodnota", prázdná firma = výchozí firma testů. Linky ze sad a zlatých odtisků
/// s výchozími parametry. Bere je i snímek výstupu agend (`SnimekAgendProbe`).
ClassMethod LinkyAgend(ByRef volani)
{
	kill volani
	set ag = "ALVA.TemplateDesigner.Agendy."
	do ##class(ALVA.TemplateDesigner.Tests.ZastavkoveJrAdSadaTest).Sada(.sada)
	for i = 1:1:$get(sada) {
		do ..PridejAd(.volani, "ZASTAVKOVY_JR_AD", ag_"ZastavkoveJrAd", sada(i))
	}
	do ##class(ALVA.TemplateDesigner.Tests.LinkovyJrSadaTest).Sada(.sada)
	for i = 1:1:$get(sada) {
		do ..PridejAd(.volani, "LINKOVY_JR_AD", ag_"LinkovyJrAd", sada(i))
	}
	// Porovnání verzí: táž dvojice jako spuštění z Porovnání linek (`SpusteniPdfNaostro`).
	set volani($increment(volani)) = "LINKOVY_JR_AD_ROZDILOVY~LINKOVY_JR_AD_ROZDILOVY-CB_CSADAD-0380760~"_ag_"LinkovyJrAdRozdilovy~~CB_CSADAD~SKUPINA_LINEK_ID=307;LINKA_ID=0380760;POROVNAT_SKUPINU=207;POROVNAT_LINKU=0380760"
	// Zastávkový MHD: linky zlatého odtisku (`ZastavkoveJrMhdGoldTest`).
	for lin = 1001, 1003, 1006 {
		set volani($increment(volani)) = "ZASTAVKOVY_JR_MHD~ZASTAVKOVY_JR_MHD-CB_DP-"_lin_"~"_ag_"ZastavkoveJrMhd~CB_DP~CB_DP~LINKA_ID="_lin
	}
	set linky = ##class(ALVA.TemplateDesigner.Tests.LinkovyJrMhdTest).#LINKY
	for i = 1:1:$length(linky, ",") {
		set idfa = $piece($piece(linky, ",", i), ":", 1), lin = $piece($piece(linky, ",", i), ":", 2)
		set volani($increment(volani)) = "LINKOVY_JR_MHD~LINKOVY_JR_MHD-"_idfa_"-"_lin_"~"_ag_"LinkovyJrMhd~"_idfa_"~"_idfa_"~LINKA_ID="_lin
	}
}

/// Položka sady AD („firma~skupina~linka~…") jako volání GetData, firma testů jako v sadových testech.
ClassMethod PridejAd(ByRef volani, kod As %String, trida As %String, polozka As %String)
{
	set db = $piece(polozka, "~", 1), sku = $piece(polozka, "~", 2), lin = $piece(polozka, "~", 3)
	set volani($increment(volani)) = kod_"~"_kod_"-"_db_"-"_lin_"~"_trida_"~~"_db_"~SKUPINA_LINEK_ID="_sku_";LINKA_ID="_lin
}

/// Zavolá GetData jedné linky z `LinkyAgend` a přidá klíče jejích stránek do `klice`: klice("udaje", KLÍČ)
/// pro údaje, klice(KOLEKCE) a klice(KOLEKCE, POLE) pro řádky. Vrátí text chyby agendy, "-" když firma
/// na serveru není, jinak "".
ClassMethod SberDat(radek As %String, ByRef klice) As %String
{
	set trida = $piece(radek, "~", 3), idfa = $piece(radek, "~", 4), db = $piece(radek, "~", 5), vstup = $piece(radek, "~", 6)
	quit:(db'="")&&'##class(%SYS.Namespace).Exists(db) "-"
	do ##class(ALVA.EDTesting.MLTestCase).SetupSys(.s, $select(idfa'="":idfa, 1:"CB_DP"))
	set:db'="" s("U","DATA") = db
	kill ^||ML
	for i = 1:1:$length(vstup, ";") {
		set par = $piece(vstup, ";", i)
		continue:par=""
		set ^||ML("IN", $piece(par, "=", 1)) = $piece(par, "=", 2, *)
	}
	do $classmethod(trida, "GetData", .s)
	set chyba = $get(^||ML("ERR")), k = ""
	for {
		set k = $order(^||ML("OUT", k))
		quit:k=""
		continue:'$isvalidnum(k)
		set strana = ##class(%DynamicObject).%FromJSON(^||ML("OUT", k))
		if $isobject(strana.values) {
			set it = strana.values.%GetIterator()
			while it.%GetNext(.klic, .hodnota) {
				set klice("udaje", klic) = ""
			}
		}
		continue:'$isobject(strana.rows)
		set it = strana.rows.%GetIterator()
		while it.%GetNext(.kol, .rady) {
			set klice(kol) = ""
			continue:'$isobject(rady)
			set ir = rady.%GetIterator()
			while ir.%GetNext(.j, .r) {
				continue:'$isobject(r)
				set ip = r.%GetIterator()
				while ip.%GetNext(.pole, .hodnota) {
					set klice(kol, pole) = ""
				}
			}
		}
	}
	kill ^||ML
	quit chyba
}

/// Nabídka dat z JSON `GetSchema`: nabidka("udaje", POLE), nabidka(KOLEKCE) a nabidka(KOLEKCE, POLE).
ClassMethod Nabidka(json As %String, ByRef nabidka)
{
	kill nabidka
	set schema = ##class(%DynamicObject).%FromJSON($select(json'="":json, 1:"{}"))
	if $isobject(schema.fields) {
		set it = schema.fields.%GetIterator()
		while it.%GetNext(.i, .f) {
			set nabidka("udaje", f.name) = ""
		}
	}
	quit:'$isobject(schema.fieldCollections)
	set it = schema.fieldCollections.%GetIterator()
	while it.%GetNext(.i, .c) {
		set nabidka(c.collectionKey) = ""
		continue:'$isobject(c.fields)
		set ip = c.fields.%GetIterator()
		while ip.%GetNext(.j, .f) {
			set nabidka(c.collectionKey, f.name) = ""
		}
	}
}

/// Agenda, která data nemá a linky v `LinkyAgend` nepotřebuje.
ClassMethod BezDat(kod As %String) As %Boolean
{
	// Dnes žádná: ukázková agenda Faktury bez zdroje dat je od 30. 10. smazaná (Jan).
	quit 0
}

/// Pole z nabídky, které data linek z `LinkyAgend` vědomě nepošlou.
ClassMethod SmiChybet(kod As %String, pole As %String) As %Boolean
{
	// Jen na stránkách výlepu; linky jedou s výchozími parametry, kde se výlepy netisknou.
	quit:((kod="LINKOVY_JR_AD")||(kod="LINKOVY_JR_AD_ROZDILOVY"))&&(pole="VYLEP_ZASTAVKY") 1
	quit 0
}

/// Položky dat, které nabídka nemá, a proč je data posílají: mimo(KOLEKCE, POLE) nebo mimo("udaje", KLÍČ)
/// = druh, mimo(KOLEKCE) = druh pro celou kolekci. Druhy:
/// `prvek` - čte je kód návrháře, uživatel je v nabídce nehledá (struktura, kterou prvek kreslí);
/// `parametr` - čte je panel parametrů přes `visibleWhenListedIn`;
/// `sablony` - vyřazené z nabídky, posílají se kvůli hotovým šablonám;
/// `zbytek` - nic je nečte, čeká na rozhodnutí, jestli je agenda přestane posílat.
/// Že `prvek` opravdu něco čte, hlídá `PolozkyMimoNabidkuTests` v .NET.
ClassMethod PolozkyMimoNabidku(ByRef mimo)
{
	kill mimo
	// Údaje stránky, které čte návrhář: vzory sloupců editoru, varianty tras a zdroj stanoviště Seznamu zastávek.
	set mimo("udaje", "PRESETY_SLOUPCU") = "prvek"
	set mimo("udaje", "VARIANTY_TRAS") = "prvek"
	set mimo("udaje", "ZDROJ_STANOVISTE") = "prvek"
	set mimo("udaje", "TYPY_PROVOZU_LINKY") = "parametr"
	set mimo("udaje", "POPIS_PLATNOSTI") = "sablony"
	// Dřívější název označníku vývěsky („nejedou spoje" byl omyl ve starém modulu), dnes NASTUPISTE.
	set mimo("udaje", "NEJEDOU_SPOJE") = "sablony"
	// Řádky zastávek: Seznam zastávek, Minuty odjezdů a Tabulka spojů.
	set mimo("ZASTAVKY", "ATRIBUTY") = "prvek"
	set mimo("ZASTAVKY", "ID") = "prvek"
	set mimo("ZASTAVKY", "KM") = "prvek"
	set mimo("ZASTAVKY", "NASTUPISTE_VARIANT") = "prvek"
	set mimo("ZASTAVKY", "NA_ZNAMENI") = "prvek"
	set mimo("ZASTAVKY", "ODSAZENI") = "prvek"
	set mimo("ZASTAVKY", "PORADI") = "prvek"
	set mimo("ZASTAVKY", "POZNAMKY_ZASTAVKY") = "prvek"
	set mimo("ZASTAVKY", "SLOUPEK_VARIANT") = "prvek"
	set mimo("ZASTAVKY", "TRASA_MIN") = "prvek"
	set mimo("ZASTAVKY", "TYP") = "prvek"
	// Spoje linkového JŘ: Tabulka spojů (s porovnáním verzí).
	set mimo("SPOJE", "CASY") = "prvek"
	set mimo("SPOJE", "DOPRAVCI") = "prvek"
	set mimo("SPOJE", "NZ_PUVODNI") = "prvek"
	set mimo("SPOJE", "NZ_VERZE") = "prvek"
	set mimo("SPOJE", "PLATNOST") = "prvek"
	set mimo("SPOJE", "PRIPOJE") = "prvek"
	set mimo("SPOJE", "ROZDILY") = "prvek"
	set mimo("SPOJE", "SKUPINA") = "prvek"
	set mimo("SPOJE", "SMER") = "prvek"
	set mimo("SPOJE", "TEXTOVE_ZNACKY") = "prvek"
	set mimo("SPOJE", "ZNACKY") = "prvek"
	set mimo("SKUPINY_DNU", "ID") = "prvek"
	// Odjezdy a poznámky: Minuty odjezdů a Poznámky; barvy jsou prezentační nápovědy (`cache-api.md`).
	set mimo("ODJEZDY", "ANOTACE") = "prvek"
	set mimo("ODJEZDY", "BACK_COLOR") = "prvek"
	set mimo("ODJEZDY", "FORE_COLOR") = "prvek"
	set mimo("ODJEZDY", "SKUPINA_DNU_ID") = "prvek"
	set mimo("ODJEZDY", "ZASTAVKA_ID") = "prvek"
	set mimo("POZNAMKY", "BACK_COLOR") = "prvek"
	set mimo("POZNAMKY", "FORE_COLOR") = "prvek"
	set mimo("POZNAMKY", "KATEGORIE") = "prvek"
	set mimo("POZNAMKY", "TYP") = "prvek"
	set mimo("POZNAMKY", "VERZE") = "prvek"
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
