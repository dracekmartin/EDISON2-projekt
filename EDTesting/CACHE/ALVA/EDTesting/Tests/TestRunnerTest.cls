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

/// AssertMessage bez assertu hledaného stavu vrátí prázdný text, ne chybu.
Method TestAssertMessageWithoutMatchingAssertIsEmpty()
{
	s method = ..Method(0, $lb(1, "ok"))
	do $$$AssertEquals(##class(ALVA.EDTesting.TestRunner).AssertMessage(method, 0), "")
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
