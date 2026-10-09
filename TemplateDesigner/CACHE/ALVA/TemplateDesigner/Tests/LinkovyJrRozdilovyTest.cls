Include %occInclude

/// `B192`: rozdílový linkový JŘ slučuje spoje dvou verzí linky. Stránky jsou syntetické (tvar
/// výstupu linkového JŘ), takže test nečte data žádné firmy.
Class ALVA.TemplateDesigner.Tests.LinkovyJrRozdilovyTest Extends ALVA.EDTesting.MLTestCase
{

Parameter AGENDA = "ALVA.TemplateDesigner.Agendy.LinkovyJrAdRozdilovy";

/// Stránka se třemi zastávkami a zadanými spoji: spoje(n) = "číslo|čas1,čas2,čas3".
ClassMethod Stranka(ByRef spoje, zastavky As %String = "A,B,C") As %DynamicObject
{
	set stranka = {"values":{"SMER":"T","DATUM_PLATNOSTI_OD":"1.1.2026","POPIS_PLATNOSTI":"Platí od 1.1.2026"},"rows":{}}
	set radky = stranka.%Get("rows")
	set zast = []
	for i=1:1:$length(zastavky,",") {
		do zast.%Push({"CZST":($piece(zastavky,",",i)),"TYP":"zastavka"})
	}
	do radky.%Set("ZASTAVKY", zast)
	set arr = []
	for i=1:1:$get(spoje) {
		set casy = []
		set texty = $piece(spoje(i),"|",2)
		for j=1:1:$length(texty,",") { do casy.%Push($piece(texty,",",j)) }
		do arr.%Push({"CISLO_SPOJE":(+$piece(spoje(i),"|",1)),"SKUPINA":1,"SMER":"T","CASY":(casy)})
	}
	do radky.%Set("SPOJE", arr)
	do radky.%Set("POZNAMKY", [])
	quit stranka
}

/// Spoje výsledku jako „číslo:verze" oddělené čárkou.
ClassMethod Prehled(stranka As %DynamicObject) As %String
{
	set out = ""
	set it = stranka.%Get("rows").%Get("SPOJE").%GetIterator()
	while it.%GetNext(.k, .spoj) {
		set out = out_$select(out="":"",1:",")_spoj.%Get("CISLO_SPOJE")_":"_spoj.%Get("VERZE")
	}
	quit out
}

/// Změněný spoj stojí dvakrát vedle sebe - původní před novým - a odlišná je jen buňka, která se
/// změnila. Spoj beze změny je jednou a nic nenese.
Method TestZmenenySpojJeDvakrat()
{
	set puv = 2, puv(1) = "1|6:00,6:10,6:20", puv(2) = "2|7:00,7:10,7:20"
	set nov = 2, nov(1) = "1|6:00,6:10,6:20", nov(2) = "2|7:00,7:12,7:20"
	set vysledek = $classmethod(..#AGENDA, "SlucStranky", ..Stranka(.nov), ..Stranka(.puv))

	do $$$AssertEquals(..Prehled(vysledek), "1:,2:puvodni,2:nova", "změněný spoj dvakrát, původní první")
	set spoje = vysledek.%Get("rows").%Get("SPOJE")
	do $$$AssertEquals(spoje.%Get(1).%Get("ROZDILY").%ToJSON(), "[0,1,0]", "původní: odlišná jen druhá zastávka")
	do $$$AssertEquals(spoje.%Get(2).%Get("ROZDILY").%ToJSON(), "[0,1,0]", "nová: odlišná jen druhá zastávka")
	do $$$AssertEquals(spoje.%Get(1).%Get("CASY").%Get(1), "7:10", "původní nese své časy")
	do $$$AssertEquals(vysledek.%Get("values").%Get("PUVODNI_POPIS_PLATNOSTI"), "Platí od 1.1.2026", "platnost původní verze")
}

/// Nový spoj je odlišný v každé buňce, ve které něco je.
Method TestNovySpojCelyOdlisny()
{
	set puv = 1, puv(1) = "1|6:00,6:10,6:20"
	set nov = 2, nov(1) = "1|6:00,6:10,6:20", nov(2) = "5|8:00,,8:20"
	set vysledek = $classmethod(..#AGENDA, "SlucStranky", ..Stranka(.nov), ..Stranka(.puv))

	do $$$AssertEquals(..Prehled(vysledek), "1:,5:nova", "nový spoj jednou, s verzí nová")
	do $$$AssertEquals(vysledek.%Get("rows").%Get("SPOJE").%Get(1).%Get("ROZDILY").%ToJSON(), "[1,0,1]", "prázdná buňka odlišná není")
}

/// Spoj, který v nové verzi chybí, se nekreslí - jen vypíše v hodnotě i v poznámce.
Method TestZrusenySpojSeVypise()
{
	set puv = 2, puv(1) = "1|6:00,6:10,6:20", puv(2) = "3|9:00,9:10,9:20"
	set nov = 1, nov(1) = "1|6:00,6:10,6:20"
	set vysledek = $classmethod(..#AGENDA, "SlucStranky", ..Stranka(.nov), ..Stranka(.puv))

	do $$$AssertEquals(..Prehled(vysledek), "1:", "zrušený spoj v tabulce není")
	do $$$AssertEquals(vysledek.%Get("values").%Get("ZRUSENE_SPOJE"), "3", "zrušený spoj v hodnotě")
	set pozn = vysledek.%Get("rows").%Get("POZNAMKY")
	do $$$AssertEquals(pozn.%Size(), 1, "jedna poznámka")
	do $$$AssertEquals(pozn.%Get(0).%Get("TEXT"), "Zrušené spoje: 3", "text poznámky")
}

/// Verze s jinými zastávkami se zatím neporovnávají - srozumitelná chyba místo nesmyslných řádků.
Method TestJinaTrasaJeChyba()
{
	set puv = 1, puv(1) = "1|6:00,6:10,6:20"
	set nov = 1, nov(1) = "1|6:00,6:10,6:20"
	set chyba = ""
	try {
		do $classmethod(..#AGENDA, "SlucStranky", ..Stranka(.nov, "A,B,D"), ..Stranka(.puv))
	} catch ex {
		set chyba = ex.DisplayString()
	}
	do $$$AssertTrue(chyba [ "liší v zastávkách", "chyba říká, co je špatně: "_chyba)
}

}
