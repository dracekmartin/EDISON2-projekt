Include GENERAL

/// Programové spuštění %UnitTest s strukturovaným výstupem.
/// Volá se přes MLRest CVar; používá se z CI/pre-push hooks.
Class ALVA.EDTesting.TestRunner Extends %RegisteredObject
{

/// IN:  package — name nebo prefix (např. "ALVA.EDISONMAIN2.Tests")
/// OUT: passed (count), failed (count), total (count),
///      failures(i, "class"/"method"/"message") pro každý failing test
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
		set (passed, failed) = 0
		kill selhani
		set kptr = 0
		while $listnext(kandidati, kptr, sada) {
			set sada = $get(sada)
			kill selhani
			set (passed, failed) = 0
			do ..SpustSadu(classList, sada, .nase, .passed, .failed, .selhani)
			quit:(passed+failed)>0
		}

		set ^||ML("OUT","passed") = passed
		set ^||ML("OUT","failed") = failed
		set ^||ML("OUT","total") = (passed + failed)
		set fi = ""
		for {
			set fi = $order(selhani(fi))
			quit:fi=""
			set ^||ML("OUT","failures",fi,"class") = $listget(selhani(fi), 1)
			set ^||ML("OUT","failures",fi,"method") = $listget(selhani(fi), 2)
			set ^||ML("OUT","failures",fi,"message") = $listget(selhani(fi), 3)
		}
	}
	catch (ex) { $$$CatchErrRest }
	ret ret
}

/// Spustí všechny třídy v jedné %UnitTest sadě a sečte výsledky, které tím vznikly.
/// Prázdná `sada` = běh přes všechny složky pod kořenem (původní, mnohonásobně dražší chování).
ClassMethod SpustSadu(classList As %List, sada As %String, ByRef nase, ByRef passed, ByRef failed, ByRef selhani) [ Internal ]
{
	set baseIndex = +$order(^UnitTest.Result(""), -1)

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
		do ##class(%UnitTest.Manager).RunTest(sada_":"_className, "/noload/nodelete/display=none")
	}

	// Restore device + cleanup
	use origIo
	close tempFile
	do ##class(%File).Delete(tempFile)

	// Pojistka: kdyby RunTest přesto něco do ^||ML zapsal.
	kill ^||ML

	set failIdx = 0
	kill seen

	// Iteruj všechny TestInstance indexy vytvořené naším batch.
	// Dedupe na (className, methodName) - drží počty i tehdy, kdyby výsledky téže metody
	// přišly víckrát (běh přes všechny složky, nebo souběžný běh kolegy).
	//
	// ⚠️ **Počítají se jen třídy, které tenhle běh sám spustil.** ^UnitTest.Result je sdílený
	// pro celý server, takže sem spadnou i výsledky, které mezitím vyrobil někdo jiný (kolegův
	// pre-push na témže dev serveru). Bez filtru si běh cizí testy přičetl - dva běhy hlásily
	// 186/186 místo 128 - a hlavně si mohl přivlastnit i **cizí selhání**, takže se pak hledá
	// chyba, která není naše.
	//
	// Zbytkové riziko, které tím nezmizí: když někdo souběžně pustí **tutéž** třídu, filtr ho
	// neodliší. Počty to nerozhodí (dedupe na třídu+metodu), ale jeho selhání by se ukázalo
	// jako naše. Na to by bylo potřeba oddělit běhy už na úrovni %UnitTest, ne až ve výsledcích.
	set idx = baseIndex
	for {
		set idx = $order(^UnitTest.Result(idx))
		quit:idx=""
		set instance = ##class(%UnitTest.Result.TestInstance).%OpenId(idx)
		if ('$isobject(instance)) { continue }

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

					if (method.Status = 1) {
						set passed = passed + 1
					} else {
						set failed = failed + 1
						set failIdx = failIdx + 1
						set selhani(failIdx) = $listbuild(case.Name, method.Name, ..FirstFailureMessage(method))
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

/// Najde první failing assertion v TestMethod a vrátí její Description.
ClassMethod FirstFailureMessage(method As %UnitTest.Result.TestMethod) As %String [ Internal ]
{
	set assertCount = method.TestAsserts.Count()
	for a = 1:1:assertCount {
		set assert = method.TestAsserts.GetAt(a)
		if (assert.Status = 0) { quit }
	}
	if ('$data(assert) || (assert.Status '= 0)) { quit "" }
	quit assert.Description
}

}
