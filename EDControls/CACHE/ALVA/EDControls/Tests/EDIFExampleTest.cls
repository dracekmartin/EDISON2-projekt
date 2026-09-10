Include %occInclude

/// Smoke test referenční implementace EDInputForm featur (EDIFExample) - CLAUDE.md
/// nařizuje rozšiřovat ji s každou novou featurou, takže rozbití wire formátu se
/// projeví nejdřív tady. Chyba je v návratové hodnotě (ret="1~..."), ne v ^||ML("ERR").
Class ALVA.EDControls.Tests.EDIFExampleTest Extends ALVA.EDTesting.MLTestCase
{

/// Init -> Validate -> Save projdou bez chyby a Init naplní ^||ML("OUT") položkami.
Method TestInitValidateSaveSmoke()
{
	do ..SetupSys(.sys)
	set ret = ##class(ALVA.EDControls.EDInputForm.EDIFExample).Init(.sys)
	do $$$AssertEquals(+ret, 0, "Init nesmí vrátit chybu, got: "_ret)
	do $$$AssertNotTrue(($data(^||ML("OUT",1))=0), "Init musí naplnit OUT položkami formuláře")

	// Simulace .NET round-tripu: OUT zpět jako IN + trigger.
	set idx = ""
	for { set idx = $order(^||ML("OUT", idx)) quit:idx=""  set ^||ML("IN", idx) = ^||ML("OUT", idx) }
	kill ^||ML("OUT")
	set ^||ML("IN","trigger") = "Init"
	set ret = ##class(ALVA.EDControls.EDInputForm.EDIFExample).Validate(.sys)
	do $$$AssertEquals(+ret, 0, "Validate nesmí vrátit chybu, got: "_ret)
	do $$$AssertNotTrue(($data(^||ML("OUT",1))=0), "Validate musí vrátit formulář v OUT")

	set idx = ""
	kill ^||ML("IN")
	for { set idx = $order(^||ML("OUT", idx)) quit:idx=""  set ^||ML("IN", idx) = ^||ML("OUT", idx) }
	kill ^||ML("OUT")
	set ^||ML("IN","trigger") = "BeforeSave"
	set ret = ##class(ALVA.EDControls.EDInputForm.EDIFExample).Save(.sys)
	do $$$AssertEquals(+ret, 0, "Save nesmí vrátit chybu, got: "_ret)
}

}
