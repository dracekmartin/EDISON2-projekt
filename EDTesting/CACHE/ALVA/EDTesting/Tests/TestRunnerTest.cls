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
