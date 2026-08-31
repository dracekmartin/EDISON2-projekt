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

		// %UnitTest.Manager vyžaduje ^UnitTestRoot na existující složku (i s /noload).
		if ($get(^UnitTestRoot) = "") { set ^UnitTestRoot = $system.Util.ManagerDirectory() }

		set classList = ..FindTestClasses(package)
		if (classList = "") { throw ##class(%Exception.General).%New("RunPackage",,,"No %UnitTest.TestCase subclasses found under "_package) }

		set baseIndex = +$order(^UnitTest.Result(""),-1)

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
			do ##class(%UnitTest.Manager).RunTest(":"_className, "/noload/nodelete/display=none")
		}

		// Restore device + cleanup
		use origIo
		close tempFile
		do ##class(%File).Delete(tempFile)

		// Pojistka: kdyby RunTest přesto něco do ^||ML zapsal.
		kill ^||ML

		set passed = 0, failed = 0, failIdx = 0
		kill seen, nase

		// Třídy, které tenhle běh sám spustil - podle nich se filtrují výsledky níže.
		set ptr = 0
		while $listnext(classList, ptr, className) { set nase(className) = "" }

		// Iteruj všechny TestInstance indexy vytvořené naším batch.
		// RunTest("ClassName") scanuje ^UnitTestRoot subdirs (Locale/MLINE/Temp),
		// takže jeden run vytvoří TestInstance s víc suites se stejným obsahem.
		// Dedupe na (className, methodName).
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
							set firstError = ..FirstFailureMessage(method)
							set failIdx = failIdx + 1
							set ^||ML("OUT","failures",failIdx,"class") = case.Name
							set ^||ML("OUT","failures",failIdx,"method") = method.Name
							set ^||ML("OUT","failures",failIdx,"message") = firstError
						}
					}
				}
			}
		}

		set ^||ML("OUT","passed") = passed
		set ^||ML("OUT","failed") = failed
		set ^||ML("OUT","total") = (passed + failed)
	}
	catch (ex) { $$$CatchErrRest }
	ret ret
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
