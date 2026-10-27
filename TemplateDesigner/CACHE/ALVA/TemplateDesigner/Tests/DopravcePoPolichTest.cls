Include %occInclude

/// `B194c` D4: údaje dopravce po jednotlivých polích nabízí každá agenda JŘ pod stejnými klíči
/// a popisky - šablona si z nich poskládá, co chce, a jde přenést mezi agendami. Do 27. 10. je měla
/// jen vývěska AD (a vývěska MHD název).
Class ALVA.TemplateDesigner.Tests.DopravcePoPolichTest Extends ALVA.EDTesting.MLTestCase
{

Parameter POLE = "NAZEV_DOPRAVCE:Název dopravce,ADRESA_DOPRAVCE:Adresa dopravce,TELEFON_DOPRAVCE:Telefon dopravce,EMAIL_DOPRAVCE:E-mail dopravce,ICO_DOPRAVCE:IČO dopravce";

Method TestVsechnyAgendyNabizejiStejnaPole()
{
	do ..SetupSys(.sys)
	for agenda = "ZastavkoveJrAd", "LinkovyJrAd", "ZastavkoveJrMhd", "LinkovyJrMhd" {
		kill ^||ML
		do $classmethod("ALVA.TemplateDesigner.Agendy."_agenda, "GetSchema", .sys)
		set it = ##class(%DynamicObject).%FromJSON($get(^||ML("OUT",1))).fields.%GetIterator()
		kill popisky
		while it.%GetNext(, .f) { set popisky(f.name) = f.label }
		for i=1:1:$length(..#POLE, ",") {
			set klic = $piece($piece(..#POLE, ",", i), ":", 1), popisek = $piece($piece(..#POLE, ",", i), ":", 2)
			do $$$AssertEquals($get(popisky(klic)), popisek, agenda_": "_klic)
		}
	}
}

/// Vlastní firma MHD nemá kontaktní e-mail: jediný na kartě firmy je odesílací adresa EDISONu
/// („no-reply@…"), cestujícím by se vytiskla (rozhodnutí Jan 27. 10.). Ostatní údaje karta má.
Method TestVlastniFirmaMhdBezEmailu()
{
	if '##class(%SYS.Namespace).Exists("KV_DP") {
		do $$$AssertSkipped("firma KV_DP není na serveru")
		quit
	}
	do ..SetupSys(.sys, "KV_DP")
	set sys("U","DATA") = "KV_DP"
	for agenda = "ZastavkoveJrMhd", "LinkovyJrMhd" {
		kill ^||ML
		set ^||ML("IN","LINKA_ID") = 1073
		do $classmethod("ALVA.TemplateDesigner.Agendy."_agenda, "GetData", .sys)
		set hodnoty = ##class(%DynamicObject).%FromJSON(^||ML("OUT",1)).values
		if hodnoty.%Get("NAZEV_DOPRAVCE") = "" {
			do $$$AssertSkipped("firemní údaje KV_DP nejsou v systémovém namespace testu")
			quit
		}
		do $$$AssertTrue(hodnoty.%IsDefined("EMAIL_DOPRAVCE"), agenda_": pole e-mailu v datech je")
		do $$$AssertEquals(hodnoty.%Get("EMAIL_DOPRAVCE"), "", agenda_": e-mail vlastní firmy prázdný")
		do $$$AssertNotEquals(hodnoty.%Get("ICO_DOPRAVCE"), "", agenda_": IČO z karty firmy")
		do $$$AssertTrue(hodnoty.%Get("ADRESA_DOPRAVCE") [ ", ", agenda_": adresa ulice, PSČ obec: "_hodnoty.%Get("ADRESA_DOPRAVCE"))
	}
}

}
