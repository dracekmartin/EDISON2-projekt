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

/// Tatáž volba rozdělení sloupců se ve vývěsce i v linkovém JŘ AD jmenuje stejně (`P2`): popisek bere
/// `Jr.Parametry.PopisekVzoruSloupcu` podle klíče. Uložené texty voleb zůstávají, jak byly - ukládají se,
/// takže jejich změna by uživatelům tiše vrátila volbu na výchozí.
Method TestStejnePopiskyVObouAgendach()
{
	set vyveska = ..ParametrAgendy("ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd", "ROZDELENI_SLOUPCU")
	set linka = ..ParametrAgendy("ALVA.TemplateDesigner.Agendy.LinkovyJrAd", "DELENI_SLOUPCU")
	if '$isobject(vyveska) || '$isobject(linka) {
		do $$$AssertTrue(0, "vývěska i linkový JŘ AD posílají parametr rozdělení sloupců")
		quit
	}
	do ..PopiskyPodleKlice(vyveska, .vPopisky, "vývěska AD")
	do ..PopiskyPodleKlice(linka, .lPopisky, "linkový JŘ AD")
	set spolecne = 0, klic = ""
	for {
		set klic = $order(vPopisky(klic)) quit:klic=""
		continue:'$data(lPopisky(klic))
		set spolecne = spolecne + 1
		do $$$AssertEquals(lPopisky(klic), vPopisky(klic), "volba "_klic_" má v obou agendách stejný popisek")
	}
	do $$$AssertEquals(spolecne, 2, "volby, které mají obě agendy (PRAC_SO_NE, PRAC_VIKEND)")
	do $$$AssertEquals(..TextyVoleb(vyveska), "Automaticky (dle linky)|Pracovní / Sobota / Neděle+svátky|Pracovní / víkend (So+Ne+svátky)|Prázdniny: Prac. mimo+o prázd. / víkend|Prázdniny: Prac. mimo+o prázd. / Sobota / Neděle+svátky|Vlastní rozložení", "uložené texty voleb vývěsky beze změny")
	do $$$AssertEquals(..TextyVoleb(linka), "Bez rozlišení|Pracovní dny / víkend+svátky|Pracovní dny / Soboty / Neděle+svátky|Vlastní rozložení", "uložené texty voleb linkového JŘ beze změny")
}

/// Parametr `nazev` z `GetParameters` agendy; "" = agenda ho neposílá.
Method ParametrAgendy(trida As %String, nazev As %String) As %DynamicObject
{
	do ..SetupSys(.sys)
	kill ^||ML("OUT")
	do $classmethod(trida, "GetParameters", .sys)
	set parametry = ##class(%DynamicAbstractObject).%FromJSON($get(^||ML("OUT",1)))
	set iter = parametry.%GetIterator()
	while iter.%GetNext(.i, .p) { return:p.name=nazev p }
	quit ""
}

/// `popisky(klíč vzoru)` = zobrazovaný popisek; každá volba musí mít klíč i popisek na stejném místě.
Method PopiskyPodleKlice(parametr As %DynamicObject, ByRef popisky, agenda As %String)
{
	kill popisky
	set volby = parametr.options, klice = parametr.optionKeys, nazvy = parametr.optionLabels
	do $$$AssertTrue($isobject(klice) && $isobject(nazvy), agenda_": posílá optionKeys i optionLabels")
	quit:'$isobject(klice)||'$isobject(nazvy)
	do $$$AssertEquals(nazvy.%Size(), volby.%Size(), agenda_": popisek ke každé volbě")
	do $$$AssertEquals(klice.%Size(), volby.%Size(), agenda_": klíč ke každé volbě")
	for i=0:1:volby.%Size()-1 {
		do $$$AssertTrue(nazvy.%Get(i) '= "", agenda_": volba "_volby.%Get(i)_" má popisek")
		set:klice.%Get(i)'="" popisky(klice.%Get(i)) = nazvy.%Get(i)
	}
}

/// Uložené texty voleb oddělené `|`.
ClassMethod TextyVoleb(parametr As %DynamicObject) As %String
{
	set s = ""
	for i=0:1:parametr.options.%Size()-1 set s = s_$select(i=0:"", 1:"|")_parametr.options.%Get(i)
	quit s
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
