Include %occInclude

/// %UnitTest pro výběr linek (`ALVA.TemplateDesigner.EDList.Instance`) - **seznam se nesmí odříznout**.
///
/// ⚠️ Do 3. 9. posílal dispatch `limit = 200` a hledací pole v dialogu filtrovalo jen načtenou
/// stránku, takže linka za dvoustovkou se nenašla a vypadalo to, že v datech není (`B103`, nález
/// Jan 1. 9. na `380761` v CB_CSADAD skupině 1 - je 233. v seznamu). Skupina má reálně stovky
/// linek, ne tisíce, takže se načítá celá.
///
/// Testy běží proti **živým firemním datům** (jiná data než ostatní sady), a proto se přeskočí,
/// když firma na serveru není - nesmí padat kolegům kvůli tomu, co na jejich stroji chybí.
Class ALVA.TemplateDesigner.Tests.VyberLinekTest Extends ALVA.EDTesting.MLTestCase
{

/// Firma s dost linkami, aby se odříznutí projevilo (naměřeno 3. 9.: skupina 1 má 240 linek).
Parameter TESTDB = "CB_CSADAD";

Method OnBeforeOneTest(testname As %String) As %Status
{
	kill ^||ML
	quit $$$OK
}

/// Kolik linek skupina reálně má - přímo z `^JRL`, nezávisle na dispatchi.
ClassMethod PocetLinekVeSkupine(db As %String, sku As %Integer) As %Integer
{
	set pocet = 0, lin = ""
	for {
		set lin = $order(^[db]JRL(sku,lin))
		quit:lin=""
		continue:'$data(^[db]JRL(sku,lin,"I"))
		set pocet = pocet + 1
	}
	quit pocet
}

/// Výběr linek vrátí **celou skupinu**, ne prvních 200.
Method TestVyberVratiCelouSkupinu()
{
	if '##class(%SYS.Namespace).Exists(..#TESTDB) {
		do $$$AssertTrue(1, "namespace "_..#TESTDB_" na tomto serveru není - přeskočeno")
		quit
	}

	set ocekavano = ..PocetLinekVeSkupine(..#TESTDB, 1)
	do $$$AssertTrue(ocekavano > 200, "testovací skupina má mít víc než 200 linek, aby test něco měřil (má "_ocekavano_")")

	do ..SetupSys(.sys, ..#TESTDB)
	set sys("U","DATA") = ..#TESTDB      // bez toho čte dispatch výchozí namespace, kde linky nejsou
	set ^||ML("IN","filter","agendaCode") = "ZASTAVKOVY_JR_AD"
	set ^||ML("IN","filter","SKUPINA_LINEK_ID") = 1
	do ##class(ALVA.TemplateDesigner.EDList.Instance).LoadList(.sys)

	// Řádky jsou v OUT pod číselnými klíči; "ERR" a případné metadatové klíče se nepočítají.
	set vraceno = 0, key = ""
	for {
		set key = $order(^||ML("OUT",key))
		quit:key=""
		continue:'$isvalidnum(key)
		set vraceno = vraceno + 1
	}

	do $$$AssertEquals(vraceno, ocekavano, "výběr vrátil "_vraceno_" linek, skupina má "_ocekavano_" - seznam je odříznutý")
}

/// Linka z původního nálezu musí být ve výsledku - je až 233. v pořadí.
Method TestLinkaZaDvoustovkouJeVeVyberu()
{
	if '##class(%SYS.Namespace).Exists(..#TESTDB) || '$data(^[..#TESTDB]JRL(1,"0380761","I")) {
		do $$$AssertTrue(1, "linka 0380761 na tomto serveru není - přeskočeno")
		quit
	}

	do ..SetupSys(.sys, ..#TESTDB)
	set sys("U","DATA") = ..#TESTDB      // bez toho čte dispatch výchozí namespace, kde linky nejsou
	set ^||ML("IN","filter","agendaCode") = "ZASTAVKOVY_JR_AD"
	set ^||ML("IN","filter","SKUPINA_LINEK_ID") = 1
	do ##class(ALVA.TemplateDesigner.EDList.Instance).LoadList(.sys)

	set nalezena = 0, key = ""
	for {
		set key = $order(^||ML("OUT",key))
		quit:key=""
		continue:'$isvalidnum(key)
		if $find($get(^||ML("OUT",key)), "380761") set nalezena = 1  quit
	}

	do $$$AssertTrue(nalezena, "linka 380761 (233. ve skupině) ve výběru chybí - seznam je odříznutý")
}

/// Strop zůstává jako pojistka proti chybě v datech, ne jako omezení pro uživatele.
Method TestStropJeRadoveNadRealnymPoctem()
{
	set strop = ##class(ALVA.TemplateDesigner.EDList.Instance).#MAXINSTANCI
	do $$$AssertTrue(strop >= 10000, "strop "_strop_" je moc nízko - naměřené skupiny mají stovky linek, pojistka má být o dva řády výš")
}

}
