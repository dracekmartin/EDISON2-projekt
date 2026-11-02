Include %occInclude

/// %UnitTest pro ALVA.TemplateDesigner.API.SablonyProTisk - pravidlo, kterou šablonu tisk vezme (`B190`).
/// Firma = větev "EDTESTING", M-line = větev "EDTESTINGML" (pravidlu se předá místo "MLINE"), takže
/// testy nezávisí na šablonách, které na serveru nalil návrhář. Výchozí šablona M-line je skutečný
/// parametr agendy Linkový JŘ AD - test do své M-line větve uloží šablonu s tímto Id.
Class ALVA.TemplateDesigner.Tests.SablonyProTiskTest Extends ALVA.EDTesting.MLTestCase
{

Parameter Firma = "EDTESTING";

Parameter Mline = "EDTESTINGML";

Parameter Agenda = "LINKOVY_JR_AD";

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

/// Bez výchozí šablony firmy i M-line tisk skončí důvodem pro uživatele, ne tichým výsledkem ani chybou programu.
Method TestBezSablonyHlaska()
{
	set sablona = ..Pravidlo("")
	do $$$AssertEquals(sablona.templateId, "", "bez šablony: prázdné templateId")
	do $$$AssertTrue(sablona.duvod [ "není nastavená šablona pro tisk", "důvod bez šablony: "_sablona.duvod)
	do $$$AssertTrue(sablona.duvod [ "Linkový JŘ (AD)", "důvod jmenuje agendu: "_sablona.duvod)
}

/// Krok 4: firma nic nenastavila → výchozí šablona M-line z parametru agendy.
Method TestVychoziMline()
{
	set mlineId = ..VychoziMlineId()
	do $$$AssertNotEquals(mlineId, "", "Linkový JŘ AD má výchozí šablonu M-line")
	do ..Uloz(..#Mline, mlineId, "A4 poznámky pod", ..#Agenda)
	do ..Uloz(..#Mline, "ML-JINA", "A5 postskript", ..#Agenda)

	set sablona = ..Pravidlo("")
	do $$$AssertEquals(sablona.templateId, mlineId, "M-line: výchozí podle parametru agendy, ne první šablona")
	do $$$AssertEquals(sablona.sourceIdFa, ..#Mline, "M-line: načíst z větve M-line")
	do $$$AssertEquals(sablona.zdroj, "mline", "M-line: zdroj")
	do $$$AssertEquals(sablona.name, "A4 poznámky pod", "M-line: jméno")
}

/// Krok 3 má přednost před M-line, i když ukazuje na jinou šablonu M-line.
Method TestVychoziFirmyMaPrednost()
{
	do ..Uloz(..#Mline, ..VychoziMlineId(), "A4 poznámky pod", ..#Agenda)
	do ..Uloz(..#Mline, "ML-JINA", "A5 postskript", ..#Agenda)
	do ..Uloz(..#Firma, "FA-1", "Naše", ..#Agenda)

	do ##class(ALVA.TemplateDesigner.API.SablonyProTisk).WriteVychoziFirmy(..#Firma, ..#Agenda, "FA-1", ..#Mline)
	set sablona = ..Pravidlo("")
	do $$$AssertEquals(sablona.templateId, "FA-1", "firma: vlastní výchozí")
	do $$$AssertEquals(sablona.zdroj, "firma", "firma: zdroj")
	do $$$AssertEquals(sablona.sourceIdFa, ..#Firma, "firma: větev firmy")

	do ##class(ALVA.TemplateDesigner.API.SablonyProTisk).WriteVychoziFirmy(..#Firma, ..#Agenda, "ML-JINA", ..#Mline)
	set sablona = ..Pravidlo("")
	do $$$AssertEquals(sablona.templateId, "ML-JINA", "firma smí za výchozí vzít jinou šablonu M-line")
	do $$$AssertEquals(sablona.zdroj, "firma", "výchozí firmy z M-line: zdroj firma")
	do $$$AssertEquals(sablona.sourceIdFa, ..#Mline, "výchozí firmy z M-line: načíst z M-line")

	do ##class(ALVA.TemplateDesigner.API.SablonyProTisk).WriteVychoziFirmy(..#Firma, ..#Agenda, "", ..#Mline)
	do $$$AssertEquals(..Pravidlo("").zdroj, "mline", "zrušená výchozí firmy → zpět M-line")
}

/// Krok 1 přebije výchozí šablony.
Method TestVolbaVolajicihoMaPrednost()
{
	do ..Uloz(..#Mline, ..VychoziMlineId(), "A4 poznámky pod", ..#Agenda)
	do ..Uloz(..#Firma, "FA-1", "Naše", ..#Agenda)
	do ..Uloz(..#Firma, "FA-2", "Druhá", ..#Agenda)
	do ##class(ALVA.TemplateDesigner.API.SablonyProTisk).WriteVychoziFirmy(..#Firma, ..#Agenda, "FA-1", ..#Mline)

	set sablona = ..Pravidlo("FA-2")
	do $$$AssertEquals(sablona.templateId, "FA-2", "volající: jeho šablona")
	do $$$AssertEquals(sablona.zdroj, "volajici", "volající: zdroj")
}

/// Šablona, kterou volající chce a v databázi není (nebo je jiné agendy), skončí důvodem - ne tichým
/// přechodem na výchozí, uživatel by dostal jiný výtisk, než si vybral.
Method TestVolbaVolajicihoNeexistujeNeniVychozi()
{
	do ..Uloz(..#Mline, ..VychoziMlineId(), "A4 poznámky pod", ..#Agenda)
	do ..Uloz(..#Firma, "FA-VYV", "Vývěska", "ZASTAVKOVY_JR_AD")

	set sablona = ..Pravidlo("FA-NENI")
	do $$$AssertEquals(sablona.templateId, "", "neexistující šablona: ne výchozí M-line")
	do $$$AssertTrue(sablona.duvod [ "Vybraná šablona pro tisk v databázi není", "neexistující šablona: "_sablona.duvod)
	set sablona = ..Pravidlo("FA-VYV")
	do $$$AssertEquals(sablona.templateId, "", "šablona jiné agendy: ne výchozí M-line")
	do $$$AssertTrue(sablona.duvod [ "patří jiné agendě", "šablona jiné agendy: "_sablona.duvod)
}

/// Neznámá agenda je chyba volajícího, ne důvod pro uživatele.
Method TestNeznamaAgendaJeChyba()
{
	set chyba = ""
	try { do ##class(ALVA.TemplateDesigner.API.SablonyProTisk).SablonaProTisk(..#Firma, "NEEXISTUJE", "", ..#Mline) }
	catch ex { set chyba = ex.DisplayString() }
	do $$$AssertTrue(chyba [ "Neznámá agenda", "neznámá agenda: "_chyba)
}

/// Smazaná výchozí šablona firmy: mazání ukazatel uklidí a pravidlo spadne na M-line.
Method TestSmazanaVychoziFirmy()
{
	do ..Uloz(..#Mline, ..VychoziMlineId(), "A4 poznámky pod", ..#Agenda)
	do ..Uloz(..#Firma, "FA-1", "Naše", ..#Agenda)
	do ##class(ALVA.TemplateDesigner.API.SablonyProTisk).WriteVychoziFirmy(..#Firma, ..#Agenda, "FA-1", ..#Mline)

	kill ^||ML
	do ..SetupSys(.sys, ..#Firma)
	set ^||ML("IN","templateId") = "FA-1"
	do ##class(ALVA.TemplateDesigner.API.Sablony).DeleteTemplate(.sys)
	do $$$AssertEquals($data(^ALVA.TemplateDesigner.Templates(..#Firma,"DEFAULT",..#Agenda)), 0, "mazání uklidí výchozí šablonu firmy")
	do $$$AssertEquals(..Pravidlo("").zdroj, "mline", "po smazání výchozí firmy M-line")

	// Ukazatel, který mazání neuklidilo (starší server), se jen přeskočí.
	set ^ALVA.TemplateDesigner.Templates(..#Firma,"DEFAULT",..#Agenda) = "FA-ZMIZELA"
	do $$$AssertEquals(..Pravidlo("").zdroj, "mline", "neplatný ukazatel výchozí firmy se přeskočí")
}

/// Výchozí šablona firmy smí být jen existující šablona téže agendy.
Method TestVychoziFirmyJenPlatnaSablona()
{
	do ..Uloz(..#Firma, "FA-VYV", "Vývěska", "ZASTAVKOVY_JR_AD")
	set chyba = ""
	try { do ##class(ALVA.TemplateDesigner.API.SablonyProTisk).WriteVychoziFirmy(..#Firma, ..#Agenda, "FA-VYV", ..#Mline) }
	catch ex { set chyba = ex.DisplayString() }
	do $$$AssertTrue(chyba [ "výchozí být nemůže", "šablona jiné agendy výchozí být nemůže: "_chyba)
	do $$$AssertEquals($data(^ALVA.TemplateDesigner.Templates(..#Firma,"DEFAULT",..#Agenda)), 0, "nic se nezapsalo")
}

/// Formulář před tiskem: vlastní šablony a pak M-line, obojí podle jména, výchozí označená.
Method TestSeznamProTisk()
{
	do ..Uloz(..#Mline, ..VychoziMlineId(), "A4 poznámky pod", ..#Agenda)
	do ..Uloz(..#Mline, "ML-JINA", "A5 postskript", ..#Agenda)
	do ..Uloz(..#Firma, "FA-2", "Zelená", ..#Agenda)
	do ..Uloz(..#Firma, "FA-1", "Modrá", ..#Agenda)
	do ..Uloz(..#Firma, "FA-VYV", "Vývěska", "ZASTAVKOVY_JR_AD")

	set seznam = ##class(ALVA.TemplateDesigner.API.SablonyProTisk).SeznamProTisk(..#Firma, ..#Agenda, ..#Mline)
	do $$$AssertEquals(seznam.%Size(), 4, "jen šablony agendy z obou větví")
	do $$$AssertEquals(..Popis(seznam), "Modrá/vlastní,Zelená/vlastní,A4 poznámky pod/M-line*,A5 postskript/M-line", "pořadí a výchozí bez volby firmy")

	do ##class(ALVA.TemplateDesigner.API.SablonyProTisk).WriteVychoziFirmy(..#Firma, ..#Agenda, "FA-2", ..#Mline)
	set seznam = ##class(ALVA.TemplateDesigner.API.SablonyProTisk).SeznamProTisk(..#Firma, ..#Agenda, ..#Mline)
	do $$$AssertEquals(..Popis(seznam), "Modrá/vlastní,Zelená/vlastní*,A4 poznámky pod/M-line,A5 postskript/M-line", "výchozí firmy označená")
}

/// Bez jediné šablony agendy je seznam prázdný, ne chyba - formulář ukáže prázdnou nabídku.
Method TestSeznamProTiskBezSablon()
{
	set seznam = ##class(ALVA.TemplateDesigner.API.SablonyProTisk).SeznamProTisk(..#Firma, ..#Agenda, ..#Mline)
	do $$$AssertEquals(seznam.%Size(), 0, "prázdný seznam")
}

/// API cesta (volá ji spuštění návrháře): výchozí firmy nastavená přes API a vrácená v OUT.
Method TestApiNastaveniAVyber()
{
	do ..Uloz(..#Firma, "FA-1", "Naše", ..#Agenda)

	kill ^||ML
	do ..SetupSys(.sys, ..#Firma)
	set ^||ML("IN","agendaCode") = ..#Agenda
	set ^||ML("IN","templateId") = "FA-1"
	do ##class(ALVA.TemplateDesigner.API.SablonyProTisk).SetDefaultTemplate(.sys)
	do $$$AssertEquals($get(^ALVA.TemplateDesigner.Templates(..#Firma,"DEFAULT",..#Agenda)), "FA-1", "SetDefaultTemplate zapíše výchozí firmy")

	kill ^||ML
	do ..SetupSys(.sys, ..#Firma)
	set ^||ML("IN","agendaCode") = ..#Agenda
	do ##class(ALVA.TemplateDesigner.API.SablonyProTisk).GetTemplateForPrint(.sys)
	do $$$AssertEquals($get(^||ML("OUT","templateId")), "FA-1", "GetTemplateForPrint: templateId")
	do $$$AssertEquals($get(^||ML("OUT","sourceIdFa")), ..#Firma, "GetTemplateForPrint: sourceIdFa")
	do $$$AssertEquals($get(^||ML("OUT","name")), "Naše", "GetTemplateForPrint: name")
	do $$$AssertEquals($get(^||ML("OUT","zdroj")), "firma", "GetTemplateForPrint: zdroj")

	kill ^||ML
	do ..SetupSys(.sys, ..#Firma)
	set ^||ML("IN","agendaCode") = ..#Agenda
	set ^||ML("IN","templateId") = ""
	do ##class(ALVA.TemplateDesigner.API.SablonyProTisk).SetDefaultTemplate(.sys)
	do $$$AssertEquals($data(^ALVA.TemplateDesigner.Templates(..#Firma,"DEFAULT",..#Agenda)), 0, "prázdné templateId výchozí zruší")

	kill ^||ML
	do ..SetupSys(.sys, ..#Firma)
	set ^||ML("IN","agendaCode") = "NEEXISTUJE"
	do ##class(ALVA.TemplateDesigner.API.SablonyProTisk).GetTemplateForPrint(.sys)
	do $$$AssertEquals($data(^||ML("OUT","templateId")), 0, "neznámá agenda: bez výsledku (chyba)")
}

/// Pravidlo nad testovacími větvemi.
Method Pravidlo(templateId As %String) As %DynamicObject
{
	quit ##class(ALVA.TemplateDesigner.API.SablonyProTisk).SablonaProTisk(..#Firma, ..#Agenda, templateId, ..#Mline)
}

Method VychoziMlineId() As %String
{
	quit $parameter("ALVA.TemplateDesigner.Agendy.LinkovyJrAd", "VychoziSablonaMline")
}

/// „jméno/zdroj" za každou položku, výchozí s hvězdičkou.
Method Popis(seznam As %DynamicArray) As %String
{
	set popis = ""
	set iter = seznam.%GetIterator()
	while iter.%GetNext(.i, .polozka) {
		set popis = popis_$select(popis="":"", 1:",")_polozka.name_"/"_polozka.zdroj_$select(polozka.vychozi:"*", 1:"")
	}
	quit popis
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
