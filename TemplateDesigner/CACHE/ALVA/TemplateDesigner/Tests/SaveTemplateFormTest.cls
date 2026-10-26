Include %occInclude

/// `B194a` C10: okno „Uložit jako" agendu šablony jen ukáže. Do 26. 10. nabízelo všechny agendy včetně
/// nezveřejněných a změna uložila šablonu s prvky a poli jedné agendy pod jinou; obecná agenda
/// v nabídce chyběla. Agendu smí měnit jen Změnit agendu, které založí novou šablonu.
Class ALVA.TemplateDesigner.Tests.SaveTemplateFormTest Extends ALVA.EDTesting.MLTestCase
{

/// Řádek pole formuláře z OUT: $lb("FIELD", číslo, klíč, popisek, typ, hodnota, …, upravitelné (10), …,
/// nápověda (12), položky „kód~název|…" (13)).
ClassMethod PoleAgendy() As %List
{
	set n = ""
	for {
		set n = $order(^||ML("OUT", n), 1, radek)
		quit:n=""
		continue:'$listvalid(radek)
		quit:$listget(radek, 3)="agendaCode"
	}
	quit $select(n="":"", 1:radek)
}

Method TestAgendaJenKeCteni()
{
	do ..SetupSys(.sys)
	kill ^||ML
	set ^||ML("IN","name") = "Zkouška", ^||ML("IN","agendaCode") = "ZASTAVKOVY_JR_AD"
	do ##class(ALVA.TemplateDesigner.EDInputForm.SaveTemplate).Init(.sys)
	set pole = ..PoleAgendy()
	do $$$AssertTrue(pole '= "", "pole agendy ve formuláři")
	do $$$AssertEquals($listget(pole, 6), "ZASTAVKOVY_JR_AD", "hodnota = agenda šablony")
	do $$$AssertEquals(+$listget(pole, 10), 0, "pole nejde upravit")
	do $$$AssertEquals($length($listget(pole, 13), "|"), 1, "jediná položka: "_$listget(pole, 13))
	do $$$AssertEquals($piece($listget(pole, 13), "~"), "ZASTAVKOVY_JR_AD", "položka = agenda šablony")
}

/// Obecná agenda v registru agend není - název pošle návrhář.
Method TestObecnaAgendaSNazvemOdKlienta()
{
	do ..SetupSys(.sys)
	kill ^||ML
	set ^||ML("IN","name") = "Zkouška", ^||ML("IN","agendaCode") = "GENERAL", ^||ML("IN","agendaLabel") = "Obecná (bez databáze)"
	do ##class(ALVA.TemplateDesigner.EDInputForm.SaveTemplate).Init(.sys)
	do $$$AssertEquals($listget(..PoleAgendy(), 13), "GENERAL~Obecná (bez databáze)", "obecná agenda s názvem")
}

}
