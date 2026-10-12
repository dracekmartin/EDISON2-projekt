Include GENERAL

/// Testovací účet DEV pro integrační a E2E testy EDISON2 (C# TestAccount v EDTesting).
/// Jméno a heslo nejsou v gitu: vlastník je nastaví v terminálu Caché do globálu
/// ^EDTesting("ITAccount","login") a ("password") v namespace, které vrací OUT("namespace").
Class ALVA.EDTesting.ITAccount Extends %RegisteredObject
{

/// OUT["login"], OUT["password"] = účet (prázdné = na serveru není nastavený)
/// OUT["namespace"] = kde global leží, aby test při chybějícím účtu řekl, kam ho zapsat
ClassMethod Get(ByRef sys As %String) As %Status
{
    $$$Init
    try {
        $$$FillParams

        s ^||ML("OUT","login") = $g(^EDTesting("ITAccount","login"))
        s ^||ML("OUT","password") = $g(^EDTesting("ITAccount","password"))
        s ^||ML("OUT","namespace") = $namespace
    } catch (ex) { $$$CatchErrRest }
    ret ret
}

}
