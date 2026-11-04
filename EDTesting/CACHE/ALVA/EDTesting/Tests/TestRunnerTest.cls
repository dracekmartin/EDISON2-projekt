Include %occInclude

Class ALVA.EDTesting.Tests.TestRunnerTest Extends ALVA.EDTesting.MLTestCase
{

/// Přeskočený test ($$$AssertSkipped) nese důvod v assertu se stavem 2 - runner ho dřív hledal jen
/// mezi selháními (stav 0), takže hlásil FAIL s prázdnou hláškou.
Method TestAssertMessageReturnsSkipReason()
{
	s method = ..Method(2, $lb(1, "ok"), $lb(2, "ISYCloud není na serveru"))
	do $$$AssertEquals(##class(ALVA.EDTesting.TestRunner).AssertMessage(method, 2), "ISYCloud není na serveru")
	do $$$AssertEquals(##class(ALVA.EDTesting.TestRunner).AssertMessage(method, 0), "", "přeskočený test nemá selhání")
}

/// Selhání: první assert se stavem 0, ne pozdější.
Method TestAssertMessageReturnsFirstFailure()
{
	s method = ..Method(0, $lb(1, "ok"), $lb(0, "první chyba"), $lb(0, "druhá chyba"))
	do $$$AssertEquals(##class(ALVA.EDTesting.TestRunner).AssertMessage(method, 0), "první chyba")
}

/// Přeskočení push neblokuje; selhání a neznámý stav ano (pochybný stav nesmí pustit rozbitý push).
Method TestVysledekByStatus()
{
	do $$$AssertEquals(##class(ALVA.EDTesting.TestRunner).Vysledek(1), "passed")
	do $$$AssertEquals(##class(ALVA.EDTesting.TestRunner).Vysledek(2), "skipped")
	do $$$AssertEquals(##class(ALVA.EDTesting.TestRunner).Vysledek(0), "failed")
	do $$$AssertEquals(##class(ALVA.EDTesting.TestRunner).Vysledek(""), "failed")
}

/// Selhání výjimkou nemá neúspěšný assert - hláška je text výjimky z metody.
Method TestFailureMessageFallsBackToErrorDescription()
{
	s method = ..Method(0, $lb(1, "ok"))
	s method.ErrorDescription = "<DIVIDE>TestThrows+1^Fixture"
	do $$$AssertEquals(##class(ALVA.EDTesting.TestRunner).FailureMessage(method), "<DIVIDE>TestThrows+1^Fixture")

	s method = ..Method(0, $lb(0, "assert selhal"))
	s method.ErrorDescription = "jiná chyba"
	do $$$AssertEquals(##class(ALVA.EDTesting.TestRunner).FailureMessage(method), "assert selhal", "neúspěšný assert má přednost")
}

/// AssertMessage bez assertu hledaného stavu vrátí prázdný text, ne chybu.
Method TestAssertMessageWithoutMatchingAssertIsEmpty()
{
	s method = ..Method(0, $lb(1, "ok"))
	do $$$AssertEquals(##class(ALVA.EDTesting.TestRunner).AssertMessage(method, 0), "")
}

/// Doba každé metody jde na wire, aby klient mohl hlásit nejpomalejší testy a vést jejich historii.
Method TestZapisTestyWritesEveryMethodWithDuration()
{
	kill ^||ML
	set testy(1) = $lb("A.Test", "TestJedna", "passed", 0.25)
	set testy(2) = $lb("A.Test", "TestDva", "failed", 12.5)
	do ##class(ALVA.EDTesting.TestRunner).ZapisTesty(.testy)
	do $$$AssertEquals($g(^||ML("OUT","tests",1,"method")), "TestJedna")
	do $$$AssertEquals($g(^||ML("OUT","tests",1,"duration")), 0.25)
	do $$$AssertEquals($g(^||ML("OUT","tests",2,"class")), "A.Test")
	do $$$AssertEquals($g(^||ML("OUT","tests",2,"status")), "failed")
	do $$$AssertEquals($g(^||ML("OUT","tests",2,"duration")), 12.5)
	do $$$AssertEquals($o(^||ML("OUT","tests",2)), "", "jen zapsané metody")
	kill ^||ML
}

/// Souběžný běh téže třídy jinde (kolega, tree-sync) zapíše do sdíleného ^UnitTest.Result vlastní výsledek.
/// Runner ho dřív přičetl: cizí zelený výsledek nahradil vlastní a sada skončila zeleně bez spuštění.
Method TestSesbirejCountsOnlyResultsWithOwnTag()
{
	set trida = "ALVA.EDTesting.Tests.Fiktivni"
	set nase(trida) = ""
	set base = +$order(^UnitTest.Result(""), -1)
	set (cizi, vlastni) = ""
	try {
		set cizi = ..UlozInstanci("cizi-"_$job, trida, "TestCizi", 0)
		set vlastni = ..UlozInstanci("nase-"_$job, trida, "TestNase", 1)
		set (passed, failed, skipped) = 0
		do ##class(ALVA.EDTesting.TestRunner).Sesbirej(base, "nase-"_$job, .nase, .passed, .failed, .skipped, .selhani, .preskoceno, .testy)
		do $$$AssertEquals(passed, 1, "vlastní výsledek se počítá")
		do $$$AssertEquals(failed, 0, "cizí selhání téže třídy se nepočítá")
		do $$$AssertEquals($lg($g(testy(1)), 2), "TestNase")
		do $$$AssertEquals($d(testy(2)), 0, "jen metody vlastního běhu")
	}
	catch ex { do $$$AssertFailure(ex.DisplayString()) }
	do:cizi'="" ##class(%UnitTest.Result.TestInstance).%DeleteId(cizi)
	do:vlastni'="" ##class(%UnitTest.Result.TestInstance).%DeleteId(vlastni)
}

/// Uloží do ^UnitTest.Result instanci s jednou metodou, jako ji zapíše %UnitTest.Manager; vrátí její index.
ClassMethod UlozInstanci(znacka As %String, trida As %String, metoda As %String, stav As %Integer) As %Integer [ Private ]
{
	set instance = ##class(%UnitTest.Result.TestInstance).%New()
	set instance.InstanceIndex = $increment(^UnitTest.Result)
	set instance.UserParam = znacka
	set suite = ##class(%UnitTest.Result.TestSuite).%New()
	set suite.Name = "Fiktivni"
	set case = ##class(%UnitTest.Result.TestCase).%New()
	set case.Name = trida
	set method = ##class(%UnitTest.Result.TestMethod).%New()
	set method.Name = metoda
	set method.Status = stav
	do case.TestMethods.Insert(method)
	do suite.TestCases.Insert(case)
	do instance.TestSuites.Insert(suite)
	$$$ThrowOnError(instance.%Save())
	quit instance.InstanceIndex
}

/// TestMethod s asserty v paměti (neukládá se), každý assert = $lb(stav, popis).
ClassMethod Method(status As %Integer, asserts... As %List) As %UnitTest.Result.TestMethod [ Private ]
{
	s method = ##class(%UnitTest.Result.TestMethod).%New()
	s method.Status = status
	f i=1:1:$g(asserts) {
		s assert = ##class(%UnitTest.Result.TestAssert).%New()
		s assert.Status = $li(asserts(i), 1)
		s assert.Description = $li(asserts(i), 2)
		d method.TestAsserts.Insert(assert)
	}
	q method
}

}
