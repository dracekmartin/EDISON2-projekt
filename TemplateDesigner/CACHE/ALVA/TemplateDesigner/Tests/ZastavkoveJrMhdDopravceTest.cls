Include %occInclude

/// `B194a` C5: vývěska MHD posílá samotný **název dopravce** jako vývěska AD. Výchozí šablona ho dává
/// do úzkého rámečku záhlaví; do 24. 10. tam stál celý popis s adresou a telefonem a pod názvem linky
/// znovu.
Class ALVA.TemplateDesigner.Tests.ZastavkoveJrMhdDopravceTest Extends ALVA.EDTesting.MLTestCase
{

Method TestNazevDopravceVNabidceDat()
{
	do ..SetupSys(.sys)
	kill ^||ML
	do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd).GetSchema(.sys)
	do $$$AssertTrue($get(^||ML("OUT",1)) [ """NAZEV_DOPRAVCE""", "nabídka dat s názvem dopravce")
}

/// Název je první řádek popisu dopravce - oba pole jdou z týchž údajů, nemohou se rozejít.
Method TestNazevDopravceJePrvniRadekPopisu()
{
	if '##class(%SYS.Namespace).Exists("KV_DP") {
		do $$$AssertSkipped("firma KV_DP není na serveru")
		quit
	}
	do ..SetupSys(.sys, "KV_DP")
	set sys("U","DATA") = "KV_DP"
	kill ^||ML
	set ^||ML("IN","LINKA_ID") = 1073
	do ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd).GetData(.sys)
	set hodnoty = ##class(%DynamicObject).%FromJSON(^||ML("OUT",1)).values
	set popis = hodnoty.%Get("POPIS_DOPRAVCE")
	if popis = "" {
		do $$$AssertSkipped("firemní údaje KV_DP nejsou v systémovém namespace testu")
		quit
	}
	do $$$AssertEquals(hodnoty.%Get("NAZEV_DOPRAVCE"), $piece(popis, $c(10), 1), "název = první řádek popisu: "_popis)
	do $$$AssertTrue(hodnoty.%Get("NAZEV_DOPRAVCE") '[ $c(10), "název bez adresy")
}

}
