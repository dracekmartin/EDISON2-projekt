Include %occInclude

/// `B192`: rozdílový linkový JŘ slučuje spoje dvou verzí linky. Stránky jsou syntetické (tvar
/// výstupu linkového JŘ), takže test nečte data žádné firmy.
Class ALVA.TemplateDesigner.Tests.LinkovyJrRozdilovyTest Extends ALVA.EDTesting.MLTestCase
{

Parameter AGENDA = "ALVA.TemplateDesigner.Agendy.LinkovyJrAdRozdilovy";

/// Stránka se třemi zastávkami a zadanými spoji:
/// spoje(n) = "číslo|čas1,čas2,čas3[|skupina dnů[|značky platnosti oddělené ;[|negativní značka]]]"
/// (bez skupiny = 1). Skupiny dnů stránky jsou ty, které spoje používají.
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
	kill skupiny
	for i=1:1:$get(spoje) {
		set casy = []
		set texty = $piece(spoje(i),"|",2)
		for j=1:1:$length(texty,",") { do casy.%Push($piece(texty,",",j)) }
		set skupina = +$piece(spoje(i),"|",3)
		set:skupina=0 skupina = 1
		set skupiny(skupina) = ""
		set spoj = {"CISLO_SPOJE":(+$piece(spoje(i),"|",1)),"SKUPINA":(skupina),"SMER":"T","CASY":(casy)}
		set platnost = [], znacky = $piece(spoje(i),"|",4)
		for j=1:1:$length(znacky,";") { do:$piece(znacky,";",j)'="" platnost.%Push($piece(znacky,";",j)) }
		do spoj.%Set("PLATNOST", platnost), spoj.%Set("NZ", $piece(spoje(i),"|",5))
		do arr.%Push(spoj)
	}
	do radky.%Set("SPOJE", arr)
	set dny = [], skupina = ""
	for {
		set skupina = $order(skupiny(skupina))
		quit:skupina=""
		do dny.%Push({"ID":(skupina),"NADPIS":("Skupina "_skupina)})
	}
	do radky.%Set("SKUPINY_DNU", dny)
	do radky.%Set("POZNAMKY", [])
	quit stranka
}

/// Přidá stránce poznámky: poznamky(n) = "druh|typ|značka|text".
ClassMethod SPoznamkami(stranka As %DynamicObject, ByRef poznamky) As %DynamicObject
{
	set arr = []
	for i=1:1:$get(poznamky) {
		set p = poznamky(i)
		do arr.%Push({"DRUH":($piece(p,"|",1)),"TYP":($piece(p,"|",2)),"ZNAK":($piece(p,"|",3)),"TEXT":($piece(p,"|",4))})
	}
	do stranka.%Get("rows").%Set("POZNAMKY", arr)
	quit stranka
}

/// Poznámky výsledku jako „značka:verze" oddělené čárkou (text bez značky jako „T").
ClassMethod PrehledPoznamek(stranka As %DynamicObject) As %String
{
	set out = ""
	set it = stranka.%Get("rows").%Get("POZNAMKY").%GetIterator()
	while it.%GetNext(.k, .pozn) {
		set znak = pozn.%Get("ZNAK")
		set out = out_$select(out="":"",1:",")_$select(znak="":"T",1:znak)_":"_pozn.%Get("VERZE")
	}
	quit out
}

/// ID skupin dnů výsledku oddělená čárkou.
ClassMethod SkupinyDnu(stranka As %DynamicObject) As %String
{
	set out = ""
	set it = stranka.%Get("rows").%Get("SKUPINY_DNU").%GetIterator()
	while it.%GetNext(.k, .skupina) { set out = out_$select(out="":"",1:",")_skupina.%Get("ID") }
	quit out
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

/// Nový spoj má barvu nové verze celý sloupec, i v buňkách mimo trasu - jako zrušený spoj ve vzoru
/// z CIS JŘ (`B192a`).
Method TestNovySpojCelyZeleny()
{
	set puv = 1, puv(1) = "1|6:00,6:10,6:20"
	set nov = 2, nov(1) = "1|6:00,6:10,6:20", nov(2) = "5|8:00,,8:20"
	set vysledek = $classmethod(..#AGENDA, "SlucStranky", ..Stranka(.nov), ..Stranka(.puv))

	do $$$AssertEquals(..Prehled(vysledek), "1:,5:nova", "nový spoj jednou, s verzí nová")
	do $$$AssertEquals(vysledek.%Get("rows").%Get("SPOJE").%Get(1).%Get("ROZDILY").%ToJSON(), "[1,1,1]", "celý sloupec, i prázdná buňka")
}

/// Zrušený spoj je v tabulce celý v barvě původní verze, na místě, kde byl v původní verzi
/// (`B192a`). Do 16. 10. se jen vypsal do poznámky, ta už nevzniká.
Method TestZrusenySpojStojiNaSvemMiste()
{
	set puv = 3, puv(1) = "1|6:00,6:10,6:20", puv(2) = "3|9:00,,9:20", puv(3) = "5|11:00,11:10,11:20"
	set nov = 2, nov(1) = "1|6:00,6:10,6:20", nov(2) = "5|11:00,11:10,11:20"
	set vysledek = $classmethod(..#AGENDA, "SlucStranky", ..Stranka(.nov), ..Stranka(.puv))

	do $$$AssertEquals(..Prehled(vysledek), "1:,3:puvodni,5:", "zrušený spoj mezi svými sousedy")
	set zruseny = vysledek.%Get("rows").%Get("SPOJE").%Get(1)
	do $$$AssertEquals(zruseny.%Get("ROZDILY").%ToJSON(), "[1,1,1]", "celý sloupec, i prázdná buňka")
	do $$$AssertEquals(zruseny.%Get("CASY").%Get(2), "9:20", "zrušený spoj nese své časy")
	do $$$AssertEquals(vysledek.%Get("values").%Get("ZRUSENE_SPOJE"), "3", "zrušený spoj v hodnotě")
	do $$$AssertEquals(vysledek.%Get("rows").%Get("POZNAMKY").%Size(), 0, "poznámka o zrušených spojích nevzniká")
}

/// Zrušený spoj za změněným stojí až za oběma jeho verzemi - dvojice se nerozdělí.
Method TestZrusenySpojZaDvojici()
{
	set puv = 3, puv(1) = "1|6:00,6:10,6:20", puv(2) = "2|7:00,7:10,7:20", puv(3) = "3|9:00,9:10,9:20"
	set nov = 2, nov(1) = "1|6:00,6:10,6:20", nov(2) = "2|7:00,7:12,7:20"
	set vysledek = $classmethod(..#AGENDA, "SlucStranky", ..Stranka(.nov), ..Stranka(.puv))

	do $$$AssertEquals(..Prehled(vysledek), "1:,2:puvodni,2:nova,3:puvodni", "dvojice pohromadě, zrušený za ní")
}

/// Zrušený spoj, který byl ve své skupině dnů první, stojí před jejím prvním spojem - ne za
/// posledním spojem předchozí skupiny, to by ho prvek nakreslil pod cizím nadpisem.
Method TestZrusenyPrvniSpojSkupinyJdePredNi()
{
	set puv = 3, puv(1) = "1|6:00,6:10,6:20|1", puv(2) = "2|7:00,7:10,7:20|2", puv(3) = "3|9:00,9:10,9:20|2"
	set nov = 2, nov(1) = "1|6:00,6:10,6:20|1", nov(2) = "3|9:00,9:10,9:20|2"
	set vysledek = $classmethod(..#AGENDA, "SlucStranky", ..Stranka(.nov), ..Stranka(.puv))

	do $$$AssertEquals(..Prehled(vysledek), "1:,2:puvodni,3:", "zrušený na začátku své skupiny")
}

/// Skupina dnů, kterou nová verze vůbec nemá, se přidá na konec i se svým nadpisem.
Method TestZrusenaCelaSkupinaDnu()
{
	set puv = 2, puv(1) = "1|6:00,6:10,6:20|1", puv(2) = "2|7:00,7:10,7:20|2"
	set nov = 1, nov(1) = "1|6:00,6:10,6:20|1"
	set vysledek = $classmethod(..#AGENDA, "SlucStranky", ..Stranka(.nov), ..Stranka(.puv))

	do $$$AssertEquals(..Prehled(vysledek), "1:,2:puvodni", "zrušená skupina na konci")
	do $$$AssertEquals(..SkupinyDnu(vysledek), "1,2", "nadpis zrušené skupiny se převezme z původní verze")
}

/// Legenda nese poznámky obou verzí (`B192b`): stejná poznámka jednou beze změny, jiný text téže
/// značky jako dvojice (původní před novou), poznámka jen z jedné verze s její verzí. Původní
/// poznámka, jejíž značka v nové verzi není, jde za poslední poznámku svého druhu.
Method TestLegendaZObouVerzi()
{
	set spoje = 1, spoje(1) = "1|6:00,6:10,6:20"
	set pp = 3, pp(1) = "2|nz|17|jede od 30.6.25", pp(2) = "2|nz|31|nejede 31.12.24", pp(3) = "9|text||Na lince platí tarif IDS."
	set pn = 3, pn(1) = "2|nz|17|jede od 10.7.26", pn(2) = "2|nz|18|jede od 10.7.26 do 28.9.26", pn(3) = "9|text||Na lince platí tarif IDS."
	set puvodni = ..SPoznamkami(..Stranka(.spoje), .pp)
	set nova = ..SPoznamkami(..Stranka(.spoje), .pn)
	set vysledek = $classmethod(..#AGENDA, "SlucStranky", nova, puvodni)

	do $$$AssertEquals(..PrehledPoznamek(vysledek), "17:puvodni,17:nova,18:nova,31:puvodni,T:", "pořadí a verze poznámek")
	do $$$AssertEquals(vysledek.%Get("rows").%Get("POZNAMKY").%Get(0).%Get("TEXT"), "jede od 30.6.25", "původní text značky 17")
}

/// Poznámky stejné v obou verzích zůstanou, jak jsou - bez verze a bez zdvojení.
Method TestStejnaLegendaBezVerzi()
{
	set spoje = 1, spoje(1) = "1|6:00,6:10,6:20"
	set pozn = 2, pozn(1) = "2|nz|17|jede od 10.7.26", pozn(2) = "1|znak|X|jede v pracovních dnech"
	set vysledek = $classmethod(..#AGENDA, "SlucStranky", ..SPoznamkami(..Stranka(.spoje), .pozn), ..SPoznamkami(..Stranka(.spoje), .pozn))

	do $$$AssertEquals(..PrehledPoznamek(vysledek), "17:,X:", "beze změny")
}

/// Spoj se stejnými časy a jinými značkami zůstane jeden sloupec (`B192c`), ale nese i značky
/// původní verze a u každé řekne, ke které verzi patří - jako ve vzoru z CIS JŘ.
Method TestZmenaZnacekSpojZustaneJednou()
{
	set puv = 1, puv(1) = "1|6:00,6:10,6:20|1|X;R|31"
	set nov = 1, nov(1) = "1|6:00,6:10,6:20|1|X;H|"
	set vysledek = $classmethod(..#AGENDA, "SlucStranky", ..Stranka(.nov), ..Stranka(.puv))

	do $$$AssertEquals(..Prehled(vysledek), "1:", "jeden sloupec bez verze")
	set spoj = vysledek.%Get("rows").%Get("SPOJE").%Get(0)
	do $$$AssertEquals(spoj.%Get("PLATNOST").%ToJSON(), "[""X"",""H"",""R""]", "značky obou verzí, původní na konci")
	do $$$AssertEquals(spoj.%Get("PLATNOST_VERZE").%ToJSON(), "["""",""nova"",""puvodni""]", "verze značek")
	do $$$AssertEquals(spoj.%Get("NZ_PUVODNI"), "31", "negativní značka, kterou má jen původní verze")
	do $$$AssertEquals(spoj.%Get("NZ_VERZE"), "", "nová verze negativní značku nemá")
}

/// Změněná negativní značka (38 → 37): nová zůstane v NZ, původní jde vedle ní.
Method TestZmenaNegativniZnacky()
{
	set puv = 1, puv(1) = "1|6:00,6:10,6:20|1|X|38"
	set nov = 1, nov(1) = "1|6:00,6:10,6:20|1|X|37"
	set spoj = $classmethod(..#AGENDA, "SlucStranky", ..Stranka(.nov), ..Stranka(.puv)).%Get("rows").%Get("SPOJE").%Get(0)

	do $$$AssertEquals(spoj.%Get("NZ")_"/"_spoj.%Get("NZ_VERZE")_"/"_spoj.%Get("NZ_PUVODNI"), "37/nova/38", "obě negativní značky s verzí")
	do $$$AssertEquals(spoj.%Get("PLATNOST_VERZE"), "", "značky platnosti se nezměnily, verzi nenesou")
}

/// Spoj se stejnými časy i značkami nic navíc nenese.
Method TestStejneZnackyBezVerze()
{
	set spoje = 1, spoje(1) = "1|6:00,6:10,6:20|1|X;R|31"
	set spoj = $classmethod(..#AGENDA, "SlucStranky", ..Stranka(.spoje), ..Stranka(.spoje)).%Get("rows").%Get("SPOJE").%Get(0)

	do $$$AssertEquals(spoj.%Get("PLATNOST_VERZE")_spoj.%Get("NZ_VERZE")_spoj.%Get("NZ_PUVODNI"), "", "beze změny")
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
