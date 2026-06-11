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

		// Najdi všechny %UnitTest.TestCase subclasses pod daným package.
		set classList = ..FindTestClasses(package)
		if (classList = "") { throw ##class(%Exception.General).%New("RunPackage",,,"No %UnitTest.TestCase subclasses found under "_package) }

		// Index před prvním spuštěním — všechno za tímhle indexem patří našemu batch.
		set baseIndex = +$order(^UnitTest.Result(""),-1)

		// Spustit každou třídu jednotlivě. /display=none je nutné, jinak log rozbije MCP response.
		// RunTest scanuje ^UnitTestRoot subdirs (Locale/MLINE/Temp) → třída se odpaluje 3x.
		// Řeším dedupem na (className, methodName) níže.
		set ptr = 0
		while $listnext(classList, ptr, className) {
			do ##class(%UnitTest.Manager).RunTest(":"_className, "/noload/nodelete/display=none")
		}

		// RunTest stále nějaký output do ^||ML pošle → killneme celé a postavíme čisté.
		kill ^||ML

		set passed = 0, failed = 0, failIdx = 0
		kill seen

		// Iteruj všechny TestInstance indexy vytvořené naším batch (baseIndex+1 .. last).
		set idx = baseIndex
		for {
			set idx = $order(^UnitTest.Result(idx))
			quit:idx=""
			set instance = ##class(%UnitTest.Result.TestInstance).%OpenId(idx)
			if ('$isobject(instance)) { continue }

			set suiteCount = instance.TestSuites.Count()
			for s = 1:1:suiteCount {
				set suite = instance.TestSuites.GetAt(s)
				set caseCount = suite.TestCases.Count()
				for c = 1:1:caseCount {
					set case = suite.TestCases.GetAt(c)
					set methodCount = case.TestMethods.Count()
					for m = 1:1:methodCount {
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
