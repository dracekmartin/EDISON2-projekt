Include %occInclude

/// Base class pro %UnitTest ALVA tříd používajících MLRest + $$$FillParams.
/// Cleanup ^||ML před každým testem + helper SetupSys pro sys array context.
Class ALVA.EDTesting.MLTestCase Extends %UnitTest.TestCase
{

/// Vyčistí process-private global před každým testem.
Method OnBeforeOneTest(testname As %String) As %Status
{
	kill ^||ML
	quit $$$OK
}

/// Naplní sys array povinnými poli pro $$$FillParams (case-sensitive subscripty per GENERAL.inc).
/// Defaulty pasují na M-line dev prostředí; přepiš parametry pro jiné firmy/uživatele.
ClassMethod SetupSys(ByRef sys, idfa As %String = "CB_DP", mlp As %String = "1", loginName As %String = "test")
{
	set sys("U","IdFa") = idfa
	set sys("U","delimiter") = "~"
	set sys("U","SystemDB") = $namespace
	set sys("U","DATA") = $namespace
	set sys("U","MLP") = mlp
	set sys("U","loginName") = loginName
	set sys("U","fullName") = "Test User"
}

}
