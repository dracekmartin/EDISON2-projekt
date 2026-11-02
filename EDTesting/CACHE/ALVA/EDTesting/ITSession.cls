Include (GENERAL, ALWASYS)

/// Stav session pro integrační testy shellu: ACTIVITY zámek (SessionLogout) a požadavek na ukončení
/// (LogoutWatcher). Pracuje jen pod testovací firmou a jen s GUIDem session, která volá.
Class ALVA.EDTesting.ITSession Extends %RegisteredObject
{

Parameter TestIdFa = "TEST_EDISON2_IT";

/// Založí ACTIVITY zámek pro firmu, IP a GUID volajícího, stejné jako čte ALVA.SYS.Login.Logout.
ClassMethod LockSet(ByRef sys As %String) As %Status
{
    $$$Init
    try {
        $$$FillParams
        s guid = ..Guard(IdFa, GUID)
        $$$ThrowOnError(##class(ALWA.SYS.Activity).LockSet(IdFa, $$$IPCache, guid))
    } catch (ex) { $$$CatchErrRest }
    ret ret
}

/// OUT["locked"] = 1, když ACTIVITY zámek volajícího existuje.
ClassMethod LockIs(ByRef sys As %String) As %Status
{
    $$$Init
    try {
        $$$FillParams
        s guid = ..Guard(IdFa, GUID)
        s ^||ML("OUT","locked") = ''##class(ALWA.SYS.Activity).LockIs(IdFa, $$$IPCache, guid)
    } catch (ex) { $$$CatchErrRest }
    ret ret
}

ClassMethod LockDel(ByRef sys As %String) As %Status
{
    $$$Init
    try {
        $$$FillParams
        s guid = ..Guard(IdFa, GUID)
        $$$ThrowOnError(##class(ALWA.SYS.Activity).LockDel(IdFa, $$$IPCache, guid))
    } catch (ex) { $$$CatchErrRest }
    ret ret
}

/// IN["sec"] = sekundy do ukončení. Zapíše požadavek jako ALVA.SYSTEM.Main.UkonciUzivatele, ale jen pro GUID
/// volajícího: UkonciUzivatele bez řádku GUID ukončí všechny shelly na serveru.
ClassMethod RequestEnd(ByRef sys As %String) As %Status
{
    $$$Init
    try {
        $$$FillParams
        s guid = ..Guard(IdFa, GUID)
        s sec = $g(^||ML("IN","sec"))
        if sec = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "sec is required"))
        s ^CacheTempEDISON("System","UkoncitEdisona",$h,guid) = $lb(userID, userName, guid, sec)
    } catch (ex) { $$$CatchErrRest }
    ret ret
}

/// Smaže požadavky na ukončení GUIDu volajícího, ostatní nechá: maSeUkoncit je prochází při každém pollu všech shellů.
ClassMethod CleanEndRequest(ByRef sys As %String) As %Status
{
    $$$Init
    try {
        $$$FillParams
        s guid = ..Guard(IdFa, GUID)
        s cas = ""
        for {
            s cas = $o(^CacheTempEDISON("System","UkoncitEdisona",cas)) q:cas=""
            k ^CacheTempEDISON("System","UkoncitEdisona",cas,guid)
        }
    } catch (ex) { $$$CatchErrRest }
    ret ret
}

/// Jiná firma nebo prázdný GUID = chyba: test nesmí sáhnout na zámky skutečné firmy ani poslat požadavek všem.
ClassMethod Guard(IdFa As %String, guid As %String) As %String [ Private ]
{
    if IdFa '= ..#TestIdFa $$$ThrowStatus($$$ERROR($$$GeneralError, "ITSession pracuje jen pod firmou "_..#TestIdFa))
    if guid = "" $$$ThrowStatus($$$ERROR($$$GeneralError, "ITSession potřebuje GUID session"))
    quit guid
}

}
