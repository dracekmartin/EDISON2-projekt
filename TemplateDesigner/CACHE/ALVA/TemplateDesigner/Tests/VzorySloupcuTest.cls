Include %occInclude

/// Vzory rozložení sloupců: klient bere popisek a klíč z voleb parametru „Rozdělení sloupců" (`optionKeys`)
/// a obsah z `PRESETY_SLOUPCU` v datech. Obojí posílá agenda, takže se musí shodovat: vzor bez volby by se
/// v editoru nenabídl, volba bez vzoru by v náhledu nic nepřepnula. Do 30. 10. měl popisky opsané klient.
Class ALVA.TemplateDesigner.Tests.VzorySloupcuTest Extends ALVA.EDTesting.MLTestCase
{

/// Namespace se zlatými daty AD (jiný server → přeskočeno).
Parameter GOLDDB = "CB_CSADAD";

Method TestVyveskaMaKeKazdemuVzoruVolbu()
{
	set db = ..#GOLDDB
	if '##class(%SYS.Namespace).Exists(db) {
		do $$$AssertSkipped("namespace "_db_" není na tomto serveru")
		quit
	}
	set data = ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).PresetySloupcu(db, 1, "0365001")
	do ..AssertStejneKlice(data, ##class(ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd).VzorySloupcu(), "vývěska AD")
}

Method TestLinkaMaKeKazdemuVzoruVolbu()
{
	set db = ..#GOLDDB
	if '##class(%SYS.Namespace).Exists(db) {
		do $$$AssertSkipped("namespace "_db_" není na tomto serveru")
		quit
	}
	set data = ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).PresetySloupcuLinky(db, "CZ", 1, "0330510")
	do ..AssertStejneKlice(data, ##class(ALVA.TemplateDesigner.Agendy.LinkovyJrAd).VzorySloupcuLinky(), "linkový JŘ AD")
}

/// Klíče vzorů v datech (`PRESETY_SLOUPCU`) a klíče voleb parametru musí být tytéž.
Method AssertStejneKlice(dataJson As %String, vzory As %List, agenda As %String)
{
	set data = ##class(%DynamicObject).%FromJSON(dataJson)
	set iter = data.%GetIterator()
	while iter.%GetNext(.klic, .hodnota) {
		set vData(klic) = ""
	}
	for i=1:1:$listlength(vzory) {
		set klic = $list($list(vzory,i),1)
		set vVolbach(klic) = ""
		do $$$AssertTrue($data(vData(klic)), agenda_": volba s klíčem "_klic_" nemá vzor v PRESETY_SLOUPCU")
	}
	set klic = ""
	for {
		set klic = $order(vData(klic))
		quit:klic=""
		do $$$AssertTrue($data(vVolbach(klic)), agenda_": vzor "_klic_" z PRESETY_SLOUPCU nemá volbu v parametru")
	}
}

}
