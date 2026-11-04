Include GENERAL

/// Programové spuštění %UnitTest s strukturovaným výstupem.
/// Volá se přes MLRest CVar; používá se z CI/pre-push hooks.
Class ALVA.EDTesting.TestRunner Extends %RegisteredObject
{

/// IN:  package — name nebo prefix (např. "ALVA.EDISONMAIN2.Tests")
/// OUT: passed (count), failed (count), skipped (count), total (count),
///      failures(i, "class"/"method"/"message") pro každý failing test,
///      skips(i, "class"/"method"/"message") pro každý přeskočený ($$$AssertSkipped, message = důvod),
///      tests(i, "class"/"method"/"status"/"duration") pro každou metodu (duration v sekundách),
///      suite (jméno sady, se kterou běh prošel; prázdné = záložní běh přes všechny složky)
ClassMethod RunPackage(ByRef sys As %String) As %Status
{
	$$$Init
	try {
		$$$FillParams

		set package = $get(^||ML("IN","package"))
		if (package = "") { throw ##class(%Exception.General).%New("RunPackage",,,"Missing parameter: package") }

		// %UnitTest.Manager vyžaduje ^UnitTestRoot na existující složku (i s /noload). Nastavuje
		// se natvrdo, ne jen když je prázdný: globál je sdílený pro celý server, takže hodnota
		// po cizím experimentu by tomuhle běhu tiše podstrčila jiný kořen.
		set ^UnitTestRoot = $system.Util.ManagerDirectory()

		set classList = ..FindTestClasses(package)
		if (classList = "") { throw ##class(%Exception.General).%New("RunPackage",,,"No %UnitTest.TestCase subclasses found under "_package) }

		// Třídy, které tenhle běh sám spustil - podle nich se filtrují výsledky.
		kill nase
		set ptr = 0
		while $listnext(classList, ptr, className) { set nase(className) = "" }

		// ⚠️ **Jméno sady drží dobu běhu celé sady.** Složka pod kořenem je pro %UnitTest
		// jednotka „sady" a prázdná část před dvojtečkou znamená „projdi všechny" - třída se
		// pak spustí jednou za každou složku pod kořenem. S kořenem na pracovním adresáři
		// Caché to bylo 14 běhů každého testu; gold sada trvala 126 s místo 8,7 s. Poznat to
		// nešlo: výsledky se v klíči (složka, třída, metoda) přepsaly na tři záznamy, takže
		// jediná stopa byl nepoměr mezi dobou jednoho průchodu a dobou běhu.
		//
		// Které jméno manager uzná, se z API zjistit nedá - vlastní založená složka neprojde
		// ani se souborem uvnitř a vrátí 0/0 bez chyby. Proto se kandidáti zkoušejí; neúspěšný
		// stojí desetinu sekundy. Poslední kandidát je prázdné jméno, tedy původní běh přes
		// všechny složky: pomalý, ale správný, takže runner nikdy neskončí tichým 0/0.
		set kandidati = ..Sady($get(^UnitTestRoot))_$listbuild("")
		set (passed, failed, skipped) = 0
		kill selhani, preskoceno, testy
		set kptr = 0
		while $listnext(kandidati, kptr, sada) {
			set sada = $get(sada)
			kill selhani, preskoceno, testy
			set (passed, failed, skipped) = 0
			do ..SpustSadu(classList, sada, .nase, .passed, .failed, .skipped, .selhani, .preskoceno, .testy)
			quit:(passed+failed+skipped)>0
		}
		// Třídy existují, ale žádný výsledek s naší značkou: zelené 0/0 by schovalo, že se nic nespustilo.
		if ((passed+failed+skipped) = 0) { throw ##class(%Exception.General).%New("RunPackage",,,"No test result of this run found for "_package) }

		set ^||ML("OUT","passed") = passed
		set ^||ML("OUT","failed") = failed
		set ^||ML("OUT","skipped") = skipped
		set ^||ML("OUT","total") = (passed + failed + skipped)
		do ..ZapisVysledky("failures", .selhani)
		do ..ZapisVysledky("skips", .preskoceno)
		do ..ZapisTesty(.testy)
		// Prázdná sada = záložní běh přes všechny složky, každý test běžel mnohokrát.
		set ^||ML("OUT","suite") = $get(sada)
	}
	catch (ex) { $$$CatchErrRest }
	ret ret
}

/// OUT(uzel, i, "class"/"method"/"message") z $listbuild(třída, metoda, hláška).
ClassMethod ZapisVysledky(uzel As %String, ByRef vysledky) [ Internal ]
{
	set i = ""
	for {
		set i = $order(vysledky(i))
		quit:i=""
		set ^||ML("OUT",uzel,i,"class") = $listget(vysledky(i), 1)
		set ^||ML("OUT",uzel,i,"method") = $listget(vysledky(i), 2)
		set ^||ML("OUT",uzel,i,"message") = $listget(vysledky(i), 3)
	}
}

/// OUT("tests", i, "class"/"method"/"status"/"duration") z $listbuild(třída, metoda, stav, doba).
ClassMethod ZapisTesty(ByRef testy) [ Internal ]
{
	set i = ""
	for {
		set i = $order(testy(i))
		quit:i=""
		set ^||ML("OUT","tests",i,"class") = $listget(testy(i), 1)
		set ^||ML("OUT","tests",i,"method") = $listget(testy(i), 2)
		set ^||ML("OUT","tests",i,"status") = $listget(testy(i), 3)
		set ^||ML("OUT","tests",i,"duration") = $listget(testy(i), 4)
	}
}

/// Spustí všechny třídy v jedné %UnitTest sadě a sečte výsledky, které tím vznikly.
/// Prázdná `sada` = běh přes všechny složky pod kořenem (původní, mnohonásobně dražší chování).
ClassMethod SpustSadu(classList As %List, sada As %String, ByRef nase, ByRef passed, ByRef failed, ByRef skipped, ByRef selhani, ByRef preskoceno, ByRef testy) [ Internal ]
{
	set baseIndex = +$order(^UnitTest.Result(""), -1)
	// Horní uzel userparam uloží %UnitTest do TestInstance.UserParam: značka odliší výsledky tohoto běhu.
	set znacka = "EDTesting:"_$system.Util.CreateGUID()

	// %UnitTest.Manager.RunTest píše log do current device. V MLRest contextu by
	// to skončilo v HTTP response streamu před naším OUT a polutovalo CVar parse.
	// Redirekt $io na temp soubor po dobu běhu testů; soubor po skončení smažeme.
	set tempFile = ##class(%File).TempFilename("husky")
	set origIo = $io
	open tempFile:("WNS"):3
	use tempFile

	// Spustit každou třídu jednotlivě (":ClassName" = compiled class only).
	set ptr = 0
	while $listnext(classList, ptr, className) {
		do ##class(%UnitTest.Manager).RunTest(sada_":"_className, "/noload/nodelete/display=none", znacka)
	}

	// Restore device + cleanup
	use origIo
	close tempFile
	do ##class(%File).Delete(tempFile)

	// Pojistka: kdyby RunTest přesto něco do ^||ML zapsal.
	kill ^||ML

	do ..Sesbirej(baseIndex, znacka, .nase, .passed, .failed, .skipped, .selhani, .preskoceno, .testy)
	quit
}

/// Sečte výsledky TestInstance za `baseIndex` se značkou `znacka`, jen pro třídy v `nase`.
///
/// ⚠️ **Počítají se jen výsledky se značkou tohoto běhu.** ^UnitTest.Result je sdílený pro celý
/// server a souběžně do něj píše kolegův pre-push, úloha tree-sync i jiné týmy. Bez značky si běh
/// přičetl cizí výsledky téže třídy: selhání, které není naše, a hlavně cizí zelený výsledek
/// místo vlastního, takže sada skončila zeleně, i když se skoro nic nespustilo.
/// Dedupe na (třída, metoda) drží počty při běhu přes všechny složky (výsledky téže metody víckrát).
ClassMethod Sesbirej(baseIndex As %Integer, znacka As %String, ByRef nase, ByRef passed, ByRef failed, ByRef skipped, ByRef selhani, ByRef preskoceno, ByRef testy) [ Internal ]
{
	set (failIdx, skipIdx, testIdx) = 0
	kill seen
	set idx = baseIndex
	for {
		set idx = $order(^UnitTest.Result(idx))
		quit:idx=""
		set instance = ##class(%UnitTest.Result.TestInstance).%OpenId(idx)
		if ('$isobject(instance)) { continue }
		continue:instance.UserParam'=znacka

		for s = 1:1:instance.TestSuites.Count() {
			set suite = instance.TestSuites.GetAt(s)
			for c = 1:1:suite.TestCases.Count() {
				set case = suite.TestCases.GetAt(c)
				continue:'$data(nase(case.Name))
				for m = 1:1:case.TestMethods.Count() {
					set method = case.TestMethods.GetAt(m)
					set key = case.Name_"|"_method.Name
					continue:$data(seen(key))
					set seen(key) = ""

					set vysledek = ..Vysledek(method.Status)
					set testIdx = testIdx + 1
					set testy(testIdx) = $listbuild(case.Name, method.Name, vysledek, +method.Duration)
					if (vysledek = "passed") {
						set passed = passed + 1
					} elseif (vysledek = "skipped") {
						set skipped = skipped + 1
						set skipIdx = skipIdx + 1
						set preskoceno(skipIdx) = $listbuild(case.Name, method.Name, ..AssertMessage(method, 2))
					} else {
						set failed = failed + 1
						set failIdx = failIdx + 1
						set selhani(failIdx) = $listbuild(case.Name, method.Name, ..FailureMessage(method))
					}
				}
			}
		}
	}
	quit
}

/// Vrátí jména podsložek kořene testů jako $listbuild - kandidáti na jméno %UnitTest sady.
/// Prázdný seznam = kořen podsložky nemá; volající pak spadne na běh bez jména sady.
ClassMethod Sady(root As %String) As %List [ Internal ]
{
	quit:root="" ""
	set seznam = ""
	set rs = ##class(%ResultSet).%New("%File:FileSet")
	quit:'$isobject(rs) ""
	if $$$ISERR(rs.Execute(root, "*", , 1)) { quit "" }
	while rs.Next() {
		continue:rs.Get("Type")'="D"
		set jmeno = ##class(%File).GetFilename(##class(%File).NormalizeFilename(rs.Get("Name")))
		continue:jmeno=""
		set seznam = seznam_$listbuild(jmeno)
	}
	do rs.Close()
	quit seznam
}

/// Najde všechny zkompilované %UnitTest.TestCase subclasses (direct i transitive)
/// jejichž název začíná packagePrefix. Vrací $listbuild() seznam class names.
ClassMethod FindTestClasses(packagePrefix As %String) As %List [ Internal ]
{
	set list = ""
	set rs = ##class(%SQL.Statement).%ExecDirect(
		.stmt,
		"SELECT Name FROM %Dictionary.CompiledClass WHERE Name %STARTSWITH ? ORDER BY Name",
		packagePrefix)
	if (rs.%SQLCODE < 0) { quit list }
	while rs.%Next() {
		set className = rs.%Get("Name")
		if $classmethod(className, "%Extends", "%UnitTest.TestCase") {
			set list = list_$listbuild(className)
		}
	}
	quit list
}

/// Stav TestMethod → "passed" (1) / "skipped" (2, $$$AssertSkipped) / "failed" (0 i cokoli neznámého -
/// pochybný stav nesmí pustit push). Selhaný assert i výjimka mají v %UnitTest přednost před skipem.
ClassMethod Vysledek(status As %Integer) As %String [ Internal ]
{
	quit $case(status, 1:"passed", 2:"skipped", :"failed")
}

/// Hláška selhání: první neúspěšný assert, jinak text výjimky - tu %UnitTest zapisuje do metody
/// (ErrorDescription), ne do assertu, takže by selhání výjimkou zůstalo bez hlášky.
ClassMethod FailureMessage(method As %UnitTest.Result.TestMethod) As %String [ Internal ]
{
	set message = ..AssertMessage(method, 0)
	quit $select(message'="":message, 1:method.ErrorDescription)
}

/// Description první assertion s daným stavem (0 = selhání, 2 = důvod přeskočení), jinak "".
ClassMethod AssertMessage(method As %UnitTest.Result.TestMethod, status As %Integer) As %String [ Internal ]
{
	set message = ""
	for a = 1:1:method.TestAsserts.Count() {
		set assert = method.TestAsserts.GetAt(a)
		if (assert.Status = status) { set message = assert.Description  quit }
	}
	quit message
}

}
