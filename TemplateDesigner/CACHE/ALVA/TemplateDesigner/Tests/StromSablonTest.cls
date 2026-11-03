Include %occInclude

/// %UnitTest pro strom šablon (`API.StromSablon`) a seznam šablon podle uzlu stromu (`EDList.Sablony`), `B190`.
/// Firma = větev "EDTESTING", M-line = větev "EDTESTINGML" (stromu se předá místo "MLINE"), jako `SablonyProTiskTest`.
/// Seznam pro okno M-line větev předat neumí, proto test u něj kontroluje jen řádky firmy.
Class ALVA.TemplateDesigner.Tests.StromSablonTest Extends ALVA.EDTesting.MLTestCase
{

Parameter Firma = "EDTESTING";

Parameter Mline = "EDTESTINGML";

Method OnBeforeOneTest(testname As %String) As %Status
{
	kill ^||ML
	kill ^ALVA.TemplateDesigner.Templates(..#Firma)
	kill ^ALVA.TemplateDesigner.Templates(..#Mline)
	quit $$$OK
}

Method OnAfterOneTest(testname As %String) As %Status
{
	kill ^ALVA.TemplateDesigner.Templates(..#Firma)
	kill ^ALVA.TemplateDesigner.Templates(..#Mline)
	quit $$$OK
}

/// Jízdní řády: agendy a varianty podle názvu, nezveřejněná varianta (rozdílový JŘ) ve stromu není,
/// Obecná agenda bez varianty na konci pod Obecné, oblasti bez agend se neukazují.
Method TestStromJizdnichRadu()
{
	set strom = ..Strom()
	do $$$AssertEquals(..Popis(strom), "Jízdní řády[Linkový JŘ(AD,MHD),Zastávkový JŘ(AD,MHD)];Obecné[Obecná (bez dat)()]", "tvar stromu")

	set linkovy = ..Varianta(strom, "LINKOVY_JR_AD")
	do $$$AssertTrue($isobject(linkovy), "varianta Linkový JŘ AD")
	do $$$AssertTrue('$isobject(..Varianta(strom, "LINKOVY_JR_AD_ROZDILOVY")), "rozdílový JŘ se uživatelům nenabízí")
	do $$$AssertEquals(..Varianta(strom, "GENERAL").label, "", "Obecná agenda je bez varianty")
}

/// Počet šablon z obou větví a výchozí šablona pro tisk (M-line, pak firma, Obecná žádnou nemá).
Method TestPocetAVychozi()
{
	set agenda = "LINKOVY_JR_AD"
	set mlineId = $parameter("ALVA.TemplateDesigner.Agendy.LinkovyJrAd", "VychoziSablonaMline")
	do ..Uloz(..#Mline, mlineId, "A4 poznámky pod", agenda)
	do ..Uloz(..#Firma, "FA-1", "Naše", agenda)
	do ..Uloz(..#Firma, "FA-2", "Druhá", agenda)
	do ..Uloz(..#Firma, "FA-VYV", "Vývěska", "ZASTAVKOVY_JR_AD")
	do ..Uloz(..#Firma, "FA-OBECNA", "Leták", "GENERAL")

	set varianta = ..Varianta(..Strom(), agenda)
	do $$$AssertEquals(varianta.pocet, 3, "počet: šablony agendy z obou větví")
	do $$$AssertEquals(varianta.vychozi.zdroj, "mline", "bez volby firmy výchozí M-line")
	do $$$AssertEquals(varianta.vychozi.name, "A4 poznámky pod", "jméno výchozí M-line")

	do ##class(ALVA.TemplateDesigner.API.SablonyProTisk).WriteVychoziFirmy(..#Firma, agenda, "FA-2", ..#Mline)
	set varianta = ..Varianta(..Strom(), agenda)
	do $$$AssertEquals(varianta.vychozi.templateId, "FA-2", "výchozí firmy")
	do $$$AssertEquals(varianta.vychozi.zdroj, "firma", "zdroj výchozí firmy")

	do $$$AssertEquals(..Varianta(..Strom(), "ZASTAVKOVY_JR_AD").vychozi.%Size(), 0, "agenda bez šablony pro tisk: prázdný objekt")
	set obecna = ..Varianta(..Strom(), "GENERAL")
	do $$$AssertEquals(obecna.pocet, 1, "Obecná: počet šablon")
	do $$$AssertEquals(obecna.vychozi.%Size(), 0, "Obecná se z modulu netiskne, výchozí nemá")
	do $$$AssertEquals(obecna.tiskne, 0, "Obecná: netiskne se z modulu")
	do $$$AssertEquals(varianta.tiskne, 1, "Linkový JŘ AD se tiskne z modulu")
}

/// Zveřejněná agenda patří do známé oblasti práce a všechny varianty jedné agendy do téže - jinak by agenda
/// spadla pod Ostatní nebo se ve stromu rozdělila do dvou oblastí.
Method TestOblastiAgend()
{
	set oblasti = ##class(ALVA.TemplateDesigner.API.AgendyRegistr).OblastiPrace()
	for i = 1:1:$listlength(oblasti) set znama($list($list(oblasti, i), 1)) = ""

	do ##class(ALVA.TemplateDesigner.API.AgendyRegistr).TridyAgend(.tridy)
	set trida = ""
	for {
		set trida = $order(tridy(trida))
		quit:trida=""
		continue:$parameter(trida, "AgendaVariantCode")=""
		set oblast = $parameter(trida, "OblastPrace")
		do $$$AssertTrue((oblast'="")&&$data(znama(oblast)), trida_": známá oblast práce ("_oblast_")")
		set agenda = $parameter(trida, "Agenda")
		if $data(oblastAgendy(agenda)) {
			do $$$AssertEquals(oblast, oblastAgendy(agenda), trida_": stejná oblast jako ostatní varianty agendy "_agenda)
		}
		set oblastAgendy(agenda) = oblast
	}
}

/// API cesta strom posílá jako JSON v OUT[1].
Method TestApiStrom()
{
	kill ^||ML
	do ..SetupSys(.sys, ..#Firma)
	do ##class(ALVA.TemplateDesigner.API.StromSablon).GetStrom(.sys)
	set json = $get(^||ML("OUT","1"))
	do $$$AssertTrue(json [ """JIZDNI_RADY""", "GetStrom: OUT[1] se stromem")
	do $$$AssertEquals($data(^||ML("OUT","ERR")), 0, "GetStrom bez chyby")
	// Přepínač tisku z okna JŘ: bez něj okno šablon výchozí šablonu neukazuje (Jan 3. 11.).
	set tisk = ##class(%DynamicObject).%FromJSON(json).%GetTypeOf("tiskZModulu")
	do $$$AssertEquals(tisk, "boolean", "GetStrom posílá tiskZModulu jako ano/ne")
}

/// Seznam v okně šablon podle uzlu stromu: kódy agend čárkou, starší jeden kód, bez filtru vše.
/// Výchozí šablona firmy je označená ve skrytém sloupci i ve sloupci Pro tisk.
Method TestSeznamPodleUzlu()
{
	do ..Uloz(..#Firma, "FA-LAD", "Linkový AD", "LINKOVY_JR_AD")
	do ..Uloz(..#Firma, "FA-LMHD", "Linkový MHD", "LINKOVY_JR_MHD")
	do ..Uloz(..#Firma, "FA-ZAD", "Vývěska AD", "ZASTAVKOVY_JR_AD")
	do ##class(ALVA.TemplateDesigner.API.SablonyProTisk).WriteVychoziFirmy(..#Firma, "LINKOVY_JR_AD", "FA-LAD")

	do ..Seznam("LINKOVY_JR_AD,LINKOVY_JR_MHD", "", .radky)
	do $$$AssertEquals(..Vlastni(.radky), "FA-LAD,FA-LMHD", "uzel agendy: obě varianty")
	do $$$AssertEquals(radky("FA-LAD").vychozi, "firma", "výchozí firmy ve skrytém sloupci")
	do $$$AssertEquals(radky("FA-LAD").proTisk, "výchozí firmy", "výchozí firmy ve sloupci Pro tisk")
	do $$$AssertEquals(radky("FA-LAD").agendaCode, "LINKOVY_JR_AD", "kód agendy ve skrytém sloupci")
	do $$$AssertEquals(radky("FA-LMHD").vychozi, "", "jiná šablona výchozí není")

	do ..Seznam("", "ZASTAVKOVY_JR_AD", .radky)
	do $$$AssertEquals(..Vlastni(.radky), "FA-ZAD", "starší filtr jedním kódem")

	do ..Seznam("", "", .radky)
	do $$$AssertEquals(..Vlastni(.radky), "FA-LAD,FA-LMHD,FA-ZAD", "bez filtru všechny šablony firmy")
}

Method Strom() As %DynamicObject
{
	quit ##class(ALVA.TemplateDesigner.API.StromSablon).Strom(..#Firma, ..#Mline)
}

/// Varianta stromu podle kódu agendy; "" když ve stromu není.
Method Varianta(strom As %DynamicObject, code As %String) As %DynamicObject
{
	set nalezena = ""
	set iterOblasti = strom.oblasti.%GetIterator()
	while iterOblasti.%GetNext(.i, .oblast) {
		set iterAgendy = oblast.agendy.%GetIterator()
		while iterAgendy.%GetNext(.j, .agenda) {
			set iterVarianty = agenda.varianty.%GetIterator()
			while iterVarianty.%GetNext(.k, .varianta) {
				set:varianta.code=code nalezena = varianta
			}
		}
	}
	quit nalezena
}

/// „Oblast[Agenda(varianta,varianta),...];..." pro porovnání tvaru stromu.
Method Popis(strom As %DynamicObject) As %String
{
	set popis = ""
	set iterOblasti = strom.oblasti.%GetIterator()
	while iterOblasti.%GetNext(.i, .oblast) {
		set agendy = ""
		set iterAgendy = oblast.agendy.%GetIterator()
		while iterAgendy.%GetNext(.j, .agenda) {
			set varianty = ""
			set iterVarianty = agenda.varianty.%GetIterator()
			while iterVarianty.%GetNext(.k, .varianta) {
				set:varianta.label'="" varianty = varianty_$select(varianty="":"", 1:",")_varianta.label
			}
			set agendy = agendy_$select(agendy="":"", 1:",")_agenda.label_"("_varianty_")"
		}
		set popis = popis_$select(popis="":"", 1:";")_oblast.label_"["_agendy_"]"
	}
	quit popis
}

/// Řádky seznamu okna šablon do `radky(id)` = JSON řádku.
Method Seznam(agendaCodes As %String, agendaCode As %String, ByRef radky)
{
	kill radky
	kill ^||ML
	do ..SetupSys(.sys, ..#Firma)
	set:agendaCodes'="" ^||ML("IN","filter","agendaCodes") = agendaCodes
	set:agendaCode'="" ^||ML("IN","filter","agendaCode") = agendaCode
	do ##class(ALVA.TemplateDesigner.EDList.Sablony).LoadList(.sys)
	do $$$AssertEquals($data(^||ML("OUT","ERR")), 0, "LoadList bez chyby")
	set n = ""
	for {
		set n = $order(^||ML("OUT", n))
		quit:n=""
		continue:n="ERR"
		set radek = ##class(%DynamicObject).%FromJSON(^||ML("OUT", n))
		set radky(radek.id) = radek
	}
	kill ^||ML
}

/// Id vlastních šablon (zdroj „vlastní") seřazená - M-line větev na serveru test neovládá.
Method Vlastni(ByRef radky) As %String
{
	set vlastni = "", id = ""
	for {
		set id = $order(radky(id))
		quit:id=""
		continue:radky(id).zdroj'="vlastní"
		set vlastni = vlastni_$select(vlastni="":"", 1:",")_id
	}
	quit vlastni
}

/// Uloží šablonu přes API do dané větve (testovací M-line větev zámek MLINE nemá).
Method Uloz(vetev As %String, templateId As %String, name As %String, agendaCode As %String)
{
	kill ^||ML
	do ..SetupSys(.sys, ..#Firma)
	set ^||ML("IN","templateId") = templateId
	set ^||ML("IN","name")       = name
	set ^||ML("IN","agendaCode") = agendaCode
	set ^||ML("IN","targetIdFa") = vetev
	set ^||ML("IN","data")       = "{""version"":""1.0.0"",""name"":""x"",""sections"":[]}"
	do ##class(ALVA.TemplateDesigner.API.Sablony).SaveTemplate(.sys)
	do $$$AssertEquals($get(^||ML("OUT","templateId")), templateId, "uložení testovací šablony "_templateId)
	kill ^||ML
}

}
