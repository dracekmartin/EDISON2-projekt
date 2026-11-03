Include %occInclude

/// %UnitTest pro posílání šablon jiným firmám (`API.SdileniSablon`, `B189`) - část bez ISYCloudu: co se pošle
/// a co ne. Spojení s ISYCloudem tu není, celý tok se ověřuje proti testovacímu ISYCloudu.
/// Firma = větev "EDTESTING", M-line = větev "EDTESTINGML", jako `SablonyProTiskTest`.
Class ALVA.TemplateDesigner.Tests.SdileniSablonTest Extends ALVA.EDTesting.MLTestCase
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

/// Výběr typů provozu linkového JŘ MHD má v názvu číslo typu firmy - příjemci by vypnul jiný typ.
/// Ostatní uložené volby šablona nese dál.
Method TestHodnotyFirmySeNeposilaji()
{
	do ..Uloz(..#Firma, "FA-MHD", "Linky MHD", "LINKOVY_JR_MHD", "{""TYP_PROVOZU_3"":""false"",""TYP_PROVOZU_12"":""true"",""SMER_TISKU"":""Oba směry""}")
	set json = ##class(ALVA.TemplateDesigner.API.SdileniSablon).ObsahKOdeslani(..#Firma, "FA-MHD")
	do $$$AssertEquals(json.printParameters.%IsDefined("TYP_PROVOZU_3"), 0, "typ provozu firmy se nepošle")
	do $$$AssertEquals(json.printParameters.%IsDefined("TYP_PROVOZU_12"), 0, "ani jiný typ provozu")
	do $$$AssertEquals(json.printParameters.%Get("SMER_TISKU"), "Oba směry", "ostatní volba zůstala")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.API.SablonyProTisk).SablonaJson(..#Firma, "FA-MHD").printParameters.%IsDefined("TYP_PROVOZU_3"), 1, "uložená šablona se nemění")
}

/// Agenda bez hodnot platných jen ve firmě pošle šablonu, jak je.
Method TestAgendaBezHodnotFirmy()
{
	do ..Uloz(..#Firma, "FA-AD", "Linky AD", "LINKOVY_JR_AD", "{""TYP_PROVOZU_3"":""false"",""SMER_TISKU"":""Tam""}")
	set json = ##class(ALVA.TemplateDesigner.API.SdileniSablon).ObsahKOdeslani(..#Firma, "FA-AD")
	do $$$AssertEquals(json.printParameters.%Get("TYP_PROVOZU_3"), "false", "linkový JŘ AD předponu nemá")
	do $$$AssertEquals(json.name, "Linky AD", "jméno šablony")
}

/// Šablonu M-line mají všechny firmy, poslat ji nejde.
Method TestSablonaMlineSeNeposila()
{
	do ..Uloz(..#Mline, "ML-1", "Šablona M-line", "LINKOVY_JR_AD")
	set chyba = ""
	try { do ##class(ALVA.TemplateDesigner.API.SdileniSablon).ObsahKOdeslani(..#Firma, "ML-1") }
	catch ex { set chyba = ex.DisplayString() }
	do $$$AssertTrue(chyba [ "jen vlastní šablonu firmy", "šablona M-line: "_chyba)
}

Method TestNazevZasilky()
{
	for i = 1:1:5 set sablony(i) = {"name": ("Šablona "_i)}
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.API.SdileniSablon).NazevZasilky(.sablony, 2, 2), "Šablona 2", "jedna šablona: její jméno")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.API.SdileniSablon).NazevZasilky(.sablony, 1, 3), "3 šablony", "tři")
	do $$$AssertEquals(##class(ALVA.TemplateDesigner.API.SdileniSablon).NazevZasilky(.sablony, 1, 5), "5 šablon", "pět")
}

/// Bez příjemce nebo bez šablony se nic neposílá a ISYCloud se nevolá.
Method TestOdeslaniBezVyberu()
{
	do $$$AssertTrue(..Odeslat("FA-1", "") [ "Vyberte firmu", "bez příjemce")
	do $$$AssertTrue(..Odeslat(" , ", "CB_JINA") [ "Vyberte šablonu", "bez šablony")
}

/// Okno šablon se podle stromu rozhodne, jestli nabídne posílání šablon.
Method TestStromPosilaSdileni()
{
	kill ^||ML
	do ..SetupSys(.sys, ..#Firma)
	do ##class(ALVA.TemplateDesigner.API.StromSablon).GetStrom(.sys)
	set strom = ##class(%DynamicObject).%FromJSON($get(^||ML("OUT","1")))
	do $$$AssertEquals(strom.%GetTypeOf("sdileni"), "boolean", "GetStrom posílá sdileni jako ano/ne")
	kill ^||ML
}

/// Chyba odeslání; "" když prošlo.
Method Odeslat(templateIds As %String, prijemci As %String) As %String
{
	kill ^||ML
	do ..SetupSys(.sys, ..#Firma)
	set ^||ML("IN","templateIds") = templateIds
	set ^||ML("IN","prijemci") = prijemci
	do ##class(ALVA.TemplateDesigner.API.SdileniSablon).SendSablony(.sys)
	set chyba = $get(^||ML("ERR"))
	kill ^||ML
	quit chyba
}

Method Uloz(vetev As %String, templateId As %String, name As %String, agendaCode As %String, printParameters As %String = "{}")
{
	kill ^||ML
	do ..SetupSys(.sys, ..#Firma)
	set ^||ML("IN","templateId") = templateId
	set ^||ML("IN","name")       = name
	set ^||ML("IN","agendaCode") = agendaCode
	set ^||ML("IN","targetIdFa") = vetev
	set ^||ML("IN","data")       = "{""version"":""1.0.0"",""id"":"""_templateId_""",""name"":"""_name_""",""agendaCode"":"""_agendaCode_""",""printParameters"":"_printParameters_",""sections"":[]}"
	do ##class(ALVA.TemplateDesigner.API.Sablony).SaveTemplate(.sys)
	do $$$AssertEquals($get(^||ML("OUT","templateId")), templateId, "uložení testovací šablony "_templateId)
	kill ^||ML
}

}
